using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using System.Reflection;

namespace SurvivalTools
{
    public class JobDriver_DropSurvivalTool : JobDriver
    {
        private const int DurationTicks = 30;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => true;

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
                    if (TargetThingA == null)
                        EndJobWith(JobCondition.Incompletable);
                    else
                    {
                        if (SS_dropSidearm != null)
                            SS_dropSidearm.Invoke(null, new object[] { pawn, TargetThingA, true });
                        else
                        {
                            if (pawn.inventory.innerContainer.Contains(TargetThingA))
                            {
                                pawn.inventory.innerContainer.TryDrop(TargetThingA, pawn.Position, pawn.Map, ThingPlaceMode.Near, out Thing tool);
                                EndJobWith(JobCondition.Succeeded);
                            }
                            else if (pawn.equipment.Contains(TargetThingA))
                            {
                                pawn.equipment.TryDropEquipment((ThingWithComps)TargetThingA, out ThingWithComps tool, pawn.Position, false);
                                EndJobWith(JobCondition.Succeeded);

                            }
                            else
                                EndJobWith(JobCondition.Incompletable);
                        }
                    }
                }
            };
        }
        public static MethodInfo SS_dropSidearm = null;
    }
}