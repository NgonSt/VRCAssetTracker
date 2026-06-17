using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VRCAssetTracker
{
    public class ScanResult
    {
        public string            BoothItemId;
        public List<PackageData> Packages;        // JSON 菫晏ｭ倡畑
        public int               TotalFiles;      // 繝代ャ繧ｱ繝ｼ繧ｸ讓ｪ譁ｭ繝ｦ繝九・繧ｯ謨ｰ
        public List<string>      MatchedFiles;    // 繝励Ο繧ｸ繧ｧ繧ｯ繝医↓蟄伜惠縺吶ｋ繝代せ縺ｮ縺ｿ
        public string            SuggestedTargetDir; // "" = 閾ｪ蜍墓ｱｺ螳壻ｸ榊庄
    }

    public static class BoothScanner
    {
        static readonly Regex BoothDirPattern = new Regex(@"^b(\d+)$");

        /// <summary>
        /// boothRoot 驟堺ｸ九・ b{ID} 繝・ぅ繝ｬ繧ｯ繝医Μ繧貞・繧ｹ繧ｭ繝｣繝ｳ縺吶ｋ縲・        /// alreadyRegisteredIds 縺ｫ蜷ｫ縺ｾ繧後ｋ蝠・刀縺ｯ繧ｹ繧ｭ繝・・縲・        /// </summary>
        public static List<ScanResult> ScanAll(
            string               boothRoot,
            HashSet<string>      alreadyRegisteredIds,
            Action<string,float> progress,
            List<string>         parseErrors)
        {
            var results = new List<ScanResult>();

            if (!Directory.Exists(boothRoot))
                return results;

            var dirs = Directory.GetDirectories(boothRoot)
                .Where(d => BoothDirPattern.IsMatch(Path.GetFileName(d)))
                .ToList();

            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            for (int i = 0; i < dirs.Count; i++)
            {
                string dir     = dirs[i];
                string dirName = Path.GetFileName(dir);
                var    match   = BoothDirPattern.Match(dirName);
                string boothId = match.Groups[1].Value;

                progress?.Invoke($"{dirName} ({i + 1}/{dirs.Count})", (float)i / dirs.Count);

                if (alreadyRegisteredIds.Contains(boothId))
                    continue;

                var pkgPaths = Directory.GetFiles(dir, "*.unitypackage", SearchOption.AllDirectories);
                if (pkgPaths.Length == 0)
                    continue;

                var packages    = new List<PackageData>();
                var allFilesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pkg in pkgPaths)
                {
                    try
                    {
                        var r = UnitypackageParser.Parse(pkg);
                        packages.Add(new PackageData
                        {
                            fileName = r.FileName,
                            files    = r.Files
                        });
                        foreach (var f in r.Files)
                            allFilesSet.Add(f);
                    }
                    catch (Exception ex)
                    {
                        parseErrors.Add($"b{boothId}/{Path.GetFileName(pkg)}: {ex.Message}");
                    }
                }

                if (packages.Count == 0)
                    continue;

                var matchedFiles = allFilesSet
                    .Where(f => ExistsInProject(projectRoot, f))
                    .ToList();

                results.Add(new ScanResult
                {
                    BoothItemId        = boothId,
                    Packages           = packages,
                    TotalFiles         = allFilesSet.Count,
                    MatchedFiles       = matchedFiles,
                    SuggestedTargetDir = SuggestTargetDir(matchedFiles)
                });
            }

            return results;
        }

        /// <summary>
        /// matchedFiles 縺ｮ荳ｭ縺ｧ譛繧よ髪驟咲噪縺ｪ Assets/X[/Y] 繧定ｿ斐☆縲・        /// 隕九▽縺九ｉ縺ｪ縺・ｴ蜷医・ ""縲・        /// </summary>
        public static string SuggestTargetDir(List<string> matchedFiles)
        {
            if (matchedFiles == null || matchedFiles.Count == 0)
                return "";

            // 蜷・ヵ繧｡繧､繝ｫ縺ｮ蜈ｨ逾門・繝・ぅ繝ｬ繧ｯ繝医Μ縺ｫ蜃ｺ迴ｾ蝗樊焚繧貞刈邂・            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in matchedFiles)
            {
                string d = Path.GetDirectoryName(file)?.Replace('\\', '/');
                while (!string.IsNullOrEmpty(d) && d != "Assets")
                {
                    counts.TryGetValue(d, out int c);
                    counts[d] = c + 1;
                    int slash = d.LastIndexOf('/');
                    d = slash > 0 ? d.Substring(0, slash) : null;
                }
            }

            if (counts.Count == 0)
                return "";

            // 蜃ｺ迴ｾ謨ｰ髯埼・竊・豺ｱ縺墓・鬆・ｼ域ｵ・＞譁ｹ蜆ｪ蜈茨ｼ・竊・霎樊嶌鬆・            string best = counts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key.Count(c => c == '/'))
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .First().Key;

            // AssetDatabase 縺ｧ螳溷惠遒ｺ隱搾ｼ帙↑縺代ｌ縺ｰ隕ｪ縺ｸ驕｡繧・            while (!string.IsNullOrEmpty(best) && best != "Assets")
            {
                if (AssetDatabase.IsValidFolder(best))
                    return best;
                int slash = best.LastIndexOf('/');
                best = slash > 0 ? best.Substring(0, slash) : null;
            }

            return "";
        }

        static bool ExistsInProject(string projectRoot, string assetPath)
        {
            string full = Path.Combine(projectRoot, assetPath);
            return File.Exists(full) || Directory.Exists(full);
        }
    }
}

