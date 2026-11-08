using UnityEngine;

namespace GetReady
{
	// The Insurance part of the Setup page: the trader the gear is insured at
	internal static partial class SetupPage
	{
		private static void InsuranceSetup(Transform section)
		{
			var insurance = SettingsStore.Current.Insurance;
			var card = SmallCard(section, "Insure at");
			UiKit.Label(card, "Everything you wear that the game lets you insure (armor, helmets, weapons, plates and mods) is insured when you press Get ready. If the roubles do not cover all of it, nothing is insured and the plan fails.", 0.8f, UiColors.Dim, wrap: true);
			var traders = InsuranceStep.Insurers();
			if (traders.Count == 0)
			{
				UiKit.Label(card, "No trader insures right now (open this in the hideout or the main menu).", 0.85f, UiColors.Dim);
				return;
			}

			foreach (var trader in traders)
			{
				var chosen = trader;
				bool on = insurance.TraderId == chosen.Id;
				var row = UiKit.HBox(card, 8f);
				var picture = TraderAvatars.Picture(chosen);
				if (picture == null)
				{
					ReadyUiWindow.IconPending(() => !TraderAvatars.Pending(chosen));
				}

				UiKit.Icon(row, picture, UiKit.Unit * 2f, chosen.LocalizedName);
				UiKit.Label(row, chosen.LocalizedName, 0.9f, on ? UiColors.Heading : UiColors.Text, flex: true);
				Pick(row, on ? "Selected" : "Use", on, () =>
				{
					insurance.TraderId = chosen.Id;
					Changed();
				}, on ? "The gear is insured here" : "Insure the gear at " + chosen.LocalizedName, UiKit.Unit * 8f);
			}
		}
	}
}
