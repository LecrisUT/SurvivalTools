using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SurvivalTools
{
    public class Alert_ColonistNeedsSurvivalTool : Alert
    {
        public Alert_ColonistNeedsSurvivalTool()
        {
            defaultPriority = AlertPriority.Medium;
        }

        private List<Pawn> culpritsResult = new List<Pawn>();

        private List<Pawn> ToollessWorkers
        {
            get
            {
                culpritsResult.Clear();
                foreach (Pawn item in PawnsFinder.AllMaps_FreeColonistsSpawned)
                {
                    if (WorkingToolless(item))
                    {
                        culpritsResult.Add(item);
                    }
                }
                return culpritsResult;
            }
        }

        private static bool WorkingToolless(Pawn pawn)
        {
            Pawn_SurvivalToolAssignmentTracker tracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
            if (tracker != null)
                foreach (SurvivalToolType toolType in tracker.RequiredToolTypes)
                    if (!tracker.usedHandler.HasSurvivalTool(toolType))
                        return true;
            return false;
        }

        private static string ToollessWorkTypesString(Pawn pawn)
        {
            List<string> types = new List<string>();
            Pawn_SurvivalToolAssignmentTracker tracker = pawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
            if (tracker != null)
                foreach (SurvivalToolType toolType in tracker.RequiredToolTypes)
                    if (!tracker.usedHandler.HasSurvivalTool(toolType))
                        types.Add(toolType.label);
            return GenText.ToCommaList(types).CapitalizeFirst();
        }

        public override TaggedString GetExplanation()
        {
            string result = "ColonistNeedsSurvivalToolDesc".Translate() + ":\n";
            foreach (Pawn pawn in ToollessWorkers)
                result += ("\n    " + pawn.LabelShort + " (" + ToollessWorkTypesString(pawn) + ")");
            return result;
        }

        public override string GetLabel() =>
            ((ToollessWorkers.Count() <= 1) ? "ColonistNeedsSurvivalTool" : "ColonistsNeedSurvivalTool").Translate();

        public override AlertReport GetReport() =>
            AlertReport.CulpritsAre(ToollessWorkers);
    }
}