// Checks the bundled class database knows the Texture2D and Sprite fields TmImages reads, across Unity versions.
using System;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

int fails = 0;
var am = new AssetsManager();
am.LoadClassPackage("../../third_party/classdata.tpk");
foreach (var ver in new[] { "2019.4.40f1", "2020.3.48f1", "2021.3.33f1", "2022.3.20f1", "6000.0.30f1" })
{
    var db = am.LoadClassDatabaseFromPackage(ver);
    foreach (var (cls, fields) in new[] {
        ("Texture2D", new[] { "m_Name", "m_Width", "m_Height", "m_TextureFormat", "image data", "m_StreamData" }),
        ("Sprite", new[] { "m_Name", "m_RD" }) })
    {
        var type = db.FindAssetClassByName(cls);
        var tmpl = new AssetTypeTemplateField();
        tmpl.FromClassDatabase(db, type);
        var names = tmpl.Children.Select(c => c.Name).ToList();
        var missing = fields.Where(f => !names.Contains(f)).ToList();
        if (cls == "Sprite")
        {
            var rd = tmpl.Children.First(c => c.Name == "m_RD");
            foreach (var f in new[] { "texture", "textureRect" }) if (!rd.Children.Any(c => c.Name == f)) missing.Add("m_RD." + f);
        }
        if (cls == "Texture2D")
        {
            var sd = tmpl.Children.First(c => c.Name == "m_StreamData");
            foreach (var f in new[] { "offset", "size", "path" }) if (!sd.Children.Any(c => c.Name == f)) missing.Add("m_StreamData." + f);
        }
        Console.WriteLine($"{(missing.Count == 0 ? "PASS" : "FAIL")} {ver} {cls} {(missing.Count == 0 ? "" : string.Join(",", missing))}");
        if (missing.Count > 0) fails++;
    }
}
Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
return fails;
