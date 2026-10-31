using System;
using System.Collections.Generic;
using UnityEngine;

namespace GetReady
{
	// A dark panel with two sections that are always there: what is on you (equipped), and what is saved or configured but not on you.
	// An empty section says what will appear in it.
	internal static partial class ReadyWindow
	{
		private const float MostAwayHeight = 240f;
		private static readonly Dictionary<string, Vector2> AwayScroll = new Dictionary<string, Vector2>();
		private static readonly Dictionary<string, float> AwayHeight = new Dictionary<string, float>();

		internal static void TwoSections(string key, string onLabel, string onEmpty, bool anyOn, Action drawOn, string awayLabel, string awayEmpty, bool anyAway, Action drawAway)
		{
			GUILayout.BeginVertical(Ui.Inner);
			GUILayout.Label(onLabel, Ui.Muted);
			if (anyOn)
			{
				drawOn();
			}
			else
			{
				GUILayout.Label(onEmpty, Ui.PlainMuted);
			}

			GUILayout.Space(6);
			GUILayout.Box(GUIContent.none, Ui.Divider);
			GUILayout.Label(awayLabel, Ui.Muted);
			if (anyAway)
			{
				// Plain while it fits; a scroll view of at most MostAwayHeight only when it does not (a scroll view takes the mouse wheel even with no
				// bar, so it is not used for a short list). The height is the one measured on the frame before.
				float height;
				AwayHeight.TryGetValue(key, out height);
				bool scrolls = height > MostAwayHeight;
				if (scrolls)
				{
					var scroll = AwayScroll.TryGetValue(key, out var kept) ? kept : Vector2.zero;
					AwayScroll[key] = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(MostAwayHeight));
				}

				GUILayout.BeginVertical();
				drawAway();
				GUILayout.EndVertical();
				if (Event.current.type == EventType.Repaint)
				{
					AwayHeight[key] = GUILayoutUtility.GetLastRect().height;
				}

				if (scrolls)
				{
					GUILayout.EndScrollView();
				}
			}
			else
			{
				GUILayout.Label(awayEmpty, Ui.PlainMuted);
			}

			GUILayout.EndVertical();
		}
	}
}
