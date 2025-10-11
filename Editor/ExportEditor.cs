using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{

    class ExportEditor
    {

        private readonly AssemblyEditor _assemblyEditor = new AssemblyEditor();

        public class ExportValidationError : Exception
        {
            public ExportValidationError(string message) : base(message)
            {
            }
        }

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
                settings.Tags = EditorGUILayout.TextField(
                    new GUIContent("Tags:", "List of comma separated tags to be added to the mod Steam information."),
                    settings.Tags);
                GUI.enabled = false;
                settings.Tags = EditorGUILayout.TextField("Workshop Handle:", settings.WorkshopHandle);
                GUI.enabled = true;
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
            GUI.enabled = false;
            DrawSection(() => {
                EditorGUILayout.LabelField("Project Assets", EditorStyles.boldLabel);
                DrawContentSelector(settings);
            });
            GUI.enabled = true;
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

        private void DrawAssembliesInline(ExportSettings settings)
        {
            DrawSection(() =>
            {
                EditorGUILayout.LabelField("Assembly definitions", EditorStyles.boldLabel);

                // reuse your AssemblyEditor’s help/candidates API
                _assemblyEditor.DrawHelpBox();

                var candidates = _assemblyEditor.GetCandidates(settings)
                    .Distinct()
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (candidates.Count == 0)
                {
                    EditorGUILayout.HelpBox("No Assembly Definition Assets found in this project.", MessageType.Info);
                    if (GUILayout.Button("Refresh"))
                        _assemblyEditor.ClearCandidates();
                    return;
                }

                // Display names for the dropdown (nice names from paths)
                var display = candidates
                    .Select(p => Path.GetFileNameWithoutExtension(p))
                    .ToArray();

                // Work on a list for add/remove convenience
                var selections = (settings.Assemblies ?? Array.Empty<string>()).ToList();

                for (int i = 0; i < selections.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    // find the current index in candidates; fallback to first
                    var currentIdx = Math.Max(0, candidates.IndexOf(selections[i]));
                    var newIdx = EditorGUILayout.Popup("", currentIdx, display);
                    selections[i] = candidates[newIdx];

                    if (GUILayout.Button("-", GUILayout.Width(24)))
                    {
                        selections.RemoveAt(i);
                        settings.Assemblies = selections.ToArray();
                        GUIUtility.ExitGUI(); // avoid layout issues after removal
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Assembly", GUILayout.Width(120)))
                {
                    // add the first unused candidate (or first if all used)
                    var firstUnused = candidates.FirstOrDefault(c => !selections.Contains(c));
                    selections.Add(firstUnused ?? candidates[0]);
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(90)))
                {
                    _assemblyEditor.ClearCandidates();
                }
                EditorGUILayout.EndHorizontal();

                // write back to settings
                settings.Assemblies = selections.ToArray();

                // (Optional) validation if you require at least one assembly:
                // if (settings.Assemblies.Length == 0)
                //     throw new ExportValidationError("You must select at least one assembly definition.");
            });
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
            DrawAssembliesInline(settings);
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
