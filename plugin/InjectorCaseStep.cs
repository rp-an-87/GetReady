using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace GetReady
{
	// The injectors you always put in the same cells of the injector case
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

		// Records the injectors in the case and binds the template to this very case
		internal static int Capture(CompoundItem injectorCase)
		{
			var template = SettingsStore.Current.InjectorCase;
			template.CaseId = injectorCase.Id.ToString();
			template.Cells.Clear();
			var grid = injectorCase.Grids[0];
			foreach (var item in grid.Items)
			{
				var location = grid.GetItemLocation(item);
				template.Cells.Add(new CaseCell { X = location.x, Y = location.y, Template = item.TemplateId.ToString(), Name = Game.NameOf(item) });
			}

			SettingsStore.Save();
			return template.Cells.Count;
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var template = SettingsStore.Current.InjectorCase;
			if (template.Cells.Count == 0)
			{
				return new List<PlanLine> { Steps.Info("Nothing saved. Fill the case the way you like it and save it.") };
			}

			var cases = FindCases(controller.Inventory);
			var injectorCase = cases.FirstOrDefault(candidate => candidate.Id.ToString() == template.CaseId);
			if (injectorCase == null)
			{
				return new List<PlanLine> { Steps.Problem($"The saved case isn't on you ({cases.Count} other injector case(s) carried, left alone). Save the one you want.") };
			}

			var grid = injectorCase.Grids[0];
			var stash = Game.Stash(controller.Inventory);
			var taken = new HashSet<Item>();
			var needs = new List<Need>();
			var others = new List<PlanLine>();
			int inPlace = 0;
			foreach (var cell in template.Cells)
			{
				var held = InCell(grid, cell.X, cell.Y);
				if (held != null)
				{
					if (held.TemplateId.ToString() == cell.Template)
					{
						inPlace++;
					}
					else
					{
						// Something else sits there: it goes back to the stash and the right injector takes its place
						var replacement = Game.Candidates(stash.Where(item => !taken.Contains(item) && !Steps.Claimed.Contains(item)), cell.Template).FirstOrDefault();
						if (replacement == null)
						{
							others.Add(Steps.Problem($"Cell {cell.X},{cell.Y}", $"holds {Game.NameOf(held)}, expected {cell.Name} and the stash has none", cell.Template));
						}
						else
						{
							taken.Add(replacement);
							Steps.Claimed.Add(replacement);
							var wrong = held;
							var right = replacement;
							var target = cell;
							needs.Add(new Need
							{
								Name = cell.Name,
								Template = cell.Template,
								Where = $"replaces {Game.NameOf(held)}",
								Run = async () =>
								{
									var slot = controller.Inventory.Stash.Grids[0].FindLocationForItem(wrong);
									if (slot == null)
									{
										return $"no room in the stash for the {Game.NameOf(wrong)} to move out";
									}

									var error = await Game.PlaceAt(controller, wrong, wrong.StackObjectsCount, slot);
									return error ?? await Game.PlaceAt(controller, right, 1, grid.CreateItemAddress(new LocationInGrid(target.X, target.Y, ItemRotation.Horizontal)));
								}
							});
						}
					}

					continue;
				}

				var found = Game.Candidates(stash.Where(item => !taken.Contains(item) && !Steps.Claimed.Contains(item)), cell.Template).FirstOrDefault();
				var need = new Need { Name = cell.Name, Template = cell.Template };
				if (found != null)
				{
					taken.Add(found);
					Steps.Claimed.Add(found);
					var chosen = found;
					var target = cell;
					need.Run = () => Game.PlaceAt(controller, chosen, 1, grid.CreateItemAddress(new LocationInGrid(target.X, target.Y, ItemRotation.Horizontal)));
				}

				needs.Add(need);
			}

			return Steps.Summarize(inPlace, template.Cells.Count, "injectors", needs, others);
		}
	}
}
