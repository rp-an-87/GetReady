using System;
using System.Collections.Generic;
using System.Threading;
using EFT.Trading;
using UnityEngine;
using UnityEngine.UI;

namespace GetReady
{
	// The trader portraits for the window. The game loads them into a UI Image, so each one is loaded once into a hidden Image and the
	// sprite it ends up with is kept and drawn in the window (an empty frame until it has loaded).
	internal static class TraderAvatars
	{
		private static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();
		private static readonly HashSet<string> Loading = new HashSet<string>();

		private static Sprite Of(Trader trader)
		{
			Sprite sprite;
			if (Loaded.TryGetValue(trader.Id, out sprite))
			{
				return sprite;
			}

			if (Loading.Add(trader.Id))
			{
				Load(trader);
			}

			return null;
		}

		private static async void Load(Trader trader)
		{
			try
			{
				var host = new GameObject("GetReadyAvatar", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.HideAndDontSave };
				var image = host.GetComponent<Image>();
				await trader.GetAndAssignAvatar(image, CancellationToken.None);
				Loaded[trader.Id] = image.sprite;
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"No portrait for {trader.LocalizedName}: {e.Message}");
				Loaded[trader.Id] = null;
			}
		}

		// A square of the given size in the layout with the trader's portrait; the name is the tooltip
		internal static void Slot(Trader trader, float size)
		{
			var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
			GUI.Box(rect, GUIContent.none, Ui.Inner);
			var sprite = Of(trader);
			if (sprite != null && sprite.texture != null)
			{
				var source = sprite.textureRect;
				float scale = Math.Min((rect.width - 4) / source.width, (rect.height - 4) / source.height);
				var shown = new Vector2(source.width * scale, source.height * scale);
				var target = new Rect(rect.x + (rect.width - shown.x) / 2f, rect.y + (rect.height - shown.y) / 2f, shown.x, shown.y);
				var texture = sprite.texture;
				GUI.DrawTextureWithTexCoords(target, texture, new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
			}

			GUI.Label(rect, new GUIContent("", trader.LocalizedName));
		}
	}
}
