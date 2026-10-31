using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The Setup card of the Repair step: what to repair, the traders, and the pieces left damaged on purpose, each in its own section.
	internal static partial class ReadyWindow
	{
		// The "leave damaged" list is out of the way until it is asked for (it is something you rarely want); it always shows while
		// at least one piece is on it
		private static bool _showKeepList;

		private static bool RepairSetup(InventoryController controller)
		{
			bool changed = false;
			var repair = SettingsStore.Current.Repair;

			// What to repair
			GUILayout.BeginVertical(Ui.Group);
			GUILayout.Label("What to repair", Ui.Name);
			bool armor = OptionRow(repair.Armor, "Repair my armor", "The armor you wear (vest, helmet, armored rig, masks with armor), with armor repair kits or a trader");
			if (armor != repair.Armor)
			{
				repair.Armor = armor;
				changed = true;
			}

			bool weapons = OptionRow(repair.Weapons, "Repair my weapons", "The weapons in your weapon slots, with weapon repair kits or a trader, the way the R key does it");
			if (weapons != repair.Weapons)
			{
				repair.Weapons = weapons;
				changed = true;
			}

			GUILayout.EndVertical();

			// Traders
			GUILayout.BeginVertical(Ui.Group);
			GUILayout.Label("Traders", Ui.Name);
			bool traders = OptionRow(repair.UseTraders, "Use a trader when I have no repair kit", "A worn piece with no kit that can repair it is repaired by a trader for roubles (all or nothing: not enough roubles is an error). A kit that is there but too small is an error too, not a reason to go to a trader.");
			if (traders != repair.UseTraders)
			{
				repair.UseTraders = traders;
				changed = true;
			}

			var repairController = RepairWithKit.Controller();
			if (repair.UseTraders && repairController != null)
			{
				changed |= PriorityList("The first trader in this order that can repair the piece is used:", RepairStep.OrderedTraders(repairController),
					trader => trader.LocalizedName, (trader, rect) => TraderAvatars.Draw(rect, trader), (trader, step) => RepairStep.MoveTrader(repairController, trader.Id, step));
			}

			GUILayout.EndVertical();

			// Pieces left damaged on purpose
			var gear = RepairStep.EquippedGear(controller.Inventory);
			var kept = repair.KeepDamaged;
			if (kept.Count == 0 && !_showKeepList)
			{
				if (GUILayout.Button(new GUIContent("Leave a piece damaged on purpose...", "For gear you want to hand in damaged, for a quest for instance"), Ui.Pick))
				{
					_showKeepList = true;
				}

				return changed;
			}

			GUILayout.BeginVertical(Ui.Group);
			GUILayout.BeginHorizontal();
			HelpTitle("This very piece, not its type: a new armor of the same type is repaired normally.", "Left damaged on purpose", Ui.Name);
			GUILayout.FlexibleSpace();
			if (kept.Count == 0 && GUILayout.Button("Hide", Ui.Pick, GUILayout.Width(60)))
			{
				_showKeepList = false;
			}

			GUILayout.EndHorizontal();
			if (gear.Count == 0)
			{
				GUILayout.Label("No armor or weapon equipped.", Ui.Muted);
			}

			foreach (var item in gear)
			{
				bool keep = RepairStep.IsKept(item);
				GUILayout.BeginHorizontal();
				Icons.Slot(item, Game.NameOf(item), 40);
				GUILayout.Label(Game.NameOf(item) + (keep ? "  -  left damaged" : ""), keep ? Ui.Name : Ui.Body);
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(keep ? "Repair it again" : "Leave it damaged", keep ? Ui.PickOn : Ui.Pick, GUILayout.Width(150)))
				{
					RepairStep.ToggleKept(item);
					changed = true;
				}

				GUILayout.EndHorizontal();
			}

			foreach (var entry in kept.Where(entry => !gear.Any(item => item.Id.ToString() == entry.Id)).ToList())
			{
				GUILayout.BeginHorizontal();
				Icons.Slot(entry.Template, entry.Name, 40);
				GUILayout.Label(entry.Name + "  -  saved, not on you", Ui.Muted);
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Remove from the list", Ui.Pick, GUILayout.Width(150)))
				{
					kept.Remove(entry);
					changed = true;
					// The last one gone: the section folds back into the small button
					if (kept.Count == 0)
					{
						_showKeepList = false;
					}
				}

				GUILayout.EndHorizontal();
			}

			GUILayout.EndVertical();
			return changed;
		}
	}
}
