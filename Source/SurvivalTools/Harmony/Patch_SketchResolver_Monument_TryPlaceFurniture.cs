using HarmonyLib;
using RimWorld;
using RimWorld.SketchGen;
using System.Collections.Generic;
using Verse;

namespace SurvivalTools.HarmonyPatches
{
    /*[HarmonyPatch(typeof(SketchResolver_Monument))]
    [HarmonyPatch("TryPlaceFurniture")]
    public static class Patch_SketchResolver_Monument_TryPlaceFurniture
    {
        public static void Postfix(ResolveParams parms, Sketch monument)
        {
            if (SurvivalToolsSettings.toolMapGen)
            {
                int count = Rand.Range(2, 4);
                for (int i = 0; i < count; i++)
                {
                    ThingDef thingDef = availableTools.RandomElementByWeight(t => ToolsWeightOffset[t]);
                    ToolsWeightOffset[thingDef] /= 2;
                    ResolveParams parms3 = parms;
                    parms3.sketch = monument;
                    parms3.requireFloor = false;
                    parms3.allowWood = true;
                    parms3.wallEdgeThing = thingDef;
                    //parms3.points = 50000;
                    parms3.totalPoints = 10;
                    SketchResolverDefOf.AddWallEdgeThings.Resolve(parms3);
                }
            }
        }
        public static Dictionary<ThingDef, float> ToolsWeightOffset = new Dictionary<ThingDef, float>();
        public static List<ThingDef> availableTools;
    }*/
}