using System;
using System.Collections.Generic;

namespace VRCAssetTracker
{
    [Serializable]
    public class AssetStoreProductData
    {
        // "{Publisher}/{PackageName}" 窶・unique key
        public string compositeId;
        public string displayName;
        public string publisherName;
        // Absolute path to the .unitypackage file on disk (for icon re-extraction)
        public string localPackagePath;
        public string targetDirectory;
        public List<PackageData> packages = new List<PackageData>();
    }

    [Serializable]
    public class AssetStoreLinkerData
    {
        public int version = 1;
        public List<AssetStoreProductData> products = new List<AssetStoreProductData>();
    }
}

