using System;
using EFT.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GetReady
{
	// Lets a window made from a copy of a game window be resized by dragging its right edge, bottom edge or bottom-right corner, with the
	// game's own resize cursors. The window keeps its top-left corner where it is.
	internal class ResizeHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler
	{
		internal RectTransform Frame;
		internal bool Horizontal, Vertical;
		internal Vector2 Min, Max;
		internal ECursorType Cursor;
		internal Action OnResized;

		private Vector2 _last;
		private bool _down;

		public void OnPointerDown(PointerEventData eventData)
		{
			_last = eventData.position;
			_down = true;
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			_down = false;
			CursorSwitcher.SetCursor(default(ECursorType));
		}

		public void OnPointerEnter(PointerEventData eventData) => CursorSwitcher.SetCursor(Cursor);

		public void OnPointerExit(PointerEventData eventData)
		{
			if (!_down)
			{
				CursorSwitcher.SetCursor(default(ECursorType));
			}
		}

		public void OnDrag(PointerEventData eventData)
		{
			var delta = eventData.position - _last;
			_last = eventData.position;
			var scale = Frame.lossyScale;
			float dw = Horizontal ? delta.x / Mathf.Max(scale.x, 0.01f) : 0f;
			float dh = Vertical ? -delta.y / Mathf.Max(scale.y, 0.01f) : 0f;
			var size = Frame.sizeDelta;
			var next = new Vector2(Mathf.Clamp(size.x + dw, Min.x, Max.x), Mathf.Clamp(size.y + dh, Min.y, Max.y));
			var change = next - size;
			if (change == Vector2.zero)
			{
				return;
			}

			Frame.sizeDelta = next;
			// The top-left corner stays put, whatever the pivot is
			Frame.anchoredPosition += new Vector2(Frame.pivot.x * change.x, -(1f - Frame.pivot.y) * change.y);
			OnResized?.Invoke();
		}
	}

	internal static class UiResize
	{
		private const float Edge = 8f, Corner = 18f;

		private static readonly string[] Names = { "Resize Right", "Resize Bottom", "Resize Corner" };

		// Adds the three handles to the window's frame. They are kept above the body, so call ToFront after the body is built.
		internal static void Add(RectTransform frame, Vector2 min, Vector2 max, Action onResized)
		{
			Handle(frame, Names[0], new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(Edge, -Corner), new Vector2(0f, Corner / 2f), true, false, ECursorType.StretchHorizontal, min, max, onResized);
			Handle(frame, Names[1], new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-Corner, Edge), new Vector2(-Corner / 2f, 0f), false, true, ECursorType.StretchVertical, min, max, onResized);
			Handle(frame, Names[2], new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(Corner, Corner), Vector2.zero, true, true, ECursorType.StretchCorner, min, max, onResized);
		}

		internal static void ToFront(RectTransform frame)
		{
			foreach (var name in Names)
			{
				var handle = frame.Find(name);
				if (handle != null)
				{
					handle.SetAsLastSibling();
				}
			}
		}

		private static void Handle(RectTransform frame, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position, bool horizontal, bool vertical, ECursorType cursor, Vector2 min, Vector2 max, Action onResized)
		{
			var old = frame.Find(name);
			if (old != null)
			{
				UnityEngine.Object.Destroy(old.gameObject);
			}

			var rect = UiKit.Node(name, frame);
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;
			rect.pivot = pivot;
			rect.sizeDelta = size;
			rect.anchoredPosition = position;
			rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
			var image = rect.gameObject.AddComponent<Image>();
			image.color = new Color(0f, 0f, 0f, 0f);
			image.raycastTarget = true;
			var handle = rect.gameObject.AddComponent<ResizeHandle>();
			handle.Frame = frame;
			handle.Horizontal = horizontal;
			handle.Vertical = vertical;
			handle.Cursor = cursor;
			handle.Min = min;
			handle.Max = max;
			handle.OnResized = onResized;
		}
	}
}
