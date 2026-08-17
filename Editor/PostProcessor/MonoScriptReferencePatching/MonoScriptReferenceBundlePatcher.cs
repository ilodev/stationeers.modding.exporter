using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Rewrites bundle-local authoring MonoScript references so the serialized
    /// MonoBehaviour points directly at a MonoScript in an external Player
    /// SerializedFile.
    ///
    /// The proven Stationeers/Unity 2022.3 PoC requires two coordinated writes:
    /// - MonoBehaviour.m_Script
    /// - AssetsFile.Metadata.ScriptTypes[scriptTypeIndex]
    ///
    /// The bundle-local MonoScript object is deliberately retained. It is useful
    /// for deterministic post-build discovery and does not need to exist at
    /// runtime once nothing references it.
    /// </summary>
    internal static class MonoScriptReferenceBundlePatcher
    {
        public static MonoScriptReferenceBundlePatchResult Patch(
            string bundlePath,
            IReadOnlyList<ResolvedMonoScriptReferencePatch> patches)
        {
            if (string.IsNullOrWhiteSpace(bundlePath))
                throw new ArgumentNullException(nameof(bundlePath));

            if (!File.Exists(bundlePath))
                throw new FileNotFoundException(
                    "AssetBundle not found.",
                    bundlePath);

            if (patches == null)
                throw new ArgumentNullException(nameof(patches));

            if (patches.Count == 0)
                return MonoScriptReferenceBundlePatchResult.Empty;

            string uncompressedPath =
                bundlePath + ".monoscript-patching.uncompressed";

            string packedPath =
                bundlePath + ".monoscript-patching";

            DeleteIfExists(uncompressedPath);
            DeleteIfExists(packedPath);

            var manager = new AssetsManager();

            try
            {
                var bundleInst =
                    manager.LoadBundleFile(
                        bundlePath,
                        true);

                var bundle = bundleInst.file;

                var assetsInst =
                    manager.LoadAssetsFileFromBundle(
                        bundleInst,
                        0,
                        false);

                var assets = assetsInst.file;

                var monoScripts =
                    ReadLocalMonoScripts(
                        manager,
                        assetsInst);

                int matchedMappings = 0;
                int rewrittenScriptTypes = 0;
                int rewrittenBehaviours = 0;

                foreach (var patch in patches)
                {
                    if (!TryFindProxyMonoScript(
                            monoScripts,
                            patch,
                            out var proxyScriptInfo))
                    {
                        // Providers are global to the Editor domain. A mapping
                        // does not have to appear in every AssetBundle.
                        continue;
                    }

                    long proxyPathId = proxyScriptInfo.PathId;

                    var localScriptTypeIndexes =
                        FindScriptTypeIndexes(
                            assets,
                            0,
                            proxyPathId);

                    var localBehaviourInfos =
                        FindMonoBehavioursByScript(
                            manager,
                            assetsInst,
                            0,
                            proxyPathId);

                    if (localScriptTypeIndexes.Count == 0 &&
                        localBehaviourInfos.Count == 0)
                    {
                        // Most likely an already-patched bundle or an orphaned
                        // local MonoScript. Do not invent a ScriptTypes index.
                        // Normal exporter builds restore the pristine bundle
                        // before Unity's incremental build, so this state is not
                        // required for the production path.
                        Debug.Log(
                            $"MonoScript reference patching: proxy " +
                            $"'{patch.ProxyIdentity}' is present but has no " +
                            $"local serialized references; skipping.");

                        continue;
                    }

                    if (localScriptTypeIndexes.Count != 1)
                    {
                        throw new InvalidOperationException(
                            $"Proxy MonoScript '{patch.ProxyIdentity}' " +
                            $"(PathID {proxyPathId}) is referenced by " +
                            $"{localScriptTypeIndexes.Count} ScriptTypes " +
                            $"entries; exactly one was expected.");
                    }

                    if (localBehaviourInfos.Count == 0)
                    {
                        Debug.LogWarning(
                            $"MonoScript reference patching: proxy " +
                            $"'{patch.ProxyIdentity}' has a ScriptTypes entry " +
                            $"but no MonoBehaviour instances in this bundle.");
                    }

                    int externalFileId =
                        FindOrAddExternal(
                            assets,
                            patch.Source.TargetSerializedFile);

                    int scriptTypeIndex =
                        localScriptTypeIndexes[0];

                    var scriptTypePPtr =
                        assets.Metadata.ScriptTypes[scriptTypeIndex];

                    scriptTypePPtr.FileId = externalFileId;
                    scriptTypePPtr.PathId = patch.Source.TargetPathId;

                    // Assign back explicitly in case the AT.NET version exposes
                    // the PPtr as a value type.
                    assets.Metadata.ScriptTypes[scriptTypeIndex] =
                        scriptTypePPtr;

                    rewrittenScriptTypes++;

                    foreach (var behaviourInfo in localBehaviourInfos)
                    {
                        var behaviour =
                            manager.GetBaseField(
                                assetsInst,
                                behaviourInfo);

                        var scriptPPtr =
                            behaviour["m_Script"];

                        scriptPPtr["m_FileID"].AsInt =
                            externalFileId;

                        scriptPPtr["m_PathID"].AsLong =
                            patch.Source.TargetPathId;

                        behaviourInfo.SetNewData(behaviour);
                        rewrittenBehaviours++;
                    }

                    matchedMappings++;

                    Debug.Log(
                        $"MonoScript reference patching: " +
                        $"'{patch.ProxyIdentity}' -> " +
                        $"'{patch.TargetIdentity}', " +
                        $"{patch.Source.TargetSerializedFile}/" +
                        $"{patch.Source.TargetPathId}; " +
                        $"ScriptTypes[{scriptTypeIndex}], " +
                        $"{localBehaviourInfos.Count} MonoBehaviour(s).");
                }

                if (rewrittenScriptTypes == 0 &&
                    rewrittenBehaviours == 0)
                {
                    Debug.Log(
                        "MonoScript reference patching: no changes required.");

                    return new MonoScriptReferenceBundlePatchResult(
                        matchedMappings,
                        0,
                        0);
                }

                bundle.BlockAndDirInfo
                    .DirectoryInfos[0]
                    .SetNewData(assets);

                using (var writer =
                       new AssetsFileWriter(uncompressedPath))
                {
                    bundle.Write(writer);
                }

                manager.UnloadAll();

                var modifiedBundle =
                    new AssetBundleFile();

                using (var stream =
                       File.OpenRead(uncompressedPath))
                {
                    var reader =
                        new AssetsFileReader(stream);

                    modifiedBundle.Read(reader);

                    using (var writer =
                           new AssetsFileWriter(packedPath))
                    {
                        modifiedBundle.Pack(
                            writer,
                            AssetBundleCompressionType.LZ4);
                    }

                    modifiedBundle.Close();
                }

                ReplaceFile(
                    packedPath,
                    bundlePath);

                DeleteIfExists(uncompressedPath);

                Debug.Log(
                    $"MonoScript reference patching complete: " +
                    $"{matchedMappings} mapping(s), " +
                    $"{rewrittenScriptTypes} ScriptTypes entry/entries, " +
                    $"{rewrittenBehaviours} MonoBehaviour(s) rewritten.");

                return new MonoScriptReferenceBundlePatchResult(
                    matchedMappings,
                    rewrittenScriptTypes,
                    rewrittenBehaviours);
            }
            finally
            {
                manager.UnloadAll();
                DeleteIfExists(packedPath);
                DeleteIfExists(uncompressedPath);
            }
        }

        private static IReadOnlyList<LocalMonoScript> ReadLocalMonoScripts(
            AssetsManager manager,
            AssetsFileInstance assetsInstance)
        {
            var result = new List<LocalMonoScript>();

            foreach (var info in
                     assetsInstance.file.GetAssetsOfType(
                         AssetClassID.MonoScript))
            {
                var script =
                    manager.GetBaseField(
                        assetsInstance,
                        info);

                result.Add(
                    new LocalMonoScript(
                        info,
                        script["m_AssemblyName"].AsString,
                        script["m_Namespace"].AsString,
                        script["m_ClassName"].AsString));
            }

            return result;
        }

        private static bool TryFindProxyMonoScript(
            IReadOnlyList<LocalMonoScript> monoScripts,
            ResolvedMonoScriptReferencePatch patch,
            out AssetFileInfo info)
        {
            var matches = monoScripts
                .Where(s =>
                    AssemblyNamesEqual(
                        s.AssemblyName,
                        patch.ProxyAssemblyName) &&
                    string.Equals(
                        s.Namespace,
                        patch.ProxyNamespace,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        s.ClassName,
                        patch.ProxyClassName,
                        StringComparison.Ordinal))
                .ToArray();

            if (matches.Length == 0)
            {
                info = null;
                return false;
            }

            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Found {matches.Length} local MonoScript objects for " +
                    $"proxy type '{patch.ProxyIdentity}'.");
            }

            info = matches[0].Info;
            return true;
        }

        private static List<int> FindScriptTypeIndexes(
            AssetsFile assets,
            int fileId,
            long pathId)
        {
            var result = new List<int>();

            for (int i = 0;
                 i < assets.Metadata.ScriptTypes.Count;
                 i++)
            {
                var scriptType =
                    assets.Metadata.ScriptTypes[i];

                if (scriptType.FileId == fileId &&
                    scriptType.PathId == pathId)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static List<AssetFileInfo> FindMonoBehavioursByScript(
            AssetsManager manager,
            AssetsFileInstance assetsInstance,
            int fileId,
            long pathId)
        {
            var result = new List<AssetFileInfo>();

            foreach (var info in
                     assetsInstance.file.GetAssetsOfType(
                         AssetClassID.MonoBehaviour))
            {
                var behaviour =
                    manager.GetBaseField(
                        assetsInstance,
                        info);

                var scriptPPtr =
                    behaviour["m_Script"];

                if (scriptPPtr["m_FileID"].AsInt == fileId &&
                    scriptPPtr["m_PathID"].AsLong == pathId)
                {
                    result.Add(info);
                }
            }

            return result;
        }

        private static int FindOrAddExternal(
            AssetsFile assets,
            string path)
        {
            var externals =
                assets.Metadata.Externals;

            for (int i = 0;
                 i < externals.Count;
                 i++)
            {
                if (string.Equals(
                        externals[i].PathName,
                        path,
                        StringComparison.OrdinalIgnoreCase))
                {
                    // PPtr m_FileID is 1-based.
                    return i + 1;
                }
            }

            externals.Add(
                new AssetsFileExternal
                {
                    VirtualAssetPathName = string.Empty,
                    Guid = new GUID128(),
                    Type = AssetsFileExternalType.Normal,
                    PathName = path,
                    OriginalPathName = string.Empty
                });

            return externals.Count;
        }

        private static bool AssemblyNamesEqual(
            string a,
            string b)
        {
            return string.Equals(
                NormalizeAssemblyName(a),
                NormalizeAssemblyName(b),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssemblyName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.EndsWith(
                    ".dll",
                    StringComparison.OrdinalIgnoreCase)
                ? value.Substring(0, value.Length - 4)
                : value;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void ReplaceFile(
            string source,
            string destination)
        {
            string backup =
                destination + ".pre-monoscript-patch";

            DeleteIfExists(backup);

            File.Move(
                destination,
                backup);

            try
            {
                File.Move(
                    source,
                    destination);

                File.Delete(backup);
            }
            catch
            {
                DeleteIfExists(destination);

                if (File.Exists(backup))
                {
                    File.Move(
                        backup,
                        destination);
                }

                throw;
            }
        }

        private sealed class LocalMonoScript
        {
            public AssetFileInfo Info { get; }
            public string AssemblyName { get; }
            public string Namespace { get; }
            public string ClassName { get; }

            public LocalMonoScript(
                AssetFileInfo info,
                string assemblyName,
                string @namespace,
                string className)
            {
                Info = info;
                AssemblyName = assemblyName ?? string.Empty;
                Namespace = @namespace ?? string.Empty;
                ClassName = className ?? string.Empty;
            }
        }
    }
}
