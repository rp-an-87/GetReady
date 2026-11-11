using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;

namespace GetReady
{
	// Fills the magazines you carry, following the rules in AmmoRules: the magazine in each equipped weapon, then your spare
	// magazines (vest, pockets, backpack and the cases in them). The game does not load a magazine that sits in a weapon, so that one
	// is taken out, filled and put back. A weapon with no magazine gets one of your equipped magazines (never one from the stash)
	// that fits it and can be filled with ammo for it. Every line says what goes where, how much, why and from where; problems of
	// the same kind are grouped in one line. Chambers, built-in magazines and barrel-only weapons come later. Locked weapons and
	// magazines are left alone.
	internal class AmmoStep : IStep
	{
		// A line about a weapon that has no card of its own (no magazine, locked): it ends the card above it
		private static PlanLine Alone(PlanLine line)
		{
			line.Standalone = true;
			return line;
		}

		// The two cards after the weapons (the window gives them a "?")
		internal const string ExtraMagazines = "Extra magazines on you";
		internal const string LooseAmmo = "Loose ammo on you";

		public string Name => "Ammo";
		public IReadOnlyList<GroupDef> Groups => new[] { Spare };

		// How many stacks of spare ammo to carry for each caliber you use (0: off) and where they go. A stack is the ammo's full stack size.
		internal static readonly GroupDef Spare = new GroupDef
		{
			Id = "spareammo", Name = "Spare ammo", Unit = "stacks per caliber", DefaultTarget = 1, Max = 5, Bindable = false,
			DefaultDestinations = new[] { "Pouch" }, Locations = new[] { "Pockets", "Vest", "Backpack", "Pouch" }
		};

		// The ammo types (with their caliber) the magazines planned in this pass will hold: they say which ammo is in use
		private static readonly Dictionary<MongoID, string> Planned = new Dictionary<MongoID, string>();
		public bool Enabled { get => SettingsStore.Current.Ammo.Enabled; set => SettingsStore.Current.Ammo.Enabled = value; }

		// The ammo a spare magazine holds that none of the equipped weapons it fits takes (a STANAG with .300 when only a 5.56 rifle is on you), or
		// null: it holds nothing, holds ammo one of them takes, or fits none of your weapons (a spare for a weapon you swap in is left alone)
		private static Ammo WrongAmmoFor(AmmoRules.AmmoView view, Magazine magazine)
		{
			var held = magazine.Cartridges.Items.OfType<Ammo>().ToList();
			if (held.Count == 0)
			{
				return null;
			}

			var fits = view.Weapons().Where(weapon => !string.IsNullOrEmpty(weapon.AmmoCaliber) && weapon.GetMagazineSlot()?.CheckCompatibility(magazine) == true).ToList();
			if (fits.Count == 0)
			{
				return null;
			}

			return held.FirstOrDefault(ammo => !fits.Any(weapon => weapon.AmmoCaliber == ammo.Caliber));
		}

		// The spare magazines that hold ammo no weapon they fit takes: one line for each kind of ammo, with how many magazines hold it
		private static void AddWrongAmmo(List<PlanLine> lines, AmmoRules.AmmoView view, IEnumerable<Magazine> spares)
		{
			var wrong = spares.Select(magazine => new { Magazine = magazine, Ammo = WrongAmmoFor(view, magazine) }).Where(pair => pair.Ammo != null).ToList();
			foreach (var kind in wrong.GroupBy(pair => pair.Ammo.TemplateId))
			{
				var list = kind.ToList();
				var ammo = list[0].Ammo;
				var takes = string.Join(", ", view.Weapons().Where(weapon => !string.IsNullOrEmpty(weapon.AmmoCaliber)).Select(weapon => AmmoPanels.CaliberLabel(weapon.AmmoCaliber)).Distinct().ToArray());
				var line = Steps.Problem(list.Count == 1 ? "1 magazine holds ammo your weapons don't take" : $"{list.Count} magazines hold ammo your weapons don't take",
					$"{Game.NameOf(ammo)} ({AmmoPanels.CaliberLabel(ammo.Caliber)}) in {string.Join(", ", list.Select(pair => $"{Game.NameOf(pair.Magazine)} in {Where(pair.Magazine)}").ToArray())}; the weapons they fit take {takes}", list[0].Magazine.TemplateId.ToString());
				line.AlertIconOnly = true;
				lines.Add(line);
			}
		}

		// A magazine that can't be filled, to be reported with the others of its kind
		private class Stuck
		{
			public string Label;
			public string Template;
			public string Options; // null: no compatible ammo left at all
			public MagInfo Info;
		}

		private static string Where(Magazine magazine) => Game.NameOf(magazine.Parent.Container.ParentItem);

		public List<PlanLine> Plan(InventoryController controller)
		{
			var view = AmmoRules.AmmoView.Of(controller);
			var stock = AmmoRules.AmmoStock.Build(controller);
			var lines = new List<PlanLine>();
			var missing = new Dictionary<string, int>();
			var stuck = new List<Stuck>();
			var spares = view.Carried().OfType<Magazine>()
				.Where(magazine => magazine.Parent?.Container is Grid && magazine.PinLockState != EItemPinLockState.Locked)
				.ToList();
			var inserted = new HashSet<Magazine>();
			Planned.Clear();

			// The magazine in each weapon: one card per weapon (where each weapon's lines end is kept: its spare ammo goes right below)
			var blockEnd = new Dictionary<Weapon, int>();
			Weapon previous = null;
			foreach (var weapon in view.Weapons())
			{
				if (previous != null)
				{
					blockEnd[previous] = lines.Count;
				}

				previous = weapon;
				var slot = weapon.GetMagazineSlot();
				if (slot == null)
				{
					continue; // barrel-only weapons and special ones: later
				}

				var name = Game.NameOf(weapon);
				var template = weapon.TemplateId.ToString();
				if (weapon.PinLockState == EItemPinLockState.Locked)
				{
					lines.Add(Alone(Steps.Info($"{name}: left alone (locked)")));
					continue;
				}

				var magazine = weapon.GetCurrentMagazine();
				if (magazine == null)
				{
					PlanInsert(controller, stock, weapon, slot, spares, inserted, lines, missing, stuck);
					continue;
				}

				if (magazine.PinLockState == EItemPinLockState.Locked)
				{
					lines.Add(Alone(Steps.Info($"{name}: left alone (its magazine is locked)")));
					continue;
				}

				lines.Add(Steps.Progress(name, magazine.Count, magazine.MaxCount, "rounds", template).WithItem(weapon).InPart("magazine"));
				// A magazine that fits but holds ammo of another caliber (a STANAG with .300 in a 5.56 rifle): the weapon is not ready, and nothing is guessed
				var wrong = string.IsNullOrEmpty(weapon.AmmoCaliber) ? null : magazine.Cartridges.Items.OfType<Ammo>().FirstOrDefault(held => held.Caliber != weapon.AmmoCaliber);
				if (wrong != null)
				{
					lines.Add(Steps.Problem(name, $"its magazine holds {Game.NameOf(wrong)} ({AmmoPanels.CaliberLabel(wrong.Caliber)}), this weapon takes {AmmoPanels.CaliberLabel(weapon.AmmoCaliber)}", template).WithItem(weapon).InPart("magazine"));
					continue;
				}

				var made = AddFill(controller, stock, magazine, $"{name}'s magazine", lines, missing, stuck, null, null, slot);
				AddChamber(controller, stock, weapon, magazine, made, lines);
			}

			// Where the last weapon's lines end: its spare ammo goes right below, before the spare magazines and the loose ammo
			if (previous != null)
			{
				blockEnd[previous] = lines.Count;
			}

			// The spare magazines on you, nearest the same container together
			var rest = spares.Where(magazine => !inserted.Contains(magazine) && WrongAmmoFor(view, magazine) == null)
				.OrderBy(magazine => Where(magazine))
				.ThenBy(magazine => magazine.Template.NameLocalizationKey)
				.ToList();
			if (rest.Count > 0)
			{
				lines.Add(Steps.Progress(ExtraMagazines, rest.Count(magazine => magazine.Count >= magazine.MaxCount), rest.Count, "full", rest[0].TemplateId.ToString()));
				foreach (var magazine in rest)
				{
					AddFill(controller, stock, magazine, $"{Game.NameOf(magazine)} in {Where(magazine)}", lines, missing, stuck, null, null, null);
				}
			}

			// The magazines that cannot be filled, and the rounds that are missing: in the "Extra magazines on you" card when there is one, else
			// each stands alone (it must not end up inside the card of a weapon)
			int problemsFrom = lines.Count;
			AddWrongAmmo(lines, view, spares.Where(magazine => !inserted.Contains(magazine)));
			AddStuck(lines, stuck);
			foreach (var pair in missing)
			{
				lines.Add(Steps.Problem($"Ammo: {pair.Key}", $"missing {pair.Value:N0} round(s), none left"));
			}

			if (rest.Count == 0)
			{
				for (int index = problemsFrom; index < lines.Count; index++)
				{
					lines[index].Standalone = true;
				}
			}

			if (SettingsStore.Current.Ammo.TopUpLoose)
			{
				AddLoose(controller, stock, lines);
			}

			// The spare ammo of each weapon right below its card, the last weapon's first so the places stay right
			var spare = AddSpare(controller, stock, lines);
			foreach (var weapon in view.Weapons().Where(candidate => spare.ContainsKey(candidate) && blockEnd.ContainsKey(candidate)).Reverse())
			{
				lines.InsertRange(blockEnd[weapon], spare[weapon]);
			}

			return lines;
		}

		// A model asks for this many stacks, whatever the number: set by hand it is its own number, even when it equals the generic one
		// (a later change of the generic number does not move it). Only RemoveModel gives it back to the generic number.
		internal static void SetStacks(string template, string name, int stacks)
		{
			var ammo = SettingsStore.Current.Ammo;
			ammo.SpareStacks.RemoveAll(entry => entry.Template == template);
			ammo.SpareStacks.Add(new ModelStacks { Template = template, Name = name, Stacks = stacks });
		}

		// The model has a number of its own (set by hand)
		internal static bool HasOwnStacks(string template)
		{
			var ammo = SettingsStore.Current.Ammo;
			return ammo.SpareStacks.Any(entry => entry.Template == template);
		}

		// A model off the list, back to the generic number
		internal static void RemoveModel(string template)
		{
			var ammo = SettingsStore.Current.Ammo;
			ammo.SpareStacks.RemoveAll(entry => entry.Template == template);
		}

		// How many stacks of spare ammo a weapon model asks for: its own number, else the generic one
		internal static int StacksOf(Weapon weapon) => StacksOf(weapon.TemplateId.ToString());

		// By model: its own number, else the generic one
		internal static int StacksOf(string template)
		{
			var own = SettingsStore.Current.Ammo.SpareStacks.FirstOrDefault(entry => entry.Template == template);
			return own != null ? own.Stacks : Spare.Setting.Target;
		}

		// Some model has a number of its own above zero (spare ammo is on for it whatever the generic number is)
		internal static bool AnyOwnStacks() => SettingsStore.Current.Ammo.SpareStacks.Any(entry => entry.Stacks > 0);

		// The weapons of a caliber share the request: the biggest of their models' numbers (two of one model count once)
		internal static int StacksFor(IEnumerable<Weapon> weapons) => weapons.Select(StacksOf).DefaultIfEmpty(0).Max();

		// For each caliber your equipped weapons use: when, after the magazines are filled, none of the places spare ammo goes to (the
		// ones switched on in Setup) holds loose ammo of it, the stash gives the owner's number of stacks of ammo that is already in use for it: what is in the
		// weapons and magazines, and what the magazines are about to be filled with. Which kind when several are in use is the owner's
		// choice (worst, best, random). When nothing tells which ammo, or the stash has none of it, it is a problem and nothing is guessed.
		private static Dictionary<Weapon, List<PlanLine>> AddSpare(InventoryController controller, AmmoRules.AmmoStock stock, List<PlanLine> planned)
		{
			var result = new Dictionary<Weapon, List<PlanLine>>();
			var setting = Spare.Setting;
			if (!stock.InStash)
			{
				return result;
			}

			var view = AmmoRules.AmmoView.Of(controller);
			var weapons = view.Weapons().Where(weapon => !string.IsNullOrEmpty(weapon.AmmoCaliber) && StacksOf(weapon) > 0).ToList();
			if (weapons.Count == 0)
			{
				return result;
			}

			var spareMagazines = view.Carried().OfType<Magazine>().Where(magazine => magazine.Parent?.Container is Grid).ToList();
			var places = Spare.PlacesToTry();
			// Loose ammo of the caliber anywhere on you is spare ammo (the loose stacks are topped up anyway); the places it is set to go to
			// only say where ammo is brought when there is none
			var loose = view.Carried().Concat(view.Secured()).OfType<Ammo>().Where(ammo => ammo.Parent?.Container is Grid && stock.Left(ammo) > 0).ToList();

			var groups = new List<KeyValuePair<List<Weapon>, List<PlanLine>>>();
			foreach (var group in weapons.GroupBy(weapon => weapon.AmmoCaliber))
			{
				var caliber = group.Key;
				var first = group.First();
				int stacks = StacksFor(group);
				if (stacks <= 0)
				{
					continue;
				}

				var names = string.Join(", ", group.Select(weapon => Game.NameOf(weapon)).Distinct().ToArray());
				var subject = $"Spare ammo for {Game.NameOf(first)}";
				var made = new List<PlanLine>();
				result[first] = made;
				groups.Add(new KeyValuePair<List<Weapon>, List<PlanLine>>(group.ToList(), made));
				// What is asked is a total: the loose ammo of the caliber on you counts toward it, and so does what the top-up of the
				// loose stacks will add; what is still short is brought from the stash
				var mine = loose.Where(ammo => ammo.Caliber == caliber).ToList();
				var onYou = mine.FirstOrDefault();
				bool has = onYou != null;
				int onYouCount = mine.Sum(ammo => stock.Left(ammo));
				int coming = planned.Where(line => line.Topped).Where(line => mine.Any(ammo => ammo.TemplateId.ToString() == line.Template)).Sum(line => line.Amount);
				int asked = has ? onYou.StackMaxSize * stacks : 0;
				int shortBy = has ? Math.Max(0, asked - onYouCount - coming) : 0;
				var head = Steps.Progress(subject, has && shortBy == 0 ? 1 : 0, 1, "spare ammo on you", first.TemplateId.ToString()).WithItem(first).InPart("spare");
				if (has)
				{
					head.What = Game.NameOf(onYou);
					head.WhatTemplate = onYou.TemplateId.ToString();
					head.Amount = onYouCount + coming;
					head.Asked = asked;
				}

				made.Add(head);
				if (has && shortBy == 0)
				{
					continue;
				}

				// The ammo in use: loaded, chambered, in the spare magazines that fit these weapons, and about to be loaded
				var inUse = new HashSet<MongoID>(Planned.Where(pair => pair.Value == caliber).Select(pair => pair.Key));
				foreach (var carried in mine)
				{
					inUse.Add(carried.TemplateId);
				}

				foreach (var weapon in group)
				{
					foreach (var item in weapon.GetAllItems())
					{
						var ammo = item as Ammo;
						if (ammo != null && !ammo.IsUsed && ammo.Caliber == caliber)
						{
							inUse.Add(ammo.TemplateId);
						}

						var magazine = item as Magazine;
						if (magazine != null)
						{
							foreach (var held in magazine.Cartridges.Items.OfType<Ammo>().Where(held => held.Caliber == caliber))
							{
								inUse.Add(held.TemplateId);
							}
						}
					}

					var slot = weapon.GetMagazineSlot();
					if (slot != null)
					{
						foreach (var magazine in spareMagazines.Where(magazine => slot.CheckCompatibility(magazine)))
						{
							foreach (var held in magazine.Cartridges.Items.OfType<Ammo>().Where(held => held.Caliber == caliber))
							{
								inUse.Add(held.TemplateId);
							}
						}
					}
				}

				if (inUse.Count == 0)
				{
					made.Add(Steps.Problem(subject, "no loose ammo on you, and nothing is loaded or chambered to tell which ammo to bring", first.TemplateId.ToString()).WithItem(first).InPart("spare"));
					continue;
				}

				var inStash = stock.Stash.OfType<Ammo>()
					.Where(ammo => ammo.Parent?.Container is Grid && ammo.PinLockState != EItemPinLockState.Locked && ammo.Caliber == caliber && inUse.Contains(ammo.TemplateId) && stock.Left(ammo) > 0 && view.Examined(ammo))
					.ToList();
				if (inStash.Count == 0)
				{
					var inUseNames = string.Join(", ", inUse.Select(type => Game.TemplateName(type.ToString())).ToArray());
					made.Add(Steps.Problem(subject, has
						? $"{onYouCount + coming:N0} of {asked:N0} {head.What}: the stash has no more of it"
						: $"no loose ammo on you, and the stash has none of the ammo in use ({inUseNames})", first.TemplateId.ToString()).WithItem(first).InPart("spare"));
					continue;
				}

				var kinds = inStash.GroupBy(ammo => ammo.TemplateId).Select(kind => kind.First()).ToList();
				var mode = SettingsStore.Current.Ammo.SparePick;
				// The owner's pick (best when it is none of the three)
				var chosen = AmmoRules.PickKind(kinds, mode == "worst" || mode == "random" ? mode : "best");

				// Loose on the stash first, non-FiR first (as for the supplies); whole stacks are used before a split is made
				var sources = inStash.Where(ammo => ammo.TemplateId == chosen.TemplateId)
					.OrderBy(ammo => Game.LooseBeforeFiR(ammo))
					.ThenBy(ammo => ammo.MarkedAsSpawnedInSession ? 1 : 0)
					.ThenBy(ammo => Game.LooseAfterFiR(ammo))
					.ThenByDescending(ammo => stock.Left(ammo))
					.ToList();
				int perStack = chosen.StackMaxSize;
				int totalWant = has ? shortBy : perStack * stacks;
				int want = totalWant;
				var picks = new List<KeyValuePair<Ammo, int>>();
				foreach (var source in sources)
				{
					int take = Math.Min(stock.Left(source), want);
					if (take <= 0)
					{
						continue;
					}

					picks.Add(new KeyValuePair<Ammo, int>(source, take));
					int used;
					stock.Used[source] = (stock.Used.TryGetValue(source, out used) ? used : 0) + take;
					want -= take;
					if (want == 0)
					{
						break;
					}
				}

				int bringing = picks.Sum(pick => pick.Value);
				var why = kinds.Count == 1 ? "the ammo in use" : mode == "random" ? $"random of the {kinds.Count} kinds in use" : $"{mode} of the {kinds.Count} kinds in use";
				var typeName = Game.NameOf(chosen);
				var move = picks;
				made.Add(new PlanLine
				{
					Text = $"Bring {bringing:N0} {typeName} for {names}  ({why}; taken from the stash{(want > 0 ? $"; the stash only has {bringing:N0} of {totalWant:N0}" : "")})",
					Part = "spare",
					Amount = bringing,
					Asked = has ? asked : perStack * stacks,
					What = typeName,
					Template = chosen.TemplateId.ToString(),
					Items = move.Count,
					Run = async () =>
					{
						foreach (var pick in move)
						{
							int count = Math.Min(pick.Value, pick.Key.StackObjectsCount);
							if (count <= 0)
							{
								continue;
							}

							bool whole = count >= pick.Key.StackObjectsCount;
							var error = await Game.MoveTo(controller, pick.Key, places, count);
							if (error == null && whole && !await Game.WaitUntilCarried(controller, pick.Key))
							{
								error = "the game accepted the move but the stack is still in the stash";
							}

							if (error != null)
							{
								Plugin.Log.LogWarning($"Spare ammo {typeName} ({pick.Key.Id}) not brought: {error}");
								return $"could not bring {typeName}: {error}";
							}
						}

						return null;
					}
				});

				// What the stash could not give is a problem: what you asked for is not on you
				if (want > 0)
				{
					int have = has ? onYouCount + coming + bringing : bringing;
					int total = has ? asked : perStack * stacks;
					made.Add(Steps.Problem(subject, $"{have:N0} of {total:N0} {typeName}: the stash has no more of it", chosen.TemplateId.ToString()).WithItem(first).InPart("spare"));
				}
			}

			// Weapons that share a caliber each show the card (repeated, with the same numbers); the ammo is only brought once, with the first
			foreach (var pair in groups)
			{
				var first = pair.Key[0];
				var firstName = Game.NameOf(first);
				var head = pair.Value[0];
				foreach (var other in pair.Key.Skip(1))
				{
					var subject = $"Spare ammo for {Game.NameOf(other)}";
					var copy = new List<PlanLine> { Steps.Progress(subject, head.Have, head.Want, head.Unit, other.TemplateId.ToString()).WithItem(other) };
					foreach (var line in pair.Value.Skip(1))
					{
						if (line.Problem)
						{
							copy.Add(Steps.Problem(subject, line.Detail, other.TemplateId.ToString()).WithItem(other));
						}
						else if (line.Run != null)
						{
							copy.Add(new PlanLine { Text = $"Brought with the stack for {firstName}", Template = line.Template, Covered = true });
						}
					}

					result[other] = copy;
				}
			}

			return result;
		}

		// Whether the right-click menu offers to leave this ammo out of the filling: a stack in a grid, while the filling is on
		internal static bool OffersTopUpExclusion(Item item)
		{
			var ammo = SettingsStore.Current.Ammo;
			return item is Ammo && ammo.Enabled && ammo.TopUpLoose && item.Parent?.Container is Grid;
		}

		internal static bool IsTopUpExcluded(string template) => SettingsStore.Current.Ammo.TopUpExcluded.Any(entry => entry.Template == template);

		// Leaves a kind of ammo out of the filling of the loose stacks, or puts it back
		internal static void ToggleTopUpExcluded(string template, string name)
		{
			var list = SettingsStore.Current.Ammo.TopUpExcluded;
			if (list.RemoveAll(entry => entry.Template == template) == 0)
			{
				list.Add(new ItemRef { Template = template, Name = name });
			}
		}

		// The kinds of ammo you carry loose (one stack of each, to show), for the list of what is left out of the filling
		internal static List<Ammo> LooseKinds(InventoryController controller)
		{
			var view = AmmoRules.AmmoView.Of(controller);
			return view.Carried().Concat(view.Secured()).OfType<Ammo>()
				.Where(stack => stack.Parent?.Container is Grid && stack.StackObjectsCount > 0)
				.GroupBy(stack => stack.TemplateId).Select(kind => kind.First())
				.OrderBy(stack => Game.NameOf(stack)).ToList();
		}

		// The loose stacks of ammo you carry (vest, pockets, backpack, pouch; not in magazines or boxes) filled up to a full stack with
		// the same ammo from the stash. After the magazines, which use loose rounds first. A stack the magazines use up completely
		// is left out (it will be gone), and so is a locked one, and a kind the owner left out (it still counts as spare ammo). Only stacks you already carry: no new kinds of ammo.
		private static void AddLoose(InventoryController controller, AmmoRules.AmmoStock stock, List<PlanLine> lines)
		{
			var view = AmmoRules.AmmoView.Of(controller);
			var mine = view.Carried().Concat(view.Secured()).OfType<Ammo>()
				.Where(stack => stack.Parent?.Container is Grid && stack.PinLockState != EItemPinLockState.Locked && stock.Left(stack) > 0 && !IsTopUpExcluded(stack.TemplateId.ToString()))
				.OrderBy(stack => Game.NameOf(stack.Parent.Container.ParentItem))
				.ThenBy(stack => Game.NameOf(stack))
				.ToList();
			if (mine.Count == 0)
			{
				return;
			}

			var sources = !view.HasStash ? new List<Ammo>() : view.Stash().OfType<Ammo>()
				.Where(stack => stack.Parent?.Container is Grid && stack.PinLockState != EItemPinLockState.Locked && view.Examined(stack))
				.ToList();
			var rows = new List<PlanLine>();
			int full = 0;
			foreach (var target in mine)
			{
				int have = stock.Left(target);
				int free = target.StackMaxSize - have;
				if (free <= 0)
				{
					full++;
					continue;
				}

				var allocation = new List<KeyValuePair<Ammo, int>>();
				foreach (var source in sources.Where(stack => stack != target && stack.TemplateId == target.TemplateId && stock.Left(stack) > 0).OrderBy(stack => stack.StackObjectsCount))
				{
					int take = Math.Min(stock.Left(source), free);
					allocation.Add(new KeyValuePair<Ammo, int>(source, take));
					int used;
					stock.Used[source] = (stock.Used.TryGetValue(source, out used) ? used : 0) + take;
					free -= take;
					if (free == 0)
					{
						break;
					}
				}

				int added = allocation.Sum(pair => pair.Value);
				if (SettingsStore.Current.Ammo.LooseShortIsProblem && free > 0)
				{
					// Short, and the stash has no more of it (or not enough): the owner asked for that to be reported
					rows.Add(Steps.Problem($"{Game.NameOf(target)} in {Game.NameOf(target.Parent.Container.ParentItem)}", $"{have + added}/{target.StackMaxSize}, the stash has no more of it", target.TemplateId.ToString()));
				}

				if (added == 0)
				{
					continue; // nothing of it in the stash: it can't be fuller
				}

				var stack = target;
				var picks = allocation;
				rows.Add(new PlanLine
				{
					Text = $"{Game.NameOf(target)} in {Game.NameOf(target.Parent.Container.ParentItem)}: {have}/{target.StackMaxSize}  topped up with {added} from the stash",
					Template = target.TemplateId.ToString(),
					Label = Game.NameOf(target),
					Place = Game.NameOf(target.Parent.Container.ParentItem),
					PlaceTemplate = target.Parent.Container.ParentItem.TemplateId.ToString(),
					Amount = added,
					Have = have,
					Want = target.StackMaxSize,
					Topped = true,
					From = SourcesOf(stock, picks),
					Items = 0,
					Run = async () =>
					{
						foreach (var pick in picks)
						{
							// The counts as they are now (earlier lines of this run have moved rounds around): never ask for more than the source
							// has or the target has room for
							int room = stack.StackMaxSize - stack.StackObjectsCount;
							int can = Math.Min(pick.Value, Math.Min(pick.Key.StackObjectsCount, room));
							if (can <= 0)
							{
								continue;
							}

							var error = await Game.MergeInto(controller, pick.Key, can, stack);
							if (error != null)
							{
								Plugin.Log.LogWarning($"Top-up of {stack.Id} ({stack.StackObjectsCount}/{stack.StackMaxSize}) from {pick.Key.Id} ({pick.Key.StackObjectsCount} left, planned {pick.Value}, asked {can}) failed: {error}");
								return $"could not add to {Game.NameOf(stack)}: {error}";
							}
						}

						return null;
					}
				});
			}

			lines.Add(Steps.Progress(LooseAmmo, full, mine.Count, "full", mine[0].TemplateId.ToString()));
			lines.AddRange(rows);
		}

		// Magazines with the same trouble in one line: "3 magazines: nothing to follow", with what they are and what ammo they could take
		private static void AddStuck(List<PlanLine> lines, List<Stuck> stuck)
		{
			foreach (var group in stuck.GroupBy(one => one.Options ?? ""))
			{
				var list = group.ToList();
				var what = string.Join(", ", list.GroupBy(one => one.Label).Select(label => label.Count() > 1 ? $"{label.Key} x{label.Count()}" : label.Key).ToArray());
				bool noAmmo = group.Key == "";
				var subject = noAmmo
					? (list.Count == 1 ? "1 magazine: no ammo left for it" : $"{list.Count} magazines: no ammo left for them")
					: (list.Count == 1 ? "1 empty magazine: which ammo?" : $"{list.Count} empty magazines: which ammo?");
				var detail = noAmmo ? what : $"{what}. They could take {group.Key}, and nothing tells which one: load one of them yourself and the others follow.";
				var line = Steps.Problem(subject, detail, list[0].Template);
				line.Part = "stuck";
				line.Mags = list.Select(one => one.Info).ToList();
				line.Options = noAmmo ? null : group.Key;
				line.AlertIconOnly = true; // the alert box has no card above it, so it keeps the icon; the Ready page drops it
				lines.Add(line);
			}
		}

		// Why none of the empty magazines you carry can be filled for the weapon: nothing tells which of several kinds of ammo to put in, or
		// there is no ammo left for it
		internal static string NoMagazineFilled(AmmoRules.FillPlan failed)
		{
			if (failed != null && failed.Problem == "nothing to follow")
			{
				return $"no magazine in it, and nothing tells which ammo to put in your empty magazines ({failed.Options}): load one of them yourself and the others follow";
			}

			if (failed != null && failed.Problem == "no compatible ammo left")
			{
				return "no magazine in it, and there is no ammo left to fill an empty magazine for it";
			}

			return "no magazine in it, and none of yours can be put in and filled for it";
		}

		// A weapon with no magazine: one of your equipped magazines that fits it goes in, the fullest first (one holding ammo of another
		// caliber is skipped), an empty one only if it can be filled with ammo that fits the weapon. Nothing from the stash. The
		// magazine is filled first, then put in.
		private static void PlanInsert(InventoryController controller, AmmoRules.AmmoStock stock, Weapon weapon, Slot slot, List<Magazine> spares, HashSet<Magazine> inserted, List<PlanLine> lines, Dictionary<string, int> missing, List<Stuck> stuck)
		{
			var name = Game.NameOf(weapon);
			var template = weapon.TemplateId.ToString();
			var candidates = spares
				.Where(magazine => !inserted.Contains(magazine) && slot.CheckCompatibility(magazine)
					&& magazine.Cartridges.Items.OfType<Ammo>().All(ammo => ammo.Caliber == weapon.AmmoCaliber))
				.OrderByDescending(magazine => magazine.Count)
				.ToList();

			Magazine chosen = null;
			AmmoRules.FillPlan fill = null, failed = null;
			foreach (var candidate in candidates)
			{
				if (candidate.Count > 0)
				{
					chosen = candidate;
					break;
				}

				var trial = AmmoRules.PlanFill(controller, stock, candidate, weapon);
				if (trial.Problem == null)
				{
					chosen = candidate;
					fill = trial;
					break;
				}

				failed = failed ?? trial;
			}

			if (chosen == null)
			{
				bool fitsAny = spares.Any(magazine => !inserted.Contains(magazine) && slot.CheckCompatibility(magazine));
				lines.Add(Alone(Steps.Problem(name, candidates.Count == 0
					? (fitsAny ? "no magazine in it, and the magazines you carry that fit it hold ammo of another caliber" : "no magazine in it, and none of the magazines you carry fits it")
					: NoMagazineFilled(failed), template).WithItem(weapon).InPart("magazine")));
				return;
			}

			inserted.Add(chosen);
			lines.Add(Steps.Progress(name, 0, chosen.MaxCount, "rounds", template).WithItem(weapon).InPart("magazine"));
			int before = lines.Count;
			var made = AddFill(controller, stock, chosen, $"{Game.NameOf(chosen)} for {name}", lines, missing, stuck, weapon, fill, null);
			bool filled = lines.Count > before;

			var put = chosen;
			var heldAmmo = chosen.Cartridges.Items.OfType<Ammo>().FirstOrDefault();
			var address = slot.CreateItemAddress();
			lines.Add(new PlanLine
			{
				Text = $"Put {Game.NameOf(chosen)} from your {Where(chosen)} into {name}  ({(filled && made != null ? Math.Min(chosen.MaxCount, chosen.Count + made.Rounds) : chosen.Count)}/{chosen.MaxCount}{(filled ? ", after filling it" : "")})",
				Place = Where(chosen),
				PlaceTemplate = chosen.Parent.Container.ParentItem.TemplateId.ToString(),
				Have = chosen.Count,
				Want = chosen.MaxCount,
				Part = "put",
				Label = heldAmmo == null ? null : Game.NameOf(heldAmmo),
				WhatTemplate = heldAmmo?.TemplateId.ToString(),
				What = Game.NameOf(chosen),
				Template = chosen.TemplateId.ToString(),
				Items = 0,
				Fill = !filled,
				Run = () => Game.PlaceAt(controller, put, 1, address)
			});
			AddChamber(controller, stock, weapon, chosen, made, lines);
		}

		// A weapon with an empty chamber gets one round of the ammo its magazine holds (or is about to hold: the plan's ammo). It
		// goes after the magazine lines, so the weapon already has its magazine when the round is chambered.
		private static void AddChamber(InventoryController controller, AmmoRules.AmmoStock stock, Weapon weapon, Magazine magazine, AmmoRules.FillPlan made, List<PlanLine> lines)
		{
			if (!SettingsStore.Current.Ammo.Chamber)
			{
				return;
			}

			var chamber = weapon.Chambers.FirstOrDefault(slot => slot.ContainedItem == null);
			if (chamber == null)
			{
				return;
			}

			MongoID? type = null;
			if (made != null && made.Problem == null && made.Rounds > 0)
			{
				type = made.Type;
			}
			else
			{
				var held = magazine.Cartridges.Items.OfType<Ammo>().FirstOrDefault();
				if (held != null)
				{
					type = held.TemplateId;
				}
			}

			var name = Game.NameOf(weapon);
			if (type == null)
			{
				return; // an empty magazine with nothing to follow is reported by its own line
			}

			var round = AmmoRules.PickForChamber(controller, stock, weapon, chamber, magazine, type.Value);
			if (round == null)
			{
				lines.Add(Steps.Problem(name, "empty chamber, and none of the ammo in its magazine is left to chamber", weapon.TemplateId.ToString()).WithItem(weapon).InPart("chamber"));
				return;
			}

			var piece = round;
			var address = chamber.CreateItemAddress();
			var where = stock.Stash.Contains(round) ? "the stash" : "your gear";
			lines.Add(new PlanLine
			{
				Text = $"Chamber {round.LocalizedShortName()} in {name}  (same as its magazine; taken from {where})",
				Part = "chamber",
				Why = "same as its magazine",
				Place = stock.Stash.Contains(round) ? "Stash" : round.Parent?.Container?.ParentItem == null ? null : Game.NameOf(round.Parent.Container.ParentItem),
				PlaceTemplate = stock.Stash.Contains(round) ? null : round.Parent?.Container?.ParentItem?.TemplateId.ToString(),
				What = round.LocalizedShortName(),
				Template = round.TemplateId.ToString(),
				Chamber = true,
				Items = 0,
				Run = () => Game.PlaceAt(controller, piece, 1, address)
			});
		}

		// The containers the rounds of a fill are taken from, with how many from each
		private static List<PlaceCount> SourcesOf(AmmoRules.AmmoStock stock, IEnumerable<KeyValuePair<Ammo, int>> allocation) =>
			allocation.Where(pair => pair.Key.Parent?.Container?.ParentItem != null)
				.GroupBy(pair => pair.Key.Parent.Container.ParentItem.Id.ToString())
				.Select(group =>
				{
					var place = group.First().Key.Parent.Container.ParentItem;
					// The stash has no icon of its own to draw: no template, the window writes its name. Ammo inside a case in the stash is from that case.
					bool stash = stock.Stash.Contains(group.First().Key) && Game.InContainer(group.First().Key) == 0;
					return new PlaceCount { Template = stash ? null : place.TemplateId.ToString(), Name = stash ? "Stash" : Game.NameOf(place), Count = group.Sum(pair => pair.Value), Container = stash ? null : place as CompoundItem };
				}).ToList();

		// A fill line for the magazine. takeOutOf: the weapon slot the magazine sits in (the game does not load it there: it is taken
		// out to a free place of yours, filled and put back).
		private static AmmoRules.FillPlan AddFill(InventoryController controller, AmmoRules.AmmoStock stock, Magazine magazine, string label, List<PlanLine> lines, Dictionary<string, int> missing, List<Stuck> stuck, Weapon forWeapon, AmmoRules.FillPlan ready, Slot takeOutOf)
		{
			if (magazine.Count >= magazine.MaxCount)
			{
				return null;
			}

			var plan = ready ?? AmmoRules.PlanFill(controller, stock, magazine, forWeapon);
			var magazineTemplate = magazine.TemplateId.ToString();
			var container = magazine.Parent?.Container is Grid ? magazine.Parent.Container.ParentItem : null;
			if (plan.Problem != null)
			{
				stuck.Add(new Stuck
				{
					Label = $"{label} ({magazine.Count}/{magazine.MaxCount})",
					Template = magazineTemplate,
					Options = plan.Problem == "nothing to follow" ? plan.Options : null,
					Info = new MagInfo
					{
						Template = magazineTemplate, Name = container == null ? label : Game.NameOf(magazine), Place = container == null ? null : Game.NameOf(container),
						PlaceTemplate = container?.TemplateId.ToString(), Have = magazine.Count, Want = magazine.MaxCount
					}
				});
				return plan;
			}

			if (plan.Short > 0)
			{
				int already;
				missing.TryGetValue(plan.TypeName, out already);
				missing[plan.TypeName] = already + plan.Short;
			}

			if (plan.Rounds == 0)
			{
				return plan;
			}

			Planned[plan.Type.Value] = plan.Allocation[0].Key.Caliber;
			var run = plan;
			var weaponSlot = takeOutOf;
			lines.Add(new PlanLine
			{
				Text = $"{label}: {magazine.Count}/{magazine.MaxCount}  +{plan.Rounds} {plan.TypeName}  ({plan.Reason}; taken from {plan.Sources}){(takeOutOf != null ? "  [taken out and put back]" : "")}",
				Label = Game.NameOf(magazine),
				Place = container == null ? null : Game.NameOf(container),
				PlaceTemplate = container?.TemplateId.ToString(),
				Have = magazine.Count,
				Want = magazine.MaxCount,
				ItemTemplate = magazineTemplate,
				Part = "magazine",
				Amount = plan.Rounds,
				Why = plan.Reason,
				From = SourcesOf(stock, plan.Allocation),
				What = plan.TypeName,
				Template = plan.Allocation[0].Key.TemplateId.ToString(),
				Fill = true,
				Items = 0,
				Run = async () =>
				{
					if (weaponSlot != null)
					{
						var outError = await Game.MoveTo(controller, run.Magazine, new[] { "Vest", "Pockets", "Backpack" });
						if (outError != null)
						{
							return $"could not take {label} out of the weapon: {outError}";
						}
					}

					int loaded = await AmmoRules.Load(controller, run);

					if (weaponSlot != null)
					{
						var backError = await Game.PlaceAt(controller, run.Magazine, 1, weaponSlot.CreateItemAddress());
						if (backError != null)
						{
							return $"{label} was filled but could not be put back in the weapon (it is in your gear): {backError}";
						}
					}

					return loaded >= run.Rounds ? null : $"only {loaded} of {run.Rounds} {run.TypeName} could be loaded into {label}";
				}
			});
			return plan;
		}
	}
}
