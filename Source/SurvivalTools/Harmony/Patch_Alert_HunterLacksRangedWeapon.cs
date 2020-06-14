using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;
using Verse.AI;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch]
    public static class Patch_Alert_HunterLacksRangedWeapon
    {
        public static MethodBase TargetMethod()
            => AccessTools.PropertyGetter(typeof(Alert_HunterLacksRangedWeapon), "HuntersWithoutRangedWeapon");
        public static void Postfix(ref List<Pawn> __result)
        {
            __result.RemoveAll(t => t.GetToolTracker().drawTool);
        }
    }
}