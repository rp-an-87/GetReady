using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The Setup page of the spare ammo check (the Ammo step): which kind to bring and which weapons to skip.
	internal static partial class ReadyWindow
	{
		private static readonly string[][] PickModes =
		{
			new[] { "worst", "Worst", "The ammo with the least penetration (then damage) of the kinds in use" },
			new[] { "best", "Best", "The ammo with the most penetration (then damage) of the kinds in use" },
			new[] { "random", "Random", "Any of the kinds in use" }
		};

		// The whole Setup card of the spare ammo in one place: how many stacks per caliber, where they go, which kind, and the stacks per weapon model
		private static bool SpareAmmoSetup(InventoryController controller)
		{
			var ammo = SettingsStore.Current.Ammo;
			var def = AmmoStep.Spare;
			var setting = def.Setting;
			bool changed = false;
			GUILayout.BeginVertical(Ui.Group);
			GUILayout.Label("Spare ammo", setting.Target > 0 || AmmoStep.AnyOwnStacks() ? Ui.Name : Ui.Muted);

			// How many stacks for each caliber: the "?" first, then "-", the number and "+"
			GUILayout.BeginHorizontal();
			Centered(() => Help("How many stacks of spare ammo to carry for each caliber your equipped weapons use (a stack is the ammo's full stack size). Loose ammo of that caliber on you counts toward it, and so does what the top-up of your loose stacks adds; only what is still missing is brought from the stash, and you get a warning when the stash has no more. A weapon model can ask for another number in the list below."), 34);
			if (CenteredButton(new GUIContent("-", "One stack fewer"), Ui.Small, 30, 34) && setting.Target > 0)
			{
				setting.Target = Math.Max(0, setting.Target - def.Step);
				changed = true;
			}

			var stacks = setting.Target > 0 ? $"{setting.Target} stack{(setting.Target == 1 ? "" : "s")} per caliber" : "off";
			Centered(() => GUILayout.Label(stacks, setting.Target > 0 ? Ui.Centered : Ui.Muted, GUILayout.Width(150)), 34, false, 150);
			if (CenteredButton(new GUIContent("+", "One stack more"), Ui.Small, 30, 34) && setting.Target < def.Max)
			{
				setting.Target = Math.Min(def.Max, setting.Target + def.Step);
				changed = true;
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			// Where the ammo goes (also when the number above is 0 but a weapon model asks for its own)
			if (setting.Target > 0 || AmmoStep.AnyOwnStacks())
			{
				GUILayout.Space(6);
				GUILayout.BeginHorizontal();
				GUILayout.BeginHorizontal(GUILayout.Width(GoesToWidth));
				HelpTitle("Each item goes to the first place in the row that has room, tried from left to right. Drag a place onto another to swap them. Click a place to switch it off (a red cross, at the end, never used).", "Bring to", Ui.Muted, 28f);
				GUILayout.EndHorizontal();
				changed |= PlacePicker(def, false);
				GUILayout.EndHorizontal();
			}

			// Which kind, when a caliber has several in use
			GUILayout.Space(8);
			TitleWithHelp("Ammo to bring when a caliber has several kinds in use (loaded, chambered or in your magazines)", "A kind is in use for a caliber when it is loaded in a weapon or a magazine you carry, chambered, or about to be loaded by this run. When there are several, the one brought is the worst (least penetration, then damage), the best (most) or a random one. Nothing is guessed: with no kind in use, you get a warning.");
			GUILayout.BeginHorizontal();
			GUILayout.Space(30);
			foreach (var mode in PickModes)
			{
				bool on = ammo.SparePick == mode[0];
				if (GUILayout.Button(new GUIContent(mode[1], mode[2]), on ? Ui.PickOn : Ui.Pick, GUILayout.Width(80)) && !on)
				{
					ammo.SparePick = mode[0];
					changed = true;
				}
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Space(8);

			// The weapons you carry now first, then the models set aside before (by model, not by the very weapon): how many stacks each asks for
			TitleWithHelp("Stacks of spare ammo per weapon (by model)", "Each model asks for the generic number above unless you change it here; 0 means no spare ammo for it. Weapons of one caliber share the request: the biggest number counts, and two of the same model count once. A model set back to the generic number leaves the list.");
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
			bool stepped = false;
			TwoSections("spare", "EQUIPPED", "No weapon equipped.", equipped.Count > 0, () =>
			{
				foreach (var pair in equipped)
				{
					StackRow(pair.Key, pair.Value.Value, pair.Value.Key, true, ref stepped);
				}
			}, "NOT EQUIPPED", "Models you gave a number of their own and are not carrying will appear here.", away.Count > 0, () =>
			{
				foreach (var entry in away)
				{
					StackRow(entry.Template, entry.Name, null, false, ref stepped);
				}
			});
			changed |= stepped;

			GUILayout.EndVertical();
			return changed;
		}

		// One model: its icon, its name and the stacks it asks for
		private static void StackRow(string template, string name, Item item, bool equipped, ref bool changed)
		{
			GUILayout.BeginHorizontal();
			if (item != null)
			{
				Icons.Slot(item, name, 34);
			}
			else
			{
				Icons.Slot(template, name, 34);
			}

			GUILayout.Space(8);
			Centered(() => GUILayout.Label(name, Ui.Plain, GUILayout.ExpandWidth(false)), 34);
			GUILayout.FlexibleSpace();
			changed |= StacksStepper(template, name);
			GUILayout.EndHorizontal();
		}

		private const int MostStacks = 9;

		// "-", the stacks this model asks for and "+": the generic number is shown muted, its own number plain
		private static bool StacksStepper(string template, string name)
		{
			int generic = AmmoStep.Spare.Setting.Target;
			int now = AmmoStep.StacksOf(template);
			bool changed = false;
			if (CenteredButton(new GUIContent("-", "One stack fewer for this model (0: no spare ammo)"), Ui.Small, 30, 34) && now > 0)
			{
				SetStacks(template, name, now - 1);
				changed = true;
			}

			// The generic number reads "default (N)" and is dim; a number of its own is bold, in the heading colour
			var amount = now == 0 ? "none" : $"{now} stack{(now == 1 ? "" : "s")}";
			var text = now == generic ? $"default ({(now == 0 ? "none" : now.ToString())})" : amount;
			Centered(() => GUILayout.Label(new GUIContent(text, now == generic ? "The generic number" : "This model's own number: " + amount), now == generic ? Ui.PlainMuted : Ui.PlainName, GUILayout.Width(110)), 34, false, 110);
			if (CenteredButton(new GUIContent("+", "One stack more for this model"), Ui.Small, 30, 34) && now < MostStacks)
			{
				SetStacks(template, name, now + 1);
				changed = true;
			}

			return changed;
		}

		// A model asks for this many stacks: the generic number takes it off the list, any other number puts it on (or changes it)
		internal static void SetStacks(string template, string name, int stacks)
		{
			var ammo = SettingsStore.Current.Ammo;
			ammo.SpareIgnored.RemoveAll(entry => entry.Template == template);
			ammo.SpareStacks.RemoveAll(entry => entry.Template == template);
			if (stacks != AmmoStep.Spare.Setting.Target)
			{
				ammo.SpareStacks.Add(new ModelStacks { Template = template, Name = name, Stacks = stacks });
			}
		}
	}
}
