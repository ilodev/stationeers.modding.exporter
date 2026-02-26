using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    namespace stationeers.modding.exporter
    {
        [Serializable]
        public class StationeersExportManifest
        {
            public string unityVersion;
            public string productName;
            public string exportFolder;
            public string buildTarget;
            public string buildOptions;
            public string bundleVersion;
            public string utcTimestamp;

            public List<string> assembliesCopied = new();
            public List<string> pdbsCopied = new();

            public List<string> foldersCopied = new(); // "Assets/..." sources
            public List<string> assetPathsBundled = new(); // prefabs+SOs assigned to bundle
            public List<string> scenePathsBundled = new(); // scenes assigned to bundle

            public int assembliesCount;
            public int folderCount;
            public int assetsCount;
            public int scenesCount;

            public List<string> warnings = new();
        }

        public static class StationeersExportManifestStore
        {
            private const string Dir = "Library/StationeersModdingExporter";
            private const string FileName = "last-export.json";

            public static string ManifestPath => Path.Combine(Dir, FileName);

            public static void Save(StationeersExportManifest manifest)
            {
                Directory.CreateDirectory(Dir);
                var json = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(ManifestPath, json);
                AssetDatabase.Refresh();
            }

            public static StationeersExportManifest LoadOrNull()
            {
                if (!File.Exists(ManifestPath))
                    return null;

                var json = File.ReadAllText(ManifestPath);
                return JsonUtility.FromJson<StationeersExportManifest>(json);
            }

            [MenuItem("Tools/Stationeers/Exporter/Open Last Export Manifest")]
            public static void OpenLastExportManifest()
            {
                var path = StationeersExportManifestStore.ManifestPath;
                if (!System.IO.File.Exists(path))
                {
                    EditorUtility.DisplayDialog("Exporter", "No manifest found yet.\nRun an export first.", "OK");
                    return;
                }

                EditorUtility.RevealInFinder(path);
            }

        }
    }
}
