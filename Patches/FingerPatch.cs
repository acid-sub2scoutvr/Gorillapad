using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine.InputSystem;

namespace Gorillapad.Patches
{
    //[HarmonyPatch]
    //public class ControllerPatch
    //{

    //    static IEnumerable<MethodBase> TargetMethods()a
    //    {
    //        return new MethodBase[] {
    //            AccessTools.Method("HandTransformFollowOffest:UpdatePositionRotation")
    //        };
    //    }

    //    private static bool Prefix(object __instance)
    //    {
    //        return !(Plugin.Enabled && !WalkSimulation.Instance.enabled);
    //    }
    //}

    [HarmonyPatch(typeof(ControllerInputPoller), "LateUpdate")]
    public class FingerPatch
    {
        public static bool forceLeftGrip, forceRightGrip;
        public static bool forceLeftTrigger, forceRightTrigger;
        public static bool forceLeftPrimary, forceRightPrimary;
        public static bool forceLeftSecondary, forceRightSecondary;

        static void Postfix(ControllerInputPoller __instance)
        {
            if (!Plugin.Instance.Enabled)
                return;

            if (forceLeftGrip)
            {
                __instance.leftControllerGripFloat = 1;
                __instance.leftGrab = true;
                __instance.leftGrabRelease = false;
            }
            if (forceRightGrip)
            {
                __instance.rightControllerGripFloat = 1;
                __instance.rightGrab = true;
                __instance.rightGrabRelease = false;
            }
            if (forceLeftTrigger)
                __instance.leftControllerIndexFloat = 1;
            if (forceRightTrigger)
                __instance.rightControllerIndexFloat = 1;
            if (forceLeftPrimary)
                __instance.leftControllerPrimaryButton = true;
            if (forceRightPrimary)
                __instance.rightControllerPrimaryButton = true;
            if (forceLeftSecondary)
                __instance.leftControllerSecondaryButton = true;
            if (forceRightSecondary)
                __instance.rightControllerSecondaryButton = true;
        }
    }
}

