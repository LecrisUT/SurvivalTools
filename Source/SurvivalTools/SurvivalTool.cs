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

        public bool InUse =>
            HoldingPawn != null && HoldingPawn.CanUseSurvivalTools() && HoldingPawn.CanUseSurvivalTool(def) &&
            SurvivalToolUtility.BestSurvivalToolsFor(HoldingPawn).Contains(this);

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

                if (HoldingPawn != null && HoldingPawn.TryGetComp<Pawn_SurvivalToolAssignmentTracker>() is Pawn_SurvivalToolAssignmentTracker tracker &&
                    tracker.forcedHandler.IsForced(this))
                    label = $"({"ApparelForcedLower".Translate()}) " + label;

                if (InUse)
                    label = $"{"ToolInUse".Translate()}: " + label;

                return label;
            }
        }

        #endregion Properties

        #region Methods

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