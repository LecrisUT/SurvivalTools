using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SurvivalTools
{
    public class WorkGiverExtension : DefModExtension
    {
        public List<SurvivalToolType> requiredToolTypes = new List<SurvivalToolType>();
        public List<JobDef> relevantJobs = new List<JobDef>();
        public void Initialize()
        {
            requiredToolTypes = SurvivalToolType.allDefs.Where(t => t.jobList.Intersect(relevantJobs).Any()).ToList();
        }
        public bool MeetsRequirementJobs(Pawn pawn)
        {
            if (!SurvivalToolsSettings.hardcoreMode)
                return true;
            foreach (JobDef job in relevantJobs)
                if (!pawn.GetToolTracker().usedHandler.BestTool.ContainsKey(job))
                    if (SurvivalToolType.allNoToolDrictionary[job].Any(t => t.value <= 0f))
                        return false;
            return true;
        }
    }
}