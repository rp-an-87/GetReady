using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace GetReady
{
	// Repairs the armor you wear (and your weapons, when switched on) with your repair kits. Pieces on the no-repair list are left as they are (that list holds the very
	// pieces, by item id: a new armor of the same type is repaired normally).
	internal class RepairStep : IStep
	{
		public string Name => "Repair";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.Repair.Enabled; set => SettingsStore.Current.Repair.Enabled = value; }

		private static bool IsArmor(Item item) =>
			item.GetItemComponent<ArmorHolderComponent>() != null || item.GetItemComponentsInChildren<ArmorComponent>(true).Any();

		// What you wear that is armor (vest, helmet, armored rig, masks with armor) and, when switched on, the weapons in your weapon slots
		internal static List<Item> EquippedGear(Inventory inventory)
		{
			bool armor = SettingsStore.Current.Repair.Armor;
			bool weapons = SettingsStore.Current.Repair.Weapons;
			return inventory.Equipment.Slots.Select(slot => slot.ContainedItem).Where(item => item != null && ((armor && IsArmor(item)) || (weapons && item is Weapon))).ToList();
		}

		// The traders that repair, in the order set in Setup (new ones at the end)
		internal static List<EFT.Trading.Trader> OrderedTraders(EFT.Repairing.RepairController repairController) => OrderedTraders(repairController.TraderRepairers);

		internal static List<EFT.Trading.Trader> OrderedTraders(IEnumerable<EFT.Trading.Trader> traders)
		{
			var order = SettingsStore.Current.Repair.TraderOrder;
			return traders
				.Select((trader, index) => new { trader, key = order.IndexOf(trader.Id) >= 0 ? order.IndexOf(trader.Id) : 1000 + index })
				.OrderBy(pair => pair.key)
				.Select(pair => pair.trader)
				.ToList();
		}

		internal static void MoveTrader(EFT.Repairing.RepairController repairController, string traderId, int step) => MoveTrader(repairController.TraderRepairers, traderId, step);

		internal static void MoveTrader(IEnumerable<EFT.Trading.Trader> traders, string traderId, int step)
		{
			var ids = OrderedTraders(traders).Select(trader => trader.Id).ToList();
			int index = ids.IndexOf(traderId);
			int target = index + step;
			if (index < 0 || target < 0 || target >= ids.Count)
			{
				return;
			}

			ids.RemoveAt(index);
			ids.Insert(target, traderId);
			SettingsStore.Current.Repair.TraderOrder = ids;
		}

		internal static bool IsKept(Item item) => SettingsStore.Current.Repair.KeepDamaged.Any(kept => kept.Id == item.Id.ToString());

		internal static void ToggleKept(Item item)
		{
			var list = SettingsStore.Current.Repair.KeepDamaged;
			if (list.RemoveAll(kept => kept.Id == item.Id.ToString()) == 0)
			{
				list.Add(new KeptItem { Id = item.Id.ToString(), Template = item.TemplateId.ToString(), Name = Game.NameOf(item) });
			}
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var repairController = RepairWithKit.Controller();
			if (repairController == null)
			{
				return new List<PlanLine> { Steps.Info("Repair isn't available on this screen.") };
			}

			if (!SettingsStore.Current.Repair.Armor && !SettingsStore.Current.Repair.Weapons)
			{
				return new List<PlanLine> { Steps.Info("Armor and weapons are both switched off.") };
			}

			var armor = EquippedGear(controller.Inventory);
			if (armor.Count == 0)
			{
				return new List<PlanLine> { Steps.Info("No armor or weapon equipped.") };
			}

			var lines = new List<PlanLine>();
			int inShape = 0;
			int spent = 0; // roubles the traders in this plan will already take
			foreach (var item in armor)
			{
				var name = Game.NameOf(item);
				var template = item.TemplateId.ToString();
				// The same decision the R key makes (kits, else a trader, else a problem), here with the roubles earlier pieces already take
				var decision = RepairWithKit.Decide(item, repairController, controller, spent);
				if (decision.Kind == RepairWithKit.RepairKind.Nothing)
				{
					inShape++;
				}
				else if (IsKept(item))
				{
					// Excluded from repairs: counts as fine
					inShape++;
					lines.Add(new PlanLine { Text = $"{name}: excluded from repairs (on your no-repair list)", Subject = name, Detail = "excluded from repairs (on your no-repair list)", Template = template });
				}
				else if (decision.Kind == RepairWithKit.RepairKind.Problem)
				{
					lines.Add(Steps.Problem(name, decision.Problem, template));
				}
				else
				{
					var piece = item;
					bool trader = decision.Kind == RepairWithKit.RepairKind.Trader;
					if (trader)
					{
						spent += decision.Quote.Price;
					}

					lines.Add(new PlanLine
					{
						Text = trader ? $"Repair {name} at {RepairWithKit.NameOfTrader(decision.Quote.Trader)} for {decision.Quote.Price:N0} roubles" : $"Repair {name}",
						Template = template,
						Repair = true,
						Items = 0,
						Run = async () => RepairWithKit.Shortfall(decision, await RepairWithKit.Execute(decision, repairController, controller, piece))
					});
				}
			}

			Steps.Spent += spent;

			// Shown with the first piece that needs work, else the first piece
			var shown = armor.FirstOrDefault(piece => lines.Any(line => line.Template == piece.TemplateId.ToString())) ?? armor[0];
			lines.Insert(0, Steps.Progress("Gear", inShape, armor.Count, "in shape", shown.TemplateId.ToString()).WithItem(shown));
			foreach (var line in lines.Where(line => line.Item == null && line.Template != null))
			{
				// The icon of the very piece (plates and mods on it), found again from the template among what is worn
				line.Item = armor.FirstOrDefault(piece => piece.TemplateId.ToString() == line.Template);
			}
			return lines;
		}
	}
}
