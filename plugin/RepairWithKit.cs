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
			// No stash on the controller means a raid: the key is for the stash only, as its hint says (IsApplicable)
			if (itemContext == null || controller == null || controller.Inventory.Stash == null || repairController == null || itemContext.Item is RepairKit)
			{
				return;
			}

			// The same decision as the Repair step in the window: kits, else a trader, else say why not
			var item = itemContext.Item;
			var decision = Decide(item, repairController, controller, 0);
			switch (decision.Kind)
			{
				case RepairKind.Nothing:
					// A key pressed over an item that is fine says nothing
					return;
				case RepairKind.Trader:
					RunKey(repairController, controller, item, decision);
					return;
				case RepairKind.Kits:
					if (FindKit(uiContext, itemContext, controller, false, out var kitWhy) != null)
					{
						RunKey(repairController, controller, item, decision);
						return;
					}

					Plugin.Trace($"Repair key: no kit used for {item.ShortName.Localized()} (reason: {kitWhy ?? "the game's menu doesn't offer Repair"})");
					if (kitWhy != null)
					{
						Refuse($"Repair: {kitWhy}");
					}

					return;
				default:
					// Nothing can repair it. With traders off the kits are the whole story, so say what is wrong with them (locked, in your
					// gear, not examined...); with traders on the trader is the last thing that failed (no money, none repairs it)
					string why = null;
					if (!SettingsStore.Current.Repair.UseTraders)
					{
						FindKit(uiContext, itemContext, controller, false, out why);
					}

					Plugin.Trace($"Repair key: nothing used for {item.ShortName.Localized()} ({why ?? decision.Problem})");
					Refuse($"Repair: {why ?? decision.Problem}");
					return;
			}
		}

		// Whether the hotkey would do something for the item: it is damaged and a kit or a trader can repair it
		internal static bool IsApplicable(ItemUiContext uiContext, InventoryController controller, Item item)
		{
			var itemContext = uiContext.CurrentItemContext;
			var repairController = uiContext.Session?.RepairController;
			if (itemContext == null || itemContext.Item != item || item is RepairKit || repairController == null || controller?.Inventory.Stash == null)
			{
				return false;
			}

			var decision = Decide(item, repairController, controller, 0);
			return decision.Kind == RepairKind.Trader || (decision.Kind == RepairKind.Kits && FindKit(uiContext, itemContext, controller, true, out _) != null);
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
			// Damaged = the game would accept some repair points (the same question the repair itself asks); a part a hair under its
			// maximum after a repair does not count
			var damaged = NeedsRepair(itemContext.Item, uiContext.Session.RepairController, out _);
			if (onlyDamaged && !damaged)
			{
				return null;
			}

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
				usable = strategy.RepairKitsCollections.SelectMany(collection => collection._repairKits).Where(kit => kit.PinLockState != EItemPinLockState.Locked).ToList();
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not ask the game for the repair kits of {itemContext.Item.ShortName.Localized()}: {e.Message}");
			}

			var kit = usable.OrderBy(candidate => candidate.Resource).FirstOrDefault();
			if (kit == null)
			{
				// Only for the key press: the tooltip hint asks the same question every frame and needs no reason
				if (damaged && !onlyDamaged)
				{
					var name = itemContext.Item.ShortName.Localized();
					var inStash = new HashSet<Item>(controller.Inventory.GetPlayerItems(EPlayerItems.Stash));
					foreach (var candidate in fitting)
					{
						Plugin.Trace($"Repair kit {candidate.Id}: resource {candidate.Resource}, {(inStash.Contains(candidate) ? "in the stash" : "NOT in the stash")}, pin/lock {candidate.PinLockState}, examined {controller.Examined(candidate)}");
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

		// The game's repair strategy for the item (replaceable, so the tests can give the repair state of an item without the game's repair controller)
		internal static Func<Item, EFT.Repairing.RepairController, IRepairStrategy> StrategyOf = (item, repairController) =>
			item.GetItemComponent<ArmorHolderComponent>() != null
				? (IRepairStrategy)new ArmorRepairStrategy(item, repairController)
				: new DefaultRepairStrategy(item, repairController);

		private static IRepairStrategy NewStrategy(Item item, EFT.Repairing.RepairController repairController) => StrategyOf(item, repairController);

		// The wait for a repair to reach the inventory, the roubles in the stash and a trader's name (replaceable for the tests)
		internal static Func<Task> Pause = () => Task.Delay(150);
		internal static Func<InventoryController, int> RoublesIn = controller =>
		{
			var sums = InventoryExtension.GetMoneySums(controller.Inventory.Stash.Grid.ContainedItems.Keys);
			int roubles;
			return sums.TryGetValue(ECurrencyType.RUB, out roubles) ? roubles : 0;
		};
		internal static Func<Trader, string> NameOfTrader = trader => trader.LocalizedName;

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

		internal enum RepairKind { Nothing, Kits, Trader, Problem }

		// What to do about one piece, the same for the Repair step and the R key
		internal class RepairDecision
		{
			public RepairKind Kind;
			public TraderQuote Quote; // Trader: who and what it costs
			public string Problem;    // Problem: why nothing can repair it
		}

		// Nothing to repair; else the kits when an unlocked one with resource can do it; else, with traders on, the first trader in your
		// order that can repair it (all or nothing: not enough roubles is a problem; spentOnTraders is what earlier pieces of the same
		// run already take); else a problem saying why
		internal static RepairDecision Decide(Item item, EFT.Repairing.RepairController repairController, InventoryController controller, int spentOnTraders)
		{
			bool hasKit;
			if (!NeedsRepair(item, repairController, out hasKit))
			{
				return new RepairDecision { Kind = RepairKind.Nothing };
			}

			if (hasKit)
			{
				return new RepairDecision { Kind = RepairKind.Kits };
			}

			if (!SettingsStore.Current.Repair.UseTraders)
			{
				return new RepairDecision { Kind = RepairKind.Problem, Problem = "needs repair, but you have no repair kit with resource left (traders are switched off)" };
			}

			TraderQuote quote = null;
			try
			{
				quote = QuoteTrader(item, repairController, controller);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not ask the traders about {item.ShortName.Localized()}: {e.Message}");
			}

			if (quote == null)
			{
				return new RepairDecision { Kind = RepairKind.Problem, Problem = "needs repair, but you have no repair kit with resource left and no trader repairs it" };
			}

			if (quote.Price + spentOnTraders > quote.Money)
			{
				return new RepairDecision { Kind = RepairKind.Problem, Problem = $"needs repair: {NameOfTrader(quote.Trader)} wants {quote.Price:N0} roubles, you have {Math.Max(0, quote.Money - spentOnTraders):N0}" };
			}

			return new RepairDecision { Kind = RepairKind.Trader, Quote = quote };
		}

		// Does what Decide said (Kits or Trader)
		internal static Task<RepairOutcome> Execute(RepairDecision decision, EFT.Repairing.RepairController repairController, InventoryController controller, Item item) =>
			decision.Kind == RepairKind.Trader
				? RepairAtTrader(repairController, controller, item, decision.Quote.Trader.Id)
				: RepairAll(repairController, item);

		// What went wrong when the repair did not finish, or null when the piece is whole
		internal static string Shortfall(RepairDecision decision, RepairOutcome outcome) =>
			outcome.Error ?? (outcome.Whole ? null : decision.Kind == RepairKind.Trader ? $"the trader left it at {outcome.Durability:F1}" : $"the repair kits ran out, it is at {outcome.Durability:F1}");

		internal class RepairOutcome
		{
			public int Steps;
			public bool Whole;
			public float Durability;
			public string Error; // set when a repair request failed
		}

		// The repair controller of the session: through the item screen when it is up, else straight from the session (the badge asks before
		// the stash screen has set the item screen up)
		internal static Func<EFT.Repairing.RepairController> Controller = () =>
			ItemUiContext.Instance?.Session?.RepairController ?? SPT.Reflection.Utils.ClientAppUtils.GetMainApp()?.GetClientBackEndSession()?.RepairController;

		// Whether the item has anything to repair and some kit the game offers could do it: what the window and the key start from
		internal static bool NeedsRepair(Item item, EFT.Repairing.RepairController repairController, out bool hasKit)
		{
			hasKit = false;
			// Medicine, ammo and the like have nothing to repair (and the game's repair strategy throws for them)
			if (!item.GetItemComponentsInChildren<RepairableComponent>(true).Any())
			{
				return false;
			}

			try
			{
				var strategy = NewStrategy(item, repairController);
				hasKit = strategy.RepairKitsCollections.SelectMany(group => group._repairKits).Any(kit => kit.Resource > KitFloor && kit.PinLockState != EItemPinLockState.Locked);
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
					foreach (var candidate in strategy.RepairKitsCollections.SelectMany(group => group._repairKits.Select(one => new { group, one })).Where(pair => pair.one.Resource > KitFloor && pair.one.PinLockState != EItemPinLockState.Locked).OrderBy(pair => pair.one.Resource))
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

					Plugin.Trace($"Repair: {name} {item.Id} with kit {kit.Id} (resource {kit.Resource:F1}), {amount:F1} of {need:F1} points, price {strategy.GetRepairPrice(amount, collection):F1}");
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
					await Pause();
				}

				var after = NewStrategy(item, repairController);
				outcome.Whole = after.HowMuchRepairScoresCanAccept() < 0.1f;
				outcome.Durability = after.Durability();
				Plugin.Trace($"Repair done: {outcome.Steps} step(s), {(outcome.Whole ? "whole" : "not whole, the kits ran out")}");
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
		internal static int Roubles(InventoryController controller) => RoublesIn(controller);

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
					outcome.Error = $"{NameOfTrader(trader)} wants {price:N0} roubles, you have {money:N0}";
					return outcome;
				}

				Plugin.Trace($"Repair: {item.ShortName.Localized()} {item.Id} at {NameOfTrader(trader)}, {amount:F1} points for {price:N0} roubles");
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
				await Pause();
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

		// The key could not repair: the message, with the game's error sound (not the plain notification sound alone)
		private static void Refuse(string message)
		{
			NotificationManager.DisplayMessageNotification(message);
			try
			{
				Singleton<GUISounds>.Instance?.PlayUISound(EUISoundType.ErrorMessage);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not play the error sound: {e.Message}");
			}
		}

		// The hotkey: repairs the way Decide said and tells what happened
		private static async void RunKey(EFT.Repairing.RepairController repairController, InventoryController controller, Item item, RepairDecision decision)
		{
			var name = item.ShortName.Localized();
			var outcome = await Execute(decision, repairController, controller, item);
			if (outcome.Error != null)
			{
				Refuse($"Repair of {name} failed: {outcome.Error}");
				return;
			}

			if (outcome.Steps > 0)
			{
				Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.RepairComplete);
			}

			// Nothing repaired and not whole: a refusal, with the error sound (a partial repair has had the repair sound already)
			if (outcome.Steps == 0 && !outcome.Whole)
			{
				Refuse(decision.Kind == RepairKind.Trader ? $"Repair: {name} is at {outcome.Durability:F1}, the trader left it so." : $"Repair: {name} is at {outcome.Durability:F1}, your repair kits ran out.");
				return;
			}

			if (outcome.Steps > 0 || !outcome.Whole)
			{
				var where = decision.Kind == RepairKind.Trader ? $" at {NameOfTrader(decision.Quote.Trader)} for {decision.Quote.Price:N0} roubles" : "";
				NotificationManager.DisplayMessageNotification(outcome.Whole
					? $"Repaired {name}{where} to {outcome.Durability:F1}."
					: decision.Kind == RepairKind.Trader ? $"Repair: {name} is at {outcome.Durability:F1}, the trader left it so." : $"Repair: {name} is at {outcome.Durability:F1}, your repair kits ran out.");
			}
		}
	}
}
