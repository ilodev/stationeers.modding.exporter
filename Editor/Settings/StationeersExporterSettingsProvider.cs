using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    public static class StationeersExporterSettingsProvider
    {
        private static bool _sanityFoldout = true;
        private static bool _aboutFoldout = true;
        private static bool _exportFoldout = true;
        private static bool _runnerFoldout = false;

        // create toggles (only meaningful if missing)
        private static bool _createAbout = true;
        private static bool _createGameData = true;
        private static bool _createAsmDef = true;
        private static bool _createEntryScript = true;

        // Mod sanity
        private static bool _sanityCached;
        private static bool _hasAboutFolder, _hasAboutXml, _hasGameData, _hasAsmDef, _hasEntryScript;

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

                    DrawSanitySection();
                    EditorGUILayout.Space(8);

                    DrawExportSection();
                    EditorGUILayout.Space(8);

                    DrawAboutSection();
                    EditorGUILayout.Space(8);

                    DrawRunnerSection();

                    // Disabled temporarily
                    //EditorGUILayout.Space(8);
                    //DrawUtilitiesSection();
                }
            };
        }

        [InitializeOnLoadMethod]
        private static void HookProjectChange()
        {
            EditorApplication.projectChanged += () =>
            {
                _sanityCached = false; // mark dirty; refresh next draw
            };
        }

        private static void RefreshSanityCache()
        {
            _hasAboutFolder = AssetDatabase.IsValidFolder("Assets/About");
            _hasAboutXml = File.Exists("Assets/About/About.xml") || File.Exists("Assets/About/about.xml");
            _hasGameData = AssetDatabase.IsValidFolder("Assets/GameData");

            // Expensive calls: do them only here
            // Replace with looking specifically for the one asmdef
            _hasAsmDef = AssetUtility.GetAssets("t:AssemblyDefinitionAsset").Count > 0;
            _hasEntryScript = HasEntryPointScript();

            _sanityCached = true;
        }

        private static void DrawSanitySection()
        {
            int missing =
                (_hasAboutFolder ? 0 : 1) +
                (_hasAboutXml ? 0 : 1) +
                (_hasGameData ? 0 : 1) +
                (_hasAsmDef ? 0 : 1) +
                (_hasEntryScript ? 0 : 1);
            if (missing > 0)
                _sanityFoldout = true;
            else
                _sanityFoldout = false;

            _sanityFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_sanityFoldout, $"Mod sanity check passed: {!_sanityFoldout}");
            if (_sanityFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    /*
                    // --- checks ---
                    bool hasAboutFolder = AssetDatabase.IsValidFolder("Assets/About");
                    bool hasAboutXml = File.Exists("Assets/About/About.xml") || File.Exists("Assets/About/about.xml");
                    bool hasGameData = AssetDatabase.IsValidFolder("Assets/GameData");
                    bool hasAsmDef = AssetUtility.GetAssets("t:AssemblyDefinitionAsset").Count > 0;
                    bool hasEntryScript = HasEntryPointScript();
                    */
                    // Summary
                    if (missing == 0)
                        EditorGUILayout.HelpBox("All required mod items are present.", MessageType.Info);
                    else
                        EditorGUILayout.HelpBox($"{missing} required item(s) are missing. Select what to create and click 'Create Selected'.", MessageType.Warning);

                    EditorGUILayout.Space(6);

                    // Rows
                    DrawSanityRow("Assets/About folder", _hasAboutFolder, ref _createAbout);
                    DrawSanityRow("Assets/About/About.xml", _hasAboutXml, ref _createAbout);
                    DrawSanityRow("Assets/GameData folder", _hasGameData, ref _createGameData);
                    DrawSanityRow("Assembly Definition (.asmdef)", _hasAsmDef, ref _createAsmDef);
                    DrawSanityRow("Entry point script", _hasEntryScript, ref _createEntryScript);

                    EditorGUILayout.Space(8);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(missing == 0))
                        {
                            if (GUILayout.Button("Create Selected"))
                            {
                                CreateSelected(_hasAboutFolder, _hasAboutXml, _hasGameData, _hasAsmDef, _hasEntryScript);
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawSanityRow(string label, bool present, ref bool createToggle)
        {

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!_sanityCached)
                    RefreshSanityCache();

                if (GUILayout.Button("Refresh"))
                {
                    RefreshSanityCache();
                    // also update the default toggle selections if you do that
                }

                EditorGUILayout.LabelField(label);

                if (!present)
                {
                    createToggle = EditorGUILayout.ToggleLeft("Create", createToggle, GUILayout.Width(70));
                }
                else
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ToggleLeft("Create", false, GUILayout.Width(70));
                }
            }
            EditorGUILayout.Space(2);
        }

        private static void CreateSelected(bool hasAboutFolder, bool hasAboutXml, bool hasGameData, bool hasAsmDef, bool hasEntryScript)
        {
            // About
            if ((!hasAboutFolder || !hasAboutXml) && _createAbout)
                AssetUtility.CreateDefaultAbout();

            // GameData folder - add a helper (see note below)
            if (!hasGameData && _createGameData)
                AssetUtility.CreateDefaultGameDataFolder();

            // asmdef
            if (!hasAsmDef && _createAsmDef)
                AssemblyDefinitionUtil.CreateDefaultAssembly();

            // entry script
            if (!hasEntryScript && _createEntryScript)
                AssetUtility.CreateDefaultScript();

            AssetDatabase.Refresh();
            Debug.Log("Sanity check: create selected completed.");
        }

        private static bool HasEntryPointScript()
        {
            string productName = StationeersModdingExport.Sanitize(PlayerSettings.productName);
            string expected = $"Assets/Scripts/{productName}.cs";

            if (File.Exists(expected))
                return true;

            // fallback: any script in Assets/Scripts with same file name
            if (AssetDatabase.IsValidFolder("Assets/Scripts"))
            {
                var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path).Equals(productName, System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static void DrawAboutSection()
        {
            var s = StationeersExporterSettings.instance;

            _aboutFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_aboutFoldout, "Mod info Synchronization");
            if (_aboutFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();
                    using (new EditorGUI.DisabledScope(true))
                    {
                        s.aboutXmlPath = EditorGUILayout.TextField("About.xml Path", s.aboutXmlPath);
                        //s.aboutWriteUtf8Bom = EditorGUILayout.Toggle(new GUIContent("Write UTF-8 BOM"), s.aboutWriteUtf8Bom);
                        EditorGUILayout.Space(4);
                    }

                    s.aboutAutoSyncBoth = EditorGUILayout.Toggle(new GUIContent("Auto-sync mod info"), s.aboutAutoSyncBoth);
                    //hidding these settings on purpose.
                    //s.aboutAutoSyncPlayerToXml = EditorGUILayout.Toggle(new GUIContent("Auto-sync PlayerSettings → About.xml"), s.aboutAutoSyncPlayerToXml);
                    //s.aboutAutoSyncXmlToPlayer = EditorGUILayout.Toggle(new GUIContent("Auto-sync About.xml → PlayerSettings"), s.aboutAutoSyncXmlToPlayer);

                    EditorGUILayout.Space(8);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Sync PlayerSettings → About.xml"))
                            AboutXmlPlayerSettingsWatcher.SyncNow(force: true);

                        if (GUILayout.Button("Apply About.xml → PlayerSettings"))
                            AboutXmlPostprocessor.ApplyNow();
                    }

                    if (EditorGUI.EndChangeCheck())
                        s.SaveNow();

                    EditorGUILayout.Space(6);
                    if (s.aboutAutoSyncBoth)
                        EditorGUILayout.HelpBox("PlayerSettings (Product/Company/Version) and About.xml will be synchronized automatically.", MessageType.None);
                    else
                        EditorGUILayout.HelpBox("PlayerSettings and About.xml will not be synchronized automatically.", MessageType.Warning);
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
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawRunnerSection()
        {
            var s = StationeersExporterSettings.instance;

            _runnerFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_runnerFoldout, "Stationeers runner Settings");
            if (_runnerFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Project specific arguments when running the game", EditorStyles.boldLabel);
                    EditorGUILayout.Space(2);

                    EditorGUI.BeginChangeCheck();
                    s.RunnerProjectArguments = EditorGUILayout.TextField("Stationeers arguments", s.RunnerProjectArguments);
                    if (EditorGUI.EndChangeCheck())
                        s.SaveNow();

                    EditorGUILayout.Space(6);
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
