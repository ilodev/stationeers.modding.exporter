// AboutXmlPostprocessor.cs
// Watches Assets/About/About.xml and applies fields to PlayerSettings.
// - Name        -> PlayerSettings.productName
// - Author      -> PlayerSettings.companyName
// - Version     -> PlayerSettings.bundleVersion

using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    public class AboutXmlPostprocessor : AssetPostprocessor
    {
        private const string AboutPath = "Assets/About/About.xml";

        // Fires on import/move/reimport
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Contains(AboutPath) || movedAssets.Contains(AboutPath))
            {
                ApplyAboutToPlayerSettings(logPrefix: "[About.xml Import]");
            }
        }

        // Also run once on editor load so PlayerSettings are kept in sync
        [InitializeOnLoadMethod]
        private static void ApplyOnLoad()
        {
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(AboutPath) != null)
                ApplyAboutToPlayerSettings(logPrefix: "[About.xml Startup]");
        }

        [MenuItem("Tools/About/Apply About.xml -> Player Settings")]
        private static void ApplyFromMenu()
        {
            ApplyAboutToPlayerSettings(logPrefix: "[About.xml Manual]");
        }

        private static void ApplyAboutToPlayerSettings(string logPrefix)
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(AboutPath);
            if (ta == null)
            {
                Debug.LogWarning(logPrefix + " File not found at " + AboutPath);
                return;
            }

            try
            {
                var doc = XDocument.Parse(ta.text);
                var root = doc.Root;
                if (root == null || root.Name.LocalName != "ModMetadata")
                {
                    Debug.LogWarning(logPrefix + " Root element is not <ModMetadata> - skipping.");
                    return;
                }

                // Helper to fetch element by LocalName (ignores namespaces)
                string Get(string name)
                {
                    var el = root.Elements().FirstOrDefault(e => e.Name.LocalName == name);
                    var val = el != null ? el.Value : string.Empty;
                    return val != null ? val.Trim() : string.Empty;
                }

                string projectName = Get("Name");
                string author = Get("Author");
                string version = Get("Version");

                // Apply to PlayerSettings (Editor)
                bool changed = false;

                if (!string.IsNullOrEmpty(projectName) && PlayerSettings.productName != projectName)
                {
                    PlayerSettings.productName = projectName;
                    changed = true;
                }
                if (!string.IsNullOrEmpty(author) && PlayerSettings.companyName != author)
                {
                    PlayerSettings.companyName = author;
                    changed = true;
                }
                if (!string.IsNullOrEmpty(version) && PlayerSettings.bundleVersion != version)
                {
                    PlayerSettings.bundleVersion = version;
                    changed = true;
                }

                if (changed)
                {
                    Debug.Log(string.Format(
                        "{0} Applied to PlayerSettings -> productName=\"{1}\", companyName=\"{2}\", bundleVersion=\"{3}\"",
                        logPrefix, PlayerSettings.productName, PlayerSettings.companyName, PlayerSettings.bundleVersion));
                }
                else
                {
                    Debug.Log(logPrefix + " No changes needed (already in sync).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(logPrefix + " Failed to parse/apply " + AboutPath + ":\n" + ex);
            }
        }
    }
}
