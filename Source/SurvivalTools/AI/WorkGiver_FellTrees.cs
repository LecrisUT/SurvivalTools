using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SurvivalTools
{
    // No Harmony patch implementation
    public class WorkGiver_FellTrees : WorkGiver_PlantsCut
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Thing thing in base.PotentialWorkThingsGlobal(pawn))
                if (thing.def.plant.IsTree)
                    yield return thing;
        }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.def.category != ThingCategory.Plant || !t.def.plant.IsTree)
                return null;
            Job prevJob = base.JobOnThing(pawn, t, forced);
            if (prevJob == null)
                return null;
            if (prevJob.def == JobDefOf.HarvestDesignated)
            {
                prevJob.def = ST_JobDefOf.HarvestTreeDesignated;
                return prevJob;
            }
            if (prevJob.def == JobDefOf.CutPlantDesignated)
            {
                prevJob.def = ST_JobDefOf.FellTreeDesignated;
                return prevJob;
            }
            Log.ErrorOnce($"[[LC]SurvivalTools] Recieved a job different than the ones coded: {prevJob}", 4579456);
            return null;
        }
    }
    // Harmony patched implementation with copy-pasted method body

    /*public class WorkGiver_FellTrees : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn)
            => Danger.Deadly;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            List<Designation> desList = pawn.Map.designationManager.allDesignations;
            for (int i = 0; i < desList.Count; i++)
            {
                Designation des = desList[i];
                if ((des.def == DesignationDefOf.CutPlant || des.def == DesignationDefOf.HarvestPlant) && des.target.Thing.def.plant.IsTree == true)
                    yield return des.target.Thing;
            }
        }
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!pawn.Map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.CutPlant))
                return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.HarvestPlant);
            return false;
        }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.def.category != ThingCategory.Plant || !t.def.plant.IsTree)
                return null;
            if (!pawn.CanReserve(t, 1, -1, null, forced))
                return null;
            if (t.IsForbidden(pawn))
                return null;
            if (t.IsBurning())
                return null;
            foreach (Designation item in pawn.Map.designationManager.AllDesignationsOn(t))
            {
                if (item.def == DesignationDefOf.HarvestPlant)
                {
                    if (!((Plant)t).HarvestableNow)
                        return null;
                    return JobMaker.MakeJob(ST_JobDefOf.HarvestTreeDesignated, t);
                }
                if (item.def == DesignationDefOf.CutPlant)
                    return JobMaker.MakeJob(ST_JobDefOf.FellTreeDesignated, t);
            }
            return null;
        }
    }*/
}