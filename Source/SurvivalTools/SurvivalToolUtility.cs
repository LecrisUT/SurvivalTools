﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace SurvivalTools
{
    public static class SurvivalToolUtility
    {

        public static readonly FloatRange AncientToolHitPointsRange = new FloatRange(0.3f, 0.7f);
        public const int MaxToolsCarriedAtOnce = 3;

        public static bool HasSurvivalTool(this Pawn pawn, StatDef stat, out SurvivalTool tool, out float statFactor)
        {
            tool = pawn.GetBestSurvivalTool(stat);
            statFactor = tool?.WorkStatFactors.ToList().GetStatFactorFromList(stat) ?? -1f;

            return tool != null;
        }

        public static SurvivalTool GetBestSurvivalTool(this Pawn pawn, StatDef stat)
        {
            SurvivalTool tool = null;
            float statFactor = stat.GetStatPart<StatPart_SurvivalTool>().NoToolStatFactor;

            List<Thing> allTools = pawn.equipment?.GetDirectlyHeldThings().Concat(pawn.inventory?.GetDirectlyHeldThings()).Where(t => t is SurvivalTool).ToList();
            foreach (SurvivalTool curTool in allTools)
                foreach (StatModifier modifier in curTool.WorkStatFactors)
                    if (modifier.stat == stat && modifier.value > statFactor)
                    {
                        tool = curTool;
                        statFactor = modifier.value;
                    }

            return tool;
        }

        public static string GetSurvivalToolOverrideReportText(SurvivalTool tool, StatDef stat)
        {
            List<StatModifier> statFactorList = tool.WorkStatFactors.ToList();
            StuffPropsTool stuffPropsTool = tool.StuffProps;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(stat.description);

            builder.AppendLine();
            builder.AppendLine(tool.def.LabelCap + ": " + tool.ToolProps.baseWorkStatFactors.GetStatFactorFromList(stat).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Factor));

            builder.AppendLine();
            builder.AppendLine(ST_StatDefOf.ToolEffectivenessFactor.LabelCap + ": " +
                tool.GetStatValue(ST_StatDefOf.ToolEffectivenessFactor).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Factor));

            if (stuffPropsTool != null && stuffPropsTool.toolStatFactors.StatFactorsListContains(stat))
            {
                builder.AppendLine();
                builder.AppendLine("StatsReport_Material".Translate() + " (" + tool.Stuff.LabelCap + "): " +
                    stuffPropsTool.toolStatFactors.GetStatFactorFromList(stat).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Factor));
            }

            builder.AppendLine();
            builder.AppendLine("StatsReport_FinalValue".Translate() + ": " + statFactorList.GetStatFactorFromList(stat).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Factor));
            return builder.ToString();
        }

        private static bool StatFactorsListContains(this List<StatModifier> list, StatDef stat)
        {
            foreach (StatModifier modifier in list)
                if (modifier.stat == stat)
                    return true;
            return false;
        }

        public static void TryApplyToolWear(SurvivalTool tool)
        {
            if (tool != null && Rand.Chance(tool.WearChancePerTick))
                tool.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 1));
        }

        // A transpiler-friendly version of the above overload
        public static void TryApplyToolWear(Pawn pawn, StatDef stat) =>
            TryApplyToolWear(pawn.GetBestSurvivalTool(stat));

        public static bool IsSurvivalTool(this BuildableDef def, out SurvivalToolProperties toolProps)
        {
            toolProps = def.GetModExtension<SurvivalToolProperties>();
            return def.IsSurvivalTool();
        }

        public static float HeldSurvivalToolCount(this Pawn pawn) =>
            pawn.inventory?.innerContainer?.Where(t => t.def.IsSurvivalTool()).Count() ?? 0f;

        public static bool CanCarryAnyMoreSurvivalTools(this Pawn pawn) =>
            pawn.RaceProps.Humanlike && pawn.HeldSurvivalToolCount() < MaxToolsCarriedAtOnce;

        public static bool IsSurvivalTool(this BuildableDef def) =>
            def is ThingDef tDef && tDef.thingClass == typeof(SurvivalTool) && tDef.HasModExtension<SurvivalToolProperties>();

        public static bool MeetsWorkGiverStatRequirement(this Pawn pawn, StatDef requiredStat) =>
            pawn.GetStatValue(requiredStat) > 0f;

    }
}
