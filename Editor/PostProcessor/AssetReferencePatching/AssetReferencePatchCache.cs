using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace stationeers.modding.exporter
{
    internal static class AssetReferencePatchCache
    {
        private const int PatcherVersion = 1;

        public static string ComputePatchKey(
            string pristineBundlePath,
            IReadOnlyList<ResolvedAssetReferencePatch> patches)
        {
            string bundleHash = ComputeFileHash(pristineBundlePath);

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

            using var sha = SHA256.Create();

            byte[] hash = sha.ComputeHash(
                Encoding.UTF8.GetBytes(input));

            return ToHex(hash);
        }

        private static string ComputeFileHash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();

            return ToHex(sha.ComputeHash(stream));
        }

        private static string ToHex(byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
                result.Append(b.ToString("x2"));

            return result.ToString();
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

            Directory.CreateDirectory(
                Path.GetDirectoryName(bundlePath));

            File.Copy(
                cachePath,
                bundlePath,
                true);

            return true;
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

            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationBundlePath));

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

            Directory.CreateDirectory(
                Path.GetDirectoryName(cachePath));

            File.Copy(
                bundlePath,
                cachePath,
                true);
        }
    }
}