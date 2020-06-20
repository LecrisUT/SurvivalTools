using HarmonyLib;
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
            float sharpness = 1f;
            // 
            if (def.statBases.StatListContains(StatDefOf.SharpDamageMultiplier))
                sharpness = def.statBases.GetStatFactorFromList(StatDefOf.SharpDamageMultiplier);
            else if (def.statBases.StatListContains(StatDefOf.ArmorRating_Sharp))
                sharpness = 0.1f + def.statBases.GetStatFactorFromList(StatDefOf.ArmorRating_Sharp);
            else if (def.statBases.StatListContains(StatDefOf.StuffPower_Armor_Sharp))
                sharpness = 0.1f + def.statBases.GetStatFactorFromList(StatDefOf.StuffPower_Armor_Sharp);
            // Increase effect
            if (def.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic))
                sharpness = 1f + (sharpness - 1.0f) * 2f;
            else if (def.stuffProps.categories.Contains(StuffCategoryDefOf.Stony))
                sharpness = 0.8f + (sharpness - 0.6f) * 2f;
            else if (def.stuffProps.categories.Contains(StuffCategoryDefOf.Woody))
                sharpness = 0.8f + (sharpness - 0.4f) * 2f;
            return sharpness;
        }

        private static float Calculate_Hardness(ThingDef def)
        {
            float hardness = 1f;
            if (def.statBases.StatListContains(StatDefOf.BluntDamageMultiplier))
                hardness = def.statBases.GetStatFactorFromList(StatDefOf.BluntDamageMultiplier);
            else if (def.statBases.StatListContains(StatDefOf.ArmorRating_Blunt))
                hardness = 0.1f + 2 * def.statBases.GetStatFactorFromList(StatDefOf.ArmorRating_Blunt);
            else if (def.statBases.StatListContains(StatDefOf.StuffPower_Armor_Blunt))
                hardness = 0.1f + 2 * def.statBases.GetStatFactorFromList(StatDefOf.StuffPower_Armor_Blunt);
            if (def.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic))
                hardness = 1f + (hardness - 1.0f) * 2f;
            else if (def.stuffProps.categories.Contains(StuffCategoryDefOf.Stony))
                hardness = 0.9f + (hardness - 1.0f) * 2f;
            else if (def.stuffProps.categories.Contains(StuffCategoryDefOf.Woody))
                hardness = 0.7f + (hardness - 0.9f) * 2f;
            return hardness;
        }

        private static float ValueFactor(ThingDef def)
        {
            float val;
            val = def.statBases.GetStatValueFromList(StatDefOf.MarketValue, 0f);
            return (float)Math.Pow(val, (1.0 / 3.0));
        }

        private static void InjectStatBase(IEnumerable<ThingDef> list)
        {
            StringBuilder stringBuilder = new StringBuilder("[[LC]SurvivalTools] Added stuff stats to the following items:\n");
            StringBuilder DataCollection1 = new StringBuilder("[[LC]SurvivalTools] Data collection Categories:\nDefName, {Categories}\n");
            StringBuilder DataCollection2 = new StringBuilder("[[LC]SurvivalTools] Data collection Sharpness:\nDefName, {SharpDamageMultiplier}, {ArmorRating_Sharp}, {StuffPower_Armor_Sharp}\n");
            StringBuilder DataCollection3 = new StringBuilder("[[LC]SurvivalTools] Data collection Hardness:\nDefName, {BluntDamageMultiplier}, {ArmorRating_Blunt}, {StuffPower_Armor_Blunt}\n");
            foreach (ThingDef thingDef in list)
            {
                StatModifier Sharpness, Hardness;
                if (!thingDef.stuffProps.statFactors.StatListContains(ST_StatDefOf.ST_Sharpness))
                {
                    Sharpness = new StatModifier() { stat = ST_StatDefOf.ST_Sharpness, value = Calculate_Sharpness(thingDef) };
                    thingDef.stuffProps.statFactors.Add(Sharpness);
                }
                else
                    Sharpness = thingDef.stuffProps.statFactors.First(t => t.stat == ST_StatDefOf.ST_Sharpness);
                if (!thingDef.stuffProps.statFactors.StatListContains(ST_StatDefOf.ST_Hardness))
                {
                    Hardness = new StatModifier() { stat = ST_StatDefOf.ST_Hardness, value = Calculate_Hardness(thingDef) };
                    thingDef.stuffProps.statFactors.Add(Hardness);
                }
                else
                    Hardness = thingDef.stuffProps.statFactors.First(t => t.stat == ST_StatDefOf.ST_Hardness);
                stringBuilder.Append(thingDef.defName + " (" + Sharpness.value + ":" + Hardness.value + "), ");
                DataCollection1.Append($"{thingDef.defName}");
                thingDef.stuffProps.categories.Do(t => DataCollection1.AppendWithComma($"{t.defName}"));
                DataCollection1.AppendLine();
                DataCollection2.AppendLine($"{thingDef.defName}, {thingDef.statBases.GetStatOffsetFromList(StatDefOf.SharpDamageMultiplier)}, {thingDef.statBases.GetStatOffsetFromList(StatDefOf.ArmorRating_Sharp)}, {thingDef.statBases.GetStatOffsetFromList(StatDefOf.StuffPower_Armor_Sharp)}");
                DataCollection3.AppendLine($"{thingDef.defName}, {thingDef.statBases.GetStatOffsetFromList(StatDefOf.BluntDamageMultiplier)}, {thingDef.statBases.GetStatOffsetFromList(StatDefOf.ArmorRating_Blunt)}, {thingDef.statBases.GetStatOffsetFromList(StatDefOf.StuffPower_Armor_Blunt)}");
            }
            Log.Message(stringBuilder.ToString().TrimEnd(new char[] { ' ', ',' }), false);
            Log.Message(DataCollection1.ToString());
            Log.Message(DataCollection2.ToString());
            Log.Message(DataCollection3.ToString());
        }

    }
}