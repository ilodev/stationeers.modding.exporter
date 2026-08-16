using System;
using System.Collections.Generic;

namespace stationeers.modding.exporter
{
    internal sealed class AssetReferenceBundlePatchResult
    {
        public int RewrittenReferenceCount { get; }
        public int RemovedProxyCount => RemovedProxyAssetPaths.Count;
        public IReadOnlyList<string> RemovedProxyAssetPaths { get; }

        public AssetReferenceBundlePatchResult(
            int rewrittenReferenceCount,
            IReadOnlyList<string> removedProxyAssetPaths)
        {
            RewrittenReferenceCount = rewrittenReferenceCount;
            RemovedProxyAssetPaths = removedProxyAssetPaths
                ?? throw new ArgumentNullException(nameof(removedProxyAssetPaths));
        }

        public static AssetReferenceBundlePatchResult Empty { get; } =
            new AssetReferenceBundlePatchResult(
                0,
                Array.Empty<string>());
    }
}
