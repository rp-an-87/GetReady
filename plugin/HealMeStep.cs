using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;

namespace GetReady
{
	// Heals you with the medicine in the stash. One list of the medicine in the owner's order: the most urgent injury present is treated
	// with the first item of the list that treats it, again and again until you are healed. Dedicated items are listed before the kits,
	// so a kit is mostly left for what is still hurt (a kit treats everything it can on a part at once, that is the game's).
	// It runs last, so what the other steps bring is already in your gear and is never used up here.
	internal class HealMeStep : IStep
	{
		public string Name => "Treat injuries";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.HealMe.Enabled; set => SettingsStore.Current.HealMe.Enabled = value; }

		internal class Trouble
		{
			public string Id;
			public string Name;
			public object Icon; // the type of its status effect in the game, or the health factor for plain HP
			public Func<Item, bool> Treats;
		}

		// The injuries, most urgent first (the ids are HealChoice's)
		internal static readonly Trouble[] Troubles =
		{
			new Trouble { Id = "heavy", Name = "Heavy bleeding", Icon = typeof(IHeavyBleeding), Treats = item => Game.Treats(item, EDamageEffectType.HeavyBleeding) },
			new Trouble { Id = "light", Name = "Light bleeding", Icon = typeof(ILightBleeding), Treats = item => Game.Treats(item, EDamageEffectType.LightBleeding) },
			new Trouble { Id = "fracture", Name = "Fracture", Icon = typeof(IFracture), Treats = item => Game.Treats(item, EDamageEffectType.Fracture) },
			new Trouble { Id = "hp", Name = "Health (HP)", Icon = EHealthFactorType.Health, Treats = item => Game.IsHealingKit(item) && Game.UnderMedikit.Any(parent => Icons.IsUnder(item.Template, parent)) }
		};

		private const string Grizzly = "590c657e86f77412b013051d", Salewa = "544fb45d4bdc2dee738b4568", Afak = "60098ad7c2240c0fe85c570a",
			Ifak = "590c678286f77426c9660122", Car = "590c661e86f7741e566b646a", Ai2 = "5755356824597772cb798962", Surv12 = "5d02797c86f774203f38e30a";

		// The medicine that may be used: found by what it treats (modded ones too), the ones named first in that order (the dedicated
		// items, then the kits, strongest first). Its order and what is switched off are kept like the supplies' (Target is not used).
		internal static readonly GroupDef Items = new GroupDef
		{
			Id = "healme", Name = "Medicine to use", Unit = "items", DefaultTarget = 0, Bindable = false,
			Under = Game.UnderMedikit.Concat(Game.UnderMedicalItem).ToArray(),
			Match = item => item is Meds && !(item is Drugs) && !(item is Stimulator) && Troubles.Any(trouble => trouble.Treats(item)),
			Entries = new[]
			{
				"544fb25a4bdc2dfb738b4567", "5751a25924597722c463c472", "60098af40accd37ef2175f27", "5e831507ea0a7c419c2f9bd9",
				"5e8488fa988a8701445df1e4", "544fb3364bdc2d34748b456a", "5af0454c86f7746bf20992e8", "6937ecf8628ee476240c07cb",
				Grizzly, Salewa, Afak, Ifak, Car, Ai2, Surv12 // the Surv12 is a surgery kit that happens to set bones: off at first
			}.Select(Entry.Count).ToArray(),
			DefaultOff = new[] { Surv12 }
		};

		private static readonly Dictionary<string, bool> TreatsCache = new Dictionary<string, bool>();

		// An item of a template, to ask what it treats: the game's own (the tests put theirs here)
		internal static Func<string, Item> ItemOf = template => Icons.ItemFor(template);

		// Whether the item can treat the injury (from the game's data)
		internal static bool Treats(string template, string trouble)
		{
			bool known;
			string key = template + "|" + trouble;
			if (!TreatsCache.TryGetValue(key, out known))
			{
				var item = ItemOf(template);
				known = item != null && Troubles.First(t => t.Id == trouble).Treats(item);
				TreatsCache[key] = known;
			}

			return known;
		}

		internal static bool IsOn(string template) => !Items.Setting.Off.Contains(template);

		// The usual order and the usual items on
		internal static void ResetAll()
		{
			Items.Setting.Order = new List<string>();
			Items.Setting.Off = Items.DefaultOff.ToList();
		}

		// May this item be used for this injury: on and treats it
		private static bool UsableFor(string template, string trouble) => IsOn(template) && Treats(template, trouble);

		private static List<string> Order() => Items.OrderedEntries().Select(entry => entry.Template).ToList();

		// The stash items that can be used: not locked, not skipped
		private static List<Item> Stock(Func<List<Item>> stash, HashSet<Item> skipped) =>
			stash().Where(item => !skipped.Contains(item) && item.PinLockState != EItemPinLockState.Locked).ToList();

		// The injuries present now, most urgent first. A part that is destroyed is left out: only a surgery kit mends it, and this
		// doesn't use those.
		private static List<string> Needed(IHealthController health)
		{
			bool heavy = false, light = false, fracture = false, hp = false;
			foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
			{
				if (part == EBodyPart.Common || health.IsBodyPartDestroyed(part))
				{
					continue;
				}

				if (!health.GetBodyPartHealth(part).AtMaximum)
				{
					hp = true;
				}

				foreach (var effect in health.GetAllActiveEffects(part))
				{
					if (typeof(IBleeding).IsAssignableFrom(effect.Type))
					{
						if (effect.Type.Name.Contains("Heavy"))
						{
							heavy = true;
						}
						else
						{
							light = true;
						}
					}
					else if (typeof(IFracture).IsAssignableFrom(effect.Type))
					{
						fracture = true;
					}
				}
			}

			var needed = new List<string>();
			if (heavy) needed.Add("heavy");
			if (light) needed.Add("light");
			if (fracture) needed.Add("fracture");
			if (hp) needed.Add("hp");
			return needed;
		}

		public List<PlanLine> Plan(InventoryController controller) =>
			Plan(Game.HealthController(), () => Game.Stash(controller.Inventory), Game.TemplateName);

		// The plan for this health and this stash (nameOf: the name of an item by template)
		internal List<PlanLine> Plan(IHealthController health, Func<List<Item>> stash, Func<string, string> nameOf)
		{
			if (health == null)
			{
				return new List<PlanLine> { Steps.Info("Your health isn't available here.") };
			}

			var present = Needed(health);
			var order = Order();
			var stock = new HashSet<string>(Stock(stash, new HashSet<Item>()).Select(item => item.TemplateId.ToString()));
			string first = HealChoice.Next(present, order, UsableFor, stock.Contains);
			var total = health.GetBodyPartHealth(EBodyPart.Common);
			var icon = first ?? Items.ActiveEntries().FirstOrDefault()?.Template;
			var afflictions = Afflictions(health);
			var progress = Steps.Progress("Health", total.Current, total.Maximum, "HP", icon);
			var status = Steps.Info(afflictions.Length == 0 ? "No afflictions" : "Afflictions: " + string.Join("; ", afflictions));
			var lines = new List<PlanLine> { progress, status };
			if (!Game.NeedsHealing(health))
			{
				return lines;
			}

			if (present.Count == 0)
			{
				lines.Add(Steps.Problem("Treat injuries", "Nothing here can treat what is wrong (a destroyed part needs a surgery kit)"));
				return lines;
			}

			foreach (var trouble in HealChoice.Missing(present, order, UsableFor, stock.Contains))
			{
				lines.Add(Steps.Problem("Treat injuries", Troubles.First(t => t.Id == trouble).Name + ": no allowed medicine left in the stash"));
			}

			if (first == null)
			{
				return lines;
			}

			var names = order.Where(template => stock.Contains(template) && present.Any(trouble => UsableFor(template, trouble))).Select(nameOf).Take(3);
			lines.Add(new PlanLine
			{
				Template = first,
				Text = "Heal up with " + string.Join(", ", names.ToArray()),
				Items = 0,
				Heal = true,
				Run = () => HealUp(health, stash)
			});
			return lines;
		}

		// "Left arm: heavy bleeding, fracture" for every hurt part (a destroyed part, bleeding, a fracture); empty when there is none
		private static string[] Afflictions(IHealthController health)
		{
			var found = new List<string>();
			foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
			{
				if (part == EBodyPart.Common)
				{
					continue;
				}

				var what = new List<string>();
				if (health.IsBodyPartDestroyed(part))
				{
					what.Add("destroyed");
				}

				foreach (var effect in health.GetAllActiveEffects(part))
				{
					if (typeof(IBleeding).IsAssignableFrom(effect.Type))
					{
						what.Add(effect.Type.Name.Contains("Heavy") ? "heavy bleeding" : "bleeding");
					}
					else if (typeof(IFracture).IsAssignableFrom(effect.Type))
					{
						what.Add("fracture");
					}
				}

				if (what.Count > 0)
				{
					found.Add(System.Text.RegularExpressions.Regex.Replace(part.ToString(), "(?<=[a-z])(?=[A-Z])", " ") + ": " + string.Join(", ", what.Distinct().ToArray()));
				}
			}

			return found.ToArray();
		}

		// Treats the most urgent injury with the first item of the list that may be used for it, again and again, until you are healed or the medicine runs out
		internal static async Task<string> HealUp(IHealthController health, Func<List<Item>> stashOf, int delayMs = 120)
		{
			if (health == null)
			{
				return "your health isn't available here";
			}

			var skipped = new HashSet<Item>();
			int used = 0;
			for (int guard = 0; guard < 80 && Game.NeedsHealing(health); guard++)
			{
				// The first item of the list that may be used for the most urgent injury present and is in the stash
				var stash = Stock(stashOf, skipped);
				var template = HealChoice.Next(Needed(health), Order(), UsableFor, wanted => stash.Any(candidate => candidate.TemplateId.ToString() == wanted));
				if (template == null)
				{
					break;
				}

				var item = stash.Where(candidate => candidate.TemplateId.ToString() == template).OrderBy(candidate => candidate.SpawnedInSession ? 1 : 0).ThenBy(Game.ResourceOf).First();

				if (!health.ApplyItem(item, EBodyPart.Common))
				{
					skipped.Add(item); // nothing to treat with this one right now
					continue;
				}

				used++;
				await Task.Delay(delayMs);
			}

			if (Game.NeedsHealing(health))
			{
				return used == 0 ? "none of the medicine you allowed could heal you" : $"used {used} item(s) but you are still hurt: the allowed medicine ran out or can't treat the rest";
			}

			Plugin.Log?.LogInfo($"Healed with {used} item(s)");
			return null;
		}
	}
}
