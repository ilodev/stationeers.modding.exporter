using System.Collections.Generic;
using UnityEditor;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Project-wide settings for the exporter package.
    /// Stored under ProjectSettings/ (shared with the project if committed to Git to preserve additional folders).
    /// </summary>
    [FilePath("ProjectSettings/StationeersModdingExporterSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class StationeersExporterSettings : ScriptableSingleton<StationeersExporterSettings>
    {
        // About.xml sync
        public bool aboutAutoSyncBoth = true; // Keep PlayerSettings and About.xml synchronized
        public bool aboutAutoSyncPlayerToXml = true;   // PlayerSettings -> About.xml watcher
        public bool aboutAutoSyncXmlToPlayer = true;   // About.xml -> PlayerSettings postprocessor/startup
        public string aboutXmlPath = "Assets/About/About.xml";
        public bool aboutWriteUtf8Bom = true;

        // Default Export folders
        public List<string> exportFolders = new List<string>
        {
            "Assets/GameData",
            "Assets/About",
        };

        public void SaveNow() => Save(true);
    }
}
