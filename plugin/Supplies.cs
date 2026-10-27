using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;

namespace GetReady
{
	// One kind of item in a group and how much it counts for: its health resource, a fixed number of points, or one each
	internal class Entry
	{
		public string Template;
		public Func<Item, float> Weight;
		public string Name => (Template + " ShortName").Localized();

		internal static Entry Count(string template) => new Entry { Template = template, Weight = item => 1f };
		internal static Entry Points(string template, float points) => new Entry { Template = template, Weight = item => points };
		internal static Entry Resource(string template) => new Entry { Template = template, Weight = Game.ResourceOf };
	}

	// What you can ask the kit to carry: a target amount, and the items that count towards it, best first
	internal class GroupDef
	{
		public string Id;
		public string Name;
		public string Unit;
		public Entry[] Entries = new Entry[0]; // in priority order
		public bool Random; // the entries are equal: whichever the stash has, picked at random
		public bool Impact; // left out of the grenade quota's random picks
		public bool IsQuota; // total grenades: topped up with random ones after the groups
		public bool AllowPouch = true; // the secured container and the special slots can hold it (not grenades)
		public int DefaultTarget;
		public int Max = 10;
		public int Step = 1;

		// Where it goes by default, first with room wins
		public string[] DefaultDestinations = { "Pockets", "Vest" };
	}

	// Healing and grenades: each group has a target and a place to put it. What you already carry counts towards the target,
	// worse carried items are swapped for better ones from the stash, and the stash fills the rest in priority order.
	internal abstract class SuppliesStep : IStep
	{
		public abstract string Name { get; }
		public abstract bool Enabled { get; set; }
		protected abstract GroupDef[] Defs { get; }

		public IReadOnlyList<GroupDef> Groups => Defs;

		public string Saved()
		{
			var parts = Defs.Where(def => Setting(def).Target > 0).Select(def => $"{Setting(def).Target} {def.Unit}").ToArray();
			return parts.Length == 0 ? "nothing wanted" : string.Join(", ", parts);
		}

		// Where a group can go, in the order they are clicked: the first with room wins. "Vest" is the tactical vest or an armored
		// rig, "Pouch" the secured container.
		internal static readonly string[] Locations = { "Special slots", "Pockets", "Vest", "Backpack", "Pouch" };

		internal static GroupSetting Setting(GroupDef def)
		{
			var groups = SettingsStore.Current.Supplies.Groups;
			GroupSetting setting;
			if (!groups.TryGetValue(def.Id, out setting))
			{
				setting = new GroupSetting { Target = def.DefaultTarget, Destinations = def.DefaultDestinations.ToList() };
				groups[def.Id] = setting;
			}

			return setting;
		}

		// Adds the place to the end of the group's list, or takes it out (the last one stays)
		internal static void ToggleDestination(GroupSetting setting, string place)
		{
			if (!setting.Destinations.Remove(place))
			{
				setting.Destinations.Add(place);
			}
			else if (setting.Destinations.Count == 0)
			{
				setting.Destinations.Add(place);
			}
		}

		// The group's items in the order the owner set (the rest in the default order after them)
		internal static List<Entry> OrderedEntries(GroupDef def)
		{
			var order = Setting(def).Order;
			return def.Entries
				.Select((entry, index) => new { entry, key = order.IndexOf(entry.Template) is int k && k >= 0 ? k : 1000 + index })
				.OrderBy(pair => pair.key)
				.Select(pair => pair.entry)
				.ToList();
		}

		// What counts: ordered and without the items switched off
		internal static Entry[] ActiveEntries(GroupDef def)
		{
			var off = Setting(def).Off;
			return OrderedEntries(def).Where(entry => !off.Contains(entry.Template)).ToArray();
		}

		// Moves an item one place up or down the group's order, and writes the order out in full
		internal static void Move(GroupDef def, string template, int step)
		{
			var entries = OrderedEntries(def).Select(entry => entry.Template).ToList();
			int index = entries.IndexOf(template);
			int target = index + step;
			if (index < 0 || target < 0 || target >= entries.Count)
			{
				return;
			}

			entries.RemoveAt(index);
			entries.Insert(target, template);
			Setting(def).Order = entries;
		}

		internal static void ToggleItem(GroupDef def, string template)
		{
			var off = Setting(def).Off;
			if (!off.Remove(template))
			{
				off.Add(template);
			}
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var inventory = controller.Inventory;
			var settings = SettingsStore.Current.Supplies;
			var carried = Game.CarriedAndSecured(inventory).ToList();
			var owned = new HashSet<Item>(Steps.Owned);
			var leaving = Steps.Leaving.ToList();
			var claimed = Steps.Claimed.ToList();
			var stash = Game.Stash(inventory).Where(item => !claimed.Contains(item)).ToList();
			var lines = new List<PlanLine>();

			foreach (var def in Defs.Where(def => !def.IsQuota))
			{
				var setting = Setting(def);
				if (setting.Target <= 0)
				{
					continue;
				}

				var places = setting.Destinations.ToList();
				float alreadyBringing = claimed.Sum(item => WeightOf(def, item));
				float leavingWeight = leaving.Sum(item => WeightOf(def, item));
				float fixedWeight = owned.Sum(item => WeightOf(def, item)) - leavingWeight;
				lines.Add(Steps.Info($"{def.Name}: {carried.Sum(item => WeightOf(def, item)) - leavingWeight + alreadyBringing:0}/{setting.Target} {def.Unit} carried"));

				if (settings.SwapWorse)
				{
					foreach (var swap in PlanSwaps(def, setting.Target, carried, stash, owned, fixedWeight))
					{
						var worse = swap.Key;
						var better = swap.Value;
						lines.Add(new PlanLine
						{
							Text = $"Replace {Describe(worse)} with {Describe(better)}",
							Run = () => Swap(controller, worse, better, places)
						});
					}
				}

				float need = setting.Target - carried.Sum(item => WeightOf(def, item)) + leavingWeight - alreadyBringing;
				var brought = new List<Item>();
				foreach (var item in Candidates(stash, def))
				{
					if (need <= 0)
					{
						break;
					}

					float weight = WeightOf(def, item);
					if (weight <= 0 || (!settings.AllowOvershoot && weight > need))
					{
						continue;
					}

					brought.Add(item);
					need -= weight;
				}

				foreach (var group in brought.GroupBy(item => Game.NameOf(item)))
				{
					var items = group.ToList();
					lines.Add(new PlanLine
					{
						Text = $"Bring {items.Count}x {group.Key}",
						Run = () => MoveAll(controller, items, places)
					});
				}

				if (need > 0)
				{
					lines.Add(Steps.Problem($"{def.Name}: {need:0} {def.Unit} short, the stash has no more."));
				}
			}

			foreach (var def in Defs.Where(def => def.IsQuota))
			{
				PlanQuota(controller, def, carried.Concat(claimed).ToList(), stash, lines);
			}

			return lines;
		}

		// Tops the grenades up to the quota with random non-impact ones from the stash. Every grenade of the groups carried
		// counts (impact ones too), and it runs after the groups so their minimums come first.
		private void PlanQuota(InventoryController controller, GroupDef quotaDef, List<Item> carried, List<Item> stash, List<PlanLine> lines)
		{
			var setting = Setting(quotaDef);
			if (setting.Target <= 0)
			{
				return;
			}

			var groups = Defs.Where(def => !def.IsQuota).ToList();
			var all = new HashSet<string>(groups.SelectMany(def => def.Entries.Select(entry => entry.Template)));
			int need = setting.Target - carried.Count(item => all.Contains(item.TemplateId.ToString()));
			lines.Add(Steps.Info($"{quotaDef.Name}: {setting.Target - Math.Max(need, 0)}/{setting.Target} carried"));
			if (need <= 0)
			{
				return;
			}

			// One pile per non-impact type, non-FiR first. A random type is picked before a grenade, so a type the stash has
			// many of (say M67) isn't picked more often than the others.
			var piles = groups
				.Where(def => !def.Impact)
				.Select(def => new Queue<Item>(stash
					.Where(item => ActiveEntries(def).Any(entry => entry.Template == item.TemplateId.ToString()))
					.OrderBy(item => item.MarkedAsSpawnedInSession ? 1 : 0)
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

			var places = setting.Destinations.ToList();
			foreach (var group in picked.GroupBy(item => Game.NameOf(item)))
			{
				var items = group.ToList();
				lines.Add(new PlanLine { Text = $"Bring {items.Count}x {group.Key}", Run = () => MoveAll(controller, items, places) });
			}

			if (need > 0)
			{
				lines.Add(Steps.Problem($"{quotaDef.Name}: {need} short, the stash has no more."));
			}
		}

		private static async Task<string> MoveAll(InventoryController controller, List<Item> items, List<string> places)
		{
			foreach (var item in items)
			{
				var error = await Game.MoveTo(controller, item, places);
				if (error != null)
				{
					return error;
				}
			}

			return null;
		}

		// The carried one goes to the stash first, and goes back if the better one can't be brought
		private static async Task<string> Swap(InventoryController controller, Item worse, Item better, List<string> places)
		{
			var error = await Game.MoveToContainers(controller, worse, new List<CompoundItem> { controller.Inventory.Stash });
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

		// Replaces carried items by better ones from the stash. Only items needed to reach the target are swapped: the best
		// carried items are taken until the target is met, and anything beyond that is a spare that is left alone. A swap must
		// not lower the total of the needed items (or the target). Works on copies of the lists, which end up as they will
		// look after the swaps.
		// Items in owned are managed by another step (the special slots template) and are never swapped here; fixedWeight is
		// what they count for.
		private static List<KeyValuePair<Item, Item>> PlanSwaps(GroupDef def, int target, List<Item> carried, List<Item> stash, ICollection<Item> owned, float fixedWeight)
		{
			var swaps = new List<KeyValuePair<Item, Item>>();
			var handled = new HashSet<string>();

			for (int guard = 0; guard < 20; guard++)
			{
				var needed = new List<Item>();
				float total = fixedWeight;
				foreach (var item in carried.Where(item => WeightOf(def, item) > 0 && !owned.Contains(item)).OrderBy(item => PriorityOf(def, item)).ThenByDescending(Game.ResourceOf))
				{
					if (total >= target)
					{
						break;
					}

					needed.Add(item);
					total += WeightOf(def, item);
				}

				var candidates = Candidates(stash.Where(item => !handled.Contains(item.Id.ToString())).ToList(), def).ToList();

				Item worse = null;
				Item better = null;
				foreach (var current in needed.Where(item => !handled.Contains(item.Id.ToString())).OrderByDescending(item => PriorityOf(def, item)).ThenBy(Game.ResourceOf))
				{
					better = candidates.FirstOrDefault(candidate => IsBetter(def, candidate, current, total < target)
						&& total - WeightOf(def, current) + WeightOf(def, candidate) >= Math.Min(total, target));
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

		// The same template with more resource (FiR is the tie-breaker: non-FiR wins a tie), or a higher priority template,
		// but only while the group is still short of its target: a set that already meets it (say two single tourniquets
		// instead of a CALOK) isn't upgraded to a higher priority item
		private static bool IsBetter(GroupDef def, Item candidate, Item current, bool belowTarget)
		{
			int candidatePriority = PriorityOf(def, candidate);
			int currentPriority = PriorityOf(def, current);
			if (candidatePriority != currentPriority)
			{
				return belowTarget && candidatePriority < currentPriority;
			}

			if (candidate.MarkedAsSpawnedInSession == current.MarkedAsSpawnedInSession)
			{
				return Game.ResourceOf(candidate) > Game.ResourceOf(current);
			}

			// FiR only counts against an item: a non-FiR one wins a tie, a FiR one has to bring more resource
			return candidate.MarkedAsSpawnedInSession
				? Game.ResourceOf(candidate) > Game.ResourceOf(current)
				: Game.ResourceOf(candidate) >= Game.ResourceOf(current);
		}

		private static int PriorityOf(GroupDef def, Item item)
		{
			if (def.Random)
			{
				return 0;
			}

			var template = item.TemplateId.ToString();
			return Array.FindIndex(ActiveEntries(def), entry => entry.Template == template);
		}

		private static float WeightOf(GroupDef def, Item item)
		{
			var template = item.TemplateId.ToString();
			var entry = ActiveEntries(def).FirstOrDefault(candidate => candidate.Template == template);
			return entry == null ? 0f : entry.Weight(item);
		}

		// Group entries in priority order; within one template, non-FiR first, then the fullest resource
		private static IEnumerable<Item> Candidates(List<Item> items, GroupDef def)
		{
			foreach (var entry in def.Random ? ActiveEntries(def).OrderBy(_ => Guid.NewGuid()) : ActiveEntries(def).AsEnumerable())
			{
				foreach (var item in items
					.Where(item => item.TemplateId.ToString() == entry.Template)
					.OrderBy(item => item.MarkedAsSpawnedInSession ? 1 : 0)
					.ThenByDescending(Game.ResourceOf))
				{
					yield return item;
				}
			}
		}

		// "Salewa (120 HP left)": the health resource only when the item has one
		private static string Describe(Item item)
		{
			float resource = Game.ResourceOf(item);
			return resource > 0f ? $"{Game.NameOf(item)} ({resource:0} left)" : Game.NameOf(item);
		}
	}

	internal class HealingStep : SuppliesStep
	{
		public override string Name => "Healing";
		public override bool Enabled { get => SettingsStore.Current.Supplies.HealingEnabled; set => SettingsStore.Current.Supplies.HealingEnabled = value; }

		private static readonly GroupDef[] Definitions =
		{
			new GroupDef
			{
				Id = "health", Name = "Healing kits", Unit = "HP", DefaultTarget = 300, Max = 2000, Step = 50,
				Entries = new[]
				{
					Entry.Resource("60098ad7c2240c0fe85c570a"), // AFAK
					Entry.Resource("544fb45d4bdc2dee738b4568"), // Salewa
					Entry.Resource("590c678286f77426c9660122"), // IFAK
					Entry.Resource("590c661e86f7741e566b646a"), // Car
					Entry.Resource("5755356824597772cb798962")  // AI-2
				}
			},
			new GroupDef
			{
				Id = "bandage", Name = "Bandages", Unit = "bandage points", DefaultTarget = 2, Max = 20,
				Entries = new[]
				{
					Entry.Resource("5751a25924597722c463c472"), // Army bandage: its remaining uses (2 new)
					Entry.Points("544fb25a4bdc2dfb738b4567", 1f) // Aseptic bandage
				}
			},
			new GroupDef
			{
				Id = "heavybleed", Name = "Heavy bleed", Unit = "tourniquet points", DefaultTarget = 2, Max = 20,
				Entries = new[]
				{
					Entry.Resource("5e8488fa988a8701445df1e4"), // CALOK-B: its remaining uses (3 new)
					Entry.Points("60098af40accd37ef2175f27", 1f), // CAT
					Entry.Points("5e831507ea0a7c419c2f9bd9", 1f)  // Esmarch
				}
			},
			new GroupDef
			{
				Id = "splint", Name = "Splints", Unit = "splint uses", DefaultTarget = 5, Max = 50, Step = 5,
				DefaultDestinations = new[] { "Vest", "Pouch" },
				Entries = new[]
				{
					Entry.Resource("5af0454c86f7746bf20992e8"), // Aluminum splint: 5 uses
					Entry.Resource("6937ecf8628ee476240c07cb"), // Tigz'Resq splint (WTT Content Backport)
					Entry.Points("544fb3364bdc2d34748b456a", 1f) // Immobilizing splint
				}
			},
			new GroupDef
			{
				Id = "surgery", Name = "Surgery kits", Unit = "surgery uses", DefaultTarget = 0, Max = 60, Step = 3,
				Entries = new[]
				{
					Entry.Resource("5d02778e86f774203e7dedbe"), // CMS: 3 uses
					Entry.Resource("5d02797c86f774203f38e30a")  // Surv12: 9 uses
				}
			},
			new GroupDef
			{
				Id = "propital", Name = "Propital", Unit = "Propital", DefaultTarget = 1,
				Entries = new[] { Entry.Count("5c0e530286f7747fa1419862") }
			}
		};

		protected override GroupDef[] Defs => Definitions;
	}

	internal class GrenadesStep : SuppliesStep
	{
		public override string Name => "Grenades";
		public override bool Enabled { get => SettingsStore.Current.Supplies.GrenadesEnabled; set => SettingsStore.Current.Supplies.GrenadesEnabled = value; }

		private static GroupDef Grenade(string id, string name, bool impact, params string[] templates) => new GroupDef
		{
			Id = id, Name = name, Unit = name, Impact = impact, AllowPouch = false, Random = templates.Length > 1, DefaultTarget = 0,
			Entries = templates.Select(Entry.Count).ToArray()
		};

		private static readonly GroupDef[] Definitions =
		{
			Grenade("m67", "M67", false, "58d3db5386f77426186285a0"),
			Grenade("rgd5", "RGD-5", false, "5448be9a4bdc2dfd2f8b456a"),
			Grenade("f1", "F-1", false, "5710c24ad2720bc3458b45a3"),
			Grenade("vog", "VOG-17/25", false, "5e32f56fcb6d5863cc5e5ee4", "5e340dcdcb6d5863cc5e5efb"),
			Grenade("impact", "Impact RGN/RGO", true, "617fd91e5539a84ec44ce155", "618a431df1eb8e24b8741deb"),
			new GroupDef { Id = "grenadequota", Name = "Any grenades (random)", Unit = "grenades in total", IsQuota = true, AllowPouch = false, DefaultTarget = 2 }
		};

		protected override GroupDef[] Defs => Definitions;
	}
}
