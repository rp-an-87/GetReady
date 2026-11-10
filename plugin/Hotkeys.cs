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

		// The groups that can have keys: the ones of the steps that are switched on
		private static System.Collections.Generic.IEnumerable<GroupDef> KeyedGroups() =>
			Steps.All.Where(step => step.Enabled).OfType<SuppliesStep>().SelectMany(step => step.Groups).Where(def => def.Bindable);

		// Another group that has the key too (its first or its second): only one item can be on a key, so the later one would take it
		internal static bool Clash(GroupDef def, string choice) =>
			choice != "" && KeyedGroups().Any(other => other != def && (other.HotkeyChoice == choice || other.HotkeyChoice2 == choice));

		// Binds the best carried item of every group that has a key, and the next best on its second key. A key that already holds an item of
		// the group you still carry is left alone; the second key never takes the item that is on the first. Returns how many keys were set.
		internal static async Task<int> Apply(InventoryController controller)
		{
			int set = 0;
			foreach (var def in KeyedGroups())
			{
				if (def.Setting.Target <= 0)
				{
					continue;
				}

				var carried = Game.Carried(Game.InventoryOf(controller)).Where(item => def.WeightOf(item) > 0 && controller.IsAtBindablePlace(item))
					.OrderBy(def.PriorityOf).ThenByDescending(Game.ResourceOf).ToList();
				var used = new System.Collections.Generic.List<Item>();
				string first = def.HotkeyChoice;
				foreach (var choice in new[] { first, def.HotkeyChoice2 })
				{
					var key = KeyOf(choice);
					// A second key that is the same as the first is ignored
					if (key == null || (used.Count > 0 && choice == first))
					{
						continue;
					}

					Item bound;
					if (Game.InventoryOf(controller).FastAccess.BoundItems.TryGetValue(key.Value, out bound) && carried.Contains(bound) && !used.Contains(bound))
					{
						used.Add(bound);
						continue;
					}

					var best = carried.FirstOrDefault(item => !used.Contains(item));
					if (best == null)
					{
						continue;
					}

					var operation = BindResult.Run(controller, best, key.Value, true);
					if (operation.Failed)
					{
						Plugin.Log.LogWarning($"Could not bind {Game.NameOf(best)} to {choice}: {operation.Error}");
						continue;
					}

					var outcome = await Game.RunOperation(controller, (OperationResult)operation);
					if (outcome.Failed)
					{
						Plugin.Log.LogWarning($"Could not bind {Game.NameOf(best)} to {choice}: {outcome.Error}");
						continue;
					}

					used.Add(best);
					set++;
				}
			}

			return set;
		}
	}
}
