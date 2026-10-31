using UnityEngine;

namespace GetReady
{
	// The tooltip of the window: the text of whatever control the mouse rests on (a GUIContent's tooltip), in a small box next to the
	// mouse after the shortest wait. It is drawn as a window of its own, so it lies on top of the others (the game's own tooltip would be
	// hidden behind an IMGUI window).
	internal static class Tooltips
	{
		internal const int TipId = 0x6e7265;
		internal const float Delay = 0.05f;
		internal const float MaxWidth = 340f;

		private static string _text = "";
		private static float _since;
		private static bool _show;
		private static Rect _rect;

		// Whether the text has been under the mouse long enough
		internal static bool IsDue(float since, float now) => now - since >= Delay;

		// Where the box goes: right and below the mouse, moved to the other side of it when that would leave the screen, and never off
		// the screen (all in the window's own units)
		internal static Rect Place(Vector2 mouse, Vector2 size, Vector2 screen)
		{
			float x = mouse.x + 16f;
			float y = mouse.y + 22f;
			if (x + size.x > screen.x - 4f)
			{
				x = mouse.x - size.x - 8f;
			}

			if (y + size.y > screen.y - 4f)
			{
				y = mouse.y - size.y - 10f;
			}

			x = Mathf.Clamp(x, 4f, Mathf.Max(4f, screen.x - size.x - 4f));
			y = Mathf.Clamp(y, 4f, Mathf.Max(4f, screen.y - size.y - 4f));
			return new Rect(x, y, size.x, size.y);
		}

		// The text of the control under the mouse (GUI.tooltip), noted once per frame while drawing; a press starts the wait again
		internal static void Observe(string text)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}

			text = text ?? "";
			if (text != _text || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				_text = text;
				_since = Time.unscaledTime;
			}
		}

		// Forgets what was under the mouse (the window closed)
		internal static void Reset()
		{
			_text = "";
			_show = false;
		}

		internal static void Draw(float scale)
		{
			// Decided once per frame, in the layout pass, so the layout and the draw pass see the same windows
			if (Event.current.type == EventType.Layout)
			{
				_show = _text.Length > 0 && IsDue(_since, Time.unscaledTime) && !Input.GetMouseButton(0) && !Input.GetMouseButton(1);
				if (_show)
				{
					var content = new GUIContent(_text);
					float width = Mathf.Min(MaxWidth, Ui.TipText.CalcSize(content).x + 2f);
					float height = Ui.TipText.CalcHeight(content, width);
					var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / scale;
					_rect = Place(mouse, new Vector2(width, height), new Vector2(Screen.width, Screen.height) / scale);
				}
			}

			if (!_show)
			{
				return;
			}

			GUI.Window(TipId, _rect, id => GUI.Label(new Rect(0, 0, _rect.width, _rect.height), _text, Ui.TipText), GUIContent.none, Ui.Tip);
			GUI.BringWindowToFront(TipId);
		}
	}
}
