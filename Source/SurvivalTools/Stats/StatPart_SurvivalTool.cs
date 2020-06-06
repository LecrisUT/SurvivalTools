using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SurvivalTools
{
    public class StatPart_SurvivalTool : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            // The AI will cheat this system for now until tool generation gets figured out
            if (req.Thing is Pawn pawn && pawn.CanUseSurvivalTools())
            {
                StringBuilder info = new StringBuilder("Available relevant tools:\n");
                SurvivalToolUsedHandler handler = pawn.GetToolTracker().usedHandler;
                List<SurvivalTool> heldRelevantTools = handler.heldTools.Where(t=>t.GetToolProperties().toolTypes.Intersect(relevantToolTypes).Any()).ToList();
                List<SurvivalTool> usedTools = handler.UsedTools;
                if (heldRelevantTools.NullOrEmpty())
                    info.AppendLine("\tEmpty");
                else
                    foreach (SurvivalTool tool in heldRelevantTools)
                    {
                        info.Append(tool.LabelCapNoCount + ":");
                        foreach (SurvivalToolType toolType in relevantToolTypes)
                            if (tool.toolTypeModifiers.TryGetValue(toolType, out float val))
                                info.AppendWithComma(" (" + toolType.LabelCap + ", x" + val.ToStringPercent() + ")");
                        info.AppendLine();
                    }
                info.AppendLine("\nAffected WorkTypes:");
                foreach (SurvivalToolType toolType in relevantToolTypes)
                {
                    info.Append(toolType.LabelCap);
                    if (!handler.BestTool_Type.TryGetValue(toolType, out SurvivalTool bestTool))
                        info.AppendLine(": " + "NoTool".Translate() + ": x" + toolType.noToolStatFactors.GetStatFactorFromList(parentStat).ToStringPercent() + ":");
                    else
                    {
                        bestTool.TryGetTypeStatValue(toolType, parentStat, out float val);
                        info.AppendLine(": " + bestTool.LabelCapNoCount + ": x" + val.ToStringPercent() + ":");
                    }
                    foreach (WorkGiverDef wg in toolType.relevantWorkGivers)
                        info.AppendWithComma(wg.LabelCap);
                    info.AppendLine();
                }
                info.AppendLine("Not affected:");
                foreach (WorkGiverDef wg in unaffectedWorkGivers)
                    info.AppendWithComma(wg.LabelCap);
                return info.ToString();
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val) { }
        private readonly List<SurvivalToolType> relevantToolTypes = new List<SurvivalToolType>();
        public List<WorkGiverDef> unaffectedWorkGivers;
        public List<WorkGiverDef> relevantWorkGivers;

        public void Initialize()
        {
            foreach (SurvivalToolType toolType in SurvivalToolType.allDefs)
                if (toolType.stats.Contains(parentStat))
                    relevantToolTypes.Add(toolType);
        }
    }
}