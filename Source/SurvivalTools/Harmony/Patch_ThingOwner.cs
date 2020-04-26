using HarmonyLib;
using System;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch(typeof(ThingOwner<Thing>))]
    [HarmonyPatch(nameof(ThingOwner<Thing>.TryAdd))]
    [HarmonyPatch(new Type[] { typeof(Thing), typeof(bool) })]
    public static class Patch_ThingOwner_TryAdd
    {
        public static void Postfix(ThingOwner<Thing> __instance, bool __result, Thing item)
        {
            if (__result == true && item is SurvivalTool tool && item != null)
            {
                Pawn pawn = null;
                if (__instance.Owner is Pawn_EquipmentTracker eq)
                    pawn = eq.pawn;
                if (__instance.Owner is Pawn_InventoryTracker inv)
                    pawn = inv.pawn;
                if (pawn?.CanUseSurvivalTools() == true)
                {
                    Pawn_SurvivalToolAssignmentTracker tracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
                    if (pawn.CurJob?.playerForced == true && tool.toBeForced)
                        tool.Forced = true;
                    if (tracker != null)
                    {
                        if (tool.Forced)
                            tracker.forcedHandler.ForcedTools.AddDistinct(tool);
                        tool.CheckIfUsed(tracker, true);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(ThingOwner))]
    [HarmonyPatch(nameof(ThingOwner.TryDrop))]
    [HarmonyPatch(new Type[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(ThingPlaceMode), typeof(Thing), typeof(Action<Thing,int>), typeof(Predicate<IntVec3>)}, 
        new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal })]
    public static class Patch_ThingOwner_TryDrop
    {
        public static void Postfix(ThingOwner __instance, bool __result, Thing thing)
        {
            if (__result == true && thing is SurvivalTool tool && thing != null)
            {
                Pawn pawn = null;
                if (__instance.Owner is Pawn_EquipmentTracker eq)
                    pawn = eq.pawn;
                if (__instance.Owner is Pawn_InventoryTracker inv)
                    pawn = inv.pawn;
                if (pawn?.CanUseSurvivalTools() == true)
                {
                    tool.Forced = false;
                    tool.InUse = false;
                    Pawn_SurvivalToolAssignmentTracker tracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
                    if (tracker != null)
                    {
                        if (tracker.ToolsInUse?.Contains(tool) == true)
                            tracker.ToolsInUse.Remove(tool);
                        if (tracker.forcedHandler?.ForcedTools?.Contains(tool) == true)
                            tracker.forcedHandler.ForcedTools.Remove(tool);
                    }
                }
            }
        }
    }
}