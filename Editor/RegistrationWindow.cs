using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VRCAssetTracker
{
    public class RegistrationWindow : EditorWindow
    {
        [MenuItem("Tools/MyAssetManager/Register Product")]
        static void Open() => GetWindow<RegistrationWindow>("Register Product");

        enum Step { SelectDir, Parsed }

        Step   _step;
        string _error;

        // Parsed state
        string _boothItemId;
        List<UnitypackageParseResult> _packages = new List<UnitypackageParseResult>();
        List<string> _dirs = new List<string>();
        string _selectedDir;
        bool   _importMode;
        Vector2 _scroll;

        void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.Space(4);

            if (_step == Step.SelectDir)
                DrawSelectStep();
            else
                DrawParsedStep();

            if (_error != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }
        }

        // ── ツールバー（Booth Root 設定） ──────────────────────────────────────

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("Booth Root:", GUILayout.Width(70));
                EditorGUILayout.LabelField(
                    AssetLinkerSettings.BoothLibraryRoot, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("変更", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    string path = EditorUtility.OpenFolderPanel(
                        "Booth Library ルートを選択",
                        AssetLinkerSettings.BoothLibraryRoot, "");
                    if (!string.IsNullOrEmpty(path))
                        AssetLinkerSettings.BoothLibraryRoot = path;
                }
            }
        }

        // ── Step 1: フォルダ選択 ───────────────────────────────────────────────

        void DrawSelectStep()
        {
            EditorGUILayout.HelpBox(
                "Booth Library Manager の b{ID} フォルダを選択してください。\n" +
                "フォルダ内の .unitypackage を再帰スキャンして登録します。",
                MessageType.Info);
            EditorGUILayout.Space(8);

            if (GUILayout.Button("b{ID} フォルダを選択...", GUILayout.Height(32)))
                SelectAndParse();
        }

        // ── Step 2: 飛び先選択 → 登録 ─────────────────────────────────────────

        void DrawParsedStep()
        {
            // サマリー
            EditorGUILayout.LabelField("商品 ID",    _boothItemId);
            EditorGUILayout.LabelField("パッケージ", $"{_packages.Count} 個");
            foreach (var p in _packages)
                EditorGUILayout.LabelField("  " + p.FileName, EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // 飛び先ディレクトリ選択
            EditorGUILayout.LabelField("飛び先ディレクトリ:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "クリックで選択。浅いパスほど上に表示されます。", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
            foreach (var dir in _dirs)
            {
                bool selected = dir == _selectedDir;
                int  depth    = dir.Count(c => c == '/');  // "Assets/X" = 1

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space((depth - 1) * 14f);

                    var oldBg = GUI.backgroundColor;
                    if (selected) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);

                    if (GUILayout.Button(dir, EditorStyles.miniButton, GUILayout.ExpandWidth(true)))
                    {
                        _selectedDir = dir;
                        _error       = null;
                    }

                    GUI.backgroundColor = oldBg;
                }
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_selectedDir))
                EditorGUILayout.HelpBox("選択中: " + _selectedDir, MessageType.None);

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Import オプション
            _importMode = EditorGUILayout.Toggle("Import も実行する（経路 2）", _importMode);
            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_selectedDir)))
            {
                if (GUILayout.Button("登録", GUILayout.Height(32)))
                    Register();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("← 最初に戻る"))
                ResetToSelectStep();
        }

        // ── ロジック ───────────────────────────────────────────────────────────

        void SelectAndParse()
        {
            _error = null;

            string dir = EditorUtility.OpenFolderPanel(
                "b{ID} フォルダを選択",
                AssetLinkerSettings.BoothLibraryRoot, "");
            if (string.IsNullOrEmpty(dir)) return;

            string dirName = Path.GetFileName(dir.TrimEnd('/', '\\'));
            var    match   = Regex.Match(dirName, @"^b(\d+)$");
            if (!match.Success)
            {
                _error = $"フォルダ名が b{{ID}} 形式ではありません: 「{dirName}」\n" +
                         "Booth Library Manager の b{ID} フォルダを選択してください。";
                return;
            }

            _boothItemId = match.Groups[1].Value;
            _packages.Clear();

            var pkgPaths = Directory.GetFiles(dir, "*.unitypackage", SearchOption.AllDirectories);
            if (pkgPaths.Length == 0)
            {
                _error = ".unitypackage が見つかりませんでした。";
                return;
            }

            try
            {
                foreach (var pkg in pkgPaths)
                    _packages.Add(UnitypackageParser.Parse(pkg));
            }
            catch (Exception ex)
            {
                _error = $"パース失敗: {ex.Message}";
                Debug.LogException(ex);
                return;
            }

            // 全ファイルの祖先ディレクトリを収集（"Assets" 自体は除外）
            var dirSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pkg in _packages)
                foreach (var f in pkg.Files)
                {
                    string d = Path.GetDirectoryName(f)?.Replace('\\', '/');
                    while (!string.IsNullOrEmpty(d) && d != "Assets")
                    {
                        dirSet.Add(d);
                        int slash = d.LastIndexOf('/');
                        d = slash > 0 ? d.Substring(0, slash) : null;
                    }
                }

            // 浅いパス順 → アルファベット順
            _dirs = dirSet
                .OrderBy(d => d.Count(c => c == '/'))
                .ThenBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _selectedDir = null;
            _step        = Step.Parsed;
        }

        void Register()
        {
            _error = null;

            if (!AssetDatabase.IsValidFolder(_selectedDir))
            {
                _error = $"プロジェクト上に存在しないフォルダです: {_selectedDir}\n" +
                         "先に手動 import するか、別のフォルダを選択してください。";
                return;
            }

            if (_importMode)
            {
                foreach (var pkg in _packages)
                    AssetDatabase.ImportPackage(pkg.SourcePath, interactive: true);
            }

            var product = new ProductData
            {
                boothItemId     = _boothItemId,
                targetDirectory = _selectedDir,
                packages        = _packages.Select(p => new PackageData
                {
                    fileName = p.FileName,
                    files    = p.Files
                }).ToList()
            };

            var data   = AssetLinkerStorage.Load();
            bool exists = data.products.Any(p => p.boothItemId == _boothItemId);
            if (exists && !EditorUtility.DisplayDialog(
                    "上書き確認",
                    $"商品 ID {_boothItemId} はすでに登録済みです。上書きしますか？",
                    "上書きする", "キャンセル"))
                return;

            AssetLinkerStorage.SaveProduct(product);
            EditorUtility.DisplayDialog("登録完了", $"商品 {_boothItemId} を登録しました。", "OK");
            ResetToSelectStep();
        }

        void ResetToSelectStep()
        {
            _step        = Step.SelectDir;
            _packages.Clear();
            _dirs.Clear();
            _selectedDir = null;
            _error       = null;
        }
    }
}
