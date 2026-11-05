using System;
using System.Collections.Generic;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GetReady
{
	// Builds pieces of Unity UI in code, for the windows made from a copy of a game window: panels, rows, text in the game's own
	// font, bars, buttons and a scroll view. Sizes come from layout groups, so nothing is placed by hand.
	internal static class UiKit
	{
		internal static TMP_FontAsset Font;

		// The size of the game window's caption text: the other sizes are fractions of it, so the window follows the game's UI scale
		internal static float BaseUnit = 20f;

		// The size the owner chose for what is inside the window (1 = as the game draws it)
		internal static float Scale = 1f;

		internal static float Unit => BaseUnit * Scale;

		// The size the owner chose in the settings (Scale is what the window was last drawn at)
		internal static float ChosenScale => Mathf.Clamp(SettingsStore.Current.UiScale, 0.8f, 2f);

		internal static readonly Color PanelColor = new Color(0.12f, 0.125f, 0.125f);
		internal static readonly Color CardColor = new Color(0.155f, 0.16f, 0.16f);
		internal static readonly Color InnerColor = new Color(0.07f, 0.075f, 0.075f);
		// The edges of the old window's boxes: sections, cards and the inner panels of a card
		internal static readonly Color SectionEdge = new Color(0.33f, 0.34f, 0.32f);
		internal static readonly Color CardEdge = new Color(0.27f, 0.28f, 0.27f);
		internal static readonly Color InnerEdge = new Color(0.22f, 0.23f, 0.22f);
		internal static readonly Color LineColor = new Color(0.3f, 0.31f, 0.3f);
		internal static readonly Color ProblemColor = new Color(0.3f, 0.13f, 0.12f);
		internal static readonly Color TodoColor = new Color(0.24f, 0.21f, 0.11f);
		internal static readonly Color OkColor = new Color(0.12f, 0.22f, 0.13f);

		internal static void Init(TMP_Text sample)
		{
			Font = sample.font;
			BaseUnit = sample.fontSize > 4f ? sample.fontSize : 20f;
		}

		internal static RectTransform Node(string name, Transform parent)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.layer = parent.gameObject.layer;
			go.transform.SetParent(parent, false);
			return (RectTransform)go.transform;
		}

		internal static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = new Vector2(left, bottom);
			rect.offsetMax = new Vector2(-right, -top);
		}

		private static Image Fill(RectTransform rect, Color color, bool raycast = false)
		{
			var image = rect.gameObject.AddComponent<Image>();
			image.color = color;
			image.raycastTarget = raycast;
			return image;
		}

		internal static LayoutElement Size(Component part, float width = -1f, float height = -1f, float flexWidth = -1f, float flexHeight = -1f)
		{
			var element = part.GetComponent<LayoutElement>() ?? part.gameObject.AddComponent<LayoutElement>();
			if (width >= 0f)
			{
				element.preferredWidth = width;
			}

			if (height >= 0f)
			{
				element.preferredHeight = height;
				element.minHeight = height;
			}

			if (flexWidth >= 0f)
			{
				element.flexibleWidth = flexWidth;
			}

			if (flexHeight >= 0f)
			{
				element.flexibleHeight = flexHeight;
			}

			return element;
		}

		// A 1 px edge round a box that has a fill
		private static void Edge(RectTransform rect, Color? edge)
		{
			if (!edge.HasValue || rect.GetComponent<Image>() == null)
			{
				return;
			}

			var outline = rect.gameObject.AddComponent<Outline>();
			outline.effectColor = edge.Value;
			outline.effectDistance = new Vector2(1f, 1f);
			outline.useGraphicAlpha = false;
		}

		internal static RectTransform VBox(Transform parent, float spacing = 4f, RectOffset padding = null, Color? background = null, Color? edge = null)
		{
			var rect = Node("VBox", parent);
			if (background.HasValue)
			{
				Fill(rect, background.Value);
				Edge(rect, edge);
			}

			var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
			group.spacing = spacing;
			group.padding = padding ?? new RectOffset(0, 0, 0, 0);
			group.childControlWidth = true;
			group.childControlHeight = true;
			group.childForceExpandWidth = true;
			group.childForceExpandHeight = false;
			return rect;
		}

		internal static RectTransform HBox(Transform parent, float spacing = 6f, RectOffset padding = null, Color? background = null, bool expandHeight = false, Color? edge = null)
		{
			var rect = Node("HBox", parent);
			if (background.HasValue)
			{
				Fill(rect, background.Value);
				Edge(rect, edge);
			}

			var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
			group.spacing = spacing;
			group.padding = padding ?? new RectOffset(0, 0, 0, 0);
			group.childAlignment = TextAnchor.MiddleLeft;
			group.childControlWidth = true;
			group.childControlHeight = true;
			group.childForceExpandWidth = false;
			group.childForceExpandHeight = expandHeight;
			return rect;
		}

		internal static RectTransform Spacer(Transform parent)
		{
			var rect = Node("Spacer", parent);
			Size(rect, flexWidth: 1f);
			return rect;
		}

		internal static RectTransform Gap(Transform parent, float width, float height = 0f)
		{
			var rect = Node("Gap", parent);
			Size(rect, width, height);
			return rect;
		}

		internal static RectTransform Line(Transform parent)
		{
			var rect = Node("Line", parent);
			Fill(rect, LineColor);
			Size(rect, height: 1f, flexWidth: 1f);
			return rect;
		}

		// Text in the game's font. size is a fraction of the game's caption size.
		internal static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Left, bool wrap = false, bool flex = false)
		{
			var rect = Node("Label", parent);
			var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
			if (Font != null)
			{
				label.font = Font;
			}

			label.fontSize = Unit * size;
			label.color = color;
			label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
			label.alignment = align;
			label.enableWordWrapping = wrap;
			label.overflowMode = TextOverflowModes.Overflow;
			label.raycastTarget = false;
			label.text = text ?? "";
			if (flex)
			{
				Size(rect, flexWidth: 1f);
			}

			return label;
		}

		// A small coloured tag
		internal static RectTransform Chip(Transform parent, string text, Color background)
		{
			var box = HBox(parent, 0f, new RectOffset(10, 10, 3, 3), background);
			Label(box, text, 0.7f, Color.white, align: TextAlignmentOptions.Center);
			return box;
		}

		// A square frame with a picture in it (the frame stays empty while the picture is null)
		internal static void Icon(Transform parent, Sprite sprite, float size, string tip = null)
		{
			// A 1 px grey edge round a dark square, like the item squares of the game
			var frame = Node("Icon", parent);
			Fill(frame, new Color(0.22f, 0.23f, 0.22f));
			Size(frame, size, size);
			Tip(frame, tip);
			var inside = Node("Fill", frame);
			Stretch(inside, 1f, 1f, 1f, 1f);
			Fill(inside, new Color(0.105f, 0.11f, 0.11f));
			if (sprite == null)
			{
				return;
			}

			var picture = Node("Picture", inside);
			Stretch(picture, 2f, 2f, 2f, 2f);
			var image = picture.gameObject.AddComponent<Image>();
			image.sprite = sprite;
			image.preserveAspect = true;
			image.raycastTarget = false;
		}

		// While a drag is going on the tooltips stay quiet (they would cover what is dragged over)
		private static bool _tipsHushed;

		internal static void HideTips(bool hush)
		{
			_tipsHushed = hush;
			if (hush)
			{
				ItemUiContext.Instance?.Tooltip?.Close();
			}
		}

		// The game's tooltip while the mouse is over the piece (it needs a picture to be hit, which every box and icon here has)
		internal static void Tip(Component part, string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}

			// A piece with no picture of its own gets an invisible one, so the mouse can hit it
			var graphic = part.GetComponent<Graphic>();
			if (graphic == null)
			{
				var clear = part.gameObject.AddComponent<Image>();
				clear.color = new Color(0f, 0f, 0f, 0f);
				graphic = clear;
			}

			graphic.raycastTarget = true;

			var hover = part.GetComponent<HoverTrigger>() ?? part.gameObject.AddComponent<HoverTrigger>();
			hover.OnHoverStart += _ => HoverEnter(part, text);
			hover.OnHoverEnd += _ => HoverExit(part);
		}

		// The pieces the mouse is over that have a tooltip, outermost first (a row, and a chip inside it): the one entered last shows, and when
		// the mouse leaves it the one below comes back, so two tooltips are never up at once
		private static readonly List<KeyValuePair<Component, string>> HoveredTips = new List<KeyValuePair<Component, string>>();

		private static void HoverEnter(Component part, string text)
		{
			HoveredTips.RemoveAll(pair => pair.Key == part);
			HoveredTips.Add(new KeyValuePair<Component, string>(part, text));
			ShowTopTip();
		}

		private static void HoverExit(Component part)
		{
			HoveredTips.RemoveAll(pair => pair.Key == part);
			ShowTopTip();
		}

		private static void ShowTopTip()
		{
			// Pieces that were taken away while the mouse was over them (the page was drawn again) are dropped
			HoveredTips.RemoveAll(pair => pair.Key == null);
			var tooltip = ItemUiContext.Instance?.Tooltip;
			if (tooltip == null)
			{
				return;
			}

			tooltip.Close();
			if (HoveredTips.Count > 0 && !_tipsHushed)
			{
				tooltip.Show(HoveredTips[HoveredTips.Count - 1].Value, null, 0.25f, null);
			}
		}

		// The three lines a row is dragged by (the reorder code looks for the child called Grip)
		internal static RectTransform Grip(Transform parent)
		{
			var grip = Node("Grip", parent);
			Fill(grip, new Color(0f, 0f, 0f, 0f), true);
			Size(grip, Unit * 1.5f, Unit * 1.5f);
			var lines = grip.gameObject.AddComponent<VerticalLayoutGroup>();
			lines.spacing = 3f;
			lines.childAlignment = TextAnchor.MiddleCenter;
			// Controlled, or each line keeps the 100 x 100 a new object starts with
			lines.childControlWidth = true;
			lines.childControlHeight = true;
			lines.childForceExpandWidth = false;
			lines.childForceExpandHeight = false;
			for (int i = 0; i < 3; i++)
			{
				var line = Node("Line", grip);
				Fill(line, new Color(0.62f, 0.63f, 0.6f));
				Size(line, Unit * 1.1f, 2f);
			}

			Tip(grip, "Drag to change the order");
			return grip;
		}

		private static Sprite _dashAcross, _dashDown;

		// A strip of dots that is repeated along an edge: 3 on, 3 off, one unit thick (the sprite is 100 pixels to the unit, like the canvas,
		// or one tile would be 100 units long and the line would look solid)
		private static Sprite Dashes(bool across)
		{
			int length = 6, thickness = 1;
			var texture = new Texture2D(across ? length : thickness, across ? thickness : length, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
			for (int x = 0; x < texture.width; x++)
			{
				for (int y = 0; y < texture.height; y++)
				{
					int along = across ? x : y;
					texture.SetPixel(x, y, along < 3 ? Color.white : new Color(1f, 1f, 1f, 0f));
				}
			}

			texture.Apply();
			return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
		}

		// A dotted frame inside a box, hidden until it is shown (the places something can be dropped on)
		internal static GameObject DottedFrame(RectTransform box, Color color)
		{
			if (_dashAcross == null)
			{
				_dashAcross = Dashes(true);
				_dashDown = Dashes(false);
			}

			var frame = Node("Dotted Frame", box);
			frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
			Stretch(frame, 0f, 0f, 0f, 0f);
			EdgeLine(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), _dashAcross, color);
			EdgeLine(frame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), _dashAcross, color);
			EdgeLine(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0f), _dashDown, color);
			EdgeLine(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(1f, 0f), _dashDown, color);
			frame.gameObject.SetActive(false);
			return frame.gameObject;
		}

		private static void EdgeLine(RectTransform frame, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Sprite dashes, Color color)
		{
			var line = Node("Edge", frame);
			line.anchorMin = anchorMin;
			line.anchorMax = anchorMax;
			line.pivot = pivot;
			line.sizeDelta = size;
			line.anchoredPosition = Vector2.zero;
			var image = line.gameObject.AddComponent<Image>();
			image.sprite = dashes;
			image.type = Image.Type.Tiled;
			image.color = color;
			image.raycastTarget = false;
		}

		private static Sprite _helpSprite, _helpHoverSprite;

		private static Sprite DiscSprite(Color fill, Color edge)
		{
			var texture = Disc(44, fill, edge);
			return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
		}

		// The round "?" of the window that explains something on hover: a dark disc with a gold edge, brighter under the mouse
		internal static void HelpMark(Transform parent, string tip)
		{
			if (_helpSprite == null)
			{
				_helpSprite = DiscSprite(new Color(0.17f, 0.18f, 0.18f), new Color(0.55f, 0.5f, 0.32f));
				_helpHoverSprite = DiscSprite(new Color(0.27f, 0.26f, 0.18f), new Color(0.8f, 0.72f, 0.45f));
			}

			var rect = Node("Help", parent);
			var image = rect.gameObject.AddComponent<Image>();
			image.sprite = _helpSprite;
			Size(rect, Unit * 1.4f, Unit * 1.4f);
			var mark = Label(rect, "?", 0.8f, new Color(0.88f, 0.85f, 0.74f), true, TextAlignmentOptions.Center);
			Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);
			Tip(rect, tip);
			var hover = rect.GetComponent<HoverTrigger>();
			hover.OnHoverStart += _ => image.sprite = _helpHoverSprite;
			hover.OnHoverEnd += _ => image.sprite = _helpSprite;
		}

		// A thin bar filled to a fraction
		internal static void Bar(Transform parent, float fraction, Color color)
		{
			var back = Node("Bar", parent);
			Fill(back, InnerColor);
			Size(back, height: 6f, flexWidth: 1f);
			var fill = Node("Fill", back);
			fill.anchorMin = Vector2.zero;
			fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
			fill.offsetMin = Vector2.zero;
			fill.offsetMax = Vector2.zero;
			Fill(fill, color);
		}

		// A box to type a whole number in. The value is handed over when the typing ends (Enter, Tab or a click elsewhere), not on every key:
		// the page is drawn again then, which would take the box away in the middle of typing.
		internal static TMP_InputField NumberField(Transform parent, string text, float width, float height, Action<string> done)
		{
			var rect = Node("Field", parent);
			var image = Fill(rect, InnerColor, true);
			Edge(rect, SectionEdge);
			rect.gameObject.SetActive(false);
			var viewport = Node("Viewport", rect);
			Stretch(viewport, 8f, 2f, 8f, 2f);
			viewport.gameObject.AddComponent<RectMask2D>();
			var label = Label(viewport, text, 0.9f, UiColors.Text, align: TextAlignmentOptions.Right);
			Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
			var field = rect.gameObject.AddComponent<TMP_InputField>();
			field.targetGraphic = image;
			field.textViewport = viewport;
			field.textComponent = label;
			field.contentType = TMP_InputField.ContentType.IntegerNumber;
			field.characterLimit = 9;
			field.text = text;
			field.onEndEdit.AddListener(value => done(value));
			rect.gameObject.SetActive(true);
			Size(rect, width, height);
			return field;
		}

		internal static Button PushButton(Transform parent, string text, Action click, Color background, Color hover, Color textColor, float width = -1f, float height = -1f, Color? edge = null)
		{
			var rect = Node("Button", parent);
			var image = Fill(rect, background, true);
			Edge(rect, edge);
			var button = rect.gameObject.AddComponent<Button>();
			button.targetGraphic = image;
			var colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(hover.r / Mathf.Max(background.r, 0.01f), hover.g / Mathf.Max(background.g, 0.01f), hover.b / Mathf.Max(background.b, 0.01f), 1f);
			colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
			colors.fadeDuration = 0.05f;
			button.colors = colors;
			if (click != null)
			{
				button.onClick.AddListener(() => click());
			}

			var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
			group.padding = new RectOffset(16, 16, 6, 6);
			group.childAlignment = TextAnchor.MiddleCenter;
			group.childControlWidth = true;
			group.childControlHeight = true;
			// No force-expand: a group that expands tells its parents it wants all the spare room, and the button would fill it
			group.childForceExpandWidth = false;
			group.childForceExpandHeight = false;
			Label(rect, text, 0.9f, textColor, align: TextAlignmentOptions.Center);
			Size(rect, width, height);
			return button;
		}

		// A canvas of its own for this part of the window (nested in the game's): what changes inside it (a hover, an animation, a scroll) is drawn
		// again for this part only, not for the whole of the game's screen it sits on. It needs its own raycaster to be clicked.
		internal static void OwnCanvas(GameObject target)
		{
			if (target.GetComponent<Canvas>() == null)
			{
				target.AddComponent<Canvas>();
			}

			if (target.GetComponent<GraphicRaycaster>() == null)
			{
				target.AddComponent<GraphicRaycaster>();
			}
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

		private const float ScrollbarWidth = 12f;

		// The bar on the right edge of a scroll view: a dark track and a grey handle the size of the visible part
		private static Scrollbar ScrollBar(Transform parent)
		{
			var track = Node("Scrollbar", parent);
			track.anchorMin = new Vector2(1f, 0f);
			track.anchorMax = new Vector2(1f, 1f);
			track.pivot = new Vector2(1f, 0.5f);
			track.sizeDelta = new Vector2(ScrollbarWidth, 0f);
			Fill(track, new Color(0.05f, 0.055f, 0.055f), true);
			// A thin light border round the track, as the game's own bar has
			var trackEdge = track.gameObject.AddComponent<Outline>();
			trackEdge.effectColor = new Color(0.45f, 0.46f, 0.44f);
			trackEdge.effectDistance = new Vector2(1f, 1f);
			var area = Node("Sliding Area", track);
			Stretch(area, 1f, 1f, 1f, 1f);
			var handle = Node("Handle", area);
			// A new object is 100 x 100: the scroll bar sets the handle along its own direction only, so the width is set here
			Stretch(handle, 0f, 0f, 0f, 0f);
			// Light, with no border of its own: the border is the track's
			var image = Fill(handle, new Color(0.68f, 0.69f, 0.66f), true);
			var bar = track.gameObject.AddComponent<Scrollbar>();
			bar.handleRect = handle;
			bar.targetGraphic = image;
			bar.direction = Scrollbar.Direction.BottomToTop;
			var colors = bar.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
			colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
			bar.colors = colors;
			return bar;
		}

		// A scroll view of the given content: mouse wheel and dragging, clipped to its box. content is where the pieces go.
		internal static ScrollRect Scroll(Transform parent, out RectTransform content)
		{
			var rect = Node("Scroll", parent);
			Fill(rect, InnerColor, true);
			var scroll = rect.gameObject.AddComponent<ScrollRect>();
			scroll.horizontal = false;
			scroll.vertical = true;
			scroll.movementType = ScrollRect.MovementType.Clamped;
			scroll.scrollSensitivity = 40f;

			var viewport = Node("Viewport", rect);
			Stretch(viewport, 0f, 0f, ScrollbarWidth + 4f, 0f);
			viewport.gameObject.AddComponent<RectMask2D>();
			Fill(viewport, new Color(0f, 0f, 0f, 0f), true);

			content = Node("Content", viewport);
			content.anchorMin = new Vector2(0f, 1f);
			content.anchorMax = new Vector2(1f, 1f);
			content.pivot = new Vector2(0.5f, 1f);
			content.offsetMin = new Vector2(0f, 0f);
			content.offsetMax = new Vector2(0f, 0f);
			// Its own canvas: scrolling moves it, and does not make every element in it again
			OwnCanvas(content.gameObject);
			var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
			layout.spacing = 8f;
			layout.padding = new RectOffset(8, 8, 8, 8);
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;
			var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			scroll.viewport = viewport;
			scroll.content = content;
			scroll.verticalScrollbar = ScrollBar(rect);
			// The bar is there only when the content is longer than the view (the view then gives up the room for it)
			scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
			scroll.verticalScrollbarSpacing = 4f;
			return scroll;
		}
	}
}
