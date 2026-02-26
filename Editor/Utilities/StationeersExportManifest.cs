using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Serializable record of the most recent export.
    /// </summary>
    /// <remarks>
    /// This manifest is intended to help you debug exports and provide traceability:
    /// - What Unity version and product settings were used
    /// - Where the export was written
    /// - What was copied or bundled
    /// - Any warnings that occurred during export
    ///
    /// The lists are initialized to empty lists to avoid null checks when writing to them.
    /// This class is JSON-serialized using UnityEngine.JsonUtility.
    /// </remarks>
    [Serializable]
    public sealed class StationeersExportManifest
    {
        /// <summary>
        /// Unity editor version used for the export (for example "2022.3.10f1").
        /// </summary>
        public string unityVersion;

        /// <summary>
        /// Product name at export time (usually PlayerSettings.productName).
        /// </summary>
        public string productName;

        /// <summary>
        /// Absolute or project-relative folder where export output was written.
        /// </summary>
        public string exportFolder;

        /// <summary>
        /// Build target name (for example "StandaloneWindows64").
        /// </summary>
        public string buildTarget;

        /// <summary>
        /// Build options used for the export (string form of BuildOptions flags).
        /// </summary>
        public string buildOptions;

        /// <summary>
        /// Bundle version at export time (usually PlayerSettings.bundleVersion).
        /// </summary>
        public string bundleVersion;

        /// <summary>
        /// Timestamp (UTC) when the export was produced, as a string.
        /// </summary>
        /// <remarks>
        /// Store this in a stable, sortable format (for example ISO-8601) in the exporter code.
        /// </remarks>
        public string utcTimestamp;

        /// <summary>
        /// Names of managed assemblies copied into the export output (for example "MyMod.dll").
        /// </summary>
        public List<string> assembliesCopied = new();

        /// <summary>
        /// Names of PDB files copied into the export output (for example "MyMod.pdb").
        /// </summary>
        public List<string> pdbsCopied = new();

        /// <summary>
        /// Source folder paths copied into the export output (usually "Assets/..." paths).
        /// </summary>
        public List<string> foldersCopied = new();

        /// <summary>
        /// Asset paths assigned to the asset bundle (prefabs and ScriptableObjects, typically "Assets/...").
        /// </summary>
        public List<string> assetPathsBundled = new();

        /// <summary>
        /// Scene paths assigned to the asset bundle (typically "Assets/...").
        /// </summary>
        public List<string> scenePathsBundled = new();

        /// <summary>
        /// Count of assemblies considered for export (not necessarily copied successfully).
        /// </summary>
        public int assembliesCount;

        /// <summary>
        /// Count of folders copied.
        /// </summary>
        public int folderCount;

        /// <summary>
        /// Count of assets assigned to the bundle.
        /// </summary>
        public int assetsCount;

        /// <summary>
        /// Count of scenes assigned to the bundle.
        /// </summary>
        public int scenesCount;

        /// <summary>
        /// Warnings collected during export (missing files, skipped items, exceptions, etc).
        /// </summary>
        public List<string> warnings = new();
    }

    /// <summary>
    /// Stores and retrieves the most recent export manifest on disk.
    /// </summary>
    /// <remarks>
    /// Storage location:
    /// - Library/StationeersModdingExporter/last-export.json
    ///
    /// Library is not a Unity asset folder, so AssetDatabase.Refresh is not needed after writing.
    /// The file is written for inspection/debugging and can be opened in the OS file explorer.
    /// </remarks>
    public static class StationeersExportManifestStore
    {
        private const string Dir = "Library/StationeersModdingExporter";
        private const string FileName = "last-export.json";

        /// <summary>
        /// Gets the full manifest file path under the project Library folder.
        /// </summary>
        public static string ManifestPath => Path.Combine(Dir, FileName);

        /// <summary>
        /// Saves a manifest as JSON to the Library folder.
        /// </summary>
        /// <param name="manifest">Manifest instance to serialize and write.</param>
        /// <remarks>
        /// This method creates the storage directory if needed and overwrites any previous manifest.
        /// </remarks>
        public static void Save(StationeersExportManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            Directory.CreateDirectory(Dir);

            string json = JsonUtility.ToJson(manifest, prettyPrint: true);
            File.WriteAllText(ManifestPath, json);
        }

        /// <summary>
        /// Loads the last saved manifest, or returns null if no manifest exists.
        /// </summary>
        /// <returns>The deserialized manifest, or null if the file is missing or invalid.</returns>
        public static StationeersExportManifest LoadOrNull()
        {
            if (!File.Exists(ManifestPath))
                return null;

            try
            {
                string json = File.ReadAllText(ManifestPath);
                return JsonUtility.FromJson<StationeersExportManifest>(json);
            }
            catch
            {
                // If the file is corrupt or JSON structure changed, do not crash caller.
                return null;
            }
        }

        /// <summary>
        /// Opens the last export manifest in the OS file explorer.
        /// </summary>
        /// <remarks>
        /// If the manifest does not exist yet, shows a dialog telling the user to run an export first.
        /// </remarks>
        [MenuItem("Tools/Stationeers/Exporter/Open Last Export Manifest")]
        public static void OpenLastExportManifest()
        {
            string path = ManifestPath;

            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog(
                    "Exporter",
                    "No manifest found yet.\nRun an export first.",
                    "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }
    }
}