using System.Collections.Generic;
using System.Linq;

namespace GetReady
{
	// What a case or wallet accepts, as the Setup page shows it: RUB, EUR and USD, each on (accepted) or off (that money is kept out of the
	// case and goes to the next place of its row), and "No cash" apart from them: while you carry this very case no cash at all is brought.
	// What is saved are the currencies kept out, and ALL for "No cash"; with nothing saved the case is in its usual state.
	internal static class CashChoice
	{
		internal static readonly string[] Currencies = { "RUB", "EUR", "USD" };

		internal static bool Accepts(List<CashExclusion> list, string caseId, string currency) =>
			!list.Any(entry => entry.CaseId == caseId && entry.Currency == currency);

		internal static bool NoCash(List<CashExclusion> list, string caseId) =>
			list.Any(entry => entry.CaseId == caseId && entry.Currency == CashExclusion.All);

		// Nothing is saved for the case: all three are accepted and cash is not stopped
		internal static bool IsDefault(List<CashExclusion> list, string caseId) => !list.Any(entry => entry.CaseId == caseId);

		// A click on a currency of the case: accepted becomes kept out and the other way round
		internal static void Toggle(List<CashExclusion> list, CashExclusion about, string currency) => Flip(list, about, currency);

		// A click on "No cash"
		internal static void ToggleNoCash(List<CashExclusion> list, CashExclusion about) => Flip(list, about, CashExclusion.All);

		private static void Flip(List<CashExclusion> list, CashExclusion about, string currency)
		{
			if (list.RemoveAll(entry => entry.CaseId == about.CaseId && entry.Currency == currency) == 0)
			{
				list.Add(new CashExclusion { CaseId = about.CaseId, CaseTemplate = about.CaseTemplate, CaseName = about.CaseName, Tag = about.Tag, TagColor = about.TagColor, Currency = currency });
			}
		}

		// The name of a saved case without where it was ("Wallet in Beta 2" was saved by older builds): it is not anywhere now
		internal static string BareName(CashExclusion entry)
		{
			if (!string.IsNullOrEmpty(entry.CaseTemplate))
			{
				var name = Game.TemplateName(entry.CaseTemplate);
				if (!string.IsNullOrEmpty(name) && !name.EndsWith(" ShortName") && !name.EndsWith(" Name"))
				{
					return name;
				}
			}

			var saved = entry.CaseName ?? "";
			var at = saved.IndexOf(" in ");
			return at > 0 ? saved.Substring(0, at) : saved;
		}
	}
}
