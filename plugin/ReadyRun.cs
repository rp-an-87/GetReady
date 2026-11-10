using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT.Communications;
using EFT.UI;
using static GetReady.ReadyPlan;

namespace GetReady
{
	// Doing the plan: the stash button's click, the window's Get ready, and what they report. Nothing is drawn here.
	internal static class ReadyRun
	{
		internal static bool _running;

		internal static int _moved;
		internal static int _repaired;
		internal static int _filled;
		internal static int _chambered;
		internal static int _topped;
		internal static int _keys;
		internal static int _healed;
		internal static int _modes;
		internal static int _insured;

		// What could not be moved in the last run, shown with the other problems
		internal static readonly List<KeyValuePair<string, PlanLine>> Failures = new List<KeyValuePair<string, PlanLine>>();

		// A notification (when there is a text, it has its own sound) and a sound (when given) for something done outside a run, like the case menu's entries. Never throws.
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		internal static void Say(string text, EUISoundType? sound)
		{
			try
			{
				if (!string.IsNullOrEmpty(text))
				{
					NotificationManager.DisplayMessageNotification(text);
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not show the notification: {e.Message}");
			}

			if (sound != null)
			{
				Play(sound.Value);
			}
		}

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

		// The button's plain click: look at the gear, bring what is missing and say what happened. Returns what is wrong (the caller shows
		// it), or null when a run was already going.
		internal static async System.Threading.Tasks.Task<List<KeyValuePair<string, PlanLine>>> QuickRun()
		{
			if (_running)
			{
				return null;
			}

			Refresh();
			await RunPlan();
			// The bar finishes first: the sound, the notification, the flash and the red box come after it
			await ReadyLoading.WhenDone();
			var problems = Failures.Concat(Problems()).ToList();

			// The red box says what is wrong, so a notification is only for when all went well
			if (problems.Count == 0)
			{
				NotificationManager.DisplayMessageNotification(Report());
			}

			// Nothing to say, nothing to hear: no sound when you already had everything
			if (problems.Count > 0)
			{
				Play(EUISoundType.ErrorMessage);
			}
			else if (_moved > 0 || _keys > 0 || _repaired > 0 || _filled > 0 || _chambered > 0 || _topped > 0 || _healed > 0 || _modes > 0 || _insured > 0)
			{
				Play(EUISoundType.QuestStarted);
				ReadyFlash.Show();
			}

			return problems;
		}

		// What the notification says after a run that went well
		internal static string Report()
		{
			var brought = _moved > 0 ? $"{_moved} item(s) brought" : "";
			var repaired = _repaired > 0 ? $"{_repaired} piece(s) of gear repaired" : "";
			var filled = _filled > 0 ? $"{_filled} magazine(s) filled" : "";
			var chambered = _chambered > 0 ? $"{_chambered} round(s) chambered" : "";
			var topped = _topped > 0 ? $"{_topped} ammo stack(s) topped up" : "";
			var keys = _keys > 0 ? $"{_keys} quick-slot key(s) set" : "";
			var healed = _healed > 0 ? "healed" : "";
			var modes = _modes > 0 ? $"{_modes} weapon(s) on full auto" : "";
			var insured = _insured > 0 ? $"{_insured} item(s) insured" : "";
			var report = string.Join(", ", new[] { brought, repaired, insured, filled, chambered, topped, keys, healed, modes }.Where(part => part != "").ToArray());
			return "Ready for raid: " + (report == "" ? "you already have everything" : report) + ".";
		}

		internal static async System.Threading.Tasks.Task RunPlan()
		{
			if (_running)
			{
				return;
			}

			_running = true;
			try
			{
				ReadyLoading.Begin(Plan.Sum(entry => entry.Value.Count(line => line.Run != null)));
				int done = 0, failed = 0, repaired = 0, filled = 0, chambered = 0, topped = 0, healed = 0, modes = 0, insured = 0;
				Failures.Clear();
				foreach (var entry in Plan.ToList())
				{
					foreach (var line in entry.Value.Where(line => line.Run != null))
					{
						ReadyLoading.Step(entry.Key.Name);
						var error = await line.Run();
						ReadyLoading.LineDone();
						if (error == null)
						{
							done += line.Items;
							repaired += line.Repair ? 1 : 0;
							filled += line.Fill ? 1 : 0;
							chambered += line.Chamber ? 1 : 0;
							topped += line.Topped ? 1 : 0;
							healed += line.Heal ? 1 : 0;
							modes += line.Mode ? 1 : 0;
							insured += line.Insured;
						}
						else
						{
							failed++;
							Plugin.Log.LogWarning($"{entry.Key.Name}: {line.Text} failed: {error}");
							Failures.Add(new KeyValuePair<string, PlanLine>(entry.Key.Name, new PlanLine { Problem = true, Subject = line.Fill && !string.IsNullOrEmpty(line.Label) ? $"{line.Label}: {line.Have}/{line.Want}" : line.Text, Detail = line.FailPrefix + error, Template = line.Template }));
						}
					}
				}

				// The keys last, when everything is where it should be
				var controller = Game.Controller();
				_keys = controller == null ? 0 : await Hotkeys.Apply(controller);

				_moved = done;
				_repaired = repaired;
				_filled = filled;
				_chambered = chambered;
				_topped = topped;
				_healed = healed;
				_modes = modes;
				_insured = insured;
				_status = failed == 0 ? $"Done: {done} moved." : $"{done} moved, {failed} failed (see the log).";
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Get ready failed: {e}");
				_status = "Failed, see the log.";
			}
			finally
			{
				ReadyLoading.Finish();
				_running = false;
				Refresh();
			}
		}
	}
}
