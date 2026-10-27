using System;
using UnityEngine;

namespace GetReady
{
	// The stash button's icon: the game's backpack icon, or a checklist drawn in code when the game has none (white on
	// transparent, tinted by the button).
	internal static class ButtonIcon
	{
		private const int Size = 64;

		private static Sprite _ready;

		// The stash button's icon: the game's backpack icon, or the drawn checklist when the game has none
		internal static Sprite Get()
		{
			try
			{
				var sprite = EFTHardSettings.Instance?.StaticIcons?.GetItemTypeIcon(EItemType.Backpack);
				if (sprite != null)
				{
					return sprite;
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Game icon not available: {e.Message}");
			}

			return Drawn();
		}

		private static Sprite Drawn()
		{
			if (_ready != null)
			{
				return _ready;
			}

			var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			var pixels = new Color32[Size * Size];
			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					// y counts from the top in the drawing, the texture from the bottom
					float alpha = Coverage(new Vector2(x + 0.5f, Size - y - 0.5f));
					pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
				}
			}

			texture.SetPixels32(pixels);
			texture.Apply();
			_ready = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
			_ready.hideFlags = HideFlags.HideAndDontSave;
			return _ready;
		}

		private static float Coverage(Vector2 p)
		{
			// The board (outline), the clip on top (filled)
			float d = Math.Abs(RoundRect(p, new Vector2(32, 35), new Vector2(19, 25), 5)) - 1.9f;
			d = Math.Min(d, RoundRect(p, new Vector2(32, 9), new Vector2(10, 5), 3));

			// Three rows: a tick and a line
			for (int row = 0; row < 3; row++)
			{
				float y = 25 + row * 12;
				d = Math.Min(d, Segment(p, new Vector2(18, y), new Vector2(21.5f, y + 3.5f)) - 1.7f);
				d = Math.Min(d, Segment(p, new Vector2(21.5f, y + 3.5f), new Vector2(27.5f, y - 3.5f)) - 1.7f);
				d = Math.Min(d, Segment(p, new Vector2(33, y), new Vector2(46, y)) - 1.7f);
			}

			return Mathf.Clamp01(0.5f - d);
		}

		// Signed distance to a rounded rectangle (negative inside)
		private static float RoundRect(Vector2 p, Vector2 center, Vector2 half, float radius)
		{
			var q = new Vector2(Math.Abs(p.x - center.x), Math.Abs(p.y - center.y)) - half + new Vector2(radius, radius);
			return new Vector2(Math.Max(q.x, 0), Math.Max(q.y, 0)).magnitude + Math.Min(Math.Max(q.x, q.y), 0) - radius;
		}

		// Distance to a line segment
		private static float Segment(Vector2 p, Vector2 a, Vector2 b)
		{
			var ab = b - a;
			float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
			return (p - (a + ab * t)).magnitude;
		}
	}
}
