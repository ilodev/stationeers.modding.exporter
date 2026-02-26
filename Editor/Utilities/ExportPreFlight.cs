using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement; // EditorSceneManager, PrefabStage, PrefabStageUtility
using UnityEngine.SceneManagement;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Pre-export helpers to ensure the Unity project is in a saved, consistent state.
    /// </summary>
    /// <remarks>
    /// This utility is intended to be called right before an export/build step.
    ///
    /// It performs a "save everything with prompts" flow:
    /// 1) Prompts to save modified scenes using Unity's standard dialog.
    /// 2) If Prefab Mode (Prefab Stage) is open and dirty, prompts to save that prefab asset.
    /// 3) Saves dirty project assets (AssetDatabase.SaveAssets) and refreshes the AssetDatabase.
    ///
    /// If the user cancels a prompt or saving fails, the operation returns false.
    /// </remarks>
    public static class ExportPreflight
    {
        /// <summary>
        /// Checks for unsaved changes in open scenes, an open Prefab Stage, and dirty assets under Assets/.
        /// </summary>
        /// <param name="dirtyItems">
        /// Output list of items with unsaved changes. Entries are scene paths/names, prefab asset paths,
        /// and asset paths under Assets/.
        /// </param>
        /// <returns>True if any unsaved changes are detected; otherwise false.</returns>
        /// <remarks>
        /// The asset scan uses Resources.FindObjectsOfTypeAll to detect currently loaded assets that are dirty.
        /// This does not guarantee listing every possible dirty asset on disk, but it reliably catches common
        /// cases (ScriptableObjects, imported assets, etc.) that are loaded in the editor.
        /// </remarks>
        public static bool HasUnsavedChanges(out List<string> dirtyItems)
        {
            dirtyItems = new List<string>();

            AddDirtyScenes(dirtyItems);
            AddDirtyPrefabStage(dirtyItems);
            AddDirtyLoadedAssets(dirtyItems);

            return dirtyItems.Count > 0;
        }

        /// <summary>
        /// Saves all modified scenes and assets with user prompts.
        /// </summary>
        /// <returns>
        /// True if everything was saved (or there was nothing to save). False if the user cancels or a save fails.
        /// </returns>
        /// <remarks>
        /// This method:
        /// - Uses Unity's built-in scene save prompt.
        /// - Prompts to save Prefab Mode changes and saves the prefab asset.
        /// - Saves project assets and refreshes the AssetDatabase.
        /// - Verifies there are no remaining unsaved changes.
        /// </remarks>
        public static bool SaveAllWithPrompts()
        {
            // Scenes: Unity's standard prompt
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[ExportPreflight] Cancelled by user (scene save prompt).");
                return false;
            }

            // Prefab Stage: prompt and save the prefab asset (not the preview scene)
            if (!SavePrefabStageIfNeededWithPrompt())
                return false;

            // Project assets
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Verify; log leftovers if any
            if (HasUnsavedChanges(out var leftover))
            {
                Debug.LogWarning(
                    "[ExportPreflight] Items still unsaved after prompts:\n - " +
                    string.Join("\n - ", leftover));
                return false;
            }

            return true;
        }

        private static void AddDirtyScenes(List<string> dirtyItems)
        {
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isDirty)
                    continue;

                dirtyItems.Add(string.IsNullOrEmpty(scene.path) ? scene.name + " (unsaved)" : scene.path);
            }
        }

        private static void AddDirtyPrefabStage(List<string> dirtyItems)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || !prefabStage.scene.IsValid() || !prefabStage.scene.isDirty)
                return;

            string path = string.IsNullOrEmpty(prefabStage.assetPath) ? "(unknown)" : prefabStage.assetPath;
            dirtyItems.Add("Prefab Stage: " + path);
        }

        private static void AddDirtyLoadedAssets(List<string> dirtyItems)
        {
            // Dirty assets under Assets/ (loaded objects only).
            // Use a HashSet to avoid duplicates when multiple loaded objects map to the same asset path.
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var objs = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
            foreach (var obj in objs)
            {
                if (obj == null)
                    continue;

                // Only consider objects that are assets.
                if (!AssetDatabase.Contains(obj))
                    continue;

                if (!EditorUtility.IsDirty(obj))
                    continue;

                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                    continue;

                // Only scan user project assets, not Packages/ or built-in assets.
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip assets that Unity generally does not serialize in a useful way for SaveAssets,
                // or that are commonly edited as loose files.
                var mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (mainType == null)
                    continue;

                if (ShouldIgnoreAssetType(mainType))
                    continue;

                // Optionally ensure it is a real file on disk. This avoids folders and some virtual assets.
                if (!File.Exists(path))
                    continue;

                if (seenPaths.Add(path))
                    dirtyItems.Add(path);
            }
        }

        private static bool SavePrefabStageIfNeededWithPrompt()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || prefabStage.prefabContentsRoot == null)
                return true;

            if (!prefabStage.scene.IsValid() || !prefabStage.scene.isDirty)
                return true;

            string path = string.IsNullOrEmpty(prefabStage.assetPath) ? "(unknown prefab asset)" : prefabStage.assetPath;

            int choice = EditorUtility.DisplayDialogComplex(
                "Save Prefab Stage changes?",
                "There are unsaved changes in Prefab Mode:\n\n" + path + "\n\nSave changes before continuing?",
                "Save",   // 0
                "Cancel", // 1
                null);

            if (choice == 1)
            {
                Debug.Log("[ExportPreflight] Cancelled by user (prefab stage prompt).");
                return false;
            }

            // SaveAsPrefabAsset writes to the asset on disk.
            PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath, out bool ok);
            if (!ok)
            {
                Debug.LogWarning("[ExportPreflight] Failed to save Prefab Stage asset: " + path);
                return false;
            }

            return true;
        }

        private static bool ShouldIgnoreAssetType(Type mainType)
        {
            // Note: do not include duplicate entries.
            if (mainType == typeof(Shader)) return true;        // .shader source
            if (mainType == typeof(MonoScript)) return true;    // .cs scripts
            if (mainType == typeof(Font)) return true;          // .ttf / .otf often treated as loose files
            if (mainType == typeof(TextAsset)) return true;     // .txt / .json / .xml / etc
            if (mainType == typeof(DefaultAsset)) return true;  // folders / unknown types

            return false;
        }
    }
}