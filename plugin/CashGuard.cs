using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using JsonType;
using SPT.Reflection.Patching;

namespace GetReady
{
	// The money in a tracked case (a case with a template that holds money) is not for paying. The server picks the stacks of a payment
	// itself (repair and insurance: it takes any roubles in the profile) and skips only stacks that are Locked, so every call that spends
	// money is wrapped: the money stacks of those cases are locked just before the request goes out and put back as they were when the
	// answer is in. A locked stack stops a case from being moved, so the lock is as short as the payment. With no other money to pay with the
	// server would fall back to the locked stacks, so the steps count only the money outside those cases (RepairWithKit.Roubles).
	internal static class CashGuard
	{
		// True while the wrapped call itself is being sent (its own patch lets it through)
		[ThreadStatic] internal static bool Passing;

		// The cases in the stash whose template holds money
		internal static HashSet<string> GuardedCases() =>
			new HashSet<string>(CasesStep.Entries.Where(entry => entry.Recorded && CasesStep.MoneyOf(entry).Count > 0).Select(entry => entry.CaseId));

		// Their money stacks (only where the game lets a stack be locked: in the stash)
		internal static List<Item> Stacks(Inventory inventory)
		{
			var cases = GuardedCases();
			if (cases.Count == 0 || inventory == null)
			{
				return new List<Item>();
			}

			return Game.Stash(inventory).OfType<CompoundItem>().Where(item => cases.Contains(item.Id.ToString()))
				.SelectMany(container => container.GetAllItems().Where(inside => inside != container && CasesStep.IsCurrency(inside.TemplateId.ToString())))
				.Distinct().ToList();
		}

		// How many roubles those stacks hold (what the steps leave out of what they may spend)
		internal static int Roubles(Inventory inventory) =>
			Stacks(inventory).Where(stack => Game.CurrencyOf(stack.TemplateId.ToString()) == "RUB").Sum(stack => stack.StackObjectsCount);

		// Sets the state of each stack (one move at a time, as the stash does). Returns what was changed with the state it had.
		internal static async Task<List<KeyValuePair<Item, EItemPinLockState>>> SetAll(InventoryController controller, List<KeyValuePair<Item, EItemPinLockState>> stacks, EItemPinLockState? forced)
		{
			var changed = new List<KeyValuePair<Item, EItemPinLockState>>();
			foreach (var pair in stacks)
			{
				var item = pair.Key;
				var want = forced ?? pair.Value;
				if (item.PinLockState == want)
				{
					continue;
				}

				var before = item.PinLockState;
				try
				{
					var operation = ItemManipulator.SetPinLockState(controller, item, want, true);
					if (operation.Failed)
					{
						Plugin.Log.LogWarning($"CashGuard: could not set {Game.NameOf(item)} to {want}: {operation.Error}");
						continue;
					}

					var outcome = await Game.RunOperation(controller, (OperationResult)operation);
					if (outcome.Failed)
					{
						Plugin.Log.LogWarning($"CashGuard: could not set {Game.NameOf(item)} to {want}: {outcome.Error}");
						continue;
					}

					changed.Add(new KeyValuePair<Item, EItemPinLockState>(item, before));
				}
				catch (Exception exception)
				{
					Plugin.Log.LogWarning($"CashGuard: {exception.Message}");
				}
			}

			return changed;
		}

		private static List<KeyValuePair<Item, EItemPinLockState>> Remembered(List<Item> stacks) =>
			stacks.Select(stack => new KeyValuePair<Item, EItemPinLockState>(stack, stack.PinLockState)).ToList();

		// The wrapped stacks for the call about to go out, or null when there is nothing to guard (then the call goes out as it is)
		private static bool Needed(out InventoryController controller, out List<Item> stacks)
		{
			controller = null;
			stacks = null;
			if (Passing)
			{
				return false;
			}

			try
			{
				controller = Game.Controller();
				stacks = controller == null ? null : Stacks(controller.Inventory);
			}
			catch (Exception exception)
			{
				Plugin.Log.LogWarning($"CashGuard: {exception.Message}");
			}

			return controller != null && stacks != null && stacks.Count > 0;
		}

		// A call that answers through a callback: locked, sent, and when the answer is in unlocked and only then passed on
		internal static bool GuardCallback(Action<Callback> send, Callback original)
		{
			if (!Needed(out var controller, out var stacks))
			{
				return true;
			}

			RunCallback(controller, stacks, send, original);
			return false;
		}

		private static async void RunCallback(InventoryController controller, List<Item> stacks, Action<Callback> send, Callback original)
		{
			var remembered = Remembered(stacks);
			List<KeyValuePair<Item, EItemPinLockState>> locked = new List<KeyValuePair<Item, EItemPinLockState>>();
			try
			{
				locked = await SetAll(controller, remembered, EItemPinLockState.Locked);
				Plugin.Trace($"CashGuard: {locked.Count} money stack(s) locked for the payment");
				send(new Callback(async result =>
				{
					try
					{
						await SetAll(controller, locked, null);
					}
					catch (Exception exception)
					{
						Plugin.Log.LogWarning($"CashGuard: {exception.Message}");
					}

					original?.Invoke(result);
				}));
			}
			catch (Exception exception)
			{
				Plugin.Log.LogWarning($"CashGuard: {exception.Message}");
				try
				{
					await SetAll(controller, locked, null);
				}
				catch
				{
				}

				original?.Invoke(null);
			}
		}

		// A call that answers with a task
		internal static bool GuardTask(Func<Task<IResult>> send, ref Task<IResult> result)
		{
			if (!Needed(out var controller, out var stacks))
			{
				return true;
			}

			result = RunTask(controller, stacks, send);
			return false;
		}

		private static async Task<IResult> RunTask(InventoryController controller, List<Item> stacks, Func<Task<IResult>> send)
		{
			var locked = await SetAll(controller, Remembered(stacks), EItemPinLockState.Locked);
			Plugin.Trace($"CashGuard: {locked.Count} money stack(s) locked for the payment");
			try
			{
				Task<IResult> task;
				Passing = true;
				try
				{
					task = send();
				}
				finally
				{
					Passing = false;
				}

				return await task;
			}
			finally
			{
				await SetAll(controller, locked, null);
			}
		}
	}

	// The calls that spend money, each wrapped by CashGuard
	internal class GuardInsurancePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(ClientBackendSession).GetMethod(nameof(ClientBackendSession.InsureItems), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		[PatchPrefix]
		private static bool Prefix(ClientBackendSession __instance, string[] items, string traderId, Callback callback) =>
			CashGuard.GuardCallback(next => { CashGuard.Passing = true; try { __instance.InsureItems(items, traderId, next); } finally { CashGuard.Passing = false; } }, callback);
	}

	internal class GuardTraderRepairPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(ClientBackendSession).GetMethod(nameof(ClientBackendSession.RepairItemsByTrader), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		[PatchPrefix]
		private static bool Prefix(ClientBackendSession __instance, string traderId, RepairItem repairItem, ref Task<IResult> __result) =>
			CashGuard.GuardTask(() => __instance.RepairItemsByTrader(traderId, repairItem), ref __result);
	}

	internal class GuardTraderBuyPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(ClientBackendSession).GetMethod(nameof(ClientBackendSession.ConfirmPurchase), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		[PatchPrefix]
		private static bool Prefix(ClientBackendSession __instance, string traderId, string itemId, int count, int schemeId, TradingItemReference[] items, ref Task<IResult> __result) =>
			CashGuard.GuardTask(() => __instance.ConfirmPurchase(traderId, itemId, count, schemeId, items), ref __result);
	}

	internal class GuardFleaBuyPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(EftClientBackendSession).GetMethod(nameof(EftClientBackendSession.RagfairPurchaseOffers), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		[PatchPrefix]
		private static bool Prefix(EftClientBackendSession __instance, CommoditiesToPurchase commodities, Callback callback) =>
			CashGuard.GuardCallback(next => { CashGuard.Passing = true; try { __instance.RagfairPurchaseOffers(commodities, next); } finally { CashGuard.Passing = false; } }, callback);
	}

	internal class GuardClothesPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(ClientBackendSession).GetMethod(nameof(ClientBackendSession.BuyCustomizationWear), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		[PatchPrefix]
		private static bool Prefix(ClientBackendSession __instance, string offer, IEnumerable<ItemToHandover> items, Callback onFinished) =>
			CashGuard.GuardCallback(next => { CashGuard.Passing = true; try { __instance.BuyCustomizationWear(offer, items, next); } finally { CashGuard.Passing = false; } }, onFinished);
	}
}
