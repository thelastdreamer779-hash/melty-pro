using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NLayer;
using TurnipDash.Generated;
using UnityEngine;

namespace TurnipDash.Content
{
    /// Loads every row of the content sheet from the player's installed games. Slow file work runs
    /// on a worker thread; Unity objects are made on the main thread in Finish().
    public sealed class ContentStore
    {
        public string GdDir, TmDir;
        public readonly List<string> Problems = new List<string>();
        public readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        public List<GdObject> Level;
        public AudioClip Music;
        public bool Done { get; private set; }

        // worker-thread results
        float[] pcm; int channels, rate;
        byte[] sheetPng; Dictionary<string, AtlasFrame> frames;
        Dictionary<string, RawImage> tmImages;

        public static ContentRow Row(string id) => Sheets.Content.First(r => r.Id == id);

        public void Begin()
        {
            GdDir = GameLocator.Find("gd");
            TmDir = GameLocator.Find("tm");
            TurnipDashMod.Info($"Geometry Dash folder: {GdDir ?? "not found"}; Turnip Mountain folder: {TmDir ?? "not found"}");
            if (GdDir == null) Problems.Add(Text.Get("ui_gd_missing"));
            if (TmDir == null) Problems.Add(Text.Get("ui_tm_missing"));
            if (Problems.Count > 0) { Done = true; return; }
            var tpk = typeof(ContentStore).Assembly.GetManifestResourceStream("TurnipDash.classdata.tpk");
            new Thread(() => { try { Work(tpk); } catch (Exception e) { lock (Problems) Problems.Add("Loading failed: " + e.Message); } Done = true; }) { IsBackground = true }.Start();
        }

        string GdPath(string rel) => Path.Combine(GdDir, rel.Replace('/', Path.DirectorySeparatorChar));

        void Work(Stream tpk)
        {
            foreach (var row in Sheets.Content)
            {
                if (row.Game != "gd") continue;
                try
                {
                    switch (row.Kind)
                    {
                        case "plistLevel":
                            var stored = GdLevel.FromLevelData(Plist.Parse(File.ReadAllText(GdPath(row.Path))), row.Match);
                            if (stored == null) throw new Exception($"level {row.Match} not in {row.Path}");
                            Level = GdLevel.Objects(GdLevel.Decode(stored));
                            TurnipDashMod.Info($"{row.Id}: {Level.Count} objects");
                            break;
                        case "mp3":
                            using (var mp3 = new MpegFile(GdPath(row.Path)))
                            {
                                channels = mp3.Channels; rate = mp3.SampleRate;
                                var all = new List<float>(rate * channels * 120);
                                var buf = new float[rate * channels];
                                int n;
                                while ((n = mp3.ReadSamples(buf, 0, buf.Length)) > 0) for (int i = 0; i < n; i++) all.Add(buf[i]);
                                pcm = all.ToArray();
                            }
                            break;
                        case "atlasSheet":
                            frames = Atlas.Frames(Plist.Parse(File.ReadAllText(GdPath(row.Path + ".plist"))));
                            sheetPng = File.ReadAllBytes(GdPath(row.Path + ".png"));
                            TurnipDashMod.Info($"{row.Id}: {frames.Count} frames");
                            break;
                    }
                }
                catch (Exception e) { if (row.Required) lock (Problems) Problems.Add($"Geometry Dash: {row.Id}: {e.Message}"); TurnipDashMod.Warn($"{row.Id}: {e.Message}"); }
            }

            var tmRows = Sheets.Content.Where(r => r.Game == "tm" && r.Kind == "unityImage").ToList();
            tmImages = TmImages.Find(TmDir, tmRows.Select(r => r.Match.ToLowerInvariant()).ToList(), tpk);
            foreach (var line in TmImages.Report) TurnipDashMod.Info("Turnip Mountain " + line);
        }

        /// Main thread: build sprites and the music clip.
        public void Finish()
        {
            if (pcm != null)
            {
                Music = AudioClip.Create("StereoMadness", pcm.Length / channels, channels, rate, false);
                Music.SetData(pcm, 0);
                pcm = null;
            }
            if (sheetPng != null)
            {
                var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                sheet.LoadImage(sheetPng);
                foreach (var row in Sheets.Content.Where(r => r.Kind == "atlasFrame"))
                {
                    if (frames.TryGetValue(row.Match, out var f))
                    {
                        // plist rects are top-left based; Unity's are bottom-left
                        var rect = new Rect(f.X, sheet.height - f.Y - f.H, f.W, f.H);
                        var s = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), f.Rotated ? f.W : f.H);
                        Sprites[row.Id] = s;
                        if (f.Rotated) Rotated.Add(row.Id);
                    }
                    else TurnipDashMod.Warn($"{row.Id}: frame {row.Match} not in the sheet");
                }
                sheetPng = null;
            }
            foreach (var row in Sheets.Content.Where(r => r.Game == "tm"))
            {
                if (tmImages != null && tmImages.TryGetValue(row.Match.ToLowerInvariant(), out var img))
                {
                    var s = MakeSprite(img);
                    if (s != null) { Sprites[row.Id] = s; TurnipDashMod.Info($"{row.Id}: using '{img.Name}' from {img.Source}"); continue; }
                }
                if (row.Required) TmArtMissing = true;
                TurnipDashMod.Warn($"{row.Id}: nothing named like '{row.Match}' could be read from Turnip Mountain");
            }
        }

        public readonly HashSet<string> Rotated = new HashSet<string>();
        public bool TmArtMissing;

        static Sprite MakeSprite(RawImage img)
        {
            Texture2D tex;
            if (img.Png != null)
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(img.Png)) return null;
                img.RectW = tex.width; img.RectH = tex.height;
            }
            else
            {
                var fmt = (TextureFormat)img.Format;
                if (!SystemInfo.SupportsTextureFormat(fmt)) return null;
                tex = new Texture2D(img.Width, img.Height, fmt, false);
                try { tex.LoadRawTextureData(img.Raw); tex.Apply(false, false); }
                catch (Exception e) { TurnipDashMod.Warn($"{img.Name}: {e.Message}"); return null; }
            }
            tex.filterMode = FilterMode.Bilinear;
            var size = Mathf.Max(img.RectW, img.RectH);
            return Sprite.Create(tex, new Rect(img.RectX, img.RectY, img.RectW, img.RectH), new Vector2(0.5f, 0.5f), size);
        }
    }
}
