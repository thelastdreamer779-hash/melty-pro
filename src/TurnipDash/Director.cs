using System.Collections.Generic;
using System.Linq;
using TurnipDash.Content;
using TurnipDash.Generated;
using TurnipDash.Play;
using UnityEngine;

namespace TurnipDash
{
    /// Walks the stages sheet: portal in King's Pass -> Stereo Madness as the turnip -> Turnip Mountain climb -> back.
    public sealed class Director : MonoBehaviour
    {
        StagesRow stage;
        ContentStore store;
        GameObject portal;
        float sceneTimer, startTime, messageUntil;
        string message;
        bool prevUp, prevR, nearPortal;
        DashRun run;
        TurnipClimb climb;
        readonly List<AudioSource> paused = new List<AudioSource>();

        static StagesRow Stage(string id) => Sheets.Stages.First(s => s.Id == id);

        void Start() { stage = Stage("portal"); }

        void Say(string text, float seconds) { message = text; messageUntil = Time.unscaledTime + seconds; }

        void Update()
        {
            switch (stage.Kind)
            {
                case "portal": UpdatePortal(); break;
                case "run": UpdateRun(); break;
                case "climb": UpdateClimb(); break;
                case "return": Return(); break;
            }
        }

        void UpdatePortal()
        {
            if (!HkBridge.InKingsPass()) { sceneTimer = 0; if (portal) Destroy(portal); return; }
            var hero = HkBridge.Hero();
            if (hero == null) return;
            sceneTimer += Time.deltaTime;
            if (store == null) { store = new ContentStore(); store.Begin(); }
            if (store.Done && store.Problems.Count == 0 && store.Music == null && store.Sprites.Count == 0) store.Finish();

            if (portal == null && sceneTimer > 1.5f)
            {
                portal = new GameObject("TurnipDash.Portal");
                portal.transform.position = hero.transform.position + new Vector3(5f, 1.2f, 0.01f);
                var r = portal.AddComponent<SpriteRenderer>();
                r.sprite = Arena.White;
                r.color = new Color(0.4f, 1f, 0.4f, 0.8f);
                portal.transform.localScale = new Vector3(1.2f, 3f, 1);
            }
            if (portal == null) return;
            var pr = portal.GetComponent<SpriteRenderer>();
            if (pr.sprite == Arena.White && store.Sprites.TryGetValue("gd_frame_portal", out var ps))
            {
                pr.sprite = ps; pr.color = Color.white;
                portal.transform.localScale = Vector3.one * (3f / Mathf.Max(ps.bounds.size.x, ps.bounds.size.y));
                if (store.Rotated.Contains("gd_frame_portal")) portal.transform.rotation = Quaternion.Euler(0, 0, 90);
            }

            nearPortal = Vector2.Distance(hero.transform.position, portal.transform.position) < 2.2f;
            bool up = HkBridge.Held("btn_up");
            if (nearPortal && up && !prevUp)
            {
                if (!store.Done) Say("The portal is still reading your games...", 2f);
                else if (store.Problems.Count > 0) { Say(string.Join("\n", store.Problems.ToArray()), 8f); store = null; }
                else Enter(Stage(stage.Next));
            }
            prevUp = up;
        }

        void Enter(StagesRow next)
        {
            stage = next;
            Say(Text.Get(next.EnterText), 5f);
            switch (next.Kind)
            {
                case "run":
                    startTime = Time.time;
                    HkBridge.FreezeHero(true);
                    paused.Clear();
                    foreach (var a in FindObjectsOfType<AudioSource>()) if (a.isPlaying) { a.Pause(); paused.Add(a); }
                    if (store.TmArtMissing) Say(Text.Get("ui_tm_art_missing") + "\n" + Text.Get(next.EnterText), 8f);
                    run = new DashRun(store);
                    break;
                case "climb":
                    run?.Destroy(); run = null;
                    climb = new TurnipClimb(store);
                    break;
            }
        }

        void UpdateRun()
        {
            bool r = HkBridge.Held("btn_quick_restart");
            run.Update(Time.deltaTime, HkBridge.Held("btn_jump"), r && !prevR);
            prevR = r;
            if (run.Complete) Enter(Stage(stage.Next));
        }

        void UpdateClimb()
        {
            var stick = new Vector2((HkBridge.Held("btn_right") ? 1 : 0) - (HkBridge.Held("btn_left") ? 1 : 0),
                                    (HkBridge.Held("btn_up") ? 1 : 0) - (HkBridge.Held("btn_down") ? 1 : 0));
            if (stick.sqrMagnitude > 1) stick.Normalize();
            var rule = Sheets.ClimbRules[0];
            climb.Update(Time.deltaTime, HkBridge.Held("btn_" + rule.LeftGrip), HkBridge.Held("btn_" + rule.RightGrip), stick);
            if (climb.Complete) Enter(Stage(stage.Next));
        }

        void Return()
        {
            climb?.Destroy(); climb = null;
            HkBridge.FreezeHero(false);
            foreach (var a in paused) if (a) a.UnPause();
            paused.Clear();
            var t = Time.time - startTime;
            Say(Text.Get(stage.EnterText, $"{(int)(t / 60)}:{t % 60:00.0}"), 8f);
            stage = Stage("portal");
        }

        GUIStyle style;
        void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, Screen.height / 30), alignment = TextAnchor.UpperCenter, wordWrap = true };
            var area = new Rect(Screen.width * 0.1f, Screen.height * 0.05f, Screen.width * 0.8f, Screen.height * 0.4f);
            string line = null;
            if (stage.Kind == "run" && run != null) line = Text.Get("ui_attempt", run.Attempt, Mathf.FloorToInt(run.Progress * 100));
            else if (stage.Kind == "climb" && climb != null) line = $"{Mathf.Max(0, climb.Height):0.0} / {Sheets.ClimbRules[0].SummitY:0} m";
            else if (stage.Kind == "portal" && nearPortal && portal != null) line = Text.Get(stage.EnterText);
            if (Time.unscaledTime < messageUntil && message != null) line = line == null ? message : message + "\n" + line;
            if (line == null) return;
            var shadow = new Rect(area.x + 2, area.y + 2, area.width, area.height);
            var c = GUI.color;
            GUI.color = Color.black; GUI.Label(shadow, line, style);
            GUI.color = Color.white; GUI.Label(area, line, style);
            GUI.color = c;
        }
    }
}
