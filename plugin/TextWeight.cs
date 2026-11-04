using System.Collections.Generic;
using System.Reflection;
using SPT.Reflection.Patching;
using UnityEngine;

namespace GetReady
{
	// The game font is thin at the size of the plain text of the window (body and hint labels, and the regular-weight buttons such as the
	// map and place picks), so that text is drawn once more half a pixel to the right, which thickens it a little. Bold text and bigger
	// text are readable as they are and are left alone. Only while the window is drawn (Active), so other mods' GUI is left alone.
	internal static class TextWeight
	{
		internal static bool Active;

		private const int MaxSize = 17;

		private static bool _drawing;
		private static readonly Dictionary<GUIStyle, GUIStyle> Copies = new Dictionary<GUIStyle, GUIStyle>();

		// The same style without its pictures, so only the letters are drawn in the extra pass
		private static GUIStyle Copy(GUIStyle style)
		{
			GUIStyle copy;
			if (!Copies.TryGetValue(style, out copy))
			{
				copy = new GUIStyle(style);
				foreach (var state in new[] { copy.normal, copy.hover, copy.active, copy.focused, copy.onNormal, copy.onHover, copy.onActive, copy.onFocused })
				{
					state.background = null;
				}

				Copies[style] = copy;
			}

			// The font can turn up after the style is made
			copy.font = style.font;
			copy.fontSize = style.fontSize;
			return copy;
		}

		// Plain small text: not bold, not big
		private static bool IsThin(GUIStyle style) => style.fontStyle == FontStyle.Normal && style.fontSize > 0 && style.fontSize <= MaxSize;

		internal static void Before(GUIStyle style, Rect position, GUIContent content)
		{
			if (!Active || _drawing || style == null || Event.current == null || Event.current.type != EventType.Repaint || content == null || string.IsNullOrEmpty(content.text) || !IsThin(style))
			{
				return;
			}

			_drawing = true;
			try
			{
				GUI.Label(new Rect(position.x + 0.5f, position.y, position.width, position.height), new GUIContent(content.text), Copy(style));
			}
			finally
			{
				_drawing = false;
			}
		}
	}

	internal class TextWeightButtonPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(GUI).GetMethod("Button", new[] { typeof(Rect), typeof(GUIContent), typeof(GUIStyle) });

		// After the button, not before: its picture would cover the extra text
		[PatchPostfix]
		private static void Postfix(Rect position, GUIContent content, GUIStyle style)
		{
			TextWeight.Before(style, position, content);
		}
	}

	// GUI.Label (which GUILayout.Label ends in) draws through native code, so it is hooked here; GUIStyle.Draw is not on its way
	internal class TextWeightPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(GUI).GetMethod("Label", new[] { typeof(Rect), typeof(GUIContent), typeof(GUIStyle) });

		[PatchPrefix]
		private static void Prefix(Rect position, GUIContent content, GUIStyle style)
		{
			TextWeight.Before(style, position, content);
		}
	}
}
