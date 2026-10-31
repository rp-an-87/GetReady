using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace GetReady
{
	// The injectors you always put in the same cells of an injector case: one template per case, exact (a cell that is not in the
	// template is emptied, a case saved empty is kept empty). A case with no template is left alone, and a template whose case is not
	// on you is ignored.
	internal class InjectorCaseStep : IStep
	{
		public string Name => "Injector case";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.InjectorCase.Enabled; set => SettingsStore.Current.InjectorCase.Enabled = value; }

		// Injector cases you carry, in the pockets, vest, backpack or the secured container
		internal static List<CompoundItem> FindCases(Inventory inventory) =>
			Game.CarriedAndSecured(inventory).Where(item => item.TemplateId.ToString() == Game.InjectorCase).OfType<CompoundItem>().ToList();

		internal static Item InCell(Grid grid, int x, int y) =>
			grid.Items.FirstOrDefault(item =>
			{
				var location = grid.GetItemLocation(item);
				return location.x == x && location.y == y;
			});

		internal static SavedCase TemplateOf(string caseId) => SettingsStore.Current.InjectorCase.Cases.FirstOrDefault(saved => saved.CaseId == caseId);

		// What a case is called: its tag when it has one, else its name
		internal static string LabelOf(SavedCase saved) => !string.IsNullOrEmpty(saved.Tag) ? saved.Tag : string.IsNullOrEmpty(saved.CaseName) ? "Injector case" : saved.CaseName;

		// Records the injectors in this case (none at all is a template too: the case is kept empty); the template of another case is untouched
		internal static int Capture(CompoundItem injectorCase)
		{
			var cases = SettingsStore.Current.InjectorCase.Cases;
			var id = injectorCase.Id.ToString();
			var saved = cases.FirstOrDefault(candidate => candidate.CaseId == id);
			if (saved == null)
			{
				saved = new SavedCase { CaseId = id };
				cases.Add(saved);
			}

			var tag = injectorCase.GetItemComponent<TagComponent>();
			saved.CaseTemplate = injectorCase.TemplateId.ToString();
			saved.CaseName = Game.NameOf(injectorCase);
			saved.Tag = tag?.Name;
			saved.TagColor = tag?.Color ?? 0;
			saved.Cells.Clear();
			var grid = injectorCase.Grids[0];
			foreach (var item in grid.Items)
			{
				var location = grid.GetItemLocation(item);
				saved.Cells.Add(new CaseCell { X = location.x, Y = location.y, Template = item.TemplateId.ToString(), Name = Game.NameOf(item) });
			}

			SettingsStore.Save();
			return saved.Cells.Count;
		}

		// Forgets the template of a case, whether or not it is on you
		internal static void Delete(string caseId)
		{
			SettingsStore.Current.InjectorCase.Cases.RemoveAll(saved => saved.CaseId == caseId);
			SettingsStore.Save();
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var templates = SettingsStore.Current.InjectorCase.Cases;
			if (templates.Count == 0)
			{
				return new List<PlanLine> { Steps.Info("Nothing saved. Fill the case the way you like it and save it (save it empty to keep a case empty).") };
			}

			var inventory = Game.InventoryOf(controller);
			var cases = FindCases(inventory);
			var present = templates.Select(saved => new { Saved = saved, Case = cases.FirstOrDefault(candidate => candidate.Id.ToString() == saved.CaseId) }).Where(pair => pair.Case != null).ToList();
			if (present.Count == 0)
			{
				return new List<PlanLine> { Steps.Info("None of the saved injector cases is on you.") };
			}

			var stash = Game.Stash(inventory);
			var taken = new HashSet<Item>();
			var lines = new List<PlanLine>();
			foreach (var pair in present)
			{
				var title = templates.Count == 1 ? null : "Injectors (" + LabelOf(pair.Saved) + ")";
				lines.AddRange(PlanCase(controller, pair.Saved, pair.Case, stash, taken, title));
			}

			return lines;
		}

		private static List<PlanLine> PlanCase(InventoryController controller, SavedCase saved, CompoundItem injectorCase, List<Item> stash, HashSet<Item> taken, string title)
		{
			var grid = injectorCase.Grids[0];
			var needs = new List<Need>();
			var others = new List<PlanLine>();
			int inPlace = 0;
			int total = saved.Cells.Count;
			Func<CaseCell, Item> heldIn = cell => InCell(grid, cell.X, cell.Y);

			// What is in the case already can be put where the template wants it before anything is taken from the stash: the injectors in
			// a cell the template leaves empty (strays), and the ones in a cell that wants something else (wrong)
			var strays = grid.Items.ToList().Where(item =>
			{
				var spot = grid.GetItemLocation(item);
				return !saved.Cells.Any(cell => cell.X == spot.x && cell.Y == spot.y);
			}).ToList();
			var wrong = saved.Cells.Select(heldIn).Where(item => item != null).Zip(saved.Cells.Where(cell => heldIn(cell) != null), (item, cell) => new { item, cell })
				.Where(pair => pair.item.TemplateId.ToString() != pair.cell.Template).Select(pair => pair.item).ToList();
			var pool = strays.Concat(wrong).ToList();
			var used = new HashSet<Item>(); // taken from where they are for another cell: their own cell is empty by then

			foreach (var cell in saved.Cells)
			{
				var held = heldIn(cell);
				if (held != null && used.Contains(held))
				{
					held = null;
				}

				if (held != null && held.TemplateId.ToString() == cell.Template)
				{
					inPlace++;
					continue;
				}

				// The injector the template wants here: already in the case (not the one that sits here), else from the stash
				var inCase = pool.FirstOrDefault(item => item != held && !used.Contains(item) && item.TemplateId.ToString() == cell.Template);
				Item source = inCase;
				if (inCase != null)
				{
					used.Add(inCase);
				}
				else
				{
					source = Game.Candidates(stash.Where(item => !taken.Contains(item) && !Steps.Claimed.Contains(item)), cell.Template).FirstOrDefault();
					if (source != null)
					{
						taken.Add(source);
						Steps.Claimed.Add(source);
					}
				}

				var target = cell;
				var chosen = source;
				if (held == null)
				{
					if (chosen == null)
					{
						needs.Add(new Need { Name = cell.Name, Template = cell.Template });
					}
					else if (inCase != null)
					{
						others.Add(new PlanLine
						{
							Text = $"Move {Game.NameOf(chosen)} to cell {cell.X},{cell.Y} of {LabelOf(saved)}",
							Template = cell.Template,
							Items = 1,
							Run = () => Game.PlaceAt(controller, chosen, 1, grid.CreateItemAddress(new LocationInGrid(target.X, target.Y, ItemRotation.Horizontal)))
						}.WithItem(chosen));
					}
					else
					{
						needs.Add(new Need
						{
							Name = cell.Name,
							Template = cell.Template,
							Run = () => Game.PlaceAt(controller, chosen, 1, grid.CreateItemAddress(new LocationInGrid(target.X, target.Y, ItemRotation.Horizontal)))
						});
					}

					continue;
				}

				// Something else sits here: it goes to the stash and the right injector takes its place
				if (chosen == null)
				{
					others.Add(Steps.Problem($"Cell {cell.X},{cell.Y}", $"holds {Game.NameOf(held)}, expected {cell.Name} and the stash has none", cell.Template));
					continue;
				}

				var wrongOne = held;
				needs.Add(new Need
				{
					Name = cell.Name,
					Template = cell.Template,
					Where = $"replaces {Game.NameOf(held)}",
					Run = async () =>
					{
						var slot = Game.InventoryOf(controller).Stash.Grids[0].FindLocationForItem(wrongOne);
						if (slot == null)
						{
							return $"no room in the stash for the {Game.NameOf(wrongOne)} to move out";
						}

						var error = await Game.PlaceAt(controller, wrongOne, wrongOne.StackObjectsCount, slot);
						return error ?? await Game.PlaceAt(controller, chosen, 1, grid.CreateItemAddress(new LocationInGrid(target.X, target.Y, ItemRotation.Horizontal)));
					}
				});
			}

			// The case holds exactly the template: what sits in a cell the template leaves empty, and nothing wants, goes to the stash
			foreach (var stray in strays.Where(item => !used.Contains(item)))
			{
				total++;
				var surplus = stray;
				others.Add(new PlanLine
				{
					Text = $"Take {Game.NameOf(stray)} out of {LabelOf(saved)} to the stash",
					Template = stray.TemplateId.ToString(),
					Items = 1,
					Run = () => Game.MoveTo(controller, surplus, new[] { "Stash" })
				}.WithItem(stray));
			}

			return Steps.Summarize(inPlace, total, "injectors", needs, others, title);
		}
	}
}
