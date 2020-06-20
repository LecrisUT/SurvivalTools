using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SurvivalTools
{
    public class SurvivalToolUsedHandler : IExposable
    {
        private List<SurvivalTool> usedTools = new List<SurvivalTool>();
        public List<SurvivalTool> UsedTools
        {
            get
            {
                if (dirtyCache)
                    Update();
                return usedTools;
            }
        }
        private Dictionary<JobDef, SurvivalTool> bestTool = new Dictionary<JobDef, SurvivalTool>();
        public Dictionary<JobDef, SurvivalTool> BestTool
        {
            get
            {
                if (dirtyCache)
                    Update();
                return bestTool;
            }
        }
        private Dictionary<SurvivalToolType, SurvivalTool> bestTool_Type = new Dictionary<SurvivalToolType, SurvivalTool>();
        public Dictionary<SurvivalToolType, SurvivalTool> BestTool_Type
        {
            get
            {
                if (dirtyCache)
                    Update();
                return bestTool_Type;
            }
        }
        private List<SurvivalTool> bestHeldTools = new List<SurvivalTool>();
        public List<SurvivalTool> BestHeldTools
        {
            get
            {
                if (dirtyCache)
                    Update();
                return bestHeldTools;
            }
        }
        private List<SurvivalTool> bestHeldAllowedTools = new List<SurvivalTool>();
        public List<SurvivalTool> BestHeldAllowedTools
        {
            get
            {
                if (dirtyCache)
                    Update();
                return bestHeldAllowedTools;
            }
        }
        public List<SurvivalTool> heldTools = new List<SurvivalTool>();
        private Pawn pawn;
        private Pawn_SurvivalToolAssignmentTracker assignmentTracker;
        public bool dirtyCache = true;
        public SurvivalToolUsedHandler(Pawn p, Pawn_SurvivalToolAssignmentTracker tracker)
        {
            pawn = p;
            assignmentTracker = tracker;
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref usedTools, "usedTools", LookMode.Reference);
        }
        public void Reset() => usedTools.Clear();
        private bool busy = false;
        public void Update()
        {
            if (busy)
                return;
            busy = true;
            CheckBestHeldTools();
            CheckToolsInUse();
            FindBestTools();
            dirtyCache = false;
            busy = false;
        }
        public void SetUsed(SurvivalTool tool, bool used)
        {
            if (used)
            {
                if (!usedTools.Contains(tool))
                    usedTools.Add(tool);
            }
            else if (usedTools.Contains(tool))
                usedTools.Remove(tool);
        }
        private bool ToolIsAllowed(SurvivalTool tool)
            => pawn.CanUseSurvivalTool(tool) && assignmentTracker.CurrentSurvivalToolAssignment.filter.Allows(tool);
        public void CheckBestHeldTools()
        {
            heldTools = pawn.GetHeldSurvivalTools().ToList();
            List<JobDef> assignedJobs = assignmentTracker.AssignedJobs;
            List<SurvivalTool> toolList = new List<SurvivalTool>();
            List<SurvivalTool> toolList2 = new List<SurvivalTool>();
            foreach (JobDef job in SurvivalToolType.allAffectedJobs)
            {
                SurvivalTool tool = null;
                float val = 0;
                SurvivalTool tool2 = null;
                float val2 = 0;
                List<JobDef> toolJobBonus = new List<JobDef>();
                List<JobDef> toolJobBonus2 = new List<JobDef>();
                foreach (SurvivalTool currTool in heldTools)
                {
                    List<JobDef> currToolJobBonus = currTool.GetBonusJobList();
                    if (currTool.TryGetJobValue(job, out float currVal))
                    {
                        if (ToolIsAllowed(currTool))
                            if (currVal > val || (!toolJobBonus.NullOrEmpty() && currVal == val && !toolJobBonus.Any(t => !currToolJobBonus.Contains(t))))
                            {
                                tool = currTool;
                                val = currVal;
                                toolJobBonus = currToolJobBonus;
                            }
                        if (currVal > val2 || (!toolJobBonus2.NullOrEmpty() && currVal == val2 && !toolJobBonus2.Any(t => !currToolJobBonus.Contains(t))))
                        {
                            tool2 = currTool;
                            val2 = currVal;
                            toolJobBonus2 = currToolJobBonus;
                        }
                    }
                }
                if (tool != null)
                    toolList.AddDistinct(tool);
                if (tool2 != null)
                    toolList2.AddDistinct(tool2);
            }
            bestHeldAllowedTools = toolList;
            bestHeldTools = toolList2;
        }
        private SurvivalTool GetBetterTool(SurvivalTool oldTool)
        {
            SurvivalTool newTool = null;
            float val = 0f;
            foreach (JobDef job in oldTool.GetJobList())
            {
                foreach (SurvivalTool currTool in bestHeldTools)
                    if (currTool.TryGetJobValue(job, out float currVal) && currVal > val)
                    {
                        newTool = currTool;
                        val = currVal;
                    }
            }
            return newTool;

        }
        public void CheckToolsInUse()
        {
            int maxTools = pawn.GetMaxTools();
            if (usedTools.Count > maxTools)
                usedTools.RemoveRange(maxTools, usedTools.Count - maxTools);
            // Check currently used tools if allowed
            foreach (SurvivalTool tool in usedTools.ToList())
            {
                CheckIsUsed(tool);
                if (!usedTools.Contains(tool))
                {
                    SurvivalTool betterTool = GetBetterTool(tool);
                    if (betterTool != null)
                        CheckIsUsed(betterTool);
                }
            }

            // Check other held tools if can be used
            if (usedTools.Count < maxTools)
                foreach (SurvivalTool tool in bestHeldAllowedTools)
                {
                    if (!usedTools.Contains(tool))
                        CheckIsUsed(tool);
                }
        }
        public void FindBestTools()
        {
            Dictionary<JobDef, SurvivalTool> toolList = new Dictionary<JobDef, SurvivalTool>();
            Dictionary<SurvivalToolType, SurvivalTool> tool_TypeList = new Dictionary<SurvivalToolType, SurvivalTool>();
            List<JobDef> assignedJobs = assignmentTracker.AssignedJobs;
            foreach (JobDef job in assignedJobs)
            {
                SurvivalTool tool = null;
                float val = 0f;
                foreach (SurvivalTool currTool in usedTools)
                    if (currTool.TryGetJobValue(job, out float currVal) && currVal > val)
                    {
                        tool = currTool;
                        val = currVal;
                    }
                if (tool != null)
                    toolList.Add(job, tool);
            }
            foreach (SurvivalToolType toolType in SurvivalToolType.allDefs)
            {
                SurvivalTool tool = null;
                float val = 0f;
                foreach (SurvivalTool currTool in usedTools)
                    if (currTool.TryGetTypeValue(toolType, out float currVal) && currVal > val)
                    {
                        tool = currTool;
                        val = currVal;
                    }
                if (tool != null)
                    tool_TypeList.Add(toolType, tool);
            }
            bestTool = toolList;
            bestTool_Type = tool_TypeList;
        }
        public bool IsUsed(SurvivalTool tool)
        {
            if (tool.Destroyed)
            {
                Log.Error("Tool was being used while Destroyed: " + tool);
                if (usedTools.Contains(tool))
                    usedTools.Remove(tool);
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
            if (!pawn.NeedsSurvivalTool(tool) || !pawn.CanUseSurvivalTool(tool) || !assignmentTracker.CurrentSurvivalToolAssignment.filter.Allows(tool))
            {
                SetUsed(tool, false);
                return;
            }
            if (bestHeldTools.Contains(tool) &&
                (usedTools.Count < pawn.GetMaxTools() || usedTools.Contains(tool)))
                SetUsed(tool, true);
            else
                SetUsed(tool, false);
        }
        public bool HasSurvivalTool(SurvivalToolType toolType)
        {
            foreach (SurvivalTool tool in UsedTools)
                if (tool.GetToolProperties().toolTypes.Contains(toolType))
                    return true;
            return false;
        }
        public bool HasSurvivalTool(JobDef jobDef)
        {
            foreach (SurvivalTool tool in UsedTools)
                if (tool.GetToolProperties().toolTypes.Any(t => t.AffectsJob(jobDef)))
                    return true;
            return false;
        }
    }
}