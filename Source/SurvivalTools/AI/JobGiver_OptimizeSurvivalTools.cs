using RimWorld;
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
            if (!pawn.CanUseSurvivalTools() || assignmentTracker == null || Find.TickManager.TicksGame < assignmentTracker.nextSurvivalToolOptimizeTick)
                return null;

            if (SurvivalToolsSettings.toolAutoDropExcess)
            {

                assignmentTracker.usedHandler.CheckToolsInUse();

                // Check if current tool assignment allows for each tool, auto-removing those that aren't allowed.
                SurvivalToolAssignment curAssignment = assignmentTracker.CurrentSurvivalToolAssignment;
                List<SurvivalTool> heldTools = pawn.GetHeldSurvivalTools();
                foreach (SurvivalTool tool in heldTools)
                    if (!assignmentTracker.usedHandler.IsUsed(tool) && !assignmentTracker.forcedHandler.IsForced(tool)
                        && StoreUtility.TryFindBestBetterStoreCellFor(tool, pawn, pawn.Map, StoreUtility.CurrentStoragePriorityOf(tool), pawn.Faction, out IntVec3 c))
                        return pawn.DequipAndTryStoreSurvivalTool(tool, true, c);

            }
            if (SurvivalToolsSettings.toolOptimization)
            {
                SurvivalToolAssignment curAssignment = assignmentTracker.CurrentSurvivalToolAssignment;
                List<StatDef> workRelevantStats = pawn.AssignedToolRelevantWorkGiversStatDefs();
                List<Thing> mapTools = pawn.MapHeld.listerThings.AllThings.Where(t => t is SurvivalTool).ToList();

                SurvivalTool curTool = null;
                SurvivalTool newTool = null;
                float optimality = 0f;
                foreach (StatDef stat in workRelevantStats)
                {
                    curTool = pawn.GetBestSurvivalTool(stat);
                    optimality = SurvivalToolScore(curTool, workRelevantStats);
                    foreach (SurvivalTool potentialTool in mapTools)
                    {
                        if (StatUtility.StatListContains(potentialTool.WorkStatFactors.ToList(), stat) && curAssignment.filter.Allows(potentialTool) && potentialTool.BetterThanWorkingToollessFor(stat) &&
                            pawn.CanUseSurvivalTool(potentialTool.def) && potentialTool.IsInAnyStorage() && !potentialTool.IsForbidden(pawn) && !potentialTool.IsBurning())
                        {
                            float potentialOptimality = SurvivalToolScore(potentialTool, workRelevantStats);
                            float delta = potentialOptimality - optimality;
                            if (delta > 0f && pawn.CanReserveAndReach(potentialTool, PathEndMode.OnCell, pawn.NormalMaxDanger()))
                            {
                                newTool = potentialTool;
                                optimality = potentialOptimality;
                            }
                        }
                    }
                    if (newTool != null)
                        break;
                }

                // Return a job based on whether or not a better tool was located

                // Failure
                if (newTool == null)
                {
                    SetNextOptimizeTick(pawn);
                    return null;
                }

                // Success
                int heldToolOffset = 0;
                if (curTool != null && !assignmentTracker.forcedHandler.IsForced(curTool))
                {
                    pawn.jobs.jobQueue.EnqueueFirst(pawn.DequipAndTryStoreSurvivalTool(curTool, false));
                    heldToolOffset = -1;
                }
                if (pawn.CanCarryAnyMoreSurvivalTools(heldToolOffset))
                {
                    Job pickupJob = new Job(JobDefOf.TakeInventory, newTool)
                    {
                        count = 1
                    };
                    return pickupJob;
                }

            }

            // Final failure state
            SetNextOptimizeTick(pawn);
            return null;
        }

        private static float SurvivalToolScore(SurvivalTool tool, List<StatDef> workRelevantStats)
        {
            if (tool == null)
                return 0f;

            float optimality = 0f;
            foreach (StatDef stat in workRelevantStats)
                optimality += StatUtility.GetStatValueFromList(tool.WorkStatFactors.ToList(), stat, 0f);

            if (tool.def.useHitPoints)
            {
                float lifespanRemaining = tool.GetStatValue(ST_StatDefOf.ToolEstimatedLifespan, true) * ((float)tool.HitPoints * tool.MaxHitPoints);
                optimality *= LifespanDaysToOptimalityMultiplierCurve.Evaluate(lifespanRemaining);
            }
            return optimality;
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