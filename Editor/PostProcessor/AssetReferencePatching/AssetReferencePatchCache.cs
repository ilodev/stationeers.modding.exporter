using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace stationeers.modding.exporter
{
    internal static class AssetReferencePatchCache
    {
        private const int PatcherVersion = 1;

        [Serializable]
        private sealed class PatchCacheMetadata
        {
            public int rewrittenReferenceCount;
            public int removedProxyCount;
            public List<string> removedProxyAssetPaths = new List<string>();
        }

        public static string SavePristineBundle(
            string bundlePath,
            string platform)
        {
            string cacheDirectory =
                Path.GetFullPath(
                    Path.Combine(
                        "Library",
                        "StationeersExporter",
                        "PristineBundles",
                        platform));

            Directory.CreateDirectory(cacheDirectory);

            string cachePath =
                Path.Combine(
                    cacheDirectory,
                    Path.GetFileName(bundlePath));

            File.Copy(
                bundlePath,
                cachePath,
                true);

            return cachePath;
        }

        public static bool RestorePristineBundle(
            string bundlePath,
            string platform)
        {
            string cachePath =
                Path.GetFullPath(
                    Path.Combine(
                        "Library",
                        "StationeersExporter",
                        "PristineBundles",
                        platform,
                        Path.GetFileName(bundlePath)));

            if (!File.Exists(cachePath))
                return false;

            string destinationDirectory =
                Path.GetDirectoryName(bundlePath);

            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            File.Copy(
                cachePath,
                bundlePath,
                true);

            return true;
        }

        public static string ComputePatchKey(
            string pristineBundlePath,
            IReadOnlyList<ResolvedAssetReferencePatch> patches)
        {
            string bundleHash =
                ComputeFileHash(pristineBundlePath);

            var mappingText = new StringBuilder();

            foreach (var patch in patches
                .OrderBy(p => p.ProxyGuid, StringComparer.Ordinal)
                .ThenBy(p => p.ProxyLocalFileId))
            {
                mappingText.Append(patch.ProxyGuid);
                mappingText.Append('|');
                mappingText.Append(patch.ProxyLocalFileId);
                mappingText.Append('|');
                mappingText.Append(patch.Source.TargetSerializedFile);
                mappingText.Append('|');
                mappingText.Append(patch.Source.TargetPathId);
                mappingText.Append('|');
                mappingText.Append(patch.Source.TargetTypeId);
                mappingText.Append('|');
                mappingText.Append(patch.Source.Cleanup);
                mappingText.Append('\n');
            }

            string input =
                $"v{PatcherVersion}|{bundleHash}|{mappingText}";

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(input));

                return ToHex(hash);
            }
        }

        public static string GetPatchedBundlePath(
            string patchKey,
            string bundleFileName)
        {
            string cacheDirectory =
                Path.GetFullPath(
                    Path.Combine(
                        "Library",
                        "StationeersExporter",
                        "PatchedBundles",
                        patchKey));

            return Path.Combine(
                cacheDirectory,
                bundleFileName);
        }

        public static bool TryRestorePatchedBundle(
            string patchKey,
            string destinationBundlePath)
        {
            string cachePath =
                GetPatchedBundlePath(
                    patchKey,
                    Path.GetFileName(destinationBundlePath));

            if (!File.Exists(cachePath))
                return false;

            string destinationDirectory =
                Path.GetDirectoryName(destinationBundlePath);

            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            File.Copy(
                cachePath,
                destinationBundlePath,
                true);

            return true;
        }

        public static void SavePatchedBundle(
            string patchKey,
            string bundlePath)
        {
            string cachePath =
                GetPatchedBundlePath(
                    patchKey,
                    Path.GetFileName(bundlePath));

            string cacheDirectory =
                Path.GetDirectoryName(cachePath);

            if (!string.IsNullOrEmpty(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);

            File.Copy(
                bundlePath,
                cachePath,
                true);
        }

        public static void SavePatchMetadata(
            string patchKey,
            string bundleFileName,
            AssetReferenceBundlePatchResult result)
        {
            string bundlePath =
                GetPatchedBundlePath(
                    patchKey,
                    bundleFileName);

            string metadataPath =
                bundlePath + ".json";

            string cacheDirectory =
                Path.GetDirectoryName(metadataPath);

            if (!string.IsNullOrEmpty(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);

            var metadata =
                new PatchCacheMetadata
                {
                    rewrittenReferenceCount =
                        result.RewrittenReferenceCount,

                    removedProxyCount =
                        result.RemovedProxyCount,

                    removedProxyAssetPaths =
                        new List<string>(
                            result.RemovedProxyAssetPaths)
                };

            File.WriteAllText(
                metadataPath,
                JsonUtility.ToJson(metadata, true));
        }

        private static string ComputeFileHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                return ToHex(
                    sha.ComputeHash(stream));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var result =
                new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
                result.Append(b.ToString("x2"));

            return result.ToString();
        }
        public static bool TryLoadPatchMetadata(
            string patchKey,
            string bundleFileName,
            out AssetReferenceBundlePatchResult result)
        {
            string bundlePath =
                GetPatchedBundlePath(
                    patchKey,
                    bundleFileName);

            string metadataPath =
                bundlePath + ".json";

            if (!File.Exists(metadataPath))
            {
                result = null;
                return false;
            }

            var metadata =
                JsonUtility.FromJson<PatchCacheMetadata>(
                    File.ReadAllText(metadataPath));

            if (metadata == null)
            {
                result = null;
                return false;
            }

            result = new AssetReferenceBundlePatchResult(
                metadata.rewrittenReferenceCount,
                metadata.removedProxyAssetPaths);

            return true;
        }

        public static bool TryRestorePatchedBundleWithMetadata(
            string patchKey,
            string destinationBundlePath,
            out AssetReferenceBundlePatchResult result)
        {
            result = null;

            string bundleFileName =
                Path.GetFileName(destinationBundlePath);

            if (!TryLoadPatchMetadata(
                    patchKey,
                    bundleFileName,
                    out var cachedResult))
            {
                return false;
            }

            if (!TryRestorePatchedBundle(
                    patchKey,
                    destinationBundlePath))
            {
                return false;
            }

            result = cachedResult;
            return true;
        }
    }
}
