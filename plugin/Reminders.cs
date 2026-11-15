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
			public string Template;
			// Worn in the Body Armor slot (the Red Rebel extract does not allow it); what is in the Tactical Rig slot is allowed
			public bool BodyArmor;
			// The front or back plate slots that are empty ("FRONT", "BACK")
			public List<string> MissingPlates = new List<string>();
			public bool MissingPlate => MissingPlates.Count > 0;
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
			// The inventory was read (an unknown inventory says nothing about armor)
			public bool ArmorKnown;
			// What is left of the insurance (see InsuranceText), when it is a reminder; null: nothing to say
			public string InsuranceText;
			// The cases set up in Cases and the ids of what you carry, for the containers that are wrong or missing for the map
			public List<CaseEntry> Cases = new List<CaseEntry>();
			public HashSet<string> CarriedIds = new HashSet<string>();
		}

		internal enum Kind
		{
			WrongContainers,
			MissingContainers,
			GreenFlare,
			Paracord,
			Armor,
			Needs,
			Plates,
			NoArmor,
			Insurance
		}

		internal class Reminder
		{
			public Kind Kind;
			public string Text;
			// Item templates shown as pictures before the text (what is missing)
			public List<string> Icons = new List<string>();
			// The containers it is about (shown with their tags), and whether the text comes before them
			public List<CaseEntry> Cases = new List<CaseEntry>();
			public bool TextFirst;
		}

		internal static List<Reminder> Check(RemindersSettings settings, Facts facts)
		{
			var found = new List<Reminder>();
			if (settings == null || facts == null)
			{
				return found;
			}

			// Only on a map that has such an extract, whatever the list holds
			bool Here(MapItemReminder panel, string[] extractMaps) => panel.Enabled && facts.Map != null && extractMaps.Contains(facts.Map, StringComparer.OrdinalIgnoreCase)
				&& panel.Maps.Contains(facts.Map, StringComparer.OrdinalIgnoreCase);

			// The containers: the cases you carry that are for other maps, and the ones for this map that are not on you
			if (settings.WrongContainers || settings.MissingContainers)
			{
				var report = MapTags.Check(facts.Cases, facts.CarriedIds, facts.Map, true).Only(settings.WrongContainers, settings.MissingContainers);
				if (report.Wrong.Count > 0)
				{
					found.Add(new Reminder { Kind = Kind.WrongContainers, Text = "WRONG CONTAINERS FOR THIS MAP!", Cases = report.Wrong });
				}

				if (report.Missing.Count > 0)
				{
					found.Add(new Reminder { Kind = Kind.MissingContainers, Text = "MISSING CONTAINERS FOR THIS MAP:", Cases = report.Missing, TextFirst = true });
				}
			}

			if (Here(settings.GreenFlare, MapTags.FlareMaps) && !facts.Carried.Contains(GreenFlare))
			{
				found.Add(new Reminder { Kind = Kind.GreenFlare, Text = "MISSING FOR THIS MAP!", Icons = { GreenFlare } });
			}

			var paracord = settings.Paracord;
			if (Here(paracord, MapTags.AlpinistMaps))
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

				var body = facts.Armors.Where(armor => armor.BodyArmor).ToList();
				if (paracord.WarnArmor && hasCord && hasRebel && body.Count > 0)
				{
					var reminder = new Reminder { Kind = Kind.Armor, Text = $"{string.Join(" AND ", body.Select(armor => armor.Name))} {(body.Count > 1 ? "ARE" : "IS")} INCOMPATIBLE WITH THE RED REBEL EXTRACT!" };
					reminder.Icons.AddRange(body.Where(armor => armor.Template != null).Select(armor => armor.Template));
					found.Add(reminder);
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

			if (facts.InsuranceText != null)
			{
				found.Add(new Reminder { Kind = Kind.Insurance, Text = facts.InsuranceText });
			}

			if (settings.Equipment.NoArmor && facts.ArmorKnown && facts.Armors.Count == 0)
			{
				found.Add(new Reminder { Kind = Kind.NoArmor, Text = "NO ARMOR EQUIPPED!" });
			}

			if (settings.Equipment.Enabled)
			{
				var bare = facts.Armors.Where(armor => armor.MissingPlate).ToList();
				if (bare.Count > 0)
				{
					// The armor's picture, and what is missing from which: "AVS IS MISSING THE FRONT AND BACK PLATE!"
					var reminder = new Reminder
					{
						Kind = Kind.Plates,
						Text = string.Join("; ", bare.Select(armor => $"{armor.Name} IS MISSING THE {string.Join(" AND ", armor.MissingPlates)} PLATE")) + "!"
					};
					reminder.Icons.AddRange(bare.Where(armor => armor.Template != null).Select(armor => armor.Template));
					found.Add(reminder);
				}
			}

			return found;
		}

		// What the insurance step has left, in words for a reminder: items still to insure (insure them on the next screen), or why it cannot
		// be done; null when there is nothing to say (all insured, or nothing to insure)
		internal static string InsuranceText(IEnumerable<PlanLine> lines)
		{
			var list = lines?.ToList() ?? new List<PlanLine>();
			var problem = list.FirstOrDefault(line => line.Problem);
			if (problem != null)
			{
				return "INSURANCE: " + (problem.Detail ?? problem.Text ?? "").TrimEnd('.').ToUpperInvariant() + "!";
			}

			int open = list.Where(line => line.Run != null).Sum(line => line.Insured);
			return open > 0 ? $"{open} {(open == 1 ? "ITEM" : "ITEMS")} NOT INSURED! DO IT ON THE NEXT SCREEN." : null;
		}

		// The plate slots that count: front and back (side plates and soft armor are left out). The ids differ in case between items.
		internal static bool IsFrontOrBackPlate(string slotId) =>
			string.Equals(slotId, "Front_plate", StringComparison.OrdinalIgnoreCase) || string.Equals(slotId, "Back_plate", StringComparison.OrdinalIgnoreCase);

		// The armor you wear: whatever is in the armored vest slot, and a rig with front or back plate slots (a plate carrier). Helmets are not looked at.
		internal static List<WornArmor> WornArmors(Inventory inventory)
		{
			var list = new List<WornArmor>();
			foreach (var slot in new[] { EquipmentSlot.ArmorVest, EquipmentSlot.TacticalVest })
			{
				var item = inventory.Equipment.GetSlot(slot).ContainedItem as CompoundItem;
			if (item == null)
				{
					continue;
				}

				// A rig is armor only when it takes front and back plates; a rig without plate slots (armor built in or not) is just a rig
				var plates = item.Slots.Where(place => IsFrontOrBackPlate(place.ID)).ToList();
				if (slot == EquipmentSlot.TacticalVest && plates.Count == 0)
				{
					continue;
				}

				var armor = new WornArmor { Name = Game.NameOf(item), Template = item.TemplateId.ToString(), BodyArmor = slot == EquipmentSlot.ArmorVest };
				armor.MissingPlates.AddRange(plates.Where(place => place.ContainedItem == null).Select(place => place.ID.StartsWith("front", StringComparison.OrdinalIgnoreCase) ? "FRONT" : "BACK"));
				list.Add(armor);
			}

			return list;
		}

		// Everything on you: what you wear and hold in every equipment slot (weapons, the scabbard's melee weapon, pockets, the secured
		// container) with what is inside it. The Red Rebel is a weapon in the scabbard, so the carried containers alone are not enough.
		internal static IEnumerable<Item> OnYou(Inventory inventory) => inventory.Equipment.GetAllItems().Where(item => item != inventory.Equipment);

		// The facts of the profile about to raid (the one of the map screen)
		internal static Facts FactsOf(Profile profile, string map, IEnumerable<CaseEntry> cases)
		{
			var facts = new Facts { Map = map, Cases = cases?.ToList() ?? new List<CaseEntry>() };
			var inventory = profile?.Inventory;
			if (inventory == null)
			{
				return facts;
			}

			foreach (var item in OnYou(inventory))
			{
				facts.Carried.Add(item.TemplateId.ToString());
				facts.CarriedIds.Add(item.Id.ToString());
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
			facts.ArmorKnown = true;
			return facts;
		}

		private static float Percent(Profile.HealthInfo.ValueInfo value) => value == null || value.Maximum <= 0f ? -1f : value.Current / value.Maximum * 100f;
	}
}
