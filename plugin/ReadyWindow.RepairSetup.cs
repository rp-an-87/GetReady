using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The Setup card of the Repair step: what to repair, the traders, and the pieces excluded from repairs, each in its own section.
	internal static partial class ReadyWindow
	{
		// The "leave damaged" list is out of the way until it is asked for (it is something you rarely want); it always shows while
		// at least one piece is on it

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

			// Pieces excluded from repairs
			var gear = RepairStep.EquippedGear(controller.Inventory);
			var kept = repair.KeepDamaged;
			GUILayout.BeginVertical(Ui.Group);
			GUILayout.BeginHorizontal();
			HelpTitle("This very piece, not its type: a new armor of the same type is repaired normally.", "Exclude from repairs", Ui.Name);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			var aside = kept.Where(entry => !gear.Any(item => item.Id.ToString() == entry.Id)).ToList();
			bool pressed = false;
			TwoSections("repair", "EQUIPPED", "No armor or weapon equipped.", gear.Count > 0, () =>
			{
				foreach (var item in gear)
				{
					bool keep = RepairStep.IsKept(item);
					GUILayout.BeginHorizontal();
					Icons.Slot(item, Game.NameOf(item), 40);
					GUILayout.Space(8);
					Centered(() => GUILayout.Label(Game.NameOf(item), Ui.Plain, GUILayout.ExpandWidth(false)), 40);
					if (keep)
					{
						Centered(() => GUILayout.Label("· excluded", Ui.Muted, GUILayout.ExpandWidth(false)), 40);
					}

					GUILayout.FlexibleSpace();
					if (CenteredButton(new GUIContent(keep ? "Include again" : "Exclude", keep ? "Repair this piece again" : "Leave this piece out of the repairs"), keep ? Ui.PickOn : Ui.Pick, 130, 40))
					{
						RepairStep.ToggleKept(item);
						pressed = true;
					}

					GUILayout.EndHorizontal();
				}
			}, "NOT EQUIPPED (they stay excluded until you include them again)", "Excluded pieces you are not wearing will appear here.", aside.Count > 0, () =>
			{
				foreach (var entry in aside)
				{
					GUILayout.BeginHorizontal();
					Icons.Slot(entry.Template, entry.Name, 40);
					GUILayout.Space(8);
					Centered(() => GUILayout.Label(entry.Name, Ui.Plain, GUILayout.ExpandWidth(false)), 40);
					GUILayout.FlexibleSpace();
					if (CenteredButton(new GUIContent("Include again", "Repairs it again when you wear it. It is not equipped, so it also leaves this list (only excluded pieces are kept here)"), Ui.PickOn, 130, 40))
					{
						kept.Remove(entry);
						pressed = true;
					}

					GUILayout.EndHorizontal();
				}
			});
			changed |= pressed;

			GUILayout.EndVertical();
			return changed;
		}
	}
}
