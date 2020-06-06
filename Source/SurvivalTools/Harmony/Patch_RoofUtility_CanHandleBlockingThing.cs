using HarmonyLib;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch(typeof(RoofUtility))]
    [HarmonyPatch(nameof(RoofUtility.CanHandleBlockingThing))]
    public static class Patch_RoofUtility_CanHandleBlockingThing
    {
        public static void Postfix(ref bool __result, Thing blocker, Pawn worker)
        {
            if (blocker?.def.plant?.IsTree == true && !ST_WorkGiverDefOf.FellTrees.GetModExtension<WorkGiverExtension>().MeetsRequirementJobs(worker))
                __result = false;
        }
    }
}