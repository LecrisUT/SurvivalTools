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
            float val = pawn.GetStatValue(stat, applyPostProcess);
            if (!pawn.CanUseSurvivalTools(out Pawn_SurvivalToolAssignmentTracker tracker))
                return val;
            JobDef jobDef = job.def;
            SurvivalTool tool = tracker.toolInUse;
            if (tool != null)
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
            Pawn_SurvivalToolAssignmentTracker tracker = pawn.GetToolTracker();
            if (tracker != null)
            {
                JobDef job = driver.job.def;
                toil.AddPreInitAction(delegate
                {
                    if (tracker.usedHandler.BestTool.TryGetValue(job, out SurvivalTool tool) && tool.GetStatList().Contains(stat))
                    {
                        tracker.toolInUse = tool;
                        if (TryGetOffHandEquipment == null)
                        {
                            tracker.memoryEquipment = pawn.equipment.Primary;
                            /*if (TryGetOffHandEquipment != null)
                            {
                                object[] param = new object[] { pawn.equipment, null };
                                if ((bool)TryGetOffHandEquipment.Invoke(null, param))
                                    tracker.memoryEquipmentOffHand = (ThingWithComps)param[1];
                            }*/
                            if (tracker.memoryEquipment != tool && tracker.memoryEquipmentOffHand != tool)
                            {
                                if (tracker.memoryEquipment != null)
                                    pawn.equipment.TryTransferEquipmentToContainer(tracker.memoryEquipment, pawn.inventory.innerContainer);
                                if (tracker.memoryEquipmentOffHand != null)
                                    pawn.equipment.TryTransferEquipmentToContainer(tracker.memoryEquipmentOffHand, pawn.inventory.innerContainer);
                                pawn.inventory.innerContainer.TryTransferToContainer(tool, pawn.equipment.GetDirectlyHeldThings());
                            }
                            tracker.drawTool = true;
                        }
                        if (tool.def.useHitPoints && SurvivalToolsSettings.ToolDegradation)
                            LessonAutoActivator.TeachOpportunity(ST_ConceptDefOf.SurvivalToolDegradation, OpportunityType.GoodToKnow);
                    }
                });
                if (SurvivalToolsSettings.ToolDegradation)
                    toil.AddPreTickAction(delegate
                    {
                        pawn.TryDegradeTool(tracker.toolInUse);
                    });
                toil.AddFinishAction(delegate
                {
                    tracker.drawTool = false;
                    SurvivalTool tool = tracker.toolInUse;
                    if (AddOffHandEquipment == null)
                    {
                        if (tracker.memoryEquipment != tool && tracker.memoryEquipmentOffHand != tool)
                        {
                            if (tool != null)
                                pawn.equipment.TryTransferEquipmentToContainer(pawn.equipment.Primary, pawn.inventory.innerContainer);
                            if (tracker.memoryEquipment != null)
                                pawn.inventory.innerContainer.TryTransferToContainer(tracker.memoryEquipment, pawn.equipment.GetDirectlyHeldThings());
                            if (tracker.memoryEquipmentOffHand != null)
                            {
                                TryGetOffHandEquipment.Invoke(null, new object[] { pawn.equipment, tracker.memoryEquipmentOffHand });
                            }
                            tracker.memoryEquipment = null;
                            tracker.memoryEquipmentOffHand = null;
                        }
                    }
                    tracker.toolInUse = null;
                });
            }
        }
        public static void TryDegradeTool(this Pawn pawn, SurvivalTool tool)
        {
            if (tool != null && tool.def.useHitPoints)
            {
                tool.workTicksDone++;
                if (tool.workTicksDone >= tool.WorkTicksToDegrade)
                {
                    ST_Degrade(tool, pawn);
                    tool.workTicksDone = 0;
                }
            }
        }
        public static MethodInfo AddOffHandEquipment;
        public static MethodInfo TryGetOffHandEquipment;
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
