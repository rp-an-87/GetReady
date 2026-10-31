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

		private static bool SpareAmmoSetup(InventoryController controller)
		{
			var ammo = SettingsStore.Current.Ammo;
			if (AmmoStep.Spare.Setting.Target <= 0)
			{
				return false;
			}

			bool changed = false;
			GUILayout.BeginVertical(Ui.Group);
			TitleWithHelp("Spare ammo", "If you carry no loose ammo for a weapon's caliber once the magazines are filled, the stash gives the stacks above of ammo that is already in use for it (loaded, chambered or in a spare magazine). Nothing is guessed: with nothing in use, or none in the stash, you get a warning.");
			GUILayout.BeginHorizontal();
			GUILayout.Label(new GUIContent("When several kinds are in use", "Bring this one of them"), Ui.Body, GUILayout.Width(190));
			foreach (var mode in PickModes)
			{
				bool on = ammo.SparePick == mode[0];
				if (GUILayout.Button(new GUIContent(mode[1], mode[2]), on ? Ui.PickOn : Ui.Pick, GUILayout.Width(80)) && !on)
				{
					ammo.SparePick = mode[0];
					changed = true;
				}
			}

			GUILayout.EndHorizontal();

			// The weapons you carry now, and the ones set aside before (by model, not by the very weapon)
			TitleWithHelp("Bring spare ammo for these weapons (by model)", "Skip the ones that never need it, like a sidearm you never reload.");
			var shown = new Dictionary<string, KeyValuePair<Item, string>>();
			foreach (var weapon in AmmoRules.EquippedWeapons(controller.Inventory).Where(weapon => !string.IsNullOrEmpty(weapon.AmmoCaliber)))
			{
				var template = weapon.TemplateId.ToString();
				if (!shown.ContainsKey(template))
				{
					shown[template] = new KeyValuePair<Item, string>(weapon, Game.NameOf(weapon));
				}
			}

			foreach (var entry in ammo.SpareIgnored.Where(entry => !shown.ContainsKey(entry.Template)))
			{
				shown[entry.Template] = new KeyValuePair<Item, string>(null, entry.Name);
			}

			if (shown.Count == 0)
			{
				GUILayout.Label("No weapon on you.", Ui.Muted);
			}

			foreach (var pair in shown)
			{
				bool ignored = ammo.SpareIgnored.Any(entry => entry.Template == pair.Key);
				GUILayout.BeginHorizontal();
				if (pair.Value.Key != null)
				{
					Icons.Slot(pair.Value.Key, pair.Value.Value, 34);
				}
				else
				{
					Icons.Slot(pair.Key, pair.Value.Value, 34);
				}

				GUILayout.Label(pair.Value.Value, ignored ? Ui.Muted : Ui.Body, GUILayout.ExpandWidth(true));
				if (GUILayout.Button(new GUIContent(ignored ? "Skipped" : "Bring spare", ignored ? "No spare ammo is brought for this weapon. Click to bring it again." : "Spare ammo is brought for this weapon. Click to skip it."), ignored ? Ui.Pick : Ui.PickOn, GUILayout.Width(130)))
				{
					if (ignored)
					{
						ammo.SpareIgnored.RemoveAll(entry => entry.Template == pair.Key);
					}
					else
					{
						ammo.SpareIgnored.Add(new ItemRef { Template = pair.Key, Name = pair.Value.Value });
					}

					changed = true;
				}

				GUILayout.EndHorizontal();
			}

			GUILayout.EndVertical();
			return changed;
		}
	}
}
