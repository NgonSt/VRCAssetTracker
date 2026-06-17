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
        string  _pendingDelete;     // 蜑企勁縺ｯ繝輔Ξ繝ｼ繝譛ｫ蟆ｾ縺ｧ蜃ｦ逅・＠縺ｦ foreach 荳ｭ縺ｮ螟画峩繧帝∩縺代ｋ
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

        // 笏笏 繝・・繧ｿ隱ｭ縺ｿ霎ｼ縺ｿ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

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

        // 笏笏 OnGUI 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void OnGUI()
        {
            // 蜑企勁縺ｮ蠕悟・逅・ｼ・oreach 縺ｮ螟悶〒陦後≧・・            if (_pendingDelete != null)
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
                    "逋ｻ骭ｲ貂医∩蝠・刀縺後≠繧翫∪縺帙ｓ縲ゅ檎匳骭ｲ縲阪・繧ｿ繝ｳ縺九ｉ霑ｽ蜉縺励※縺上□縺輔＞縲・,
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
                if (GUILayout.Button("逋ｻ骭ｲ", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    GetWindow<RegistrationWindow>("Register Product");
                if (GUILayout.Button("荳諡ｬ逋ｻ骭ｲ", EditorStyles.toolbarButton, GUILayout.Width(56)))
                    GetWindow<ScanAllWindow>("荳諡ｬ逋ｻ骭ｲ");
                if (GUILayout.Button("AS 逋ｻ骭ｲ", EditorStyles.toolbarButton, GUILayout.Width(52)))
                {
                    var w = GetWindow<AssetStoreScanAllWindow>("AS 荳諡ｬ逋ｻ骭ｲ");
                    w.ShowUtility();
                }
                if (GUILayout.Button("譖ｴ譁ｰ", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    ClearCache();
                    Reload();
                }
                if (GUILayout.Button("繧ｭ繝｣繝・す繝･繧ｯ繝ｪ繧｢", EditorStyles.toolbarButton, GUILayout.Width(84)))
                {
                    ThumbnailCache.ClearAll();
                    ClearCache();
                    Reload();
                }
            }
        }

        // 笏笏 蝠・刀陦・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawProductRow(ProductData product)
        {
            _cache.TryGetValue(product.boothItemId, out var entry);

            Rect row = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.Height(RowHeight), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                DrawRowContent(row, product, entry);

            // 鬟帙・蜈亥､画峩繝懊ち繝ｳ・亥承荳奇ｼ・            var rePickRect = new Rect(row.xMax - 48f, row.y + 4f, 20f, 18f);
            if (GUI.Button(rePickRect, "窶ｦ", EditorStyles.miniButton))
                RePickTargetDir(product);

            // 蜑企勁繝懊ち繝ｳ・亥承荳奇ｼ・            var deleteRect = new Rect(row.xMax - 24f, row.y + 4f, 20f, 18f);
            if (GUI.Button(deleteRect, "ﾃ・, EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("蜑企勁遒ｺ隱・,
                    $"蝠・刀 {product.boothItemId} 縺ｮ逋ｻ骭ｲ繧定ｧ｣髯､縺励∪縺吶°・・, "蜑企勁", "繧ｭ繝｣繝ｳ繧ｻ繝ｫ"))
                {
                    _pendingDelete = product.boothItemId;
                    Repaint();
                }
            }

            // 陦後け繝ｪ繝・け 竊・Ping
            if (Event.current.type == EventType.MouseDown
                && row.Contains(Event.current.mousePosition)
                && !rePickRect.Contains(Event.current.mousePosition)
                && !deleteRect.Contains(Event.current.mousePosition))
            {
                PingProduct(product);
                Event.current.Use();
            }

            // 蛹ｺ蛻・ｊ邱・            EditorGUI.DrawRect(
                new Rect(row.x, row.yMax - 1f, row.width, 1f),
                new Color(0.2f, 0.2f, 0.2f));
        }

        void DrawRowContent(Rect row, ProductData product, CacheEntry entry)
        {
            // 繧ｵ繝繝阪う繝ｫ
            var thumbRect = new Rect(
                row.x + RowPadding,
                row.y + (RowHeight - ThumbSize) / 2f,
                ThumbSize, ThumbSize);

            if (entry?.Thumbnail != null)
                GUI.DrawTexture(thumbRect, entry.Thumbnail, ScaleMode.ScaleToFit);
            else if (entry?.Loading == true)
                GUI.Label(thumbRect, "窶ｦ", EditorStyles.centeredGreyMiniLabel);
            else
                EditorGUI.DrawRect(thumbRect, new Color(0.22f, 0.22f, 0.22f));

            // 繝・く繧ｹ繝・            float tx = thumbRect.xMax + RowPadding;
            float tw = row.width - tx - 54f;

            string name = entry?.ProductName ?? $"ID: {product.boothItemId}";
            GUI.Label(new Rect(tx, row.y + 10f, tw, 20f), name, EditorStyles.boldLabel);
            GUI.Label(new Rect(tx, row.y + 30f, tw, 16f), $"ID: {product.boothItemId}", EditorStyles.miniLabel);
            GUI.Label(new Rect(tx, row.y + 46f, tw, 16f), product.targetDirectory, EditorStyles.miniLabel);
        }

        // 笏笏 繝輔か繝ｫ繝 Ping 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void PingProduct(ProductData product)
        {
            if (!AssetDatabase.IsValidFolder(product.targetDirectory))
            {
                bool pick = EditorUtility.DisplayDialog(
                    "繝輔か繝ｫ繝縺瑚ｦ九▽縺九ｊ縺ｾ縺帙ｓ",
                    $"縲鶏product.targetDirectory}縲阪′蟄伜惠縺励∪縺帙ｓ縲・n驕ｸ縺ｳ逶ｴ縺励∪縺吶°・・,
                    "驕ｸ縺ｳ逶ｴ縺・, "繧ｭ繝｣繝ｳ繧ｻ繝ｫ");
                if (pick) RePickTargetDir(product);
                return;
            }

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(product.targetDirectory);
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        void RePickTargetDir(ProductData product)
        {
            string picked = EditorUtility.OpenFolderPanel("譁ｰ縺励＞鬟帙・蜈医ｒ驕ｸ謚・, "Assets", "");
            if (string.IsNullOrEmpty(picked)) return;

            string projectRoot = Path.GetDirectoryName(Application.dataPath)
                .Replace('\\', '/').TrimEnd('/') + "/";
            string norm = picked.Replace('\\', '/');

            if (!norm.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("繧ｨ繝ｩ繝ｼ",
                    "繝励Ο繧ｸ繧ｧ繧ｯ繝亥・縺ｮ繝輔か繝ｫ繝繧帝∈謚槭＠縺ｦ縺上□縺輔＞縲・, "OK");
                return;
            }

            string rel = norm.Substring(projectRoot.Length);
            if (!AssetDatabase.IsValidFolder(rel))
            {
                EditorUtility.DisplayDialog("繧ｨ繝ｩ繝ｼ", $"譛牙柑縺ｪ繝輔か繝ｫ繝縺ｧ縺ｯ縺ゅｊ縺ｾ縺帙ｓ: {rel}", "OK");
                return;
            }

            product.targetDirectory = rel;
            AssetLinkerStorage.SaveProduct(product);
            _data = AssetLinkerStorage.Load();
            Repaint();
        }

        // 笏笏 Asset Store 蝠・刀陦・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        void DrawAsProductRow(AssetStoreProductData product)
        {
            _asCache.TryGetValue(product.compositeId, out var entry);

            Rect row = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.Height(RowHeight), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                DrawAsRowContent(row, product, entry);

            var rePickRect = new Rect(row.xMax - 48f, row.y + 4f, 20f, 18f);
            if (GUI.Button(rePickRect, "窶ｦ", EditorStyles.miniButton))
                RePickAsTargetDir(product);

            var deleteRect = new Rect(row.xMax - 24f, row.y + 4f, 20f, 18f);
            if (GUI.Button(deleteRect, "ﾃ・, EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("蜑企勁遒ｺ隱・,
                    $"縲鶏product.displayName}縲阪・逋ｻ骭ｲ繧定ｧ｣髯､縺励∪縺吶°・・, "蜑企勁", "繧ｭ繝｣繝ｳ繧ｻ繝ｫ"))
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
                GUI.Label(thumbRect, "窶ｦ", EditorStyles.centeredGreyMiniLabel);
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
                    "繝輔か繝ｫ繝縺瑚ｦ九▽縺九ｊ縺ｾ縺帙ｓ",
                    $"縲鶏product.targetDirectory}縲阪′蟄伜惠縺励∪縺帙ｓ縲・n驕ｸ縺ｳ逶ｴ縺励∪縺吶°・・,
                    "驕ｸ縺ｳ逶ｴ縺・, "繧ｭ繝｣繝ｳ繧ｻ繝ｫ");
                if (pick) RePickAsTargetDir(product);
                return;
            }

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(product.targetDirectory);
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        void RePickAsTargetDir(AssetStoreProductData product)
        {
            string picked = EditorUtility.OpenFolderPanel("譁ｰ縺励＞鬟帙・蜈医ｒ驕ｸ謚・, "Assets", "");
            if (string.IsNullOrEmpty(picked)) return;

            string projectRoot = Path.GetDirectoryName(Application.dataPath)
                .Replace('\\', '/').TrimEnd('/') + "/";
            string norm = picked.Replace('\\', '/');

            if (!norm.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("繧ｨ繝ｩ繝ｼ",
                    "繝励Ο繧ｸ繧ｧ繧ｯ繝亥・縺ｮ繝輔か繝ｫ繝繧帝∈謚槭＠縺ｦ縺上□縺輔＞縲・, "OK");
                return;
            }

            string rel = norm.Substring(projectRoot.Length);
            if (!AssetDatabase.IsValidFolder(rel))
            {
                EditorUtility.DisplayDialog("繧ｨ繝ｩ繝ｼ", $"譛牙柑縺ｪ繝輔か繝ｫ繝縺ｧ縺ｯ縺ゅｊ縺ｾ縺帙ｓ: {rel}", "OK");
                return;
            }

            product.targetDirectory = rel;
            AssetStoreStorage.SaveProduct(product);
            _asData = AssetStoreStorage.Load();
            Repaint();
        }

        // 笏笏 Asset Store 繧｢繧､繧ｳ繝ｳ隱ｭ縺ｿ霎ｼ縺ｿ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

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

        // 笏笏 Booth 諠・ｱ蜿門ｾ・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        async Task FetchBoothInfoAsync(string boothItemId)
        {
            _cache[boothItemId] = new CacheEntry { Loading = true };

            string key      = ThumbnailCache.BoothKey(boothItemId);
            bool   hasImg   = ThumbnailCache.TryLoad(key,     out var cachedImg);
            bool   hasMeta  = ThumbnailCache.TryLoadMeta(key, out var cachedTitle);

            // 螳悟・繧ｭ繝｣繝・す繝･繝偵ャ繝茨ｼ唏TTP荳崎ｦ・            if (hasImg && hasMeta)
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

            // 驛ｨ蛻・く繝｣繝・す繝･ or 譛ｪ繧ｭ繝｣繝・す繝･・唏TML蜿門ｾ・            try
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
                    // 逕ｻ蜒上く繝｣繝・す繝･縺後≠繧後・陦ｨ遉ｺ繧堤ｶｭ謖√☆繧・                    Texture2D thumb = null;
                    if (cachedImg != null)
                    {
                        thumb = new Texture2D(2, 2);
                        thumb.LoadImage(cachedImg);
                    }
                    _cache[boothItemId] = new CacheEntry
                    {
                        Thumbnail   = thumb,
                        ProductName = cachedTitle ?? "(蜿門ｾ怜､ｱ謨・",
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

            // 螻樊ｧ縺ｮ鬆・ｺ上′騾・・蝣ｴ蜷・            m = Regex.Match(html,
                $@"<meta[^>]+content=""([^""]+)""[^>]+property=""{pat}""",
                RegexOptions.IgnoreCase);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
        }
    }
}

