using UnityEngine;

namespace GetReady
{
	// The "READY" flash after a run that brought everything: a light green vignette around the screen and a green READY in the
	// middle, in the game's font. Fades in, holds, fades out. Drawn from OnGUI on top of everything; F12 turns it off.
	internal static class ReadyFlash
	{
		private const float FadeIn = 0.2f;
		private const float HoldUntil = 1f;
		private const float FadeOut = 0.5f;

		private static readonly Color Green = new Color(0.5f, 1f, 0.55f);

		private static float _start = -1f;
		private static Texture2D _vignette, _glow, _barGlow;
		private static GUIStyle _text;
		private static bool _logged;

		internal static void Show()
		{
			Plugin.Log.LogInfo($"READY flash requested (enabled: {Plugin.ShowFlash?.Value})");
			if (Plugin.ShowFlash != null && Plugin.ShowFlash.Value)
			{
				_start = Time.unscaledTime;
			}
		}

		// A radial gradient: alpha follows the distance from the centre, 0 in the middle and 1 at the corners (or the other way round)
		private static Texture2D Radial(System.Func<float, float> alphaAtDistance)
		{
			const int size = 128;
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					float dx = (x + 0.5f) / size * 2f - 1f;
					float dy = (y + 0.5f) / size * 2f - 1f;
					float distance = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.2f);
					texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alphaAtDistance(distance))));
				}
			}

			texture.Apply();
			return texture;
		}

		private static float Smooth(float edge0, float edge1, float value)
		{
			float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
			return t * t * (3f - 2f * t);
		}

		internal static void Draw()
		{
			if (_start < 0f || Event.current.type != EventType.Repaint)
			{
				return;
			}

			if (!_logged)
			{
				_logged = true;
				Plugin.Log.LogInfo($"READY flash drawing at {Screen.width}x{Screen.height}");
			}

			float age = Time.unscaledTime - _start;
			float strength = age < FadeIn ? age / FadeIn : age < HoldUntil ? 1f : 1f - (age - HoldUntil) / FadeOut;
			if (age >= HoldUntil + FadeOut)
			{
				_start = -1f;
				_logged = false;
				return;
			}

			if (_vignette == null)
			{
				_vignette = Radial(distance => Smooth(0.35f, 1f, distance) * 0.3f);
				_glow = Radial(distance => (1f - Smooth(0f, 0.8f, distance)) * 0.11f);
				_barGlow = Radial(distance => (1f - Smooth(0f, 0.85f, distance)) * 0.7f);
				_text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false };
				if (Ui.Font != null)
				{
					_text.font = Ui.Font;
				}
			}

			var screen = new Rect(0, 0, Screen.width, Screen.height);
			var previous = GUI.color;
			GUI.color = new Color(Green.r, Green.g, Green.b, strength);
			GUI.DrawTexture(screen, _vignette, ScaleMode.StretchToFill, true);
			GUI.DrawTexture(screen, _glow, ScaleMode.StretchToFill, true);

			// Like the GET READY bar of the deploy screen: a small filled bar with a soft glow around it and the word in dark letters
			float height = Screen.height * 0.034f;
			var bar = new Rect(Screen.width * 0.5f - Screen.height * 0.075f, Screen.height * 0.5f - height / 2f, Screen.height * 0.15f, height);
			var halo = new Rect(bar.x - bar.width * 0.6f, bar.y - bar.height * 1.8f, bar.width * 2.2f, bar.height * 4.6f);
			GUI.color = new Color(Green.r, Green.g, Green.b, strength);
			GUI.DrawTexture(halo, _barGlow, ScaleMode.StretchToFill, true);
			GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
			_text.fontSize = Mathf.RoundToInt(height * 0.62f);
			GUI.color = new Color(0.02f, 0.2f, 0.05f, strength);
			GUI.Label(bar, "READY", _text);
			GUI.color = previous;
		}
	}
}
