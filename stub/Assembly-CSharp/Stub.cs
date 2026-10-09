// Compile-time stand-in for the Hollow Knight Modding API's Mod class (hk-modding/api 1.5.78.11833-77,
// Assembly-CSharp/Mod.cs and Loggable.cs). Only the members TurnipDash uses, with the same names and
// signatures, so the built DLL binds to the real Assembly-CSharp.dll in the player's game. Never shipped.
using System;
namespace Modding
{
    public abstract class Loggable
    {
        protected Loggable() { }
        public void Log(string message) => throw new NotImplementedException();
        public void LogWarn(string message) => throw new NotImplementedException();
        public void LogError(string message) => throw new NotImplementedException();
    }

    public abstract class Mod : Loggable
    {
        protected Mod(string name = null) { }
        public virtual void Initialize() { }
        public virtual string GetVersion() => throw new NotImplementedException();
    }
}
