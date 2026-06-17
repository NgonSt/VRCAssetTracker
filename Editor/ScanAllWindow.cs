using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VRCAssetTracker
{
    public class ScanAllWindow : EditorWindow
    {
        [MenuItem("Tools/MyAssetManager/Scan All & Auto-Register")]
        static void Open() => GetWindow<ScanAllWindow>("荳諡ｬ逋ｻ骭ｲ");

        class RowState
        {
            public bool   Checked;
            public string TargetDir;
            public bool   HasError;
        }

        List<ScanResult> _results    = new List<ScanResult>();
        List<RowState>   _rowStates  = new List<RowState>();
        List<string>     _parseErrors = new List<string>();
        bool    _scanned;
        Vector2 _scroll;

        // 蛻怜ｹ・        const float ColCheck  = 20f;
        const float ColId     = 80f;
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
                    "Import 貂医∩蝠・刀縺瑚ｦ九▽縺九ｊ縺ｾ縺帙ｓ縺ｧ縺励◆縲・n" +
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
                EditorGUILayout.LabelField("Booth Root:", GUILayout.Width(70));
                EditorGUILayout.LabelField(
                    AssetLinkerSettings.BoothLibraryRoot, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("繧ｹ繧ｭ繝｣繝ｳ髢句ｧ・, EditorStyles.toolbarButton, GUILayout.Width(72)))
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
                "Booth Library Manager 縺ｮ b{ID} 繝輔か繝ｫ繝繧偵☆縺ｹ縺ｦ繧ｹ繧ｭ繝｣繝ｳ縺励―n" +
                "Unity 繝励Ο繧ｸ繧ｧ繧ｯ繝医↓ Import 貂医∩縺ｮ蝠・刀繧定・蜍墓､懷・縺励∪縺吶・n\n" +
                "縲後せ繧ｭ繝｣繝ｳ髢句ｧ九阪・繧ｿ繝ｳ繧呈款縺励※縺上□縺輔＞縲・,
                MessageType.Info);

            if (!Directory.Exists(AssetLinkerSettings.BoothLibraryRoot))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    $"Booth Root 縺悟ｭ伜惠縺励∪縺帙ｓ: {AssetLinkerSettings.BoothLibraryRoot}\n" +
                    "Product List 繧ｦ繧｣繝ｳ繝峨え縺ｮ繝・・繝ｫ繝舌・縺九ｉ螟画峩縺励※縺上□縺輔＞縲・,
                    MessageType.Error);
            }
        }

        // 笏笏 繝倥ャ繝陦・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawHeaderRow()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(ColCheck + 4);
                EditorGUILayout.LabelField("蝠・刀 ID", EditorStyles.miniLabel, GUILayout.Width(ColId));
                EditorGUILayout.LabelField("繝槭ャ繝・,  EditorStyles.miniLabel, GUILayout.Width(ColMatch));
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
            var result = _results[i];
            var state  = _rowStates[i];
            bool hasMatch = result.MatchedFiles.Count > 0;

            using (new EditorGUILayout.HorizontalScope())
            {
                // 繝√ぉ繝・け繝懊ャ繧ｯ繧ｹ・医・繝・メ縺ｪ縺励・辟｡蜉ｹ・・                using (new EditorGUI.DisabledScope(!hasMatch))
                    state.Checked = EditorGUILayout.Toggle(state.Checked,
                        GUILayout.Width(ColCheck));

                // 蝠・刀 ID
                EditorGUILayout.LabelField(result.BoothItemId, GUILayout.Width(ColId));

                // 繝槭ャ繝∵焚・医・繝・メ縺ｪ縺励・繧ｰ繝ｬ繝ｼ・・                string matchLabel = $"{result.MatchedFiles.Count}/{result.TotalFiles}";
                var matchStyle = hasMatch ? EditorStyles.label : EditorStyles.centeredGreyMiniLabel;
                EditorGUILayout.LabelField(matchLabel, matchStyle, GUILayout.Width(ColMatch));

                // 繧ｿ繝ｼ繧ｲ繝・ヨ繝・ぅ繝ｬ繧ｯ繝医Μ・育ｷｨ髮・庄閭ｽ縲√お繝ｩ繝ｼ譎ゅ・襍､・・                if (state.HasError)
                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                state.TargetDir = EditorGUILayout.TextField(state.TargetDir,
                    GUILayout.ExpandWidth(true));
                GUI.backgroundColor = Color.white;

                // 蜿ら・繝懊ち繝ｳ
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

        // 笏笏 繧ｹ繧ｭ繝｣繝ｳ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void RunScan()
        {
            _results.Clear();
            _rowStates.Clear();
            _parseErrors.Clear();
            _scanned = false;

            var registeredIds = new HashSet<string>(
                AssetLinkerStorage.Load().products.Select(p => p.boothItemId));

            try
            {
                _results = BoothScanner.ScanAll(
                    AssetLinkerSettings.BoothLibraryRoot,
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
                AssetLinkerStorage.SaveProduct(new ProductData
                {
                    boothItemId     = _results[i].BoothItemId,
                    targetDirectory = dir,
                    packages        = _results[i].Packages
                });
                saved++;
            }

            // ProductListWindow 縺碁幕縺・※縺・ｌ縺ｰ譖ｴ譁ｰ
            var plw = Resources.FindObjectsOfTypeAll<ProductListWindow>()
                .FirstOrDefault();
            plw?.Reload();

            if (anyError) Repaint();

            string msg = saved > 0
                ? $"{saved} 莉ｶ繧堤匳骭ｲ縺励∪縺励◆縲・
                : "";
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

