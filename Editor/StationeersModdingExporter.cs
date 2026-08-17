using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

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
#if DEVELOPMENT_BUILD
            Debug.Log($"Deleting build directory: {folder}");
#endif
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        private static void CreateOutputFolder(string folder)
        {
#if DEVELOPMENT_BUILD
            Debug.Log($"Creating build directory: {folder}");
#endif
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private static int ExportAssemblies(BuildPlayerOptions options, StationeersExportManifest manifest)
        {
            // Find all asmdef assets in the project (excluding Editor/Packages as defined by AssetUtility).
            List<string> asmdefAssetPaths = AssetUtility.GetAssets("t:AssemblyDefinitionAsset");

#if DEVELOPMENT_BUILD
            Debug.Log($"Exporting {asmdefAssetPaths.Count} assemblies");
#endif

            // Validate export folder early.
            if (string.IsNullOrWhiteSpace(exportFolder))
                throw new InvalidOperationException("exportFolder is null or empty.");

            Directory.CreateDirectory(exportFolder);

            // Library/ScriptAssemblies is where Unity writes compiled managed assemblies in the editor.
            string scriptAssembliesDir = Path.Combine("Library", "ScriptAssemblies");

            bool copyPdb = UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles;
            const bool overwrite = true;

            int processed = 0;

            foreach (string asmdefAssetPath in asmdefAssetPaths)
            {
                if (string.IsNullOrEmpty(asmdefAssetPath))
                    continue;

                try
                {
                    // Load the AssemblyDefinitionAsset to get the assembly name without parsing JSON manually.
                    var asmdefAsset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefAssetPath);
                    if (asmdefAsset == null)
                    {
                        manifest.warnings.Add($"Could not load asmdef asset: {asmdefAssetPath}");
                        continue;
                    }

                    string assemblyName = asmdefAsset.name;
                    if (string.IsNullOrWhiteSpace(assemblyName))
                    {
                        manifest.warnings.Add($"Asmdef has no valid name: {asmdefAssetPath}");
                        continue;
                    }

                    // Copy DLL
                    string dllFileName = assemblyName + ".dll";
                    string dllSourcePath = Path.Combine(scriptAssembliesDir, dllFileName);
                    string dllDestPath = Path.Combine(exportFolder, dllFileName);

                    if (File.Exists(dllSourcePath))
                    {
                        File.Copy(dllSourcePath, dllDestPath, overwrite);
                        manifest.assembliesCopied.Add(dllFileName);
                    }
                    else
                    {
                        manifest.warnings.Add($"Missing DLL: {dllSourcePath} (asmdef: {asmdefAssetPath})");
                    }

                    // Copy PDB (optional)
                    if (copyPdb)
                    {
                        string pdbFileName = assemblyName + ".pdb";
                        string pdbSourcePath = Path.Combine(scriptAssembliesDir, pdbFileName);
                        string pdbDestPath = Path.Combine(exportFolder, pdbFileName);

                        if (File.Exists(pdbSourcePath))
                        {
                            File.Copy(pdbSourcePath, pdbDestPath, overwrite);
                            manifest.pdbsCopied.Add(pdbFileName);
                        }
                        else
                        {
                            manifest.warnings.Add($"Missing PDB: {pdbSourcePath} (asmdef: {asmdefAssetPath})");
                        }
                    }

                    processed++;
                }
                catch (Exception ex)
                {
                    manifest.warnings.Add($"Failed exporting asmdef '{asmdefAssetPath}': {ex.Message}");
                    Debug.Log($"Failed exporting asmdef '{asmdefAssetPath}': {ex.Message}");
                }
            }

            return processed;
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
#if DEVELOPMENT_BUILD
                    Debug.Log($"- Removed scene from bundle: {path}");
#endif
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

        private static int ExportAssetBundles(
            BuildPlayerOptions options,
            StationeersExportManifest manifest)
        {
            Debug.Log("Exporting package");

            List<string> assetPaths =
                AssetUtility.GetAssets("t:prefab t:scriptableobject");

            assetPaths.ForEach(s => SetAssetBundle(s));
            manifest.assetPathsBundled.AddRange(assetPaths);

            List<string> scenePaths =
                SyncSceneAssetBundles("scenes");

            scenePaths =
                scenePaths
                    .Where(System.IO.File.Exists)
                    .ToList();

            manifest.scenePathsBundled.AddRange(scenePaths);

            var platform =
                BuildTarget.StandaloneWindows.ToString();

            var subDir =
                Path.Combine(
                    exportFolder,
                    platform);

            Directory.CreateDirectory(subDir);

            string bundleName =
                $"{Sanitize(PlayerSettings.productName)}.assets";

            string assetsBundlePath =
                Path.Combine(
                    subDir,
                    bundleName);

            IReadOnlyList<ResolvedAssetReferencePatch> patches =
                Array.Empty<ResolvedAssetReferencePatch>();

            IReadOnlyList<ResolvedMonoScriptReferencePatch> monoScriptPatches =
                Array.Empty<ResolvedMonoScriptReferencePatch>();

            bool patchingEnabled =
                StationeersExporterSettings
                    .instance
                    .enableAssetReferencePatching;

            bool monoScriptPatchingEnabled =
                StationeersExporterSettings
                    .instance
                    .enableMonoScriptReferencePatching;

            manifest.assetReferencePatching.enabled =
                patchingEnabled;

            manifest.monoScriptReferencePatching.enabled =
                monoScriptPatchingEnabled;

            // ------------------------------------------------------------
            // Collect asset-reference patch mappings
            // ------------------------------------------------------------

            if (patchingEnabled)
            {
                var collectedPatches =
                    AssetReferencePatchRegistry.Collect();

                patches =
                    AssetReferencePatchRegistry.Resolve(
                        collectedPatches);

                manifest.assetReferencePatching.mappingCount =
                    patches.Count;

                foreach (var patch in patches)
                {
                    manifest.assetReferencePatching.mappings.Add(
                        new AssetReferencePatchManifestEntry
                        {
                            proxyAssetPath =
                                patch.ProxyAssetPath,

                            targetSerializedFile =
                                patch.Source.TargetSerializedFile,

                            targetPathId =
                                patch.Source.TargetPathId,

                            targetTypeId =
                                patch.Source.TargetTypeId,

                            cleanup =
                                patch.Source.Cleanup.ToString()
                        });
                }

#if DEVELOPMENT_BUILD
                Debug.Log(
                    $"Asset reference patching: " +
                    $"{patches.Count} mapping(s).");
#endif
            }

            // ------------------------------------------------------------
            // Collect MonoScript-reference patch mappings
            // ------------------------------------------------------------

            if (monoScriptPatchingEnabled)
            {
                var collectedMonoScriptPatches =
                    MonoScriptReferencePatchRegistry.Collect();

                monoScriptPatches =
                    MonoScriptReferencePatchRegistry.Resolve(
                        collectedMonoScriptPatches);

                manifest.monoScriptReferencePatching.mappingCount =
                    monoScriptPatches.Count;

                foreach (var patch in monoScriptPatches)
                {
                    manifest.monoScriptReferencePatching.mappings.Add(
                        new MonoScriptReferencePatchManifestEntry
                        {
                            proxyAssembly = patch.ProxyAssemblyName,
                            proxyNamespace = patch.ProxyNamespace,
                            proxyClass = patch.ProxyClassName,
                            targetAssembly =
                                patch.Source.TargetAssemblyName,
                            targetNamespace =
                                patch.Source.TargetNamespace,
                            targetClass =
                                patch.Source.TargetClassName,
                            targetSerializedFile =
                                patch.Source.TargetSerializedFile,
                            targetPathId =
                                patch.Source.TargetPathId
                        });
                }

#if DEVELOPMENT_BUILD
                Debug.Log(
                    $"MonoScript reference patching: " +
                    $"{monoScriptPatches.Count} mapping(s).");
#endif
            }

            bool hasAssetReferencePatches =
                patchingEnabled && patches.Count > 0;

            bool hasMonoScriptReferencePatches =
                monoScriptPatchingEnabled && monoScriptPatches.Count > 0;

            bool hasBundlePostProcessing =
                hasAssetReferencePatches ||
                hasMonoScriptReferencePatches;

            // ------------------------------------------------------------
            // Build AssetBundles
            // ------------------------------------------------------------

            AssetBundleManifest abManifest = null;

            if (hasBundlePostProcessing)
            {
                // Restore Unity's pristine output before invoking the
                // incremental AssetBundle build. This is shared by both
                // ordinary asset-reference patching and MonoScript patching
                // so Unity never sees an already post-processed bundle as
                // its incremental-build input.
                //
                // If no pristine cache exists, force exactly one rebuild.
                bool restoredPristine =
                    AssetReferencePatchCache
                        .RestorePristineBundle(
                            assetsBundlePath,
                            platform);

                var buildOptions =
                    restoredPristine
                        ? BuildAssetBundleOptions.None
                        : BuildAssetBundleOptions
                            .ForceRebuildAssetBundle;

                if (hasAssetReferencePatches)
                {
                    // Registered ordinary proxy assets are explicit roots
                    // so their actual bundle PathIDs can be read from
                    // AssetBundle.m_Container. MonoScript proxies do not
                    // need this: they are serialized through the prefabs or
                    // ScriptableObjects that use their components.
                    var buildMap =
                        AssetReferencePatchBuildMap.Create(
                            Sanitize(PlayerSettings.productName),
                            assetPaths,
                            scenePaths,
                            patches);

                    abManifest =
                        BuildPipeline.BuildAssetBundles(
                            subDir,
                            buildMap,
                            buildOptions,
                            BuildTarget.StandaloneWindows);
                }
                else
                {
                    abManifest =
                        BuildPipeline.BuildAssetBundles(
                            subDir,
                            buildOptions,
                            BuildTarget.StandaloneWindows);
                }
            }
            else
            {
                abManifest =
                    BuildPipeline.BuildAssetBundles(
                        subDir,
                        BuildAssetBundleOptions.None,
                        BuildTarget.StandaloneWindows);
            }

            if (abManifest == null)
            {
                throw new InvalidOperationException(
                    "AssetBundle build returned no manifest; no AssetBundle was built.");
            }

            // ------------------------------------------------------------
            // Post-process assets bundle
            // ------------------------------------------------------------

            if (hasBundlePostProcessing)
            {
                // Use Unity's own content hash rather than hashing the
                // complete bundle file ourselves.
                string unityBundleHash =
                    abManifest
                        .GetAssetBundleHash(bundleName)
                        .ToString();

                // IMPORTANT:
                // Save this BEFORE any post-processing. This is Unity's
                // pristine output and is restored before the next incremental
                // build.
                AssetReferencePatchCache.SavePristineBundleIfChanged(
                    assetsBundlePath,
                    platform,
                    unityBundleHash);

                // --------------------------------------------------------
                // Ordinary asset-reference patching
                // --------------------------------------------------------

                if (hasAssetReferencePatches)
                {
                    string patchKey =
                        AssetReferencePatchCache.ComputePatchKey(
                            unityBundleHash,
                            patches);

                    AssetReferenceBundlePatchResult patchResult;

                    if (AssetReferencePatchCache
                        .TryRestorePatchedBundleWithMetadata(
                            patchKey,
                            assetsBundlePath,
                            out patchResult))
                    {
                        manifest.assetReferencePatching.cacheHit =
                            true;

#if DEVELOPMENT_BUILD
                        Debug.Log(
                            "Asset reference patching: cache hit.");
#endif
                    }
                    else
                    {
                        manifest.assetReferencePatching.cacheHit =
                            false;

                        patchResult =
                            AssetReferenceBundlePatcher.Patch(
                                assetsBundlePath,
                                patches);

                        // Save the asset-patched output before the MonoScript
                        // pass. This keeps the existing cache independent of
                        // MonoScript mappings; the script pass is cheap and is
                        // intentionally reapplied every export.
                        AssetReferencePatchCache.SavePatchedBundle(
                            patchKey,
                            assetsBundlePath);

                        AssetReferencePatchCache.SavePatchMetadata(
                            patchKey,
                            Path.GetFileName(assetsBundlePath),
                            patchResult);

#if DEVELOPMENT_BUILD
                        Debug.Log(
                            "Asset reference patching: cache updated.");
#endif
                    }

                    manifest
                        .assetReferencePatching
                        .rewrittenReferenceCount =
                            patchResult.RewrittenReferenceCount;

                    manifest
                        .assetReferencePatching
                        .removedProxyCount =
                            patchResult.RemovedProxyCount;

                    if (patchResult.RemovedProxyCount > 0)
                    {
                        var removedPaths =
                            new HashSet<string>(
                                patchResult.RemovedProxyAssetPaths,
                                StringComparer.OrdinalIgnoreCase);

                        foreach (var mapping in
                                 manifest.assetReferencePatching.mappings)
                        {
                            mapping.proxyRemoved =
                                removedPaths.Contains(
                                    mapping.proxyAssetPath);
                        }
                    }
                }

                // --------------------------------------------------------
                // MonoScript-reference patching
                // --------------------------------------------------------

                if (hasMonoScriptReferencePatches)
                {
                    var monoScriptPatchResult =
                        MonoScriptReferenceBundlePatcher.Patch(
                            assetsBundlePath,
                            monoScriptPatches);

                    manifest
                        .monoScriptReferencePatching
                        .matchedMappingCount =
                            monoScriptPatchResult.MatchedMappingCount;

                    manifest
                        .monoScriptReferencePatching
                        .rewrittenScriptTypeCount =
                            monoScriptPatchResult.RewrittenScriptTypeCount;

                    manifest
                        .monoScriptReferencePatching
                        .rewrittenMonoBehaviourCount =
                            monoScriptPatchResult.RewrittenMonoBehaviourCount;
                }
            }

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

        private static void TrySaveManifestAfterUnsuccessfulExport(
            StationeersExportManifest manifest,
            string outcome)
        {
            try
            {
                StationeersExportManifestStore.Save(manifest);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"Could not save export manifest after {outcome}: {ex.Message}");
            }
        }

        /// <summary>
        /// Export process
        /// </summary>
        /// <param name="options"></param>
        public static void Export(BuildPlayerOptions options)
        {
#if DEVELOPMENT_BUILD
            Debug.Log("Export started");
#endif
            exportFolder = options.locationPathName;

            var manifest = new StationeersExportManifest
            {
                unityVersion = Application.unityVersion,
                productName = PlayerSettings.productName,
                exportFolder = exportFolder,
                buildTarget = options.target.ToString(),
                buildOptions = options.options.ToString(),
                utcTimestamp = DateTime.UtcNow.ToString("o"),
                bundleVersion = PlayerSettings.bundleVersion
            };

            try
            {
                if (CleanBuildCacheIsSet(options))
                {
                    DeleteOutputFolder(exportFolder);
                    AssetReferencePatchCache.Clear();
                }

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

                if (manifest.warnings.Count > 0)
                {
                    Debug.LogWarning(
                        $"Export completed with {manifest.warnings.Count} warning(s). " +
                        "Check the export manifest for details.");
                }

                if (StationeersExporterUserPreferences.AutoIncrementBuild)
                    StationeersVersioning.IncrementBuildVersion(out var oldVersion, out var newVersion);

                Debug.Log($"Export complete: {assemblies} Assemblie(s), {assets} Assetbundle(s), {folderAssets} Folder(s).");
            }
            catch (OperationCanceledException oce)
            {
                string message =
                    string.IsNullOrWhiteSpace(oce.Message)
                        ? "Export canceled by user."
                        : $"Export canceled: {oce.Message}";

                manifest.warnings.Add(message);
                Debug.LogWarning(message);
                TrySaveManifestAfterUnsuccessfulExport(manifest, "cancellation");
                return;
            }
            catch (Exception ex)
            {
                string message = $"Export failed: {ex}";

                manifest.warnings.Add(message);
                Debug.LogError(message);
                TrySaveManifestAfterUnsuccessfulExport(manifest, "failure");
                throw;
            }
        }

    }
}