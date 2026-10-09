using System;
using System.Collections;
using System.Linq;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.TurnipDash;

// Entities (sheet: entities.json). Each draws Geometry Dash / Turnip Mountain art read from the player's installs.
static class Art {
    public static void Tile(Frame f, Vector2 pos, int w, int h, Color c) {
        for (int y = 0; y < h; y += 8) for (int x = 0; x < w; x += 8)
            if (f != null) f.Draw(pos + new Vector2(x + 4, y + 4), 8f, c); else Draw.HollowRect(pos.X + x, pos.Y + y, 8, 8, c);
    }
    public static int Phase(float beat, int on, int off, int phase) {
        int cycle = on + off; int b = (int) Math.Floor(beat) + phase * on;
        return ((b % cycle) + cycle) % cycle;
    }
}

[CustomEntity("TurnipDash/BeatBlock")]
public class BeatBlock : Solid {
    readonly int on, off, phase; bool active = true;
    public BeatBlock(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, 8, false) {
        on = data.Int("beatsOn", 2); off = data.Int("beatsOff", 2); phase = data.Int("phase");
    }
    public override void Update() {
        base.Update();
        bool want = Art.Phase(Jukebox.Beat, on, off, phase) < on;
        if (want && !active && CollideCheck<Player>()) want = false; // don't appear inside the player
        active = want; Collidable = active;
    }
    public override void Render() {
        float pulse = 1f - (Jukebox.Beat - (float) Math.Floor(Jukebox.Beat)) * 0.3f;
        Art.Tile(GDContent.Get(Sheets.GdBlock), Position, (int) Width, 8, active ? Color.White * pulse : Color.White * 0.2f);
    }
}

[CustomEntity("TurnipDash/BeatSpike")]
public class BeatSpike : Entity {
    readonly int on, off, phase; bool outNow;
    public BeatSpike(EntityData data, Vector2 offset) : base(data.Position + offset) {
        on = data.Int("beatsOn", 1); off = data.Int("beatsOff", 1); phase = data.Int("phase");
        Collider = new Hitbox(data.Width, 4, 0, 4);
        Add(new PlayerCollider(p => { if (outNow) p.Die(-Vector2.UnitY); }));
    }
    public override void Update() { base.Update(); outNow = Art.Phase(Jukebox.Beat, on, off, phase) < on; }
    public override void Render() {
        var f = GDContent.Get(Sheets.GdSpike);
        for (int x = 0; x < Width; x += 8) {
            var at = Position + new Vector2(x + 4, outNow ? 4 : 7);
            if (f == null) { Draw.HollowRect(Position.X + x, Position.Y + (outNow ? 0 : 6), 8, outNow ? 8 : 2, Color.Red); continue; }
            f.Tex.DrawCentered(at, outNow ? Color.White : Color.White * 0.5f, new Vector2(1f, outNow ? 1f : 0.25f) * (8f / Math.Max(f.Size.X, f.Size.Y)), f.Rotated ? -MathHelper.PiOver2 : 0f);
        }
    }
}

[CustomEntity("TurnipDash/GDSpike")]
public class GDSpike : Entity {
    public GDSpike(EntityData data, Vector2 offset) : base(data.Position + offset) {
        Collider = new Hitbox(4, 4, 2, 4); // Geometry Dash spikes kill on their inner part only
        Add(new PlayerCollider(p => p.Die(-Vector2.UnitY)));
    }
    public override void Render() { var f = GDContent.Get(Sheets.GdSpike); if (f != null) f.Draw(Position + new Vector2(4, 4), 8f, Color.White); else Draw.HollowRect(X, Y, 8, 8, Color.Red); }
}

[CustomEntity("TurnipDash/GDBlock")]
public class GDBlock : Solid {
    public GDBlock(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, data.Height, true) { }
    public override void Render() => Art.Tile(GDContent.Get(Sheets.GdBlock), Position, (int) Width, (int) Height, Color.White);
}

[CustomEntity("TurnipDash/ModePortal")]
public class ModePortal : Entity {
    readonly string mode;
    public ModePortal(EntityData data, Vector2 offset) : base(data.Position + offset) {
        mode = data.Attr("mode", "normal");
        Collider = new Hitbox(16, 32);
        Add(new PlayerCollider(p => Modes.Set(p, mode)));
        Depth = -100;
    }
    public override void Render() {
        var box = new Rectangle((int) X, (int) Y, 16, 32);
        var frame = GDContent.Get(mode == "ship" ? Sheets.GdPortalShip : Sheets.GdPortalCube);
        Color tint = mode switch { "turnip" => new Color(255, 160, 60), "normal" => new Color(255, 120, 200), _ => Color.White };
        if (frame != null) frame.DrawFit(box, tint); else Draw.HollowRect(box, tint);
        if (mode == "turnip" && TMContent.Sprite(Sheets.TmTurnip) is MTexture t) t.DrawCentered(Center, Color.White, 10f / Math.Max(t.Width, t.Height));
    }
}

[CustomEntity("TurnipDash/TurnipStatue")]
public class TurnipStatue : Entity {
    readonly string returnTo;
    public TurnipStatue(EntityData data, Vector2 offset) : base(data.Position + offset) {
        returnTo = data.Attr("returnTo", "hub");
        Collider = new Hitbox(16, 16, -4, -8);
        Add(new PlayerCollider(p => Banner.Complete(p, "TURNIP REACHED!", returnTo)));
    }
    public override void Render() {
        var t = TMContent.Sprite(Sheets.TmTurnip);
        var at = Center + new Vector2(0, (float) Math.Sin(Scene.TimeActive * 3) * 1.5f);
        if (t != null) t.DrawCentered(at, Color.White, 16f / Math.Max(t.Width, t.Height)); else Draw.HollowRect(Collider.Bounds, Color.Orange);
    }
}

[CustomEntity("TurnipDash/FinishGate")]
public class FinishGate : Entity {
    readonly string returnTo;
    public FinishGate(EntityData data, Vector2 offset) : base(data.Position + offset) {
        returnTo = data.Attr("returnTo", "hub");
        Collider = new Hitbox(16, 24);
        Add(new PlayerCollider(p => Banner.Complete(p, "LEVEL COMPLETE!", returnTo)));
    }
    public override void Render() {
        for (int i = 0; i < 6; i++) Draw.Rect(X + 6, Y + i * 4, 4, 2, i % 2 == 0 ? Color.White : Color.Black);
    }
}

// "LEVEL COMPLETE!" in Geometry Dash's big font, then back to the hub.
[Tracked]
public class Banner : Entity {
    readonly string text; float t;
    Banner(string text) { this.text = text; Tag = Tags.HUD | Tags.Global | Tags.FrozenUpdate | Tags.TransitionUpdate; }
    public static void Complete(Player p, string text, string returnTo) {
        if (p.Scene is not Level level || level.Tracker.GetEntity<Banner>() != null) return;
        level.Session.SetFlag("td_done_" + level.Session.Level);
        Audio.Play("event:/game/general/strawberry_get", p.Position);
        var b = new Banner(text); level.Add(b);
        b.Add(new Coroutine(b.Run(level, returnTo)));
    }
    IEnumerator Run(Level level, string returnTo) {
        yield return 0.8f;
        if (level.Tracker.GetEntity<Player>() is Player p) { Modes.Set(p, "normal"); level.TeleportTo(p, returnTo, Player.IntroTypes.Respawn); }
        yield return 1.2f;
        RemoveSelf();
    }
    public override void Update() { base.Update(); t += Engine.DeltaTime; }
    public override void Render() {
        float s = Math.Min(1f, t * 4f);
        if (GDContent.Font != null) GDContent.Font.Draw(text, new Vector2(960, 420), 110f * s, Color.White);
        else ActiveFont.DrawOutline(text, new Vector2(960, 420), new Vector2(0.5f), Vector2.One * 2f * s, Color.White, 2f, Color.Black);
    }
}

[CustomEntity("TurnipDash/HubSign")]
public class HubSign : Entity {
    public HubSign(EntityData data, Vector2 offset) : base(data.Position + offset) { Tag = Tags.HUD; }
    public override void Render() {
        var s = (Scene as Level)?.Session;
        string Done(string room) => s != null && s.GetFlag("td_done_" + room) ? " *" : "";
        string title = "TURNIP DASH";
        if (GDContent.Font != null) GDContent.Font.Draw(title, new Vector2(960, 110), 90f, Color.White);
        else ActiveFont.DrawOutline(title, new Vector2(960, 110), new Vector2(0.5f), Vector2.One * 1.6f, Color.White, 2f, Color.Black);
        ActiveFont.DrawOutline($"< Rhythm climb{Done("rhythm")}      ^ Turnip climb{Done("turnip")}      Auto-run{Done("autorun")} >",
            new Vector2(960, 190), new Vector2(0.5f), Vector2.One * 0.8f, Color.White, 2f, Color.Black);
        string gd = GDContent.Found ? "Geometry Dash: found" : "Geometry Dash: not installed";
        string tm = TMContent.Found ? $"Turnip Mountain: found ({TMContent.Engine})" : "Turnip Mountain: not installed";
        ActiveFont.DrawOutline(gd + "    " + tm, new Vector2(960, 1030), new Vector2(0.5f), Vector2.One * 0.5f, Color.LightGray, 2f, Color.Black);
    }
}

// Says in game what couldn't be read from the other games' installs.
[Tracked]
public class NoticeHud : Entity {
    float shown = 10f;
    public NoticeHud() { Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate; Depth = -10000; }
    public void Show() => shown = 6f;
    public override void Update() { base.Update(); shown -= Engine.DeltaTime; }
    public override void Render() {
        if (shown <= 0) return;
        var lines = GDContent.Missing.Concat(TMContent.Missing).Distinct().Take(6).ToList();
        float a = Math.Min(1f, shown);
        for (int i = 0; i < lines.Count; i++)
            ActiveFont.DrawOutline(lines[i], new Vector2(40, 300 + i * 46), Vector2.Zero, Vector2.One * 0.6f, Color.Orange * a, 2f, Color.Black * a);
    }
}
