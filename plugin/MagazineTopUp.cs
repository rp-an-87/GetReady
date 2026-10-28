using System;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;

namespace GetReady
{
	// Hotkey on a hovered magazine (stash and raid): fills it up following the rules in AmmoRules (the ammo it already holds, or what
	// the things around it hold; nothing is guessed). Uses the controller's own LoadMagazine, so in a raid it is the timed load that
	// ContinuousLoadAmmo carries on outside the inventory.
	internal static class MagazineTopUp
	{
		private static readonly FieldInfo ControllerField = typeof(ItemUiContext).GetField("_inventoryController", BindingFlags.Instance | BindingFlags.NonPublic);

		private static bool _running;

		// A magazine that isn't full (whether there is ammo to fill it is looked for when the hotkey is pressed)
		internal static bool IsApplicable(Item item) => item is Magazine magazine && magazine.Count < magazine.MaxCount;

		internal static void Update()
		{
			if (!Plugin.TopUpHotkey.Value.IsDown())
			{
				return;
			}

			var uiContext = ItemUiContext.Instance;
			var magazine = uiContext?.CurrentItemContext?.Item as Magazine;
			var controller = uiContext == null ? null : ControllerField.GetValue(uiContext) as InventoryController;
			if (magazine != null && controller != null)
			{
				Run(controller, magazine);
			}
		}

		private static async void Run(InventoryController controller, Magazine magazine)
		{
			if (_running)
			{
				return;
			}

			_running = true;
			try
			{
				string message = await TopUp(controller, magazine);
				if (message != null)
				{
					NotificationManager.DisplayMessageNotification(message);
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Magazine top-up failed: {e}");
			}
			finally
			{
				_running = false;
			}
		}

		internal static async System.Threading.Tasks.Task<string> TopUp(InventoryController controller, Magazine magazine)
		{
			if (magazine.Count >= magazine.MaxCount)
			{
				return null;
			}

			var plan = AmmoRules.PlanFill(controller, AmmoRules.AmmoStock.Build(controller), magazine);
			if (plan.Problem == "no compatible ammo left")
			{
				return "Top-up: no compatible ammo found.";
			}

			if (plan.Problem != null)
			{
				return $"Top-up: nothing to follow for this magazine. Compatible ammo: {plan.Options}.";
			}

			int loaded = await AmmoRules.Load(controller, plan);
			return loaded > 0 ? $"Top-up: loaded {loaded} {plan.TypeName} from {plan.Sources}. Followed: {plan.Reason}." : "Top-up: nothing could be loaded.";
		}
	}
}
