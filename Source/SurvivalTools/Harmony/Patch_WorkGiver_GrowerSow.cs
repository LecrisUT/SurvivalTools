using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch(typeof(WorkGiver_GrowerSow))]
    [HarmonyPatch(nameof(WorkGiver_GrowerSow.JobOnCell))]
    public static class Patch_WorkGiver_GrowerSow_JobOnCell
    {
        public static void Postfix(ref Job __result, Pawn pawn)
        {
            if (__result?.def == JobDefOf.CutPlant && __result.targetA.Thing.def.plant.IsTree)
            {
                if (ST_WorkGiverDefOf.FellTrees.GetModExtension<WorkGiverExtension>().MeetsRequirementJobs(pawn))
                    __result = JobMaker.MakeJob(ST_JobDefOf.FellTree, __result.targetA);
                else
                    __result = null;
            }
        }
    }
}