using System;
using System.Collections.Generic;
using System.Linq;
using EFT.Communications;
using EFT.InventoryLogic;
using UnityEngine;

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
		private static Rect _rect = new Rect(80, 60, 700, 760);
		private static Vector2 _scroll;
		private static readonly List<KeyValuePair<IStep, List<PlanLine>>> Plan = new List<KeyValuePair<IStep, List<PlanLine>>>();
		private static string _status = "";
		private static bool _running;

		// What a quick run found wrong, shown in the middle of the screen until it is dismissed
		private static readonly List<string> Alerts = new List<string>();
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
			else
			{
				Watch(null);
			}
		}

		internal static void Tick()
		{
			if (_open && _dirty && !_running && Time.unscaledTime - _dirtyAt > 0.3f)
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

		private static void Refresh()
		{
			Plan.Clear();
			_dirty = false;
			var controller = Game.Controller();
			Watch(controller);
			if (controller == null)
			{
				_status = "No stash here: open this in the hideout or the main menu.";
				return;
			}

			Steps.Claimed.Clear();
			foreach (var step in Steps.All.Where(step => step.Enabled))
			{
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
		}

		// Redraw with a fresh plan on the next frame (never in the middle of drawing)
		private static void RefreshSoon()
		{
			_dirty = true;
			_dirtyAt = 0f;
		}

		private static async void Go()
		{
			await RunPlan();
		}

		// What is wrong right now, one line each: the problems of every step in the plan
		private static List<string> Problems() =>
			Plan.SelectMany(entry => entry.Value.Where(line => line.Problem).Select(line => entry.Key is MissingResourceWarning ? line.Text : $"{entry.Key.Name}: {line.Text}")).ToList();

		// The button's plain click: look at the gear, bring what is missing and say what happened, without the window
		internal static async void QuickRun()
		{
			if (_running)
			{
				return;
			}

			Refresh();
			await RunPlan();
			var problems = Problems();
			NotificationManager.DisplayMessageNotification("Get ready: " + _status.TrimEnd('.') + (problems.Count > 0 ? $". {problems.Count} thing(s) need attention!" : "."));
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
				foreach (var entry in Plan.ToList())
				{
					foreach (var line in entry.Value.Where(line => line.Run != null))
					{
						var error = await line.Run();
						if (error == null)
						{
							done++;
						}
						else
						{
							failed++;
							Plugin.Log.LogWarning($"{entry.Key.Name}: {line.Text} failed: {error}");
						}
					}
				}

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
			foreach (var text in Alerts)
			{
				GUILayout.Label("•  " + text, Ui.AlertText);
				GUILayout.Space(4);
			}

			GUILayout.Space(12);
			if (GUILayout.Button("Got it", Ui.Primary))
			{
				Alerts.Clear();
			}

			GUI.DragWindow(new Rect(0, 0, 10000, 60));
		}

		// A small coloured tag on a card: what the step will do
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

		// One row of a step: its name, the amount with - and +, and where it goes (click to change)
		private static bool GroupRow(GroupDef def)
		{
			var setting = SuppliesStep.Setting(def);
			bool changed = false;
			GUILayout.BeginHorizontal();
			GUILayout.Label(def.Name, setting.Target == 0 ? Ui.Plain : Ui.Body, GUILayout.Width(170));
			if (GUILayout.Button("-", Ui.Small) && setting.Target > 0)
			{
				setting.Target = Math.Max(0, setting.Target - def.Step);
				changed = true;
			}

			GUILayout.Label(setting.Target == 0 ? "off" : $"{setting.Target} {def.Unit}", setting.Target == 0 ? Ui.Plain : Ui.Body, GUILayout.Width(140));
			if (GUILayout.Button("+", Ui.Small) && setting.Target < def.Max)
			{
				setting.Target = Math.Min(def.Max, setting.Target + def.Step);
				changed = true;
			}

			if (GUILayout.Button(new GUIContent(SuppliesStep.DestinationLabel(setting), "Where it goes, first with room first. Click to change."), Ui.Button))
			{
				SuppliesStep.NextDestination(setting);
				changed = true;
			}

			GUILayout.EndHorizontal();
			return changed;
		}

		private static void PlanLines(List<PlanLine> lines)
		{
			foreach (var line in lines)
			{
				if (line.Problem)
				{
					GUILayout.Label("!  " + line.Text, Ui.Problem);
				}
				else if (line.Run != null)
				{
					GUILayout.Label("+  " + line.Text, Ui.Move);
				}
				else
				{
					GUILayout.Label(line.Text, Ui.Plain);
				}
			}
		}

		// A step as a card: switch and status tag, what is saved, its settings, then what it is about to do
		private static bool StepCard(IStep step, InventoryController controller)
		{
			bool changed = false;
			var lines = Plan.Where(entry => entry.Key == step).Select(entry => entry.Value).FirstOrDefault();
			GUILayout.BeginVertical(Ui.Card);
			GUILayout.BeginHorizontal();
			bool enabled = GUILayout.Toggle(step.Enabled, step.Name, Ui.Toggle);
			GUILayout.FlexibleSpace();
			Chip(step, lines);
			GUILayout.EndHorizontal();
			if (enabled != step.Enabled)
			{
				step.Enabled = enabled;
				changed = true;
			}

			if (step.Enabled)
			{
				GUILayout.Label(step.Saved(), Ui.Muted);
				if (step is SpecialSlotsStep && controller != null)
				{
					if (GUILayout.Button(new GUIContent("Save my current special slots as the template", "Remembers what is in each special slot of your pockets right now"), Ui.Button))
					{
						_status = $"Saved {SpecialSlotsStep.Capture(controller)} special slot(s).";
						RefreshSoon();
					}
				}
				else if (step is InjectorCaseStep && controller != null)
				{
					var cases = InjectorCaseStep.FindCases(controller.Inventory);
					if (cases.Count == 0)
					{
						GUILayout.Label("No injector case on you (pockets, vest, backpack or pouch).", Ui.Plain);
					}

					for (int i = 0; i < cases.Count; i++)
					{
						var injectorCase = cases[i];
						bool bound = injectorCase.Id.ToString() == SettingsStore.Current.InjectorCase.CaseId;
						string name = cases.Count == 1 ? "my injector case" : $"injector case {i + 1} ({injectorCase.Grids[0].Items.Count()} injectors inside)";
						string label = $"Save {name} as the template" + (bound ? " (this is the saved one)" : "");
						if (GUILayout.Button(new GUIContent(label, "Remembers which injector is in each cell. Only this case is filled later; other injector cases are left alone."), Ui.Button))
						{
							_status = $"Saved {InjectorCaseStep.Capture(injectorCase)} injector(s) from {name}.";
							RefreshSoon();
						}
					}
				}

				foreach (var def in step.Groups)
				{
					changed |= GroupRow(def);
				}

				if (lines != null && lines.Count > 0)
				{
					GUILayout.Space(4);
					PlanLines(lines);
				}
			}

			GUILayout.EndVertical();
			return changed;
		}

		private static void Contents(int id)
		{
			var controller = Game.Controller();
			var supplies = SettingsStore.Current.Supplies;
			bool changed = false;

			GUILayout.BeginHorizontal();
			GUILayout.Label("Get ready for raid", Ui.Title);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("x", "Close"), Ui.Small))
			{
				_closeRequested = true;
			}

			GUILayout.EndHorizontal();
			GUILayout.Label("Clicking the stash button does everything below at once. Ctrl+click opens this window.", Ui.Muted);
			GUILayout.Space(6);

			var problems = Problems();
			if (problems.Count > 0)
			{
				GUILayout.BeginVertical(Ui.AlertCard);
				GUILayout.Label(problems.Count == 1 ? "1 thing needs attention" : $"{problems.Count} things need attention", Ui.Problem);
				foreach (var text in problems)
				{
					GUILayout.Label("•  " + text, Ui.Body);
				}

				GUILayout.EndVertical();
			}

			_scroll = GUILayout.BeginScrollView(_scroll);
			foreach (var step in Steps.All)
			{
				changed |= StepCard(step, controller);
			}

			GUILayout.BeginVertical(Ui.Card);
			GUILayout.Label("Options", Ui.Toggle);
			bool swap = GUILayout.Toggle(supplies.SwapWorse, new GUIContent("Replace worse kits with better ones from the stash", "A used or lower-priority kit you carry is swapped for a better one"));
			bool overshoot = GUILayout.Toggle(supplies.AllowOvershoot, new GUIContent("Take a bigger item when only a little is missing", "1 point short and the next item is an Army bandage (2): take it anyway"));
			GUILayout.EndVertical();
			if (swap != supplies.SwapWorse || overshoot != supplies.AllowOvershoot)
			{
				supplies.SwapWorse = swap;
				supplies.AllowOvershoot = overshoot;
				changed = true;
			}

			GUILayout.EndScrollView();

			GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? _status : GUI.tooltip, Ui.Muted);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(new GUIContent("Check again", "Looks at your gear and stash again"), Ui.Button))
			{
				RefreshSoon();
			}

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
				Watch(null);
			}

			GUI.DragWindow(new Rect(0, 0, 10000, 44));
		}
	}
}
