using System;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    internal static class VanillaScriptPocPatcher
    {
        private const string ProxyAssembly = "stationeers.modding.exporter.authoring";
        private const string ProxyNamespace = "Stationeers.EditorReferences";
        private const string ProxyClass = "AlwaysRenderObjectProxy";

        private const string VanillaFile = "globalgamemanagers.assets";
        private const long VanillaPathId = 2323;

        public static void PatchAlwaysRenderObject(
            string bundlePath)
        {
            if (!File.Exists(bundlePath))
            {
                throw new FileNotFoundException(
                    "Bundle not found.",
                    bundlePath);
            }

            Debug.Log(
                $"[Script PoC] Opening: {bundlePath}");

            var manager = new AssetsManager();

            var bundleInst =
                manager.LoadBundleFile(
                    bundlePath,
                    true);

            var bundle =
                bundleInst.file;

            var assetsInst =
                manager.LoadAssetsFileFromBundle(
                    bundleInst,
                    0,
                    false);

            var assets =
                assetsInst.file;

            // -----------------------------------------------------
            // Find our authoring MonoScript.
            // -----------------------------------------------------

            AssetFileInfo proxyScriptInfo = null;

            foreach (var info in
                     assets.GetAssetsOfType(
                         AssetClassID.MonoScript))
            {
                var script =
                    manager.GetBaseField(
                        assetsInst,
                        info);

                string assembly =
                    script["m_AssemblyName"].AsString;

                string ns =
                    script["m_Namespace"].AsString;

                string className =
                    script["m_ClassName"].AsString;

                if (!AssemblyNamesEqual(
                        assembly,
                        ProxyAssembly))
                {
                    continue;
                }

                if (!string.Equals(
                        ns,
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
                        "More than one matching proxy MonoScript found.");
                }

                proxyScriptInfo =
                    info;
            }

            if (proxyScriptInfo == null)
            {
                throw new InvalidOperationException(
                    $"Proxy MonoScript not found: " +
                    $"{ProxyAssembly} / " +
                    $"{ProxyNamespace} / " +
                    $"{ProxyClass}");
            }

            long proxyPathId =
                proxyScriptInfo.PathId;

            Debug.Log(
                $"[Script PoC] Proxy MonoScript PathID = " +
                $"{proxyPathId}");

            // -----------------------------------------------------
            // Find ScriptTypes entry.
            // -----------------------------------------------------

            int scriptTypeIndex =
                -1;

            for (int i = 0;
                 i < assets.Metadata.ScriptTypes.Count;
                 i++)
            {
                var pptr =
                    assets.Metadata.ScriptTypes[i];

                if (pptr.FileId == 0 &&
                    pptr.PathId == proxyPathId)
                {
                    if (scriptTypeIndex != -1)
                    {
                        throw new InvalidOperationException(
                            "More than one ScriptTypes entry " +
                            "references the proxy MonoScript.");
                    }

                    scriptTypeIndex =
                        i;
                }
            }

            if (scriptTypeIndex < 0)
            {
                throw new InvalidOperationException(
                    "No ScriptTypes entry references " +
                    $"proxy MonoScript {proxyPathId}.");
            }

            Debug.Log(
                $"[Script PoC] ScriptTypes index = " +
                $"{scriptTypeIndex}");

            // Your inspection should currently report 0.
            if (scriptTypeIndex != 0)
            {
                Debug.LogWarning(
                    $"[Script PoC] Expected index 0 from the " +
                    $"inspection, but got {scriptTypeIndex}. " +
                    "Continuing.");
            }

            // -----------------------------------------------------
            // Find MonoBehaviour(s) pointing to the proxy.
            // -----------------------------------------------------

            int matchingBehaviours =
                0;

            foreach (var info in
                     assets.GetAssetsOfType(
                         AssetClassID.MonoBehaviour))
            {
                var behaviour =
                    manager.GetBaseField(
                        assetsInst,
                        info);

                var scriptPPtr =
                    behaviour["m_Script"];

                int fileId =
                    scriptPPtr["m_FileID"].AsInt;

                long pathId =
                    scriptPPtr["m_PathID"].AsLong;

                if (fileId == 0 &&
                    pathId == proxyPathId)
                {
                    Debug.Log(
                        $"[Script PoC] Found matching " +
                        $"MonoBehaviour PathID {info.PathId}");

                    matchingBehaviours++;
                }
            }

            if (matchingBehaviours == 0)
            {
                throw new InvalidOperationException(
                    "Proxy MonoScript exists, but no " +
                    "MonoBehaviour references it.");
            }

            // -----------------------------------------------------
            // Add/reuse globalgamemanagers.assets.
            // -----------------------------------------------------

            int externalFileId =
                FindOrAddExternal(
                    assets,
                    VanillaFile);

            Debug.Log(
                $"[Script PoC] {VanillaFile} FileID = " +
                $"{externalFileId}");

            // -----------------------------------------------------
            // PATCH #1:
            // Metadata.ScriptTypes[index]
            //
            // BEFORE:
            //   0 / proxyPathId
            //
            // AFTER:
            //   externalFileId / 2323
            // -----------------------------------------------------

            var scriptTypePPtr =
                assets.Metadata.ScriptTypes[
                    scriptTypeIndex];

            Debug.Log(
                $"[Script PoC] ScriptTypes[{scriptTypeIndex}] " +
                $"before = " +
                $"{scriptTypePPtr.FileId}/" +
                $"{scriptTypePPtr.PathId}");

            scriptTypePPtr.FileId =
                externalFileId;

            scriptTypePPtr.PathId =
                VanillaPathId;

            assets.Metadata.ScriptTypes[
                scriptTypeIndex] =
                scriptTypePPtr;

            Debug.Log(
                $"[Script PoC] ScriptTypes[{scriptTypeIndex}] " +
                $"after = " +
                $"{externalFileId}/" +
                $"{VanillaPathId}");

            // -----------------------------------------------------
            // PATCH #2:
            // MonoBehaviour.m_Script
            // -----------------------------------------------------

            int patchedBehaviours =
                0;

            foreach (var info in
                     assets.GetAssetsOfType(
                         AssetClassID.MonoBehaviour))
            {
                var behaviour =
                    manager.GetBaseField(
                        assetsInst,
                        info);

                var scriptPPtr =
                    behaviour["m_Script"];

                int fileId =
                    scriptPPtr["m_FileID"].AsInt;

                long pathId =
                    scriptPPtr["m_PathID"].AsLong;

                if (fileId != 0 ||
                    pathId != proxyPathId)
                {
                    continue;
                }

                scriptPPtr["m_FileID"].AsInt =
                    externalFileId;

                scriptPPtr["m_PathID"].AsLong =
                    VanillaPathId;

                info.SetNewData(
                    behaviour);

                patchedBehaviours++;

                Debug.Log(
                    $"[Script PoC] Patched MonoBehaviour " +
                    $"PathID {info.PathId} -> " +
                    $"{externalFileId}/{VanillaPathId}");
            }

            if (patchedBehaviours == 0)
            {
                throw new InvalidOperationException(
                    "No MonoBehaviours were patched.");
            }

            // -----------------------------------------------------
            // Write SerializedFile back into bundle.
            // -----------------------------------------------------

            bundle.BlockAndDirInfo
                  .DirectoryInfos[0]
                  .SetNewData(assets);

            string uncompressedPath =
                bundlePath +
                ".script-poc-uncompressed";

            string packedPath =
                bundlePath +
                ".script-poc-packed";

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

            // -----------------------------------------------------
            // Repack LZ4.
            // -----------------------------------------------------

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
                $"[Script PoC] SUCCESS. " +
                $"{patchedBehaviours} MonoBehaviour(s) now " +
                $"point to {VanillaFile} / " +
                $"PathID {VanillaPathId}.");
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
                    // PPtr FileID is one-based.
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


        [MenuItem(
    "Tools/Stationeers Modding/PoC/Patch AlwaysRenderObject")]
        private static void PatchAlwaysRenderObjectFromMenu()
        {
            string bundlePath = EditorUtility.OpenFilePanel(
                "Select AssetBundle to patch",
                "",
                "");

            if (string.IsNullOrWhiteSpace(bundlePath))
                return;

            try
            {
                // Make a backup before modifying the bundle.
                string backupPath =
                    bundlePath + ".before-script-poc";

                File.Copy(
                    bundlePath,
                    backupPath,
                    true);

                Debug.Log(
                    $"[Script PoC] Backup created:\n{backupPath}");

                PatchAlwaysRenderObject(bundlePath);

                Debug.Log(
                    "[Script PoC] Patch complete. " +
                    "Run the MonoScript inspector on the patched bundle.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}