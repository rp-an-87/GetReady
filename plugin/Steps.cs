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

	// One item a template wants and the gear lacks: what it is, where it goes, and how to bring it (null when the stash has none)
	internal class Need
	{
		public string Name;
		public string Where;
		public Func<Task<string>> Run;
	}

	// A part of getting ready. Plan reads the inventory and returns what is missing; nothing moves until the lines are run.
	internal interface IStep
	{
		string Name { get; }
		bool Enabled { get; set; }
		// What the saved template holds, in a line
		string Saved();
		// The amounts and destinations the window lets you set (none for the steps that work from a saved template)
		IReadOnlyList<GroupDef> Groups { get; }
		List<PlanLine> Plan(InventoryController controller);
	}

	internal static class Steps
	{
		internal static readonly IStep[] All =
		{
			new SpecialSlotsStep(), new InjectorCaseStep(), new HealingStep(), new GrenadesStep(),
			// Found by what the item treats, so modded splints count too: anything that fixes a fracture without doing surgery
			// (Surv12 does both and counts as surgery only); Grizzly fixes fractures and counts as a splint
			new MissingResourceWarning("Splint warning", "splint", s => s.Splint, (s, on) => s.Splint = on,
				item => Treats(item, EDamageEffectType.Fracture) && !Treats(item, EDamageEffectType.DestroyedPart)),
			new MissingResourceWarning("Surgery warning", "surgery kit", s => s.Surgery, (s, on) => s.Surgery = on,
				item => Treats(item, EDamageEffectType.DestroyedPart))
		};

		private static bool Treats(Item item, EDamageEffectType effect) =>
			item.GetItemComponent<HealthEffectsComponent>()?.AffectsAny(effect) ?? false;

		// Stash items an earlier step has already planned to bring this pass, so a later step doesn't bring them a second time
		// (a Propital for the injector case also counts for the Propital supply). Cleared before every planning pass.
		internal static readonly List<Item> Claimed = new List<Item>();

		// Items already in a special slot of the template (managed by that step, so the supplies leave them alone but count
		// them) and the ones it is about to swap out (which stop counting)
		internal static readonly List<Item> Owned = new List<Item>();
		internal static readonly List<Item> Leaving = new List<Item>();

		internal static void NewPass()
		{
			Claimed.Clear();
			Owned.Clear();
			Leaving.Clear();
		}

		internal static PlanLine Info(string text) => new PlanLine { Text = text };
		internal static PlanLine Problem(string text) => new PlanLine { Text = text, Problem = true };

		// " (slot 4, slot 5)", or nothing when the items have no place worth naming
		private static string WhereOf(IEnumerable<Need> needs)
		{
			var places = needs.Where(need => need.Where != null).Select(need => need.Where).ToArray();
			return places.Length == 0 ? "" : $" ({string.Join(", ", places)})";
		}

		// "2/3 in place", then what to bring grouped by name ("Bring 2x Propital (cells 0,0, 1,0)"), then what the stash lacks
		internal static List<PlanLine> Summarize(int inPlace, int total, string unit, List<Need> needs, List<PlanLine> others)
		{
			var lines = new List<PlanLine> { Info($"{inPlace}/{total} {unit} in place") };
			foreach (var group in needs.Where(need => need.Run != null).GroupBy(need => need.Name))
			{
				var runs = group.Select(need => need.Run).ToList();
				lines.Add(new PlanLine
				{
					Text = $"Bring {group.Count()}x {group.Key}{WhereOf(group)}",
					Run = async () =>
					{
						foreach (var run in runs)
						{
							var error = await run();
							if (error != null)
							{
								return error;
							}
						}

						return null;
					}
				});
			}

			foreach (var group in needs.Where(need => need.Run == null).GroupBy(need => need.Name))
			{
				lines.Add(Problem($"Not in the stash: {group.Count()}x {group.Key}{WhereOf(group)}"));
			}

			lines.AddRange(others);
			return lines;
		}
	}

	// Warns when you carry none of a kind of item (splints, surgery kits), counting what the other steps are about to bring
	internal class MissingResourceWarning : IStep
	{
		private readonly string _kind;
		private readonly Func<WarningSettings, bool> _get;
		private readonly Action<WarningSettings, bool> _set;
		private readonly Func<Item, bool> _matches;

		public MissingResourceWarning(string name, string kind, Func<WarningSettings, bool> get, Action<WarningSettings, bool> set, Func<Item, bool> matches)
		{
			Name = name;
			_kind = kind;
			_get = get;
			_set = set;
			_matches = matches;
		}

		public string Name { get; }
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => _get(SettingsStore.Current.Warnings); set => _set(SettingsStore.Current.Warnings, value); }
		public string Saved() => "warns when you carry none";

		public List<PlanLine> Plan(InventoryController controller)
		{
			int count = Game.CarriedAndSecured(controller.Inventory).Concat(Steps.Claimed).Count(_matches);
			return new List<PlanLine>
			{
				count > 0 ? Steps.Info($"{count} {_kind}(s) on you") : Steps.Problem($"Missing {_kind} resources: you carry none.")
			};
		}
	}

	// What goes in the special slots of the pockets: CMS kits, a flare, whatever you saved
	internal class SpecialSlotsStep : IStep
	{
		public string Name => "Special slots";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.SpecialSlots.Enabled; set => SettingsStore.Current.SpecialSlots.Enabled = value; }

		public string Saved()
		{
			var slots = SettingsStore.Current.SpecialSlots.Slots;
			return slots.Count == 0
				? "nothing saved yet"
				: string.Join(", ", slots.Select(entry => $"{entry.SlotId.Replace("SpecialSlot", "slot ")}: {entry.Templates[0].Name}").ToArray());
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

				var entry = new SlotEntry { SlotId = slot.ID, Count = item.StackObjectsCount };
				entry.Templates.Add(new ItemRef { Template = item.TemplateId.ToString(), Name = Game.NameOf(item) });
				template.Slots.Add(entry);
			}

			SettingsStore.Save();
			return template.Slots.Count;
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var template = SettingsStore.Current.SpecialSlots;
			if (template.Slots.Count == 0)
			{
				return new List<PlanLine> { Steps.Info("Nothing saved. Put the items in the special slots and save them.") };
			}

			var slots = SlotsOf(controller.Inventory);
			var stash = Game.Stash(controller.Inventory);
			var taken = new HashSet<Item>();
			var needs = new List<Need>();
			var others = new List<PlanLine>();
			int inPlace = 0;
			foreach (var entry in template.Slots)
			{
				var slot = slots.FirstOrDefault(candidate => candidate.ID == entry.SlotId);
				string slotName = entry.SlotId.Replace("SpecialSlot", "slot ");
				string wanted = string.Join(" / ", entry.Templates.Select(t => t.Name).ToArray());
				if (slot == null)
				{
					others.Add(Steps.Problem($"{slotName}: this slot doesn't exist on your pockets."));
					continue;
				}

				var held = slot.ContainedItem;
				if (held != null)
				{
					if (entry.Templates.Any(t => t.Template == held.TemplateId.ToString()))
					{
						Steps.Owned.Add(held);
					}

					if (!entry.Templates.Any(t => t.Template == held.TemplateId.ToString()))
					{
						others.Add(Steps.Problem($"{slotName}: holds {Game.NameOf(held)}, expected {wanted}."));
					}
					else if (held.StackObjectsCount < entry.Count)
					{
						others.Add(Steps.Problem($"{slotName}: holds {held.StackObjectsCount}x {Game.NameOf(held)}, expected {entry.Count} (topping up a stack isn't supported yet)."));
					}
					else if (Game.MaxResourceOf(held) > 1f && Game.ResourceOf(held) < Game.MaxResourceOf(held))
					{
						// A partly used kit (CMS 2/3): swap it for the fullest one in the stash
						var current = held;
						var fuller = Game.Candidates(stash.Where(item => !taken.Contains(item) && !Steps.Claimed.Contains(item)), entry.Templates.First(t => t.Template == current.TemplateId.ToString()).Template)
							.FirstOrDefault(item => Game.ResourceOf(item) > Game.ResourceOf(current));
						string usage = $"{Game.NameOf(held)} {Game.ResourceOf(held):0}/{Game.MaxResourceOf(held):0}";
						if (fuller == null)
						{
							others.Add(Steps.Problem($"{slotName}: {usage}, the stash has no fuller one."));
							inPlace++;
						}
						else
						{
							taken.Add(fuller);
							Steps.Claimed.Add(fuller);
							Steps.Leaving.Add(held);
							var better = fuller;
							var target = slot;
							others.Add(new PlanLine
							{
								Text = $"{slotName}: replace {usage} with {Game.NameOf(better)} {Game.ResourceOf(better):0}/{Game.MaxResourceOf(better):0}",
								Run = async () =>
								{
									var error = await Game.MoveToContainers(controller, current, new List<CompoundItem> { controller.Inventory.Stash });
									if (error != null)
									{
										return error;
									}

									error = await Game.PlaceAt(controller, better, 1, target.CreateItemAddress());
									if (error != null)
									{
										await Game.PlaceAt(controller, current, 1, target.CreateItemAddress());
									}

									return error;
								}
							});
							inPlace++;
						}
					}
					else
					{
						inPlace++;
					}

					continue;
				}

				Item found = null;
				foreach (var reference in entry.Templates)
				{
					found = Game.Candidates(stash.Where(item => !taken.Contains(item) && !Steps.Claimed.Contains(item)), reference.Template).FirstOrDefault();
					if (found != null)
					{
						break;
					}
				}

				var need = new Need { Name = found != null ? Game.NameOf(found) : entry.Templates[0].Name, Where = slotName };
				if (found != null)
				{
					taken.Add(found);
					Steps.Claimed.Add(found);
					var chosen = found;
					var target = slot;
					int count = Math.Min(entry.Count, found.StackObjectsCount);
					need.Run = () => Game.PlaceAt(controller, chosen, count, target.CreateItemAddress());
				}

				needs.Add(need);
			}

			return Steps.Summarize(inPlace, template.Slots.Count, "slots", needs, others);
		}
	}

	// The injectors you always put in the same cells of the injector case
	internal class InjectorCaseStep : IStep
	{
		public string Name => "Injector case";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.InjectorCase.Enabled; set => SettingsStore.Current.InjectorCase.Enabled = value; }

		public string Saved()
		{
			var cells = SettingsStore.Current.InjectorCase.Cells;
			return cells.Count == 0
				? "nothing saved yet"
				: $"{cells.Count} injectors saved: " + string.Join(", ", cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).Select(cell => cell.Name).ToArray());
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
						others.Add(Steps.Problem($"Cell {cell.X},{cell.Y}: holds {Game.NameOf(held)}, expected {cell.Name}."));
					}

					continue;
				}

				var found = Game.Candidates(stash.Where(item => !taken.Contains(item) && !Steps.Claimed.Contains(item)), cell.Template).FirstOrDefault();
				var need = new Need { Name = cell.Name };
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
