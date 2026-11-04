using System;
using System.Collections.Generic;
using EFT.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GetReady
{
	// A chip that is clicked to switch (a normal button click) and dragged onto another chip to swap places with it. The hand cursor shows
	// it can be dragged, the chip it would swap with gets a gold frame, and it does not have to be dropped exactly on it: near it is enough.
	internal class SwapChip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
	{
		internal string Key;
		internal List<SwapChip> Peers;
		internal Action<string, string> OnSwap;

		private const float Reach = 14f;
		private SwapChip _target;
		private bool _over, _dragging;
		private int _dragEndFrame = -1;

		// Letting go of a drag over the chip it started on would also click it: a click that comes with a drag is not meant
		internal bool JustDragged => _dragging || _dragEndFrame == Time.frameCount;

		private RectTransform Rect => (RectTransform)transform;

		public void OnPointerEnter(PointerEventData eventData)
		{
			_over = true;
			CursorSwitcher.SetCursor(ECursorType.Hand);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			_over = false;
			if (!_dragging)
			{
				CursorSwitcher.SetCursor(ECursorType.Idle);
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			_dragging = true;
			CursorSwitcher.SetCursor(ECursorType.Hand);
			Dim(0.5f);
		}

		public void OnDrag(PointerEventData eventData)
		{
			var next = Nearest(eventData);
			if (next != _target)
			{
				Mark(_target, false);
				_target = next;
				Mark(_target, true);
			}
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			_dragging = false;
			_dragEndFrame = Time.frameCount;
			CursorSwitcher.SetCursor(_over ? ECursorType.Hand : ECursorType.Idle);
			Dim(1f);
			Mark(_target, false);
			var target = _target;
			_target = null;
			if (target != null)
			{
				OnSwap(Key, target.Key);
			}
		}

		// The other chip the pointer is on or close to (the nearest when several are)
		private SwapChip Nearest(PointerEventData eventData)
		{
			SwapChip best = null;
			float bestDistance = float.MaxValue;
			foreach (var peer in Peers)
			{
				if (peer == this || peer == null)
				{
					continue;
				}

				Vector2 local;
				RectTransformUtility.ScreenPointToLocalPointInRectangle(peer.Rect, eventData.position, eventData.pressEventCamera, out local);
				var reach = peer.Rect.rect;
				reach.xMin -= Reach;
				reach.xMax += Reach;
				reach.yMin -= Reach;
				reach.yMax += Reach;
				if (reach.Contains(local))
				{
					float distance = (local - peer.Rect.rect.center).sqrMagnitude;
					if (distance < bestDistance)
					{
						bestDistance = distance;
						best = peer;
					}
				}
			}

			return best;
		}

		private void Dim(float alpha)
		{
			var group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
			group.alpha = alpha;
		}

		private static void Mark(SwapChip chip, bool on)
		{
			if (chip == null)
			{
				return;
			}

			var outline = chip.GetComponent<Outline>() ?? chip.gameObject.AddComponent<Outline>();
			outline.effectColor = new Color(0.8f, 0.72f, 0.45f);
			outline.effectDistance = new Vector2(2f, 2f);
			outline.enabled = on;
		}
	}
}
