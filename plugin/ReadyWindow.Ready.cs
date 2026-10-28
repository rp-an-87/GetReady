using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GetReady
{
	// The Ready page and the widgets that draw a plan.
	internal static partial class ReadyWindow
	{
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

		// What is wanted against what is there: icon, name, a bar and the numbers, green when complete, amber when partly, red when none
		private static void ProgressRow(PlanLine line)
		{
			bool done = line.Have >= line.Want;
			var color = line.Skipped ? Ui.Dim : done ? Ui.Ok : line.Have > 0 ? Ui.Warn : Ui.Bad;
			GUILayout.BeginHorizontal();
			if (line.Item != null)
			{
				Icons.Slot(line.Item, line.Subject, 34);
			}
			else if (!string.IsNullOrEmpty(line.Template))
			{
				Icons.Slot(line.Template, line.Subject, 34);
			}

			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			GUILayout.Label(line.Subject, line.Skipped ? Ui.Muted : Ui.Body);
			if (line.Skipped)
			{
				GUILayout.Label($"{line.Note}  ({line.Have:N0}/{line.Want:N0} {line.Unit})", Ui.ValueMuted);
			}
			else
			{
				GUILayout.Label($"{line.Have:N0}/{line.Want:N0} {line.Unit}", done ? Ui.ValueOk : line.Have > 0 ? Ui.ValueWarn : Ui.ValueBad);
			}
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
				if (line.AlertIconOnly)
				{
					// no icon here
				}
				else if (line.Item != null)
				{
					Icons.Slot(line.Item, line.Subject ?? "", 34);
				}
				else if (!string.IsNullOrEmpty(line.Template))
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
				GUILayout.Label(moves > 0 ? (moves == 1 ? "1 thing to fulfil" : $"{moves} things to fulfil") : "Needs your attention", Ui.SectionTitle);
				if (problemGroups > 0)
				{
					GUILayout.Label(moves > 0
						? (problemGroups == 1 ? "1 thing needs your attention" : $"{problemGroups} things need your attention")
						: (problemGroups == 1 ? "1 thing can't be done as set up" : $"{problemGroups} things can't be done as set up"), Ui.Problem);
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
	}
}
