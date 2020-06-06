using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SurvivalTools.HarmonyPatches
{
    // Alternative implementation used with copy-pasted WorkGiver

    /*[HarmonyPatch(typeof(WorkGiver_PlantsCut))]
    [HarmonyPatch(nameof(WorkGiver_PlantsCut.JobOnThing))]
    public static class Patch_WorkGiver_PlantsCut_JobOnThing
    {
        public static bool Prefix(ref Job __result, Thing t, Pawn pawn)
        {
            if (t.def.plant?.IsTree == true)
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(WorkGiver_PlantsCut))]
    [HarmonyPatch(nameof(WorkGiver_PlantsCut.PotentialWorkThingsGlobal))]
    public static class Patch_WorkGiver_PlantsCut_PotentialWorkThingsGlobal
    {
        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values)
        {
            foreach (Thing thing in values)
                if (!thing.def.plant.IsTree)
                    yield return thing;
        }
    }*/
}