using System;
using System.Collections.Generic;
using System.Linq;
using EFT.UI;
using EFT.UI.Builds;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GetReady
{
	// What a quick run leaves behind when something is wrong: a window of the game's own kind (a copy of one, like the Get ready window) in the
	// upper middle of the screen, the problems grouped by step, each with its item's picture, what it is about and what is wrong. It stays until
	// it is closed (the red cross, Got it, or Esc) and does not need the Get ready window to be open. A new run replaces what it shows.
	internal static class ReadyAlert
	{
		private static EditBuildNameWindow _window;
		private static DefaultUIButton _buttonTemplate;
		private static RectTransform _root;
		private static List<KeyValuePair<string, PlanLine>> _problems = new List<KeyValuePair<string, PlanLine>>();
		private static float _captionHeight;
		private static float _redrawAt;

		internal static bool IsOpen => _window != null;

		internal static void Show(List<KeyValuePair<string, PlanLine>> problems)
		{
			_problems = problems.ToList();
			try
			{
				if (_window != null)
				{
					BuildBody();
					return;
				}

				var context = ItemUiContext.Instance;
				if (context == null || context._editBuildNameWindow == null || context._infoWindowsContainer == null)
				{
					Plugin.Log.LogWarning("Alert: the game's window template is not available here");
					return;
				}

				var window = UnityEngine.Object.Instantiate(context._editBuildNameWindow, context._infoWindowsContainer, false);
				window.name = "GetReady Alert";
				var windowContext = ((Window<EditBuildNameWindowContext>)window).Show();
				UiKit.Init(window.Caption);
				UiKit.Scale = ReadyWindow.Scale;
				window.Caption.text = "Check this before you leave";
				_buttonTemplate = window._saveButtonSpawner;
				Shell(window);
				window.WindowTransform.SetAsLastSibling();
				context.RegisterWindow<EditBuildNameWindowContext>(window, windowContext);
				windowContext.OnClose += () =>
				{
					if (window != null)
					{
						UnityEngine.Object.Destroy(window.gameObject);
					}

					_window = null;
					_root = null;
				};
				_window = window;
				BuildBody();
				// Pictures the game has not made yet come a moment later: drawn once more then
				_redrawAt = Time.unscaledTime + 0.7f;
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Alert failed: {e}");
			}
		}

		internal static void Close()
		{
			if (_window != null)
			{
				_window.CloseAsync();
			}
		}

		// Called every frame: the one redraw for pictures that were not there yet
		internal static void Update()
		{
			if (_window != null && _redrawAt > 0f && Time.unscaledTime >= _redrawAt)
			{
				_redrawAt = 0f;
				BuildBody();
			}
		}

		// A made-up run for looking at it (F12, Debugging)
		internal static void ShowTest()
		{
			Show(new List<KeyValuePair<string, PlanLine>>
			{
				new KeyValuePair<string, PlanLine>("Medicine to bring", new PlanLine { Problem = true, Text = "Healing kits: 120/300 HP", Subject = "Salewa", Detail = "Only 120 of the 300 HP you want: the stash has no more.", Template = "544fb45d4bdc2dee738b4568" }),
				new KeyValuePair<string, PlanLine>("Medicine to bring", new PlanLine { Problem = true, Text = "Bandages", Subject = "Aseptic bandage", Detail = "None in the stash.", Template = "544fb25a4bdc2dfb738b4567" }),
				new KeyValuePair<string, PlanLine>("Repair", new PlanLine { Problem = true, Text = "Repair", Subject = "6B43 Zabralo-Sh body armor", Detail = "No repair kit can repair it, and there are not enough roubles for a trader." }),
				new KeyValuePair<string, PlanLine>("Cash", new PlanLine { Problem = true, Text = "Cash", Subject = "Roubles", Detail = "No place on you takes the money: pockets, vest, backpack and pouch are full.", Template = Game.Roubles })
			});
		}

		// The game's window made ours: its caption and red cross stay, the rest is hidden, sized to the screen and put in the upper middle
		private static void Shell(EditBuildNameWindow window)
		{
			var frame = window.WindowTransform;
			window._inputField.gameObject.SetActive(false);
			window._saveButtonSpawner.gameObject.SetActive(false);
			window._placeholderText.gameObject.SetActive(false);
			var caption = window._captionPanel != null ? window._captionPanel.transform : null;
			for (int i = 0; i < frame.childCount; i++)
			{
				var child = frame.GetChild(i);
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

			_captionHeight = window._captionPanel != null ? ((RectTransform)window._captionPanel.transform).rect.height : 0f;
			frame.sizeDelta = new Vector2(Mathf.Min(UiKit.Unit * 32f, Screen.width * 0.9f), 300f);
			try
			{
				frame.SetInCenter();
				frame.anchoredPosition += new Vector2(0f, Screen.height * 0.12f);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Alert: could not place it: {e.Message}");
			}
		}

		private static void BuildBody()
		{
			if (_window == null)
			{
				return;
			}

			var frame = _window.WindowTransform;
			if (_root != null)
			{
				_root.SetParent(null, false);
				UnityEngine.Object.Destroy(_root.gameObject);
			}

			float top = Mathf.Max(_captionHeight, 30f);
			var root = UiKit.VBox(frame, 10f, new RectOffset(14, 14, 12, 14));
			_root = root;
			root.name = "GetReady Alert Root";
			root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
			UiKit.Stretch(root, 0f, 0f, 0f, top);

			var scroll = UiKit.Scroll(root, out var content);
			UiKit.Size(scroll, flexWidth: 1f, flexHeight: 1f);
			var red = new Color(0.9f, 0.4f, 0.35f);
			foreach (var step in _problems.GroupBy(pair => pair.Key))
			{
				UiKit.Label(content, step.Key, 0.95f, red, bold: true);
				foreach (var pair in step)
				{
					var line = pair.Value;
					var box = UiKit.HBox(content, 10f, new RectOffset(8, 8, 6, 6), UiKit.ProblemColor, edge: new Color(0.5f, 0.22f, 0.2f));
					if (line.Item != null)
					{
						SetupPage.ItemIcon(box, line.Item, UiKit.Unit * 2.6f, line.Subject ?? "");
					}
					else if (!string.IsNullOrEmpty(line.Template))
					{
						SetupPage.TemplateIcon(box, line.Template, UiKit.Unit * 2.6f, line.Subject ?? "");
					}

					var column = UiKit.VBox(box, 2f);
					UiKit.Size(column, flexWidth: 1f);
					UiKit.Label(column, line.Subject ?? line.Text, 0.95f, Ui.Text, bold: true, wrap: true, flex: true);
					if (!string.IsNullOrEmpty(line.Detail))
					{
						UiKit.Label(column, line.Detail, 0.85f, Ui.Dim, wrap: true, flex: true);
					}
				}
			}

			var footer = UiKit.HBox(root, 12f);
			UiKit.Spacer(footer);
			if (GameWidgets.Button(footer, "Got it", Close, UiKit.Unit * 8f, UiKit.Unit * 2.2f, false, _buttonTemplate) == null)
			{
				UiKit.PushButton(footer, "Got it", Close, new Color(0.2f, 0.21f, 0.21f), new Color(0.27f, 0.28f, 0.27f), Ui.Text, width: UiKit.Unit * 8f, height: UiKit.Unit * 2.2f);
			}

			// As high as what is in it, up to most of the screen
			LayoutRebuilder.ForceRebuildLayoutImmediate(content);
			float wanted = top + 12f + 14f + 20f + LayoutUtility.GetPreferredHeight(content) + UiKit.Unit * 2.2f + 6f;
			frame.sizeDelta = new Vector2(frame.sizeDelta.x, Mathf.Clamp(wanted, 220f, Screen.height * 0.75f));
		}
	}
}
