using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch(typeof(Mineable))]
    [HarmonyPatch(nameof(Mineable.Notify_TookMiningDamage))]
    public static class Patch_Mineable_Notify_TookMiningDamage
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (instruction.opcode == OpCodes.Ldsfld && instruction.operand as FieldInfo == AccessTools.Field(typeof(StatDefOf), nameof(StatDefOf.MiningYield)))
                {
                    instruction.operand = AccessTools.Field(typeof(ST_StatDefOf), nameof(ST_StatDefOf.MiningYieldDigging));
                }

                yield return instruction;
            }
        }
    }
}