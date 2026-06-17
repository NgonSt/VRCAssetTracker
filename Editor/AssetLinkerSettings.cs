using System;
using System.IO;
using UnityEditor;

namespace VRCAssetTracker
{
    public static class AssetLinkerSettings
    {
        const string KeyBoothRoot      = "MyAssetManager.BoothLibraryRoot";
        const string KeyAssetStoreRoot = "MyAssetManager.AssetStoreRoot";

        public static string BoothLibraryRoot
        {
            get => EditorPrefs.GetString(KeyBoothRoot, @"D:\VRC\Booth");
            set => EditorPrefs.SetString(KeyBoothRoot, value);
        }

        public static string AssetStoreLibraryRoot
        {
            get => EditorPrefs.GetString(KeyAssetStoreRoot, DefaultAssetStoreRoot);
            set => EditorPrefs.SetString(KeyAssetStoreRoot, value);
        }

        static string DefaultAssetStoreRoot =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Unity", "Asset Store-5.x");
    }
}
