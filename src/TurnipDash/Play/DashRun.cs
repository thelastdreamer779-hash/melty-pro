using System.Collections.Generic;
using System.Linq;
using TurnipDash.Content;
using TurnipDash.Generated;
using UnityEngine;

namespace TurnipDash.Play
{
    /// Stereo Madness, auto-run, with the turnip as the player. Rules from run_physics and gd_objects.
    public sealed class DashRun
    {
        struct Thing { public GdObjectsRow Row; public Rect Box; public bool Used; }

        readonly ContentStore store;
        readonly Arena arena;
        readonly List<Thing> things = new List<Thing>();
        readonly Transform avatar;
        readonly AudioSource music;
        readonly float endX;

        RunPhysicsRow phys;
        Vector2 pos, prevPos; float vy; bool grounded, prevJump; float spin;
        float shipFloor, shipCeil, deadTimer;
        public int Attempt = 1;
        public bool Complete;
        public float Progress => Mathf.Clamp01(pos.x / endX);

        public DashRun(ContentStore store)
        {
            this.store = store;
            arena = new Arena("DashRun", new Color(0.16f, 0.35f, 0.9f), 12f);
            var byId = Sheets.GdObjects.ToDictionary(r => r.GdObjectId);
            float maxX = 0;
            foreach (var o in store.Level)
            {
                if (!byId.TryGetValue(o.Id, out var row)) continue;
                var rot = o.Rotation;
                var w = row.HitW; var h = row.HitH; var off = row.HitOffsetY;
                if (o.FlipY) { off = -off; rot += 180; }
                // turn the hitbox with the object (GD rotates clockwise, in 90 degree steps for these objects)
                var q = Mathf.RoundToInt(rot / 90f) & 3;
                Vector2 c = new Vector2(o.X, o.Y);
                Vector2 offV = q == 0 ? new Vector2(0, off) : q == 1 ? new Vector2(off, 0) : q == 2 ? new Vector2(0, -off) : new Vector2(-off, 0);
                if ((q & 1) == 1) { var t = w; w = h; h = t; }
                var center = c + offV;
                things.Add(new Thing { Row = row, Box = new Rect(center.x - w / 2, center.y - h / 2, w, h) });
                store.Sprites.TryGetValue(row.Frame, out var spr);
                float extra = store.Rotated.Contains(row.Frame) ? 90f : 0f;
                arena.Add(row.Id, spr, c, row.Behaviour.StartsWith("to") ? 3f : 1f, spr != null ? Color.white : Tint(row.Behaviour), 1, -rot + extra);
                maxX = Mathf.Max(maxX, o.X);
            }
            endX = maxX + 6f;
            arena.Add("ground", null, new Vector2(endX / 2, -5f), endX + 40, new Color(0.05f, 0.12f, 0.45f), 0).transform.localScale = new Vector3(endX + 40, 10f, 1);
            store.Sprites.TryGetValue("tm_turnip_body", out var turnip);
            avatar = arena.Add("turnip", turnip, Vector2.zero, 1f, turnip != null ? Color.white : new Color(0.95f, 0.85f, 0.95f), 5).transform;
            music = arena.Root.AddComponent<AudioSource>();
            music.clip = store.Music; music.spatialBlend = 0f; music.volume = 0.8f;
            Restart();
        }

        static Color Tint(string behaviour) =>
            behaviour == "hazard" ? new Color(1f, 0.3f, 0.3f) : behaviour == "solid" ? new Color(0.2f, 0.2f, 0.25f) : new Color(1f, 0.9f, 0.2f);

        RunPhysicsRow Mode(string mode) => Sheets.RunPhysics.First(r => r.Mode == mode);

        void Restart()
        {
            phys = Mode("cube");
            pos = prevPos = new Vector2(0, 0); vy = 0; grounded = true; spin = 0; deadTimer = 0;
            for (int i = 0; i < things.Count; i++) { var t = things[i]; t.Used = false; things[i] = t; }
            if (music.clip != null) { music.Stop(); music.time = 0; music.Play(); }
        }

        public void Update(float dt, bool jumpHeld, bool restartPressed)
        {
            if (Complete) return;
            if (restartPressed) { Attempt++; Restart(); }
            if (deadTimer > 0) { deadTimer -= dt; if (deadTimer <= 0) { Attempt++; Restart(); } return; }

            const float step = 1f / 240f;
            for (float t = 0; t < dt && deadTimer <= 0 && !Complete; t += step)
                Step(Mathf.Min(step, dt - t), jumpHeld, jumpHeld && !prevJump);
            prevJump = jumpHeld;

            avatar.localPosition = new Vector3(pos.x, pos.y + phys.Size / 2, 0);
            avatar.localRotation = Quaternion.Euler(0, 0, phys.Mode == "ship" ? Mathf.Clamp(vy * 3f, -45, 45) : spin);
            arena.Look(new Vector2(pos.x + 4f, Mathf.Max(3.5f, pos.y)));
        }

        void Step(float dt, bool held, bool pressed)
        {
            prevPos = pos;
            pos.x += phys.Speed * dt;
            if (phys.Mode == "cube")
            {
                if (held && grounded) { vy = phys.JumpVelocity; grounded = false; }
                vy -= phys.Gravity * dt;
                if (!grounded) spin -= 420f * dt;
            }
            else vy += ((held ? phys.HoldLift : 0f) - phys.Gravity) * dt;
            vy = Mathf.Clamp(vy, -phys.MaxFall, phys.MaxFall * 2);
            pos.y += vy * dt;

            float floor = phys.Mode == "ship" ? shipFloor : 0f;
            grounded = false;
            if (pos.y <= floor) { pos.y = floor; vy = Mathf.Max(0, vy); grounded = true; }
            if (phys.Mode == "ship" && pos.y + phys.Size >= shipCeil) { pos.y = shipCeil - phys.Size; vy = Mathf.Min(0, vy); }

            var me = new Rect(pos.x - phys.Size / 2, pos.y, phys.Size, phys.Size);
            for (int i = 0; i < things.Count; i++)
            {
                var th = things[i];
                if (th.Box.xMin > me.xMax + 1) break; // sorted by x
                if (th.Box.xMax < me.xMin - 1 || !th.Box.Overlaps(me)) continue;
                switch (th.Row.Behaviour)
                {
                    case "solid":
                        if (prevPos.y >= th.Box.yMax - 0.3f && vy <= 0) { pos.y = th.Box.yMax; vy = 0; grounded = true; }
                        else if (phys.Mode == "ship" && prevPos.y + phys.Size <= th.Box.yMin + 0.3f && vy >= 0) { pos.y = th.Box.yMin - phys.Size; vy = 0; }
                        else Die();
                        break;
                    case "hazard": Die(); break;
                    case "pad": if (!th.Used) { vy = phys.PadVelocity; grounded = false; th.Used = true; } break;
                    case "orb": if (!th.Used && pressed) { vy = phys.OrbVelocity; grounded = false; th.Used = true; } break;
                    case "toShip":
                        if (phys.Mode != "ship") { phys = Mode("ship"); shipFloor = Mathf.Max(0, Mathf.Round(th.Box.center.y) - 5); shipCeil = shipFloor + 10; }
                        break;
                    case "toCube": if (phys.Mode != "cube") { phys = Mode("cube"); spin = 0; } break;
                }
                things[i] = th;
                if (deadTimer > 0) return;
            }
            if (grounded && phys.Mode == "cube") spin = Mathf.Round(spin / 90f) * 90f;
            if (pos.x >= endX) { Complete = true; music.Stop(); }
        }

        void Die()
        {
            if (deadTimer > 0) return;
            deadTimer = 0.6f;
            music.Stop();
        }

        public void Destroy() => arena.Destroy();
    }
}
