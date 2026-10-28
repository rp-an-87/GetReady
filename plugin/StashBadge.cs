using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GetReady
{
	// The count on the corner of the stash button: red with the number of problems, else gold with the number of things to fulfil.
	// There is one button per stash panel (the main menu and the hideout each have one), so every badge is kept and all of them are
	// updated; destroyed ones are dropped, and a new one shows the current count at once.
	internal static class StashBadge
	{
		private class Badge
		{
			public GameObject Object;
			public Image Image;
			public Text Text;
		}

		private static readonly List<Badge> Badges = new List<Badge>();

		private static int _problems;

		private static int _todo;

		// A small count on the button: red with the number of problems, else gold with the number of things to bring
		internal static void Set(int problems, int todo)
		{
			_problems = problems;
			_todo = todo;
			Badges.RemoveAll(badge => badge.Object == null);
			foreach (var badge in Badges)
			{
				Apply(badge);
			}
		}

		private static void Apply(Badge badge)
		{
			int count = _problems > 0 ? _problems : _todo;
			badge.Object.SetActive(count > 0);
			badge.Image.color = _problems > 0 ? new Color(0.78f, 0.2f, 0.17f) : new Color(0.62f, 0.52f, 0.2f);
			badge.Text.text = count.ToString();
		}

		private static Sprite _dot;

		// A small round dot (drawn in code)
		private static Sprite Dot()
		{
			if (_dot != null)
			{
				return _dot;
			}

			const int size = 32;
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
					texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(size / 2f - distance)));
				}
			}

			texture.Apply();
			_dot = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
			return _dot;
		}

		// The count sits on the top right corner of the icon, small enough to leave the icon visible
		internal static void Add(Transform icon)
		{
			var made = new GameObject("Badge", typeof(RectTransform), typeof(Image));
			made.transform.SetParent(icon, false);
			var rect = (RectTransform)made.transform;
			rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(15f, 15f);
			rect.anchoredPosition = new Vector2(-1f, -1f);
			var image = made.GetComponent<Image>();
			image.sprite = Dot();
			image.raycastTarget = false;

			var label = new GameObject("Count", typeof(RectTransform), typeof(Text));
			label.transform.SetParent(made.transform, false);
			var labelRect = (RectTransform)label.transform;
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
			var text = label.GetComponent<Text>();
			text.raycastTarget = false;
			text.alignment = TextAnchor.MiddleCenter;
			text.fontSize = 10;
			text.fontStyle = FontStyle.Bold;
			text.color = Color.white;
			text.font = Font.CreateDynamicFontFromOSFont("Arial", 10);

			var badge = new Badge { Object = made, Image = image, Text = text };
			Badges.RemoveAll(other => other.Object == null);
			Badges.Add(badge);
			Apply(badge);
		}
	}
}
