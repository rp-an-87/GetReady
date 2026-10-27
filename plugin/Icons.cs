using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;

namespace GetReady
{
	// Icons: the game's own (item icons for the window, a type icon for the stash button), and a checklist drawn in code as
	// the button's fallback. White on transparent, tinted by the button.
	internal static class Icons
	{
		private const int Size = 64;
		private static Sprite _ready;

		// One item of each template found in the stash or on you, to take the icon from, and the icons asked for so far
		private static readonly Dictionary<string, Item> Known = new Dictionary<string, Item>();
		private static readonly Dictionary<string, ItemIcon> Loaded = new Dictionary<string, ItemIcon>();

		// Remembers an item of every template in the inventory (the game builds icons from items, not templates)
		internal static void Index(Inventory inventory)
		{
			Known.Clear();
			foreach (var item in Game.Stash(inventory).Concat(Game.CarriedAndSecured(inventory)))
			{
				var template = item.TemplateId.ToString();
				if (!Known.ContainsKey(template))
				{
					Known[template] = item;
				}
			}
		}

		internal static Sprite Of(string template)
		{
			ItemIcon icon;
			if (!Loaded.TryGetValue(template, out icon))
			{
				Item item;
				if (!Known.TryGetValue(template, out item))
				{
					return null;
				}

				try
				{
					icon = ItemViewFactory.LoadItemIcon(item, 1, false);
				}
				catch (Exception e)
				{
					Plugin.Log.LogWarning($"No icon for {template}: {e.Message}");
					icon = null;
				}

				Loaded[template] = icon;
			}

			return icon?.Sprite;
		}

		// The item's icon fitted into the rect (an empty frame while it is loading or when you have none of it); the item's
		// name is the tooltip
		internal static void Draw(Rect rect, string template, string name)
		{
			GUI.Box(rect, GUIContent.none, Ui.Inner);
			var sprite = Of(template);
			if (sprite != null && sprite.texture != null)
			{
				var source = sprite.textureRect;
				float scale = Math.Min((rect.width - 4) / source.width, (rect.height - 4) / source.height);
				var size = new Vector2(source.width * scale, source.height * scale);
				var target = new Rect(rect.x + (rect.width - size.x) / 2f, rect.y + (rect.height - size.y) / 2f, size.x, size.y);
				var texture = sprite.texture;
				GUI.DrawTextureWithTexCoords(target, texture, new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
			}

			GUI.Label(rect, new GUIContent("", name));
		}

		// A square of the given size in the layout with the item's icon
		internal static void Slot(string template, string name, float size)
		{
			Draw(GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size)), template, name);
		}

		// The stash button's icon: the game's backpack icon, or the drawn checklist when the game has none
		internal static Sprite Button()
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

			return Ready();
		}

		internal static Sprite Ready()
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
