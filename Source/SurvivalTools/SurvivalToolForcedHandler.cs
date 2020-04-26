using System.Collections.Generic;
using Verse;

namespace SurvivalTools
{
    public class SurvivalToolForcedHandler : IExposable
    {
        private List<Thing> forcedTools = new List<Thing>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref forcedTools, "forcedTools", LookMode.Reference);
        }
        public void Reset()
        {
            foreach (SurvivalTool tool in forcedTools)
                tool.Forced = false;
            forcedTools.Clear();
        }

        public List<Thing> ForcedTools => forcedTools;

        public bool SomethingForced => !forcedTools.NullOrEmpty();
    }
}