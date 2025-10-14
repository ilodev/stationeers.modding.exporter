using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
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
            bool isBuildAndRun = (options.options & BuildOptions.AutoRunPlayer) != 0;

            // >>> Your code here <<<
            // Run whatever you want before/after or *instead of* Unity's default build.
            // Example: call your own static method
            MyBuildRunner.Run(isBuildAndRun, options);

            if (!ExportPreflight.SaveAllWithPrompts())
                return; // user canceled or something failed to save

            // Start/await a compile pass and get the result
            bool hadErrors = CompileMonitor.LastPassHadErrors;

            if (hadErrors)
                Debug.LogError($"Last build had errors, exporting stopped");
            else 
                LaunchPadExport.Export(options);


            // If you STILL want Unity to actually build afterwards, call:
            // var report = BuildPipeline.BuildPlayer(options);
            // Debug.Log("Build result: " + report.summary.result);
        }


        public static class MyBuildRunner
        {
            public static void Run(bool isBuildAndRun, BuildPlayerOptions incoming)
            {
                // Put your custom logic here: versioning, preflight checks, CI triggers, etc.
                Debug.Log(isBuildAndRun ? "Intercepted Build And Run" : "Intercepted Build");


                // Example: do something, then decide whether to hand back to Unity:
                // BuildPipeline.BuildPlayer(incoming); // optional
            }
        }
    }
}
