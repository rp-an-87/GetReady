using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EFT.UI;
using EFT.UI.Builds;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GetReady
{
	// The Get ready window made from a copy of a game window (the "name a preset" window of the Builds tab), so it has the game's own
	// caption bar, red cross, dragging, Esc and stacking, and its pages are built from Unity UI in the game's font. Experimental: opened
	// by an F12 key, next to the IMGUI window (ReadyWindow), which stays the real one until this one is complete. The Ready page only.
	internal static partial class ReadyUiWindow
	{
		private static EditBuildNameWindow _window;
		private static RectTransform _content;
		private static ScrollRect _scroll;
		private static TextMeshProUGUI _status;
		private static string _signature = "";
		private static float _checkedAt;
		private static int _pending;
		private static int _pendingRounds;
		private static bool _dumped;

		// Which page shows: 0 = Ready, 1 = Setup
		private static int _tab;

		// The window's size: what the owner dragged it to is kept while the game runs
		private static float _width = 900f, _height = 940f;

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
					Plugin.Log.LogWarning("Get ready window: the game's window template is not available here");
					return;
				}

				var window = UnityEngine.Object.Instantiate(context._editBuildNameWindow, context._infoWindowsContainer, false);
				window.name = "GetReady Window";
				var windowContext = ((Window<EditBuildNameWindowContext>)window).Show();
				UiKit.Init(window.Caption);
				UiKit.Scale = ReadyWindow.Scale;
				window.Caption.text = "Get ready for raid";
				Build(window);
				window.WindowTransform.SetAsLastSibling();
				context.RegisterWindow<EditBuildNameWindowContext>(window, windowContext);
				windowContext.OnClose += () =>
				{
					if (window != null)
					{
						UnityEngine.Object.Destroy(window.gameObject);
					}

					_window = null;
					_content = null;
					_scroll = null;
					_status = null;
				};
				_window = window;
				_signature = "";
				_pendingRounds = 0;
				ReadyPlan.Refresh();
				Rebuild();
				Plugin.Log.LogInfo("Get ready window: opened");
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Get ready window failed: {e}");
			}
		}

		// The size of what is inside: the saved Window size setting scales the text and the pictures of the page, built again when it
		// changes. The window itself keeps its size, so it stays where the game puts a window and can be dragged.
		internal static void ApplyScale()
		{
			if (_window == null)
			{
				return;
			}

			float scale = ReadyWindow.Scale;
			if (!Mathf.Approximately(scale, UiKit.Scale))
			{
				UiKit.Scale = scale;
				BuildBody(_window.WindowTransform);
			}
		}

		// Looked at from the plugin's Update: the page is built again when the plan (or the icons that were still loading) changed
		internal static void Update()
		{
			if (_window == null || _content == null || Time.unscaledTime - _checkedAt < 0.4f)
			{
				return;
			}

			_checkedAt = Time.unscaledTime;
			ApplyScale();
			try
			{
				string signature = Signature();
				if (signature != _signature || (_pending > 0 && _pendingRounds < 25))
				{
					_pendingRounds = signature != _signature ? 0 : _pendingRounds + 1;
					_signature = signature;
					Rebuild();
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Get ready window update failed: {e}");
			}
		}

		private static string Signature()
		{
			var text = new StringBuilder();
			foreach (var pair in ReadyPlan.Plan)
			{
				text.Append(pair.Key.Name).Append(pair.Key.Enabled).Append('|');
				foreach (var line in pair.Value)
				{
					text.Append(line.Text).Append(line.Subject).Append(line.Detail).Append(line.Have).Append(line.Want).Append(line.Problem).Append(line.Skipped).Append(line.Run != null).Append('|');
				}
			}

			return text.Append(ReadyPlan._status).ToString();
		}

		// The window: the game's caption, then our own body in place of the name field and the Save button
		private static void Build(EditBuildNameWindow window)
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

			frame.sizeDelta = new Vector2(_width, _height);
			try
			{
				frame.SetInCenter();
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Get ready window: could not centre it: {e.Message}");
			}

			_captionHeight = window._captionPanel != null ? ((RectTransform)window._captionPanel.transform).rect.height : 0f;
			UiResize.Add(frame, new Vector2(520f, 420f), new Vector2(4000f, 4000f), () =>
			{
				_width = frame.sizeDelta.x;
				_height = frame.sizeDelta.y;
				FitBody(frame);
			});
			BuildBody(frame);
			if (!_dumped)
			{
				_dumped = true;
				Dump(frame, 0, 4);
			}
		}

		private static float _captionHeight;

		private static readonly Color TabOn = new Color(0.25f, 0.23f, 0.15f), TabOff = new Color(0.13f, 0.135f, 0.135f);

		private static void Tab(Transform parent, string name, int index)
		{
			bool on = _tab == index;
			var button = UiKit.PushButton(parent, name, () => SetTab(index), on ? TabOn : TabOff, on ? new Color(0.31f, 0.29f, 0.19f) : new Color(0.2f, 0.21f, 0.21f), on ? new Color(0.95f, 0.9f, 0.72f) : Ui.Text, height: UiKit.Unit * 1.8f);
			button.GetComponent<LayoutElement>().flexibleWidth = 1f;
		}

		private static void SetTab(int tab)
		{
			if (_tab == tab || _window == null)
			{
				return;
			}

			_tab = tab;
			BuildBody(_window.WindowTransform);
		}

		// One small tab per section of the Setup page: a click scrolls the section to the top
		private static void JumpStrip(Transform parent)
		{
			var names = SetupPage.Names();
			var widths = names.Select(name => name.Length * UiKit.Unit * 0.5f + UiKit.Unit * 2f + 6f).ToList();
			foreach (var rowIndexes in ReadyWindow.TabRows(widths, _width - 40f))
			{
				var row = UiKit.HBox(parent, 4f);
				foreach (int index in rowIndexes)
				{
					int section = index;
					var button = UiKit.PushButton(row, names[index], () => JumpTo(section), new Color(0.15f, 0.155f, 0.155f), new Color(0.22f, 0.23f, 0.22f), Ui.Text, height: UiKit.Unit * 1.5f);
					button.GetComponent<LayoutElement>().flexibleWidth = 1f;
				}
			}
		}

		private static void JumpTo(int index)
		{
			if (_content == null || _scroll == null || index < 0 || index >= SetupPage.Sections.Count || SetupPage.Sections[index] == null)
			{
				return;
			}

			Canvas.ForceUpdateCanvases();
			float top = -RectTransformUtility.CalculateRelativeRectTransformBounds(_content, SetupPage.Sections[index]).max.y;
			float most = Mathf.Max(0f, _content.rect.height - _scroll.viewport.rect.height);
			_content.anchoredPosition = new Vector2(0f, Mathf.Clamp(top - 8f, 0f, most));
		}

		// A setting was changed on the Setup page: keep it, plan again, and draw the page again
		internal static void SettingChanged()
		{
			SettingsStore.Save();
			ReadyPlan.RefreshSoon();
			Rebuild();
		}
		private const float ScrollMin = 200f;
		private static int _fitLogged;

		// The body fills the window under the caption, whatever the window's size is now
		private static void FitBody(RectTransform frame)
		{
			if (_root == null)
			{
				return;
			}

			UiKit.Stretch(_root, 0f, 0f, 0f, Mathf.Max(_captionHeight, 30f));
			LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
			if (_fitLogged < 3)
			{
				_fitLogged++;
				Plugin.Log.LogInfo($"Get ready window resized: frame {frame.rect.size} (sizeDelta {frame.sizeDelta}, anchors {frame.anchorMin}-{frame.anchorMax}), body {_root.rect.size} (anchors {_root.anchorMin}-{_root.anchorMax})");
			}
		}
		private static RectTransform _root;

		// Our body under the caption: the tabs, the page and the footer. Built again when the size changes.
		private static void BuildBody(RectTransform frame)
		{
			if (_root != null)
			{
				_root.SetParent(null, false);
				UnityEngine.Object.Destroy(_root.gameObject);
			}

			var root = UiKit.VBox(frame, 8f, new RectOffset(10, 10, 10, 10));
			_root = root;
			root.name = "GetReady Root";
			root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
			UiKit.Stretch(root, 0f, 0f, 0f, Mathf.Max(_captionHeight, 30f));

			var tabs = UiKit.HBox(root, 8f);
			Tab(tabs, "Ready", 0);
			Tab(tabs, "Setup", 1);
			if (_tab == 1)
			{
				JumpStrip(root);
			}

			_scroll = UiKit.Scroll(root, out _content);
			var scrollSize = UiKit.Size(_scroll, flexWidth: 1f, flexHeight: 1f);
			scrollSize.minHeight = ScrollMin;
			scrollSize.preferredHeight = 400f;

			var footer = UiKit.HBox(root, 12f);
			_status = UiKit.Label(footer, "", 0.8f, Ui.Dim, flex: true);
			UiKit.PushButton(footer, "Get ready", ReadyWindow.QuickRun, new Color(0.24f, 0.36f, 0.19f), new Color(0.3f, 0.44f, 0.23f), Color.white, width: UiKit.Unit * 9f, height: UiKit.Unit * 2.2f);

			// What the tabs, the footer, the spacing and a little of the page need, so the body always fits the window
			float minHeight = Mathf.Max(_captionHeight, 30f) + 20f + UiKit.Unit * 2f + UiKit.Unit * 2.4f + 16f + ScrollMin;
			UiResize.SetMin(frame, new Vector2(520f, minHeight));
			_height = Mathf.Max(_height, minHeight);
			_signature = "";
			Rebuild();
			UiResize.ToFront(frame);
		}

		// What the copy of the window is made of, once in the log, for when something sits in the wrong place
		private static void Dump(Transform node, int depth, int max)
		{
			var rect = node as RectTransform;
			var parts = string.Join(", ", node.GetComponents<Component>().Where(part => part != null && !(part is Transform)).Select(part => part.GetType().Name).ToArray());
			Plugin.Log.LogInfo($"Get ready window tree: {new string(' ', depth * 2)}{node.name} active={node.gameObject.activeSelf} size={(rect != null ? rect.rect.size.ToString() : "-")} [{parts}]");
			if (depth >= max)
			{
				return;
			}

			for (int i = 0; i < node.childCount; i++)
			{
				Dump(node.GetChild(i), depth + 1, max);
			}
		}

		private static void Rebuild()
		{
			if (_content == null)
			{
				return;
			}

			float scroll = _content.anchoredPosition.y;
			for (int i = _content.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
			}

			_pending = 0;
			if (_tab == 1)
			{
				SetupPage.Build(_content);
			}
			else
			{
				ReadyPage(_content);
			}

			if (_status != null)
			{
				_status.text = ReadyPlan._status ?? "";
			}

			LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
			_content.anchoredPosition = new Vector2(0f, scroll);
		}

		private static Sprite SpriteOf(PlanLine line)
		{
			Sprite sprite = null;
			if (line.Item != null)
			{
				sprite = Icons.OfItem(line.Item);
			}
			else if (!string.IsNullOrEmpty(line.Template))
			{
				sprite = Icons.Of(line.Template);
			}
			else
			{
				return null;
			}

			if (sprite == null)
			{
				_pending++;
			}

			return sprite;
		}

		private static Color ColorOf(ReadyPlan.Standing standing) => ReadyWindow.StandingColor(standing);

		// The Ready page: one line of status on top, then what each step found
		private static void ReadyPage(RectTransform content)
		{
			int moves, problemGroups;
			ReadyPlan.Tally(out moves, out problemGroups);
			if (ReadyPlan.Plan.Count == 0)
			{
				var box = UiKit.VBox(content, 4f, new RectOffset(12, 12, 10, 10), UiKit.PanelColor);
				UiKit.Label(box, "Nothing is switched on. Turn something on in Setup.", 0.9f, Ui.Text);
				return;
			}

			if (moves == 0 && problemGroups == 0)
			{
				var box = UiKit.VBox(content, 4f, new RectOffset(14, 14, 10, 10), UiKit.OkColor);
				UiKit.Label(box, "You are all set", 1.1f, Ui.Heading, bold: true);
				UiKit.Label(box, "Everything you asked for is already on you.", 0.9f, Ui.Text);
			}
			else
			{
				var row = UiKit.HBox(content, 8f, null, null, true);
				if (problemGroups > 0)
				{
					var box = UiKit.VBox(row, 0f, new RectOffset(14, 14, 10, 10), UiKit.ProblemColor);
					UiKit.Size(box, flexWidth: 1f);
					UiKit.Label(box, problemGroups == 1 ? "1 thing can't be done as set up" : $"{problemGroups} things can't be done as set up", 1f, Ui.Bad);
					UiKit.Tip(box, ReadyPlan.ProblemsTooltip());
				}

				if (moves > 0)
				{
					var box = UiKit.VBox(row, 0f, new RectOffset(14, 14, 10, 10), UiKit.TodoColor);
					UiKit.Size(box, flexWidth: 1f);
					UiKit.Label(box, moves == 1 ? "1 thing to fulfil" : $"{moves} things to fulfil", 1f, Ui.Warn);
					UiKit.Tip(box, ReadyPlan.ToDoTooltip());
				}
			}

			// What is switched off goes last, greyed out (the sort keeps the usual order inside each group)
			foreach (var step in Steps.DisplayOrder.OrderBy(step => step.Enabled ? 0 : 1))
			{
				var lines = ReadyPlan.Plan.Where(entry => entry.Key == step).Select(entry => entry.Value).FirstOrDefault();
				var section = UiKit.VBox(content, 6f, new RectOffset(12, 12, 10, 10), UiKit.PanelColor, UiKit.SectionEdge);
				if (!step.Enabled)
				{
					section.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
				}

				var head = UiKit.HBox(section, 8f);
				UiKit.Icon(head, Icons.StepSprite(step.Name), UiKit.Unit * 1.3f, step.Name);
				UiKit.Label(head, step.Name, 1f, Ui.Heading, bold: true);
				UiKit.Spacer(head);
				StepChip(head, step, lines);
				if (step.Enabled && lines != null && lines.Count > 0)
				{
					UiKit.Line(section);
					if (step is AmmoStep)
					{
						AmmoPanelLines(section, lines);
					}
					else
					{
						PlanLines(section, lines);
					}
				}
			}
		}

		// What the step will do, as a small tag: red for what cannot be done, amber for what Get ready will do
		private static void StepChip(Transform parent, IStep step, List<PlanLine> lines)
		{
			if (!step.Enabled || lines == null)
			{
				UiKit.Chip(parent, "Off", new Color(0.25f, 0.26f, 0.25f));
				return;
			}

			int moves = 0, problems = 0;
			ReadyPlan.TallyLines(lines, ref moves, ref problems);
			if (problems > 0)
			{
				UiKit.Chip(parent, problems == 1 ? "1 problem" : $"{problems} problems", new Color(0.6f, 0.2f, 0.18f));
			}

			if (moves > 0)
			{
				UiKit.Chip(parent, $"{moves} to do", new Color(0.42f, 0.36f, 0.16f));
			}

			if (problems == 0 && moves == 0)
			{
				UiKit.Chip(parent, "Ready", new Color(0.2f, 0.38f, 0.22f));
			}
		}

		// The lines of a step: each progress line opens a card, and what follows it (what will be brought, what is wrong) sits in it
		private static void PlanLines(Transform parent, List<PlanLine> lines)
		{
			RectTransform card = null;
			for (int index = 0; index < lines.Count; index++)
			{
				var line = lines[index];
				if (line.Standalone)
				{
					card = null;
				}

				if (line.IsProgress)
				{
					var standing = ReadyPlan.StandingFor(lines, index);
					card = Card(parent, standing);
					ProgressRow(card, line, standing);
					continue;
				}

				DetailLine(card ?? (RectTransform)parent, line, card != null);
			}
		}

		// One card: a stripe in the colour of how it stands, and the box its content goes in
		private static RectTransform Card(Transform parent, ReadyPlan.Standing standing)
		{
			var outer = UiKit.HBox(parent, 0f, null, UiKit.CardColor, true, UiKit.CardEdge);
			var stripe = UiKit.Node("Stripe", outer);
			stripe.gameObject.AddComponent<Image>().color = ColorOf(standing);
			UiKit.Size(stripe, 4f);
			var card = UiKit.VBox(outer, 4f, new RectOffset(10, 10, 8, 8));
			UiKit.Size(card, flexWidth: 1f);
			return card;
		}

		// What is wanted against what is there: icon, name, the numbers and a bar, in the colour of how the card stands
		private static void ProgressRow(Transform parent, PlanLine line, ReadyPlan.Standing standing)
		{
			var color = line.Skipped ? Ui.Dim : ColorOf(standing);
			var row = UiKit.HBox(parent, 8f);
			var sprite = SpriteOf(line);
			if (line.Item != null || !string.IsNullOrEmpty(line.Template))
			{
				UiKit.Icon(row, sprite, UiKit.Unit * 1.8f, line.Subject);
			}

			var column = UiKit.VBox(row, 3f);
			UiKit.Size(column, flexWidth: 1f);
			var top = UiKit.HBox(column, 8f);
			UiKit.Label(top, line.Subject, 0.9f, line.Skipped ? Ui.Dim : Ui.Text, flex: true);
			if (line.Skipped)
			{
				UiKit.Label(top, line.Note, 0.8f, Ui.Dim, align: TextAlignmentOptions.Right);
				return;
			}

			UiKit.Label(top, Game.Ratio(line.Have, line.Want, line.Unit), 0.9f, color, align: TextAlignmentOptions.Right);
			UiKit.Bar(column, line.Want > 0 ? line.Have / line.Want : 1f, color);
		}

		private static void DetailLine(RectTransform parent, PlanLine line, bool indent)
		{
			if (line.Problem)
			{
				var box = UiKit.HBox(parent, 8f, new RectOffset(10, 10, 6, 6), UiKit.ProblemColor);
				if (!line.AlertIconOnly && (line.Item != null || !string.IsNullOrEmpty(line.Template)))
				{
					UiKit.Icon(box, SpriteOf(line), UiKit.Unit * 1.8f, line.Subject);
				}

				var column = UiKit.VBox(box, 2f);
				UiKit.Size(column, flexWidth: 1f);
				UiKit.Label(column, line.Subject ?? line.Text, 0.9f, Ui.Bad, wrap: true);
				if (line.Detail != null)
				{
					UiKit.Label(column, line.Detail, 0.85f, Ui.Text, wrap: true);
				}
			}
			else if (line.Run != null)
			{
				var row = UiKit.HBox(parent, 6f);
				if (indent)
				{
					UiKit.Gap(row, UiKit.Unit * 2f);
				}

				UiKit.Label(row, "+", 0.9f, Ui.Warn);
				UiKit.Label(row, line.Text, 0.85f, Color.white, wrap: true, flex: true);
			}
			else
			{
				UiKit.Label(parent, line.Text, 0.8f, Ui.Dim, wrap: true);
			}
		}
	}
}
