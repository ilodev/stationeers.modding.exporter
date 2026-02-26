using stationeers.modding.exporter.stationeers.modding.exporter;
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

        private static int ExportAssemblies(BuildPlayerOptions options, StationeersExportManifest manifest)
        {
            List<string> _candidatesCache = AssetUtility.GetAssets("t:AssemblyDefinitionAsset").ToList();

            Debug.Log($"Exporting {_candidatesCache.Count} Assemblies");

            foreach (var asmDefPath in _candidatesCache)
            {
                var json = File.ReadAllText(asmDefPath);
                var asmDef = JsonUtility.FromJson<AsmDef>(json);

                var modAsmPath = Path.Combine("Library", "ScriptAssemblies", $"{asmDef.name}.dll");
                bool overwrite = true;

                if (File.Exists(modAsmPath))
                {
                    File.Copy(modAsmPath, Path.Combine(exportFolder, $"{asmDef.name}.dll"), overwrite);
                    manifest.assembliesCopied.Add($"{asmDef.name}.dll");
                }
                else
                {
                    manifest.warnings.Add($"Missing DLL: {modAsmPath}");
                }

                if (UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles == true)
                {
                    var modPdbPath = Path.Combine("Library", "ScriptAssemblies", $"{asmDef.name}.pdb");
                    if (File.Exists(modPdbPath))
                    {
                        File.Copy(modPdbPath, Path.Combine(exportFolder, $"{asmDef.name}.pdb"), overwrite);
                        manifest.pdbsCopied.Add($"{asmDef.name}.pdb");
                    }
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

        /// <summary>
        /// Export additional folders from Assets/
        /// </summary>
        /// <param name="options"></param>
        private static int ExportAssets(BuildPlayerOptions options, StationeersExportManifest manifest)
        {
            int exportedFolders = 0;

            var settings = StationeersExporterSettings.instance;
            var folders = (settings != null && settings.exportFolders != null)
                ? settings.exportFolders
                : new List<string> { "Assets/GameData", "Assets/About" };

            foreach (var folder in folders)
            {
                if (string.IsNullOrEmpty(folder))
                    continue;

                var normalized = folder.Replace('\\', '/');
                if (!normalized.StartsWith("Assets/"))
                {
                    manifest.warnings.Add($"Skipping export folder (must be under Assets/): {folder}");
                    continue;
                }

                if (!Directory.Exists(normalized))
                {
                    manifest.warnings.Add($"Export folder not found: {folder}");
                    continue;
                }

                var relUnderAssets = normalized.Substring("Assets/".Length);
                var dest = Path.Combine(exportFolder, relUnderAssets);
                CopyDirectory(normalized, dest, true);

                manifest.foldersCopied.Add(normalized);
                exportedFolders++;
            }

            return exportedFolders;
        }

        /// <summary>
        /// Assign an assetbundle to an assetpath
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="variant"></param>
        public static void SetAssetBundle(string assetPath, string variant = "assets")
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            importer.assetBundleName = Sanitize(PlayerSettings.productName);
            importer.assetBundleVariant = variant;
        }

        /// <summary>
        /// Removes the assetbundle from an assetpath
        /// </summary>
        /// <param name="assetPath"></param>
        public static void ResetAssetBundle(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            importer.SetAssetBundleNameAndVariant(null, null);
        }

        static List<string> SyncSceneAssetBundles(string bundleName)
        {
            // Scenes selected for build
            var buildScenes = new HashSet<string>(
                EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .Where(p => !string.IsNullOrEmpty(p))
            );

            // All scenes currently assigned to this bundle
            var assignedScenes = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName)
                .Where(p => p.EndsWith(".unity"))
                .ToList();

            // 1) Remove bundle from scenes no longer in build list
            foreach (var path in assignedScenes)
            {
                if (!buildScenes.Contains(path))
                {
                    AssetImporter.GetAtPath(path).SetAssetBundleNameAndVariant(null, null);
                    Debug.Log($"- Removed scene from bundle: {path}");
                }
            }

            // 2) Assign bundle to current build scenes
            foreach (var path in buildScenes)
            {
                SetAssetBundle(path, bundleName);
            }

            Debug.Log($"- Build scenes synced. Active: {buildScenes.Count}, Removed: {assignedScenes.Count - buildScenes.Count}");
            return buildScenes.ToList();
        }

        private static int ExportAssetBundles(BuildPlayerOptions options, StationeersExportManifest manifest)
        {
            Debug.Log("Export AssetBundles");

            List<string> assetPaths = AssetUtility.GetAssets("t:prefab t:scriptableobject");
            assetPaths.ForEach(s => SetAssetBundle(s));
            manifest.assetPathsBundled.AddRange(assetPaths);

            List<string> scenePaths = SyncSceneAssetBundles("scenes");
            scenePaths = scenePaths.Where(System.IO.File.Exists).ToList();
            manifest.scenePathsBundled.AddRange(scenePaths);

            var platform = BuildTarget.StandaloneWindows.ToString();
            var subDir = Path.Combine(exportFolder, platform);
            Directory.CreateDirectory(subDir);
            BuildPipeline.BuildAssetBundles(subDir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);

            return assetPaths.Count;
        }

        private static bool CleanBuildCacheIsSet(BuildPlayerOptions options)
        {
            return (options.options & BuildOptions.CleanBuildCache) != 0;
        }

        private static bool BuildScriptsOnlyIsSet(BuildPlayerOptions options)
        {
            return (options.options & BuildOptions.BuildScriptsOnly) != 0;
        }

        /// <summary>
        /// Export process
        /// </summary>
        /// <param name="options"></param>
        public static void Export(BuildPlayerOptions options)
        {
            Debug.Log("Export started");

            exportFolder = options.locationPathName;

            var manifest = new StationeersExportManifest
            {
                unityVersion = Application.unityVersion,
                productName = PlayerSettings.productName,
                exportFolder = exportFolder,
                buildTarget = options.target.ToString(),
                buildOptions = options.options.ToString(),
                utcTimestamp = DateTime.UtcNow.ToString("o")
            };

            if (CleanBuildCacheIsSet(options))
                DeleteOutputFolder(exportFolder);

            CreateOutputFolder(exportFolder);

            int assemblies = ExportAssemblies(options, manifest);

            int assets = 0;
            int folderAssets = 0;
            if (!BuildScriptsOnlyIsSet(options))
            {
                folderAssets = ExportAssets(options, manifest);
                assets = ExportAssetBundles(options, manifest);
            }

            manifest.assembliesCount = assemblies;
            manifest.assetsCount = assets;
            manifest.folderCount = folderAssets;
            manifest.scenesCount = manifest.scenePathsBundled.Count;

            StationeersExportManifestStore.Save(manifest);

            var path = Path.GetFullPath(StationeersExportManifestStore.ManifestPath)
               .Replace('\\', '/'); // important for Windows
            var uri = new Uri(path); // automatically file:/// and escaped
            Debug.Log($"Export complete: {assemblies} Assemblies, {assets} Assets, {folderAssets} Folders. See  <a href=\"{uri.AbsoluteUri}\">Report</a>");
        }
    }
}
