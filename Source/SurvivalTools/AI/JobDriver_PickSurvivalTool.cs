using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SurvivalTools
{
    public class JobDriver_PickSurvivalTool : JobDriver_TakeInventory
    {
		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(TargetIndex.A);
			SurvivalTool tool = TargetThingA as SurvivalTool;
			this.FailOn(() => tool == null);
			Toil move = new Toil()
			{
				initAction = delegate
				{
					pawn.pather.StartPath(TargetThingA, PathEndMode.ClosestTouch);
				},
				defaultCompleteMode = ToilCompleteMode.PatherArrival
			};
			move.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			yield return move;
			Toil pickup = Toils_Haul.TakeToInventory(TargetIndex.A, job.count);
			pickup.AddFinishAction(delegate
			{
				Pawn_SurvivalToolAssignmentTracker assignmentTracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
				if (assignmentTracker != null && job.playerForced)
					assignmentTracker.forcedHandler.SetForced(tool, true);
			});
			yield return pickup;
		}
	}
}