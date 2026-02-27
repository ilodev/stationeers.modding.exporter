using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Utilities for creating Unity Assembly Definition (.asmdef) files for a mod project.
    /// </summary>
    /// <remarks>
    /// This class writes an asmdef JSON file into the Unity project (typically under Assets/)
    /// and then imports it so Unity picks it up.
    ///
    /// Notes:
    /// - Paths passed to UnityEditor APIs (AssetDatabase) must be project-relative (for example "Assets/MyFolder").
    /// - After creating a new asmdef, Unity may need to recompile scripts. This class requests compilation.
    /// </remarks>
    public class AssemblyDefinitionUtil

    {
        private const string AsmDefExtension = ".asmdef";

        /// <summary>
        /// Creates a default asmdef for the current project product name if it does not already exist.
        /// </summary>
        /// <remarks>
        /// - The asmdef is created at the root of Assets (Assets/{ProductName}.asmdef).
        /// - The assembly name and root namespace are based on PlayerSettings.productName (sanitized).
        /// - Existing files are not overwritten.
        /// </remarks>
        public static void CreateDefaultAssembly()
        {
            string productName = StationeersModdingExport.Sanitize(PlayerSettings.productName);
            if (string.IsNullOrWhiteSpace(productName))
            {
                Debug.LogWarning("[AssemblyDefinitionUtil] PlayerSettings.productName is empty after sanitizing. Aborting asmdef creation.");
                return;
            }

            string assetFolder = "Assets";
            string assetPath = NormalizeAssetPath(Path.Combine(assetFolder, productName + AsmDefExtension));

            // Do not overwrite an existing asmdef.
            if (File.Exists(assetPath))
                return;

            CreateAsmdef(
                folderPath: assetFolder,
                assemblyName: productName,
                rootNamespace: productName,
                references: new List<string> { "Unity.TextMeshPro" },
                precompiledReferences: new List<string>
                {
                    "Assembly-CSharp.dll",
                    "Assembly-CSharp-firstpass.dll",
                    "BepInEx.dll",
                    "0Harmony.dll",
                    "Brutal.Raknet.dll",
                    "LaunchPadBooster.dll",
                    "UniTask.dll"
                },
                defineConstraints: new List<string>(),
                versionDefines: new List<VersionDefine> 
                {
                    new VersionDefine
                    {
                        name = "stationeers.modding.assemblies",
                        expression = "",
                        define = "STATIONEERS_DLL_PRESENT"
                    }
                }
            );
        }

        /// <summary>
        /// Creates an asmdef file in the specified folder and imports it into the AssetDatabase.
        /// </summary>
        /// <param name="folderPath">
        /// Project-relative folder path that must exist (for example "Assets" or "Assets/MyMod").
        /// </param>
        /// <param name="assemblyName">Assembly name written into the asmdef (and used for the file name).</param>
        /// <param name="rootNamespace">Root namespace written into the asmdef.</param>
        /// <param name="references">
        /// Optional list of assembly references (for example "Unity.TextMeshPro"). Use null for none.
        /// </param>
        /// <param name="precompiledReferences">
        /// Optional list of precompiled references (DLL file names). Use null for none.
        /// </param>
        /// <param name="defineConstraints">
        /// Optional list of define constraints. Use null for none.
        /// </param>
        /// <remarks>
        /// includePlatforms is set to:
        /// - Editor
        /// - WindowsStandalone64
        ///
        /// overrideReferences is set to true and autoReferenced is true, matching the original intent:
        /// - Your assembly will use the explicit precompiled DLL list.
        /// - Unity will auto-reference the assembly where appropriate.
        ///
        /// This method does not overwrite an existing file; if the file exists, it returns without changes.
        /// </remarks>
        public static void CreateAsmdef(
            string folderPath,
            string assemblyName,
            string rootNamespace,
            List<string> references,
            List<string> precompiledReferences,
            List<string> defineConstraints,
            List<VersionDefine> versionDefines)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("folderPath is null or empty.", nameof(folderPath));
            if (string.IsNullOrWhiteSpace(assemblyName))
                throw new ArgumentException("assemblyName is null or empty.", nameof(assemblyName));

            folderPath = NormalizeAssetPath(folderPath);

            // Ensure folder exists (AssetDatabase requires a project-relative path).
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError("[AssemblyDefinitionUtil] Folder not found: " + folderPath);
                return;
            }

            string assetPath = NormalizeAssetPath(Path.Combine(folderPath, assemblyName + AsmDefExtension));

            // Do not overwrite an existing asmdef.
            if (File.Exists(assetPath))
                return;

            var asmDef = new AssemblyDefinitionData
            {
                name = assemblyName,
                rootNamespace = rootNamespace ?? string.Empty,
                references = (references ?? new List<string>()).ToArray(),
                includePlatforms = new[] { "Editor" },
                excludePlatforms = Array.Empty<string>(),
                allowUnsafeCode = false,
                autoReferenced = true,
                overrideReferences = true,
                precompiledReferences = (precompiledReferences ?? new List<string>()).ToArray(),
                defineConstraints = defineConstraints?.ToArray() ?? new[] { "STATIONEERS_DLL_PRESENT" },
                versionDefines = versionDefines?.ToArray() ?? Array.Empty<VersionDefine>(),
                noEngineReferences = false
            };

            string json = JsonUtility.ToJson(asmDef, true);
            File.WriteAllText(assetPath, json);

            ImportAssetOrRefresh(assetPath);

            // Request compilation so the asmdef is picked up promptly.
            CompilationPipeline.RequestScriptCompilation();
        }

        /// <summary>
        /// Imports a single asset and falls back to AssetDatabase.Refresh if Unity does not assign a GUID.
        /// </summary>
        /// <param name="assetPath">Project-relative asset path (for example "Assets/My.asmdef").</param>
        /// <remarks>
        /// In normal cases, ImportAsset is sufficient. The refresh fallback is a safety net for edge cases.
        /// </remarks>
        private static void ImportAssetOrRefresh(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            // If Unity does not report a GUID after import, do a refresh as a last resort.
            if (AssetDatabase.GUIDFromAssetPath(assetPath).Empty())
                AssetDatabase.Refresh();
        }

        /// <summary>
        /// Normalizes a Unity asset path to use forward slashes.
        /// </summary>
        /// <param name="path">Input path.</param>
        /// <returns>Normalized path using forward slashes, or the original value if null or empty.</returns>
        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }

        /// <summary>
        /// Serializable representation of Unity's asmdef JSON structure.
        /// </summary>
        /// <remarks>
        /// This maps directly to the fields Unity expects in a .asmdef file.
        /// Only the fields used by this exporter are included.
        /// </remarks>
        [Serializable]
        private sealed class AssemblyDefinitionData
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

        /// <summary>
        /// Serializable representation of a Unity asmdef versionDefines entry.
        /// </summary>
        /// <remarks>
        /// versionDefines allow conditional defines based on referenced package versions.
        /// This exporter does not currently populate these.
        /// </remarks>
        [Serializable]
        public sealed class VersionDefine
        {
            public string name;
            public string expression;
            public string define;
        }
    }
}