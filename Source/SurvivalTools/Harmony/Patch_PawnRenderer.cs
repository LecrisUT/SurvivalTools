using HarmonyLib;
using System;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch(typeof(PawnRenderer))]
    [HarmonyPatch("CarryWeaponOpenly")]
    public static class Patch_PawnRenderer_CarryWeaponOpenly
    {
        public static bool Prefix(ref bool __result, Pawn ___pawn)
        {
            if (___pawn.GetToolTracker()?.drawTool == true)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}