using RimWorld;
using SurvivalTools.HarmonyPatches;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SurvivalTools
{
    [StaticConstructorOnStartup]
    public static class StaticConstructorClass
    {
        static StaticConstructorClass()
        {
            Patch_SketchResolver_Monument_TryPlaceFurniture.availableTools = ST_ThingCategoryDefOf.SurvivalTools.DescendantThingDefs.Where(t => t.techLevel <= TechLevel.Medieval).ToList();
            foreach (ThingDef tool in Patch_SketchResolver_Monument_TryPlaceFurniture.availableTools)
            {
                float offset = 1;
                switch (tool.techLevel)
                {
                    case TechLevel.Neolithic:
                        offset = 8;
                        break;
                    case TechLevel.Medieval:
                        offset = 1;
                        break;
                    default:
                        offset = 0;
                        break;
                }
                Patch_SketchResolver_Monument_TryPlaceFurniture.ToolsWeightOffset.Add(tool, offset);
            }

            if (ModCompatibilityCheck.MendAndRecycle)
                ResolveMendAndRecycleRecipes();
            ResolveSmeltingRecipeUsers();
            CheckStuffForStuffPropsTool();

            // Add SurvivalToolAssignmentTracker to all appropriate pawns
            foreach (ThingDef tDef in DefDatabase<ThingDef>.AllDefs.Where(t => t.race?.Humanlike == true))
            {
                if (tDef.comps == null)
                    tDef.comps = new List<CompProperties>();
                tDef.comps.Add(new CompProperties(typeof(Pawn_SurvivalToolAssignmentTracker)));
                tDef.comps.Add(new CompProperties(typeof(ThingComp_WorkSettings)));
            }
        }

        private static void ResolveMendAndRecycleRecipes()
        {
            bool categoryMatch = false;
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs.Where(r => r.defName.Contains("SurvivalTool") && r.workerClass != typeof(RecipeWorker)))
            {
                categoryMatch = false;
                foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefsListForReading.Where(t => t.thingClass == typeof(SurvivalTool)))
                    if (recipe.IsIngredient(thing))
                    {
                        categoryMatch = true;
                        break;
                    }
                if (!categoryMatch)
                {
                    recipe.recipeUsers.Clear();
                }
            }
        }

        private static void ResolveSmeltingRecipeUsers()
        {
            foreach (ThingDef benchDef in DefDatabase<ThingDef>.AllDefs.Where(t => t.IsWorkTable))
                if (benchDef.recipes != null)
                {
                    if (benchDef.recipes.Contains(ST_RecipeDefOf.SmeltWeapon))
                        benchDef.recipes.Add(ST_RecipeDefOf.SmeltSurvivalTool);
                    if (benchDef.recipes.Contains(ST_RecipeDefOf.DestroyWeapon))
                        benchDef.recipes.Add(ST_RecipeDefOf.DestroySurvivalTool);
                }
        }

        private static void CheckStuffForStuffPropsTool()
        {
            StringBuilder stuffBuilder = new StringBuilder();
            stuffBuilder.AppendLine("Checking all stuff for StuffPropsTool modExtension...");
            stuffBuilder.AppendLine();
            StringBuilder hasPropsBuilder = new StringBuilder("Has props:\n");
            StringBuilder noPropsBuilder = new StringBuilder("Doesn't have props:\n");

            List<StuffCategoryDef> toolCats = new List<StuffCategoryDef>();
            foreach (ThingDef tool in DefDatabase<ThingDef>.AllDefsListForReading.Where(t => t.IsSurvivalTool()))
                if (!tool.stuffCategories.NullOrEmpty())
                    foreach (StuffCategoryDef category in tool.stuffCategories)
                        if (!toolCats.Contains(category))
                            toolCats.Add(category);

            foreach (ThingDef stuff in DefDatabase<ThingDef>.AllDefsListForReading.Where(
                (ThingDef t) =>
                {
                    if (!t.IsStuff)
                        return false;
                    bool retVal = false;
                    foreach (StuffCategoryDef stuffCat in t.stuffProps.categories)
                        if (toolCats.Contains(stuffCat))
                        {
                            retVal = true;
                            break;
                        }
                    return retVal;
                }))
            {
                if (stuff.modContentPack == null) continue;
                string newLine = $"{stuff} ({stuff.modContentPack.Name})";
                if (stuff.HasModExtension<StuffPropsTool>())
                    hasPropsBuilder.AppendLine(newLine);
                else
                    noPropsBuilder.AppendLine(newLine);
            }

            stuffBuilder.Append(hasPropsBuilder);
            stuffBuilder.AppendLine();
            stuffBuilder.Append(noPropsBuilder);
            Log.Message(stuffBuilder.ToString(), false);
        }
    }
}