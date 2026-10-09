using System;
using System.Linq;
using Monocle;

namespace Celeste.Mod.TurnipDash;

public class TurnipDashSettings : EverestModuleSettings {
    [SettingMaxLength(260)] public string GeometryDashFolder { get; set; } = "";
    [SettingMaxLength(260)] public string TurnipMountainFolder { get; set; } = "";
    public bool ShowContentReport { get; set; } = false;
}

public class TurnipDashModule : EverestModule {
    public const string SID = "TurnipDash/TurnipDash";
    public static TurnipDashModule Instance;
    public override Type SettingsType => typeof(TurnipDashSettings);
    public static TurnipDashSettings Settings => (TurnipDashSettings) Instance._Settings;
    static bool autoStartPending;

    public TurnipDashModule() { Instance = this; }

    public override void Load() {
        autoStartPending = Environment.GetCommandLineArgs().Any(a => a == "--turnip-dash")
            || Environment.GetEnvironmentVariable("TURNIPDASH_AUTOSTART") == "1";
        Modes.Load();
        Hooks.Load();
        On.Celeste.OuiChapterSelect.Enter += OnChapterSelectEnter;
    }

    public override void Unload() {
        Modes.Unload();
        Hooks.Unload();
        On.Celeste.OuiChapterSelect.Enter -= OnChapterSelectEnter;
        Jukebox.Stop();
    }

    // Melty launches Celeste with --turnip-dash: once a save file is picked, go straight to the hub.
    static System.Collections.IEnumerator OnChapterSelectEnter(On.Celeste.OuiChapterSelect.orig_Enter orig, OuiChapterSelect self, Oui from) {
        if (autoStartPending && SaveData.Instance != null && AreaData.Get(SID) != null) {
            autoStartPending = false;
            LevelEnter.Go(new Session(AreaData.Get(SID).ToKey()), false);
        }
        return orig(self, from);
    }

    public static void Log(string msg) => Logger.Log(LogLevel.Info, "TurnipDash", msg);
}
