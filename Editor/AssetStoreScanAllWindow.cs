using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VRCAssetTracker
{
    public class AssetStoreScanAllWindow : EditorWindow
    {
        [MenuItem("Tools/MyAssetManager/Scan Asset Store & Auto-Register")]
        static void Open()
        {
            var w = GetWindow<AssetStoreScanAllWindow>("AS 荳諡ｬ逋ｻ骭ｲ");
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

        // 笏笏 OnGUI 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

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
                    "譛ｪ逋ｻ骭ｲ縺ｮ繝代ャ繧ｱ繝ｼ繧ｸ縺瑚ｦ九▽縺九ｊ縺ｾ縺帙ｓ縺ｧ縺励◆縲・n" +
                    "縺吶∋縺ｦ逋ｻ骭ｲ貂医∩縺九√・繝ｭ繧ｸ繧ｧ繧ｯ繝医↓菴輔ｂ Import 縺輔ｌ縺ｦ縺・∪縺帙ｓ縲・,
                    MessageType.Info);
                return;
            }

            DrawHeaderRow();
            DrawResultList();
            DrawFooter();
        }

        // 笏笏 繝・・繝ｫ繝舌・ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("AS Root:", GUILayout.Width(52));
                EditorGUILayout.LabelField(
                    AssetLinkerSettings.AssetStoreLibraryRoot, EditorStyles.miniLabel);
                if (GUILayout.Button("窶ｦ", EditorStyles.toolbarButton, GUILayout.Width(24)))
                    BrowseAssetStoreRoot();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(_scanned ? "蜀阪せ繧ｭ繝｣繝ｳ" : "繧ｹ繧ｭ繝｣繝ｳ髢句ｧ・, EditorStyles.toolbarButton, GUILayout.Width(72)))
                    RunScan();

                int n = CheckedCount;
                using (new EditorGUI.DisabledScope(n == 0))
                {
                    if (GUILayout.Button($"驕ｸ謚槭＠縺溷膚蜩√ｒ逋ｻ骭ｲ ({n} 莉ｶ)",
                            EditorStyles.toolbarButton, GUILayout.Width(148)))
                        RegisterChecked();
                }
            }
        }

        // 笏笏 繧ｹ繧ｭ繝｣繝ｳ蜑阪・隱ｬ譏・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawPrescanBody()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Unity Asset Store 縺ｮ繝ｭ繝ｼ繧ｫ繝ｫ繧ｭ繝｣繝・す繝･・・sset Store-5.x・峨ｒ繧ｹ繧ｭ繝｣繝ｳ縺励―n" +
                "繝励Ο繧ｸ繧ｧ繧ｯ繝医↓ Import 貂医∩縺ｮ繝代ャ繧ｱ繝ｼ繧ｸ繧定・蜍墓､懷・縺励∪縺吶・n\n" +
                "縲後せ繧ｭ繝｣繝ｳ髢句ｧ九阪・繧ｿ繝ｳ繧呈款縺励※縺上□縺輔＞縲・,
                MessageType.Info);

            if (!Directory.Exists(AssetLinkerSettings.AssetStoreLibraryRoot))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    $"Asset Store Root 縺悟ｭ伜惠縺励∪縺帙ｓ:\n{AssetLinkerSettings.AssetStoreLibraryRoot}\n\n" +
                    "繝・・繝ｫ繝舌・縺ｮ縲娯ｦ縲阪・繧ｿ繝ｳ縺ｧ豁｣縺励＞繝輔か繝ｫ繝繧呈欠螳壹＠縺ｦ縺上□縺輔＞縲・n" +
                    "騾壼ｸｸ縺ｯ  %APPDATA%\\Unity\\Asset Store-5.x  縺ｧ縺吶・,
                    MessageType.Error);
                if (GUILayout.Button("繝輔か繝ｫ繝繧貞盾辣ｧ縺励※險ｭ螳壺ｦ", GUILayout.Height(28)))
                    BrowseAssetStoreRoot();
            }
        }

        // 笏笏 繝倥ャ繝陦・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawHeaderRow()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(ColCheck + 4);
                EditorGUILayout.LabelField("繝代ヶ繝ｪ繝・す繝｣繝ｼ", EditorStyles.miniLabel, GUILayout.Width(ColPubl));
                EditorGUILayout.LabelField("繝代ャ繧ｱ繝ｼ繧ｸ蜷・,   EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField("繝槭ャ繝・,         EditorStyles.miniLabel, GUILayout.Width(ColMatch));
                EditorGUILayout.LabelField("繧ｿ繝ｼ繧ｲ繝・ヨ繝・ぅ繝ｬ繧ｯ繝医Μ", EditorStyles.miniLabel,
                    GUILayout.ExpandWidth(true));
                GUILayout.Space(ColBrowse);
            }
        }

        // 笏笏 邨先棡繝ｪ繧ｹ繝・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

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

        // 笏笏 繝輔ャ繧ｿ繝ｼ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawFooter()
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_parseErrors.Count > 0)
                {
                    EditorGUILayout.LabelField(
                        $"繝代・繧ｹ繧ｨ繝ｩ繝ｼ: {_parseErrors.Count} 莉ｶ",
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("隧ｳ邏ｰ", EditorStyles.miniButton, GUILayout.Width(40)))
                        EditorUtility.DisplayDialog(
                            "繝代・繧ｹ繧ｨ繝ｩ繝ｼ荳隕ｧ",
                            string.Join("\n", _parseErrors),
                            "髢峨§繧・);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("蜈ｨ驕ｸ謚・, EditorStyles.miniButton, GUILayout.Width(48)))
                    foreach (var (r, s) in _results.Zip(_rowStates, (r, s) => (r, s)))
                        if (r.MatchedFiles.Count > 0) s.Checked = true;
                if (GUILayout.Button("蜈ｨ隗｣髯､", EditorStyles.miniButton, GUILayout.Width(48)))
                    foreach (var s in _rowStates) s.Checked = false;
            }
        }

        // 笏笏 繝輔か繝ｫ繝蜿ら・・医Ν繝ｼ繝茨ｼ・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void BrowseAssetStoreRoot()
        {
            string current = AssetLinkerSettings.AssetStoreLibraryRoot;
            string start   = Directory.Exists(current) ? current : "";
            string picked  = EditorUtility.OpenFolderPanel("Asset Store 繧ｭ繝｣繝・す繝･繝輔か繝ｫ繝繧帝∈謚・, start, "");
            if (string.IsNullOrEmpty(picked)) return;
            AssetLinkerSettings.AssetStoreLibraryRoot = picked;
            _scanned = false;
            Repaint();
        }

        // 笏笏 繧ｹ繧ｭ繝｣繝ｳ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

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
                    (label, t) => EditorUtility.DisplayProgressBar("繧ｹ繧ｭ繝｣繝ｳ荳ｭ...", label, t),
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

        // 笏笏 荳諡ｬ逋ｻ骭ｲ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

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

            string msg = saved > 0 ? $"{saved} 莉ｶ繧堤匳骭ｲ縺励∪縺励◆縲・ : "";
            if (skipped > 0)
                msg += $"\n{skipped} 莉ｶ縺ｯ繧ｿ繝ｼ繧ｲ繝・ヨ縺梧悴險ｭ螳壹・縺溘ａ繧ｹ繧ｭ繝・・縺励∪縺励◆・郁ｵ､縺上ワ繧､繝ｩ繧､繝茨ｼ峨・;

            EditorUtility.DisplayDialog("逋ｻ骭ｲ螳御ｺ・, msg.Trim(), "OK");
        }

        // 笏笏 繝輔か繝ｫ繝蜿ら・ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void BrowseTargetDir(int i)
        {
            string picked = EditorUtility.OpenFolderPanel("繧ｿ繝ｼ繧ｲ繝・ヨ繝輔か繝ｫ繝繧帝∈謚・, "Assets", "");
            if (string.IsNullOrEmpty(picked)) return;

            string projectRoot = Path.GetDirectoryName(Application.dataPath)
                .Replace('\\', '/').TrimEnd('/') + "/";
            string norm = picked.Replace('\\', '/');

            if (!norm.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("繧ｨ繝ｩ繝ｼ",
                    "繝励Ο繧ｸ繧ｧ繧ｯ繝亥・縺ｮ繝輔か繝ｫ繝繧帝∈謚槭＠縺ｦ縺上□縺輔＞縲・, "OK");
                return;
            }

            string rel = norm.Substring(projectRoot.Length);
            if (!AssetDatabase.IsValidFolder(rel))
            {
                EditorUtility.DisplayDialog("繧ｨ繝ｩ繝ｼ",
                    $"譛牙柑縺ｪ繝輔か繝ｫ繝縺ｧ縺ｯ縺ゅｊ縺ｾ縺帙ｓ: {rel}", "OK");
                return;
            }

            _rowStates[i].TargetDir = rel;
            _rowStates[i].HasError  = false;
            Repaint();
        }
    }
}

