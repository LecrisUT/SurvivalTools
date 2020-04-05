using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SurvivalTools
{
    public static class MiscDef
    {
        public static List<RaceExemption> IgnoreRaceList = DefDatabase<RaceExemption>.AllDefsListForReading;

    }
    // I know how it looks, but I'm not racist I swear :D
    public class RaceExemption : Def
    {
        public ThingDef race;
        public List<StatDef> stat = new List<StatDef>();
        public bool all = false;
    }
}
