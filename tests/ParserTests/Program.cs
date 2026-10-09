using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using TurnipDash;
using TurnipDash.Content;

int fails = 0;
void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

// 1. GD level string round trip: gzip + base64url, as GD stores it
var level = "kS38,1_40_2_125,kA2,0;1,1,2,15,3,15;1,8,2,165,3,15;1,39,2,195,3,15,6,180;1,13,2,300,3,75;";
var ms = new MemoryStream();
using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true)) { var b = Encoding.UTF8.GetBytes(level); gz.Write(b, 0, b.Length); }
var stored = Convert.ToBase64String(ms.ToArray()).Replace('+', '-').Replace('/', '_');
Check(stored.StartsWith("H4sI"), "gzip+base64url string starts like GD's (H4sI)");
var objs = GdLevel.Objects(GdLevel.Decode(stored));
Check(objs.Count == 4, $"4 objects decoded (got {objs.Count})");
Check(objs[0].Id == 1 && Math.Abs(objs[0].X - 0.5f) < 1e-4 && Math.Abs(objs[0].Y - 0.5f) < 1e-4, "block at (15,15) units -> (0.5,0.5) blocks");
Check(objs[2].Rotation == 180, "rotation key 6 read");
// zlib variant
var z = new MemoryStream(); z.WriteByte(0x78); z.WriteByte(0x9c);
using (var d = new DeflateStream(z, CompressionLevel.Optimal, true)) { var b = Encoding.UTF8.GetBytes(level); d.Write(b, 0, b.Length); }
Check(GdLevel.Decode(Convert.ToBase64String(z.ToArray())) == level, "zlib variant decodes");
Check(GdLevel.Decode(level) == level, "plain level string passes through");

// 2. LevelData.plist in GD's short-tag form and the standard form
var shortPlist = $"<?xml version=\"1.0\"?><plist version=\"1.0\" gjver=\"2.0\"><dict><k>1</k><s>{stored}</s><k>2</k><s>x</s></dict></plist>";
Check(GdLevel.FromLevelData(Plist.Parse(shortPlist), "1") == stored, "short-tag plist: level 1 found");
var longPlist = $"<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>root</key><dict><key>1</key><dict><key>k4</key><string>{stored}</string></dict></dict></dict></plist>";
Check(GdLevel.FromLevelData(Plist.Parse(longPlist), "1") == stored, "nested plist with k4 found");

// 3. cocos2d sprite sheet frames (format 3, one rotated)
var sheet = "<?xml version=\"1.0\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict><key>frames</key><dict>" +
  "<key>square_01_001.png</key><dict><key>textureRect</key><string>{{10,20},{120,120}}</string><key>textureRotated</key><false/></dict>" +
  "<key>spike_01_001.png</key><dict><key>textureRect</key><string>{{200,4},{116,60}}</string><key>textureRotated</key><true/></dict>" +
  "</dict><key>metadata</key><dict><key>format</key><integer>3</integer></dict></dict></plist>";
var frames = Atlas.Frames(Plist.Parse(sheet));
Check(frames.Count == 2, "2 frames read (DOCTYPE tolerated)");
Check(frames["square_01_001.png"].X == 10 && frames["square_01_001.png"].W == 120, "frame rect read");
Check(frames["spike_01_001.png"].Rotated && frames["spike_01_001.png"].W == 60 && frames["spike_01_001.png"].H == 116, "rotated frame swaps w/h in the sheet");

// 4. Steam library discovery via libraryfolders.vdf + appmanifest installdir
var home = Path.Combine(Path.GetTempPath(), "td_home_" + Guid.NewGuid().ToString("N"));
var steam = Path.Combine(home, ".steam/steam");
var lib2 = Path.Combine(home, "lib2");
Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
Directory.CreateDirectory(Path.Combine(lib2, "steamapps/common/GeometryDashFolder/Resources"));
Directory.CreateDirectory(Path.Combine(lib2, "steamapps/common/Turnip Mountain/TurnipMountain_Data"));
File.WriteAllText(Path.Combine(lib2, "steamapps/common/GeometryDashFolder/Resources/StereoMadness.mp3"), "x");
File.WriteAllText(Path.Combine(lib2, "steamapps/appmanifest_322170.acf"), "\"AppState\"\n{\n\t\"appid\"\t\t\"322170\"\n\t\"installdir\"\t\t\"GeometryDashFolder\"\n}");
File.WriteAllText(Path.Combine(steam, "steamapps/libraryfolders.vdf"), $"\"libraryfolders\"\n{{\n\t\"0\"\n\t{{\n\t\t\"path\"\t\t\"{steam}\"\n\t}}\n\t\"1\"\n\t{{\n\t\t\"path\"\t\t\"{lib2}\"\n\t}}\n}}");
Environment.SetEnvironmentVariable("HOME", home);
var gd = GameLocator.Find("gd");
Check(gd != null && gd.EndsWith("GeometryDashFolder"), $"GD found through second library + appmanifest ({gd})");
var tm = GameLocator.Find("tm");
Check(tm != null && tm.EndsWith("Turnip Mountain"), $"TM found by default folder name ({tm})");
Environment.SetEnvironmentVariable("TURNIPDASH_GD_DIR", Path.Combine(lib2, "steamapps/common/GeometryDashFolder"));
Check(GameLocator.Find("gd") != null, "override variable honoured");
Directory.Delete(home, true);

Console.WriteLine(fails == 0 ? "ALL PASS" : $"{fails} FAILED");
return fails;
