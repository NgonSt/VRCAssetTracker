using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VRCAssetTracker
{
    public class ProductListWindow : EditorWindow
    {
        [MenuItem("Tools/MyAssetManager/Product List")]
        static void Open() => GetWindow<ProductListWindow>("Booth Asset Linker");

        const float ThumbSize  = 64f;
        const float RowHeight  = 80f;
        const float RowPadding = 8f;

        class CacheEntry
        {
            public Texture2D Thumbnail;
            public string    ProductName;
            public bool      Loading;
        }

        AssetLinkerData      _data;
        AssetStoreLinkerData _asData;
        readonly Dictionary<string, CacheEntry> _cache   = new Dictionary<string, CacheEntry>();
        readonly Dictionary<string, CacheEntry> _asCache = new Dictionary<string, CacheEntry>();
        Vector2 _scroll;
        string  _pendingDelete;     // 削除はフレーム末尾で処理して foreach 中の変更を避ける
        string  _pendingDeleteAs;

        static readonly HttpClient Http;

        static ProductListWindow()
        {
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            Http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        void OnEnable()  => Reload();
        void OnFocus()   => Reload();
        void OnDestroy() => ClearCache();

        // ── データ読み込み ────────────────────────────────────────────────────

        public void Reload()
        {
            _data = AssetLinkerStorage.Load();
            foreach (var p in _data.products)
                if (!_cache.ContainsKey(p.boothItemId))
                    _ = FetchBoothInfoAsync(p.boothItemId);

            _asData = AssetStoreStorage.Load();
            foreach (var p in _asData.products)
                if (!_asCache.ContainsKey(p.compositeId))
                    _ = LoadAssetStoreIconAsync(p.compositeId, p.displayName, p.localPackagePath);
        }

        void ClearCache()
        {
            foreach (var e in _cache.Values)
                if (e.Thumbnail != null) DestroyImmediate(e.Thumbnail);
            _cache.Clear();

            foreach (var e in _asCache.Values)
                if (e.Thumbnail != null) DestroyImmediate(e.Thumbnail);
            _asCache.Clear();
        }

        // ── OnGUI ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            // 削除の後処理（foreach の外で行う）
            if (_pendingDelete != null)
            {
                if (_cache.TryGetValue(_pendingDelete, out var e) && e.Thumbnail != null)
                    DestroyImmediate(e.Thumbnail);
                _cache.Remove(_pendingDelete);
                AssetLinkerStorage.RemoveProduct(_pendingDelete);
                _data          = AssetLinkerStorage.Load();
                _pendingDelete = null;
            }
            if (_pendingDeleteAs != null)
            {
                if (_asCache.TryGetValue(_pendingDeleteAs, out var e) && e.Thumbnail != null)
                    DestroyImmediate(e.Thumbnail);
                _asCache.Remove(_pendingDeleteAs);
                AssetStoreStorage.RemoveProduct(_pendingDeleteAs);
                _asData          = AssetStoreStorage.Load();
                _pendingDeleteAs = null;
            }

            DrawToolbar();

            bool hasAny = (_data?.products.Count > 0) || (_asData?.products.Count > 0);
            if (!hasAny)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    "登録済み商品がありません。「登録」ボタンから追加してください。",
                    EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_data != null)
                foreach (var product in _data.products)
                    DrawProductRow(product);
            if (_asData != null)
                foreach (var product in _asData.products)
                    DrawAsProductRow(product);
            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("Booth Asset Linker", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("登録", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    GetWindow<RegistrationWindow>("Register Product");
                if (GUILayout.Button("一括登録", EditorStyles.toolbarButton, GUILayout.Width(56)))
                    GetWindow<ScanAllWindow>("一括登録");
                if (GUILayout.Button("AS 登録", EditorStyles.toolbarButton, GUILayout.Width(52)))
                {
                    var w = GetWindow<AssetStoreScanAllWindow>("AS 一括登録");
                    w.ShowUtility();
                }
                if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    ClearCache();
                    Reload();
                }
                if (GUILayout.Button("キャッシュクリア", EditorStyles.toolbarButton, GUILayout.Width(84)))
                {
                    ThumbnailCache.ClearAll();
                    ClearCache();
                    Reload();
                }
            }
        }

        // ── 商品行 ────────────────────────────────────────────────────────────

        void DrawProductRow(ProductData product)
        {
            _cache.TryGetValue(product.boothItemId, out var entry);

            Rect row = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.Height(RowHeight), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                DrawRowContent(row, product, entry);

            // 飛び先変更ボタン（右上）
            var rePickRect = new Rect(row.xMax - 48f, row.y + 4f, 20f, 18f);
            if (GUI.Button(rePickRect, "…", EditorStyles.miniButton))
                RePickTargetDir(product);

            // 削除ボタン（右上）
            var deleteRect = new Rect(row.xMax - 24f, row.y + 4f, 20f, 18f);
            if (GUI.Button(deleteRect, "×", EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("削除確認",
                    $"商品 {product.boothItemId} の登録を解除しますか？", "削除", "キャンセル"))
                {
                    _pendingDelete = product.boothItemId;
                    Repaint();
                }
            }

            // 行クリック → Ping
            if (Event.current.type == EventType.MouseDown
                && row.Contains(Event.current.mousePosition)
                && !rePickRect.Contains(Event.current.mousePosition)
                && !deleteRect.Contains(Event.current.mousePosition))
            {
                PingProduct(product);
                Event.current.Use();
            }

            // 区切り線
            EditorGUI.DrawRect(
                new Rect(row.x, row.yMax - 1f, row.width, 1f),
                new Color(0.2f, 0.2f, 0.2f));
        }

        void DrawRowContent(Rect row, ProductData product, CacheEntry entry)
        {
            // サムネイル
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

            // テキスト
            float tx = thumbRect.xMax + RowPadding;
            float tw = row.width - tx - 54f;

            string name = entry?.ProductName ?? $"ID: {product.boothItemId}";
            GUI.Label(new Rect(tx, row.y + 10f, tw, 20f), name, EditorStyles.boldLabel);
            GUI.Label(new Rect(tx, row.y + 30f, tw, 16f), $"ID: {product.boothItemId}", EditorStyles.miniLabel);
            GUI.Label(new Rect(tx, row.y + 46f, tw, 16f), product.targetDirectory, EditorStyles.miniLabel);
        }

        // ── フォルダ Ping ─────────────────────────────────────────────────────

        void PingProduct(ProductData product)
        {
            if (!AssetDatabase.IsValidFolder(product.targetDirectory))
            {
                bool pick = EditorUtility.DisplayDialog(
                    "フォルダが見つかりません",
                    $"「{product.targetDirectory}」が存在しません。\n選び直しますか？",
                    "選び直す", "キャンセル");
                if (pick) RePickTargetDir(product);
                return;
            }

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(product.targetDirectory);
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        void RePickTargetDir(ProductData product)
        {
            string picked = EditorUtility.OpenFolderPanel("新しい飛び先を選択", "Assets", "");
            if (string.IsNullOrEmpty(picked)) return;

            string projectRoot = Path.GetDirectoryName(Application.dataPath)
                .Replace('\\', '/').TrimEnd('/') + "/";
            string norm = picked.Replace('\\', '/');

            if (!norm.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("エラー",
                    "プロジェクト内のフォルダを選択してください。", "OK");
                return;
            }

            string rel = norm.Substring(projectRoot.Length);
            if (!AssetDatabase.IsValidFolder(rel))
            {
                EditorUtility.DisplayDialog("エラー", $"有効なフォルダではありません: {rel}", "OK");
                return;
            }

            product.targetDirectory = rel;
            AssetLinkerStorage.SaveProduct(product);
            _data = AssetLinkerStorage.Load();
            Repaint();
        }

        // ── Asset Store 商品行 ────────────────────────────────────────────────

        void DrawAsProductRow(AssetStoreProductData product)
        {
            _asCache.TryGetValue(product.compositeId, out var entry);

            Rect row = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.Height(RowHeight), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                DrawAsRowContent(row, product, entry);

            var rePickRect = new Rect(row.xMax - 48f, row.y + 4f, 20f, 18f);
            if (GUI.Button(rePickRect, "…", EditorStyles.miniButton))
                RePickAsTargetDir(product);

            var deleteRect = new Rect(row.xMax - 24f, row.y + 4f, 20f, 18f);
            if (GUI.Button(deleteRect, "×", EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("削除確認",
                    $"「{product.displayName}」の登録を解除しますか？", "削除", "キャンセル"))
                {
                    _pendingDeleteAs = product.compositeId;
                    Repaint();
                }
            }

            if (Event.current.type == EventType.MouseDown
                && row.Contains(Event.current.mousePosition)
                && !rePickRect.Contains(Event.current.mousePosition)
                && !deleteRect.Contains(Event.current.mousePosition))
            {
                PingAsProduct(product);
                Event.current.Use();
            }

            EditorGUI.DrawRect(
                new Rect(row.x, row.yMax - 1f, row.width, 1f),
                new Color(0.2f, 0.2f, 0.2f));
        }

        void DrawAsRowContent(Rect row, AssetStoreProductData product, CacheEntry entry)
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
            float tw = row.width - tx - 54f;

            GUI.Label(new Rect(tx, row.y + 10f, tw, 20f), product.displayName, EditorStyles.boldLabel);
            GUI.Label(new Rect(tx, row.y + 30f, tw, 16f), product.publisherName, EditorStyles.miniLabel);
            GUI.Label(new Rect(tx, row.y + 46f, tw, 16f), product.targetDirectory, EditorStyles.miniLabel);
        }

        void PingAsProduct(AssetStoreProductData product)
        {
            if (!AssetDatabase.IsValidFolder(product.targetDirectory))
            {
                bool pick = EditorUtility.DisplayDialog(
                    "フォルダが見つかりません",
                    $"「{product.targetDirectory}」が存在しません。\n選び直しますか？",
                    "選び直す", "キャンセル");
                if (pick) RePickAsTargetDir(product);
                return;
            }

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(product.targetDirectory);
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        void RePickAsTargetDir(AssetStoreProductData product)
        {
            string picked = EditorUtility.OpenFolderPanel("新しい飛び先を選択", "Assets", "");
            if (string.IsNullOrEmpty(picked)) return;

            string projectRoot = Path.GetDirectoryName(Application.dataPath)
                .Replace('\\', '/').TrimEnd('/') + "/";
            string norm = picked.Replace('\\', '/');

            if (!norm.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("エラー",
                    "プロジェクト内のフォルダを選択してください。", "OK");
                return;
            }

            string rel = norm.Substring(projectRoot.Length);
            if (!AssetDatabase.IsValidFolder(rel))
            {
                EditorUtility.DisplayDialog("エラー", $"有効なフォルダではありません: {rel}", "OK");
                return;
            }

            product.targetDirectory = rel;
            AssetStoreStorage.SaveProduct(product);
            _asData = AssetStoreStorage.Load();
            Repaint();
        }

        // ── Asset Store アイコン読み込み ──────────────────────────────────────

        async Task LoadAssetStoreIconAsync(string compositeId, string displayName, string localPackagePath)
        {
            _asCache[compositeId] = new CacheEntry { Loading = true, ProductName = displayName };

            string cacheKey = ThumbnailCache.AsKey(compositeId);
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
                if (iconBytes != null)
                {
                    thumb = new Texture2D(2, 2);
                    thumb.LoadImage(iconBytes);
                }
                _asCache[compositeId] = new CacheEntry
                {
                    Thumbnail   = thumb,
                    ProductName = displayName,
                    Loading     = false
                };
                Repaint();
            };
        }

        // ── Booth 情報取得 ────────────────────────────────────────────────────

        async Task FetchBoothInfoAsync(string boothItemId)
        {
            _cache[boothItemId] = new CacheEntry { Loading = true };

            string key      = ThumbnailCache.BoothKey(boothItemId);
            bool   hasImg   = ThumbnailCache.TryLoad(key,     out var cachedImg);
            bool   hasMeta  = ThumbnailCache.TryLoadMeta(key, out var cachedTitle);

            // 完全キャッシュヒット：HTTP不要
            if (hasImg && hasMeta)
            {
                EditorApplication.delayCall += () =>
                {
                    var thumb = new Texture2D(2, 2);
                    thumb.LoadImage(cachedImg);
                    _cache[boothItemId] = new CacheEntry
                    {
                        Thumbnail   = thumb,
                        ProductName = cachedTitle,
                        Loading     = false
                    };
                    Repaint();
                };
                return;
            }

            // 部分キャッシュ or 未キャッシュ：HTML取得
            try
            {
                string html  = await Http.GetStringAsync($"https://booth.pm/ja/items/{boothItemId}");
                string title = ParseOgMeta(html, "og:title");

                if (!hasMeta)
                    ThumbnailCache.SaveMeta(key, title);

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
                    {
                        Thumbnail   = thumb,
                        ProductName = title,
                        Loading     = false
                    };
                    Repaint();
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AssetLinker] Booth fetch failed ({boothItemId}): {ex.Message}");
                EditorApplication.delayCall += () =>
                {
                    // 画像キャッシュがあれば表示を維持する
                    Texture2D thumb = null;
                    if (cachedImg != null)
                    {
                        thumb = new Texture2D(2, 2);
                        thumb.LoadImage(cachedImg);
                    }
                    _cache[boothItemId] = new CacheEntry
                    {
                        Thumbnail   = thumb,
                        ProductName = cachedTitle ?? "(取得失敗)",
                        Loading     = false
                    };
                    Repaint();
                };
            }
        }

        static string ParseOgMeta(string html, string property)
        {
            string pat = Regex.Escape(property);

            // <meta property="og:xxx" content="..." />
            var m = Regex.Match(html,
                $@"<meta[^>]+property=""{pat}""[^>]+content=""([^""]+)""",
                RegexOptions.IgnoreCase);
            if (m.Success) return System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);

            // 属性の順序が逆の場合
            m = Regex.Match(html,
                $@"<meta[^>]+content=""([^""]+)""[^>]+property=""{pat}""",
                RegexOptions.IgnoreCase);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
        }
    }
}
