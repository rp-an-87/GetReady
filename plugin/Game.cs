using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;

namespace GetReady
{
	// What the steps need from the game's inventory: the controller, what is carried and in the stash, and item names. Moving items
	// is in Game.Placement.cs and what a medical item treats in Game.Medical.cs.
	internal static partial class Game
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

		// What the steps ask of the inventory controller, so the tests can give them an inventory and take the moves without the game's
		// controller: the inventory, and running a move
		internal static Func<InventoryController, Inventory> InventoryOf = controller => controller.Inventory;
		internal static Func<InventoryController, OperationResult, System.Threading.Tasks.Task<IResult>> RunOperation = (controller, operation) => controller.TryRunNetworkTransaction(operation);

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

	// The game's error for a move, without the internals (ids, "item item"), for the screen; the log keeps the original
	internal static string Friendly(string error)
	{
		if (string.IsNullOrEmpty(error))
		{
			return error;
		}

		if (error.IndexOf("locked item", System.StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "it is locked in your stash";
		}

		return System.Text.RegularExpressions.Regex.Replace(error, @"\s*\(id: [0-9a-fA-F]{24}\)", "").Trim();
	}

		// 1 when the item sits inside a container of the stash (a case, a bag), 0 when it lies loose on the stash grid. Sorted
		// first, so what lies loose is always taken before anything inside a container.
		internal static int InContainer(Item item) => IsInsideContainer(item) ? 1 : 0;

		// Whether the item sits inside a container of the stash (replaceable, so the tests can put items in one)
		internal static Func<Item, bool> IsInsideContainer = item =>
		{
			var parent = item.Parent?.Container?.ParentItem;
			return parent != null && parent != Controller()?.Inventory?.Stash;
		};

		// The two places in a sort for "loose before in a container": after FiR when NonFiRBeforeLoose is on (a tie-breaker), before it when off
		internal static int LooseBeforeFiR(Item item) => SettingsStore.Current.Supplies.NonFiRBeforeLoose ? 0 : InContainer(item);

		internal static int LooseAfterFiR(Item item) => SettingsStore.Current.Supplies.NonFiRBeforeLoose ? InContainer(item) : 0;

		// Stash items of a template, best first: loose on the stash before in a container, then non-FiR before FiR (the FiR ones are
		// worth more kept), then the fullest resource (the order of the first two can be switched in the settings)
		internal static IEnumerable<Item> Candidates(IEnumerable<Item> stash, string template) =>
			stash.Where(item => item.TemplateId.ToString() == template && item.PinLockState != EItemPinLockState.Locked)
				.OrderBy(LooseBeforeFiR)
				.ThenBy(item => item.SpawnedInSession)
				.ThenBy(LooseAfterFiR)
				.ThenByDescending(ResourceOf);

		internal static float ResourceOf(Item item) => item.GetItemComponent<MedKitComponent>()?.HpResource ?? 0f;

		internal static float MaxResourceOf(Item item) => item.GetItemComponent<MedKitComponent>()?.MaxHpResource ?? 0f;

		// Templates whose short name is shared with another item of the same group (two items called "Bandage"): shown with their
		// full name
		internal static readonly HashSet<string> LongNames = new HashSet<string>();

		// The game's localization, and a random pick: replaceable, so the tests don't need the game's
		internal static Func<string, string> Localize = key => key.Localized();
		internal static Func<int, int> RandomIndex = count => UnityEngine.Random.Range(0, count);

		internal static string TemplateName(string template) => Localize(template + (LongNames.Contains(template) ? " Name" : " ShortName"));

		internal static string NameOf(Item item) => TemplateName(item.TemplateId.ToString());

		// Inside an injector case, which the injector case template looks after
		internal static bool InsideInjectorCase(Item item) => IsInsideInjectorCase(item);

		// (replaceable, so the tests don't need items with a place in an inventory)
		internal static Func<Item, bool> IsInsideInjectorCase = item =>
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
		};
	}
}
