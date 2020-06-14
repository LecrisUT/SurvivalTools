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
using UnityEngine;
using SurvivalTools.AutoPatcher;

namespace SurvivalTools.HarmonyPatches
{
    internal class Controller : ModBase
    {
        private static readonly Type patchType = typeof(Controller);
        public static Harmony harmony = new Harmony("Lecris.survivaltools.AutoPatch");
        public static Harmony tempHarmony = new Harmony("Lecris.survivaltools.TempPatch");
        //static string modIdentifier;
        public override void DefsLoaded()
        {
            // Automatic patches
            //modIdentifier = ModContentPack.PackageIdPlayerFacing;

            SurvivalToolType.allDefs.Do(t => t.Initialize());
            IEnumerable<ThingDef> things = DefDatabase<ThingDef>.AllDefsListForReading.Where(t => t.HasModExtension<SurvivalToolProperties>());
            // things.Do(t=> t.GetModExtension<SurvivalToolProperties>().Initialize());
            // harmony.PatchAll(Assembly.GetExecutingAssembly());
            AutoPatchInitialize();
            PatchJobDrivers();
            FindJobDefs();
            PatchWorkGivers();
            things.Do(t => t.GetModExtension<SurvivalToolProperties>().Initialize());
            PrintPatchDebug();
            Stat_Injector.Inject();
            foreach(WorkGiverDef workGiverDef in DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(t => t.HasModExtension<WorkGiverExtension>()))
            {
                WorkGiverExtension extension = workGiverDef.GetModExtension<WorkGiverExtension>();
                extension.Initialize();
                extension.requiredToolTypes.Do(t => t.relevantWorkGivers.AddDistinct(workGiverDef));
            }
            Patch_StatWorker_GetExplanationFinalizePart.StatsWithTools = SurvivalToolType.allDefs.SelectMany(t => t.stats).ToList();
            Patch_StatWorker_GetExplanationFinalizePart.StatsWithTools.RemoveDuplicates();
            // Search and add LTS's Degrade mod
            Type LTS_Degradation_Utility = GenTypes.GetTypeInAnyAssembly("Degradation.Utility.Utility", null);
            if (LTS_Degradation_Utility != null)
                JobDriver_AddToolDegrade.modDegrade = AccessTools.Method(LTS_Degradation_Utility, "DegradeTool");
            // Manual patches
            // Plants that obstruct construction zones
            // erdelf never fails to impress :)
            Type WorkTabPriorityTracker = AccessTools.TypeByName("WorkTab.PriorityTracker");
            HarmonyMethod postfixWorkTabSetPriority = new HarmonyMethod(patchType, nameof(Postfix_WorkTab_SetPriority));
            if (WorkTabPriorityTracker != null)
                harmony.Patch(AccessTools.Method(WorkTabPriorityTracker, "SetPriority", new Type[] { typeof(WorkGiverDef), typeof(int), typeof(int), typeof(bool) }), postfix: postfixWorkTabSetPriority);
            HarmonyMethod postfixHandleBlockingThingJob = new HarmonyMethod(patchType, nameof(Postfix_HandleBlockingThingJob));
            harmony.Patch(AccessTools.Method(typeof(GenConstruct), nameof(GenConstruct.HandleBlockingThingJob)), postfix: postfixHandleBlockingThingJob);
            harmony.Patch(AccessTools.Method(typeof(RoofUtility), nameof(RoofUtility.HandleBlockingThingJob)), postfix: postfixHandleBlockingThingJob);
            harmony.Patch(AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders"), postfix: new HarmonyMethod(patchType, nameof(Postfix_FloatMenuMakerMap_AddHumanlikeOrders)));
            Type SS_WeaponAssingment = AccessTools.TypeByName("SimpleSidearms.utilities.WeaponAssingment");
            if (SS_WeaponAssingment != null)
                JobDriver_DropSurvivalTool.SS_dropSidearm = AccessTools.Method(SS_WeaponAssingment, "dropSidearm");
        }
        #region AutoPatch_Fields
        public static List<StatPatchDef> FoundPatches = new List<StatPatchDef>();
        public static List<StatPatchDef> auxPatch = new List<StatPatchDef>();
        public static List<Type> JobDefOfTypes;
        private static List<(int pos, List<CodeInstruction> instructions)> transpile_ChangeToil_List;
        private static readonly Dictionary<int, OpCode> intToOpCode = new Dictionary<int, OpCode>()
        {
            { 0, OpCodes.Ldloc_0 },
            { 1, OpCodes.Ldloc_1 },
            { 2, OpCodes.Ldloc_2 },
            { 3, OpCodes.Ldloc_3 }
        };
        #endregion
        #region AutoPatch_Transpiler_Methods
        private static bool SearchForStat(MethodInfo method, out StatPatchDef foundPatch, out bool foundPawn, MethodInfo methodToSearch = null, StatPatchDef fallbackPatch = null)
        {
            bool result = false;
            foundPawn = false;
            foundPatch = fallbackPatch;
            ParameterInfo pawnParameter = method.GetParameters().FirstOrDefault(t => t.ParameterType.IsAssignableFrom(typeof(Pawn)));
            int paraeterPos = pawnParameter?.Position ?? -1;
            LocalVariableInfo pawnLocal = method.GetMethodBody().LocalVariables.FirstOrFallback(t => t.LocalType.IsAssignableFrom(typeof(Pawn)));
            int localPos = pawnLocal?.LocalIndex ?? -1;
            List<CodeInstruction> instructions = new List<CodeInstruction>();
            ILGenerator generator;
            try { instructions = PatchProcessor.GetCurrentInstructions(method, out generator); }
            catch { instructions = PatchProcessor.GetOriginalInstructions(method, out generator); }
            for (int i = 0; i < instructions.Count; i++)
            {
                CodeInstruction instruction = instructions[i];
                if (pawnParameter != null && instruction.IsLdarg(paraeterPos))
                {
                    foundPawn = true;
                    continue;
                }
                if (pawnLocal != null && instruction.IsLdloc())
                {
                    if (intToOpCode.TryGetValue(localPos, out OpCode opCode) && instruction.opcode == opCode)
                        goto Found;
                    else if (instruction.operand is int pos && pos == localPos)
                        goto Found;
                    else
                        goto Skip;
                    Found:
                    foundPawn = true;
                Skip:
                    continue;
                }
                if (instruction.opcode == OpCodes.Ldfld)
                {
                    FieldInfo field = instruction.operand as FieldInfo;
                    if (field == AccessTools.Field(typeof(JobDriver), nameof(JobDriver.pawn)) || field == AccessTools.Field(typeof(Toil), nameof(Toil.actor)))
                        foundPawn = true;
                    continue;
                }
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo calledMethod)
                {
                    if (calledMethod.ReturnType.IsAssignableFrom(typeof(Pawn)))
                    {
                        foundPawn = true;
                        continue;
                    }
                    if (calledMethod.ReturnType.IsAssignableFrom(typeof(StatDef)))
                    {
                        if (IsBaseMethod(methodToSearch, calledMethod))
                            goto Found;
                        if (AutoPatch.StatsToPatch.FirstOrFallback(t => !t.skip && t.FoundStatMethods.Exists(tt => IsBaseMethod(tt, calledMethod))) is StatPatchDef patch)
                            foundPatch = patch;
                        else
                            continue;
                        Found:
                        result = true;
                        if (foundPawn)
                            return true;
                        continue;
                    }
                }
                if (instruction.opcode == OpCodes.Ldsfld)
                {
                    FieldInfo field = instruction.operand as FieldInfo;
                    if (AutoPatch.StatsToPatch.FirstOrFallback(t => !t.skip && t.oldStatFieldInfo == field) is StatPatchDef patch)
                    {
                        result = true;
                        foundPatch = patch;
                        if (foundPawn)
                            return true;
                    }
                }
            }
            return result;
        }
        private static Dictionary<StatPatchDef, List<JobDef>> SearchForJob(MethodInfo method)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>();
            try { instructions = PatchProcessor.GetCurrentInstructions(method); }
            catch { instructions = PatchProcessor.GetOriginalInstructions(method); }
            Dictionary<StatPatchDef,List<JobDef>> fPatch = new Dictionary<StatPatchDef, List<JobDef>>();
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld)
                {
                    FieldInfo field = instruction.operand as FieldInfo;
                    foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => !t.skip))
                        if (patch.FoundJobDef.FirstOrFallback(t => t.fieldInfo == field) is JobDefPatch jdPatch)
                        {
                            if (fPatch.ContainsKey(patch))
                                fPatch[patch].AddDistinct(jdPatch.def);
                            else
                                fPatch.Add(patch, new List<JobDef>() { jdPatch.def });
                        }
                }
            }
            return fPatch;
        }
        // Get the nested class called in MakeNewToils
        private static Type Sealed_MakeNewToils(MethodInfo method, Type jobDriver)
        {
            List<Type> nestedTypes = jobDriver.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance).ToList();
            List<CodeInstruction> instructions = new List<CodeInstruction>();
            try { instructions = PatchProcessor.GetCurrentInstructions(method); }
            catch { instructions = PatchProcessor.GetOriginalInstructions(method); }
            for (int i = 0; i < instructions.Count; i++)
            {
                CodeInstruction instruction = instructions[i];
                if (instruction.opcode == OpCodes.Newobj && instruction.operand is ConstructorInfo constructor)
                {
                    Type type = constructor.DeclaringType;
                    if (type.IsNested && nestedTypes.Contains(type))
                        return type;
                }
            }
            return null;
        }
        private static bool SearchMethodWithStat(Type jobDriver, List<StatDef> stats, ref List<MethodInfo> methodsWithStat, ref List<(MethodInfo actionMethod, StatDef statFound, MethodInfo methodFound)> actionsWithStat)
        {
            bool found = !actionsWithStat.NullOrEmpty();
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(jobDriver))
            {
                if (method.IsAbstract || actionsWithStat.Exists(t => t.actionMethod == method))
                    continue;
                if (SearchMethodWithStat(method, stats, ref methodsWithStat, ref actionsWithStat, (jobDriver, null)))
                    found = true;
            }
            foreach (Type nType in jobDriver.GetNestedTypes(AccessTools.all))
                foreach (MethodInfo method in AccessTools.GetDeclaredMethods(nType))
                {
                    if (method.IsAbstract || actionsWithStat.Exists(t => t.actionMethod == method))
                        continue;
                    if (SearchMethodWithStat(method, stats, ref methodsWithStat, ref actionsWithStat, (jobDriver, nType)))
                        found = true;
                }
            return found;
        }
        private static bool SearchMethodWithStat(MethodInfo method, List<StatDef> stats, ref List<MethodInfo> methodsWithStat, ref List<(MethodInfo actionMethod, StatDef statFound, MethodInfo methodFound)> actionsWithStat, (Type jobDriver, Type nType) info)
        {
            List<MethodInfo> allMethodsWithStat = new List<MethodInfo>(methodsWithStat);
            allMethodsWithStat.AddRange(actionsWithStat.Select(t => t.actionMethod));
            if (SearchStatAndCalledMethod(method, stats, allMethodsWithStat, out StatDef statFound, out MethodInfo methodFound))
            {
                if (method.ReturnType == typeof(void))
                {
                    actionsWithStat.Add((method, statFound, methodFound));
                }
                else if (method.ReturnType.IsAssignableFrom(typeof(StatDef)))
                {
                    methodsWithStat.AddDistinct(method);
                }
                else
                    Log.Warning($"[AutoPatcher] SearchMethodWithStat: Could not backtrack stat method\n{info.jobDriver} : {info.nType} : {method}");
                return true;
            }
            return false;
        }
        private static bool SearchStatAndCalledMethod(MethodInfo method, List<StatDef> stats, List<MethodInfo> methodWithStat, out StatDef statFound, out MethodInfo methodFound)
        {
            methodFound = null;
            statFound = null;
            List<CodeInstruction> instructions = new List<CodeInstruction>();
            try { instructions = PatchProcessor.GetCurrentInstructions(method); }
            catch { instructions = PatchProcessor.GetOriginalInstructions(method); }
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld)
                {
                    FieldInfo field = instruction.operand as FieldInfo;
                    if (field.GetValue(null) is StatDef stat && stats.Contains(stat))
                    {
                        statFound = stat;
                        return true;
                    }
                }
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    instruction.operand is MethodInfo calledMethod && methodWithStat.Exists(t => IsBaseMethod(t, calledMethod)))
                {
                    methodFound = calledMethod;
                    return true;
                }
            }
            return false;
        }
        private static bool IsBaseMethod(MethodInfo finalMethod, MethodInfo baseMethod)
        {
            MethodInfo currMethod = finalMethod;
            MethodInfo prevMethod = null;
            while (currMethod != prevMethod)
            {
                if (currMethod == baseMethod)
                    return true;
                prevMethod = currMethod;
                currMethod = currMethod.GetBaseDefinition();
            }
            return false;
        }
        private static bool SearchForToil(Type type, List<(MethodInfo actionMethod, StatDef statFound, MethodInfo methodFound)> actionsWithStat, out List<(int pos, MethodInfo actionMethod, StatDef statFound, MethodInfo methodFound)> result)
        {
            result = new List<(int, MethodInfo, StatDef, MethodInfo)>();
            bool found = false;
            (MethodInfo actionFound, StatDef statFound, MethodInfo methodFound) foundItem = default;
            ConstructorInfo action_ctor = AccessTools.Constructor(typeof(Action), new[] { typeof(object), typeof(IntPtr) });
            FieldInfo current = AccessTools.GetDeclaredFields(type).First(t => t.FieldType == typeof(Toil));
            MethodInfo method = AccessTools.Method(type, "MoveNext");
            List<CodeInstruction> instructions = new List<CodeInstruction>();
            try { instructions = PatchProcessor.GetCurrentInstructions(method); }
            catch { instructions = PatchProcessor.GetOriginalInstructions(method); }
            for (int i = 0; i < instructions.Count; i++)
            {
                CodeInstruction instruction = instructions[i];
                if (foundItem.actionFound != null && instruction.Is(OpCodes.Stfld, current))
                {
                    found = true;
                    result.Add((i, foundItem.actionFound, foundItem.statFound, foundItem.methodFound));
                    foundItem = default;
                    continue;
                }
                if (foundItem.actionFound == null && instruction.Is(OpCodes.Newobj, action_ctor))
                {
                    CodeInstruction prevInstruction = instructions[i - 1];
                    if (prevInstruction.opcode == OpCodes.Ldftn)
                    {
                        MethodInfo calledMethod = prevInstruction.operand as MethodInfo;
                        foundItem = actionsWithStat.FirstOrFallback(t => t.actionMethod == calledMethod);
                    }
                }
            }
            return found;
        }
        private static IEnumerable<CodeInstruction> Transpile_ChangeToil(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            transpile_ChangeToil_List.SortByDescending(t => t.pos);
            foreach ((int pos, List<CodeInstruction> instructions) pair in transpile_ChangeToil_List)
                instructionList.InsertRange(pair.pos + 1, pair.instructions);
            return instructionList.AsEnumerable();
        }
        private static IEnumerable<CodeInstruction> Transpile_Replace_Stat(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if (instruction.opcode == OpCodes.Ldsfld && instruction.operand is FieldInfo field)
                    foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => !t.skip && t.oldStatFieldInfo == field))
                    {
                        FoundPatches.AddDistinct(patch);
                        if (patch.newStat != null)
                            instruction.operand = patch.newStatFieldInfo;
                        if (patch.StatReplacer != null && patch.canPatch)
                            patch.StatReplacer_Transpile(ref instructionList, i);
                        break;
                    }
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    instruction.operand is MethodInfo method)
                    foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => !t.skip && t.FoundStatMethods.Exists(tt => IsBaseMethod(tt, method))))
                    {
                        FoundPatches.AddDistinct(patch);
                        if (patch.StatReplacer != null && patch.canPatch)
                            patch.StatReplacer_Transpile(ref instructionList, i);
                        break;
                    }
            }
            return instructionList.AsEnumerable();
        }
        #endregion
        #region ManualPatches

        public static PropertyInfo workTab_Prop = AccessTools.Property(AccessTools.TypeByName("WorkTab.PriorityTracker"), "Pawn");
        public static void Postfix_WorkTab_SetPriority(object __instance)
        {
            Pawn pawn = (Pawn)workTab_Prop.GetValue(__instance);
            pawn.GetToolTracker().dirtyCache = true;
        }
        public static void Postfix_HandleBlockingThingJob(ref Job __result, Pawn worker)
        {
            if (__result?.def == JobDefOf.CutPlant && __result.targetA.Thing.def.plant.IsTree &&
                !ST_WorkGiverDefOf.FellTrees.GetModExtension<WorkGiverExtension>().MeetsRequirementJobs(worker))
                __result = null;
        }
        public static void Postfix_FloatMenuMakerMap_AddHumanlikeOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                return;
            IntVec3 position = IntVec3.FromVector3(clickPos);
            List<Thing> tools = position.GetThingList(pawn.Map).Where(t => t is SurvivalTool).ToList();
            foreach (SurvivalTool tool in tools)
            {
                if (!pawn.CanReach(tool, PathEndMode.ClosestTouch, Danger.Deadly))
                    return;
                if (!MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, tool, 1))
                {
                    opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PickUp".Translate(tool.Label, tool) + "ST_AsTool".Translate() + " (" + "ApparelForcedLower".Translate() + ")", delegate
                    {
                        tool.SetForbidden(value: false, warnOnFail: false);
                        Job job9 = JobMaker.MakeJob(ST_JobDefOf.PickSurvivalTool, tool);
                        job9.count = 1;
                        job9.checkEncumbrance = true;
                        pawn.jobs.TryTakeOrderedJob(job9);
                    }, MenuOptionPriority.High), pawn, tool));
                }
            }
        }
        #endregion
        #region AutoPatch_Loops
        private static readonly HarmonyMethod transpileReplaceStat = new HarmonyMethod(patchType, nameof(Transpile_Replace_Stat));
        public static bool PatchStat(Type jobDriver, MethodInfo method, Type nType = null, MethodInfo methodToSearch = null, bool recursive = true, StatPatchDef fallbackPatch = null)
        {
            if (method.IsAbstract || (methodToSearch != null && string.Compare(method.Name, methodToSearch.Name) == 0))
                return false;
            if (methodToSearch != null && fallbackPatch != null && fallbackPatch.StatReplacer != null)
            {
                fallbackPatch.StatReplacer_Initialize(jobDriver, nType);
                if (!fallbackPatch.canPatch)
                    return false;
            }
            if (SearchForStat(method, out StatPatchDef foundPatch, out bool foundPawn, methodToSearch, fallbackPatch))
            {
                if (foundPawn)
                {
                    FoundPatches = new List<StatPatchDef>();
                    harmony.Patch(method, transpiler: transpileReplaceStat);
                    foreach (StatPatchDef patch in FoundPatches)
                    {
                        JobDriverPatch jdpatch = patch.FoundJobDrivers.Find(t => t.driver == jobDriver);
                        if (jdpatch is null)
                        {
                            jdpatch = new JobDriverPatch(jobDriver);
                            patch.FoundJobDrivers.Add(jdpatch);
                        }
                        jdpatch.methods.Add(method);
                        if (method.ReturnType == typeof(void))
                            patch.FoundStatActions.Add(method);
                        else
                            patch.FoundStatMethods.Add(method);
                    }
                    return true;
                }
                else if (recursive && method.ReturnType.IsAssignableFrom(typeof(StatDef)))
                {
                    AutoPatch.StatsToPatch.Do(t => t.skip = true);
                    foundPatch.skip = false;
                    foundPatch.FoundStatMethods.Add(method);
                    Type baseType = jobDriver.BaseType;
                    MethodInfo tmethodInfo;
                    do
                    {
                        foreach (MethodInfo currMethod in AccessTools.GetDeclaredMethods(baseType))
                            if (PatchStat(baseType, currMethod, null, method, recursive, foundPatch))
                            {
                                tmethodInfo = currMethod;
                                goto Found;
                            }
                        foreach (Type currNType in baseType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance))
                            foreach (MethodInfo currMethod in AccessTools.GetDeclaredMethods(currNType))
                                if (PatchStat(baseType, currMethod, currNType, method, recursive, foundPatch))
                                {
                                    tmethodInfo = currMethod;
                                    goto Found;
                                }
                        baseType = baseType.BaseType;
                        continue;
                    Found:
                        JobDriverPatch jdpatch2 = foundPatch.FoundJobDrivers.Find(t => t.driver == jobDriver);
                        if (jdpatch2 is null)
                        {
                            jdpatch2 = new JobDriverPatch(jobDriver);
                            foundPatch.FoundJobDrivers.Add(jdpatch2);
                        }
                        jdpatch2.methods.Add(method);
                        jdpatch2.methods.Add(tmethodInfo);
                        JobDriverPatch jdpatch1 = foundPatch.FoundJobDrivers.First(t => t.driver == baseType);
                        jdpatch1.methods.Remove(tmethodInfo);
                        if (jdpatch1.methods.NullOrEmpty())
                            foundPatch.FoundJobDrivers.Remove(jdpatch1);
                        CheckJobDriver(jobDriver);
                        return true;
                    }
                    while (baseType.IsSubclassOf(typeof(JobDriver)));
                    foundPatch.FoundStatMethods.Remove(method);
                    CheckJobDriver(jobDriver);
                }
                else
                    Log.Warning($"[[LC]SurvivalTools.AutoPatcher] Found a method with no pawn or StatDef return:\n{jobDriver} : {nType} : {method}");
            }
            return false;
        }
        private static readonly HarmonyMethod transpileChangeToil = new HarmonyMethod(patchType, nameof(Transpile_ChangeToil));
        public static bool PatchToil(Type jobDriver, Type type, StatPatchDef patch, bool canIgnore = false, Type baseType = null)
        {
            if (baseType == null)
                baseType = jobDriver;
            List<(MethodInfo actionMethod, StatDef statFound, MethodInfo methodFound)> actionsWithStat =
                patch.FoundStatActions.Select(t => (t, patch.oldStat, (MethodInfo)null)).ToList();
            if (!SearchMethodWithStat(baseType, new List<StatDef>() { patch.oldStat }, ref patch.FoundStatMethods,
                ref actionsWithStat))
            {
                if (!canIgnore)
                    Log.Warning($"[[LC]SurvivalTools.AutoPatcher] MakeNewToils did not find actions with stat {patch.oldStat}:\n{jobDriver} : {baseType} : {patch}");
                return false;
            }
            if (!SearchForToil(type, actionsWithStat, out List<(int, MethodInfo, StatDef, MethodInfo)> actionsFound))
            {
                if (!canIgnore)
                    Log.Warning($"[[LC]SurvivalTools.AutoPatcher] MakeNewToils did not find Toils calling action with stat {patch.oldStat}:\n{jobDriver} : {baseType} : {type} : {patch}");
                return false;
            }
            MethodInfo MoveNext_Info = AccessTools.Method(type, "MoveNext");
            FieldInfo CurrentToil_Field = AccessTools.GetDeclaredFields(type).First(t => t.FieldType == typeof(Toil));
            List<CodeInstruction> jobDriver_instructions = new List<CodeInstruction>();
            LocalVariableInfo localVar = MoveNext_Info.GetMethodBody().LocalVariables.FirstOrFallback(t => t.LocalType.IsAssignableFrom(baseType));
            if (localVar != null)
            {
                int n = localVar.LocalIndex;
                if (intToOpCode.TryGetValue(n, out OpCode LdLoc_OpCode))
                    jobDriver_instructions.Add(new CodeInstruction(LdLoc_OpCode, null));
                else
                    jobDriver_instructions.Add(new CodeInstruction(OpCodes.Ldloc_S, n));
            }
            else
            {
                FieldInfo localField = AccessTools.GetDeclaredFields(type).FirstOrFallback(t => t.FieldType.IsAssignableFrom(baseType));
                if (localField == null)
                {
                    Log.Warning($"[[LC]SurvivalTools.AutoPatcher] MakeNewToils did not find local variable or field {jobDriver}\n{jobDriver} : {baseType}: {patch}");
                    return false;
                }
                jobDriver_instructions = new List<CodeInstruction>()
                            {
                                new CodeInstruction(OpCodes.Ldarg_0, null),
                                new CodeInstruction(OpCodes.Ldfld, localField)
                            };
            }
            transpile_ChangeToil_List = new List<(int pos, List<CodeInstruction> instructions)>();
            foreach ((int pos, MethodInfo actionMethod, StatDef statFound, MethodInfo methodFound) item in actionsFound)
            {
                List<CodeInstruction> transpileInstructions = new List<CodeInstruction>()
                            {
                                new CodeInstruction(OpCodes.Ldarg_0,null),
                                new CodeInstruction(OpCodes.Ldflda,CurrentToil_Field)
                            };
                transpileInstructions.AddRange(jobDriver_instructions);
                if (item.statFound != null || item.methodFound?.ReturnType == typeof(void))
                    transpileInstructions.Add(new CodeInstruction(OpCodes.Ldsfld, patch.oldStatFieldInfo));
                else if (item.methodFound != null && item.methodFound.ReturnType.IsAssignableFrom(typeof(StatDef)))
                {
                    MethodInfo methodFound = AccessTools.Method(baseType, item.methodFound.Name);
                    if (methodFound != null)
                    {
                        transpileInstructions.AddRange(jobDriver_instructions);
                        if (methodFound.IsVirtual)
                            transpileInstructions.Add(new CodeInstruction(OpCodes.Callvirt, methodFound));
                        else
                            transpileInstructions.Add(new CodeInstruction(OpCodes.Call, methodFound));
                    }
                    else
                    {
                        Log.Warning($"[[LC]SurvivalTools.AutoPatcher] MakeNewToils have not coded this situation: {item.methodFound}\n{jobDriver} : {baseType} : {item.methodFound}");
                        continue;
                    }
                }
                transpileInstructions.Add(new CodeInstruction(OpCodes.Call, patch.toilChanger_ChangeToil));
                transpile_ChangeToil_List.Add((item.pos, transpileInstructions));
            }
            harmony.Patch(MoveNext_Info, transpiler: transpileChangeToil);
            return true;
        }
        private static bool CheckJobDriver(Type jobDriver)
        {
            bool skip = true;
            foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
            {
                patch.CheckJobDriver(jobDriver);
                if (!patch.skip)
                {
                    skip = false;
                    if (patch.StatReplacer != null)
                    {
                        patch.StatReplacer_Initialize(jobDriver);
                        patch.skip |= !patch.canPatch;
                    }
                }
            }
            return skip;
        }
        public void PatchJobDrivers()
        {
            // StringBuilder test = new StringBuilder("Test 1\n");
            foreach (Type jobDriver in GenTypes.AllSubclasses(typeof(JobDriver)))
            {
                // Check if any patch is available
                if (CheckJobDriver(jobDriver))
                    continue;
                foreach (MethodInfo jdMethod in AccessTools.GetDeclaredMethods(jobDriver))
                {
                    if (jdMethod.IsAbstract)
                        continue;
                    PatchStat(jobDriver, jdMethod);
                }
                foreach (Type nType in jobDriver.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    AutoPatch.StatsToPatch.Where(t => t.StatReplacer != null).Do(t => { t.StatReplacer_Initialize(jobDriver, nType); t.skip |= !t.canPatch; });
                    foreach (MethodInfo jdMethod in AccessTools.GetDeclaredMethods(nType))
                    {
                        if (jdMethod.IsAbstract)
                            continue;
                        PatchStat(jobDriver, jdMethod, nType);
                    }
                }
                // Patch toil
                // Ignore if nothing was found
                CheckJobDriver(jobDriver);
                List<StatPatchDef> ChangeToilList = AutoPatch.StatsToPatch.Where(t => !t.skip && t.ToilChanger != null && t.FoundJobDrivers.Exists(tt => tt.driver.IsAssignableFrom(jobDriver))).ToList();
                if (ChangeToilList.NullOrEmpty())
                    continue;
                ChangeToilList.Do(t => t.ToilChanger_Initialize(jobDriver));
                MethodInfo MakeNewToils_Info = AccessTools.DeclaredMethod(jobDriver, "MakeNewToils");
                if (MakeNewToils_Info != null)
                {
                    Type sealedType = Sealed_MakeNewToils(MakeNewToils_Info, jobDriver);
                    if (sealedType == null)
                    {
                        Log.Warning($"[[LC]SurvivalTools.AutoPatcher] MakeNewToils is not a sealed type: {jobDriver}");
                        sealedType = jobDriver;
                    }
                    bool canIgnore = ChangeToilList.Any(t => t.toilChanger_PatchedJd.Any(tt => tt.IsAssignableFrom(jobDriver)));
                    foreach (StatPatchDef patch in ChangeToilList)
                        if (PatchToil(jobDriver, sealedType, patch, canIgnore))
                        {
                            patch.toilChanger_PatchedJd.Add(jobDriver);
                        }
                }
                // Search in superTypes
                else
                {
                    Type baseType = jobDriver;
                    while (true)
                    {
                        baseType = baseType.BaseType;
                        if (ChangeToilList.Any(t => t.toilChanger_PatchedJd.Contains(baseType)))
                            goto Finish;
                        if (baseType == typeof(JobDriver) || baseType is null)
                        {
                            Logger.Warning($"Couldn't backtrack jobDriver patch: {jobDriver} => {baseType}");
                            break;
                        }
                        MakeNewToils_Info = AccessTools.DeclaredMethod(baseType, "MakeNewToils");
                        if (MakeNewToils_Info == null)
                            continue;
                        Type sealedType = Sealed_MakeNewToils(MakeNewToils_Info, baseType);
                        if (sealedType == null)
                        {
                            Log.Warning($"[[LC]SurvivalTools.AutoPatcher] MakeNewToils is not a sealed type:\n{jobDriver} : {baseType}");
                            sealedType = baseType;
                        }
                        bool canIgnore = ChangeToilList.Any(t => t.toilChanger_PatchedJd.Any(tt => tt.IsAssignableFrom(baseType)));
                        foreach (StatPatchDef patch in ChangeToilList)
                            if (PatchToil(jobDriver, sealedType, patch, canIgnore, baseType))
                            {
                                patch.toilChanger_PatchedJd.Add(baseType);
                                goto Finish;
                            }
                    }
                Finish:;
                }
            }
            // Extra Patches
            AutoPatch.StatsToPatch.Do(t => t.skip = true);
            foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => t.OtherTypes.Count > 0))
            {
                patch.skip = false;
                foreach (Type type in patch.OtherTypes)
                {
                    if (patch.StatReplacer != null)
                        patch.StatReplacer_Initialize(type);
                    if (patch.skip)
                        continue;
                    foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
                    {
                        if (method.IsAbstract)
                            continue;
                        PatchStat(type, method);
                    }
                    foreach (Type nType in type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(nType))
                        {
                            if (method.IsAbstract)
                                continue;
                            PatchStat(type, method, nType);
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
                if (jobDef?.driverClass == null)
                {
                    Logger.Warning($"Found null jobDef: {jobDef} : {jobDef?.driverClass}");
                    continue;
                }
                foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
                {
                    foreach (JobDriverPatch jdPatch in patch.FoundJobDrivers)
                        if (jdPatch.driver.IsAssignableFrom(jobDef.driverClass) && !patch.JobDefExemption.Contains(jobDef))
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
                            foreach (SurvivalToolType toolType in patch.toolTypes)
                            {
                                if (!toolType.jobSpecific.NullOrEmpty() || toolType.jobException.Contains(jobDef))
                                    continue;
                                toolType.RegisterJobDef(jobDef);
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
                FoundPatches = new List<StatPatchDef>();
                List<MethodInfo> wgMethods = AccessTools.GetDeclaredMethods(workGiver);
                foreach (MethodInfo wgMethod in wgMethods)
                {
                    if (wgMethod.IsAbstract)
                        continue;
                    Dictionary<StatPatchDef, List<JobDef>> dict = SearchForJob(wgMethod);
                    foreach (StatPatchDef patch in dict.Keys)
                    {
                        WorkGiverPatch wgpatch = patch.FoundWorkGivers.FirstOrFallback(t => t.giver == workGiver);
                        if (wgpatch is null)
                        {
                            wgpatch = new WorkGiverPatch(workGiver);
                            patch.FoundWorkGivers.Add(wgpatch);
                        }
                        wgpatch.methods.Add(wgMethod);
                        dict[patch].Do(t => wgpatch.jobDefs.AddDistinct(t));
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
                        Dictionary<StatPatchDef, List<JobDef>> dict = SearchForJob(wgMethod);
                        foreach (StatPatchDef patch in dict.Keys)
                        {
                            WorkGiverPatch wgpatch = patch.FoundWorkGivers.FirstOrFallback(t => t.giver == workGiver);
                            if (wgpatch is null)
                            {
                                wgpatch = new WorkGiverPatch(workGiver);
                                patch.FoundWorkGivers.Add(wgpatch);
                            }
                            wgpatch.methods.Add(wgMethod);
                            dict[patch].Do(t => wgpatch.jobDefs.AddDistinct(t));
                        }
                    }
                }
            }
            List<WorkGiverDef> workGiverDefList = DefDatabase<WorkGiverDef>.AllDefsListForReading;
            foreach (WorkGiverDef workGiverDef in workGiverDefList)
                foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
                    if (patch.FoundWorkGivers.FirstOrFallback(t => t.giver.IsAssignableFrom(workGiverDef.giverClass)) is WorkGiverPatch wgPatach &&
                        !patch.WorkGiverExemption.Exists(t => t.IsAssignableFrom(workGiverDef.giverClass)))
                    {
                        if (workGiverDef.modExtensions is null)
                            workGiverDef.modExtensions = new List<DefModExtension>();
                        WorkGiverExtension extension = new WorkGiverExtension();
                        if (workGiverDef.GetModExtension<WorkGiverExtension>() is null)
                            workGiverDef.modExtensions.Add(extension);
                        else
                            extension = workGiverDef.GetModExtension<WorkGiverExtension>();
                        wgPatach.jobDefs.Do(t => extension.relevantJobs.AddDistinct(t));
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
            foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
                patch.Initialize();
        }
        private void PrintPatchDebug()
        {
            StringBuilder debugString = new StringBuilder($"Stats auto patched:\n");
            foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
            {
                debugString.AppendLine($"\nPatch : {patch.oldStat} => {patch.newStat} | {patch.StatReplacer}");
                debugString.AppendLine($"JobDrivers:");
                foreach (JobDriverPatch jdPatch in patch.FoundJobDrivers)
                {
                    debugString.Append($" [ {jdPatch.FoundStage1} | {jdPatch.FoundStage2} ] {jdPatch.driver} :");
                    foreach (MethodInfo method in jdPatch.methods)
                        debugString.Append($": {method} ");
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
