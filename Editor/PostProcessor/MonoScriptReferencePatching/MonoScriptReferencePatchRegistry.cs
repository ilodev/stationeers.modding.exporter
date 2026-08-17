using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace stationeers.modding.exporter
{
    internal static class MonoScriptReferencePatchRegistry
    {
        public static IReadOnlyList<MonoScriptReferencePatch> Collect()
        {
            var collector = new Collector();

            var providerTypes =
                TypeCache
                    .GetTypesDerivedFrom<IMonoScriptReferencePatchProvider>()
                    .Where(t =>
                        !t.IsAbstract &&
                        !t.IsInterface)
                    .OrderBy(t => t.FullName, StringComparer.Ordinal);

            foreach (var providerType in providerTypes)
            {
                try
                {
                    var provider =
                        (IMonoScriptReferencePatchProvider)
                        Activator.CreateInstance(
                            providerType,
                            nonPublic: true);

                    provider.CollectPatches(collector);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed collecting MonoScript reference patches " +
                        $"from provider '{providerType.FullName}'.",
                        ex);
                }
            }

            return collector.Patches;
        }

        public static IReadOnlyList<ResolvedMonoScriptReferencePatch> Resolve(
            IReadOnlyList<MonoScriptReferencePatch> patches)
        {
            if (patches == null)
                throw new ArgumentNullException(nameof(patches));

            var resolved =
                patches
                    .Select(p => new ResolvedMonoScriptReferencePatch(p))
                    .OrderBy(p => p.ProxyAssemblyName, StringComparer.Ordinal)
                    .ThenBy(p => p.ProxyNamespace, StringComparer.Ordinal)
                    .ThenBy(p => p.ProxyClassName, StringComparer.Ordinal)
                    .ToList();

            foreach (var group in resolved.GroupBy(
                p => p.ProxyIdentity,
                StringComparer.Ordinal))
            {
                if (group.Count() <= 1)
                    continue;

                var targets = group
                    .Select(p =>
                        $"{p.Source.TargetSerializedFile}/" +
                        $"{p.Source.TargetPathId} ({p.TargetIdentity})")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                throw new InvalidOperationException(
                    $"More than one MonoScript reference patch was " +
                    $"registered for proxy type '{group.Key}'. Targets: " +
                    string.Join(", ", targets));
            }

            return resolved;
        }

        private sealed class Collector : IMonoScriptReferencePatchCollector
        {
            private readonly List<MonoScriptReferencePatch> _patches =
                new List<MonoScriptReferencePatch>();

            public IReadOnlyList<MonoScriptReferencePatch> Patches =>
                _patches;

            public void Add(MonoScriptReferencePatch patch)
            {
                if (patch == null)
                    throw new ArgumentNullException(nameof(patch));

                _patches.Add(patch);
            }
        }
    }
}
