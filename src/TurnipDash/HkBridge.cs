using System;
using System.Collections.Generic;
using System.Reflection;
using TurnipDash.Generated;
using UnityEngine;

namespace TurnipDash
{
    /// Every touch point with Hollow Knight, driven by the hooks sheet and read by reflection,
    /// with each row's keyboard fallback when a game member can't be found.
    static class HkBridge
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static readonly Dictionary<string, HooksRow> rows = new Dictionary<string, HooksRow>();
        static readonly HashSet<string> warned = new HashSet<string>();

        static HooksRow Row(string id)
        {
            if (rows.Count == 0) foreach (var r in Sheets.Hooks) rows[r.Id] = r;
            return rows[id];
        }

        static Type T(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name, false);
                if (t != null) return t;
            }
            return null;
        }

        static void WarnOnce(string id, string what)
        {
            if (warned.Add(id)) TurnipDashMod.Warn($"hook {id}: {what}; using fallback");
        }

        static object StaticMember(string id)
        {
            var r = Row(id);
            var t = T(r.TypeName);
            if (t == null) { WarnOnce(id, "type " + r.TypeName + " not found"); return null; }
            var p = t.GetProperty(r.Member, Any);
            if (p != null) return p.GetValue(null, null);
            var f = t.GetField(r.Member, Any);
            if (f != null) return f.GetValue(null);
            WarnOnce(id, "member " + r.Member + " not found");
            return null;
        }

        static object Member(object target, string id)
        {
            if (target == null) return null;
            var r = Row(id);
            var t = target.GetType();
            var f = t.GetField(r.Member, Any);
            if (f != null) return f.GetValue(target);
            var p = t.GetProperty(r.Member, Any);
            if (p != null) return p.GetValue(target, null);
            WarnOnce(id, "member " + r.Member + " not found");
            return null;
        }

        static void Call(object target, string id)
        {
            if (target == null) return;
            var r = Row(id);
            var m = target.GetType().GetMethod(r.Member, Any, null, Type.EmptyTypes, null);
            if (m == null) { WarnOnce(id, "method " + r.Member + " not found"); return; }
            try { m.Invoke(target, null); } catch (Exception e) { TurnipDashMod.Warn($"hook {id}: {e.InnerException?.Message ?? e.Message}"); }
        }

        public static Component Hero()
        {
            var h = StaticMember("hero_instance") as Component;
            if (h != null) return h;
            var go = GameObject.Find(Row("hero_instance").Fallback.Split(':')[1]);
            return go != null ? go.transform as Component : null;
        }

        public static bool InKingsPass() =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Row("scene_kings_pass").Member;

        public static void FreezeHero(bool freeze)
        {
            var hero = StaticMember("hero_instance");
            if (freeze) { Call(hero, "hero_relinquish"); Call(hero, "hero_anim_stop"); }
            else { Call(hero, "hero_anim_start"); Call(hero, "hero_regain"); }
            var pd = StaticMember("pd_instance");
            if (pd != null)
            {
                var f = pd.GetType().GetField(Row("pd_invincible").Member, Any);
                if (f != null) f.SetValue(pd, freeze); else WarnOnce("pd_invincible", "field not found");
            }
            var c = hero as Component;
            if (c != null)
            {
                foreach (var r in c.GetComponentsInChildren<Renderer>()) r.enabled = !freeze;
                var rb = c.GetComponent<Rigidbody2D>();
                if (rb != null) { rb.velocity = Vector2.zero; rb.isKinematic = freeze; }
            }
        }

        /// Is the game's bound button for this hooks row held? Reads the player's own bindings
        /// (InputHandler.Instance.inputActions.<action>.IsPressed) and falls back to the row's key.
        public static bool Held(string id)
        {
            var r = Row(id);
            if (r.Kind == "key") return Input.GetKey(Key(r.Member));
            var actions = Member(StaticMember("input_handler"), "input_actions");
            var action = Member(actions, id);
            if (action != null)
            {
                var p = action.GetType().GetProperty("IsPressed", BindingFlags.Public | BindingFlags.Instance);
                if (p != null) return (bool)p.GetValue(action, null) || Input.GetKey(Key(r.Fallback));
            }
            return Input.GetKey(Key(r.Fallback));
        }

        static KeyCode Key(string name)
        {
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), name); } catch { return KeyCode.None; }
        }
    }
}
