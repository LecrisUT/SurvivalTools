using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;

namespace SurvivalTools
{
    public class JobGiver_OptimizeSurvivalTools : ThinkNode_JobGiver
    {
        private void SetNextOptimizeTick(Pawn pawn)
        {
            pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>().nextSurvivalToolOptimizeTick = Find.TickManager.TicksGame + Rand.Range(6000, 9000);
        }

        // This is a janky mess and a half, but works!
        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn_SurvivalToolAssignmentTracker assignmentTracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();

            // Pawn can't use tools, lacks a tool assignment tracker or it isn't yet time to re-optimise tools
            if (assignmentTracker == null || !pawn.CanUseSurvivalTools() || Find.TickManager.TicksGame < assignmentTracker.nextSurvivalToolOptimizeTick)
                return null;

            assignmentTracker.usedHandler.Update();
            if (SurvivalToolsSettings.toolAutoDropExcess)
            {
                // Check if current tool assignment allows for each tool, auto-removing those that aren't allowed.
                foreach (SurvivalTool tool in pawn.GetHeldSurvivalTools())
                    if (!assignmentTracker.usedHandler.IsUsed(tool) && !assignmentTracker.forcedHandler.IsForced(tool)
                        && StoreUtility.TryFindBestBetterStoreCellFor(tool, pawn, pawn.Map, StoreUtility.CurrentStoragePriorityOf(tool), pawn.Faction, out IntVec3 c))
                        return pawn.DequipAndTryStoreSurvivalTool(tool, true, c);

            }
            if (SurvivalToolsSettings.toolOptimization)
            {
                // Return a job based on whether or not a better tool was located
                (SurvivalTool oldTool, SurvivalTool newTool) = SearchForBetterTools(pawn, assignmentTracker).FirstOrFallback();
                // Failure
                if (newTool == null)
                {
                    SetNextOptimizeTick(pawn);
                    return null;
                }

                // Success
                if (oldTool != null && !assignmentTracker.forcedHandler.IsForced(oldTool))
                    pawn.jobs.jobQueue.EnqueueFirst(pawn.DequipAndTryStoreSurvivalTool(oldTool, false));
                Job pickupJob = new Job(JobDefOf.TakeInventory, newTool)
                {
                    count = 1
                };
                return pickupJob;

            }

            // Final failure state
            SetNextOptimizeTick(pawn);
            return null;
        }
        private static IEnumerable<(SurvivalTool oldTool, SurvivalTool newTool)> SearchForBetterTools(Pawn pawn, Pawn_SurvivalToolAssignmentTracker assignmentTracker)
        {

            SurvivalToolAssignment toolAssignment = assignmentTracker.CurrentSurvivalToolAssignment;
            List<SurvivalToolType> requiredToolTypes = assignmentTracker.RequiredToolTypes;
            // Tick rare update the list
            List<Thing> mapTools = pawn.MapHeld.listerThings.AllThings.Where(t => t is SurvivalTool).ToList();

            Dictionary<SurvivalToolType, (float score, SurvivalTool oldTool, SurvivalTool newTool)> curTools
                = new Dictionary<SurvivalToolType, (float, SurvivalTool, SurvivalTool)>(requiredToolTypes.Select(
                t => BestSurvivalToolScore(assignmentTracker.usedHandler.UsedTools, t, requiredToolTypes)));
            float potentialScore;
            foreach (SurvivalTool potentialTool in mapTools)
            {
                if (potentialTool == null || !toolAssignment.filter.Allows(potentialTool) || !potentialTool.BetterThanWorkingToolless() ||
                    potentialTool.IsForbidden(pawn) || potentialTool.IsBurning() || !potentialTool.IsInAnyStorage())
                    continue;
                foreach (SurvivalToolType toolType in potentialTool.def.GetModExtension<SurvivalToolProperties>().toolTypes)
                {
                    if (curTools.TryGetValue(toolType, out (float score, SurvivalTool oldTool, SurvivalTool newTool) value))
                    {
                        if (value.score < (potentialScore = SurvivalToolScore(potentialTool, requiredToolTypes)))
                        {
                            curTools[toolType] = (potentialScore, value.oldTool, potentialTool);
                        }
                    }
                }
            }
            foreach ((SurvivalTool oldTool, SurvivalTool newTool) tools in curTools.Values.Select(t => (t.oldTool, t.newTool)))
                if (tools.newTool != null)
                    yield return tools;
        }

        private static float SurvivalToolScore(SurvivalTool tool, List<SurvivalToolType> requiredToolTypes)
        {
            if (tool == null)
                return 0f;
            float optimality = 0f;
            foreach (SurvivalToolType toolType in requiredToolTypes)
                if (tool.TryGetTypeValue(toolType, out float val))
                    optimality += val;
            if (tool.def.useHitPoints)
            {
                float lifespanRemaining = tool.GetStatValue(ST_StatDefOf.ToolEstimatedLifespan, true) * ((float)tool.HitPoints * tool.MaxHitPoints);
                optimality *= LifespanDaysToOptimalityMultiplierCurve.Evaluate(lifespanRemaining);
            }
            return optimality;
        }

        private static KeyValuePair<SurvivalToolType, (float score, SurvivalTool oldTool, SurvivalTool newTool)> BestSurvivalToolScore(List<SurvivalTool> toolList, SurvivalToolType toolType, List<SurvivalToolType> requiredToolTypes)
        {
            SurvivalTool tool = null;
            float val = 0f;
            foreach (SurvivalTool currTool in toolList)
            {
                float currVal = SurvivalToolScore(currTool, requiredToolTypes);
                if (currVal > val)
                {
                    tool = currTool;
                    val = currVal;
                }
            }
            return new KeyValuePair<SurvivalToolType, (float, SurvivalTool, SurvivalTool)>(toolType, (val, tool, null));
        }

        private static readonly SimpleCurve LifespanDaysToOptimalityMultiplierCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0.04f),
            new CurvePoint(0.5f, 0.2f),
            new CurvePoint(1f, 0.5f),
            new CurvePoint(2f, 1f),
            new CurvePoint(4f, 1f),
            new CurvePoint(999f, 10f)
        };
    }
}