using Verse;

namespace SurvivalTools
{
    public class Pawn_SurvivalToolAssignmentTracker : ThingComp, IExposable
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

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            forcedHandler = new SurvivalToolForcedHandler();
            usedHandler = new SurvivalToolUsedHandler(Pawn, this);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref nextSurvivalToolOptimizeTick, "nextSurvivalToolOptimizeTick", -99999);
            Scribe_Deep.Look(ref forcedHandler, "forcedHandler");
            Scribe_Deep.Look(ref usedHandler, "usedHandler");
            Scribe_References.Look(ref curSurvivalToolAssignment, "curSurvivalToolAssignment");
        }

        public int nextSurvivalToolOptimizeTick = -99999;
        public SurvivalToolForcedHandler forcedHandler;
        public SurvivalToolUsedHandler usedHandler;
        private SurvivalToolAssignment curSurvivalToolAssignment;
    }
}