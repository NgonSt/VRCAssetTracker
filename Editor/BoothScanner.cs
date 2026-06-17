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
        public List<PackageData> Packages;        // JSON 保存用
        public int               TotalFiles;      // パッケージ横断ユニーク数
        public List<string>      MatchedFiles;    // プロジェクトに存在するパスのみ
        public string            SuggestedTargetDir; // "" = 自動決定不可
    }

    public static class BoothScanner
    {
        static readonly Regex BoothDirPattern = new Regex(@"^b(\d+)$");

        /// <summary>
        /// boothRoot 配下の b{ID} ディレクトリを全スキャンする。
        /// alreadyRegisteredIds に含まれる商品はスキップ。
        /// </summary>
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
        /// matchedFiles の中で最も支配的な Assets/X[/Y] を返す。
        /// 見つからない場合は ""。
        /// </summary>
        public static string SuggestTargetDir(List<string> matchedFiles)
        {
            if (matchedFiles == null || matchedFiles.Count == 0)
                return "";

            // 各ファイルの全祖先ディレクトリに出現回数を加算
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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

            // 出現数降順 → 深さ昇順（浅い方優先） → 辞書順
            string best = counts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key.Count(c => c == '/'))
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .First().Key;

            // AssetDatabase で実在確認；なければ親へ遡る
            while (!string.IsNullOrEmpty(best) && best != "Assets")
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
