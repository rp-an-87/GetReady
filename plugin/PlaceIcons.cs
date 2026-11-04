using UnityEngine;

namespace GetReady
{
	// The small picture of a place (where something is brought to): the game's own icon of the kind of item that goes there, the ones the
	// traders and the barter windows use (rig for the vest, backpack, container for the pouch, the special icon for the special slots). The
	// game has none for the pockets, so that one is drawn here. Null when the game has not got the picture (then the chip shows the name).
	internal static class PlaceIcons
	{
		private static Sprite _pockets;

		internal static Sprite Of(string place)
		{
			switch (place)
			{
				case "Pockets": return _pockets ?? (_pockets = DrawPockets());
				case "Vest": return Icons.TypeSprite(EItemType.Rig);
				case "Backpack": return Icons.TypeSprite(EItemType.Backpack);
				case "Pouch": return Icons.TypeSprite(EItemType.Container);
				case "Special slots": return Icons.TypeSprite(EItemType.Special);
				default: return null;
			}
		}

		// A pocket: a rounded outline, the flap across the top and a button, white on transparent like the other pictures
		private static Sprite DrawPockets()
		{
			const int size = 48;
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			var centre = new Vector2(size / 2f, size / 2f - 2f);
			var half = new Vector2(13f, 14f);
			float radius = 8f;
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					var p = new Vector2(x + 0.5f, y + 0.5f) - centre;
					// Distance to the edge of a rounded rectangle: the outline is where it is near zero
					var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (half - new Vector2(radius, radius));
					float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
					float alpha = Mathf.Clamp01(1.6f - Mathf.Abs(outside));
					// The flap, across the top
					float flap = Mathf.Abs(p.y - 8f) < 1.6f && Mathf.Abs(p.x) < half.x - 1f ? 1f : 0f;
					// The button
					float button = Mathf.Clamp01(2.2f - (p - new Vector2(0f, 1f)).magnitude);
					texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(alpha, Mathf.Max(flap, button))));
				}
			}

			texture.Apply();
			return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
		}
	}
}
