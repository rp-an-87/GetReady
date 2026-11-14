using System;
using System.Collections.Generic;
using System.Linq;
using EFT.UI.Matchmaker;
using UnityEngine;

namespace GetReady
{
	// Every warning of the map screen but NOT READY, as orange bars (see WarningBar) stacked under the title (PVE ZONE), one per kind. What is
	// said is decided by Reminders (containers wrong or missing for the map included); this only draws it. Nothing is clickable. They are looked at twice a second, in the layout pass only, so the two passes of a frame draw the same thing.
	internal static class ReminderBars
	{
		private static MatchMakerSelectionLocationScreen _screen;
		private static string _map;
		private static float _checkedAt = float.NegativeInfinity;
		private static List<Reminders.Reminder> _found = new List<Reminders.Reminder>();

		internal static void Picked(MatchMakerSelectionLocationScreen screen, string locationId)
		{
			_screen = screen;
			_map = MapTags.MapOf(locationId);
			_checkedAt = float.NegativeInfinity;
		}

		private static WarningBar.Piece PieceOf(CaseEntry entry) => new WarningBar.Piece { Template = entry.CaseTemplate, Tag = entry.Tag, TagColor = entry.TagColor };

		internal static void Draw()
		{
			if (_screen == null || !_screen.isActiveAndEnabled)
			{
				return;
			}

			var type = Event.current.type;
			if (type == EventType.Layout && Time.unscaledTime - _checkedAt > 0.5f)
			{
				_checkedAt = Time.unscaledTime;
				try
				{
					// A scav raid takes the scav's own kit: nothing from your stash and no reminders
					_found = MapScreen.IsScav(_screen) ? new List<Reminders.Reminder>() : Reminders.Check(SettingsStore.Current.Reminders, Reminders.FactsOf(MapScreen.ProfileOf(_screen), _map, SettingsStore.Current.Cases.Cases));
				}
				catch (Exception e)
				{
					_found = new List<Reminders.Reminder>();
					Plugin.Log.LogError($"Reminders failed: {e}");
				}
			}

			if (type != EventType.Repaint || _found.Count == 0)
			{
				return;
			}

			float top = Screen.height * 0.085f;
			foreach (var reminder in _found)
			{
				var pieces = reminder.Icons.Select(template => WarningBar.Part.Of(new WarningBar.Piece { Template = template }))
					.Concat(reminder.Cases.Select(entry => WarningBar.Part.Of(PieceOf(entry)))).ToList();
				var text = WarningBar.Part.Of(reminder.Text);
				var parts = reminder.TextFirst ? new[] { text }.Concat(pieces).ToList() : pieces.Concat(new[] { text }).ToList();
				top = WarningBar.Draw(top, parts);
			}
		}
	}
}
