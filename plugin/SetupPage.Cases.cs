using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using EFT.UI;
using TMPro;
using UnityEngine;

namespace GetReady
{
	// The Cases section of the Setup page: every container you carry (a SICC, a docs case, a keytool, a wallet, an injector case), with what you
	// save for it: its template, whether the cash step is off while you carry it, exact positions, taking out the rest, and the maps it is
	// for. A case with nothing set is not kept.
	internal static partial class SetupPage
	{
		private const int MapsPerRow = 4, StripPerRow = 10;

		private static void CasesSetup(Transform section, InventoryController controller)
		{
			var settings = SettingsStore.Current.Cases;
			OptionRow(section, settings.AutoUpdate, "Keep tracked items up to date when I move things", "Items you put in or take out of a case you track, by hand in the stash, are added to or removed from what is tracked. What Get ready itself moves is not tracked, and neither are the cases you never tracked.", value => settings.AutoUpdate = value);
			OptionRow(section, settings.MapWarnings, "Warn on the map screen about cases for another map", "A red bar over the map screen shows the cases you carry that are set to other maps than the one you picked, and the ones set for it that are not on you", value => settings.MapWarnings = value);
			if (controller == null)
			{
				UiKit.Label(section, "Open this in the stash to set up the cases you carry.", 0.85f, Ui.Dim);
				return;
			}

			var cases = CasesStep.FindCases(controller.Inventory);
			var ids = cases.Select(item => item.Id.ToString()).ToList();
			var onYou = cases.OrderBy(item => CasesStep.EntryOf(item.Id.ToString()) != null ? 0 : 1).ToList();
			var away = settings.Cases.Where(entry => !ids.Contains(entry.CaseId)).ToList();
			TwoSections(section, "ON YOU", "No case on you (pockets, vest, backpack or pouch).", onYou.Count > 0, box =>
			{
				foreach (var item in onYou)
				{
					CaseCard(box, item, CasesStep.EntryOf(item.Id.ToString()));
				}
			}, "SAVED, NOT ON YOU (set up when you carry them; the x clears a case)", "Cases you set up and are not carrying will appear here.", away.Count > 0, box =>
			{
				foreach (var entry in away.ToList())
				{
					CaseCard(box, null, entry);
				}
			});

			if (settings.Cases.Count == 0)
			{
				UiKit.Label(section, "Nothing tracked yet. Fill a case the way you like it, then press \"Track items inside\" (an empty case works too: it is kept empty).", 0.85f, Ui.Dim, wrap: true, flex: true);
			}
		}

		private static string MoneyText(CaseEntry entry) =>
			string.Join("   ", CasesStep.Currencies.Where(currency => entry.Money.ContainsKey(currency)).Select(currency => Game.Amount(currency, entry.Money[currency])));

		private static string ItemsText(int count) => count == 1 ? "1 item" : $"{count} items";

		// The name of a saved case that is not on you: its type
		private static string CaseName(CaseEntry entry)
		{
			var name = string.IsNullOrEmpty(entry.CaseTemplate) ? "" : Game.TemplateName(entry.CaseTemplate);
			return string.IsNullOrEmpty(name) || name.EndsWith(" ShortName") || name.EndsWith(" Name") ? entry.CaseName ?? "" : name;
		}

		// A small square button (the + - x of a case's header)
		private static void SquareButton(Transform parent, string text, System.Action click, string tip)
		{
			var button = UiKit.PushButton(parent, text, click, ButtonFill, ButtonHover, Ui.Text, width: UiKit.Unit * 1.7f, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
			button.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
			UiKit.Tip(button, tip);
		}

		// An item tag as the game draws it on the item: the tag's colour as a badge with its name on it
		private static void TagBadge(Transform parent, string name, int colorIndex)
		{
			var color = EditTagWindow.GetColor(colorIndex);
			bool light = (0.299f * color.r + 0.587f * color.g + 0.114f * color.b) > 0.55f;
			var box = UiKit.HBox(parent, 0f, new RectOffset(8, 8, 2, 2), color);
			UiKit.Label(box, name, 0.75f, light ? new Color(0.08f, 0.08f, 0.08f) : Color.white, bold: true, align: TextAlignmentOptions.Center);
		}

		// An item picture with its amount in the corner and, when asked, its short name at the top, as the game draws a stack
		private static void StripIcon(Transform row, string template, string tip, int count, string label)
		{
			TemplateIcon(row, template, UiKit.Unit * 2.4f, tip);
			var frame = row.GetChild(row.childCount - 1);
			if (!string.IsNullOrEmpty(label))
			{
				var name = UiKit.Label(frame, label, 0.45f, new Color(0.92f, 0.92f, 0.88f), align: TextAlignmentOptions.TopRight);
				name.overflowMode = TextOverflowModes.Truncate;
				UiKit.Stretch(name.rectTransform, 1f, 1f, 3f, 2f);
			}

			if (count > 1)
			{
				var amount = UiKit.Label(frame, count.ToString(), 0.6f, new Color(0.55f, 0.9f, 0.45f), bold: true, align: TextAlignmentOptions.BottomRight);
				UiKit.Stretch(amount.rectTransform, 0f, 0f, 4f, 2f);
			}
		}

		// What a case tracks, as rows of pictures
		private static void TrackedStrip(Transform parent, CaseEntry entry)
		{
			var panel = UiKit.VBox(parent, 4f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge);
			var shown = (entry.Exact ? entry.Items.OrderBy(held => held.Grid).ThenBy(held => held.Y).ThenBy(held => held.X) : entry.Items.OrderBy(held => held.Template)).ToList();
			var pieces = new List<System.Action<Transform>>();
			foreach (var held in shown)
			{
				var kept = held;
				pieces.Add(row => StripIcon(row, kept.Template, kept.Count > 1 ? $"{kept.Name} x{kept.Count}" : kept.Name, kept.Count, kept.Name));
			}

			foreach (var currency in CasesStep.Currencies.Where(currency => entry.Money.ContainsKey(currency)))
			{
				string code = currency;
				pieces.Add(row => StripIcon(row, CasesStep.TemplateOfCurrency(code), Game.Amount(code, entry.Money[code]), entry.Money[code], null));
			}

			if (pieces.Count == 0)
			{
				EmptyNote(panel, "Tracking nothing: getting ready empties this case.");
				return;
			}

			for (int first = 0; first < pieces.Count; first += StripPerRow)
			{
				var row = UiKit.HBox(panel, 6f);
				foreach (var piece in pieces.Skip(first).Take(StripPerRow))
				{
					piece(row);
				}
			}
		}

		// One case: its name, tag and icon, the buttons for its tracked items, what it holds, and its switches and maps. item is null for a saved
		// case that is not on you: it only shows what is saved (and the cross to clear it), it is set up when you carry it. A case on you can be
		// collapsed to the same; it stays collapsed until you open it, whatever you do with the case.
		private static void CaseCard(Transform parent, CompoundItem item, CaseEntry entry)
		{
			string id = item != null ? item.Id.ToString() : entry.CaseId;
			bool collapsed = item == null || CasesStep.IsCollapsed(id);
			var card = UiKit.VBox(parent, 6f, new RectOffset(10, 10, 8, 8), UiKit.CardColor, UiKit.CardEdge);
			var header = UiKit.HBox(card, 8f);
			if (item != null)
			{
				SquareButton(header, collapsed ? "+" : "-", () =>
				{
					CasesStep.ToggleCollapsed(id);
					Changed();
				}, collapsed ? "Open this case" : "Collapse this case (it stays collapsed until you open it)");
			}

			string name = item != null ? Game.NameOf(item) : CaseName(entry);
			if (item != null)
			{
				ItemIcon(header, item, UiKit.Unit * 2.6f, name);
			}
			else
			{
				TemplateIcon(header, entry.CaseTemplate, UiKit.Unit * 2.6f, name);
			}

			var tag = item?.GetItemComponent<TagComponent>();
			string tagName = item != null ? tag?.Name : entry.Tag;
			if (!string.IsNullOrEmpty(tagName))
			{
				TagBadge(header, tagName, item != null ? tag.Color : entry.TagColor);
			}

			bool recorded = entry != null && entry.Recorded;
			int count = recorded ? CasesStep.ItemCount(entry) : item != null ? item.Grids.Sum(grid => grid.Items.Count()) : 0;
			UiKit.Label(header, $"{name}  -  {ItemsText(count)}" + (recorded ? "  -  tracked" : ""), 0.9f, Ui.Text, flex: true);
			if (!collapsed)
			{
				var track = UiKit.PushButton(header, recorded ? "Update tracked items" : "Track items inside", () =>
				{
					var saved = CasesStep.Record(item);
					ReadyPlan._status = $"Tracking {ItemsText(CasesStep.ItemCount(saved))}{(saved.Money.Count > 0 ? " and " + MoneyText(saved) : "")} in {name}.";
					Changed();
				}, ButtonFill, ButtonHover, Ui.Text, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
				UiKit.Tip(track, "Remembers what this case holds now (and where each item sits): getting ready brings back what is missing and, with \"Take out the rest\", takes out everything else. Other cases are left alone.");
				if (recorded)
				{
					var stop = UiKit.PushButton(header, "Stop tracking items", () =>
					{
						CasesStep.Forget(entry.CaseId);
						ReadyPlan._status = $"Stopped tracking items in {name}.";
						Changed();
					}, ButtonFill, ButtonHover, Ui.Text, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
					UiKit.Tip(stop, "Stops tracking what is in this case (nothing moves). Its maps and the cash switch stay.");
				}
			}

			// The cross at the end of the header clears everything saved for the case, so it leaves the list
			if (entry != null)
			{
				SquareButton(header, "x", () =>
				{
					CasesStep.Clear(entry.CaseId);
					ReadyPlan._status = $"Cleared everything saved for {name}.";
					Changed();
				}, "Clear everything saved for this case: what it tracks, its maps and Skip cash step (nothing moves)");
			}

			if (entry != null)
			{
				UiKit.Label(card, "Saved: " + CasesStep.Summary(entry), 0.8f, Ui.Dim, wrap: true, flex: true);
			}

			if (collapsed)
			{
				return;
			}

			if (recorded)
			{
				TrackedStrip(card, entry);
			}

			var switches = UiKit.HBox(card, 8f);
			bool stops = entry != null && entry.StopsCash;
			Pick(switches, "Skip cash step", stops, () =>
			{
				var target = entry ?? CasesStep.Touch(item);
				target.StopsCash = !target.StopsCash;
				CasesStep.Prune(target);
				Changed();
			}, stops ? "While you carry this case the Cash step is skipped. Click to let it work again." : "Click to skip the Cash step while you carry this case (it has what it needs, or it is for something else). A case that tracks money does the same.");
			if (recorded)
			{
				Pick(switches, "Take out the rest", entry.TakeOutRest, () =>
				{
					entry.TakeOutRest = !entry.TakeOutRest;
					Changed();
				}, entry.TakeOutRest ? "What is not tracked is taken out to the stash, money included (a tracked amount keeps that much, the extra goes out). Click to leave it where it is (only what is missing is brought)." : "Nothing is taken out. Click to take out what is not tracked, money included.");
				Pick(switches, "Exact positions", entry.Exact, () =>
				{
					entry.Exact = !entry.Exact;
					Changed();
				}, entry.Exact ? "Each item goes back to the cell it was in when tracked; a wrong item in a cell goes to the stash. Click to match by type and count only." : "Only the type and the count of each item matter. Click to put each item back in the cell it was in when tracked (what the injector case needs).");
			}

			// The maps it is for: none picked is any map
			UiKit.Label(card, entry != null && entry.Maps.Count > 0 ? "For these maps only:" : "For any map (pick the maps it is for):", 0.8f, Ui.Dim);
			for (int first = 0; first < MapTags.Maps.Length; first += MapsPerRow)
			{
				var row = UiKit.HBox(card, 6f);
				row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
				var maps = MapTags.Maps.Skip(first).Take(MapsPerRow).ToList();
				foreach (var map in maps)
				{
					string mapName = map.Name;
					bool on = entry != null && entry.Maps.Contains(mapName);
					Pick(row, mapName, on, () =>
					{
						var target = entry ?? CasesStep.Touch(item);
						if (on)
						{
							target.Maps.Remove(mapName);
						}
						else
						{
							target.Maps.Add(mapName);
						}

						CasesStep.Prune(target);
						Changed();
					}, null);
					// The same width asked for and the room shared out equally, so the buttons are the same width
					UiKit.Size(row.GetChild(row.childCount - 1), width: 1f, flexWidth: 1f).minWidth = 0f;
				}

				// A short last row keeps the width of the buttons above it
				for (int fill = maps.Count; fill < MapsPerRow; fill++)
				{
					UiKit.Size(UiKit.Node("Empty", row), flexWidth: 1f).minWidth = 0f;
				}
			}
		}
	}
}
