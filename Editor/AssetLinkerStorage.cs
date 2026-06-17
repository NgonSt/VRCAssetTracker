using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VRCAssetTracker
{
    public static class AssetLinkerStorage
    {
        const string FileName = "booth-asset-linker.json";

        // <project>/UserSettings/booth-asset-linker.json
        static string FilePath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserSettings", FileName);

        public static AssetLinkerData Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return new AssetLinkerData();

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonUtility.FromJson<AssetLinkerData>(json);
                return data ?? new AssetLinkerData();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AssetLinker] Failed to load {path}: {ex.Message}");
                return new AssetLinkerData();
            }
        }

        public static void Save(AssetLinkerData data)
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: true), Encoding.UTF8);
        }

        /// <summary>
        /// Adds or replaces a product entry. Keyed by boothItemId.
        /// </summary>
        public static void SaveProduct(ProductData product)
        {
            var data = Load();
            int idx = data.products.FindIndex(p => p.boothItemId == product.boothItemId);
            if (idx >= 0)
                data.products[idx] = product;
            else
                data.products.Add(product);
            Save(data);
        }

        public static void RemoveProduct(string boothItemId)
        {
            var data = Load();
            data.products.RemoveAll(p => p.boothItemId == boothItemId);
            Save(data);
        }
    }
}

