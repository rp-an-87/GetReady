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
		// Replace carried items by better ones from the stash (more resource, higher priority, non-FiR)
		public bool SwapWorse = true;
		// When the missing amount is smaller than the next item (1 point missing, an Army bandage next), take it anyway
		public bool AllowOvershoot = true;
		// Non-FiR copies of an item go before FiR ones, even when the non-FiR one is in a container and the FiR one lies loose. Between
		// equals the loose one always goes first. Off: loose before in a container, whatever FiR is.
		public bool NonFiRBeforeLoose = true;
		public Dictionary<string, GroupSetting> Groups = new Dictionary<string, GroupSetting>();
	}

	// The rules for loading ammo into magazines, shared by the T key and the Get ready ammo step
	internal class AmmoSettings
	{
		// The share of a magazine (of its capacity, rounded up) that is the highest-penetration ammo, loaded last so it is fired first (0: off)
		public int BestOnTopPercent;
	}

	// An armor piece left damaged on purpose: that very item (its id), with its name kept so it can be shown
	internal class KeptItem
	{
		public string Id;
		public string Template;
		public string Name;
	}

	internal class RepairSettings
	{
		public bool Enabled;
		// Also repair the weapons you carry in the weapon slots (with weapon repair kits)
		public bool Weapons = true;
		// A piece with no usable kit is repaired by a trader instead (for roubles), the first of TraderOrder that can repair it. A kit that
		// is there but too small is an error, not a reason to go to a trader.
		public bool UseTraders = true;
		public List<string> TraderOrder = new List<string>();
		public List<KeptItem> KeepDamaged = new List<KeptItem>();
	}

	internal class Settings
	{
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
