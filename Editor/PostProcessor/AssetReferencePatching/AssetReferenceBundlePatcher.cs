using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEngine;

namespace stationeers.modding.exporter
{
    internal static class AssetReferenceBundlePatcher
    {
        public static int Patch(
            string bundlePath,
            IReadOnlyList<ResolvedAssetReferencePatch> patches)
        {
            if (string.IsNullOrEmpty(bundlePath))
                throw new ArgumentNullException(nameof(bundlePath));

            if (!File.Exists(bundlePath))
                throw new FileNotFoundException(
                    "AssetBundle not found.",
                    bundlePath);

            if (patches == null)
                throw new ArgumentNullException(nameof(patches));

            if (patches.Count == 0)
                return 0;

            string uncompressedPath =
                bundlePath + ".patching.uncompressed";

            string packedPath =
                bundlePath + ".patching";

            if (File.Exists(uncompressedPath))
                File.Delete(uncompressedPath);

            if (File.Exists(packedPath))
                File.Delete(packedPath);

            var manager = new AssetsManager();

            try
            {
                var bundleInst =
                    manager.LoadBundleFile(
                        bundlePath,
                        true);

                var bundle =
                    bundleInst.file;

                // Current exporter assets bundle uses the first
                // directory entry as its SerializedFile.
                var assetsInst =
                    manager.LoadAssetsFileFromBundle(
                        bundleInst,
                        0,
                        false);

                // Resolve PathIDs from the REAL bundle, not from a separate
                // probe bundle. The build map makes every registered proxy an
                // explicit root so it appears in AssetBundle.m_Container.
                var builtPatches =
                    ResolveBuiltPatches(
                        manager,
                        assetsInst,
                        patches);

                foreach (var patch in builtPatches)
                {
                    Debug.Log(
                        $"[AssetReferencePatching] " +
                        $"{patch.Source.ProxyAssetPath} -> " +
                        $"local PathID={patch.ProxyBundlePathId} -> " +
                        $"{patch.TargetSerializedFile}/{patch.TargetPathId}");
                }

                int rewritten =
                    AssetReferencePPtrRewriter.Rewrite(
                        manager,
                        assetsInst,
                        builtPatches);

                if (rewritten == 0)
                {
                    Debug.Log(
                        $"[AssetReferencePatching] " +
                        $"No matching PPtrs found in '{bundlePath}'.");

                    return 0;
                }

                // Replace the SerializedFile stored inside the bundle.
                bundle.BlockAndDirInfo
                    .DirectoryInfos[0]
                    .SetNewData(assetsInst.file);

                // AssetsTools.NET applies the queued object/metadata changes
                // when Write() creates the modified uncompressed bundle.
                using (var writer =
                       new AssetsFileWriter(uncompressedPath))
                {
                    bundle.Write(writer);
                }

                manager.UnloadAll();

                // Reopen the already-modified bundle, then compress it.
                // AssetBundleFile keeps reading from the AssetsFileReader during
                // Pack(), so the input stream must remain open until packing is
                // complete.
                var modifiedBundle =
                    new AssetBundleFile();

                using (var stream =
                       File.OpenRead(uncompressedPath))
                {
                    var reader =
                        new AssetsFileReader(stream);

                    modifiedBundle.Read(reader);

                    using (var writer =
                           new AssetsFileWriter(packedPath))
                    {
                        modifiedBundle.Pack(
                            writer,
                            AssetBundleCompressionType.LZ4);
                    }

                    modifiedBundle.Close();
                }

                ReplaceFile(
                    packedPath,
                    bundlePath);

                File.Delete(uncompressedPath);

                Debug.Log(
                    $"[AssetReferencePatching] " +
                    $"Rewrote {rewritten} PPtr(s) in " +
                    $"'{Path.GetFileName(bundlePath)}'.");

                return rewritten;
            }
            finally
            {
                manager.UnloadAll();

                if (File.Exists(packedPath))
                    File.Delete(packedPath);

                if (File.Exists(uncompressedPath))
                    File.Delete(uncompressedPath);
            }
        }

        private static IReadOnlyList<BuiltAssetReferencePatch>
            ResolveBuiltPatches(
                AssetsManager manager,
                AssetsFileInstance assetsInstance,
                IReadOnlyList<ResolvedAssetReferencePatch> patches)
        {
            var bundleAssets =
                assetsInstance.file.GetAssetsOfType(
                    AssetClassID.AssetBundle);

            if (bundleAssets.Count == 0)
            {
                throw new InvalidOperationException(
                    "AssetBundle SerializedFile contains no AssetBundle object.");
            }

            var bundleField =
                manager.GetBaseField(
                    assetsInstance,
                    bundleAssets[0]);

            var container =
                bundleField["m_Container.Array"];

            var pathIdsByAssetPath =
                new Dictionary<string, long>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var entry in container.Children)
            {
                string assetPath =
                    entry[0].AsString;

                var assetPPtr =
                    entry[1]["asset"];

                int fileId =
                    assetPPtr["m_FileID"].AsInt;

                long pathId =
                    assetPPtr["m_PathID"].AsLong;

                if (fileId != 0)
                    continue;

                pathIdsByAssetPath[
                    NormalizeAssetPath(assetPath)] = pathId;
            }

            var result =
                new List<BuiltAssetReferencePatch>(patches.Count);

            foreach (var patch in patches)
            {
                string key =
                    NormalizeAssetPath(patch.ProxyAssetPath);

                if (!pathIdsByAssetPath.TryGetValue(
                        key,
                        out long pathId))
                {
                    throw new InvalidOperationException(
                        $"Registered proxy asset '{patch.ProxyAssetPath}' " +
                        "was not found in the real AssetBundle m_Container. " +
                        "The proxy must be included as an explicit root of " +
                        "the assets bundle before patching.");
                }

                result.Add(
                    new BuiltAssetReferencePatch(
                        patch,
                        pathId));
            }

            return result;
        }

        private static string NormalizeAssetPath(
            string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .ToLowerInvariant();
        }

        private static void ReplaceFile(
            string source,
            string destination)
        {
            string backup =
                destination + ".prepatch";

            if (File.Exists(backup))
                File.Delete(backup);

            File.Move(
                destination,
                backup);

            try
            {
                File.Move(
                    source,
                    destination);

                File.Delete(backup);
            }
            catch
            {
                if (File.Exists(destination))
                    File.Delete(destination);

                if (File.Exists(backup))
                    File.Move(
                        backup,
                        destination);

                throw;
            }
        }
    }
}
