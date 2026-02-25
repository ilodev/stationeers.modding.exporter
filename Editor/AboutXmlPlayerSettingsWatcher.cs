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
        private static string _lastProduct;
        private static string _lastCompany;
        private static string _lastVersion;

        static AboutXmlPlayerSettingsWatcher()
        {
            SnapshotCurrent();

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// Manual sync entry point (used by the Project Settings UI).
        /// </summary>
        public static void SyncNow(bool force)
        {
            TryWriteXmlFromPlayerSettings(force);
            SnapshotCurrent();
        }

        private static void Tick()
        {
            var settings = StationeersExporterSettings.instance;
            bool needsToUpdate = settings.aboutAutoSyncPlayerToXml || settings.aboutAutoSyncBoth;
            if (settings == null || !needsToUpdate)
                return;

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

            var settings = StationeersExporterSettings.instance;
            if (settings == null)
                return;

            var aboutPath = string.IsNullOrEmpty(settings.aboutXmlPath) ? "Assets/About/About.xml" : settings.aboutXmlPath;

            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(aboutPath);
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
                    Debug.LogWarning("[About Watcher] Root element is not <ModMetadata>; skipping update: " + aboutPath);
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
                File.WriteAllText(aboutPath, xmlOut, new UTF8Encoding(settings.aboutWriteUtf8Bom));

                // Reimport to update TextAsset
                AssetDatabase.ImportAsset(aboutPath, ImportAssetOptions.ForceUpdate);

                Debug.Log(string.Format(
                    "[About Watcher] Updated {0} -> Name=\"{1}\", Author=\"{2}\", Version=\"{3}\"",
                    aboutPath, product, company, version));
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[About Watcher] Failed to update " + aboutPath + ":\n" + ex);
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