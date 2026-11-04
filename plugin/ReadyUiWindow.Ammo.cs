using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace GetReady
{
	// The Ammo step of the Ready page in Unity UI (the same panels as ReadyWindow.AmmoPanels): one card per caliber with an inner panel
	// per equipped weapon and one for the spare ammo, then the cards of the extra magazines and the loose stacks.
	internal static partial class ReadyUiWindow
	{
		private static readonly Color InnerPanel = new Color(0.095f, 0.1f, 0.1f);

		private static float CellIconSize => UiKit.Unit * 1.5f;

		private static Sprite SpriteOfTemplate(string template)
		{
			if (string.IsNullOrEmpty(template))
			{
				return null;
			}

			var sprite = Icons.Of(template);
			if (sprite == null)
			{
				_pending++;
			}

			return sprite;
		}

		private static void CellText(Transform row, string text, Color? color = null) => UiKit.Label(row, text, 0.85f, color ?? Ui.Text);

		private static void CellArrow(Transform row) => UiKit.Label(row, "→", 0.85f, Ui.Dim);

		private static void CellPlus(Transform row) => UiKit.Label(row, "+", 0.85f, Ui.Warn);

		// An icon with its name as the tooltip (or the tip given: why this ammo was chosen)
		private static void CellIcon(Transform row, string template, string name, string tip = null)
		{
			if (string.IsNullOrEmpty(template))
			{
				return;
			}

			UiKit.Icon(row, SpriteOfTemplate(template), CellIconSize, tip ?? name);
		}

		// A line of a panel: its label, then what it says. The tooltip is the whole text of the plan line behind it.
		private static void AmmoRow(Transform parent, string label, Action<Transform> content, string tip = null)
		{
			var row = UiKit.HBox(parent, 6f);
			UiKit.Gap(row, UiKit.Unit * 2.4f);
			var name = UiKit.Label(row, label, 0.8f, Ui.Dim);
			UiKit.Size(name, UiKit.Unit * 6f);
			content(row);
			UiKit.Tip(row, tip);
		}

		// "have/want", in the colour of whether it is full
		private static void CellCount(Transform row, float have, float want, bool amber = false) =>
			CellText(row, $"{have:N0}/{want:N0}", have >= want ? Ui.Ok : amber ? Ui.Warn : Ui.Bad);

		// The ammo to put in: its icon (hover: why it was chosen), its name, and "from" the containers it is taken from (hover: how many from each)
		private static void CellAmmo(Transform row, string template, string name, string why, List<PlaceCount> from)
		{
			CellIcon(row, template, name, string.IsNullOrEmpty(why) ? name : $"{name}: {why}");
			CellText(row, name);
			if (from != null && from.Count > 0)
			{
				CellText(row, "from", Ui.Dim);
				foreach (var place in from)
				{
					if (string.IsNullOrEmpty(place.Template))
					{
						// The stash has no icon: its name in a small tag
						UiKit.Tip(UiKit.Chip(row, place.Name, new Color(0.25f, 0.26f, 0.25f)), $"{place.Name}: {place.Count:N0} rounds");
					}
					else
					{
						CellIcon(row, place.Template, $"{place.Name}: {place.Count:N0} rounds");
					}
				}
			}
		}

		// The chip that says how a set of lines stands: red problems, amber to do, else green
		private static void StandingChip(Transform parent, List<PlanLine> lines)
		{
			int problems = lines.Count(line => line.Problem), moves = lines.Count(line => line.Run != null);
			if (problems > 0)
			{
				UiKit.Chip(parent, problems == 1 ? "1 problem" : $"{problems} problems", new Color(0.6f, 0.2f, 0.18f));
			}

			if (moves > 0)
			{
				UiKit.Chip(parent, $"{moves} to do", new Color(0.42f, 0.36f, 0.16f));
			}

			if (problems == 0 && moves == 0)
			{
				UiKit.Chip(parent, "Ready", new Color(0.2f, 0.38f, 0.22f));
			}
		}

		private static void AmmoPanelLines(Transform parent, List<PlanLine> lines)
		{
			var layout = AmmoPanels.Build(lines);
			foreach (var group in layout.Groups)
			{
				AmmoGroup(parent, group);
			}

			foreach (var note in layout.Notes)
			{
				UiKit.Label(parent, note.Text, 0.8f, Ui.Dim, wrap: true);
			}

			if (layout.Rest.Count > 0)
			{
				AmmoRestLines(parent, layout.Rest);
			}
		}

		// One card: the caliber (its bullet and name), the weapons that take it and the spare ammo for it, each in an inner panel
		private static void AmmoGroup(Transform parent, AmmoPanels.CaliberGroup group)
		{
			var card = Card(parent, ReadyWindow.StandingOfAll(group.All()));
			var caliber = ReadyWindow.CaliberName(group.Caliber);
			var bullet = AmmoPanels.AmmoTemplateOf(group);
			if (caliber != "" || bullet != null)
			{
				var head = UiKit.HBox(card, 6f);
				if (bullet != null)
				{
					UiKit.Icon(head, SpriteOfTemplate(bullet), CellIconSize, caliber);
				}

				UiKit.Label(head, caliber, 0.95f, Ui.Heading);
			}

			foreach (var weapon in group.Weapons)
			{
				AmmoWeapon(UiKit.VBox(card, 3f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge), weapon);
			}

			if (group.SpareHead != null)
			{
				AmmoSpare(UiKit.VBox(card, 3f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge), group);
			}
		}

		// One thing Get ready will do for the weapon, numbered in the order it runs (no number when it is the only one)
		private static void AmmoStepRow(Transform parent, int number, int of, string label, Action<Transform> content, string tip) =>
			AmmoRow(parent, of > 1 ? $"{number}  {label}" : label, content, tip);

		private static void AmmoWeapon(Transform parent, AmmoPanels.WeaponPanel panel)
		{
			var all = panel.All();
			var head = UiKit.HBox(parent, 8f);
			UiKit.Icon(head, Icons.OfItem(panel.Item), UiKit.Unit * 2.2f, panel.Name);
			UiKit.Label(head, panel.Name, 0.9f, Ui.Text);
			if (!string.IsNullOrEmpty(panel.Slot))
			{
				UiKit.Label(head, "· " + panel.Slot, 0.85f, Ui.Dim);
			}

			UiKit.Spacer(head);
			StandingChip(head, all);

			var magazine = panel.Lines.FirstOrDefault(line => line.Part == "magazine" && line.Problem);
			var fill = panel.Lines.FirstOrDefault(line => line.Part == "magazine" && line.Run != null);
			var put = panel.Lines.FirstOrDefault(line => line.Part == "put");
			var chamber = panel.Lines.FirstOrDefault(line => line.Part == "chamber");
			var other = panel.Lines.Where(line => line != magazine && line != fill && line != put && line != chamber).ToList();
			var magazineIcon = AmmoPanels.MagazineTemplateOf(panel);

			// What is there now
			if (magazine != null)
			{
				AmmoRow(parent, "In the gun", row => CellText(row, AmmoPanels.ShortProblem(magazine.Detail ?? magazine.Text), Ui.Bad), magazine.Text);
			}
			else if (put != null)
			{
				// Amber: a magazine goes in (the steps below), red is only for a weapon nothing can be put in
				AmmoRow(parent, "In the gun", row => CellText(row, "no magazine", Ui.Warn));
			}
			else if (panel.Head != null)
			{
				var current = panel.Head;
				AmmoRow(parent, "In the gun", row =>
				{
					CellIcon(row, magazineIcon, "magazine");
					CellCount(row, current.Have, current.Want, fill != null);
					foreach (var held in AmmoPanels.HeldAmmo(panel))
					{
						CellIcon(row, held.Key, held.Value);
						CellText(row, held.Value);
					}
				});
			}

			// The chamber as it is now, when Get ready has nothing to do about it
			if (chamber == null && AmmoPanels.HasChamber(panel))
			{
				var round = AmmoPanels.ChamberedAmmo(panel);
				AmmoRow(parent, "Chamber", row =>
				{
					if (round == null)
					{
						CellText(row, "empty", Ui.Dim);
					}
					else
					{
						CellIcon(row, round.Value.Key, round.Value.Value);
						CellText(row, round.Value.Value);
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
				AmmoStepRow(parent, number, steps, "Fill", row =>
				{
					CellIcon(row, fill.ItemTemplate, fill.Label);
					CellText(row, fill.Label);
					if (forPut)
					{
						CellText(row, "from", Ui.Dim);
						CellIcon(row, fill.PlaceTemplate, fill.Place);
					}

					CellCount(row, have, want, true);
					CellArrow(row);
					CellCount(row, Math.Min(want, have + fill.Amount), want, true);
					CellAmmo(row, fill.Template, fill.What, fill.Why, fill.From);
					if (!forPut)
					{
						CellText(row, "(taken out and put back)", Ui.Dim);
					}
				}, AmmoPanels.Tip(fill));
			}

			if (put != null)
			{
				number++;
				AmmoStepRow(parent, number, steps, "Put in", row =>
				{
					CellIcon(row, put.Template, put.What);
					CellText(row, put.What);
					CellText(row, "from", Ui.Dim);
					CellIcon(row, put.PlaceTemplate, put.Place);
					CellText(row, "into", Ui.Dim);
					UiKit.Icon(row, Icons.OfItem(panel.Item), CellIconSize, panel.Name);
				}, put.Text);
			}

			if (chamber != null)
			{
				number++;
				AmmoStepRow(parent, number, steps, "Chamber", row =>
				{
					if (chamber.Problem)
					{
						CellText(row, AmmoPanels.ShortProblem(chamber.Detail ?? chamber.Text), Ui.Bad);
					}
					else
					{
						CellAmmo(row, chamber.Template, chamber.What, chamber.Why, string.IsNullOrEmpty(chamber.Place) ? null : new List<PlaceCount> { new PlaceCount { Template = chamber.PlaceTemplate, Name = chamber.Place, Count = 1 } });
					}
				}, AmmoPanels.Tip(chamber));
			}

			foreach (var line in other)
			{
				var shown = line;
				AmmoRow(parent, "", row => CellText(row, AmmoPanels.Short(shown.Detail ?? shown.Text), shown.Problem ? Ui.Bad : Ui.Text), shown.Text);
			}
		}

		// The spare ammo of the caliber: what is on you, or what will be brought, against what is asked; red when it can't be had
		private static void AmmoSpare(Transform parent, AmmoPanels.CaliberGroup group)
		{
			var head = group.SpareHead;
			var problem = group.Spare.FirstOrDefault(line => line.Problem);
			var bring = group.Spare.FirstOrDefault(line => line.Run != null);
			var asked = bring?.Asked ?? head.Asked;
			AmmoRow(parent, "Spare ammo", row =>
			{
				if (bring != null)
				{
					CellPlus(row);
					CellIcon(row, bring.Template, bring.What);
					CellText(row, $"{bring.Amount:N0} {bring.What}");
				}
				else if (problem == null && head.What != null)
				{
					CellIcon(row, head.WhatTemplate, head.What);
					CellText(row, $"{head.Amount:N0} {head.What} on you", Ui.Ok);
				}

				if (problem != null)
				{
					CellText(row, AmmoPanels.ShortProblem(problem.Detail ?? problem.Text), Ui.Bad);
				}
				else if (asked > 0)
				{
					CellText(row, $"asks for {asked:N0}", Ui.Dim);
				}
			}, bring?.Text ?? problem?.Text ?? "Loose ammo of this caliber on you counts toward what is asked, and so does what the top-up of the loose stacks adds.");
		}

		// The cards after the weapons: each progress line opens a card with a header (icon, title, "?", how many), its lines in an inner panel
		private static void AmmoRestLines(Transform parent, List<PlanLine> lines)
		{
			RectTransform card = null, inner = null;
			for (int index = 0; index < lines.Count; index++)
			{
				var line = lines[index];
				if (line.Standalone || line.IsProgress)
				{
					card = null;
					inner = null;
				}

				if (line.IsProgress)
				{
					var standing = line.Subject == AmmoStep.LooseAmmo ? ReadyWindow.LooseStanding(lines, index) : ReadyWindow.StandingFor(lines, index);
					card = Card(parent, standing);
					AmmoRestHeader(card, line, standing);
					continue;
				}

				if (!line.Standalone && card != null && inner == null)
				{
					inner = UiKit.VBox(card, 3f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge);
				}

				AmmoRestLine(inner ?? (RectTransform)parent, line);
			}
		}

		private static void HelpMark(Transform parent, string text) => UiKit.Tip(UiKit.Label(parent, "?", 0.8f, Ui.Dim, align: TextAlignmentOptions.Center), text);

		private static void AmmoRestHeader(Transform parent, PlanLine line, ReadyWindow.Standing standing)
		{
			// Short, nothing to do and nothing wrong (loose stacks the stash can't fill): the count is dim, not a warning
			bool left = line.Subject == AmmoStep.LooseAmmo && standing == ReadyWindow.Standing.Done && line.Have < line.Want;
			var color = left ? Ui.Dim : standing == ReadyWindow.Standing.Done ? Ui.Ok : standing == ReadyWindow.Standing.Doable ? Ui.Warn : Ui.Bad;
			var row = UiKit.HBox(parent, 8f);
			if (!string.IsNullOrEmpty(line.Template))
			{
				UiKit.Icon(row, SpriteOfTemplate(line.Template), UiKit.Unit * 2.2f, line.Subject);
			}

			UiKit.Label(row, line.Subject, 0.9f, Ui.Text);
			if (line.Subject == AmmoStep.ExtraMagazines)
			{
				HelpMark(row, "Magazines not including the ones going into your weapons.");
			}
			else if (line.Subject == AmmoStep.LooseAmmo)
			{
				HelpMark(row, "Loose stacks of ammo you carry (not in magazines or boxes), topped up to a full stack from the stash.");
			}

			UiKit.Spacer(row);
			var count = UiKit.Label(row, $"{line.Have:N0}/{line.Want:N0} {line.Unit}", 0.9f, color);
			if (left)
			{
				UiKit.Tip(count, "Some stacks can't be filled: the stash has no more of that ammo.");
			}
		}

		// Magazines that cannot be filled: what they are and where, in a red box, and why (which ammo, or none left)
		private static void StuckBox(Transform parent, PlanLine line)
		{
			var box = UiKit.VBox(parent, 4f, new RectOffset(10, 10, 6, 6), UiKit.ProblemColor);
			UiKit.Label(box, line.Subject, 0.9f, Ui.Bad);
			foreach (var mag in line.Mags)
			{
				var row = UiKit.HBox(box, 6f);
				CellIcon(row, mag.Template, mag.Name);
				CellText(row, mag.Name);
				if (!string.IsNullOrEmpty(mag.Place))
				{
					CellText(row, "in", Ui.Dim);
					CellIcon(row, mag.PlaceTemplate, mag.Place);
				}

				CellCount(row, mag.Have, mag.Want);
			}

			UiKit.Label(box, string.IsNullOrEmpty(line.Options)
				? "There is no ammo left on you or in the stash that fits them."
				: $"They could take {line.Options}, and nothing tells which one: load one of them yourself and the others follow.", 0.85f, Ui.Text, wrap: true);
			UiKit.Tip(box, line.Text);
		}

		private static void AmmoRestLine(RectTransform parent, PlanLine line)
		{
			if (line.Part == "stuck" && line.Mags.Count > 0)
			{
				StuckBox(parent, line);
				return;
			}

			if (line.Problem || line.Run == null)
			{
				DetailLine(parent, line, false);
				return;
			}

			var row = UiKit.HBox(parent, 6f);
			CellPlus(row);
			if (line.Fill && !string.IsNullOrEmpty(line.ItemTemplate))
			{
				CellIcon(row, line.ItemTemplate, line.Label);
				CellText(row, line.Label);
				CellText(row, "in", Ui.Dim);
				CellIcon(row, line.PlaceTemplate, line.Place);
				CellCount(row, line.Have, line.Want, true);
				CellArrow(row);
				CellCount(row, Math.Min(line.Want, line.Have + line.Amount), line.Want, true);
				CellAmmo(row, line.Template, line.What, line.Why, line.From);
			}
			else if (line.Topped)
			{
				CellIcon(row, line.Template, line.Label);
				CellText(row, line.Label);
				CellIcon(row, line.PlaceTemplate, line.Place);
				CellCount(row, line.Have, line.Want, true);
				CellArrow(row);
				CellCount(row, Math.Min(line.Want, line.Have + line.Amount), line.Want, true);
				CellText(row, $"+{line.Amount:N0}", Ui.Dim);
			}
			else
			{
				CellIcon(row, line.Template, line.What);
				CellText(row, AmmoPanels.Short(line.Text));
			}

			UiKit.Tip(row, AmmoPanels.Tip(line));
		}
	}
}
