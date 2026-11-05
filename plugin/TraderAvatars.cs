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

		// The portrait for the new window; null until the game has made it
		internal static Sprite Picture(Trader trader) => Of(trader);

		internal static bool Pending(Trader trader) => !Loaded.ContainsKey(trader.Id);

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

	}
}
