using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    internal static class AssetReferencePatchPathIdResolver
    {
        private const string ProbeBundleName =
            "__stationeers_asset_reference_probe";

        public static IReadOnlyList<BuiltAssetReferencePatch> Resolve(
            IReadOnlyList<ResolvedAssetReferencePatch> patches)
        {
            if (patches == null)
                throw new ArgumentNullException(nameof(patches));

            if (patches.Count == 0)
                return Array.Empty<BuiltAssetReferencePatch>();

            ValidateMainAssets(patches);

            string outputDir = Path.GetFullPath(
                Path.Combine(
                    "Library",
                    "StationeersExporter",
                    "AssetReferenceProbe"));

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);

            Directory.CreateDirectory(outputDir);

            var addressToPatch =
                new Dictionary<string, ResolvedAssetReferencePatch>(
                    StringComparer.Ordinal);

            string[] assetNames = new string[patches.Count];
            string[] addressableNames = new string[patches.Count];

            for (int i = 0; i < patches.Count; i++)
            {
                var patch = patches[i];

                string address =
                    $"__stationeers_patch/{patch.ProxyGuid}/{patch.ProxyLocalFileId}";

                assetNames[i] = patch.ProxyAssetPath;
                addressableNames[i] = address;

                addressToPatch.Add(address, patch);
            }

            var build = new AssetBundleBuild
            {
                assetBundleName = ProbeBundleName,
                assetNames = assetNames,
                addressableNames = addressableNames
            };

            var manifest =
                BuildPipeline.BuildAssetBundles(
                    outputDir,
                    new[] { build },
                    BuildAssetBundleOptions.ForceRebuildAssetBundle |
                    BuildAssetBundleOptions.UncompressedAssetBundle,
                    BuildTarget.StandaloneWindows);

            if (manifest == null)
            {
                throw new InvalidOperationException(
                    "Failed to build asset-reference probe bundle.");
            }

            string bundlePath =
                Path.Combine(outputDir, ProbeBundleName);

            var pathIds =
                ReadContainerPathIds(bundlePath);

            var result =
                new List<BuiltAssetReferencePatch>(patches.Count);

            foreach (var pair in addressToPatch)
            {
                if (!pathIds.TryGetValue(
                        pair.Key,
                        out long pathId))
                {
                    throw new InvalidOperationException(
                        $"Probe asset '{pair.Key}' was not found " +
                        "in the probe bundle container.");
                }

                result.Add(
                    new BuiltAssetReferencePatch(
                        pair.Value,
                        pathId));
            }

            return result;
        }

        private static void ValidateMainAssets(
            IReadOnlyList<ResolvedAssetReferencePatch> patches)
        {
            foreach (var patch in patches)
            {
                var mainAsset =
                    AssetDatabase.LoadMainAssetAtPath(
                        patch.ProxyAssetPath);

                if (mainAsset != patch.Source.ProxyAsset)
                {
                    throw new NotSupportedException(
                        $"Proxy asset '{patch.ProxyAssetPath}' is a sub-asset. " +
                        "Sub-asset proxy resolution is not supported yet.");
                }
            }
        }

        private static Dictionary<string, long>
            ReadContainerPathIds(string bundlePath)
        {
            var result =
                new Dictionary<string, long>(
                    StringComparer.Ordinal);

            var manager = new AssetsManager();

            try
            {
                var bundleInst =
                    manager.LoadBundleFile(bundlePath, true);

                var assetsInst =
                    manager.LoadAssetsFileFromBundle(
                        bundleInst,
                        0,
                        false);

                var bundleAssets =
                    assetsInst.file.GetAssetsOfType(
                        AssetClassID.AssetBundle);

                if (bundleAssets.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Probe bundle contains no AssetBundle object.");
                }

                var bundleField =
                    manager.GetBaseField(
                        assetsInst,
                        bundleAssets[0]);

                var container =
                    bundleField["m_Container.Array"];

                foreach (var entry in container.Children)
                {
                    string address =
                        entry[0].AsString;

                    var assetPPtr =
                        entry[1]["asset"];

                    int fileId =
                        assetPPtr["m_FileID"].AsInt;

                    long pathId =
                        assetPPtr["m_PathID"].AsLong;

                    if (fileId != 0)
                    {
                        throw new InvalidOperationException(
                            $"Probe asset '{address}' unexpectedly " +
                            $"references external file ID {fileId}.");
                    }

                    result[address] = pathId;
                }
            }
            finally
            {
                manager.UnloadAll();
            }

            return result;
        }
    }
}