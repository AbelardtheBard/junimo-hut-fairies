using HarmonyLib;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Events;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace FarmCompanionRoamerMod
{
    [HarmonyPatch]
    public static class CropFairyHarmonyPatch
    {
        // Patch Utility.pickFarmEvent to add global event chance bonus using a prefix
        [HarmonyPatch(typeof(Utility), nameof(Utility.pickFarmEvent))]
        [HarmonyPrefix]
        public static bool Utility_pickFarmEvent_Prefix(ref FarmEvent __result)
        {
            ModEntry.Logger?.Log("[DEBUG] Utility_pickFarmEvent_Prefix called!", StardewModdingAPI.LogLevel.Info);
            
            if (!ModEntry.Config?.EnhancedCropFairy ?? false)
            {
                ModEntry.Logger?.Log("[DEBUG] EnhancedCropFairy disabled, running vanilla", StardewModdingAPI.LogLevel.Info);
                return true; // Run original method
            }
            
            ModEntry.Logger?.Log("[DEBUG] EnhancedCropFairy enabled, calculating bonus", StardewModdingAPI.LogLevel.Info);
            
            // Calculate our enhanced fairy chance
            int fairyBoxCount = 0;
            foreach (var farm in Game1.locations.OfType<Farm>())
            {
                foreach (var hut in farm.buildings.OfType<JunimoHut>())
                {
                    var chest = hut.GetOutputChest();
                    if (chest?.Items == null) continue;
                    fairyBoxCount += chest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox");
                }
            }
            
            int maxBoxes = ModEntry.Config?.CropFairyMaxBoxes ?? 8;
            double chancePerBox = (ModEntry.Config?.CropFairyChancePerBox ?? 0.1) / 100.0;
            int capped = maxBoxes == -1 ? fairyBoxCount : Math.Min(fairyBoxCount, maxBoxes);
            double bonus = capped * chancePerBox;
            
            ModEntry.Logger?.Log($"[DEBUG] Found {fairyBoxCount} Fairy Boxes, capped at {capped}, bonus = {bonus * 100:F1}%", StardewModdingAPI.LogLevel.Info);
            
            // Only proceed if we have a bonus and it's time for potential fairy event
            if (bonus > 0 && !Game1.IsWinter && Game1.dayOfMonth != 1)
            {
                ModEntry.Logger?.Log("[DEBUG] Conditions met for fairy event check", StardewModdingAPI.LogLevel.Info);
                var random = Utility.CreateDaySaveRandom();
                // Skip the first 10 random calls (matching vanilla)
                for (int i = 0; i < 10; i++)
                {
                    random.NextDouble();
                }
                
                // Check if our enhanced chance triggers a fairy event
                double baseChance = 0.01;
                double fairyRoseBonus = Game1.getFarm().hasMatureFairyRoseTonight ? 0.007 : 0.0;
                double totalChance = baseChance + fairyRoseBonus + bonus;
                
                ModEntry.Logger?.Log($"[DEBUG] Total chance: {totalChance * 100:F2}% (base: {baseChance * 100}%, rose: {fairyRoseBonus * 100}%, mod: {bonus * 100:F1}%)", StardewModdingAPI.LogLevel.Info);
                
                double roll = random.NextDouble();
                ModEntry.Logger?.Log($"[DEBUG] Random roll: {roll:F4}, needed: < {totalChance:F4}", StardewModdingAPI.LogLevel.Info);
                
                if (roll < totalChance)
                {
                    string msg = $"Enhanced Crop Fairy: +{bonus * 100:F1}% event chance for {capped} Fairy Boxes (triggered!)";
                    ModEntry.Logger?.Log($"[EnhancedCropFairy] {msg}", StardewModdingAPI.LogLevel.Info);
                    
                    __result = new FairyEvent();
                    return false; // Skip original method
                }
                else
                {
                    string msg = $"Enhanced Crop Fairy: +{bonus * 100:F1}% event chance for {capped} Fairy Boxes (not triggered)";
                    ModEntry.Logger?.Log($"[EnhancedCropFairy] {msg}", StardewModdingAPI.LogLevel.Debug);
                }
            }
            else
            {
                ModEntry.Logger?.Log($"[DEBUG] Skipping fairy check: bonus={bonus}, isWinter={Game1.IsWinter}, dayOfMonth={Game1.dayOfMonth}", StardewModdingAPI.LogLevel.Info);
            }
            
            return true; // Run original method for other events
        }

        // Patch FairyEvent.ChooseCrop to apply weighted crop selection
        [HarmonyPatch(typeof(FairyEvent), "ChooseCrop")]
        [HarmonyPrefix]
        public static bool FairyEvent_ChooseCrop_Prefix(FairyEvent __instance, Farm ___f, ref Vector2 __result)
        {
            if (!ModEntry.Config?.EnhancedCropFairy ?? false)
                return true; // run vanilla

            // 1. Gather all valid crops (as vanilla)
            var validCrops = (from p in ___f.terrainFeatures.Pairs
                              where p.Value is StardewValley.TerrainFeatures.HoeDirt { crop: not null } hoeDirt
                              && !hoeDirt.crop.dead.Value
                              && !hoeDirt.crop.isWildSeedCrop()
                              && hoeDirt.crop.currentPhase.Value < hoeDirt.crop.phaseDays.Count - 1
                              select p.Key).ToList();
            if (validCrops.Count == 0)
            {
                __result = Vector2.Zero;
                Game1.addHUDMessage(new HUDMessage("Enhanced Crop Fairy: No valid crops found!", HUDMessage.error_type));
                ModEntry.Logger?.Log("[EnhancedCropFairy] No valid crops found for selection.", StardewModdingAPI.LogLevel.Warn);
                return false;
            }
            // (No per-crop log here; use debug command for details)

            // 2. Gather all Junimo Huts and their Fairy Box counts
            var huts = ___f.buildings.OfType<JunimoHut>().ToList();
            var hutBoxCounts = new Dictionary<Vector2, int>();
            foreach (var hut in huts)
            {
                var chest = hut.GetOutputChest();
                int count = chest?.Items?.Count(item => item?.QualifiedItemId == "(TR)FairyBox") ?? 0;
                hutBoxCounts[new Vector2(hut.tileX.Value, hut.tileY.Value)] = count;
                // (No per-hut log here; use debug command for details)
            }

            // 3. For each crop, find nearest hut (within Junimo Hut range: 8 tiles) and get its Fairy Box count
            int hutRange = 8; // Always match vanilla Junimo Hut range
            double weightPerBox = ModEntry.Config?.CropFairyWeightPerBox ?? 1.0;
            int maxWeightBoxes = ModEntry.Config?.CropFairyMaxWeightBoxes ?? 36;
            
            List<(Vector2 pos, int weight, int boxes, Vector2? hutPos)> weightedCrops = new();
            foreach (var cropPos in validCrops)
            {
                int maxBoxes = 0;
                Vector2? nearestHut = null;
                foreach (var hut in hutBoxCounts)
                {
                    float dx = Math.Abs(cropPos.X - hut.Key.X);
                    float dy = Math.Abs(cropPos.Y - hut.Key.Y);
                    if (dx <= hutRange && dy <= hutRange)
                    {
                        if (hut.Value > maxBoxes)
                        {
                            maxBoxes = hut.Value;
                            nearestHut = hut.Key;
                        }
                    }
                }
                // Apply the configurable cap and weight calculation
                int cappedBoxes = Math.Min(maxBoxes, maxWeightBoxes);
                int weight = 1 + (int)(cappedBoxes * weightPerBox);
                weightedCrops.Add((cropPos, weight, cappedBoxes, nearestHut));
            }
            // (No per-crop weighted log here; use debug command for details)

            // 4. Weighted random selection
            int totalWeight = weightedCrops.Sum(wc => wc.weight);
            if (totalWeight == 0)
            {
                __result = validCrops[Game1.random.Next(validCrops.Count)];
                Game1.addHUDMessage(new HUDMessage("Enhanced Crop Fairy: No weighted crops, picking randomly!", HUDMessage.error_type));
                ModEntry.Logger?.Log("[EnhancedCropFairy] No weighted crops, picking randomly!", StardewModdingAPI.LogLevel.Warn);
                return false;
            }
            int choice = Game1.random.Next(totalWeight);
            int accum = 0;
            foreach (var (pos, weight, boxes, hutPos) in weightedCrops)
            {
                accum += weight;
                if (choice < accum)
                {
                    __result = pos;
                    string msg = $"Enhanced Crop Fairy: Chose crop at {pos} (weight={weight}, boxes={boxes}, hut={hutPos})";
                    Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.newQuest_type));
                    ModEntry.Logger?.Log($"[EnhancedCropFairy] FINAL CHOICE: {pos}", StardewModdingAPI.LogLevel.Info);
                    return false; // skip vanilla
                }
            }
            // Fallback (should not hit)
            __result = validCrops[Game1.random.Next(validCrops.Count)];
            Game1.addHUDMessage(new HUDMessage("Enhanced Crop Fairy: Fallback to random crop!", HUDMessage.error_type));
            ModEntry.Logger?.Log("[EnhancedCropFairy] Fallback to random crop!", StardewModdingAPI.LogLevel.Warn);
            return false;
        }
    }
}
