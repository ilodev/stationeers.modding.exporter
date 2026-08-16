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

                // AssetsTools.NET applies the directory replacers while
                // writing the modified bundle. Write that result first.
                using (var writer =
                       new AssetsFileWriter(uncompressedPath))
                {
                    bundle.Write(writer);
                }

                // Release the original bundle before reopening/replacing it.
                manager.UnloadAll();

                // Reopen the already-modified bundle and compress it.
                var modifiedBundle =
                    new AssetBundleFile();

                try
                {
                    using (var stream =
                           File.OpenRead(uncompressedPath))
                    {
                        modifiedBundle.Read(
                            new AssetsFileReader(stream));

                        using (var writer =
                               new AssetsFileWriter(packedPath))
                        {
                            modifiedBundle.Pack(
                                writer,
                                AssetBundleCompressionType.LZ4);
                        }
                    }
                }
                finally
                {
                    modifiedBundle.Close();
                }

                ReplaceFile(
                    packedPath,
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

                if (File.Exists(packedPath))
                    File.Delete(packedPath);

                if (File.Exists(uncompressedPath))
                    File.Delete(uncompressedPath);
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