using HarmonyLib;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    public static class Patch_Pawn_InventoryTracker
    {
        [HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.FirstUnloadableThing), MethodType.Getter)]
        public static class FirstUnloadableThing
        {
            public static void Postfix(Pawn_InventoryTracker __instance, ref ThingCount __result)
            {
                if (__result.Thing is SurvivalTool tool && (tool.InUse || tool.Forced))
                {
                    bool foundNewThing = false;
                    // Had to iterate through because a lambda expression in this case isn't possible
                    for (int i = 0; i < __instance.innerContainer.Count; i++)
                    {
                        Thing newThing = __instance.innerContainer[i];
                        if (newThing as SurvivalTool == null || (!((SurvivalTool)newThing).InUse && !((SurvivalTool)newThing).Forced))
                        {
                            __result = new ThingCount(newThing, newThing.stackCount);
                            foundNewThing = true;
                            break;
                        }
                    }
                    if (!foundNewThing)
                        __result = default;
                }
            }
        }
    }
}