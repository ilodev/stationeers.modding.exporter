using System.Linq;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    public static class StationeersVersioning
    {
        /// <summary>
        /// Increments the LAST numeric component of PlayerSettings.bundleVersion.
        /// Examples:
        /// 1        -> 1.1
        /// 1.0      -> 1.1
        /// 1.0.3    -> 1.0.4
        /// 1.2.9    -> 1.2.10
        /// Preserves suffixes like "-beta".
        /// </summary>
        public static bool IncrementBuildVersion(out string oldVersion, out string newVersion)
        {

            oldVersion = PlayerSettings.bundleVersion?.Trim();
            newVersion = oldVersion;

            if (string.IsNullOrEmpty(oldVersion))
                oldVersion = "1";

            var settings = StationeersExporterSettings.instance;
            bool needsToUpdate = settings.aboutAutoSyncPlayerToXml || settings.aboutAutoSyncBoth;
            if (!needsToUpdate)
                return false;

            // Split suffix (-beta, +meta, etc.)
            string numericPart = oldVersion;
            string suffix = "";

            int suffixIndex = oldVersion.IndexOfAny(new[] { '-', '+' });
            if (suffixIndex >= 0)
            {
                numericPart = oldVersion.Substring(0, suffixIndex);
                suffix = oldVersion.Substring(suffixIndex);
            }

            var parts = numericPart.Split('.').ToList();

            // Ensure at least two components so "1" -> "1.1"
            if (parts.Count == 1)
                parts.Add("0");

            int lastIndex = parts.Count - 1;

            if (!int.TryParse(parts[lastIndex], out int value))
            {
                Debug.LogWarning($"Cannot auto-increment bundleVersion '{oldVersion}'. Last component is not numeric.");
                return false;
            }

            value += 1;
            parts[lastIndex] = value.ToString();

            newVersion = string.Join(".", parts) + suffix;
            PlayerSettings.bundleVersion = newVersion;

            // Update about.xml if enabled
            TryUpdateAboutXml(settings, newVersion);
            Debug.Log($"Build version incremented: {oldVersion} ? {newVersion}");
            return true;
        }


        /// <summary>
        /// Increments PlayerSettings.bundleVersion minor component (major.minor.patch),
        /// resets patch to 0, and (optionally) propagates to about.xml.
        /// </summary>
        public static bool IncrementMinorAndPropagate(out string oldVersion, out string newVersion)
        {
            oldVersion = PlayerSettings.bundleVersion?.Trim() ?? "0.0.0";

            if (!TryParseSemVer3(oldVersion, out int major, out int minor, out int patch, out string suffix))
            {
                Debug.LogWarning($"Could not parse bundleVersion '{oldVersion}'. Expected something like '1.2.3' (optional suffix like '-beta'). No version bump performed.");
                newVersion = oldVersion;
                return false;
            }

            minor += 1;
            patch = 0;

            newVersion = $"{major}.{minor}.{patch}{suffix}";

            // Update Unity project version
            PlayerSettings.bundleVersion = newVersion;
            Debug.Log($"Version bumped: {oldVersion} -> {newVersion}");

            // Update about.xml if enabled
            var settings = StationeersExporterSettings.instance;
            bool needsToUpdate = settings.aboutAutoSyncPlayerToXml || settings.aboutAutoSyncBoth;
            if (settings == null || needsToUpdate)
                TryUpdateAboutXml(settings, newVersion);

            return true;
        }

        /// <summary>
        /// Parses "major.minor.patch" with optional suffix (e.g., "1.2.3-beta").
        /// Also tolerates "1.2" by treating patch as 0; "1" becomes 1.0.0.
        /// </summary>
        private static bool TryParseSemVer3(string version, out int major, out int minor, out int patch, out string suffix)
        {
            major = minor = patch = 0;
            suffix = "";

            if (string.IsNullOrWhiteSpace(version))
                return false;

            // Preserve suffix like "-beta" or "+meta" if present.
            // Split at first '-' or '+' (common semver suffix separators)
            int cut = version.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0)
            {
                suffix = version.Substring(cut);
                version = version.Substring(0, cut);
            }

            var parts = version.Split('.').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            if (parts.Length == 0) return false;

            bool ok = int.TryParse(parts[0], out major);
            if (!ok) return false;

            if (parts.Length >= 2)
            {
                ok = int.TryParse(parts[1], out minor);
                if (!ok) return false;
            }
            else minor = 0;

            if (parts.Length >= 3)
            {
                ok = int.TryParse(parts[2], out patch);
                if (!ok) return false;
            }
            else patch = 0;

            // Ignore any extra components (e.g. 1.2.3.4) rather than failing
            return true;
        }

        private static void TryUpdateAboutXml(StationeersExporterSettings settings, string newVersion)
        {
            AboutXmlPlayerSettingsWatcher.SyncNow(force: true);
        }

        
    }
}
