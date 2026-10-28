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
			return Game.NameOf(item) + (where == null ? "" : $" in {Game.NameOf(where)}");
		}

		private static bool CashExclusionsUi(InventoryController controller)
		{
			bool changed = false;
			var list = SettingsStore.Current.Supplies.CashExclusions;
			if (list.Count == 0 && !_showCashList)
			{
				if (GUILayout.Button(new GUIContent("Keep a currency out of a case on purpose...", "For a case that has no use for that money, a SICC case of a map that needs no euros for instance"), Ui.Pick))
				{
					_showCashList = true;
				}

				return false;
			}

			var cases = Game.CarriedAndSecured(controller.Inventory).OfType<CompoundItem>()
				.Where(item => Game.CasePlaces.Values.Contains(item.TemplateId.ToString()))
				.ToList();

			GUILayout.BeginVertical(Ui.Group);
			GUILayout.BeginHorizontal();
			GUILayout.Label("Cash kept out of cases", Ui.Name);
			GUILayout.FlexibleSpace();
			if (list.Count == 0 && GUILayout.Button("Hide", Ui.Pick, GUILayout.Width(60)))
			{
				_showCashList = false;
			}

			GUILayout.EndHorizontal();
			GUILayout.Label("This very case, not its type. If the first case the money would go to is kept out, the money is not brought at all (no other place is tried).", Ui.Muted);
			if (cases.Count == 0)
			{
				GUILayout.Label("No wallet or case on you.", Ui.Muted);
			}

			foreach (var item in cases)
			{
				var id = item.Id.ToString();
				GUILayout.BeginHorizontal();
				Icons.Slot(item, Game.NameOf(item), 40);
				var tagComponent = item.GetItemComponent<TagComponent>();
				TagChip.Draw(tagComponent?.Name, tagComponent?.Color ?? 0);
				GUILayout.Label(CaseLabel(item), Ui.Body);
				GUILayout.FlexibleSpace();
				foreach (var currency in Currencies)
				{
					bool kept = list.Any(entry => entry.CaseId == id && entry.Currency == currency);
					if (GUILayout.Button(new GUIContent(currency, kept ? "Kept out: click to allow it again" : "Click to keep this money out of this case"), kept ? Ui.PickOn : Ui.Pick, GUILayout.Width(52)))
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

				GUILayout.EndHorizontal();
			}

			foreach (var entry in list.Where(entry => !cases.Any(item => item.Id.ToString() == entry.CaseId)).ToList())
			{
				GUILayout.BeginHorizontal();
				Icons.Slot(entry.CaseTemplate, entry.CaseName, 40);
				TagChip.Draw(entry.Tag, entry.TagColor);
				GUILayout.Label($"{entry.CaseName}  -  {entry.Currency}  -  saved, not on you", Ui.Muted);
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Remove from the list", Ui.Pick, GUILayout.Width(150)))
				{
					list.Remove(entry);
					changed = true;
				}

				GUILayout.EndHorizontal();
			}

			GUILayout.EndVertical();
			return changed;
		}
	}
}
