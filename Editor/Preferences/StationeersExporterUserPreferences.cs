using UnityEditor;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Per-user, per-machine preferences for the Stationeers exporter.
    /// </summary>
    /// <remarks>
    /// These preferences are stored in UnityEditor.EditorPrefs:
    /// - They are not shared with the project.
    /// - They are not suitable for team-wide configuration.
    /// - They persist across Unity sessions on the same machine/user.
    ///
    /// Use StationeersExporterSettings (ScriptableSingleton under ProjectSettings) for shared settings.
    /// </remarks>
    internal static class StationeersExporterUserPreferences
    {
        // EditorPrefs key names
        private const string ExportFolderKey = "StationeersExport_Folder";
        private const string RunnerEnabledKey = "StationeersRunner_Enabled";
        private const string RunnerExeOverrideKey = "StationeersRunner.ExeOverride";
        private const string AutoIncrementBuildVersionKey = "StationeersExport_AutoIncrementBuild";

        /// <summary>
        /// Gets or sets the export output folder.
        /// </summary>
        /// <remarks>
        /// Stored in EditorPrefs under key "StationeersExport_Folder".
        /// This should be a path that your exporter will write output to.
        /// An empty string means "not configured".
        /// </remarks>
        public static string ExportFolder
        {
            get => EditorPrefs.GetString(ExportFolderKey, string.Empty);
            set => EditorPrefs.SetString(ExportFolderKey, value ?? string.Empty);
        }

        /// <summary>
        /// Gets or sets whether the Stationeers runner auto-launch behavior is enabled.
        /// </summary>
        /// <remarks>
        /// Stored in EditorPrefs under key "StationeersRunner_Enabled".
        /// The runner should respect this flag in TryRunStationeers().
        /// </remarks>
        public static bool RunnerEnabled
        {
            get => EditorPrefs.GetBool(RunnerEnabledKey, false);
            set => EditorPrefs.SetBool(RunnerEnabledKey, value);
        }

        /// <summary>
        /// Gets or sets an override path to rocketstation.exe used by the Stationeers runner.
        /// </summary>
        /// <remarks>
        /// Stored in EditorPrefs under key "StationeersRunner.ExeOverride".
        /// An empty string means "no override".
        /// </remarks>
        public static string RunnerExeOverride
        {
            get => EditorPrefs.GetString(RunnerExeOverrideKey, string.Empty);
            set => EditorPrefs.SetString(RunnerExeOverrideKey, value ?? string.Empty);
        }

        /// <summary>
        /// Clears the runner executable override preference.
        /// </summary>
        /// <remarks>
        /// This removes the key entirely from EditorPrefs, rather than setting it to an empty string.
        /// </remarks>
        public static void ClearRunnerExeOverride()
        {
            EditorPrefs.DeleteKey(RunnerExeOverrideKey);
        }

        /// <summary>
        /// Gets or sets whether the exporter should auto-increment the build version during export.
        /// </summary>
        /// <remarks>
        /// Stored in EditorPrefs under key "StationeersExport_AutoIncrementBuild".
        /// This is a user preference because some team members may want different behavior locally.
        /// </remarks>
        public static bool AutoIncrementBuild
        {
            get => EditorPrefs.GetBool(AutoIncrementBuildVersionKey, false);
            set => EditorPrefs.SetBool(AutoIncrementBuildVersionKey, value);
        }
    }
}