using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;

namespace GetReady
{
	// Moving supplies: bringing items from the stash, and swapping carried ones for better ones
	internal static class SupplyMoves
	{
		// Brings every item (a count of 0 is the whole item). A full place doesn't stop the rest: every item is tried.
		internal static async Task<string> MoveAll(InventoryController controller, List<KeyValuePair<Item, int>> moves, List<string> places)
		{
			int failed = 0;
			string last = null;
			foreach (var move in moves)
			{
				var error = await Game.MoveTo(controller, move.Key, places, move.Value);

				// The game can accept a move and apply it a moment later: wait for it, and call it failed if it never shows up
				if (error == null && move.Value == 0 && !await Game.WaitUntilCarried(controller, move.Key))
				{
					error = "the game accepted the move but the item is still in the stash";
				}

				if (error != null)
				{
					failed++;
					last = error;
					Plugin.Log.LogWarning($"Could not move {Game.NameOf(move.Key)} ({move.Key.Id}): {error}");
				}
			}

			return failed == 0 ? null : moves.Count == 1 ? Game.Friendly(last) : $"{failed} of {moves.Count} could not be moved ({Game.Friendly(last)})";
		}

		// The carried one goes to the stash first, and goes back if the better one can't be brought
		internal static async Task<string> Swap(InventoryController controller, Item worse, Item better, List<string> places)
		{
			var error = await Game.MoveTo(controller, worse, new[] { "Stash" });
			if (error != null)
			{
				return error;
			}

			error = await Game.MoveTo(controller, better, places);
			if (error != null)
			{
				await Game.MoveTo(controller, worse, places);
			}

			return error;
		}

		// "Salewa (120 left)": the health resource only when the item has one
		internal static string Describe(Item item)
		{
			float resource = Game.ResourceOf(item);
			return resource > 0f ? $"{Game.NameOf(item)} ({resource:0} left)" : Game.NameOf(item);
		}

		// Replaces carried items by better ones from the stash. Only items needed to reach the target are swapped: the best
		// carried items are taken until the target is met, and anything beyond that is a spare that is left alone. A swap must
		// not lower the total of the needed items (or the target). Works on copies of the lists, which end up as they will
		// look after the swaps.
		// Items in owned are managed by another step (the special slots template) and are never swapped here; fixedWeight is
		// what they count for.
		internal static List<KeyValuePair<Item, Item>> PlanSwaps(GroupDef def, int target, List<Item> carried, List<Item> stash, ICollection<Item> owned, float fixedWeight)
		{
			var swaps = new List<KeyValuePair<Item, Item>>();
			var handled = new HashSet<string>();

			for (int guard = 0; guard < 20; guard++)
			{
				var needed = new List<Item>();
				float total = fixedWeight;
				foreach (var item in carried.Where(item => def.WeightOf(item) > 0 && !owned.Contains(item)).OrderBy(def.PriorityOf).ThenByDescending(Game.ResourceOf))
				{
					if (total >= target)
					{
						break;
					}

					needed.Add(item);
					total += def.WeightOf(item);
				}

				var candidates = def.Candidates(stash.Where(item => !handled.Contains(item.Id.ToString())).ToList()).ToList();

				Item worse = null;
				Item better = null;
				foreach (var current in needed.Where(item => !handled.Contains(item.Id.ToString())).OrderByDescending(def.PriorityOf).ThenBy(Game.ResourceOf))
				{
					better = candidates.FirstOrDefault(candidate => IsBetter(def, candidate, current, total < target) && WithinLimit(def, carried, candidate, current)
						&& total - def.WeightOf(current) + def.WeightOf(candidate) >= Math.Min(total, target));
					if (better != null)
					{
						worse = current;
						break;
					}
				}

				if (worse == null)
				{
					return swaps;
				}

				handled.Add(worse.Id.ToString());
				handled.Add(better.Id.ToString());
				swaps.Add(new KeyValuePair<Item, Item>(worse, better));
				carried.Remove(worse);
				stash.Add(worse);
				carried.Add(better);
				stash.Remove(better);
			}

			return swaps;
		}

		// A swap doesn't take the group over the owner's limit for an item (replacing an item by one of its own kind never does)
		private static bool WithinLimit(GroupDef def, List<Item> carried, Item candidate, Item current)
		{
			var template = candidate.TemplateId.ToString();
			int max = def.MaxOf(template);
			return max <= 0 || template == current.TemplateId.ToString() || carried.Count(item => item.TemplateId.ToString() == template) < max;
		}

		// The same template with more resource (FiR is the tie-breaker: non-FiR wins a tie), or a higher priority template,
		// but only while the group is still short of its target: a set that already meets it (say two single tourniquets
		// instead of a CALOK) isn't upgraded to a higher priority item
		private static bool IsBetter(GroupDef def, Item candidate, Item current, bool belowTarget)
		{
			int candidatePriority = def.PriorityOf(candidate);
			int currentPriority = def.PriorityOf(current);
			if (candidatePriority != currentPriority)
			{
				return belowTarget && candidatePriority < currentPriority;
			}

			if (!SettingsStore.Current.Supplies.SwapFiRForNonFiR || candidate.MarkedAsSpawnedInSession == current.MarkedAsSpawnedInSession)
			{
				return Game.ResourceOf(candidate) > Game.ResourceOf(current);
			}

			// FiR only counts against an item: a non-FiR one wins a tie, a FiR one has to bring more resource
			return candidate.MarkedAsSpawnedInSession
				? Game.ResourceOf(candidate) > Game.ResourceOf(current)
				: Game.ResourceOf(candidate) >= Game.ResourceOf(current);
		}
	}
}
