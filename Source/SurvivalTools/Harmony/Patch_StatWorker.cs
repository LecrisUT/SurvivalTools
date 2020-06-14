using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;

namespace SurvivalTools.HarmonyPatches
{
    [HarmonyPatch(typeof(StatWorker))]
    [HarmonyPatch(nameof(StatWorker.GetExplanationFinalizePart))]
    public static class Patch_StatWorker_GetExplanationFinalizePart
    {
        public static void Postfix(ref string __result, StatRequest req, StatDef ___stat)
        {
            if (StatsWithTools.Contains(___stat) && req.Thing is Pawn pawn && pawn.CanUseSurvivalTools())
                AppendToolExplanation(pawn, ___stat, ref __result);

        }
        public static List<StatDef> StatsWithTools = new List<StatDef>();
        public static void AppendToolExplanation(Pawn pawn, StatDef stat, ref string str)
        {
            StringBuilder info = new StringBuilder("\n\nAffected by tools:\n");
            int infoLength = info.Length;
            List<SurvivalToolType> relevantToolTypes = SurvivalToolType.allDefs.Where(t => t.stats.Contains(stat)).ToList();
            SurvivalToolUsedHandler handler = pawn.GetToolTracker().usedHandler;
            List<SurvivalTool> heldRelevantTools = handler.heldTools.Where(t => t.GetToolProperties().toolTypes.Intersect(relevantToolTypes).Any()).ToList();
            List<SurvivalTool> usedTools = handler.UsedTools;
            List<WorkGiverDef> unaffectedWorkGivers = new List<WorkGiverDef>();
            foreach (SurvivalToolType toolType in relevantToolTypes)
            {
                info.Append("  " + toolType.LabelCap);
                if (!handler.BestTool_Type.TryGetValue(toolType, out SurvivalTool bestTool))
                    info.AppendLine(":   [" + "NoTool".Translate() + ": x" + toolType.noToolStatFactors.GetStatFactorFromList(stat).ToStringPercent() + "]:");
                else
                {
                    bestTool.TryGetTypeStatValue(toolType, stat, out float val);
                    info.AppendLine(":   x" + val.ToStringPercent());
                }
                info.Append("    ");
                if (toolType.relevantWorkGivers.NullOrEmpty())
                    info.Append("(Empty)");
                else
                {
                    infoLength = info.Length;
                    toolType.relevantWorkGivers.Do(t => info.AppendWithComma(t.LabelCap));
                    info.Remove(infoLength, 2);
                }
                info.AppendLine();
            }
            info.AppendLine("Unaffected work:");
            if (unaffectedWorkGivers.NullOrEmpty())
                info.AppendLine("    [Not implemented]");
            else
            {
                infoLength = info.Length;
                unaffectedWorkGivers.Do(t => info.AppendWithComma(t.LabelCap));
                info.Remove(infoLength, 2);
            }
            info.AppendLine("Available relevant tools:");
            if (heldRelevantTools.NullOrEmpty())
                info.AppendLine("    (Empty)");
            else
                foreach (SurvivalTool tool in heldRelevantTools)
                {
                    info.Append("  " + tool.LabelCapNoCount + ":");
                    bool empty = true;
                    infoLength = info.Length;
                    foreach (SurvivalToolType toolType in relevantToolTypes)
                        if (tool.toolTypeModifiers.TryGetValue(toolType, out float val))
                        {
                            empty = false;
                            info.AppendWithComma(" (" + toolType.LabelCap + ", x" + val.ToStringPercent() + ")");
                        }
                    if (empty)
                        info.Append("(Empty)");
                    else
                        info.Remove(infoLength, 2);
                    info.AppendLine();
                }
            str += info.ToString();
        }
    }
}