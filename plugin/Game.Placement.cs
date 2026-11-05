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
		// ---- Placing items: one way for everything. A place is a name (Special slots, Pockets, Vest, Backpack, Pouch or Stash); it resolves to containers of yours, and an item goes to the first spot found in them.

		// A container of yours and which of its parts an item may go into
		private class Target
		{
			public CompoundItem Container;
			public bool Grids = true;
			public bool Slots;
		}

		private static List<Target> TargetsOf(Inventory inventory, string place)
		{
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

		// Everything inside the containers of this place (empty when you do not have it)
		internal static IEnumerable<Item> ItemsInPlace(Inventory inventory, string place) =>
			TargetsOf(inventory, place).SelectMany(target => target.Container.GetAllItems().Where(item => item != target.Container));

		// Whether you have this place: the vest or backpack is worn, the stash is there
		internal static bool HasPlace(Inventory inventory, string place) => TargetsOf(inventory, place).Count > 0;

		private static List<Target> One(CompoundItem container, bool grids, bool slots) =>
			container == null ? new List<Target>() : new List<Target> { new Target { Container = container, Grids = grids, Slots = slots } };

		// Moves count of the item to the address (the whole stack as a move, else a split), checked but not run
		private static OperationResult Simulate(InventoryController controller, Item item, int count, ItemAddress address) =>
			item.StackObjectsCount == count
				? (OperationResult)ItemManipulator.Move(item, address, controller, true)
				: ItemManipulator.SplitExact(item, count, address, controller, controller, true);

		private static async Task<string> Run(InventoryController controller, OperationResult operation)
		{
			var outcome = await RunOperation(controller, operation);
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

		// To an exact address (a cell of a case with exact positions, a special slot). Returns null, or why it failed.
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

						var attempt = Simulate(controller, item, count, slot.CreateItemAddress());
						if (attempt.Succeeded)
						{
							operation = attempt;
							planned = true;
							break;
						}

						// A slot that does not take the item (its filter) is no reason to show, unless slots are all this place has
						if (!target.Grids)
						{
							operation = attempt;
							planned = true;
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

		// The sign the game shows for a currency code (null for anything else: HP, grenades...)
		internal static string Symbol(string unit) => unit == "RUB" ? "₽" : unit == "EUR" ? "€" : unit == "USD" ? "$" : null;

		// An amount as the game's header shows money (the sign first), else the number and its unit
		internal static string Amount(string unit, float amount) => Symbol(unit) is string sign ? $"{sign} {amount:N0}" : $"{amount:N0} {unit}";

		// "have/want" of a progress line, in the same style
		internal static string Ratio(float have, float want, string unit) => Symbol(unit) is string sign ? $"{sign} {have:N0}/{want:N0}" : $"{have:N0}/{want:N0} {unit}";

		internal static string CurrencyOf(string template) =>
			template == Roubles ? "RUB" : template == Euros ? "EUR" : template == Dollars ? "USD" : null;

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
				var targets = TargetsOf(InventoryOf(controller), place);
				if (targets.Count == 0)
				{
					last = $"no {place} on you";
					continue;
				}

				var error = await PlaceInto(controller, item, count, targets);
				if (error == null)
				{
					return null;
				}

				last = error;
				Plugin.Trace($"{NameOf(item)}: {place} did not take it ({error})");
			}

			return last;
		}

		// Into this very container (a case): added to a stack of the same item when there is room, else a free spot of its grids. Returns null,
		// or why it failed.
		internal static Task<string> MoveInto(InventoryController controller, Item item, int count, CompoundItem container)
		{
			if (count <= 0 || count > item.StackObjectsCount)
			{
				count = item.StackObjectsCount;
			}

			return PlaceInto(controller, item, count, new List<Target> { new Target { Container = container } });
		}

		// Waits up to a second for the item to leave the stash (to be on you, or merged into a stack there)
		internal static async Task<bool> WaitUntilCarried(InventoryController controller, Item item)
		{
			for (int i = 0; i < 20; i++)
			{
				if (!Stash(InventoryOf(controller)).Contains(item))
				{
					return true;
				}

				await Task.Delay(50);
			}

			return false;
		}
	}
}
