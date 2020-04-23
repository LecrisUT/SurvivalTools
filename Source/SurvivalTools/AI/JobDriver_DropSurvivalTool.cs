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

        /*protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            Log.Message("Test0");
            yield return new Toil
            {
                initAction = () => pawn.pather.StopDead(),
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = DurationTicks
            };
            //yield return Toils_General.Wait(10);
            Log.Message("Test1");
            yield return new Toil
            {
                initAction = () =>
                {
                    Log.Message("Test1.0");
                    if (!StoreUtility.TryFindStoreCellNearColonyDesperate(TargetThingA, pawn, out IntVec3 storeCell))
                    {
                        pawn.inventory.innerContainer.TryDrop(TargetThingA, ThingPlaceMode.Near, 1, out Thing _);
                        //pawn.inventory.innerContainer.TryDrop(TargetThingA, pawn.Position, pawn.MapHeld, ThingPlaceMode.Near, out Thing tool);
                        EndJobWith(JobCondition.Succeeded);
                    }
                    else
                    {
                        job.SetTarget(TargetIndex.B, storeCell);
                    }
                    Log.Message("Test1.1");
                }
            };

            yield return new Toil
            {
                initAction = () =>
                {
                    Log.Message("Test4.0");
                    if (TargetThingA == null || !pawn.inventory.innerContainer.Contains(TargetThingA))
                    {
                        EndJobWith(JobCondition.Incompletable);
                    }
                    else
                    {
                        if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                        {
                            pawn.inventory.innerContainer.TryDrop(TargetThingA, ThingPlaceMode.Near, 1, out Thing tool);
                            EndJobWith(JobCondition.Succeeded);
                        }
                        else
                        {
                            pawn.inventory.innerContainer.TryTransferToContainer(TargetThingA, pawn.carryTracker.innerContainer, 1, out Thing tool);
                        }
                        TargetThingA.SetForbidden(value: false, warnOnFail: false);
                    }
                }
            };
            Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return carryToCell;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, storageMode: true);
        }*/
    }
}