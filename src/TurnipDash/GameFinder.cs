using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Celeste.Mod.TurnipDash;

// Finds a Steam game's install folder on the player's PC (Melty does not know Geometry Dash or Turnip Mountain yet,
// so the mod looks for them itself): settings override, then every Steam library listed in libraryfolders.vdf.
public static class GameFinder {
    public static string Find(GameRow game, string overridePath) {
        if (!string.IsNullOrWhiteSpace(overridePath) && Directory.Exists(overridePath)) return overridePath;
        foreach (string lib in SteamLibraries()) {
            string manifest = Path.Combine(lib, "steamapps", $"appmanifest_{game.SteamAppId}.acf");
            string dir = null;
            if (File.Exists(manifest)) {
                var m = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                if (m.Success) dir = Path.Combine(lib, "steamapps", "common", m.Groups[1].Value);
            }
            dir ??= Path.Combine(lib, "steamapps", "common", game.InstallDirName);
            if (Directory.Exists(dir)) return dir;
        }
        return null;
    }

    static IEnumerable<string> SteamLibraries() {
        var roots = new List<string>();
        if (OperatingSystem.IsWindows()) {
            try {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (k?.GetValue("SteamPath") is string p) roots.Add(p.Replace('/', '\\'));
            } catch { }
            try {
                using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                if (k?.GetValue("InstallPath") is string p) roots.Add(p);
            } catch { }
            roots.Add(@"C:\Program Files (x86)\Steam");
        } else {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(Path.Combine(home, ".steam", "steam"));
            roots.Add(Path.Combine(home, ".local", "share", "Steam"));
            roots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
            roots.Add(Path.Combine(home, "Library", "Application Support", "Steam"));
        }
        // Celeste itself usually sits in a Steam library: <lib>/steamapps/common/Celeste
        try {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            if (d.Parent?.Name.Equals("common", StringComparison.OrdinalIgnoreCase) == true) roots.Add(d.Parent.Parent.Parent.FullName);
        } catch { }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots) {
            if (!Directory.Exists(root)) continue;
            if (seen.Add(Path.GetFullPath(root))) yield return root;
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\"")) {
                string lib = m.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(lib) && seen.Add(Path.GetFullPath(lib))) yield return lib;
            }
        }
    }
}
