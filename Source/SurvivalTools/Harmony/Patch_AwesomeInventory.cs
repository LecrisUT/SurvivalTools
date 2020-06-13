using HarmonyLib;
using System;
using System.Reflection;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch]
    public static class Patch_JobGiver_AwesomeInventory_TakeArm
    {
        private static Type JobGiver_AwesomeInventory_TakeArm;
        public static bool Prepare()
        {
            JobGiver_AwesomeInventory_TakeArm = AccessTools.TypeByName("AwesomeInventory.Jobs.JobGiver_AwesomeInventory_TakeArm");
            return JobGiver_AwesomeInventory_TakeArm != null;
        }
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(JobGiver_AwesomeInventory_TakeArm, "Validator");
        }
        public static bool Prefix(ref bool __result, Thing thing)
        {
            if (thing.def.IsSurvivalTool())
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}