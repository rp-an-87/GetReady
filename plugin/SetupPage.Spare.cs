using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using TMPro;
using UnityEngine;

namespace GetReady
{
	// The spare ammo card of the Ammo section: stacks per caliber, where they go, which kind, and the stacks per weapon model.
	internal static partial class SetupPage
	{
		private static readonly string[][] PickModes =
		{
			new[] { "worst", "Worst", "The ammo with the least penetration (then damage) of the kinds in use" },
			new[] { "best", "Best", "The ammo with the most penetration (then damage) of the kinds in use" },
			new[] { "random", "Random", "Any of the kinds in use" }
		};

		private const int MostStacks = 9;

		private static void SpareCard(Transform parent, InventoryController controller)
		{
			var ammo = SettingsStore.Current.Ammo;
			var def = AmmoStep.Spare;
			var setting = def.Setting;
			bool anyOwn = AmmoStep.AnyOwnStacks();
			var card = UiKit.VBox(parent, 8f, new RectOffset(10, 10, 8, 8), UiKit.CardColor, UiKit.CardEdge);
			UiKit.Label(card, "Spare ammo", 0.95f, setting.Target > 0 || anyOwn ? UiColors.Heading : UiColors.Dim);

			// How many stacks for each caliber
			var stacks = UiKit.HBox(card, 8f);
			UiKit.HelpMark(stacks, "How many stacks of spare ammo to carry for each caliber your equipped weapons use (a stack is the ammo's full stack size). Loose ammo of that caliber on you counts toward it, and so does what the top-up of your loose stacks adds; only what is still missing is brought from the stash, and you get a warning when the stash has no more. A weapon model can ask for another number in the list below.");
			Stepper(stacks, setting.Target > 0 ? $"{setting.Target} stack{(setting.Target == 1 ? "" : "s")} per caliber" : "off", setting.Target > 0 ? UiColors.Text : UiColors.Dim, 11f,
				() =>
				{
					if (setting.Target > 0)
					{
						setting.Target = Math.Max(0, setting.Target - def.Step);
						Changed();
					}
				},
				() =>
				{
					if (setting.Target < def.Max)
					{
						setting.Target = Math.Min(def.Max, setting.Target + def.Step);
						Changed();
					}
				}, "One stack fewer", "One stack more");

			// Where the ammo goes (also when the number above is 0 but a weapon model asks for its own)
			if (setting.Target > 0 || anyOwn)
			{
				PlacesPanel(card, def);
			}

			// Which kind, when a caliber has several in use
			HelpTitle(card, "Ammo to bring when a caliber has several kinds in use (loaded, chambered or in your magazines)", "A kind is in use for a caliber when it is loaded in a weapon or a magazine you carry, chambered, or about to be loaded by this run. When there are several, the one brought is the worst (least penetration, then damage), the best (most) or a random one. Nothing is guessed: with no kind in use, you get a warning.");
			var picks = UiKit.HBox(card, 6f);
			UiKit.Gap(picks, UiKit.Unit * 2f);
			foreach (var mode in PickModes)
			{
				string key = mode[0];
				bool on = ammo.SparePick == key;
				Pick(picks, mode[1], on, () =>
				{
					if (!on)
					{
						ammo.SparePick = key;
						Changed();
					}
				}, mode[2], UiKit.Unit * 5f);
			}

			// The weapons you carry now first, then the models set aside before (by model, not by the very weapon)
			HelpTitle(card, "Stacks of spare ammo per weapon (by model)", "Each model asks for the generic number above unless you change it here; 0 means no spare ammo for it. Weapons of one caliber share the request: the biggest number counts, and two of the same model count once. A model set back to the generic number leaves the list.");
			var equipped = new List<KeyValuePair<string, KeyValuePair<Item, string>>>();
			foreach (var weapon in AmmoRules.EquippedWeapons(controller.Inventory).Where(weapon => !string.IsNullOrEmpty(weapon.AmmoCaliber)))
			{
				var template = weapon.TemplateId.ToString();
				if (!equipped.Any(entry => entry.Key == template))
				{
					equipped.Add(new KeyValuePair<string, KeyValuePair<Item, string>>(template, new KeyValuePair<Item, string>(weapon, Game.NameOf(weapon))));
				}
			}

			var remembered = ammo.SpareStacks.Select(entry => new ItemRef { Template = entry.Template, Name = entry.Name }).Concat(ammo.SpareIgnored);
			var away = remembered.Where(entry => !equipped.Any(known => known.Key == entry.Template)).GroupBy(entry => entry.Template).Select(group => group.First()).ToList();
			TwoSections(card, "EQUIPPED", "No weapon equipped.", equipped.Count > 0, box =>
			{
				foreach (var pair in equipped)
				{
					StackRow(box, pair.Key, pair.Value.Value, pair.Value.Key);
				}
			}, "NOT EQUIPPED", "Models you gave a number of their own and are not carrying will appear here.", away.Count > 0, box =>
			{
				foreach (var entry in away)
				{
					StackRow(box, entry.Template, entry.Name, null);
				}
			});
		}

		// One model: its icon, its name and the stacks it asks for
		private static void StackRow(Transform parent, string template, string name, Item item)
		{
			var row = UiKit.HBox(parent, 8f);
			if (item != null)
			{
				ItemIcon(row, item, UiKit.Unit * 2.2f, name);
			}
			else
			{
				TemplateIcon(row, template, UiKit.Unit * 2.2f, name);
			}

			UiKit.Label(row, name, 0.9f, UiColors.Text, flex: true);
			int generic = AmmoStep.Spare.Setting.Target;
			int now = AmmoStep.StacksOf(template);
			string amount = now == 0 ? "none" : $"{now} stack{(now == 1 ? "" : "s")}";
			string text = now == generic ? $"default ({(now == 0 ? "none" : now.ToString())})" : amount;
			Stepper(row, text, now == generic ? UiColors.Dim : UiColors.Heading, 7.5f,
				() =>
				{
					if (now > 0)
					{
						AmmoStep.SetStacks(template, name, now - 1);
						Changed();
					}
				},
				() =>
				{
					if (now < MostStacks)
					{
						AmmoStep.SetStacks(template, name, now + 1);
						Changed();
					}
				}, "One stack fewer for this model (0: no spare ammo)", "One stack more for this model");
		}
	}
}
