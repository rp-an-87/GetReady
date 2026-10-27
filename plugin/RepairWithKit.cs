using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using UnityEngine;

namespace GetReady
{
	// Hotkey on a hovered item in the stash: repairs it fully with a repair kit, without the repair window. It runs the game's own
	// repair strategy (the one the window's Repair button calls), with the kit that has the least resource left that can repair it,
	// and as much as the kits of that type can pay for.
	internal static class RepairWithKit
	{
		private static readonly FieldInfo ControllerField = typeof(ItemUiContext).GetField("_inventoryController", BindingFlags.Instance | BindingFlags.NonPublic);

		private static bool _running;

		// The controller raises this after every repair without checking for listeners; only the repair window normally has one
		private static void NoListener()
		{
		}

		internal static void Update()
		{
			if (_running || !Plugin.RepairHotkey.Value.IsDown())
			{
				return;
			}

			var uiContext = ItemUiContext.Instance;
			var itemContext = uiContext?.CurrentItemContext;
			var controller = uiContext == null ? null : ControllerField.GetValue(uiContext) as InventoryController;
			var repairController = uiContext?.Session?.RepairController;
			var kit = FindKit(uiContext, itemContext, controller, false, out var why);
			if (kit == null || repairController == null)
			{
				if (itemContext != null)
				{
					Plugin.Log.LogInfo($"Repair key: no kit used for {itemContext.Item.ShortName.Localized()} (reason: {why ?? "nothing to repair, or the game's menu doesn't offer Repair"}, repair controller: {repairController != null})");
				}

				// Only tell when there is something to repair: a key pressed over an item that is fine says nothing
				if (why != null)
				{
					NotificationManager.DisplayMessageNotification($"Repair: {why}");
				}

				return;
			}

			Run(repairController, itemContext.Item);
		}

		// Whether the hotkey would do something for the item: it is damaged and one of your kits can repair it
		internal static bool IsApplicable(ItemUiContext uiContext, InventoryController controller, Item item)
		{
			var itemContext = uiContext.CurrentItemContext;
			return itemContext != null && itemContext.Item == item && FindKit(uiContext, itemContext, controller, true, out _) != null;
		}

		// The kit the hotkey uses, or null when there is nothing to do: the least used kit that can repair the item
		// why: what is wrong when the item needs repair but no kit can do it (null when there is nothing to repair or nothing to say)
		private static RepairKit FindKit(ItemUiContext uiContext, ItemContext itemContext, InventoryController controller, bool onlyDamaged, out string why)
		{
			why = null;
			if (uiContext == null || itemContext == null || uiContext.Session?.RepairController == null || controller?.Inventory.Stash == null || itemContext.Item is RepairKit)
			{
				return null;
			}

			var repairables = itemContext.Item.GetItemComponentsInChildren<RepairableComponent>(true).ToList();
			if (onlyDamaged && !repairables.Any(repairable => repairable.Durability + 0.1f < repairable.MaxDurability))
			{
				return null;
			}

			var damaged = repairables.Any(repairable => repairable.Durability + 0.1f < repairable.MaxDurability);
			var fitting = controller.Inventory.GetPlayerItems(EPlayerItems.Stash | EPlayerItems.Equipment)
				.OfType<RepairKit>()
				.Where(candidate => repairables.Any(repairable => candidate.CanRepair(repairable.Item)))
				.ToList();

			// The kits the game itself offers for this item (the repair window's list): unlocked, examined, with resource left, and
			// able to repair every part of the item. Picking from anywhere else gives a kit the repair request can't use.
			var usable = new List<RepairKit>();
			try
			{
				IRepairStrategy strategy = itemContext.Item.GetItemComponent<ArmorHolderComponent>() != null
					? (IRepairStrategy)new ArmorRepairStrategy(itemContext.Item, uiContext.Session.RepairController)
					: new DefaultRepairStrategy(itemContext.Item, uiContext.Session.RepairController);
				usable = strategy.RepairKitsCollections.SelectMany(collection => collection._repairKits).ToList();
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not ask the game for the repair kits of {itemContext.Item.ShortName.Localized()}: {e.Message}");
			}

			var kit = usable.OrderBy(candidate => candidate.Resource).FirstOrDefault();
			if (kit == null)
			{
				if (damaged)
				{
					var name = itemContext.Item.ShortName.Localized();
					var inStash = new HashSet<Item>(controller.Inventory.GetPlayerItems(EPlayerItems.Stash));
					foreach (var candidate in fitting)
					{
						Plugin.Log.LogInfo($"Repair kit {candidate.Id}: resource {candidate.Resource}, {(inStash.Contains(candidate) ? "in the stash" : "NOT in the stash")}, pin/lock {candidate.PinLockState}, examined {controller.Examined(candidate)}");
					}

					var excluded = fitting.OrderBy(candidate => candidate.Resource).FirstOrDefault(candidate => candidate.Resource > 0f);
					why = fitting.Count == 0
						? $"you have no repair kit that can repair {name}."
						: excluded == null
							? $"your repair kits for {name} are out of resource."
							: !inStash.Contains(excluded)
								? $"your repair kit ({excluded.Resource:F0} left) is in your gear, the game only uses kits in the stash."
								: excluded.PinLockState == EItemPinLockState.Locked
									? $"your repair kit ({excluded.Resource:F0} left) is locked."
									: !controller.Examined(excluded)
										? $"your repair kit ({excluded.Resource:F0} left) isn't examined yet."
										: $"your repair kit ({excluded.Resource:F0} left) can't repair every part of {name}.";
				}

				return null;
			}

			// The game's own check, so locked armor slots and the like are left alone
			var interactions = uiContext.GetItemContextInteractions(new RepairItemContext(itemContext, kit), null);
			return interactions.IsInteractionAvailable(EItemInfoButton.Repair).Succeed ? kit : null;
		}

		private static IRepairStrategy NewStrategy(Item item, EFT.Repairing.RepairController repairController) =>
			item.GetItemComponent<ArmorHolderComponent>() != null
				? (IRepairStrategy)new ArmorRepairStrategy(item, repairController)
				: new DefaultRepairStrategy(item, repairController);

		// A kit with this little left (or less) is not used: it keeps what a repair can't safely take
		private const float KitFloor = 1.5f;

		// The largest amount whose price the kit can pay, leaving it one point (as the repair window does). The price is not in
		// proportion to the amount, so it is searched for. The game repairs an armor part by part, one server request each, and
		// charges the kit for each part: a kit that runs dry between two parts is deleted by the server while the next part's
		// request still lists it (server error, null pointer), so a repair never takes a kit past what it holds.
		private static float AffordableAmount(IRepairStrategy strategy, RepairKitsCollection collection, float need, float resource)
		{
			if (strategy.GetRepairPrice(need, collection) <= resource - 0.5f)
			{
				return need;
			}

			float low = 0f, high = need, limit = resource - 1f;
			for (int step = 0; step < 24; step++)
			{
				float middle = (low + high) / 2f;
				if (strategy.GetRepairPrice(middle, collection) <= limit)
				{
					low = middle;
				}
				else
				{
					high = middle;
				}
			}

			return low;
		}

		// Repairs the item with one kit after the other, the one with least left first, until it is whole or the kits are used up
		private static async void Run(EFT.Repairing.RepairController repairController, Item item)
		{
			_running = true;
			var name = item.ShortName.Localized();
			try
			{
				int repairs = 0;
				for (int round = 0; round < 12; round++)
				{
					var strategy = NewStrategy(item, repairController);
					float need = strategy.HowMuchRepairScoresCanAccept();
					if (need < 0.1f)
					{
						break;
					}

					RepairKit kit = null;
					RepairKitsCollection collection = null;
					float amount = 0f;
					foreach (var candidate in strategy.RepairKitsCollections.SelectMany(group => group._repairKits.Select(one => new { group, one })).Where(pair => pair.one.Resource > KitFloor).OrderBy(pair => pair.one.Resource))
					{
						strategy.CurrentRepairer = candidate.group;
						amount = AffordableAmount(strategy, candidate.group, need, candidate.one.Resource);
						if (amount >= 0.1f)
						{
							kit = candidate.one;
							collection = candidate.group;
							break;
						}
					}

					if (kit == null)
					{
						break;
					}

					Plugin.Log.LogInfo($"Repair: {name} {item.Id} with kit {kit.Id} (resource {kit.Resource:F1}), {amount:F1} of {need:F1} points, price {strategy.GetRepairPrice(amount, collection):F1}");
					repairController.OnSuccessfulRepairChangedEvent += NoListener;
					IResult result;
					try
					{
						result = await strategy.RepairItem(amount, kit);
					}
					finally
					{
						repairController.OnSuccessfulRepairChangedEvent -= NoListener;
					}

					if (!result.Succeed)
					{
						Plugin.Log.LogWarning($"Repair with kit failed: {result.Error}");
						NotificationManager.DisplayMessageNotification($"Repair of {name} failed: {result.Error}");
						return;
					}

					repairs++;

					// Let the kit's new resource (or its removal) reach the inventory before the next round looks at it
					await System.Threading.Tasks.Task.Delay(150);
				}

				var after = NewStrategy(item, repairController);
				bool whole = after.HowMuchRepairScoresCanAccept() < 0.1f;
				Plugin.Log.LogInfo($"Repair done: {repairs} step(s), {(whole ? "whole" : "not whole, the kits ran out")}");
				if (repairs > 0)
				{
					Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.RepairComplete);
				}

				if (repairs > 0 || !whole)
				{
					NotificationManager.DisplayMessageNotification(whole
						? $"Repaired {name} to {after.Durability():F1}."
						: $"Repair: {name} is at {after.Durability():F1}, your repair kits ran out.");
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Repair with kit failed: {e}");
				NotificationManager.DisplayMessageNotification($"Repair of {name} failed, see the log.");
			}
			finally
			{
				_running = false;
			}
		}
	}
}
