namespace stationeers.modding.exporter
{
    internal sealed class BuiltAssetReferencePatch
    {
        public ResolvedAssetReferencePatch Source { get; }

        /// <summary>
        /// Local PathID assigned by Unity to the proxy object
        /// inside built AssetBundles.
        /// </summary>
        public long ProxyBundlePathId { get; }

        public BuiltAssetReferencePatch(
            ResolvedAssetReferencePatch source,
            long proxyBundlePathId)
        {
            Source = source;
            ProxyBundlePathId = proxyBundlePathId;
        }

        public string TargetSerializedFile =>
            Source.Source.TargetSerializedFile;

        public long TargetPathId =>
            Source.Source.TargetPathId;

        public int TargetTypeId =>
            Source.Source.TargetTypeId;

        public AssetReferencePatchCleanup Cleanup =>
            Source.Source.Cleanup;
    }
}