using System.Linq;
using TMPro;
using UnityEngine;

namespace GetReady
{
	// The look of the window: the game's dark panels, its warm grey text and its font (when it can be found), built once from
	// flat-colour textures (no assets). Call Init from OnGUI.
	internal static class Ui
	{
		internal static readonly Color Heading = new Color(0.88f, 0.85f, 0.74f);
		internal static readonly Color Text = new Color(0.88f, 0.88f, 0.84f);
		internal static readonly Color Dim = new Color(0.65f, 0.66f, 0.63f);
		internal static readonly Color Sand = new Color(0.72f, 0.65f, 0.42f);
		internal static readonly Color Ok = new Color(0.45f, 0.78f, 0.5f);
		internal static readonly Color Warn = new Color(1f, 0.64f, 0.28f);
		internal static readonly Color Bad = new Color(1f, 0.4f, 0.36f);

		internal static GUIStyle Window, Section, Group, Inner, Divider, Field, ValueOk, ValueWarn, ValueBad, ValueMuted, Title, SectionTitle, Name, Body, Muted, Move, Problem, ProblemBox,
			Button, Small, Primary, Pick, PickOn, Tab, TabOn, JumpTab, JumpTabOff, JumpTabOn, OkCard, TodoCard, SwitchOn, SwitchOff, Centered,
			ChipOk, ChipTodo, ChipBad, ChipOff, AlertCard, AlertTitle, AlertText, AlertDetail, Alert, Tip, TipText, Help, Todo, ProblemTitle, TodoTitle, ProblemBoxTop, TodoBoxTop, Plain, PlainMuted, PlainName, Note, PlainWarn, Arrow;

		// The Used / Not used switches of the rows: no margin, so they line up with the picture beside them
		internal static GUIStyle PickRow, PickOnRow;

		internal static Font Font;
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

		// A filled circle with an edge, smooth at its rim
		private static Texture2D Disc(int size, Color fill, Color edge)
		{
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			float radius = size / 2f;
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
					var color = distance > radius - 1.6f ? edge : fill;
					color.a *= Mathf.Clamp01(radius - distance);
					texture.SetPixel(x, y, color);
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

		private static GUIStyle Label(int size, Color color, FontStyle fontStyle = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
		{
			var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, wordWrap = true, richText = false, alignment = anchor, padding = new RectOffset(0, 0, 2, 2) };
			TextColor(style, color);
			return style;
		}

		private static GUIStyle Panel(Color fill, Color edge, int edgeWidth, RectOffset padding, RectOffset margin) =>
			Back(new GUIStyle(GUI.skin.box) { padding = padding, margin = margin }, fill, edge, edgeWidth);

		private static GUIStyle Chip(Color fill)
		{
			var style = Back(new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, stretchWidth = false, padding = new RectOffset(10, 10, 3, 3) }, fill, fill, 1);
			TextColor(style, Color.white);
			return style;
		}

		private static GUIStyle Press(GUIStyle style, Color fill, Color hover, Color down, Color edge, Color text)
		{
			Back(style, fill, edge, 1);
			style.hover.background = Box(hover, edge, 1);
			style.active.background = Box(down, edge, 1);
			style.onNormal.background = style.normal.background;
			style.onHover.background = style.hover.background;
			style.onActive.background = style.active.background;
			TextColor(style, text);
			style.alignment = TextAnchor.MiddleCenter;
			return style;
		}

		// The game's own font: its TextMeshPro font assets keep the plain font they were made from
		private static Font FindFont()
		{
			try
			{
				var assets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
				var fonts = Resources.FindObjectsOfTypeAll<Font>();
				if (!_fontsLogged)
				{
					_fontsLogged = true;
					Plugin.Log.LogInfo($"Fonts loaded: TMP assets [{string.Join(", ", assets.Select(a => a.name + (a.sourceFontFile != null ? "*" : "")).Distinct().Take(40).ToArray())}], fonts [{string.Join(", ", fonts.Select(f => f.name).Distinct().Take(40).ToArray())}]");
				}

				var asset = assets.FirstOrDefault(candidate => candidate.name.ToLowerInvariant().Contains("bender") && candidate.sourceFontFile != null);
				if (asset != null)
				{
					return asset.sourceFontFile;
				}

				// A plain font with the game's name
				var plain = fonts.FirstOrDefault(candidate => candidate.name.ToLowerInvariant().Contains("bender"));
				if (plain != null)
				{
					return plain;
				}

				// The font a live label of the game is drawn with
				foreach (var label in Resources.FindObjectsOfTypeAll<TMP_Text>())
				{
					if (label.font != null && label.font.sourceFontFile != null)
					{
						return label.font.sourceFontFile;
					}
				}

				return null;
			}
			catch
			{
				return null;
			}
		}

		private static bool _fontsLogged;
		private static float _fontTriedAt = float.NegativeInfinity;

		private static void ApplyFont()
		{
			foreach (var style in new[] { Field, ValueOk, ValueWarn, ValueBad, ValueMuted, Window, Section, Group, Inner, Title, SectionTitle, Name, Body, Muted, Move, Problem, ProblemBox, Centered, AlertTitle, AlertText, AlertDetail, Alert, AlertCard, Tip, TipText, Help, ProblemTitle, TodoTitle,
				Button, Small, Primary, Pick, PickOn, PickRow, PickOnRow, Tab, TabOn, JumpTab, JumpTabOff, JumpTabOn, OkCard, TodoCard, SwitchOn, SwitchOff, ChipOk, ChipTodo, ChipBad, ChipOff })
			{
				style.font = Font;
			}
		}

		internal static void Init()
		{
			if (_ready)
			{
				// The game's screens load their text after the plugin: look again now and then until the font turns up
				if (Font == null && Time.unscaledTime - _fontTriedAt > 3f)
				{
					_fontTriedAt = Time.unscaledTime;
					Font = FindFont();
					if (Font != null)
					{
						Plugin.Log.LogInfo($"Using the game font {Font.name} (found late)");
						ApplyFont();
					}
				}

				return;
			}

			_ready = true;
			// The text boxes' caret: the skin's own may be dark or never flash under the game, and the box says nothing without it
			GUI.skin.settings.cursorColor = Heading;
			GUI.skin.settings.cursorFlashSpeed = 1.6f;
			GUI.skin.settings.selectionColor = new Color(0.72f, 0.65f, 0.42f, 0.45f);
			Font = FindFont();
			_fontTriedAt = Time.unscaledTime;
			Plugin.Log.LogInfo(Font != null ? $"Using the game font {Font.name}" : "The game font was not found, using the default one");

			var edge = new Color(0.27f, 0.28f, 0.27f);
			Window = Back(new GUIStyle(GUI.skin.window) { padding = new RectOffset(16, 16, 14, 14) }, new Color(0.085f, 0.09f, 0.09f, 0.985f), new Color(0.36f, 0.37f, 0.35f), 2);
			Section = Panel(new Color(0.12f, 0.125f, 0.125f), new Color(0.33f, 0.34f, 0.32f), 1, new RectOffset(12, 12, 10, 12), new RectOffset(0, 0, 0, 12));
			Group = Panel(new Color(0.16f, 0.165f, 0.16f), edge, 1, new RectOffset(10, 10, 8, 8), new RectOffset(0, 0, 4, 4));
			Inner = Panel(new Color(0.105f, 0.11f, 0.11f), new Color(0.22f, 0.23f, 0.22f), 1, new RectOffset(8, 8, 6, 6), new RectOffset(0, 0, 4, 2));
			Divider = Back(new GUIStyle(GUI.skin.box) { fixedHeight = 1, margin = new RectOffset(0, 0, 6, 6), padding = new RectOffset(0, 0, 0, 0) }, new Color(0.33f, 0.34f, 0.32f), new Color(0.33f, 0.34f, 0.32f), 0);
			AlertCard = Panel(new Color(0.27f, 0.09f, 0.09f), Bad, 2, new RectOffset(12, 12, 10, 10), new RectOffset(0, 0, 4, 10));
			Alert = Back(new GUIStyle(GUI.skin.window) { padding = new RectOffset(24, 24, 20, 20) }, new Color(0.15f, 0.05f, 0.05f, 0.99f), Bad, 3);
			Tip = Back(new GUIStyle(GUI.skin.window) { padding = new RectOffset(0, 0, 0, 0) }, new Color(0.075f, 0.08f, 0.08f, 0.995f), new Color(0.5f, 0.46f, 0.3f), 1);
			TipText = Label(15, Text);
			TipText.wordWrap = true;
			TipText.padding = new RectOffset(9, 9, 6, 6);
			Help = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 1), margin = new RectOffset(2, 8, 3, 3), fixedWidth = 22, fixedHeight = 22 };
			var disc = Disc(44, new Color(0.17f, 0.18f, 0.18f), new Color(0.55f, 0.5f, 0.32f));
			var discHover = Disc(44, new Color(0.27f, 0.26f, 0.18f), new Color(0.8f, 0.72f, 0.45f));
			Help.normal.background = disc;
			Help.hover.background = discHover;
			Help.normal.textColor = Sand;
			Help.hover.textColor = new Color(0.95f, 0.9f, 0.72f);
			ProblemBox = Panel(new Color(0.26f, 0.1f, 0.1f), new Color(0.62f, 0.22f, 0.2f), 1, new RectOffset(8, 8, 4, 4), new RectOffset(0, 0, 8, 2));

			// The two summary boxes at the top of the Ready page: the same size and margins, only the colours differ
			ProblemBoxTop = Panel(new Color(0.27f, 0.09f, 0.09f), Bad, 2, new RectOffset(14, 14, 10, 10), new RectOffset(0, 0, 0, 10));
			TodoBoxTop = Panel(new Color(0.22f, 0.2f, 0.12f), Sand, 2, new RectOffset(14, 14, 10, 10), new RectOffset(0, 0, 0, 10));
			OkCard = Panel(new Color(0.12f, 0.22f, 0.13f), new Color(0.35f, 0.6f, 0.38f), 2, new RectOffset(14, 14, 10, 10), new RectOffset(0, 0, 0, 10));
			TodoCard = Panel(new Color(0.22f, 0.2f, 0.12f), Sand, 2, new RectOffset(14, 14, 10, 10), new RectOffset(0, 0, 0, 10));

			Field = Back(new GUIStyle(GUI.skin.textField) { fontSize = 16, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(6, 6, 4, 4), margin = new RectOffset(2, 2, 2, 2), fixedHeight = 30 }, new Color(0.07f, 0.075f, 0.075f), new Color(0.38f, 0.39f, 0.37f), 1);
			TextColor(Field, Color.white);
			ValueOk = Label(16, Ok, FontStyle.Normal, TextAnchor.MiddleRight);
			ValueWarn = Label(16, Warn, FontStyle.Normal, TextAnchor.MiddleRight);
			ValueBad = Label(16, Bad, FontStyle.Normal, TextAnchor.MiddleRight);
			ValueMuted = Label(16, Dim, FontStyle.Normal, TextAnchor.MiddleRight);

			Title = Label(23, Heading, FontStyle.Bold);
			SectionTitle = Label(18, Heading, FontStyle.Bold);
			Name = Label(17, Heading);
			Body = Label(16, Text);
			Muted = Label(14, Dim);
			Move = Label(16, Color.white);
			Plain = new GUIStyle(Body) { wordWrap = false };
			PlainMuted = new GUIStyle(Muted) { wordWrap = false };
			PlainName = new GUIStyle(Name) { wordWrap = false };
			Note = new GUIStyle(Body) { wordWrap = false };
			Note.normal.textColor = Bad;
			PlainWarn = new GUIStyle(Body) { wordWrap = false };
			PlainWarn.normal.textColor = Warn;
			Arrow = Label(16, Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
			Problem = Label(16, Bad);
			Todo = Label(16, Warn);
			ProblemTitle = Label(18, Bad);
			TodoTitle = Label(18, Warn);
			Centered = Label(16, Text, FontStyle.Normal, TextAnchor.MiddleCenter);
			AlertTitle = Label(26, Bad, FontStyle.Bold);
			AlertText = Label(19, Color.white, FontStyle.Bold);
			AlertDetail = Label(18, Text);

			var fill = new Color(0.2f, 0.21f, 0.21f);
			var hover = new Color(0.27f, 0.28f, 0.27f);
			var down = new Color(0.14f, 0.15f, 0.15f);
			Button = Press(new GUIStyle(GUI.skin.button) { fontSize = 16, padding = new RectOffset(12, 12, 6, 6), margin = new RectOffset(2, 2, 3, 3) }, fill, hover, down, new Color(0.38f, 0.39f, 0.37f), Text);
			Small = Press(new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 2, 3), fixedWidth = 28, fixedHeight = 26, margin = new RectOffset(2, 2, 2, 2) }, fill, hover, down, new Color(0.38f, 0.39f, 0.37f), Heading);
			Tab = Press(new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(22, 22, 5, 6), margin = new RectOffset(0, 4, 0, 0), fixedHeight = 36 }, new Color(0.13f, 0.135f, 0.135f), hover, down, new Color(0.3f, 0.31f, 0.3f), Text);
			TabOn = Press(new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(22, 22, 5, 6), margin = new RectOffset(0, 4, 0, 0), fixedHeight = 36 }, new Color(0.25f, 0.23f, 0.15f), new Color(0.31f, 0.29f, 0.19f), new Color(0.2f, 0.18f, 0.11f), Sand, new Color(0.95f, 0.9f, 0.72f));
			JumpTab = Press(new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(9, 9, 2, 3), margin = new RectOffset(0, 4, 0, 4), fixedHeight = 26 }, new Color(0.15f, 0.155f, 0.155f), hover, down, new Color(0.3f, 0.31f, 0.3f), Text);
			JumpTabOff = Press(new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(9, 9, 2, 3), margin = new RectOffset(0, 4, 0, 4), fixedHeight = 26 }, new Color(0.13f, 0.135f, 0.135f), hover, down, new Color(0.25f, 0.26f, 0.25f), Dim);
			JumpTabOn = Press(new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(9, 9, 2, 3), margin = new RectOffset(0, 4, 0, 4), fixedHeight = 26 }, new Color(0.25f, 0.23f, 0.15f), new Color(0.31f, 0.29f, 0.19f), new Color(0.2f, 0.18f, 0.11f), Sand, new Color(0.95f, 0.9f, 0.72f));
			Primary = Press(new GUIStyle(GUI.skin.button) { fontSize = 19, fontStyle = FontStyle.Bold, padding = new RectOffset(26, 26, 9, 9), margin = new RectOffset(2, 2, 3, 3) }, new Color(0.24f, 0.36f, 0.19f), new Color(0.3f, 0.44f, 0.23f), new Color(0.18f, 0.28f, 0.14f), new Color(0.5f, 0.62f, 0.38f), Color.white);
			Pick = Press(new GUIStyle(GUI.skin.button) { fontSize = 15, padding = new RectOffset(8, 8, 3, 4), margin = new RectOffset(2, 2, 2, 2), fixedHeight = 30 }, new Color(0.15f, 0.155f, 0.155f), hover, down, new Color(0.3f, 0.31f, 0.3f), Text);
			PickOn = Press(new GUIStyle(GUI.skin.button) { fontSize = 15, padding = new RectOffset(8, 8, 3, 4), margin = new RectOffset(2, 2, 2, 2), fixedHeight = 30 }, new Color(0.25f, 0.23f, 0.15f), new Color(0.31f, 0.29f, 0.19f), new Color(0.2f, 0.18f, 0.11f), Sand, new Color(0.95f, 0.9f, 0.72f));
			PickRow = new GUIStyle(Pick) { margin = new RectOffset(0, 0, 0, 0) };
			PickOnRow = new GUIStyle(PickOn) { margin = new RectOffset(0, 0, 0, 0) };
			SwitchOn = Press(new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 3, 4), fixedWidth = 46, fixedHeight = 24, margin = new RectOffset(0, 8, 2, 2) }, new Color(0.24f, 0.36f, 0.19f), new Color(0.3f, 0.44f, 0.23f), new Color(0.18f, 0.28f, 0.14f), new Color(0.5f, 0.62f, 0.38f), Color.white);
			SwitchOff = Press(new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 3, 4), fixedWidth = 46, fixedHeight = 24, margin = new RectOffset(0, 8, 2, 2) }, new Color(0.16f, 0.165f, 0.165f), hover, down, new Color(0.3f, 0.31f, 0.3f), Dim);

			ChipOk = Chip(new Color(0.2f, 0.38f, 0.22f));
			ChipTodo = Chip(new Color(0.42f, 0.36f, 0.16f));
			ChipBad = Chip(new Color(0.6f, 0.2f, 0.18f));
			ChipOff = Chip(new Color(0.26f, 0.27f, 0.27f));

			// One-line texts never wrap: under the window scale their measured width can be a pixel short, and "Off" would break into "Of" and "f"
			foreach (var style in new[] { ValueOk, ValueWarn, ValueBad, ValueMuted, Title, SectionTitle, Name, Centered, ChipOk, ChipTodo, ChipBad, ChipOff })
			{
				style.wordWrap = false;
			}

			if (Font != null)
			{
				ApplyFont();
			}
		}
	}
}
