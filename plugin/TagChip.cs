using EFT.UI;
using UnityEngine;

namespace GetReady
{
	// An item tag drawn as the game draws it on the item: the tag's colour as a badge with its name on it
	internal static class TagChip
	{
		private static GUIStyle _dark, _light;

		private static void Init()
		{
			if (_dark == null)
			{
				_dark = new GUIStyle(Ui.Centered) { fontStyle = FontStyle.Bold, fontSize = 12, wordWrap = false };
				_dark.normal.textColor = new Color(0.08f, 0.08f, 0.08f);
				_light = new GUIStyle(_dark);
				_light.normal.textColor = Color.white;
			}
		}

		private static GUIStyle _small;

		// A small badge across the bottom of an icon (the tag of the container), in the tag's colour
		internal static void DrawOn(Rect icon, string name, int colorIndex)
		{
			if (string.IsNullOrEmpty(name))
			{
				return;
			}

			Init();
			var color = EditTagWindow.GetColor(colorIndex);
			if (_small == null)
			{
				_small = new GUIStyle(_dark) { clipping = TextClipping.Clip };
			}

			bool dark = (0.299f * color.r + 0.587f * color.g + 0.114f * color.b) > 0.55f;
			_small.normal.textColor = dark ? new Color(0.08f, 0.08f, 0.08f) : Color.white;
			_small.fontSize = Mathf.Max(9, Mathf.RoundToInt(icon.height * 0.3f));
			var rect = new Rect(icon.x, icon.yMax - icon.height * 0.36f, icon.width, icon.height * 0.36f);
			var previous = GUI.color;
			GUI.color = color;
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			GUI.color = previous;
			GUI.Label(rect, name, _small);
		}

		// How wide the badge of this name is
		internal static float WidthOf(string name)
		{
			Init();
			return string.IsNullOrEmpty(name) ? 0f : _dark.CalcSize(new GUIContent(name)).x + 14f;
		}

		internal static void Draw(string name, int colorIndex)
		{
			if (string.IsNullOrEmpty(name))
			{
				return;
			}

			var color = EditTagWindow.GetColor(colorIndex);
			Init();

			var content = new GUIContent(name);
			var style = (0.299f * color.r + 0.587f * color.g + 0.114f * color.b) > 0.55f ? _dark : _light;
			var width = style.CalcSize(content).x + 14f;
			var rect = GUILayoutUtility.GetRect(width, 20f, GUILayout.Width(width), GUILayout.Height(20f));
			var previous = GUI.color;
			GUI.color = color;
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			GUI.color = previous;
			GUI.Label(rect, content, style);
		}
	}
}
