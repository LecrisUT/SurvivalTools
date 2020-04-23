using HarmonyLib;
using System;
using Verse;
using Verse.AI;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch(typeof(Toils_Haul))]
    [HarmonyPatch(nameof(Toils_Haul.TakeToInventory))]
    [HarmonyPatch(new Type[] { typeof(TargetIndex), typeof(Func<int>) })]
    public static class Patch_Toils_Haul_TakeToInventory
    {
        public static void Postfix(Toil __result, TargetIndex ind)
        {
            Action initAction = __result.initAction;
            __result.initAction = () =>
            {
                initAction();
                Pawn actor = __result.actor;
                Thing thing = actor.CurJob.GetTarget(ind).Thing;
                if (thing is SurvivalTool && actor.CanUseSurvivalTools() && actor.inventory.Contains(thing))
                {
                    SurvivalTool tool = thing as SurvivalTool;
                    if (actor.CurJob.playerForced && tool.toBeForced)
                    {
                        actor.GetComp<Pawn_SurvivalToolAssignmentTracker>().forcedHandler.SetForced(thing, true);
                        tool.toBeForced = false;
                    }
                }
            };
        }
    }
}