using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GetReady
{
	// The Ready page and the widgets that draw a plan.
	internal static partial class ReadyWindow
	{
		// The step's icon and name, the name centred on the icon
		private static void StepTitle(string name)
		{
			// One rect for both, so the text is centred on the icon whatever the row around it does
			var style = _titleMiddle ?? (_titleMiddle = new GUIStyle(Ui.SectionTitle) { alignment = TextAnchor.MiddleLeft });
			float text = style.CalcSize(new GUIContent(name)).x + 6;
			var rect = GUILayoutUtility.GetRect(26 + 8 + text, 26, GUILayout.Width(26 + 8 + text), GUILayout.Height(26));
			Icons.Step(new Rect(rect.x, rect.y, 26, 26), name);
			GUI.Label(new Rect(rect.x + 34, rect.y, text, 26), name, style);
		}

		private static GUIStyle _titleMiddle;

		// A small coloured tag on a section: what the step will do
		private static void Chip(IStep step, List<PlanLine> lines)
		{
			if (!step.Enabled || lines == null)
			{
				GUILayout.Label("Off", Ui.ChipOff);
				return;
			}

			// Cards, as in the summary and on the stash button: red for what cannot be done, amber for what Get ready will do
			int moves = 0, problems = 0;
			TallyLines(lines, ref moves, ref problems);
			if (problems > 0)
			{
				GUILayout.Label(problems == 1 ? "1 problem" : $"{problems} problems", Ui.ChipBad);
			}

			if (moves > 0)
			{
				GUILayout.Label($"{moves} to do", Ui.ChipTodo);
			}

			if (problems == 0 && moves == 0)
			{
				GUILayout.Label("Ready", Ui.ChipOk);
			}
		}

		// How a card stands: red when something is wrong, else green when it is complete, else amber when Get ready can make it complete
		internal enum Standing { Done, Doable, Problem }

		internal static Standing StandingOf(bool done, bool hasRun, bool hasProblem) =>
			hasProblem ? Standing.Problem : done ? Standing.Done : hasRun ? Standing.Doable : Standing.Problem;

		// The standing of the card the progress line at this index opens: what follows it up to the next progress line decides
		internal static Standing StandingFor(List<PlanLine> lines, int progressIndex)
		{
			var progress = lines[progressIndex];
			bool hasRun = false, hasProblem = false;
			for (int i = progressIndex + 1; i < lines.Count && !lines[i].IsProgress && !lines[i].Standalone; i++)
			{
				hasRun |= lines[i].Run != null || lines[i].Covered;
				hasProblem |= lines[i].Problem;
			}

			return StandingOf(progress.Have >= progress.Want, hasRun, hasProblem);
		}

		// What is wanted against what is there: icon, name, a bar and the numbers, in the colour of how the card stands
		private static void ProgressRow(PlanLine line, Standing standing, string subject = null, string iconTemplate = null)
		{
			subject = subject ?? line.Subject;
			var color = line.Skipped ? Ui.Dim : standing == Standing.Done ? Ui.Ok : standing == Standing.Doable ? Ui.Warn : Ui.Bad;
			var valueStyle = standing == Standing.Done ? Ui.ValueOk : standing == Standing.Doable ? Ui.ValueWarn : Ui.ValueBad;
			GUILayout.BeginHorizontal();
			if (iconTemplate != null)
			{
				Icons.Slot(iconTemplate, subject, 34);
				GUILayout.Space(8);
			}
			else if (line.Item != null)
			{
				Icons.Slot(line.Item, subject, 34);
				GUILayout.Space(8);
			}
			else if (!string.IsNullOrEmpty(line.Template))
			{
				Icons.Slot(line.Template, subject, 34);
				GUILayout.Space(8);
			}

			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			GUILayout.Label(subject, line.Skipped ? Ui.Muted : Ui.Body);
			if (line.Skipped)
			{
				GUILayout.Label($"{line.Note}  ({line.Have:N0}/{line.Want:N0} {line.Unit})", Ui.ValueMuted);
			}
			else
			{
				GUILayout.Label($"{line.Have:N0}/{line.Want:N0} {line.Unit}", valueStyle);
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

		internal static Color StandingColor(Standing standing) => standing == Standing.Done ? Ui.Ok : standing == Standing.Doable ? Ui.Warn : Ui.Bad;

		// The end of a card: a thin stripe down its left edge in the colour of how it stands, so the page can be read down one line
		private static void EndCard(Standing standing)
		{
			GUILayout.EndVertical();
			if (Event.current.type == EventType.Repaint)
			{
				var rect = GUILayoutUtility.GetLastRect();
				var old = GUI.color;
				GUI.color = StandingColor(standing);
				GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, 3, rect.height - 2), Texture2D.whiteTexture);
				GUI.color = old;
			}
		}

		// The lines of a step: each progress line opens a card, and what follows it (what will be brought, what is wrong) sits in it
		private static void PlanLines(List<PlanLine> lines)
		{
			bool open = false;
			var standing = Standing.Done;
			for (int index = 0; index < lines.Count; index++)
			{
				var line = lines[index];
				if (line.Standalone && open)
				{
					EndCard(standing);
					open = false;
				}

				if (line.IsProgress)
				{
					if (open)
					{
						EndCard(standing);
					}

					GUILayout.BeginVertical(Ui.Group);
					open = true;
					standing = StandingFor(lines, index);
					ProgressRow(line, standing);
					continue;
				}

				DetailLine(line, open);
			}

			if (open)
			{
				EndCard(standing);
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

				GUILayout.Label("+", Ui.Todo, GUILayout.Width(12));
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
				// Two boxes side by side, never one fill for both: red for what cannot be done as set up, amber for what Get ready will do
				GUILayout.BeginHorizontal();
				if (problemGroups > 0)
				{
					GUILayout.BeginVertical(Ui.ProblemBoxTop);
					GUILayout.Label(problemGroups == 1 ? "1 thing can't be done as set up" : $"{problemGroups} things can't be done as set up", Ui.ProblemTitle);
					GUILayout.EndVertical();
					GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", ProblemsTooltip()));
				}

				if (problemGroups > 0 && moves > 0)
				{
					GUILayout.Space(8);
				}

				if (moves > 0)
				{
					GUILayout.BeginVertical(Ui.TodoBoxTop);
					GUILayout.Label(moves == 1 ? "1 thing to fulfil" : $"{moves} things to fulfil", Ui.TodoTitle);
					GUILayout.EndVertical();
					GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", ToDoTooltip()));
				}

				GUILayout.EndHorizontal();
			}

			_scroll = GUILayout.BeginScrollView(_scroll);
			foreach (var step in Steps.DisplayOrder)
			{
				var lines = Plan.Where(entry => entry.Key == step).Select(entry => entry.Value).FirstOrDefault();
				GUILayout.BeginVertical(Ui.Section);
				GUILayout.BeginHorizontal();
				StepTitle(step.Name);
				GUILayout.FlexibleSpace();
				Chip(step, lines);
				GUILayout.EndHorizontal();
				if (step.Enabled && lines != null && lines.Count > 0)
				{
					GUILayout.Box(GUIContent.none, Ui.Divider);
					if (step is AmmoStep)
					{
						AmmoPanelLines(lines);
					}
					else
					{
						PlanLines(lines);
					}
				}

				GUILayout.EndVertical();
			}

			GUILayout.EndScrollView();
		}
	}
}
