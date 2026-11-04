using System;
using System.Collections.Generic;
using EFT.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GetReady
{
	// A chip that is clicked to switch (a normal button click) and dragged onto another chip to swap places with it. The hand cursor shows
	// it can be dragged. While it is dragged, the chips it can be dropped on swap their edge for a 1 px dotted one and the one it would swap
	// with gets a solid gold one. It does not have to be dropped exactly on a chip: near it is enough. The tooltips stay quiet meanwhile.
	internal class SwapChip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
	{
		internal string Key;
		internal List<SwapChip> Peers;
		internal Action<string, string> OnSwap;
		internal GameObject Dots;

		private const float Reach = 14f;
		private static readonly Color Gold = new Color(0.8f, 0.72f, 0.45f);

		private SwapChip _target;
		private bool _over, _dragging;
		private int _dragEndFrame = -1;

		// The chip's own edge, put back when the drag is over
		private Outline _edge;
		private Color _edgeColor;
		private Vector2 _edgeDistance;
		private bool _hadEdge, _captured;

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
			UiKit.HideTips(true);
			CursorSwitcher.SetCursor(ECursorType.Hand);
			Dim(0.5f);
			foreach (var peer in Peers)
			{
				if (peer != null && peer != this)
				{
					peer.Show(State.Droppable);
				}
			}
		}

		public void OnDrag(PointerEventData eventData)
		{
			var next = Nearest(eventData);
			if (next != _target)
			{
				_target?.Show(State.Droppable);
				_target = next;
				_target?.Show(State.Target);
			}
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			_dragging = false;
			_dragEndFrame = Time.frameCount;
			UiKit.HideTips(false);
			CursorSwitcher.SetCursor(_over ? ECursorType.Hand : ECursorType.Idle);
			Dim(1f);
			foreach (var peer in Peers)
			{
				if (peer != null)
				{
					peer.Show(State.Normal);
				}
			}

			var target = _target;
			_target = null;
			if (target != null)
			{
				OnSwap(Key, target.Key);
			}
		}

		private enum State { Normal, Droppable, Target }

		// Normal: its own edge. Droppable: a dotted edge in place of it. Target: a solid gold edge.
		private void Show(State state)
		{
			if (!_captured)
			{
				_captured = true;
				_edge = GetComponent<Outline>();
				_hadEdge = _edge != null;
				if (_edge == null)
				{
					_edge = gameObject.AddComponent<Outline>();
					_edge.effectColor = new Color(0.3f, 0.31f, 0.3f);
					_edge.effectDistance = new Vector2(1f, 1f);
				}

				_edgeColor = _edge.effectColor;
				_edgeDistance = _edge.effectDistance;
			}

			if (Dots != null)
			{
				Dots.SetActive(state == State.Droppable);
			}

			switch (state)
			{
				case State.Normal:
					_edge.enabled = _hadEdge;
					_edge.effectColor = _edgeColor;
					_edge.effectDistance = _edgeDistance;
					break;
				case State.Droppable:
					_edge.enabled = false;
					break;
				default:
					_edge.enabled = true;
					_edge.effectColor = Gold;
					_edge.effectDistance = new Vector2(1f, 1f);
					break;
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
	}
}
