using System;
using System.Linq;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GetReady
{
	// Widgets the game draws itself, copied rather than imitated: the tab buttons of the flea market and the character screen (a toggle with the
	// game's look, hover and selected animation, a caption and an optional picture). The game keeps one prefab of each in memory whichever
	// screen is open, so a copy can be made from anywhere. When none is found the callers keep their own buttons.
	internal static class GameWidgets
	{
		private static UISpawnableToggle _tabTemplate;
		private static int _searches;

		private static UISpawnableToggle TabTemplate()
		{
			if (_tabTemplate != null)
			{
				return _tabTemplate;
			}

			// A few tries (screens load and unload), then give up for good so a game without them is not searched at every redraw
			if (_searches >= 3)
			{
				return null;
			}

			_searches++;
			try
			{
				var found = Resources.FindObjectsOfTypeAll<UISpawnableToggle>()
					.Where(tab => tab != null && tab.Toggle != null && tab._headerLabel != null)
					.ToList();
				// A prefab asset (not in a scene) has no state of its own from a screen that used it
				_tabTemplate = found.FirstOrDefault(tab => !tab.gameObject.scene.IsValid()) ?? found.FirstOrDefault();
				Plugin.Trace($"Game tabs: {found.Count} found, using {(_tabTemplate != null ? _tabTemplate.name + " (" + (_tabTemplate.gameObject.scene.IsValid() ? "in a scene" : "prefab") + ")" : "none")}");
				if (found.Count > 0)
				{
					Plugin.Trace("Game tabs: " + string.Join(", ", found.Take(25).Select(tab => tab.name + (tab.gameObject.scene.IsValid() ? "" : "*")).ToArray()));
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Game tabs: could not look for them: {e.Message}");
			}

			return _tabTemplate;
		}

		// A copy of the game's tab button in the row: the caption (the game puts it in capitals), an optional picture, lit when it is on; select is
		// called when it is clicked on. The group makes the tabs of a row switch each other off. Null when the game's tab is not there.
		internal static AnimatedToggle Tab(Transform parent, string text, Sprite icon, bool on, ToggleGroup group, Action select, float height)
		{
			var template = TabTemplate();
			if (template == null)
			{
				return null;
			}

			try
			{
				var copy = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
				copy.name = "Tab " + text;
				copy.SetActive(true);
				var tab = copy.GetComponent<UISpawnableToggle>();
				tab.Init(group);
				int size = Mathf.Max(8, Mathf.RoundToInt(tab._headerLabel.fontSize * UiKit.Scale));
				tab.InitSpawnableButton(text, size, icon, null);
				var toggle = tab.Toggle;
				toggle.onValueChanged.AddListener(value =>
				{
					if (value)
					{
						select();
					}
				});
				toggle.ToggleSilent(on);
				UiKit.Size(copy.transform, width: 1f, height: height, flexWidth: 1f).minWidth = 0f;
				return toggle;
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Game tab: could not make one: {e.Message}");
				return null;
			}
		}

		// For finding the game's big green Ready button: what buttons the game has in memory, once in the log (with Log details on)
		private static bool _dumped;

		internal static void DumpButtons()
		{
			if (_dumped || Plugin.DebugLogging == null || !Plugin.DebugLogging.Value)
			{
				return;
			}

			_dumped = true;
			try
			{
				var buttons = Resources.FindObjectsOfTypeAll<DefaultUIButton>();
				Plugin.Trace($"Game buttons: {buttons.Length} found");
				foreach (var button in buttons.Take(80))
				{
					var parent = button.transform.parent;
					Plugin.Trace($"Game button: {button.name} in {(parent != null ? parent.name : "-")} text '{button.HeaderText}' {(button.gameObject.scene.IsValid() ? "scene" : "prefab")} active={button.gameObject.activeInHierarchy}");
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Game buttons: could not list them: {e.Message}");
			}
		}
	}
}
