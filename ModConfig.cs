namespace FarmCompanionRoamerMod
{
    public class ModConfig
    {
        public object FairyStyleID { get; set; } = "Shuffled"; // "Shuffled" (default), "sequential", "random", or 1-8 (specific styles)

        /// <summary>
        /// If true, enables the Enhanced Crop Fairy feature: Fairy Boxes in Junimo Huts increase the Crop Fairy event chance (global cap +0.8%) and weight crop selection.
        /// </summary>
        public bool EnhancedCropFairy { get; set; } = true;

        /// <summary>
        /// The chance bonus per Fairy Box for the Crop Fairy event (as a percentage). Default: 0.125 (0.125% per box).
        /// </summary>
        public double CropFairyChancePerBox { get; set; } = 0.125;

        /// <summary>
        /// Maximum number of Fairy Boxes that count toward the global Crop Fairy event chance bonus. Default: 8. Use -1 for unlimited.
        /// </summary>
        public int CropFairyMaxBoxes { get; set; } = 8;

        /// <summary>
        /// Weight bonus per box for crops within hut range during Crop Fairy's crop selection. Default: 1.0 (1 weight per box).
        /// </summary>
        public double CropFairyWeightPerBox { get; set; } = 1.0;

        /// <summary>
        /// Maximum number of Fairy Boxes per hut that contribute to crop selection weighting. Default: 36 (Junimo Hut max capacity).
        /// </summary>
        public int CropFairyMaxWeightBoxes { get; set; } = 36;

        /// <summary>
        /// Maximum number of fairy companions that can spawn per Junimo Hut. Default: 8, Maximum: 36 (Junimo Hut capacity).
        /// </summary>
        public int FairySpawnCap { get; set; } = 8;
    }
}
