using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;

namespace SurvivalTools
{
    public static class AutoPatch
    {
        public static List<StatPatchDef> StatsToPatch = DefDatabase<StatPatchDef>.AllDefsListForReading;

    }
    public class StatPatchDef : Def
    {
        public StatDef oldStat;
        public StatDef newStat;
        public Type oldStatType = typeof(StatDefOf);
        public Type newStatType = typeof(ST_StatDefOf);
        // Patch jobDrivers to use ST stats
        private bool patchAllJobDrivers = true;
        private List<Type> JobDriverExemption = new List<Type>();
        private List<Type> JobDriverList = new List<Type>();
        // Patch workGivers to require tool
        private bool patchAllWorkGivers = true;
        private List<Type> WorkGiverExemption = new List<Type>();
        private List<Type> WorkGiverList = new List<Type>();
        public List<JobDriverPatch> FoundJobDrivers = new List<JobDriverPatch>();
        public List<WorkGiverPatch> FoundWorkGivers = new List<WorkGiverPatch>();
        public List<JobDefPatch> FoundJobDef = new List<JobDefPatch>();
        public bool skip;
        public void CheckJobDriver(Type jd)
        {
            if (patchAllJobDrivers)
                skip = JobDriverExemption.Contains(jd) ? true : false;
            else
                skip = JobDriverList.Contains(jd) ? false : true;
        }
        public void CheckWorkGiver(Type wg)
        {
            if (patchAllWorkGivers)
                skip = WorkGiverExemption.Contains(wg) ? true : false;
            else
                skip = WorkGiverList.Contains(wg) ? false : true;
        }
    }
    public class JobDriverPatch
    {
        public Type driver;
        public List<MethodInfo> methods = new List<MethodInfo>();
        public List<MethodInfo> auxmethods = new List<MethodInfo>();
        public List<MethodInfo> degradeMethods = new List<MethodInfo>();
        public bool FoundStage1 = false;
        public bool FoundStage2 = false;
        public JobDriverPatch(Type Driver) { driver = Driver; }
    }
    public class JobDefPatch
    {
        public JobDef def;
        public FieldInfo fieldInfo;
        public JobDefPatch(JobDef Def, FieldInfo FInfo) { def = Def; fieldInfo = FInfo; }
    }
    public class WorkGiverPatch
    {
        public Type giver;
        public List<MethodInfo> methods = new List<MethodInfo>();
        public WorkGiverPatch(Type Giver) { giver = Giver; }
    }
}
