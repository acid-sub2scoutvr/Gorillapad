using HarmonyLib;
using Gorillapad.Animators;

namespace Gorillapad.Patches
{
    /// <summary>
    /// The game only grabs an item whose interaction point its hand collider is overlapping, which
    /// doesn't always register for the mod's hands. While a mode (glider, hoverboard) is reaching for
    /// a handle, it sets these, and the handle is slipped into the overlap list right before the game
    /// checks it, so the normal grab runs.
    /// </summary>
    [HarmonyPatch(typeof(EquipmentInteractor), "LateUpdate")]
    public class EquipmentInteractorPatch
    {
        public static InteractionPoint injectLeft, injectRight;

        static void Prefix(EquipmentInteractor __instance)
        {
            if (!Plugin.Instance.Enabled) return;
            var left = injectLeft;
            var right = injectRight;
            if (left && !__instance.overlapInteractionPointsLeft.Contains(left))
                __instance.overlapInteractionPointsLeft.Add(left);
            if (right && !__instance.overlapInteractionPointsRight.Contains(right))
                __instance.overlapInteractionPointsRight.Add(right);
        }
    }
}
