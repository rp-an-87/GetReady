using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GetReady.ReadyPlan;
using static GetReady.ReadyRun;

namespace GetReady
{
	// The Setup page of the Heal me and Full auto steps.
	internal static partial class ReadyWindow
	{
		// Treat injuries: one list of the medicine in the order of use, in a panel of its own in the middle. What each item treats is
		// shown with the game's icons. Not used goes to the end of the list.
		private static bool HealMeSetup()
		{
			bool changed = false;
			var def = HealMeStep.Items;
			var troubles = HealMeStep.Troubles;
			GUILayout.BeginHorizontal();
			Help("The most urgent injury is treated with the first item of the list that treats it, until you are healed. Put the dedicated items before the kits and a kit is only left for what is still hurt (a kit treats everything it can on a part at once). Runs last, so what Get ready just brought is left alone. The icons on each row show what the item treats.");
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("Reset", "The usual order and the usual items on"), Ui.Pick, GUILayout.Width(Fit("Reset", Ui.Pick, 70))))
			{
				HealMeStep.ResetAll();
				changed = true;
			}

			GUILayout.EndHorizontal();
			GUILayout.Space(6);

			// Only the list is a panel: the darker box the other lists have, of a fixed width, in the middle
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.BeginVertical(Ui.Inner, GUILayout.Width(HealPanelWidth));
			var entries = ShownEntries(def);
			var rects = new List<Rect>();
			for (int i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];
				bool off = !HealMeStep.IsOn(entry.Template);
				GUILayout.BeginVertical();
				GUILayout.BeginHorizontal();
				Dnd.Grip(HealIcon, "item", "healme", entry.Template, entry.Name);
				Icons.Slot(entry.Template, entry.Name, HealIcon);
				GUILayout.Label(entry.Name, off ? Ui.Muted : Ui.Body, GUILayout.Width(HealName), GUILayout.Height(HealIcon));
				if (off)
				{
					StrikeLast(entry.Name, Ui.Muted);
				}

				foreach (var trouble in troubles)
				{
					// What it treats, in a fixed column each so the icons line up; nothing where it can't
					var slot = GUILayoutUtility.GetRect(HealTreatIcon + 4, HealIcon, GUILayout.Width(HealTreatIcon + 4), GUILayout.Height(HealIcon));
					if (HealMeStep.Treats(entry.Template, trouble.Id) && Event.current.type == EventType.Repaint)
					{
						var old = GUI.color;
						GUI.color = new Color(1f, 1f, 1f, off ? 0.4f : 1f);
						Icons.DrawEffect(new Rect(slot.x + 2, slot.y + (HealIcon - HealTreatIcon) / 2f, HealTreatIcon, HealTreatIcon), trouble.Icon, "Treats " + trouble.Name);
						GUI.color = old;
					}
				}

				GUILayout.FlexibleSpace();
				if (GUILayout.Button(off ? "Not used" : "Used", off ? Ui.PickRow : Ui.PickOnRow, GUILayout.Width(Fit("Not used", Ui.PickOnRow, HealButton)), GUILayout.Height(HealIcon)))
				{
					def.ToggleEntry(entry.Template);
					changed = true;
				}

				GUILayout.EndHorizontal();
				GUILayout.EndVertical();
				rects.Add(GUILayoutUtility.GetLastRect());
			}

			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			int from, to;
			if (Dnd.RowDrop("healme", entries.Select(entry => entry.Template).ToList(), rects, out from, out to))
			{
				ReorderEntries(def, entries, from, to);
				changed = true;
			}

			Dnd.RowBar("healme", rects);
			return changed;
		}

		private const float HealPanelWidth = 500f, HealIcon = 30f, HealTreatIcon = 22f, HealName = 130f, HealButton = 84f;

		private static bool FullAutoSetup()
		{
			bool changed = false;
			TitleWithHelp("Weapon slots to put on full auto", "Puts the weapon in each picked slot on full auto, if it has that mode.");
			foreach (var pair in FullAutoStep.Slots)
			{
				bool selected = FullAutoStep.IsSelected(pair.Key);
				if (OptionRow(selected, pair.Value, "Set the weapon in this slot to full auto") != selected)
				{
					FullAutoStep.Toggle(pair.Key);
					changed = true;
				}
			}

			return changed;
		}
	}
}
