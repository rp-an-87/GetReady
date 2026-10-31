using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The plan: what each step found, kept up to date while the stash is up, with the counts the button and the pages show.
	internal static partial class ReadyWindow
	{
		internal static readonly List<KeyValuePair<IStep, List<PlanLine>>> Plan = new List<KeyValuePair<IStep, List<PlanLine>>>();

		internal static string _status = "";

		// The plan is read again shortly after the inventory changes (an item moved in the stash, say)
		private static InventoryController _watched;

		private static bool _dirty;

		private static float _dirtyAt;

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
				StashBadge.Set(0, 0);
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
		private static string _lastBadge = "";

		private static void UpdateBadge()
		{
			int toBring, withProblems;
			Tally(out toBring, out withProblems);
			StashBadge.Set(withProblems, toBring);

			// What the badge shows and why (logged when it changes): the lines that can be run and the problems, per step
			var why = $"red {withProblems}, gold {toBring}: " + string.Join("; ", Plan.Select(entry => $"{entry.Key.Name} {entry.Value.Count(line => line.Run != null)} to do, {entry.Value.Count(line => line.Problem)} problem(s)").ToArray());
			if (why != _lastBadge)
			{
				_lastBadge = why;
				Plugin.Log.LogInfo($"Badge {why}");
			}
		}

		// How many groups (a supply, a template: a card of the Ready page) have something to bring, and how many have a problem.
		// It counts groups, not items: "bring 3 grenades" is one thing to get ready, not one per grenade type.
		internal static void Tally(out int toBring, out int withProblems)
		{
			toBring = 0;
			withProblems = 0;
			foreach (var entry in Plan)
			{
				TallyLines(entry.Value, ref toBring, ref withProblems);
			}
		}

		// The same count for the lines of one step
		internal static void TallyLines(List<PlanLine> lines, ref int toBring, ref int withProblems)
		{
			bool run = false, problem = false, open = false;
			foreach (var line in lines)
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

				if (line.Standalone && open)
				{
					// Its own thing: the card above ends here
					toBring += run ? 1 : 0;
					withProblems += problem ? 1 : 0;
					open = false;
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

		// The colours of the tooltip: the window's own (green when fine, amber when there is something to do, red for a problem)
		private const string Green = "#73C77F";
		private const string Amber = "#FFA347";
		private const string Red = "#FF665C";
		private const string SoftRed = "#D98B84";
		private const string Grey = "#8A8A8A";
		private const string Key = "#B8B8B8";

		private static string Separator => "<size=7><color=#555555>" + new string('—', 40) + "</color></size>";

		private static string Short(string text, int length) => text.Length <= length ? text : text.Substring(0, length - 1).TrimEnd() + "…";

		private const int TipLines = 14;

		// What the red box says on hover: every problem, step by step, with what is wrong
		internal static string ProblemsTooltip()
		{
			var text = new System.Text.StringBuilder("Cannot be done as set up:");
			var problems = Problems();
			foreach (var problem in problems.Take(TipLines))
			{
				var line = problem.Value;
				text.Append($"\n- {problem.Key}: {line.Subject ?? line.Text}");
				if (!string.IsNullOrEmpty(line.Detail))
				{
					text.Append($"\n     {line.Detail}");
				}
			}

			if (problems.Count > TipLines)
			{
				text.Append($"\n... and {problems.Count - TipLines} more");
			}

			return text.ToString();
		}

		// What the amber box says on hover: what Get ready will do, step by step
		internal static string ToDoTooltip()
		{
			var text = new System.Text.StringBuilder("Get ready will:");
			int shown = 0, hidden = 0;
			foreach (var entry in Plan)
			{
				var work = entry.Value.Where(line => line.Run != null).ToList();
				if (work.Count == 0)
				{
					continue;
				}

				if (shown < TipLines)
				{
					text.Append($"\n{entry.Key.Name}");
				}

				foreach (var line in work)
				{
					if (shown >= TipLines)
					{
						hidden++;
						continue;
					}

					text.Append($"\n  + {line.Text}");
					shown++;
				}
			}

			if (hidden > 0)
			{
				text.Append($"\n... and {hidden} more");
			}

			return text.ToString();
		}

		// The tooltip of the stash button: the title, how it stands, what is wrong (red, a few lines), and what the clicks do
		internal static string Summary()
		{
			int moves, problemGroups;
			Tally(out moves, out problemGroups);
			var problems = Problems();
			var text = new System.Text.StringBuilder("<b>Get ready for raid</b>\n");
			if (Plan.Count == 0)
			{
				text.Append($"<color={Grey}>Nothing is switched on</color>");
			}
			else if (problems.Count > 0)
			{
				// Red first: whatever can't be done decides how it stands, then what Get ready can still do
				text.Append($"<color={Red}>Needs your attention</color>");
				if (moves > 0)
				{
					text.Append($"\n<color={Amber}>{(moves == 1 ? "1 thing to fulfil" : $"{moves} things to fulfil")}</color>");
				}
			}
			else if (moves > 0)
			{
				text.Append($"<color={Amber}>{(moves == 1 ? "1 thing to fulfil" : $"{moves} things to fulfil")}</color>");
			}
			else
			{
				text.Append($"<color={Green}>You are all set</color>");
			}

			if (problems.Count > 0)
			{
				text.Append("\n" + Separator + $"\n<color={Red}><b>{(problems.Count == 1 ? "1 problem" : $"{problems.Count} problems")}</b></color>");
				foreach (var problem in problems.Take(5))
				{
					var line = problem.Value;
					var subject = Short(line.Subject ?? line.Text, 34);
					var detail = line.Detail != null && line.Subject != null ? Short(line.Detail, 46) : null;
					text.Append($"\n<color={Red}>• {subject}</color>" + (detail != null ? $"\n<size=11><color={SoftRed}>    {detail}</color></size>" : ""));
				}

				if (problems.Count > 5)
				{
					text.Append($"\n<color={SoftRed}>… and {problems.Count - 5} more</color>");
				}
			}

			text.Append("\n" + Separator + $"\n<size=11><color={Key}>[Left click]</color> <color={Grey}>get ready</color>   <color={Key}>[Right click]</color> <color={Grey}>configure</color></size>");
			return text.ToString();
		}

		// Redraw with a fresh plan on the next frame (never in the middle of drawing)
		private static void RefreshSoon()
		{
			_dirty = true;
			_dirtyAt = 0f;
		}

		// What is wrong right now, one line each: the problems of every step in the plan
		internal static List<KeyValuePair<string, PlanLine>> Problems() =>
			Plan.SelectMany(entry => entry.Value.Where(line => line.Problem).Select(line => new KeyValuePair<string, PlanLine>(entry.Key.Name, line))).ToList();
	}
}
