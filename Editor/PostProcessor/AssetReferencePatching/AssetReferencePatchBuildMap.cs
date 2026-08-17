using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Builds the explicit AssetBundle map used while asset-reference patching
    /// is enabled. Registered proxy assets are added as explicit roots of the
    /// real assets bundle so their final PathIDs can be read from m_Container.
    /// </summary>
    internal static class AssetReferencePatchBuildMap
    {
        public static AssetBundleBuild[] Create(
            string bundleName,
            IReadOnlyList<string> assetPaths,
            IReadOnlyList<string> scenePaths,
            IReadOnlyList<ResolvedAssetReferencePatch> patches)
        {
            if (string.IsNullOrWhiteSpace(bundleName))
                throw new ArgumentException("Bundle name is required.", nameof(bundleName));

            if (assetPaths == null)
                throw new ArgumentNullException(nameof(assetPaths));

            if (scenePaths == null)
                throw new ArgumentNullException(nameof(scenePaths));

            if (patches == null)
                throw new ArgumentNullException(nameof(patches));

            var assets = new HashSet<string>(
                assetPaths,
                StringComparer.OrdinalIgnoreCase);

            foreach (var patch in patches)
                assets.Add(patch.ProxyAssetPath);

            var builds = new List<AssetBundleBuild>(2);

            if (assets.Count > 0)
            {
                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetBundleVariant = "assets",
                    assetNames = assets
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .ToArray()
                });
            }

            if (scenePaths.Count > 0)
            {
                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetBundleVariant = "scenes",
                    assetNames = scenePaths
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .ToArray()
                });
            }

            return builds.ToArray();
        }
    }
}
