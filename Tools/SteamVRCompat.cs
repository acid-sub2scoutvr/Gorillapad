using System.Runtime.CompilerServices;
using UnityEngine;

namespace Gorillapad.Tools
{
    /// <summary>
    /// Everything that touches SteamVR. The Oculus/Meta PC version of the game ships without parts of
    /// SteamVR, and any method that so much as mentions a SteamVR type fails there as soon as it runs.
    /// So SteamVR is only referenced from the small non-inlined methods below, and any failure means
    /// "SteamVR isn't available".
    /// </summary>
    public static class SteamVRCompat
    {
        static bool unavailable;

        public static bool Running
        {
            get
            {
                if (unavailable) return false;
                try { return IsRunning(); }
                catch { unavailable = true; return false; }
            }
        }

        public static string State
        {
            get
            {
                if (unavailable) return "unavailable";
                try { return GetState(); }
                catch { unavailable = true; return "unavailable"; }
            }
        }

        public static bool TryGetJoysticks(out Vector2 left, out Vector2 right)
        {
            left = right = Vector2.zero;
            if (unavailable) return false;
            try
            {
                ReadJoysticks(out left, out right);
                return true;
            }
            catch { unavailable = true; return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool IsRunning() =>
            Valve.VR.SteamVR.initializedState == Valve.VR.SteamVR.InitializedStates.InitializeSuccess;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetState() => Valve.VR.SteamVR.initializedState.ToString();

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ReadJoysticks(out Vector2 left, out Vector2 right)
        {
            left = Valve.VR.SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.axis;
            right = Valve.VR.SteamVR_Actions.gorillaTag_RightJoystick2DAxis.axis;
        }
    }
}
