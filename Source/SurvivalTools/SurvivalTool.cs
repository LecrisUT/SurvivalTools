using RimWorld;
using System.Collections.Generic;
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

        public int WorkTicksToDegrade => Mathf.FloorToInt(
                (this.GetStatValue(ST_StatDefOf.ToolEstimatedLifespan) * GenDate.TicksPerDay) / this.MaxHitPoints);

        public IEnumerable<SurvivalToolTypeModifier> toolTypeModifiers
        {
            get
            {
                foreach(SurvivalToolTypeModifier modifier in this.GetToolProperties().GetToolTypesValue(Stuff))
                    yield return new SurvivalToolTypeModifier()
                    {
                        toolType = modifier.toolType,
                        value = modifier.value * this.GetStatValue(ST_StatDefOf.ToolEffectivenessFactor, false)
                    };
            }
        }
        public bool TryGetJobValue(JobDef job, StatDef stat, out float value)
            => this.GetToolProperties().GetValue_Job(job, stat, this, out value, out _);
        public bool TryGetJobValue(JobDef job, out float value)
            => this.GetToolProperties().GetValue_Job(job, this, out value, out _);
        public bool TryGetTypeValue(SurvivalToolType toolType, out float value)
            => this.GetToolProperties().GetValue_Type(toolType, this, out value);
        public bool TryGetTypeStatValue(SurvivalToolType toolType, StatDef stat, out float value)
            => this.GetToolProperties().GetValue_Stat(toolType, stat, this, out value);
        public List<JobDef> GetJobList()
            => this.GetToolProperties().jobList;
        public List<JobDef> GetBonusJobList()
            => this.GetToolProperties().jobBonusList;
        public List<StatDef> GetStatList()
            => this.GetToolProperties().stats;

        public override string LabelNoCount
        {
            get
            {
                string label = base.LabelNoCount;
                Pawn_SurvivalToolAssignmentTracker assignmentTracker = HoldingPawn?.TryGetComp<Pawn_SurvivalToolAssignmentTracker>();
                if (assignmentTracker != null)
                {
                    if (assignmentTracker.usedHandler.IsUsed(this))
                        label = $"{"ToolInUse".Translate()}: " + label;
                    if (assignmentTracker.forcedHandler.IsForced(this))
                        label += $", {"ApparelForcedLower".Translate()}";
                }
                return label;
            }
        }

        #endregion Properties

        #region Methods
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            foreach (SurvivalToolTypeModifier modifier in toolTypeModifiers)
                yield return new
                    StatDrawEntry(ST_StatCategoryDefOf.SurvivalTool, //calling StatDraw Entry and Category
                    modifier.toolType.LabelCap, //Capatalize the Label?
                    modifier.value.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor), //Dunno what this does, I think show the value?
                    reportText: modifier.toolType.description, // Desc of the stat?
                    displayPriorityWithinCategory: 99999,  // Priority of the display?
                    overrideReportTitle: SurvivalToolUtility.GetSurvivalToolOverrideReportText(this, modifier), //show me somethin.
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