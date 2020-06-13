using System.Collections.Generic;
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
		}
	}
}