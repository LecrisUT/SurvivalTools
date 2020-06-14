using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SurvivalTools
{
    public static class SurvivalToolUtility
    {
        public static readonly FloatRange MapGenToolHitPointsRange = new FloatRange(0.3f, 0.7f);
        public const float MapGenToolMaxStuffMarketValue = 3f;

        public static List<WorkGiverDef> SurvivalToolWorkGivers { get; } =
            DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(w => w.HasModExtension<WorkGiverExtension>()).ToList();

        public static bool IsSurvivalTool(this BuildableDef def, out SurvivalToolProperties toolProps)
        {
            toolProps = def.GetModExtension<SurvivalToolProperties>();
            return def.IsSurvivalTool();
        }

        public static bool IsSurvivalTool(this BuildableDef def) =>
            def is ThingDef tDef && tDef.thingClass == typeof(SurvivalTool) && tDef.HasModExtension<SurvivalToolProperties>();

        public static bool CanUseSurvivalTools(this Pawn pawn) =>
            pawn.RaceProps.intelligence >= Intelligence.ToolUser && pawn.Faction == Faction.OfPlayer &&
            (pawn.equipment != null || pawn.inventory != null) && pawn.TraderKind == null &&
            !(MiscDef.IgnoreRaceList.Find(t => t.race == pawn.kindDef.race)?.all == true);
        public static bool CanUseSurvivalTools(this Pawn pawn, out Pawn_SurvivalToolAssignmentTracker tracker)
            => (tracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>()) != null;

        public static IEnumerable<SurvivalTool> GetHeldSurvivalTools(this Pawn pawn)
        {
            if (pawn.equipment != null)
                foreach (SurvivalTool tool in pawn.equipment.GetDirectlyHeldThings().OfType<SurvivalTool>())
                    yield return tool;
            if (pawn.inventory != null)
                foreach (SurvivalTool tool in pawn.inventory.innerContainer.OfType<SurvivalTool>())
                    yield return tool;
        }

        // Save a list in assingment tracker
        public static bool CanUseSurvivalTool(this Pawn pawn, SurvivalToolType toolType)
            => pawn.GetToolTracker().CurrentSurvivalToolAssignment.CanUseToolType(toolType);
        public static bool CanUseSurvivalTool(this Pawn pawn, SurvivalTool tool)
            => pawn.GetToolTracker().CurrentSurvivalToolAssignment.CanUseTool(tool);

        public static int GetMaxTools(this Pawn pawn)
        {
            if (!SurvivalToolsSettings.toolLimit)
                return int.MaxValue;
            return Mathf.RoundToInt(pawn.GetStatValue(ST_StatDefOf.SurvivalToolCarryCapacity, false));
        }

        public static string GetSurvivalToolOverrideReportText(SurvivalTool tool, SurvivalToolTypeModifier modifier)
        {
            StringBuilder builder = new StringBuilder();
            SurvivalToolType toolType = modifier.toolType;
            builder.AppendLine(toolType.description);

            builder.AppendLine();
            builder.AppendLine(tool.def.LabelCap + ": " + tool.GetToolProperties().GetToolTypesValue().GetValue(toolType).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Factor));

            builder.AppendLine();
            builder.AppendLine(ST_StatDefOf.ToolEffectivenessFactor.LabelCap + ": " +
                tool.GetStatValue(ST_StatDefOf.ToolEffectivenessFactor, false).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Factor));

            ThingDef stuff = tool.Stuff;
            if (stuff != null)
            {
                float val = toolType.StuffEffect(stuff);
                builder.AppendLine();
                builder.AppendLine("StatsReport_Material".Translate() + " (" + stuff.LabelCap + "): " +
                    val.ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Factor));
            }

            builder.AppendLine();
            builder.AppendLine("StatsReport_FinalValue".Translate() + ": " + modifier.value.ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Factor));
            return builder.ToString();
        }

        public static bool NeedsSurvivalTool(this Pawn pawn, SurvivalTool tool)
        {
            Pawn_SurvivalToolAssignmentTracker tracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
            foreach (SurvivalToolType toolType in tool.GetToolProperties().toolTypes)
                if (tracker.RequiredToolTypes.Contains(toolType))
                    return true;
            return false;
        }

        public static bool BetterThanWorkingToolless(this SurvivalTool tool)
        {
            foreach (JobDef job in tool.GetToolProperties().jobList)
            {
                bool flag = true;
                foreach (StatModifier stat in SurvivalToolType.allNoToolDrictionary[job])
                    if (!tool.TryGetJobValue(job, stat.stat, out float val) || val < stat.value)
                        flag = false;
                if (flag)
                    return true;
            }
            return false;
        }

        public static JobDef jobDef_AwesomeInventory_Unload = DefDatabase<JobDef>.GetNamedSilentFail("AwesomeInventory_Unload");
        public static Job DequipAndTryStoreSurvivalTool(this Pawn pawn, Thing tool, bool enqueueCurrent = true, IntVec3? c = null)
        {
            if (jobDef_AwesomeInventory_Unload != null)
                return new Job(jobDef_AwesomeInventory_Unload, tool);
            if (pawn.CurJob != null && enqueueCurrent)
                pawn.jobs.jobQueue.EnqueueFirst(pawn.CurJob);
            if (c == null)
                if (StoreUtility.TryFindBestBetterStoreCellFor(tool, pawn, pawn.Map, StoreUtility.CurrentStoragePriorityOf(tool), pawn.Faction, out IntVec3 pos))
                {
                    Job haulJob = new Job(JobDefOf.HaulToCell, tool, pos)
                    {
                        count = 1
                    };
                    pawn.jobs.jobQueue.EnqueueFirst(haulJob);
                }
            else
            {
                Job haulJob = new Job(JobDefOf.HaulToCell, tool, (IntVec3) c)
                {
                    count = 1
                };
                pawn.jobs.jobQueue.EnqueueFirst(haulJob);
            }
            return new Job(ST_JobDefOf.DropSurvivalTool, tool);
        }
        public static float GetValue(this IEnumerable<SurvivalToolTypeModifier> mods, SurvivalToolType toolType, float fallback = 1f)
        {
            mods.TryGetValue(toolType, out float val, fallback);
            return val;
        }
        public static bool TryGetValue(this IEnumerable<SurvivalToolTypeModifier> mods, SurvivalToolType toolType, out float val, float fallback = 1f)
        {
            val = fallback;
            foreach (SurvivalToolTypeModifier modifier in mods)
                if (modifier.toolType == toolType)
                {
                    val = modifier.value;
                    return true;
                }
            return false;
        }
        public static SurvivalToolProperties GetToolProperties(this SurvivalTool tool)
            => tool.def.GetModExtension<SurvivalToolProperties>();
        public static Pawn_SurvivalToolAssignmentTracker GetToolTracker(this Pawn pawn)
            => pawn.GetComp<Pawn_SurvivalToolAssignmentTracker>();
    }
}