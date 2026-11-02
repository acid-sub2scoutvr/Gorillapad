using GorillaGameModes;
using GorillaNetworking;
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;

namespace Gorillapad.Patches
{
    /// <summary>
    /// When a zone doesn't support the selected modded game mode's base type (e.g. Modded Infection
    /// in the city), Utilla swaps in the zone's supported mode but drops the modded flag, which sends
    /// you to a public lobby. Put the modded flag back by mapping to Utilla's modded equivalent.
    /// </summary>
    [HarmonyPatch(typeof(GorillaNetworkJoinTrigger), nameof(GorillaNetworkJoinTrigger.GetDesiredGameType))]
    public class JoinTriggerPatch
    {
        static PropertyInfo managerInstance;
        static FieldInfo moddedPerMode;
        static PropertyInfo gamemodeId;

        static void Postfix(ref string __result)
        {
            try
            {
                string selected = GorillaComputer.instance.currentGameMode.Value;
                if (selected is null || !selected.ToUpper().Contains("MODDED")) return;
                if (__result is null || __result.ToUpper().Contains("MODDED")) return;
                if (!Enum.TryParse(__result, out GameModeType baseMode)) return;

                if (managerInstance == null)
                {
                    var managerType = AccessTools.TypeByName("Utilla.Behaviours.GamemodeManager");
                    managerInstance = AccessTools.Property(managerType, "Instance");
                    moddedPerMode = AccessTools.Field(managerType, "ModdedGamemodesPerMode");
                    gamemodeId = AccessTools.Property(AccessTools.TypeByName("Utilla.Models.Gamemode"), "ID");
                }
                var modded = (IDictionary)moddedPerMode.GetValue(managerInstance.GetValue(null));
                if (!modded.Contains(baseMode)) return;

                string moddedId = (string)gamemodeId.GetValue(modded[baseMode]);
                Logging.Info($"Join trigger wanted {__result} with {selected} selected; using {moddedId}");
                __result = moddedId;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }
    }
}
