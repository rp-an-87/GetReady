using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace GetReady
{
	// The one set of rules for what ammo goes into a magazine, used by the T key and by the Ammo step alike. It follows, it does not
	// choose: a magazine is filled with what it already holds, or with what the things around it hold. When there is nothing to follow
	// it fills nothing and says so (no "best ammo" guess). It only fills free space: rounds already inside are never touched.
	// A magazine that is not equipped (in the stash, or in a case in the stash) only follows what it holds or an equipped weapon that
	// has ammo: the stash holds too many magazines and ammo types to read a pattern from.
	internal static class AmmoRules
	{
		// Where the ammo can come from and what is already spoken for in this pass
		internal class AmmoStock
		{
			public HashSet<Item> Stash = new HashSet<Item>();
			public HashSet<Item> Carried = new HashSet<Item>();
			public HashSet<Item> Pouch = new HashSet<Item>();
			public bool InStash;
			public readonly Dictionary<Ammo, int> Used = new Dictionary<Ammo, int>();

			public static AmmoStock Build(InventoryController controller)
			{
				var view = AmmoView.Of(controller);
				var stock = new AmmoStock();
				if (view.HasStash)
				{
					stock.Stash = new HashSet<Item>(view.Stash());
				}

				stock.InStash = view.HasStash;
				stock.Pouch = new HashSet<Item>(view.Secured());
				stock.Carried = new HashSet<Item>(view.Carried());
				return stock;
			}

			public int Left(Ammo stack)
			{
				int used;
				return stack.StackObjectsCount - (Used.TryGetValue(stack, out used) ? used : 0);
			}
		}

		// What is decided for one magazine: the ammo, why, and which stacks the rounds come from
		internal class FillPlan
		{
			public Magazine Magazine;
			public int Free;
			public MongoID? Type;
			public string TypeName;
			public string Reason;
			public readonly List<KeyValuePair<Ammo, int>> Allocation = new List<KeyValuePair<Ammo, int>>();
			public int Short;          // rounds that could not be found
			public string Problem;     // why nothing is planned (null when something is)
			public string Options;     // the compatible ammo in stock, for "nothing to follow"
			public string Sources;     // "your gear" / "the stash" / both

			public int Rounds => Allocation.Sum(pair => pair.Value);
		}

		// What the rules need to know about your inventory, so the tests can give it without the game's inventory controller
		internal class AmmoView
		{
			public bool HasStash;
			public Func<IEnumerable<Item>> Stash, Equipment, Carried, Secured;
			// Everything inside the containers of a place (Pouch, Pockets, Vest, Backpack...)
			public Func<string, IEnumerable<Item>> InPlace = place => Enumerable.Empty<Item>();
			public Func<List<Weapon>> Weapons;
			public Func<Item, bool> Examined;

			// The view of a controller; the tests put theirs here
			internal static Func<InventoryController, AmmoView> Of = controller => From(controller);

			private static AmmoView From(InventoryController controller)
			{
				var inventory = controller.Inventory;
				return new AmmoView
				{
					HasStash = inventory.Stash != null,
					Stash = () => inventory.GetPlayerItems(EPlayerItems.Stash),
					Equipment = () => inventory.GetPlayerItems(EPlayerItems.Equipment),
					Carried = () => Game.Carried(inventory),
					Secured = () => Game.Secured(inventory),
					InPlace = place => Game.ItemsInPlace(inventory, place),
					Weapons = () => EquippedWeapons(inventory),
					Examined = item => controller.Examined(item)
				};
			}
		}

		internal static readonly string[] PointerKeys = { "nearest", "others", "loose", "weapon", "anywhere" };

		// The pointers in the owner's order (any that is missing from the saved list comes last)
		internal static List<string> PointerOrder()
		{
			var order = SettingsStore.Current.Ammo.Pointers.Where(key => PointerKeys.Contains(key)).Distinct().ToList();
			order.AddRange(PointerKeys.Where(key => !order.Contains(key)));
			return order;
		}

		internal static string PointerName(string key) =>
			key == "loose" ? "Loose ammo in the same container"
				: key == "nearest" ? "What magazines in the same container hold"
				: key == "others" ? "What magazines in other containers hold"
				: key == "anywhere" ? "Loose ammo anywhere in an equipped container"
				: "What an equipped weapon that takes it holds";

		internal static void MovePointer(string key, int step)
		{
			var order = PointerOrder();
			int index = order.IndexOf(key);
			int target = index + step;
			if (index < 0 || target < 0 || target >= order.Count)
			{
				return;
			}

			order.RemoveAt(index);
			order.Insert(target, key);
			SettingsStore.Current.Ammo.Pointers = order;
		}

		internal static List<Weapon> EquippedWeapons(Inventory inventory) =>
			inventory.Equipment.Slots.Select(slot => slot.ContainedItem).OfType<Weapon>().ToList();

		// Ammo lying in a container grid (not in magazines, boxes or chambers), compatible with the magazine. The rounds already in
		// the magazine set the caliber: a magazine that takes two calibers is never mixed.
		internal static List<Ammo> FindAmmo(InventoryController controller, Magazine magazine)
		{
			var view = AmmoView.Of(controller);
			var items = view.HasStash ? view.Stash().Concat(view.Equipment()) : view.Equipment();
			return items
				.OfType<Ammo>()
				.Where(ammo => ammo.Parent?.Container is Grid && view.Examined(ammo) && ammo.PinLockState != EItemPinLockState.Locked && magazine.CheckCompatibility(ammo))
				.Where(ammo => !magazine.Cartridges.Items.OfType<Ammo>().Any(held => held.Caliber != ammo.Caliber))
				.ToList();
		}

		private static string ContainerName(Item container) => container == null ? "your gear" : Game.NameOf(container);

		// One of several kinds of ammo by the owner's mode: the worst or the best (penetration, then damage) or a random one. Null for "conflict":
		// nothing is guessed. A single kind is always the answer.
		internal static Ammo PickKind(List<Ammo> kinds, string mode)
		{
			if (kinds.Count == 0)
			{
				return null;
			}

			if (kinds.Count == 1)
			{
				return kinds[0];
			}

			if (mode == "random")
			{
				return kinds[Game.RandomIndex(kinds.Count)];
			}

			if (mode == "worst")
			{
				return kinds.OrderBy(ammo => ammo.PenetrationPower).ThenBy(ammo => ammo.Damage).First();
			}

			return mode == "best" ? kinds.OrderByDescending(ammo => ammo.PenetrationPower).ThenByDescending(ammo => ammo.Damage).First() : null;
		}

		// The ammo to follow, in this order. Only ammo that is in stock counts. Null when there is nothing to follow.
		//   1. what the magazine already holds
		//   not equipped (stash): 2. an equipped weapon that takes the magazine and has ammo (in its magazine, or chambered)
		//   a spare magazine on you, in the owner's order: what the nearest magazine in the same container holds, what a magazine in
		//       another container holds, loose ammo in the same container, an equipped weapon that takes it and has ammo; then
		//       the only compatible ammo you carry
		//   a magazine in a weapon: 2. what a spare magazine for that weapon holds, 3. the round chambered, 4. the only compatible
		//       ammo you carry
		private static KeyValuePair<MongoID, string>? Follow(InventoryController controller, AmmoStock stock, List<Ammo> stacks, Magazine magazine, Weapon forWeapon = null)
		{
			bool Available(Item item) => item is Ammo ammo && stacks.Any(stack => stack.TemplateId == ammo.TemplateId);
			KeyValuePair<MongoID, string>? Found(Item ammo, string reason) => ammo == null ? (KeyValuePair<MongoID, string>?)null : new KeyValuePair<MongoID, string>(ammo.TemplateId, reason);

			// The kind of ammo among these stacks: the only one there is, else the owner's mode (null when nothing is guessed)
			KeyValuePair<MongoID, string>? OfKinds(IEnumerable<Ammo> found, string reason)
			{
				var kinds = found.GroupBy(ammo => ammo.TemplateId).Select(kind => kind.First()).ToList();
				var mode = SettingsStore.Current.Ammo.PointerConflict;
				var chosen = PickKind(kinds, mode);
				return chosen == null ? null : Found(chosen, kinds.Count > 1 ? $"the {mode} of the {reason}" : reason);
			}

			// The round a weapon has chambered says what you load for it (you put it there)
			Ammo Chambered(Weapon weapon) => weapon.Chambers.Select(chamber => chamber.ContainedItem as Ammo).FirstOrDefault(ammo => ammo != null && !ammo.IsUsed && Available(ammo));

			var held = magazine.Cartridges.Items.FirstOrDefault(Available);
			if (held != null)
			{
				return Found(held, "same as inside");
			}

			var view = AmmoView.Of(controller);
			// A magazine that is about to go into a weapon is for that weapon (no guessing which one it is for): only that weapon is asked
			var weapons = forWeapon != null ? new List<Weapon> { forWeapon } : view.Weapons().ToList();

			// The equipped weapons that take this magazine. With one, follow it if it has ammo. With more (a STANAG that fits both a .300
			// and a 5.56 rifle) every one of them must have ammo and they must all hold the same: a weapon without ammo, or two that
			// differ, leaves nothing to follow, since it is not guessed which weapon the magazine is for.
			KeyValuePair<MongoID, string>? FromWeapons()
			{
				var found = new List<KeyValuePair<MongoID, string>>();
				int taking = 0;
				foreach (var weapon in weapons)
				{
					var slot = weapon.GetMagazineSlot();
					if (slot == null || !slot.CheckCompatibility(magazine))
					{
						continue;
					}

					taking++;

					var ammo = weapon.GetCurrentMagazine()?.Cartridges.Items.FirstOrDefault(Available);
					if (ammo != null)
					{
						found.Add(new KeyValuePair<MongoID, string>(ammo.TemplateId, $"same as in your {Game.NameOf(weapon)}"));
						continue;
					}

					var round = Chambered(weapon);
					if (round != null)
					{
						found.Add(new KeyValuePair<MongoID, string>(round.TemplateId, $"same as the round chambered in your {Game.NameOf(weapon)}"));
					}
				}

				if (found.Count == 0 || found.Count < taking || found.Select(pair => pair.Key).Distinct().Count() > 1)
				{
					return null;
				}

				return found[0];
			}

			bool inWeapon = magazine.Parent?.Container is Slot;
			if (!inWeapon && !stock.Carried.Contains(magazine))
			{
				return FromWeapons();
			}

			var address = magazine.CurrentAddress as GridItemAddress;
			if (address != null)
			{
				var grid = address.Grid;
				var container = grid.ParentItem;
				var siblings = container.Grids.SelectMany(g => g.Items).ToList();
				// Each pointer on its own; the order they are tried in is the owner's (Setup > Ammo): the first that gives an answer wins
				KeyValuePair<MongoID, string>? Loose()
				{
					// Loose ammo of more than one kind in the container says nothing about which one: by the owner's mode, and with none set it
					// is not guessed
					return OfKinds(siblings.OfType<Ammo>().Where(Available), $"loose ammo in your {ContainerName(container)}");
				}

				KeyValuePair<MongoID, string>? Nearest()
				{
					// Other grids of the same container (rigs have several) count as farther away than any cell of this grid.
					var near = siblings
						.Where(item => item is Magazine && item != magazine && item.CurrentAddress is GridItemAddress)
						.OrderBy(item =>
						{
							var other = (GridItemAddress)item.CurrentAddress;
							var location = other.LocationInGrid;
							return (other.Grid == grid ? 0 : 1000) + Mathf.Abs(location.x - address.LocationInGrid.x) + Mathf.Abs(location.y - address.LocationInGrid.y);
						})
						.Select(item => ((Magazine)item).Cartridges.Items.FirstOrDefault(Available))
						.FirstOrDefault(ammo => ammo != null);
					return near != null ? Found(near, "same as the nearest magazine") : null;
				}

				KeyValuePair<MongoID, string>? Others()
				{
					// A magazine on you in another container; the ones in the same container are the other rule's business
					var found = view.Carried().Concat(view.Secured()).OfType<Magazine>()
						.Where(item => item != magazine && item.CurrentAddress is GridItemAddress && ((GridItemAddress)item.CurrentAddress).Grid.ParentItem != container)
						.Select(item => new { Ammo = item.Cartridges.Items.FirstOrDefault(Available), Where = ((GridItemAddress)item.CurrentAddress).Grid.ParentItem })
						.OrderBy(pair => ContainerName(pair.Where))
						.FirstOrDefault(pair => pair.Ammo != null);
					return found == null ? null : Found(found.Ammo, $"same as a magazine in your {ContainerName(found.Where)}");
				}

				KeyValuePair<MongoID, string>? Anywhere()
				{
					// Loose ammo in any container you wear (the stash does not count): only its kind is taken from it, the rounds come from where
					// rounds always come from
					return OfKinds(view.Carried().Concat(view.Secured()).OfType<Ammo>().Where(ammo => ammo.Parent?.Container is Grid && Available(ammo)), "loose ammo on you");
				}

				foreach (var pointer in PointerOrder())
				{
					var answer = pointer == "loose" ? Loose() : pointer == "nearest" ? Nearest() : pointer == "others" ? Others() : pointer == "anywhere" ? Anywhere() : FromWeapons();
					if (answer != null)
					{
						return answer;
					}
				}
			}
			else if (inWeapon)
			{
				var weapon = magazine.Parent.Container.ParentItem as Weapon;
				var slot = weapon?.GetMagazineSlot();
				if (slot != null)
				{
					var spare = view.Carried().OfType<Magazine>()
						.Where(other => other != magazine && other.Parent?.Container is Grid && slot.CheckCompatibility(other))
						.Select(other => other.Cartridges.Items.FirstOrDefault(Available))
						.FirstOrDefault(ammo => ammo != null);
					if (spare != null)
					{
						return Found(spare, "same as your spare magazine");
					}

					var round = Chambered(weapon);
					if (round != null)
					{
						return Found(round, "same as the round chambered");
					}
				}
			}

			var carried = stacks.Where(stack => !stock.Stash.Contains(stack)).Select(stack => stack.TemplateId).Distinct().ToList();
			if (carried.Count == 1)
			{
				return new KeyValuePair<MongoID, string>(carried[0], "the only compatible ammo you carry");
			}

			return null;
		}

		private static string Sources(AmmoStock stock, IEnumerable<Ammo> used) =>
			string.Join(" and ", new[] { used.Any(stack => !stock.Stash.Contains(stack)) ? "your gear" : null, used.Any(stack => stock.Stash.Contains(stack)) ? "the stash" : null }.Where(part => part != null).ToArray());

		// How many containers deep the item lies: 0 = directly in a vest, pocket, backpack or the pouch, 1 = in a case inside one of those...
		private static int Depth(Item item)
		{
			int depth = 0;
			var holder = item.Parent?.Container?.ParentItem;
			while (holder != null && holder.Parent?.Container is Grid && depth < 16)
			{
				depth++;
				holder = holder.Parent.Container.ParentItem;
			}

			return depth;
		}

		// Where a stack is taken from, lowest first. In the stash screen: the stash first, then what you carry (the container the magazine
		// lies in, any other container, then inside a case in them), and the pouch last of all. In a raid: the container the magazine
		// lies in, then the pouch, then any other container you wear, then what lies inside a case in them.
		private static int Rank(AmmoStock stock, Magazine magazine, Ammo stack)
		{
			if (stock.Stash.Contains(stack))
			{
				return 0;
			}

			if (stock.InStash && stock.Pouch.Contains(stack))
			{
				return 4;
			}

			int rank;
			var home = magazine.Parent?.Container is Grid ? magazine.Parent.Container.ParentItem : null;
			if (home != null && stack.Parent?.Container?.ParentItem == home)
			{
				rank = 0;
			}
			else if (Depth(stack) > 0)
			{
				rank = 3;
			}
			else
			{
				rank = stock.Pouch.Contains(stack) ? 1 : 2;
			}

			return stock.InStash ? rank + 1 : rank;
		}

		// Decides what goes into the magazine and takes the rounds from the stock (so the next magazine sees what is left). A magazine
		// in a weapon, or one that is about to go into forWeapon, only gets ammo that fits that weapon's chamber.
		internal static FillPlan PlanFill(InventoryController controller, AmmoStock stock, Magazine magazine, Weapon forWeapon = null)
		{
			var plan = new FillPlan { Magazine = magazine, Free = magazine.MaxCount - magazine.Count };
			var weapon = forWeapon ?? (magazine.Parent?.Container is Slot ? magazine.Parent.Container.ParentItem as Weapon : null);
			var stacks = FindAmmo(controller, magazine)
				.Where(stack => stock.Left(stack) > 0 && (weapon == null || stack.Caliber == weapon.AmmoCaliber))
				.ToList();
			if (stacks.Count == 0)
			{
				plan.Problem = "no compatible ammo left";
				return plan;
			}

			var follow = Follow(controller, stock, stacks, magazine, forWeapon);
			if (follow == null)
			{
				plan.Problem = "nothing to follow";
				plan.Options = string.Join(", ", stacks.GroupBy(stack => stack.TemplateId).Select(group => group.First().LocalizedShortName()).Distinct().Take(5).ToArray());
				return plan;
			}

			plan.Type = follow.Value.Key;
			plan.Reason = follow.Value.Value;

			// Where the rounds come from, see Rank; within the same rank the loose stacks before the ones inside a case, then the smallest first
			var ordered = stacks.Where(stack => stack.TemplateId == plan.Type.Value)
				.OrderBy(stack => Rank(stock, magazine, stack))
				.ThenBy(stack => Game.InContainer(stack))
				.ThenBy(stack => stack.StackObjectsCount)
				.ToList();
			plan.TypeName = ordered[0].LocalizedShortName();
			int free = plan.Free;
			foreach (var stack in ordered)
			{
				int take = Math.Min(stock.Left(stack), free);
				if (take <= 0)
				{
					continue;
				}

				plan.Allocation.Add(new KeyValuePair<Ammo, int>(stack, take));
				int used;
				stock.Used[stack] = (stock.Used.TryGetValue(stack, out used) ? used : 0) + take;
				free -= take;
				if (free == 0)
				{
					break;
				}
			}

			plan.Short = free;
			plan.Sources = Sources(stock, plan.Allocation.Select(pair => pair.Key));
			return plan;
		}

		// One round for the weapon's empty chamber: of the given ammo, fitting the weapon's caliber and the chamber, from your gear
		// first, then the stash (the smallest stack first). Null when there is none left.
		internal static Ammo PickForChamber(InventoryController controller, AmmoStock stock, Weapon weapon, Slot chamber, Magazine magazine, MongoID type)
		{
			var stack = FindAmmo(controller, magazine)
				.Where(ammo => ammo.TemplateId == type && stock.Left(ammo) > 0 && ammo.Caliber == weapon.AmmoCaliber && chamber.CheckCompatibility(ammo))
				.OrderBy(ammo => Rank(stock, magazine, ammo))
				.ThenBy(ammo => Game.InContainer(ammo))
				.ThenBy(ammo => ammo.StackObjectsCount)
				.FirstOrDefault();
			if (stack != null)
			{
				int used;
				stock.Used[stack] = (stock.Used.TryGetValue(stack, out used) ? used : 0) + 1;
			}

			return stack;
		}

		// The load itself (replaceable, so the tests don't need the game's controller to load rounds)
		internal static Func<InventoryController, Ammo, Magazine, int, Task<Comfort.Common.IResult>> LoadRounds = (controller, ammo, magazine, count) => controller.LoadMagazine(ammo, magazine, count);

		// Loads what the plan says, with the controller's own LoadMagazine (in a raid the timed load). Returns the rounds loaded.
		internal static async Task<int> Load(InventoryController controller, FillPlan plan)
		{
			int loaded = 0;
			foreach (var pair in plan.Allocation)
			{
				int before = plan.Magazine.Count;
				var result = await LoadRounds(controller, pair.Key, plan.Magazine, pair.Value);
				loaded += plan.Magazine.Count - before;
				if (result.Failed)
				{
					Plugin.Log.LogWarning($"Loading {plan.Magazine.Id} failed: {result.Error}");
					break;
				}
			}

			return loaded;
		}
	}
}
