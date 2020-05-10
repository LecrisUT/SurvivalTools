using Verse;
using Verse.AI;

namespace SurvivalTools
{
    public class ThinkNode_UseTools : ThinkNode
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            pawn?.TryGetComp<Pawn_SurvivalToolAssignmentTracker>()?.usedHandler.CheckToolsInUse();
            return ThinkResult.NoJob;
        }
    }

}