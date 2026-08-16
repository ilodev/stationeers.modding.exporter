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
            IReadOnlyList<BuiltAssetReferencePatch> patches)
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

            string tempPath =
                bundlePath + ".patching";

            if (File.Exists(tempPath))
                File.Delete(tempPath);

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

                int rewritten =
                    AssetReferencePPtrRewriter.Rewrite(
                        manager,
                        assetsInst,
                        patches);

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

                // For now write LZ4, matching the working PoC.
                using (var writer =
                       new AssetsFileWriter(tempPath))
                {
                    bundle.Pack(
                        writer,
                        AssetBundleCompressionType.LZ4);
                }

                manager.UnloadAll();

                ReplaceFile(
                    tempPath,
                    bundlePath);

                Debug.Log(
                    $"[AssetReferencePatching] " +
                    $"Rewrote {rewritten} PPtr(s) in " +
                    $"'{Path.GetFileName(bundlePath)}'.");

                return rewritten;
            }
            finally
            {
                manager.UnloadAll();

                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
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