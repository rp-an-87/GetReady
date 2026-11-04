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

		// The pockets, as a pair of slim trousers: a soft grey silhouette, lighter at the waist and darker toward the hems, with the waistband,
		// the fly and the two pocket slits in a darker grey, so it is shaded like the game's pictures (which the chip then tints the same)
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
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					// Covered part of the pixel, and how much of that is detail, by looking at nine points in it
					int covered = 0, detail = 0;
						float shade = 0f;
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

							covered++;
							// The light comes from the top left, as with the game's pictures
							float legMiddle = point.x < 24f ? 15.5f : 32.5f;
														// Lit from the top left: the left of each leg and the upper part lighter, the right and the lower part a little darker
								shade += 1f - 0.2f * Mathf.Clamp((point.x - legMiddle) / 6.5f, -1f, 1f) - 0.12f * Mathf.Clamp((24f - point.y) / 16f, -1f, 1f);
							bool belt = Mathf.Abs(point.y - 36.5f) < 1.3f;
							bool fly = Mathf.Abs(point.x - 24f) < 0.8f && point.y < 36.5f && point.y > 26f;
							bool slitLeft = Near(point, new Vector2(12f, 35f), new Vector2(17f, 28f), 1.1f);
							bool slitRight = Near(point, new Vector2(36f, 35f), new Vector2(31f, 28f), 1.1f);
							if (belt || fly || slitLeft || slitRight)
							{
								detail++;
							}
						}
					}

					if (covered == 0)
					{
						texture.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
						continue;
					}

					// Lighter at the top, darker down the legs; the details darker still
					float body = Mathf.Lerp(0.7f, 0.95f, Mathf.Clamp01((y - 8f) / 32f));
					float grey = Mathf.Lerp(body * shade / covered, 0.2f, detail / (float)covered);
					texture.SetPixel(x, y, new Color(grey, grey, grey, covered / 9f));
				}
			}

			texture.Apply();
			return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
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
