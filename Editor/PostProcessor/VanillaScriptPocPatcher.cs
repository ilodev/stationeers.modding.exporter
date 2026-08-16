using System;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEngine;

namespace stationeers.modding.exporter
{
    internal static class VanillaScriptPocPatcher
    {
        // ---------------------------------------------------------
        // EDITOR-SIDE TEST SCRIPT
        //
        // For this first PoC, make the Editor script have the same
        // managed identity as the real Stationeers script:
        //
        //     Assembly-CSharp
        //     namespace: ""
        //     class: AlwaysRenderObject
        //
        // This avoids testing scriptID translation at the same time.
        // ---------------------------------------------------------

        private const string ProxyAssembly =
            "Assembly-CSharp";

        private const string ProxyNamespace =
            "";

        private const string ProxyClass =
            "AlwaysRenderObject";

        // From StationeersMonoScripts.xml:
        //
        // <MonoScript
        //     assembly="Assembly-CSharp"
        //     namespace=""
        //     class="AlwaysRenderObject"
        //     file="globalgamemanagers.assets"
        //     pathId="2323"
        //     classId="115"
        //     kind="MonoBehaviour"
        //     baseType="UnityEngine.MonoBehaviour" />

        private const string VanillaFile =
            "globalgamemanagers.assets";

        private const long VanillaPathId = 2323;

        public static void PatchAlwaysRenderObject(string bundlePath)
        {
            if (!File.Exists(bundlePath))
            {
                throw new FileNotFoundException(
                    "Bundle not found.",
                    bundlePath);
            }

            Debug.Log(
                $"[Vanilla Script PoC] Opening: {bundlePath}");

            var manager = new AssetsManager();

            var bundleInst =
                manager.LoadBundleFile(bundlePath, true);

            var bundle = bundleInst.file;

            var assetsInst =
                manager.LoadAssetsFileFromBundle(
                    bundleInst,
                    0,
                    false);

            var assets = assetsInst.file;

            if (!assets.Metadata.TypeTreeEnabled)
            {
                throw new InvalidOperationException(
                    "PoC bundle has stripped TypeTrees. " +
                    "This test currently expects the built bundle " +
                    "to contain TypeTrees.");
            }

            // ---------------------------------------------------------
            // Find the locally embedded Editor MonoScript.
            // ---------------------------------------------------------

            AssetFileInfo proxyScriptInfo = null;

            foreach (var info in
                     assets.GetAssetsOfType(
                         AssetClassID.MonoScript))
            {
                var script =
                    manager.GetBaseField(
                        assetsInst,
                        info);

                string assemblyName =
                    script["m_AssemblyName"].AsString;

                string namespaceName =
                    script["m_Namespace"].AsString;

                string className =
                    script["m_ClassName"].AsString;

                if (!AssemblyNamesEqual(
                        assemblyName,
                        ProxyAssembly))
                {
                    continue;
                }

                if (!string.Equals(
                        namespaceName,
                        ProxyNamespace,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(
                        className,
                        ProxyClass,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (proxyScriptInfo != null)
                {
                    throw new InvalidOperationException(
                        $"More than one local MonoScript matched " +
                        $"{ProxyAssembly} / " +
                        $"{ProxyNamespace} / " +
                        $"{ProxyClass}.");
                }

                proxyScriptInfo = info;
            }

            if (proxyScriptInfo == null)
            {
                throw new InvalidOperationException(
                    $"Local MonoScript not found: " +
                    $"{ProxyAssembly} / " +
                    $"{ProxyNamespace} / " +
                    $"{ProxyClass}");
            }

            long proxyScriptPathId =
                proxyScriptInfo.PathId;

            Debug.Log(
                $"[Vanilla Script PoC] " +
                $"Proxy MonoScript local PathID = " +
                $"{proxyScriptPathId}");

            // ---------------------------------------------------------
            // Add/reuse globalgamemanagers.assets external.
            // ---------------------------------------------------------

            int externalFileId =
                FindOrAddExternal(
                    assets,
                    VanillaFile);

            Debug.Log(
                $"[Vanilla Script PoC] " +
                $"{VanillaFile} m_FileID = " +
                $"{externalFileId}");

            // ---------------------------------------------------------
            // Patch SerializedFile Metadata.ScriptTypes.
            //
            // Find any ScriptTypes entry pointing at the local
            // MonoScript:
            //
            //     fileID = 0
            //     pathID = proxyScriptPathId
            //
            // and redirect it to:
            //
            //     globalgamemanagers.assets
            //     PathID 2323
            //
            // We intentionally KEEP THE SAME SCRIPT TYPE INDEX.
            // ---------------------------------------------------------

            int patchedScriptTypes = 0;

            var scriptTypes =
                assets.Metadata.ScriptTypes;

            for (int i = 0;
                 i < scriptTypes.Count;
                 i++)
            {
                var scriptType =
                    scriptTypes[i];

                if (scriptType.FileId != 0 ||
                    scriptType.PathId != proxyScriptPathId)
                {
                    continue;
                }

                Debug.Log(
                    $"[Vanilla Script PoC] " +
                    $"ScriptTypes[{i}] before: " +
                    $"{scriptType.FileId}/" +
                    $"{scriptType.PathId}");

                scriptType.FileId =
                    externalFileId;

                scriptType.PathId =
                    VanillaPathId;

                // Important if AssetPPtr happens to be a value type
                // in the AT.NET version in use.
                scriptTypes[i] =
                    scriptType;

                patchedScriptTypes++;

                Debug.Log(
                    $"[Vanilla Script PoC] " +
                    $"ScriptTypes[{i}] after: " +
                    $"{externalFileId}/" +
                    $"{VanillaPathId}");
            }

            if (patchedScriptTypes == 0)
            {
                throw new InvalidOperationException(
                    $"Found local MonoScript PathID " +
                    $"{proxyScriptPathId}, but no Metadata.ScriptTypes " +
                    $"entry referenced it.");
            }

            // For this first PoC I would expect exactly one.
            if (patchedScriptTypes != 1)
            {
                Debug.LogWarning(
                    $"[Vanilla Script PoC] " +
                    $"Patched {patchedScriptTypes} ScriptTypes entries. " +
                    $"Expected one; continuing for inspection.");
            }

            // ---------------------------------------------------------
            // Rewrite MonoBehaviour.m_Script references.
            // ---------------------------------------------------------

            int patchedMonoBehaviours = 0;

            foreach (var info in
                     assets.GetAssetsOfType(
                         AssetClassID.MonoBehaviour))
            {
                var monoBehaviour =
                    manager.GetBaseField(
                        assetsInst,
                        info);

                var scriptPPtr =
                    monoBehaviour["m_Script"];

                int fileId =
                    scriptPPtr["m_FileID"].AsInt;

                long pathId =
                    scriptPPtr["m_PathID"].AsLong;

                if (fileId != 0 ||
                    pathId != proxyScriptPathId)
                {
                    continue;
                }

                Debug.Log(
                    $"[Vanilla Script PoC] " +
                    $"Patching MonoBehaviour PathID {info.PathId}");

                scriptPPtr["m_FileID"].AsInt =
                    externalFileId;

                scriptPPtr["m_PathID"].AsLong =
                    VanillaPathId;

                info.SetNewData(
                    monoBehaviour);

                patchedMonoBehaviours++;
            }

            if (patchedMonoBehaviours == 0)
            {
                throw new InvalidOperationException(
                    $"Found proxy MonoScript PathID " +
                    $"{proxyScriptPathId}, but no MonoBehaviour " +
                    $"referenced it.");
            }

            Debug.Log(
                $"[Vanilla Script PoC] Rewrote " +
                $"{patchedMonoBehaviours} MonoBehaviour(s).");

            Debug.Log(
                $"[Vanilla Script PoC] Rewrote " +
                $"{patchedScriptTypes} ScriptTypes entry/entries.");

            // ---------------------------------------------------------
            // Put modified SerializedFile back into bundle.
            // ---------------------------------------------------------

            bundle.BlockAndDirInfo
                  .DirectoryInfos[0]
                  .SetNewData(assets);

            string uncompressedPath =
                bundlePath +
                ".vanilla-script-poc-uncompressed";

            string packedPath =
                bundlePath +
                ".vanilla-script-poc-packed";

            if (File.Exists(uncompressedPath))
                File.Delete(uncompressedPath);

            if (File.Exists(packedPath))
                File.Delete(packedPath);

            using (var writer =
                   new AssetsFileWriter(
                       uncompressedPath))
            {
                bundle.Write(writer);
            }

            manager.UnloadAll();

            // ---------------------------------------------------------
            // Repack LZ4 exactly like the material PoC.
            // ---------------------------------------------------------

            var uncompressedBundle =
                new AssetBundleFile();

            using (var stream =
                   File.OpenRead(
                       uncompressedPath))
            {
                uncompressedBundle.Read(
                    new AssetsFileReader(
                        stream));

                using (var writer =
                       new AssetsFileWriter(
                           packedPath))
                {
                    uncompressedBundle.Pack(
                        writer,
                        AssetBundleCompressionType.LZ4);
                }
            }

            uncompressedBundle.Close();

            File.Delete(
                uncompressedPath);

            File.Delete(
                bundlePath);

            File.Move(
                packedPath,
                bundlePath);

            Debug.Log(
                "[Vanilla Script PoC] SUCCESS. " +
                $"MonoBehaviour now references " +
                $"{VanillaFile} / " +
                $"PathID {VanillaPathId}.");
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
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.EndsWith(
                    ".dll",
                    StringComparison.OrdinalIgnoreCase))
            {
                return value.Substring(
                    0,
                    value.Length - 4);
            }

            return value;
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
                var external =
                    externals[i];

                if (string.Equals(
                        external.PathName,
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
                    VirtualAssetPathName =
                        string.Empty,

                    Guid =
                        new GUID128(),

                    Type =
                        AssetsFileExternalType.Normal,

                    PathName =
                        path,

                    OriginalPathName =
                        string.Empty
                });

            return externals.Count;
        }
    }
}