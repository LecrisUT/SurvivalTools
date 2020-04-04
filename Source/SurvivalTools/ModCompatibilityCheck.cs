using System.Linq;
using Verse;

namespace SurvivalTools
{
    [StaticConstructorOnStartup]
    public static class ModCompatibilityCheck
    {
        public static bool CombatExtended = ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "Combat Extended");
        public static bool PickUpAndHaul = ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "PickUpAndHaul");
        public static bool MendAndRecycle = ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "MendAndRecycle");
        public static bool OtherInventoryModsActive = CombatExtended || PickUpAndHaul;
        public static bool DubsBadHygiene = ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "Dubs Bad Hygiene");
    }
}