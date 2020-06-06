using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SurvivalTools
{
    public class SurvivalToolType : Def
    {
        private static Dictionary<JobDef, List<StatModifier>> allNoToolDrictionaryRegular = new Dictionary<JobDef, List<StatModifier>>();
        private static Dictionary<JobDef, List<StatModifier>> allNoToolDrictionaryHardCore = new Dictionary<JobDef, List<StatModifier>>();
        public static Dictionary<JobDef, List<StatModifier>> allNoToolDrictionary
            => (SurvivalToolsSettings.hardcoreMode) ? allNoToolDrictionaryHardCore : allNoToolDrictionaryRegular;
        public static List<JobDef> allAffectedJobs = new List<JobDef>();
        public void RegisterJobDef(JobDef jobDef)
        {
            if (allNoToolDrictionaryRegular.ContainsKey(jobDef))
                foreach (StatModifier modifier in noToolStatFactorsRegular)
                {
                    for (int i = 0; i < allNoToolDrictionaryRegular[jobDef].Count; i++)
                        if (allNoToolDrictionaryRegular[jobDef][i].stat == modifier.stat)
                        {
                            if (modifier.value < allNoToolDrictionaryRegular[jobDef][i].value)
                                allNoToolDrictionaryRegular[jobDef][i].value = modifier.value;
                            goto Skip1;
                        }
                    allNoToolDrictionaryRegular[jobDef].Add(modifier);
                }
            else
                allNoToolDrictionaryRegular.Add(jobDef, noToolStatFactorsRegular);
            Skip1:
            if (allNoToolDrictionaryHardCore.ContainsKey(jobDef))
                foreach (StatModifier modifier in noToolStatFactorsHardCore)
                {
                    for (int i = 0; i < allNoToolDrictionaryHardCore[jobDef].Count; i++)
                        if (allNoToolDrictionaryHardCore[jobDef][i].stat == modifier.stat)
                        {
                            if (modifier.value < allNoToolDrictionaryHardCore[jobDef][i].value)
                                allNoToolDrictionaryHardCore[jobDef][i].value = modifier.value;
                            goto Skip2;
                        }
                    allNoToolDrictionaryHardCore[jobDef].Add(modifier);
                }
            else
                allNoToolDrictionaryHardCore.Add(jobDef, noToolStatFactorsRegular);
            Skip2:
            jobList.AddDistinct(jobDef);
            allAffectedJobs.AddDistinct(jobDef);
        }
        public static readonly List<SurvivalToolType> allDefs = new List<SurvivalToolType>();
        public List<StatModifier> baseWorkStatFactors = new List<StatModifier>();
        private List<StatModifier> noToolStatFactorsRegular = new List<StatModifier>();
        private List<StatModifier> noToolStatFactorsHardCore = new List<StatModifier>();
        public List<StatModifier> efficiencyModifiers = new List<StatModifier>();
        public List<StatModifier> noToolStatFactors
            => (SurvivalToolsSettings.hardcoreMode) ? noToolStatFactorsHardCore : noToolStatFactorsRegular;
        private float noToolFactorRegular = 0.3f;
        private float noToolFactorHardcore = -1f;
        public float noToolFactor
            => (SurvivalToolsSettings.hardcoreMode) ? noToolFactorHardcore : noToolFactorRegular;
        public List<JobDef> jobList = new List<JobDef>();
        public List<JobDef> jobSpecific = new List<JobDef>();
        public List<JobDef> jobException = new List<JobDef>();
        private List<Type> jobDriverSpecific = new List<Type>();
        private List<Type> jobDriverException = new List<Type>();
        public List<WorkGiverDef> relevantWorkGivers = new List<WorkGiverDef>();
        private float NoToolFactorHardcore =>
            (noToolFactorHardcore != -1f) ? noToolFactorHardcore : noToolFactorRegular;

        [NoTranslate]
        public List<string> defaultSurvivalToolAssignmentTags;
        public List<StatDef> stats = new List<StatDef>();
        public SurvivalToolType()
        {
            allDefs.Add(this);
        }
        public bool initialized = false;
        public void Initialize()
        {
            if (initialized)
                return;
            foreach (StatModifier modifier in baseWorkStatFactors)
            {
                stats.AddDistinct(modifier.stat);
                if (!noToolStatFactorsRegular.StatListContains(modifier.stat))
                    noToolStatFactorsRegular.Add(new StatModifier() { stat = modifier.stat, value = noToolFactorRegular });
                if (!noToolStatFactorsHardCore.StatListContains(modifier.stat))
                    noToolStatFactorsHardCore.Add(new StatModifier() { stat = modifier.stat, value = NoToolFactorHardcore });
            }
            foreach (JobDef jobDef in DefDatabase<JobDef>.AllDefsListForReading)
            {
                if (jobDriverSpecific.Any(t => t.IsAssignableFrom(jobDef.driverClass) && !jobException.Contains(jobDef)))
                    jobSpecific.AddDistinct(jobDef);
                if (jobDriverException.Any(t => t.IsAssignableFrom(jobDef.driverClass)))
                    jobException.AddDistinct(jobDef);
            }
            foreach (JobDef jobDef in jobSpecific)
                RegisterJobDef(jobDef);
            initialized = true;
        }
        public bool AffectsJob(JobDef jobDef)
            => jobList.Contains(jobDef);
        public float StuffEffect(ThingDef stuff)
        {
            if (stuff == null)
                return 1f;
            float factor = 0f;
            foreach (StatModifier modifier in efficiencyModifiers)
                factor += modifier.value * stuff.stuffProps.statFactors.GetStatFactorFromList(modifier.stat);
            return factor;
        }
    }
}
