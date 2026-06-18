using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MyAssetManager
{
    public class UsedProductsWindow : EditorWindow
    {
        [MenuItem("Tools/VRCAssetTracker/Find Used Products")]
        static void Open() => GetWindow<UsedProductsWindow>("使用商品を検索");

        class UsageEntry
        {
            public string assetPath;
            public string hierarchyPath;
            public string componentTypeName;
            public string propertyDisplayName;
        }

        class ProductUsageResult
        {
            public string productId;
            public string displayName;
            public bool   isBooth;
            public List<UsageEntry> usages = new List<UsageEntry>();
            public bool   foldout = true;
        }

        List<ProductUsageResult> _results;
        string _searchedObjectName;
        Vector2 _scroll;

        void OnEnable()  => Selection.selectionChanged += Repaint;
        void OnDisable() => Selection.selectionChanged -= Repaint;

        void OnGUI()
        {
            DrawHeader();

            if (_results == null)
            {
                EditorGUILayout.HelpBox("Hierarchyでオブジェクトを選択して「検索する」を押してください。", MessageType.Info);
                return;
            }

            if (_results.Count == 0)
            {
                string msg = string.IsNullOrEmpty(_searchedObjectName)
                    ? "登録済み商品のアセットは使用されていません。"
                    : $"\"{_searchedObjectName}\" では登録済み商品のアセットは使用されていません。";
                EditorGUILayout.HelpBox(msg, MessageType.None);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var result in _results)
                DrawProductResult(result);
            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var selected = Selection.activeGameObject;
            EditorGUILayout.LabelField("選択中:", GUILayout.Width(48));
            EditorGUILayout.LabelField(
                selected != null ? selected.name : "(未選択)",
                EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(selected == null);
            if (GUILayout.Button("検索する", EditorStyles.toolbarButton, GUILayout.Width(80)))
                RunSearch(selected);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        void DrawProductResult(ProductUsageResult result)
        {
            string tag    = result.isBooth ? "[Booth]" : "[AS]";
            string header = $"{tag} {result.displayName}  ({result.usages.Count} アセット使用中)";

            result.foldout = EditorGUILayout.Foldout(result.foldout, header, toggleOnLabelClick: true);
            if (!result.foldout) return;

            EditorGUI.indentLevel += 2;

            var byHierarchy = result.usages
                .GroupBy(u => u.hierarchyPath)
                .OrderBy(g => g.Key);

            foreach (var group in byHierarchy)
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var entry in group)
                {
                    string fileName = Path.GetFileName(entry.assetPath);
                    EditorGUILayout.LabelField(
                        $"{entry.componentTypeName} > {entry.propertyDisplayName}: {fileName}");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel -= 2;
            EditorGUILayout.Space(4);
        }

        void RunSearch(GameObject root)
        {
            _results = new List<ProductUsageResult>();
            _searchedObjectName = root != null ? root.name : string.Empty;
            if (root == null) return;

            // Collect all asset paths referenced in the GameObject hierarchy
            var assetUsages = new Dictionary<string, List<UsageEntry>>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var component in root.GetComponentsInChildren<Component>(includeInactive: true))
            {
                if (component == null) continue;

                string hierarchyPath = GetHierarchyPath(component.transform, root.transform);
                string typeName      = component.GetType().Name;

                var so   = new SerializedObject(component);
                var prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    enterChildren = true;
                    if (prop.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    enterChildren = false;
                    if (prop.objectReferenceValue == null) continue;

                    string assetPath = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    var entry = new UsageEntry
                    {
                        assetPath           = assetPath,
                        hierarchyPath       = hierarchyPath,
                        componentTypeName   = typeName,
                        propertyDisplayName = prop.displayName
                    };

                    if (!assetUsages.TryGetValue(assetPath, out var list))
                        assetUsages[assetPath] = list = new List<UsageEntry>();
                    list.Add(entry);
                }
            }

            if (assetUsages.Count == 0) return;

            // Build file sets per product for efficient lookup
            var boothData = AssetLinkerStorage.Load();
            var asData    = AssetStoreStorage.Load();

            var boothFileSets = boothData.products.Select(p => (
                product: p,
                files: new HashSet<string>(
                    p.packages.SelectMany(pkg => pkg.files),
                    System.StringComparer.OrdinalIgnoreCase)
            )).ToList();

            var asFileSets = asData.products.Select(p => (
                product: p,
                files: new HashSet<string>(
                    p.packages.SelectMany(pkg => pkg.files),
                    System.StringComparer.OrdinalIgnoreCase)
            )).ToList();

            var resultMap = new Dictionary<string, ProductUsageResult>();

            foreach (var kvp in assetUsages)
            {
                string assetPath = kvp.Key;
                var    entries   = kvp.Value;

                ProductUsageResult matched = null;

                foreach (var (product, files) in boothFileSets)
                {
                    if (!files.Contains(assetPath)) continue;
                    if (!resultMap.TryGetValue(product.boothItemId, out matched))
                    {
                        matched = new ProductUsageResult
                        {
                            productId   = product.boothItemId,
                            displayName = product.boothItemId,
                            isBooth     = true
                        };
                        resultMap[product.boothItemId] = matched;
                        _results.Add(matched);
                    }
                    break;
                }

                if (matched == null)
                {
                    foreach (var (product, files) in asFileSets)
                    {
                        if (!files.Contains(assetPath)) continue;
                        if (!resultMap.TryGetValue(product.compositeId, out matched))
                        {
                            matched = new ProductUsageResult
                            {
                                productId   = product.compositeId,
                                displayName = product.displayName ?? product.compositeId,
                                isBooth     = false
                            };
                            resultMap[product.compositeId] = matched;
                            _results.Add(matched);
                        }
                        break;
                    }
                }

                matched?.usages.AddRange(entries);
            }
        }

        static string GetHierarchyPath(Transform t, Transform root)
        {
            if (t == root) return root.name;

            var parts   = new List<string>();
            var current = t;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            parts.Insert(0, root.name);
            return string.Join("/", parts);
        }
    }
}
