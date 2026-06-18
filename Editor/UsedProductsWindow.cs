using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRCAssetTracker
{
    public class UsedProductsWindow : EditorWindow
    {
        [MenuItem("Tools/VRCAssetTracker/Find Used Products")]
        static void Open() => GetWindow<UsedProductsWindow>("使用商品を検索");

        const float ThumbSize  = 64f;
        const float RowHeight  = 80f;
        const float RowPadding = 8f;

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
            public string localPackagePath;
            public List<UsageEntry> usages = new List<UsageEntry>();
            public bool   foldout;
        }

        class CacheEntry
        {
            public Texture2D Thumbnail;
            public string    ProductName;
            public bool      Loading;
        }

        readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();

        List<ProductUsageResult> _results;
        string _searchedObjectName;
        Vector2 _scroll;

        static readonly HttpClient Http;

        static UsedProductsWindow()
        {
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            Http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        void OnEnable()  => Selection.selectionChanged += Repaint;
        void OnDisable() => Selection.selectionChanged -= Repaint;

        void OnDestroy()
        {
            foreach (var e in _cache.Values)
                if (e.Thumbnail != null) DestroyImmediate(e.Thumbnail);
            _cache.Clear();
        }

        // ── OnGUI ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            DrawHeader();

            if (_results == null)
            {
                EditorGUILayout.HelpBox("Hierarchyでオブジェクトを選択して「選択を検索」、またはシーン全体を調べるには「シーン全体を検索」を押してください。", MessageType.Info);
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
            if (GUILayout.Button("選択を検索", EditorStyles.toolbarButton, GUILayout.Width(80)))
                RunSearch(new[] { selected }, selected.name);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("シーン全体を検索", EditorStyles.toolbarButton, GUILayout.Width(112)))
                RunSearch(SceneManager.GetActiveScene().GetRootGameObjects(), "シーン全体");

            EditorGUILayout.EndHorizontal();
        }

        // ── 商品行 ────────────────────────────────────────────────────────────

        void DrawProductResult(ProductUsageResult result)
        {
            _cache.TryGetValue(result.productId, out var entry);
            string name = entry?.ProductName ?? result.displayName;

            Rect row = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.Height(RowHeight), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                DrawRowContent(row, result, entry, name);

            if (Event.current.type == EventType.MouseDown
                && row.Contains(Event.current.mousePosition))
            {
                result.foldout = !result.foldout;
                Event.current.Use();
                Repaint();
            }

            EditorGUI.DrawRect(
                new Rect(row.x, row.yMax - 1f, row.width, 1f),
                new Color(0.2f, 0.2f, 0.2f));

            if (!result.foldout) return;

            // ── 詳細セクション ──
            EditorGUI.indentLevel += 2;

            var byHierarchy = new Dictionary<string, List<UsageEntry>>();
            foreach (var u in result.usages)
            {
                if (!byHierarchy.TryGetValue(u.hierarchyPath, out var list))
                    byHierarchy[u.hierarchyPath] = list = new List<UsageEntry>();
                list.Add(u);
            }

            var paths = new List<string>(byHierarchy.Keys);
            paths.Sort();

            foreach (var path in paths)
            {
                EditorGUILayout.LabelField(path, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var u in byHierarchy[path])
                {
                    string fileName = Path.GetFileName(u.assetPath);
                    EditorGUILayout.LabelField(
                        $"{u.componentTypeName} > {u.propertyDisplayName}: {fileName}");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel -= 2;
            EditorGUILayout.Space(4);

            Rect sep = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1f), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(sep, new Color(0.15f, 0.15f, 0.15f));
        }

        void DrawRowContent(Rect row, ProductUsageResult result, CacheEntry entry, string name)
        {
            var thumbRect = new Rect(
                row.x + RowPadding,
                row.y + (RowHeight - ThumbSize) / 2f,
                ThumbSize, ThumbSize);

            if (entry?.Thumbnail != null)
                GUI.DrawTexture(thumbRect, entry.Thumbnail, ScaleMode.ScaleToFit);
            else if (entry?.Loading == true)
                GUI.Label(thumbRect, "…", EditorStyles.centeredGreyMiniLabel);
            else
                EditorGUI.DrawRect(thumbRect, new Color(0.22f, 0.22f, 0.22f));

            float tx = thumbRect.xMax + RowPadding;
            float tw = row.width - tx - 28f;
            string tag = result.isBooth ? "[Booth]" : "[AS]";

            GUI.Label(new Rect(tx, row.y + 10f, tw, 20f), name, EditorStyles.boldLabel);
            GUI.Label(new Rect(tx, row.y + 30f, tw, 16f), $"{tag}  {result.productId}", EditorStyles.miniLabel);
            GUI.Label(new Rect(tx, row.y + 46f, tw, 16f),
                $"{result.usages.Count} アセット使用中", EditorStyles.miniLabel);

            string icon = result.foldout ? "▼" : "▶";
            GUI.Label(
                new Rect(row.xMax - 24f, row.y + (RowHeight - 20f) / 2f, 20f, 20f),
                icon, EditorStyles.centeredGreyMiniLabel);
        }

        // ── 検索 ──────────────────────────────────────────────────────────────

        void RunSearch(GameObject[] roots, string label)
        {
            _results = new List<ProductUsageResult>();
            _searchedObjectName = label;

            var assetUsages = new Dictionary<string, List<UsageEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
            {
                if (root == null) continue;
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
            }

            if (assetUsages.Count == 0) return;

            var boothData = AssetLinkerStorage.Load();
            var asData    = AssetStoreStorage.Load();
            var resultMap = new Dictionary<string, ProductUsageResult>();

            foreach (var kvp in assetUsages)
            {
                string assetPath = kvp.Key;
                var    entries   = kvp.Value;

                ProductUsageResult matched = null;

                foreach (var product in boothData.products)
                {
                    bool found = false;
                    foreach (var pkg in product.packages)
                    {
                        foreach (var f in pkg.files)
                        {
                            if (string.Equals(f, assetPath, StringComparison.OrdinalIgnoreCase))
                            { found = true; break; }
                        }
                        if (found) break;
                    }
                    if (!found) continue;

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
                    foreach (var product in asData.products)
                    {
                        bool found = false;
                        foreach (var pkg in product.packages)
                        {
                            foreach (var f in pkg.files)
                            {
                                if (string.Equals(f, assetPath, StringComparison.OrdinalIgnoreCase))
                                { found = true; break; }
                            }
                            if (found) break;
                        }
                        if (!found) continue;

                        if (!resultMap.TryGetValue(product.compositeId, out matched))
                        {
                            matched = new ProductUsageResult
                            {
                                productId        = product.compositeId,
                                displayName      = product.displayName ?? product.compositeId,
                                isBooth          = false,
                                localPackagePath = product.localPackagePath
                            };
                            resultMap[product.compositeId] = matched;
                            _results.Add(matched);
                        }
                        break;
                    }
                }

                if (matched != null)
                    matched.usages.AddRange(entries);
            }

            // サムネイル読み込み開始
            foreach (var result in _results)
            {
                if (_cache.ContainsKey(result.productId)) continue;
                if (result.isBooth)
                    _ = FetchBoothInfoAsync(result.productId);
                else
                    _ = LoadAssetStoreIconAsync(result.productId, result.displayName, result.localPackagePath);
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

        // ── Booth 情報取得 ────────────────────────────────────────────────────

        async Task FetchBoothInfoAsync(string boothItemId)
        {
            _cache[boothItemId] = new CacheEntry { Loading = true };

            string key     = ThumbnailCache.BoothKey(boothItemId);
            bool   hasImg  = ThumbnailCache.TryLoad(key,     out var cachedImg);
            bool   hasMeta = ThumbnailCache.TryLoadMeta(key, out var cachedTitle);

            if (hasImg && hasMeta)
            {
                EditorApplication.delayCall += () =>
                {
                    var thumb = new Texture2D(2, 2);
                    thumb.LoadImage(cachedImg);
                    _cache[boothItemId] = new CacheEntry
                        { Thumbnail = thumb, ProductName = cachedTitle, Loading = false };
                    Repaint();
                };
                return;
            }

            try
            {
                string html  = await Http.GetStringAsync($"https://booth.pm/ja/items/{boothItemId}");
                string title = ParseOgMeta(html, "og:title");

                if (!hasMeta) ThumbnailCache.SaveMeta(key, title);

                byte[] imgBytes = cachedImg;
                if (!hasImg)
                {
                    string imageUrl = ParseOgMeta(html, "og:image");
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        imgBytes = await Http.GetByteArrayAsync(imageUrl);
                        ThumbnailCache.Save(key, imgBytes);
                    }
                }

                EditorApplication.delayCall += () =>
                {
                    Texture2D thumb = null;
                    if (imgBytes != null)
                    {
                        thumb = new Texture2D(2, 2);
                        thumb.LoadImage(imgBytes);
                    }
                    _cache[boothItemId] = new CacheEntry
                        { Thumbnail = thumb, ProductName = title, Loading = false };
                    Repaint();
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AssetLinker] Booth fetch failed ({boothItemId}): {ex.Message}");
                EditorApplication.delayCall += () =>
                {
                    Texture2D thumb = null;
                    if (cachedImg != null) { thumb = new Texture2D(2, 2); thumb.LoadImage(cachedImg); }
                    _cache[boothItemId] = new CacheEntry
                        { Thumbnail = thumb, ProductName = cachedTitle ?? "(取得失敗)", Loading = false };
                    Repaint();
                };
            }
        }

        // ── Asset Store アイコン読み込み ──────────────────────────────────────

        async Task LoadAssetStoreIconAsync(string compositeId, string displayName, string localPackagePath)
        {
            _cache[compositeId] = new CacheEntry { Loading = true, ProductName = displayName };

            string cacheKey  = ThumbnailCache.AsKey(compositeId);
            byte[] iconBytes = null;

            if (ThumbnailCache.TryLoad(cacheKey, out var cached))
            {
                iconBytes = cached;
            }
            else if (!string.IsNullOrEmpty(localPackagePath) && File.Exists(localPackagePath))
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var r = UnitypackageParser.Parse(localPackagePath);
                        iconBytes = r.IconBytes;
                    });
                    if (iconBytes != null)
                        ThumbnailCache.Save(cacheKey, iconBytes);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AssetLinker] AS icon load failed ({compositeId}): {ex.Message}");
                }
            }

            EditorApplication.delayCall += () =>
            {
                Texture2D thumb = null;
                if (iconBytes != null) { thumb = new Texture2D(2, 2); thumb.LoadImage(iconBytes); }
                _cache[compositeId] = new CacheEntry
                    { Thumbnail = thumb, ProductName = displayName, Loading = false };
                Repaint();
            };
        }

        static string ParseOgMeta(string html, string property)
        {
            string pat = Regex.Escape(property);

            var m = Regex.Match(html,
                $@"<meta[^>]+property=""{pat}""[^>]+content=""([^""]+)""",
                RegexOptions.IgnoreCase);
            if (m.Success) return System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);

            m = Regex.Match(html,
                $@"<meta[^>]+content=""([^""]+)""[^>]+property=""{pat}""",
                RegexOptions.IgnoreCase);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
        }
    }
}
