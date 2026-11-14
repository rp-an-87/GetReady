using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;

namespace GetReady
{
	// The soft reminders of the map screen: what is worth a word before you go (a green flare, the paracord and Red Rebel pair, low needs,
	// an armor with an empty plate slot). Not part of the plan: never "not ready", nothing is moved. This file is the decision (from plain
	// facts, so it can be tested); ReminderBars draws the result.
	internal static class Reminders
	{
		internal const string GreenFlare = "6217726288ed9f0845317459"; // RSP-30 reactive signal cartridge (Green)
		internal const string Paracord = "5c12688486f77426843c7d32";
		internal const string RedRebel = "5c0126f40db834002a125382";

		// The groups of items the game sorts food and drinks under
		internal const string FoodGroup = "5448e8d04bdc2ddf718b4569";
		internal const string DrinkGroup = "5448e8d64bdc2dce718b4568";

		// A worn armor piece: the armored vest slot, or a vest that is armor (a plate carrier)
		internal class WornArmor
		{
			public string Name;
			// It has a front or back plate slot, and one of them is empty
			public bool MissingPlate;
		}

		// What the check needs to know about the raid you are about to start
		internal class Facts
		{
			public string Map;
			public HashSet<string> Carried = new HashSet<string>();
			// Current needs in percent of the maximum; negative: unknown (nothing is said about it)
			public float HydrationPercent = -1f;
			public float EnergyPercent = -1f;
			public bool HasFood;
			public bool HasDrink;
			public List<WornArmor> Armors = new List<WornArmor>();
		}

		internal enum Kind
		{
			GreenFlare,
			Paracord,
			Armor,
			Needs,
			Plates
		}

		internal class Reminder
		{
			public Kind Kind;
			public string Text;
			// Item templates shown as pictures before the text (what is missing)
			public List<string> Icons = new List<string>();
		}

		internal static List<Reminder> Check(RemindersSettings settings, Facts facts)
		{
			var found = new List<Reminder>();
			if (settings == null || facts == null)
			{
				return found;
			}

			bool Here(MapItemReminder panel) => panel.Enabled && facts.Map != null && panel.Maps.Contains(facts.Map, StringComparer.OrdinalIgnoreCase);

			if (Here(settings.GreenFlare) && !facts.Carried.Contains(GreenFlare))
			{
				found.Add(new Reminder { Kind = Kind.GreenFlare, Text = "MISSING FOR THIS MAP!", Icons = { GreenFlare } });
			}

			var paracord = settings.Paracord;
			if (Here(paracord))
			{
				bool hasCord = facts.Carried.Contains(Paracord), hasRebel = facts.Carried.Contains(RedRebel);
				var missing = new List<string>();
				if (!hasCord && (!paracord.PairOnly || hasRebel))
				{
					missing.Add(Paracord);
				}

				if (!hasRebel && (!paracord.PairOnly || hasCord))
				{
					missing.Add(RedRebel);
				}

				if (missing.Count > 0)
				{
					found.Add(new Reminder { Kind = Kind.Paracord, Text = "MISSING FOR THIS MAP!", Icons = missing });
				}

				if (paracord.WarnArmor && hasCord && hasRebel && facts.Armors.Count > 0)
				{
					found.Add(new Reminder { Kind = Kind.Armor, Text = $"{string.Join(" AND ", facts.Armors.Select(armor => armor.Name))} {(facts.Armors.Count > 1 ? "ARE" : "IS")} INCOMPATIBLE WITH THE RED REBEL EXTRACT!" });
				}
			}

			var needs = settings.Needs;
			if (needs.Enabled)
			{
				var low = new List<string>();
				if (needs.WarnHydration && facts.HydrationPercent >= 0f && facts.HydrationPercent < needs.HydrationBelow && !(needs.OnlyIfNoSupplies && facts.HasDrink))
				{
					low.Add("HYDRATION");
				}

				if (needs.WarnEnergy && facts.EnergyPercent >= 0f && facts.EnergyPercent < needs.EnergyBelow && !(needs.OnlyIfNoSupplies && facts.HasFood))
				{
					low.Add("ENERGY");
				}

				if (low.Count > 0)
				{
					found.Add(new Reminder { Kind = Kind.Needs, Text = $"{string.Join(" AND ", low)} {(low.Count > 1 ? "ARE" : "IS")} TOO LOW!" });
				}
			}

			if (settings.Equipment.Enabled)
			{
				var bare = facts.Armors.Where(armor => armor.MissingPlate).ToList();
				if (bare.Count > 0)
				{
					found.Add(new Reminder { Kind = Kind.Plates, Text = $"{string.Join(" AND ", bare.Select(armor => armor.Name))} {(bare.Count > 1 ? "HAVE" : "HAS")} MISSING PIECES!" });
				}
			}

			return found;
		}

		// The plate slots that count: front and back (side plates and soft armor are left out). The ids differ in case between items.
		internal static bool IsFrontOrBackPlate(string slotId) =>
			string.Equals(slotId, "Front_plate", StringComparison.OrdinalIgnoreCase) || string.Equals(slotId, "Back_plate", StringComparison.OrdinalIgnoreCase);

		// The armor you wear: whatever is in the armored vest slot, and a vest that has armor in it (a plate carrier). Helmets are not looked at.
		internal static List<WornArmor> WornArmors(Inventory inventory)
		{
			var list = new List<WornArmor>();
			foreach (var slot in new[] { EquipmentSlot.ArmorVest, EquipmentSlot.TacticalVest })
			{
				var item = inventory.Equipment.GetSlot(slot).ContainedItem as CompoundItem;
				if (item == null || (slot == EquipmentSlot.TacticalVest && item.GetItemComponent<ArmorHolderComponent>() == null))
				{
					continue;
				}

				var plates = item.Slots.Where(place => IsFrontOrBackPlate(place.ID)).ToList();
				list.Add(new WornArmor { Name = Game.NameOf(item), MissingPlate = plates.Any(place => place.ContainedItem == null) });
			}

			return list;
		}

		// The facts of the profile about to raid (the one of the map screen)
		internal static Facts FactsOf(Profile profile, string map)
		{
			var facts = new Facts { Map = map };
			var inventory = profile?.Inventory;
			if (inventory == null)
			{
				return facts;
			}

			foreach (var item in Game.CarriedAndSecured(inventory))
			{
				facts.Carried.Add(item.TemplateId.ToString());
				if (item.GetItemComponent<FoodDrinkComponent>() != null)
				{
					string group = item.Template.ParentId?.ToString();
					facts.HasFood |= group == FoodGroup;
					facts.HasDrink |= group == DrinkGroup;
				}
			}

			var health = profile.Health;
			if (health != null)
			{
				facts.HydrationPercent = Percent(health.Hydration);
				facts.EnergyPercent = Percent(health.Energy);
			}

			facts.Armors = WornArmors(inventory);
			return facts;
		}

		private static float Percent(Profile.HealthInfo.ValueInfo value) => value == null || value.Maximum <= 0f ? -1f : value.Current / value.Maximum * 100f;
	}
}
