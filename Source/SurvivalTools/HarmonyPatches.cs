using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using HugsLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using Verse.AI;

namespace SurvivalTools.HarmonyPatches
{
    internal class HarmonyPatches : ModBase
    {
        private static readonly Type patchType = typeof(HarmonyPatches);
        public static Harmony harmony = new Harmony("jelly.survivaltoolsreborn");
        public override void DefsLoaded()
        {
            // Automatic patches
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            AutoPatchInitialize();
            PatchJobDrivers();
            FindJobDefs();
            PatchWorkGivers();
            PrintPatchDebug();
            // Search and add LTS's Degrade mod
            Type LTS_Degradation_Utility = GenTypes.GetTypeInAnyAssembly("Degradation.Utility.Utility", null);
            if (LTS_Degradation_Utility != null)
                SurvivalToolUtility.modDegrade = AccessTools.Method(LTS_Degradation_Utility, "DegradeTool");
            // Manual patches
            // Plants that obstruct construction zones
            // erdelf never fails to impress :)
            var postfixHandleBlockingThingJob = new HarmonyMethod(patchType, nameof(Postfix_HandleBlockingThingJob));
            harmony.Patch(AccessTools.Method(typeof(GenConstruct), nameof(GenConstruct.HandleBlockingThingJob)), postfix: postfixHandleBlockingThingJob);
            harmony.Patch(AccessTools.Method(typeof(RoofUtility), nameof(RoofUtility.HandleBlockingThingJob)), postfix: postfixHandleBlockingThingJob);
            if (!ModCompatibilityCheck.OtherInventoryModsActive)
                harmony.Patch(AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders"), transpiler: new HarmonyMethod(patchType, nameof(Transpile_FloatMenuMakerMad_AddHumanlikeOrders)));

            //// Combat Extended
            //if (ModCompatibilityCheck.CombatExtended)
            //{
            //    // Prevent tools from incorrectly being removed based on loadout
            //    var combatExtendedHoldTrackerExcessThingClass = GenTypes.GetTypeInAnyAssembly("CombatExtended.Utility_HoldTracker", null);
            //    if (combatExtendedHoldTrackerExcessThingClass != null)
            //        harmony.Patch(AccessTools.Method(combatExtendedHoldTrackerExcessThingClass, "GetExcessThing"), postfix: new HarmonyMethod(patchType, nameof(Postfix_CombatExtended_Utility_HoldTracker_GetExcessThing)));
            //    else
            //        Log.Error("Survival Tools - Could not find CombatExtended.Utility_HoldTracker type to patch");

            //    // Prevent pawns from picking up excess tools with Combat Extended's CompInventory
            //    var combatExtendedCompInventory = GenTypes.GetTypeInAnyAssembly("CombatExtended.CompInventory", null);
            //    if (combatExtendedCompInventory != null)
            //        harmony.Patch(AccessTools.Method(combatExtendedCompInventory, "CanFitInInventory"), postfix: new HarmonyMethod(patchType, nameof(Postfix_CombatExtended_CompInventory_CanFitInInventory)));
            //    else
            //        Log.Error("Survival Tools - Could not find CombatExtended.CompInventory type to patch");
            //}
        }
        #region AutoPatch_Properties
        public static List<StatPatchDef> FoundPatch = new List<StatPatchDef>();
        public static List<MethodInfo> auxMethods;
        public static List<StatPatchDef> auxPatch = new List<StatPatchDef>();
        public static List<Type> JobDefOfTypes;
        private static MethodInfo TryDegradeTool =>
           AccessTools.Method(typeof(SurvivalToolUtility), nameof(SurvivalToolUtility.TryDegradeTool), new[] { typeof(Pawn), typeof(StatDef) });
        #endregion
        #region AutoPatch_Transpiler_Methods
        private static bool SearchForStat(MethodInfo method)
        {
            IEnumerable<KeyValuePair<OpCode, object>> instructions = PatchProcessor.ReadMethodBody(method);
            foreach (KeyValuePair<OpCode, object> instruction in instructions)
            {
                if (instruction.Key == OpCodes.Ldsfld)
                {
                    FieldInfo field = instruction.Value as FieldInfo;
                    if (AutoPatch.StatsToPatch.Exists(t => !t.skip && field == t.oldStatFieldInfo))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        private static bool SearchCalledFunction(MethodInfo method)
        {
            //Log.Message("TEst3.0");
            IEnumerable<KeyValuePair<OpCode, object>> instructions = PatchProcessor.ReadMethodBody(method);
            foreach (KeyValuePair<OpCode, object> instruction in instructions)
            {
                if (instruction.Key == OpCodes.Call || instruction.Key == OpCodes.Callvirt)
                {
                    MethodInfo calledMethod = instruction.Value as MethodInfo;
                    if (auxMethods.Contains(calledMethod))
                    {
                        //Log.Message($"TEst3.1 : {instruction.opcode} : {instruction.operand}");
                        return true;
                    }
                }
            }
            //Log.Message("TEst3.2");
            return false;
        }
        private static List<StatPatchDef> SearchForJob(MethodInfo method)
        {
            IEnumerable<KeyValuePair<OpCode, object>> instructions = PatchProcessor.ReadMethodBody(method);
            List<StatPatchDef> fPatch = new List<StatPatchDef>();
            foreach (KeyValuePair<OpCode, object> instruction in instructions)
            {
                if (instruction.Key == OpCodes.Ldsfld)
                {
                    FieldInfo field = instruction.Value as FieldInfo;
                    foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => !t.skip))
                        if (patch.FoundJobDef.Exists(t => t.fieldInfo == field))
                            fPatch.Add(patch);
                }
            }
            return fPatch;
        }
        private static IEnumerable<CodeInstruction> Transpile_Replace_Stat(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            foreach (CodeInstruction instruction in instructionList)
            {
                if (instruction.opcode == OpCodes.Ldsfld)
                {
                    foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => !t.skip))
                    {
                        if (instruction.operand as FieldInfo == AccessTools.Field(patch.oldStatType, patch.oldStat.defName))
                        {
                            FoundPatch.AddDistinct(patch);
                            if (patch.newStat != null)
                                instruction.operand = AccessTools.Field(typeof(ST_StatDefOf), patch.newStat.defName);
                            break;
                        }
                    }
                    //if (FoundWorker) break;
                }
            }
            return instructions.AsEnumerable();
        }
        private static IEnumerable<CodeInstruction> Transpile_AddDegrade(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction0 = instructionList[i];
                if (instruction0.opcode == OpCodes.Ldfld)
                {
                    if (instruction0.operand as FieldInfo == AccessTools.Field(typeof(JobDriver), nameof(JobDriver.pawn)) || instruction0.operand as FieldInfo == AccessTools.Field(typeof(Toil), nameof(Toil.actor)))
                    {
                        CodeInstruction instruction1 = instructionList[i - 1];
                        CodeInstruction instruction2 = instructionList[i - 2];
                        foreach (StatPatchDef patch in FoundPatch)
                        {
                            instructionList.Insert(i + 1, instruction0);
                            instructionList.Insert(i + 1, instruction1);
                            instructionList.Insert(i + 1, instruction2);
                            instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Call, TryDegradeTool));
                            if (patch.newStat is null)
                                instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(StatDefOf), patch.oldStat.defName)));
                            else
                                instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ST_StatDefOf), patch.newStat.defName)));
                        }
                        break;
                    }
                }
            }
            return instructionList.AsEnumerable();
        }
        #endregion
        #region ManualPatches
        public static void Postfix_HandleBlockingThingJob(ref Job __result, Pawn worker)
        {
            if (__result?.def == JobDefOf.CutPlant && __result.targetA.Thing.def.plant.IsTree)
            {
                if (worker.MeetsWorkGiverStatRequirements(ST_WorkGiverDefOf.FellTrees.GetModExtension<WorkGiverExtension>().requiredStats))
                    __result = new Job(ST_JobDefOf.FellTree, __result.targetA);
                else
                    __result = null;
            }
        }
        public static IEnumerable<CodeInstruction> Transpile_FloatMenuMakerMad_AddHumanlikeOrders(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo playerHome = AccessTools.Property(typeof(Map), nameof(Map.IsPlayerHome)).GetGetMethod();
            List<CodeInstruction> instructionList = instructions.ToList();

            //instructionList.RemoveRange(instructions.FirstIndexOf(ci => ci.operand == playerHome) - 3, 5);
            //return instructionList;

            bool patched = false;

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if (!patched && (instruction.operand as MethodInfo) == playerHome)
                // if (!patched && (instruction.operand == playerHome) // CE, Pick Up And Haul etc.
                //if (instructionList[i + 3].opcode == OpCodes.Callvirt && instruction.operand == playerHome)
                //if (instructionList[i + 3].operand == playerHome)
                {
                    {
                        instruction.opcode = OpCodes.Ldc_I4_0;
                        instruction.operand = null;
                        yield return instruction;
                        patched = true;
                    }
                    //    //{ instructionList[i + 5].labels = instruction.labels;}
                    //    instructionList.RemoveRange(i, 5);
                    //    patched = true;
                }
                yield return instruction;
            }
        }
        //public static void Postfix_JobDriver_MineQuarry_Mine(JobDriver __instance, Toil __result)
        //{
        //    __result.defaultDuration = (int)Mathf.Clamp(3000f / pawn.GetStatValue(ST_StatDefOf.DiggingSpeed, false), 500f, 10000f);
        //}
        public static void Postfix_CombatExtended_Utility_HoldTracker_GetExcessThing(ref bool __result, Thing dropThing)
        {
            // If there's an excess thing to be dropped for automatic loadout fixing and that thing is a tool, don't treat it as an excess thing
            if (__result && dropThing as SurvivalTool != null)
                __result = false;
        }

        public static void Postfix_CombatExtended_CompInventory_CanFitInInventory(ThingComp __instance, ref bool __result, Thing thing, ref int count)
        {
            // If the pawn could normally take an item to inventory - check if that item's a tool and obeys the pawn's carrying capacity
            if (__result && thing is SurvivalTool tool)
            {
                var compParentPawn = __instance.parent as Pawn;
                if (compParentPawn != null && !compParentPawn.CanCarryAnyMoreSurvivalTools())
                {
                    count = 0;
                    __result = false;
                }
            }
        }
        #endregion
        #region AutoPatch_Loops
        public void PatchJobDrivers()
        {
            HarmonyMethod transpileReplaceStat = new HarmonyMethod(patchType, nameof(Transpile_Replace_Stat));
            HarmonyMethod transpileAddDegrade = new HarmonyMethod(patchType, nameof(Transpile_AddDegrade));
            // AutoPatch
            IEnumerable<Type> AllJobDrivers = GenTypes.AllSubclasses(typeof(JobDriver));
            foreach (Type jobDriver in AllJobDrivers)
            {
                FoundPatch = new List<StatPatchDef>();
                // Check which patch can be ignored
                foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
                    patch.CheckJobDriver(jobDriver);
                // Patch auxiliary methods: Don't add ToolDegrade here
                List<MethodInfo> jbMethods = AccessTools.GetDeclaredMethods(jobDriver);
                foreach (MethodInfo jbMethod in jbMethods)
                {
                    if (jbMethod.IsAbstract)
                        continue;
                    if (SearchForStat(jbMethod))
                    {
                        harmony.Patch(jbMethod, transpiler: transpileReplaceStat);
                        foreach (StatPatchDef patch in FoundPatch)
                        {
                            JobDriverPatch jdpatch = patch.FoundJobDrivers.Find(t => t.driver == jobDriver);
                            if (jdpatch is null)
                            {
                                jdpatch = new JobDriverPatch(jobDriver);
                                patch.FoundJobDrivers.Add(jdpatch);
                            }
                            jdpatch.methods.Add(jbMethod);
                            jdpatch.auxmethods.Add(jbMethod);
                            jdpatch.FoundStage1 = true;
                        }
                    }
                }
                // Patch MakeToil: Assumed is in delegated method: add ToolDegrade here
                auxPatch = FoundPatch;
                Type[] nestedTypes = jobDriver.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (Type nType in nestedTypes)
                {
                    jbMethods = AccessTools.GetDeclaredMethods(nType);
                    foreach (MethodInfo jbMethod in jbMethods)
                    {
                        FoundPatch = new List<StatPatchDef>();
                        if (jbMethod.IsAbstract)
                            continue;
                        if (SearchForStat(jbMethod))
                        {
                            harmony.Patch(jbMethod, transpiler: transpileReplaceStat);
                            foreach (StatPatchDef patch in FoundPatch)
                            {
                                if (patch.addTollDegrade)
                                    harmony.Patch(jbMethod, transpiler: transpileAddDegrade);
                                JobDriverPatch jdpatch = patch.FoundJobDrivers.Find(t => t.driver == jobDriver);
                                if (jdpatch is null)
                                {
                                    jdpatch = new JobDriverPatch(jobDriver);
                                    patch.FoundJobDrivers.Add(jdpatch);
                                }
                                jdpatch.methods.Add(jbMethod);
                                jdpatch.FoundStage2 = true;
                            }
                        }
                    }
                }
                // Patch MakeToil again: Add ToolDegrade if there are function calls with speed stat
                foreach (StatPatchDef patch in auxPatch.Where(t => t.addTollDegrade))
                {
                    JobDriverPatch jdpatch = patch.FoundJobDrivers.Find(t => t.driver == jobDriver);
                    if (jdpatch.FoundStage2) continue;
                    auxMethods = jdpatch.auxmethods;
                    FoundPatch = new List<StatPatchDef> { patch };
                    foreach (Type nType in nestedTypes)
                    {
                        jbMethods = AccessTools.GetDeclaredMethods(nType);
                        foreach (MethodInfo jbMethod in jbMethods)
                        {
                            if (jbMethod.IsAbstract)
                                continue;
                            if (SearchCalledFunction(jbMethod))
                            {
                                jdpatch.FoundStage2 = true;
                                harmony.Patch(jbMethod, transpiler: transpileAddDegrade);
                                jdpatch.methods.Add(jbMethod);
                                break;
                            }
                        }
                        if (jdpatch.FoundStage2)
                            break;
                    }
                    // Patch MakeToil again: Add ToolDegrade to the base jobDriver
                    if (jdpatch.FoundStage2)
                        continue;
                    List<MethodInfo> auxMethods2 = auxMethods;
                    Type baseType = jobDriver;
                    while (true)
                    {
                        baseType = baseType.BaseType;
                        if (baseType == typeof(JobDriver) || baseType is null)
                        {
                            Logger.Warning($"Couldn't backtrack jobDriver patch: {jobDriver} => {baseType}");
                            break;
                        }
                        auxMethods = new List<MethodInfo>();
                        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(baseType))
                            if (auxMethods2.Exists(t => t.Name == method.Name))
                                auxMethods.Add(method);
                        Type[] nestedTypes2 = baseType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (Type nType in nestedTypes2)
                        {
                            jbMethods = AccessTools.GetDeclaredMethods(nType);
                            foreach (MethodInfo jbMethod in jbMethods)
                            {
                                if (jbMethod.IsAbstract)
                                    continue;
                                if (SearchCalledFunction(jbMethod))
                                {
                                    jdpatch.FoundStage2 = true;
                                    harmony.Patch(jbMethod, transpiler: transpileAddDegrade);
                                    jdpatch.methods.Add(jbMethod);
                                    break;
                                }
                            }
                            if (jdpatch.FoundStage2) break;
                        }
                        if (jdpatch.FoundStage2) break;
                    }
                }
            }
            // Extra Patches
            foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => t.OtherTypes.Count > 0))
            {
                patch.skip = false;
                foreach (Type jobDriver in patch.OtherTypes)
                {
                    // Patch auxiliary methods: Don't add ToolDegrade here
                    List<MethodInfo> jbMethods = AccessTools.GetDeclaredMethods(jobDriver);
                    foreach (MethodInfo jbMethod in jbMethods)
                    {
                        if (jbMethod.IsAbstract)
                            continue;
                        if (SearchForStat(jbMethod))
                        {
                            harmony.Patch(jbMethod, transpiler: transpileReplaceStat);
                            JobDriverPatch jdpatch = patch.FoundJobDrivers.Find(t => t.driver == jobDriver);
                            if (jdpatch is null)
                            {
                                jdpatch = new JobDriverPatch(jobDriver);
                                patch.FoundJobDrivers.Add(jdpatch);
                            }
                            jdpatch.methods.Add(jbMethod);
                            jdpatch.FoundStage1 = true;
                        }
                    }
                    // Patch MakeToil: Assumed is in delegated method: add ToolDegrade here
                    Type[] nestedTypes = jobDriver.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (Type nType in nestedTypes)
                    {
                        jbMethods = AccessTools.GetDeclaredMethods(nType);
                        foreach (MethodInfo jbMethod in jbMethods)
                        {
                            if (jbMethod.IsAbstract)
                                continue;
                            if (SearchForStat(jbMethod))
                            {
                                harmony.Patch(jbMethod, transpiler: transpileReplaceStat);
                                if (patch.addTollDegrade)
                                    harmony.Patch(jbMethod, transpiler: transpileAddDegrade);
                                JobDriverPatch jdpatch = patch.FoundJobDrivers.Find(t => t.driver == jobDriver);
                                if (jdpatch is null)
                                {
                                    jdpatch = new JobDriverPatch(jobDriver);
                                    patch.FoundJobDrivers.Add(jdpatch);
                                }
                                jdpatch.methods.Add(jbMethod);
                                jdpatch.FoundStage2 = true;
                            }
                        }
                    }
                }
            }
        }
        public void FindJobDefs()
        {
            foreach (JobDef jobDef in DefDatabase<JobDef>.AllDefsListForReading)
            {
                bool found = false;
                foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
                {
                    foreach (JobDriverPatch jdPatch in patch.FoundJobDrivers)
                        if (jobDef.driverClass.IsSubclassOf(jdPatch.driver) || jobDef.driverClass == jdPatch.driver)
                        {
                            found = true;
                            foreach (Type defOfType in JobDefOfTypes)
                            {
                                FieldInfo fieldInfo = AccessTools.Field(defOfType, jobDef.defName);
                                if (fieldInfo != null)
                                {
                                    patch.FoundJobDef.Add(new JobDefPatch(jobDef, fieldInfo));
                                    break;
                                }
                            }
                            break;
                        }
                    if (found)
                        break;
                }
            }
        }
        public void PatchWorkGivers()
        {
            // Patch directly from list
            foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => !t.patchAllWorkGivers))
                foreach (Type workGiver in patch.WorkGiverList)
                {
                    WorkGiverPatch wgpatch = patch.FoundWorkGivers.Find(t => t.giver == workGiver);
                    if (wgpatch is null)
                    {
                        wgpatch = new WorkGiverPatch(workGiver);
                        patch.FoundWorkGivers.Add(wgpatch);
                    }
                }
            IEnumerable<Type> AllWorkGivers = GenTypes.AllSubclasses(typeof(WorkGiver));
            foreach (Type workGiver in AllWorkGivers)
            {
                foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => t.patchAllWorkGivers))
                {
                    patch.CheckWorkGiver(workGiver);
                    if (!workGiver.IsAbstract && !patch.skip)
                    {
                        // Check if there are fields or properties with jobdef
                        List<FieldInfo> fieldInfos = AccessTools.GetDeclaredFields(workGiver)?.Where(t => t.FieldType == typeof(JobDef)).ToList();
                        foreach (FieldInfo fieldInfo in fieldInfos)
                            if (patch.FoundJobDef.Exists(t => t.def == fieldInfo.GetValue(AccessTools.CreateInstance(workGiver)) as JobDef))
                            {
                                //patch.skip = true;
                                WorkGiverPatch wgpatch = patch.FoundWorkGivers.Find(t => t.giver == workGiver);
                                if (wgpatch is null)
                                {
                                    wgpatch = new WorkGiverPatch(workGiver);
                                    patch.FoundWorkGivers.Add(wgpatch);
                                }
                                wgpatch.fields.Add(fieldInfo);

                            }
                        List<PropertyInfo> propInfos = AccessTools.GetDeclaredProperties(workGiver)?.Where(t => t.PropertyType == typeof(JobDef)).ToList();
                        foreach (PropertyInfo propInfo in propInfos)
                            if (propInfo.GetGetMethod()?.IsAbstract == false)
                                if (patch.FoundJobDef.Exists(t => t.def == propInfo.GetValue(AccessTools.CreateInstance(workGiver)) as JobDef))
                                {
                                    //patch.skip = true;
                                    WorkGiverPatch wgpatch = patch.FoundWorkGivers.Find(t => t.giver == workGiver);
                                    if (wgpatch is null)
                                    {
                                        wgpatch = new WorkGiverPatch(workGiver);
                                        patch.FoundWorkGivers.Add(wgpatch);
                                    }
                                    wgpatch.properties.Add(propInfo);

                                }
                    }
                }
                // Check if methods call a jobdef
                FoundPatch = new List<StatPatchDef>();
                List<MethodInfo> wgMethods = AccessTools.GetDeclaredMethods(workGiver);
                foreach (MethodInfo wgMethod in wgMethods)
                {
                    if (wgMethod.IsAbstract)
                        continue;
                    foreach (StatPatchDef patch in SearchForJob(wgMethod))
                    {
                        WorkGiverPatch wgpatch = patch.FoundWorkGivers.Find(t => t.giver == workGiver);
                        if (wgpatch is null)
                        {
                            wgpatch = new WorkGiverPatch(workGiver);
                            patch.FoundWorkGivers.Add(wgpatch);
                        }
                        wgpatch.methods.Add(wgMethod);
                    }
                }
                Type[] nestedTypes = workGiver.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (Type nType in nestedTypes)
                {
                    wgMethods = AccessTools.GetDeclaredMethods(nType);
                    foreach (MethodInfo wgMethod in wgMethods)
                    {
                        if (wgMethod.IsAbstract)
                            continue;
                        foreach (StatPatchDef patch in SearchForJob(wgMethod))
                        {
                            WorkGiverPatch wgpatch = patch.FoundWorkGivers.Find(t => t.giver == workGiver);
                            if (wgpatch is null)
                            {
                                wgpatch = new WorkGiverPatch(workGiver);
                                patch.FoundWorkGivers.Add(wgpatch);
                            }
                            wgpatch.methods.Add(wgMethod);
                        }
                    }
                }
            }
            List<WorkGiverDef> workGiverDefList = DefDatabase<WorkGiverDef>.AllDefsListForReading;
            foreach (WorkGiverDef workGiverDef in workGiverDefList)
                foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
                    if (patch.FoundWorkGivers.Exists(t => t.giver == workGiverDef.giverClass))
                    {
                        if (workGiverDef.modExtensions is null)
                            workGiverDef.modExtensions = new List<DefModExtension>();
                        WorkGiverExtension extension = new WorkGiverExtension();
                        if (workGiverDef.GetModExtension<WorkGiverExtension>() is null)
                            workGiverDef.modExtensions.Add(extension);
                        else
                            extension = workGiverDef.GetModExtension<WorkGiverExtension>();
                        extension.requiredStats.Add(patch.newStat is null ? patch.oldStat : patch.newStat);
                    }
        }
        #endregion
        #region Utility
        private void AutoPatchInitialize()
        {

            foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
                if (!patch.CheckIfValidPatch())
                {
                    Logger.Error($"Invalid AutoPatch: {patch} ({patch.modContentPack?.Name}) : {patch.oldStat?.defName}");
                    AutoPatch.StatsToPatch.Remove(patch);
                }
            List<Type> allDefsOfs = GenTypes.AllTypesWithAttribute<DefOf>().ToList();
            JobDefOfTypes = allDefsOfs.Where(t => t.GetFields().Where(tt => tt.FieldType == typeof(JobDef)).Count() > 0).ToList();
            List<Type> StatDefOfTypes = allDefsOfs.Where(t => t.GetFields().Where(tt => tt.FieldType == typeof(StatDef)).Count() > 0).ToList();
            foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
            {
                if (patch.oldStatType is null)
                {
                    List<Type> foundTypes = StatDefOfTypes.Where(t => AccessTools.Field(t, patch.oldStat.defName) != null).ToList();
                    if (foundTypes.Count > 1)
                    {
                        StringBuilder message = new StringBuilder("Please report this back to us: ");
                        message.AppendLine("AutoPatchInitialize : Found more than one stat with the same name");
                        foreach (Type type in foundTypes)
                            message.AppendLine($"Type: {type} | oldStat: {patch.oldStat} | FieldInfo: {AccessTools.Field(type, patch.oldStat.defName)}");
                        Logger.Error(message.ToString());
                    }
                    patch.oldStatType = foundTypes[0];
                }
                patch.oldStatFieldInfo = AccessTools.Field(patch.oldStatType, patch.oldStat.defName);
                if (patch.newStatType is null && patch.newStat != null)
                {
                    List<Type> foundTypes = StatDefOfTypes.Where(t => AccessTools.Field(t, patch.newStat.defName) != null).ToList();
                    if (foundTypes.Count > 1)
                    {
                        StringBuilder message = new StringBuilder("Please report this back to us: ");
                        message.AppendLine("AutoPatchInitialize : Found more than one stat with the same name");
                        foreach (Type type in foundTypes)
                            message.AppendLine($"Type: {type} | oldStat: {patch.newStat} | FieldInfo: {AccessTools.Field(type, patch.newStat.defName)}");
                        Logger.Error(message.ToString());
                    }
                    patch.newStatType = foundTypes[0];
                }
                if (patch.newStat != null)
                    patch.newStatFieldInfo = AccessTools.Field(patch.newStatType, patch.newStat.defName);
            }
        }
        private void PrintPatchDebug()
        {
            StringBuilder debugString = new StringBuilder($"Stats auto patched:\n");
            foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
            {
                debugString.AppendLine($"\nPatch : {patch.oldStat} => {patch.newStat}");
                debugString.AppendLine($"JobDrivers:");
                foreach (JobDriverPatch jdPatch in patch.FoundJobDrivers)
                {
                    debugString.Append($" [ {jdPatch.FoundStage1} | {jdPatch.FoundStage2} ] {jdPatch.driver} :");
                    foreach (MethodInfo method in jdPatch.methods)
                        if (jdPatch.auxmethods.Contains(method)) debugString.Append($": ! {method} ! ");
                        else debugString.Append($": {method} ");
                    debugString.AppendLine("");
                }
                debugString.AppendLine($"\nJobDefs:");
                foreach (JobDefPatch jdefPatch in patch.FoundJobDef)
                {
                    debugString.AppendLine($" {jdefPatch.def} : {jdefPatch.fieldInfo}");
                }
                debugString.AppendLine($"\nWorkGivers:");
                foreach (WorkGiverPatch wgPatch in patch.FoundWorkGivers)
                {
                    debugString.Append($"{wgPatch.giver} :: Methods :");
                    foreach (MethodInfo method in wgPatch.methods)
                        debugString.Append($": {method} ");
                    debugString.Append($":: Properties :");
                    foreach (PropertyInfo prop in wgPatch.properties)
                        debugString.Append($": {prop.Name} ");
                    debugString.Append($":: Fields :");
                    foreach (FieldInfo field in wgPatch.fields)
                        debugString.Append($": {field.Name} ");
                    debugString.AppendLine("");
                }
            }
            Logger.Message(debugString.ToString(), false);

            StringBuilder OtherPatches = new StringBuilder("Other Patches:\n");
            OtherPatches.AppendLine("Race exemption list: (I know how it sounds, but it's not racist ><)");
            foreach (RaceExemption exemption in MiscDef.IgnoreRaceList)
                OtherPatches.AppendLine($"{exemption.defName} : {exemption.race} : {exemption.all}");
            Logger.Message(OtherPatches.ToString());
        }
        #endregion
    }
}