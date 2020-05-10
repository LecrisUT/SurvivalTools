using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace SurvivalTools
{
    public class SurvivalToolUsedHandler : IExposable
    {
        private List<SurvivalTool> usedTools = new List<SurvivalTool>();
        public List<SurvivalTool> UsedTools => usedTools;
        private readonly Pawn pawn;
        private readonly Pawn_SurvivalToolAssignmentTracker assignmentTracker;
        public SurvivalToolUsedHandler(Pawn p, Pawn_SurvivalToolAssignmentTracker tracker)
        {
            pawn = p;
            assignmentTracker = tracker;
        }
        public void Reset() => usedTools.Clear();
        public void SetUsed(SurvivalTool tool, bool used)
        {
            if (used)
            {
                if (!usedTools.Contains(tool))
                {
                    usedTools.Add(tool);
                }
            }
            else if (usedTools.Contains(tool))
            {
                usedTools.Remove(tool);
            }
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref usedTools, "usedTools", LookMode.Reference);
        }
        public bool IsUsed(SurvivalTool tool)
        {
            if (tool.Destroyed)
            {
                Log.Error("Tool was being used while Destroyed: " + tool);
                if (usedTools.Contains(tool))
                {
                    usedTools.Remove(tool);
                }
                return false;
            }
            return usedTools.Contains(tool);
        }
        public void CheckIsUsed(SurvivalTool tool)
        {
            if (tool == null || pawn == null)
            {
                Log.Error($"[Survival Tools]: Cannot check if tool is used: tool={tool} : pawn={pawn}");
                return;
            }
            if (!pawn.CanUseSurvivalTools())
            {
                Log.Warning($"[Survival Tools]: Checking if tool is used on a pawn uncapable of using tools: {pawn}");
                return;
            }
            if (!pawn.NeedsSurvivalTool(tool) || !pawn.CanUseSurvivalTool(tool.def) || !assignmentTracker.CurrentSurvivalToolAssignment.filter.Allows(tool))
            {
                SetUsed(tool, false);
                return;
            }
            float maxTools = pawn.GetStatValue(ST_StatDefOf.SurvivalToolCarryCapacity, false);
            if (SurvivalToolUtility.BestSurvivalToolsFor(pawn).Contains(tool) &&
                (usedTools.Count < maxTools || usedTools.Contains(tool)))
                SetUsed(tool, true);
            else
                SetUsed(tool, false);
        }
        public void CheckToolsInUse()
        {
            foreach (SurvivalTool tool in usedTools.ToList())
                CheckIsUsed(tool);
            foreach (SurvivalTool tool in pawn.GetHeldSurvivalTools())
                if (!usedTools.Contains(tool))
                    CheckIsUsed(tool);
        }
    }
}