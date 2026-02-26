using System.Collections.Generic;
using UnityEditor;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Project-wide settings for the Stationeers mod exporter.
    /// </summary>
    /// <remarks>
    /// This settings object is stored as a ScriptableSingleton asset under ProjectSettings:
    /// "ProjectSettings/StationeersModdingExporterSettings.asset"
    ///
    /// Because it lives under ProjectSettings, it can be committed to source control so the exporter
    /// behaves consistently across machines (team members, build agents, etc).
    ///
    /// The exporter uses these settings primarily to:
    /// - Control About.xml <-> PlayerSettings synchronization behavior.
    /// - Define which folders should be included by default during export.
    ///
    /// This class is Editor-only (UnityEditor dependency) and should not be referenced at runtime.
    /// </remarks>
    [FilePath("ProjectSettings/StationeersModdingExporterSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class StationeersExporterSettings : ScriptableSingleton<StationeersExporterSettings>
    {
        // About.xml sync settings

        /// <summary>
        /// When true, keep PlayerSettings and About.xml synchronized in both directions.
        /// </summary>
        /// <remarks>
        /// This is a convenience switch used by the exporter to enable "two-way" syncing behavior.
        /// When enabled, you should ensure your sync pipeline avoids infinite loops
        /// (for example, by comparing values before writing or using a "currently syncing" guard).
        /// </remarks>
        public bool aboutAutoSyncBoth = true;

        /// <summary>
        /// When true, automatically propagate PlayerSettings values into About.xml.
        /// </summary>
        /// <remarks>
        /// This is typically implemented by a watcher that runs when PlayerSettings fields change,
        /// and rewrites About.xml to match.
        /// </remarks>
        public bool aboutAutoSyncPlayerToXml = true;

        /// <summary>
        /// When true, automatically propagate About.xml values into PlayerSettings.
        /// </summary>
        /// <remarks>
        /// This is typically implemented by an asset postprocessor or editor startup routine
        /// that reads About.xml and updates PlayerSettings to match.
        /// </remarks>
        public bool aboutAutoSyncXmlToPlayer = true;

        /// <summary>
        /// Unity project-relative path to the About.xml file used by the exporter.
        /// </summary>
        /// <remarks>
        /// Default: "Assets/About/About.xml"
        /// The exporter assumes this file exists or will be created by tooling such as AssetUtility.CreateDefaultAbout().
        /// </remarks>
        public string aboutXmlPath = "Assets/About/About.xml";

        /// <summary>
        /// When true, About.xml is written using UTF-8 with BOM.
        /// </summary>
        /// <remarks>
        /// Some XML readers and legacy tooling expect a BOM. If you do not need it,
        /// you can disable this to write plain UTF-8 without BOM.
        /// </remarks>
        public bool aboutWriteUtf8Bom = true;

        // Default export folder list

        /// <summary>
        /// Default list of project folders to include during export operations.
        /// </summary>
        /// <remarks>
        /// These are Unity project-relative paths.
        /// The exporter typically copies these folders into the output folder (or uses them for bundling).
        /// </remarks>
        public List<string> exportFolders = new List<string>
        {
            "Assets/GameData",
            "Assets/About",
        };

        /// <summary>
        /// Saves the settings asset immediately to disk.
        /// </summary>
        /// <remarks>
        /// Uses ScriptableSingleton.Save(saveAsText: true) so the asset is persisted under ProjectSettings.
        /// Call this after changing settings via code.
        /// </remarks>
        public void SaveNow() => Save(true);
    }
}