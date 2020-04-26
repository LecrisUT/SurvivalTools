using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace SurvivalTools
{
    public class JobDriver_DropSurvivalTool : JobDriver
    {
        private const int DurationTicks = 30;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return new Toil
            {
                initAction = () => pawn.pather.StopDead(),
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = DurationTicks
            };
            yield return new Toil
            {
                initAction = () =>
                {
                    if (TargetThingA == null || !pawn.inventory.innerContainer.Contains(TargetThingA))
                        EndJobWith(JobCondition.Incompletable);
                    else
                    {
                        pawn.inventory.innerContainer.TryDrop(TargetThingA, pawn.Position, pawn.Map, ThingPlaceMode.Near, out Thing tool);
                        EndJobWith(JobCondition.Succeeded);
                    }
                }
            };
        }
    }
}