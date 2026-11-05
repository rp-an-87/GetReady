using System.Linq;
using TMPro;
using UnityEngine;

namespace GetReady
{
	// The game's own font for the overlays that are drawn by the plugin itself (the map warning, the READY flash). The game's screens load their
	// text after the plugin, so it is looked for again now and then until it turns up.
	internal static class OverlayFont
	{
		internal static Font Font;
		private static bool _asked;
		private static float _triedAt = float.NegativeInfinity;

		internal static void Init()
		{
			if (_asked && (Font != null || Time.unscaledTime - _triedAt <= 3f))
			{
				return;
			}

			_asked = true;
			_triedAt = Time.unscaledTime;
			Font = Find();
			Plugin.Trace(Font != null ? $"Using the game font {Font.name}" : "The game font was not found, using the default one");
		}

		// The game's TextMeshPro font assets keep the plain font they were made from
		private static Font Find()
		{
			try
			{
				var asset = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(candidate => candidate.name.ToLowerInvariant().Contains("bender") && candidate.sourceFontFile != null);
				if (asset != null)
				{
					return asset.sourceFontFile;
				}

				var plain = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(candidate => candidate.name.ToLowerInvariant().Contains("bender"));
				if (plain != null)
				{
					return plain;
				}

				// The font a live label of the game is drawn with
				foreach (var label in Resources.FindObjectsOfTypeAll<TMP_Text>())
				{
					if (label.font != null && label.font.sourceFontFile != null)
					{
						return label.font.sourceFontFile;
					}
				}

				return null;
			}
			catch
			{
				return null;
			}
		}
	}
}
