using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SurvivalTools
{
    // No Harmony patch implementation

    public class WorkGiver_PlantsCut_NoTrees : WorkGiver_PlantsCut
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach(Thing thing in base.PotentialWorkThingsGlobal(pawn))
                if (!thing.def.plant.IsTree)
                    yield return thing;
        }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.def.category != ThingCategory.Plant || t.def.plant.IsTree)
                return null;
            return base.JobOnThing(pawn, t, forced);
        }
    }
}