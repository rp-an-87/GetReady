using System.Linq;
using Diz.LanguageExtensions;
using System.Threading.Tasks;
using EFT.InventoryLogic;

namespace GetReady
{
	// The quick-slot keys (4 to 0 and G): after getting ready, the best item of a group you carry is put on the key it is set to.
	// Only items in the pockets or the tactical vest can be bound, that is the game's rule.
	internal static class Hotkeys
	{
		// "" is no key; 4 to 9, then 0 (the tenth slot), then G (the grenade key)
		internal static readonly string[] Choices = { "", "4", "5", "6", "7", "8", "9", "0", "G" };

		internal static string Next(string choice) => Choices[(System.Array.IndexOf(Choices, choice) + 1) % Choices.Length];

		internal static string Label(string choice) => choice == "" ? "No key" : choice;

		private static EBoundItem? KeyOf(string choice)
		{
			switch (choice)
			{
				case "4": return EBoundItem.Item4;
				case "5": return EBoundItem.Item5;
				case "6": return EBoundItem.Item6;
				case "7": return EBoundItem.Item7;
				case "8": return EBoundItem.Item8;
				case "9": return EBoundItem.Item9;
				case "0": return EBoundItem.Item10;
				case "G": return EBoundItem.ItemG;
				default: return null;
			}
		}

		// Binds the best carried item of every group that has a key. A key that already holds an item of the group you still
		// carry is left alone. Returns how many keys were set.
		internal static async Task<int> Apply(InventoryController controller)
		{
			int set = 0;
			var groups = Steps.All.Where(step => step.Enabled).OfType<SuppliesStep>().SelectMany(step => step.Groups);
			foreach (var def in groups)
			{
				var key = KeyOf(def.HotkeyChoice);
				if (key == null || !def.Bindable || def.Setting.Target <= 0)
				{
					continue;
				}

				var carried = Game.Carried(Game.InventoryOf(controller)).Where(item => def.WeightOf(item) > 0 && controller.IsAtBindablePlace(item)).ToList();
				var best = carried.OrderBy(def.PriorityOf).ThenByDescending(Game.ResourceOf).FirstOrDefault();
				if (best == null)
				{
					continue;
				}

				Item bound;
				if (Game.InventoryOf(controller).FastAccess.BoundItems.TryGetValue(key.Value, out bound) && carried.Contains(bound))
				{
					continue;
				}

				var operation = BindResult.Run(controller, best, key.Value, true);
				if (operation.Failed)
				{
					Plugin.Log.LogWarning($"Could not bind {Game.NameOf(best)} to {def.HotkeyChoice}: {operation.Error}");
					continue;
				}

				var outcome = await Game.RunOperation(controller, (OperationResult)operation);
				if (outcome.Failed)
				{
					Plugin.Log.LogWarning($"Could not bind {Game.NameOf(best)} to {def.HotkeyChoice}: {outcome.Error}");
					continue;
				}

				set++;
			}

			return set;
		}
	}
}
