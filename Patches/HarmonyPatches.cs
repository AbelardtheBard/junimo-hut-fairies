using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using StardewModdingAPI;

namespace FarmCompanionRoamerMod
{
    // Harmony patch for Wand.DoFunction to ensure fairy persistence across warps
    using System.Reflection;
    using StardewValley.Tools;

    [HarmonyPatch(typeof(Wand), nameof(Wand.DoFunction))]
    public static class Wand_DoFunction_Patch
    {
        // Save fairy state before warp
        public static void Prefix()
        {
            ModEntry.Logger?.Log("[Harmony] Wand.DoFunction Prefix: Saving fairy state before warp.", LogLevel.Trace);
            FarmCompanionRoamerMod.ModEntry.Instance?.SaveFairyState();
        }

        // Restore fairies after warp
        public static void Postfix()
        {
            ModEntry.Logger?.Log("[Harmony] Wand.DoFunction Postfix: Re-scanning fairies after warp.", LogLevel.Trace);
            FarmCompanionRoamerMod.ModEntry.Instance?.ScanAndSpawnFairies();
        }
    }
}
