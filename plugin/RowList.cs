using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GetReady
{
	// The lists that are in an order of use, as rows: a grip at the left (drag a row by it), the picture and the name. What is switched
	// off is dim, struck out in red and at the end of the list.
	internal static partial class ReadyWindow
	{
		private static readonly Dictionary<string, float> FlowWidth = new Dictionary<string, float>();

		private static readonly Color StrikeRed = new Color(0.86f, 0.3f, 0.27f);

		private static Texture2D _crossTexture, _chevronTexture;

		// A thin line drawing made pixel by pixel: the points get full strength, the pixels beside them a little, so it is about
		// 1.5 px thick and not jagged
		private static Texture2D LineArt(int width, int height, IEnumerable<Vector2Int> points)
		{
			var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
			var pixels = new Color32[width * height];
			foreach (var point in points)
			{
				foreach (var side in new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0) })
				{
					int x = point.x + side.x;
					if (x >= 0 && x < width && pixels[point.y * width + x].a < 70)
					{
						pixels[point.y * width + x] = new Color32(255, 255, 255, 70);
					}
				}
			}

			foreach (var point in points)
			{
				pixels[point.y * width + point.x] = new Color32(255, 255, 255, 255);
			}

			texture.SetPixels32(pixels);
			texture.Apply();
			return texture;
		}

		// A small red cross, for something switched off
		private static void DrawCross(Vector2 center, float size)
		{
			if (_crossTexture == null)
			{
				_crossTexture = LineArt(9, 9, Enumerable.Range(0, 9).SelectMany(i => new[] { new Vector2Int(i, i), new Vector2Int(i, 8 - i) }).ToList());
			}

			var old = GUI.color;
			GUI.color = StrikeRed;
			GUI.DrawTexture(new Rect(Mathf.Round(center.x - 4.5f), Mathf.Round(center.y - 4.5f), 9, 9), _crossTexture);
			GUI.color = old;
		}

		// A small ">" between two places, showing the order
		private static void DrawChevron(Rect rect)
		{
			if (_chevronTexture == null)
			{
				_chevronTexture = LineArt(6, 12, Enumerable.Range(0, 12).Select(y => new Vector2Int(Math.Min(y, 11 - y), y)).ToList());
			}

			var old = GUI.color;
			GUI.color = new Color(0.72f, 0.74f, 0.7f);
			GUI.DrawTexture(new Rect(Mathf.Round(rect.center.x - 3), Mathf.Round(rect.center.y - 6), 6, 12), _chevronTexture);
			GUI.color = old;
		}

		// A red cross after the last label drawn (the name), for something switched off
		private static void StrikeLast(string text, GUIStyle style)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}

			var rect = GUILayoutUtility.GetLastRect();
			float textWidth = Math.Min(style.CalcSize(new GUIContent(text)).x - style.padding.horizontal, rect.width - 20);
			DrawCross(new Vector2(rect.x + style.padding.left + textWidth + 10, rect.center.y), 10f);
		}

		// The standard priority list of the Setup page (repair traders, the ammo rules): rows in the order of use, dragged by the grip
		// to change it. icon draws a picture before the name.
		private static bool PriorityList<T>(string title, List<T> items, Func<T, string> label, Action<T, Rect> icon, Action<T, int> move, string help = null)
		{
			bool changed = false;
			var keys = items.Select(item => label(item)).ToList();
			var rects = new List<Rect>();
			GUILayout.BeginVertical(Ui.Inner);
			if (help == null)
			{
				GUILayout.Label(title, Ui.Muted);
			}
			else
			{
				GUILayout.BeginHorizontal();
				HelpTitle(help, title, Ui.Muted, 26f);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}

			for (int i = 0; i < items.Count; i++)
			{
				GUILayout.BeginVertical();
				GUILayout.BeginHorizontal();
				Dnd.Grip(34, "item", title, keys[i], keys[i]);
				if (icon != null)
				{
					var rect = GUILayoutUtility.GetRect(34, 34, GUILayout.Width(34), GUILayout.Height(34));
					icon(items[i], rect);
				}

				GUILayout.Label(keys[i], Ui.Body, GUILayout.ExpandWidth(true), GUILayout.Height(34));
				LogAlignmentOnce(Dnd.LastGrip, GUILayoutUtility.GetLastRect());
				GUILayout.EndHorizontal();
				GUILayout.EndVertical();
				rects.Add(GUILayoutUtility.GetLastRect());
			}

			GUILayout.EndVertical();

			int from, to;
			if (Dnd.RowDrop(title, keys, rects, out from, out to))
			{
				move(items[from], to - from);
				changed = true;
			}

			Dnd.RowBar(title, rects);
			return changed;
		}

		private static bool _alignLogged;

		// A one-time measurement for the grip/text alignment: where the grip and the label are, and how the font sits in its line
		private static void LogAlignmentOnce(Rect grip, Rect label)
		{
			if (_alignLogged || Event.current.type != EventType.Repaint)
			{
				return;
			}

			_alignLogged = true;
			var font = Ui.Body.font;
			CharacterInfo info = default(CharacterInfo);
			bool ok = false;
			if (font != null)
			{
				font.RequestCharactersInTexture("H", Ui.Body.fontSize);
				ok = font.GetCharacterInfo('H', out info, Ui.Body.fontSize);
			}

			Plugin.Log.LogInfo($"Alignment: grip {grip}, label {label}, label padding {Ui.Body.padding}, margin {Ui.Body.margin}, size {Ui.Body.fontSize}, font {(font != null ? font.name : "none")} ascent {(font != null ? font.ascent : 0)} lineHeight {(font != null ? font.lineHeight : 0)} fontSize {(font != null ? font.fontSize : 0)}, H minY {(ok ? info.minY : 0)} maxY {(ok ? info.maxY : 0)}");
		}

		// The entries of a group in the order they are shown: the ones in use in their order, then the ones switched off
		internal static List<Entry> ShownEntries(GroupDef def)
		{
			var off = def.Setting.Off;
			var all = def.OrderedEntries();
			return all.Where(entry => !off.Contains(entry.Template)).Concat(all.Where(entry => off.Contains(entry.Template))).ToList();
		}

		// A row dragged from one place of a group's list to another: writes the order out in full
		internal static void ReorderEntries(GroupDef def, List<Entry> shown, int from, int to)
		{
			var order = shown.Select(entry => entry.Template).ToList();
			var key = order[from];
			order.RemoveAt(from);
			order.Insert(to, key);
			def.Setting.Order = order;
		}
	}
}
