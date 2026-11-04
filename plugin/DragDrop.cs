using System.Collections.Generic;
using UnityEngine;

namespace GetReady
{
	// Drag and drop for the Setup window: press on a grip or a chip, move more than a few pixels and let go over a target. A press that
	// doesn't move is a click. The drop is not applied where it happens (the targets drawn later in the pass don't have their rects
	// yet): it is kept for the next repaint, when the list that owns it has drawn its targets and settles it (TakeDrop).
	internal static class Dnd
	{
		private const float Threshold = 6f;

		private static Vector2 _start;
		private static string _ghost;

		internal static bool Active;
		internal static string Kind, Group, Key;

		private static bool _hasDrop;
		private static string _dropKind, _dropGroup, _dropKey;
		private static Vector2 _dropPos;
		private static int _dropFrame;

		internal static bool IsDragging(string kind, string group, string key) => Active && Kind == kind && Group == group && Key == key;

		// Whether this kind of drag, for this list, is going on (to draw where it would land)
		internal static bool IsDragging(string kind, string group) => Active && Kind == kind && Group == group;

		// Handles the mouse for one source in the rect: true when it was a click (pressed and released without moving)
		internal static bool Handle(Rect rect, string kind, string group, string key, string ghost)
		{
			int id = GUIUtility.GetControlID(FocusType.Passive);
			var e = Event.current;
			if (e.type == EventType.Repaint && Active && !Input.GetMouseButton(0))
			{
				Active = false; // the button was let go where nobody saw it (the window closed, the mouse left the game)
			}

			switch (e.GetTypeForControl(id))
			{
				case EventType.MouseDown:
					if (e.button == 0 && rect.Contains(e.mousePosition))
					{
						GUIUtility.hotControl = id;
						_start = e.mousePosition;
						Kind = kind;
						Group = group;
						Key = key;
						_ghost = ghost;
						Active = false;
						e.Use();
					}

					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == id)
					{
						if (!Active && (e.mousePosition - _start).sqrMagnitude > Threshold * Threshold)
						{
							Active = true;
						}

						e.Use();
					}

					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == id)
					{
						GUIUtility.hotControl = 0;
						e.Use();
						if (!Active)
						{
							return true;
						}

						Active = false;
						_hasDrop = true;
						_dropKind = kind;
						_dropGroup = group;
						_dropKey = key;
						_dropPos = e.mousePosition;
						_dropFrame = Time.frameCount;
					}

					break;
			}

			return false;
		}

		// The drop waiting for this list, once the list has drawn its targets (in a repaint). The position is where the mouse was let go.
		internal static bool TakeDrop(string kind, string group, out string key, out Vector2 pos)
		{
			key = null;
			pos = Vector2.zero;
			if (!_hasDrop || Event.current.type != EventType.Repaint)
			{
				return false;
			}

			if (Time.frameCount - _dropFrame > 5)
			{
				_hasDrop = false; // nobody claimed it
				return false;
			}

			if (_dropKind != kind || _dropGroup != group)
			{
				return false;
			}

			_hasDrop = false;
			key = _dropKey;
			pos = _dropPos;
			return true;
		}

		private static readonly Color Gold = new Color(0.95f, 0.85f, 0.45f);
		private static readonly Color GripGrey = new Color(0.62f, 0.64f, 0.62f);

		// A thin gold bar showing where the dragged thing would land
		internal static void Bar(Rect rect)
		{
			var old = GUI.color;
			GUI.color = Gold;
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			GUI.color = old;
		}

		// A thin gold frame around a rect: the thing the dragged one would swap with
		internal static void Frame(Rect r)
		{
			Bar(new Rect(r.x - 2, r.y - 2, r.width + 4, 2));
			Bar(new Rect(r.x - 2, r.yMax, r.width + 4, 2));
			Bar(new Rect(r.x - 2, r.y, 2, r.height));
			Bar(new Rect(r.xMax, r.y, 2, r.height));
		}

		// Three short lines: what says "drag me". Gold under the mouse or while dragged.
		internal static void DrawGrip(Rect rect, bool lit)
		{
			var old = GUI.color;
			GUI.color = lit ? Gold : GripGrey;
			for (int i = 0; i < 3; i++)
			{
				GUI.DrawTexture(new Rect(rect.center.x - 8, rect.center.y - 6 + i * 5, 16, 2), Texture2D.whiteTexture);
			}

			GUI.color = old;
		}

		internal static Rect LastGrip;

		// A grip to grab a row by, in the layout
		internal static bool Grip(float height, string kind, string group, string key, string ghost)
		{
			var rect = GUILayoutUtility.GetRect(28, height, GUILayout.Width(28), GUILayout.Height(height));
			LastGrip = rect;
			if (Event.current.type == EventType.Repaint)
			{
				DrawGrip(rect, rect.Contains(Event.current.mousePosition) || IsDragging(kind, group, key));
			}

			GUI.Label(rect, new GUIContent("", "Drag to change the order"));
			return Handle(rect, kind, group, key, ghost);
		}

		// Rows of a list moved by their grips: the drop of this list, as the row it came from and the place it ends at (both indexes in
		// the list as it is now). sideways: the rows sit side by side in a grid, so before or after is by the left or right half.
		// False when nothing moved.
		internal static bool RowDrop(string group, IList<string> keys, IList<Rect> rects, out int from, out int to, bool sideways = false)
		{
			from = -1;
			to = -1;
			string key;
			Vector2 pos;
			if (!TakeDrop("item", group, out key, out pos))
			{
				return false;
			}

			from = keys.IndexOf(key);
			int at = -1;
			for (int i = 0; i < rects.Count; i++)
			{
				if (rects[i].Contains(pos))
				{
					at = (sideways ? pos.x < rects[i].center.x : pos.y < rects[i].center.y) ? i : i + 1;
					break;
				}
			}

			if (from < 0 || at < 0)
			{
				return false;
			}

			to = at > from ? at - 1 : at;
			return to != from;
		}

		// A bar beside the row under the mouse while a row of this list is dragged
		internal static void RowBar(string group, IList<Rect> rects, bool sideways = false)
		{
			if (!IsDragging("item", group) || Event.current.type != EventType.Repaint)
			{
				return;
			}

			var mouse = Event.current.mousePosition;
			for (int i = 0; i < rects.Count; i++)
			{
				if (!rects[i].Contains(mouse))
				{
					continue;
				}

				var r = rects[i];
				if (sideways)
				{
					Bar(new Rect(mouse.x < r.center.x ? r.x - 2 : r.xMax, r.y, 2, r.height));
				}
				else
				{
					Bar(new Rect(r.x, mouse.y < r.center.y ? r.y - 1 : r.yMax - 1, r.width, 2));
				}

				return;
			}
		}

		// What is being dragged, following the mouse (drawn last, at window level, so no card covers it)
		internal static void DrawGhost()
		{
			if (!Active || Event.current.type != EventType.Repaint)
			{
				return;
			}

			var mouse = Event.current.mousePosition;
			var rect = new Rect(mouse.x + 10, mouse.y + 6, Mathf.Max(90, 12 + 8 * (_ghost ?? "").Length), 26);
			var old = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, 0.92f);
			Ui.PickOn.Draw(rect, new GUIContent(_ghost), false, true, true, false);
			GUI.color = old;
		}
	}
}
