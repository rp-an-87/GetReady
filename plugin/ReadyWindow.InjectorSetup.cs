using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The Setup card of the Injector case step: one template per case, like the cash cases. The cases on you come first (save or update a
	// template, delete it), then the saved ones that are not on you (they can be deleted too).
	internal static partial class ReadyWindow
	{
		// A case on you keeps its template's tag and name current
		private static void RefreshSaved(SavedCase saved, CompoundItem injectorCase)
		{
			var tag = injectorCase.GetItemComponent<TagComponent>();
			saved.CaseName = Game.NameOf(injectorCase);
			saved.Tag = tag?.Name;
			saved.TagColor = tag?.Color ?? 0;
		}

		// The icon, the tag and the text of a case, all on the middle line of the icon
		private static void InjectorHeader(string template, string name, string tag, int tagColor, string text, GUIStyle style)
		{
			Icons.Slot(template, name, 40);
			GUILayout.Space(8);
			if (!string.IsNullOrEmpty(tag))
			{
				Centered(() => TagChip.Draw(tag, tagColor));
				GUILayout.Space(8);
			}

			CenteredLabel(text, style);
			GUILayout.FlexibleSpace();
		}

		private static string Injectors(int count) => count == 1 ? "1 injector" : $"{count} injectors";

		// What a template holds, in a darker panel as wide as the fullest template needs (every panel has the same width)
		private static void TemplatePanel(SavedCase saved, string empty, float width)
		{
			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical(Ui.Inner, GUILayout.Width(width));
			TemplateStrip(saved.Cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).Select(cell => new KeyValuePair<string, string>(cell.Template, cell.Name)), empty);
			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		// The width of the panels: room for the most injectors any template holds, and for the "Saved empty" text
		internal static float PanelWidth(IEnumerable<SavedCase> templates) =>
			Mathf.Max(230f, templates.Select(saved => saved.Cells.Count).DefaultIfEmpty(0).Max() * 46f + 24f);

		private static void InjectorCaseSetup(InventoryController controller)
		{
			var templates = SettingsStore.Current.InjectorCase.Cases;
			float panelWidth = PanelWidth(templates);
			var cases = controller == null ? new List<CompoundItem>() : InjectorCaseStep.FindCases(controller.Inventory);
			foreach (var injectorCase in cases)
			{
				var saved = InjectorCaseStep.TemplateOf(injectorCase.Id.ToString());
				if (saved != null)
				{
					RefreshSaved(saved, injectorCase);
				}
			}

			var onYouIds = new HashSet<string>(cases.Select(injectorCase => injectorCase.Id.ToString()));
			var away = templates.Where(saved => !onYouIds.Contains(saved.CaseId)).ToList();
			TwoSections("injector", "ON YOU", "No injector case on you (pockets, vest, backpack or pouch).", cases.Count > 0, () =>
			{
				for (int i = 0; i < cases.Count; i++)
				{
					var injectorCase = cases[i];
					var saved = InjectorCaseStep.TemplateOf(injectorCase.Id.ToString());
					var tag = injectorCase.GetItemComponent<TagComponent>();
					GUILayout.BeginVertical(Ui.Group);
					GUILayout.BeginHorizontal();
					string name = Game.NameOf(injectorCase);
					InjectorHeader(injectorCase.TemplateId.ToString(), Game.NameOf(injectorCase), tag?.Name, tag?.Color ?? 0,
						$"{name}  -  {Injectors(injectorCase.Grids[0].Items.Count())}" + (saved != null ? "  -  saved" : ""), Ui.Plain);
					if (CenteredButton(new GUIContent(saved != null ? "Update template" : "Save as template", "Remembers which injector is in each cell of this case, and which cells are empty: getting ready makes the case exactly like this. Other injector cases are left alone."), Ui.Button, 150))
					{
						_status = $"Saved {Injectors(InjectorCaseStep.Capture(injectorCase))} from {name}.";
						RefreshSoon();
					}

					GUILayout.EndHorizontal();
					if (saved != null)
					{
						TemplatePanel(saved, "Saved empty: getting ready empties this case.", panelWidth);
						if (RemoveTemplateButton())
						{
							InjectorCaseStep.Delete(saved.CaseId);
							_status = $"Removed the template of {name}.";
							RefreshSoon();
						}
					}

					GUILayout.EndVertical();
				}
			}, "SAVED, NOT ON YOU (ignored until you carry them)", "Templates of injector cases you are not carrying will appear here.", away.Count > 0, () =>
			{
				foreach (var saved in away)
				{
					GUILayout.BeginVertical(Ui.Group);
					GUILayout.BeginHorizontal();
					InjectorHeader(string.IsNullOrEmpty(saved.CaseTemplate) ? Game.InjectorCase : saved.CaseTemplate, saved.CaseName, saved.Tag, saved.TagColor,
						$"{saved.CaseName}  -  {Injectors(saved.Cells.Count)}", Ui.Plain);
					GUILayout.EndHorizontal();
					TemplatePanel(saved, "Saved empty.", panelWidth);
					if (RemoveTemplateButton())
					{
						InjectorCaseStep.Delete(saved.CaseId);
						_status = $"Removed the template of {InjectorCaseStep.LabelOf(saved)}.";
						RefreshSoon();
						GUILayout.EndVertical();
						break;
					}

					GUILayout.EndVertical();
				}
			});

			if (templates.Count == 0)
			{
				GUILayout.Label("Nothing saved yet. Fill the case the way you like it, then save it as a template (an empty case works too: it is kept empty).", Ui.Muted);
			}
		}

		// Under the injectors a template holds: a text button like the other "Remove" buttons
		private static bool RemoveTemplateButton()
		{
			GUILayout.BeginHorizontal();
			bool pressed = GUILayout.Button(new GUIContent("Remove template", "Forgets this case's template (nothing moves)"), Ui.Pick, GUILayout.Width(130));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			return pressed;
		}
	}
}
