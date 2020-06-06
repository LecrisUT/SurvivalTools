using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            checkAlowedToolTypes();
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
        private List<WorkGiver> assignedWorkGivers = new List<WorkGiver>();
        public List<WorkGiver> AssignedWorkGivers
        {
            get
            {
                if (dirtyCache)
                    Update();
                return assignedWorkGivers;
            }
        }

        // Save allowed ToolTypes based on race, etc.
        public List<SurvivalToolType> allowedToolTypes = new List<SurvivalToolType>();
        private List<SurvivalToolType> requiredToolTypes = new List<SurvivalToolType>();
        public List<SurvivalToolType> RequiredToolTypes
        {
            get
            {
                if (dirtyCache)
                    Update();
                return requiredToolTypes;
            }
        }
        private List<JobDef> assignedJobs = new List<JobDef>();
        public List<JobDef> AssignedJobs
        {
            get
            {
                if (dirtyCache)
                    Update();
                return assignedJobs;
            }
        }
        public bool dirtyCache = false;
        private bool busy = false;
        public void Update()
        {
            if (busy)
                return;
            busy = true;
            Pawn_WorkSettings workSettings = Pawn.workSettings;
            if (workSettings == null)
            {
                Log.ErrorOnce($"Tried to get tool-relevant work givers for {Pawn} but has null workSettings", 11227);
                return;
            }
            List<WorkGiver> workList = new List<WorkGiver>();
            List<SurvivalToolType> toolList = new List<SurvivalToolType>();
            List<JobDef> jobList = new List<JobDef>();
            foreach (WorkGiver giver in Pawn.workSettings.WorkGiversInOrderNormal)
            {
                if (!allowedWorkGiver(Pawn, giver))
                    continue;
                WorkGiverExtension extension = giver.def.GetModExtension<WorkGiverExtension>();
                if (extension != null)
                {
                    workList.Add(giver);
                    extension.requiredToolTypes.Do(t => toolList.AddDistinct(t));
                    extension.relevantJobs.Do(t => jobList.AddDistinct(t));
                }
            }
            assignedWorkGivers = workList;
            requiredToolTypes = toolList;
            assignedJobs = jobList;
            dirtyCache = false;
            busy = false;
            usedHandler.dirtyCache = true;
        }
        public void checkAlowedToolTypes()
        {
            allowedToolTypes = SurvivalToolType.allDefs.ToList();
            RaceExemption rule = MiscDef.IgnoreRaceList.FirstOrFallback(t => t.race == Pawn.kindDef.race);
            if (rule != null)
            {
                if (rule.all)
                    return;
                else
                    foreach (SurvivalToolType toolType in SurvivalToolType.allDefs)
                        if (!rule.checkIfAllowed(toolType) && toolType.relevantWorkGivers.All(t => allowedWorkGiver(Pawn, t.Worker)))
                            allowedToolTypes.Remove(toolType);
            }
        }
        public static MethodInfo WorkTab_CapableOf = AccessTools.Method(typeof(Pawn), "WorkTab.Pawn_Extensions.CapableOf");
        private bool allowedWorkGiver(Pawn pawn, WorkGiver wg)
        {
            if (WorkTab_CapableOf != null)
                return (bool)WorkTab_CapableOf.Invoke(null, new object[] { pawn, wg });
            if (pawn.WorkTypeIsDisabled(wg.def.workType))
                return false;
            return true;
        }
        public bool CanUseTool(SurvivalTool tool)
        {
            List<SurvivalToolType> toolTypes = tool.GetToolProperties().toolTypes;
            if (allowedToolTypes.Any(t => toolTypes.Contains(t)))
                return true;
            return false;
        }
        public bool CanUseToolType(SurvivalToolType toolType)
            => allowedToolTypes.Contains(toolType);
    }
}