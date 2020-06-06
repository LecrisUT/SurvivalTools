using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SurvivalTools
{
    public static class MiscDef
    {
        public static List<RaceExemption> IgnoreRaceList = DefDatabase<RaceExemption>.AllDefsListForReading;

    }
    public class RaceExemption : Def
    {
        public ThingDef race;
        public List<SurvivalToolType> toolTypes = new List<SurvivalToolType>();
        public bool all = false;
        public bool checkIfAllowed(SurvivalToolType toolType)
        {
            if (all || toolTypes.Contains(toolType))
                return false;
            return true;
        }
    }
}
