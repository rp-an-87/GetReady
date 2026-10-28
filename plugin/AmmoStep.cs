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
		public string Name => "Ammo";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.Ammo.Enabled; set => SettingsStore.Current.Ammo.Enabled = value; }

		// A magazine that can't be filled, to be reported with the others of its kind
		private class Stuck
		{
			public string Label;
			public string Template;
			public string Options; // null: no compatible ammo left at all
		}

		private static string Where(Magazine magazine) => Game.NameOf(magazine.Parent.Container.ParentItem);

		public List<PlanLine> Plan(InventoryController controller)
		{
			var inventory = controller.Inventory;
			var stock = AmmoRules.AmmoStock.Build(controller);
			var lines = new List<PlanLine>();
			var missing = new Dictionary<string, int>();
			var stuck = new List<Stuck>();
			var spares = Game.Carried(inventory).OfType<Magazine>()
				.Where(magazine => magazine.Parent?.Container is Grid && magazine.PinLockState != EItemPinLockState.Locked)
				.ToList();
			var inserted = new HashSet<Magazine>();

			// The magazine in each weapon: one card per weapon
			foreach (var weapon in AmmoRules.EquippedWeapons(inventory))
			{
				var slot = weapon.GetMagazineSlot();
				if (slot == null)
				{
					continue; // barrel-only weapons and special ones: later
				}

				var name = Game.NameOf(weapon);
				var template = weapon.TemplateId.ToString();
				if (weapon.PinLockState == EItemPinLockState.Locked)
				{
					lines.Add(Steps.Info($"{name}: left alone (locked)"));
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
					lines.Add(Steps.Info($"{name}: left alone (its magazine is locked)"));
					continue;
				}

				lines.Add(Steps.Progress(name, magazine.Count, magazine.MaxCount, "rounds", template).WithItem(weapon));
				var made = AddFill(controller, stock, magazine, $"{name}'s magazine", lines, missing, stuck, null, null, slot);
				AddChamber(controller, stock, weapon, magazine, made, lines);
			}

			// The spare magazines on you, nearest the same container together
			var rest = spares.Where(magazine => !inserted.Contains(magazine))
				.OrderBy(magazine => Where(magazine))
				.ThenBy(magazine => magazine.Template.NameLocalizationKey)
				.ToList();
			if (rest.Count > 0)
			{
				lines.Add(Steps.Progress("Magazines on you", rest.Count(magazine => magazine.Count >= magazine.MaxCount), rest.Count, "full", rest[0].TemplateId.ToString()));
				foreach (var magazine in rest)
				{
					AddFill(controller, stock, magazine, $"{Game.NameOf(magazine)} in {Where(magazine)}", lines, missing, stuck, null, null, null);
				}
			}

			AddStuck(lines, stuck);
			foreach (var pair in missing)
			{
				lines.Add(Steps.Problem($"Ammo: {pair.Key}", $"missing {pair.Value:N0} round(s), none left"));
			}

			if (SettingsStore.Current.Ammo.TopUpLoose)
			{
				AddLoose(controller, stock, lines);
			}

			return lines;
		}

		// The loose stacks of ammo you carry (vest, pockets, backpack, pouch; not in magazines or boxes) filled up to a full stack with
		// the same ammo from the stash. After the magazines, which use loose rounds first. A stack the magazines use up completely
		// is left out (it will be gone), and so is a locked one. Only stacks you already carry: no new kinds of ammo.
		private static void AddLoose(InventoryController controller, AmmoRules.AmmoStock stock, List<PlanLine> lines)
		{
			var inventory = controller.Inventory;
			var mine = Game.CarriedAndSecured(inventory).OfType<Ammo>()
				.Where(stack => stack.Parent?.Container is Grid && stack.PinLockState != EItemPinLockState.Locked && stock.Left(stack) > 0)
				.OrderBy(stack => Game.NameOf(stack.Parent.Container.ParentItem))
				.ThenBy(stack => Game.NameOf(stack))
				.ToList();
			if (mine.Count == 0)
			{
				return;
			}

			var sources = inventory.Stash == null ? new List<Ammo>() : inventory.GetPlayerItems(EPlayerItems.Stash).OfType<Ammo>()
				.Where(stack => stack.Parent?.Container is Grid && stack.PinLockState != EItemPinLockState.Locked && controller.Examined(stack))
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
				if (added == 0)
				{
					continue; // nothing of it in the stash: it can't be fuller
				}

				var stack = target;
				var picks = allocation;
				rows.Add(new PlanLine
				{
					Text = $"{Game.NameOf(target)} in {Game.NameOf(target.Parent.Container.ParentItem)}: {have}/{target.StackMaxSize}  +{added} from the stash",
					Template = target.TemplateId.ToString(),
					Topped = true,
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

			lines.Add(Steps.Progress("Loose ammo on you", full, mine.Count, "full", mine[0].TemplateId.ToString()));
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
				var detail = noAmmo ? what : $"{what}. Load one of them yourself and the others follow.";
				var line = Steps.Problem(subject, detail, list[0].Template);
				line.AlertIconOnly = true; // the alert box has no card above it, so it keeps the icon; the Ready page drops it
				lines.Add(line);
			}
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
			AmmoRules.FillPlan fill = null;
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
			}

			if (chosen == null)
			{
				lines.Add(Steps.Problem(name, candidates.Count == 0
					? "no magazine in it, and none of the magazines you carry fits it"
					: "no magazine in it, and none of yours can be put in and filled for it", template).WithItem(weapon));
				return;
			}

			inserted.Add(chosen);
			lines.Add(Steps.Progress(name, 0, chosen.MaxCount, "rounds", template).WithItem(weapon));
			int before = lines.Count;
			var made = AddFill(controller, stock, chosen, $"{Game.NameOf(chosen)} for {name}", lines, missing, stuck, weapon, fill, null);
			bool filled = lines.Count > before;

			var put = chosen;
			var address = slot.CreateItemAddress();
			lines.Add(new PlanLine
			{
				Text = $"Put {Game.NameOf(chosen)} from your {Where(chosen)} into {name}  ({chosen.Count}/{chosen.MaxCount}{(filled ? ", after filling it" : "")})",
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
				lines.Add(Steps.Problem(name, "empty chamber, and none of the ammo in its magazine is left to chamber", weapon.TemplateId.ToString()).WithItem(weapon));
				return;
			}

			var piece = round;
			var address = chamber.CreateItemAddress();
			var where = stock.Stash.Contains(round) ? "the stash" : "your gear";
			lines.Add(new PlanLine
			{
				Text = $"Chamber {round.LocalizedShortName()} in {name}  (same as its magazine, from {where})",
				Template = round.TemplateId.ToString(),
				Chamber = true,
				Items = 0,
				Run = () => Game.PlaceAt(controller, piece, 1, address)
			});
		}

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
			if (plan.Problem != null)
			{
				stuck.Add(new Stuck { Label = $"{label} ({magazine.Count}/{magazine.MaxCount})", Template = magazineTemplate, Options = plan.Problem == "nothing to follow" ? plan.Options : null });
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

			var run = plan;
			var weaponSlot = takeOutOf;
			lines.Add(new PlanLine
			{
				Text = $"{label}: {magazine.Count}/{magazine.MaxCount}  +{plan.Rounds} {plan.TypeName}  ({plan.Reason}, from {plan.Sources}){(takeOutOf != null ? "  [taken out and put back]" : "")}",
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
