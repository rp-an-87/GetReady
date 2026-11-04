using System.Collections.Generic;
using EFT;
using EFT.UI;
using UnityEngine;

namespace GetReady
{
	// The small picture of a place (where something is brought to): the pictures the traders and the handbook show for the same kind of gear
	// (rigs, backpacks, secured containers, special equipment), which the game fetches from the server as images and which are fetched here the
	// same way, once. The game has none for the pockets, so that one is drawn here. Null until a picture has arrived (the chip shows the name
	// meanwhile).
	internal static class PlaceIcons
	{
		private static Sprite _pockets;
		private static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();
		private static readonly HashSet<string> Loading = new HashSet<string>(), Failed = new HashSet<string>();

		private static string PathOf(string place)
		{
			switch (place)
			{
				case "Vest": return "/files/handbook/icon_gear_rigs.png";
				case "Backpack": return "/files/handbook/icon_gear_backpacks.png";
				case "Pouch": return "/files/handbook/icon_gear_secured.png";
				case "Special slots": return "/files/handbook/icon_spec.png";
				default: return null;
			}
		}

		// Whether a picture is on its way (so the page should be drawn again when it has come)
		internal static bool Pending(string place) => PathOf(place) != null && !Loaded.ContainsKey(place) && !Failed.Contains(place);

		internal static Sprite Of(string place)
		{
			if (place == "Pockets")
			{
				return _pockets ?? (_pockets = DrawPockets());
			}

			Sprite sprite;
			if (Loaded.TryGetValue(place, out sprite))
			{
				return sprite;
			}

			string path = PathOf(place);
			if (path != null && !Loading.Contains(place) && !Failed.Contains(place))
			{
				Loading.Add(place);
				Fetch(place, path);
			}

			return null;
		}

		private static async void Fetch(string place, string path)
		{
			try
			{
				var context = ItemUiContext.Instance;
				var loader = (context?.Session as IImageLoader) ?? (context?.ClientSession as IImageLoader);
				if (loader == null)
				{
					Failed.Add(place);
					return;
				}

				var texture = await loader.LoadTextureMain(path);
				if (texture != null)
				{
					Loaded[place] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
				}
				else
				{
					Failed.Add(place);
				}
			}
			catch (System.Exception e)
			{
				Failed.Add(place);
				Plugin.Log.LogWarning($"No picture for {place}: {e.Message}");
			}
			finally
			{
				Loading.Remove(place);
			}
		}

		// The pockets, as a pair of trousers: a filled silhouette with the waistband line and the fly cut out, white on transparent like the
		// other pictures
		private static Sprite DrawPockets()
		{
			const int size = 48;
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			// Waist, outer edges, the hems and the crotch
			var outline = new[]
			{
				new Vector2(12f, 42f), new Vector2(36f, 42f), new Vector2(35f, 6f), new Vector2(26f, 6f),
				new Vector2(24f, 22f), new Vector2(22f, 6f), new Vector2(13f, 6f)
			};
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					// Covered part of the pixel, by looking at nine points in it
					int covered = 0;
					for (int sx = 0; sx < 3; sx++)
					{
						for (int sy = 0; sy < 3; sy++)
						{
							var point = new Vector2(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f);
							bool belt = Mathf.Abs(point.y - 37f) < 0.8f && point.x > 12.5f && point.x < 35.5f;
							bool fly = Mathf.Abs(point.x - 24f) < 0.5f && point.y < 37f && point.y > 22f;
							if (!belt && !fly && Inside(outline, point))
							{
								covered++;
							}
						}
					}

					texture.SetPixel(x, y, new Color(1f, 1f, 1f, covered / 9f));
				}
			}

			texture.Apply();
			return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
		}

		// Whether a point is inside a polygon (counting the crossings of a line to the right)
		private static bool Inside(Vector2[] polygon, Vector2 point)
		{
			bool inside = false;
			for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
			{
				if ((polygon[i].y > point.y) != (polygon[j].y > point.y)
					&& point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
				{
					inside = !inside;
				}
			}

			return inside;
		}
	}
}
