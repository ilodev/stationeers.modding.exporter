using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Checks and configures the tags and layers required by the Stationeers modding project.
    /// Based on Layers and Tags found through the game files.
    /// </summary>
    public static class TagLayerUtility
    {
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";
        private const int FirstUserLayer = 8;
        private const int LastLayer = 31;

        /// <summary>
        /// Tags required by the Stationeers modding project.
        /// Existing tags that are not in this list are preserved.
        /// </summary>
        private static readonly string[] RequiredTags =
        {
            "UIHelper",
            "Slot",
            "NoNotDelete",
            "NotSpawnable",
            "CollidersAlwaysVisible",
            "Cursor",
            "NotPaintable",
            "Asteroid",
            "GraphicRaycastCamera",
            "BlueprintHelper",
            "Vehicle",
            "Sun",
            "EffectLight",
            "AnimatedCollider",
            "DontHideOnClick",
            "NonLocalized",
            "ExcludeFromBounds"
        };

        /// <summary>
        /// Layers required by the Stationeers modding project.
        /// The dictionary key is the exact Unity layer index.
        /// </summary>
        private static readonly Dictionary<int, string> RequiredLayers =
            new Dictionary<int, string>
            {
                { 8, "PlayerInvisible" },
                { 9, "Player" },
                { 10, "PlayerImmune" },
                { 11, "PlayerRagdoll" },
                { 12, "WorldspaceUI" },
                { 13, "HUD" },
                { 14, "MiniMap" },
                { 15, "LabelText" },
                { 16, "Terrain" },
                { 17, "CursorVoxel" },
                { 18, "CharacterCreation" },
                { 19, "Planets" },
                { 20, "IgnoreWheelColliders" },
                { 21, "PostProcess" },
                { 22, "Stars" },
                { 23, "BlockSound" },
                { 24, "Plants" },
                { 25, "Cables" },
                { 26, "FirstPerson" },
                { 27, "LiquidSolverParticles" },
                { 29, "ThumbnailCreation" },
                { 30, "TerrainMeshStamp" },
                { 31, "TransformGizmo" }
            };

        /// <summary>
        /// Result of checking the current project TagManager settings. Internal use only
        /// </summary>
        public sealed class CheckResult
        {
            public readonly List<string> MissingTags = new List<string>();
            public readonly List<LayerIssue> MissingLayers = new List<LayerIssue>();
            public readonly List<LayerIssue> ConflictingLayers = new List<LayerIssue>();
            public readonly List<string> Warnings = new List<string>();

            public bool IsValid {
                get {
                    return MissingTags.Count == 0 && MissingLayers.Count == 0 && ConflictingLayers.Count == 0;
                }
            }

            public int IssueCount {
                get {
                    return MissingTags.Count + MissingLayers.Count + ConflictingLayers.Count;
                }
            }
        }

        /// <summary>
        /// Describes a missing or conflicting layer slot. Not showing for now, might as well remove it
        /// </summary>
        public sealed class LayerIssue {
            public int Index;
            public string ExpectedName;
            public string CurrentName;

            public LayerIssue(int index, string expectedName, string currentName) {
                Index = index;
                ExpectedName = expectedName;
                CurrentName = currentName;
            }
        }

        /// <summary>
        /// Checks the current project tags and layers without modifying them.
        /// </summary>
        public static CheckResult Check()
        {
            var result = new CheckResult();

            SerializedObject tagManager = LoadTagManager();
            if (tagManager == null) {
                result.Warnings.Add("Could not load " + TagManagerPath + ".");
                return result;
            }

            tagManager.Update();
            SerializedProperty tagsProperty = tagManager.FindProperty("tags");
            SerializedProperty layersProperty = tagManager.FindProperty("layers");

            if (tagsProperty == null) {
                result.Warnings.Add("Could not find the tags property in " + TagManagerPath + ".");
            }
            else {
                CheckTags(tagsProperty, result);
            }

            if (layersProperty == null) {
                result.Warnings.Add("Could not find the layers property in " + TagManagerPath + ".");
            }
            else {
                CheckLayers(layersProperty, result);
            }

            if (RequiredLayers.ContainsKey(31)) {
                result.Warnings.Add(
                    "Layer 31 is configured as TransformGizmo. " +
                    "Some Unity versions reserve layer 31 for editor preview objects."
                );
            }

            return result;
        }

        /// <summary>
        /// Adds missing tags and fills missing layer slots.
        /// Conflicting occupied layer slots are replaced only when replaceConflictingLayers is true
        /// </summary>
        public static bool ApplyRequiredSettings(bool replaceConflictingLayers)
        {
            SerializedObject tagManager = LoadTagManager();
            if (tagManager == null) {
                Debug.LogError("[TagLayerUtility] Could not load " + TagManagerPath + ".");
                return false;
            }

            tagManager.Update();
            SerializedProperty tagsProperty = tagManager.FindProperty("tags");
            SerializedProperty layersProperty = tagManager.FindProperty("layers");

            if (tagsProperty == null || layersProperty == null) {
                Debug.LogError(
                    "[TagLayerUtility] TagManager.asset does not contain " +
                    "the expected tags and layers properties."
                );

                return false;
            }

            bool changed = false;

            changed |= AddMissingTags(tagsProperty);
            changed |= ApplyRequiredLayers(layersProperty, replaceConflictingLayers);

            if (!changed) {
                Debug.Log("[TagLayerUtility] Required tags and layers are already configured.");
                return true;
            }

            try {
                tagManager.ApplyModifiedPropertiesWithoutUndo();

                UnityEngine.Object target = tagManager.targetObject;
                if (target != null) {
                    EditorUtility.SetDirty(target);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                CheckResult verification = Check();
                if (!verification.IsValid) {
                    Debug.LogWarning(
                        "[TagLayerUtility] Settings were saved, but some tag or layer issues remain."
                    );

                    return false;
                }

                Debug.Log("[TagLayerUtility] Required tags and layers were applied.");
                return true;
            }
            catch (Exception exception) {
                Debug.LogException(exception);
                Debug.LogError("[TagLayerUtility] Failed to save required tags and layers.");

                return false;
            }
        }

        /// <summary>
        /// Shows a confirmation prompt and then applies the required settings.
        /// </summary>
        public static bool ApplyRequiredSettingsWithPrompt() {
            CheckResult result = Check();

            if (result.IsValid) {
                EditorUtility.DisplayDialog(
                    "Tags and Layers",
                    "All required tags and layers are already configured.",
                    "OK"
                );

                return true;
            }

            bool hasConflicts = result.ConflictingLayers.Count > 0;

            string message = BuildApplyPromptMessage(result);

            if (!hasConflicts) {
                bool apply = EditorUtility.DisplayDialog(
                    "Apply Required Tags and Layers",
                    message,
                    "Apply",
                    "Cancel"
                );

                if (!apply)
                    return false;

                return ApplyRequiredSettings(replaceConflictingLayers: false);
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Apply Required Tags and Layers",
                message,
                "Replace Conflicts",
                "Cancel",
                "Apply Missing Only"
            );

            if (choice == 1)
                return false;

            bool replaceConflicts = choice == 0;
            return ApplyRequiredSettings(replaceConflicts);
        }

        /// <summary>
        /// Returns true when all required tags and layers are configured.
        /// </summary>
        public static bool HasRequiredSettings()
        {
            return Check().IsValid;
        }

        /// <summary>
        /// Opens Unity's Tags and Layers project settings page.
        /// </summary>
        public static void OpenTagsAndLayersSettings()
        {
            SettingsService.OpenProjectSettings("Project/Tags and Layers");
        }

        private static SerializedObject LoadTagManager() {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);

            if (assets == null || assets.Length == 0)
                return null;

            if (assets[0] == null)
                return null;

            return new SerializedObject(assets[0]);
        }

        private static void CheckTags(SerializedProperty tagsProperty, CheckResult result) {
            var existingTags = new HashSet<string>(
                StringComparer.Ordinal);

            for (int i = 0; i < tagsProperty.arraySize; i++) {
                SerializedProperty element = tagsProperty.GetArrayElementAtIndex(i);

                string value = element.stringValue;

                if (!string.IsNullOrEmpty(value))
                    existingTags.Add(value);
            }

            foreach (string requiredTag in RequiredTags) {
                if (!existingTags.Contains(requiredTag))
                    result.MissingTags.Add(requiredTag);
            }
        }

        private static void CheckLayers(SerializedProperty layersProperty, CheckResult result) {
            foreach (KeyValuePair<int, string> pair in RequiredLayers)
            {
                int index = pair.Key;
                string expectedName = pair.Value;

                if (index < FirstUserLayer || index > LastLayer)
                {
                    result.Warnings.Add("Required layer " + expectedName + " uses invalid user layer index " + index + ".");
                    continue;
                }

                if (index >= layersProperty.arraySize)
                {
                    result.MissingLayers.Add(new LayerIssue(index, expectedName, string.Empty));
                    continue;
                }

                SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(index);

                string currentName = layerProperty.stringValue;

                if (string.Equals(currentName, expectedName, StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrEmpty(currentName))
                    result.MissingLayers.Add(new LayerIssue(index, expectedName, currentName));
                else
                    result.ConflictingLayers.Add(new LayerIssue(index, expectedName, currentName));
            }
        }

        private static bool AddMissingTags(SerializedProperty tagsProperty) {
            var existingTags = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < tagsProperty.arraySize; i++)
            {
                string value = tagsProperty
                    .GetArrayElementAtIndex(i)
                    .stringValue;

                if (!string.IsNullOrEmpty(value))
                    existingTags.Add(value);
            }

            bool changed = false;
            foreach (string requiredTag in RequiredTags)
            {
                if (existingTags.Contains(requiredTag))
                    continue;

                int newIndex = tagsProperty.arraySize;
                tagsProperty.InsertArrayElementAtIndex(newIndex);

                SerializedProperty newElement = tagsProperty.GetArrayElementAtIndex(newIndex);

                newElement.stringValue = requiredTag;
                existingTags.Add(requiredTag);
                changed = true;
            }

            return changed;
        }

        private static bool ApplyRequiredLayers(SerializedProperty layersProperty, bool replaceConflictingLayers) {
            bool changed = false;

            EnsureLayerArraySize(layersProperty);

            foreach (KeyValuePair<int, string> pair in RequiredLayers)
            {
                int index = pair.Key;
                string expectedName = pair.Value;

                if (index < FirstUserLayer || index > LastLayer)
                    continue;

                SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(index);

                string currentName = layerProperty.stringValue;

                if (string.Equals(currentName, expectedName, StringComparison.Ordinal))
                    continue;

                bool isEmpty = string.IsNullOrEmpty(currentName);

                if (!isEmpty && !replaceConflictingLayers)
                    continue;

                layerProperty.stringValue = expectedName;
                changed = true;
            }

            return changed;
        }

        private static void EnsureLayerArraySize(SerializedProperty layersProperty) {
            const int requiredArraySize = 32;

            while (layersProperty.arraySize < requiredArraySize)
            {
                int index = layersProperty.arraySize;
                layersProperty.InsertArrayElementAtIndex(index);

                SerializedProperty element = layersProperty.GetArrayElementAtIndex(index);

                element.stringValue = string.Empty;
            }
        }

        private static string BuildApplyPromptMessage(CheckResult result) {
            var lines = new List<string>();

            if (result.MissingTags.Count > 0)
            {
                lines.Add("Missing tags: " + result.MissingTags.Count);

                foreach (string tag in result.MissingTags)
                    lines.Add("  " + tag);
            }

            if (result.MissingLayers.Count > 0) {
                if (lines.Count > 0)
                    lines.Add(string.Empty);

                lines.Add("Missing layers: " + result.MissingLayers.Count);

                foreach (LayerIssue issue in result.MissingLayers)
                    lines.Add("  Layer " + issue.Index + ": " + issue.ExpectedName);
            }

            if (result.ConflictingLayers.Count > 0)
            {
                if (lines.Count > 0)
                    lines.Add(string.Empty);

                lines.Add("Conflicting layers: " + result.ConflictingLayers.Count);

                foreach (LayerIssue issue in result.ConflictingLayers)
                {
                    lines.Add("  Layer " + issue.Index + ": currently \"" + issue.CurrentName + "\", expected \"" + issue.ExpectedName + "\"");
                }

                lines.Add(string.Empty);
                lines.Add(
                    "Replacing conflicts can change the meaning of " +
                    "existing layer assignments in scenes and prefabs."
                );
            }

            if (result.Warnings.Count > 0)
            {
                if (lines.Count > 0)
                    lines.Add(string.Empty);

                lines.Add("Warnings:");

                foreach (string warning in result.Warnings)
                    lines.Add("  " + warning);
            }

            return string.Join("\n", lines);
        }
    }
}