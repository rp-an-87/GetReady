using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace GetReady
{
	// The supply groups of the Setup page (Medicine, Grenades): a card per group with its item, the amount, the quick-slot key, where it goes
	// and, opened with "Items", which items count and in what order.
	internal static partial class SetupPage
	{
		// Groups whose item list is open
		private static readonly HashSet<string> Expanded = new HashSet<string>();

		// The groups one under the other, each in a card of the full width
		private static void GroupList(Transform parent, IEnumerable<GroupDef> defs)
		{
			foreach (var def in defs)
			{
				GroupCard(parent, def, false);
			}
		}

		// The groups two to a row, each card narrow
		private static void GroupGrid(Transform parent, IReadOnlyList<GroupDef> defs)
		{
			for (int i = 0; i < defs.Count; i += 2)
			{
				var row = UiKit.HBox(parent, 8f, null, null, true);
				var widthRow = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
				widthRow.childForceExpandWidth = true;
				for (int k = i; k < Math.Min(i + 2, defs.Count); k++)
				{
					var card = GroupCard(row, defs[k], true);
					// The same width asked for, and the room shared out equally, so the two cards are the same width
					UiKit.Size(card, width: 1f, flexWidth: 1f).minWidth = 0f;
				}

				if (defs.Count - i == 1)
				{
					// An odd one out keeps the width of a card: the other half stays empty
					var empty = UiKit.Node("Empty", row);
					UiKit.Size(empty, width: 1f, flexWidth: 1f).minWidth = 0f;
				}
			}
		}

		// A group's card. It is registered, so a change made inside it draws only this card again (see RefreshCard), not the whole section.
		private static RectTransform GroupCard(Transform parent, GroupDef def, bool compact)
		{
			var card = UiKit.VBox(parent, 8f, new RectOffset(10, 10, 8, 8), UiKit.CardColor, UiKit.CardEdge);
			Action fill = () => FillGroupCard(card, def, compact);
			fill();
			Cards.Add(new KeyValuePair<RectTransform, Action>(card, fill));
			return card;
		}

		private static void FillGroupCard(RectTransform card, GroupDef def, bool compact)
		{
			var setting = def.Setting;
			var active = def.ActiveEntries();
			bool on = setting.Target > 0;
			var top = UiKit.HBox(card, 10f);
			if (def.Entries.Length > 0)
			{
				var shown = active.FirstOrDefault() ?? def.Entries[0];
				TemplateIcon(top, shown.Template, UiKit.Unit * 3f, shown.Name);
			}
			else if (def.IsQuota)
			{
				// A square with a "?", like an item not inspected yet: it stands for any of several items
				UiKit.Icon(top, null, UiKit.Unit * 3f, "Any of your grenades, picked at random");
				var mark = UiKit.Label(top.GetChild(top.childCount - 1), "?", 1.1f, UiColors.Dim, align: TextAlignmentOptions.Center);
				UiKit.Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);
			}

			var column = UiKit.VBox(top, 4f);
			// Money has room for the places on the same row, so its column keeps to what it needs
			UiKit.Size(column, flexWidth: def.Money ? 0f : 1f);
			UiKit.Label(column, def.Name, 0.95f, on ? UiColors.Heading : UiColors.Dim);
			var amount = UiKit.HBox(column, 6f);
			if (def.Money)
			{
				// Typed in: the amounts are too big to click up to
				UiKit.PushButton(amount, "-", () => StepTarget(def, -1), ButtonFill, ButtonHover, UiColors.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
				UiKit.NumberField(amount, setting.Target.ToString(), UiKit.Unit * 7f, UiKit.Unit * 1.7f, typed =>
				{
					int parsed;
					if (int.TryParse(typed == "" ? "0" : typed, out parsed) && Math.Max(0, Math.Min(def.Max, parsed)) != setting.Target)
					{
						setting.Target = Math.Max(0, Math.Min(def.Max, parsed));
						Changed();
					}
				});
				var sign = UiKit.Label(amount, Game.Symbol(def.Unit) ?? def.Unit, 0.9f, UiColors.Dim, align: TextAlignmentOptions.Center);
				UiKit.Size(sign, UiKit.Unit * 1.5f);
				UiKit.PushButton(amount, "+", () => StepTarget(def, 1), ButtonFill, ButtonHover, UiColors.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
			}
			else
			{
				Stepper(amount, on ? Game.Amount(def.Unit, setting.Target) : "off", on ? UiColors.Text : UiColors.Dim, compact ? (def.IsQuota ? 9f : 6f) : 9f,
					() => StepTarget(def, -1),
					() => StepTarget(def, 1), null, null);
			}

			// The key and the items button, on the right of the amount
			// Beside the amount when there is no key to take room (grenades), else under it in a narrow card
			Transform side = compact ? (def.Bindable ? (Transform)UiKit.HBox(column, 6f) : amount) : UiKit.HBox(top, 6f);
			if (def.Bindable)
			{
				string key = def.HotkeyChoice;
				Pick(side, Hotkeys.Label(key), key != "", () =>
				{
					setting.Hotkey = Hotkeys.Next(key);
					Changed();
				}, "The quick-slot key this goes on. After getting ready, the best one you carry is put on it (only items in the pockets or the tactical vest can be).", UiKit.Unit * 5.5f);
			}

			if (def.Entries.Length > 1)
			{
				bool open = Expanded.Contains(def.Id);
				Pick(side, open ? "Items  ▲" : "Items  ▼", open, () =>
				{
					if (open)
					{
						Expanded.Remove(def.Id);
					}
					else
					{
						Expanded.Add(def.Id);
					}

					Changed();
				}, "Which items count for this and in what order they are used", UiKit.Unit * 5.5f);
			}

			// The places: only when it is wanted
			if (on)
			{
				if (def.Money && !compact)
				{
					UiKit.Size(PlacesPanel(top, def), flexWidth: 1f);
				}
				else
				{
					PlacesPanel(card, def);
				}
			}

			// A kit you carry that a better one in the stash can replace (a fuller copy, a higher-ranked item): per row, so it can be left alone
			// Only where they mean something: not for grenades, the quota or injectors (one each, nothing runs down, nothing is bigger)
			if (on && def.CanOvershoot)
			{
				OptionRow(card, setting.AllowOvershoot, "Go over rather than stay short", "When you are a little short, the stash may only have items bigger than what is missing.\n\nExample: you want 300 HP and carry 250, and the stash has a Salewa (400 HP).\n<b>On:</b> the Salewa is brought, so you end up with 650 HP.\n<b>Off:</b> nothing is brought and you stay 50 HP short.", value => setting.AllowOvershoot = value);
			}

			if (on && def.CanSwap)
			{
				OptionRow(card, setting.ReplaceWorse, "Swap used kits for fuller ones", "A kit you carry that is partly used is swapped for a fuller one from the stash, or for one you rank higher in Items.\n\nExample: you carry a Salewa with 120 HP left and the stash has a full one (400 HP).\n<b>On:</b> the full Salewa replaces the used one.\n<b>Off:</b> you keep the used one, and only the HP you are missing is brought.", value => setting.ReplaceWorse = value);
			}

			if (def.Entries.Length > 1 && Expanded.Contains(def.Id))
			{
				GroupItems(card, def);
			}
		}

		// The items of a group (opened with "Items"): their order (drag a row), limits and switches. What is not used is at the end.
		private static void GroupItems(Transform parent, GroupDef def)
		{
			var box = UiKit.VBox(parent, 4f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge);
			UiKit.Label(box, def.Random ? "Used, in any order" : "Used, best first (drag a row to move it)", 0.8f, UiColors.Dim);
			var entries = def.ShownEntries();
			var rows = new List<RectTransform>();
			bool headed = false;
			foreach (var entry in entries)
			{
				bool off = def.Setting.Off.Contains(entry.Template);
				if (off && !headed)
				{
					headed = true;
					NotUsedHeading(box);
				}

				var row = UiKit.HBox(box, 8f);
				if (!def.Random)
				{
					UiKit.Grip(row);
				}

				TemplateIcon(row, entry.Template, UiKit.Unit * 2f, entry.Name);
				UiKit.Label(row, entry.Name, 0.9f, off ? UiColors.Dim : UiColors.Text, flex: true);
				if (!def.Random)
				{
					MaxControls(row, def, entry);
				}

				string template = entry.Template;
				UsedPick(row, def, template, off);

				rows.Add(row);
			}

			if (!def.Random)
			{
				UiReorder.Attach(box, rows, (from, to) =>
				{
					def.ReorderEntries(entries, from, to);
					Changed();
				});
			}
		}

		// The most of an item to carry for the group: - any +
		private static void MaxControls(Transform row, GroupDef def, Entry entry)
		{
			int max = def.MaxOf(entry.Template);
			string template = entry.Template;
			UiKit.Tip(UiKit.Label(row, "Max", 0.8f, UiColors.Dim), "The most of this item to carry for the group. If nothing else can make up the amount, more are still brought.");
			Stepper(row, max == 0 ? "any" : max.ToString(), UiColors.Text, 3f,
				() =>
				{
					if (max > 0)
					{
						def.SetMax(template, max - 1);
						Changed();
					}
				},
				() =>
				{
					if (max < 20)
					{
						def.SetMax(template, max + 1);
						Changed();
					}
				}, null, null);
		}
	}
}
