using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The Ammo section of the Setup page: how the magazines are filled, then the spare ammo card.
	internal static partial class SetupPage
	{
		private static void AmmoSetup(Transform section, InventoryController controller)
		{
			var ammo = SettingsStore.Current.Ammo;
			// The rules for filling the magazines are a card of their own, like the spare ammo card below it
			var rules = UiKit.VBox(section, 8f, new RectOffset(10, 10, 8, 8), UiKit.CardColor, UiKit.CardEdge);
			UiKit.Label(rules, "Magazine top-up rules", 0.95f, Ui.Heading);
			HelpTitle(rules, "How the magazines are filled", "Follows what your magazines and containers already hold. Nothing moves until you press Get ready.");
			OptionRow(rules, ammo.Chamber, "Chamber a round in weapons with an empty chamber", "The round is the same ammo as the weapon's magazine, taken from your gear first, then the stash", value => ammo.Chamber = value);
			OptionRow(rules, ammo.TopUpLoose, "Keep the loose ammo stacks I carry full", "The stacks of loose ammo in your vest, pockets, backpack and pouch are filled up to a full stack with the same ammo from the stash. No new kinds of ammo are added.", value => ammo.TopUpLoose = value);
			OptionRow(rules, ammo.LooseShortIsProblem, "A loose stack that can't be filled is a problem", "When on, a loose stack that is short and that the stash has no more of is reported in red, and counted in the box at the top. When off it is just left as it is.", value => ammo.LooseShortIsProblem = value);
			PriorityList(rules, "What an empty spare magazine on you copies", AmmoRules.PointerOrder(), pointer => AmmoRules.PointerName(pointer), (pointer, step) => AmmoRules.MovePointer(pointer, step),
				"The first that applies wins. A magazine in the stash only copies what it holds or an equipped weapon. Rounds are taken from the stash first, the pouch last.");
			if (controller != null)
			{
				SpareCard(section, controller);
			}
			else
			{
				UiKit.Label(section, "Open this with the stash to set the spare ammo.", 0.85f, Ui.Dim);
			}
		}
	}
}
