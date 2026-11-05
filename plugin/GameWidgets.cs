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
		internal static DefaultUIButton Button(Transform parent, string text, Action click, float width, float height, bool flex = false, DefaultUIButton template = null)
		{
			template = template != null ? template : ButtonTemplate;
			if (template == null)
			{
				return null;
			}

			try
			{
				var copy = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
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

		private static Sprite[] _allSprites;
		private static readonly System.Collections.Generic.Dictionary<string, Sprite> Named = new System.Collections.Generic.Dictionary<string, Sprite>();

		// One of the game's sprites by its exact name (a fixed name written in the code, not a search); null when the game has none by that name
		internal static UnityEngine.Sprite ByName(string name)
		{
			UnityEngine.Sprite sprite;
			if (Named.TryGetValue(name, out sprite))
			{
				return sprite;
			}

			try
			{
				if (_allSprites == null)
				{
					_allSprites = Resources.FindObjectsOfTypeAll<UnityEngine.Sprite>();
				}

				sprite = _allSprites.FirstOrDefault(candidate => candidate != null && candidate.name == name);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Sprite {name}: could not look for it: {e.Message}");
			}

			if (sprite == null)
			{
				Plugin.Log.LogWarning($"The game has no sprite named {name}");
			}
			else
			{
				Named[name] = sprite;
			}

			return sprite;
		}

		private static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> Whitened = new System.Collections.Generic.Dictionary<Sprite, Sprite>();

		// A copy of a sprite in white with the same shape (its alpha kept), made in memory: for a game icon that is coloured where the others are white
		internal static Sprite White(Sprite source)
		{
			if (source == null)
			{
				return null;
			}

			Sprite copy;
			if (Whitened.TryGetValue(source, out copy))
			{
				return copy;
			}

			try
			{
				var texture = source.texture;
				var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
				Graphics.Blit(texture, target);
				var previous = RenderTexture.active;
				RenderTexture.active = target;
				var rect = source.textureRect;
				int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
				var made = new Texture2D(width, height, TextureFormat.RGBA32, false);
				made.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(target);
				var pixels = made.GetPixels32();
				for (int i = 0; i < pixels.Length; i++)
				{
					pixels[i] = new Color32(255, 255, 255, pixels[i].a);
				}

				made.SetPixels32(pixels);
				made.Apply();
				copy = Sprite.Create(made, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not make {source.name} white: {e.Message}");
				copy = source;
			}

			Whitened[source] = copy;
			return copy;
		}

		// The stash's own picture: the one of its "All" filter, taken when the stash opens
		internal static Sprite StashSprite;

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

	}
}
