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

		private static RectTransform GroupCard(Transform parent, GroupDef def, bool compact)
		{
			var setting = def.Setting;
			var active = def.ActiveEntries();
			bool on = setting.Target > 0;
			var card = UiKit.VBox(parent, 8f, new RectOffset(10, 10, 8, 8), UiKit.CardColor, UiKit.CardEdge);
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
				var mark = UiKit.Label(top.GetChild(top.childCount - 1), "?", 1.1f, Ui.Dim, align: TextAlignmentOptions.Center);
				UiKit.Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);
			}

			var column = UiKit.VBox(top, 4f);
			UiKit.Size(column, flexWidth: 1f);
			UiKit.Label(column, def.Name, 0.95f, on ? Ui.Heading : Ui.Dim);
			var amount = UiKit.HBox(column, 6f);
			if (def.Money)
			{
				// Typed in, which is the Cash section's: not built here yet
				UiKit.Label(amount, Game.Amount(def.Unit, setting.Target), 0.9f, Ui.Text);
			}
			else
			{
				Stepper(amount, on ? Game.Amount(def.Unit, setting.Target) : "off", on ? Ui.Text : Ui.Dim, compact ? (def.IsQuota ? 9f : 6f) : 9f,
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
					}, null, null);
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
				PlacesPanel(card, def);
			}

			if (def.Entries.Length > 1 && Expanded.Contains(def.Id))
			{
				GroupItems(card, def);
			}

			return card;
		}

		// The items of a group (opened with "Items"): their order (drag a row), limits and switches. What is not used is at the end.
		private static void GroupItems(Transform parent, GroupDef def)
		{
			var box = UiKit.VBox(parent, 4f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge);
			UiKit.Label(box, def.Random ? "Used, in any order" : "Used, best first (drag a row to move it)", 0.8f, Ui.Dim);
			var entries = ReadyWindow.ShownEntries(def);
			var rows = new List<RectTransform>();
			foreach (var entry in entries)
			{
				bool off = def.Setting.Off.Contains(entry.Template);
				var row = UiKit.HBox(box, 8f);
				if (!def.Random)
				{
					UiKit.Grip(row);
				}

				TemplateIcon(row, entry.Template, UiKit.Unit * 2f, entry.Name);
				UiKit.Label(row, off ? "× " + entry.Name : entry.Name, 0.9f, off ? Ui.Dim : Ui.Text, flex: true);
				if (!def.Random)
				{
					MaxControls(row, def, entry);
				}

				string template = entry.Template;
				Pick(row, off ? "Not used" : "Used", !off, () =>
				{
					def.ToggleEntry(template);
					Changed();
				}, null, UiKit.Unit * 6f);
				rows.Add(row);
			}

			if (!def.Random)
			{
				UiReorder.Attach(box, rows, (from, to) =>
				{
					ReadyWindow.ReorderEntries(def, entries, from, to);
					Changed();
				});
			}
		}

		// The most of an item to carry for the group: - any +
		private static void MaxControls(Transform row, GroupDef def, Entry entry)
		{
			int max = def.MaxOf(entry.Template);
			string template = entry.Template;
			UiKit.Tip(UiKit.Label(row, "Max", 0.8f, Ui.Dim), "The most of this item to carry for the group. If nothing else can make up the amount, more are still brought.");
			Stepper(row, max == 0 ? "any" : max.ToString(), Ui.Text, 3f,
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
