using System;
using UnityEngine;

namespace stationeers.modding.exporter
{
    public enum AssetReferencePatchCleanup
    {
        Keep,
        RemoveIfUnreferenced
    }

    public sealed class AssetReferencePatch
    {
        public UnityEngine.Object ProxyAsset { get; }

        public string TargetSerializedFile { get; }

        public long TargetPathId { get; }

        public int TargetTypeId { get; }

        public AssetReferencePatchCleanup Cleanup { get; }

        public AssetReferencePatch(
            UnityEngine.Object proxyAsset,
            string targetSerializedFile,
            long targetPathId,
            int targetTypeId,
            AssetReferencePatchCleanup cleanup =
                AssetReferencePatchCleanup.Keep)
        {
            ProxyAsset = proxyAsset
                ? proxyAsset
                : throw new ArgumentNullException(nameof(proxyAsset));

            TargetSerializedFile =
                !string.IsNullOrWhiteSpace(targetSerializedFile)
                    ? targetSerializedFile
                    : throw new ArgumentException(
                        "Target serialized file is required.",
                        nameof(targetSerializedFile));

            TargetPathId = targetPathId;
            TargetTypeId = targetTypeId;
            Cleanup = cleanup;
        }
    }

    public interface IAssetReferencePatchCollector
    {
        void Add(AssetReferencePatch patch);
    }

    public interface IAssetReferencePatchProvider
    {
        void CollectPatches(
            IAssetReferencePatchCollector collector);
    }
}