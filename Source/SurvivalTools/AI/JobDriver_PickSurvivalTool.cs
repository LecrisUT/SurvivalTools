using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SurvivalTools
{
    public class JobDriver_PickSurvivalTool : JobDriver_TakeInventory
    {
		protected override IEnumerable<Toil> MakeNewToils()
		{
			SurvivalTool tool = TargetThingA as SurvivalTool;
			this.FailOn(() => tool == null);
			this.FailOnForbidden(TargetIndex.A);
			foreach (Toil toil in base.MakeNewToils())
				yield return toil;
			Pawn_SurvivalToolAssignmentTracker assignmentTracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
			if (assignmentTracker != null && job.playerForced)
				assignmentTracker.forcedHandler.SetForced(tool, true);
			yield break;
		}
	}
}