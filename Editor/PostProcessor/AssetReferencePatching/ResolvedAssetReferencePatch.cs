using System;
using UnityEditor;

namespace stationeers.modding.exporter
{
    internal sealed class ResolvedAssetReferencePatch
    {
        public AssetReferencePatch Source { get; }

        public string ProxyAssetPath { get; }
        public string ProxyGuid { get; }
        public long ProxyLocalFileId { get; }

        public ResolvedAssetReferencePatch(
            AssetReferencePatch source,
            string proxyAssetPath,
            string proxyGuid,
            long proxyLocalFileId)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ProxyAssetPath = proxyAssetPath;
            ProxyGuid = proxyGuid;
            ProxyLocalFileId = proxyLocalFileId;
        }
    }
}