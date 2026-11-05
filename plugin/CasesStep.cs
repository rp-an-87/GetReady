using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace GetReady
{
	// The cases you carry (a SICC, a docs case, a keytool, a wallet, an injector case): each is that very container, by item id, with what you
	// set up for it in Setup > Cases. The template is what the case holds: what is missing is brought from the stash (an item the stash does
	// not have is a problem), and what is not in the template is taken out (a switch). With exact positions each item goes back to the cell
	// it was recorded in (the way the injector case always worked), otherwise only the type and the count matter. A nested container (a
	// card holder in a docs case) is an item like any other for the case around it, and has a template of its own. Money is topped up to the
	// recorded amount, and with "exact money" the extra is taken out too. The map and cash switches of a case are read elsewhere (the map
	// screen, the Cash step).
	internal class CasesStep : IStep
	{
		public string Name => "Cases";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.Cases.Enabled; set => SettingsStore.Current.Cases.Enabled = value; }

		internal static readonly string[] Currencies = { "RUB", "EUR", "USD" };

		internal static string TemplateOfCurrency(string currency) => currency == "RUB" ? Game.Roubles : currency == "EUR" ? Game.Euros : Game.Dollars;

		internal static bool IsMoney(Item item) => Game.CurrencyOf(item.TemplateId.ToString()) != null;

		internal static List<CaseEntry> Entries => SettingsStore.Current.Cases.Cases;

		internal static CaseEntry EntryOf(string caseId) => Entries.FirstOrDefault(entry => entry.CaseId == caseId);

		// A container a case can be set up for: it has a grid and is not a piece of the gear itself (backpack, vest, pouch)
		internal static bool IsCase(Inventory inventory, CompoundItem item) =>
			item.Grids.Length > 0 && item.Parent?.Container?.ParentItem != inventory.Equipment;

		// Cases you carry, in the pockets, vest, backpack or the secured container
		internal static List<CompoundItem> FindCases(Inventory inventory) =>
			Game.CarriedAndSecured(inventory).OfType<CompoundItem>().Where(item => IsCase(inventory, item)).ToList();

		// What is directly in the case (a nested container counts as one item; its own contents are its business)
		internal static List<Item> Contents(CompoundItem container) => container.Grids.SelectMany(grid => grid.Items).ToList();

		internal static Item InCell(Grid grid, int x, int y) =>
			grid.Items.FirstOrDefault(item =>
			{
				var location = grid.GetItemLocation(item);
				return location.x == x && location.y == y;
			});

		// What a case is called: its tag when it has one, else its name
		internal static string LabelOf(CaseEntry entry) => !string.IsNullOrEmpty(entry.Tag) ? entry.Tag : string.IsNullOrEmpty(entry.CaseName) ? "Case" : entry.CaseName;

		// ---- the template

		// The entry of this very case, made when it has none, with its name and tag as they are now
		internal static CaseEntry Touch(CompoundItem container)
		{
			var id = container.Id.ToString();
			var entry = EntryOf(id);
			if (entry == null)
			{
				entry = new CaseEntry { CaseId = id };
				Entries.Add(entry);
			}

			var tag = container.GetItemComponent<TagComponent>();
			entry.CaseTemplate = container.TemplateId.ToString();
			entry.CaseName = Game.NameOf(container);
			entry.Tag = tag?.Name;
			entry.TagColor = tag?.Color ?? 0;
			return entry;
		}

		// Saves what the case holds now as its template (nothing at all is a template too: a case kept empty), item by item with its place.
		// The other settings of the case are kept.
		internal static CaseEntry Record(CompoundItem container)
		{
			var entry = Touch(container);
			entry.Recorded = true;
			entry.Items.Clear();
			entry.Money.Clear();
			for (int gridIndex = 0; gridIndex < container.Grids.Length; gridIndex++)
			{
				var grid = container.Grids[gridIndex];
				foreach (var item in grid.Items.Where(item => !IsMoney(item)))
				{
					var place = grid.GetItemLocation(item);
					entry.Items.Add(new CaseItem
					{
						Template = item.TemplateId.ToString(), Name = Game.NameOf(item), Count = item.StackObjectsCount,
						Grid = gridIndex, X = place.x, Y = place.y, Rotation = (int)place.r
					});
				}
			}

			var held = Contents(container);
			foreach (var currency in Currencies)
			{
				int amount = held.Where(item => item.TemplateId.ToString() == TemplateOfCurrency(currency)).Sum(item => item.StackObjectsCount);
				if (amount > 0)
				{
					entry.Money[currency] = amount;
				}
			}

			SettingsStore.Save();
			return entry;
		}

		// How many items (not money) a template holds
		internal static int ItemCount(CaseEntry entry) => entry.Items.Sum(item => item.Count);

		// Removes the template of a case, whether or not it is on you; its maps and cash switch stay
		internal static void Forget(string caseId)
		{
			var entry = EntryOf(caseId);
			if (entry == null)
			{
				return;
			}

			entry.Recorded = false;
			entry.Items.Clear();
			entry.Money.Clear();
			Prune(entry);
			SettingsStore.Save();
		}

		// Everything saved for a case (template, maps, cash switch), whether or not it is on you; nothing moves
		internal static void Clear(string caseId)
		{
			Entries.RemoveAll(entry => entry.CaseId == caseId);
			SettingsStore.Current.Cases.Collapsed.Remove(caseId);
			Pending.Remove(caseId);
			SettingsStore.Save();
		}

		internal static bool IsCollapsed(string caseId) => SettingsStore.Current.Cases.Collapsed.Contains(caseId);

		internal static void ToggleCollapsed(string caseId)
		{
			var collapsed = SettingsStore.Current.Cases.Collapsed;
			if (!collapsed.Remove(caseId))
			{
				collapsed.Add(caseId);
			}

			SettingsStore.Save();
		}

		// What is saved for a case, in one line: why it is in the list
		internal static string Summary(CaseEntry entry)
		{
			var parts = new List<string>();
			if (entry.Recorded)
			{
				int count = ItemCount(entry);
				parts.Add(entry.Money.Count > 0 ? $"tracking {count} item{(count == 1 ? "" : "s")} and money" : $"tracking {count} item{(count == 1 ? "" : "s")}");
			}

			if (entry.Maps.Count > 0)
			{
				parts.Add(entry.Maps.Count >= MapTags.Maps.Length ? "all maps" : "maps: " + string.Join(", ", entry.Maps));
			}

			if (entry.StopsCash)
			{
				parts.Add("cash step skipped");
			}

			return parts.Count == 0 ? "nothing saved" : string.Join(" · ", parts);
		}

		// A case with nothing set for it (no template, no maps, no cash switch) is not kept
		internal static void Prune(CaseEntry entry)
		{
			if (!entry.Recorded && !entry.StopsCash && entry.Maps.Count == 0)
			{
				Entries.Remove(entry);
			}
		}

		// A saved case was tagged or untagged: its saved tag follows. Returns whether there was a saved case with another tag.
		internal static bool OnTagChanged(string caseId, string tag, int color)
		{
			var entry = EntryOf(caseId);
			if (entry == null)
			{
				return false;
			}

			tag = string.IsNullOrEmpty(tag) ? null : tag;
			if (entry.Tag == tag && entry.TagColor == color)
			{
				return false;
			}

			entry.Tag = tag;
			entry.TagColor = color;
			SettingsStore.Save();
			return true;
		}

		// ---- the other settings of a case, read by the Cash step and the map screen

		// The case you carry that skips the Cash step, else null. The money a case holds is no reason to skip it: that money counts toward
		// the Cash step's target (Steps.CaseMoney) and the step brings only what is still missing.
		internal static CaseEntry CashMutedBy(Inventory inventory, string currency)
		{
			foreach (var item in Game.CarriedAndSecured(inventory))
			{
				var entry = EntryOf(item.Id.ToString());
				if (entry != null && entry.StopsCash)
				{
					return entry;
				}
			}

			return null;
		}

		// Why the Cash step is off
		internal static string CashMuteText(CaseEntry entry) => "Cash step skipped while you carry";

		// ---- keeping the template current while you rearrange things in the stash

		// Cases that had something added or taken out by hand, waiting for the screen to settle (the ids)
		internal static readonly HashSet<string> Pending = new HashSet<string>();

		// An item was added to or taken out of the container at this address
		internal static void OnItemEvent(ItemAddress address)
		{
			var parent = address?.Container?.ParentItem;
			if (parent == null || !SettingsStore.Current.Cases.AutoUpdate)
			{
				return;
			}

			var entry = EntryOf(parent.Id.ToString());
			if (entry != null && entry.Recorded)
			{
				Pending.Add(entry.CaseId);
			}
		}

		// Saves again the templates of the cases that were changed by hand, wherever they are (on you or in the stash). Returns how many.
		internal static int SyncPending(Inventory inventory)
		{
			if (Pending.Count == 0)
			{
				return 0;
			}

			var ids = Pending.ToList();
			Pending.Clear();
			var everywhere = Game.CarriedAndSecured(inventory).Concat(Game.Stash(inventory)).OfType<CompoundItem>().ToList();
			int done = 0;
			foreach (var id in ids)
			{
				var container = everywhere.FirstOrDefault(item => item.Id.ToString() == id);
				if (container != null && EntryOf(id)?.Recorded == true)
				{
					Record(container);
					done++;
				}
			}

			return done;
		}

		// ---- the plan

		public List<PlanLine> Plan(InventoryController controller)
		{
			var lines = new List<PlanLine>();
			var recorded = Entries.Where(entry => entry.Recorded).ToList();
			if (recorded.Count == 0)
			{
				return lines;
			}

			var inventory = Game.InventoryOf(controller);
			var cases = FindCases(inventory);
			var stash = Game.Stash(inventory);
			var taken = new HashSet<Item>();
			foreach (var entry in recorded)
			{
				var container = cases.FirstOrDefault(candidate => candidate.Id.ToString() == entry.CaseId);
				if (container != null)
				{
					lines.AddRange(PlanCase(controller, entry, container, stash, taken));
				}
			}

			return lines;
		}

		private static PlanLine Take(InventoryController controller, Item item, int count, string label, string name, bool money)
		{
			return new PlanLine
			{
				Text = money ? $"Take {Game.Amount(name, count)} out of {label} to the stash" : $"Take {name} out of {label} to the stash",
				Template = item.TemplateId.ToString(),
				Items = 1,
				Run = () => Game.MoveTo(controller, item, new[] { "Stash" }, count)
			}.WithItem(item);
		}

		// What a plan of the items says: how many items there are, how many are in place, and the lines
		private class Part
		{
			public int Total;
			public int InPlace;
			public List<PlanLine> Lines = new List<PlanLine>();
		}

		private static List<PlanLine> PlanCase(InventoryController controller, CaseEntry entry, CompoundItem container, List<Item> stash, HashSet<Item> taken)
		{
			var label = LabelOf(entry);
			Func<string, IEnumerable<Item>> candidates = template =>
				Game.Candidates(stash.Where(item => !taken.Contains(item) && !Steps.Claimed.Contains(item) && !Game.InsideTemplatedCase(item)), template);

			var items = entry.Exact ? PlanExact(controller, entry, container, label, candidates, taken) : PlanByType(controller, entry, container, label, candidates, taken);
			var money = PlanMoney(controller, entry, container, label, candidates, taken);
			var lines = new List<PlanLine> { Steps.Progress(label, items.InPlace + money.InPlace, items.Total + money.Total, "in place", entry.CaseTemplate) };
			lines.AddRange(items.Lines);
			lines.AddRange(money.Lines);
			return lines;
		}

		private static PlanLine Bring(InventoryController controller, Item source, int count, string text, CompoundItem container, string template)
		{
			return new PlanLine
			{
				Text = text,
				Template = template,
				Items = 1,
				Run = () => Game.MoveInto(controller, source, count, container)
			}.WithItem(source);
		}

		// By type and count: what is recorded and missing is brought, what is more than recorded or not recorded at all goes out
		private static Part PlanByType(InventoryController controller, CaseEntry entry, CompoundItem container, string label, Func<string, IEnumerable<Item>> candidates, HashSet<Item> taken)
		{
			var part = new Part();
			var held = Contents(container);
			var removals = new List<PlanLine>();
			var brings = new List<PlanLine>();
			var problems = new List<PlanLine>();
			var wantedByType = entry.Items.GroupBy(item => item.Template).Select(group => new { Template = group.Key, group.First().Name, Count = group.Sum(item => item.Count) }).ToList();

			foreach (var wanted in wantedByType)
			{
				var here = held.Where(item => item.TemplateId.ToString() == wanted.Template).ToList();
				int have = here.Sum(item => item.StackObjectsCount);
				part.Total += wanted.Count;
				part.InPlace += Math.Min(have, wanted.Count);
				if (have < wanted.Count)
				{
					int need = wanted.Count - have;
					foreach (var source in candidates(wanted.Template).ToList())
					{
						if (need <= 0)
						{
							break;
						}

						int count = Math.Min(source.StackObjectsCount, need);
						taken.Add(source);
						Steps.Claimed.Add(source);
						need -= count;
						brings.Add(Bring(controller, source, count, $"Bring {count}x {wanted.Name} to {label}", container, wanted.Template));
					}

					if (need > 0)
					{
						problems.Add(Steps.Problem(wanted.Name, $"Missing {need}: none left in the stash (for {label})", wanted.Template));
					}
				}
				else if (have > wanted.Count && entry.TakeOutRest)
				{
					int extra = have - wanted.Count;
					foreach (var item in here.AsEnumerable().Reverse())
					{
						if (extra <= 0)
						{
							break;
						}

						int count = Math.Min(item.StackObjectsCount, extra);
						extra -= count;
						removals.Add(Take(controller, item, count, label, wanted.Name, false));
					}
				}
			}

			if (entry.TakeOutRest)
			{
				foreach (var item in held.Where(item => !IsMoney(item) && !wantedByType.Any(wanted => wanted.Template == item.TemplateId.ToString())))
				{
					removals.Add(Take(controller, item, item.StackObjectsCount, label, Game.NameOf(item), false));
				}
			}

			part.Lines.AddRange(removals);
			part.Lines.AddRange(brings);
			part.Lines.AddRange(problems);
			return part;
		}

		// Item by item in its cell (the way the injector case always worked): a wrong item goes to the stash and the right one takes its place,
		// the right ones in another cell of the case are moved first, and what sits in a cell the template leaves empty goes out
		private static Part PlanExact(InventoryController controller, CaseEntry entry, CompoundItem container, string label, Func<string, IEnumerable<Item>> candidates, HashSet<Item> taken)
		{
			var needs = new List<Need>();
			var others = new List<PlanLine>();
			var part = new Part();
			var cells = entry.Items.Where(cell => cell.Grid >= 0 && cell.Grid < container.Grids.Length).ToList();
			part.Total = cells.Count;
			Func<CaseItem, Item> heldIn = cell => InCell(container.Grids[cell.Grid], cell.X, cell.Y);
			Func<CaseItem, ItemAddress> addressOf = cell => container.Grids[cell.Grid].CreateItemAddress(new LocationInGrid(cell.X, cell.Y, (ItemRotation)cell.Rotation));

			// What is in the case already can be put where the template wants it before anything is taken from the stash: the items in a cell the
			// template leaves empty (strays), and the ones in a cell that wants something else (wrong)
			var strays = new List<Item>();
			for (int gridIndex = 0; gridIndex < container.Grids.Length; gridIndex++)
			{
				var grid = container.Grids[gridIndex];
				int index = gridIndex;
				strays.AddRange(grid.Items.Where(item =>
				{
					if (IsMoney(item))
					{
						return false;
					}

					var spot = grid.GetItemLocation(item);
					return !cells.Any(cell => cell.Grid == index && cell.X == spot.x && cell.Y == spot.y);
				}));
			}

			var wrong = cells.Where(cell => heldIn(cell) != null && heldIn(cell).TemplateId.ToString() != cell.Template).Select(cell => heldIn(cell)).ToList();
			var pool = strays.Concat(wrong).ToList();
			var used = new HashSet<Item>(); // taken from where they are for another cell: their own cell is empty by then
			int inPlace = 0;

			foreach (var cell in cells)
			{
				var held = heldIn(cell);
				if (held != null && used.Contains(held))
				{
					held = null;
				}

				if (held != null && held.TemplateId.ToString() == cell.Template)
				{
					inPlace++;
					continue;
				}

				// The item the template wants here: already in the case (not the one that sits here), else from the stash
				var inCase = pool.FirstOrDefault(item => item != held && !used.Contains(item) && item.TemplateId.ToString() == cell.Template);
				Item source = inCase;
				if (inCase != null)
				{
					used.Add(inCase);
				}
				else
				{
					source = candidates(cell.Template).FirstOrDefault();
					if (source != null)
					{
						taken.Add(source);
						Steps.Claimed.Add(source);
					}
				}

				var target = cell;
				var chosen = source;
				if (held == null)
				{
					if (chosen == null)
					{
						needs.Add(new Need { Name = cell.Name, Template = cell.Template });
					}
					else if (inCase != null)
					{
						others.Add(new PlanLine
						{
							Text = $"Move {Game.NameOf(chosen)} to cell {cell.X},{cell.Y} of {label}",
							Template = cell.Template,
							Items = 1,
							Run = () => Game.PlaceAt(controller, chosen, Math.Min(target.Count, chosen.StackObjectsCount), addressOf(target))
						}.WithItem(chosen));
					}
					else
					{
						needs.Add(new Need
						{
							Name = cell.Name,
							Template = cell.Template,
							Run = () => Game.PlaceAt(controller, chosen, Math.Min(target.Count, chosen.StackObjectsCount), addressOf(target))
						});
					}

					continue;
				}

				// Something else sits here: it goes to the stash and the right item takes its place
				if (chosen == null)
				{
					others.Add(Steps.Problem($"Cell {cell.X},{cell.Y}", $"holds {Game.NameOf(held)}, expected {cell.Name} and the stash has none", cell.Template));
					continue;
				}

				var wrongOne = held;
				needs.Add(new Need
				{
					Name = cell.Name,
					Template = cell.Template,
					Where = $"replaces {Game.NameOf(held)}",
					Run = async () =>
					{
						var slot = Game.InventoryOf(controller).Stash.Grids[0].FindLocationForItem(wrongOne);
						if (slot == null)
						{
							return $"no room in the stash for the {Game.NameOf(wrongOne)} to move out";
						}

						var error = await Game.PlaceAt(controller, wrongOne, wrongOne.StackObjectsCount, slot);
						return error ?? await Game.PlaceAt(controller, chosen, Math.Min(target.Count, chosen.StackObjectsCount), addressOf(target));
					}
				});
			}

			// The case holds exactly the template: what sits in a cell the template leaves empty, and nothing wants, goes to the stash
			if (entry.TakeOutRest)
			{
				foreach (var stray in strays.Where(item => !used.Contains(item)))
				{
					part.Total++;
					var surplus = stray;
					others.Add(new PlanLine
					{
						Text = $"Take {Game.NameOf(stray)} out of {label} to the stash",
						Template = stray.TemplateId.ToString(),
						Items = 1,
						Run = () => Game.MoveTo(controller, surplus, new[] { "Stash" })
					}.WithItem(stray));
				}
			}

			part.InPlace = inPlace;
			// The progress line is made once for the whole case
			part.Lines = Steps.Summarize(0, 0, "items", needs, others).Skip(1).ToList();
			return part;
		}

		// Money: topped up to what is recorded; with "Take out the rest" the extra goes out too (all of a currency that is not recorded).
		// What the case will hold afterwards is added to Steps.CaseMoney (the Cash step counts it), and what it gives up to Steps.Released.
		private static Part PlanMoney(InventoryController controller, CaseEntry entry, CompoundItem container, string label, Func<string, IEnumerable<Item>> candidates, HashSet<Item> taken)
		{
			var part = new Part();
			var held = Contents(container);
			var removals = new List<PlanLine>();
			var brings = new List<PlanLine>();
			var problems = new List<PlanLine>();
			foreach (var currency in Currencies)
			{
				var template = TemplateOfCurrency(currency);
				int want;
				entry.Money.TryGetValue(currency, out want);
				var stacks = held.Where(item => item.TemplateId.ToString() == template).ToList();
				int have = stacks.Sum(item => item.StackObjectsCount);
				int after = have;
				if (want > 0)
				{
					part.Total++;
					part.InPlace += have >= want ? 1 : 0;
				}

				if (have < want)
				{
					int need = want - have;
					foreach (var source in candidates(template).ToList())
					{
						if (need <= 0)
						{
							break;
						}

						int count = Math.Min(source.StackObjectsCount, need);
						taken.Add(source);
						Steps.Claimed.Add(source);
						need -= count;
						brings.Add(Bring(controller, source, count, $"Bring {Game.Amount(currency, count)} to {label}", container, template));
					}

					after = want - need;
					if (need > 0)
					{
						problems.Add(Steps.Problem(Game.Symbol(currency), $"Missing {Game.Amount(currency, need)}: not enough in the stash (for {label})", template));
					}
				}
				else if (have > want && entry.TakeOutRest)
				{
					int extra = have - want;
					after = want;
					Steps.Add(Steps.Released, currency, extra);
					foreach (var stack in stacks.OrderBy(item => item.StackObjectsCount))
					{
						if (extra <= 0)
						{
							break;
						}

						int count = Math.Min(stack.StackObjectsCount, extra);
						extra -= count;
						removals.Add(Take(controller, stack, count, label, currency, true));
					}
				}

				Steps.Add(Steps.CaseMoney, currency, after);
			}

			part.Lines.AddRange(removals);
			part.Lines.AddRange(brings);
			part.Lines.AddRange(problems);
			return part;
		}
	}
}
