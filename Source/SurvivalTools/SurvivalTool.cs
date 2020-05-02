using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SurvivalTools
{
    public class SurvivalTool : ThingWithComps
    {
        public int workTicksDone = 0;
        public bool toBeForced = false;

        #region Properties

        public Pawn HoldingPawn
        {
            get
            {
                if (ParentHolder is Pawn_EquipmentTracker eq)
                    return eq.pawn;
                if (ParentHolder is Pawn_InventoryTracker inv)
                    return inv.pawn;
                return null;
            }
        }

        public bool InUse = false;
        public bool Forced = false;

        public int WorkTicksToDegrade => Mathf.FloorToInt(
                (this.GetStatValue(ST_StatDefOf.ToolEstimatedLifespan) * GenDate.TicksPerDay) / this.MaxHitPoints);

        public IEnumerable<StatModifier> WorkStatFactors
        {
            get
            {
                foreach (StatModifier modifier in def.GetModExtension<SurvivalToolProperties>().baseWorkStatFactors)
                {
                    float newFactor = modifier.value * this.GetStatValue(ST_StatDefOf.ToolEffectivenessFactor, false);

                    if (Stuff?.GetModExtension<StuffPropsTool>()?.toolStatFactors.NullOrEmpty() == false)
                        foreach (StatModifier modifier2 in Stuff?.GetModExtension<StuffPropsTool>()?.toolStatFactors)
                            if (modifier2.stat == modifier.stat)
                                newFactor *= modifier2.value;

                    yield return new StatModifier
                    {
                        stat = modifier.stat,
                        value = newFactor
                    };
                }
            }
        }

        public override string LabelNoCount
        {
            get
            {
                string label = base.LabelNoCount;

                if (InUse)
                    label = $"{"ToolInUse".Translate()}: " + label;

                if (Forced)
                    label += $", {"ApparelForcedLower".Translate()}";

                return label;
            }
        }

        #endregion Properties

        #region Methods
        public void CheckIfUsed(Pawn_SurvivalToolAssignmentTracker assignmentTracker, bool changeList = false)
        {
            if (HoldingPawn == null || HoldingPawn?.NeedsSurvivalTool(this) == false
                || HoldingPawn?.CanUseSurvivalTools() == false || HoldingPawn?.CanUseSurvivalTool(def) == false)
            {
                InUse = false;
                if (changeList && HoldingPawn?.TryGetComp<Pawn_SurvivalToolAssignmentTracker>()?.ToolsInUse.Contains(this) == true)
                    HoldingPawn?.TryGetComp<Pawn_SurvivalToolAssignmentTracker>()?.ToolsInUse.Remove(this);
                return;
            }
            float maxTools = HoldingPawn.GetStatValue(ST_StatDefOf.SurvivalToolCarryCapacity, false);
            if (SurvivalToolUtility.BestSurvivalToolsFor(HoldingPawn).Contains(this) &&
                (assignmentTracker?.ToolsInUse.Count < maxTools || assignmentTracker?.ToolsInUse.Contains(this) == true))
            {
                InUse = true;
                if (changeList)
                    HoldingPawn?.TryGetComp<Pawn_SurvivalToolAssignmentTracker>()?.ToolsInUse.AddDistinct(this);
            }
            else
            {
                InUse = false;
                if (changeList && HoldingPawn?.TryGetComp<Pawn_SurvivalToolAssignmentTracker>()?.ToolsInUse.Contains(this) == true)
                    HoldingPawn?.TryGetComp<Pawn_SurvivalToolAssignmentTracker>()?.ToolsInUse.Remove(this);
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            /*
            public StatDrawEntry(StatCategoryDef category,
            string label,
            string valueString,
            string reportText,
            int displayPriorityWithinCategory,
            string overrideReportTitle = null,
            IEnumerable<Dialog_InfoCard.Hyperlink> hyperlinks = null,
            bool forceUnfinalizedMode = false)
            */
            foreach (StatModifier modifier in WorkStatFactors)
                yield return new
                    StatDrawEntry(ST_StatCategoryDefOf.SurvivalTool, //calling StatDraw Entry and Category
                    modifier.stat.LabelCap, //Capatalize the Label?
                    modifier.value.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor), //Dunno what this does, I think show the value?
                    reportText: modifier.stat.description, // Desc of the stat?
                    displayPriorityWithinCategory: 99999,  // Priority of the display?
                    overrideReportTitle: SurvivalToolUtility.GetSurvivalToolOverrideReportText(this, modifier.stat), //show me somethin.
                    hyperlinks: null, // ingame hyperlinks in description
                    forceUnfinalizedMode: false //Dunno what this is, so lets set it to false and find out if it breaks shit.

                    );
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workTicksDone, "workTicksDone", 0);
        }

        #endregion Methods
    }
}