using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GetReady
{
	// Rows of a list moved by dragging their grip: the dragged row dims, a bar shows where it would land, and on release the list is told
	// "from this index to that one" (both as the list is now), or nothing when it did not move.
	internal class ReorderGrip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		internal RectTransform Container;
		internal List<RectTransform> Rows;
		internal int Index;
		internal Action<int, int> OnMove;
		internal RectTransform Bar;

		private int _at = -1;

		public void OnBeginDrag(PointerEventData eventData)
		{
			_at = -1;
			Dim(Rows[Index], 0.45f);
		}

		public void OnDrag(PointerEventData eventData)
		{
			_at = Landing(eventData);
			if (_at < 0)
			{
				Bar.gameObject.SetActive(false);
				return;
			}

			var row = Rows[Mathf.Min(_at, Rows.Count - 1)];
			var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Container, row);
			Bar.gameObject.SetActive(true);
			Bar.SetAsLastSibling();
			Bar.sizeDelta = new Vector2(Container.rect.width, 3f);
			Bar.localPosition = new Vector3(Container.rect.center.x, _at < Rows.Count ? bounds.max.y : bounds.min.y, 0f);
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			Dim(Rows[Index], 1f);
			Bar.gameObject.SetActive(false);
			if (_at >= 0)
			{
				int to = _at > Index ? _at - 1 : _at;
				if (to != Index)
				{
					OnMove(Index, to);
				}
			}
		}

		// The place between rows the pointer is at: before the row it is in the upper half of, after the one it is in the lower half of
		private int Landing(PointerEventData eventData)
		{
			for (int i = 0; i < Rows.Count; i++)
			{
				Vector2 local;
				RectTransformUtility.ScreenPointToLocalPointInRectangle(Rows[i], eventData.position, eventData.pressEventCamera, out local);
				if (Rows[i].rect.Contains(local))
				{
					return local.y > Rows[i].rect.center.y ? i : i + 1;
				}

				if (i == 0 && local.y > Rows[i].rect.yMax)
				{
					return 0;
				}

				if (i == Rows.Count - 1 && local.y < Rows[i].rect.yMin)
				{
					return Rows.Count;
				}
			}

			return -1;
		}

		private static void Dim(RectTransform row, float alpha)
		{
			var group = row.GetComponent<CanvasGroup>() ?? row.gameObject.AddComponent<CanvasGroup>();
			group.alpha = alpha;
		}
	}

	internal static class UiReorder
	{
		// Makes the grip of each row (the child called "Grip", see UiKit.Grip) drag the row inside the container
		internal static void Attach(RectTransform container, List<RectTransform> rows, Action<int, int> onMove)
		{
			var bar = UiKit.Node("Drop Bar", container);
			bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
			bar.gameObject.AddComponent<Image>().color = new Color(0.8f, 0.72f, 0.45f);
			bar.GetComponent<Image>().raycastTarget = false;
			bar.gameObject.SetActive(false);
			for (int i = 0; i < rows.Count; i++)
			{
				var grip = rows[i].Find("Grip");
				if (grip == null)
				{
					continue;
				}

				var handle = grip.gameObject.AddComponent<ReorderGrip>();
				handle.Container = container;
				handle.Rows = rows;
				handle.Index = i;
				handle.OnMove = onMove;
				handle.Bar = bar;
			}
		}
	}
}
