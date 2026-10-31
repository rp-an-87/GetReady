using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GetReady
{
	// One chip: a place in the "Bring to" row
	internal class Chip
	{
		public string Key;
		public string Label;
		public string Tip;
		public bool On = true;
	}

	internal static partial class ReadyWindow
	{
		private static readonly Dictionary<string, GUIStyle> ChipTexts = new Dictionary<string, GUIStyle>();

		private static GUIStyle ChipText(bool on)
		{
			string name = on ? "on" : "off";
			GUIStyle style;
			if (!ChipTexts.TryGetValue(name, out style) || style == null)
			{
				style = new GUIStyle(on ? Ui.Body : Ui.Muted) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip };
				ChipTexts[name] = style;
			}

			return style;
		}

		// The ">" between two places that are on: the order they are tried in
		private const float ChipArrow = 14f;

		// The room before the first place that is off, so the ones that are off stand apart from the ones that are on
		private const float ChipOffGap = 22f;

		// What goes where, as chips in the order of use: where the first one that has room wins. A ">" between the places that are on
		// shows the order. Click switches a chip off (a red cross, at the end, no ">" before it) or on again. Drag a chip onto another
		// chip: they swap places (and with them on or off). Only a chip is a target, nothing in between. onSwap gets the dragged key
		// and the key of the chip it was dropped on.
		private static bool Chips(string id, List<Chip> chips, float height, float width, int perRow, Action<string> onToggle, Action<string, string> onSwap)
		{
			bool changed = false;
			bool repaint = Event.current.type == EventType.Repaint;
			var rects = new List<Rect>();
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			for (int i = 0; i < chips.Count; i++)
			{
				if (i > 0 && i % perRow == 0)
				{
					GUILayout.EndHorizontal();
					GUILayout.Space(5);
					GUILayout.BeginHorizontal();
				}

				if (i % perRow > 0 && !chips[i].On && !chips[i - 1].On)
				{
					GUILayout.Space(5);
				}

				var chip = chips[i];
				if (i % perRow > 0 && chip.On && chips[i - 1].On)
				{
					var arrow = GUILayoutUtility.GetRect(ChipArrow, height, GUILayout.Width(ChipArrow), GUILayout.Height(height));
					if (repaint)
					{
						DrawChevron(arrow);
					}
				}

				if (i % perRow > 0 && !chip.On && chips[i - 1].On)
				{
					GUILayout.Space(ChipOffGap);
				}

				var style = chip.On ? Ui.PickOn : Ui.Pick;
				var rect = GUILayoutUtility.GetRect(GUIContent.none, style, GUILayout.Width(width), GUILayout.Height(height));
				rects.Add(rect);
				if (repaint)
				{
					var old = GUI.color;
					if (Dnd.IsDragging("chip", id, chip.Key))
					{
						GUI.color = new Color(1f, 1f, 1f, 0.4f);
					}

					bool hover = rect.Contains(Event.current.mousePosition) && !Dnd.Active;
					style.Draw(rect, GUIContent.none, hover, false, false, false);
					if (chip.On)
					{
						GUI.Label(rect, chip.Label, ChipText(true));
					}
					else
					{
						// A red cross before the name: it is not used
						GUI.Label(new Rect(rect.x + 18, rect.y, rect.width - 22, rect.height), chip.Label, ChipText(false));
						DrawCross(new Vector2(rect.x + 14, rect.center.y), 9f);
					}

					GUI.color = old;
				}

				GUI.Label(rect, new GUIContent("", chip.Tip));
				if (Dnd.Handle(rect, "chip", id, chip.Key, chip.Label))
				{
					onToggle(chip.Key);
					changed = true;
				}
			}

			GUILayout.EndHorizontal();
			GUILayout.EndVertical();

			string key;
			Vector2 pos;
			if (Dnd.TakeDrop("chip", id, out key, out pos))
			{
				int under = rects.FindIndex(rect => rect.Contains(pos));
				int from = chips.FindIndex(chip => chip.Key == key);
				if (under >= 0 && from >= 0 && under != from)
				{
					onSwap(key, chips[under].Key);
					changed = true;
				}
			}

			if (Dnd.IsDragging("chip", id) && repaint)
			{
				// The chip it would swap with gets a gold frame
				var mouse = Event.current.mousePosition;
				int under = rects.FindIndex(rect => rect.Contains(mouse));
				if (under >= 0 && chips[under].Key != Dnd.Key)
				{
					Dnd.Frame(rects[under]);
				}
			}

			return changed;
		}
	}
}
