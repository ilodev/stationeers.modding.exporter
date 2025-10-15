using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace stationeers.modding.exporter
{
    public class AsmDef
    {
        public bool allowUnsafeCode;
        public bool autoReferenced;
        public string[] defineConstraints;
        public string name;
        public string[] optionalUnityReferences;
        public bool overrideReferences;
        public string[] precompiledReferences;
        public string[] references;

        [MenuItem("Tools/Setup/Default Assembly")]
        public static void CreateDefaultAssembly()
        {
            string productName = LaunchPadExport.Sanitize(PlayerSettings.productName);

            // don't overwrite existing files.
            string path = Path.Combine(Application.dataPath, $"{productName}.asmdef");
            if (File.Exists(path))
                return;

            CreateAsmdef(
                "Assets", // Default relative to Assets
                productName,
                productName,
                new List<string> { "Unity.TextMeshPro"},
                new List<string> { "Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll", "BepInEx.dll", "0Harmony.dll", "RW.RocketNet.dll", "LaunchPadBooster.dll" }, 
                new List<string> { "STATIONEERS_DLL_PRESENT" }
            );
        }


        public static void CreateAsmdef(string folderPath, string assemblyName, string nameSpace, List<string> references, List<string> precompiled, List<string> constraints)
        {

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"Folder not found: {folderPath}");
                return;
            }

            VersionDefine verCheck = new VersionDefine {
                name = "stationeers.modding.assemblies",
                expression = "1.0.0",
                define = "STATIONEERS_DLL_PRESENT"
            };


            // Define the assembly definition structure
            var asmDef = new AssemblyDefinitionData
            {
                name = assemblyName,
                rootNamespace = nameSpace,
                references = references?.ToArray() ?? new string[0],
                includePlatforms = new string[1] { "Editor" },
                excludePlatforms = new string[0],
                allowUnsafeCode = false,
                autoReferenced = true,
                overrideReferences = true,
                precompiledReferences = precompiled?.ToArray() ?? new string[0],
                defineConstraints = constraints?.ToArray() ?? new string[0],
                versionDefines = new VersionDefine[1] { verCheck },
                noEngineReferences = false
            };

            // Convert to JSON
            string json = JsonUtility.ToJson(asmDef, true);

            // Write to file
            string filePath = Path.Combine(folderPath, $"{assemblyName}.asmdef");
            File.WriteAllText(filePath, json);


            // Import that specific asset; fall back to full refresh if needed
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            if (!AssetDatabase.GUIDFromAssetPath(filePath).Empty())
            {
                // good
            }
            else
            {
                AssetDatabase.Refresh(); // last resort
            }

            // Trigger script compilation so the new asmdef is picked up immediately
            CompilationPipeline.RequestScriptCompilation();
        }

        // Internal representation of Unity's asmdef JSON structure
        [System.Serializable]
        private class AssemblyDefinitionData
        {
            public string name;
            public string rootNamespace;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public bool allowUnsafeCode;
            public bool autoReferenced;
            public bool overrideReferences;
            public string[] precompiledReferences;
            public string[] defineConstraints;
            public VersionDefine[] versionDefines;
            public bool noEngineReferences;
        }

        [System.Serializable]
        private class VersionDefine
        {
            public string name;
            public string expression;
            public string define;
        }
    }
}