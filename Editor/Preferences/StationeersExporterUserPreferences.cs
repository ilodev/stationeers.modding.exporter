using UnityEditor;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Per-user, per-machine preferences for the exporter package.
    /// Stored in EditorPrefs, not shared with the project settings.
    /// </summary>
    internal static class StationeersExporterUserPreferences
    {
        // Stationeers export folder
        private const string ExportFolderKey = "StationeersExport_Folder";
        public static string ExportFolder
        {
            get => EditorPrefs.GetString(ExportFolderKey, string.Empty);
            set => EditorPrefs.SetString(ExportFolderKey, value ?? string.Empty);
        }


        // Stationeers runner
        private const string RunnerEnabledKey = "StationeersRunner_Enabled";
        private const string RunnerExeOverrideKey = "StationeersRunner.ExeOverride";
        public static bool RunnerEnabled
        {
            get => EditorPrefs.GetBool(RunnerEnabledKey, false);
            set => EditorPrefs.SetBool(RunnerEnabledKey, value);
        }
        public static string RunnerExeOverride
        {
            get => EditorPrefs.GetString(RunnerExeOverrideKey, string.Empty);
            set => EditorPrefs.SetString(RunnerExeOverrideKey, value ?? string.Empty);
        }


        public static void ClearRunnerExeOverride() => EditorPrefs.DeleteKey(RunnerExeOverrideKey);
    }
}
