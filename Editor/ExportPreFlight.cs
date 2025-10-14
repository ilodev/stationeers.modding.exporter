// ExportPreflight.cs
// "Save everything WITH PROMPTS" before export:
// - Prompts with Unity's scene save dialog
// - Prompts to save an open Prefab Stage
// - Saves dirty project assets

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement; // EditorSceneManager, PrefabStage, PrefabStageUtility
using UnityEngine;
using UnityEngine.SceneManagement;

namespace stationeers.modding.exporter
{
    public static class ExportPreflight
    {
        // Optional: list unsaved items for diagnostics
        public static bool HasUnsavedChanges(out List<string> dirtyItems)
        {
            dirtyItems = new List<string>();

            // Scenes
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scn = SceneManager.GetSceneAt(i);
                if (scn.IsValid() && scn.isDirty)
                    dirtyItems.Add(string.IsNullOrEmpty(scn.path) ? scn.name + " (unsaved)" : scn.path);
            }

            // Prefab Stage (info only)
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.scene.isDirty)
                dirtyItems.Add("Prefab Stage: " + (string.IsNullOrEmpty(prefabStage.assetPath) ? "(unknown)" : prefabStage.assetPath));

            // Dirty assets under Assets/
            var objs = Resources.FindObjectsOfTypeAll<Object>();
            var seen = new HashSet<string>();
            foreach (var o in objs)
            {
                if (o == null) continue;
                if (!AssetDatabase.Contains(o)) continue;
                if (!EditorUtility.IsDirty(o)) continue;

                string path = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.StartsWith("Assets/")) continue;
                if (!File.Exists(path)) continue;
                if (AssetDatabase.GetMainAssetTypeAtPath(path) == null) continue;

                if (seen.Add(path))
                    dirtyItems.Add(path);
            }

            return dirtyItems.Count > 0;
        }

        // Save everything WITH PROMPTS. Returns true if saved (or nothing to save); false if user cancels or save fails.
        public static bool SaveAllWithPrompts()
        {
            // A) Scenes: Unity's standard prompt
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false; // user canceled

            // B) Prefab Stage: explicit prompt, then save prefab asset (never the preview scene)
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null && prefabStage.scene.isDirty)
            {
                string path = string.IsNullOrEmpty(prefabStage.assetPath) ? "(unknown prefab asset)" : prefabStage.assetPath;
                int choice = EditorUtility.DisplayDialogComplex(
                    "Save Prefab Stage changes?",
                    "There are unsaved changes in Prefab Mode:\n\n" + path + "\n\nSave changes before continuing?",
                    "Save",            // 0
                    "Cancel",          // 1
                    null
                );
                if (choice == 1) return false; // cancel

                bool ok;
                PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath, out ok);
                
                // Prefab stage saving seems to be tricky
                EditorApplication.ExecuteMenuItem("File/Save");
                return true;
            }

            // C) Project assets
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // D) Verify; log leftovers if any
            if (HasUnsavedChanges(out var leftover))
            {
                Debug.LogWarning("[Preflight] Items still unsaved after prompts:\n - " + string.Join("\n - ", leftover));
                return false;
            }
            return true;
        }

        // Back-compat name
        public static bool PromptUserToSaveAll() => SaveAllWithPrompts();

        // Menu helpers for quick testing
        [MenuItem("Tools/Export/Preflight/Save Everything (with prompts)")]
        private static void MenuSaveWithPrompts()
        {
            Debug.Log(SaveAllWithPrompts() ? "Saved everything." : "Save canceled or failed.");
        }

        [MenuItem("Tools/Export/Preflight/Check Unsaved")]
        private static void MenuCheck()
        {
            Debug.Log(HasUnsavedChanges(out var dirty)
                ? "Unsaved changes:\n - " + string.Join("\n - ", dirty)
                : "No unsaved changes.");
        }
    }
}
