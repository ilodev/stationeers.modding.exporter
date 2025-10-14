using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    public class LaunchPadExport
    {
        public static string exportFolder;
        public static string tempFolder;

        static string Sanitize(string part)
        {
            // Replace invalid chars with underscores
            string clean = Regex.Replace(part, @"[^A-Za-z0-9_]", "_");
            // Remove leading digits and underscores
            clean = Regex.Replace(clean, @"^[^A-Za-z_]+", "");
            // PascalCase words separated by underscores
            clean = string.Join("", clean
                .Split(new[] { '_', ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower()));
            // Fallback if it became empty
            return string.IsNullOrEmpty(clean) ? "Unnamed" : clean;
        }

        static string GetExportFolder(BuildPlayerOptions opts)
        {
            bool isFileOutput =
                opts.target == BuildTarget.StandaloneWindows ||
                opts.target == BuildTarget.StandaloneWindows64 ||
                opts.target == BuildTarget.StandaloneLinux64 ||
                opts.target == BuildTarget.Android ||        // .apk or .aab
                opts.target == BuildTarget.WSAPlayer;        // .appx/.msix

            if (isFileOutput)
                return Path.GetDirectoryName(opts.locationPathName);

            // macOS builds are .app bundles (folders). WebGL/iOS are folders too.
            return opts.locationPathName;
        }

        private static void DeleteTempFolder(string folder)
        {
            Debug.Log($"Deleting build directory: {folder}");
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        private static void CreateTempFolder(string folder)
        {
            Debug.Log($"Creating build directory: {folder}");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private static void ExportAssemblies(BuildPlayerOptions options)
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
            string gamedata = Path.Combine("Assets", "GameData");
            string about = Path.Combine("Assets", "About");
            var dir = new DirectoryInfo(gamedata);
            if (dir.Exists)
            {
                CopyDirectory(gamedata, Path.Combine(tempFolder, "GameData"), true);
            }
            dir = new DirectoryInfo(about);
            if (dir.Exists)
            {
                CopyDirectory(about, Path.Combine(tempFolder, "About"), true);
            }
        }

        public static void SetAssetBundle(string assetPath, string variant = "assets")
        {
            //Debug.Log($"PATH: {assetPath}");
            var importer = AssetImporter.GetAtPath(assetPath);
            importer.assetBundleName = Sanitize(PlayerSettings.productName);
            importer.assetBundleVariant = variant;
        }

        private static void ExportAssetBundles(BuildPlayerOptions options)
        {
            Debug.Log("Export AssetBundles");
            
            List<string> assetPaths = AssetUtility.GetAssets("t:prefab t:scriptableobject");
            assetPaths.ForEach(s => SetAssetBundle(s));
            Debug.Log($"- Total Asset count {assetPaths.Count}");

            //scenePaths.ForEach(s => SetAssetBundle(s, "scenes"));
            // Debug.Log($"- Total Scene count {scenePaths.Count}");

            // Forcing building platform as standalone windows for now.
            var platform = BuildTarget.StandaloneWindows.ToString();
            var subDir = Path.Combine(tempFolder, platform);
            Directory.CreateDirectory(subDir);
            Debug.Log($"Exporting assets for {platform} to: {subDir}");
            BuildPipeline.BuildAssetBundles(subDir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
            
        }

        public static void Export(BuildPlayerOptions options)
        {
            exportFolder = GetExportFolder(options);
            //tempFolder = Path.Combine(exportFolder, "Temp", PlayerSettings.productName);
            tempFolder = Path.Combine(exportFolder, Sanitize(PlayerSettings.productName));

            if ((options.options & BuildOptions.CleanBuildCache) != 0)
                DeleteTempFolder(tempFolder);

            CreateTempFolder(tempFolder);

            ExportAssemblies(options);

            if ((options.options & BuildOptions.BuildScriptsOnly) == 0)
                ExportAssets(options);

            if ((options.options & BuildOptions.BuildScriptsOnly) == 0)
                ExportAssetBundles(options);


            Debug.Log("Export complete");
        }
    }
}
