using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TurnipDash.Generated;

namespace TurnipDash
{
    /// Finds the player's own install of a secondary game (rows of the games sheet):
    /// its override variable first, then every Steam library listed in libraryfolders.vdf.
    static class GameLocator
    {
        public static string Find(string gameId)
        {
            GamesRow row = null;
            foreach (var r in Sheets.Games) if (r.Id == gameId) row = r;
            if (row == null) return null;

            var env = Environment.GetEnvironmentVariable(row.EnvOverride);
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env) && Probe(env, row.ProbeFile)) return env;

            foreach (var lib in SteamLibraries())
            {
                var apps = Path.Combine(lib, "steamapps");
                var dir = InstallDirFromManifest(apps, row.SteamAppId) ?? row.SteamInstallDir;
                var full = Path.Combine(Path.Combine(apps, "common"), dir);
                if (Directory.Exists(full) && Probe(full, row.ProbeFile)) return full;
            }
            return null;
        }

        static bool Probe(string dir, string probe)
        {
            if (probe == "*") return Directory.GetFileSystemEntries(dir).Length > 0;
            return File.Exists(Path.Combine(dir, probe.Replace('/', Path.DirectorySeparatorChar)));
        }

        static string InstallDirFromManifest(string steamapps, int appId)
        {
            var acf = Path.Combine(steamapps, "appmanifest_" + appId + ".acf");
            if (!File.Exists(acf)) return null;
            var m = Regex.Match(File.ReadAllText(acf), "\"installdir\"\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static IEnumerable<string> SteamLibraries()
        {
            var roots = new List<string>();
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    if (k?.GetValue("SteamPath") is string p) roots.Add(p.Replace('/', '\\'));
            }
            catch { /* not Windows, or no registry access */ }
            roots.Add(@"C:\Program Files (x86)\Steam");
            roots.Add(@"C:\Program Files\Steam");
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                roots.Add(Path.Combine(home, ".steam/steam"));
                roots.Add(Path.Combine(home, ".local/share/Steam"));
                roots.Add(Path.Combine(home, "Library/Application Support/Steam"));
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                if (seen.Add(Path.GetFullPath(root))) yield return root;
                var vdf = Path.Combine(Path.Combine(root, "steamapps"), "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                {
                    var lib = m.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(lib) && seen.Add(Path.GetFullPath(lib))) yield return lib;
                }
            }
        }
    }
}
