using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FMOD;

namespace Celeste.Mod.TurnipDash;

// Plays a song borrowed from another game through Celeste's own FMOD core system, and keeps a beat clock
// measured from that song's audio (tempo and first-beat offset are detected from the decoded samples).
public static class Jukebox {
    sealed class Song { public Sound Sound, Play; public float Bpm, OffsetMs; public GCHandle Pin; }
    static readonly Dictionary<string, Song> songs = new();
    static Channel channel; static Song current; static string currentKey;
    public const float FallbackBpm = 120f; // only when no borrowed song is playing
    static float fallbackMs;

    static FMOD.System Core { get { Audio.System.getLowLevelSystem(out var s); return s; } }

    public static string CurrentKey => currentKey;
    public static bool HasSong => current != null;
    public static float Bpm => current?.Bpm ?? FallbackBpm;
    public static float PositionMs {
        get {
            if (current == null || channel == null) return fallbackMs;
            channel.getPosition(out uint ms, TIMEUNIT.MS); return ms;
        }
    }
    // Beats since the song's first beat (negative before it).
    public static float Beat => (PositionMs - (current?.OffsetMs ?? 0f)) * Bpm / 60000f;

    public static void PlayFile(string key, string path) => Play(key, () => {
        var s = new Song();
        Check(Core.createSound(path, MODE._2D | MODE.CREATESTREAM | MODE.LOOP_NORMAL | MODE.ACCURATETIME, out s.Sound), path);
        s.Play = s.Sound;
        Analyze(s, () => { Core.createSound(path, MODE._2D | MODE.OPENONLY | MODE.ACCURATETIME, out var a); return a; });
        return s;
    });

    public static void PlayBytes(string key, byte[] data) => Play(key, () => {
        var s = new Song { Pin = GCHandle.Alloc(data, GCHandleType.Pinned) };
        var ex = new CREATESOUNDEXINFO { cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(), length = (uint) data.Length };
        Check(Core.createSound(data, MODE._2D | MODE.OPENMEMORY | MODE.CREATESAMPLE | MODE.LOOP_NORMAL | MODE.ACCURATETIME, ref ex, out s.Sound), key);
        s.Play = s.Sound;
        // Unity packs each clip as an FSB5 bank holding one sub-sound
        if (s.Sound.getNumSubSounds(out int subs) == RESULT.OK && subs > 0) { s.Sound.getSubSound(0, out var sub); sub.setMode(MODE.LOOP_NORMAL); s.Play = sub; }
        s.Bpm = FallbackBpm; // Turnip Mountain music drives no beat-synced objects
        return s;
    });

    static void Play(string key, Func<Song> make) {
        if (currentKey == key && channel != null && channel.isPlaying(out bool p) == RESULT.OK && p) return;
        StopChannel();
        try {
            if (!songs.TryGetValue(key, out var s)) songs[key] = s = make();
            Core.getMasterChannelGroup(out var master);
            Check(Core.playSound(s.Play, master, false, out channel), key);
            current = s; currentKey = key; UpdateVolume();
            TurnipDashModule.Log($"playing {key} at {s.Bpm:0.0} BPM, first beat {s.OffsetMs:0} ms");
        } catch (Exception e) { TurnipDashModule.Log($"music {key}: {e.Message}"); current = null; currentKey = null; }
    }

    public static void Restart() { channel?.setPosition(0, TIMEUNIT.MS); fallbackMs = 0; }
    public static void SetPaused(bool paused) => channel?.setPaused(paused);
    public static void Tick(float dt) { if (current == null) fallbackMs += dt * 1000f; UpdateVolume(); }
    static void UpdateVolume() => channel?.setVolume(Settings.Instance.MusicVolume / 10f * 0.8f);

    static void StopChannel() { channel?.stop(); channel = null; current = null; currentKey = null; fallbackMs = 0; }
    public static void Stop() => StopChannel();

    static void Check(RESULT r, string what) { if (r != RESULT.OK) throw new Exception($"FMOD {r} ({what})"); }

    // ---- tempo detection: energy onsets, autocorrelation for the period, comb for the phase ----
    static void Analyze(Song s, Func<Sound> openForRead) {
        s.Bpm = FallbackBpm; s.OffsetMs = 0;
        Sound src = openForRead();
        try {
            src.getFormat(out _, out SOUND_FORMAT fmt, out int ch, out int bits);
            src.getDefaults(out float rate, out _);
            if (rate <= 0) rate = 44100;
            int hop = 512, maxSamples = (int) (rate * 40); // first 40 s
            var env = new List<float>();
            int bytesPerSample = bits / 8 * ch;
            byte[] buf = new byte[hop * bytesPerSample];
            var pin = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try {
                for (int total = 0; total < maxSamples; total += hop) {
                    if (src.readData(pin.AddrOfPinnedObject(), (uint) buf.Length, out uint read) != RESULT.OK || read < buf.Length) break;
                    double e = 0;
                    for (int i = 0; i < hop * ch; i++) {
                        float v = fmt == SOUND_FORMAT.PCMFLOAT ? BitConverter.ToSingle(buf, i * 4) : fmt == SOUND_FORMAT.PCM16 ? BitConverter.ToInt16(buf, i * 2) / 32768f : 0f;
                        e += v * v;
                    }
                    env.Add((float) Math.Sqrt(e / (hop * ch)));
                }
            } finally { pin.Free(); }
            if (env.Count < 200) return;
            var onset = new float[env.Count];
            for (int i = 1; i < env.Count; i++) onset[i] = Math.Max(0, env[i] - env[i - 1]);
            float framesPerSec = rate / hop;
            int minLag = (int) (framesPerSec * 60 / 200), maxLag = (int) (framesPerSec * 60 / 70);
            double best = -1; int bestLag = 0;
            for (int lag = minLag; lag <= maxLag; lag++) {
                double c = 0; for (int i = lag; i < onset.Length; i++) c += onset[i] * onset[i - lag];
                if (c > best) { best = c; bestLag = lag; }
            }
            float bpm = 60f * framesPerSec / bestLag;
            while (bpm < 100) bpm *= 2; while (bpm > 200) bpm /= 2;
            float period = framesPerSec * 60 / bpm;
            double bestPhase = -1; float phase = 0;
            for (float ph = 0; ph < period; ph += 0.5f) {
                double c = 0; for (float t = ph; t < onset.Length; t += period) c += onset[(int) t];
                if (c > bestPhase) { bestPhase = c; phase = ph; }
            }
            s.Bpm = bpm; s.OffsetMs = phase / framesPerSec * 1000f;
        } catch (Exception e) { TurnipDashModule.Log($"tempo detection failed: {e.Message}"); }
        finally { src?.release(); }
    }
}
