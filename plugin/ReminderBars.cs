using System;
using System.Collections.Generic;
using System.Linq;
using EFT.UI.Matchmaker;
using UnityEngine;

namespace GetReady
{
	// Every warning of the map screen but NOT READY, as orange bars (see WarningBar) stacked under the title (PVE ZONE), one per kind: the
	// containers that are wrong for the map, the ones that are missing, and the soft reminders (see Reminders). They only inform: nothing is
	// clickable. The reminders are looked at twice a second, in the layout pass only, so the two passes of a frame draw the same thing.
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
					_found = MapWarning.IsScav(_screen) ? new List<Reminders.Reminder>() : Reminders.Check(SettingsStore.Current.Reminders, Reminders.FactsOf(MapWarning.ProfileOf(_screen), _map));
				}
				catch (Exception e)
				{
					_found = new List<Reminders.Reminder>();
					Plugin.Log.LogError($"Reminders failed: {e}");
				}
			}

			var cases = MapWarning.Current();
			if (type != EventType.Repaint || (_found.Count == 0 && !cases.Any))
			{
				return;
			}

			float top = Screen.height * 0.085f;
			if (cases.Wrong.Count > 0)
			{
				var parts = cases.Wrong.Select(entry => WarningBar.Part.Of(PieceOf(entry))).ToList();
				parts.Add(WarningBar.Part.Of("WRONG CONTAINERS FOR THIS MAP!"));
				top = WarningBar.Draw(top, parts);
			}

			if (cases.Missing.Count > 0)
			{
				var parts = new List<WarningBar.Part> { WarningBar.Part.Of("MISSING CONTAINERS FOR THIS MAP:") };
				parts.AddRange(cases.Missing.Select(entry => WarningBar.Part.Of(PieceOf(entry))));
				top = WarningBar.Draw(top, parts);
			}

			foreach (var reminder in _found)
			{
				var parts = reminder.Icons.Select(template => WarningBar.Part.Of(new WarningBar.Piece { Template = template })).ToList();
				parts.Add(WarningBar.Part.Of(reminder.Text));
				top = WarningBar.Draw(top, parts);
			}
		}
	}
}
