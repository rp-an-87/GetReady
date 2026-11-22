using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.HealthSystem;
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
			!SettingsStore.Current.Cases.ProtectMoney ? new HashSet<string>() : new HashSet<string>(CasesStep.Entries.Where(entry => entry.Recorded && CasesStep.MoneyOf(entry).Count > 0).Select(entry => entry.CaseId));

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

		internal static List<KeyValuePair<Item, EItemPinLockState>> Remembered(List<Item> stacks) =>
			stacks.Select(stack => new KeyValuePair<Item, EItemPinLockState>(stack, stack.PinLockState)).ToList();

		// The wrapped stacks for the call about to go out, or null when there is nothing to guard (then the call goes out as it is)
		internal static bool Needed(out InventoryController controller, out List<Item> stacks)
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
				if (Plugin.Profiling) Plugin.Trace($"CashGuard: {locked.Count} money stack(s) locked for the payment");
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
			if (Plugin.Profiling) Plugin.Trace($"CashGuard: {locked.Count} money stack(s) locked for the payment");
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

	// The health treatment at the therapist is sent queued and answered by a callback that does nothing, so there is no answer to wait on in
	// the call itself: the stacks are locked before the request goes out and let go when the game's own (empty) answer handler runs, or after
	// half a minute if no answer comes.
	internal static class HealthGuard
	{
		private static InventoryController _controller;
		private static List<KeyValuePair<Item, EItemPinLockState>> _locked;
		private static int _open;

		internal static bool Guard(Action send)
		{
			if (CashGuard.Passing || !CashGuard.Needed(out var controller, out var stacks))
			{
				return true;
			}

			Run(controller, stacks, send);
			return false;
		}

		private static async void Run(InventoryController controller, List<Item> stacks, Action send)
		{
			try
			{
				_open++;
				var locked = await CashGuard.SetAll(controller, CashGuard.Remembered(stacks), EItemPinLockState.Locked);
				_controller = controller;
				_locked = (_locked ?? new List<KeyValuePair<Item, EItemPinLockState>>()).Concat(locked).ToList();
				if (Plugin.Profiling) Plugin.Trace($"CashGuard: {locked.Count} money stack(s) locked for the health treatment");
				CashGuard.Passing = true;
				try
				{
					send();
				}
				finally
				{
					CashGuard.Passing = false;
				}

				await Task.Delay(30000);
				Release(true);
			}
			catch (Exception exception)
			{
				Plugin.Log.LogWarning($"CashGuard: {exception.Message}");
				Release(true);
			}
		}

		// The game's answer handler ran (one treatment answered), or the wait is over
		internal static async void Release(bool timeout)
		{
			try
			{
				if (_open <= 0)
				{
					return;
				}

				_open--;
				if (_open > 0 && !timeout)
				{
					return;
				}

				_open = 0;
				var locked = _locked;
				_locked = null;
				if (locked != null && _controller != null)
				{
					await CashGuard.SetAll(_controller, locked, null);
				}
			}
			catch (Exception exception)
			{
				Plugin.Log.LogWarning($"CashGuard: {exception.Message}");
			}
		}
	}

	internal class GuardHealthPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(EftClientBackendSession).GetMethod(nameof(EftClientBackendSession.RestoreHealth), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		[PatchPrefix]
		private static bool Prefix(EftClientBackendSession __instance, HealthTreatmentData treatmentData, List<ItemToHandover> cost, string traderId) =>
			HealthGuard.Guard(() => __instance.RestoreHealth(treatmentData, cost, traderId));
	}

	// The game's answer handler of that request (it does nothing itself)
	internal class GuardHealthAnswerPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(EftClientBackendSession).GetNestedType("CG_Class1577", BindingFlags.Public | BindingFlags.NonPublic).GetMethod("method_14", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		[PatchPostfix]
		private static void Postfix() => HealthGuard.Release(false);
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
