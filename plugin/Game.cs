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

		// To an exact address (a cell of the injector case, a special slot). Returns null, or why it failed.
		internal static async Task<string> PlaceAt(InventoryController controller, Item item, int count, ItemAddress address)
		{
			var operation = Simulate(controller, item, count, address);
			return operation.Failed ? operation.Error.ToString() : await Run(controller, operation);
		}

		// Into the first of the containers that has a spot: added to a stack of the same item when there is room for all of it,
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
						&& other.PinLockState != EItemPinLockState.Locked && other.StackMaxSize - other.StackObjectsCount >= count)
					: null;
				if (existing != null)
				{
					operation = ItemManipulator.TransferExact(item, count, existing, controller, true);
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

				var error = await PlaceInto(controller, item, count, targets);
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
	}
}
