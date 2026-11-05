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

		private static AnimatedToggle _navTemplate;
		private static int _navSearches;

		// One of the tabs of the game's top bar (Character, Traders, Flea market...): dark with a picture and a caption, beige when it is on
		// or under the mouse
		private static AnimatedToggle NavTemplate()
		{
			if (_navTemplate != null || _navSearches >= 3)
			{
				return _navTemplate;
			}

			_navSearches++;
			try
			{
				var bar = Resources.FindObjectsOfTypeAll<MenuTaskBar>().FirstOrDefault(found => found.gameObject.scene.IsValid());
				var toggles = bar != null && bar._toggleButtons != null ? bar._toggleButtons.Values.Where(toggle => toggle != null).ToList() : new System.Collections.Generic.List<AnimatedToggle>();
				_navTemplate = toggles.FirstOrDefault();
				Plugin.Trace($"Game top bar: {(bar != null ? "found" : "not found")}, {toggles.Count} tabs, using {(_navTemplate != null ? _navTemplate.name : "none")}");
				if (_navTemplate != null)
				{
					DumpTree(_navTemplate.transform, 0);
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Game top bar: could not look for it: {e.Message}");
			}

			return _navTemplate;
		}

		private static void DumpTree(Transform node, int depth)
		{
			var rect = node as RectTransform;
			var parts = string.Join(", ", node.GetComponents<Component>().Where(part => part != null && !(part is Transform)).Select(part => part.GetType().Name).ToArray());
			Plugin.Trace($"Game tab tree: {new string(' ', depth * 2)}{node.name} active={node.gameObject.activeSelf} size={(rect != null ? rect.rect.size.ToString() : "-")} [{parts}]");
			if (depth < 4)
			{
				for (int i = 0; i < node.childCount; i++)
				{
					DumpTree(node.GetChild(i), depth + 1);
				}
			}
		}

		// A copy of a top bar tab with our caption and picture; it behaves as the game's own (hover, selected). Null when the bar is not there.
		private static AnimatedToggle NavTab(Transform parent, string text, Sprite icon, bool on, ToggleGroup group, Action select, float height, float textUnits)
		{
			var template = NavTemplate();
			if (template == null)
			{
				return null;
			}

			try
			{
				var copy = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
				copy.name = "Tab " + text;
				copy.SetActive(true);
				// The tooltip and behaviour the game gave this tab for its own screen are not wanted on ours
				foreach (var area in copy.GetComponentsInChildren<HoverTooltipArea>(true))
				{
					UnityEngine.Object.Destroy(area);
				}

				var toggle = copy.GetComponent<AnimatedToggle>();
				for (int i = toggle.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
				{
					toggle.onValueChanged.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
				}

				toggle.group = group;
				int size = Mathf.Max(8, Mathf.RoundToInt(UiKit.Unit * textUnits));
				foreach (var label in copy.GetComponentsInChildren<TMPro.TMP_Text>(true))
				{
					label.text = text.ToUpper();
					label.fontSize = size;
					label.enableWordWrapping = false;
					label.overflowMode = TMPro.TextOverflowModes.Ellipsis;
				}

				// The picture is the image that is not the tab's own background
				Image picture = null;
				foreach (var image in copy.GetComponentsInChildren<Image>(true))
				{
					if (image.gameObject != copy && image.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						picture = image;
						break;
					}
				}

				if (picture != null)
				{
					picture.gameObject.SetActive(icon != null);
					if (icon != null)
					{
						picture.sprite = icon;
						picture.preserveAspect = true;
					}
				}

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
				Plugin.Log.LogWarning($"Game top bar tab: could not make one: {e.Message}");
				return null;
			}
		}

		// A copy of the game's tab button in the row: the caption (the game puts it in capitals), an optional picture, lit when it is on; select is
		// called when it is clicked on. The group makes the tabs of a row switch each other off. Null when the game's tab is not there.
		internal static AnimatedToggle Tab(Transform parent, string text, Sprite icon, bool on, ToggleGroup group, Action select, float height, float textUnits)
		{
			// The top bar's tab first (a picture, the game's hover); the generic toggle of the game's screens when the bar is not there
			var nav = NavTab(parent, text, icon, on, group, select, height, textUnits);
			if (nav != null)
			{
				return nav;
			}

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
				// The text is sized like the rest of the window (the game's own tabs are made for a big screen of their own)
				tab.InitSpawnableButton(text, Mathf.Max(8, Mathf.RoundToInt(UiKit.Unit * textUnits)), icon, null);
				copy.AddComponent<TabHover>().Tab = tab;
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

		// The game's standard button, copied from a window of its own that this window is made from (its Save button): the same look and
		// hover and click feel as every button of the game. Set when the window opens.
		internal static DefaultUIButton ButtonTemplate;

		// A copy of the game's button with this text; null when there is no template (the caller then makes its own)
		internal static DefaultUIButton Button(Transform parent, string text, Action click, float width, float height, bool flex = false)
		{
			if (ButtonTemplate == null)
			{
				return null;
			}

			try
			{
				var copy = UnityEngine.Object.Instantiate(ButtonTemplate.gameObject, parent, false);
				copy.name = "Button " + text;
				copy.SetActive(true);
				var button = copy.GetComponent<DefaultUIButton>();
				button.OnClick.RemoveAllListeners();
				button.OnClick.AddListener(() => click());
				button.Interactable = true;
				button.SetHeaderText(text, Mathf.Max(8, Mathf.RoundToInt(UiKit.Unit * 1.1f)));
				if (flex)
				{
					// The row's room shared out equally between such buttons
					UiKit.Size(copy.transform, width: 1f, height: height, flexWidth: 1f).minWidth = 0f;
				}
				else
				{
					UiKit.Size(copy.transform, width: width, height: height);
				}

				return button;
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Game button: could not make one: {e.Message}");
				return null;
			}
		}

		// The page that is open: its button is kept in the game's highlighted state (the one it has under the mouse), whatever the mouse does
		internal static void Mark(DefaultUIButton button, bool on)
		{
			if (!on)
			{
				return;
			}

			var tween = button._button != null ? button._button : button.GetComponent<TweenAnimatedButton>();
			if (tween == null || tween._animation == null)
			{
				return;
			}

			tween._animation.SetState(EButtonAnimationState.Highlighted);
			// Nothing happens when it is pressed: no press animation, no click sound
			button._onPointerClickSound = false;
			button.gameObject.AddComponent<KeepLit>().Tween = tween;
		}

		// Added after the game's own button script, so it runs after it: when that sends the button back to normal (the mouse leaves, a
		// click ends), this puts it back to highlighted
		internal class KeepLit : MonoBehaviour, UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerDownHandler
		{
			internal TweenAnimatedButton Tween;

			public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => Lit();

			public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData) => Lit();

			// The game sends it to pressed: straight back, with no animation in between
			public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
			{
				if (Tween != null && Tween._animation != null)
				{
					Tween._animation.SetState(EButtonAnimationState.Highlighted);
				}
			}

			private void Lit()
			{
				if (Tween != null && Tween._animation != null)
				{
					Tween._animation.TransitionToState(EButtonAnimationState.Highlighted);
				}
			}
		}

		private static Sprite _gear;

		// The settings gear of the game's top bar (its Settings tab), for the Options section
		internal static Sprite SettingsGear()
		{
			if (_gear != null)
			{
				return _gear;
			}

			try
			{
				var bar = Resources.FindObjectsOfTypeAll<MenuTaskBar>().FirstOrDefault(found => found.gameObject.scene.IsValid());
				AnimatedToggle toggle;
				if (bar != null && bar._toggleButtons != null && bar._toggleButtons.TryGetValue(EMenuType.Settings, out toggle) && toggle != null)
				{
					foreach (var image in toggle.GetComponentsInChildren<Image>(true))
					{
						if (image.gameObject != toggle.gameObject && image.sprite != null && image.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							_gear = image.sprite;
							break;
						}
					}
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Settings gear: could not find it: {e.Message}");
			}

			return _gear;
		}

		// The game's tab shows no hover of its own here, so the pointer over it gives the bold the game's tab has on hover (and the hand cursor
		// is the tab's own)
		internal class TabHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
		{
			internal UISpawnableToggle Tab;

			public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) => Tab?.Highlighted();

			public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => Tab?.Default();
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
