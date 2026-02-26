using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Editor-only utilities for creating default mod assets and working with project assets.
    /// </summary>
    /// <remarks>
    /// Responsibilities:
    /// - Create default mod setup (About.xml, preview images, starter script, asmdef).
    /// - Provide helpers for finding/moving assets. 
    /// - Provide path helpers (absolute to project-relative).
    ///
    /// Notes:
    /// - These APIs depend on UnityEditor and should not be used in player runtime.
    /// - File writes are done using System.IO, then imported via AssetDatabase.
    /// </remarks>
    public static class AssetUtility
    {
        private const string DefaultScriptFolder = "Scripts";
        private const string DefaultAboutFolder = "About";
        private const string AboutFileName = "About.xml";
        private const string PreviewFileName = "Preview.png";
        private const string ThumbFileName = "Thumb.png";

        /// <summary>
        /// Creates a standard set of starter assets for a mod project.
        /// </summary>
        /// <remarks>
        /// Creates:
        /// - Assets/About/About.xml (if missing)
        /// - Assets/About/Preview.png (if missing)
        /// - Assets/About/Thumb.png (if missing)
        /// - Assets/Scripts/{ProductName}.cs (if missing)
        /// - Default assembly definition via AsmDef.CreateDefaultAssembly()
        /// </remarks>
        public static void CreateDefaultSetup()
        {
            CreateDefaultAbout();
            CreateDefaultScript();
            AsmDef.CreateDefaultAssembly();
        }

        /// <summary>
        /// Creates a starter mod script file under Assets/Scripts if it does not already exist.
        /// </summary>
        /// <remarks>
        /// The script is created using a template and includes the product name and bundle version.
        /// If the target file already exists, no changes are made.
        /// </remarks>
        public static void CreateDefaultScript()
        {
            string productName = StationeersModdingExport.Sanitize(PlayerSettings.productName);
            string productVersion = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion) ? "1.0.0" : PlayerSettings.bundleVersion;

            string fileName = productName + ".cs";
            string savePath = Path.Combine("Assets", DefaultScriptFolder, fileName);

            if (File.Exists(savePath))
            {
                Debug.Log(savePath + " exists, aborting.");
                return;
            }

            EnsureFolder("Assets", DefaultScriptFolder);

            string src = GetScriptTemplate()
                .Replace("{productName}", productName)
                .Replace("{productVersion}", productVersion);

            File.WriteAllText(savePath, src);
            AssetDatabase.ImportAsset(savePath);
        }

        /// <summary>
        /// Creates the default About folder content (About.xml, Preview.png, Thumb.png) under Assets/About.
        /// </summary>
        /// <remarks>
        /// - About.xml is created only if missing.
        /// - Preview.png and Thumb.png are copied from the local "templates" folder adjacent to this file,
        ///   only if missing.
        /// </remarks>
        public static void CreateDefaultAbout()
        {
            string companyName = StationeersModdingExport.Sanitize(PlayerSettings.companyName ?? "Default Company");
            string productName = StationeersModdingExport.Sanitize(PlayerSettings.productName ?? "ProductName");
            string productVersion = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion) ? "1.0.0" : PlayerSettings.bundleVersion;

            EnsureFolder("Assets", DefaultAboutFolder);

            string aboutPath = Path.Combine("Assets", DefaultAboutFolder, AboutFileName);
            if (!File.Exists(aboutPath))
            {
                string src = GetXmlTemplate()
                    .Replace("{productName}", productName)
                    .Replace("{author}", companyName)
                    .Replace("{productVersion}", productVersion);

                File.WriteAllText(aboutPath, src);
                AssetDatabase.ImportAsset(aboutPath);
            }

            // Copy default images (if they do not exist)
            string thisFileAbs = GetThisFilePath();
            string thisDirAbs = Path.GetDirectoryName(thisFileAbs) ?? string.Empty;
            string templatesAbs = Path.Combine(thisDirAbs, "templates");

            CopyTemplateFileIfMissing(templatesAbs, Path.Combine("Assets", DefaultAboutFolder, PreviewFileName), PreviewFileName);
            CopyTemplateFileIfMissing(templatesAbs, Path.Combine("Assets", DefaultAboutFolder, ThumbFileName), ThumbFileName);
        }

        /// <summary>
        /// Returns all unique asset paths that match a Unity AssetDatabase search filter.
        /// </summary>
        /// <param name="filter">
        /// The filter string can contain search data for names, asset labels, and types (class names).
        /// </param>
        /// <returns>Unique asset paths, excluding Editor folders and Packages/ paths.</returns>
        public static List<string> GetAssets(string filter)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var guids = AssetDatabase.FindAssets(filter);
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(assetPath))
                    continue;

                if (assetPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    assetPath.EndsWith("/Editor", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.IndexOf("\\Editor\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (assetPath.StartsWith("Packages", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (seen.Add(assetPath))
                    results.Add(assetPath);
            }

            return results;
        }

        /// <summary>
        /// Ensures a Unity project folder exists (creates it if missing).
        /// </summary>
        /// <param name="parent">Parent folder (for example "Assets").</param>
        /// <param name="name">Folder name to create under parent.</param>
        public static void EnsureFolder(string parent, string name)
        {
            string combined = NormalizeAssetPath(Path.Combine(parent, name));
            if (AssetDatabase.IsValidFolder(combined))
                return;

            AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>
        /// Ensures Assets/GameData exists.
        /// </summary>
        public static void CreateDefaultGameDataFolder()
        {
            EnsureFolder("Assets", "GameData");
        }

        /// <summary>
        /// Returns the absolute file path of this source file at compile time.
        /// </summary>
        /// <param name="path">
        /// Automatically supplied by the compiler. Do not pass manually.
        /// </param>
        /// <returns>
        /// Absolute path to this .cs file on disk.
        /// </returns>
        /// <remarks>
        /// This method is Editor-only and relies on CallerFilePath.
        /// It is used to locate adjacent template files on disk.
        /// </remarks>
        private static string GetThisFilePath([CallerFilePath] string path = "") => path;

        /// <summary>
        /// Copies a template file into the Unity project if the destination does not already exist.
        /// </summary>
        /// <param name="templatesAbsDir">Absolute directory containing template files.</param>
        /// <param name="destProjectPath">Destination path inside the Unity project (Assets/...)</param>
        /// <param name="fileName">File name to copy from the templates directory.</param>
        /// <remarks>
        /// - Does nothing if the destination file already exists.
        /// - Logs a warning if the source template file is missing.
        /// - Imports the copied file into the AssetDatabase.
        /// </remarks>
        private static void CopyTemplateFileIfMissing(string templatesAbsDir, string destProjectPath, string fileName)
        {
            if (File.Exists(destProjectPath))
                return;

            string sourceAbs = Path.Combine(templatesAbsDir, fileName);
            if (!File.Exists(sourceAbs))
            {
                Debug.LogWarning("[AssetUtility] Missing template file: " + sourceAbs);
                return;
            }

            File.Copy(sourceAbs, destProjectPath, overwrite: true);
            AssetDatabase.ImportAsset(destProjectPath);
        }

        /// <summary>
        /// Normalizes a Unity asset path to use forward slashes.
        /// </summary>
        /// <param name="path">Input path.</param>
        /// <returns>
        /// Normalized path using forward slashes, or the original value if null or empty.
        /// </returns>
        /// <remarks>
        /// Unity APIs expect asset paths to use '/' regardless of platform.
        /// </remarks>
        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }

        /// <summary>
        /// Returns the C# source template used for generating the default mod script.
        /// </summary>
        /// <returns>
        /// A C# source file template with placeholders for product name and version.
        /// </returns>
        /// <remarks>
        /// Placeholders:
        /// - {productName}
        /// - {productVersion}
        ///
        /// The template intentionally avoids verbatim strings so replacements remain simple.
        /// </remarks>
        private static string GetScriptTemplate()
        {
            // Keep the template as a normal string (not verbatim) so it is easy to inject values.
            return
                "using UnityEngine;\n" +
                "using LaunchPadBooster;\n" +
                "using System.Collections.Generic;\n" +
                "\n" +
                "namespace {productName}\n" +
                "{\n" +
                "    public class {productName} : MonoBehaviour\n" +
                "    {\n" +
                "        public static readonly Mod MOD = new(\"{productName}\", \"{productVersion}\");\n" +
                "\n" +
                "        public void OnLoaded(List<GameObject> prefabs)\n" +
                "        {\n" +
                "            MOD.AddPrefabs(prefabs);\n" +
                "\n" +
                "#if DEVELOPMENT_BUILD\n" +
                "            Debug.Log($\"Loaded {prefabs.Count} prefabs\");\n" +
                "#endif\n" +
                "\n" +
                "            // Additional initialization goes here\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
        }

        /// <summary>
        /// Returns the XML template used for generating About.xml.
        /// </summary>
        /// <returns>
        /// XML content with placeholders for product name, author, and version.
        /// </returns>
        /// <remarks>
        /// Placeholders:
        /// - {productName}
        /// - {author}
        /// - {productVersion}
        ///
        /// The generated file conforms to Stationeers mod metadata expectations.
        /// </remarks>
        private static string GetXmlTemplate()
        {
            return
                "<?xml version=\"1.0\"?>\n" +
                "<ModMetadata xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "  <Name>{productName}</Name>\n" +
                "  <Author>{author}</Author>\n" +
                "  <Version>{productVersion}</Version>\n" +
                "  <Description>Stationeers mod</Description>\n" +
                "  <WorkshopHandle>0</WorkshopHandle>\n" +
                "  <Tags>\n" +
                "    <Tag>LaunchPad</Tag>\n" +
                "  </Tags>\n" +
                "</ModMetadata>\n";
        }
    }
}