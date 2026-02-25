using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    public static class StationeersExporterUserPreferencesProvider
    {
        private static bool _runnerFoldout = true;

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Preferences/Stationeers Exporter", SettingsScope.User)
            {
                label = "Stationeers Exporter",
                guiHandler = _ =>
                {
                    EditorGUILayout.LabelField("Stationeers Exporter (User Prefs)", EditorStyles.boldLabel);
                    EditorGUILayout.Space(6);

                    DrawRunnerPrefs();
                }
            };
        }

        private static void DrawRunnerPrefs()
        {
            _runnerFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_runnerFoldout, "Stationeers Runner");
            if (_runnerFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool enabled = StationeersExporterUserPreferences.RunnerEnabled;
                    string overridePath = StationeersExporterUserPreferences.RunnerExeOverride;

                    EditorGUI.BeginChangeCheck();
                    enabled = EditorGUILayout.Toggle("Enable Run", enabled);
                    if (EditorGUI.EndChangeCheck())
                        StationeersExporterUserPreferences.RunnerEnabled = enabled;

                    EditorGUI.BeginChangeCheck();
                    overridePath = EditorGUILayout.TextField("rocketstation.exe override", overridePath);
                    if (EditorGUI.EndChangeCheck())
                        StationeersExporterUserPreferences.RunnerExeOverride = overridePath;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Browse…"))
                        {
                            string picked = EditorUtility.OpenFilePanel("Locate rocketstation.exe", "", "exe");
                            if (!string.IsNullOrEmpty(picked))
                                StationeersExporterUserPreferences.RunnerExeOverride = picked;
                        }

                        if (GUILayout.Button("Clear"))
                        {
                            StationeersExporterUserPreferences.ClearRunnerExeOverride();
                        }
                    }

                    EditorGUILayout.Space(8);

                    using (new EditorGUI.DisabledScope(!enabled))
                    {
                        if (GUILayout.Button("Run Stationeers"))
                            StationeersRunner.RunStationeers();
                    }

                    EditorGUILayout.Space(6);
                    EditorGUILayout.HelpBox(
                        "These preferences are per-user/per-machine (not saved in the project).",
                        MessageType.Info);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}