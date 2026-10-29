using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;

namespace GetReady
{
	// Heals you with the medicine in the stash, in your priority order and only with the items you allow (only Grizzlies, say).
	// It runs last, so what the other steps bring is already in your gear and is never used up here.
	internal class HealMeStep : IStep
	{
		public string Name => "Heal me";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.HealMe.Enabled; set => SettingsStore.Current.HealMe.Enabled = value; }

		// The medicine that can be used: its order and what is switched off are kept like the supplies' (Target is not used)
		internal static readonly GroupDef Items = new GroupDef
		{
			Id = "healme", Name = "Medicine to use", Unit = "items", DefaultTarget = 0, Bindable = false,
			Under = Game.UnderMedikit.Concat(Game.UnderMedicalItem).ToArray(),
			Match = item => item is Meds && !(item is Drugs) && !(item is Stimulator),
			Entries = new[]
			{
				Entry.Count("590c657e86f77412b013051d"), // Grizzly
				Entry.Count("544fb45d4bdc2dee738b4568"), // Salewa
				Entry.Count("60098ad7c2240c0fe85c570a"), // AFAK
				Entry.Count("590c678286f77426c9660122"), // IFAK
				Entry.Count("590c661e86f7741e566b646a"), // Car
				Entry.Count("5755356824597772cb798962")  // AI-2
			}
		};

		// The stash items that may be used, in the owner's order; within one kind, non-FiR first, then the least resource left
		private static IEnumerable<Item> Usable(InventoryController controller, HashSet<Item> skipped)
		{
			var stash = Game.Stash(controller.Inventory).Where(item => !skipped.Contains(item) && item.PinLockState != EItemPinLockState.Locked).ToList();
			foreach (var entry in Items.ActiveEntries())
			{
				foreach (var item in stash.Where(item => item.TemplateId.ToString() == entry.Template).OrderBy(item => item.SpawnedInSession ? 1 : 0).ThenBy(Game.ResourceOf))
				{
					yield return item;
				}
			}
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var health = Game.HealthController();
			if (health == null)
			{
				return new List<PlanLine> { Steps.Info("Your health isn't available here.") };
			}

			if (!Game.NeedsHealing(health))
			{
				return new List<PlanLine> { Steps.Progress("Health", 1, 1, "healed") };
			}

			var lines = new List<PlanLine> { Steps.Progress("Health", 0, 1, "healed") };
			var first = Usable(controller, new HashSet<Item>()).FirstOrDefault();
			if (first == null)
			{
				lines.Add(Steps.Problem("Heal me", "You are hurt and the stash has none of the medicine you allowed"));
				return lines;
			}

			var names = Items.ActiveEntries().Where(entry => Game.Stash(controller.Inventory).Any(item => item.TemplateId.ToString() == entry.Template)).Select(entry => entry.Name).Take(3);
			lines.Add(new PlanLine
			{
				Template = first.TemplateId.ToString(),
				Text = "Heal up with " + string.Join(", ", names.ToArray()),
				Items = 0,
				Heal = true,
				Run = () => HealUp(controller)
			});
			return lines;
		}

		// Uses the first allowed item that can do something, again and again, until you are healed or the medicine runs out
		private static async Task<string> HealUp(InventoryController controller)
		{
			var health = Game.HealthController();
			if (health == null)
			{
				return "your health isn't available here";
			}

			var skipped = new HashSet<Item>();
			int used = 0;
			for (int guard = 0; guard < 80 && Game.NeedsHealing(health); guard++)
			{
				var item = Usable(controller, skipped).FirstOrDefault();
				if (item == null)
				{
					break;
				}

				if (!health.ApplyItem(item, EBodyPart.Common))
				{
					skipped.Add(item); // nothing to treat with this one right now
					continue;
				}

				used++;
				await Task.Delay(120);
			}

			if (Game.NeedsHealing(health))
			{
				return used == 0 ? "none of the medicine you allowed could heal you" : $"used {used} item(s) but you are still hurt: the allowed medicine ran out or can't treat the rest";
			}

			Plugin.Log.LogInfo($"Healed with {used} item(s)");
			return null;
		}
	}
}
