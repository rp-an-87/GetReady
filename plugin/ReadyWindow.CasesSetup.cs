using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;
using static GetReady.ReadyPlan;
using static GetReady.ReadyRun;

namespace GetReady
{
	// The Setup card of the Cases step: every container you carry (a SICC, a docs case, a keytool, a wallet, an injector case), with what you
	// save for it: its template, whether the cash step is off while you carry it, exact positions, taking out the rest, exact money, and the
	// maps it is for. A case with nothing set is not kept.
	internal static partial class ReadyWindow
	{
		private const int MapsPerRow = 5;

		// The width of the panels that show a template: room for the most items any template holds, and for the "Saved empty" text
		internal static float PanelWidth(IEnumerable<CaseEntry> templates) =>
			Mathf.Max(230f, templates.Select(saved => saved.Items.Count + saved.Money.Count).DefaultIfEmpty(0).Max() * 46f + 24f);

		private static float _panelWidth = 230f;

		private static GUIStyle _cross;

		// The cross of the old map rules (square, not bold), as tall as the buttons it stands beside
		private static GUIStyle Cross
		{
			get
			{
				if (_cross == null)
				{
					_cross = new GUIStyle(Ui.Small)
					{
						fontStyle = FontStyle.Normal,
						fixedHeight = Ui.Button.CalcHeight(new GUIContent("Update tracked items"), 170f),
						margin = new RectOffset(Ui.Button.margin.left, Ui.Button.margin.right, Ui.Button.margin.top, Ui.Button.margin.bottom)
					};
				}

				return _cross;
			}
		}

		private static bool CasesSetup(InventoryController controller)
		{
			var settings = SettingsStore.Current.Cases;
			bool changed = false;
			bool update = OptionRow(settings.AutoUpdate, "Keep tracked items up to date when I move things", "Items you put in or take out of a case you track, by hand in the stash, are added to or removed from what is tracked. What Get ready itself moves is not tracked, and neither are the cases you never tracked.");
			bool warn = OptionRow(settings.MapWarnings, "Warn on the map screen about cases for another map", "A red bar over the map screen shows the cases you carry that are set to other maps than the one you picked, and the ones set for it that are not on you");
			if (update != settings.AutoUpdate || warn != settings.MapWarnings)
			{
				settings.AutoUpdate = update;
				settings.MapWarnings = warn;
				changed = true;
			}

			if (controller == null)
			{
				GUILayout.Label("Open this in the stash to set up the cases you carry.", Ui.Muted);
				return changed;
			}

			_panelWidth = PanelWidth(settings.Cases);
			var cases = CasesStep.FindCases(controller.Inventory);
			var ids = cases.Select(item => item.Id.ToString()).ToList();
			var onYou = cases.OrderBy(item => CasesStep.EntryOf(item.Id.ToString()) != null ? 0 : 1).ToList();
			var away = settings.Cases.Where(entry => !ids.Contains(entry.CaseId)).ToList();
			bool pressed = false;
			GUILayout.Space(4);
			TwoSections("cases", "ON YOU", "No case on you (pockets, vest, backpack or pouch).", onYou.Count > 0, () =>
			{
				foreach (var item in onYou)
				{
					pressed |= CaseCard(item, CasesStep.EntryOf(item.Id.ToString()));
				}
			}, "SAVED, NOT ON YOU (set up when you carry them; the x clears a case)", "Cases you set up and are not carrying will appear here.", away.Count > 0, () =>
			{
				foreach (var entry in away.ToList())
				{
					pressed |= CaseCard(null, entry);
				}
			});

			if (settings.Cases.Count == 0)
			{
				GUILayout.Label("Nothing tracked yet. Fill a case the way you like it, then press \"Track items inside\" (an empty case works too: it is kept empty).", Ui.Muted);
			}

			return changed | pressed;
		}

		private static string MoneyText(CaseEntry entry) =>
			string.Join("   ", CasesStep.Currencies.Where(currency => entry.Money.ContainsKey(currency)).Select(currency => Game.Amount(currency, entry.Money[currency])));

		private static string ItemsText(int count) => count == 1 ? "1 item" : $"{count} items";

		// A switch with its tooltip on one button, for the row under a case
		private static bool CaseSwitch(string text, string tooltip, bool on, float width)
		{
			return GUILayout.Button(new GUIContent(text, tooltip), on ? Ui.PickOn : Ui.Pick, GUILayout.Width(width));
		}

		// One case: its name, tag and icon, the buttons for its tracked items, what it holds, and its switches and maps. item is null for a saved
		// case that is not on you: it only shows what is saved (and the cross to clear it), it is set up when you carry it. A case on you can be
		// collapsed to the same; it stays collapsed until you open it, whatever you do with the case.
		private static bool CaseCard(CompoundItem item, CaseEntry entry)
		{
			bool changed = false;
			string id = item != null ? item.Id.ToString() : entry.CaseId;
			bool collapsed = item == null || CasesStep.IsCollapsed(id);
			GUILayout.BeginVertical(Ui.Group);
			GUILayout.BeginHorizontal();
			if (item != null && CenteredButton(new GUIContent(collapsed ? "+" : "-", collapsed ? "Open this case" : "Collapse this case (it stays collapsed until you open it)"), Cross, 28))
			{
				CasesStep.ToggleCollapsed(id);
				collapsed = !collapsed;
			}

			var template = item != null ? item.TemplateId.ToString() : entry.CaseTemplate;
			var name = item != null ? Game.NameOf(item) : CaseName(entry);
			if (item != null)
			{
				Icons.Slot(item, name, 40);
			}
			else
			{
				Icons.Slot(template, name, 40);
			}

			GUILayout.Space(8);
			var tag = item?.GetItemComponent<TagComponent>();
			string tagName = item != null ? tag?.Name : entry.Tag;
			if (!string.IsNullOrEmpty(tagName))
			{
				Centered(() => TagChip.Draw(tagName, item != null ? tag.Color : entry.TagColor));
				GUILayout.Space(8);
			}

			bool recorded = entry != null && entry.Recorded;
			int count = recorded ? CasesStep.ItemCount(entry) : item != null ? item.Grids.Sum(grid => grid.Items.Count()) : 0;
			CenteredLabel($"{name}  -  {ItemsText(count)}" + (recorded ? "  -  tracked" : ""), Ui.Plain);
			GUILayout.FlexibleSpace();
			if (!collapsed)
			{
				if (CenteredButton(new GUIContent(recorded ? "Update tracked items" : "Track items inside", "Remembers what this case holds now (and where each item sits): getting ready brings back what is missing and, with \"Take out the rest\", takes out everything else. Other cases are left alone."), Ui.Button, 170))
				{
					var saved = CasesStep.Record(item);
					_status = $"Tracking {ItemsText(CasesStep.ItemCount(saved))}{(saved.Money.Count > 0 ? " and " + MoneyText(saved) : "")} in {name}.";
					RefreshSoon();
					changed = true;
				}

				if (recorded && CenteredButton(new GUIContent("Stop tracking items", "Stops tracking what is in this case (nothing moves). Its maps and the cash switch stay."), Ui.Button, 170))
				{
					CasesStep.Forget(entry.CaseId);
					_status = $"Stopped tracking items in {name}.";
					RefreshSoon();
					changed = true;
				}
			}

			// The cross at the end of the header clears everything saved for the case, so it leaves the list
			if (entry != null && CenteredButton(new GUIContent("x", "Clear everything saved for this case: what it tracks, its maps and Skip cash step (nothing moves)"), Cross, 28))
			{
				CasesStep.Clear(entry.CaseId);
				_status = $"Cleared everything saved for {name}.";
				RefreshSoon();
				changed = true;
			}

			GUILayout.EndHorizontal();

			if (entry != null)
			{
				GUILayout.Label("Saved: " + CasesStep.Summary(entry), Ui.Muted);
			}

			if (collapsed)
			{
				GUILayout.EndVertical();
				return changed;
			}

			if (recorded)
			{
				var shown = entry.Exact ? entry.Items.OrderBy(held => held.Grid).ThenBy(held => held.Y).ThenBy(held => held.X) : entry.Items.OrderBy(held => held.Template);
				GUILayout.BeginHorizontal();
				GUILayout.BeginVertical(Ui.Inner, GUILayout.Width(_panelWidth));
				var money = CasesStep.Currencies.Where(currency => entry.Money.ContainsKey(currency))
					.Select(currency => new StripItem { Template = CasesStep.TemplateOfCurrency(currency), Name = Game.Amount(currency, entry.Money[currency]), Count = entry.Money[currency], Label = Game.TemplateName(CasesStep.TemplateOfCurrency(currency)) });
				TemplateStrip(shown.Select(held => new StripItem { Template = held.Template, Name = held.Count > 1 ? $"{held.Name} x{held.Count}" : held.Name, Count = held.Count, Label = held.Name }).Concat(money),
					"Tracking nothing: getting ready empties this case.");

				GUILayout.EndVertical();
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}

			GUILayout.BeginHorizontal();
			bool stops = entry != null && entry.StopsCash;
			if (CaseSwitch("Skip cash step", stops ? "While you carry this case the Cash step is skipped. Click to let it work again." : "Click to skip the Cash step while you carry this case (it has what it needs, or it is for something else). A case that tracks money does the same.", stops, 130))
			{
				var target = entry ?? CasesStep.Touch(item);
				target.StopsCash = !target.StopsCash;
				CasesStep.Prune(target);
				SettingsStore.Save();
				RefreshSoon();
				changed = true;
			}

			if (recorded)
			{
				if (CaseSwitch("Take out the rest", entry.TakeOutRest ? "What is not tracked is taken out to the stash, money included (a tracked amount keeps that much, the extra goes out). Click to leave it where it is (only what is missing is brought)." : "Nothing is taken out. Click to take out what is not tracked, money included.", entry.TakeOutRest, 140))
				{
					entry.TakeOutRest = !entry.TakeOutRest;
					SettingsStore.Save();
					RefreshSoon();
					changed = true;
				}

				if (CaseSwitch("Exact positions", entry.Exact ? "Each item goes back to the cell it was in when tracked; a wrong item in a cell goes to the stash. Click to match by type and count only." : "Only the type and the count of each item matter. Click to put each item back in the cell it was in when tracked (what the injector case needs).", entry.Exact, 130))
				{
					entry.Exact = !entry.Exact;
					SettingsStore.Save();
					RefreshSoon();
					changed = true;
				}
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			// The maps it is for: none picked is any map
			GUILayout.Label(entry != null && entry.Maps.Count > 0 ? "For these maps only:" : "For any map (pick the maps it is for):", Ui.Muted);
			for (int first = 0; first < MapTags.Maps.Length; first += MapsPerRow)
			{
				GUILayout.BeginHorizontal();
				foreach (var map in MapTags.Maps.Skip(first).Take(MapsPerRow))
				{
					bool on = entry != null && entry.Maps.Contains(map.Name);
					if (GUILayout.Button(map.Name, on ? Ui.PickOn : Ui.Pick))
					{
						var target = entry ?? CasesStep.Touch(item);
						if (on)
						{
							target.Maps.Remove(map.Name);
						}
						else
						{
							target.Maps.Add(map.Name);
						}

						CasesStep.Prune(target);
						SettingsStore.Save();
						changed = true;
					}
				}

				GUILayout.EndHorizontal();
			}

			GUILayout.EndVertical();
			return changed;
		}

		// The name of a saved case that is not on you: its type
		private static string CaseName(CaseEntry entry)
		{
			var name = string.IsNullOrEmpty(entry.CaseTemplate) ? "" : Game.TemplateName(entry.CaseTemplate);
			return string.IsNullOrEmpty(name) || name.EndsWith(" ShortName") || name.EndsWith(" Name") ? entry.CaseName ?? "" : name;
		}
	}
}
