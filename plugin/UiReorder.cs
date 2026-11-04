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
			// Only where the row would end up somewhere else: no bar for the place it is already in
			if (_at < 0 || (_at > Index ? _at - 1 : _at) == Index)
			{
				_at = -1;
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
				OnMove(Index, _at > Index ? _at - 1 : _at);
			}
		}

		// The place between rows nearest the pointer, by how high it is: it does not have to be over a row, or even over the list
		private int Landing(PointerEventData eventData)
		{
			Vector2 local;
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Container, eventData.position, eventData.pressEventCamera, out local))
			{
				return -1;
			}

			int above = 0;
			foreach (var row in Rows)
			{
				if (RectTransformUtility.CalculateRelativeRectTransformBounds(Container, row).center.y > local.y)
				{
					above++;
				}
			}

			return above;
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
