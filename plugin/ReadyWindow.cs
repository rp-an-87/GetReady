using System;
using System.Collections.Generic;
using System.Linq;
using EFT.Communications;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The window: what to get ready, capturing templates, the plan and the Go button. IMGUI for now, which needs no assets.
	internal static class ReadyWindow
	{
		private const int WindowId = 0x6e7264;

		private static bool _open;
		private static Rect _rect = new Rect(80, 80, 640, 680);
		private static Vector2 _scroll;
		private static readonly List<KeyValuePair<IStep, List<PlanLine>>> Plan = new List<KeyValuePair<IStep, List<PlanLine>>>();
		private static string _status = "";
		private static bool _running;

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
			if (_open)
			{
				_rect = GUILayout.Window(WindowId, _rect, Contents, "Get ready for raid");
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

		private static async void Go()
		{
			await RunPlan();
		}

		// The button's plain click: look at the gear, bring what is missing and say what happened, without the window
		internal static async void QuickRun()
		{
			if (_running)
			{
				return;
			}

			Refresh();
			await RunPlan();
			int problems = Plan.Sum(entry => entry.Value.Count(line => line.Problem));
			var message = "Get ready: " + _status.TrimEnd('.') + (problems > 0 ? $". {problems} thing(s) need a look, Ctrl+click the button to see." : ".");
			NotificationManager.DisplayMessageNotification(message);
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

		// One row of a step: its name, the amount with - and +, and where it goes (click to change)
		private static bool GroupRow(GroupDef def)
		{
			var setting = SuppliesStep.Setting(def);
			bool changed = false;
			GUILayout.BeginHorizontal();
			GUILayout.Label($"  {def.Name}", GUILayout.Width(170));
			if (GUILayout.Button("-", GUILayout.Width(28)) && setting.Target > 0)
			{
				setting.Target = Math.Max(0, setting.Target - def.Step);
				changed = true;
			}

			GUILayout.Label(setting.Target == 0 ? "off" : $"{setting.Target} {def.Unit}", GUILayout.Width(130));
			if (GUILayout.Button("+", GUILayout.Width(28)) && setting.Target < def.Max)
			{
				setting.Target = Math.Min(def.Max, setting.Target + def.Step);
				changed = true;
			}

			if (GUILayout.Button(new GUIContent(SuppliesStep.DestinationLabel(setting), "Where it goes, first with room first. Click to change.")))
			{
				SuppliesStep.NextDestination(setting);
				changed = true;
			}

			GUILayout.EndHorizontal();
			return changed;
		}

		// The step's switch and what is saved for it. True when the switch changed.
		private static bool StepHeader(IStep step)
		{
			bool enabled = GUILayout.Toggle(step.Enabled, step.Name);
			GUILayout.Label("  " + step.Saved());
			if (enabled == step.Enabled)
			{
				return false;
			}

			step.Enabled = enabled;
			return true;
		}

		private static void Contents(int id)
		{
			var controller = Game.Controller();
			var settings = SettingsStore.Current;
			bool changed = false;

			_scroll = GUILayout.BeginScrollView(_scroll);

			// One block per step: switch, what is saved, and the button that saves it again from what you wear now
			changed |= StepHeader(Steps.All[0]);
			if (controller != null && GUILayout.Button(new GUIContent("Save my current special slots as the template", "Remembers what is in each special slot of your pockets right now")))
			{
				_status = $"Saved {SpecialSlotsStep.Capture(controller)} special slot(s).";
				Refresh();
			}

			GUILayout.Space(10);
			changed |= StepHeader(Steps.All[1]);
			if (controller != null)
			{
				var cases = InjectorCaseStep.FindCases(controller.Inventory);
				if (cases.Count == 0)
				{
					GUILayout.Label("No injector case on you (pockets, vest, backpack or pouch).");
				}

				for (int i = 0; i < cases.Count; i++)
				{
					var injectorCase = cases[i];
					bool bound = injectorCase.Id.ToString() == settings.InjectorCase.CaseId;
					string name = cases.Count == 1 ? "my injector case" : $"injector case {i + 1} ({injectorCase.Grids[0].Items.Count()} injectors inside)";
					string label = $"Save {name} as the template" + (bound ? " (this is the saved one)" : "");
					if (GUILayout.Button(new GUIContent(label, "Remembers which injector is in each cell. Only this case is filled later; other injector cases are left alone.")))
					{
						_status = $"Saved {InjectorCaseStep.Capture(injectorCase)} injector(s) from {name}.";
						Refresh();
					}
				}
			}

			foreach (var supplies in Steps.All.Skip(2))
			{
				GUILayout.Space(10);
				changed |= StepHeader(supplies);
				foreach (var def in supplies.Groups)
				{
					changed |= GroupRow(def);
				}
			}

			GUILayout.Space(10);
			var supplySettings = settings.Supplies;
			bool swap = GUILayout.Toggle(supplySettings.SwapWorse, new GUIContent("Replace worse kits with better ones from the stash", "A used or lower-priority kit you carry is swapped for a better one"));
			bool overshoot = GUILayout.Toggle(supplySettings.AllowOvershoot, new GUIContent("Take a bigger item when only a little is missing", "1 point short and the next item is an Army bandage (2): take it anyway"));
			if (swap != supplySettings.SwapWorse || overshoot != supplySettings.AllowOvershoot)
			{
				supplySettings.SwapWorse = swap;
				supplySettings.AllowOvershoot = overshoot;
				changed = true;
			}

			GUILayout.Space(14);
			GUILayout.Label("What is missing right now");
			if (Plan.Count == 0)
			{
				GUILayout.Label("  Nothing is switched on.");
			}

			foreach (var entry in Plan)
			{
				GUILayout.Label(entry.Key.Name);
				foreach (var line in entry.Value)
				{
					var previous = GUI.contentColor;
					GUI.contentColor = line.Problem ? new Color(1f, 0.6f, 0.3f) : line.Run != null ? Color.white : new Color(0.7f, 0.7f, 0.7f);
					GUILayout.Label("  " + line.Text);
					GUI.contentColor = previous;
				}
			}

			GUILayout.EndScrollView();

			GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? _status : GUI.tooltip);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(new GUIContent("Check again", "Looks at your gear and stash again")))
			{
				Refresh();
			}

			GUI.enabled = !_running && Plan.Any(entry => entry.Value.Any(line => line.Run != null));
			if (GUILayout.Button(new GUIContent("Bring what is missing", "Moves the items listed in white from the stash into your gear")))
			{
				Go();
			}

			GUI.enabled = true;
			if (GUILayout.Button("Close"))
			{
				_open = false;
				Watch(null);
			}

			GUILayout.EndHorizontal();

			if (changed)
			{
				SettingsStore.Save();
				Refresh();
			}

			GUI.DragWindow(new Rect(0, 0, 10000, 20));
		}
	}
}
