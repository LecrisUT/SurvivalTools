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
            if (___pawn?.TryGetComp<ThingComp_WorkSettings>()?.WorkSettingsChanged == false)
                ___pawn.GetComp<ThingComp_WorkSettings>().WorkSettingsChanged = true;
        }
    }
}