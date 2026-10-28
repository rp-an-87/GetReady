using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Diz.LanguageExtensions;

namespace GetReady
{
	// Moving items: one way for everything.
	internal static partial class Game
	{
		// ---- Placing items: one way for everything. A place is a name (Special slots, Pockets, Vest, Backpack, Pouch, Stash or one of
		// the cases); it resolves to containers of yours, and an item goes to the first spot found in them.

		// Cases that hold money, as places an item can go: carried or in the secured container (the pouch)
		internal static readonly Dictionary<string, string> CasePlaces = new Dictionary<string, string>
		{
			{ "Wallet", "5783c43d2459774bbe137486" },
			{ "WZ wallet", "60b0f6c058e0b0481a09ad11" },
			{ "Docs case", "590c60fc86f77412b13fddcf" },
			{ "SICC case", "5d235bb686f77443f4331278" }
		};

		// A container of yours and which of its parts an item may go into
		private class Target
		{
			public CompoundItem Container;
			public bool Grids = true;
			public bool Slots;
		}

		private static List<Target> TargetsOf(Inventory inventory, string place)
		{
			string caseTemplate;
			if (CasePlaces.TryGetValue(place, out caseTemplate))
			{
				return CarriedAndSecured(inventory).OfType<CompoundItem>().Where(container => container.TemplateId.ToString() == caseTemplate).Select(container => new Target { Container = container }).ToList();
			}

			switch (place)
			{
				case "Special slots":
					return One(EquippedContainer(inventory, EquipmentSlot.Pockets), false, true);
				case "Pockets":
					return One(EquippedContainer(inventory, EquipmentSlot.Pockets), true, true);
				case "Vest":
					return One(EquippedContainer(inventory, EquipmentSlot.TacticalVest), true, false).Concat(One(EquippedContainer(inventory, EquipmentSlot.ArmorVest), true, false)).ToList();
				case "Backpack":
					return One(EquippedContainer(inventory, EquipmentSlot.Backpack), true, false);
				case "Pouch":
					return One(EquippedContainer(inventory, EquipmentSlot.SecuredContainer), true, false);
				case "Stash":
					return One(inventory.Stash, true, false);
				default:
					return new List<Target>();
			}
		}

		private static List<Target> One(CompoundItem container, bool grids, bool slots) =>
			container == null ? new List<Target>() : new List<Target> { new Target { Container = container, Grids = grids, Slots = slots } };

		// Moves count of the item to the address (the whole stack as a move, else a split), checked but not run
		private static OperationResult Simulate(InventoryController controller, Item item, int count, ItemAddress address) =>
			item.StackObjectsCount == count
				? (OperationResult)ItemManipulator.Move(item, address, controller, true)
				: ItemManipulator.SplitExact(item, count, address, controller, controller, true);

		private static async Task<string> Run(InventoryController controller, OperationResult operation)
		{
			var outcome = await controller.TryRunNetworkTransaction(operation);
			return outcome.Failed ? outcome.Error.ToString() : null;
		}

		// Adds count of the source stack to the target stack, checked but not run. The one place that does it, for the cash going into
		// a wallet and the ammo topping up a stack. The game's "transfer exactly" never moves a whole stack (it caps the amount one
		// below the stack's count, "you can only transfer 10" for 11), so a whole stack is merged.
		private static OperationResult MergeOperation(InventoryController controller, Item source, int count, Item target) =>
			count >= source.StackObjectsCount
				? (OperationResult)ItemManipulator.Merge(source, target, controller, true)
				: ItemManipulator.TransferExact(source, count, target, controller, true);

		// Adds count of the source stack to the target stack. Returns null, or why it failed.
		internal static async Task<string> MergeInto(InventoryController controller, Item source, int count, Item target)
		{
			var operation = MergeOperation(controller, source, count, target);
			return operation.Failed ? operation.Error.ToString() : await Run(controller, operation);
		}

		// To an exact address (a cell of the injector case, a special slot). Returns null, or why it failed.
		internal static async Task<string> PlaceAt(InventoryController controller, Item item, int count, ItemAddress address)
		{
			var operation = Simulate(controller, item, count, address);
			return operation.Failed ? operation.Error.ToString() : await Run(controller, operation);
		}

		// Into the first of the containers that has a spot: added to a stack of the same item when there is room for all of it
		// (and it is the same kind: adding new money to a found-in-raid stack would make the whole stack lose its FiR status),
		// else a free spot of a grid, else a free slot. The game's own "quick move" is not used: it falls back to the stash (a
		// "successful" move inside the stash) and stops at the first failure in a container.
		private static async Task<string> PlaceInto(InventoryController controller, Item item, int count, List<Target> targets)
		{
			string last = "no room";
			var template = item.TemplateId.ToString();
			foreach (var target in targets)
			{
				var container = target.Container;
				OperationResult operation = default;
				bool planned = false;

				var existing = item.StackMaxSize > 1
					? container.GetAllItems().FirstOrDefault(other => other != container && other != item && other.TemplateId.ToString() == template
						&& other.PinLockState != EItemPinLockState.Locked && other.StackMaxSize - other.StackObjectsCount >= count
						&& other.MarkedAsSpawnedInSession == item.MarkedAsSpawnedInSession)
					: null;
				if (existing != null)
				{
					operation = MergeOperation(controller, item, count, existing);
					planned = true;
				}

				if ((!planned || operation.Failed) && target.Grids)
				{
					foreach (var grid in container.Grids)
					{
						var address = grid.FindLocationForItem(item);
						if (address == null)
						{
							continue;
						}

						operation = Simulate(controller, item, count, address);
						planned = true;
						if (operation.Succeeded)
						{
							break;
						}
					}
				}

				if ((!planned || operation.Failed) && target.Slots)
				{
					foreach (var slot in container.Slots)
					{
						if (slot.ContainedItem != null)
						{
							continue;
						}

						operation = Simulate(controller, item, count, slot.CreateItemAddress());
						planned = true;
						if (operation.Succeeded)
						{
							break;
						}
					}
				}

				if (!planned)
				{
					last = $"no free spot in {container.ShortName.Localized()}";
					continue;
				}

				if (operation.Failed)
				{
					last = operation.Error.ToString();
					continue;
				}

				var error = await Run(controller, operation);
				if (error == null)
				{
					return null;
				}

				last = error;
			}

			return last;
		}

		internal const string Roubles = "5449016a4bdc2d6f028b456f";
		internal const string Euros = "569668774bdc2da2298b4568";
		internal const string Dollars = "5696686a4bdc2da3298b456a";

		internal static string CurrencyOf(string template) =>
			template == Roubles ? "RUB" : template == Euros ? "EUR" : template == Dollars ? "USD" : null;

		// The money of this template is kept out of this very case (the cash exclusions of the Setup page)
		internal static bool IsCashExcluded(string template, CompoundItem container)
		{
			var currency = CurrencyOf(template);
			var id = container.Id.ToString();
			return currency != null && SettingsStore.Current.Supplies.CashExclusions.Any(excluded => excluded.CaseId == id && excluded.Currency == currency);
		}

		// The first of the places that exists on you decides: if every container of it is kept out for this money, it is not brought at
		// all, and the other places are not tried. Returns that place's name, or null when the money may go there.
		internal static string CashLeftOut(string template, IEnumerable<string> places, Inventory inventory)
		{
			foreach (var place in places)
			{
				var targets = TargetsOf(inventory, place);
				if (targets.Count == 0)
				{
					continue;
				}

				return targets.All(target => IsCashExcluded(template, target.Container)) ? place : null;
			}

			return null;
		}

		// Moves the item (count of it, or all) to the first of the places that takes it. Returns null, or why the last one refused.
		internal static async Task<string> MoveTo(InventoryController controller, Item item, IEnumerable<string> places, int count = 0)
		{
			if (count <= 0 || count > item.StackObjectsCount)
			{
				count = item.StackObjectsCount;
			}

			string last = "no place to put it";
			foreach (var place in places)
			{
				var targets = TargetsOf(controller.Inventory, place);
				if (targets.Count == 0)
				{
					last = $"no {place} on you";
					continue;
				}

				// Cases this money is kept out of are not used; when that leaves none, it stops there (no other place is tried)
				var allowed = targets.Where(target => !IsCashExcluded(item.TemplateId.ToString(), target.Container)).ToList();
				if (allowed.Count == 0)
				{
					return $"{place} is on your cash exclusion list";
				}

				var error = await PlaceInto(controller, item, count, allowed);
				if (error == null)
				{
					return null;
				}

				last = error;
				Plugin.Log.LogInfo($"{NameOf(item)}: {place} did not take it ({error})");
			}

			return last;
		}

		// Waits up to a second for the item to leave the stash (to be on you, or merged into a stack there)
		internal static async Task<bool> WaitUntilCarried(InventoryController controller, Item item)
		{
			for (int i = 0; i < 20; i++)
			{
				if (!Stash(controller.Inventory).Contains(item))
				{
					return true;
				}

				await Task.Delay(50);
			}

			return false;
		}
	}
}
