using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace VRCAssetTracker
{
    public class UnitypackageParseResult
    {
        public string SourcePath;  // full path to the .unitypackage file (not persisted)
        public string FileName;
        public List<string> Files = new List<string>();
        public byte[] IconBytes;   // embedded .icon.png bytes, null if absent (not persisted)
    }

    public static class UnitypackageParser
    {
        /// <summary>
        /// Parses a .unitypackage file and returns all asset paths found in it.
        /// The package is a .tar.gz archive; each entry's "pathname" file (first line)
        /// gives the project-relative path the asset would be imported to.
        /// </summary>
        public static UnitypackageParseResult Parse(string packagePath)
        {
            var result = new UnitypackageParseResult
            {
                SourcePath = packagePath,
                FileName   = Path.GetFileName(packagePath)
            };

            using (var fs = File.OpenRead(packagePath))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            {
                var pathnames = new Dictionary<string, string>(); // GUID -> asset path
                ReadTar(gz, pathnames, result);
                result.Files.AddRange(pathnames.Values);
            }

            return result;
        }

        static void ReadTar(Stream stream, Dictionary<string, string> pathnames, UnitypackageParseResult result)
        {
            var header = new byte[512];
            int zeroBlocks = 0;

            while (true)
            {
                if (!ReadAll(stream, header, 512)) return;

                if (IsZeroBlock(header))
                {
                    if (++zeroBlocks >= 2) return;
                    continue;
                }
                zeroBlocks = 0;

                string name     = ReadNullTermString(header, 0, 100);
                string prefix   = ReadNullTermString(header, 345, 155);
                long   size     = ParseOctal(header, 124, 12);
                char   typeflag = (char)header[156];

                // Build full entry path; ustar prefix goes before name
                string fullPath = string.IsNullOrEmpty(prefix)
                    ? name
                    : prefix.TrimEnd('/') + "/" + name;
                fullPath = fullPath.TrimEnd('/');

                long paddedSize = ((size + 511) / 512) * 512;

                bool isRegular = typeflag == '0' || typeflag == '\0';
                if (isRegular && IsPathnameEntry(fullPath) && size > 0)
                {
                    string guid = fullPath.Substring(0, fullPath.Length - "/pathname".Length);
                    var buf = new byte[size];
                    ReadAll(stream, buf, (int)size);

                    string content  = Encoding.UTF8.GetString(buf);
                    int    nl       = content.IndexOfAny(new[] { '\n', '\r' });
                    string assetPath = (nl >= 0 ? content.Substring(0, nl) : content).Trim();
                    if (!string.IsNullOrEmpty(assetPath))
                        pathnames[guid] = assetPath;

                    long padding = paddedSize - size;
                    if (padding > 0) Skip(stream, padding);
                }
                else if (isRegular && fullPath.EndsWith("/.icon.png") && size > 0 && result.IconBytes == null)
                {
                    var buf = new byte[size];
                    ReadAll(stream, buf, (int)size);
                    result.IconBytes = buf;

                    long padding = paddedSize - size;
                    if (padding > 0) Skip(stream, padding);
                }
                else
                {
                    Skip(stream, paddedSize);
                }
            }
        }

        // A pathname entry has exactly the form "{GUID}/pathname"
        static bool IsPathnameEntry(string path)
        {
            var parts = path.Split('/');
            return parts.Length == 2 && parts[1] == "pathname";
        }

        static bool ReadAll(Stream s, byte[] buf, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int n = s.Read(buf, offset, count - offset);
                if (n == 0) return false;
                offset += n;
            }
            return true;
        }

        static void Skip(Stream s, long bytes)
        {
            var buf = new byte[4096];
            long rem = bytes;
            while (rem > 0)
            {
                int n = s.Read(buf, 0, (int)Math.Min(rem, buf.Length));
                if (n == 0) return;
                rem -= n;
            }
        }

        static bool IsZeroBlock(byte[] block)
        {
            foreach (byte b in block)
                if (b != 0) return false;
            return true;
        }

        static string ReadNullTermString(byte[] buf, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && buf[end] != 0) end++;
            return Encoding.UTF8.GetString(buf, offset, end - offset);
        }

        static long ParseOctal(byte[] buf, int offset, int length)
        {
            // GNU tar uses base-256 encoding when the high bit of the first byte is set
            if ((buf[offset] & 0x80) != 0)
            {
                long val = 0;
                for (int i = 1; i < length; i++)
                    val = (val << 8) | buf[offset + i];
                return val;
            }
            string str = ReadNullTermString(buf, offset, length).Trim();
            if (string.IsNullOrEmpty(str)) return 0;
            try { return Convert.ToInt64(str, 8); }
            catch { return 0; }
        }
    }
}

