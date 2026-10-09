using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.TurnipDash;

// Hooks into Celeste (sheet: hooks.json).
public static class Hooks {
    public static bool InMashup(Level l) => l?.Session?.Area.SID == TurnipDashModule.SID;

    public static void Load() {
        Everest.Events.Level.OnLoadLevel += OnLoadLevel;
        Everest.Events.Player.OnSpawn += OnSpawn;
        Everest.Events.Level.OnExit += OnExit;
        On.Celeste.Level.Update += LevelUpdate;
    }
    public static void Unload() {
        Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
        Everest.Events.Player.OnSpawn -= OnSpawn;
        Everest.Events.Level.OnExit -= OnExit;
        On.Celeste.Level.Update -= LevelUpdate;
    }

    static void OnLoadLevel(Level level, Player.IntroTypes intro, bool fromLoader) {
        if (!InMashup(level)) return;
        GDContent.Load(); TMContent.Load();
        var room = Sheets.Rooms.FirstOrDefault(r => r.Id == level.Session.Level);
        if (room != null) {
            PlayMusic(Sheets.Content.First(c => c.Id == room.Music));
            if (level.Tracker.GetEntity<Player>() is Player p) Modes.Set(p, room.StartMode);
        }
        Audio.SetMusic(null);
        if (level.Tracker.GetEntity<NoticeHud>() == null) level.Add(new NoticeHud());
        else level.Tracker.GetEntity<NoticeHud>().Show();
    }

    static void PlayMusic(ContentRow row) {
        if (Jukebox.CurrentKey == row.Id) return;
        if (row.Game == Sheets.GameGeometryDash.Id) {
            string path = GDContent.Song(row);
            if (path != null) Jukebox.PlayFile(row.Id, path); else Jukebox.Stop();
        } else {
            byte[] data = TMContent.Audio(row);
            if (data != null) Jukebox.PlayBytes(row.Id, data); else Jukebox.Stop();
        }
    }

    // Geometry Dash style: dying restarts the song, so beat-timed rooms line up again.
    static void OnSpawn(Player p) { if (InMashup(p.Scene as Level)) Jukebox.Restart(); }
    static void OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) { if (InMashup(level)) Jukebox.Stop(); }

    static void LevelUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);
        if (!InMashup(self)) return;
        Jukebox.SetPaused(self.FrozenOrPaused);
        if (!self.FrozenOrPaused) Jukebox.Tick(Engine.DeltaTime);
    }

    static byte[] grabSfx; static bool grabLoaded;
    public static void PlayGrab(Player p) {
        if (!grabLoaded) { grabLoaded = true; grabSfx = TMContent.Audio(Sheets.TmGrab); }
        if (grabSfx != null) Sfx.Play(grabSfx); else Audio.Play("event:/char/madeline/grab", p.Position);
    }
}

// One-shot sound effects from bytes (Turnip Mountain's own grab sound).
static class Sfx {
    static FMOD.Sound sound; static byte[] loadedFrom;
    public static void Play(byte[] data) {
        try {
            Audio.System.getLowLevelSystem(out var core);
            if (loadedFrom != data) {
                var ex = new FMOD.CREATESOUNDEXINFO { cbsize = System.Runtime.InteropServices.Marshal.SizeOf<FMOD.CREATESOUNDEXINFO>(), length = (uint) data.Length };
                core.createSound(data, FMOD.MODE._2D | FMOD.MODE.OPENMEMORY | FMOD.MODE.CREATESAMPLE, ref ex, out sound);
                if (sound.getNumSubSounds(out int n) == FMOD.RESULT.OK && n > 0) sound.getSubSound(0, out sound);
                loadedFrom = data;
            }
            core.getMasterChannelGroup(out var master);
            core.playSound(sound, master, false, out var ch);
            ch.setVolume(Settings.Instance.SFXVolume / 10f);
        } catch (Exception e) { TurnipDashModule.Log($"grab sfx: {e.Message}"); }
    }
}
