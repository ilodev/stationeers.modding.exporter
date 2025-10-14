// AboutXmlPlayerSettingsWatcher.cs
// Watches PlayerSettings (productName, companyName, bundleVersion).
// If Assets/About/About.xml exists, updates its <Name>, <Author>, <Version>
// when those PlayerSettings values change.


using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{

    [InitializeOnLoad]
    public static class AboutXmlPlayerSettingsWatcher
    {
        private const string AboutPath = "Assets/About/About.xml";
        private const string PrefEnabledKey = "AboutXmlPlayerSettingsWatcher_Enabled";

        private static string _lastProduct;
        private static string _lastCompany;
        private static string _lastVersion;
        private static bool _enabled;

        static AboutXmlPlayerSettingsWatcher()
        {
            _enabled = EditorPrefs.GetBool(PrefEnabledKey, true);
            SnapshotCurrent();

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/About/Watcher/Enable", priority = 0)]
        private static void EnableWatcher()
        {
            _enabled = true;
            EditorPrefs.SetBool(PrefEnabledKey, true);
            Debug.Log("[About Watcher] Enabled");
        }

        [MenuItem("Tools/About/Watcher/Enable", true)]
        private static bool EnableValidate()
        {
            Menu.SetChecked("Tools/About/Watcher/Enable", _enabled);
            return true;
        }

        [MenuItem("Tools/About/Watcher/Disable", priority = 1)]
        private static void DisableWatcher()
        {
            _enabled = false;
            EditorPrefs.SetBool(PrefEnabledKey, false);
            Debug.Log("[About Watcher] Disabled");
        }

        [MenuItem("Tools/About/Watcher/Disable", true)]
        private static bool DisableValidate()
        {
            Menu.SetChecked("Tools/About/Watcher/Disable", !_enabled);
            return true;
        }

        [MenuItem("Tools/About/Sync PlayerSettings -> About.xml", priority = 20)]
        private static void SyncNow()
        {
            TryWriteXmlFromPlayerSettings(force: true);
        }

        private static void Tick()
        {
            if (!_enabled) return;

            var curProduct = PlayerSettings.productName ?? string.Empty;
            var curCompany = PlayerSettings.companyName ?? string.Empty;
            var curVersion = PlayerSettings.bundleVersion ?? string.Empty;

            if (curProduct != _lastProduct || curCompany != _lastCompany || curVersion != _lastVersion)
            {
                // PlayerSettings changed since last snapshot; attempt to sync to XML.
                TryWriteXmlFromPlayerSettings(force: false);
                SnapshotCurrent();
            }
        }

        private static void SnapshotCurrent()
        {
            _lastProduct = PlayerSettings.productName ?? string.Empty;
            _lastCompany = PlayerSettings.companyName ?? string.Empty;
            _lastVersion = PlayerSettings.bundleVersion ?? string.Empty;
        }

        private static bool _writeInProgress = false;

        private static void TryWriteXmlFromPlayerSettings(bool force)
        {
            if (_writeInProgress) return;

            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(AboutPath);
            if (ta == null)
            {
                // File not present; nothing to do.
                return;
            }

            try
            {
                _writeInProgress = true;

                // Parse the existing XML
                var doc = XDocument.Parse(ta.text);
                var root = doc.Root;
                if (root == null || root.Name.LocalName != "ModMetadata")
                {
                    Debug.LogWarning("[About Watcher] Root element is not <ModMetadata>; skipping update: " + AboutPath);
                    return;
                }

                string product = PlayerSettings.productName ?? string.Empty;
                string company = PlayerSettings.companyName ?? string.Empty;
                string version = PlayerSettings.bundleVersion ?? string.Empty;

                // Fetch current XML values
                XElement elName = GetOrCreateChild(root, "Name");
                XElement elAuthor = GetOrCreateChild(root, "Author");
                XElement elVersion = GetOrCreateChild(root, "Version");

                bool differs =
                    (elName.Value != product) ||
                    (elAuthor.Value != company) ||
                    (elVersion.Value != version);

                if (!differs && !force)
                {
                    // Already in sync
                    return;
                }

                elName.Value = product;
                elAuthor.Value = company;
                elVersion.Value = version;

                // Write back with UTF-8 (with BOM). Use false if you prefer no BOM.
                string xmlOut = doc.ToString(SaveOptions.None);
                File.WriteAllText(AboutPath, xmlOut, new UTF8Encoding(true));

                // Reimport to update TextAsset
                AssetDatabase.ImportAsset(AboutPath, ImportAssetOptions.ForceUpdate);

                Debug.Log(string.Format(
                    "[About Watcher] Updated {0} -> Name=\"{1}\", Author=\"{2}\", Version=\"{3}\"",
                    AboutPath, product, company, version));
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[About Watcher] Failed to update " + AboutPath + ":\n" + ex);
            }
            finally
            {
                _writeInProgress = false;
            }
        }

        private static XElement GetOrCreateChild(XElement parent, string localName)
        {
            var el = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
            if (el == null)
            {
                el = new XElement(localName);
                parent.Add(el);
            }
            return el;
        }
    }
}