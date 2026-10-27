using UnityEngine;

namespace GetReady
{
	// Dark look for the IMGUI window: styles built once from flat-colour textures (no assets). Call Init from OnGUI.
	internal static class Ui
	{
		internal static readonly Color Good = new Color(0.42f, 0.78f, 0.5f);
		internal static readonly Color Warn = new Color(1f, 0.64f, 0.28f);
		internal static readonly Color Bad = new Color(1f, 0.38f, 0.34f);
		internal static readonly Color Text = new Color(0.9f, 0.9f, 0.88f);
		internal static readonly Color Dim = new Color(0.62f, 0.64f, 0.64f);

		internal static GUIStyle Window, Card, Title, Muted, Body, Plain, Move, Problem, Toggle, Button, Small, Primary,
			ChipOk, ChipTodo, ChipBad, ChipOff, AlertCard, AlertTitle, AlertText, Alert;

		private static bool _ready;

		private static Texture2D Box(Color fill, Color edge, int edgeWidth)
		{
			int size = edgeWidth * 2 + 2;
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					bool onEdge = x < edgeWidth || y < edgeWidth || x >= size - edgeWidth || y >= size - edgeWidth;
					texture.SetPixel(x, y, edgeWidth > 0 && onEdge ? edge : fill);
				}
			}

			texture.Apply();
			return texture;
		}

		private static GUIStyle Back(GUIStyle style, Color fill, Color edge, int edgeWidth)
		{
			var texture = Box(fill, edge, edgeWidth);
			style.normal.background = texture;
			style.hover.background = texture;
			style.active.background = texture;
			style.focused.background = texture;
			style.onNormal.background = texture;
			style.onHover.background = texture;
			style.onActive.background = texture;
			style.onFocused.background = texture;
			style.border = new RectOffset(edgeWidth, edgeWidth, edgeWidth, edgeWidth);
			return style;
		}

		private static void TextColor(GUIStyle style, Color color)
		{
			style.normal.textColor = color;
			style.hover.textColor = color;
			style.active.textColor = color;
			style.focused.textColor = color;
			style.onNormal.textColor = color;
			style.onHover.textColor = color;
			style.onActive.textColor = color;
			style.onFocused.textColor = color;
		}

		private static GUIStyle Label(int size, Color color, FontStyle fontStyle = FontStyle.Normal)
		{
			var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, wordWrap = true, richText = false };
			TextColor(style, color);
			return style;
		}

		private static GUIStyle Chip(Color fill)
		{
			var style = Back(new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, stretchWidth = false, padding = new RectOffset(10, 10, 3, 3) }, fill, fill, 1);
			TextColor(style, Color.white);
			return style;
		}

		private static GUIStyle Press(GUIStyle style, Color fill, Color hover, Color down)
		{
			Back(style, fill, fill, 1);
			style.hover.background = Box(hover, hover, 1);
			style.active.background = Box(down, down, 1);
			style.onNormal.background = style.normal.background;
			style.onHover.background = style.hover.background;
			style.onActive.background = style.active.background;
			TextColor(style, Color.white);
			style.alignment = TextAnchor.MiddleCenter;
			return style;
		}

		internal static void Init()
		{
			if (_ready)
			{
				return;
			}

			_ready = true;
			Window = Back(new GUIStyle(GUI.skin.window) { padding = new RectOffset(16, 16, 14, 14) }, new Color(0.11f, 0.12f, 0.13f, 0.98f), new Color(0.32f, 0.35f, 0.37f), 1);
			Card = Back(new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(0, 0, 0, 8) }, new Color(0.16f, 0.17f, 0.18f), new Color(0.24f, 0.26f, 0.27f), 1);
			AlertCard = Back(new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(0, 0, 4, 10) }, new Color(0.3f, 0.1f, 0.1f), Bad, 2);
			Alert = Back(new GUIStyle(GUI.skin.window) { padding = new RectOffset(24, 24, 20, 20) }, new Color(0.17f, 0.06f, 0.06f, 0.99f), Bad, 3);

			Title = Label(21, Color.white, FontStyle.Bold);
			Muted = Label(13, Dim);
			Body = Label(14, Text);
			Plain = Label(14, Dim);
			Move = Label(14, Color.white);
			Problem = Label(14, Warn, FontStyle.Bold);
			AlertTitle = Label(26, Bad, FontStyle.Bold);
			AlertText = Label(19, Color.white, FontStyle.Bold);

			Toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 16, fontStyle = FontStyle.Bold, padding = new RectOffset(24, 0, 2, 2) };
			TextColor(Toggle, Color.white);

			Button = Press(new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(12, 12, 7, 7), margin = new RectOffset(2, 2, 3, 3) }, new Color(0.25f, 0.27f, 0.29f), new Color(0.33f, 0.36f, 0.38f), new Color(0.19f, 0.2f, 0.22f));
			Small = Press(new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 2, 2), fixedWidth = 30 }, new Color(0.25f, 0.27f, 0.29f), new Color(0.33f, 0.36f, 0.38f), new Color(0.19f, 0.2f, 0.22f));
			Primary = Press(new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold, padding = new RectOffset(18, 18, 9, 9), margin = new RectOffset(2, 2, 3, 3) }, new Color(0.2f, 0.5f, 0.3f), new Color(0.26f, 0.6f, 0.37f), new Color(0.15f, 0.4f, 0.24f));

			ChipOk = Chip(new Color(0.18f, 0.42f, 0.25f));
			ChipTodo = Chip(new Color(0.2f, 0.36f, 0.58f));
			ChipBad = Chip(new Color(0.62f, 0.2f, 0.18f));
			ChipOff = Chip(new Color(0.3f, 0.32f, 0.33f));
		}
	}
}
