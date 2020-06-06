using HarmonyLib;
using RimWorld;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    // Alternative implementation used with copy-pasted WorkGiver

    /*[HarmonyPatch(typeof(WorkGiver_PlantsCut))]
    [HarmonyPatch(nameof(WorkGiver_PlantsCut.HasJobOnCell))]
    public static class Patch_WorkGiver_GrowerHarvest_HasJobOnCell
    {
        public static void Postfix(ref bool __result, Pawn pawn, IntVec3 c)
        {
            if (__result)
            {
                Plant plant = c.GetPlant(pawn.Map);
                if (plant.def.plant.IsTree)
                    __result = false;
            }
        }
    }*/
}