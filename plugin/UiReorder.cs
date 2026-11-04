using System;
using System.Collections.Generic;
using EFT.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GetReady
{
	// Rows of a list moved by dragging them (anywhere on the row). The hand cursor shows they can be; the row under the mouse is lit; while one
	// is dragged (and dims) the row it would take the place of is the one that is lit, nearest by height, so it need not be dropped exactly
	// on it. On release the list is told "from this index to that one" (both as the list is now), or nothing when it would not move.
	internal class ReorderGrip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
	{
		internal List<RectTransform> Rows;
		internal int Index;
		internal Action<int, int> OnMove;
		internal Image Backdrop;

		// The row being dragged, so the others do not light up (or change the cursor) by themselves meanwhile
		private static ReorderGrip _active;

		private static readonly Color Lit = new Color(0.8f, 0.72f, 0.45f, 0.16f), Off = new Color(0f, 0f, 0f, 0f);

		private int _target = -1;
		private bool _over;

		public void OnPointerEnter(PointerEventData eventData)
		{
			_over = true;
			if (_active == null)
			{
				CursorSwitcher.SetCursor(ECursorType.Hand);
				Light(this, true);
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			_over = false;
			if (_active == null)
			{
				CursorSwitcher.SetCursor(ECursorType.Idle);
				Light(this, false);
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			_active = this;
			_target = -1;
			CursorSwitcher.SetCursor(ECursorType.Hand);
			Light(this, false);
			Dim(Rows[Index], 0.45f);
		}

		public void OnDrag(PointerEventData eventData)
		{
			int next = Nearest(eventData);
			if (next == Index)
			{
				next = -1;
			}

			if (next != _target)
			{
				if (_target >= 0)
				{
					Light(Rows[_target].GetComponent<ReorderGrip>(), false);
				}

				_target = next;
				if (_target >= 0)
				{
					Light(Rows[_target].GetComponent<ReorderGrip>(), true);
				}
			}
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			if (_target >= 0)
			{
				Light(Rows[_target].GetComponent<ReorderGrip>(), false);
			}

			int target = _target;
			_target = -1;
			_active = null;
			Dim(Rows[Index], 1f);
			CursorSwitcher.SetCursor(_over ? ECursorType.Hand : ECursorType.Idle);
			Light(this, _over);
			if (target >= 0)
			{
				OnMove(Index, target);
			}
		}

		// The row the pointer is on, or the one nearest to it by height (it does not have to be over a row, or even over the list)
		private int Nearest(PointerEventData eventData)
		{
			var container = (RectTransform)Rows[Index].parent;
			Vector2 local;
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, eventData.position, eventData.pressEventCamera, out local))
			{
				return -1;
			}

			int best = -1;
			float bestDistance = float.MaxValue;
			for (int i = 0; i < Rows.Count; i++)
			{
				var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(container, Rows[i]);
				float distance = local.y > bounds.max.y ? local.y - bounds.max.y : local.y < bounds.min.y ? bounds.min.y - local.y : 0f;
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = i;
				}
			}

			return best;
		}

		private static void Light(ReorderGrip row, bool on)
		{
			if (row != null && row.Backdrop != null)
			{
				row.Backdrop.color = on ? Lit : Off;
			}
		}

		private static void Dim(RectTransform row, float alpha)
		{
			var group = row.GetComponent<CanvasGroup>() ?? row.gameObject.AddComponent<CanvasGroup>();
			group.alpha = alpha;
		}
	}

	internal static class UiReorder
	{
		// Makes each row draggable by any part of it, inside the container (UiKit.Grip draws the three lines that tell it can be)
		internal static void Attach(RectTransform container, List<RectTransform> rows, Action<int, int> onMove)
		{
			for (int i = 0; i < rows.Count; i++)
			{
				// A row with no picture of its own cannot be hit: an invisible one makes all of it grabbable
				if (rows[i].GetComponent<Graphic>() == null)
				{
					var clear = rows[i].gameObject.AddComponent<Image>();
					clear.color = new Color(0f, 0f, 0f, 0f);
					clear.raycastTarget = true;
				}

				var backdrop = UiKit.Node("Hover", rows[i]);
				backdrop.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
				UiKit.Stretch(backdrop, -4f, -2f, -4f, -2f);
				var light = backdrop.gameObject.AddComponent<Image>();
				light.color = new Color(0f, 0f, 0f, 0f);
				light.raycastTarget = false;
				backdrop.SetAsFirstSibling();
				var handle = rows[i].gameObject.AddComponent<ReorderGrip>();
				handle.Backdrop = light;
				handle.Rows = rows;
				handle.Index = i;
				handle.OnMove = onMove;
			}
		}
	}
}
