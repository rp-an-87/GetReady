using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;

namespace GetReady
{
	// One line of the plan: what is there, or what will be done. Run is null for lines that are only information.
	internal class PlanLine
	{
		public string Text;
		public bool Problem;
		public Func<Task<string>> Run; // null, or why it failed
	}

	// A part of getting ready. Plan reads the inventory and returns what is missing; nothing moves until the lines are run.
	internal interface IStep
	{
		string Name { get; }
		bool Enabled { get; set; }
		// What the saved template holds, in a line
		string Saved();
		List<PlanLine> Plan(InventoryController controller);
	}

	internal static class Steps
	{
		internal static readonly IStep[] All = { new SpecialSlotsStep(), new InjectorCaseStep(), new FlaresStep() };

		internal static PlanLine Info(string text) => new PlanLine { Text = text };
		internal static PlanLine Problem(string text) => new PlanLine { Text = text, Problem = true };
	}

	// CMS (or whatever you captured) in the special slots of the pockets
	internal class SpecialSlotsStep : IStep
	{
		public string Name => "Special slots";
		public bool Enabled { get => SettingsStore.Current.SpecialSlots.Enabled; set => SettingsStore.Current.SpecialSlots.Enabled = value; }

		public string Saved()
		{
			var slots = SettingsStore.Current.SpecialSlots.Slots;
			return slots.Count == 0 ? "nothing saved yet" : string.Join(", ", slots.Select(entry => $"{entry.SlotId.Replace("SpecialSlot", "slot ")}: {entry.Templates[0].Name}").ToArray());
		}

		internal static List<Slot> SlotsOf(Inventory inventory) =>
			Game.EquippedContainer(inventory, EquipmentSlot.Pockets)?.Slots.ToList() ?? new List<Slot>();

		// Records what each special slot holds now
		internal static int Capture(InventoryController controller)
		{
			var template = SettingsStore.Current.SpecialSlots;
			template.Slots.Clear();
			foreach (var slot in SlotsOf(controller.Inventory))
			{
				var item = slot.ContainedItem;
				if (item == null)
				{
					continue;
				}

				var entry = new SlotEntry { SlotId = slot.ID };
				entry.Templates.Add(new ItemRef { Template = item.TemplateId.ToString(), Name = Game.NameOf(item) });
				template.Slots.Add(entry);
			}

			SettingsStore.Save();
			return template.Slots.Count;
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var lines = new List<PlanLine>();
			var template = SettingsStore.Current.SpecialSlots;
			if (template.Slots.Count == 0)
			{
				lines.Add(Steps.Info("Nothing captured. Put the kits in the special slots and capture."));
				return lines;
			}

			var slots = SlotsOf(controller.Inventory);
			var stash = Game.Stash(controller.Inventory);
			var taken = new HashSet<Item>();
			int inPlace = 0;
			foreach (var entry in template.Slots)
			{
				var slot = slots.FirstOrDefault(candidate => candidate.ID == entry.SlotId);
				string wanted = string.Join(" / ", entry.Templates.Select(t => t.Name).ToArray());
				if (slot == null)
				{
					lines.Add(Steps.Problem($"{entry.SlotId}: this slot doesn't exist on your pockets."));
					continue;
				}

				var held = slot.ContainedItem;
				if (held != null)
				{
					if (entry.Templates.Any(t => t.Template == held.TemplateId.ToString()))
					{
						inPlace++;
					}
					else
					{
						lines.Add(Steps.Problem($"{entry.SlotId}: holds {Game.NameOf(held)}, expected {wanted}."));
					}

					continue;
				}

				Item found = null;
				foreach (var reference in entry.Templates)
				{
					found = Game.Candidates(stash.Where(item => !taken.Contains(item)), reference.Template).FirstOrDefault();
					if (found != null)
					{
						break;
					}
				}

				if (found == null)
				{
					lines.Add(Steps.Problem($"{entry.SlotId}: no {wanted} in the stash."));
					continue;
				}

				taken.Add(found);
				var chosen = found;
				var target = slot;
				lines.Add(new PlanLine
				{
					Text = $"{entry.SlotId}: bring {Game.NameOf(chosen)}.",
					Run = () => Game.PlaceAt(controller, chosen, 1, target.CreateItemAddress())
				});
			}

			lines.Insert(0, Steps.Info($"{inPlace} of {template.Slots.Count} slots in place."));
			return lines;
		}
	}

	// The injectors you always put in the same cells of the injector case
	internal class InjectorCaseStep : IStep
	{
		public string Name => "Injector case";
		public bool Enabled { get => SettingsStore.Current.InjectorCase.Enabled; set => SettingsStore.Current.InjectorCase.Enabled = value; }

		public string Saved()
		{
			var cells = SettingsStore.Current.InjectorCase.Cells;
			return cells.Count == 0 ? "nothing saved yet" : $"{cells.Count} injectors saved: " + string.Join(", ", cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).Select(cell => cell.Name).ToArray());
		}

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
			var lines = new List<PlanLine>();
			var template = SettingsStore.Current.InjectorCase;
			if (template.Cells.Count == 0)
			{
				lines.Add(Steps.Info("Nothing captured. Fill the case the way you like it and capture."));
				return lines;
			}

			var cases = FindCases(controller.Inventory);
			var injectorCase = cases.FirstOrDefault(candidate => candidate.Id.ToString() == template.CaseId);
			if (injectorCase == null)
			{
				lines.Add(Steps.Problem($"The captured case isn't on you ({cases.Count} other injector case(s) carried, left alone). Capture the one you want."));
				return lines;
			}

			var grid = injectorCase.Grids[0];
			var stash = Game.Stash(controller.Inventory);
			var taken = new HashSet<Item>();
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
						lines.Add(Steps.Problem($"Cell {cell.X},{cell.Y}: holds {Game.NameOf(held)}, expected {cell.Name}."));
					}

					continue;
				}

				var found = Game.Candidates(stash.Where(item => !taken.Contains(item)), cell.Template).FirstOrDefault();
				if (found == null)
				{
					lines.Add(Steps.Problem($"Cell {cell.X},{cell.Y}: no {cell.Name} in the stash."));
					continue;
				}

				taken.Add(found);
				var chosen = found;
				var target = cell;
				lines.Add(new PlanLine
				{
					Text = $"Cell {cell.X},{cell.Y}: bring {cell.Name}.",
					Run = () => Game.PlaceAt(controller, chosen, 1, grid.CreateItemAddress(new LocationInGrid(target.X, target.Y, ItemRotation.Horizontal)))
				});
			}

			lines.Insert(0, Steps.Info($"{inPlace} of {template.Cells.Count} injectors in place."));
			return lines;
		}
	}

	// Green flares (or another item) in the amount you want, wherever you carry them
	internal class FlaresStep : IStep
	{
		public string Name => "Flares";
		public bool Enabled { get => SettingsStore.Current.Flares.Enabled; set => SettingsStore.Current.Flares.Enabled = value; }

		public string Saved() => $"carry {SettingsStore.Current.Flares.Count} {SettingsStore.Current.Flares.Item.Name}";

		private static EquipmentSlot SlotOf(string name) => (EquipmentSlot)Enum.Parse(typeof(EquipmentSlot), name);

		public List<PlanLine> Plan(InventoryController controller)
		{
			var lines = new List<PlanLine>();
			var template = SettingsStore.Current.Flares;
			if (template.Count <= 0)
			{
				return lines;
			}

			int carried = Game.Carried(controller.Inventory).Where(item => item.TemplateId.ToString() == template.Item.Template).Sum(item => item.StackObjectsCount);
			int need = template.Count - carried;
			if (need <= 0)
			{
				lines.Add(Steps.Info($"{carried} {template.Item.Name} carried, {template.Count} wanted."));
				return lines;
			}

			var targets = template.Destinations
				.Select(name => Game.EquippedContainer(controller.Inventory, SlotOf(name)))
				.Where(container => container != null)
				.ToList();
			foreach (var stack in Game.Candidates(Game.Stash(controller.Inventory), template.Item.Template))
			{
				if (need <= 0)
				{
					break;
				}

				int count = Math.Min(need, stack.StackObjectsCount);
				need -= count;
				var chosen = stack;
				lines.Add(new PlanLine
				{
					Text = $"Bring {count} {template.Item.Name}.",
					Run = () => Place(controller, chosen, count, targets)
				});
			}

			if (need > 0)
			{
				lines.Add(Steps.Problem($"{need} {template.Item.Name} short: not enough in the stash."));
			}

			return lines;
		}

		// The first container with room takes it, as a split when the stack is bigger than what is needed
		private static async Task<string> Place(InventoryController controller, Item stack, int count, List<CompoundItem> targets)
		{
			foreach (var container in targets)
			{
				foreach (var grid in container.Grids)
				{
					var address = grid.FindLocationForItem(stack);
					if (address != null)
					{
						return await Game.PlaceAt(controller, stack, count, address);
					}
				}
			}

			return "no room in " + string.Join(", ", SettingsStore.Current.Flares.Destinations.ToArray());
		}
	}
}
