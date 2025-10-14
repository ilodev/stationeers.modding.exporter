using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
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

        public static void CreateAsmdef(string folderPath, string assemblyName, List<string> references, List<string> precompiled)
        {
            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"Folder not found: {folderPath}");
                return;
            }

            // Define the assembly definition structure
            var asmDef = new AssemblyDefinitionData
            {
                name = assemblyName,
                references = references?.ToArray() ?? new string[0],
                includePlatforms = new string[0],
                excludePlatforms = new string[0],
                allowUnsafeCode = false,
                autoReferenced = true,
                overrideReferences = true,
                precompiledReferences = precompiled?.ToArray() ?? new string[0],
                defineConstraints = new string[0],
                versionDefines = new VersionDefine[0],
                noEngineReferences = false
            };

            // Convert to JSON
            string json = JsonUtility.ToJson(asmDef, true);

            // Write to file
            string filePath = Path.Combine(folderPath, $"{assemblyName}.asmdef");
            File.WriteAllText(filePath, json);

        }

        // Internal representation of Unity's asmdef JSON structure
        [System.Serializable]
        private class AssemblyDefinitionData
        {
            public string name;
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