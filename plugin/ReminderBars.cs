using System;
using System.Collections.Generic;
using System.Linq;
using EFT.UI.Matchmaker;
using UnityEngine;

namespace GetReady
{
	// The soft reminders on the map screen (see Reminders): one orange bar per kind, black letters, stacked under the title (PVE ZONE). They
	// only inform: nothing is clickable. Looked at twice a second, in the layout pass only, so the two passes of a frame draw the same thing.
	internal static class ReminderBars
	{
		private static MatchMakerSelectionLocationScreen _screen;
		private static string _map;
		private static float _checkedAt = float.NegativeInfinity;
		private static List<Reminders.Reminder> _found = new List<Reminders.Reminder>();

		private static readonly Color Orange = new Color(1f, 0.6f, 0.08f);
		private static readonly Color Glow = new Color(1f, 0.5f, 0.05f);
		private static readonly Color Ink = new Color(0.07f, 0.05f, 0.02f);
		private static GUIStyle _text, _mark;

		internal static void Picked(MatchMakerSelectionLocationScreen screen, string locationId)
		{
			_screen = screen;
			_map = MapTags.MapOf(locationId);
			_checkedAt = float.NegativeInfinity;
		}

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

			if (_found.Count == 0 || type != EventType.Repaint)
			{
				return;
			}

			OverlayFont.Init();
			float row = Screen.height * 0.045f;
			float height = row * 1.2f;
			if (_text == null)
			{
				_text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, richText = false, wordWrap = false, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
				_text.normal.textColor = Ink;
				_mark = new GUIStyle(_text) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
				_mark.normal.textColor = Orange;
			}

			if (OverlayFont.Font != null && _text.font != OverlayFont.Font)
			{
				_text.font = OverlayFont.Font;
				_mark.font = OverlayFont.Font;
			}

			_text.fontSize = Mathf.RoundToInt(row * 0.4f);
			_mark.fontSize = Mathf.RoundToInt(row * 0.55f);
			float pad = height * 0.16f;
			float icon = height - pad * 2f;
			float gap = row * 0.3f;
			float top = Screen.height * 0.085f;
			var previous = GUI.color;
			foreach (var reminder in _found)
			{
				float width = pad + icon + gap + reminder.Icons.Count * (icon + gap) + _text.CalcSize(new GUIContent(reminder.Text)).x + row * 0.9f;
				var bar = new Rect((Screen.width - width) / 2f, top, width, height);
				// The glow of the NOT READY bar, in orange
				var halo = new Rect(bar.x - bar.width * 0.3f, bar.y - bar.height * 1.1f, bar.width * 1.6f, bar.height * 3.2f);
				float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.5f);
				GUI.color = new Color(Glow.r, Glow.g, Glow.b, pulse * 0.8f);
				GUI.DrawTexture(halo, ReadyFlash.GlowTexture(), ScaleMode.StretchToFill, true);
				GUI.color = Orange;
				GUI.DrawTexture(bar, Texture2D.whiteTexture);
				// The warning mark: the game's own icon in black on the bar (an orange "!" on black when the game has none)
				var mark = new Rect(bar.x + pad, bar.y + pad, icon, icon);
				var warning = WarningIcon();
				if (warning != null && warning.texture != null)
				{
					GUI.color = Ink;
					var source = warning.textureRect;
					var texture = warning.texture;
					// Kept in proportion, centred in the square
					float fit = Mathf.Min(mark.width / source.width, mark.height / source.height);
					var shown = new Vector2(source.width * fit, source.height * fit);
					var target = new Rect(mark.x + (mark.width - shown.x) / 2f, mark.y + (mark.height - shown.y) / 2f, shown.x, shown.y);
					GUI.DrawTextureWithTexCoords(target, texture, new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
				}
				else
				{
					GUI.color = Ink;
					GUI.DrawTexture(mark, Texture2D.whiteTexture);
					GUI.color = Color.white;
					GUI.Label(mark, "!", _mark);
				}

				float x = mark.xMax + gap;
				foreach (var template in reminder.Icons)
				{
					Picture(new Rect(x, bar.y + pad, icon, icon), template);
					x += icon + gap;
				}

				GUI.Label(new Rect(x, bar.y, bar.xMax - x, height), reminder.Text, _text);
				top += height + row * 0.45f;
			}

			GUI.color = previous;
		}

		private static bool _warningLooked;
		private static Sprite _warning;

		// The game's own warning icon, in white to be tinted; null when none of the guessed names exists (the "!" is drawn instead). The sprites
		// with a likely word in their name are written to the log once, to find the right name.
		private static Sprite WarningIcon()
		{
			if (_warningLooked)
			{
				return _warning;
			}

			_warningLooked = true;
			try
			{
				var names = GameWidgets.NamesLike("warn", "attention", "alert", "exclam");
				Plugin.Log.LogInfo($"Reminders: sprites that may be a warning icon: {string.Join(", ", names)}");
				var name = new[] { "Warning_Icon", "Attention_icon", "icon_attention" }.FirstOrDefault(names.Contains);
				_warning = name == null ? null : GameWidgets.White(GameWidgets.ByName(name));
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Reminders: no warning icon: {e.Message}");
			}

			return _warning;
		}

		// An item's picture in a small dark frame
		private static void Picture(Rect rect, string template)
		{
			GUI.color = new Color(0.105f, 0.11f, 0.11f);
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			GUI.color = Color.white;
			var sprite = Icons.Of(template);
			if (sprite == null || sprite.texture == null)
			{
				return;
			}

			var source = sprite.textureRect;
			float scale = Mathf.Min((rect.width - 4f) / source.width, (rect.height - 4f) / source.height);
			var shown = new Vector2(source.width * scale, source.height * scale);
			var target = new Rect(rect.x + (rect.width - shown.x) / 2f, rect.y + (rect.height - shown.y) / 2f, shown.x, shown.y);
			var texture = sprite.texture;
			GUI.DrawTextureWithTexCoords(target, texture, new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
		}
	}
}
