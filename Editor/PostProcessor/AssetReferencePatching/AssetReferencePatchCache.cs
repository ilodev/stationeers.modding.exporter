using System.IO;

namespace stationeers.modding.exporter
{
    internal static class AssetReferencePatchCache
    {
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
    }
}