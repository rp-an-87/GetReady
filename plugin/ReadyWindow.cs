using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The window: what to get ready, capturing templates, the plan and the Go button. IMGUI for now, which needs no assets.
	internal static class ReadyWindow
	{
		private const int WindowId = 0x6e7264;

		private static bool _open;
		private static Rect _rect = new Rect(80, 80, 520, 640);
		private static Vector2 _scroll;
		private static readonly List<KeyValuePair<IStep, List<PlanLine>>> Plan = new List<KeyValuePair<IStep, List<PlanLine>>>();
		private static string _status = "";
		private static bool _running;

		internal static void Toggle()
		{
			_open = !_open;
			if (_open)
			{
				Refresh();
			}
		}

		internal static void Draw()
		{
			if (_open)
			{
				_rect = GUILayout.Window(WindowId, _rect, Contents, "Get ready");
			}
		}

		private static void Refresh()
		{
			Plan.Clear();
			var controller = Game.Controller();
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

		private static void Contents(int id)
		{
			var controller = Game.Controller();
			var settings = SettingsStore.Current;
			bool changed = false;

			_scroll = GUILayout.BeginScrollView(_scroll);

			foreach (var step in Steps.All)
			{
				bool enabled = GUILayout.Toggle(step.Enabled, step.Name);
				if (enabled != step.Enabled)
				{
					step.Enabled = enabled;
					changed = true;
				}
			}

			GUILayout.Space(8);
			GUILayout.Label("Capture (arrange it by hand first)");
			if (controller != null)
			{
				if (GUILayout.Button("Special slots: capture what the pockets hold"))
				{
					_status = $"Captured {SpecialSlotsStep.Capture(controller)} special slot(s).";
					Refresh();
				}

				var cases = InjectorCaseStep.FindCases(controller.Inventory);
				if (cases.Count == 0)
				{
					GUILayout.Label("Injector case: none carried (pockets, vest, backpack or pouch).");
				}

				for (int i = 0; i < cases.Count; i++)
				{
					var injectorCase = cases[i];
					bool bound = injectorCase.Id.ToString() == settings.InjectorCase.CaseId;
					string label = $"Injector case {i + 1} ({injectorCase.Grids[0].Items.Count()} injectors){(bound ? " - captured" : "")}: capture";
					if (GUILayout.Button(label))
					{
						_status = $"Captured {InjectorCaseStep.Capture(injectorCase)} injector(s) from case {i + 1}.";
						Refresh();
					}
				}
			}

			GUILayout.Space(8);
			var flares = settings.Flares;
			GUILayout.BeginHorizontal();
			GUILayout.Label($"{flares.Item.Name}: carry at least {flares.Count}");
			if (GUILayout.Button("-", GUILayout.Width(28)) && flares.Count > 0)
			{
				flares.Count--;
				changed = true;
			}

			if (GUILayout.Button("+", GUILayout.Width(28)))
			{
				flares.Count++;
				changed = true;
			}

			GUILayout.EndHorizontal();

			GUILayout.Space(8);
			GUILayout.Label("Plan");
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

			GUILayout.Label(_status);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Refresh plan"))
			{
				Refresh();
			}

			GUI.enabled = !_running && Plan.Any(entry => entry.Value.Any(line => line.Run != null));
			if (GUILayout.Button("Go"))
			{
				Go();
			}

			GUI.enabled = true;
			if (GUILayout.Button("Close"))
			{
				_open = false;
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
