using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Project Settings UI for the package.
    /// Splits configuration into:
    /// - Project settings (shared): StationeersExporterSettings (export folders, About.xml sync)
    /// - User prefs (local): StationeersExporterUserPrefs (runner enable + executable location)
    /// </summary>
    public static class StationeersExporterSettingsProvider
    {
        private static bool _aboutFoldout = true;
        private static bool _exportFoldout = true;
        private static bool _utilitiesFoldout = false;

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Stationeers Modding Exporter", SettingsScope.Project)
            {
                label = "Stationeers Modding Exporter",
                guiHandler = _ =>
                {
                    EditorGUILayout.LabelField("Stationeers Modding Exporter", EditorStyles.boldLabel);
                    EditorGUILayout.Space(6);

                    DrawExportSection();
                    EditorGUILayout.Space(8);
                    DrawAboutSection();
                    EditorGUILayout.Space(8);
                    DrawUtilitiesSection();
                }
            };
        }

        private static void DrawAboutSection()
        {
            var s = StationeersExporterSettings.instance;

            _aboutFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_aboutFoldout, "About.xml Sync");
            if (_aboutFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();

                    s.aboutXmlPath = EditorGUILayout.TextField("About.xml Path", s.aboutXmlPath);
                    s.aboutWriteUtf8Bom = EditorGUILayout.Toggle(new GUIContent("Write UTF-8 BOM"), s.aboutWriteUtf8Bom);

                    EditorGUILayout.Space(4);

                    s.aboutAutoSyncPlayerToXml = EditorGUILayout.Toggle(new GUIContent("Auto-sync PlayerSettings → About.xml"), s.aboutAutoSyncPlayerToXml);
                    s.aboutAutoSyncXmlToPlayer = EditorGUILayout.Toggle(new GUIContent("Auto-sync About.xml → PlayerSettings"), s.aboutAutoSyncXmlToPlayer);

                    EditorGUILayout.Space(8);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Sync PlayerSettings → About.xml"))
                            //AboutXmlPlayerSettingsWatcher.SyncNow(force: true);
                            Debug.Log("Unimplemented 1");

                        if (GUILayout.Button("Apply About.xml → PlayerSettings"))
                            //AboutXmlPostprocessor.ApplyNow();
                            Debug.Log("Unimplemented 1");
                    }

                    if (EditorGUI.EndChangeCheck())
                        s.SaveNow();

                    EditorGUILayout.Space(6);
                    EditorGUILayout.HelpBox(
                        "Keeps PlayerSettings (Product/Company/Version) and About.xml in sync. These options are project-wide.",
                        MessageType.Info);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawExportSection()
        {
            var s = StationeersExporterSettings.instance;

            _exportFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_exportFoldout, "Exporting Assets");
            if (_exportFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Folders to copy into the mod folder during the export process", EditorStyles.boldLabel);
                    EditorGUILayout.Space(2);

                    EditorGUI.BeginChangeCheck();

                    // Draw list with add/remove
                    if (s.exportFolders == null)
                        s.exportFolders = new List<string>();

                    int removeAt = -1;
                    for (int i = 0; i < s.exportFolders.Count; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            s.exportFolders[i] = EditorGUILayout.TextField($"Folder {i + 1}", s.exportFolders[i]);
                            if (GUILayout.Button("-", GUILayout.Width(24)))
                                removeAt = i;
                        }
                    }
                    if (removeAt >= 0 && removeAt < s.exportFolders.Count)
                        s.exportFolders.RemoveAt(removeAt);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Add Folder"))
                        {
                            var picked = EditorUtility.OpenFolderPanel("Pick folder under Assets/", Application.dataPath, "");
                            if (!string.IsNullOrEmpty(picked))
                            {
                                var assetsPath = ToAssetsPath(picked);
                                if (!string.IsNullOrEmpty(assetsPath))
                                    s.exportFolders.Add(assetsPath);
                                else
                                    EditorUtility.DisplayDialog("Export Folders", "Please pick a folder inside this project's Assets folder.", "OK");
                            }
                        }

                        if (GUILayout.Button("Reset Defaults"))
                        {
                            // TODO: get this list from a centralized default class.
                            s.exportFolders = new List<string> { "Assets/GameData", "Assets/About" };
                        }
                    }

                    if (EditorGUI.EndChangeCheck())
                        s.SaveNow();

                    EditorGUILayout.Space(6);
                    EditorGUILayout.HelpBox(
                        "These folders are copied into the mod output folder.", MessageType.Info);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawUtilitiesSection()
        {
            _utilitiesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_utilitiesFoldout, "Utilities");
            if (_utilitiesFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Project setup", EditorStyles.boldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Create Default Mod Setup"))
                            AssetUtility.CreateDefaultSetup();
                        if (GUILayout.Button("Create About.xml"))
                            AssetUtility.CreateDefaultAbout();
                        if (GUILayout.Button("Create Script"))
                            AssetUtility.CreateDefaultScript();
                        if (GUILayout.Button("Create .asmdef"))
                            AsmDef.CreateDefaultAssembly();
                    }

                    EditorGUILayout.Space(6);
                    EditorGUILayout.HelpBox(
                        "These actions used to be in the Tools menu; they're now available here.",
                        MessageType.None);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static string ToAssetsPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return null;

            var fullAssets = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            var fullPicked = Path.GetFullPath(absolutePath).Replace('\\', '/');

            if (!fullPicked.StartsWith(fullAssets))
                return null;

            // Convert /.../Project/Assets/SomeFolder -> Assets/SomeFolder
            var rel = "Assets" + fullPicked.Substring(fullAssets.Length);
            return rel.Replace('\\', '/');
        }
    }
}
