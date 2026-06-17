using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VRCAssetTracker
{
    public static class AssetStoreStorage
    {
        const string FileName = "assetstore-asset-linker.json";

        static string FilePath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserSettings", FileName);

        public static AssetStoreLinkerData Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return new AssetStoreLinkerData();

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonUtility.FromJson<AssetStoreLinkerData>(json);
                return data ?? new AssetStoreLinkerData();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AssetLinker] Failed to load {path}: {ex.Message}");
                return new AssetStoreLinkerData();
            }
        }

        public static void Save(AssetStoreLinkerData data)
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: true), Encoding.UTF8);
        }

        public static void SaveProduct(AssetStoreProductData product)
        {
            var data = Load();
            int idx = data.products.FindIndex(p => p.compositeId == product.compositeId);
            if (idx >= 0)
                data.products[idx] = product;
            else
                data.products.Add(product);
            Save(data);
        }

        public static void RemoveProduct(string compositeId)
        {
            var data = Load();
            data.products.RemoveAll(p => p.compositeId == compositeId);
            Save(data);
        }
    }
}

