using System.IO;
using UnityEngine;

namespace VRCAssetTracker
{
    public static class ThumbnailCache
    {
        static string Dir => Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "UserSettings", "MyAssetManager", "thumbnails");

        public static bool TryLoad(string key, out byte[] bytes)
        {
            string path = Path.Combine(Dir, key + ".png");
            if (File.Exists(path)) { bytes = File.ReadAllBytes(path); return true; }
            bytes = null;
            return false;
        }

        public static void Save(string key, byte[] bytes)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Path.Combine(Dir, key + ".png"), bytes);
        }

        public static bool TryLoadMeta(string key, out string text)
        {
            string path = Path.Combine(Dir, key + ".txt");
            if (File.Exists(path)) { text = File.ReadAllText(path, System.Text.Encoding.UTF8); return true; }
            text = null;
            return false;
        }

        public static void SaveMeta(string key, string text)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, key + ".txt"), text, System.Text.Encoding.UTF8);
        }

        public static void ClearAll()
        {
            if (Directory.Exists(Dir))
                Directory.Delete(Dir, recursive: true);
        }

        public static string BoothKey(string boothItemId)    => "booth_" + boothItemId;
        public static string AsKey(string compositeId)        => "as_" + compositeId.Replace('/', '_').Replace('\\', '_');
    }
}

