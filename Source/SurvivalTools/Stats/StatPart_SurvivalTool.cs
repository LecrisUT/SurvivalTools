using RimWorld;
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
                if (pawn.HasSurvivalToolFor(parentStat, out SurvivalTool tool, out float statFactor))
                    return tool.LabelCapNoCount + ": x" + statFactor.ToStringPercent();
                return "NoTool".Translate() + ": x" + NoToolStatFactor.ToStringPercent();
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn && pawn.CanUseSurvivalTools())
            {
                //if (pawn.HasSurvivalToolFor(parentStat, out SurvivalTool tool, out float statFactor))
                //    val *= statFactor;
                //else
                //    val *= NoToolStatFactor;
                if (pawn.HasSurvivalToolFor(parentStat, out SurvivalTool tool, out float statFactor))
                {
                    val *= statFactor;
                    Log.Message($"Test 0 [patched] : {pawn} : {pawn.Name} : {parentStat}");
                }
                else
                {
                    val *= NoToolStatFactor;
                    Log.Message($"Test 1 [noPatched] : {pawn} : {pawn.Name} : {parentStat}");
                }
            }
        }

        public float NoToolStatFactor =>
            (SurvivalToolsSettings.hardcoreMode) ? NoToolStatFactorHardcore : noToolStatFactor;

        private float noToolStatFactor = 0.3f;

        private float noToolStatFactorHardcore = -1f;

        private float NoToolStatFactorHardcore =>
            (noToolStatFactorHardcore != -1f) ? noToolStatFactorHardcore : noToolStatFactor;
    }
}