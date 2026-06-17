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

        // 笏笏 繝・・繝ｫ繝舌・・・ooth Root 險ｭ螳夲ｼ・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("Booth Root:", GUILayout.Width(70));
                EditorGUILayout.LabelField(
                    AssetLinkerSettings.BoothLibraryRoot, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("螟画峩", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    string path = EditorUtility.OpenFolderPanel(
                        "Booth Library 繝ｫ繝ｼ繝医ｒ驕ｸ謚・,
                        AssetLinkerSettings.BoothLibraryRoot, "");
                    if (!string.IsNullOrEmpty(path))
                        AssetLinkerSettings.BoothLibraryRoot = path;
                }
            }
        }

        // 笏笏 Step 1: 繝輔か繝ｫ繝驕ｸ謚・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawSelectStep()
        {
            EditorGUILayout.HelpBox(
                "Booth Library Manager 縺ｮ b{ID} 繝輔か繝ｫ繝繧帝∈謚槭＠縺ｦ縺上□縺輔＞縲・n" +
                "繝輔か繝ｫ繝蜀・・ .unitypackage 繧貞・蟶ｰ繧ｹ繧ｭ繝｣繝ｳ縺励※逋ｻ骭ｲ縺励∪縺吶・,
                MessageType.Info);
            EditorGUILayout.Space(8);

            if (GUILayout.Button("b{ID} 繝輔か繝ｫ繝繧帝∈謚・..", GUILayout.Height(32)))
                SelectAndParse();
        }

        // 笏笏 Step 2: 鬟帙・蜈磯∈謚・竊・逋ｻ骭ｲ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawParsedStep()
        {
            // 繧ｵ繝槭Μ繝ｼ
            EditorGUILayout.LabelField("蝠・刀 ID",    _boothItemId);
            EditorGUILayout.LabelField("繝代ャ繧ｱ繝ｼ繧ｸ", $"{_packages.Count} 蛟・);
            foreach (var p in _packages)
                EditorGUILayout.LabelField("  " + p.FileName, EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // 鬟帙・蜈医ョ繧｣繝ｬ繧ｯ繝医Μ驕ｸ謚・            EditorGUILayout.LabelField("鬟帙・蜈医ョ繧｣繝ｬ繧ｯ繝医Μ:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "繧ｯ繝ｪ繝・け縺ｧ驕ｸ謚槭よｵ・＞繝代せ縺ｻ縺ｩ荳翫↓陦ｨ遉ｺ縺輔ｌ縺ｾ縺吶・, EditorStyles.miniLabel);
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
                EditorGUILayout.HelpBox("驕ｸ謚樔ｸｭ: " + _selectedDir, MessageType.None);

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Import 繧ｪ繝励す繝ｧ繝ｳ
            _importMode = EditorGUILayout.Toggle("Import 繧ょｮ溯｡後☆繧具ｼ育ｵ瑚ｷｯ 2・・, _importMode);
            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_selectedDir)))
            {
                if (GUILayout.Button("逋ｻ骭ｲ", GUILayout.Height(32)))
                    Register();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("竊・譛蛻昴↓謌ｻ繧・))
                ResetToSelectStep();
        }

        // 笏笏 繝ｭ繧ｸ繝・け 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void SelectAndParse()
        {
            _error = null;

            string dir = EditorUtility.OpenFolderPanel(
                "b{ID} 繝輔か繝ｫ繝繧帝∈謚・,
                AssetLinkerSettings.BoothLibraryRoot, "");
            if (string.IsNullOrEmpty(dir)) return;

            string dirName = Path.GetFileName(dir.TrimEnd('/', '\\'));
            var    match   = Regex.Match(dirName, @"^b(\d+)$");
            if (!match.Success)
            {
                _error = $"繝輔か繝ｫ繝蜷阪′ b{{ID}} 蠖｢蠑上〒縺ｯ縺ゅｊ縺ｾ縺帙ｓ: 縲鶏dirName}縲構n" +
                         "Booth Library Manager 縺ｮ b{ID} 繝輔か繝ｫ繝繧帝∈謚槭＠縺ｦ縺上□縺輔＞縲・;
                return;
            }

            _boothItemId = match.Groups[1].Value;
            _packages.Clear();

            var pkgPaths = Directory.GetFiles(dir, "*.unitypackage", SearchOption.AllDirectories);
            if (pkgPaths.Length == 0)
            {
                _error = ".unitypackage 縺瑚ｦ九▽縺九ｊ縺ｾ縺帙ｓ縺ｧ縺励◆縲・;
                return;
            }

            try
            {
                foreach (var pkg in pkgPaths)
                    _packages.Add(UnitypackageParser.Parse(pkg));
            }
            catch (Exception ex)
            {
                _error = $"繝代・繧ｹ螟ｱ謨・ {ex.Message}";
                Debug.LogException(ex);
                return;
            }

            // 蜈ｨ繝輔ぃ繧､繝ｫ縺ｮ逾門・繝・ぅ繝ｬ繧ｯ繝医Μ繧貞庶髮・ｼ・Assets" 閾ｪ菴薙・髯､螟厄ｼ・            var dirSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            // 豬・＞繝代せ鬆・竊・繧｢繝ｫ繝輔ぃ繝吶ャ繝磯・            _dirs = dirSet
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
                _error = $"繝励Ο繧ｸ繧ｧ繧ｯ繝井ｸ翫↓蟄伜惠縺励↑縺・ヵ繧ｩ繝ｫ繝縺ｧ縺・ {_selectedDir}\n" +
                         "蜈医↓謇句虚 import 縺吶ｋ縺九∝挨縺ｮ繝輔か繝ｫ繝繧帝∈謚槭＠縺ｦ縺上□縺輔＞縲・;
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
                    "荳頑嶌縺咲｢ｺ隱・,
                    $"蝠・刀 ID {_boothItemId} 縺ｯ縺吶〒縺ｫ逋ｻ骭ｲ貂医∩縺ｧ縺吶ゆｸ頑嶌縺阪＠縺ｾ縺吶°・・,
                    "荳頑嶌縺阪☆繧・, "繧ｭ繝｣繝ｳ繧ｻ繝ｫ"))
                return;

            AssetLinkerStorage.SaveProduct(product);
            EditorUtility.DisplayDialog("逋ｻ骭ｲ螳御ｺ・, $"蝠・刀 {_boothItemId} 繧堤匳骭ｲ縺励∪縺励◆縲・, "OK");
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

