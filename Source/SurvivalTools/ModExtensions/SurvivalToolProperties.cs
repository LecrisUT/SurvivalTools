using RimWorld;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SurvivalTools
{
    public class SurvivalToolProperties : DefModExtension
    {
        public static readonly SurvivalToolProperties defaultValues = new SurvivalToolProperties();

        private readonly List<SurvivalToolTypeModifier> toolTypesValue = new List<SurvivalToolTypeModifier>();
        public List<SurvivalToolType> toolTypes;

        public float toolWearFactor = 1f;
        public List<StatDef> stats = new List<StatDef>();
        private List<KeyValuePair<JobDef, float>> jobBonus = new List<KeyValuePair<JobDef, float>>();
        public List<JobDef> jobBonusList = new List<JobDef>();
        public List<JobDef> jobList = new List<JobDef>();
        private Dictionary<JobDef, (SurvivalToolType, List<StatModifier>)> jobFactors = new Dictionary<JobDef, (SurvivalToolType, List<StatModifier>)>();
        private Dictionary<JobDef, (SurvivalToolType, float)> jobBase = new Dictionary<JobDef, (SurvivalToolType, float)>();
        private Dictionary<SurvivalToolType, List<StatModifier>> toolTypeBestStatModifiers = new Dictionary<SurvivalToolType, List<StatModifier>>();
        public IEnumerable<SurvivalToolTypeModifier> GetToolTypesValue(ThingDef stuff = null)
        {
            if (stuff == null)
                for (int i = 0; i < toolTypesValue.Count; i++)
                    yield return toolTypesValue[i];
            else
                foreach (SurvivalToolTypeModifier modifier in toolTypesValue)
                    yield return new SurvivalToolTypeModifier()
                    {
                        toolType = modifier.toolType,
                        value = modifier.value * modifier.toolType.StuffEffect(stuff)
                    };
        }
        public void Initialize()
        {
            toolTypes = toolTypesValue.Select(t => t.toolType).ToList();
            toolTypes.SelectMany(t => t.stats.AsEnumerable()).Do(tt=>stats.AddDistinct(tt));
            jobList = toolTypes.SelectMany(t => t.jobList).ToList();
            foreach (JobDef job in jobList)
            {
                List<SurvivalToolType> tools = toolTypes.Where(t => t.jobList.Contains(job)).ToList();
                if (tools.Count != 1)
                {
                    StringBuilder warning = new StringBuilder($"[[LC]SurvivalTools] Non-unique tool Types for job={job} [{tools.Count}]\n");
                    foreach(SurvivalToolType toolType in tools)
                    {
                        warning.Append($"{toolType} : ");
                        foreach (JobDef job1 in toolType.jobList)
                            warning.AppendWithComma($"{job1}");
                        warning.AppendLine();
                    }
                }
                SurvivalToolType tooltype = tools.First();
                List<StatModifier> toolTypeStatModifiers = new List<StatModifier>(tooltype.baseWorkStatFactors);
                float bonus = jobBonus.FirstOrFallback(t => t.Key == job, new KeyValuePair<JobDef, float>(job, 1f)).Value;
                float toolTypeFactor = toolTypesValue.First(t => t.toolType == tooltype).value;
                foreach (StatModifier modifier in toolTypeStatModifiers)
                    modifier.value *= bonus * toolTypeFactor;
                if (toolTypeBestStatModifiers.TryGetValue(tooltype, out List<StatModifier> modifierList))
                    foreach(StatModifier modifier in modifierList)
                    {
                        float val = toolTypeStatModifiers.GetStatFactorFromList(modifier.stat);
                        if (val > modifier.value)
                            modifier.value = val;
                    }
                else
                    toolTypeBestStatModifiers.Add(tooltype, new List<StatModifier>(toolTypeStatModifiers));
                jobFactors.Add(job, (tooltype, toolTypeStatModifiers));
                jobBase.Add(job, (tooltype, toolTypeFactor));
            }
            jobBonus.Do(t => jobBonusList.AddDistinct(t.Key));
        }
        public float GetValue_Job(JobDef job, StatDef stat, SurvivalTool tool)
        {
            GetValue_Job(job, stat, tool, out float val, out _);
            return val;
        }
        public float GetValue_Job(JobDef job, SurvivalTool tool)
        {
            GetValue_Job(job, tool, out float val, out _);
            return val;
        }
        public bool GetValue_Job(JobDef job, StatDef stat, SurvivalTool tool, out float val, out SurvivalToolType toolType)
        {
            val = 0f;
            toolType = null;
            if (!jobFactors.TryGetValue(job, out (SurvivalToolType toolType, List<StatModifier> statModifiers) pair))
                return false;
            if (!pair.statModifiers.StatListContains(stat))
                return false;
            toolType = pair.toolType;
            val = pair.statModifiers.GetStatFactorFromList(stat) * toolType.StuffEffect(tool.Stuff) * tool.GetStatValue(ST_StatDefOf.ToolEffectivenessFactor, false);
            return true;
        }
        public bool GetValue_Job(JobDef job, SurvivalTool tool, out float val, out SurvivalToolType toolType)
        {
            val = 0f;
            toolType = null;
            if (!jobBase.TryGetValue(job, out (SurvivalToolType toolType, float val) pair))
                return false;
            toolType = pair.toolType;
            val = pair.val * toolType.StuffEffect(tool.Stuff) * tool.GetStatValue(ST_StatDefOf.ToolEffectivenessFactor, false);
            return true;
        }
        public bool GetValue_Type(SurvivalToolType toolType, SurvivalTool tool, out float val)
        {
            val = 0f;
            if (!toolTypesValue.TryGetValue(toolType, out val, val))
                return false;
            val *= toolType.StuffEffect(tool.Stuff) * tool.GetStatValue(ST_StatDefOf.ToolEffectivenessFactor, false);
            return true;
        }
        public bool GetValue_Stat(SurvivalToolType toolType, StatDef stat, SurvivalTool tool, out float val)
        {
            val = 0f;
            if (!toolTypeBestStatModifiers.TryGetValue(toolType, out List<StatModifier> modifiers))
                return false;
            if (!modifiers.StatListContains(stat))
                return false;
            val = modifiers.GetStatFactorFromList(stat) * toolType.StuffEffect(tool.Stuff) * tool.GetStatValue(ST_StatDefOf.ToolEffectivenessFactor, false);
            return true;
        }
        public bool AffectsJob(JobDef jobDef)
            => jobList.Contains(jobDef);
    }
}