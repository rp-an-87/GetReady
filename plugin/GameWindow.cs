using System;
using EFT.UI;
using EFT.UI.Builds;
using UnityEngine;
using UnityEngine.UI;

namespace GetReady
{
	// A window of the game's own kind, made from a copy of one of its windows (the one for naming a build): its caption, red cross and border
	// stay, everything else is hidden and the window is ours to fill. The Get ready window and the alert are both made this way.
	internal static class GameWindow
	{
		// Opens it with this title. onClose runs when it is closed (after the window is gone). Returns null when the game has no window to copy
		// here. The button is the copy's own Save button, the game's standard button, for the callers to copy before it is hidden.
		internal static EditBuildNameWindow Open(string name, string title, Action onClose, out float captionHeight, out DefaultUIButton button)
		{
			captionHeight = 0f;
			button = null;
			var context = ItemUiContext.Instance;
			if (context == null || context._editBuildNameWindow == null || context._infoWindowsContainer == null)
			{
				Plugin.Log.LogWarning($"{title}: the game's window template is not available here");
				return null;
			}

			var window = UnityEngine.Object.Instantiate(context._editBuildNameWindow, context._infoWindowsContainer, false);
			window.name = name;
			var windowContext = ((Window<EditBuildNameWindowContext>)window).Show();
			UiKit.Init(window.Caption);
			UiKit.Scale = UiKit.ChosenScale;
			window.Caption.text = title;
			button = window._saveButtonSpawner;
			captionHeight = Hollow(window);
			window.WindowTransform.SetAsLastSibling();
			context.RegisterWindow<EditBuildNameWindowContext>(window, windowContext);
			windowContext.OnClose += () =>
			{
				if (window != null)
				{
					UnityEngine.Object.Destroy(window.gameObject);
				}

				onClose?.Invoke();
			};
			return window;
		}

		// Everything of the copy but its caption and border hidden, its own layout off, a canvas of its own. Returns the caption's height.
		private static float Hollow(EditBuildNameWindow window)
		{
			var frame = window.WindowTransform;
			window._inputField.gameObject.SetActive(false);
			window._saveButtonSpawner.gameObject.SetActive(false);
			window._placeholderText.gameObject.SetActive(false);
			var caption = window._captionPanel != null ? window._captionPanel.transform : null;
			for (int i = 0; i < frame.childCount; i++)
			{
				var child = frame.GetChild(i);
				// The caption stays, and so does the grey border every game window has
				if (child.name == "Border" || (caption != null && (child == caption || caption.IsChildOf(child))))
				{
					continue;
				}

				child.gameObject.SetActive(false);
			}

			var fitter = frame.GetComponent<ContentSizeFitter>();
			if (fitter != null)
			{
				fitter.enabled = false;
			}

			foreach (var group in frame.GetComponents<LayoutGroup>())
			{
				group.enabled = false;
			}

			UiKit.OwnCanvas(frame.gameObject);
			return window._captionPanel != null ? ((RectTransform)window._captionPanel.transform).rect.height : 0f;
		}
	}
}
