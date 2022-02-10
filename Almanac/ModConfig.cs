using StardewModdingAPI.Utilities;

namespace Leclair.Stardew.Almanac {
	public class ModConfig {

		// General
		public bool AlmanacAlwaysAvailable { get; set; } = false;


		// Bindings
		public KeybindList UseKey { get; set; } = KeybindList.Parse("F7, ControllerStart");


		// Crop Page
		public bool ShowCrops { get; set; } = true;

		public bool ShowPreviews { get; set; } = true;
		public bool PreviewPlantOnFirst { get; set; } = false;
		public bool PreviewUseHarvestSprite { get; set; } = true;


		// Weather Page
		public bool ShowWeather { get; set; } = true;
		public bool EnableDeterministicWeather { get; set; } = true;


		// Horoscope Page
		public bool ShowHoroscopes { get; set; } = true;
		public bool EnableDeterministicLuck { get; set; } = true;

		// Train Page
		public bool ShowTrains { get; set; } = true;


	}
}
