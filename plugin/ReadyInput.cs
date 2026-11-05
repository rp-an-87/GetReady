using UnityEngine;

namespace GetReady
{
	// The keys and the screen the windows depend on: Esc closes the alert and then the window, and leaving the stash closes both. The plan's own
	// upkeep (refresh after changes, recording the cases moved by hand) is ReadyPlan.Tick.
	internal static class ReadyInput
	{
		private static bool _escapePending;
		private static int _escapeFrame = -1;

		// Esc pressed: closes the alert if it is up, else the window. Returns whether there was something to close (the key is then taken, so the
		// game's screen does not also react to it).
		internal static bool ConsumeEscape()
		{
			_escapePending = false;
			_escapeFrame = Time.frameCount;
			if (ReadyAlert.IsOpen)
			{
				ReadyAlert.Close();
				return true;
			}

			if (ReadyUiWindow.IsOpen)
			{
				ReadyUiWindow.Close();
				return true;
			}

			return false;
		}

		internal static void Tick()
		{
			// Esc is taken by the game's stash screens through EscapePatches (ConsumeEscape), so only these windows close. Should a screen not
			// ask first, they still close a frame later.
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				// Not when a game screen already handed the key to us this frame
				_escapePending = _escapeFrame != Time.frameCount;
			}
			else if (_escapePending)
			{
				_escapePending = false;
				ConsumeEscape();
			}

			bool shown = ReadyButtonPatch.StashShown;
			if (!shown)
			{
				// Leaving the stash closes the window and any alert
				ReadyUiWindow.Close();
				ReadyAlert.Close();
			}

			ReadyPlan.Tick(shown);
		}
	}
}
