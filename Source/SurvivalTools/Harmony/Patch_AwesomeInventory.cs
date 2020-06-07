using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    /*[HarmonyPatch]
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
    }*/
    [HarmonyPatch]
    public static class Patch_JobGiver_AwesomeInventory_TakeArm_Backup
    {
        private static Type thisType = typeof(Patch_JobGiver_AwesomeInventory_TakeArm_Backup);
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("AwesomeInventory.Jobs.JobGiver_AwesomeInventory_TakeArm"), "TryGiveJob");
        }
        public static Type Type_JobGiver_FindItemByRadius;
        public static MethodInfo Method_JobGiver_FindItemByRadius_FindItem;
        public static bool Prepare()
        {
            Type_JobGiver_FindItemByRadius = AccessTools.TypeByName("AwesomeInventory.Jobs.JobGiver_FindItemByRadius");
            Method_JobGiver_FindItemByRadius_FindItem = AccessTools.Method(Type_JobGiver_FindItemByRadius, "FindItem");
            if (Type_JobGiver_FindItemByRadius == null || Method_JobGiver_FindItemByRadius_FindItem == null)
                return false;
            return true;
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if (instruction.Calls(Method_JobGiver_FindItemByRadius_FindItem))
                {
                    int j = -1;
                    while (instructionList[i + j].opcode != OpCodes.Ldarg_0)
                    {
                        if (instructionList[i + j].opcode == OpCodes.Ldnull)
                        {
                            instructionList[i + j] = new CodeInstruction(OpCodes.Ldsfld, Field_NotSurvivalTool);
                            goto Found;
                        }
                        j -= 1;
                    }
                }
            }
        Found:
            return instructionList;
        }
        private static FieldInfo Field_NotSurvivalTool = AccessTools.Field(thisType, "NotSurvivalTool");
        public static Func<Thing, bool> NotSurvivalTool = t => !t.def.IsSurvivalTool();
    }
}