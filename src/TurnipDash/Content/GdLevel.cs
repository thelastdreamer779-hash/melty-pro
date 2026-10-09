using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TurnipDash.Content
{
    public struct GdObject
    {
        public int Id;
        public float X, Y;      // centre, in GD blocks (30 units)
        public float Rotation;  // degrees, clockwise as in GD
        public bool FlipY;
    }

    /// Geometry Dash level strings: "header;1,id,2,x,3,y,...;..." stored as base64url(gzip) or base64url(zlib).
    public static class GdLevel
    {
        public static string Decode(string stored)
        {
            stored = stored.Trim();
            if (stored.IndexOf(';') >= 0 && stored.IndexOf(',') >= 0 && !stored.StartsWith("H4sI")) return stored;
            var b64 = stored.Replace('-', '+').Replace('_', '/');
            while (b64.Length % 4 != 0) b64 += "=";
            var raw = Convert.FromBase64String(b64);
            Stream s = new MemoryStream(raw);
            if (raw.Length > 2 && raw[0] == 0x1f && raw[1] == 0x8b) s = new GZipStream(s, CompressionMode.Decompress);
            else { s.Position = 2; s = new DeflateStream(s, CompressionMode.Decompress); } // zlib: skip the 2-byte header
            using (var r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd();
        }

        public static List<GdObject> Objects(string level)
        {
            var list = new List<GdObject>();
            var parts = level.Split(';');
            for (int i = 1; i < parts.Length; i++) // parts[0] is the level header
            {
                var kv = parts[i].Split(',');
                if (kv.Length < 2) continue;
                var o = new GdObject();
                bool hasId = false;
                for (int k = 0; k + 1 < kv.Length; k += 2)
                {
                    switch (kv[k])
                    {
                        case "1": hasId = int.TryParse(kv[k + 1], out o.Id); break;
                        case "2": o.X = F(kv[k + 1]) / 30f; break;
                        case "3": o.Y = F(kv[k + 1]) / 30f; break;
                        case "5": o.FlipY = kv[k + 1] == "1"; break;
                        case "6": o.Rotation = F(kv[k + 1]); break;
                    }
                }
                if (hasId) list.Add(o);
            }
            list.Sort((a, b) => a.X.CompareTo(b.X));
            return list;
        }

        static float F(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

        /// Pulls a main level's stored string out of Resources/LevelData.plist.
        public static string FromLevelData(object plist, string levelKey)
        {
            var d = plist as Dictionary<string, object>;
            if (d == null) return null;
            if (d.TryGetValue(levelKey, out var v))
            {
                if (v is string s) return s;
                if (v is Dictionary<string, object> lvl && lvl.TryGetValue("k4", out var k4)) return k4 as string;
            }
            foreach (var child in d.Values) // nested ("LLM_01" style) layouts
            {
                var found = FromLevelData(child, levelKey);
                if (found != null) return found;
            }
            return null;
        }
    }
}
