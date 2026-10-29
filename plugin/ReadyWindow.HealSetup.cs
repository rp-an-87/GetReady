using System.Linq;
using UnityEngine;

namespace GetReady
{
	// The Setup page of the Heal me and Full auto steps.
	internal static partial class ReadyWindow
	{
		// The medicine that may be used, best first. "Only" switches every other one off.
		private static bool HealMeSetup()
		{
			bool changed = false;
			var def = HealMeStep.Items;
			var setting = def.Setting;
			GUILayout.Label("Uses the medicine in the stash on whatever is hurt, first in the list first, until you are healed. Switch off what you don't want used. Runs last, so what Get ready just brought is left alone.", Ui.Muted);
			var entries = def.OrderedEntries();
			for (int i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];
				bool off = setting.Off.Contains(entry.Template);
				GUILayout.BeginHorizontal();
				Icons.Slot(entry.Template, entry.Name, 30);
				GUI.enabled = i > 0;
				if (GUILayout.Button("▲", Ui.Small))
				{
					def.MoveEntry(entry.Template, -1);
					changed = true;
				}

				GUI.enabled = i < entries.Count - 1;
				if (GUILayout.Button("▼", Ui.Small))
				{
					def.MoveEntry(entry.Template, 1);
					changed = true;
				}

				GUI.enabled = true;
				GUILayout.Label($"{i + 1}. {entry.Name}", off ? Ui.Muted : Ui.Body);
				if (GUILayout.Button(new GUIContent("Only", "Use only this one: every other item is switched off"), Ui.Pick, GUILayout.Width(60)))
				{
					setting.Off = entries.Where(other => other.Template != entry.Template).Select(other => other.Template).ToList();
					changed = true;
				}

				if (GUILayout.Button(off ? "Not used" : "Used", off ? Ui.Pick : Ui.PickOn, GUILayout.Width(90)))
				{
					def.ToggleEntry(entry.Template);
					changed = true;
				}

				GUILayout.EndHorizontal();
			}

			return changed;
		}

		private static bool FullAutoSetup()
		{
			bool changed = false;
			GUILayout.Label("Puts the weapon in each picked slot on full auto, if it has that mode.", Ui.Muted);
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
