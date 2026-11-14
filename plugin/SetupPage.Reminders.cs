using System;
using System.Linq;
using UnityEngine;

namespace GetReady
{
	// The Reminders section of the Setup page: a panel per kind of soft reminder (see Reminders), each with its own switch
	internal static partial class SetupPage
	{
		private const int MapsPerRow = 4;

		// The map buttons, four to a row, equal width: lit when isOn says so, toggle gets the map's name
		private static void MapButtons(Transform parent, System.Collections.Generic.IList<MapTags.MapDef> choices, Func<string, bool> isOn, Action<string> toggle)
		{
			for (int first = 0; first < choices.Count; first += MapsPerRow)
			{
				var row = UiKit.HBox(parent, 6f);
				row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
				var maps = choices.Skip(first).Take(MapsPerRow).ToList();
				foreach (var map in maps)
				{
					string mapName = map.Name;
					Pick(row, mapName, isOn(mapName), () => toggle(mapName), null);
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

		// A kind's card: its switch and name, and what it sets underneath when it is on
		private static RectTransform ReminderCard(Transform section, string title, string template, bool on, Action<bool> set)
		{
			var card = UiKit.VBox(section, 6f, new RectOffset(10, 10, 8, 8), UiKit.CardColor, UiKit.CardEdge);
			var head = UiKit.HBox(card, 8f);
			Switch(head, on, () =>
			{
				set(!on);
				Changed();
			});
			if (template != null)
			{
				TemplateIcon(head, template, UiKit.Unit * 1.6f, title);
			}

			UiKit.Label(head, title, 0.95f, UiColors.Heading, flex: true);
			return card;
		}

		private static void MapPanel(Transform card, MapItemReminder reminder, string[] extractMaps, string what)
		{
			var maps = UiKit.VBox(card, 6f, new RectOffset(8, 8, 6, 8), InnerPanel, UiKit.InnerEdge);
			UiKit.Label(maps, reminder.Maps.Any(extractMaps.Contains) ? "Remind me on these maps:" : $"Maps with {what} (pick the ones to be reminded on):", 0.8f, UiColors.Dim);
			MapButtons(maps, MapTags.Only(extractMaps).ToList(), name => reminder.Maps.Contains(name), name =>
			{
				if (!reminder.Maps.Remove(name))
				{
					reminder.Maps.Add(name);
				}

				Changed();
			});
		}

		// A threshold row: "Warn if <what> is below  - 30% +"
		private static void ThresholdRow(Transform card, string what, bool on, Action<bool> setOn, int value, Action<int> setValue)
		{
			var row = UiKit.HBox(card, 8f);
			Switch(row, on, () =>
			{
				setOn(!on);
				Changed();
			});
			UiKit.Label(row, $"Warn if {what} is below", 0.9f, UiColors.Text, flex: true);
			Stepper(row, value + "%", UiColors.Text, 4f,
				() => { setValue(Mathf.Max(5, value - 5)); Changed(); },
				() => { setValue(Mathf.Min(95, value + 5)); Changed(); },
				"Lower", "Higher");
		}

		private static void RemindersSetup(RectTransform section)
		{
			var settings = SettingsStore.Current.Reminders;
			UiKit.Label(section, "Reminders", 1f, UiColors.Heading, bold: true);
			UiKit.Label(section, "Orange notes on the map screen for things the plan cannot decide for you. They never make you \"not ready\" and nothing is moved.", 0.8f, UiColors.Dim, wrap: true);
			UiKit.Line(section);

			var wrong = ReminderCard(section, "Containers for another map", null, settings.WrongContainers, value => settings.WrongContainers = value);
			UiKit.Label(wrong, "Warns about a case you carry that is set to other maps only. The maps are set on the case, in Cases.", 0.8f, UiColors.Dim, wrap: true);
			var missing = ReminderCard(section, "Missing containers", null, settings.MissingContainers, value => settings.MissingContainers = value);
			UiKit.Label(missing, "Warns about a case set for this map that is not on you. The maps are set on the case, in Cases.", 0.8f, UiColors.Dim, wrap: true);

			var flare = ReminderCard(section, "Green flare", Reminders.GreenFlare, settings.GreenFlare.Enabled, value => settings.GreenFlare.Enabled = value);
			if (settings.GreenFlare.Enabled)
			{
				MapPanel(flare, settings.GreenFlare, MapTags.FlareMaps, "a green flare extract");
			}

			var paracord = settings.Paracord;
			var cord = ReminderCard(section, "Paracord and Red Rebel", Reminders.Paracord, paracord.Enabled, value => paracord.Enabled = value);
			if (paracord.Enabled)
			{
				OptionRow(cord, paracord.PairOnly, "Only when I have one of the two and not the other", "<b>On:</b> warns when you carry the paracord and not the Red Rebel, or the other way round. Carrying neither is left alone.\n<b>Off:</b> warns for whichever of the two is missing.", value => paracord.PairOnly = value);
				OptionRow(cord, paracord.WarnArmor, "Warn if I wear body armor with both", "With the paracord and the Red Rebel on you, warns when something is in the Body Armor slot: the Red Rebel extract does not allow it. Anything in the Tactical Rig slot (armored rigs too) is fine.", value => paracord.WarnArmor = value);
				MapPanel(cord, paracord, MapTags.AlpinistMaps, "a Red Rebel extract");
			}

			var needs = settings.Needs;
			var needsCard = ReminderCard(section, "Needs", null, needs.Enabled, value => needs.Enabled = value);
			if (needs.Enabled)
			{
				ThresholdRow(needsCard, "hydration", needs.WarnHydration, value => needs.WarnHydration = value, needs.HydrationBelow, value => needs.HydrationBelow = value);
				ThresholdRow(needsCard, "energy", needs.WarnEnergy, value => needs.WarnEnergy = value, needs.EnergyBelow, value => needs.EnergyBelow = value);
				OptionRow(needsCard, needs.OnlyIfNoSupplies, "Only if I carry no food or drink for it", "<b>On:</b> low hydration is left alone when you carry a drink, low energy when you carry food.\n<b>Off:</b> warns whatever you carry.", value => needs.OnlyIfNoSupplies = value);
			}

			ReminderCard(section, "Equipment: armor with an empty plate slot", null, settings.Equipment.Enabled, value => settings.Equipment.Enabled = value);
		}
	}
}
