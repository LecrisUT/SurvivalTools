using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace SurvivalTools
{
    public class SurvivalToolAssignment : IExposable, ILoadReferenceable
    {
        private Pawn pawn;
        public SurvivalToolAssignment() { }

        public SurvivalToolAssignment(int uniqueId, string label)
        {
            this.uniqueId = uniqueId;
            this.label = label;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref uniqueId, "uniqueId");
            Scribe_Values.Look(ref label, "label");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Deep.Look(ref filter, "filter", new object[0]);
            Scribe_Collections.Look(ref ToolTypeBlacklist, "ToolTypeBlacklist", LookMode.Def);
        }

        public string GetUniqueLoadID()
        {
            return "SurvivalToolAssignment_" + label + uniqueId.ToString();
        }

        public int uniqueId;
        public string label;
        public ThingFilter filter = new ThingFilter();
        // Not yet implemented
        private List<SurvivalToolType> ToolTypeBlacklist = new List<SurvivalToolType>();
        public void Initialize(Pawn pawn)
        {
            this.pawn = pawn;
            checkAlowedToolTypes();
        }
        public void checkAlowedToolTypes()
        {
            RaceExemption rule = MiscDef.IgnoreRaceList.FirstOrFallback(t => t.race == pawn.def);
            if (rule != null)
            {
                if (rule.all)
                    ToolTypeBlacklist = SurvivalToolType.allDefs;
                else
                    foreach (SurvivalToolType toolType in SurvivalToolType.allDefs)
                        if (!rule.checkIfAllowed(toolType) || toolType.relevantWorkGivers.Any(t => allowedWorkGiver(pawn, t.Worker)))
                            ToolTypeBlacklist.Add(toolType);
            }
        }
        // Switch to reversePatch
        public static MethodInfo WorkTab_CapableOf = AccessTools.Method(typeof(Pawn), "WorkTab.Pawn_Extensions.CapableOf");
        public static bool allowedWorkGiver(Pawn pawn, WorkGiver wg)
        {
            if (WorkTab_CapableOf != null)
                return (bool)WorkTab_CapableOf.Invoke(null, new object[] { pawn, wg });
            if (pawn.WorkTypeIsDisabled(wg.def.workType))
                return false;
            return true;
        }
        public bool CanUseTool(SurvivalTool tool)
        {
            List<SurvivalToolType> toolTypes = tool.GetToolProperties().toolTypes;
            if (toolTypes.Any(t => !ToolTypeBlacklist.Contains(t)))
                return true;
            return false;
        }
        public bool CanUseToolType(SurvivalToolType toolType)
            => !ToolTypeBlacklist.Contains(toolType);
    }
}