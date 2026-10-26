using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Diz.LanguageExtensions;
using EFT.UI;

namespace GetReady
{
	// What the steps need from the game's inventory: the controller, what is carried and in the stash, and moving items
	internal static class Game
	{
		internal const string InjectorCase = "619cbf7d23893217ec30b689";

		private static readonly FieldInfo ControllerField = typeof(ItemUiContext).GetField("_inventoryController", BindingFlags.Instance | BindingFlags.NonPublic);

		// The controller the stash panel was last shown with (set by the button's patch)
		internal static InventoryController StashController;

		// The player's inventory controller, available where the stash is (hideout and menu)
		internal static InventoryController Controller()
		{
			if (StashController != null)
			{
				return StashController;
			}

			var uiContext = ItemUiContext.Instance;
			return uiContext == null ? null : ControllerField.GetValue(uiContext) as InventoryController;
		}

		internal static CompoundItem EquippedContainer(Inventory inventory, EquipmentSlot slot) =>
			inventory.Equipment.GetSlot(slot).ContainedItem as CompoundItem;

		// Pockets, vest, armored vest and backpack
		internal static IEnumerable<Item> Carried(Inventory inventory)
		{
			foreach (var slot in new[] { EquipmentSlot.Pockets, EquipmentSlot.TacticalVest, EquipmentSlot.ArmorVest, EquipmentSlot.Backpack })
			{
				var container = EquippedContainer(inventory, slot);
				if (container == null)
				{
					continue;
				}

				foreach (var item in container.GetAllItems())
				{
					yield return item;
				}
			}
		}

		// The secured container (the pouch) and what is in it
		internal static IEnumerable<Item> Secured(Inventory inventory) =>
			inventory.Equipment.GetSlot(EquipmentSlot.SecuredContainer).ContainedItem?.GetAllItems() ?? Enumerable.Empty<Item>();

		internal static IEnumerable<Item> CarriedAndSecured(Inventory inventory) => Carried(inventory).Concat(Secured(inventory));

		internal static List<Item> Stash(Inventory inventory) => inventory.GetPlayerItems(EPlayerItems.Stash).ToList();

		// Stash items of a template, best first: non-FiR before FiR (the FiR ones are worth more kept), then the fullest resource
		internal static IEnumerable<Item> Candidates(IEnumerable<Item> stash, string template) =>
			stash.Where(item => item.TemplateId.ToString() == template && item.PinLockState != EItemPinLockState.Locked)
				.OrderBy(item => item.SpawnedInSession)
				.ThenByDescending(ResourceOf);

		internal static float ResourceOf(Item item) => item.GetItemComponent<MedKitComponent>()?.HpResource ?? 0f;

		internal static string NameOf(Item item) => item.ShortName.Localized();

		// Moves count of the stack to the address (the whole stack as a move, else a split). Returns null, or why it failed.
		internal static async Task<string> PlaceAt(InventoryController controller, Item item, int count, ItemAddress address)
		{
			OperationResult operation = item.StackObjectsCount == count
				? (OperationResult)ItemManipulator.Move(item, address, controller, true)
				: ItemManipulator.SplitExact(item, count, address, controller, controller, true);
			if (operation.Failed)
			{
				return operation.Error.ToString();
			}

			var outcome = await controller.TryRunNetworkTransaction(operation);
			return outcome.Failed ? outcome.Error.ToString() : null;
		}

		// Moves the item to the first of the containers with room
		internal static async Task<string> MoveToContainers(InventoryController controller, Item item, List<CompoundItem> targets)
		{
			var result = ItemManipulator.QuickFindAppropriatePlace(item, controller, targets, ItemManipulator.EMoveItemOrder.PrioritizeTargetsOrder, true);
			if (result.Failed)
			{
				return result.Error.ToString();
			}

			var outcome = await controller.TryRunNetworkTransaction(result);
			return outcome.Failed ? outcome.Error.ToString() : null;
		}
	}
}
