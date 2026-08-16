using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    internal static class AssetReferencePatchRegistry
    {
        public static IReadOnlyList<AssetReferencePatch> Collect()
        {
            var collector = new Collector();

            var providerTypes =
                TypeCache
                    .GetTypesDerivedFrom<IAssetReferencePatchProvider>()
                    .Where(t =>
                        !t.IsAbstract &&
                        !t.IsInterface)
                    .OrderBy(t => t.FullName, StringComparer.Ordinal);

            foreach (var providerType in providerTypes)
            {
                try
                {
                    var provider =
                        (IAssetReferencePatchProvider)
                        Activator.CreateInstance(
                            providerType,
                            nonPublic: true);

                    provider.CollectPatches(collector);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed collecting asset reference patches from " +
                        $"provider '{providerType.FullName}'.",
                        ex);
                }
            }

            return collector.Patches;
        }

        private sealed class Collector : IAssetReferencePatchCollector
        {
            private readonly List<AssetReferencePatch> _patches =
                new List<AssetReferencePatch>();

            public IReadOnlyList<AssetReferencePatch> Patches =>
                _patches;

            public void Add(AssetReferencePatch patch)
            {
                if (patch == null)
                    throw new ArgumentNullException(nameof(patch));

                _patches.Add(patch);
            }
        }
    }
}