using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.Trading;
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

		internal class RepairOutcome
		{
			public int Steps;
			public bool Whole;
			public float Durability;
			public string Error; // set when a repair request failed
		}

		// The repair controller of the session: through the item screen when it is up, else straight from the session (the badge asks before
		// the stash screen has set the item screen up)
		internal static EFT.Repairing.RepairController Controller() =>
			ItemUiContext.Instance?.Session?.RepairController ?? SPT.Reflection.Utils.ClientAppUtils.GetMainApp()?.GetClientBackEndSession()?.RepairController;

		// Whether the item has anything to repair and some kit the game offers could do it: what the window and the key start from
		internal static bool NeedsRepair(Item item, EFT.Repairing.RepairController repairController, out bool hasKit)
		{
			hasKit = false;
			try
			{
				var strategy = NewStrategy(item, repairController);
				hasKit = strategy.RepairKitsCollections.SelectMany(group => group._repairKits).Any(kit => kit.Resource > KitFloor);
				return strategy.HowMuchRepairScoresCanAccept() >= 0.1f;
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not ask the game about the repair of {item.ShortName.Localized()}: {e.Message}");
				return false;
			}
		}

		// Repairs the item with one kit after the other, the one with least left first, until it is whole or the kits are used up
		internal static async Task<RepairOutcome> RepairAll(EFT.Repairing.RepairController repairController, Item item)
		{
			var outcome = new RepairOutcome();
			_running = true;
			var name = item.ShortName.Localized();
			try
			{
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
						outcome.Error = result.Error;
						break;
					}

					outcome.Steps++;

					// Let the kit's new resource (or its removal) reach the inventory before the next round looks at it
					await Task.Delay(150);
				}

				var after = NewStrategy(item, repairController);
				outcome.Whole = after.HowMuchRepairScoresCanAccept() < 0.1f;
				outcome.Durability = after.Durability();
				Plugin.Log.LogInfo($"Repair done: {outcome.Steps} step(s), {(outcome.Whole ? "whole" : "not whole, the kits ran out")}");
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Repair with kit failed: {e}");
				outcome.Error = e.Message;
			}
			finally
			{
				_running = false;
			}

			return outcome;
		}

		internal class TraderQuote
		{
			public Trader Trader;
			public int Price;
			public int Money;
		}

		// Roubles in the stash (the repair window counts the same)
		internal static int Roubles(InventoryController controller)
		{
			var sums = InventoryExtension.GetMoneySums(controller.Inventory.Stash.Grid.ContainedItems.Keys);
			int roubles;
			return sums.TryGetValue(ECurrencyType.RUB, out roubles) ? roubles : 0;
		}

		// The traders that can repair the item, in the order set in Setup (the ones not listed after them, in the game's order)
		internal static List<Trader> TradersFor(Item item, EFT.Repairing.RepairController repairController)
		{
			var order = SettingsStore.Current.Repair.TraderOrder;
			return NewStrategy(item, repairController).Repairers.OfType<Trader>()
				.Select((trader, index) => new { trader, key = order.IndexOf(trader.Id) >= 0 ? order.IndexOf(trader.Id) : 1000 + index })
				.OrderBy(pair => pair.key)
				.Select(pair => pair.trader)
				.ToList();
		}

		// What the first trader that can repair the item would charge for the whole repair, and the roubles you have
		internal static TraderQuote QuoteTrader(Item item, EFT.Repairing.RepairController repairController, InventoryController controller)
		{
			var trader = TradersFor(item, repairController).FirstOrDefault();
			if (trader == null)
			{
				return null;
			}

			var strategy = NewStrategy(item, repairController);
			strategy.CurrentRepairer = strategy.Repairers.OfType<Trader>().First(candidate => candidate.Id == trader.Id);
			return new TraderQuote { Trader = trader, Price = strategy.GetCurrencyPrice(strategy.HowMuchRepairScoresCanAccept()), Money = Roubles(controller) };
		}

		// Repairs the whole item at the trader, or says why not (all or nothing: not enough roubles is an error)
		internal static async Task<RepairOutcome> RepairAtTrader(EFT.Repairing.RepairController repairController, InventoryController controller, Item item, string traderId)
		{
			var outcome = new RepairOutcome();
			_running = true;
			try
			{
				var strategy = NewStrategy(item, repairController);
				var trader = strategy.Repairers.OfType<Trader>().FirstOrDefault(candidate => candidate.Id == traderId);
				if (trader == null)
				{
					outcome.Error = "the trader can not repair it any more";
					return outcome;
				}

				strategy.CurrentRepairer = trader;
				float amount = strategy.HowMuchRepairScoresCanAccept();
				if (amount < 0.1f)
				{
					outcome.Whole = true;
					return outcome;
				}

				int price = strategy.GetCurrencyPrice(amount);
				int money = Roubles(controller);
				if (price > money)
				{
					outcome.Error = $"{trader.LocalizedName} wants {price:N0} roubles, you have {money:N0}";
					return outcome;
				}

				Plugin.Log.LogInfo($"Repair: {item.ShortName.Localized()} {item.Id} at {trader.LocalizedName}, {amount:F1} points for {price:N0} roubles");
				repairController.OnSuccessfulRepairChangedEvent += NoListener;
				IResult result;
				try
				{
					result = await strategy.RepairItem(amount, null);
				}
				finally
				{
					repairController.OnSuccessfulRepairChangedEvent -= NoListener;
				}

				if (!result.Succeed)
				{
					Plugin.Log.LogWarning($"Repair at the trader failed: {result.Error}");
					outcome.Error = result.Error;
					return outcome;
				}

				outcome.Steps = 1;
				await Task.Delay(150);
				var after = NewStrategy(item, repairController);
				outcome.Whole = after.HowMuchRepairScoresCanAccept() < 0.1f;
				outcome.Durability = after.Durability();
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Repair at the trader failed: {e}");
				outcome.Error = e.Message;
			}
			finally
			{
				_running = false;
			}

			return outcome;
		}

		// The hotkey: repairs and tells what happened
		private static async void Run(EFT.Repairing.RepairController repairController, Item item)
		{
			var name = item.ShortName.Localized();
			var outcome = await RepairAll(repairController, item);
			if (outcome.Error != null)
			{
				NotificationManager.DisplayMessageNotification($"Repair of {name} failed: {outcome.Error}");
				return;
			}

			if (outcome.Steps > 0)
			{
				Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.RepairComplete);
			}

			if (outcome.Steps > 0 || !outcome.Whole)
			{
				NotificationManager.DisplayMessageNotification(outcome.Whole
					? $"Repaired {name} to {outcome.Durability:F1}."
					: $"Repair: {name} is at {outcome.Durability:F1}, your repair kits ran out.");
			}
		}
	}
}
