using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using TMPro;
using UnityEngine;

namespace GetReady
{
	// The Repair and Treat injuries sections of the Setup page
	internal static partial class SetupPage
	{
		private static RectTransform SmallCard(Transform parent, string title)
		{
			var card = UiKit.VBox(parent, 6f, new RectOffset(10, 10, 8, 8), UiKit.CardColor, UiKit.CardEdge);
			UiKit.Label(card, title, 0.95f, Ui.Heading);
			return card;
		}

		private static void RepairSetup(Transform section, InventoryController controller)
		{
			var repair = SettingsStore.Current.Repair;

			var what = SmallCard(section, "What to repair");
			OptionRow(what, repair.Armor, "Repair my armor", "The armor you wear (vest, helmet, armored rig, masks with armor), with armor repair kits or a trader", value => repair.Armor = value);
			OptionRow(what, repair.Weapons, "Repair my weapons", "The weapons in your weapon slots, with weapon repair kits or a trader, the way the R key does it", value => repair.Weapons = value);

			var traders = SmallCard(section, "Traders");
			OptionRow(traders, repair.UseTraders, "Use a trader when I have no repair kit", "A worn piece with no kit that can repair it is repaired by a trader for roubles (all or nothing: not enough roubles is an error). A kit that is there but too small is an error too, not a reason to go to a trader.", value => repair.UseTraders = value);
			var repairController = RepairWithKit.Controller();
			if (repair.UseTraders && repairController != null)
			{
				PriorityList(traders, "The first trader in this order that can repair the piece is used", RepairStep.OrderedTraders(repairController), trader => trader.LocalizedName,
					(trader, step) => RepairStep.MoveTrader(repairController, trader.Id, step), null, false, (row, trader) =>
					{
						var picture = TraderAvatars.Picture(trader);
						if (picture == null)
						{
							ReadyUiWindow.IconPending(() => !TraderAvatars.Pending(trader));
						}

						UiKit.Icon(row, picture, UiKit.Unit * 2f, trader.LocalizedName);
					});
			}

			var exclude = SmallCard(section, "Exclude from repairs");
			if (controller == null)
			{
				UiKit.Label(exclude, "Open this with the stash to pick the pieces.", 0.85f, Ui.Dim);
				return;
			}

			UiKit.Label(exclude, "This very piece, not its type: a new armor of the same type is repaired normally.", 0.8f, Ui.Dim);
			var gear = RepairStep.EquippedGear(controller.Inventory);
			var kept = repair.KeepDamaged;
			var aside = kept.Where(entry => !gear.Any(item => item.Id.ToString() == entry.Id)).ToList();
			TwoSections(exclude, "EQUIPPED", "No armor or weapon equipped.", gear.Count > 0, box =>
			{
				foreach (var item in gear)
				{
					var piece = item;
					bool keep = RepairStep.IsKept(piece);
					var row = UiKit.HBox(box, 8f);
					string name = Game.NameOf(piece);
					ItemIcon(row, piece, UiKit.Unit * 2.2f, name);
					UiKit.Label(row, keep ? name + "  · excluded" : name, 0.9f, keep ? Ui.Dim : Ui.Text, flex: true);
					Pick(row, keep ? "Include again" : "Exclude", keep, () =>
					{
						RepairStep.ToggleKept(piece);
						Changed();
					}, keep ? "Repair this piece again" : "Leave this piece out of the repairs", UiKit.Unit * 8f);
				}
			}, "NOT EQUIPPED (they stay excluded until you include them again)", "Excluded pieces you are not wearing will appear here.", aside.Count > 0, box =>
			{
				foreach (var entry in aside)
				{
					var gone = entry;
					var row = UiKit.HBox(box, 8f);
					TemplateIcon(row, gone.Template, UiKit.Unit * 2.2f, gone.Name);
					UiKit.Label(row, gone.Name, 0.9f, Ui.Text, flex: true);
					Pick(row, "Include again", true, () =>
					{
						kept.Remove(gone);
						Changed();
					}, "Repairs it again when you wear it. It is not equipped, so it also leaves this list (only excluded pieces are kept here)", UiKit.Unit * 8f);
				}
			});
		}

		// Treat injuries: one list of the medicine in the order of use; the game's icons on each row show what it treats
		private static void HealMe(Transform section)
		{
			var def = HealMeStep.Items;
			var head = UiKit.HBox(section, 8f);
			UiKit.HelpMark(head, "The most urgent injury is treated with the first item of the list that treats it, until you are healed. Put the dedicated items before the kits and a kit is only left for what is still hurt (a kit treats everything it can on a part at once). Runs last, so what Get ready just brought is left alone. The icons on each row show what the item treats.");
			UiKit.Label(head, "Used in this order: the first that treats the injury", 0.9f, Ui.Text, flex: true);
			Pick(head, "Reset", false, () =>
			{
				HealMeStep.ResetAll();
				Changed();
			}, "The usual order and the usual items on", UiKit.Unit * 5f);

			var box = UiKit.VBox(section, 4f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge);
			var entries = ReadyWindow.ShownEntries(def);
			var rows = new List<RectTransform>();
			foreach (var entry in entries)
			{
				string template = entry.Template;
				bool off = !HealMeStep.IsOn(template);
				var row = UiKit.HBox(box, 8f);
				UiKit.Grip(row);
				TemplateIcon(row, template, UiKit.Unit * 2f, entry.Name);
				UiKit.Size(UiKit.Label(row, off ? "× " + entry.Name : entry.Name, 0.9f, off ? Ui.Dim : Ui.Text), UiKit.Unit * 10f);
				foreach (var trouble in HealMeStep.Troubles)
				{
					// A fixed column each, so the pictures line up; nothing where it can't treat
					if (!HealMeStep.Treats(template, trouble.Id))
					{
						UiKit.Gap(row, UiKit.Unit * 1.6f);
						continue;
					}

					var sprite = Icons.EffectSprite(trouble.Icon);
					UiKit.Icon(row, sprite, UiKit.Unit * 1.6f, "Treats " + trouble.Name);
					if (off)
					{
						row.GetChild(row.childCount - 1).gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;
					}
				}

				UiKit.Spacer(row);
				Pick(row, off ? "Not used" : "Used", !off, () =>
				{
					def.ToggleEntry(template);
					Changed();
				}, null, UiKit.Unit * 6f);
				rows.Add(row);
			}

			UiReorder.Attach(box, rows, (from, to) =>
			{
				ReadyWindow.ReorderEntries(def, entries, from, to);
				Changed();
			});
		}
	}
}
