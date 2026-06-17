using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VRCAssetTracker
{
    public class AssetStoreScanResult
    {
        // "{Publisher}/{PackageName}" 窶・stable unique key
        public string CompositeId;
        public string DisplayName;
        public string PublisherName;
        public string LocalPackagePath;
        public List<PackageData> Packages;
        public int TotalFiles;
        public List<string> MatchedFiles;
        public string SuggestedTargetDir;
        public byte[] IconBytes;   // null if package has no embedded icon
    }

    public static class AssetStoreScanner
    {
        /// <summary>
        /// Scans assetStoreRoot recursively for *.unitypackage files.
        /// Publisher is determined from the first subfolder under the root.
        /// compositeId = "{Publisher}/{PackageNameWithoutExtension}"
        /// </summary>
        public static List<AssetStoreScanResult> ScanAll(
            string assetStoreRoot,
            HashSet<string> alreadyRegisteredIds,
            Action<string, float> progress,
            List<string> parseErrors)
        {
            var results = new List<AssetStoreScanResult>();

            if (!Directory.Exists(assetStoreRoot))
                return results;

            var pkgPaths = Directory.GetFiles(assetStoreRoot, "*.unitypackage", SearchOption.AllDirectories);
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            for (int i = 0; i < pkgPaths.Length; i++)
            {
                string pkgPath = pkgPaths[i];

                // Derive publisher from the first directory level below the root
                string relative    = pkgPath.Substring(assetStoreRoot.TrimEnd('\\', '/').Length).TrimStart('\\', '/');
                string[] parts     = relative.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                string publisher   = parts.Length > 0 ? parts[0] : "Unknown";
                string displayName = Path.GetFileNameWithoutExtension(pkgPath);
                string compositeId = $"{publisher}/{displayName}";

                progress?.Invoke($"{displayName} ({i + 1}/{pkgPaths.Length})", (float)i / pkgPaths.Length);

                if (alreadyRegisteredIds.Contains(compositeId))
                    continue;

                List<PackageData> packages;
                byte[] iconBytes;
                try
                {
                    var r = UnitypackageParser.Parse(pkgPath);
                    packages  = new List<PackageData> { new PackageData { fileName = r.FileName, files = r.Files } };
                    iconBytes = r.IconBytes;
                }
                catch (Exception ex)
                {
                    parseErrors.Add($"{compositeId}: {ex.Message}");
                    continue;
                }

                var allFilesSet = new HashSet<string>(packages[0].files, StringComparer.OrdinalIgnoreCase);
                var matchedFiles = allFilesSet
                    .Where(f => ExistsInProject(projectRoot, f))
                    .ToList();

                results.Add(new AssetStoreScanResult
                {
                    CompositeId       = compositeId,
                    DisplayName       = displayName,
                    PublisherName     = publisher,
                    LocalPackagePath  = pkgPath,
                    Packages          = packages,
                    TotalFiles        = allFilesSet.Count,
                    MatchedFiles      = matchedFiles,
                    SuggestedTargetDir = BoothScanner.SuggestTargetDir(matchedFiles),
                    IconBytes         = iconBytes,
                });
            }

            return results;
        }

        static bool ExistsInProject(string projectRoot, string assetPath)
        {
            string full = Path.Combine(projectRoot, assetPath);
            return File.Exists(full) || Directory.Exists(full);
        }
    }
}

