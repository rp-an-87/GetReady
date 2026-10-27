using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GetReady
{
	// The big red box a run leaves behind when something is wrong.
	internal static partial class ReadyWindow
	{
		private const int AlertId = 0x6e7265;

		// What a quick run found wrong, shown in the middle of the screen until it is dismissed
		private static readonly List<KeyValuePair<string, PlanLine>> Alerts = new List<KeyValuePair<string, PlanLine>>();

		private static Rect _alertRect;

		private static void AlertContents(int id)
		{
			GUILayout.Label("Check this before you leave", Ui.AlertTitle);
			GUILayout.Space(8);
			// Grouped by step, each problem with its item's icon, what it is about and what is wrong
			foreach (var step in Alerts.GroupBy(pair => pair.Key))
			{
				GUILayout.Label(step.Key, Ui.Name);
				foreach (var pair in step)
				{
					GUILayout.BeginHorizontal();
					if (!string.IsNullOrEmpty(pair.Value.Template))
					{
						Icons.Slot(pair.Value.Template, pair.Value.Subject ?? "", 46);
					}

					GUILayout.BeginVertical();
					GUILayout.Label(pair.Value.Subject ?? pair.Value.Text, Ui.AlertText);
					if (pair.Value.Detail != null)
					{
						GUILayout.Label(pair.Value.Detail, Ui.AlertDetail);
					}

					GUILayout.EndVertical();
					GUILayout.EndHorizontal();
					GUILayout.Space(6);
				}

				GUILayout.Space(4);
			}

			GUILayout.Space(12);
			if (GUILayout.Button("Got it", Ui.Primary))
			{
				Alerts.Clear();
			}

			GUI.DragWindow(new Rect(0, 0, 10000, 60));
		}
	}
}
