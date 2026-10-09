using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.TurnipDash;

// Per-player state for the borrowed modes (sheet: modes.json).
public class ModeData : Component {
    public Vector2 HandL, HandR, AnchorL, AnchorR; public bool GripL, GripR;
    public float Spin, MouseActive; Vector2 lastMouse;
    public ModeData() : base(true, false) { }
    public override void Update() {
        var m = MInput.Mouse.Position;
        if (m != lastMouse || MInput.Mouse.CheckLeftButton) MouseActive = 2f; else MouseActive -= Engine.DeltaTime;
        lastMouse = m;
    }
}

public static class Modes {
    public static int StCube = -1, StShip = -1, StTurnip = -1;
    static readonly Color GDColor1 = new(125, 255, 0), GDColor2 = new(0, 255, 255), Arm = new(70, 50, 40);

    public static void Load() { On.Celeste.Player.ctor += Ctor; On.Celeste.Player.Render += Render; }
    public static void Unload() { On.Celeste.Player.ctor -= Ctor; On.Celeste.Player.Render -= Render; }

    static void Ctor(On.Celeste.Player.orig_ctor orig, Player self, Vector2 position, PlayerSpriteMode spriteMode) {
        orig(self, position, spriteMode);
        StCube = self.AddState(Sheets.ModeCube.State, CubeUpdate, null, p => Data(p).Spin = 0f, null);
        StShip = self.AddState(Sheets.ModeShip.State, ShipUpdate, null, null, null);
        StTurnip = self.AddState(Sheets.ModeTurnip.State, TurnipUpdate, null, TurnipBegin, null);
        self.Add(new ModeData());
    }

    public static ModeData Data(Player p) => p.Get<ModeData>();

    public static void Set(Player p, string mode) {
        int st = mode switch { "cube" => StCube, "ship" => StShip, "turnip" => StTurnip, _ => Player.StNormal };
        if (st < 0 || p.StateMachine.State == st) return;
        p.StateMachine.State = st;
    }

    public static bool InBorrowedMode(Player p) { int s = p.StateMachine.State; return s == StCube || s == StShip || s == StTurnip; }

    static bool Holding => Input.Jump.Check || MInput.Mouse.CheckLeftButton;

    static bool HitWall(Player p, ModeRow m) {
        if (!m.DiesOnWallHit || !p.CollideCheck<Solid>(p.Position + Vector2.UnitX)) return false;
        p.Die(-Vector2.UnitX); return true;
    }

    // Geometry Dash cube: runs on its own, hold to jump, spins in the air and lands square.
    static int CubeUpdate(Player p) {
        var m = Sheets.ModeCube; var d = Data(p); float dt = Engine.DeltaTime;
        p.Speed.X = m.RunSpeed;
        if (p.OnGround() && p.Speed.Y >= 0) {
            d.Spin = (float) Math.Round(d.Spin / MathHelper.PiOver2) * MathHelper.PiOver2;
            p.Speed.Y = Holding ? -m.JumpSpeed : 0f;
        } else {
            p.Speed.Y = Calc.Approach(p.Speed.Y, m.MaxFall, m.Gravity * dt);
            d.Spin += MathHelper.ToRadians(m.SpinDegPerSec) * dt;
        }
        HitWall(p, m);
        return StCube;
    }

    // Geometry Dash ship: hold to rise, let go to sink.
    static int ShipUpdate(Player p) {
        var m = Sheets.ModeShip; var d = Data(p); float dt = Engine.DeltaTime;
        p.Speed.X = m.RunSpeed;
        p.Speed.Y = Holding ? Calc.Approach(p.Speed.Y, -m.MaxRise, m.LiftAccel * dt) : Calc.Approach(p.Speed.Y, m.MaxFall, m.Gravity * dt);
        d.Spin = (float) Math.Atan2(p.Speed.Y, p.Speed.X) * 0.7f;
        HitWall(p, m);
        return StShip;
    }

    static void TurnipBegin(Player p) {
        var d = Data(p); d.HandL = p.Center + new Vector2(-6, -4); d.HandR = p.Center + new Vector2(6, -4); d.GripL = d.GripR = false;
    }

    // Turnip Mountain's turnip: each hand moves on its own and grips any wall it touches; momentum carries on release.
    static int TurnipUpdate(Player p) {
        var m = Sheets.ModeTurnip; var d = Data(p); float dt = Engine.DeltaTime;
        Vector2 c = p.Center;
        var pad = MInput.GamePads[Input.Gamepad];
        Vector2 ls = pad.Attached ? pad.GetLeftStick(0.2f) : Vector2.Zero, rs = pad.Attached ? pad.GetRightStick(0.2f) : Vector2.Zero;
        Vector2 aim = Input.Aim.Value;
        Vector2 aimL = ls.LengthSquared() > 0.04f ? ls : aim;
        Vector2 aimR = rs.LengthSquared() > 0.04f ? rs : aim;
        if (d.MouseActive > 0 && p.Scene is Level level) {
            Vector2 mouseWorld = level.Camera.Position + (MInput.Mouse.Position - new Vector2(Engine.Viewport.X, Engine.Viewport.Y)) * (320f / Engine.ViewWidth);
            aimR = (mouseWorld - c) / m.Reach;
        }
        if (aimL.Length() > 1) aimL.Normalize();
        if (aimR.Length() > 1) aimR.Normalize();
        bool wantL = Input.Grab.Check || pad.LeftTriggerCheck(0.3f);
        bool wantR = Input.Jump.Check || MInput.Mouse.CheckLeftButton || pad.RightTriggerCheck(0.3f);
        Hand(p, m, c, aimL, wantL, ref d.HandL, ref d.AnchorL, ref d.GripL, dt);
        Hand(p, m, c, aimR, wantR, ref d.HandR, ref d.AnchorR, ref d.GripR, dt);

        if (d.GripL || d.GripR) {
            Vector2 want = Vector2.Zero; int n = 0;
            if (d.GripL) { want += d.AnchorL - aimL * m.Reach; n++; }
            if (d.GripR) { want += d.AnchorR - aimR * m.Reach; n++; }
            Vector2 vel = (want / n - c) * m.Pull;
            if (vel.Length() > 260f) vel = vel.SafeNormalize() * 260f;
            p.Speed = Calc.Approach(p.Speed, vel, 1800f * dt);
        } else {
            p.Speed.Y = Calc.Approach(p.Speed.Y, m.MaxFall, m.Gravity * dt);
            p.Speed.X = Calc.Approach(p.Speed.X, 0f, (p.OnGround() ? 500f : 30f) * dt);
        }
        return StTurnip;
    }

    static void Hand(Player p, ModeRow m, Vector2 c, Vector2 aim, bool want, ref Vector2 hand, ref Vector2 anchor, ref bool grip, float dt) {
        Vector2 target = c + aim * m.Reach;
        if (!want) grip = false;
        else if (!grip && TryGrip(p, target, m.GripRadius, out anchor)) { grip = true; Hooks.PlayGrab(p); }
        if (grip && Vector2.Distance(anchor, c) > m.Reach * 1.3f) grip = false; // arm can't stretch further
        hand = grip ? anchor : Calc.Approach(hand, target, 420f * dt);
        if (!grip && Vector2.Distance(hand, c) > m.Reach) hand = c + (hand - c).SafeNormalize() * m.Reach;
    }

    static bool TryGrip(Player p, Vector2 at, float r, out Vector2 anchor) {
        for (float dy = -r; dy <= r; dy += r) for (float dx = -r; dx <= r; dx += r) {
            var q = at + new Vector2(dx, dy);
            if (p.Scene.CollideCheck<Solid>(q)) { anchor = q; return true; }
        }
        anchor = at; return false;
    }

    // Draw the Geometry Dash icon or Turnip Mountain's turnip in place of Madeline; Madeline stays when the art is missing.
    static void Render(On.Celeste.Player.orig_Render orig, Player self) {
        int st = self.StateMachine.State;
        if (st != StCube && st != StShip && st != StTurnip) { orig(self); return; }
        var d = Data(self); Vector2 c = self.Center;
        if (st == StTurnip) {
            var body = TMContent.Sprite(Sheets.TmTurnip); var hand = TMContent.Sprite(Sheets.TmHand);
            Draw.Line(c, d.HandL, Arm, 2f); Draw.Line(c, d.HandR, Arm, 2f);
            if (body != null) body.DrawCentered(c, Color.White, 14f / Math.Max(body.Width, body.Height)); else orig(self);
            foreach (var h in new[] { d.HandL, d.HandR }) {
                if (hand != null) hand.DrawCentered(h, Color.White, 6f / Math.Max(hand.Width, hand.Height)); else Draw.Circle(h, 2f, Arm, 4);
            }
            return;
        }
        var cube = GDContent.Get(Sheets.GdCube); var cube2 = GDContent.Get(Sheets.GdCube2);
        if (cube == null) { orig(self); return; }
        if (st == StShip) {
            var ship = GDContent.Get(Sheets.GdShip);
            cube2?.Draw(c + new Vector2(0, -4), 6f, GDColor2, d.Spin); cube.Draw(c + new Vector2(0, -4), 6f, GDColor1, d.Spin);
            ship?.Draw(c + new Vector2(0, 1), 13f, GDColor1, d.Spin);
        } else {
            cube2?.Draw(c, 9f, GDColor2, d.Spin); cube.Draw(c, 9f, GDColor1, d.Spin);
        }
    }
}
