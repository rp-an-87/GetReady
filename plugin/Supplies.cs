using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
		// What the owner set up does not work: the places it is set to bring to are not on you
		internal static string NoPlaceOnYou(IEnumerable<string> places)
		{
			var list = places.ToList();
			var what = list.Count == 1 ? $"set to bring to {list[0]}, which you are not carrying" : $"set to bring to {string.Join(", ", list)}, none of which you are carrying";
			return what + ": carry one, or add another place under \"Bring to\" in Setup";
		}

		private class Context
		{
			public InventoryController Controller;
			public SuppliesSettings Settings;
			public List<Item> Carried;           // on you, apart from what the template of a case looks after
			public List<Item> Stash;             // apart from what an earlier step has claimed
			public HashSet<Item> Owned;          // in a special slot of the template: counted, never swapped here
			public List<Item> Leaving;           // about to be swapped out of a special slot: no longer counted
			public List<Item> Planned = new List<Item>(); // about to be brought by the groups above (the grenade total counts it)
			public Func<string, CaseEntry> CashMuted; // the case you carry that takes this currency off the Cash step, or null
			public Func<string, bool> PlaceExists = place => true; // whether you have this place (a wallet on you, a vest worn...)
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var inventory = controller.Inventory;
			return Plan(controller, Game.CarriedAndSecured(inventory).ToList(), Game.Stash(inventory), currency => CasesStep.CashMutedBy(inventory, currency), place => Game.HasPlace(inventory, place));
		}

		// The plan for what is on you (carried and in the secure container) and what is in the stash; the controller is only for the moves
		internal List<PlanLine> Plan(InventoryController controller, List<Item> carriedAndSecured, List<Item> stash, Func<string, CaseEntry> cashMuted, Func<string, bool> placeExists = null)
		{
			var claimed = Steps.Claimed.ToList();
			var context = new Context
			{
				Controller = controller,
				CashMuted = cashMuted,
				PlaceExists = placeExists ?? (place => true),
				Settings = SettingsStore.Current.Supplies,
				Carried = carriedAndSecured.Where(item => !Game.InsideTemplatedCase(item)).ToList(),
				Stash = stash.Where(item => !claimed.Contains(item)).ToList(),
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
			// The money the cases you carry will hold counts toward the target; the Cash step brings only what is still missing
			float inCases = def.Money ? Steps.Of(Steps.CaseMoney, def.Unit) : 0;
			var progress = Steps.Progress(def.Name, context.Carried.Sum(def.WeightOf) + inCases - leaving + bringing, setting.Target, def.Unit, def.ActiveEntries().FirstOrDefault()?.Template);
			lines.Add(progress);

			if (def.Money)
			{
				var muted = context.CashMuted(def.Unit);
				if (muted != null)
				{
					progress.Skipped = true;
					progress.Note = CasesStep.CashMuteText(muted);
					progress.By = muted;
					float wanted = setting.Target - context.Carried.Sum(def.WeightOf) - inCases;
					if (wanted > 0)
					{
						lines.Add(Steps.Info($"Would bring {Game.Amount(def.Unit, wanted)}, but the Cash step is skipped while you carry {CasesStep.LabelOf(muted)}"));
					}

					return;
				}
			}

			if (places.Count == 0)
			{
				lines.Add(Steps.Problem(def.Name, "every place is switched off: switch at least one on in Setup", def.ActiveEntries().FirstOrDefault()?.Template));
				return;
			}

			// A place you do not have (no wallet on you, no vest worn) cannot be brought to: the places left are the ones you have
			var wanted_places = def.PlacesToTry();
			places = places.Where(context.PlaceExists).ToList();
			if (places.Count == 0)
			{
				lines.Add(Steps.Problem(def.Name, NoPlaceOnYou(wanted_places), def.ActiveEntries().FirstOrDefault()?.Template));
				return;
			}

			if (setting.ReplaceWorse && !def.Stack)
			{
				float fixedWeight = context.Owned.Sum(def.WeightOf) - leaving;
				PlanSwaps(def, context, fixedWeight, places, lines);
			}

			float need = setting.Target - context.Carried.Sum(def.WeightOf) - inCases + leaving - bringing;
			need = PlanBring(def, context, need, places, lines);
			if (def.Money && need > 0)
			{
				need = PlanReleased(def, context, need, places, lines);
			}

			if (need > 0)
			{
				lines.Add(Steps.Problem(def.Name, $"Missing {Game.Amount(def.Unit, need)}: {WhyNoneLeft(def, context, need)}", def.ActiveEntries().FirstOrDefault()?.Template));
			}
		}

		// Why the stash could not make up what is missing: nothing there, or only items too big for it (the option is off), and how many
		// are locked
		private static string WhyNoneLeft(GroupDef def, Context context, float need)
		{
			var active = new HashSet<string>(def.ActiveEntries().Select(entry => entry.Template));
			if (active.Count == 0)
			{
				return "every item of this group is switched off (open Items and switch one on)";
			}

			int locked = context.Stash.Count(item => active.Contains(item.TemplateId.ToString()) && item.PinLockState == EItemPinLockState.Locked && !context.Planned.Contains(item));
			var tooBig = def.Stack || def.Setting.AllowOvershoot
				? null
				: def.Candidates(context.Stash).Where(item => !context.Planned.Contains(item) && def.WeightOf(item) > need).OrderBy(def.WeightOf).FirstOrDefault();
			if (tooBig != null)
			{
				return $"the smallest in the stash, {Game.NameOf(tooBig)} ({def.WeightOf(tooBig):N0}), is more than is missing and \"Take a bigger item when only a little is missing\" is off";
			}

			return locked > 0 ? $"none left in the stash that you haven't locked ({locked} locked)" : "none left in the stash";
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
				bool firstPass = pass == 0;
				while (need > 0)
				{
					// Still usable this pass, in priority order
					var usable = candidates.Where(candidate =>
					{
						if (brought.Any(pair => pair.Key == candidate))
						{
							return false;
						}

						float size = def.WeightOf(candidate);
						if (size <= 0 || (!def.Stack && !def.Setting.AllowOvershoot && size > need))
						{
							return false;
						}

						int owned;
						counts.TryGetValue(candidate.TemplateId.ToString(), out owned);
						int limit = def.MaxOf(candidate.TemplateId.ToString());
						return !(firstPass && limit > 0 && owned >= limit);
					}).ToList();

					// The ranking decides which item comes next. Among copies of that same item, one that covers all that is missing comes before
					// a partly used one that would need another copy after it (two splints, a 4/7 and a 7/7, where one 7/7 was enough)
					var item = usable.FirstOrDefault();
					if (item == null)
					{
						break;
					}

					if (!def.Stack && def.WeightOf(item) < need)
					{
						item = usable.FirstOrDefault(candidate => candidate.TemplateId == item.TemplateId && def.WeightOf(candidate) >= need) ?? item;
					}

					float weight = def.WeightOf(item);
					var template = item.TemplateId.ToString();
					int have;
					counts.TryGetValue(template, out have);
					int max = def.MaxOf(template);
					bool atLimit = max > 0 && have >= max;

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
					Text = def.Money ? $"Bring {Game.Amount(def.Unit, amount)} to {places[0]}" : $"Bring {amount}x {group.Key}{over}",
					Run = () => SupplyMoves.MoveAll(context.Controller, moves, places)
				});
			}

			return need;
		}

		// Money a case gives up to the stash this pass (Take out the rest) is not in the stash yet when the plan is made, so it is brought on from
		// there when the line runs, after the case's own moves: whichever stacks of the stash hold the money then. Returns what is still missing.
		private static float PlanReleased(GroupDef def, Context context, float need, List<string> places, List<PlanLine> lines)
		{
			int amount = (int)Math.Min(need, Steps.Of(Steps.Released, def.Unit));
			if (amount <= 0)
			{
				return need;
			}

			var template = CasesStep.TemplateOfCurrency(def.Unit);
			lines.Add(new PlanLine
			{
				Template = template,
				Items = 1,
				Text = $"Bring {Game.Amount(def.Unit, amount)} to {places[0]}",
				Run = () =>
				{
					var moves = new List<KeyValuePair<Item, int>>();
					int left = amount;
					foreach (var stack in Game.Stash(Game.InventoryOf(context.Controller)).Where(item => item.TemplateId.ToString() == template && item.PinLockState != EItemPinLockState.Locked))
					{
						if (left <= 0)
						{
							break;
						}

						int count = Math.Min(stack.StackObjectsCount, left);
						left -= count;
						moves.Add(new KeyValuePair<Item, int>(stack, count));
					}

					return left > 0
						? Task.FromResult($"only {Game.Amount(def.Unit, amount - left)} of the {Game.Amount(def.Unit, amount)} reached the stash")
						: SupplyMoves.MoveAll(context.Controller, moves, places);
				}
			});

			return need - amount;
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

			var groups = Defs.Where(def => !def.IsQuota && !def.Stack && !def.NotGrenade).ToList();
			var grenades = new HashSet<string>(groups.SelectMany(def => def.Entries.Select(entry => entry.Template)));
			int have = context.Carried.Concat(Steps.Bringing).Concat(context.Planned).Count(item => grenades.Contains(item.TemplateId.ToString()));
			int need = setting.Target - have;
			var shown = context.Carried.Concat(Steps.Bringing).Concat(context.Planned).FirstOrDefault(item => grenades.Contains(item.TemplateId.ToString()))
				?? context.Stash.FirstOrDefault(item => grenades.Contains(item.TemplateId.ToString()));
			lines.Add(Steps.Progress(quota.Name, setting.Target - Math.Max(need, 0), setting.Target, "grenades", shown?.TemplateId.ToString()));
			if (need <= 0)
			{
				return;
			}

			// One pile per non-impact type, non-FiR first. A random type is picked before a grenade, so a type the stash has
			// many of (say M67) isn't picked more often than the others.
			var available = context.Stash.Where(item => !context.Planned.Contains(item) && item.PinLockState != EItemPinLockState.Locked).ToList();
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
				var pile = piles[Game.RandomIndex(piles.Count)];
				picked.Add(pile.Dequeue());
				if (pile.Count == 0)
				{
					piles.Remove(pile);
				}

				need--;
			}

			var places = quota.PlacesToTry();
			if (picked.Count > 0 && places.Count > 0 && !places.Any(context.PlaceExists))
			{
				lines.Add(Steps.Problem(quota.Name, NoPlaceOnYou(places)));
				return;
			}

			places = places.Where(context.PlaceExists).ToList();
			if (picked.Count > 0 && places.Count == 0)
			{
				lines.Add(Steps.Problem(quota.Name, "every place is switched off: switch at least one on in Setup"));
				return;
			}

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
