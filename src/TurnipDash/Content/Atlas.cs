using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TurnipDash.Content
{
    public struct AtlasFrame { public int X, Y, W, H; public bool Rotated; }

    /// cocos2d/TexturePacker sprite sheet frames (format 2 "frame"/"rotated" and 3 "textureRect"/"textureRotated").
    public static class Atlas
    {
        static readonly Regex Rect = new Regex(@"\{\{\s*(-?\d+)\s*,\s*(-?\d+)\s*\}\s*,\s*\{\s*(-?\d+)\s*,\s*(-?\d+)\s*\}\}");

        public static Dictionary<string, AtlasFrame> Frames(object plist)
        {
            var result = new Dictionary<string, AtlasFrame>();
            var root = plist as Dictionary<string, object>;
            if (root == null || !(root.TryGetValue("frames", out var fo) && fo is Dictionary<string, object> frames)) return result;
            foreach (var kv in frames)
            {
                if (!(kv.Value is Dictionary<string, object> f)) continue;
                var rectStr = (f.TryGetValue("textureRect", out var tr) ? tr : f.TryGetValue("frame", out var fr) ? fr : null) as string;
                if (rectStr == null) continue;
                var m = Rect.Match(rectStr);
                if (!m.Success) continue;
                bool rot = (f.TryGetValue("textureRotated", out var a) && a is bool ab && ab) || (f.TryGetValue("rotated", out var b) && b is bool bb && bb);
                int w = I(m.Groups[3].Value), h = I(m.Groups[4].Value);
                // size is given unrotated; in the sheet a rotated frame occupies h x w
                result[kv.Key] = new AtlasFrame { X = I(m.Groups[1].Value), Y = I(m.Groups[2].Value), W = rot ? h : w, H = rot ? w : h, Rotated = rot };
            }
            return result;
        }

        static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);
    }
}
