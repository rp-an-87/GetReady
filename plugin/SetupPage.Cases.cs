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
		private const int MapsPerRow = 4;

		private static void CasesSetup(Transform section, InventoryController controller)
		{
			var settings = SettingsStore.Current.Cases;
			OptionRow(section, settings.AutoUpdate, "Keep tracked items up to date when I move things", "What you put in or take out of a case you track, by hand in the stash.\n<b>On:</b> it is added to or removed from what the case tracks.\n<b>Off:</b> what is tracked stays as you set it.\n\nWhat Get ready itself moves is never tracked, and neither are the cases you never tracked.", value => settings.AutoUpdate = value);
			OptionRow(section, settings.MapWarnings, "Warn on the map screen about cases for another map", "The map screen, when you pick a map.\n<b>On:</b> a red bar shows the cases you carry that are set to other maps, and the cases set for this map that are not on you.\n<b>Off:</b> no bar.", value => settings.MapWarnings = value);
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

		// An amount for a small corner: 1500 is 1.5k, 100000 is 100k, 2500000 is 2.5M
		private static string Compact(int amount)
		{
			var culture = System.Globalization.CultureInfo.InvariantCulture;
			if (amount >= 1000000)
			{
				return (amount / 1000000.0).ToString("0.#", culture) + "M";
			}

			return amount >= 1000 ? (amount / 1000.0).ToString("0.#", culture) + "k" : amount.ToString(culture);
		}

		// The game's own red cross (a copy of its window's close button) for clearing a case; a plain x when the window has none
		private static void CrossButton(Transform parent, System.Action click, string tip)
		{
			var template = ReadyUiWindow.CloseTemplate;
			if (template == null)
			{
				SquareButton(parent, "x", click, tip);
				return;
			}

			var copy = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
			copy.name = "Cross";
			var button = copy.GetComponent<UnityEngine.UI.Button>();
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() => click());
			UiKit.Size(copy.transform, width: UiKit.Unit * 1.7f, height: UiKit.Unit * 1.7f);
			UiKit.Tip(button, tip);
		}

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
		private static RectTransform StripIcon(Transform row, string template, string tip, int count, string label, string amountText = null)
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
				var amount = UiKit.Label(frame, amountText ?? count.ToString(), 0.6f, new Color(0.55f, 0.9f, 0.45f), bold: true, align: TextAlignmentOptions.BottomRight);
				UiKit.Stretch(amount.rectTransform, 0f, 0f, 4f, 2f);
			}

			return (RectTransform)frame;
		}

		// A colour laid over a picture, out of the mouse's way
		private static GameObject Tint(Transform frame, Color color)
		{
			var rect = UiKit.Node("Tint", frame);
			UiKit.Stretch(rect, 0f, 0f, 0f, 0f);
			var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
			image.color = color;
			image.raycastTarget = false;
			rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
			return rect.gameObject;
		}

		// What a case tracks, as pictures that use the whole width of the panel before going on to another row. Returns what shows what Update would
		// do while the mouse is on its button (items it would add in green, ones it would remove in red); null when there is nothing to show.
		private static System.Action<bool> TrackedStrip(Transform parent, CaseEntry entry, CasesStep.CaseChange change)
		{
			var panel = UiKit.VBox(parent, 4f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge);
			var shown = (entry.Exact ? entry.Items.OrderBy(held => held.Grid).ThenBy(held => held.Y).ThenBy(held => held.X) : entry.Items.OrderBy(held => held.Template)).ToList();
			var money = CasesStep.Currencies.Where(currency => entry.Money.ContainsKey(currency)).ToList();
			bool anyAdded = change != null && change.Added.Count > 0;
			if (shown.Count == 0 && money.Count == 0)
			{
				EmptyNote(panel, "Tracking nothing: getting ready empties this case.");
				if (!anyAdded)
				{
					return null;
				}
			}

			// A grid of fixed cells that wraps by the width it has: no count per row to guess
			var cells = UiKit.Node("Strip", panel);
			var grid = cells.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
			grid.cellSize = new Vector2(UiKit.Unit * 2.4f, UiKit.Unit * 2.4f);
			grid.spacing = new Vector2(6f, 6f);
			grid.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
			grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
			grid.childAlignment = TextAnchor.UpperLeft;
			grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.Flexible;
			var removed = new HashSet<string>(change != null ? change.Removed.Select(item => item.Template) : new string[0]);
			var previews = new List<GameObject>();
			foreach (var held in shown)
			{
				var frame = StripIcon(cells, held.Template, held.Count > 1 ? $"{held.Name} x{held.Count}" : held.Name, held.Count, held.Name);
				if (removed.Contains(held.Template))
				{
					var red = Tint(frame, new Color(0.9f, 0.2f, 0.18f, 0.45f));
					red.SetActive(false);
					previews.Add(red);
				}
			}

			foreach (var currency in money)
			{
				StripIcon(cells, CasesStep.TemplateOfCurrency(currency), Game.Amount(currency, entry.Money[currency]), entry.Money[currency], currency, Compact(entry.Money[currency]));
			}

			// What Update would add: shown only while the mouse is on the button, in green
			if (anyAdded)
			{
				foreach (var added in change.Added)
				{
					var frame = StripIcon(cells, added.Template, $"Would add {added.Name}" + (added.Count > 1 ? $" x{added.Count}" : ""), added.Count, added.Name);
					Tint(frame, new Color(0.25f, 0.8f, 0.3f, 0.4f));
					frame.gameObject.SetActive(false);
					previews.Add(frame.gameObject);
				}
			}

			return previews.Count == 0 ? null : (System.Action<bool>)(show =>
			{
				foreach (var preview in previews)
				{
					if (preview != null)
					{
						preview.SetActive(show);
					}
				}
			});
		}

		// One case: its name, tag and icon, the buttons for its tracked items, what it holds, and its switches and maps. item is null for a saved
		// case that is not on you: it only shows what is saved (and the cross to clear it), it is set up when you carry it. A case on you can be
		// collapsed to the same; it stays collapsed until you open it, whatever you do with the case.
		private static void CaseCard(Transform parent, CompoundItem item, CaseEntry entry)
		{
			string id = item != null ? item.Id.ToString() : entry.CaseId;
			bool collapsed = item == null || CasesStep.IsCollapsed(id);
			var card = UiKit.VBox(parent, 8f, new RectOffset(10, 10, 10, 10), UiKit.CardColor, UiKit.CardEdge);
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
			// What Update would change in what is tracked, for the button and the strip
			var change = recorded && item != null ? CasesStep.Diff(item, entry) : null;
			UnityEngine.UI.Button trackButton = null;
			int count = recorded ? CasesStep.ItemCount(entry) : item != null ? item.Grids.Sum(grid => grid.Items.Count()) : 0;
			var title = UiKit.Label(header, $"{name}  -  {ItemsText(count)}", 0.9f, Ui.Text, flex: true);
			title.overflowMode = TextOverflowModes.Ellipsis;
			UiKit.Size(title, flexWidth: 1f).minWidth = 0f;
			// The cross at the end of the header clears everything saved for the case, so it leaves the list
			if (entry != null)
			{
				CrossButton(header, () =>
				{
					CasesStep.Clear(entry.CaseId);
					ReadyPlan._status = $"Cleared everything saved for {name}.";
					Changed();
				}, "Clear everything saved for this case: what it tracks, its maps and Skip cash step (nothing moves)");
			}

			if (!collapsed)
			{
				// The two buttons have a row of their own, so a narrow window does not squeeze them into the header
				var buttons = UiKit.HBox(card, 8f);
				buttons.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
				string trackText = recorded ? "Update tracked items" : "Track items inside";
				if (change != null && change.AddedCount > 0)
				{
					trackText += $" <color=#7fd36b>(+{change.AddedCount})</color>";
				}

				if (change != null && change.RemovedCount > 0)
				{
					trackText += $" <color=#ec6a5f>(-{change.RemovedCount})</color>";
				}

				var track = UiKit.PushButton(buttons, trackText, () =>
				{
					var saved = CasesStep.Record(item);
					ReadyPlan._status = $"Tracking {ItemsText(CasesStep.ItemCount(saved))}{(saved.Money.Count > 0 ? " and " + MoneyText(saved) : "")} in {name}.";
					Changed();
				}, ButtonFill, ButtonHover, Ui.Text, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
				UiKit.Size(track, width: 1f, flexWidth: 1f).minWidth = 0f;
				trackButton = track;
				UiKit.Tip(track, "Remembers what this case holds now, and where each item sits.\n\nGetting ready then brings back what is missing and, with <b>Take out the rest</b>, takes out everything else. Other cases are left alone.");
				if (recorded)
				{
					var stop = UiKit.PushButton(buttons, "Stop tracking items", () =>
					{
						CasesStep.Forget(entry.CaseId);
						ReadyPlan._status = $"Stopped tracking items in {name}.";
						Changed();
					}, ButtonFill, ButtonHover, Ui.Dim, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
					UiKit.Size(stop, width: 1f, flexWidth: 1f).minWidth = 0f;
					UiKit.Tip(stop, "Stops tracking what is in this case (nothing moves). Its maps and the cash switch stay.");
				}
			}

			// What is saved, for a collapsed card (open, the strip and the buttons below say it)
			if (entry != null && collapsed)
			{
				UiKit.Label(card, "Saved: " + CasesStep.Summary(entry), 0.8f, Ui.Dim, wrap: true, flex: true);
			}

			if (collapsed)
			{
				return;
			}

			if (recorded)
			{
				var preview = TrackedStrip(card, entry, change);
				if (preview != null && trackButton != null)
				{
					trackButton.gameObject.AddComponent<HoverRelay>().OnHover = preview;
				}
			}

			var options = UiKit.VBox(card, 6f, new RectOffset(8, 8, 6, 8), InnerPanel, UiKit.InnerEdge);
			UiKit.Label(options, "Options:", 0.8f, Ui.Dim);
			var switches = UiKit.HBox(options, 8f);
			bool stops = entry != null && entry.StopsCash;
			Pick(switches, "Skip cash step", stops, () =>
			{
				var target = entry ?? CasesStep.Touch(item);
				target.StopsCash = !target.StopsCash;
				CasesStep.Prune(target);
				Changed();
			}, "For a case that has the cash it needs, or is for something else.\n<b>On:</b> the Cash step is skipped while you carry this case.\n<b>Off:</b> the Cash step works as usual.\n\nA case that tracks money skips the step on its own.");
			if (recorded)
			{
				Pick(switches, "Take out the rest", entry.TakeOutRest, () =>
				{
					entry.TakeOutRest = !entry.TakeOutRest;
					Changed();
				}, "What the case holds that is not tracked.\n<b>On:</b> it is taken out to the stash, money included (a tracked amount keeps that much, the extra goes out).\n<b>Off:</b> it stays where it is; only what is missing is brought.");
				Pick(switches, "Exact positions", entry.Exact, () =>
				{
					entry.Exact = !entry.Exact;
					Changed();
				}, "Where the tracked items sit in the case.\n<b>On:</b> each item goes back to the cell it was in when tracked; a wrong item in a cell goes to the stash (what an injector case needs).\n<b>Off:</b> only the type and the count of each item matter.");
			}

			// The maps it is for: none picked is any map
			var mapsPanel = UiKit.VBox(card, 6f, new RectOffset(8, 8, 6, 8), InnerPanel, UiKit.InnerEdge);
			UiKit.Label(mapsPanel, entry != null && entry.Maps.Count > 0 ? "For these maps only:" : "For any map (pick the maps it is for):", 0.8f, Ui.Dim);
			for (int first = 0; first < MapTags.Maps.Length; first += MapsPerRow)
			{
				var row = UiKit.HBox(mapsPanel, 6f);
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

	// Tells a function when the mouse comes onto and leaves an object (what Update would do, shown while it is over its button)
	internal class HoverRelay : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
	{
		internal System.Action<bool> OnHover;

		public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) => OnHover?.Invoke(true);

		public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => OnHover?.Invoke(false);
	}
}
