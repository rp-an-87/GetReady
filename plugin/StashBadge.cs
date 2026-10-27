using UnityEngine;
using UnityEngine.UI;

namespace GetReady
{
	// The count on the corner of the stash button: red with the number of problems, else gold with the number of things to fulfil.
	internal static class StashBadge
	{
		private static GameObject _badge;

		private static Image _badgeImage;

		private static Text _badgeText;

		// A small count on the button: red with the number of problems, else gold with the number of things to bring
		internal static void Set(int problems, int todo)
		{
			if (_badge == null)
			{
				return;
			}

			int count = problems > 0 ? problems : todo;
			_badge.SetActive(count > 0);
			_badgeImage.color = problems > 0 ? new Color(0.78f, 0.2f, 0.17f) : new Color(0.62f, 0.52f, 0.2f);
			_badgeText.text = count.ToString();
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
			_badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
			_badge.transform.SetParent(icon, false);
			var rect = (RectTransform)_badge.transform;
			rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(15f, 15f);
			rect.anchoredPosition = new Vector2(-1f, -1f);
			_badgeImage = _badge.GetComponent<Image>();
			_badgeImage.sprite = Dot();
			_badgeImage.raycastTarget = false;

			var label = new GameObject("Count", typeof(RectTransform), typeof(Text));
			label.transform.SetParent(_badge.transform, false);
			var labelRect = (RectTransform)label.transform;
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
			_badgeText = label.GetComponent<Text>();
			_badgeText.raycastTarget = false;
			_badgeText.alignment = TextAnchor.MiddleCenter;
			_badgeText.fontSize = 10;
			_badgeText.fontStyle = FontStyle.Bold;
			_badgeText.color = Color.white;
			_badgeText.font = Font.CreateDynamicFontFromOSFont("Arial", 10);
			_badge.SetActive(false);
		}
	}
}
