using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{

    [InitializeOnLoad]
    public static class BuildButtonHook
    {
        static BuildButtonHook()
        {
            // Register once on editor load
            BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuildButtonPressed);
        }

        // This is called for BOTH "Build" and "Build And Run"
        private static void OnBuildButtonPressed(BuildPlayerOptions options)
        {
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



            // If you STILL want Unity to actually build afterwards, call:
            // var report = BuildPipeline.BuildPlayer(options);
            // Debug.Log("Build result: " + report.summary.result);
        }


        public static class MyBuildRunner
        {
            public static void Run(bool isBuildAndRun, BuildPlayerOptions incoming)
            {
                if (isBuildAndRun)
                    // After your build completes:
                    StationeersRunner.RunStationeers();
            }
        }
    }
}
