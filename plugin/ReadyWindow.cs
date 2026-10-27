using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT.Communications;
using EFT.UI;
using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GetReady
{
	// The window: a card per step with its settings and what it is about to do, and the Get ready button. IMGUI with a
	// custom dark look (Ui), which needs no assets. Also the big alert that a quick run leaves behind when something is wrong.
	internal static class ReadyWindow
	{
		private const int WindowId = 0x6e7264;
		private const int AlertId = 0x6e7265;

		private static bool _open;
		private static bool _closeRequested;
		private static Rect _rect = new Rect(80, 40, 740, 800);
		private static Vector2 _scroll;
		private static readonly List<KeyValuePair<IStep, List<PlanLine>>> Plan = new List<KeyValuePair<IStep, List<PlanLine>>>();
		private static string _status = "";
		private static bool _running;
		private static int _moved;

		// What a quick run found wrong, shown in the middle of the screen until it is dismissed
		private static readonly List<KeyValuePair<string, PlanLine>> Alerts = new List<KeyValuePair<string, PlanLine>>();
		private static Rect _alertRect;

		// The plan is read again shortly after the inventory changes (an item moved in the stash, say)
		private static InventoryController _watched;
		private static bool _dirty;
		private static float _dirtyAt;

		internal static void Toggle()
		{
			_open = !_open;
			if (_open)
			{
				Refresh();
			}
		}

		private static EventSystem _blocked;

		// The game's own UI (stash scrolling, clicks) listens to the EventSystem and knows nothing of this IMGUI window, so
		// it is switched off while the mouse is over the window, and back on when the mouse leaves
		private static void BlockGameInput()
		{
			var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
			bool over = (_open && _rect.Contains(mouse)) || (Alerts.Count > 0 && _alertRect.Contains(mouse));
			if (over == (_blocked != null))
			{
				return;
			}

			if (over)
			{
				_blocked = EventSystem.current;
				if (_blocked != null)
				{
					_blocked.enabled = false;
				}
			}
			else
			{
				_blocked.enabled = true;
				_blocked = null;
			}
		}

		private static bool _wasShown;

		private static void Close()
		{
			_open = false;
		}

		internal static void Tick()
		{
			// Esc closes it (and the alert), and so does leaving the stash screen
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				Alerts.Clear();
				if (_open)
				{
					Close();
				}
			}

			bool shown = ReadyButtonPatch.StashShown;
			if (_open && !shown)
			{
				Close();
			}

			// The status is kept up to date while the stash is up, window or not (the button shows it)
			if (shown && !_wasShown)
			{
				MarkDirty();
			}

			_wasShown = shown;
			if (!shown && _watched != null)
			{
				Watch(null);
			}

			BlockGameInput();
			if (ReadyButtonPatch.StashShown && _dirty && !_running && Time.unscaledTime - _dirtyAt > 0.3f)
			{
				Refresh();
			}
		}

		private static void Watch(InventoryController controller)
		{
			if (_watched == controller)
			{
				return;
			}

			if (_watched != null)
			{
				_watched.AddItemEvent -= OnItemAdded;
				_watched.RemoveItemEvent -= OnItemRemoved;
			}

			_watched = controller;
			if (controller != null)
			{
				controller.AddItemEvent += OnItemAdded;
				controller.RemoveItemEvent += OnItemRemoved;
			}
		}

		private static void OnItemAdded(AddItemEventArgs args) => MarkDirty();

		private static void OnItemRemoved(RemoveItemEventArgs args) => MarkDirty();

		private static void MarkDirty()
		{
			_dirty = true;
			_dirtyAt = Time.unscaledTime;
		}

		internal static void Draw()
		{
			Ui.Init();
			if (_open)
			{
				_rect = GUILayout.Window(WindowId, _rect, Contents, GUIContent.none, Ui.Window);
			}

			if (Alerts.Count > 0)
			{
				_alertRect = GUILayout.Window(AlertId, _alertRect, AlertContents, GUIContent.none, Ui.Alert);
				GUI.BringWindowToFront(AlertId);
			}
		}

		private static bool _loggedFirst;

		private static void Refresh()
		{
			Plan.Clear();
			_dirty = false;
			var controller = Game.Controller();
			Watch(controller);
			if (controller == null)
			{
				_status = "No stash here: open this in the hideout or the main menu.";
				ReadyButtonPatch.SetBadge(0, 0);
				return;
			}

			Steps.NewPass();
			if (!_loggedFirst)
			{
				Plugin.Log.LogInfo("Indexing the inventory");
			}

			Icons.Index(controller);
			foreach (var step in Steps.All.Where(step => step.Enabled))
			{
				if (!_loggedFirst)
				{
					Plugin.Log.LogInfo($"Planning {step.Name}");
				}

				try
				{
					Plan.Add(new KeyValuePair<IStep, List<PlanLine>>(step, step.Plan(controller)));
				}
				catch (Exception e)
				{
					Plugin.Log.LogError($"Plan of {step.Name} failed: {e}");
					Plan.Add(new KeyValuePair<IStep, List<PlanLine>>(step, new List<PlanLine> { Steps.Problem("Could not read the inventory, see the log.") }));
				}
			}

			_loggedFirst = true;
			UpdateBadge();
		}

		// The count on the stash button, and the text of its tooltip
		private static void UpdateBadge()
		{
			int toBring, withProblems;
			Tally(out toBring, out withProblems);
			ReadyButtonPatch.SetBadge(withProblems, toBring);
		}

		// How many groups (a supply, a template: a card of the Ready page) have something to bring, and how many have a problem.
		// It counts groups, not items: "bring 3 grenades" is one thing to get ready, not one per grenade type.
		private static void Tally(out int toBring, out int withProblems)
		{
			toBring = 0;
			withProblems = 0;
			foreach (var entry in Plan)
			{
				bool run = false, problem = false, open = false;
				foreach (var line in entry.Value)
				{
					if (line.IsProgress)
					{
						if (open)
						{
							toBring += run ? 1 : 0;
							withProblems += problem ? 1 : 0;
						}

						open = true;
						run = false;
						problem = false;
						continue;
					}

					if (!open)
					{
						toBring += line.Run != null ? 1 : 0;
						withProblems += line.Problem ? 1 : 0;
						continue;
					}

					run |= line.Run != null;
					problem |= line.Problem;
				}

				if (open)
				{
					toBring += run ? 1 : 0;
					withProblems += problem ? 1 : 0;
				}
			}
		}

		internal static string Summary()
		{
			int moves, problemGroups;
			Tally(out moves, out problemGroups);
			var problems = Problems();
			var text = new System.Text.StringBuilder("Get ready for raid\nClick: bring what is missing. Ctrl+click: open the window.");
			if (moves == 0 && problemGroups == 0)
			{
				text.Append("\n\nYou are all set.");
			}

			if (moves > 0)
			{
				text.Append($"\n\n{moves} to bring from the stash");
			}

			foreach (var problem in problems.Take(6))
			{
				text.Append($"\n! {problem.Value.Subject ?? problem.Value.Text}" + (problem.Value.Detail != null ? ": " + problem.Value.Detail : ""));
			}

			return text.ToString();
		}

		// Redraw with a fresh plan on the next frame (never in the middle of drawing)
		private static void RefreshSoon()
		{
			_dirty = true;
			_dirtyAt = 0f;
		}

		// The window's button: close the window and do it like the stash button does (alert and sound included)
		private static void Go()
		{
			_closeRequested = true;
			QuickRun();
		}

		// What could not be moved in the last run, shown with the other problems
		private static readonly List<KeyValuePair<string, PlanLine>> Failures = new List<KeyValuePair<string, PlanLine>>();

		private static void Play(EUISoundType sound)
		{
			try
			{
				Singleton<GUISounds>.Instance?.PlayUISound(sound);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not play the sound: {e.Message}");
			}
		}

		// What is wrong right now, one line each: the problems of every step in the plan
		private static List<KeyValuePair<string, PlanLine>> Problems() =>
			Plan.SelectMany(entry => entry.Value.Where(line => line.Problem).Select(line => new KeyValuePair<string, PlanLine>(entry.Key.Name, line))).ToList();

		// The button's plain click: look at the gear, bring what is missing and say what happened, without the window
		internal static async void QuickRun()
		{
			if (_running)
			{
				return;
			}

			Refresh();
			await RunPlan();
			var problems = Failures.Concat(Problems()).ToList();

			// The red box says what is wrong, so a notification is only for when all went well
			if (problems.Count == 0)
			{
				NotificationManager.DisplayMessageNotification(_moved > 0 ? $"Ready for raid: {_moved} item(s) brought." : "Ready for raid: you already have everything.");
			}

			Play(problems.Count > 0 ? EUISoundType.ErrorMessage : EUISoundType.QuestSubTrackComplete);
			if (problems.Count > 0)
			{
				Alerts.Clear();
				Alerts.AddRange(problems);
				_alertRect = new Rect((Screen.width - 640) / 2f, Screen.height * 0.18f, 640, 10);
			}
		}

		private static async System.Threading.Tasks.Task RunPlan()
		{
			if (_running)
			{
				return;
			}

			_running = true;
			try
			{
				int done = 0, failed = 0;
				Failures.Clear();
				foreach (var entry in Plan.ToList())
				{
					foreach (var line in entry.Value.Where(line => line.Run != null))
					{
						var error = await line.Run();
						if (error == null)
						{
							done += line.Items;
						}
						else
						{
							failed++;
							Plugin.Log.LogWarning($"{entry.Key.Name}: {line.Text} failed: {error}");
							Failures.Add(new KeyValuePair<string, PlanLine>(entry.Key.Name, new PlanLine { Problem = true, Subject = line.Text, Detail = "Could not move it: " + error, Template = line.Template }));
						}
					}
				}

				_moved = done;
				_status = failed == 0 ? $"Done: {done} moved." : $"{done} moved, {failed} failed (see the log).";
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Get ready failed: {e}");
				_status = "Failed, see the log.";
			}
			finally
			{
				_running = false;
				Refresh();
			}
		}

		private static void AlertContents(int id)
		{
			GUILayout.Label("Check this before you leave", Ui.AlertTitle);
			GUILayout.Space(8);
			// Grouped by step, each problem with its item's icon, what it is about and what is wrong
			foreach (var step in Alerts.GroupBy(pair => pair.Key))
			{
				GUILayout.Label(step.Key, Ui.Name);
				foreach (var pair in step)
				{
					GUILayout.BeginHorizontal();
					if (!string.IsNullOrEmpty(pair.Value.Template))
					{
						Icons.Slot(pair.Value.Template, pair.Value.Subject ?? "", 46);
					}

					GUILayout.BeginVertical();
					GUILayout.Label(pair.Value.Subject ?? pair.Value.Text, Ui.AlertText);
					if (pair.Value.Detail != null)
					{
						GUILayout.Label(pair.Value.Detail, Ui.AlertDetail);
					}

					GUILayout.EndVertical();
					GUILayout.EndHorizontal();
					GUILayout.Space(6);
				}

				GUILayout.Space(4);
			}

			GUILayout.Space(12);
			if (GUILayout.Button("Got it", Ui.Primary))
			{
				Alerts.Clear();
			}

			GUI.DragWindow(new Rect(0, 0, 10000, 60));
		}

		// A small coloured tag on a section: what the step will do
		private static void Chip(IStep step, List<PlanLine> lines)
		{
			if (!step.Enabled || lines == null)
			{
				GUILayout.Label("Off", Ui.ChipOff);
				return;
			}

			int problems = lines.Count(line => line.Problem);
			int moves = lines.Count(line => line.Run != null);
			if (problems > 0)
			{
				GUILayout.Label(problems == 1 ? "Needs attention" : $"{problems} problems", Ui.ChipBad);
			}
			else if (moves > 0)
			{
				GUILayout.Label($"{moves} to do", Ui.ChipTodo);
			}
			else
			{
				GUILayout.Label("Ready", Ui.ChipOk);
			}
		}

		// Groups whose item list is open
		private static readonly HashSet<string> Expanded = new HashSet<string>();

		private static string PlaceLabel(string place) => place == "Special slots" ? "Special" : place;

		// A supply group as a card: the item's icon, the name, the amount with - and +, where it goes (click the places in the
		// order you want them tried) and, opened with "Items", which items count and in what order
		private static bool GroupRow(GroupDef def)
		{
			var setting = SuppliesStep.Setting(def);
			var active = SuppliesStep.ActiveEntries(def);
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
				GUILayout.Space(2);
				GUILayout.BeginHorizontal();
				GUILayout.Label(new GUIContent("Goes to", def.NoFallback ? "The places you click are tried in order. Nothing else is used: if they are full or missing, you get a warning." : "The places you click are tried in order. If they are all full, the pockets, vest and backpack are tried after them."), Ui.Muted, GUILayout.Width(52));
				int shownPlaces = 0;
				foreach (var place in def.Locations ?? SuppliesStep.Locations)
				{
					if ((place == "Pouch" || place == "Special slots") && !def.AllowPouch)
					{
						continue;
					}

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
						SuppliesStep.ToggleDestination(setting, place);
						changed = true;
					}
				}

				GUILayout.EndHorizontal();
			}

			if (def.Entries.Length > 1 && Expanded.Contains(def.Id))
			{
				GUILayout.BeginVertical(Ui.Inner);
				GUILayout.Label(def.Random ? "Used, in any order" : "Used, best first", Ui.Muted);
				var entries = SuppliesStep.OrderedEntries(def);
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
							SuppliesStep.Move(def, entry.Template, -1);
							changed = true;
						}

						GUI.enabled = i < entries.Count - 1;
						if (GUILayout.Button("▼", Ui.Small))
						{
							SuppliesStep.Move(def, entry.Template, 1);
							changed = true;
						}

						GUI.enabled = true;
					}

					GUILayout.Label(def.Random ? entry.Name : $"{i + 1}. {entry.Name}", off ? Ui.Muted : Ui.Body);
					if (GUILayout.Button(off ? "Not used" : "Used", off ? Ui.Pick : Ui.PickOn, GUILayout.Width(90)))
					{
						SuppliesStep.ToggleItem(def, entry.Template);
						changed = true;
					}

					GUILayout.EndHorizontal();
				}

				GUILayout.EndVertical();
			}

			GUILayout.EndVertical();
			return changed;
		}

		// What is wanted against what is there: icon, name, a bar and the numbers, green when complete, amber when partly, red when none
		private static void ProgressRow(PlanLine line)
		{
			bool done = line.Have >= line.Want;
			var color = done ? Ui.Ok : line.Have > 0 ? Ui.Warn : Ui.Bad;
			GUILayout.BeginHorizontal();
			if (!string.IsNullOrEmpty(line.Template))
			{
				Icons.Slot(line.Template, line.Subject, 34);
			}

			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			GUILayout.Label(line.Subject, Ui.Body);
			GUILayout.Label($"{line.Have:N0}/{line.Want:N0} {line.Unit}", done ? Ui.ValueOk : line.Have > 0 ? Ui.ValueWarn : Ui.ValueBad);
			GUILayout.EndHorizontal();
			var bar = GUILayoutUtility.GetRect(10, 6, GUILayout.ExpandWidth(true), GUILayout.Height(6));
			var previous = GUI.color;
			GUI.color = new Color(0.07f, 0.075f, 0.075f);
			GUI.DrawTexture(bar, Texture2D.whiteTexture);
			GUI.color = color;
			GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(line.Want > 0 ? line.Have / line.Want : 1f), bar.height), Texture2D.whiteTexture);
			GUI.color = previous;
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}

		// The lines of a step: each progress line opens a card, and what follows it (what will be brought, what is wrong) sits in it
		private static void PlanLines(List<PlanLine> lines)
		{
			bool open = false;
			foreach (var line in lines)
			{
				if (line.IsProgress)
				{
					if (open)
					{
						GUILayout.EndVertical();
					}

					GUILayout.BeginVertical(Ui.Group);
					open = true;
					ProgressRow(line);
					continue;
				}

				DetailLine(line, open);
			}

			if (open)
			{
				GUILayout.EndVertical();
			}
		}

		private static void DetailLine(PlanLine line, bool indent)
		{
			if (line.Problem)
			{
				GUILayout.BeginHorizontal(Ui.ProblemBox);
				if (!string.IsNullOrEmpty(line.Template))
				{
					Icons.Slot(line.Template, line.Subject ?? "", 34);
				}

				GUILayout.BeginVertical();
				GUILayout.Label(line.Subject ?? line.Text, Ui.Problem);
				if (line.Detail != null)
				{
					GUILayout.Label(line.Detail, Ui.Body);
				}

				GUILayout.EndVertical();
				GUILayout.EndHorizontal();
			}
			else if (line.Run != null)
			{
				GUILayout.BeginHorizontal();
				if (indent)
				{
					GUILayout.Space(44);
				}

				GUILayout.Label("+", Ui.Move, GUILayout.Width(12));
				GUILayout.Label(line.Text, Ui.Move);
				GUILayout.EndHorizontal();
			}
			else
			{
				GUILayout.Label(line.Text, Ui.Muted);
			}
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

		// Which page is showing: 0 = Ready (what is missing and the button), 1 = Setup (what to bring and where)
		private static int _tab;

		// The Ready page: one line of status on top, then what each step found
		private static void ReadyPage()
		{
			int moves, problemGroups;
			Tally(out moves, out problemGroups);
			if (Plan.Count == 0)
			{
				GUILayout.BeginVertical(Ui.Section);
				GUILayout.Label("Nothing is switched on. Turn something on in Setup.", Ui.Body);
				GUILayout.EndVertical();
			}
			else if (moves == 0 && problemGroups == 0)
			{
				GUILayout.BeginVertical(Ui.OkCard);
				GUILayout.Label("You are all set", Ui.SectionTitle);
				GUILayout.Label("Everything you asked for is already on you.", Ui.Body);
				GUILayout.EndVertical();
			}
			else
			{
				GUILayout.BeginVertical(problemGroups > 0 && moves == 0 ? Ui.AlertCard : Ui.TodoCard);
				GUILayout.Label(moves > 0 ? (moves == 1 ? "1 thing to bring from the stash" : $"{moves} things to bring from the stash") : "Nothing to bring", Ui.SectionTitle);
				if (problemGroups > 0)
				{
					GUILayout.Label(problemGroups == 1 ? "1 thing needs your attention" : $"{problemGroups} things need your attention", Ui.Problem);
				}

				GUILayout.EndVertical();
			}

			_scroll = GUILayout.BeginScrollView(_scroll);
			foreach (var step in Steps.All)
			{
				var lines = Plan.Where(entry => entry.Key == step).Select(entry => entry.Value).FirstOrDefault();
				GUILayout.BeginVertical(Ui.Section);
				GUILayout.BeginHorizontal();
				GUILayout.Label(step.Name, Ui.SectionTitle);
				GUILayout.FlexibleSpace();
				Chip(step, lines);
				GUILayout.EndHorizontal();
				if (step.Enabled && lines != null && lines.Count > 0)
				{
					GUILayout.Box(GUIContent.none, Ui.Divider);
					PlanLines(lines);
				}

				GUILayout.EndVertical();
			}

			GUILayout.EndScrollView();
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

		private static void Contents(int id)
		{
			var controller = Game.Controller();
			bool changed = false;

			GUILayout.BeginHorizontal();
			GUILayout.Label("Get ready for raid", Ui.Title);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("x", "Close"), Ui.Small))
			{
				_closeRequested = true;
			}

			GUILayout.EndHorizontal();
			GUILayout.Label("Clicking the stash button does everything on the Ready page at once. Ctrl+click opens this window.", Ui.Muted);
			GUILayout.Space(4);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Ready", _tab == 0 ? Ui.TabOn : Ui.Tab))
			{
				_tab = 0;
			}

			if (GUILayout.Button("Setup", _tab == 1 ? Ui.TabOn : Ui.Tab))
			{
				_tab = 1;
			}

			GUILayout.EndHorizontal();
			GUILayout.Box(GUIContent.none, Ui.Divider);

			if (_tab == 0)
			{
				ReadyPage();
			}
			else
			{
				changed = SetupPage(controller);
			}

			GUILayout.Box(GUIContent.none, Ui.Divider);
			GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? _status : GUI.tooltip, Ui.Muted);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUI.enabled = !_running && Plan.Any(entry => entry.Value.Any(line => line.Run != null));
			if (GUILayout.Button(new GUIContent("Get ready", "Moves the items marked + from the stash into your gear"), Ui.Primary))
			{
				Go();
			}

			GUI.enabled = true;
			GUILayout.EndHorizontal();

			if (changed)
			{
				SettingsStore.Save();
				RefreshSoon();
			}

			if (_closeRequested)
			{
				_closeRequested = false;
				_open = false;
			}

			GUI.DragWindow(new Rect(0, 0, 10000, 44));
		}
	}
}
