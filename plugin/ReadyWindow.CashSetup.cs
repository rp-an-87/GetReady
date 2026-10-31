using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The Setup card of the Cash step: currencies kept out of one very case or wallet you carry. Out of the way until asked for, and
	// always shown while something is on the list.
	internal static partial class ReadyWindow
	{
		private static string CaseLabel(Item item)
		{
			var where = item.Parent?.Container?.ParentItem;
			var whereName = where == null ? null : Game.NameOf(where);
			return Game.NameOf(item) + (string.IsNullOrEmpty(whereName) ? "" : $" in {whereName}");
		}

		// Keeps the saved entries current: an entry from an older build has the tag inside its name (S I C C "labs" in Gamma) and none of its
		// own; it is split into name and tag. A case that is on you gives its tag and name as they are now.
		private static bool Refresh(System.Collections.Generic.List<CashExclusion> list, InventoryController controller)
		{
			bool changed = false;
			var here = Game.CarriedAndSecured(controller.Inventory).ToDictionary(item => item.Id.ToString(), item => item, System.StringComparer.Ordinal);
			foreach (var entry in list)
			{
				Item item;
				if (here.TryGetValue(entry.CaseId, out item))
				{
					var tag = item.GetItemComponent<TagComponent>();
					var name = Game.NameOf(item);
					if (entry.Tag != tag?.Name || entry.TagColor != (tag?.Color ?? 0) || entry.CaseName != name)
					{
						entry.Tag = tag?.Name;
						entry.TagColor = tag?.Color ?? 0;
						entry.CaseName = name;
						changed = true;
					}
				}
				else if (entry.Tag == null)
				{
					var match = System.Text.RegularExpressions.Regex.Match(entry.CaseName ?? "", "^(.*?) \"([^\"]+)\"( in .*)?$");
					if (match.Success)
					{
						entry.CaseName = match.Groups[1].Value + match.Groups[3].Value;
						entry.Tag = match.Groups[2].Value;
						changed = true;
					}
				}
			}

			return changed;
		}

		private static bool CashExclusionsUi(InventoryController controller)
		{
			var list = SettingsStore.Current.Supplies.CashExclusions;
			bool changed = Refresh(list, controller);
			var cases = Game.CarriedAndSecured(controller.Inventory).OfType<CompoundItem>()
				.Where(item => Game.CasePlaces.Values.Contains(item.TemplateId.ToString()))
				.ToList();

			GUILayout.BeginVertical(Ui.Group);
			GUILayout.BeginHorizontal();
			HelpTitle("RUB, EUR, USD on: this very case accepts that money; turn one off and that money goes to the next place of its row. No cash: while you carry this very case no cash at all is brought. Both are about the very case, not its type.", "Cases and cash", Ui.Name);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			// What is set up comes first among the cases on you; the saved ones that are not on you are in the second section, one row each
			var configured = list.Select(entry => entry.CaseId).Distinct().ToList();
			var onYou = cases.OrderBy(item => configured.Contains(item.Id.ToString()) ? 0 : 1).ToList();
			var away = configured.Where(id => !cases.Any(candidate => candidate.Id.ToString() == id)).ToList();
			bool pressed = false;
			TwoSections("cash", "ON YOU", "No wallet or case on you.", onYou.Count > 0, () =>
			{
				foreach (var item in onYou)
				{
					pressed |= CaseRow(item, list);
				}
			}, "NOT ON YOU (saved, ignored until you carry them)", "Cases you set up and are not carrying will appear here.", away.Count > 0, () =>
			{
				foreach (var id in away)
				{
					var saved = list.First(entry => entry.CaseId == id);
					GUILayout.BeginHorizontal();
					Icons.Slot(saved.CaseTemplate, CashChoice.BareName(saved), 40);
					GUILayout.Space(8);
					if (!string.IsNullOrEmpty(saved.Tag))
					{
						Centered(() => TagChip.Draw(saved.Tag, saved.TagColor));
						GUILayout.Space(8);
					}

					CenteredLabel(CashChoice.BareName(saved), Ui.Plain, false);
					GUILayout.FlexibleSpace();
					pressed |= CurrencyButtons(list, saved);
					GUILayout.EndHorizontal();
				}
			});

			changed |= pressed;
			GUILayout.EndVertical();
			return changed;
		}

		// "No cash" and then RUB, EUR and USD of a case: a currency on means the case accepts that money
		private static bool CurrencyButtons(System.Collections.Generic.List<CashExclusion> list, CashExclusion about)
		{
			bool changed = false;
			bool noCash = CashChoice.NoCash(list, about.CaseId);
			if (CenteredButton(new GUIContent("No cash", noCash ? "While you carry this case no cash at all is brought. Click to allow cash again." : "Click to stop all cash while you carry this case (it already has what it needs, or it is for something else)"), noCash ? Ui.PickOn : Ui.Pick, 84))
			{
				CashChoice.ToggleNoCash(list, about);
				changed = true;
			}

			var enabled = GUI.enabled;
			GUI.enabled = enabled && !noCash;
			foreach (var currency in CashChoice.Currencies)
			{
				bool accepts = CashChoice.Accepts(list, about.CaseId, currency);
				if (CenteredButton(new GUIContent(currency, accepts ? "Accepted: click to keep this money out of this case (it goes to the next place of its row)" : "Kept out: click to accept this money again"), accepts ? Ui.PickOn : Ui.Pick, 52))
				{
					CashChoice.Toggle(list, about, currency);
					changed = true;
				}
			}

			GUI.enabled = enabled;
			return changed;
		}

		// A case or wallet on you with its three currency switches
		private static bool CaseRow(Item item, System.Collections.Generic.List<CashExclusion> list)
		{
			var id = item.Id.ToString();
			GUILayout.BeginHorizontal();
			Icons.Slot(item, Game.NameOf(item), 40);
			GUILayout.Space(8);
			var tagComponent = item.GetItemComponent<TagComponent>();
			if (!string.IsNullOrEmpty(tagComponent?.Name))
			{
				Centered(() => TagChip.Draw(tagComponent.Name, tagComponent.Color));
				GUILayout.Space(8);
			}

			CenteredLabel(Game.NameOf(item), Ui.Plain, false);
			GUILayout.FlexibleSpace();
			var about = new CashExclusion { CaseId = id, CaseTemplate = item.TemplateId.ToString(), CaseName = Game.NameOf(item), Tag = tagComponent?.Name, TagColor = tagComponent?.Color ?? 0 };
			bool changed = CurrencyButtons(list, about);
			GUILayout.EndHorizontal();
			return changed;
		}
	}
}
