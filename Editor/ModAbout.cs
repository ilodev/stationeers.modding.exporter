using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace stationeers.modding.exporter
{
    public class ModAbout
    {
        public string Name { get; }
        public string Author { get; }
        public string Description { get; }
        public string Version { get; }

        public ModAbout(string name, string author, string description, string version)
        {
            Name = name ?? string.Empty;
            Author = author ?? string.Empty;
            Description = description ?? string.Empty;
            Version = version ?? string.Empty;
        }

        /// <summary>
        /// Create or update Assets/About/About.xml.
        /// - If the file exists, updates Name/Author/Version/Description while preserving other nodes.
        /// - If missing, creates a minimal ModMetadata document.
        /// </summary>
        public static void Save(string path, ModAbout about)
        {
            if (about == null) throw new System.ArgumentNullException(nameof(about));
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            XDocument doc;

            if (File.Exists(path))
            {
                // Load existing and preserve everything we’re not explicitly setting
                doc = XDocument.Load(path, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                if (doc.Root == null)
                {
                    doc = CreateFreshDoc();
                }
                else if (doc.Root.Name.LocalName != "ModMetadata")
                {
                    // If the root isn’t what we expect, replace with our expected format
                    doc = CreateFreshDoc();
                }
            }
            else
            {
                doc = CreateFreshDoc();
            }

            var root = doc.Root!;

            void SetOrAdd(string name, string value)
            {
                var el = root.Element(name);
                if (el == null)
                {
                    root.Add(new XElement(name, value ?? string.Empty));
                }
                else
                {
                    el.Value = value ?? string.Empty;
                }
            }

            SetOrAdd("Name", about.Name);
            SetOrAdd("Author", about.Author);
            SetOrAdd("Version", about.Version);
            SetOrAdd("Description", about.Description);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace
            };

            using (var writer = XmlWriter.Create(path, settings))
            {
                doc.Save(writer);
            }

#if UNITY_EDITOR
            // Ensure Unity sees/refreshes the asset
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();
#endif
        }

        public static ModAbout Load(string path)
        {
            if (!File.Exists(path)) return new ModAbout("", "", "", "");
            var doc = XDocument.Load(path);
            var root = doc.Root;

            string G(string name) => root?.Element(name)?.Value ?? string.Empty;

            return new ModAbout(
                G("Name"),
                G("Author"),
                G("Description"),
                G("Version")
            );
        }

        private static XDocument CreateFreshDoc()
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("ModMetadata",            // matches your existing file’s root
                    new XElement("Name", ""),
                    new XElement("Author", ""),
                    new XElement("Version", ""),
                    new XElement("Description", "")
                )
            );
        }
    }
}
