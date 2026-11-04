using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GetReady.ReadyPlan;
using static GetReady.ReadyRun;

namespace GetReady
{
	// The Ammo step on the Ready page: one card per caliber (its bullet and name), in it an inner panel per equipped weapon (icon once,
	// name, where it is carried, how it stands, a line for each thing about it) and one for the spare ammo of the caliber. The rest of
	// the step (the extra magazines, the loose stacks, what is wrong with them) follows as cards of the same kind.
	internal static partial class ReadyWindow
	{
		private const float LabelWidth = 84;

		private static void AmmoPanelLines(List<PlanLine> lines)
		{
			var layout = AmmoPanels.Build(lines);
			foreach (var group in layout.Groups)
			{
				AmmoGroup(group);
			}

			foreach (var note in layout.Notes)
			{
				GUILayout.Label(note.Text, Ui.Muted);
			}

			if (layout.Rest.Count > 0)
			{
				AmmoRestLines(layout.Rest);
			}
		}

		// The chip that says how a set of lines stands: red problems, amber to do, else green
		private static void StandingChip(List<PlanLine> lines)
		{
			int problems = lines.Count(line => line.Problem), moves = lines.Count(line => line.Run != null);
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

		// One card: the caliber (its bullet and name), the weapons that take it and the spare ammo for it, each in an inner panel
		private static void AmmoGroup(AmmoPanels.CaliberGroup group)
		{
			GUILayout.BeginVertical(Ui.Group);
			var caliber = CaliberName(group.Caliber);
			var bullet = AmmoPanels.AmmoTemplateOf(group);
			if (caliber != "" || bullet != null)
			{
				GUILayout.BeginHorizontal();
				if (bullet != null)
				{
					Icons.Slot(bullet, caliber, 22);
					GUILayout.Space(6);
				}

				GUILayout.Label(caliber, Ui.Name);
				GUILayout.EndHorizontal();
			}

			foreach (var weapon in group.Weapons)
			{
				GUILayout.BeginVertical(Ui.Inner);
				AmmoWeapon(weapon);
				GUILayout.EndVertical();
			}

			if (group.SpareHead != null)
			{
				GUILayout.BeginVertical(Ui.Inner);
				AmmoSpare(group);
				GUILayout.EndVertical();
			}

			EndCard(StandingOfAll(group.All()));
		}

		private const float CellHeight = 26;

		// Every piece of a row sits in the middle of the row's height, so icons, numbers and arrows line up
		private static void Cell(Action draw) => Centered(draw, CellHeight);

		private static void CellText(string text, GUIStyle style = null) => Cell(() => GUILayout.Label(text, style ?? Ui.Plain, GUILayout.ExpandWidth(false)));

		private static void CellArrow() => Cell(() => GUILayout.Label("→", Ui.Arrow, GUILayout.Width(20)));

		private static void CellPlus() => Cell(() => GUILayout.Label("+", Ui.Todo, GUILayout.Width(12)));

		// An icon with its name as the tooltip (or the tip given: why this ammo was chosen)
		private static void CellIcon(string template, string name, string tip = null)
		{
			if (string.IsNullOrEmpty(template))
			{
				return;
			}

			Cell(() =>
			{
				Icons.Slot(template, name, 22);
				if (!string.IsNullOrEmpty(tip ?? name))
				{
					GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", tip ?? name));
				}
			});
			GUILayout.Space(4);
		}

		// A line of a panel: its label, then what it says. The tooltip is the whole text of the plan line behind it.
		private static void AmmoRow(string label, Action content, string tip = null)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Space(44);
			Cell(() => GUILayout.Label(label, Ui.Muted, GUILayout.Width(LabelWidth)));
			content();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			if (!string.IsNullOrEmpty(tip))
			{
				GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", tip));
			}
		}

		// "have/want", in the colour of whether it is full
		private static void CellCount(float have, float want, bool amber = false) =>
			CellText($"{have:N0}/{want:N0}", have >= want ? Ui.ValueOk : amber ? Ui.ValueWarn : Ui.ValueBad);

		// The ammo to put in: its icon (hover: why it was chosen), its name, and "from" the containers it is taken from (hover: how many from each)
		private static void CellAmmo(string template, string name, string why, List<PlaceCount> from)
		{
			CellIcon(template, name, string.IsNullOrEmpty(why) ? name : $"{name}: {why}");
			CellText(name);
			if (from != null && from.Count > 0)
			{
				CellText("from", Ui.Muted);
				foreach (var place in from)
				{
					if (string.IsNullOrEmpty(place.Template))
					{
						// The stash has no icon: its name in a small tag
						Cell(() => GUILayout.Label(new GUIContent(place.Name, $"{place.Name}: {place.Count:N0} rounds"), Ui.ChipOff, GUILayout.ExpandWidth(false)));
						GUILayout.Space(4);
					}
					else
					{
						CellIcon(place.Template, $"{place.Name}: {place.Count:N0} rounds");
					}
				}
			}
		}

		// One thing Get ready will do for the weapon, numbered in the order it runs (no number when it is the only one)
		private static void AmmoStepRow(int number, int of, string label, Action content, string tip)
		{
			AmmoRow(of > 1 ? $"{number}  {label}" : label, content, tip);
		}

		private static void AmmoWeapon(AmmoPanels.WeaponPanel panel)
		{
			var all = panel.All();
			GUILayout.BeginHorizontal();
			Icons.Slot(panel.Item, panel.Name, 34);
			GUILayout.Space(8);
			Centered(() => GUILayout.Label(panel.Name, Ui.Plain, GUILayout.ExpandWidth(false)), 34);
			if (!string.IsNullOrEmpty(panel.Slot))
			{
				Centered(() => GUILayout.Label("· " + panel.Slot, Ui.PlainMuted, GUILayout.ExpandWidth(false)), 34);
			}

			GUILayout.FlexibleSpace();
			Centered(() => StandingChip(all), 34);
			GUILayout.EndHorizontal();

			var magazine = panel.Lines.FirstOrDefault(line => line.Part == "magazine" && line.Problem);
			var fill = panel.Lines.FirstOrDefault(line => line.Part == "magazine" && line.Run != null);
			var put = panel.Lines.FirstOrDefault(line => line.Part == "put");
			var chamber = panel.Lines.FirstOrDefault(line => line.Part == "chamber");
			var other = panel.Lines.Where(line => line != magazine && line != fill && line != put && line != chamber).ToList();
			var magazineIcon = AmmoPanels.MagazineTemplateOf(panel);

			// What is there now
			if (magazine != null)
			{
				AmmoRow("In the gun", () => CellText(AmmoPanels.ShortProblem(magazine.Detail ?? magazine.Text), Ui.Note), magazine.Text);
			}
			else if (put != null)
			{
				// Amber: a magazine goes in (the steps below), red is only for a weapon nothing can be put in
				AmmoRow("In the gun", () => CellText("no magazine", Ui.PlainWarn));
			}
			else if (panel.Head != null)
			{
				var head = panel.Head;
				AmmoRow("In the gun", () =>
				{
					CellIcon(magazineIcon, "magazine");
					CellCount(head.Have, head.Want, fill != null);
					foreach (var held in AmmoPanels.HeldAmmo(panel))
					{
						CellIcon(held.Key, held.Value);
						CellText(held.Value);
					}
				});
			}

			// The chamber as it is now, when Get ready has nothing to do about it
			if (chamber == null && AmmoPanels.HasChamber(panel))
			{
				var round = AmmoPanels.ChamberedAmmo(panel);
				AmmoRow("Chamber", () =>
				{
					if (round == null)
					{
						CellText("empty", Ui.Muted);
					}
					else
					{
						CellIcon(round.Value.Key, round.Value.Value);
						CellText(round.Value.Value);
					}
				});
			}

			// What Get ready will do, in the order it runs
			int steps = (fill != null ? 1 : 0) + (put != null ? 1 : 0) + (chamber != null ? 1 : 0), number = 0;
			if (fill != null)
			{
				number++;
				var forPut = put != null;
				float have = fill.Have, want = fill.Want;
				AmmoStepRow(number, steps, "Fill", () =>
				{
					CellIcon(fill.ItemTemplate, fill.Label);
					CellText(fill.Label);
					if (forPut)
					{
						CellText("from", Ui.Muted);
						CellIcon(fill.PlaceTemplate, fill.Place);
					}

					CellCount(have, want, true);
					CellArrow();
					CellCount(Math.Min(want, have + fill.Amount), want, true);
					CellAmmo(fill.Template, fill.What, fill.Why, fill.From);
					if (!forPut)
					{
						CellText("(taken out and put back)", Ui.Muted);
					}
				}, AmmoPanels.Tip(fill));
			}

			if (put != null)
			{
				number++;
				AmmoStepRow(number, steps, "Put in", () =>
				{
					CellIcon(put.Template, put.What);
					CellText(put.What);
					CellText("from", Ui.Muted);
					CellIcon(put.PlaceTemplate, put.Place);
					CellText("into", Ui.Muted);
					Cell(() => Icons.Slot(panel.Item, panel.Name, 22));
				}, put.Text);
			}

			if (chamber != null)
			{
				number++;
				AmmoStepRow(number, steps, "Chamber", () =>
				{
					if (chamber.Problem)
					{
						CellText(AmmoPanels.ShortProblem(chamber.Detail ?? chamber.Text), Ui.Note);
					}
					else
					{
						CellAmmo(chamber.Template, chamber.What, chamber.Why, string.IsNullOrEmpty(chamber.Place) ? null : new List<PlaceCount> { new PlaceCount { Template = chamber.PlaceTemplate, Name = chamber.Place, Count = 1 } });
					}
				}, AmmoPanels.Tip(chamber));
			}

			foreach (var line in other)
			{
				AmmoRow("", () => CellText(AmmoPanels.Short(line.Detail ?? line.Text), line.Problem ? Ui.Note : Ui.Plain), line.Text);
			}
		}

		// The spare ammo of the caliber: what is on you, or what will be brought, against what is asked; red when it can't be had
		private static void AmmoSpare(AmmoPanels.CaliberGroup group)
		{
			var head = group.SpareHead;
			var problem = group.Spare.FirstOrDefault(line => line.Problem);
			var bring = group.Spare.FirstOrDefault(line => line.Run != null);
			var asked = bring?.Asked ?? head.Asked;
			AmmoRow("Spare ammo", () =>
			{
				if (bring != null)
				{
					CellPlus();
					CellIcon(bring.Template, bring.What);
					CellText($"{bring.Amount:N0} {bring.What}");
				}
				else if (problem == null && head.What != null)
				{
					CellIcon(head.WhatTemplate, head.What);
					CellText($"{head.Amount:N0} {head.What} on you", Ui.ValueOk);
				}

				if (problem != null)
				{
					if (bring != null)
					{
						GUILayout.Space(8);
					}

					CellText(AmmoPanels.ShortProblem(problem.Detail ?? problem.Text), Ui.Note);
				}
				else if (asked > 0)
				{
					GUILayout.Space(8);
					CellText($"asks for {asked:N0}", Ui.Muted);
				}
			}, bring?.Text ?? problem?.Text ?? "Loose ammo of this caliber on you counts toward what is asked, and so does what the top-up of the loose stacks adds.");
		}

		// The cards after the weapons: each progress line opens a card with a header (icon, title, "?", how many), its lines in an inner panel
		private static void AmmoRestLines(List<PlanLine> lines)
		{
			bool open = false, inner = false;
			var standing = Standing.Done;
			for (int index = 0; index < lines.Count; index++)
			{
				var line = lines[index];
				if (line.Standalone || line.IsProgress)
				{
					if (inner)
					{
						GUILayout.EndVertical();
						inner = false;
					}

					if (open)
					{
						EndCard(standing);
						open = false;
					}
				}

				if (line.IsProgress)
				{
					GUILayout.BeginVertical(Ui.Group);
					open = true;
					standing = line.Subject == AmmoStep.LooseAmmo ? LooseStanding(lines, index) : StandingFor(lines, index);
					AmmoRestHeader(line, standing);
					continue;
				}

				if (!line.Standalone && open && !inner)
				{
					GUILayout.BeginVertical(Ui.Inner);
					inner = true;
				}

				AmmoRestLine(line);
			}

			if (inner)
			{
				GUILayout.EndVertical();
			}

			if (open)
			{
				EndCard(standing);
			}
		}

		private static void AmmoRestHeader(PlanLine line, Standing standing)
		{
			// Short, nothing to do and nothing wrong (loose stacks the stash can't fill): the count is dim, not a warning
			bool left = line.Subject == AmmoStep.LooseAmmo && standing == Standing.Done && line.Have < line.Want;
			var style = left ? Ui.ValueMuted : standing == Standing.Done ? Ui.ValueOk : standing == Standing.Doable ? Ui.ValueWarn : Ui.ValueBad;
			GUILayout.BeginHorizontal();
			if (!string.IsNullOrEmpty(line.Template))
			{
				Icons.Slot(line.Template, line.Subject, 34);
				GUILayout.Space(8);
			}

			GUILayout.Label(line.Subject, Ui.Plain);
			if (line.Subject == AmmoStep.ExtraMagazines)
			{
				Help("Magazines not including the ones going into your weapons.");
			}
			else if (line.Subject == AmmoStep.LooseAmmo)
			{
				Help("Loose stacks of ammo you carry (not in magazines or boxes), topped up to a full stack from the stash.");
			}

			GUILayout.FlexibleSpace();
			GUILayout.Label(new GUIContent($"{line.Have:N0}/{line.Want:N0} {line.Unit}", left ? "Some stacks can't be filled: the stash has no more of that ammo." : ""), style, GUILayout.ExpandWidth(false));
			GUILayout.EndHorizontal();
		}

		// Magazines that cannot be filled: what they are and where, in a red box, and why (which ammo, or none left)
		private static void StuckBox(PlanLine line)
		{
			GUILayout.BeginVertical(Ui.ProblemBox);
			GUILayout.Label(line.Subject, Ui.Problem);
			foreach (var mag in line.Mags)
			{
				GUILayout.BeginHorizontal();
				CellIcon(mag.Template, mag.Name);
				CellText(mag.Name);
				if (!string.IsNullOrEmpty(mag.Place))
				{
					CellText("in", Ui.Muted);
					CellIcon(mag.PlaceTemplate, mag.Place);
				}

				CellCount(mag.Have, mag.Want);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}

			GUILayout.Label(string.IsNullOrEmpty(line.Options)
				? "There is no ammo left on you or in the stash that fits them."
				: $"They could take {line.Options}, and nothing tells which one: load one of them yourself and the others follow.", Ui.Body);
			GUILayout.EndVertical();
			GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", line.Text));
		}

		private static void AmmoRestLine(PlanLine line)
		{
			if (line.Part == "stuck" && line.Mags.Count > 0)
			{
				StuckBox(line);
				return;
			}

			if (line.Problem || line.Run == null)
			{
				DetailLine(line, false);
				return;
			}

			GUILayout.BeginHorizontal();
			CellPlus();
			if (line.Fill && !string.IsNullOrEmpty(line.ItemTemplate))
			{
				CellIcon(line.ItemTemplate, line.Label);
				CellText(line.Label);
				CellText("in", Ui.Muted);
				CellIcon(line.PlaceTemplate, line.Place);
				CellCount(line.Have, line.Want, true);
				CellArrow();
				CellCount(Math.Min(line.Want, line.Have + line.Amount), line.Want, true);
				CellAmmo(line.Template, line.What, line.Why, line.From);
			}
			else if (line.Topped)
			{
				CellIcon(line.Template, line.Label);
				CellText(line.Label);
				CellIcon(line.PlaceTemplate, line.Place);
				CellCount(line.Have, line.Want, true);
				CellArrow();
				CellCount(Math.Min(line.Want, line.Have + line.Amount), line.Want, true);
				CellText($"+{line.Amount:N0}", Ui.Muted);
			}
			else
			{
				CellIcon(line.Template, line.What);
				CellText(AmmoPanels.Short(line.Text));
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", AmmoPanels.Tip(line)));
		}
	}
}
