using EFT.UI;
using UnityEngine;

namespace GetReady
{
	// An item tag drawn as the game draws it on the item: the tag's colour as a badge with its name on it
	internal static class TagChip
	{
		private static GUIStyle _dark, _light;

		internal static void Draw(string name, int colorIndex)
		{
			if (string.IsNullOrEmpty(name))
			{
				return;
			}

			var color = EditTagWindow.GetColor(colorIndex);
			if (_dark == null)
			{
				_dark = new GUIStyle(Ui.Centered) { fontStyle = FontStyle.Bold, fontSize = 12, wordWrap = false };
				_dark.normal.textColor = new Color(0.08f, 0.08f, 0.08f);
				_light = new GUIStyle(_dark);
				_light.normal.textColor = Color.white;
			}

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
