using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GetReady.ReadyPlan;
using static GetReady.ReadyRun;

namespace GetReady
{
	// The tab strip above the Setup page: one tab per section, always in view. A click scrolls the section to the top, and the tab of the
	// section at the top of the view is lit as you scroll.
	internal static partial class ReadyWindow
	{
		// Where each section starts inside the scrolled content (measured while drawing, so a frame behind), and the sizes needed to tell
		// which one is showing
		private static readonly List<float> SectionTops = new List<float>();
		private static float _setupContentHeight;
		private static float _setupViewHeight;

		// The section showing at the top of the view: the last one that has started, and the last one when the page is scrolled to the
		// end (a short last section can never reach the top)
		internal static int ActiveSection(IList<float> tops, float scrollY, float viewHeight, float contentHeight)
		{
			if (tops.Count == 0)
			{
				return -1;
			}

			if (contentHeight > viewHeight && scrollY + viewHeight >= contentHeight - 2)
			{
				return tops.Count - 1;
			}

			int active = 0;
			for (int i = 0; i < tops.Count; i++)
			{
				if (tops[i] <= scrollY + 12)
				{
					active = i;
				}
			}

			return active;
		}

		// How the tabs break into rows for a width; each row is the indexes it holds
		internal static List<List<int>> TabRows(IList<float> widths, float available)
		{
			var rows = new List<List<int>> { new List<int>() };
			float used = 0;
			for (int i = 0; i < widths.Count; i++)
			{
				if (rows[rows.Count - 1].Count > 0 && used + widths[i] > available)
				{
					rows.Add(new List<int>());
					used = 0;
				}

				rows[rows.Count - 1].Add(i);
				used += widths[i];
			}

			return rows;
		}

		// Setup has a scroll position of its own, and starts from the top each time it is shown (opening the window, or coming from the Ready page)
		private static Vector2 _setupScroll;

		private static void ResetSetup()
		{
			_setupScroll = Vector2.zero;
			SectionTops.Clear();
			_setupContentHeight = 0f;
			_setupViewHeight = 0f;
		}

		private const float JumpPad = 9f, JumpIcon = 18f, JumpGap = 5f;

		// The text of a tab: its button style without the background, so it can be drawn over the button
		private static readonly Dictionary<GUIStyle, GUIStyle> JumpLabels = new Dictionary<GUIStyle, GUIStyle>();

		private static GUIStyle LabelStyleOf(GUIStyle tab)
		{
			if (!JumpLabels.TryGetValue(tab, out var label) || label == null)
			{
				label = new GUIStyle(tab) { padding = new RectOffset(0, 0, 0, 1), fixedHeight = 0, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
				label.normal.background = null;
				label.hover.background = null;
				label.active.background = null;
				JumpLabels[tab] = label;
			}

			return label;
		}

		private static float TabWidth(string name, GUIStyle style) =>
			LabelStyleOf(style).CalcSize(new GUIContent(name)).x + 2 * JumpPad + (Icons.HasStepIcon(name) ? JumpIcon + JumpGap : 0) + style.margin.horizontal;

		private static void JumpStrip(List<KeyValuePair<string, bool>> sections)
		{
			int active = Mathf.Max(0, ActiveSection(SectionTops, _setupScroll.y, _setupViewHeight, _setupContentHeight));
			var styles = sections.Select((section, index) => index == active ? Ui.JumpTabOn : section.Value ? Ui.JumpTab : Ui.JumpTabOff).ToList();
			var widths = sections.Select((section, index) => TabWidth(section.Key, styles[index])).ToList();
			float available = _rect.width - Ui.Window.padding.horizontal - 8;
			foreach (var row in TabRows(widths, available))
			{
				GUILayout.BeginHorizontal();
				foreach (int index in row)
				{
					// The tab is a plain button; the icon of the section (the one its title has) and the name are drawn over it
					var rect = GUILayoutUtility.GetRect(widths[index] - styles[index].margin.horizontal, 22, styles[index], GUILayout.MinWidth(widths[index] - styles[index].margin.horizontal), GUILayout.ExpandWidth(true));
					if (GUI.Button(rect, GUIContent.none, styles[index]) && index < SectionTops.Count)
					{
						_setupScroll.y = SectionTops[index];
					}

					if (Event.current.type == EventType.Repaint)
					{
						// The row fills the width: each tab takes its share of what is left, and its icon and name are centred in it
						bool hasIcon = Icons.HasStepIcon(sections[index].Key);
						float textWidth = LabelStyleOf(styles[index]).CalcSize(new GUIContent(sections[index].Key)).x;
						float x = rect.x + (rect.width - (textWidth + (hasIcon ? JumpIcon + JumpGap : 0))) / 2f;
						if (hasIcon)
						{
							Icons.Step(new Rect(x, rect.y + 2, JumpIcon, JumpIcon), sections[index].Key);
							x += JumpIcon + JumpGap;
						}

						LabelStyleOf(styles[index]).Draw(new Rect(x, rect.y, textWidth + 2, rect.height), sections[index].Key, false, false, false, false);
					}
				}

				GUILayout.EndHorizontal();
			}

			GUILayout.Space(4);
		}

		// Called right after a section is drawn inside the scroll view: remembers where it starts
		private static void NoteSection(int index)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}

			var rect = GUILayoutUtility.GetLastRect();
			while (SectionTops.Count <= index)
			{
				SectionTops.Add(0);
			}

			SectionTops[index] = rect.y;
			_setupContentHeight = rect.yMax;
		}
	}
}
