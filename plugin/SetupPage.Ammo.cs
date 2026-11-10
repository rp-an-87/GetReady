using System.Linq;
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
			// The kinds of ammo left out of the filling, like the repair exclusions: what you carry loose, and what is excluded but not on you
			if (controller == null)
			{
				UiKit.Label(inner, "Open this with the stash to pick the ammo to leave out.", 0.85f, UiColors.Dim);
			}
			else
			{
				UiKit.Label(inner, "Leave these kinds of ammo out of the filling. They still count as spare ammo.", 0.8f, UiColors.Dim);
				var carried = AmmoStep.LooseKinds(controller);
				var excluded = ammo.TopUpExcluded;
				var aside = excluded.Where(entry => !carried.Any(stack => stack.TemplateId.ToString() == entry.Template)).ToList();
				TwoSections(inner, "LOOSE ON YOU", "No loose ammo on you.", carried.Count > 0, box =>
				{
					foreach (var stack in carried)
					{
						var kind = stack;
						string template = kind.TemplateId.ToString();
						bool left = AmmoStep.IsTopUpExcluded(template);
						var row = UiKit.HBox(box, 8f);
						string name = Game.NameOf(kind);
						ItemIcon(row, kind, UiKit.Unit * 2.2f, name);
						UiKit.Label(row, left ? name + "  · left out" : name, 0.9f, left ? UiColors.Dim : UiColors.Text, flex: true);
						Pick(row, left ? "Include again" : "Exclude", left, () =>
						{
							AmmoStep.ToggleTopUpExcluded(template, name);
							Changed();
						}, left ? "Fill the stacks of this ammo up again" : "Do not fill up the stacks of this ammo. They still count as spare ammo; a shortfall of spare ammo is still brought.", UiKit.Unit * 8f);
					}
				}, "NOT ON YOU (they stay excluded until you include them again)", "Excluded ammo you are not carrying will appear here.", aside.Count > 0, box =>
				{
					foreach (var entry in aside)
					{
						var gone = entry;
						var row = UiKit.HBox(box, 8f);
						TemplateIcon(row, gone.Template, UiKit.Unit * 2.2f, gone.Name);
						UiKit.Label(row, gone.Name, 0.9f, UiColors.Text, flex: true);
						Pick(row, "Include again", true, () =>
						{
							excluded.Remove(gone);
							Changed();
						}, "Fills the stacks of this ammo up again when you carry it. It is not on you, so it also leaves this list (only excluded ammo is kept here)", UiKit.Unit * 8f);
					}
				});
			}

			if (!ammo.TopUpLoose)
			{
				var dim = sub.gameObject.AddComponent<CanvasGroup>();
				dim.alpha = 0.4f;
				dim.interactable = false;
			}

			UiKit.Line(rules);
			GroupHeading(rules, "EMPTY SPARE MAGAZINES", "The first that applies wins. A magazine in the stash only copies what it holds or an equipped weapon. Rounds are taken from the stash first, the pouch last.");
			PriorityList(rules, "What an empty spare magazine on you copies, first match wins", AmmoRules.PointerOrder(), pointer => AmmoRules.PointerName(pointer), (pointer, step) => AmmoRules.MovePointer(pointer, step), null);

			HelpTitle(rules, "When the loose ammo is of several kinds", "Two of the rules above look at loose ammo to tell which ammo an empty magazine gets (in the same container, and anywhere in an equipped container). When that loose ammo is of more than one kind: Conflict does not guess (the next rule is asked, and with none left the magazine is reported), Worst takes the kind with the least penetration (then damage), Best the most, Random any of them.");
			var conflict = UiKit.HBox(rules, 6f);
			UiKit.Gap(conflict, UiKit.Unit * 2f);
			foreach (var mode in new[] { new[] { "conflict", "Conflict", "Nothing is guessed: the next rule is asked, and with none left the magazine is reported" } }.Concat(PickModes))
			{
				string key = mode[0];
				bool on = ammo.PointerConflict == key;
				Pick(conflict, mode[1], on, () =>
				{
					if (!on)
					{
						ammo.PointerConflict = key;
						Changed();
					}
				}, mode[2], UiKit.Unit * 5.5f);
			}

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
