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
            /*Patch_SketchResolver_Monument_TryPlaceFurniture.availableTools = ST_ThingCategoryDefOf.SurvivalTools.DescendantThingDefs.Where(t => t.techLevel <= TechLevel.Medieval).ToList();
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
            }*/

            if (ModCompatibilityCheck.MendAndRecycle)
                ResolveMendAndRecycleRecipes();
            ResolveSmeltingRecipeUsers();

            // Add SurvivalToolAssignmentTracker to all appropriate pawns
            foreach (ThingDef tDef in DefDatabase<ThingDef>.AllDefs.Where(t => t.race?.Humanlike == true))
            {
                if (tDef.comps == null)
                    tDef.comps = new List<CompProperties>();
                tDef.comps.Add(new CompProperties(typeof(Pawn_SurvivalToolAssignmentTracker)));
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
    }
}