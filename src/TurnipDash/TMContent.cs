using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Monocle;

namespace Celeste.Mod.TurnipDash;

// Reads Turnip Mountain's own sprites and audio from the player's install. Its engine isn't documented, so the
// reader detects it from the files: Unity (AssetsTools.NET), Godot (.pck), GameMaker (data.win), else loose files.
public static class TMContent {
    public sealed class Candidate {
        public string Name, Kind; // "sprite" | "audio"
        public long Size; public double Seconds;
        public Func<MTexture> Texture; public Func<byte[]> Bytes;
    }
    public static string Root, Engine = "not found";
    public static readonly List<Candidate> All = new();
    public static readonly List<string> Missing = new();
    static readonly Dictionary<string, object> picked = new();
    static bool loaded;
    public static bool Found => Root != null;

    public static void Load() {
        if (loaded) return; loaded = true;
        Root = GameFinder.Find(Sheets.GameTurnipMountain, TurnipDashModule.Settings.TurnipMountainFolder);
        if (Root == null) { Missing.Add("Turnip Mountain isn't installed (Steam app 2138410), so its turnip, hands and music can't come into Celeste."); return; }
        try {
            string unityData = Directory.GetDirectories(Root, "*_Data").FirstOrDefault(d => File.Exists(Path.Combine(d, "globalgamemanagers")) || File.Exists(Path.Combine(d, "data.unity3d")));
            string pck = Directory.GetFiles(Root, "*.pck").FirstOrDefault() ?? Directory.GetFiles(Root, "*.exe").FirstOrDefault(Godot.HasEmbeddedPck);
            string win = new[] { "data.win", "game.unx", "game.ios" }.Select(f => Path.Combine(Root, f)).FirstOrDefault(File.Exists);
            if (unityData != null) { Engine = "Unity"; Unity.Scan(unityData); }
            else if (pck != null) { Engine = "Godot"; Godot.Scan(pck); }
            else if (win != null) { Engine = "GameMaker"; GameMaker.Scan(win); }
            else Engine = "unknown";
        } catch (Exception e) { TurnipDashModule.Log($"Turnip Mountain ({Engine}) scan failed: {e}"); }
        Loose.Scan(Root);
        TurnipDashModule.Log($"Turnip Mountain at {Root}: engine {Engine}, {All.Count(c => c.Kind == "sprite")} sprites, {All.Count(c => c.Kind == "audio")} sounds");
        if (TurnipDashModule.Settings.ShowContentReport)
            foreach (var c in All.OrderBy(c => c.Kind).ThenBy(c => c.Name)) TurnipDashModule.Log($"  TM {c.Kind}: {c.Name} ({c.Size} B, {c.Seconds:0.0}s)");
    }

    static int Score(string name, string[] words) {
        string n = name.ToLowerInvariant(); int s = 0;
        for (int i = 0; i < words.Length; i++) if (n.Contains(words[i])) s += (words.Length - i) * 10;
        if (s > 0 && (n.Contains("icon") || n.Contains("button") || n.Contains("ui_") || n.Contains("logo"))) s -= 5;
        return s;
    }

    static Candidate Pick(ContentRow row, string kind) {
        var pool = All.Where(c => c.Kind == kind).Select(c => (c, s: Score(c.Name, row.Lookup))).ToList();
        var hits = pool.Where(p => p.s > 0).ToList();
        if (hits.Count == 0 && row.Pick == "best-name-longest") hits = pool; // still the game's real music, just unnamed
        if (hits.Count == 0) return null;
        return row.Pick switch {
            "best-name-longest" => hits.OrderByDescending(p => p.s).ThenByDescending(p => p.c.Seconds > 0 ? p.c.Seconds : p.c.Size).First().c,
            "best-name-shortest" => hits.OrderByDescending(p => p.s).ThenBy(p => p.c.Size).First().c,
            _ => hits.OrderByDescending(p => p.s).ThenByDescending(p => p.c.Size).First().c,
        };
    }

    public static MTexture Sprite(ContentRow row) {
        if (picked.TryGetValue(row.Id, out var o)) return o as MTexture;
        Load(); MTexture t = null;
        var c = Pick(row, "sprite");
        if (c != null) { try { t = c.Texture(); TurnipDashModule.Log($"{row.Id} <- Turnip Mountain {c.Name}"); } catch (Exception e) { TurnipDashModule.Log($"{row.Id}: {c.Name} failed: {e.Message}"); } }
        if (t == null && Found) Missing.Add($"Couldn't find Turnip Mountain's {row.Lookup[0]} in its {Engine} files ({row.WhenMissing}).");
        picked[row.Id] = t; return t;
    }

    public static byte[] Audio(ContentRow row) {
        if (picked.TryGetValue(row.Id, out var o)) return o as byte[];
        Load(); byte[] b = null;
        var c = Pick(row, "audio");
        if (c != null) { try { b = c.Bytes(); TurnipDashModule.Log($"{row.Id} <- Turnip Mountain {c.Name}"); } catch (Exception e) { TurnipDashModule.Log($"{row.Id}: {c.Name} failed: {e.Message}"); } }
        if (b == null && Found) Missing.Add($"Couldn't find Turnip Mountain's {row.Lookup[0]} sound in its {Engine} files ({row.WhenMissing}).");
        picked[row.Id] = b; return b;
    }

    static void Add(string name, string kind, long size, Func<MTexture> tex = null, Func<byte[]> bytes = null, double seconds = 0)
        => All.Add(new Candidate { Name = name, Kind = kind, Size = size, Texture = tex, Bytes = bytes, Seconds = seconds });

    static byte[] Slice(string path, long offset, long length) {
        using var f = File.OpenRead(path); f.Position = offset;
        var b = new byte[length]; f.ReadExactly(b); return b;
    }

    // ---------------- Unity ----------------
    static class Unity {
        static AssetsManager am;
        public static void Scan(string dataDir) {
            am = new AssetsManager();
            if (!Everest.Content.TryGet("TurnipDash/classdata.tpk", out ModAsset tpk) && !Everest.Content.TryGet("TurnipDash/classdata", out tpk)) throw new FileNotFoundException("classdata.tpk missing from the mod");
            using (var s = tpk.Stream) { var ms = new MemoryStream(); s.CopyTo(ms); ms.Position = 0; am.LoadClassPackage(ms); }
            var files = Directory.GetFiles(dataDir).Where(f => f.EndsWith(".assets") || Path.GetFileName(f).StartsWith("level") && !Path.HasExtension(f) || Path.GetFileName(f) == "globalgamemanagers");
            foreach (string f in files) { try { ScanFile(am.LoadAssetsFile(f, false)); } catch (Exception e) { TurnipDashModule.Log($"TM unity {f}: {e.Message}"); } }
            string sa = Path.Combine(dataDir, "StreamingAssets");
            if (Directory.Exists(sa))
                foreach (string b in Directory.EnumerateFiles(sa, "*.bundle", SearchOption.AllDirectories)) {
                    try {
                        var bun = am.LoadBundleFile(b, true);
                        for (int i = 0; i < bun.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
                            if (bun.file.IsAssetsFile(i)) ScanFile(am.LoadAssetsFileFromBundle(bun, i, false));
                    } catch (Exception e) { TurnipDashModule.Log($"TM bundle {b}: {e.Message}"); }
                }
        }

        static void ScanFile(AssetsFileInstance inst) {
            var cldb = am.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);
            foreach (var info in inst.file.GetAssetsOfType(AssetClassID.Sprite)) {
                var i = info; string n = AssetHelper.GetAssetNameFast(inst.file, cldb, i);
                Add(n, "sprite", 0, () => SpriteTex(inst, i));
            }
            foreach (var info in inst.file.GetAssetsOfType(AssetClassID.Texture2D)) {
                var i = info; string n = AssetHelper.GetAssetNameFast(inst.file, cldb, i);
                Add(n, "sprite", i.ByteSize, () => { var (px, w, h) = Decode(inst, am.GetBaseField(inst, i)); return Textures.FromRgba(px, w, h, n); });
            }
            foreach (var info in inst.file.GetAssetsOfType(AssetClassID.AudioClip)) {
                var bf = am.GetBaseField(inst, info);
                string n = bf["m_Name"].AsString; var res = bf["m_Resource"];
                if (res.IsDummy) continue;
                string src = res["m_Source"].AsString; long off = (long) res["m_Offset"].AsULong, size = (long) res["m_Size"].AsULong;
                double secs = bf["m_Length"].IsDummy ? 0 : bf["m_Length"].AsFloat;
                Add(n, "audio", size, bytes: () => ResourceBytes(inst, src, off, size), seconds: secs);
            }
        }

        // Unity stores textures bottom-up; returns top-down RGBA.
        static (byte[] px, int w, int h) Decode(AssetsFileInstance inst, AssetTypeValueField bf) {
            var tf = TextureFile.ReadTextureFile(bf);
            byte[] enc = tf.FillPictureData(inst);
            if (enc == null || enc.Length == 0) enc = tf.SetPictureDataFromBundle(inst.parentBundle) ? tf.pictureData : null;
            byte[] rgba = tf.DecodeTextureRaw(enc, false);
            int w = tf.m_Width, h = tf.m_Height, stride = w * 4;
            var flipped = new byte[rgba.Length];
            for (int y = 0; y < h; y++) Buffer.BlockCopy(rgba, y * stride, flipped, (h - 1 - y) * stride, stride);
            return (flipped, w, h);
        }

        static MTexture SpriteTex(AssetsFileInstance inst, AssetFileInfo info) {
            var bf = am.GetBaseField(inst, info);
            var ext = am.GetExtAsset(inst, bf["m_RD"]["texture"]);
            var (px, w, h) = Decode(ext.file, ext.baseField);
            var r = bf["m_Rect"];
            int rx = (int) r["x"].AsFloat, rw = (int) r["width"].AsFloat, rh = (int) r["height"].AsFloat;
            int ry = h - (int) r["y"].AsFloat - rh; // Unity rect origin is bottom-left
            var crop = new byte[rw * rh * 4];
            for (int y = 0; y < rh; y++) Buffer.BlockCopy(px, ((ry + y) * w + rx) * 4, crop, y * rw * 4, rw * 4);
            return Textures.FromRgba(crop, rw, rh, bf["m_Name"].AsString);
        }

        static byte[] ResourceBytes(AssetsFileInstance inst, string src, long off, long size) {
            if (inst.parentBundle != null) {
                var bun = inst.parentBundle.file; int idx = bun.GetFileIndex(Path.GetFileName(src));
                bun.GetFileRange(idx, out long o, out _);
                var r = bun.DataReader; lock (r) { r.Position = o + off; return r.ReadBytes((int) size); }
            }
            return Slice(Path.Combine(Path.GetDirectoryName(inst.path), Path.GetFileName(src)), off, size);
        }
    }

    // ---------------- Godot ----------------
    static class Godot {
        public static bool HasEmbeddedPck(string exe) {
            try { using var f = File.OpenRead(exe); f.Position = f.Length - 4; var b = new byte[4]; f.ReadExactly(b); return Encoding.ASCII.GetString(b) == "GDPC"; } catch { return false; }
        }
        public static void Scan(string file) {
            using var f = File.OpenRead(file); var r = new BinaryReader(f);
            long start = 0;
            if (!file.EndsWith(".pck")) { f.Position = f.Length - 12; long size = r.ReadInt64(); start = f.Length - 12 - size; }
            f.Position = start;
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "GDPC") throw new InvalidDataException("not a Godot pack");
            uint fmt = r.ReadUInt32(); r.ReadUInt32(); r.ReadUInt32(); r.ReadUInt32();
            long fileBase = 0; uint flags = 0;
            if (fmt >= 2) { flags = r.ReadUInt32(); fileBase = r.ReadInt64(); }
            if (fmt >= 3) { long dir = r.ReadInt64(); for (int i = 0; i < 16; i++) r.ReadUInt32(); f.Position = start + dir; }
            else for (int i = 0; i < 16; i++) r.ReadUInt32();
            if ((flags & 1) != 0) throw new InvalidDataException("encrypted pack");
            uint count = r.ReadUInt32();
            for (uint i = 0; i < count; i++) {
                string path = Encoding.UTF8.GetString(r.ReadBytes((int) r.ReadUInt32())).TrimEnd('\0');
                long off = r.ReadInt64(), size = r.ReadInt64(); r.ReadBytes(16);
                if (fmt >= 2) r.ReadUInt32();
                long abs = (fmt >= 2 ? start + fileBase : start) + off;
                string name = Path.GetFileName(path), ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext is ".png" or ".ctex" or ".stex")
                    Add(name, "sprite", size, () => { var png = Carve(Slice(file, abs, size), Png); return png == null ? null : Textures.FromStream(new MemoryStream(png), name); });
                else if (ext is ".ogg" or ".oggstr" or ".mp3" or ".mp3str" or ".wav")
                    Add(name, "audio", size, bytes: () => { var b = Slice(file, abs, size); return Carve(b, "OggS"u8.ToArray()) is byte[] ogg ? ogg : ext.StartsWith(".mp3") || ext == ".wav" ? b : null; });
            }
        }
    }

    // ---------------- GameMaker ----------------
    static class GameMaker {
        public static void Scan(string win) {
            byte[] d = File.ReadAllBytes(win);
            var chunks = new Dictionary<string, (int at, int len)>();
            for (int p = 8; p + 8 <= d.Length;) { string id = Encoding.ASCII.GetString(d, p, 4); int len = BitConverter.ToInt32(d, p + 4); chunks[id] = (p + 8, len); p += 8 + len; }
            string Str(int ptr) { if (ptr <= 4 || ptr >= d.Length) return "?"; int n = BitConverter.ToInt32(d, ptr - 4); return n is > 0 and < 512 ? Encoding.UTF8.GetString(d, ptr, n) : "?"; }
            int I(int p) => BitConverter.ToInt32(d, p);
            var pages = new List<int>();
            if (chunks.TryGetValue("TXTR", out var tx)) {
                for (int i = 0, n = I(tx.at); i < n; i++) {
                    int e = I(tx.at + 4 + i * 4), blob = -1;
                    for (int k = 0; k < 8 && blob < 0; k++) { int v = I(e + k * 4); if (v > 0 && v + 4 < d.Length && (IsAt(d, v, Png) || IsAt(d, v, "fioq"u8.ToArray()) || IsAt(d, v, "2zoq"u8.ToArray()))) blob = v; }
                    pages.Add(blob);
                }
            }
            if (chunks.TryGetValue("SPRT", out var sp)) {
                var pageTex = new Dictionary<int, MTexture>();
                MTexture Page(int i) => pageTex.TryGetValue(i, out var t) ? t : pageTex[i] = PageTexture(d, pages[i], "gm-page" + i);
                for (int i = 0, n = I(sp.at); i < n; i++) {
                    int e = I(sp.at + 4 + i * 4); string name = Str(I(e));
                    int p = e + 14 * 4, cnt = I(p);
                    if (cnt == -1) { int ver = I(p + 4); p += 16; if (ver >= 2) p += 4; if (ver >= 3) p += 4; cnt = I(p); }
                    if (cnt <= 0 || cnt > 4096) continue;
                    int tp = I(p + 4);
                    if (tp <= 0 || tp + 22 > d.Length) continue;
                    int sx = BitConverter.ToUInt16(d, tp), sy = BitConverter.ToUInt16(d, tp + 2), sw = BitConverter.ToUInt16(d, tp + 4), sh = BitConverter.ToUInt16(d, tp + 6), page = BitConverter.ToInt16(d, tp + 20);
                    if (page < 0 || page >= pages.Count) continue;
                    Add(name, "sprite", sw * sh, () => Page(page) is MTexture pt ? new MTexture(pt, sx, sy, sw, sh) : null);
                }
            }
            var audo = new Dictionary<int, List<(int at, int len)>>();
            List<(int, int)> Audo(byte[] bytes) {
                var l = new List<(int, int)>(); int p = 8;
                while (p + 8 <= bytes.Length) { string id = Encoding.ASCII.GetString(bytes, p, 4); int len = BitConverter.ToInt32(bytes, p + 4);
                    if (id == "AUDO") { int n = BitConverter.ToInt32(bytes, p + 8); for (int i = 0; i < n; i++) { int e = BitConverter.ToInt32(bytes, p + 12 + i * 4); l.Add((e + 4, BitConverter.ToInt32(bytes, e))); } }
                    p += 8 + len; }
                return l;
            }
            var groups = new Dictionary<int, byte[]> { [0] = d };
            if (chunks.TryGetValue("SOND", out var so)) {
                for (int i = 0, n = I(so.at); i < n; i++) {
                    int e = I(so.at + 4 + i * 4); string name = Str(I(e)), file = Str(I(e + 12)); int group = I(e + 28), id = I(e + 32);
                    if (id < 0) continue; // streamed: a loose .ogg next to data.win (Loose.Scan finds it)
                    Add(name, "audio", 0, bytes: () => {
                        if (!groups.TryGetValue(group, out var gb)) { string gp = Path.Combine(Path.GetDirectoryName(win), $"audiogroup{group}.dat"); gb = groups[group] = File.Exists(gp) ? File.ReadAllBytes(gp) : null; }
                        if (gb == null) return null;
                        var list = Audo(gb); if (id >= list.Count) return null;
                        var (at, len) = list[id]; return gb.AsSpan(at, len).ToArray();
                    });
                }
            }
        }

        static MTexture PageTexture(byte[] d, int blob, string name) {
            if (blob < 0) return null;
            if (IsAt(d, blob, Png)) { var png = Carve(d.AsSpan(blob).ToArray(), Png); return Textures.FromStream(new MemoryStream(png), name); }
            // GameMaker 2022+: QOI pages, optionally bzip2-compressed ("2zoq")
            int w = BitConverter.ToUInt16(d, blob + 4), h = BitConverter.ToUInt16(d, blob + 6);
            byte[] qoi;
            if (IsAt(d, blob, "2zoq"u8.ToArray())) {
                int bzAt = d.AsSpan(blob + 8, 8).IndexOf("BZh"u8) + blob + 8;
                using var bz = SharpCompress.Compressors.BZip2.BZip2Stream.Create(new MemoryStream(d, bzAt, d.Length - bzAt), SharpCompress.Compressors.CompressionMode.Decompress, false);
                var ms = new MemoryStream(); bz.CopyTo(ms); qoi = ms.ToArray();
                if (!IsAt(qoi, 0, "fioq"u8.ToArray())) qoi = d.AsSpan(blob, 12).ToArray().Concat(qoi).ToArray(); // ops only: give it a header
            } else qoi = d.AsSpan(blob).ToArray();
            return Textures.FromRgba(Qoi.Decode(qoi, w, h), w, h, name);
        }
    }

    // GameMaker's QOI variant ("fioq": magic, u16 w, u16 h, u32 length, then GameMaker's own op set).
    static class Qoi {
        public static byte[] Decode(byte[] q, int w, int h) {
            var px = new byte[w * h * 4]; var idx = new byte[64 * 4];
            int r = 0, g = 0, b = 0, a = 255, run = 0, p = 12, n = w * h;
            for (int i = 0; i < n; i++) {
                if (run > 0) run--;
                else if (p < q.Length) {
                    int b1 = q[p++];
                    if ((b1 & 0xC0) == 0x00) { int k = b1 * 4; r = idx[k]; g = idx[k + 1]; b = idx[k + 2]; a = idx[k + 3]; }
                    else if ((b1 & 0xE0) == 0x40) run = b1 & 0x1F;
                    else if ((b1 & 0xE0) == 0x60) { int b2 = q[p++]; run = (((b1 & 0x1F) << 8) | b2) + 32; }
                    else if ((b1 & 0xC0) == 0x80) { r += ((b1 >> 4) & 3) - 2; g += ((b1 >> 2) & 3) - 2; b += (b1 & 3) - 2; }
                    else if ((b1 & 0xE0) == 0xC0) { int b2 = q[p++]; r += (b1 & 0x1F) - 16; g += (b2 >> 4) - 8; b += (b2 & 0x0F) - 8; }
                    else if ((b1 & 0xF0) == 0xE0) { int b2 = q[p++], b3 = q[p++]; r += (((b1 & 0x0F) << 1) | (b2 >> 7)) - 16; g += ((b2 & 0x7C) >> 2) - 16; b += (((b2 & 0x03) << 3) | ((b3 & 0xE0) >> 5)) - 16; a += (b3 & 0x1F) - 16; }
                    else { if ((b1 & 8) != 0) r = q[p++]; if ((b1 & 4) != 0) g = q[p++]; if ((b1 & 2) != 0) b = q[p++]; if ((b1 & 1) != 0) a = q[p++]; }
                    r &= 255; g &= 255; b &= 255; a &= 255;
                    int k2 = ((r ^ g ^ b ^ a) & 63) * 4;
                    idx[k2] = (byte) r; idx[k2 + 1] = (byte) g; idx[k2 + 2] = (byte) b; idx[k2 + 3] = (byte) a;
                }
                px[i * 4] = (byte) r; px[i * 4 + 1] = (byte) g; px[i * 4 + 2] = (byte) b; px[i * 4 + 3] = (byte) a;
            }
            return px;
        }
    }

    // ---------------- Loose files (any engine) ----------------
    static class Loose {
        public static void Scan(string root) {
            int n = 0;
            foreach (string f in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 5, IgnoreInaccessible = true })) {
                if (++n > 20000) break;
                string ext = Path.GetExtension(f).ToLowerInvariant(), name = Path.GetFileName(f);
                if (ext == ".png") Add(name, "sprite", new FileInfo(f).Length, () => Textures.FromFile(f));
                else if (ext is ".ogg" or ".wav" or ".mp3") Add(name, "audio", new FileInfo(f).Length, bytes: () => File.ReadAllBytes(f));
            }
        }
    }

    static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    static bool IsAt(byte[] d, int at, byte[] magic) { if (at < 0 || at + magic.Length > d.Length) return false; for (int i = 0; i < magic.Length; i++) if (d[at + i] != magic[i]) return false; return true; }
    // Returns the first embedded file starting with `magic` (a PNG is cut at its IEND chunk).
    static byte[] Carve(byte[] d, byte[] magic) {
        int s = d.AsSpan().IndexOf(magic);
        if (s < 0) return null;
        if (magic == Png) { int e = d.AsSpan(s).IndexOf("IEND"u8); return e < 0 ? null : d.AsSpan(s, e + 8).ToArray(); }
        return d.AsSpan(s).ToArray();
    }
}
