using System;
using System.Reflection;
using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection.Emit;
using Verse.AI;

namespace SurvivalTools.AutoPatcher
{
    static class JobDriver_StatReplace
    {
        private static readonly Type thisType = typeof(JobDriver_StatReplace);
        private static Type JobDriver;
        private static FieldInfo Job_Info;
        private static FieldInfo JobDriver_Info;
        private static bool ToolTypeInitialized = false;
        public static bool Initialize(StatPatchDef patch, Type jd, Type ntype = null)
        {
            if (!ToolTypeInitialized)
            {
                SurvivalToolType.allDefs.Do(t => t.Initialize());
                ToolTypeInitialized = true;
            }
            JobDriver = jd;
            JobDriver_Info = null;
            if (!patch.OtherTypes.NullOrEmpty() && patch.OtherTypes.Contains(jd))
            {
                JobDef job = patch.toolTypes.FirstOrFallback()?.jobList.FirstOrFallback();
                Job_Info = AccessTools.Field(typeof(ST_JobDefOf), job?.defName);
                goto Finish;
            }
            if (ntype != null)
            {
                List<FieldInfo> fields = AccessTools.GetDeclaredFields(ntype);
                JobDriver_Info = fields.FirstOrFallback(t => t.FieldType == JobDriver);
                if (JobDriver_Info == null)
                    return false;
            }
            Job_Info = AccessTools.Field(jd, "job");
        Finish:
            return Job_Info != null;
        }
        private static readonly MethodInfo GetStatValue_Info = AccessTools.Method(typeof(StatExtension), "GetStatValue");
        private static List<CodeInstruction> InsertedInstructions
        {
            get
            {
                if (JobDriver_Info == null)
                {
                    if (Job_Info.IsStatic)
                        return new List<CodeInstruction>()
                        {
                            new CodeInstruction(OpCodes.Ldsfld,Job_Info)
                        };
                    else
                        return new List<CodeInstruction>()
                        {
                            new CodeInstruction(OpCodes.Ldarg_0, null),
                            new CodeInstruction(OpCodes.Ldfld, Job_Info)
                        };
                }
                return new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_0, null),
                    new CodeInstruction(OpCodes.Ldfld, JobDriver_Info),
                    new CodeInstruction(OpCodes.Ldfld, Job_Info)
                };
            }
        }
        public static bool Transpile(ref List<CodeInstruction> instructions, int pos)
        {
            if (pos + 2 >= instructions.Count || 
                !instructions[pos + 2].Is(OpCodes.Call, GetStatValue_Info) ||
                !InstructionIsStatDef(instructions[pos]))
                return false;
            instructions[pos + 2].operand = thisGetStatValue_Info;
            instructions.InsertRange(pos + 1, InsertedInstructions);
            return true;
        }
        private static bool InstructionIsStatDef(CodeInstruction instruction)
        {
            if (instruction.operand is MethodInfo method && method.ReturnType.IsAssignableFrom(typeof(StatDef)))
                return true;
            if (instruction.operand is FieldInfo field && field.FieldType.IsAssignableFrom(typeof(StatDef)))
                return true;
            return false;
        }
        private static readonly MethodInfo thisGetStatValue_Info = AccessTools.Method(thisType, "GetStatValue");
        public static float GetStatValue(this Pawn pawn, StatDef stat, Job job, bool applyPostProcess = true)
        {
            JobDef jobDef = job.def;
            float val = pawn.GetStatValue(stat, applyPostProcess);
            if (pawn.GetToolTracker().usedHandler.BestTool.TryGetValue(jobDef, out SurvivalTool tool))
            {
                tool.TryGetJobValue(jobDef, stat, out float effect);
                return val * effect;
            }
            if (SurvivalToolType.allNoToolDrictionary.TryGetValue(jobDef, out List<StatModifier> modifiers))
            {
                return val * modifiers.GetStatFactorFromList(stat);
            }
            return val;
        }
    }
    public static class JobDriver_AddToolDegrade
    {
        public static bool Initialize(StatPatchDef stat, Type type)
        {
            return true;
        }
        public static void ChangeToil(ref Toil toil, JobDriver driver, StatDef stat)
        {
            Pawn pawn = toil.actor ?? driver.pawn;
            if (pawn == null)
            {
                Log.Error($"[SurvivalTools] Pawn not found: {toil} : {driver.job.def} : {driver} : {stat}");
                return;
            }
            toil.AddPreTickAction(delegate
            {
                pawn.TryDegradeTool(stat, driver.job.def);
            });
        }
        public static void TryDegradeTool(this Pawn pawn, StatDef stat, JobDef job)
        {
            if (SurvivalToolsSettings.ToolDegradation && pawn.GetToolTracker() is Pawn_SurvivalToolAssignmentTracker tracker &&
                tracker.usedHandler.BestTool.TryGetValue(job, out SurvivalTool tool) && tool.GetStatList().Contains(stat) && tool.def.useHitPoints)
            {
                LessonAutoActivator.TeachOpportunity(ST_ConceptDefOf.SurvivalToolDegradation, OpportunityType.GoodToKnow);
                tool.workTicksDone++;
                if (tool.workTicksDone >= tool.WorkTicksToDegrade)
                {
                    ST_Degrade(tool, pawn);
                    tool.workTicksDone = 0;
                }
            }
        }
        public static MethodInfo modDegrade;
        public static void ST_Degrade(Thing item, Pawn pawn)
        {
            if (modDegrade is null)
                item.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 1));
            else
                modDegrade.Invoke(null, new[] { item, pawn });
        }
    }
}
