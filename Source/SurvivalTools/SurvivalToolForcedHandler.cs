using System.Collections.Generic;
using Verse;

namespace SurvivalTools
{
    public class SurvivalToolForcedHandler : IExposable
    {
        private List<SurvivalTool> forcedTools = new List<SurvivalTool>();
        public bool SomethingIsForced => forcedTools.Count > 0;
        public List<SurvivalTool> ForcedTools => forcedTools;
        public void Reset() => forcedTools.Clear();
        public bool AllowedToAutomaticallyDrop(SurvivalTool tool)
        {
            return !forcedTools.Contains(tool);
        }
        public void SetForced(SurvivalTool tool, bool forced)
        {
            if (forced)
            {
                if (!forcedTools.Contains(tool))
                {
                    forcedTools.Add(tool);
                }
            }
            else if (forcedTools.Contains(tool))
            {
                forcedTools.Remove(tool);
            }
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref forcedTools, "forcedTools", LookMode.Reference);
        }
        public bool IsForced(SurvivalTool tool)
        {
            if (tool.Destroyed)
            {
                Log.Error("Tool was forced while Destroyed: " + tool);
                if (forcedTools.Contains(tool))
                {
                    forcedTools.Remove(tool);
                }
                return false;
            }
            return forcedTools.Contains(tool);
        }
    }
}