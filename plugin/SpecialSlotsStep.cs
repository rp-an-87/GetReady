using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace GetReady
{
	// What goes in the special slots of the pockets: CMS kits, a flare, whatever you saved
	internal class SpecialSlotsStep : IStep
	{
		public string Name => "Special slots";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.SpecialSlots.Enabled; set => SettingsStore.Current.SpecialSlots.Enabled = value; }

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
					others.Add(Steps.Problem(slotName, "this slot doesn't exist on your pockets", entry.Templates[0].Template));
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
						others.Add(Steps.Problem(slotName, $"holds {Game.NameOf(held)}, expected {wanted}", entry.Templates[0].Template));
					}
					else if (held.StackObjectsCount < entry.Count)
					{
						others.Add(Steps.Problem(slotName, $"holds {held.StackObjectsCount}x {Game.NameOf(held)}, expected {entry.Count} (topping up a stack isn't supported yet)", entry.Templates[0].Template));
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
							others.Add(Steps.Problem(slotName, $"{usage}: no fuller one left in the stash", entry.Templates[0].Template));
							inPlace++;
						}
						else
						{
							taken.Add(fuller);
							Steps.Claimed.Add(fuller);
						Steps.Bringing.Add(fuller);
							Steps.Leaving.Add(held);
							var better = fuller;
							var target = slot;
							others.Add(new PlanLine
							{
								Text = $"{slotName}: replace {usage} with {Game.NameOf(better)} {Game.ResourceOf(better):0}/{Game.MaxResourceOf(better):0}",
								Run = async () =>
								{
									var error = await Game.MoveTo(controller, current, new[] { "Stash" });
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

				var need = new Need { Name = found != null ? Game.NameOf(found) : entry.Templates[0].Name, Where = slotName, Template = entry.Templates[0].Template };
				if (found != null)
				{
					taken.Add(found);
					Steps.Claimed.Add(found);
					Steps.Bringing.Add(found);
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
}
