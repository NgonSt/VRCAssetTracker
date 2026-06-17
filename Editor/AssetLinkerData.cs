using System;
using System.Collections.Generic;

namespace VRCAssetTracker
{
    [Serializable]
    public class PackageData
    {
        public string fileName;
        public List<string> files = new List<string>();
    }

    [Serializable]
    public class ProductData
    {
        public string boothItemId;
        public string targetDirectory;
        public List<PackageData> packages = new List<PackageData>();
    }

    [Serializable]
    public class AssetLinkerData
    {
        public int version = 1;
        public List<ProductData> products = new List<ProductData>();
    }
}

