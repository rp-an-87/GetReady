using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace GetReady
{
	// One item of a template, kept with its name so it can be shown without the item being around
	internal class ItemRef
	{
		public string Template;
		public string Name;
	}

	// One cell of the injector case (the case is 3x3 and injectors are 1x1)
	internal class CaseCell : ItemRef
	{
		public int X;
		public int Y;
	}

	// One special slot of the pockets: the first of Templates that the stash has goes in (a second one is a fallback)
	internal class SlotEntry
	{
		public string SlotId;
		// How many of it the slot held when saved (a flare stack, say)
		public int Count = 1;
		public List<ItemRef> Templates = new List<ItemRef>();
	}

	internal class InjectorCaseTemplate
	{
		public bool Enabled = true;
		// The case the cells were captured from: item ids stay the same for the same item, so a second case of the same type is left alone
		public string CaseId;
		public List<CaseCell> Cells = new List<CaseCell>();
	}

	internal class SpecialSlotsTemplate
	{
		public bool Enabled = true;
		public List<SlotEntry> Slots = new List<SlotEntry>();
	}

	// One group of supplies (healing kits, bandages, one grenade type...): how much to carry and where it goes
	internal class GroupSetting
	{
		public int Target;
		// Pockets, Vest (tactical vest or armored rig) and Backpack; the first with room wins
		public List<string> Destinations = new List<string>();
		// The group's items by template, best first (empty = the default order), and the ones switched off
		public List<string> Order = new List<string>();
		public List<string> Off = new List<string>();
		// The quick-slot key (4 to 9, 0 or G): null is the group's default, "" none
		public string Hotkey;
		// The most of one item to carry for the group, by template (missing or 0: no limit)
		public Dictionary<string, int> MaxPer = new Dictionary<string, int>();

		// Adds the place to the end of the list, or takes it out (the last one stays)
		public void ToggleDestination(string place)
		{
			if (!Destinations.Remove(place) || Destinations.Count == 0)
			{
				Destinations.Add(place);
			}
		}
	}

	internal class SuppliesSettings
	{
		public bool HealingEnabled = true;
		public bool GrenadesEnabled = true;
		public bool CashEnabled = true;
		public List<CashExclusion> CashExclusions = new List<CashExclusion>();
		// Replace carried items by better ones from the stash (more resource, higher priority, non-FiR)
		public bool SwapWorse = true;
		// When the missing amount is smaller than the next item (1 point missing, an Army bandage next), take it anyway
		public bool AllowOvershoot = true;
		// Non-FiR copies of an item go before FiR ones, even when the non-FiR one is in a container and the FiR one lies loose. Between
		// equals the loose one always goes first. Off: loose before in a container, whatever FiR is.
		public bool NonFiRBeforeLoose = true;
		public Dictionary<string, GroupSetting> Groups = new Dictionary<string, GroupSetting>();
	}

	// The Ammo step (the rules for loading ammo are fixed and shared with the T key, see AmmoRules)
	internal class AmmoSettings
	{
		public bool Enabled;
		// A weapon with an empty chamber gets a round of the ammo in its magazine
		public bool Chamber = true;
		// The loose stacks of ammo you carry are filled up (from the stash) to a full stack
		public bool TopUpLoose = true;
		// Who decides, first match wins, for an empty spare magazine: loose ammo in the same container, the nearest magazine, an
		// equipped weapon that takes it
		public List<string> Pointers = new List<string> { "loose", "nearest", "weapon" };
	}

	// An armor piece left damaged on purpose: that very item (its id), with its name kept so it can be shown
	internal class KeptItem
	{
		public string Id;
		public string Template;
		public string Name;
	}

	// A currency kept out of one very case or wallet you carry (that case, by item id, not its type)
	internal class CashExclusion
	{
		public string CaseId;
		public string CaseTemplate;
		public string CaseName;
		public string Tag;      // the tag on the case when it was saved, and its colour (for the list)
		public int TagColor;
		public string Currency; // RUB, EUR or USD
	}

	internal class RepairSettings
	{
		public bool Enabled;
		// Repair the armor you wear (vest, helmet, armored rig, masks with armor)
		public bool Armor = true;
		// Repair the weapons you carry in the weapon slots (with weapon repair kits)
		public bool Weapons = true;
		// A piece with no usable kit is repaired by a trader instead (for roubles), the first of TraderOrder that can repair it. A kit that
		// is there but too small is an error, not a reason to go to a trader.
		public bool UseTraders = true;
		public List<string> TraderOrder = new List<string>();
		public List<KeptItem> KeepDamaged = new List<KeptItem>();
	}

	// The Heal me step: the medicine allowed and its order are kept with the supplies' groups (group "healme")
	internal class HealMeSettings
	{
		public bool Enabled;
	}

	// The Full auto step: the weapon slots (EquipmentSlot names) whose weapon is put on full auto
	internal class FullAutoSettings
	{
		public bool Enabled;
		public List<string> Slots = new List<string> { "FirstPrimaryWeapon", "SecondPrimaryWeapon" };
	}

	internal class Settings
	{
		public HealMeSettings HealMe = new HealMeSettings();
		public FullAutoSettings FullAuto = new FullAutoSettings();
		public RepairSettings Repair = new RepairSettings();
		public AmmoSettings Ammo = new AmmoSettings();
		public SuppliesSettings Supplies = new SuppliesSettings();
		public InjectorCaseTemplate InjectorCase = new InjectorCaseTemplate();
		public SpecialSlotsTemplate SpecialSlots = new SpecialSlotsTemplate();
	}

	internal static class SettingsStore
	{
		private static readonly string FilePath = Path.Combine(BepInEx.Paths.ConfigPath, "GetReady", "settings.json");

		internal static Settings Current { get; private set; } = new Settings();

		internal static void Load()
		{
			try
			{
				if (File.Exists(FilePath))
				{
					Current = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Could not read {FilePath}, using defaults: {e}");
			}
		}

		internal static void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllText(FilePath, JsonConvert.SerializeObject(Current, Formatting.Indented));
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Could not write {FilePath}: {e}");
			}
		}
	}
}
