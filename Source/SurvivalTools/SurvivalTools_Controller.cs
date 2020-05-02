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
using RimWorld.SketchGen;

namespace SurvivalTools.HarmonyPatches
{
    internal class Controller : ModBase
    {
        private static readonly Type patchType = typeof(Controller);
        public static Harmony harmony = new Harmony("Lecris.survivaltools");
        public static Harmony tempHarmony = new Harmony("Lecris.survivaltools.TempPatch");
        //static string modIdentifier;
        public override void DefsLoaded()
        {
            // Automatic patches
            //modIdentifier = ModContentPack.PackageIdPlayerFacing;
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
            Type WorkTabPriorityTracker = AccessTools.TypeByName("WorkTab.PriorityTracker");
            HarmonyMethod postfixWorkTabSetPriority = new HarmonyMethod(patchType, nameof(Postfix_WorkTab_SetPriority));
            if (WorkTabPriorityTracker != null)
                harmony.Patch(AccessTools.Method(WorkTabPriorityTracker, "SetPriority", new Type[] { typeof(WorkGiverDef), typeof(int), typeof(int), typeof(bool) }), postfix: postfixWorkTabSetPriority);
            HarmonyMethod postfixHandleBlockingThingJob = new HarmonyMethod(patchType, nameof(Postfix_HandleBlockingThingJob));
            harmony.Patch(AccessTools.Method(typeof(GenConstruct), nameof(GenConstruct.HandleBlockingThingJob)), postfix: postfixHandleBlockingThingJob);
            harmony.Patch(AccessTools.Method(typeof(RoofUtility), nameof(RoofUtility.HandleBlockingThingJob)), postfix: postfixHandleBlockingThingJob);
            harmony.Patch(AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders"), postfix: new HarmonyMethod(patchType, nameof(Postfix_FloatMenuMakerMap_AddHumanlikeOrders)));
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
        #region AutoPatch_Fields
        public static List<StatPatchDef> FoundPatch = new List<StatPatchDef>();
        public static List<MethodInfo> auxMethods;
        public static List<StatPatchDef> auxPatch = new List<StatPatchDef>();
        public static List<Type> JobDefOfTypes;
        private static Type currJobDriver0;
        private static Type currJobDriver;
        private static Type currNestedType;
        private static MethodInfo currMethod;
        private static int foundPawnInstruction;
        private static int foundPawnInstructionOffset;
        private static MethodInfo foundCalledMethod;
        private static MethodInfo TryDegradeTool =>
           AccessTools.Method(typeof(SurvivalToolUtility), nameof(SurvivalToolUtility.TryDegradeTool), new[] { typeof(Pawn), typeof(StatDef) });
        #endregion
        #region AutoPatch_Transpiler_Methods
        private static bool Search_Result_bool = false;
        private static MethodInfo Search_Result_method = null;
        private static List<StatPatchDef> Search_Result_ListStatPatch = new List<StatPatchDef>();
        private static bool Search_Input_bool = false;
        private static IEnumerable<CodeInstruction> Transpile_SearchForStat(IEnumerable<CodeInstruction> instructions)
        {
            Search_Result_bool = false;
            List<CodeInstruction> instructionList = instructions.ToList();
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if (instruction.opcode == OpCodes.Ldfld)
                {
                    FieldInfo field = instruction.operand as FieldInfo;
                    if (field == AccessTools.Field(typeof(JobDriver), nameof(JobDriver.pawn)) || field == AccessTools.Field(typeof(Toil), nameof(Toil.actor)))
                    {
                        foundPawnInstruction = i;
                        for (int j = 1; j <= i; j++)
                        {
                            CodeInstruction instruction1 = instructionList[i - j];
                            if (instruction1.opcode == OpCodes.Ldarg_0)
                            {
                                foundPawnInstructionOffset = j;
                                break;
                            }
                        }
                    }
                }
                if (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                {
                    MethodInfo method = instruction.operand as MethodInfo;
                    if (method?.ReturnType == typeof(Pawn) || method?.ReturnType?.IsSubclassOf(typeof(Pawn)) == true)
                    {
                        foundPawnInstruction = i;
                        for (int j = 1; j <= i; j++)
                        {
                            CodeInstruction instruction1 = instructionList[i - j];
                            if (instruction1.opcode == OpCodes.Ldarg_0)
                            {
                                foundPawnInstructionOffset = j;
                                break;
                            }
                        }
                    }
                }
                if (instruction.opcode == OpCodes.Ldsfld)
                {
                    FieldInfo field = instruction.operand as FieldInfo;
                    if (AutoPatch.StatsToPatch.Exists(t => !t.skip && field == t.oldStatFieldInfo))
                    {
                        Search_Result_bool = true;
                        break;
                    }
                }
            }
            return instructions;
        }
        /*private static bool SearchForStat(MethodInfo method)
        {
            List<KeyValuePair<OpCode, object>> instructions = PatchProcessor.ReadMethodBody(method).ToList();
            for (int i = 0; i < instructions.Count; i++)
            {
                KeyValuePair<OpCode, object> instruction = instructions[i];
                if (instruction.Key == OpCodes.Ldfld)
                {
                    FieldInfo field = instruction.Value as FieldInfo;
                    if (field == AccessTools.Field(typeof(JobDriver), nameof(JobDriver.pawn)) || field == AccessTools.Field(typeof(Toil), nameof(Toil.actor)))
                        foundPawnInstruction = i;
                }
                if (instruction.Key == OpCodes.Ldsfld)
                {
                    FieldInfo field = instruction.Value as FieldInfo;
                    if (AutoPatch.StatsToPatch.Exists(t => !t.skip && field == t.oldStatFieldInfo))
                        return true;
                }
            }
            return false;
        }*/
        private static IEnumerable<CodeInstruction> Transpile_SearchForPawn(IEnumerable<CodeInstruction> instructions)
        {
            Search_Result_bool = false;
            bool foundPawn = false;
            List<CodeInstruction> instructionList = instructions.ToList();
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if (instruction.opcode == OpCodes.Ldfld)
                {
                    FieldInfo field = instruction.operand as FieldInfo;
                    if (field == AccessTools.Field(typeof(JobDriver), nameof(JobDriver.pawn)) || field == AccessTools.Field(typeof(Toil), nameof(Toil.actor)))
                    {
                        foundPawnInstruction = i;
                        for (int j = 1; j <= i; j++)
                        {
                            CodeInstruction instruction1 = instructionList[i - j];
                            if (instruction1.opcode == OpCodes.Ldarg_0)
                            {
                                foundPawnInstructionOffset = j;
                                break;
                            }
                        }
                        foundPawn = true;
                    }
                }
                if (instruction.opcode == OpCodes.Ldsfld && !Search_Input_bool)
                {
                    FieldInfo field = instruction.operand as FieldInfo;
                    if (FoundPatch.Exists(t => field == t.newStatFieldInfo || field == t.oldStatFieldInfo) && !Search_Input_bool)
                    {
                        Search_Result_bool = true;
                        break;
                    }
                }
                if (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt && Search_Input_bool)
                {
                    MethodInfo method = instruction.operand as MethodInfo;
                    if (method?.ReturnType == typeof(Pawn) || method?.ReturnType?.IsSubclassOf(typeof(Pawn)) == true)
                    {
                        foundPawnInstruction = i;
                        for (int j = 1; j <= i; j++)
                        {
                            CodeInstruction instruction1 = instructionList[i - j];
                            if (instruction1.opcode == OpCodes.Ldarg_0)
                            {
                                foundPawnInstructionOffset = j;
                                break;
                            }
                        }
                        foundPawn = true;
                    }
                    else if (method == foundCalledMethod && Search_Input_bool && foundPawn)
                    {
                        Search_Result_bool = true;
                        break;
                    }
                }
            }
            return instructions;
        }
        /*private static bool SearchForPawn(MethodInfo method, bool searchCallFunction)
        {
            bool foundPawn = false;
            List<KeyValuePair<OpCode, object>> instructions = PatchProcessor.ReadMethodBody(method).ToList();
            for (int i = 0; i < instructions.Count; i++)
            {
                KeyValuePair<OpCode, object> instruction = instructions[i];
                if (instruction.Key == OpCodes.Ldfld)
                {
                    FieldInfo field = instruction.Value as FieldInfo;
                    if (field == AccessTools.Field(typeof(JobDriver), nameof(JobDriver.pawn)) || field == AccessTools.Field(typeof(Toil), nameof(Toil.actor)))
                    {
                        foundPawnInstruction = i;
                        foundPawn = true;
                    }
                }
                if (instruction.Key == OpCodes.Ldsfld && !searchCallFunction)
                {
                    FieldInfo field = instruction.Value as FieldInfo;
                    if (FoundPatch.Exists(t => field == t.newStatFieldInfo || field == t.oldStatFieldInfo) && !searchCallFunction)
                        return true;
                }
                if (instruction.Key == OpCodes.Call || instruction.Key == OpCodes.Callvirt && searchCallFunction)
                {
                    MethodInfo calledMethod = instruction.Value as MethodInfo;
                    if (calledMethod == foundCalledMethod)
                        if (foundPawn)
                            return true;
                }
            }
            return false;
        }*/
        private static IEnumerable<CodeInstruction> Transpile_SearchCalledFunction(IEnumerable<CodeInstruction> instructions)
        {
            Search_Result_method = null;
            List<CodeInstruction> instructionList = instructions.ToList();
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                {
                    MethodInfo calledMethod = instruction.operand as MethodInfo;
                    if (auxMethods.Contains(calledMethod))
                    {
                        foundCalledMethod = calledMethod;
                        Search_Result_method = currMethod;
                        break;
                    }
                }
                if (instruction.opcode == OpCodes.Ldftn)
                {
                    CodeInstruction nextInstruction = instructionList[i + 1];
                    if (nextInstruction.opcode == OpCodes.Newobj && nextInstruction.operand as ConstructorInfo == AccessTools.Constructor(typeof(Action), new[] { typeof(object), typeof(IntPtr) }))
                    {
                        MethodInfo calledMethod = instruction.operand as MethodInfo;
                        if (auxMethods.Contains(calledMethod))
                        {
                            Search_Result_method = calledMethod;
                            break;
                        }
                    }
                }
            }
            return instructions;
        }
        /*private static MethodInfo SearchCalledFunction(MethodInfo method)
        {
            List<KeyValuePair<OpCode, object>> instructions = PatchProcessor.ReadMethodBody(method).ToList();
            for (int i = 0; i < instructions.Count; i++)
            {
                KeyValuePair<OpCode, object> instruction = instructions[i];
                if (instruction.Key == OpCodes.Call || instruction.Key == OpCodes.Callvirt)
                {
                    MethodInfo calledMethod = instruction.Value as MethodInfo;
                    if (auxMethods.Contains(calledMethod))
                    {
                        foundCalledMethod = calledMethod;
                        return method;
                    }
                }
                if (instruction.Key == OpCodes.Ldftn)
                {
                    KeyValuePair<OpCode, object> nextInstruction = instructions[i + 1];
                    if (nextInstruction.Key == OpCodes.Newobj && nextInstruction.Value as ConstructorInfo == AccessTools.Constructor(typeof(Action), new[] { typeof(object), typeof(IntPtr) }))
                    {
                        MethodInfo calledMethod = instruction.Value as MethodInfo;
                        if (auxMethods.Contains(calledMethod))
                            return calledMethod;
                    }
                }
            }
            return null;
        }*/
        private static IEnumerable<CodeInstruction> Transpile_SearchForJob(IEnumerable<CodeInstruction> instructions)
        {
            Search_Result_ListStatPatch = new List<StatPatchDef>();
            List<CodeInstruction> instructionList = instructions.ToList();
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if (instruction.opcode == OpCodes.Ldsfld)
                {
                    FieldInfo field = instruction.operand as FieldInfo;
                    foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => !t.skip))
                        if (patch.FoundJobDef.Exists(t => t.fieldInfo == field))
                            Search_Result_ListStatPatch.AddDistinct(patch);
                }
            }
            return instructions;
        }
        /*private static List<StatPatchDef> SearchForJob(MethodInfo method)
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
                            fPatch.AddDistinct(patch);
                }
            }
            return fPatch;
        }*/
        private static IEnumerable<CodeInstruction> Transpile_Replace_Stat(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if (instruction.opcode == OpCodes.Ldsfld)
                {
                    foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => !t.skip))
                    {
                        if (instruction.operand as FieldInfo == patch.oldStatFieldInfo)
                        {
                            FoundPatch.AddDistinct(patch);
                            if (patch.newStat != null)
                                instruction.operand = patch.newStatFieldInfo;
                            if (patch.StatReplacer != null && patch.canPatch)
                            {
                                List<CodeInstruction> ReplacementCI = patch.Stat_CodeInstructions;
                                ReplacementCI.Reverse();
                                instructionList.RemoveAt(i);
                                foreach (CodeInstruction CI in ReplacementCI)
                                    instructionList.Insert(i, CI);
                            }
                            break;
                        }
                    }
                    //if (FoundWorker) break;
                }
            }
            return instructionList.AsEnumerable();
        }
        private static IEnumerable<CodeInstruction> Transpile_AddDegrade(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            int i = foundPawnInstruction;
            int j = foundPawnInstructionOffset;
            CodeInstruction instruction0 = instructionList[i];
            string errMessage = $"Not a pawn instruction to add tool degrade\n{currJobDriver0} : {currJobDriver} : {currNestedType} : {currMethod}\n{i} : {instruction0.opcode} : {instruction0.operand}";
            switch (instruction0.opcode)
            {
                case OpCode opCode when opCode == OpCodes.Ldfld:
                    FieldInfo field = instruction0.operand as FieldInfo;
                    if (field != AccessTools.Field(typeof(JobDriver), nameof(JobDriver.pawn)) && field != AccessTools.Field(typeof(Toil), nameof(Toil.actor)))
                    {
                        Log.Error(errMessage);
                        return instructionList.AsEnumerable();
                    }
                    break;
                case OpCode opCode when opCode == OpCodes.Call || opCode == OpCodes.Callvirt:
                    MethodInfo method = instruction0.operand as MethodInfo;
                    if (method.ReturnType != typeof(Pawn) && !method.ReturnType.IsSubclassOf(typeof(Pawn)))
                    {
                        Log.Error(errMessage);
                        return instructionList.AsEnumerable();
                    }
                    break;
                default:
                    Log.Error(errMessage);
                    return instructionList.AsEnumerable();
            }
            List<CodeInstruction> prevInstructions = instructionList.GetRange(i - j, j + 1);
            prevInstructions.Reverse();
            foreach (StatPatchDef patch in FoundPatch.Where(t => t.addToolDegrade))
            {
                //instructionList.Insert(i + 1, instruction0);
                foreach (CodeInstruction prevInstruction in prevInstructions)
                    instructionList.Insert(i + 1, prevInstruction);
                instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Call, TryDegradeTool));
                if (patch.newStat != null)
                    instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ST_StatDefOf), patch.newStat.defName)));
                else if (patch.StatReplacer != null)
                {
                    List<CodeInstruction> ReplacementCI = patch.Stat_CodeInstructions;
                    ReplacementCI.Reverse();
                    foreach (CodeInstruction CI in ReplacementCI)
                        instructionList.Insert(i + 1, CI);
                }
                else
                    instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(StatDefOf), patch.oldStat.defName)));
            }
            return instructionList.AsEnumerable();
        }
        #endregion
        #region ManualPatches

        public static PropertyInfo workTab_Prop = AccessTools.Property(AccessTools.TypeByName("WorkTab.PriorityTracker"), "Pawn");
        public static void Postfix_WorkTab_SetPriority(object __instance)
        {
            Pawn pawn = (Pawn)workTab_Prop.GetValue(__instance);
            if (pawn?.TryGetComp<ThingComp_WorkSettings>()?.WorkSettingsChanged == false)
                pawn.GetComp<ThingComp_WorkSettings>().WorkSettingsChanged = true;
        }
        public static void Postfix_HandleBlockingThingJob(ref Job __result, Pawn worker)
        {
            if (__result?.def == JobDefOf.CutPlant && __result.targetA.Thing.def.plant.IsTree)
            {
                if (!worker.MeetsWorkGiverStatRequirements(ST_WorkGiverDefOf.FellTrees.GetModExtension<WorkGiverExtension>().requiredStats))
                    __result = null;
            }
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
                        Job job9 = JobMaker.MakeJob(JobDefOf.TakeInventory, tool);
                        tool.toBeForced = true;
                        job9.count = 1;
                        job9.checkEncumbrance = true;
                        pawn.jobs.TryTakeOrderedJob(job9);
                    }, MenuOptionPriority.High), pawn, tool));
                }
            }
        }
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

            HarmonyMethod TranspileSearchForStat = new HarmonyMethod(patchType, nameof(Transpile_SearchForStat));
            HarmonyMethod TranspileSearchForPawn = new HarmonyMethod(patchType, nameof(Transpile_SearchForPawn));
            HarmonyMethod TranspileSearchCalledFunction = new HarmonyMethod(patchType, nameof(Transpile_SearchCalledFunction));
            // AutoPatch
            IEnumerable<Type> AllJobDrivers = GenTypes.AllSubclasses(typeof(JobDriver));
            foreach (Type jobDriver in AllJobDrivers)
            {
                currJobDriver0 = jobDriver;
                currJobDriver = null;
                currNestedType = null;
                auxPatch = new List<StatPatchDef>();
                // Check which patch can be ignored
                foreach (StatPatchDef patch in AutoPatch.StatsToPatch)
                {
                    patch.CheckJobDriver(jobDriver);
                    if (!patch.skip && patch.StatReplacer != null)
                        patch.Initialize_StatReplacer(jobDriver);
                }
                // Patch auxiliary methods: Don't add ToolDegrade here
                List<MethodInfo> jbMethods = AccessTools.GetDeclaredMethods(jobDriver);
                foreach (MethodInfo jbMethod in jbMethods)
                {
                    currMethod = jbMethod;
                    FoundPatch = new List<StatPatchDef>();
                    if (jbMethod.IsAbstract)
                        continue;
                    //if (SearchForStat(jbMethod))
                    tempHarmony.Patch(jbMethod, transpiler: TranspileSearchForStat);
                    tempHarmony.Unpatch(jbMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                    if (Search_Result_bool)
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
                            if (patch.addToolDegrade && (AccessTools.GetReturnedType(jbMethod) == typeof(Action) || AccessTools.GetReturnedType(jbMethod) == typeof(Toil)))
                                jdpatch.FoundStage2 = true;
                        }
                        if (FoundPatch.Exists(t => t.addToolDegrade) && (AccessTools.GetReturnedType(jbMethod) == typeof(Action) || AccessTools.GetReturnedType(jbMethod) == typeof(Toil)))
                        {
                            harmony.Patch(jbMethod, transpiler: transpileAddDegrade);
                        }
                    }
                    foreach (StatPatchDef patch in FoundPatch)
                        auxPatch.AddDistinct(patch);
                }
                // Patch MakeToil: Assumed is in delegated method: add ToolDegrade here
                Type[] nestedTypes = jobDriver.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (Type nType in nestedTypes)
                {
                    currNestedType = nType;
                    foreach (StatPatchDef patch in AutoPatch.StatsToPatch.Where(t => !t.skip && t.StatReplacer != null))
                        patch.Initialize_StatReplacer(jobDriver, nType);
                    jbMethods = AccessTools.GetDeclaredMethods(nType);
                    foreach (MethodInfo jbMethod in jbMethods)
                    {
                        currMethod = jbMethod;
                        FoundPatch = new List<StatPatchDef>();
                        if (jbMethod.IsAbstract)
                            continue;
                        //if (SearchForStat(jbMethod))
                        tempHarmony.Patch(jbMethod, transpiler: TranspileSearchForStat);
                        tempHarmony.Unpatch(jbMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                        if (Search_Result_bool)
                        {
                            harmony.Patch(jbMethod, transpiler: transpileReplaceStat);
                            foreach (StatPatchDef patch in FoundPatch)
                            {
                                if (patch.addToolDegrade)
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
                foreach (StatPatchDef patch in auxPatch.Where(t => t.addToolDegrade))
                {
                    JobDriverPatch jdpatch = patch.FoundJobDrivers.Find(t => t.driver == jobDriver);
                    if (jdpatch.FoundStage2) continue;
                    auxMethods = jdpatch.auxmethods;
                    FoundPatch = new List<StatPatchDef> { patch };
                    foreach (Type nType in nestedTypes)
                    {
                        currNestedType = nType;
                        jbMethods = AccessTools.GetDeclaredMethods(nType);
                        foreach (MethodInfo jbMethod in jbMethods)
                        {
                            currMethod = jbMethod;
                            if (jbMethod.IsAbstract)
                                continue;
                            //MethodInfo foundMethod = SearchCalledFunction(jbMethod);
                            tempHarmony.Patch(jbMethod, transpiler: TranspileSearchCalledFunction);
                            tempHarmony.Unpatch(jbMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                            MethodInfo foundMethod = Search_Result_method;
                            if (foundMethod != null)
                            {
                                currMethod = foundMethod;
                                jdpatch.FoundStage2 = true;
                                //if (!SearchForPawn(foundMethod, foundMethod == jbMethod))
                                Search_Input_bool = (foundMethod == jbMethod);
                                tempHarmony.Patch(foundMethod, transpiler: TranspileSearchForPawn);
                                tempHarmony.Unpatch(foundMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                                if (!Search_Result_bool)
                                {
                                    Logger.Error($"{jobDriver} : {nType} : {foundMethod}: No pawn instruction found to patch tool degrade.\n");
                                    continue;
                                }
                                harmony.Patch(foundMethod, transpiler: transpileAddDegrade);
                                jdpatch.methods.Add(foundMethod);
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
                        currJobDriver = baseType;
                        auxMethods = new List<MethodInfo>();
                        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(baseType))
                            if (auxMethods2.Exists(t => t.Name == method.Name))
                                auxMethods.Add(method);
                        Type[] nestedTypes2 = baseType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (Type nType in nestedTypes2)
                        {
                            currNestedType = nType;
                            jbMethods = AccessTools.GetDeclaredMethods(nType);
                            foreach (MethodInfo jbMethod in jbMethods)
                            {
                                currMethod = jbMethod;
                                if (jbMethod.IsAbstract)
                                    continue;
                                //MethodInfo foundMethod = SearchCalledFunction(jbMethod);
                                tempHarmony.Patch(jbMethod, transpiler: TranspileSearchCalledFunction);
                                tempHarmony.Unpatch(jbMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                                MethodInfo foundMethod = Search_Result_method;
                                if (foundMethod != null)
                                {
                                    currMethod = foundMethod;
                                    jdpatch.FoundStage2 = true;
                                    //if (!SearchForPawn(foundMethod, foundMethod == jbMethod))
                                    Search_Input_bool = (foundMethod == jbMethod);
                                    tempHarmony.Patch(foundMethod, transpiler: TranspileSearchForPawn);
                                    tempHarmony.Unpatch(foundMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                                    if (!Search_Result_bool)
                                    {
                                        Logger.Error($"{jobDriver} : {baseType} : {nType} : {foundMethod}: No pawn instruction found to patch tool degrade.");
                                        continue;
                                    }
                                    harmony.Patch(foundMethod, transpiler: transpileAddDegrade);
                                    jdpatch.methods.Add(foundMethod);
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
                    currJobDriver0 = jobDriver;
                    currNestedType = null;
                    currJobDriver = null;
                    // Patch auxiliary methods: Don't add ToolDegrade here
                    List<MethodInfo> jbMethods = AccessTools.GetDeclaredMethods(jobDriver);
                    foreach (MethodInfo jbMethod in jbMethods)
                    {
                        currMethod = jbMethod;
                        if (jbMethod.IsAbstract)
                            continue;
                        //if (SearchForStat(jbMethod))
                        tempHarmony.Patch(jbMethod, transpiler: TranspileSearchForStat);
                        tempHarmony.Unpatch(jbMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                        if (Search_Result_bool)
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
                        currNestedType = nType;
                        jbMethods = AccessTools.GetDeclaredMethods(nType);
                        foreach (MethodInfo jbMethod in jbMethods)
                        {
                            currMethod = jbMethod;
                            if (jbMethod.IsAbstract)
                                continue;
                            //if (SearchForStat(jbMethod))
                            tempHarmony.Patch(jbMethod, transpiler: TranspileSearchForStat);
                            tempHarmony.Unpatch(jbMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                            if (Search_Result_bool)
                            {
                                harmony.Patch(jbMethod, transpiler: transpileReplaceStat);
                                if (patch.addToolDegrade)
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
                if (jobDef?.driverClass == null)
                {
                    Logger.Warning($"Found null jobDef: {jobDef} : {jobDef?.driverClass}");
                    continue;
                }
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
            HarmonyMethod TranspileSearchForJob = new HarmonyMethod(patchType, nameof(Transpile_SearchForJob));
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

                    //foreach (StatPatchDef patch in SearchForJob(wgMethod))
                    tempHarmony.Patch(wgMethod, transpiler: TranspileSearchForJob);
                    tempHarmony.Unpatch(wgMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                    List<StatPatchDef> foundPatches = Search_Result_ListStatPatch;
                    foreach (StatPatchDef patch in foundPatches)
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
                        //foreach (StatPatchDef patch in SearchForJob(wgMethod))
                        tempHarmony.Patch(wgMethod, transpiler: TranspileSearchForJob);
                        tempHarmony.Unpatch(wgMethod, HarmonyPatchType.Transpiler, "Lecris.survivaltools.TempPatch");
                        List<StatPatchDef> foundPatches = Search_Result_ListStatPatch;
                        foreach (StatPatchDef patch in foundPatches)
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
                        if (patch.newStat != null)
                            extension.requiredStats.Add(patch.newStat);
                        else if (patch.StatReplacer != null)
                            extension.requiredStats.AddRange(patch.potentialStats);
                        else
                            extension.requiredStats.Add(patch.oldStat);
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
