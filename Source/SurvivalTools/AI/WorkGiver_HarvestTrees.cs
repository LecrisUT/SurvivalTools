using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace SurvivalTools
{
    // No Harmony patch implementation
    public class WorkGiver_HarvestTrees : WorkGiver_GrowerHarvest
    {
        public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            Plant plant = c.GetPlant(pawn.Map);
            if (plant == null || !plant.def.plant.IsTree)
                return false;
            return base.HasJobOnCell(pawn, c, forced);
        }
        public override Job JobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            Job job = base.JobOnCell(pawn, c, forced);
            if (job.def == JobDefOf.Harvest)
            {
                job.def = ST_JobDefOf.HarvestTree;
                return job;
            }
            Log.ErrorOnce($"[[LC]SurvivalTools] Tried to make JobOnCell, but job returned is not recognized: {job}", 547869153);
            return job;
        }
    }
    // Harmony patched implementation with copy-pasted
    /*public class WorkGiver_HarvestTrees : WorkGiver_Grower
    {
		public override PathEndMode PathEndMode => PathEndMode.Touch;
		public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
		{
			Plant plant = c.GetPlant(pawn.Map);
			if (plant == null || !plant.def.plant.IsTree)
				return false;
			if (plant.IsForbidden(pawn))
				return false;
			if (!plant.HarvestableNow || plant.LifeStage != PlantLifeStage.Mature)
				return false;
			if (!plant.CanYieldNow())
				return false;
			if (!pawn.CanReserve(plant, 1, -1, null, forced))
				return false;
			return true;
		}
		public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
			if (pawn.GetLord() != null)
				return true;
			return base.ShouldSkip(pawn, forced);
		}

		public override Job JobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
		{
			Job job = JobMaker.MakeJob(JobDefOf.Harvest);
			Map map = pawn.Map;
			Room room = c.GetRoom(map);
			float num = 0f;
			for (int i = 0; i < 40; i++)
			{
				IntVec3 intVec = c + GenRadial.RadialPattern[i];
				if (intVec.GetRoom(map) != room || !HasJobOnCell(pawn, intVec))
					continue;
				Plant plant = intVec.GetPlant(map);
				if (!(intVec != c) || plant.def == WorkGiver_Grower.CalculateWantedPlantDef(intVec, map))
				{
					num += plant.def.plant.harvestWork;
					if (intVec != c && num > 2400f)
						break;
					job.AddQueuedTarget(TargetIndex.A, plant);
				}
			}
			if (job.targetQueueA != null && job.targetQueueA.Count >= 3)
				job.targetQueueA.SortBy((LocalTargetInfo targ) => targ.Cell.DistanceToSquared(pawn.Position));
			return job;
		}
	}*/
}