using RimWorld;
using Verse;
using Verse.AI;

namespace SurvivalTools
{
    /*public class ThinkNode_WorkSettingsChange : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn?.Faction == Faction.OfPlayer)
            {
                if (pawn.TryGetComp<ThingComp_WorkSettings>()?.WorkSettingsChanged == true)
                {
                    pawn.GetComp<ThingComp_WorkSettings>().WorkSettingsChanged = false;
                    pawn.GetToolTracker().dirtyCache = true;
                    return true;
                }
            }
            return false;
        }
    }*/
}