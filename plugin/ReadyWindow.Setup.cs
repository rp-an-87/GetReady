using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The Setup page: the switch, template and settings of each step.
	internal static partial class ReadyWindow
	{
		// Groups whose item list is open
		private static readonly HashSet<string> Expanded = new HashSet<string>();

		private static string PlaceLabel(string place) => place == "Special slots" ? "Special" : place;

		// A supply group as a card: the item's icon, the name, the amount with - and +, where it goes (click the places in the
		// order you want them tried) and, opened with "Items", which items count and in what order
		private static bool GroupRow(GroupDef def)
		{
			var setting = def.Setting;
			var active = def.ActiveEntries();
			bool changed = false;
			bool on = setting.Target > 0;

			GUILayout.BeginVertical(Ui.Group);
			GUILayout.BeginHorizontal();
			if (def.Entries.Length > 0)
			{
				var shown = active.FirstOrDefault() ?? def.Entries[0];
				Icons.Slot(shown.Template, shown.Name, 44);
			}

			GUILayout.BeginVertical();
			GUILayout.Label(def.Name, on ? Ui.Name : Ui.Muted);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("-", Ui.Small) && setting.Target > 0)
			{
				setting.Target = Math.Max(0, setting.Target - def.Step);
				changed = true;
			}

			if (def.Money)
			{
				// Typed in: the amounts are too big to click up to
				string typed = GUILayout.TextField(setting.Target.ToString(), 8, Ui.Field, GUILayout.Width(100));
				int parsed;
				if (typed != setting.Target.ToString() && int.TryParse(typed == "" ? "0" : typed, out parsed))
				{
					setting.Target = Math.Max(0, Math.Min(def.Max, parsed));
					changed = true;
				}

				GUILayout.Label(def.Unit, Ui.Muted, GUILayout.Width(40));
			}
			else
			{
				GUILayout.Label(on ? $"{setting.Target} {def.Unit}" : "off", on ? Ui.Centered : Ui.Muted, GUILayout.Width(150));
			}

			if (GUILayout.Button("+", Ui.Small) && setting.Target < def.Max)
			{
				setting.Target = Math.Min(def.Max, setting.Target + def.Step);
				changed = true;
			}

			GUILayout.EndHorizontal();
			GUILayout.EndVertical();

			GUILayout.FlexibleSpace();
			if (def.Entries.Length > 1)
			{
				bool open = Expanded.Contains(def.Id);
				if (GUILayout.Button(new GUIContent(open ? "Items  ▲" : "Items  ▼", "Which items count for this and in what order they are used"), open ? Ui.PickOn : Ui.Pick, GUILayout.Width(86)))
				{
					if (open)
					{
						Expanded.Remove(def.Id);
					}
					else
					{
						Expanded.Add(def.Id);
					}
				}
			}

			GUILayout.EndHorizontal();

			if (on && !def.IsQuota)
			{
				GUILayout.Space(10);
				GUILayout.BeginHorizontal();
				GUILayout.Label(new GUIContent("Goes to", def.NoFallback ? "The places you click are tried in order. Nothing else is used: if they are full or missing, you get a warning." : "The places you click are tried in order. If they are all full, the pockets, vest and backpack are tried after them."), Ui.Muted, GUILayout.Width(52));
				int shownPlaces = 0;
				foreach (var place in def.PickablePlaces)
				{
					// Five to a row
					if (shownPlaces > 0 && shownPlaces % 5 == 0)
					{
						GUILayout.EndHorizontal();
						GUILayout.BeginHorizontal();
						GUILayout.Space(56);
					}

					shownPlaces++;
					int rank = setting.Destinations.IndexOf(place);
					string label = rank >= 0 ? $"{rank + 1}. {PlaceLabel(place)}" : PlaceLabel(place);
					if (GUILayout.Button(new GUIContent(label, "Click to use this place. They are tried in the order you click them; click again to drop one."), rank >= 0 ? Ui.PickOn : Ui.Pick, GUILayout.Width(90)))
					{
						setting.ToggleDestination(place);
						changed = true;
					}
				}

				GUILayout.EndHorizontal();
			}

			if (def.Entries.Length > 1 && Expanded.Contains(def.Id))
			{
				GUILayout.BeginVertical(Ui.Inner);
				GUILayout.Label(def.Random ? "Used, in any order" : "Used, best first", Ui.Muted);
				var entries = def.OrderedEntries();
				for (int i = 0; i < entries.Count; i++)
				{
					var entry = entries[i];
					bool off = setting.Off.Contains(entry.Template);
					GUILayout.BeginHorizontal();
					Icons.Slot(entry.Template, entry.Name, 30);
					if (!def.Random)
					{
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
					}

					GUILayout.Label(def.Random ? entry.Name : $"{i + 1}. {entry.Name}", off ? Ui.Muted : Ui.Body);
					if (GUILayout.Button(off ? "Not used" : "Used", off ? Ui.Pick : Ui.PickOn, GUILayout.Width(90)))
					{
						def.ToggleEntry(entry.Template);
						changed = true;
					}

					GUILayout.EndHorizontal();
				}

				GUILayout.EndVertical();
			}

			GUILayout.EndVertical();
			return changed;
		}

		// The saved template as a row of item icons (hover for the name)
		private static void TemplateStrip(IEnumerable<KeyValuePair<string, string>> items, string empty)
		{
			var list = items.ToList();
			if (list.Count == 0)
			{
				GUILayout.Label(empty, Ui.Muted);
				return;
			}

			GUILayout.BeginHorizontal();
			foreach (var item in list)
			{
				Icons.Slot(item.Key, item.Value, 40);
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		// A step as a section: its switch and status tag, then its template or settings, then what it is about to do
		private static bool StepSection(IStep step, InventoryController controller)
		{
			bool changed = false;
			GUILayout.BeginVertical(Ui.Section);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(step.Enabled ? "ON" : "OFF", step.Enabled ? Ui.SwitchOn : Ui.SwitchOff))
			{
				step.Enabled = !step.Enabled;
				changed = true;
			}

			GUILayout.Label(step.Name, Ui.SectionTitle);
			GUILayout.EndHorizontal();

			if (step.Enabled)
			{
				GUILayout.Box(GUIContent.none, Ui.Divider);
				if (step is SpecialSlotsStep)
				{
					var slots = SettingsStore.Current.SpecialSlots.Slots;
					TemplateStrip(slots.Select(entry => new KeyValuePair<string, string>(entry.Templates[0].Template, $"{entry.SlotId.Replace("SpecialSlot", "Slot ")}: {entry.Templates[0].Name}" + (entry.Count > 1 ? $" x{entry.Count}" : ""))), "Nothing saved yet. Put what you want in the special slots, then save them.");
					if (controller != null && GUILayout.Button(new GUIContent("Save my current special slots as the template", "Remembers what is in each special slot of your pockets right now"), Ui.Button))
					{
						_status = $"Saved {SpecialSlotsStep.Capture(controller)} special slot(s).";
						RefreshSoon();
					}
				}
				else if (step is InjectorCaseStep)
				{
					var cells = SettingsStore.Current.InjectorCase.Cells;
					TemplateStrip(cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).Select(cell => new KeyValuePair<string, string>(cell.Template, cell.Name)), "Nothing saved yet. Fill the case the way you like it, then save it.");
					if (controller != null)
					{
						var cases = InjectorCaseStep.FindCases(controller.Inventory);
						if (cases.Count == 0)
						{
							GUILayout.Label("No injector case on you (pockets, vest, backpack or pouch).", Ui.Muted);
						}

						for (int i = 0; i < cases.Count; i++)
						{
							var injectorCase = cases[i];
							bool bound = injectorCase.Id.ToString() == SettingsStore.Current.InjectorCase.CaseId;
							string name = cases.Count == 1 ? "Your injector case" : $"Injector case {i + 1}";
							GUILayout.BeginHorizontal();
							GUILayout.Label($"{name}  -  {injectorCase.Grids[0].Items.Count()} injectors inside" + (bound ? "  -  this is the saved one" : ""), bound ? Ui.Name : Ui.Body);
							if (GUILayout.Button(new GUIContent(bound ? "Update template" : "Use as template", "Remembers which injector is in each cell of this case. Only this case is filled later; other injector cases are left alone."), Ui.Button, GUILayout.Width(150)))
							{
								_status = $"Saved {InjectorCaseStep.Capture(injectorCase)} injector(s) from {name.ToLowerInvariant()}.";
								RefreshSoon();
							}

							GUILayout.EndHorizontal();
						}
					}
				}
				foreach (var def in step.Groups)
				{
					changed |= GroupRow(def);
				}

			}

			GUILayout.EndVertical();
			return changed;
		}

		// A switch with its explanation on one row
		private static bool OptionRow(bool value, string text, string tooltip)
		{
			GUILayout.BeginHorizontal();
			bool flip = GUILayout.Button(value ? "ON" : "OFF", value ? Ui.SwitchOn : Ui.SwitchOff);
			GUILayout.Label(new GUIContent(text, tooltip), Ui.Body);
			GUILayout.EndHorizontal();
			return flip ? !value : value;
		}

		// The Setup page: what to bring and where, per step
		private static bool SetupPage(InventoryController controller)
		{
			var supplies = SettingsStore.Current.Supplies;
			bool changed = false;
			_scroll = GUILayout.BeginScrollView(_scroll);
			foreach (var step in Steps.All)
			{
				changed |= StepSection(step, controller);
			}

			GUILayout.BeginVertical(Ui.Section);
			GUILayout.Label("Options", Ui.SectionTitle);
			GUILayout.Box(GUIContent.none, Ui.Divider);
			bool swap = OptionRow(supplies.SwapWorse, "Replace worse kits with better ones from the stash", "A used or lower-priority kit you carry is swapped for a better one");
			bool overshoot = OptionRow(supplies.AllowOvershoot, "Take a bigger item when only a little is missing", "1 point short and the next item is an Army bandage (2): take it anyway");
			GUILayout.EndVertical();
			if (swap != supplies.SwapWorse || overshoot != supplies.AllowOvershoot)
			{
				supplies.SwapWorse = swap;
				supplies.AllowOvershoot = overshoot;
				changed = true;
			}

			GUILayout.EndScrollView();
			return changed;
		}
	}
}
