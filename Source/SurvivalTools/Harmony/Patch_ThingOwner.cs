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
            if (__result == true && item is SurvivalTool tool)
            {
                Pawn pawn = null;
                if (__instance.Owner is Pawn_EquipmentTracker eq)
                    pawn = eq.pawn;
                if (__instance.Owner is Pawn_InventoryTracker inv)
                    pawn = inv.pawn;
                if (pawn?.CanUseSurvivalTools() == true)
                {
                    Pawn_SurvivalToolAssignmentTracker assignmentTracker = pawn.GetToolTracker();
                    if (assignmentTracker != null)
                    {
                        assignmentTracker.usedHandler.dirtyCache = true;
                        // assignmentTracker.usedHandler.Update();
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(ThingOwner))]
    [HarmonyPatch(nameof(ThingOwner.TryDrop_NewTmp))]
    public static class Patch_ThingOwner_TryDrop_NewTmp
    {
        public static void Postfix(ThingOwner __instance, bool __result, Thing thing)
        {
            if (__result == true && thing is SurvivalTool tool)
            {
                Pawn pawn = null;
                if (__instance.Owner is Pawn_EquipmentTracker eq)
                    pawn = eq.pawn;
                if (__instance.Owner is Pawn_InventoryTracker inv)
                    pawn = inv.pawn;
                if (__instance.Owner is Pawn_CarryTracker carry)
                    pawn = carry.pawn;
                if (pawn?.CanUseSurvivalTools() == true)
                {
                    Pawn_SurvivalToolAssignmentTracker assignmentTracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
                    if (assignmentTracker != null)
                    {
                        assignmentTracker.usedHandler.SetUsed(tool, false);
                        assignmentTracker.forcedHandler.SetForced(tool, false);
                        assignmentTracker.usedHandler.dirtyCache = true;
                        // assignmentTracker.usedHandler.Update();
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(ThingOwner))]
    [HarmonyPatch(nameof(ThingOwner.TryTransferToContainer))]
    [HarmonyPatch(new Type[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(Thing), typeof(bool) },
        new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    public static class Patch_ThingOwner_TryTransferToContainer
    {
        public static void Postfix(ThingOwner __instance, int __result, Thing item, ThingOwner otherContainer)
        {
            if (__result >0 && item is SurvivalTool tool)
            {
                Pawn pawn = null;
                if (__instance.Owner is Pawn_EquipmentTracker eq)
                    pawn = eq.pawn;
                if (__instance.Owner is Pawn_InventoryTracker inv)
                    pawn = inv.pawn;
                if (__instance.Owner is Pawn_CarryTracker carry)
                    pawn = carry.pawn;
                if (pawn?.CanUseSurvivalTools() == true)
                {
                    Pawn_SurvivalToolAssignmentTracker assignmentTracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
                    if (assignmentTracker != null)
                    {
                        assignmentTracker.usedHandler.SetUsed(tool, false);
                        assignmentTracker.forcedHandler.SetForced(tool, false);
                        assignmentTracker.usedHandler.dirtyCache = true;
                        // assignmentTracker.usedHandler.Update();
                    }
                }
                if (otherContainer.Owner is Pawn_EquipmentTracker || otherContainer.Owner is Pawn_InventoryTracker)
                {
                    Pawn otherPawn = null;
                    if (otherContainer.Owner is Pawn_EquipmentTracker otherEq)
                        otherPawn = otherEq.pawn;
                    if (otherContainer.Owner is Pawn_InventoryTracker otherInv)
                        otherPawn = otherInv.pawn;
                    if (pawn?.CanUseSurvivalTools() == true)
                    {
                        Pawn_SurvivalToolAssignmentTracker assignmentTracker = otherPawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
                        if (assignmentTracker != null)
                        {
                            assignmentTracker.usedHandler.dirtyCache = true;
                            // assignmentTracker.usedHandler.Update();
                        }
                    }
                }
            }
        }
    }
}