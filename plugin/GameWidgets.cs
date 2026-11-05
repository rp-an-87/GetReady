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
				// The tabs of the trading screen (Traders, Flea market) first: they are the ones that light up and animate as the owner wants. Then a
				// prefab asset (no state from a screen that used it), then any.
				_tabTemplate = found.FirstOrDefault(tab => tab.name.StartsWith("Rag Fair Toggle") || tab.name.StartsWith("Merchants"))
					?? found.FirstOrDefault(tab => !tab.gameObject.scene.IsValid()) ?? found.FirstOrDefault();
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

		// The game's own tab asks for room in its layout settings (a flexible height): the height wanted is fixed, whatever room there is
		private static void Pin(LayoutElement element, float height)
		{
			element.minWidth = 0f;
			element.minHeight = height;
			element.preferredHeight = height;
			element.flexibleHeight = 0f;
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
					label.alignment = TMPro.TextAlignmentOptions.Center;
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
				Pin(UiKit.Size(copy.transform, width: 1f, height: height, flexWidth: 1f), height);
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
		internal static AnimatedToggle Tab(Transform parent, string text, Sprite icon, bool on, ToggleGroup group, Action select, float height, float textUnits, bool generic = false)
		{
			// The top bar's tab first (a picture, the game's hover); the generic toggle of the game's screens when the bar is not there
			var nav = generic ? null : NavTab(parent, text, icon, on, group, select, height, textUnits);
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
				// What the game gave this tab for its own screen is not wanted on ours
				foreach (var area in copy.GetComponentsInChildren<HoverTooltipArea>(true))
				{
					UnityEngine.Object.Destroy(area);
				}

				var own = copy.GetComponent<AnimatedToggle>();
				for (int i = own.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
				{
					own.onValueChanged.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
				}

				var tab = copy.GetComponent<UISpawnableToggle>();
				tab.Init(group);
				// The text is sized like the rest of the window (the game's own tabs are made for a big screen of their own)
				tab.InitSpawnableButton(text, Mathf.Max(8, Mathf.RoundToInt(UiKit.Unit * textUnits)), icon, null);
				var toggle = tab.Toggle;
				// The hover of these tabs is the same plate as the lit one, animated in: the copy gets no hover from the game's own events, so the
				// pointer plays that animation itself
				copy.AddComponent<HoverLight>().Toggle = toggle;
				toggle.onValueChanged.AddListener(value =>
				{
					if (value)
					{
						select();
					}
				});
				toggle.ToggleSilent(on);
				Pin(UiKit.Size(copy.transform, width: 1f, height: height, flexWidth: 1f), height);
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
					Pin(UiKit.Size(copy.transform, width: 1f, height: height, flexWidth: 1f), height);
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

		private static bool _spritesListed;

		// For choosing icons: the game's sprites whose names have these words, once in the log (with Log details on). Nothing here picks an icon;
		// the icons the window shows are fixed in Icons.StepSprite.
		internal static void ListSprites(params string[] words)
		{
			if (_spritesListed || Plugin.DebugLogging == null || !Plugin.DebugLogging.Value)
			{
				return;
			}

			_spritesListed = true;
			try
			{
				var all = Resources.FindObjectsOfTypeAll<Sprite>();
				foreach (var word in words)
				{
					var names = all.Where(candidate => candidate != null && candidate.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0).Select(candidate => candidate.name).Distinct().OrderBy(name => name).Take(60).ToArray();
					Plugin.Trace($"Sprites with '{word}': {names.Length} " + string.Join(", ", names));
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Sprites: could not list them: {e.Message}");
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

		// The tab that is not open lights up (the animation of being switched on) while the mouse is on it and goes dark when it leaves
		internal class HoverLight : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
		{
			internal AnimatedToggle Toggle;
			private static bool _logged;

			public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
			{
				if (Toggle != null && !Toggle.isOn)
				{
					if (!_logged)
					{
						_logged = true;
						Plugin.Trace($"Tab hover: {Toggle.name}, transition {Toggle.transition}, interactable {Toggle.IsInteractable()}");
					}

					Toggle.TriggerAnimation(true);
				}
			}

			public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
			{
				if (Toggle != null && !Toggle.isOn)
				{
					Toggle.TriggerAnimation(false);
				}
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
