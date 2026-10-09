using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.TurnipDash;

// A sprite frame read from one of Geometry Dash's cocos2d sprite sheets (.plist + .png).
public sealed class Frame {
    public MTexture Tex; public bool Rotated; public Vector2 Size;
    // Draw centred so the frame's longest side is `px` pixels.
    public void Draw(Vector2 at, float px, Color color, float rotation = 0f, bool flipX = false) {
        float scale = px / Math.Max(Size.X, Size.Y);
        Tex.DrawCentered(at, color, scale, rotation - (Rotated ? MathHelper.PiOver2 : 0f),
            flipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
    }
    public void DrawFit(Rectangle box, Color color) => Draw(new Vector2(box.Center.X, box.Center.Y), Math.Max(box.Width, box.Height), color);
}

public static class GDContent {
    public static string Root, Resources, Suffix;
    static readonly Dictionary<string, (string sheet, Rectangle rect, bool rot, Vector2 size)> frames = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, MTexture> sheets = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Frame> cache = new();
    public static readonly List<string> Missing = new();
    public static GDFont Font;
    public static bool Found => Resources != null;

    public static void Load() {
        if (Root != null || Resources != null) return;
        Root = GameFinder.Find(Sheets.GameGeometryDash, TurnipDashModule.Settings.GeometryDashFolder);
        if (Root == null) { Missing.Add("Geometry Dash isn't installed (Steam app 322170), so its music, blocks and icon can't come into Celeste."); return; }
        string res = Path.Combine(Root, Sheets.GameGeometryDash.ContentRoot);
        if (!Directory.Exists(res)) { Missing.Add($"Found Geometry Dash at {Root}, but it has no Resources folder."); Root = null; return; }
        Resources = res;
        Suffix = File.Exists(Path.Combine(res, "GJ_GameSheet-uhd.plist")) ? "-uhd" : File.Exists(Path.Combine(res, "GJ_GameSheet-hd.plist")) ? "-hd" : "";
        var plists = Directory.GetFiles(res, "*" + Suffix + ".plist").Concat(
            Directory.Exists(Path.Combine(res, "icons")) ? new[] { "player_01", "ship_01" }.Select(n => Path.Combine(res, "icons", n + Suffix + ".plist")).Where(File.Exists) : Enumerable.Empty<string>());
        foreach (string p in plists) {
            if (Suffix == "" && (p.EndsWith("-hd.plist") || p.EndsWith("-uhd.plist"))) continue;
            try { ReadPlist(p); } catch (Exception e) { TurnipDashModule.Log($"GD plist {p}: {e.Message}"); }
        }
        TurnipDashModule.Log($"Geometry Dash at {Root}: {frames.Count} frames ({Suffix})");
        foreach (string f in Sheets.GdFont.Lookup) {
            string fp = Path.Combine(res, f);
            if (File.Exists(fp)) { try { Font = GDFont.Read(fp); break; } catch (Exception e) { TurnipDashModule.Log($"GD font: {e.Message}"); } }
        }
        if (Font == null) Missing.Add("Geometry Dash's big font wasn't found; using Celeste's font for titles.");
    }

    static Vector2 V(string s) { var m = Regex.Matches(s, "-?[0-9.]+"); return new Vector2(F(m[0].Value), F(m[1].Value)); }
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    static void ReadPlist(string path) {
        var doc = new XmlDocument { XmlResolver = null };
        using (var r = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null })) doc.Load(r);
        XmlNode root = doc.SelectSingleNode("/plist/dict");
        XmlNode framesDict = Value(root, "frames");
        string tex = Value(Value(root, "metadata"), "textureFileName")?.InnerText ?? Path.GetFileNameWithoutExtension(path) + ".png";
        string sheet = Path.Combine(Path.GetDirectoryName(path), tex);
        for (XmlNode k = framesDict.FirstChild; k != null; k = k.NextSibling) {
            if (k.Name != "key") continue;
            XmlNode d = k.NextSibling;
            string rect = (Value(d, "textureRect") ?? Value(d, "frame"))?.InnerText;
            if (rect == null) continue;
            var m = Regex.Matches(rect, "-?[0-9]+");
            bool rot = (Value(d, "textureRotated") ?? Value(d, "rotated"))?.Name == "true";
            var size = (Value(d, "spriteSourceSize") ?? Value(d, "sourceSize")) is XmlNode ss ? V(ss.InnerText) : new Vector2(int.Parse(m[2].Value), int.Parse(m[3].Value));
            int x = int.Parse(m[0].Value), y = int.Parse(m[1].Value), w = int.Parse(m[2].Value), h = int.Parse(m[3].Value);
            frames[k.InnerText] = (sheet, rot ? new Rectangle(x, y, h, w) : new Rectangle(x, y, w, h), rot, new Vector2(w, h));
        }
    }

    static XmlNode Value(XmlNode dict, string key) {
        if (dict == null) return null;
        for (XmlNode k = dict.FirstChild; k != null; k = k.NextSibling)
            if (k.Name == "key" && k.InnerText == key) return k.NextSibling;
        return null;
    }

    // Look a sheet row up: exact frame names first, then (for exact-then-prefix) the first frame starting with a prefix.
    public static Frame Get(ContentRow row) {
        if (cache.TryGetValue(row.Id, out var f)) return f;
        Load();
        string name = row.Lookup.FirstOrDefault(frames.ContainsKey);
        if (name == null && row.Pick == "exact-then-prefix")
            name = row.Lookup.Where(p => p.EndsWith("_")).Select(p => frames.Keys.Where(k => k.StartsWith(p, StringComparison.OrdinalIgnoreCase)).OrderBy(k => k).FirstOrDefault()).FirstOrDefault(n => n != null);
        if (name != null) {
            var e = frames[name];
            var sheet = Sheet(e.sheet);
            if (sheet != null) f = new Frame { Tex = new MTexture(sheet, e.rect.X, e.rect.Y, e.rect.Width, e.rect.Height), Rotated = e.rot, Size = e.size };
        }
        if (f == null && Found) Missing.Add($"Geometry Dash frame {row.Lookup[0]} not found ({row.WhenMissing}).");
        return cache[row.Id] = f;
    }

    public static MTexture Sheet(string path) {
        if (sheets.TryGetValue(path, out var t)) return t;
        try { t = Textures.FromFile(path); } catch (Exception e) { TurnipDashModule.Log($"GD sheet {path}: {e.Message}"); }
        return sheets[path] = t;
    }

    public static string Song(ContentRow row) {
        Load();
        if (!Found) return null;
        foreach (string f in row.Lookup) {
            foreach (string cand in new[] { f, Path.ChangeExtension(f, ".ogg") }) {
                string p = Path.Combine(Resources, cand);
                if (File.Exists(p)) return p;
            }
        }
        Missing.Add($"Geometry Dash song {row.Lookup[0]} not found ({row.WhenMissing}).");
        return null;
    }
}

// Geometry Dash's bitmap fonts (AngelCode BMFont text format, e.g. bigFont-uhd.fnt).
public sealed class GDFont {
    readonly Dictionary<int, (MTexture tex, int xo, int yo, int adv)> glyphs = new();
    public float LineHeight;

    public static GDFont Read(string path) {
        var font = new GDFont(); MTexture page = null;
        foreach (string line in File.ReadAllLines(path)) {
            var kv = Regex.Matches(line, "(\\w+)=(\"[^\"]*\"|\\S+)").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim('"'));
            if (line.StartsWith("common ")) font.LineHeight = int.Parse(kv["lineHeight"]);
            else if (line.StartsWith("page ")) page = Textures.FromFile(Path.Combine(Path.GetDirectoryName(path), kv["file"]));
            else if (line.StartsWith("char ") && page != null) {
                int w = int.Parse(kv["width"]), h = int.Parse(kv["height"]);
                if (w > 0 && h > 0)
                    font.glyphs[int.Parse(kv["id"])] = (new MTexture(page, int.Parse(kv["x"]), int.Parse(kv["y"]), w, h), int.Parse(kv["xoffset"]), int.Parse(kv["yoffset"]), int.Parse(kv["xadvance"]));
                else font.glyphs[int.Parse(kv["id"])] = (null, 0, 0, int.Parse(kv["xadvance"]));
            }
        }
        if (page == null || font.glyphs.Count == 0) throw new InvalidDataException("no glyphs");
        return font;
    }

    public float Measure(string s) => s.Sum(c => glyphs.TryGetValue(c, out var g) ? g.adv : LineHeight / 2);

    // Draws text centred on `at` with the given pixel height (HUD space).
    public void Draw(string s, Vector2 at, float height, Color color) {
        float scale = height / LineHeight, x = at.X - Measure(s) * scale / 2, y = at.Y - height / 2;
        foreach (char c in s) {
            if (!glyphs.TryGetValue(c, out var g)) { x += LineHeight / 2 * scale; continue; }
            g.tex?.Draw(new Vector2(x + g.xo * scale, y + g.yo * scale), Vector2.Zero, color, scale);
            x += g.adv * scale;
        }
    }
}

// PNG / raw pixel data -> Monocle textures.
public static class Textures {
    public static MTexture FromFile(string path) { using var s = File.OpenRead(path); return FromStream(s, path); }
    public static MTexture FromStream(Stream s, string name) {
        var tex = Texture2D.FromStream(Engine.Graphics.GraphicsDevice, s);
        var vt = VirtualContent.CreateTexture("TurnipDash:" + name, tex.Width, tex.Height, Color.White);
        vt.Texture?.Dispose(); vt.Texture = tex;
        return new MTexture(vt);
    }
    public static MTexture FromRgba(byte[] rgba, int w, int h, string name) {
        var tex = new Texture2D(Engine.Graphics.GraphicsDevice, w, h);
        tex.SetData(rgba);
        var vt = VirtualContent.CreateTexture("TurnipDash:" + name, w, h, Color.White);
        vt.Texture?.Dispose(); vt.Texture = tex;
        return new MTexture(vt);
    }
}
