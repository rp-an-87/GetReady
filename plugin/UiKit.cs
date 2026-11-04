using System;
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
		internal static float Unit = 20f;

		internal static readonly Color PanelColor = new Color(0.12f, 0.125f, 0.125f);
		internal static readonly Color CardColor = new Color(0.155f, 0.16f, 0.16f);
		internal static readonly Color InnerColor = new Color(0.07f, 0.075f, 0.075f);
		internal static readonly Color LineColor = new Color(0.3f, 0.31f, 0.3f);
		internal static readonly Color ProblemColor = new Color(0.3f, 0.13f, 0.12f);
		internal static readonly Color TodoColor = new Color(0.24f, 0.21f, 0.11f);
		internal static readonly Color OkColor = new Color(0.12f, 0.22f, 0.13f);

		internal static void Init(TMP_Text sample)
		{
			Font = sample.font;
			Unit = sample.fontSize > 4f ? sample.fontSize : 20f;
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

		internal static RectTransform VBox(Transform parent, float spacing = 4f, RectOffset padding = null, Color? background = null)
		{
			var rect = Node("VBox", parent);
			if (background.HasValue)
			{
				Fill(rect, background.Value);
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

		internal static RectTransform HBox(Transform parent, float spacing = 6f, RectOffset padding = null, Color? background = null, bool expandHeight = false)
		{
			var rect = Node("HBox", parent);
			if (background.HasValue)
			{
				Fill(rect, background.Value);
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
		internal static void Chip(Transform parent, string text, Color background)
		{
			var box = HBox(parent, 0f, new RectOffset(10, 10, 3, 3), background);
			Label(box, text, 0.7f, Color.white, align: TextAlignmentOptions.Center);
		}

		// A square frame with a picture in it (the frame stays empty while the picture is null)
		internal static void Icon(Transform parent, Sprite sprite, float size)
		{
			var frame = Node("Icon", parent);
			Fill(frame, InnerColor);
			Size(frame, size, size);
			if (sprite == null)
			{
				return;
			}

			var picture = Node("Picture", frame);
			Stretch(picture, 2f, 2f, 2f, 2f);
			var image = picture.gameObject.AddComponent<Image>();
			image.sprite = sprite;
			image.preserveAspect = true;
			image.raycastTarget = false;
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

		internal static Button PushButton(Transform parent, string text, Action click, Color background, Color hover, Color textColor, float width = -1f, float height = -1f)
		{
			var rect = Node("Button", parent);
			var image = Fill(rect, background, true);
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
			group.childForceExpandWidth = true;
			group.childForceExpandHeight = true;
			Label(rect, text, 0.9f, textColor, align: TextAlignmentOptions.Center);
			Size(rect, width, height);
			return button;
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
			Stretch(viewport, 0f, 0f, 0f, 0f);
			viewport.gameObject.AddComponent<RectMask2D>();
			Fill(viewport, new Color(0f, 0f, 0f, 0f), true);

			content = Node("Content", viewport);
			content.anchorMin = new Vector2(0f, 1f);
			content.anchorMax = new Vector2(1f, 1f);
			content.pivot = new Vector2(0.5f, 1f);
			content.offsetMin = new Vector2(0f, 0f);
			content.offsetMax = new Vector2(0f, 0f);
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
			return scroll;
		}
	}
}
