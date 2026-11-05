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

			// One card, three rules, each with a small heading: chambering, the loose stacks, and what an empty spare magazine copies
			var rules = UiKit.VBox(section, 8f, new RectOffset(10, 10, 8, 8), UiKit.CardColor, UiKit.CardEdge);
			UiKit.Label(rules, "Magazines and loose ammo", 0.95f, UiColors.Heading);

			GroupHeading(rules, "CHAMBER", "The round is the same ammo as the weapon's magazine, taken from your gear first, then the stash.");
			OptionRow(rules, ammo.Chamber, "Chamber a round in weapons with an empty chamber", null, value => ammo.Chamber = value);

			UiKit.Line(rules);
			GroupHeading(rules, "LOOSE AMMO STACKS", "The stacks of loose ammo in your vest, pockets, backpack and pouch are filled up to a full stack with the same ammo from the stash. No new kinds of ammo are added.");
			OptionRow(rules, ammo.TopUpLoose, "Keep the loose ammo stacks I carry full", null, value => ammo.TopUpLoose = value);
			// Only means something while the stacks are kept full: indented under it, and dim while that is off
			var sub = UiKit.HBox(rules, 0f, new RectOffset((int)(UiKit.Unit * 2.4f), 0, 0, 0));
			UiKit.Size(sub, flexWidth: 1f);
			var inner = UiKit.VBox(sub, 0f);
			UiKit.Size(inner, flexWidth: 1f);
			OptionRow(inner, ammo.LooseShortIsProblem, "A loose stack that can't be filled is a problem", "A loose stack of ammo you carry that is short, when the stash has no more of it to fill it up.\n<b>On:</b> it is reported in red and counted in the box at the top.\n<b>Off:</b> it is just left as it is.", value => ammo.LooseShortIsProblem = value);
			if (!ammo.TopUpLoose)
			{
				var dim = sub.gameObject.AddComponent<CanvasGroup>();
				dim.alpha = 0.4f;
				dim.interactable = false;
			}

			UiKit.Line(rules);
			GroupHeading(rules, "EMPTY SPARE MAGAZINES", "The first that applies wins. A magazine in the stash only copies what it holds or an equipped weapon. Rounds are taken from the stash first, the pouch last.");
			PriorityList(rules, "What an empty spare magazine on you copies, first match wins", AmmoRules.PointerOrder(), pointer => AmmoRules.PointerName(pointer), (pointer, step) => AmmoRules.MovePointer(pointer, step), null);

			if (controller != null)
			{
				SpareCard(section, controller);
			}
			else
			{
				UiKit.Label(section, "Open this with the stash to set the spare ammo.", 0.85f, UiColors.Dim);
			}
		}

		// A small heading for a group of settings, with the round "?" that explains it
		private static void GroupHeading(Transform parent, string title, string help)
		{
			var row = UiKit.HBox(parent, 8f);
			UiKit.Label(row, title, 0.75f, UiColors.Dim, bold: true);
			UiKit.HelpMark(row, help);
		}
	}
}
