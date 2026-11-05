using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GetReady
{
	// The picture that follows the pointer while a row or a chip of the window is dragged: a see-through copy of it, on top of everything and
	// out of the pointer's way. The dragged one itself stays where it is (dimmed by the caller) so you see where it comes from.
	internal static class DragGhost
	{
		private static GameObject _ghost;
		private static Vector3 _offset;

		internal static void Begin(RectTransform source, PointerEventData eventData)
		{
			End();
			try
			{
				var canvas = source.GetComponentInParent<Canvas>();
				var top = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
				if (top == null)
				{
					return;
				}

				_ghost = UnityEngine.Object.Instantiate(source.gameObject, top, false);
				_ghost.name = "GetReady drag ghost";
				// A copy that only looks like the original: none of the drag or tooltip behaviour comes along
				foreach (var behaviour in _ghost.GetComponentsInChildren<MonoBehaviour>(true))
				{
					if (behaviour is ReorderGrip || behaviour is SwapChip)
					{
						UnityEngine.Object.Destroy(behaviour);
					}
				}

				var rect = (RectTransform)_ghost.transform;
				rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
				rect.pivot = source.pivot;
				rect.sizeDelta = source.rect.size;
				rect.localScale = new Vector3(source.lossyScale.x / top.lossyScale.x, source.lossyScale.y / top.lossyScale.y, 1f);
				rect.SetAsLastSibling();
				// A row has no fill of its own (only text and pictures), which would float over the list: it gets a solid one and a thin edge
				var fill = _ghost.GetComponent<UnityEngine.UI.Image>();
				if (fill == null)
				{
					fill = _ghost.AddComponent<UnityEngine.UI.Image>();
					fill.color = new Color(0f, 0f, 0f, 0f);
				}

				if (fill.color.a < 0.05f)
				{
					fill.color = new Color(0.13f, 0.14f, 0.14f, 1f);
					var edge = _ghost.AddComponent<UnityEngine.UI.Outline>();
					edge.effectColor = new Color(0.8f, 0.72f, 0.45f, 0.7f);
					edge.effectDistance = new Vector2(1f, 1f);
				}

				var group = _ghost.GetComponent<CanvasGroup>() ?? _ghost.AddComponent<CanvasGroup>();
				group.alpha = 0.88f;
				group.blocksRaycasts = false;
				group.interactable = false;
				rect.position = source.position;
				_offset = source.position - Pointer(eventData);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Drag ghost: {e.Message}");
				End();
			}
		}

		internal static void Move(PointerEventData eventData)
		{
			if (_ghost != null)
			{
				_ghost.transform.position = Pointer(eventData) + _offset;
			}
		}

		internal static void End()
		{
			if (_ghost != null)
			{
				UnityEngine.Object.Destroy(_ghost);
				_ghost = null;
			}
		}

		// Where the pointer is, in the world of the window's canvas
		private static Vector3 Pointer(PointerEventData eventData)
		{
			var top = (RectTransform)_ghost.transform.parent;
			Vector3 world;
			return RectTransformUtility.ScreenPointToWorldPointInRectangle(top, eventData.position, eventData.pressEventCamera, out world) ? world : _ghost.transform.position;
		}
	}
}
