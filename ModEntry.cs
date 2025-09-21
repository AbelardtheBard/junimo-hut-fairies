using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.Objects.Trinkets;
using StardewValley.BellsAndWhistles;

namespace FarmCompanionRoamerMod
{
    public class ModEntry : Mod
    {
    // Show any pending HUD message on the main thread
    private void OnUpdateTicked_ShowHudMessage(object? sender, UpdateTickedEventArgs e)
    {
        if (!string.IsNullOrEmpty(pendingHudMessage))
        {
            Game1.addHUDMessage(new HUDMessage(pendingHudMessage, HUDMessage.error_type));
            pendingHudMessage = null;
        }
    }
    private string? pendingHudMessage = null;
    private int pendingFarmRespawnTicks = 0;
        // Removed unused lastWarpType field
        private void ClearAllFairies()
        {
            hutCompanions.Clear();
        }

        // ...existing code...
        public static ModEntry? Instance { get; private set; }
        internal static IMonitor? Logger { get; private set; }
        public static Dictionary<Vector2, List<Models.TrinketCompanion>> hutCompanions = new();
        private static int lastScanDay = -1;
        private static bool shouldSaveAfterFirstScan = false;
        internal static ModConfig Config = null!;

        /// <summary>
        /// Gets the appropriate style index based on the current configuration (1-8 only)
        /// </summary>
        private static int GetStyleIndex()
        {
            // Simple 1-8 style selection with fallback to style 1
            if (Config?.FairyStyleID is int styleId && styleId >= 1 && styleId <= 8)
                return styleId;
            
            return 1; // Default fallback
        }
        
        public override void Entry(IModHelper helper)
        {
            this.Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            this.Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked_ShowHudMessage;
            // Removed: RenderedWorld event handler. Drawing is now injected via Harmony patch for correct layering.
            var harmony = new HarmonyLib.Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();
            Instance = this;
            Logger = Monitor;
            
            // Load global config using standard SMAPI method
            Config = this.Helper.ReadConfig<ModConfig>();

            this.Helper.Events.GameLoop.DayStarted += OnDayStarted;
            this.Helper.Events.GameLoop.DayEnding += this.OnDayEnding_LogFairyChance;
            this.Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            this.Helper.Events.GameLoop.Saving += OnSaving; // Save fairy state when game saves
            this.Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            this.Helper.Events.Display.MenuChanged += OnMenuChanged;
            this.Helper.Events.Player.Warped += OnPlayerWarped;
            this.Helper.Events.World.ObjectListChanged += OnObjectListChanged;
            // Debug/utility console commands
            this.Helper.ConsoleCommands.Add("fcr_scan", "Scan Junimo Huts for Fairy Boxes", OnScanCommand);
            this.Helper.ConsoleCommands.Add("fcr_clear", "Clear all roaming fairies", OnClearCommand);
            this.Helper.ConsoleCommands.Add("fcr_status", "Show status of all roaming fairies", OnStatusCommand);
            this.Helper.ConsoleCommands.Add("fcr_print_fairybox_moddata", "Print all Fairy Box modData in inventory", OnPrintFairyBoxModDataCommand);
            this.Helper.ConsoleCommands.Add("fcr_force_fairy", "Force the Fairy event to occur the next night.", OnForceFairyEventCommand);
            this.Helper.ConsoleCommands.Add("fcr_give_fairybox", "Give yourself a Fairy Box trinket. Usage: fcr_give_fairybox [amount]", OnGiveFairyBoxCommand);
            this.Helper.ConsoleCommands.Add("fcr_test_fairy_chance", "Test the enhanced fairy chance calculation", OnTestFairyChanceCommand);
        }

        private void OnGameLaunched(object? sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            SetupGenericModConfigMenu();
        }

        private void SetupGenericModConfigMenu()
        {
            // Get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // Register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => {
                    Config = new ModConfig();
                    this.Helper.WriteConfig(Config);
                },
                save: () => {
                    this.Helper.WriteConfig(Config);
                },
                titleScreenOnly: false
            );

            // Add config sections and options
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => "Fairy Companion Settings"
            );

            // FairyStyleID - simple numeric selection 1-8
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Fairy Style",
                tooltip: () => "Choose fairy appearance style (1-8). Each style has a different look and color.",
                getValue: () => Config.FairyStyleID,
                setValue: value => Config.FairyStyleID = value,
                min: 1,
                max: 8
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Fairy Spawn Cap",
                tooltip: () => "Maximum number of fairy companions per Junimo Hut",
                getValue: () => Config.FairySpawnCap,
                setValue: value => Config.FairySpawnCap = value,
                min: 1,
                max: 36
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => "Enhanced Crop Fairy Settings"
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enhanced Crop Fairy",
                tooltip: () => "If enabled, Fairy Boxes boost Crop Fairy event chance and add weight to the crop selection",
                getValue: () => Config.EnhancedCropFairy,
                setValue: value => Config.EnhancedCropFairy = value
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Chance Bonus Per Box (%)",
                tooltip: () => "[Event Chance] Bonus to the chance for the Crop Fairy event, per Fairy Box.",
                getValue: () => (float)Config.CropFairyChancePerBox,
                setValue: value => Config.CropFairyChancePerBox = value,
                min: 0.025f,
                max: 1.0f,
                interval: 0.025f,
                formatValue: value => $"{value:F3}%"
            );

            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => "Max Boxes for Global Bonus",
                tooltip: () => "[Event Chance] Maximum total Fairy Boxes (across all huts) that count toward the global Crop Fairy event chance bonus (minimum: 1, or -1 for unlimited)",
                getValue: () => Config.CropFairyMaxBoxes == -1 ? "unlimited" : Config.CropFairyMaxBoxes.ToString(),
                setValue: value => {
                    if (value.ToLower() == "unlimited" || value == "-1")
                        Config.CropFairyMaxBoxes = -1;
                    else if (int.TryParse(value, out int result) && result >= 1)
                        Config.CropFairyMaxBoxes = result;
                    else
                        Config.CropFairyMaxBoxes = Math.Max(1, Config.CropFairyMaxBoxes); // Keep current valid value
                }
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Weight Per Box",
                tooltip: () => "[Per-Hut] Weight multiplier per Fairy Box when selecting crops within that hut's range (0.0 = no effect, 1.0 = normal, 5.0+ = heavy influence)",
                getValue: () => (float)Config.CropFairyWeightPerBox,
                setValue: value => Config.CropFairyWeightPerBox = value,
                min: 0.0f,
                max: 10.0f,
                interval: 0.1f,
                formatValue: value => $"{value:F1}x"
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Max Boxes for Crop Weighting",
                tooltip: () => "[Per-Hut] Maximum Fairy Boxes per individual hut that contribute to crop selection weighting within that hut's range (1-36, Junimo Hut capacity)",
                getValue: () => Config.CropFairyMaxWeightBoxes,
                setValue: value => Config.CropFairyMaxWeightBoxes = value,
                min: 1,
                max: 36
            );
        }

        // Debug command to give the player a Fairy Box trinket
        private void OnGiveFairyBoxCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("You must be in-game to use this command.", LogLevel.Error);
                return;
            }
            int amount = 1;
            if (args.Length > 0 && !int.TryParse(args[0], out amount))
            {
                Monitor.Log("Usage: fcr_give_fairybox [amount]", LogLevel.Info);
                return;
            }
            if (amount < 1) amount = 1;
            for (int i = 0; i < amount; i++)
            {
                var fairyBox = ItemRegistry.Create("(TR)FairyBox");
                Game1.player.addItemByMenuIfNecessary(fairyBox);
            }
            Monitor.Log($"Gave you {amount} Fairy Box trinket(s).", LogLevel.Info);
        }

        private void OnTestFairyChanceCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("You must be in-game to use this command.", LogLevel.Error);
                return;
            }

            Monitor.Log("=== Testing Enhanced Fairy Chance ===", LogLevel.Info);
            
            // Calculate fairy box count
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
            
            int maxBoxes = Config?.CropFairyMaxBoxes ?? 8;
            double chancePerBox = (Config?.CropFairyChancePerBox ?? 0.1) / 100.0;
            int capped = maxBoxes == -1 ? fairyBoxCount : Math.Min(fairyBoxCount, maxBoxes);
            double bonus = capped * chancePerBox;
            
            double baseChance = 0.01;
            double fairyRoseBonus = Game1.getFarm().hasMatureFairyRoseTonight ? 0.007 : 0.0;
            double totalChance = baseChance + fairyRoseBonus + bonus;
            
            Monitor.Log($"Fairy Boxes found: {fairyBoxCount} (capped at {capped})", LogLevel.Info);
            Monitor.Log($"Base chance: {baseChance * 100}%", LogLevel.Info);
            Monitor.Log($"Fairy Rose bonus: {fairyRoseBonus * 100}%", LogLevel.Info);
            Monitor.Log($"Mod bonus: {bonus * 100:F1}%", LogLevel.Info);
            Monitor.Log($"TOTAL CHANCE: {totalChance * 100:F1}%", LogLevel.Info);
            Monitor.Log($"Enhanced Crop Fairy: {(Config?.EnhancedCropFairy ?? false ? "ENABLED" : "DISABLED")}", LogLevel.Info);
        }

        // Debug command to force the Fairy event for the next night
        private void OnForceFairyEventCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("You must be in-game to use this command.", LogLevel.Error);
                return;
            }
            Game1.weatherForTomorrow = Game1.weather_sunny;
            Game1.farmEventOverride = new StardewValley.Events.FairyEvent();
            // (No debug log)
        }
        // Restore OnSaveLoaded for config/state setup
        // ...existing code...

        // Log the real, final event chance and Fairy Box count every night, and log crop weights
        private void OnDayEnding_LogFairyChance(object? sender, StardewModdingAPI.Events.DayEndingEventArgs e)
        {
            if (!Config.EnhancedCropFairy)
                return;
            try
            {
                int fairyBoxCount = 0;
                var farm = Game1.getFarm();
                foreach (var hut in farm.buildings.OfType<StardewValley.Buildings.JunimoHut>())
                {
                    var chest = hut.GetOutputChest();
                    if (chest?.Items == null) continue;
                    fairyBoxCount += chest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox");
                }
                int maxBoxes = Config.CropFairyMaxBoxes;
                double chancePerBox = Config.CropFairyChancePerBox / 100.0; // Convert percentage to decimal
                int capped = maxBoxes == -1 ? fairyBoxCount : Math.Min(fairyBoxCount, maxBoxes);
                double bonus = capped * chancePerBox;
                // Only summary log every night
                string msg = $"[EnhancedCropFairy] (Nightly Log) +{bonus * 100:F1}% event chance for {capped} Fairy Boxes (raw count: {fairyBoxCount})";
                this.Monitor.Log(msg, StardewModdingAPI.LogLevel.Info);
                // Warning if no Fairy Boxes found
                if (fairyBoxCount == 0)
                    this.Monitor.Log("[EnhancedCropFairy] WARNING: No Fairy Boxes found in any Junimo Hut output chests!", StardewModdingAPI.LogLevel.Warn);
                // (No per-hut/crop logs here)
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"[EnhancedCropFairy] Exception in OnDayEnding_LogFairyChance: {ex}", LogLevel.Error);
            }
        }

        // Helper to log candidate crops and weights for all huts
        private void LogCropWeightsForAllHuts()
        {
            var farm = Game1.getFarm();
            var huts = farm.buildings.Where(b => b.buildingType.Value == "Junimo Hut").ToList();
            var hutBoxCounts = new Dictionary<Vector2, int>();
            foreach (var hut in huts)
            {
                Vector2 hutPos = new Vector2(hut.tileX.Value, hut.tileY.Value);
                var hutObj = hut as JunimoHut;
                var chest = hutObj?.GetOutputChest();
                int boxCount = 0;
                if (chest?.Items != null)
                    boxCount = Math.Min(8, chest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox"));
                hutBoxCounts[hutPos] = boxCount;
            }
            foreach (var hut in huts)
            {
                Vector2 hutPos = new Vector2(hut.tileX.Value, hut.tileY.Value);
                var crops = new List<string>();
                var weights = new List<double>();
                int hutRange = 8; // Always match vanilla Junimo Hut range
                double weightPerBox = Config.CropFairyWeightPerBox;
                int maxWeightBoxes = Config.CropFairyMaxWeightBoxes;
                
                for (int dx = -hutRange; dx <= hutRange; dx++)
                {
                    for (int dy = -hutRange; dy <= hutRange; dy++)
                    {
                        Vector2 tile = new Vector2(hut.tileX.Value + dx, hut.tileY.Value + dy);
                        if (farm.terrainFeatures.TryGetValue(tile, out var tf) && tf is StardewValley.TerrainFeatures.HoeDirt dirt && dirt.crop != null)
                        {
                            string cropName = $"Crop #{dirt.crop.indexOfHarvest.Value} at ({tile.X},{tile.Y})";
                            double baseWeight = 1.0;
                            int boxCount = hutBoxCounts.TryGetValue(hutPos, out int count) ? count : 0;
                            int cappedBoxes = Math.Min(boxCount, maxWeightBoxes);
                            double weight = baseWeight + (cappedBoxes * weightPerBox);
                            crops.Add(cropName);
                            weights.Add(weight);
                        }
                    }
                }
                if (crops.Count == 0)
                {
                    this.Monitor.Log($"[EnhancedCropFairy] Hut at ({hutPos.X},{hutPos.Y}): No crops found in hut range.", LogLevel.Info);
                }
                else
                {
                    this.Monitor.Log($"[EnhancedCropFairy] Hut at ({hutPos.X},{hutPos.Y}):", LogLevel.Info);
                    for (int i = 0; i < crops.Count; i++)
                    {
                        this.Monitor.Log($"    Crop: {crops[i]}, Weight: {weights[i]:F2}", LogLevel.Info);
                    }
                }
            }
        }

        // Debug command handler for fcr_test_fairy_patch

        // Command handler to give Junimo Hut ingredients
        private void OnGiveJunimoHutIngredientsCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("You must be in-game to use this command.", LogLevel.Error);
                return;
            }
            // 400 Stone (390), 18 Starfruit (268), 200 Fiber (771)
            var player = Game1.player;
            player.addItemByMenuIfNecessary(ItemRegistry.Create("(O)390", 400));
            player.addItemByMenuIfNecessary(ItemRegistry.Create("(O)268", 18));
            player.addItemByMenuIfNecessary(ItemRegistry.Create("(O)771", 200));
            Monitor.Log("Gave you 400 Stone, 18 Starfruit, and 200 Fiber for two Junimo Huts!", LogLevel.Info);
        }
        private void OnTestFairyPatchCommand(string command, string[] args)
        {
            Monitor.Log("fcr_test_fairy_patch command triggered", LogLevel.Info);
            if (!Config.EnhancedCropFairy)
            {
                Monitor.Log("[EnhancedCropFairy] Feature is disabled in config.", LogLevel.Info);
                return;
            }
            var farm = Game1.getFarm();
            var huts = farm.buildings.Where(b => b.buildingType.Value == "Junimo Hut").ToList();
            var hutBoxCounts = new Dictionary<Vector2, int>();
            // Count Fairy Boxes in each hut's output chest
            foreach (var hut in huts)
            {
                Vector2 hutPos = new Vector2(hut.tileX.Value, hut.tileY.Value);
                var hutObj = hut as JunimoHut;
                var chest = hutObj?.GetOutputChest();
                int boxCount = 0;
                if (chest?.Items != null)
                    boxCount = Math.Min(8, chest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox"));
                hutBoxCounts[hutPos] = boxCount;
            }
            // Log hutBoxCounts for debugging - removed VerboseLogging check
            this.Monitor.Log("[Enhanced Crop Fairy Debug] Fairy Box counts per hut:", LogLevel.Debug);
            foreach (var kvp in hutBoxCounts)
                this.Monitor.Log($"  Hut at ({kvp.Key.X},{kvp.Key.Y}): {kvp.Value} Fairy Boxes", LogLevel.Debug);
            this.Monitor.Log($"[Enhanced Crop Fairy Debug]", LogLevel.Info);
            this.Monitor.Log($"Found {huts.Count} Junimo Huts on the farm.", LogLevel.Info);
            foreach (var hut in huts)
            {
                Vector2 hutPos = new Vector2(hut.tileX.Value, hut.tileY.Value);
                var crops = new List<string>();
                var weights = new List<double>();
                int hutRange = 8; // Always match vanilla Junimo Hut range
                double weightPerBox = Config.CropFairyWeightPerBox;
                int maxWeightBoxes = Config.CropFairyMaxWeightBoxes;
                
                for (int dx = -hutRange; dx <= hutRange; dx++)
                {
                    for (int dy = -hutRange; dy <= hutRange; dy++)
                    {
                        Vector2 tile = new Vector2(hut.tileX.Value + dx, hut.tileY.Value + dy);
                        if (farm.terrainFeatures.TryGetValue(tile, out var tf) && tf is StardewValley.TerrainFeatures.HoeDirt dirt && dirt.crop != null)
                        {
                            string cropName = $"Crop #{{dirt.crop.indexOfHarvest.Value}} at ({{tile.X}},{{tile.Y}})";
                            double baseWeight = 1.0;
                            int boxCount = hutBoxCounts.TryGetValue(hutPos, out int count) ? count : 0;
                            int cappedBoxes = Math.Min(boxCount, maxWeightBoxes);
                            double weight = baseWeight + (cappedBoxes * weightPerBox);
                            crops.Add(cropName);
                            weights.Add(weight);
                        }
                    }
                }
                if (crops.Count == 0)
                {
                    this.Monitor.Log($"  Hut at ({{hutPos.X}},{{hutPos.Y}}): No crops found in hut range.", LogLevel.Info);
                }
                else
                {
                    this.Monitor.Log($"  Hut at ({{hutPos.X}},{{hutPos.Y}}):", LogLevel.Info);
                    for (int i = 0; i < crops.Count; i++)
                    {
                        this.Monitor.Log($"    Crop: {{crops[i]}}, Weight: {{weights[i]:F2}}", LogLevel.Info);
                    }
                    double totalWeight = weights.Sum();
                    double roll = Game1.random.NextDouble() * totalWeight;
                    double accum = 0;
                    int chosen = 0;
                    for (int i = 0; i < weights.Count; i++)
                    {
                        accum += weights[i];
                        if (roll < accum)
                        {
                            chosen = i;
                            break;
                        }
                    }
                    this.Monitor.Log($"    Simulated selection: {{crops[chosen]}}", LogLevel.Info);
                }
            }
        }


        // Harmony patch: Draw fairies at the correct world layer for proper occlusion
        [HarmonyLib.HarmonyPatch(typeof(StardewValley.Farm), nameof(StardewValley.Farm.draw))]
        public static class FarmDrawPatch
        {
            public static void Postfix(StardewValley.Farm __instance, SpriteBatch b)
            {
                if (Game1.currentLocation != __instance)
                    return;
                foreach (var companions in hutCompanions.Values)
                {
                    foreach (var companion in companions)
                    {
                        companion.Draw(b);
                    }
                }
            }
        }

        // --- Fairy state persistence for warps ---
        // Persistent fairy state storage key
        private const string FairyStateKey = "mod.fcr.fairies";

        public void SaveFairyState()
        {
            var farm = Game1.getLocationFromName("Farm") as Farm;
            if (farm == null) return;
            var hutData = new List<(Vector2 hutPos, List<(int styleIndex, Vector2 position, string fairyBoxGUID)>)>();
            foreach (var kvp in hutCompanions)
            {
                var hutPos = kvp.Key;
                var fairies = kvp.Value;
                var fairyData = fairies.Select(tc => (tc.StyleIndex, tc.Position, tc.FairyBoxGUID)).ToList();
                hutData.Add((hutPos, fairyData));
            }
            // Serialize and store in modData
            farm.modData[FairyStateKey] = Newtonsoft.Json.JsonConvert.SerializeObject(hutData);
            Logger?.Log($"[FairyPersistence] Saved fairy state for {hutData.Count} huts to modData.", LogLevel.Trace);
        }

        public void RestoreFairyState()
        {
            var farm = Game1.getLocationFromName("Farm") as Farm;
            if (farm == null) return;
            if (!farm.modData.TryGetValue(FairyStateKey, out string json) || string.IsNullOrEmpty(json)) return;
            
            try
            {
                // Try to deserialize with FairyBoxGUID (new format)
                var hutDataWithGUID = Newtonsoft.Json.JsonConvert.DeserializeObject<List<(Vector2 hutPos, List<(int styleIndex, Vector2 position, string fairyBoxGUID)>)>>(json);
                if (hutDataWithGUID != null)
                {
                    Logger?.Log($"[FairyPersistence] Restoring fairy state for {hutDataWithGUID.Count} huts from modData (with GUID).", LogLevel.Trace);
                    ClearAllFairies();
                    foreach (var (hutPos, fairyList) in hutDataWithGUID)
                    {
                        var hut = farm.buildings.OfType<JunimoHut>().FirstOrDefault(h => h.tileX.Value == (int)hutPos.X && h.tileY.Value == (int)hutPos.Y);
                        if (hut == null) continue;
                        var companions = new List<Models.TrinketCompanion>();
                        foreach (var (styleIndex, pos, fairyBoxGUID) in fairyList)
                        {
                            var companion = new Models.TrinketCompanion(pos, styleIndex, "Fairy");
                            companion.FairyBoxGUID = fairyBoxGUID;
                            companions.Add(companion);
                        }
                        hutCompanions[hutPos] = companions;
                    }
                    return;
                }
            }
            catch
            {
                // Fall through to old format
            }
            
            try
            {
                // Fallback to old format without FairyBoxGUID for backward compatibility
                var hutDataOld = Newtonsoft.Json.JsonConvert.DeserializeObject<List<(Vector2 hutPos, List<(int styleIndex, Vector2 position)>)>>(json);
                if (hutDataOld == null) return;
                Logger?.Log($"[FairyPersistence] Restoring fairy state for {hutDataOld.Count} huts from modData (legacy format, will regenerate).", LogLevel.Trace);
                ClearAllFairies();
                foreach (var (hutPos, fairyList) in hutDataOld)
                {
                    var hut = farm.buildings.OfType<JunimoHut>().FirstOrDefault(h => h.tileX.Value == (int)hutPos.X && h.tileY.Value == (int)hutPos.Y);
                    if (hut == null) continue;
                    var companions = new List<Models.TrinketCompanion>();
                    foreach (var (styleIndex, pos) in fairyList)
                    {
                        var companion = new Models.TrinketCompanion(pos, styleIndex, "Fairy");
                        // FairyBoxGUID will be empty, so ScanAndSpawnFairies will regenerate these
                        companions.Add(companion);
                    }
                    hutCompanions[hutPos] = companions;
                }
            }
            catch (Exception ex)
            {
                Logger?.Log($"[FairyPersistence] Failed to restore fairy state: {ex.Message}", LogLevel.Warn);
            }
        }


        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.OldMenu is StardewValley.Menus.ItemGrabMenu igm)
            {
                // Check if the closed menu was for a hut output chest
                bool foundHutChest = false;
                foreach (var farm in Game1.locations.OfType<Farm>())
                {
                    foreach (var hut in farm.buildings.OfType<JunimoHut>())
                    {
                        var outputChest = hut.GetOutputChest();
                        if (outputChest != null && igm.ItemsToGrabMenu?.actualInventory == outputChest.Items)
                        {
                            foundHutChest = true;
                        }
                    }
                }
                if (foundHutChest)
                {
                    // After closing the hut chest, check all huts for >8 Fairy Boxes and show HUD message if needed
                    foreach (var farm in Game1.locations.OfType<Farm>())
                    {
                        foreach (var hut in farm.buildings.OfType<JunimoHut>())
                        {
                            var chest = hut.GetOutputChest();
                            if (chest?.Items == null) continue;
                            int fairyBoxCount = chest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox");
                            int spawnCap = Math.Min(Config.FairySpawnCap, 36);
                            if (fairyBoxCount > spawnCap)
                            {
                                Game1.addHUDMessage(new HUDMessage($"Junimo Hut fairy limit: {spawnCap} max (found {fairyBoxCount})", HUDMessage.error_type));
                            }
                        }
                    }
                    ScanAndSpawnFairies();
                }
            }
        }

        private void OnPlayerWarped(object? sender, WarpedEventArgs e)
        {
            if (e.NewLocation is Farm)
            {
                // Set a flag to respawn fairies after a short delay (timing workaround)
                pendingFarmRespawnTicks = 3; // 3 ticks (~0.05s) is usually enough
            }
        }
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // (Obsolete critter fallback logic removed)
            // Handle delayed respawn after warping to farm
            if (pendingFarmRespawnTicks > 0)
            {
                pendingFarmRespawnTicks--;
                if (pendingFarmRespawnTicks == 0 && Game1.currentLocation is Farm)
                {
                    Monitor.Log("[FairyPersistence] Delayed fairy sync after warp to farm.", LogLevel.Trace);
                    ScanAndSpawnFairies(); // Use ScanAndSpawnFairies instead of RestoreFairyState for better persistence
                }
            }
            // Update companion movement every tick
            if (Game1.currentLocation is Farm)
            {
                // dt = seconds per tick (1/60)
                float dt = 1f / 60f;
                foreach (var companions in hutCompanions.Values)
                {
                    foreach (var companion in companions)
                    {
                        companion.Update(dt);
                    }
                }
            }
            // (Obsolete critter fallback logic fully removed)
        }

        private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
        {
            if (e.Location is Farm)
            {
                ScanAndSpawnFairies();
                return;
            }
            foreach (var farm in Game1.locations.OfType<Farm>())
            {
                foreach (var hut in farm.buildings.OfType<JunimoHut>())
                {
                    var outputChest = hut.GetOutputChest();
                    if (outputChest != null && ReferenceEquals(e.Location, outputChest))
                    {
                        // Check Fairy Box count and show HUD message if needed
                        var fairyBoxCount = outputChest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox");
                        int spawnCap = Math.Min(Config.FairySpawnCap, 36);
                        if (fairyBoxCount > spawnCap)
                        {
                            Monitor.Log($"[FairyDebug] fairyBoxCount at HUD check (OnObjectListChanged): {fairyBoxCount}", LogLevel.Debug);
                            Monitor.Log($"⚠️ Hut at ({hut.tileX.Value}, {hut.tileY.Value}) has {fairyBoxCount} Fairy Boxes, limiting to {spawnCap} fairies for balance", LogLevel.Warn);
                            pendingHudMessage = $"Junimo Hut fairy limit: {spawnCap} max (found {fairyBoxCount})";
                        }
                        ScanAndSpawnFairies();
                        return;
                    }
                }
            }
            if (e.Location is GameLocation loc)
            {
                foreach (var obj in loc.Objects.Values)
                {
                    if (obj is StardewValley.Objects.Chest chest)
                    {
                        foreach (var farm in Game1.locations.OfType<Farm>())
                        {
                            foreach (var hut in farm.buildings.OfType<JunimoHut>())
                            {
                                var outputChest = hut.GetOutputChest();
                                if (outputChest != null && ReferenceEquals(outputChest, chest))
                                {
                                    ScanAndSpawnFairies();
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            Monitor.Log($"[Config] Using global config: FairyStyleID={Config.FairyStyleID}, EnhancedCropFairy={Config.EnhancedCropFairy}, FairySpawnCap={Config.FairySpawnCap}", LogLevel.Info);
            Monitor.Log($"[Config] Crop Fairy Settings: ChancePerBox={Config.CropFairyChancePerBox}%, MaxBoxes={Config.CropFairyMaxBoxes}, WeightPerBox={Config.CropFairyWeightPerBox}, MaxWeightBoxes={Config.CropFairyMaxWeightBoxes}", LogLevel.Info);

            // Set flag to save state after first scan on load
            shouldSaveAfterFirstScan = true;
            
            // Restore fairy state if it exists
            RestoreFairyState();
            // Scan and spawn fairies based on current state
            ScanAndSpawnFairies();
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            // Save fairy state when the game saves (when sleeping)
            SaveFairyState();
            Monitor.Log("[FairyPersistence] Saved fairy state during game save.", LogLevel.Trace);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e) => ScanAndSpawnFairies();

        private void OnScanCommand(string command, string[] args)
        {
            lastScanDay = -1;
            ScanAndSpawnFairies();
            Monitor.Log("Rescanned Junimo Huts.", LogLevel.Info);
        }

        private void OnClearCommand(string command, string[] args)
        {
            ClearAllFairies();
            Monitor.Log("Cleared all roaming fairies.", LogLevel.Info);
        }

        private void OnStatusCommand(string command, string[] args)
        {
            if (hutCompanions.Count == 0)
            {
                Monitor.Log("No roaming fairies currently active.", LogLevel.Info);
                return;
            }
            Monitor.Log($"Status of {hutCompanions.Values.Sum(list => list.Count)} active fairies:", LogLevel.Info);
            foreach (var (hutPos, fairies) in hutCompanions)
            {
                Monitor.Log($"  Hut at ({hutPos.X}, {hutPos.Y}): {fairies.Count} fairies", LogLevel.Info);
                for (int i = 0; i < fairies.Count; i++)
                {
                    if (fairies[i] is Models.TrinketCompanion fairy)
                    {
                        var hutWorldPos = hutPos * 64f + new Vector2(32f, 32f);
                        var distance = (fairy.Position - hutWorldPos).Length();
                        Monitor.Log($"    Fairy {i + 1}: distance {distance:F1} pixels from hut", LogLevel.Info);
                    }
                }
            }
        }

        private void OnPrintFairyBoxModDataCommand(string command, string[] args)
        {
            // Removed unused variable 'found'
            long saveUniqueId = Game1.player.UniqueMultiplayerID;
            // UniqueMultiplayerID is no longer used for style assignment.
            Monitor.Log($"[DEBUG] --- Hut Fairy Style Calculation ---", LogLevel.Info);
            for (int i = 0; i < Game1.player.Items.Count; i++)
            {
                // ...existing code...
            }
        }
        private void OnPrintTrinketStyleCommand(string command, string[] args)
        {
        }

        private void OnSetFairyBoxIdCommand(string command, string[] args)
        {
            if (args.Length < 2)
            {
                Monitor.Log("Usage: fcr_set_fairybox_id <slot> <id>", LogLevel.Info);
                return;
            }
            if (!int.TryParse(args[0], out int slot) || slot < 0 || slot >= Game1.player.Items.Count)
            {
                Monitor.Log($"Invalid slot: {args[0]}", LogLevel.Warn);
                return;
            }
            var item = Game1.player.Items[slot];
            if (item?.QualifiedItemId != "(TR)FairyBox" || !(item is StardewValley.Objects.Trinkets.Trinket trinket))
            {
                Monitor.Log($"Slot {slot} does not contain a Fairy Box.", LogLevel.Warn);
                return;
            }
            trinket.modData["UniqueMultiplayerID"] = args[1];
            Monitor.Log($"Set modData['UniqueMultiplayerID'] = {args[1]} on Fairy Box in slot {slot}.", LogLevel.Info);
        }

        internal void ScanAndSpawnFairies()
        {
            if (!Context.IsWorldReady) return;
            var config = Config;
            if (config == null) return;
            lastScanDay = Game1.dayOfMonth;
            int totalHuts = 0;
            int hutsWithFairies = 0;
            foreach (var farm in Game1.locations.OfType<Farm>())
            {
                foreach (var hut in farm.buildings.OfType<JunimoHut>())
                {
                    totalHuts++;
                    var hutPos = new Vector2(hut.tileX.Value, hut.tileY.Value);
                    var chest = hut.GetOutputChest();
                    if (chest?.Items == null) continue;
                    // Build a dictionary of FairyBoxGUID -> trinket for all Fairy Boxes in the hut
                    int spawnCap = Math.Min(config.FairySpawnCap, 36); // Cap at 36 (Junimo Hut capacity)
                    var fairyBoxTrinkets = chest.Items.Where(item => item?.QualifiedItemId == "(TR)FairyBox").OfType<StardewValley.Objects.Trinkets.Trinket>().Take(spawnCap).ToList();
                    var boxDict = new Dictionary<string, StardewValley.Objects.Trinkets.Trinket>();
                    foreach (var trinket in fairyBoxTrinkets)
                    {
                        if (!trinket.modData.TryGetValue("FairyBoxGUID", out string guid) || string.IsNullOrWhiteSpace(guid))
                        {
                            guid = Guid.NewGuid().ToString();
                            trinket.modData["FairyBoxGUID"] = guid;
                        }
                        boxDict[guid] = trinket;
                    }
                    // Get or create the hut's fairy dictionary (FairyBoxGUID -> fairy)
                    if (!hutCompanions.TryGetValue(hutPos, out var fairyDictObj) || fairyDictObj == null)
                        hutCompanions[hutPos] = new List<Models.TrinketCompanion>();
                    var fairyDict = hutCompanions[hutPos].OfType<Models.TrinketCompanion>().ToDictionary(f => f.FairyBoxGUID, f => f);
                    // Add new fairies for new Fairy Boxes
                    foreach (var kvp in boxDict)
                    {
                        if (!fairyDict.ContainsKey(kvp.Key))
                        {
                    // Removed VerboseLogging check - always log debug info
                    Logger?.Log($"[FairyDebug] FairyBox modData for GUID={kvp.Key}: {Newtonsoft.Json.JsonConvert.SerializeObject(kvp.Value.modData)}", LogLevel.Debug);
                            var trinket = kvp.Value;
                            int styleIndex = GetStyleIndex();
                            // Debug log for diagnosis - removed VerboseLogging check
                            Logger?.Log($"[FairyDebug] Spawning fairy: Config.FairyStyleID={config.FairyStyleID}, styleIndex={styleIndex}, FairyBoxGUID={kvp.Key}", LogLevel.Debug);
                            // Spawn at hut center
                            Vector2 spawnPos = hutPos;
                            var companion = new Models.TrinketCompanion(spawnPos, styleIndex, "Fairy");
                            companion.FairyBoxGUID = kvp.Key;
                            hutCompanions[hutPos].Add(companion);
                        }
                    }
                    // Remove fairies whose FairyBoxGUID is no longer present
                    hutCompanions[hutPos] = hutCompanions[hutPos].OfType<Models.TrinketCompanion>().Where(f => boxDict.ContainsKey(f.FairyBoxGUID)).ToList();
                    if (hutCompanions[hutPos].Count > 0) hutsWithFairies++;
                }
            }
            Monitor.Log($"✓ Fairy scan complete: {hutsWithFairies}/{totalHuts} huts have fairies active", LogLevel.Info);
            
            // Save fairy state only on first scan after load, or if explicitly requested
            if (shouldSaveAfterFirstScan)
            {
                SaveFairyState();
                shouldSaveAfterFirstScan = false;
                Monitor.Log("[FairyPersistence] Saved initial fairy state after first scan on load.", LogLevel.Trace);
            }
            // (Removed verbose crop weight logging)
        }

        private void SpawnFairiesForHut(JunimoHut hut, GameLocation location)
        {
            var chest = hut.GetOutputChest();
            if (chest?.Items == null)
            {
                Monitor.Log($"No chest found for hut at ({hut.tileX.Value}, {hut.tileY.Value})", LogLevel.Debug);
                return;
            }
            var hutPos = new Vector2(hut.tileX.Value, hut.tileY.Value);
            Monitor.Log($"Scanning hut at ({hutPos.X}, {hutPos.Y}): chest has {chest.Items.Count} items", LogLevel.Debug);
            int spawnCap = Math.Min(Config.FairySpawnCap, 36);
            var fairyBoxTrinkets = chest.Items.Where(item => item?.QualifiedItemId == "(TR)FairyBox").Take(spawnCap).ToList();
            int fairyBoxCount = fairyBoxTrinkets.Count;
            if (fairyBoxCount == 0)
            {
                Monitor.Log($"No Fairy Boxes found in hut at ({hutPos.X}, {hutPos.Y}) - searched for '(TR)FairyBox'", LogLevel.Debug);
                return;
            }
            if (chest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox") > spawnCap)
            {
                Monitor.Log($"[FairyDebug] fairyBoxCount at HUD check: {chest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox")}", LogLevel.Debug);
                Monitor.Log($"⚠️ Hut at ({hutPos.X}, {hutPos.Y}) has more than {spawnCap} Fairy Boxes, limiting to {spawnCap} fairies for balance", LogLevel.Warn);
                Game1.addHUDMessage(new HUDMessage($"Junimo Hut fairy limit: {spawnCap} max (found {chest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox")})", HUDMessage.error_type));
            }
            var fairies = new List<Models.TrinketCompanion>();
            int spawnCount = fairyBoxCount;
            Monitor.Log($"Found {chest.Items.Count(item => item?.QualifiedItemId == "(TR)FairyBox") } Fairy Boxes in hut at ({hutPos.X}, {hutPos.Y}), spawning {spawnCount} fairies", LogLevel.Info);
            float radius = 32f;
            float angleStep = 360f / spawnCount;
            for (int i = 0; i < spawnCount; i++)
            {
                var trinket = fairyBoxTrinkets[i] as StardewValley.Objects.Trinkets.Trinket;
                if (trinket != null)
                {
                    int styleIndex = GetStyleIndex();
                    Monitor.Log($"  [DEBUG] Fairy Box Trinket {i + 1}: ItemId={trinket.ItemId}, generationSeed={trinket.generationSeed.Value}, DisplayName={trinket.DisplayName}", LogLevel.Info);
                    foreach (var kvp in trinket.modData.Pairs)
                        Monitor.Log($"    modData['{kvp.Key}'] = {kvp.Value}", LogLevel.Info);
                    int finalStyleIndex = styleIndex;
                    Monitor.Log($"    [DEBUG] Fairy {i + 1}: styleIndex={finalStyleIndex} [session cached]", LogLevel.Debug);
                    float angle = MathHelper.ToRadians(i * angleStep);
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                    Vector2 spawnPos = hutPos + offset / 64f;
                    var companion = new Models.TrinketCompanion(spawnPos, finalStyleIndex, "Fairy");
                    fairies.Add(companion);
                    Monitor.Log($"  Spawned fairy {i + 1} at hut ({spawnPos.X}, {spawnPos.Y})", LogLevel.Info);
                }
            }
            hutCompanions[hutPos] = fairies;
        }
    }
}
