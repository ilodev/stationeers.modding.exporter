using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Helpers for bumping the Unity project version (PlayerSettings.bundleVersion) and optionally
    /// syncing the Stationeers about.xml file when auto-sync is enabled in exporter settings.
    /// </summary>
    /// <remarks>
    /// This class does not attempt to validate or rewrite about.xml directly. Instead, it delegates
    /// to AboutXmlPlayerSettingsWatcher.SyncNow(force: true) when sync is enabled.
    ///
    /// Supported version formats:
    /// - "1" becomes "1.1" when incrementing last numeric component (ensures at least 2 parts).
    /// - "1.2" is treated as "1.2.0" by the semver parser.
    /// - "1.2.3-beta" and "1.2.3+meta" keep their suffix when bumping.
    /// </remarks>
    public static class StationeersVersioning
    {
        /// <summary>
        /// Increments the last numeric component of PlayerSettings.bundleVersion.
        /// </summary>
        /// <param name="oldVersion">Previous bundleVersion (trimmed). If empty, treated as "1".</param>
        /// <param name="newVersion">New bundleVersion after increment.</param>
        /// <returns>
        /// True if the version was incremented and written to PlayerSettings.bundleVersion; otherwise false.
        /// </returns>
        /// <remarks>
        /// Examples:
        /// - "1"       becomes "1.1"
        /// - "1.0"     becomes "1.1"
        /// - "1.0.3"   becomes "1.0.4"
        /// - "1.2.9"   becomes "1.2.10"
        /// - "1.2-beta" becomes "1.3-beta" (last numeric part bumped, suffix preserved)
        ///
        /// This method only performs the bump when exporter settings indicate that about.xml sync is enabled
        /// (aboutAutoSyncPlayerToXml or aboutAutoSyncBoth). This matches the original behavior.
        /// </remarks>
        public static bool IncrementBuildVersion(out string oldVersion, out string newVersion)
        {
            oldVersion = (PlayerSettings.bundleVersion ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(oldVersion))
                oldVersion = "1";

            newVersion = oldVersion;

            var settings = StationeersExporterSettings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[StationeersVersioning] Exporter settings not available. Version bump skipped.");
                return false;
            }

            bool shouldSyncAboutXml = settings.aboutAutoSyncPlayerToXml || settings.aboutAutoSyncBoth;
            if (!shouldSyncAboutXml)
                return false;

            SplitSuffix(oldVersion, out string numericPart, out string suffix);

            var parts = new List<string>(numericPart.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries));

            // Ensure at least two components so "1" -> "1.1".
            if (parts.Count == 1)
                parts.Add("0");

            int lastIndex = parts.Count - 1;

            if (!int.TryParse(parts[lastIndex], out int value))
            {
                Debug.LogWarning($"[StationeersVersioning] Cannot auto-increment bundleVersion '{oldVersion}'. Last component is not numeric.");
                return false;
            }

            value += 1;
            parts[lastIndex] = value.ToString();

            newVersion = string.Join(".", parts) + suffix;
            PlayerSettings.bundleVersion = newVersion;

            TrySyncAboutXml(settings, newVersion);

            Debug.Log($"[StationeersVersioning] Build version incremented: {oldVersion} -> {newVersion}");
            return true;
        }

        /// <summary>
        /// Splits a version string into a numeric part and a suffix part.
        /// </summary>
        /// <param name="version">Full version string.</param>
        /// <param name="numericPart">Portion before the first '-' or '+'.</param>
        /// <param name="suffix">Portion starting at the first '-' or '+', or empty if none.</param>
        private static void SplitSuffix(string version, out string numericPart, out string suffix)
        {
            numericPart = version;
            suffix = string.Empty;

            int cut = version.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0)
            {
                numericPart = version.Substring(0, cut);
                suffix = version.Substring(cut);
            }
        }

        /// <summary>
        /// Syncs about.xml based on the current exporter settings.
        /// </summary>
        /// <param name="settings">Exporter settings instance.</param>
        /// <param name="newVersion">The version that was written to PlayerSettings.bundleVersion.</param>
        /// <remarks>
        /// The current implementation delegates to AboutXmlPlayerSettingsWatcher and forces a sync.
        /// The newVersion parameter is kept for clarity and future use.
        /// </remarks>
        private static void TrySyncAboutXml(StationeersExporterSettings settings, string newVersion)
        {
            AboutXmlPlayerSettingsWatcher.SyncNow(force: true);
        }
    }
}