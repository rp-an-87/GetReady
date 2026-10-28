using System;
using System.Collections.Generic;
using UnityEngine;

namespace GetReady
{
	internal static partial class ReadyWindow
	{
		// The standard priority list of the Setup page: a box with a row per entry, its place in the order, and the ▲ ▼ buttons that
		// move it (the same look as the order of the items of a supply group). lead draws something before the buttons (an icon).
		private static bool PriorityList<T>(string title, List<T> items, Func<T, string> label, Action<T> lead, Action<T, int> move)
		{
			bool changed = false;
			GUILayout.BeginVertical(Ui.Inner);
			GUILayout.Label(title, Ui.Muted);
			for (int i = 0; i < items.Count; i++)
			{
				var item = items[i];
				GUILayout.BeginHorizontal();
				lead?.Invoke(item);
				GUI.enabled = i > 0;
				if (GUILayout.Button("▲", Ui.Small))
				{
					move(item, -1);
					changed = true;
				}

				GUI.enabled = i < items.Count - 1;
				if (GUILayout.Button("▼", Ui.Small))
				{
					move(item, 1);
					changed = true;
				}

				GUI.enabled = true;
				GUILayout.Label($"{i + 1}. {label(item)}", Ui.Body);
				GUILayout.EndHorizontal();
			}

			GUILayout.EndVertical();
			return changed;
		}
	}
}
