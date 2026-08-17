using System;

namespace stationeers.modding.exporter
{
    internal sealed class ResolvedMonoScriptReferencePatch
    {
        public MonoScriptReferencePatch Source { get; }

        public string ProxyAssemblyName { get; }

        public string ProxyNamespace { get; }

        public string ProxyClassName { get; }

        public ResolvedMonoScriptReferencePatch(
            MonoScriptReferencePatch source)
        {
            Source = source
                ?? throw new ArgumentNullException(nameof(source));

            ProxyAssemblyName =
                source.ProxyType.Assembly.GetName().Name;

            ProxyNamespace =
                source.ProxyType.Namespace ?? string.Empty;

            ProxyClassName =
                source.ProxyType.Name;
        }

        public string ProxyIdentity =>
            $"{ProxyAssemblyName}:{ProxyNamespace}.{ProxyClassName}";

        public string TargetIdentity =>
            $"{Source.TargetAssemblyName}:" +
            $"{Source.TargetNamespace}.{Source.TargetClassName}";
    }
}
