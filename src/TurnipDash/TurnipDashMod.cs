using Modding;
using UnityEngine;

namespace TurnipDash
{
    /// Hollow Knight Modding API entry point. Everything else lives on the Director object.
    public class TurnipDashMod : Mod
    {
        internal static TurnipDashMod Instance;

        public TurnipDashMod() : base("Turnip Dash") { Instance = this; }

        public override string GetVersion() => "0.2.0";

        public override void Initialize()
        {
            var go = new GameObject("TurnipDash.Director");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Director>();
            Log("Turnip Dash ready: a Geometry Dash portal waits in King's Pass.");
        }

        internal static void Info(string m) { if (Instance != null) Instance.Log(m); else Debug.Log("[TurnipDash] " + m); }
        internal static void Warn(string m) { if (Instance != null) Instance.LogWarn(m); else Debug.LogWarning("[TurnipDash] " + m); }
    }
}
