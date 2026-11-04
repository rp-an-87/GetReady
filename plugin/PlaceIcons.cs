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

		// The pockets, as a pair of slim trousers drawn flat, like the game's pictures: one grey, the waistband, the fly and the pocket slits in
		// a darker grey, and under it the soft dark glow the game's pictures have (the shape blurred, black, a little lower)
		private static Sprite DrawPockets()
		{
			const int size = 48;
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			// Waist, outer edges, the hems and the crotch
			var outline = new[]
			{
				new Vector2(9f, 40f), new Vector2(39f, 40f), new Vector2(38f, 8f), new Vector2(27f, 8f),
				new Vector2(24f, 24f), new Vector2(21f, 8f), new Vector2(10f, 8f)
			};
			var covered = new float[size, size];
			var detail = new float[size, size];
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					// Covered part of the pixel, and how much of that is detail, by looking at nine points in it
					int inside = 0, marks = 0;
					for (int sx = 0; sx < 3; sx++)
					{
						for (int sy = 0; sy < 3; sy++)
						{
							// Looked up at a larger drawing, so the whole glyph is smaller in the square
							var point = (new Vector2(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f) - new Vector2(24f, 24f)) / 0.82f + new Vector2(24f, 24f);
							if (!Inside(outline, point))
							{
								continue;
							}

							inside++;
							bool belt = Mathf.Abs(point.y - 36.5f) < 1.3f;
							bool fly = Mathf.Abs(point.x - 24f) < 0.8f && point.y < 36.5f && point.y > 26f;
							bool slitLeft = Near(point, new Vector2(12f, 35f), new Vector2(17f, 28f), 1.1f);
							bool slitRight = Near(point, new Vector2(36f, 35f), new Vector2(31f, 28f), 1.1f);
							if (belt || fly || slitLeft || slitRight)
							{
								marks++;
							}
						}
					}

					covered[x, y] = inside / 9f;
					detail[x, y] = inside == 0 ? 0f : marks / (float)inside;
				}
			}

			var glow = Blur(Blur(Blur(covered, size, 3), size, 3), size, 3);
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					// The glow, a little lower than the shape, under the trousers
					int lower = Mathf.Min(size - 1, y + 2);
					float under = Mathf.Clamp01(glow[x, lower] * 1.5f) * 0.75f;
					float top = covered[x, y];
					float alpha = top + under * (1f - top);
					if (alpha <= 0.001f)
					{
						texture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
						continue;
					}

					float value = Mathf.Lerp(0.86f, 0.25f, detail[x, y]) * top / alpha;
					texture.SetPixel(x, y, new Color(value, value, value, alpha));
				}
			}

			texture.Apply();
			return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
		}

		// A square blur (the average of the pixels within a radius)
		private static float[,] Blur(float[,] source, int size, int radius)
		{
			var result = new float[size, size];
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					float sum = 0f;
					int count = 0;
					for (int dx = -radius; dx <= radius; dx++)
					{
						for (int dy = -radius; dy <= radius; dy++)
						{
							int nx = x + dx, ny = y + dy;
							if (nx >= 0 && ny >= 0 && nx < size && ny < size)
							{
								sum += source[nx, ny];
							}

							count++;
						}
					}

					result[x, y] = sum / count;
				}
			}

			return result;
		}

		// Whether a point is within a distance of the line from a to b
		private static bool Near(Vector2 point, Vector2 a, Vector2 b, float distance)
		{
			var along = b - a;
			float t = Mathf.Clamp01(Vector2.Dot(point - a, along) / along.sqrMagnitude);
			return (point - (a + along * t)).magnitude < distance;
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
