using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace GetReady
{
	// A step made of supply groups (healing, grenades, cash): each group has a target and places to put it. What you already
	// carry counts towards the target, worse carried items are swapped for better ones from the stash, and the stash fills the
	// rest in priority order.
	internal abstract class SuppliesStep : IStep
	{
		public abstract string Name { get; }
		public abstract bool Enabled { get; set; }
		protected abstract GroupDef[] Defs { get; }

		public IReadOnlyList<GroupDef> Groups => Defs;

		// What a plan works on: the lists change as swaps and bring-ups are planned, so the later groups see the result
		private class Context
		{
			public InventoryController Controller;
			public SuppliesSettings Settings;
			public List<Item> Carried;           // on you, apart from what an injector case looks after
			public List<Item> Stash;             // apart from what an earlier step has claimed
			public HashSet<Item> Owned;          // in a special slot of the template: counted, never swapped here
			public List<Item> Leaving;           // about to be swapped out of a special slot: no longer counted
			public List<Item> Planned = new List<Item>(); // about to be brought by the groups above (the grenade total counts it)
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var inventory = controller.Inventory;
			var claimed = Steps.Claimed.ToList();
			var context = new Context
			{
				Controller = controller,
				Settings = SettingsStore.Current.Supplies,
				Carried = Game.CarriedAndSecured(inventory).Where(item => !Game.InsideInjectorCase(item)).ToList(),
				Stash = Game.Stash(inventory).Where(item => !claimed.Contains(item)).ToList(),
				Owned = new HashSet<Item>(Steps.Owned),
				Leaving = Steps.Leaving.ToList()
			};

			var lines = new List<PlanLine>();
			foreach (var def in Defs.Where(def => !def.IsQuota))
			{
				PlanGroup(def, context, lines);
			}

			// After the groups, so their minimums come first
			foreach (var def in Defs.Where(def => def.IsQuota))
			{
				PlanQuota(def, context, lines);
			}

			return lines;
		}

		private static void PlanGroup(GroupDef def, Context context, List<PlanLine> lines)
		{
			var setting = def.Setting;
			if (setting.Target <= 0)
			{
				return;
			}

			var places = def.PlacesToTry();
			float bringing = Steps.Bringing.Sum(def.WeightOf);
			float leaving = context.Leaving.Sum(def.WeightOf);
			lines.Add(Steps.Progress(def.Name, context.Carried.Sum(def.WeightOf) - leaving + bringing, setting.Target, def.Unit, def.ActiveEntries().FirstOrDefault()?.Template));

			if (context.Settings.SwapWorse && !def.Stack)
			{
				float fixedWeight = context.Owned.Sum(def.WeightOf) - leaving;
				PlanSwaps(def, context, fixedWeight, places, lines);
			}

			float need = setting.Target - context.Carried.Sum(def.WeightOf) + leaving - bringing;
			need = PlanBring(def, context, need, places, lines);
			if (need > 0)
			{
				lines.Add(Steps.Problem(def.Name, $"Missing {need:N0} {def.Unit}: none left in the stash", def.ActiveEntries().FirstOrDefault()?.Template));
			}
		}

		private static void PlanSwaps(GroupDef def, Context context, float fixedWeight, List<string> places, List<PlanLine> lines)
		{
			foreach (var swap in SupplyMoves.PlanSwaps(def, def.Setting.Target, context.Carried, context.Stash, context.Owned, fixedWeight))
			{
				var worse = swap.Key;
				var better = swap.Value;
				lines.Add(new PlanLine
				{
					Template = better.TemplateId.ToString(),
					Text = $"Replace {SupplyMoves.Describe(worse)} with {SupplyMoves.Describe(better)}",
					Run = () => SupplyMoves.Swap(context.Controller, worse, better, places)
				});
			}
		}

		// Plans what the stash brings for the missing amount; returns what is still missing. The owner's per-item limits come first:
		// items are taken in priority order without going over a limit, and only when that can't make up the amount the limits are
		// given up (the lines say so).
		private static float PlanBring(GroupDef def, Context context, float need, List<string> places, List<PlanLine> lines)
		{
			var candidates = def.Candidates(context.Stash).ToList();
			var counts = context.Carried.Where(item => def.WeightOf(item) > 0).GroupBy(item => item.TemplateId.ToString()).ToDictionary(group => group.Key, group => group.Count());
			var brought = new List<KeyValuePair<Item, int>>();
			var overLimit = new HashSet<Item>();

			for (int pass = 0; pass < 2 && need > 0; pass++)
			{
				foreach (var item in candidates)
				{
					if (need <= 0)
					{
						break;
					}

					if (brought.Any(pair => pair.Key == item))
					{
						continue;
					}

					float weight = def.WeightOf(item);
					if (weight <= 0 || (!def.Stack && !context.Settings.AllowOvershoot && weight > need))
					{
						continue;
					}

					var template = item.TemplateId.ToString();
					int have;
					counts.TryGetValue(template, out have);
					int max = def.MaxOf(template);
					bool atLimit = max > 0 && have >= max;
					if (atLimit && pass == 0)
					{
						continue;
					}

					// A stack gives only what is missing, anything else moves whole (count 0)
					int count = def.Stack ? Math.Max(1, (int)Math.Min(weight, need)) : 0;
					brought.Add(new KeyValuePair<Item, int>(item, count));
					counts[template] = have + 1;
					if (atLimit)
					{
						overLimit.Add(item);
					}

					need -= def.Stack ? count : weight;
				}
			}

			context.Planned.AddRange(brought.Select(pair => pair.Key));
			foreach (var group in brought.GroupBy(pair => Game.NameOf(pair.Key)))
			{
				var moves = group.ToList();
				int amount = moves.Sum(pair => pair.Value > 0 ? pair.Value : 1);
				string over = moves.Any(pair => overLimit.Contains(pair.Key)) ? $" (over your limit of {def.MaxOf(moves[0].Key.TemplateId.ToString())}, nothing else is left)" : "";
				lines.Add(new PlanLine
				{
					Template = moves[0].Key.TemplateId.ToString(),
					Items = moves.Count,
					Text = def.Money ? $"Bring {amount:N0} {def.Unit} to {places[0]}" : $"Bring {amount}x {group.Key}{over}",
					Run = () => SupplyMoves.MoveAll(context.Controller, moves, places)
				});
			}

			return need;
		}

		// Tops the grenades up to the total with random non-impact ones from the stash. Every grenade of the groups counts
		// (impact ones too, and the ones about to be brought).
		private void PlanQuota(GroupDef quota, Context context, List<PlanLine> lines)
		{
			var setting = quota.Setting;
			if (setting.Target <= 0)
			{
				return;
			}

			var groups = Defs.Where(def => !def.IsQuota && !def.Stack).ToList();
			var grenades = new HashSet<string>(groups.SelectMany(def => def.Entries.Select(entry => entry.Template)));
			int have = context.Carried.Concat(Steps.Bringing).Concat(context.Planned).Count(item => grenades.Contains(item.TemplateId.ToString()));
			int need = setting.Target - have;
			lines.Add(Steps.Progress(quota.Name, setting.Target - Math.Max(need, 0), setting.Target, "grenades"));
			if (need <= 0)
			{
				return;
			}

			// One pile per non-impact type, non-FiR first. A random type is picked before a grenade, so a type the stash has
			// many of (say M67) isn't picked more often than the others.
			var available = context.Stash.Where(item => !context.Planned.Contains(item)).ToList();
			var piles = groups
				.Where(def => !def.Impact)
				.Select(def => new Queue<Item>(available
					.Where(item => def.ActiveEntries().Any(entry => entry.Template == item.TemplateId.ToString()))
					.OrderBy(Game.LooseBeforeFiR)
					.ThenBy(item => item.MarkedAsSpawnedInSession ? 1 : 0)
					.ThenBy(Game.LooseAfterFiR)
					.ThenBy(_ => Guid.NewGuid())))
				.Where(pile => pile.Count > 0)
				.ToList();

			var picked = new List<Item>();
			while (need > 0 && piles.Count > 0)
			{
				var pile = piles[UnityEngine.Random.Range(0, piles.Count)];
				picked.Add(pile.Dequeue());
				if (pile.Count == 0)
				{
					piles.Remove(pile);
				}

				need--;
			}

			var places = quota.PlacesToTry();
			foreach (var group in picked.GroupBy(item => Game.NameOf(item)))
			{
				var items = group.ToList();
				var moves = items.Select(item => new KeyValuePair<Item, int>(item, 0)).ToList();
				lines.Add(new PlanLine
				{
					Template = items[0].TemplateId.ToString(),
					Items = items.Count,
					Text = $"Bring {items.Count}x {group.Key}",
					Run = () => SupplyMoves.MoveAll(context.Controller, moves, places)
				});
			}

			if (need > 0)
			{
				lines.Add(Steps.Problem(quota.Name, $"Missing {need}: no other grenade left in the stash"));
			}
		}
	}
}
