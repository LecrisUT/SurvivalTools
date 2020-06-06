using RimWorld;
using Verse;

namespace SurvivalTools
{
    // No Harmony patch implementation
    public class WorkGiver_GrowerHarvest_NoTrees : WorkGiver_GrowerHarvest
    {
        public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            Plant plant = c.GetPlant(pawn.Map);
            if (plant == null || plant.def.plant.IsTree)
                return false;
            return base.HasJobOnCell(pawn, c, forced);
        }
    }
}