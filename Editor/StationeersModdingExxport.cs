using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace stationeers.modding.exporter
{
    public static class StationeersModdingExport
    {
        public static string exportFolder;
        public static string tempFolder;

        public static string Sanitize(string part)
        {
            if (string.IsNullOrEmpty(part))
                return "Unnamed";

            // Replace invalid chars with underscores
            string clean = Regex.Replace(part, @"[^A-Za-z0-9_]", "_");

            // Remove leading chars until a letter or underscore
            clean = Regex.Replace(clean, @"^[^A-Za-z_]+", "");

            // Split on underscores/spaces, preserve inner casing
            clean = string.Concat(
                clean
                    .Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w =>
                    {
                        if (w.Length == 0) return "";
                        if (w.Length == 1) return w.ToUpper();
                        return char.ToUpper(w[0]) + w.Substring(1);
                    })
            );

            return string.IsNullOrEmpty(clean) ? "Unnamed" : clean;
        }

        private static void DeleteOutputFolder(string folder)
        {
            Debug.Log($"Deleting build directory: {folder}");
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        private static void CreateOutputFolder(string folder)
        {
            Debug.Log($"Creating build directory: {folder}");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private static int ExportAssemblies(BuildPlayerOptions options)
        {
            // Candidates list already excludes package and editor asmdef files.
            List<string> _candidatesCache = AssetUtility.GetAssets("t:AssemblyDefinitionAsset").ToList();

            Debug.Log($"Exporting {_candidatesCache.Count} Assemblies");

            foreach (var asmDefPath in _candidatesCache)
            {
                Debug.Log($"Exporting Assembly {asmDefPath}");

                var json = File.ReadAllText(asmDefPath);
                var asmDef = JsonUtility.FromJson<AsmDef>(json);

                var modAsmPath = Path.Combine("Library", "ScriptAssemblies", $"{asmDef.name}.dll");

                // When copying dlls/pdb, always overwrite the previous version.
                bool overwrite = true;

                if (File.Exists(modAsmPath))
                {
                    Debug.Log($" + Copying {modAsmPath}");
                    File.Copy(modAsmPath, Path.Combine(tempFolder, $"{asmDef.name}.dll"), overwrite);
                }

                if (UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles == true)
                {
                    var modPdbPath = Path.Combine("Library", "ScriptAssemblies", $"{asmDef.name}.pdb");
                    Debug.Log($" + Copying {modPdbPath}");
                    if (File.Exists(modPdbPath))
                        File.Copy(modPdbPath, Path.Combine(tempFolder, $"{asmDef.name}.pdb"), overwrite);
                }
            }

            return _candidatesCache.Count;
        }


        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                if (!file.FullName.EndsWith("meta"))
                {
                    file.CopyTo(targetFilePath, true);
                }
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        private static void ExportAssets(BuildPlayerOptions options)
        {
            Debug.Log("Exporting copy assets...");

            var settings = StationeersExporterSettings.instance;
            var folders = (settings != null && settings.exportFolders != null)
                ? settings.exportFolders
                : new List<string> { "Assets/GameData", "Assets/About" };

            foreach (var folder in folders)
            {
                if (string.IsNullOrEmpty(folder))
                    continue;

                // Only support Assets/* folders
                var normalized = folder.Replace('\\', '/');
                if (!normalized.StartsWith("Assets/"))
                {
                    Debug.LogWarning($"Skipping export folder (must be under Assets/): {folder}");
                    continue;
                }

                var abs = Path.GetFullPath(normalized);
                if (!Directory.Exists(abs))
                {
                    // Also try relative to project root without Path.GetFullPath quirks
                    if (!Directory.Exists(normalized))
                    {
                        Debug.LogWarning($"Export folder not found: {folder}");
                        continue;
                    }
                }

                // Preserve relative structure under Assets/
                var relUnderAssets = normalized.Substring("Assets/".Length);
                var dest = Path.Combine(tempFolder, relUnderAssets);
                CopyDirectory(normalized, dest, true);
            }
        }

        public static void SetAssetBundle(string assetPath, string variant = "assets")
        {
            //Debug.Log($"PATH: {assetPath}");
            var importer = AssetImporter.GetAtPath(assetPath);
            importer.assetBundleName = Sanitize(PlayerSettings.productName);
            importer.assetBundleVariant = variant;
        }

        private static int ExportAssetBundles(BuildPlayerOptions options)
        {
            Debug.Log("Export AssetBundles");

            List<string> assetPaths = AssetUtility.GetAssets("t:prefab t:scriptableobject");
            assetPaths.ForEach(s => SetAssetBundle(s));
            Debug.Log($"- Total Asset count {assetPaths.Count}");

            // Adding all scenes for now, maybe later we can use the scene build editor settings
            List<string> scenePaths = AssetUtility.GetAssets("t:scene");
            scenePaths.ForEach(s => SetAssetBundle(s, "scenes"));
            Debug.Log($"- Total Scene count {scenePaths.Count}");

            // Forcing building platform as standalone windows for now.
            var platform = BuildTarget.StandaloneWindows.ToString();
            var subDir = Path.Combine(tempFolder, platform);
            Directory.CreateDirectory(subDir);
            Debug.Log($"Exporting assets for {platform} to: {subDir}");
            BuildPipeline.BuildAssetBundles(subDir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);

            return assetPaths.Count;
        }

        public static void Export(BuildPlayerOptions options)
        {
            Debug.Log("Export started");
            int assemblies = 0;
            int bundles = 0;

            tempFolder = options.locationPathName;
            Debug.Log("********** Export folder" + tempFolder);

            if ((options.options & BuildOptions.CleanBuildCache) != 0)
                DeleteOutputFolder(tempFolder);

            CreateOutputFolder(tempFolder);

            assemblies = ExportAssemblies(options);

            if ((options.options & BuildOptions.BuildScriptsOnly) == 0)
                ExportAssets(options);

            // TODO: Consider cleaning up the bundles first.
            if ((options.options & BuildOptions.BuildScriptsOnly) == 0)
                bundles = ExportAssetBundles(options);

            Debug.Log($"Export complete: {assemblies} Assemblies, {bundles} Assets.");
        }
    }
}
