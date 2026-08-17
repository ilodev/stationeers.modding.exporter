using System;
using System.Collections.Generic;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace stationeers.modding.exporter
{
    internal static class AssetReferencePPtrRewriter
    {
        public static int Rewrite(
            AssetsManager manager,
            AssetsFileInstance assetsInstance,
            IReadOnlyList<BuiltAssetReferencePatch> patches)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            if (assetsInstance == null)
                throw new ArgumentNullException(nameof(assetsInstance));

            if (patches == null)
                throw new ArgumentNullException(nameof(patches));

            if (patches.Count == 0)
                return 0;

            var assets = assetsInstance.file;

            var patchesByProxyPathId =
                BuildPatchLookup(patches);

            var externalFileIds =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            int totalRewritten = 0;

            foreach (var info in assets.AssetInfos)
            {
                // AssetBundle.m_Container is bundle bookkeeping, not an
                // ordinary serialized asset reference. Rewriting its PPtrs
                // would leave proxy asset paths advertised by the finished
                // bundle while pointing those paths at external game assets.
                // Cleanup handles m_Container explicitly instead.
                if ((AssetClassID)info.TypeId == AssetClassID.AssetBundle)
                    continue;

                var baseField =
                    manager.GetBaseField(
                        assetsInstance,
                        info);

                bool changed = RewriteFieldRecursive(
                    baseField,
                    assets,
                    patchesByProxyPathId,
                    externalFileIds,
                    ref totalRewritten);

                if (changed)
                    info.SetNewData(baseField);
            }

            return totalRewritten;
        }

        private static Dictionary<long, BuiltAssetReferencePatch>
            BuildPatchLookup(
                IReadOnlyList<BuiltAssetReferencePatch> patches)
        {
            var result =
                new Dictionary<long, BuiltAssetReferencePatch>();

            foreach (var patch in patches)
            {
                long proxyPathId =
                    patch.ProxyBundlePathId;

                if (result.TryGetValue(
                        proxyPathId,
                        out var existing))
                {
                    if (!string.Equals(
                            existing.TargetSerializedFile,
                            patch.TargetSerializedFile,
                            StringComparison.OrdinalIgnoreCase) ||
                        existing.TargetPathId != patch.TargetPathId)
                    {
                        throw new InvalidOperationException(
                            $"Conflicting asset-reference patches for " +
                            $"proxy PathID {proxyPathId}.");
                    }

                    continue;
                }

                result.Add(
                    proxyPathId,
                    patch);
            }

            return result;
        }

        private static bool RewriteFieldRecursive(
            AssetTypeValueField field,
            AssetsFile assets,
            IReadOnlyDictionary<long, BuiltAssetReferencePatch>
                patchesByProxyPathId,
            Dictionary<string, int> externalFileIds,
            ref int totalRewritten)
        {
            if (field == null || field.IsDummy)
                return false;

            bool changed = false;

            // A serialized Unity PPtr<T> has:
            //
            // PPtr<Something>
            //     m_FileID
            //     m_PathID
            //
            // Check the type as well as the child names so we don't
            // accidentally rewrite an unrelated structure which happens
            // to contain similarly named fields.
            if (IsPPtr(field))
            {
                var fileIdField = field["m_FileID"];
                var pathIdField = field["m_PathID"];

                // We only replace references to objects local to this
                // SerializedFile. Existing external references must be left
                // untouched.
                if (!fileIdField.IsDummy &&
                    !pathIdField.IsDummy &&
                    fileIdField.AsInt == 0)
                {
                    long localPathId =
                        pathIdField.AsLong;

                    if (patchesByProxyPathId.TryGetValue(
                            localPathId,
                            out var patch))
                    {
                        int targetFileId =
                            GetOrAddExternalFileId(
                                assets,
                                patch.TargetSerializedFile,
                                externalFileIds);

                        fileIdField.AsInt =
                            targetFileId;

                        pathIdField.AsLong =
                            patch.TargetPathId;

                        totalRewritten++;
                        changed = true;

                        // Nothing below a PPtr needs further traversal.
                        return true;
                    }
                }
            }

            foreach (var child in field.Children)
            {
                if (RewriteFieldRecursive(
                        child,
                        assets,
                        patchesByProxyPathId,
                        externalFileIds,
                        ref totalRewritten))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsPPtr(
            AssetTypeValueField field)
        {
            string typeName =
                field.TypeName;

            if (string.IsNullOrEmpty(typeName) ||
                !typeName.StartsWith(
                    "PPtr<",
                    StringComparison.Ordinal))
            {
                return false;
            }

            return
                !field["m_FileID"].IsDummy &&
                !field["m_PathID"].IsDummy;
        }

        private static int GetOrAddExternalFileId(
            AssetsFile assets,
            string targetSerializedFile,
            Dictionary<string, int> cache)
        {
            if (cache.TryGetValue(
                    targetSerializedFile,
                    out int cachedFileId))
            {
                return cachedFileId;
            }

            var externals =
                assets.Metadata.Externals;

            for (int i = 0; i < externals.Count; i++)
            {
                if (string.Equals(
                        externals[i].PathName,
                        targetSerializedFile,
                        StringComparison.OrdinalIgnoreCase))
                {
                    // PPtr m_FileID is 1-based.
                    int fileId = i + 1;

                    cache[targetSerializedFile] =
                        fileId;

                    return fileId;
                }
            }

            externals.Add(
                new AssetsFileExternal
                {
                    VirtualAssetPathName = string.Empty,
                    Guid = new GUID128(),
                    Type = AssetsFileExternalType.Normal,
                    PathName = targetSerializedFile,
                    OriginalPathName = string.Empty
                });

            int newFileId =
                externals.Count;

            cache[targetSerializedFile] =
                newFileId;

            return newFileId;
        }
    }
}