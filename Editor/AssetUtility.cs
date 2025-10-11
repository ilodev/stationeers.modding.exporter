﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    ///     A set of utilities for handling assets.
    /// </summary>
    public class AssetUtility
    {
        /// <summary>
        ///     Finds and returns the directory where ModTool is located.
        /// </summary>
        /// <returns>The directory where ModTool is located.</returns>
        public static string GetModToolDirectory()
        {
            //var location = typeof(ModInfo).Assembly.Location; 
            var location = "";

            var modToolDirectory = Path.GetDirectoryName(location);

            if (!Directory.Exists(modToolDirectory))
                modToolDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");

            // Fallback to Assets in the exporter is installed as a package the editor 
            if (modToolDirectory.Contains("PackageCache"))
                modToolDirectory = Application.dataPath;
            
            return GetRelativePath(modToolDirectory);
        }

        /// <summary>
        ///     Get the relative path for an absolute path.
        /// </summary>
        /// <param name="path">The absolute path.</param>
        /// <returns>The relative path.</returns>
        public static string GetRelativePath(string path)
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            var pathUri = new Uri(path);

            if (!currentDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                currentDirectory += Path.DirectorySeparatorChar;

            var directoryUri = new Uri(currentDirectory);

            var relativePath = Uri.UnescapeDataString(directoryUri.MakeRelativeUri(pathUri).ToString()
                .Replace('/', Path.DirectorySeparatorChar));

            return relativePath;
        }

        /// <summary>
        ///     Get all asset paths for assets that match the filter.
        /// </summary>
        /// <param name="filter">The filter string can contain search data for: names, asset labels and types (class names).</param>
        /// <returns>A list of asset paths</returns>
        public static List<string> GetAssets(string filter)
        {
            var assetPaths = new List<string>();

            var assetGuids = AssetDatabase.FindAssets(filter);

            foreach (var guid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (assetPath.Contains("ExportSettings."))
                    continue;

                if (assetPath.Contains("Editor"))
                    continue;

                if (assetPath.StartsWith("Packages"))
                    continue;

                //NOTE: AssetDatabase.FindAssets() can contain duplicates for some reason
                if (assetPaths.Contains(assetPath))
                    continue;

                assetPaths.Add(assetPath);
            }

            return assetPaths;
        }

        /// <summary>
        ///     Move assets to a directory.
        /// </summary>
        /// <param name="assetPaths">A list of asset paths</param>
        /// <param name="targetDirectory">The directory to move all assets to.</param>
        public static void MoveAssets(List<string> assetPaths, string targetDirectory)
        {
            for (var i = 0; i < assetPaths.Count; i++)
            {
                var assetPath = assetPaths[i];

                if (Path.GetDirectoryName(assetPath) != targetDirectory)
                {
                    var assetName = Path.GetFileName(assetPath);
                    var newAssetPath = Path.Combine(targetDirectory, assetName);

                    AssetDatabase.MoveAsset(assetPath, newAssetPath);
                    assetPaths[i] = newAssetPath;
                }
            }
        }

        /// <summary>
        /// Create an asset for a ScriptableObject in a ModTool Resources directory.
        /// </summary>
        /// <param name="scriptableObject">A ScriptableObject instance.</param>
        public static void CreateAsset(ScriptableObject scriptableObject)
        {
            var resourcesParentDirectory = "Assets";
            
            var resourcesDirectory = "";

            resourcesDirectory = Directory
                .GetDirectories(resourcesParentDirectory, "Resources", SearchOption.AllDirectories).FirstOrDefault();

            if (string.IsNullOrEmpty(resourcesDirectory))
            {
                resourcesDirectory = Path.Combine(resourcesParentDirectory, "Resources");
                Directory.CreateDirectory(resourcesDirectory);
            }

            var path = Path.Combine(resourcesDirectory, scriptableObject.GetType().Name + ".asset");
            Debug.Log($"Creating asset at {path}");
            AssetDatabase.CreateAsset(scriptableObject, path);
        }
    }
}