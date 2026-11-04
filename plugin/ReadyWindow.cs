using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GetReady
{
	// The window: a shell with two pages, Ready (what is missing, and the button) and Setup (what to bring and where). IMGUI with
	// a custom dark look (Ui), which needs no assets. The class is split by concern: the plan, running it, the alert, and the pages.
	internal static partial class ReadyWindow
	{
		private const int WindowId = 0x6e7264;

		private static bool _open;

		private static bool _closeRequested;

		private static Rect _rect = new Rect(80, 40, 740, 800);

		// The window is drawn through a scaling matrix: every size and position of the window (_rect, the alert box) is in its own
		// units, a screen pixel is Scale of them
		internal static float Scale => Mathf.Clamp(SettingsStore.Current.UiScale, 0.8f, 1.6f);

		private static Vector2 _scroll;

		internal static void Toggle()
		{
			_open = !_open;
			if (_open)
			{
				Refresh();
			}
		}

		private static EventSystem _blocked;

		// Whether the mouse button that is down now went down on our window (a click or a drag that is ours), else it belongs to the game
		private static bool _pressedOnUs;

		// The game's own UI (stash scrolling, clicks) listens to the EventSystem and knows nothing of this IMGUI window, so
		// it is switched off while the mouse is over the window, and back on when the mouse leaves. A drag that started in the game
		// (an item dragged across this window) is left alone: switching the game's input off in the middle of it would break the drag.
		private static void BlockGameInput()
		{
			var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / Scale;
			bool inside = (_open && _rect.Contains(mouse)) || (Alerts.Count > 0 && _alertRect.Contains(mouse));
			bool down = Input.GetMouseButton(0) || Input.GetMouseButton(1);
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				_pressedOnUs = inside;
			}

			// While a button is held, whoever it went down on keeps the mouse; with none held, whoever the mouse is over has it
			bool over = down ? _pressedOnUs : inside;
			if (over == (_blocked != null))
			{
				return;
			}

			if (over)
			{
				_blocked = EventSystem.current;
				if (_blocked != null)
				{
					_blocked.enabled = false;
				}
			}
			else
			{
				_blocked.enabled = true;
				_blocked = null;
			}
		}

		private static bool _wasShown;
		private static bool _escapePending;
		private static int _escapeFrame = -1;

		// Esc pressed: closes the alert box if it is up, else the window. Returns whether there was something to close (the key is then
		// taken, so the game's screen does not also react to it).
		internal static bool ConsumeEscape()
		{
			_escapePending = false;
			_escapeFrame = Time.frameCount;
			if (Alerts.Count > 0)
			{
				Alerts.Clear();
				return true;
			}

			if (_open)
			{
				Close();
				return true;
			}

			return false;
		}

		private static void Close()
		{
			_open = false;
		}

		internal static void Tick()
		{
			// Esc is taken by the game's stash screens through EscapePatches (ConsumeEscape), so only this window closes. Should a screen
			// not ask first, the window still closes a frame later, as it always did.
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
			if (_open && !shown)
			{
				Close();
			}

			// The status is kept up to date while the stash is up, window or not (the button shows it)
			if (shown && !_wasShown)
			{
				MarkDirty();
			}

			_wasShown = shown;
			if (!shown && _watched != null)
			{
				Watch(null);
			}

			BlockGameInput();
			// Cases changed by hand are recorded again once the moving has settled (a drag is a remove and an add)
			if (shown && !_running && CasesStep.Pending.Count > 0 && Time.unscaledTime - _dirtyAt > 0.3f)
			{
				var controller = Game.Controller();
				if (controller != null && CasesStep.SyncPending(controller.Inventory) > 0)
				{
					MarkDirty();
				}
			}

			if (ReadyButtonPatch.StashShown && _dirty && !_running && Time.unscaledTime - _dirtyAt > 0.3f)
			{
				Refresh();
			}
		}

		internal static void Draw()
		{
			Ui.Init();
			TextWeight.Active = true;
			try
			{
				DrawWindows();
			}
			finally
			{
				TextWeight.Active = false;
			}
		}

		// Unity runs a window's function after Draw has returned, so the text weight are switched on inside it
		private static void WeightedContents(int id)
		{
			var was = TextWeight.Active;
			TextWeight.Active = true;
			try
			{
				Contents(id);
			}
			finally
			{
				TextWeight.Active = was;
			}
		}

		private static void WeightedAlertContents(int id)
		{
			var was = TextWeight.Active;
			TextWeight.Active = true;
			try
			{
				AlertContents(id);
			}
			finally
			{
				TextWeight.Active = was;
			}
		}

		private static void DrawWindows()
		{
			var matrix = GUI.matrix;
			GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
			if (_open)
			{
				_rect.width = Mathf.Max(_rect.width, MinWindowWidth);
				_rect = GUILayout.Window(WindowId, _rect, WeightedContents, GUIContent.none, Ui.Window);
				Tooltips.Draw(Scale);
			}
			else
			{
				Tooltips.Reset();
				ResetSetup();
			}

			if (Alerts.Count > 0)
			{
				_alertRect = GUILayout.Window(AlertId, _alertRect, WeightedAlertContents, GUIContent.none, Ui.Alert);
				GUI.BringWindowToFront(AlertId);
			}

			GUI.matrix = matrix;
		}

		// Which page is showing: 0 = Ready (what is missing and the button), 1 = Setup (what to bring and where)
		private static int _tab;

		private static void Contents(int id)
		{
			var controller = Game.Controller();
			bool changed = false;

			GUILayout.BeginHorizontal();
			HelpTitle("Left-click the stash button to do everything on the Ready page at once. Right-click it to open this window.", "Get ready for raid", Ui.Title, 32f);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("x", "Close"), Ui.Small))
			{
				_closeRequested = true;
			}

			GUILayout.EndHorizontal();
			GUILayout.Space(4);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Ready", _tab == 0 ? Ui.TabOn : Ui.Tab))
			{
				_tab = 0;
			}

			if (GUILayout.Button("Setup", _tab == 1 ? Ui.TabOn : Ui.Tab))
			{
				_tab = 1;
			}

			GUILayout.EndHorizontal();
			GUILayout.Box(GUIContent.none, Ui.Divider);

			// Setup is measured while it is shown: leaving it, or closing the window, throws that away so it comes back from the top
			if (_tab != 1)
			{
				ResetSetup();
			}

			if (_tab == 0)
			{
				ReadyPage();
			}
			else
			{
				changed = SetupPage(controller);
			}

			GUILayout.Box(GUIContent.none, Ui.Divider);
			Tooltips.Observe(GUI.tooltip);
			GUILayout.Label(_status, Ui.Muted, GUILayout.Height(24));
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUI.enabled = !_running && Plan.Any(entry => entry.Value.Any(line => line.Run != null));
			if (GUILayout.Button(new GUIContent("Get ready", "Moves the items marked + from the stash into your gear"), Ui.Primary))
			{
				Go();
			}

			GUI.enabled = true;
			GUILayout.EndHorizontal();

			if (changed)
			{
				SettingsStore.Save();
				RefreshSoon();
			}

			if (_closeRequested)
			{
				_closeRequested = false;
				_open = false;
			}

			GUI.DragWindow(new Rect(0, 0, 10000, 44));
		}
	}
}
