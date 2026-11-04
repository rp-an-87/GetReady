using UnityEngine;

namespace GetReady
{
	// The small "?" next to a title: its tooltip holds the explanation that used to be a grey paragraph.
	internal static partial class ReadyWindow
	{
		private const float RowHeight = 40f;

		// Draws something in the middle of a row's height (a row is as high as its icon)
		private static void Centered(System.Action draw, float height = RowHeight, bool expand = false, float width = 0f)
		{
			if (width > 0f)
			{
				GUILayout.BeginVertical(GUILayout.Height(height), GUILayout.Width(width));
			}
			else
			{
				GUILayout.BeginVertical(GUILayout.Height(height), GUILayout.ExpandWidth(expand));
			}

			GUILayout.FlexibleSpace();
			draw();
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
		}

		// A button in the middle of a row's height
		// A button width that fits its text: the given width, or more when the text needs it (the game font is wide)
		private static float Fit(string text, GUIStyle style, float min) => Mathf.Max(min, style.CalcSize(new GUIContent(text)).x + 4f);

		private static bool CenteredButton(GUIContent content, GUIStyle style, float width, float height = RowHeight)
		{
			width = Fit(content.text, style, width);
			bool pressed = false;
			Centered(() => pressed = GUILayout.Button(content, style, GUILayout.Width(width)), height, false, width);
			return pressed;
		}

		// A line of text that does not wrap, in the middle of a row's height
		private static void CenteredLabel(string text, GUIStyle style, bool expand = false, float height = RowHeight)
		{
			var plain = new GUIStyle(style) { wordWrap = false };
			Centered(() => GUILayout.Label(text, plain, GUILayout.ExpandWidth(expand)), height, expand);
		}

		private static void Help(string text) => GUILayout.Label(new GUIContent("?", text), Ui.Help);

		// The "?" and a title, on the same middle line
		private static void HelpTitle(string help, string title, GUIStyle style, float height = 28f)
		{
			Centered(() => Help(help), height);
			CenteredLabel(title, style, false, height);
		}

		// A line of text with the "?" before it
		private static void TitleWithHelp(string title, string help)
		{
			GUILayout.BeginHorizontal();
			HelpTitle(help, title, Ui.Body);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}
	}
}
