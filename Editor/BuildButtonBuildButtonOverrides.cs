using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    [InitializeOnLoad]
    public static class BuildButtonOverrides
    {
        //TODO: Probably move all keys to a single class
        private const string ExportFolderKey = "StationeersExport_Folder";

        static BuildButtonOverrides()
        {
            // Register once on editor
            BuildPlayerWindow.RegisterGetBuildPlayerOptionsHandler(GetBuildPlayerOptions);
            BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuildButtonPressed);
        }

        /// <summary>
        /// Used to override the output folder so it doesn't keep asking for destination and
        /// we can use a custom user preference between all projects.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        private static BuildPlayerOptions GetBuildPlayerOptions(BuildPlayerOptions options)
        {

            string exportFolder = EditorPrefs.GetString(ExportFolderKey, string.Empty);

            if (string.IsNullOrEmpty(exportFolder) || !Directory.Exists(exportFolder))
            {
                // Prompt once, store for future projects
                var start = Directory.GetParent(Application.dataPath)?.FullName ?? "";
                string picked = EditorUtility.OpenFolderPanel("Select Export Folder", start, "");
                if (string.IsNullOrEmpty(picked))
                {
                    // User cancelled: return options unchanged.
                    // OnBuildButtonPressed can still decide to abort if locationPathName is unusable.
                    return options;
                }

                exportFolder = picked;
                EditorPrefs.SetString(ExportFolderKey, exportFolder);
            }

            // IMPORTANT: locationPathName is usually a full *file* path for the built player (e.g. .../MyGame.exe)
            // For Windows Standalone, it must include the .exe.
            options.locationPathName = ComposePlayerLocation(options, exportFolder);

            return options;
        }

        public static string Sanitize(string part)
        {
            if (string.IsNullOrEmpty(part))
                return "Unnamed";

            // Replace invalid chars with underscores
            string clean = Regex.Replace(part, @"[^A-Za-z0-9_]", "_");

            // Remove leading chars until a letter or underscore
            clean = Regex.Replace(clean, @"^[^A-Za-z_]+", "");

            // Split on underscores/spaces, preserve inner casing
            clean = string.Concat(
                clean
                    .Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w =>
                    {
                        if (w.Length == 0) return "";
                        if (w.Length == 1) return w.ToUpper();
                        return char.ToUpper(w[0]) + w.Substring(1);
                    })
            );

            return string.IsNullOrEmpty(clean) ? "Unnamed" : clean;
        }

        /// <summary>
        /// Completely unnecessary, but kept inline with Unity expectations. We only need the folder
        /// </summary>
        /// <param name="options"></param>
        /// <param name="exportFolder"></param>
        /// <returns></returns>
        private static string ComposePlayerLocation(BuildPlayerOptions options, string exportFolder)
        {
            // Use Unity's productName for default exe name
            string product = PlayerSettings.productName;
            if (string.IsNullOrEmpty(product))
                product = "StationeersModdingExporter";

            // Ignore options.target and always return the sanitized version of the output path + product name
            return Path.Combine(exportFolder, Sanitize(product));
        }

        /// <summary>
        /// This is called for BOTH "Build" and "Build And Run" 
        /// </summary>
        /// <param name="options"></param>
        private static void OnBuildButtonPressed(BuildPlayerOptions options)
        {
            Debug.Log($"Saving at {options.locationPathName}");
            if (!ExportPreflight.SaveAllWithPrompts())
                return; // user canceled or something failed to save

            // Start/await a compile pass and get the result
            bool hadErrors = CompileMonitor.LastPassHadErrors;

            if (hadErrors)
            {
                Debug.LogError($"Last build had errors, exporting stopped");
                return;
            }
            else
                LaunchPadExport.Export(options);

            bool isBuildAndRun = (options.options & BuildOptions.AutoRunPlayer) != 0;

            // Run game here
            MyBuildRunner.Run(isBuildAndRun, options);
        }

        /// <summary>
        /// Runner class to Run the game
        /// </summary>
        public static class MyBuildRunner
        {
            public static void Run(bool isBuildAndRun, BuildPlayerOptions incoming)
            {
                // After your build completes:
                if (isBuildAndRun)
                    StationeersRunner.TryRunStationeers();
            }
        }
    }
}
