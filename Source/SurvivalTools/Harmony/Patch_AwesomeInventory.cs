using HarmonyLib;
using System.Reflection;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch]
    public static class Patch_JobGiver_AwesomeInventory_TakeArm
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("AwesomeInventory.Jobs.JobGiver_AwesomeInventory_TakeArm"), "Validator");
        }
        public static bool Prefix(ref bool __result, Thing ___thing)
        {
            if (___thing.def.IsSurvivalTool())
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}