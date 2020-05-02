﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SurvivalTools
{
    public class Pawn_SurvivalToolAssignmentTracker : ThingComp
    {
        private Pawn Pawn => (Pawn)parent;

        public SurvivalToolAssignment CurrentSurvivalToolAssignment
        {
            get
            {
                if (curSurvivalToolAssignment == null)
                    curSurvivalToolAssignment = Current.Game.GetComponent<SurvivalToolAssignmentDatabase>().DefaultSurvivalToolAssignment();
                return curSurvivalToolAssignment;
            }
            set
            {
                curSurvivalToolAssignment = value;
                nextSurvivalToolOptimizeTick = Find.TickManager.TicksGame;
            }
        }

        public override void CompTick()
        {
            // If forced handler is somehow null, fix that
            if (forcedHandler == null)
                forcedHandler = new SurvivalToolForcedHandler();
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            forcedHandler = new SurvivalToolForcedHandler();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextSurvivalToolOptimizeTick, "nextSurvivalToolOptimizeTick", -99999);
            Scribe_Deep.Look(ref forcedHandler, "forcedHandler");
            Scribe_References.Look(ref curSurvivalToolAssignment, "curSurvivalToolAssignment");
        }

        public int nextSurvivalToolOptimizeTick = -99999;
        public SurvivalToolForcedHandler forcedHandler;
        private SurvivalToolAssignment curSurvivalToolAssignment;
        public List<SurvivalTool> ToolsInUse = new List<SurvivalTool>();
        public void CheckToolsInUse()
        {
            foreach (SurvivalTool tool in ToolsInUse)
                tool.CheckIfUsed(this);
            ToolsInUse.RemoveAll(t => !t.InUse);
            foreach (SurvivalTool tool in Pawn.GetUnusedSurvivalTools())
                tool.CheckIfUsed(this);
            ToolsInUse = Pawn.GetUsedSurvivalTools().ToList();
        }
    }
}