using System;
using EFT.UI;
using EFT.UI.Builds;
using UnityEngine;

namespace GetReady
{
	// A spike: a real game window (a copy of the "name a preset" window of the Builds tab) with our caption, to see in the game whether the
	// chrome (caption bar, red cross, dragging, Esc, click to bring on top) works for a window of ours. Opened by an F12 hotkey, off by
	// default. The existing window is not touched.
	internal static class UiWindowSpike
	{
		private static EditBuildNameWindow _window;

		internal static void Toggle()
		{
			try
			{
				if (_window != null)
				{
					_window.CloseAsync();
					return;
				}

				var context = ItemUiContext.Instance;
				if (context == null || context._editBuildNameWindow == null || context._infoWindowsContainer == null)
				{
					Plugin.Log.LogWarning("Window spike: the game's window template is not available here");
					return;
				}

				var window = UnityEngine.Object.Instantiate(context._editBuildNameWindow, context._infoWindowsContainer, false);
				window.name = "GetReady Spike Window";
				var windowContext = ((Window<EditBuildNameWindowContext>)window).Show();
				window.Caption.text = "Get ready for raid";
				window._placeholderText.text = "Spike: a copy of a game window";
				window.WindowTransform.SetAsLastSibling();
				context.RegisterWindow<EditBuildNameWindowContext>(window, windowContext);
				windowContext.OnClose += () =>
				{
					if (window != null)
					{
						UnityEngine.Object.Destroy(window.gameObject);
					}

					_window = null;
				};
				_window = window;
				Plugin.Log.LogInfo("Window spike: opened");
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Window spike failed: {e}");
			}
		}
	}
}
