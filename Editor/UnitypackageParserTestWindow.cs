using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRCAssetTracker
{
    public class UnitypackageParserTestWindow : EditorWindow
    {
        [MenuItem("Tools/MyAssetManager/Parser Test")]
        static void Open() => GetWindow<UnitypackageParserTestWindow>("Parser Test");

        string _packagePath = "";
        UnitypackageParseResult _result;
        string _error;
        Vector2 _scroll;

        void OnGUI()
        {
            EditorGUILayout.LabelField("unitypackage Parser Test", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            _packagePath = EditorGUILayout.TextField("Package", _packagePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFilePanel("Select unitypackage", "", "unitypackage");
                if (!string.IsNullOrEmpty(path))
                {
                    _packagePath = path;
                    _result = null;
                    _error  = null;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_packagePath)))
            {
                if (GUILayout.Button("Parse"))
                    RunParse();
            }

            EditorGUILayout.Space();

            if (_error != null)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
                return;
            }

            if (_result == null) return;

            EditorGUILayout.LabelField($"{_result.FileName}  窶・ {_result.Files.Count} files");
            EditorGUILayout.Space(2);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var f in _result.Files)
                EditorGUILayout.LabelField(f, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        void RunParse()
        {
            _result = null;
            _error  = null;

            if (!File.Exists(_packagePath))
            {
                _error = $"File not found: {_packagePath}";
                return;
            }

            try
            {
                _result = UnitypackageParser.Parse(_packagePath);
            }
            catch (System.Exception ex)
            {
                _error = ex.Message;
                Debug.LogException(ex);
            }
        }
    }
}

