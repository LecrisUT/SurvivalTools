using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SurvivalTools
{
    public static class Stat_Injector
    {

        public static void Inject()
        {
            IEnumerable<ThingDef> matDefs = DefDatabase<ThingDef>.AllDefsListForReading.Where(x => x.stuffProps != null &&
                (x.stuffProps.categories.Contains(StuffCategoryDefOf.Stony) ||
                x.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic) ||
                x.stuffProps.categories.Contains(StuffCategoryDefOf.Woody)));
            InjectStatBase(matDefs);
        }

        private static float Calculate_Sharpness(ThingDef def)
        {
            if (def.statBases.StatListContains(StatDefOf.SharpDamageMultiplier))
                return def.statBases.GetStatFactorFromList(StatDefOf.SharpDamageMultiplier);
            if (def.statBases.StatListContains(StatDefOf.ArmorRating_Sharp))
                return def.statBases.GetStatFactorFromList(StatDefOf.ArmorRating_Sharp);
            return def.statBases.GetStatFactorFromList(StatDefOf.StuffPower_Armor_Sharp);
        }

        private static float Calculate_Hardness(ThingDef def)
        {
            if (def.statBases.StatListContains(StatDefOf.BluntDamageMultiplier))
                return def.statBases.GetStatFactorFromList(StatDefOf.BluntDamageMultiplier);
            if (def.statBases.StatListContains(StatDefOf.ArmorRating_Blunt))
                return def.statBases.GetStatFactorFromList(StatDefOf.ArmorRating_Blunt);
            return def.statBases.GetStatFactorFromList(StatDefOf.StuffPower_Armor_Blunt);
        }

        private static float ValueFactor(ThingDef def)
        {
            float val;
            val = def.statBases.GetStatValueFromList(StatDefOf.MarketValue, 0f);
            return (float)Math.Pow(val, (1.0 / 3.0));
        }

        private static void InjectStatBase(IEnumerable<ThingDef> list)
        {
            StringBuilder stringBuilder = new StringBuilder("[SurvivalTools] Added stats to: ");
            foreach (ThingDef thingDef in list)
            {
                StatModifier Sharpness = new StatModifier();
                Sharpness.stat = ST_StatDefOf.ST_Sharpness;
                Sharpness.value = Calculate_Sharpness(thingDef);
                thingDef.stuffProps.statFactors.Add(Sharpness);
                StatModifier Hardness = new StatModifier();
                Hardness.stat = ST_StatDefOf.ST_Hardness;
                Hardness.value = Calculate_Hardness(thingDef);
                thingDef.stuffProps.statFactors.Add(Hardness);
                stringBuilder.Append(thingDef.defName + " (" + Sharpness.value + ":" + Hardness.value + "), ");
            }
            Log.Message(stringBuilder.ToString().TrimEnd(new char[] { ' ', ',' }), false);
        }

    }
}