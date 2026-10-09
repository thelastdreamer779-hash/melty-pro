using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace TurnipDash.Content
{
    /// Reads Apple/cocos2d plists, including Geometry Dash's short-tag form (d/k/s/i/r/t/f/a).
    public static class Plist
    {
        public static object Parse(string xml)
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(xml);
            var root = doc.DocumentElement;
            foreach (XmlNode n in root.ChildNodes)
                if (n.NodeType == XmlNodeType.Element) return Value((XmlElement)n);
            return null;
        }

        static object Value(XmlElement e)
        {
            switch (e.Name)
            {
                case "dict": case "d":
                    var d = new Dictionary<string, object>();
                    string key = null;
                    foreach (XmlNode n in e.ChildNodes)
                    {
                        if (n.NodeType != XmlNodeType.Element) continue;
                        var c = (XmlElement)n;
                        if (c.Name == "key" || c.Name == "k") key = c.InnerText;
                        else if (key != null) { d[key] = Value(c); key = null; }
                    }
                    return d;
                case "array": case "a":
                    var l = new List<object>();
                    foreach (XmlNode n in e.ChildNodes) if (n.NodeType == XmlNodeType.Element) l.Add(Value((XmlElement)n));
                    return l;
                case "integer": case "i": return long.Parse(e.InnerText.Trim(), CultureInfo.InvariantCulture);
                case "real": case "r": return double.Parse(e.InnerText.Trim(), CultureInfo.InvariantCulture);
                case "true": case "t": return true;
                case "false": case "f": return false;
                default: return e.InnerText;
            }
        }
    }
}
