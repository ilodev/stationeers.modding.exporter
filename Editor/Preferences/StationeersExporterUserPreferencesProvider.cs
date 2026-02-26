using System.IO;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    public static class StationeersExporterUserPreferencesProvider
    {
        private static bool _exportFoldout = true;
        private static bool _runnerFoldout = true;

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Preferences/Stationeers Modding Exporter", SettingsScope.User)
            {
                label = "Stationeers Modding Exporter",
                guiHandler = _ =>
                {
                    EditorGUILayout.LabelField("User preferences for the exporter package, shared across all Stationeers modding projects.", EditorStyles.boldLabel);
                    EditorGUILayout.Space(6);
                    DrawExportFolderPrefs();
                    EditorGUILayout.Space(6);
                    DrawRunnerPrefs();
                }
            };
        }

        // Optional helper you can call from your build/export code later:
        public static string GetExportFolderOrEmpty() =>
            StationeersExporterUserPreferences.ExportFolder;

        private static void DrawExportFolderPrefs()
        {
            _exportFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_exportFoldout, "Export Options");
            if (_exportFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string exportFolder = StationeersExporterUserPreferences.ExportFolder;

                    EditorGUILayout.LabelField("Mods Export Folder", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Used as the default export destination if set.", EditorStyles.miniLabel);
                    EditorGUILayout.Space(6);

                    // Path field + buttons
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        exportFolder = EditorGUILayout.TextField(exportFolder);
                        if (EditorGUI.EndChangeCheck())
                        {
                            StationeersExporterUserPreferences.ExportFolder = exportFolder?.Trim() ?? string.Empty;
                        }

                        if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                        {
                            // Start browsing from current value if valid, otherwise project folder
                            var start = Directory.Exists(exportFolder) ? exportFolder : Directory.GetParent(Application.dataPath)?.FullName;
                            string picked = EditorUtility.OpenFolderPanel("Select Mods Export Folder", start ?? "", "");
                            if (!string.IsNullOrEmpty(picked))
                            {
                                StationeersExporterUserPreferences.ExportFolder = picked;
                                exportFolder = picked;
                                GUI.FocusControl(null);
                            }
                        }
                    }

                    // Secondary actions row
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(exportFolder) || !Directory.Exists(exportFolder)))
                        {
                            if (GUILayout.Button("Open destination folder", GUILayout.Height(22)))
                                EditorUtility.RevealInFinder(exportFolder);
                        }

                        if (GUILayout.Button("Clear", GUILayout.Width(80), GUILayout.Height(22)))
                        {
                            StationeersExporterUserPreferences.ClearExportFolder();
                            exportFolder = string.Empty;
                            GUI.FocusControl(null);
                        }
                    }

                    EditorGUILayout.Space(6);

                    if (string.IsNullOrEmpty(exportFolder))
                        EditorGUILayout.HelpBox("No export folder set. The build/export step will ask you to choose one.", MessageType.Info);
                    else if (!Directory.Exists(exportFolder))
                        EditorGUILayout.HelpBox("This folder does not exist (or is not accessible). Pick another folder or create it.", MessageType.Warning);
                    else
                        EditorGUILayout.HelpBox("Export folder is set and will be used as the default destination when exporting any mod.", MessageType.None);


                    EditorGUILayout.Space(6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool autoIncrementBuild = StationeersExporterUserPreferences.AutoIncrementBuild;

                        EditorGUI.BeginChangeCheck();
                        autoIncrementBuild = EditorGUILayout.Toggle("Auto-increment Build", autoIncrementBuild);
                        if (EditorGUI.EndChangeCheck())
                        {
                            StationeersExporterUserPreferences.AutoIncrementBuild = autoIncrementBuild;
                        }
                    }

                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
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
                    enabled = EditorGUILayout.Toggle("Enable running the game", enabled);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Debug.Log($"Autorun the game on build: {enabled}");
                        StationeersExporterUserPreferences.RunnerEnabled = enabled;
                    }

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

                    if (GUILayout.Button("Run Stationeers"))
                        StationeersRunner.RunStationeers();

                    //EditorGUILayout.Space(6);
                    //EditorGUILayout.HelpBox("These preferences are per-user/per-machine (not saved in the project).", MessageType.Info);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}