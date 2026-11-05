namespace GetReady
{
	// What the Get ready buttons do: run the plan and show what went wrong, if anything
	internal static class ReadyActions
	{
		// The window's button: close the window and do it like the stash button does (alert and sound included)
		internal static void Go()
		{
			ReadyUiWindow.Close();
			QuickRun();
		}

		// The button's plain click: do it (ReadyRun) and show the alert when it leaves problems
		internal static async void QuickRun()
		{
			// A new run starts from a clean screen: the last alert is closed (it comes back only if this run leaves problems too)
			ReadyAlert.Close();
			var problems = await ReadyRun.QuickRun();
			if (problems != null && problems.Count > 0)
			{
				ReadyAlert.Show(problems);
			}
		}
	}
}
