using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT.Communications;
using EFT.UI;
using UnityEngine;

namespace GetReady
{
	// Doing the plan: the stash button's click, the window's Get ready, and what they report.
	internal static partial class ReadyWindow
	{
		private static bool _running;

		private static int _moved;
		private static int _keys;

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
				var brought = _moved > 0 ? $"{_moved} item(s) brought" : "";
				var keys = _keys > 0 ? $"{_keys} quick-slot key(s) set" : "";
				var report = string.Join(", ", new[] { brought, keys }.Where(part => part != "").ToArray());
				NotificationManager.DisplayMessageNotification("Ready for raid: " + (report == "" ? "you already have everything" : report) + ".");
			}

			Play(problems.Count > 0 ? EUISoundType.ErrorMessage : EUISoundType.QuestStarted);
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

				// The keys last, when everything is where it should be
				var controller = Game.Controller();
				_keys = controller == null ? 0 : await Hotkeys.Apply(controller);

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
	}
}
