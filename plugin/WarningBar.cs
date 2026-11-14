using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GetReady
{
	// One orange warning bar of the map screen (see ReminderBars): the game's warning mark, then pictures and text in any order, black on
	// orange with a glow. Every warning of the screen but NOT READY is drawn with this, so they all look the same.
	internal static class WarningBar
	{
		// An item's picture in a small dark square; a tag (a case's) is a strip across the bottom, the square widens when the tag needs it
		internal class Piece
		{
			public string Template;
			public string Tag;
			public int TagColor;
		}

		internal class Part
		{
			public string Text;
			public Piece Piece;

			internal static Part Of(string text) => new Part { Text = text };

			internal static Part Of(Piece piece) => new Part { Piece = piece };
		}

		private static readonly Color Orange = new Color(1f, 0.6f, 0.08f);
		private static readonly Color Glow = new Color(1f, 0.5f, 0.05f);
		private static readonly Color Ink = new Color(0.07f, 0.05f, 0.02f);
		private static GUIStyle _text, _mark;

		internal static float Row => Screen.height * 0.045f;

		internal static float Height => Row * 1.2f;

		// How far down the next bar starts
		internal static float Step => Height + Row * 0.45f;

		// Draws one bar centred, its top at the height given, in a repaint; returns where the next bar starts
		internal static float Draw(float top, IList<Part> parts)
		{
			OverlayFont.Init();
			float row = Row, height = Height;
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
			float markWidth = WarningWidth(icon);

			var widths = parts.Select(part => part.Piece != null ? PieceWidth(part.Piece, icon) : _text.CalcSize(new GUIContent(part.Text)).x + 2f).ToList();
			float width = pad + markWidth + gap + widths.Sum() + gap * (parts.Count - 1) + row * 0.6f;
			var bar = new Rect((Screen.width - width) / 2f, top, width, height);

			var previous = GUI.color;
			// The glow of the NOT READY bar, in orange
			var halo = new Rect(bar.x - bar.width * 0.3f, bar.y - bar.height * 1.1f, bar.width * 1.6f, bar.height * 3.2f);
			float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.5f);
			GUI.color = new Color(Glow.r, Glow.g, Glow.b, pulse * 0.8f);
			GUI.DrawTexture(halo, ReadyFlash.GlowTexture(), ScaleMode.StretchToFill, true);
			GUI.color = Orange;
			GUI.DrawTexture(bar, Texture2D.whiteTexture);

			// The warning mark: the game's own icon in black on the bar (an orange "!" on black when the game has none)
			var mark = new Rect(bar.x + pad, bar.y + pad, markWidth, icon);
			var warning = WarningIcon();
			if (warning != null && warning.texture != null)
			{
				GUI.color = Ink;
				GUI.DrawTextureWithTexCoords(mark, warning.texture, Coords(warning));
			}
			else
			{
				GUI.color = Ink;
				GUI.DrawTexture(mark, Texture2D.whiteTexture);
				GUI.color = Color.white;
				GUI.Label(mark, "!", _mark);
			}

			float x = mark.xMax + gap;
			for (int i = 0; i < parts.Count; i++)
			{
				if (parts[i].Piece != null)
				{
					Picture(new Rect(x, bar.y + pad, widths[i], icon), parts[i].Piece);
				}
				else
				{
					GUI.color = Color.white;
					GUI.Label(new Rect(x, bar.y, widths[i], height), parts[i].Text, _text);
				}

				x += widths[i] + gap;
			}

			GUI.color = previous;
			return top + Step;
		}

		private static Rect Coords(Sprite sprite)
		{
			var source = sprite.textureRect;
			var texture = sprite.texture;
			return new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height);
		}

		private static float PieceWidth(Piece piece, float icon) =>
			string.IsNullOrEmpty(piece.Tag) ? icon : Mathf.Max(icon, TagChip.Width(piece.Tag, icon));

		// The width of the warning mark at this height: its own proportions (a square for the drawn "!")
		private static float WarningWidth(float height)
		{
			var warning = WarningIcon();
			return warning != null && warning.texture != null && warning.textureRect.height > 0f ? height * warning.textureRect.width / warning.textureRect.height : height;
		}

		private static bool _warningLooked;
		private static Sprite _warning;

		// The game's own warning icon, in white to be tinted; null when it has none by these names (the "!" is drawn instead)
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
				var name = new[] { "Warning_Icon", "Attention_icon", "icon_attention" }.FirstOrDefault(names.Contains);
				_warning = name == null ? null : GameWidgets.White(GameWidgets.ByName(name));
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Warning bars: no warning icon: {e.Message}");
			}

			return _warning;
		}

		// The thin light edge of an item's square
		private static void Edge(Rect rect)
		{
			GUI.color = new Color(0.42f, 0.43f, 0.42f);
			const float line = 1.5f;
			GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, line), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.yMax - line, rect.width, line), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.y, line, rect.height), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.xMax - line, rect.y, line, rect.height), Texture2D.whiteTexture);
		}

		// An item's picture in a dark square with an edge, its tag across the bottom; the picture is centred, in proportion
		private static void Picture(Rect frame, Piece piece)
		{
			GUI.color = new Color(0.105f, 0.11f, 0.11f);
			GUI.DrawTexture(frame, Texture2D.whiteTexture);
			Edge(frame);
			GUI.color = Color.white;
			var sprite = Icons.Of(piece.Template);
			if (sprite != null && sprite.texture != null)
			{
				var source = sprite.textureRect;
				float scale = Mathf.Min((frame.height - 4f) / source.width, (frame.height - 4f) / source.height);
				var shown = new Vector2(source.width * scale, source.height * scale);
				GUI.DrawTextureWithTexCoords(new Rect(frame.x + (frame.width - shown.x) / 2f, frame.y + (frame.height - shown.y) / 2f, shown.x, shown.y), sprite.texture, Coords(sprite));
			}

			TagChip.DrawOn(frame, piece.Tag, piece.TagColor);
		}
	}
}
