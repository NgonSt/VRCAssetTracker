using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VRCAssetTracker
{
    public class AssetStoreScanAllWindow : EditorWindow
    {
        [MenuItem("Tools/VRCAssetTracker/Scan Asset Store & Auto-Register")]
        static void Open()
        {
            var w = GetWindow<AssetStoreScanAllWindow>("AS 一括登録");
            w.ShowUtility();
        }

        class RowState
        {
            public bool   Checked;
            public string TargetDir;
            public bool   HasError;
        }

        List<AssetStoreScanResult> _results     = new List<AssetStoreScanResult>();
        List<RowState>             _rowStates   = new List<RowState>();
        List<string>               _parseErrors = new List<string>();
        bool    _scanned;
        Vector2 _scroll;

const float ColCheck  = 20f;
        const float ColPubl   = 120f;
        const float ColMatch  = 60f;
        const float ColBrowse = 28f;

        int CheckedCount => _rowStates.Count(s => s.Checked);

        // ── OnGUI ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            DrawToolbar();

            if (!_scanned)
            {
                DrawPrescanBody();
                return;
            }

            if (_results.Count == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "未登録のパッケージが見つかりませんでした。\n" +
                    "すべて登録済みか、プロジェクトに何も Import されていません。",
                    MessageType.Info);
                return;
            }

            DrawHeaderRow();
            DrawResultList();
            DrawFooter();
        }

        // ── ツールバー ────────────────────────────────────────────────────────

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("AS Root:", GUILayout.Width(52));
                EditorGUILayout.LabelField(
                    AssetLinkerSettings.AssetStoreLibraryRoot, EditorStyles.miniLabel);
                if (GUILayout.Button("…", EditorStyles.toolbarButton, GUILayout.Width(24)))
                    BrowseAssetStoreRoot();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(_scanned ? "再スキャン" : "スキャン開始", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    RunScan();

                int n = CheckedCount;
                using (new EditorGUI.DisabledScope(n == 0))
                {
                    if (GUILayout.Button($"選択した商品を登録 ({n} 件)",
                            EditorStyles.toolbarButton, GUILayout.Width(148)))
                        RegisterChecked();
                }
            }
        }

        // ── スキャン前の説明 ──────────────────────────────────────────────────

        void DrawPrescanBody()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Unity Asset Store のローカルキャッシュ（Asset Store-5.x）をスキャンし、\n" +
                "プロジェクトに Import 済みのパッケージを自動検出します。\n\n" +
                "「スキャン開始」ボタンを押してください。",
                MessageType.Info);

            if (!Directory.Exists(AssetLinkerSettings.AssetStoreLibraryRoot))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    $"Asset Store Root が存在しません:\n{AssetLinkerSettings.AssetStoreLibraryRoot}\n\n" +
                    "ツールバーの「…」ボタンで正しいフォルダを指定してください。\n" +
                    "通常は  %APPDATA%\\Unity\\Asset Store-5.x  です。",
                    MessageType.Error);
                if (GUILayout.Button("フォルダを参照して設定…", GUILayout.Height(28)))
                    BrowseAssetStoreRoot();
            }
        }

        // ── ヘッダ行 ─────────────────────────────────────────────────────────

        void DrawHeaderRow()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(ColCheck + 4);
                EditorGUILayout.LabelField("パブリッシャー", EditorStyles.miniLabel, GUILayout.Width(ColPubl));
                EditorGUILayout.LabelField("パッケージ名",   EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField("マッチ",         EditorStyles.miniLabel, GUILayout.Width(ColMatch));
                EditorGUILayout.LabelField("ターゲットディレクトリ", EditorStyles.miniLabel,
                    GUILayout.ExpandWidth(true));
                GUILayout.Space(ColBrowse);
            }
        }

        // ── 結果リスト ────────────────────────────────────────────────────────

        void DrawResultList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _results.Count; i++)
                DrawRow(i);
            EditorGUILayout.EndScrollView();
        }

        void DrawRow(int i)
        {
            var result   = _results[i];
            var state    = _rowStates[i];
            bool hasMatch = result.MatchedFiles.Count > 0;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hasMatch))
                    state.Checked = EditorGUILayout.Toggle(state.Checked, GUILayout.Width(ColCheck));

                EditorGUILayout.LabelField(result.PublisherName, GUILayout.Width(ColPubl));
                EditorGUILayout.LabelField(result.DisplayName, GUILayout.ExpandWidth(true));

                string matchLabel = $"{result.MatchedFiles.Count}/{result.TotalFiles}";
                var matchStyle = hasMatch ? EditorStyles.label : EditorStyles.centeredGreyMiniLabel;
                EditorGUILayout.LabelField(matchLabel, matchStyle, GUILayout.Width(ColMatch));

                if (state.HasError)
                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                state.TargetDir = EditorGUILayout.TextField(state.TargetDir, GUILayout.ExpandWidth(true));
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("...", GUILayout.Width(ColBrowse)))
                    BrowseTargetDir(i);
            }
        }

        // ── フッター ─────────────────────────────────────────────────────────

        void DrawFooter()
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_parseErrors.Count > 0)
                {
                    EditorGUILayout.LabelField(
                        $"パースエラー: {_parseErrors.Count} 件",
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("詳細", EditorStyles.miniButton, GUILayout.Width(40)))
                        EditorUtility.DisplayDialog(
                            "パースエラー一覧",
                            string.Join("\n", _parseErrors),
                            "閉じる");
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("全選択", EditorStyles.miniButton, GUILayout.Width(48)))
                    foreach (var (r, s) in _results.Zip(_rowStates, (r, s) => (r, s)))
                        if (r.MatchedFiles.Count > 0) s.Checked = true;
                if (GUILayout.Button("全解除", EditorStyles.miniButton, GUILayout.Width(48)))
                    foreach (var s in _rowStates) s.Checked = false;
            }
        }

        // ── フォルダ参照（ルート） ────────────────────────────────────────────

        void BrowseAssetStoreRoot()
        {
            string current = AssetLinkerSettings.AssetStoreLibraryRoot;
            string start   = Directory.Exists(current) ? current : "";
            string picked  = EditorUtility.OpenFolderPanel("Asset Store キャッシュフォルダを選択", start, "");
            if (string.IsNullOrEmpty(picked)) return;
            AssetLinkerSettings.AssetStoreLibraryRoot = picked;
            _scanned = false;
            Repaint();
        }

        // ── スキャン ──────────────────────────────────────────────────────────

        void RunScan()
        {
            _results.Clear();
            _rowStates.Clear();
            _parseErrors.Clear();
            _scanned = false;

            var registeredIds = new HashSet<string>(
                AssetStoreStorage.Load().products.Select(p => p.compositeId));

            try
            {
                _results = AssetStoreScanner.ScanAll(
                    AssetLinkerSettings.AssetStoreLibraryRoot,
                    registeredIds,
                    (label, t) => EditorUtility.DisplayProgressBar("スキャン中...", label, t),
                    _parseErrors);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _rowStates = _results.Select(r => new RowState
            {
                Checked   = r.MatchedFiles.Count > 0,
                TargetDir = r.SuggestedTargetDir
            }).ToList();

            _scanned = true;
            Repaint();
        }

        // ── 一括登録 ─────────────────────────────────────────────────────────

        void RegisterChecked()
        {
            int saved = 0, skipped = 0;
            bool anyError = false;

            for (int i = 0; i < _results.Count; i++)
            {
                var state = _rowStates[i];
                if (!state.Checked) continue;

                string dir = state.TargetDir?.Trim() ?? "";
                if (string.IsNullOrEmpty(dir))
                {
                    state.HasError = true;
                    anyError = true;
                    skipped++;
                    continue;
                }

                state.HasError = false;
                var r = _results[i];
                AssetStoreStorage.SaveProduct(new AssetStoreProductData
                {
                    compositeId      = r.CompositeId,
                    displayName      = r.DisplayName,
                    publisherName    = r.PublisherName,
                    localPackagePath = r.LocalPackagePath,
                    targetDirectory  = dir,
                    packages         = r.Packages
                });
                saved++;
            }

            var plw = Resources.FindObjectsOfTypeAll<ProductListWindow>().FirstOrDefault();
            plw?.Reload();

            if (anyError) Repaint();

            string msg = saved > 0 ? $"{saved} 件を登録しました。" : "";
            if (skipped > 0)
                msg += $"\n{skipped} 件はターゲットが未設定のためスキップしました（赤くハイライト）。";

            EditorUtility.DisplayDialog("登録完了", msg.Trim(), "OK");
        }

        // ── フォルダ参照 ─────────────────────────────────────────────────────

        void BrowseTargetDir(int i)
        {
            string picked = EditorUtility.OpenFolderPanel("ターゲットフォルダを選択", "Assets", "");
            if (string.IsNullOrEmpty(picked)) return;

            string projectRoot = Path.GetDirectoryName(Application.dataPath)
                .Replace('\\', '/').TrimEnd('/') + "/";
            string norm = picked.Replace('\\', '/');

            if (!norm.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("エラー",
                    "プロジェクト内のフォルダを選択してください。", "OK");
                return;
            }

            string rel = norm.Substring(projectRoot.Length);
            if (!AssetDatabase.IsValidFolder(rel))
            {
                EditorUtility.DisplayDialog("エラー",
                    $"有効なフォルダではありません: {rel}", "OK");
                return;
            }

            _rowStates[i].TargetDir = rel;
            _rowStates[i].HasError  = false;
            Repaint();
        }
    }
}
