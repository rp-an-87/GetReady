using EFT.UI;
using UnityEngine;

namespace GetReady
{
	// An item tag drawn as the game draws it on the item, for the map warning: a badge across the bottom of the container's icon, in the tag's colour
	internal static class TagChip
	{
		private static GUIStyle _style;

		internal static void DrawOn(Rect icon, string name, int colorIndex)
		{
			if (string.IsNullOrEmpty(name))
			{
				return;
			}

			if (_style == null)
			{
				_style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip };
			}

			_style.font = OverlayFont.Font;
			var color = EditTagWindow.GetColor(colorIndex);
			bool dark = (0.299f * color.r + 0.587f * color.g + 0.114f * color.b) > 0.55f;
			_style.normal.textColor = dark ? new Color(0.08f, 0.08f, 0.08f) : Color.white;
			_style.fontSize = Mathf.Max(9, Mathf.RoundToInt(icon.height * 0.3f));
			var rect = new Rect(icon.x, icon.yMax - icon.height * 0.36f, icon.width, icon.height * 0.36f);
			var previous = GUI.color;
			GUI.color = color;
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			GUI.color = previous;
			GUI.Label(rect, name, _style);
		}
	}
}
