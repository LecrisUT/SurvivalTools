using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch(typeof(ThingDef))]
    [HarmonyPatch(nameof(ThingDef.SpecialDisplayStats))]
    public static class Patch_ThingDef_SpecialDisplayStats
    {
        public static void Postfix(ThingDef __instance, ref IEnumerable<StatDrawEntry> __result, StatRequest req)
        {
            // Tool def
            /*if (req.Thing == null && __instance.IsSurvivalTool(out SurvivalToolProperties tProps))
            {
                foreach (SurvivalToolTypeModifier modifier in tProps.GetToolTypesValue())
                    __result = __result.AddItem(new StatDrawEntry(ST_StatCategoryDefOf.SurvivalTool,
                    modifier.toolType.LabelCap,
                    modifier.value.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor),
                    reportText: modifier.toolType.description,
                    displayPriorityWithinCategory: 99999,
                    overrideReportTitle: SurvivalToolUtility.GetSurvivalToolOverrideReportText(this, modifier.stat),
                    hyperlinks: null,
                    forceUnfinalizedMode: false
                          ));
            }

            // Stuff
            /*if (__instance.IsStuff && __instance.GetModExtension<StuffPropsTool>() is StuffPropsTool sPropsTool)
            {
                foreach (SurvivalToolTypeModifier modifier in sPropsTool.toolTypeFactors)
                    __result = __result.AddItem(new StatDrawEntry(ST_StatCategoryDefOf.SurvivalToolMaterial,
                        modifier.toolType.LabelCap,
                        modifier.value.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor),
                        reportText: modifier.toolType.description,
                        displayPriorityWithinCategory: 99999));
            }*/
        }
    }
}