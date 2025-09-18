Junimo Hut Fairies Mod
======================

Junimo Hut Fairies Mod adds magical fairy companions to your Stardew Valley farm. Fairies roam around Junimo Huts, bringing life and atmosphere to your fields. The mod also enhances the Crop Fairy event, letting you influence both the chance and the target of the event through in-game actions. All features are globally configurable and work across all save files.

Features
--------
- Fairy Companions: Store the Fairy Box trinket in a Junimo Hut to summon a fairy. Up to 8 fairies can be spawned per hut (configurable up to 36).
- Enhanced Crop Fairy Event:
    * Global Boost: Each Fairy Box in a Junimo Hut adds a configurable percentage to the nightly Crop Fairy event chance (default: +0.1% per box, max 8 boxes = +0.8% total). Use -1 for unlimited boxes.
    * Local Boost: When the Crop Fairy event triggers, each crop within 8 tiles of a Junimo Hut gets weight bonus per Fairy Box in that hut (default: +1 weight per box, up to 36 boxes max).
- Simple Fairy Styles: Choose from 8 different fairy appearance styles (1-8). Each has a unique look and color.
- Global Configuration: All settings apply to every save file and can be changed via the in-game config menu (requires Generic Mod Config Menu).
- Harmony Integration: Uses Harmony for compatibility with other mods.

Configuration
-------------
**Via Generic Mod Config Menu (GMCM) - Recommended**
- Install Generic Mod Config Menu from NexusMods
- Access settings from the in-game options menu or title screen
- Easy-to-use interface with sliders, dropdowns, checkboxes, and tooltips
- Clear section organization: Fairy Companion Settings + Enhanced Crop Fairy Settings

**Config Options:**

**Fairy Companion Settings:**
- Fairy Style: Choose appearance style (1-8). Each style has a unique look and color.
- Fairy Spawn Cap: Maximum fairy companions per Junimo Hut (1-36, default: 8)

**Enhanced Crop Fairy Settings:**
- Enhanced Crop Fairy: Enable/disable the enhanced Crop Fairy features (checkbox, default: enabled)

[Event Chance Settings:]
- Chance Bonus Per Box: Percentage bonus per Fairy Box for Crop Fairy event chance (0.025-1.0%, 0.025% steps, default: 0.125%)
- Max Boxes for Global Bonus: Maximum total Fairy Boxes counting toward global chance bonus (text input, minimum: 1, or type "unlimited"/"−1" for no limit, default: 8)

[Per-Hut Crop Weighting Settings:]
- Weight Per Box: Weight multiplier per Fairy Box for crops near that hut (0.0-10.0x, 0.1 steps, default: 1.0x)
- Max Boxes for Crop Weighting: Maximum Fairy Boxes per hut for crop weighting (1-36, default: 36)

**Manual config.json editing:**
Open config.json in your mod folder with a text editor. Example:

{
  "FairyStyleID": 1,
  "EnhancedCropFairy": true,
  "CropFairyChancePerBox": 0.125,
  "CropFairyMaxBoxes": 8,
  "CropFairyWeightPerBox": 1.0,
  "CropFairyMaxWeightBoxes": 36,
  "FairySpawnCap": 8
}

FairyStyleID options:
- 1-8: Choose from 8 different fairy styles (each with unique look and color)

CropFairyMaxBoxes options:
- Positive number: Cap at that many boxes
- -1: Unlimited boxes count toward global bonus

SMAPI Debug Commands
--------------------
- fcr_scan           Scan Junimo Huts for Fairy Boxes and update fairies.
- fcr_clear          Remove all roaming fairies from the farm.
- fcr_status         Show the status of all active fairies.
- fcr_print_fairybox_moddata  Print all Fairy Box modData in your inventory.
- fcr_force_fairy    Force the Crop Fairy event to occur the next night.
- fcr_give_fairybox [amount]  Give yourself Fairy Box trinkets (default 1, specify amount for more).

Installation
------------
1. Install SMAPI (https://smapi.io/).
2. Download and extract the Junimo Hut Fairies Mod into your Mods folder.
3. Launch Stardew Valley with SMAPI.
4. Optional: Install Generic Mod Config Menu for easy configuration.
5. Load any save to generate the config file.
6. Configure settings via GMCM (in-game) or edit config.json manually.
7. Enjoy your fairy companions!

Compatibility
-------------
- Compatible with Stardew Valley 1.6 and later.
- Works with Elle's Cuter Companions mod.
- Should work with most other mods. If you encounter issues, please report them.
- Global configuration system means settings work across all save files.

Troubleshooting
---------------
- If fairies do not appear, check your config file for typos or use GMCM to reset to defaults.
- If configuration changes don't apply, restart the game after editing config.json manually.
- For unlimited global bonus, use "unlimited" or "-1" in the Max Boxes for Global Bonus field.

Credits
-------
- Developed by Abelard the Bard.
- Uses Harmony, Newtonsoft.Json, and Generic Mod Config Menu integration.

License
-------
This mod is open source. Feel free to modify and share, but please credit the original author.
