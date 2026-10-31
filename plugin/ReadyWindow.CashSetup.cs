using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The Setup card of the Cash step: currencies kept out of one very case or wallet you carry. Out of the way until asked for, and
	// always shown while something is on the list.
	internal static partial class ReadyWindow
	{
		private static bool _showCashList;

		private static readonly string[] Currencies = { "RUB", "EUR", "USD" };

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
					var name = CaseLabel(item);
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
			if (list.Count == 0 && !_showCashList)
			{
				if (GUILayout.Button(new GUIContent("Choose what a case does with cash...", "Keep a currency out of a case (a SICC case of a map that needs no euros), or stop all cash while you carry a case"), Ui.Pick))
				{
					_showCashList = true;
				}

				return changed;
			}

			var cases = Game.CarriedAndSecured(controller.Inventory).OfType<CompoundItem>()
				.Where(item => Game.CasePlaces.Values.Contains(item.TemplateId.ToString()))
				.ToList();

			GUILayout.BeginVertical(Ui.Group);
			GUILayout.BeginHorizontal();
			HelpTitle("RUB, EUR, USD: never put that money in this very case (it goes to the next place of its row). No cash: while you carry this very case, no cash at all is brought. Both are about the very case, not its type.", "Cases and cash", Ui.Name);
			GUILayout.FlexibleSpace();
			if (list.Count == 0 && GUILayout.Button("Hide", Ui.Pick, GUILayout.Width(60)))
			{
				_showCashList = false;
			}

			GUILayout.EndHorizontal();
			if (cases.Count == 0)
			{
				GUILayout.Label("No wallet or case on you.", Ui.Muted);
			}

			// What is kept out comes first (cases on you with their switches, then saved ones that are not on you, one row each), then the
			// other cases on you
			var configured = list.Select(entry => entry.CaseId).Distinct().ToList();
			if (configured.Count > 0)
			{
				GUILayout.Label("Kept out", Ui.Name);
			}

			foreach (var id in configured)
			{
				var item = cases.FirstOrDefault(candidate => candidate.Id.ToString() == id);
				if (item != null)
				{
					changed |= CaseRow(item, list);
					continue;
				}

				var saved = list.Where(entry => entry.CaseId == id).ToList();
				GUILayout.BeginHorizontal();
				Icons.Slot(saved[0].CaseTemplate, saved[0].CaseName, 40);
				GUILayout.Space(8);
				if (!string.IsNullOrEmpty(saved[0].Tag))
				{
					Centered(() => TagChip.Draw(saved[0].Tag, saved[0].TagColor));
					GUILayout.Space(8);
				}

				CenteredLabel($"{saved[0].CaseName}  -  {(saved.Any(entry => entry.Currency == CashExclusion.All) ? "no cash" : string.Join(", ", saved.Select(entry => entry.Currency).ToArray()))}  -  saved, not on you", Ui.Muted, true);
				if (CenteredButton(new GUIContent("Remove from the list"), Ui.Pick, 150))
				{
					list.RemoveAll(entry => entry.CaseId == id);
					changed = true;
				}

				GUILayout.EndHorizontal();
			}

			var others = cases.Where(item => !configured.Contains(item.Id.ToString())).ToList();
			if (others.Count > 0)
			{
				GUILayout.Space(6);
				GUILayout.Label("On you", Ui.Name);
			}

			foreach (var item in others)
			{
				changed |= CaseRow(item, list);
			}

			GUILayout.EndVertical();
			return changed;
		}

		// A case or wallet on you with the three currency switches
		private static bool CaseRow(Item item, System.Collections.Generic.List<CashExclusion> list)
		{
			bool changed = false;
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

			CenteredLabel(CaseLabel(item), Ui.Body, true);
			bool noCash = list.Any(entry => entry.CaseId == id && entry.Currency == CashExclusion.All);
			if (CenteredButton(new GUIContent("No cash", noCash ? "While you carry this case no cash at all is brought. Click to allow cash again." : "Click to stop all cash while you carry this case (it already has what it needs, or it is for something else)"), noCash ? Ui.PickOn : Ui.Pick, 84))
			{
				if (noCash)
				{
					list.RemoveAll(entry => entry.CaseId == id && entry.Currency == CashExclusion.All);
				}
				else
				{
					list.Add(new CashExclusion { CaseId = id, CaseTemplate = item.TemplateId.ToString(), CaseName = CaseLabel(item), Tag = tagComponent?.Name, TagColor = tagComponent?.Color ?? 0, Currency = CashExclusion.All });
				}

				changed = true;
			}

			var enabled = GUI.enabled;
			GUI.enabled = enabled && !noCash;
			foreach (var currency in Currencies)
			{
				bool kept = list.Any(entry => entry.CaseId == id && entry.Currency == currency);
				if (CenteredButton(new GUIContent(currency, kept ? "Kept out: click to allow it again" : "Click to keep this money out of this case (it goes to the next place of its row)"), kept ? Ui.PickOn : Ui.Pick, 52))
				{
					if (kept)
					{
						list.RemoveAll(entry => entry.CaseId == id && entry.Currency == currency);
					}
					else
					{
						list.Add(new CashExclusion { CaseId = id, CaseTemplate = item.TemplateId.ToString(), CaseName = CaseLabel(item), Tag = tagComponent?.Name, TagColor = tagComponent?.Color ?? 0, Currency = currency });
					}

					changed = true;
				}
			}

			GUI.enabled = enabled;
			GUILayout.EndHorizontal();
			return changed;
		}
	}
}
