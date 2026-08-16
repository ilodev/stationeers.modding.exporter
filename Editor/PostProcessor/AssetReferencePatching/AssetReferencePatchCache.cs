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
    }
}