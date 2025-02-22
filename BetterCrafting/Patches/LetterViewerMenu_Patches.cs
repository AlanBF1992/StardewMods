using System;

using HarmonyLib;

using StardewModdingAPI;

using StardewValley;
using StardewValley.Menus;

namespace Leclair.Stardew.BetterCrafting.Patches;

public static class LetterViewerMenu_Patches {

	private static IMonitor? Monitor;

	public static void Patch(ModEntry mod) {
		Monitor = mod.Monitor;

		try {
			mod.Harmony!.Patch(
				original: AccessTools.Method(typeof(LetterViewerMenu), nameof(LetterViewerMenu.HandleItemCommand)),
				prefix: new HarmonyMethod(typeof(LetterViewerMenu_Patches), nameof(HandleItemCommand_Prefix))
			);

		} catch (Exception ex) {
			mod.Log("An error occurred while registering a harmony patch for the vanilla LetterViewerMenu.", LogLevel.Warn, ex);
		}
	}

	public static void HandleItemCommand_Prefix(LetterViewerMenu __instance, ref string mail) {
		try {
			mail = ProcessMail(__instance, mail);
		} catch (Exception ex) {
			Monitor?.Log($"An error occurred while attempting to process item commands in a LetterViewerMenu.", LogLevel.Error);
			Monitor?.Log($"Details:\n{ex}", LogLevel.Error);
		}
	}

	public static string ProcessMail(LetterViewerMenu menu, string input) {

		ModEntry mod = ModEntry.Instance;

		int searchFromIndex = 0;
		while (true) {
			int startItemIndex = input.IndexOf("%item", searchFromIndex, StringComparison.InvariantCulture);
			if (startItemIndex < 0)
				break;
			int endItemIndex = input.IndexOf("%%", startItemIndex, StringComparison.InvariantCulture);
			if (endItemIndex < 0)
				break;

			string substring = input.Substring(startItemIndex, endItemIndex + 2 - startItemIndex);

			string[] typeAndArgs = ArgUtility.SplitBySpace(substring.Substring("%item".Length, substring.Length - "%item".Length - "%%".Length), 2);
			string type = typeAndArgs[0];
			string[] args = typeAndArgs.Length > 1
				? ArgUtility.SplitBySpace(typeAndArgs[1])
				: [];

			bool doReplace = false;

			// Do we have a craftingrecipe or cookingrecipe?
			if (!menu.isFromCollection && (type.ToLower() == "cookingrecipe" || type.ToLower() == "craftingrecipe")) {
				string recipeKey = string.Join(" ", args);
				//bool cooking = type.ToLower() == "cookingrecipe";

				if (!string.IsNullOrEmpty(recipeKey)) {
					if (mod.DataRecipes.TryGetRecipeById(recipeKey, out var recipe)) {
						if (recipe.Data.IsCooking)
							Game1.player.cookingRecipes.TryAdd(recipeKey, 0);
						else
							Game1.player.craftingRecipes.TryAdd(recipeKey, 0);

						menu.learnedRecipe = recipe.DisplayName;
						menu.cookingOrCrafting = Game1.content.LoadString(@"Strings\UI:LearnedRecipe_crafting");

						doReplace = true;
					}
				}
			}

			if (doReplace) {
				// Since we did handle an entry, remove it from the string.
				input = input.Substring(0, startItemIndex) + input.Substring(startItemIndex + substring.Length);
				searchFromIndex = startItemIndex;
			} else
				// TODO: Is this correct?
				searchFromIndex = endItemIndex + 2;
		}

		return input;

	}

}
