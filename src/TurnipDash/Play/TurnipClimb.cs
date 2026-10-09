using System.Collections.Generic;
using System.Linq;
using TurnipDash.Content;
using TurnipDash.Generated;
using UnityEngine;

namespace TurnipDash.Play
{
    /// Turnip Mountain's two-hand climb: each hand grabs on its own button, the free hand is steered,
    /// and the turnip swings under whatever it holds. Rules from climb_rules, holds from climb_route.
    public sealed class TurnipClimb
    {
        sealed class Hand { public Vector2 Pos; public int Hold = -1; public Transform View; }

        readonly Arena arena;
        readonly ClimbRulesRow rule = Sheets.ClimbRules[0];
        readonly List<Vector2> holds;
        readonly Hand left = new Hand(), right = new Hand();
        readonly Transform body;
        Vector2 pos, vel;
        public int Falls;
        public bool Complete;
        public float Height => pos.y;

        public TurnipClimb(ContentStore store)
        {
            arena = new Arena("TurnipClimb", new Color(0.55f, 0.78f, 0.95f), 14f);
            holds = Sheets.ClimbRoute.Select(h => new Vector2(h.X, h.Y)).ToList();
            store.Sprites.TryGetValue("tm_rock", out var rock);
            store.Sprites.TryGetValue("tm_hand", out var hand);
            store.Sprites.TryGetValue("tm_turnip_body", out var turnip);
            arena.Add("cliff", null, new Vector2(0, rule.SummitY / 2), 1, new Color(0.45f, 0.38f, 0.32f), 0).transform.localScale = new Vector3(12f, rule.SummitY + 20f, 1);
            arena.Add("summit", null, new Vector2(0, rule.SummitY + 0.5f), 1, new Color(0.95f, 0.97f, 1f), 1).transform.localScale = new Vector3(12f, 1f, 1);
            for (int i = 0; i < holds.Count; i++)
            {
                var ledge = Sheets.ClimbRoute[i].Kind == "ledge";
                arena.Add("hold" + i, rock, holds[i], ledge ? 1.1f : 0.7f, rock != null ? Color.white : new Color(0.3f, 0.25f, 0.2f), 2);
            }
            body = arena.Add("turnip", turnip, Vector2.zero, 1.2f, turnip != null ? Color.white : new Color(0.95f, 0.85f, 0.95f), 5).transform;
            left.View = arena.Add("handL", hand, Vector2.zero, 0.45f, hand != null ? Color.white : new Color(0.85f, 0.75f, 0.55f), 6).transform;
            right.View = arena.Add("handR", hand, Vector2.zero, 0.45f, hand != null ? Color.white : new Color(0.85f, 0.75f, 0.55f), 6).transform;
            right.View.localScale = new Vector3(-right.View.localScale.x, right.View.localScale.y, 1);
            Respawn();
        }

        void Respawn()
        {
            vel = Vector2.zero;
            pos = holds[0] + new Vector2(0, -rule.Reach * 0.7f);
            left.Hold = 0; left.Pos = holds[0];
            right.Hold = -1; right.Pos = pos + new Vector2(0.6f, 0.3f);
        }

        public void Update(float dt, bool leftHeld, bool rightHeld, Vector2 stick)
        {
            if (Complete) return;
            Grip(left, leftHeld);
            Grip(right, rightHeld);

            // steer the free hand(s) around the body
            foreach (var h in new[] { left, right })
            {
                if (h.Hold >= 0) continue;
                h.Pos += stick * rule.HandSpeed * dt;
                var d = h.Pos - pos;
                if (d.magnitude > rule.Reach) h.Pos = pos + d.normalized * rule.Reach;
            }

            bool hanging = left.Hold >= 0 || right.Hold >= 0;
            vel += Vector2.down * rule.Gravity * dt;
            if (hanging) vel += stick * rule.PullStrength * dt; // pull yourself toward the stick while holding on
            vel *= Mathf.Pow(rule.Damping, dt * 60f);
            pos += vel * dt;

            // arms are ropes of length `reach` to every gripping hand
            foreach (var h in new[] { left, right })
            {
                if (h.Hold < 0) continue;
                var d = pos - h.Pos;
                if (d.magnitude > rule.Reach)
                {
                    var n = d.normalized;
                    pos = h.Pos + n * rule.Reach;
                    var outward = Vector2.Dot(vel, n);
                    if (outward > 0) vel -= n * outward;
                }
            }
            if (!hanging) foreach (var h in new[] { left, right }) h.Pos += vel * dt;

            if (pos.y < rule.FallResetY) { Falls++; Respawn(); }
            if (pos.y >= rule.SummitY) Complete = true;

            body.localPosition = pos;
            body.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(-vel.x * 4f, -30, 30));
            left.View.localPosition = left.Pos;
            right.View.localPosition = right.Pos;
            arena.Look(new Vector2(0, Mathf.Clamp(pos.y + 1.5f, 3f, rule.SummitY)));
        }

        void Grip(Hand h, bool held)
        {
            if (!held) { h.Hold = -1; return; }
            if (h.Hold >= 0) return;
            int best = -1; float bestD = rule.GrabRadius;
            for (int i = 0; i < holds.Count; i++)
            {
                var d = Vector2.Distance(h.Pos, holds[i]);
                if (d <= bestD) { bestD = d; best = i; }
            }
            if (best >= 0) { h.Hold = best; h.Pos = holds[best]; }
        }

        public void Destroy() => arena.Destroy();
    }
}
