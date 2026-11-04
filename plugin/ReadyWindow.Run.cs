using System.Linq;
using UnityEngine;
using static GetReady.ReadyPlan;
using static GetReady.ReadyRun;

namespace GetReady
{
	// The window's side of running the plan: the Get ready button and the red box a run can leave behind.
	internal static partial class ReadyWindow
	{
		// The window's button: close the window and do it like the stash button does (alert and sound included)
		private static void Go()
		{
			_closeRequested = true;
			QuickRun();
		}

		// The button's plain click: do it (ReadyRun) and show what went wrong, if anything
		internal static async void QuickRun()
		{
			var problems = await ReadyRun.QuickRun();
			if (problems != null && problems.Count > 0)
			{
				Alerts.Clear();
				Alerts.AddRange(problems);
				_alertRect = new Rect((Screen.width / Scale - 640) / 2f, Screen.height / Scale * 0.18f, 640, 10);
			}
		}
	}
}
