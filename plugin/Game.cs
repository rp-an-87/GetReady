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

		// Cases that hold money, as places a supply can go: carried or in the secured container (the pouch)
		internal static readonly Dictionary<string, string> CasePlaces = new Dictionary<string, string>
		{
			{ "Wallet", "5783c43d2459774bbe137486" },
			{ "WZ wallet", "60b0f6c058e0b0481a09ad11" },
			{ "Docs case", "590c60fc86f77412b13fddcf" },
			{ "SICC case", "5d235bb686f77443f4331278" }
		};

		// Moves count of the stack into the first case that takes it: added to a stack of the same item when there is room for
		// all of it, otherwise as a split (or the whole stack) into a free spot of the case.
		private static async Task<string> MoveIntoCases(InventoryController controller, Item stack, int count, List<CompoundItem> cases)
		{
			if (count <= 0 || count > stack.StackObjectsCount)
			{
				count = stack.StackObjectsCount;
			}

			string last = "no room";
			var template = stack.TemplateId.ToString();
			foreach (var container in cases)
			{
				var existing = container.GetAllItems().FirstOrDefault(item => item != container && item.TemplateId.ToString() == template
					&& item.PinLockState != EItemPinLockState.Locked && item.StackMaxSize - item.StackObjectsCount >= count);
				OperationResult operation = default;
				bool planned = false;
				if (existing != null)
				{
					operation = ItemManipulator.TransferExact(stack, count, existing, controller, true);
					planned = true;
				}
				else
				{
					foreach (var grid in container.Grids)
					{
						var address = grid.FindLocationForItem(stack);
						if (address == null)
						{
							continue;
						}

						operation = stack.StackObjectsCount == count
							? ItemManipulator.Move(stack, address, controller, true)
							: ItemManipulator.SplitExact(stack, count, address, controller, controller, true);
						planned = true;
						if (operation.Succeeded)
						{
							break;
						}
					}
				}

				if (!planned || operation.Failed)
				{
					last = planned ? operation.Error.ToString() : "no room";
					continue;
				}

				var outcome = await controller.TryRunNetworkTransaction(operation);
				if (outcome.Failed)
				{
					last = outcome.Error.ToString();
					continue;
				}

				return null;
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
				string caseTemplate;
				if (CasePlaces.TryGetValue(place, out caseTemplate))
				{
					var cases = CarriedAndSecured(controller.Inventory).OfType<CompoundItem>().Where(container => container.TemplateId.ToString() == caseTemplate).ToList();
					if (cases.Count == 0)
					{
						last = $"no {place} on you";
						continue;
					}

					error = await MoveIntoCases(controller, item, count, cases);
				}
				else if (place == "Special slots")
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
				Plugin.Log.LogInfo($"{NameOf(item)}: {place} did not take it ({error})");
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

		// Moves the item to the first of the containers with room. IgnoreItemParent matters: without it the game adds the item's
		// own container (the stash) as the last place to try, and "moving" the item to another spot of the stash counts as success.
		internal static async Task<string> MoveToContainers(InventoryController controller, Item item, List<CompoundItem> targets)
		{
			var result = ItemManipulator.QuickFindAppropriatePlace(item, controller, targets, ItemManipulator.EMoveItemOrder.PrioritizeTargetsOrder | ItemManipulator.EMoveItemOrder.IgnoreItemParent, true);
			if (result.Failed)
			{
				return result.Error.ToString();
			}

			var outcome = await controller.TryRunNetworkTransaction(result);
			return outcome.Failed ? outcome.Error.ToString() : null;
		}
	}
}
