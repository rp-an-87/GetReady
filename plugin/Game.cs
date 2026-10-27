using System;
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

		internal static float MaxResourceOf(Item item) => item.GetItemComponent<MedKitComponent>()?.MaxHpResource ?? 0f;

		// Templates whose short name is shared with another item of the same group (two items called "Bandage"): shown with their
		// full name
		internal static readonly HashSet<string> LongNames = new HashSet<string>();

		internal static string TemplateName(string template) => (template + (LongNames.Contains(template) ? " Name" : " ShortName")).Localized();

		internal static string NameOf(Item item) => TemplateName(item.TemplateId.ToString());

		// What an item treats (a bandage the light bleeding, a splint the fracture, a surgery kit a destroyed part)
		internal static bool Treats(Item item, EDamageEffectType effect) =>
			item.GetItemComponent<HealthEffectsComponent>()?.AffectsAny(effect) ?? false;

		// Where the medical items live in the item tree: medikits (AFAK, Salewa...) and the single-purpose "medical items"
		internal static readonly string[] UnderMedikit = { "5448f39d4bdc2d0a728b4568" };
		internal static readonly string[] UnderMedicalItem = { "5448f3ac4bdc2dce718b4569" };

		internal static bool IsHealingKit(Item item) => item.GetItemComponent<MedKitComponent>() != null && !Treats(item, EDamageEffectType.DestroyedPart);

		internal static bool IsBandage(Item item) =>
			Treats(item, EDamageEffectType.LightBleeding) && !Treats(item, EDamageEffectType.HeavyBleeding) && !Treats(item, EDamageEffectType.Fracture) && !Treats(item, EDamageEffectType.DestroyedPart);

		internal static bool IsTourniquet(Item item) =>
			Treats(item, EDamageEffectType.HeavyBleeding) && !Treats(item, EDamageEffectType.Fracture) && !Treats(item, EDamageEffectType.DestroyedPart);

		// Surv12 treats fractures too but counts as a surgery kit only
		internal static bool IsSplint(Item item) => Treats(item, EDamageEffectType.Fracture) && !Treats(item, EDamageEffectType.DestroyedPart);

		internal static bool IsSurgeryKit(Item item) => Treats(item, EDamageEffectType.DestroyedPart);

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

		// "Vest" is the tactical vest or an armored rig, "Pouch" the secured container
		internal static List<CompoundItem> ContainersOf(Inventory inventory, string place)
		{
			var slots = new List<EquipmentSlot>();
			switch (place)
			{
				case "Vest":
					slots.Add(EquipmentSlot.TacticalVest);
					slots.Add(EquipmentSlot.ArmorVest);
					break;
				case "Pouch":
					slots.Add(EquipmentSlot.SecuredContainer);
					break;
				default:
					slots.Add((EquipmentSlot)Enum.Parse(typeof(EquipmentSlot), place));
					break;
			}

			return slots.Select(slot => EquippedContainer(inventory, slot)).Where(container => container != null).ToList();
		}

		// Inside an injector case, which the injector case template looks after
		internal static bool InsideInjectorCase(Item item)
		{
			var parent = item.Parent?.Container?.ParentItem;
			for (int depth = 0; parent != null && depth < 12; depth++, parent = parent.Parent?.Container?.ParentItem)
			{
				if (parent.TemplateId.ToString() == InjectorCase)
				{
					return true;
				}
			}

			return false;
		}

		// Moves the item to the first of the places (Special slots, Pockets, Vest, Backpack, Pouch) that takes it.
		// With a count below the stack, only that many are split off. Returns null, or why the last one refused.
		internal static async Task<string> MoveTo(InventoryController controller, Item item, IEnumerable<string> places, int count = 0)
		{
			bool split = count > 0 && count < item.StackObjectsCount;
			string last = "no place to put it";
			foreach (var place in places)
			{
				string error;
				if (place == "Special slots")
				{
					error = await MoveToSpecialSlots(controller, item, split ? count : item.StackObjectsCount);
				}
				else
				{
					var containers = ContainersOf(controller.Inventory, place);
					if (containers.Count == 0)
					{
						last = $"no {place} equipped";
						continue;
					}

					error = split ? await SplitToContainers(controller, item, count, containers) : await MoveToContainers(controller, item, containers);
				}

				if (error == null)
				{
					return null;
				}

				last = error;
			}

			return last;
		}

		// Splits count off the stack into the first grid with room for one
		private static async Task<string> SplitToContainers(InventoryController controller, Item item, int count, List<CompoundItem> containers)
		{
			string last = "no room";
			foreach (var grid in containers.SelectMany(container => container.Grids))
			{
				ItemAddress location;
				if (!grid.TryFindLocationForItem(item, out location))
				{
					continue;
				}

				var error = await PlaceAt(controller, item, count, location);
				if (error == null)
				{
					return null;
				}

				last = error;
			}

			return last;
		}

		// The first empty special slot of the pockets that accepts the item
		private static async Task<string> MoveToSpecialSlots(InventoryController controller, Item item, int count)
		{
			string last = "no free special slot";
			var pockets = EquippedContainer(controller.Inventory, EquipmentSlot.Pockets);
			foreach (var slot in pockets?.Slots ?? new Slot[0])
			{
				if (slot.ContainedItem != null)
				{
					continue;
				}

				var error = await PlaceAt(controller, item, count, slot.CreateItemAddress());
				if (error == null)
				{
					return null;
				}

				last = error;
			}

			return last;
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
