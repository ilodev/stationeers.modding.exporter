using System;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEngine;

namespace stationeers.modding.exporter
{
    internal static class VanillaReferencePocPatcher
    {
        private const string ProxyMaterialName =
            "__STATIONEERS_PROXY_COLORWHITE__";

        private const string VanillaFile =
            "resources.assets";

        private const long VanillaPathId = 103;

        public static void PatchColorWhite(string bundlePath)
        {
            if (!File.Exists(bundlePath))
                throw new FileNotFoundException("Bundle not found.", bundlePath);

            Debug.Log($"[Vanilla PoC] Opening: {bundlePath}");

            var manager = new AssetsManager();
            var bundleInst = manager.LoadBundleFile(bundlePath, true);
            var bundle = bundleInst.file;

            // For the normal prefab/assets bundle, the first entry is
            // the SerializedFile. AT.NET documents this as the standard case.
            var assetsInst =
                manager.LoadAssetsFileFromBundle(bundleInst, 0, false);

            var assets = assetsInst.file;

            if (!assets.Metadata.TypeTreeEnabled)
            {
                throw new InvalidOperationException(
                    "PoC bundle has stripped TypeTrees. " +
                    "We will need classdata.tpk.");
            }

            // ---------------------------------------------------------
            // Find the locally embedded proxy Material.
            // ---------------------------------------------------------

            AssetFileInfo proxyInfo = null;

            foreach (var info in
                     assets.GetAssetsOfType(AssetClassID.Material))
            {
                var material =
                    manager.GetBaseField(assetsInst, info);

                if (material["m_Name"].AsString == ProxyMaterialName)
                {
                    if (proxyInfo != null)
                    {
                        throw new InvalidOperationException(
                            $"More than one material named " +
                            $"'{ProxyMaterialName}' was found.");
                    }

                    proxyInfo = info;
                }
            }

            if (proxyInfo == null)
            {
                throw new InvalidOperationException(
                    $"Proxy material '{ProxyMaterialName}' not found.");
            }

            long proxyPathId = proxyInfo.PathId;

            Debug.Log(
                $"[Vanilla PoC] Proxy local PathID = {proxyPathId}");

            // ---------------------------------------------------------
            // Add/reuse "resources.assets" external.
            // ---------------------------------------------------------

            int externalFileId =
                FindOrAddExternal(assets, VanillaFile);

            Debug.Log(
                $"[Vanilla PoC] resources.assets m_FileID = " +
                $"{externalFileId}");

            // ---------------------------------------------------------
            // Rewrite MeshRenderer material references.
            // ---------------------------------------------------------

            int patchedReferences = 0;

            foreach (var rendererInfo in
                     assets.GetAssetsOfType(AssetClassID.MeshRenderer))
            {
                var renderer =
                    manager.GetBaseField(assetsInst, rendererInfo);

                var materials = renderer["m_Materials.Array"];

                bool rendererChanged = false;

                foreach (var materialPPtr in materials)
                {
                    int fileId =
                        materialPPtr["m_FileID"].AsInt;

                    long pathId =
                        materialPPtr["m_PathID"].AsLong;

                    if (fileId == 0 &&
                        pathId == proxyPathId)
                    {
                        materialPPtr["m_FileID"].AsInt =
                            externalFileId;

                        materialPPtr["m_PathID"].AsLong =
                            VanillaPathId;

                        rendererChanged = true;
                        patchedReferences++;
                    }
                }

                if (rendererChanged)
                    rendererInfo.SetNewData(renderer);
            }

            if (patchedReferences == 0)
            {
                throw new InvalidOperationException(
                    $"Found proxy Material PathID {proxyPathId}, " +
                    "but no MeshRenderer referenced it.");
            }

            Debug.Log(
                $"[Vanilla PoC] Rewrote {patchedReferences} reference(s) " +
                $"to {VanillaFile}/{VanillaPathId}");

            // Put modified SerializedFile back into bundle.
            bundle.BlockAndDirInfo
                  .DirectoryInfos[0]
                  .SetNewData(assets);

            string uncompressedPath =
                bundlePath + ".vanilla-poc-uncompressed";

            string packedPath =
                bundlePath + ".vanilla-poc-packed";

            if (File.Exists(uncompressedPath))
                File.Delete(uncompressedPath);

            if (File.Exists(packedPath))
                File.Delete(packedPath);

            // AT.NET bundle writes are uncompressed first.
            using (var writer =
                   new AssetsFileWriter(uncompressedPath))
            {
                bundle.Write(writer);
            }

            manager.UnloadAll();

            // Repack as LZ4.
            var uncompressedBundle =
                new AssetBundleFile();

            using (var stream = File.OpenRead(uncompressedPath))
            {
                uncompressedBundle.Read(
                    new AssetsFileReader(stream));

                using (var writer =
                       new AssetsFileWriter(packedPath))
                {
                    uncompressedBundle.Pack(
                        writer,
                        AssetBundleCompressionType.LZ4);
                }
            }

            uncompressedBundle.Close();

            File.Delete(uncompressedPath);

            // Replace exporter output with patched bundle.
            File.Delete(bundlePath);
            File.Move(packedPath, bundlePath);

            Debug.Log(
                "[Vanilla PoC] SUCCESS. Bundle now references " +
                "resources.assets / PathID 103.");
        }

        private static int FindOrAddExternal(
            AssetsFile assets,
            string path)
        {
            var externals = assets.Metadata.Externals;

            for (int i = 0; i < externals.Count; i++)
            {
                var external = externals[i];

                if (string.Equals(
                        external.PathName,
                        path,
                        StringComparison.OrdinalIgnoreCase))
                {
                    // PPtr m_FileID is 1-based.
                    return i + 1;
                }
            }

            externals.Add(
                new AssetsFileExternal
                {
                    VirtualAssetPathName = string.Empty,
                    Guid = new GUID128(),
                    Type = AssetsFileExternalType.Normal,
                    PathName = path,
                    OriginalPathName = string.Empty
                });

            return externals.Count;
        }
    }
}