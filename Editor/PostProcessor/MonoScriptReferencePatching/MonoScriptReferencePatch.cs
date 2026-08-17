using System;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Declares an export-time replacement from an authoring MonoBehaviour type
    /// to a MonoScript object that already exists in an external SerializedFile.
    /// </summary>
    public sealed class MonoScriptReferencePatch
    {
        public Type ProxyType { get; }

        public string TargetSerializedFile { get; }

        public long TargetPathId { get; }

        public string TargetAssemblyName { get; }

        public string TargetNamespace { get; }

        public string TargetClassName { get; }

        public MonoScriptReferencePatch(
            Type proxyType,
            string targetSerializedFile,
            long targetPathId,
            string targetAssemblyName,
            string targetNamespace,
            string targetClassName)
        {
            ProxyType = proxyType
                ?? throw new ArgumentNullException(nameof(proxyType));

            if (!typeof(MonoBehaviour).IsAssignableFrom(proxyType))
            {
                throw new ArgumentException(
                    $"Proxy type '{proxyType.FullName}' must derive from " +
                    $"{typeof(MonoBehaviour).FullName}.",
                    nameof(proxyType));
            }

            if (proxyType.IsAbstract)
            {
                throw new ArgumentException(
                    $"Proxy type '{proxyType.FullName}' must be concrete.",
                    nameof(proxyType));
            }

            TargetSerializedFile =
                !string.IsNullOrWhiteSpace(targetSerializedFile)
                    ? targetSerializedFile
                    : throw new ArgumentException(
                        "Target serialized file is required.",
                        nameof(targetSerializedFile));

            TargetPathId = targetPathId;

            TargetAssemblyName =
                !string.IsNullOrWhiteSpace(targetAssemblyName)
                    ? targetAssemblyName
                    : throw new ArgumentException(
                        "Target assembly name is required.",
                        nameof(targetAssemblyName));

            TargetNamespace = targetNamespace ?? string.Empty;

            TargetClassName =
                !string.IsNullOrWhiteSpace(targetClassName)
                    ? targetClassName
                    : throw new ArgumentException(
                        "Target class name is required.",
                        nameof(targetClassName));
        }
    }

    public interface IMonoScriptReferencePatchCollector
    {
        void Add(MonoScriptReferencePatch patch);
    }

    public interface IMonoScriptReferencePatchProvider
    {
        void CollectPatches(
            IMonoScriptReferencePatchCollector collector);
    }
}
