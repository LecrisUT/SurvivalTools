using RimWorld;

namespace SurvivalTools
{
    [DefOf]
    public static class ST_StatDefOf
    {
        // Pawn
        public static StatDef SurvivalToolCarryCapacity;

        // Thing
        // Tool lifespan can be applied to stuff as well
        public static StatDef ToolEstimatedLifespan;
        // public static StatDef ToolWearModifier;
        public static StatDef ToolEffectivenessFactor;

        // Stuff
        public static StatDef ST_Hardness;
        public static StatDef ST_Sharpness;
    }
}