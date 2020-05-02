using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System;
using System.Linq;
using Verse;

namespace SurvivalTools.AutoPatcher
{
    public static class PlantWork_AutoPatch
    {
        public static bool Initialize(Type jd, Type nType = null)
        {
            jobDriver = jd;
            field = null;
            propGetMethod = null;
            if (nType != null)
            {
                List<FieldInfo> fields = AccessTools.GetDeclaredFields(nType);
                field = fields.FirstOrDefault(t => t.FieldType == jobDriver);
                if (field == null)
                    return false;
            }
            propGetMethod = AccessTools.PropertyGetter(jobDriver, "Plant");
            if (propGetMethod == null)
                return false;
            return true;
        }
        private static FieldInfo field;
        private static Type jobDriver;
        private static MethodInfo propGetMethod;
        public static List<CodeInstruction> CodeInstructions
            => field == null ? new List<CodeInstruction>() {
                new CodeInstruction(OpCodes.Ldarg_0, null),
                new CodeInstruction(OpCodes.Call, propGetMethod),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlantWork_AutoPatch), "SpeedStat"))
            } : new List<CodeInstruction>() {
                new CodeInstruction(OpCodes.Ldarg_0, null),
                new CodeInstruction(OpCodes.Ldfld, field),
                new CodeInstruction(OpCodes.Call, propGetMethod),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlantWork_AutoPatch), "SpeedStat"))
            };
        public static StatDef SpeedStat(Plant plant)
        {
            if (plant.def.plant.IsTree)
                return ST_StatDefOf.PlantWorkSpeed_Felling_Tool;
            return ST_StatDefOf.PlantWorkSpeed_Harvesting_Tool;
        }
    }
}
