using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{

    class ExportEditor
    {
        private void DrawSection(Action thunk)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

            GUILayout.Space(5);

            try
            {
                thunk();
                GUILayout.Space(5);
                EditorGUILayout.EndVertical();

            } catch (ExportValidationError e)
            {
                GUILayout.Space(5);
                EditorGUILayout.EndVertical();
                throw e;
            }
        }

        private void DrawDetails(ExportSettings settings)
        {
            DrawSection(() => {
                settings.Name = EditorGUILayout.TextField("Mod name:", settings.Name);
                settings.Author = EditorGUILayout.TextField("Author:", settings.Author);
                settings.Version = EditorGUILayout.TextField("Version:", settings.Version);
                settings.Description = EditorGUILayout.TextField("Description:", settings.Description, GUILayout.Height(60));
            });

            var details = new string[] { settings.Name, settings.Author, settings.Version, settings.Description };

            if (details.Any(o => o == ""))
            {
                throw new ExportValidationError("All mod details must be specified.");
            }
        }

        private void DrawContentSelector(ExportSettings settings)
        {
            settings.ContentTypes = (ContentType)EditorGUILayout.EnumFlagsField("Included content:", settings.ContentTypes);
           
            if ((int)settings.ContentTypes == 0)
            {
                throw new ExportValidationError("You must include some content in your mod.");
            }
        }

        private void DrawPdbSelector(ExportSettings settings)
        {
            EditorGUILayout.LabelField("You can include a debug database. You might want to exclude it once you publish, since it is of no use to end users.");
            settings.IncludePdbs = EditorGUILayout.Toggle("Include Pdb's:", settings.IncludePdbs);
        }

        private void DrawContentSection(ExportSettings settings)
        {
            DrawSection(() => {
                DrawContentSelector(settings);
            });
        }
        private void DrawDirectorySelector(ExportSettings settings)
        {
            GUILayout.BeginHorizontal();

            EditorGUILayout.TextField("Output Directory*:", ExporterEditorWindow.GetShortString(settings.OutputDirectory));

            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var selectedDirectory =
                    EditorUtility.SaveFolderPanel("Choose output directory", settings.OutputDirectory, "");
                if (!string.IsNullOrEmpty(selectedDirectory))
                    settings.OutputDirectory = selectedDirectory;
            }

            GUILayout.EndHorizontal();

            if (settings.OutputDirectory == "")
            {
                throw new ExportValidationError("You must specify an output directory.");
            }
        }

        private void DrawLogSelector()
        {
            LogUtility.logLevel = (LogLevel)EditorGUILayout.EnumPopup("Log Level:", LogUtility.logLevel);
        }

        private void DrawExportOptions(ExportSettings settings)
        {
            DrawSection(() => {
                DrawLogSelector();
                DrawPdbSelector(settings);
                DrawDirectorySelector(settings);
            });
        }

        private void DrawSections(ExportSettings settings)
        {
            
            DrawAlert(settings);
            DrawDetails(settings);
            DrawContentSection(settings);
            DrawExportOptions(settings);
        }

        private static void DrawAlert(ExportSettings settings)
        {
            if (DevelopmentEditor.Patcher.DevelopmentModeEnabled == true && !settings.IncludePdbs)
            {
                EditorGUILayout.HelpBox("Development mode is enabled. Debug information needs to be exported or it will not be possible to debug your code. Make sure to tick Include PDBs.", MessageType.Warning, true);
            }
        }

        public bool Draw(ExportSettings settings)
        {
            var valid = true;

            try { DrawSections(settings); } catch (ExportValidationError e)
            {
                EditorGUILayout.HelpBox(e.Message, MessageType.Warning);
                valid = false;
            }

            return valid;
        }
    }
}
