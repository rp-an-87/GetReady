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

		// The game's own UI (stash scrolling, clicks) listens to the EventSystem and knows nothing of this IMGUI window, so
		// it is switched off while the mouse is over the window, and back on when the mouse leaves
		private static void BlockGameInput()
		{
			var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
			bool over = (_open && _rect.Contains(mouse)) || (Alerts.Count > 0 && _alertRect.Contains(mouse));
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

		private static void Close()
		{
			_open = false;
		}

		internal static void Tick()
		{
			// Esc closes it (and the alert), and so does leaving the stash screen
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				Alerts.Clear();
				if (_open)
				{
					Close();
				}
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
			if (ReadyButtonPatch.StashShown && _dirty && !_running && Time.unscaledTime - _dirtyAt > 0.3f)
			{
				Refresh();
			}
		}

		internal static void Draw()
		{
			Ui.Init();
			if (_open)
			{
				_rect = GUILayout.Window(WindowId, _rect, Contents, GUIContent.none, Ui.Window);
			}

			if (Alerts.Count > 0)
			{
				_alertRect = GUILayout.Window(AlertId, _alertRect, AlertContents, GUIContent.none, Ui.Alert);
				GUI.BringWindowToFront(AlertId);
			}
		}

		// Which page is showing: 0 = Ready (what is missing and the button), 1 = Setup (what to bring and where)
		private static int _tab;

		private static void Contents(int id)
		{
			var controller = Game.Controller();
			bool changed = false;

			GUILayout.BeginHorizontal();
			GUILayout.Label("Get ready for raid", Ui.Title);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("x", "Close"), Ui.Small))
			{
				_closeRequested = true;
			}

			GUILayout.EndHorizontal();
			GUILayout.Label("Clicking the stash button does everything on the Ready page at once. Ctrl+click opens this window.", Ui.Muted);
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

			if (_tab == 0)
			{
				ReadyPage();
			}
			else
			{
				changed = SetupPage(controller);
			}

			GUILayout.Box(GUIContent.none, Ui.Divider);
			// A fixed height of three lines: the hover text changes between the layout pass and the draw pass, and a label whose height
			// follows its text breaks the layout as soon as a hint wraps onto a second line
			GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? _status : GUI.tooltip, Ui.Muted, GUILayout.Height(50));
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
