// DevelopmentBuildDefineSync.cs
// Mirrors the "Development Build" checkbox to a scripting define symbol.

using UnityEditor;

namespace stationeers.modding.exporter
{

    [InitializeOnLoad]
    public static class DevelopmentBuildDefineSync
    {
        // Change this if you want a different symbol name
        private const string Define = "DEVELOPMENT_BUILD";

        // If true, we update ALL groups when the flag changes.
        // If false, we only update the currently selected build target group.
        private const bool SyncAllGroups = false;

        private static bool _lastDev;
        private static BuildTargetGroup _lastGroup;

        static DevelopmentBuildDefineSync()
        {
            _lastDev = EditorUserBuildSettings.development;
            _lastGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            EditorApplication.update += Tick;
            // Initial sync on load (once)
            Sync(currentOnly: !SyncAllGroups);
        }

        private static void Tick()
        {
            bool dev = EditorUserBuildSettings.development;
            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;

            if (dev != _lastDev || group != _lastGroup)
            {
                _lastDev = dev;
                _lastGroup = group;
                Sync(currentOnly: !SyncAllGroups);
            }
        }

        private static void Sync(bool currentOnly)
        {
            if (currentOnly)
            {
                var g = EditorUserBuildSettings.selectedBuildTargetGroup;
                if (g != BuildTargetGroup.Unknown)
                    SetDefineForGroup(g, EditorUserBuildSettings.development);
                return;
            }

            // Update all "real" groups (skip Unknown and obsolete)
            var groups = (BuildTargetGroup[])System.Enum.GetValues(typeof(BuildTargetGroup));
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g == BuildTargetGroup.Unknown) continue;
                // You can add more filters if some groups are irrelevant to your project
                SetDefineForGroup(g, EditorUserBuildSettings.development);
            }
        }

        private static void SetDefineForGroup(BuildTargetGroup group, bool enabled)
        {
            // Read current defines
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group) ?? string.Empty;

            // Quick membership check (whole token match)
            bool has = HasToken(defines, Define);

            if (enabled && !has)
            {
                // Add token
                defines = AppendToken(defines, Define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
                // Optional: Debug.Log($"[DevDefine] Added {Define} to {group}");
            }
            else if (!enabled && has)
            {
                // Remove token
                defines = RemoveToken(defines, Define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
                // Optional: Debug.Log($"[DevDefine] Removed {Define} from {group}");
            }
        }

        // Helpers for semicolon-separated define strings

        private static bool HasToken(string list, string token)
        {
            if (string.IsNullOrEmpty(list)) return false;
            var parts = list.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Trim() == token) return true;
            }
            return false;
        }

        private static string AppendToken(string list, string token)
        {
            if (string.IsNullOrEmpty(list)) return token;
            // Avoid duplicates
            if (HasToken(list, token)) return list;
            return list.EndsWith(";") ? list + token : list + ";" + token;
        }

        private static string RemoveToken(string list, string token)
        {
            if (string.IsNullOrEmpty(list)) return string.Empty;
            var parts = list.Split(';');
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (p.Length == 0) continue;
                if (p == token) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(p);
            }
            return sb.ToString();
        }

        // Menu: manual sync (current group)
        [MenuItem("Tools/Build/Sync Development Build Define (current group)")]
        private static void MenuSyncCurrent()
        {
            Sync(currentOnly: true);
        }

        // Menu: manual sync (all groups)
        [MenuItem("Tools/Build/Sync Development Build Define (all groups)")]
        private static void MenuSyncAll()
        {
            Sync(currentOnly: false);
        }
    }
}
