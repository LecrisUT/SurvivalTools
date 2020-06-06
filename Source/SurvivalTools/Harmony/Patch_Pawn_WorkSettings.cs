using HarmonyLib;
using RimWorld;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_WorkSettings))]
    [HarmonyPatch(nameof(Pawn_WorkSettings.SetPriority))]
    public static class Patch_Pawn_WorkSettings
    {
        public static void Postfix(Pawn ___pawn)
        {
            ___pawn.GetToolTracker().dirtyCache = true;
        }
    }
}