using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TurnipDash.Content
{
    /// One image read out of the player's Turnip Mountain install, still as raw bytes
    /// (made into a Unity texture on the main thread).
    public sealed class RawImage
    {
        public string Name, Source;
        public byte[] Png;                       // loose image file, or
        public byte[] Raw; public int Format, Width, Height;   // a Unity Texture2D's first mip
        public float RectX, RectY, RectW, RectH; // sprite rect inside the texture (pixels, bottom-left)
    }

    /// Reads named images from a game folder: Unity builds (serialized files and asset bundles,
    /// with the bundled class database for stripped type trees), then loose image files.
    public static class TmImages
    {
        public static readonly List<string> Report = new List<string>();

        public static Dictionary<string, RawImage> Find(string gameDir, IList<string> matches, Stream classPackage)
        {
            Report.Clear();
            var found = new Dictionary<string, (RawImage img, int score)>();
            try { ScanUnity(gameDir, matches, classPackage, found); }
            catch (Exception e) { Report.Add("unity scan stopped: " + e.Message); }
            ScanLoose(gameDir, matches, found);
            return found.ToDictionary(k => k.Key, v => v.Value.img);
        }

        // lower is better: exact name, then prefix, then shortest name containing the match
        static int Score(string name, string match)
        {
            var n = name.ToLowerInvariant();
            if (n == match) return 0;
            if (n.StartsWith(match)) return 100 + n.Length;
            return 1000 + n.Length;
        }

        static void Offer(Dictionary<string, (RawImage, int)> found, string match, RawImage img, int score)
        {
            Report.Add($"candidate '{match}': {img.Name} ({img.Source}) score {score}");
            if (!found.TryGetValue(match, out var cur) || score < cur.Item2) found[match] = (img, score);
        }

        static void ScanLoose(string dir, IList<string> matches, Dictionary<string, (RawImage, int)> found)
        {
            foreach (var ext in new[] { "*.png", "*.jpg" })
                foreach (var f in SafeFiles(dir, ext))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    foreach (var m in matches)
                        if (name.ToLowerInvariant().Contains(m) && !found.ContainsKey(m))
                            Offer(found, m, new RawImage { Name = name, Source = f, Png = File.ReadAllBytes(f) }, 5000 + name.Length);
                }
        }

        static IEnumerable<string> SafeFiles(string dir, string pattern)
        {
            try { return Directory.GetFiles(dir, pattern, SearchOption.AllDirectories); }
            catch { return Array.Empty<string>(); }
        }

        static void ScanUnity(string gameDir, IList<string> matches, Stream classPackage, Dictionary<string, (RawImage, int)> found)
        {
            var dataDir = Directory.GetDirectories(gameDir, "*_Data").FirstOrDefault();
            if (dataDir == null) { Report.Add("no *_Data folder: not a Unity build"); return; }
            Report.Add("Unity data folder: " + dataDir);

            var am = new AssetsManager();
            am.LoadClassPackage(classPackage);
            var serialized = Directory.GetFiles(dataDir).Where(f =>
            {
                var n = Path.GetFileName(f);
                return n.EndsWith(".assets") || (n.StartsWith("level") && !n.Contains("."));
            });
            foreach (var path in serialized)
            {
                try
                {
                    var inst = am.LoadAssetsFile(path, false);
                    am.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);
                    ScanFile(am, inst, matches, found, dataDir, null);
                }
                catch (Exception e) { Report.Add(Path.GetFileName(path) + ": " + e.Message); }
            }
            foreach (var path in SafeFiles(dataDir, "*.bundle"))
            {
                try
                {
                    var bun = am.LoadBundleFile(path, true);
                    var dirs = bun.file.BlockAndDirInfo.DirectoryInfos;
                    for (int i = 0; i < dirs.Count; i++)
                    {
                        if (!bun.file.IsAssetsFile(i)) continue;
                        var inst = am.LoadAssetsFileFromBundle(bun, i, false);
                        am.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);
                        ScanFile(am, inst, matches, found, dataDir, bun);
                    }
                }
                catch (Exception e) { Report.Add(Path.GetFileName(path) + ": " + e.Message); }
            }
            am.UnloadAll(true);
        }

        static void ScanFile(AssetsManager am, AssetsFileInstance inst, IList<string> matches,
                             Dictionary<string, (RawImage, int)> found, string dataDir, BundleFileInstance bun)
        {
            // Sprites first (they carry the cut-out rect), then whole textures.
            foreach (var info in inst.file.GetAssetsOfType(AssetClassID.Sprite))
            {
                var bf = am.GetBaseField(inst, info);
                var name = bf["m_Name"].AsString;
                foreach (var m in matches)
                {
                    if (!name.ToLowerInvariant().Contains(m)) continue;
                    var rd = bf["m_RD"];
                    var tex = am.GetExtAsset(inst, rd["texture"]);
                    if (tex.baseField == null) continue;
                    var img = ReadTexture(tex.baseField, dataDir, bun, name, inst.name);
                    if (img == null) continue;
                    var r = rd["textureRect"];
                    img.RectX = r["x"].AsFloat; img.RectY = r["y"].AsFloat; img.RectW = r["width"].AsFloat; img.RectH = r["height"].AsFloat;
                    Offer(found, m, img, Score(name, m));
                }
            }
            foreach (var info in inst.file.GetAssetsOfType(AssetClassID.Texture2D))
            {
                var bf = am.GetBaseField(inst, info);
                var name = bf["m_Name"].AsString;
                foreach (var m in matches)
                {
                    if (!name.ToLowerInvariant().Contains(m)) continue;
                    var img = ReadTexture(bf, dataDir, bun, name, inst.name);
                    if (img != null) Offer(found, m, img, 500 + Score(name, m));
                }
            }
        }

        static RawImage ReadTexture(AssetTypeValueField t, string dataDir, BundleFileInstance bun, string name, string file)
        {
            int w = t["m_Width"].AsInt, h = t["m_Height"].AsInt, fmt = t["m_TextureFormat"].AsInt;
            int size0 = Mip0Size(fmt, w, h);
            if (size0 <= 0) { Report.Add($"{name}: texture format {fmt} not supported"); return null; }

            byte[] data = null;
            var sd = t["m_StreamData"];
            var streamPath = sd.IsDummy ? "" : sd["path"].AsString;
            if (!string.IsNullOrEmpty(streamPath))
            {
                long offset = (long)sd["offset"].AsULong;
                data = ReadStream(streamPath, offset, size0, dataDir, bun);
            }
            else
            {
                var all = t["image data"].AsByteArray;
                if (all != null && all.Length >= size0) { data = new byte[size0]; Buffer.BlockCopy(all, 0, data, 0, size0); }
            }
            if (data == null) { Report.Add($"{name}: no pixel data"); return null; }
            return new RawImage { Name = name, Source = file, Raw = data, Format = fmt, Width = w, Height = h, RectX = 0, RectY = 0, RectW = w, RectH = h };
        }

        static byte[] ReadStream(string streamPath, long offset, int size, string dataDir, BundleFileInstance bun)
        {
            if (streamPath.StartsWith("archive:/") && bun != null)
            {
                var entry = Path.GetFileName(streamPath);
                var dirs = bun.file.BlockAndDirInfo.DirectoryInfos;
                for (int i = 0; i < dirs.Count; i++)
                {
                    if (dirs[i].Name != entry) continue;
                    var reader = bun.file.DataReader;
                    reader.Position = dirs[i].Offset + offset;
                    return reader.ReadBytes(size);
                }
                return null;
            }
            var file = Path.Combine(dataDir, Path.GetFileName(streamPath));
            if (!File.Exists(file)) return null;
            using (var fs = File.OpenRead(file))
            {
                fs.Position = offset;
                var buf = new byte[size];
                int read = 0;
                while (read < size) { int n = fs.Read(buf, read, size - read); if (n <= 0) return null; read += n; }
                return buf;
            }
        }

        /// Bytes in the first mip of an uncompressed or BCn texture (UnityEngine.TextureFormat values).
        public static int Mip0Size(int fmt, int w, int h)
        {
            int blocks = ((w + 3) / 4) * ((h + 3) / 4);
            switch (fmt)
            {
                case 1: case 63: return w * h;                 // Alpha8, R8
                case 2: case 7: case 13: case 9: return w * h * 2; // ARGB4444, RGB565, RGBA4444, R16
                case 3: return w * h * 3;                      // RGB24
                case 4: case 5: case 14: return w * h * 4;     // RGBA32, ARGB32, BGRA32
                case 10: case 26: return blocks * 8;           // DXT1, BC4
                case 12: case 27: case 25: case 24: return blocks * 16; // DXT5, BC5, BC7, BC6H
                case 17: return w * h * 8;                     // RGBAHalf
                case 20: return w * h * 16;                    // RGBAFloat
                default: return -1;                            // crunched, mobile and other formats
            }
        }
    }
}
