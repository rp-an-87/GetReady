using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
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
	}

	internal class SuppliesSettings
	{
		public bool HealingEnabled = true;
		public bool GrenadesEnabled = true;
		// Replace carried items by better ones from the stash (more resource, higher priority, non-FiR)
		public bool SwapWorse = true;
		// When the missing amount is smaller than the next item (1 point missing, an Army bandage next), take it anyway
		public bool AllowOvershoot = true;
		public Dictionary<string, GroupSetting> Groups = new Dictionary<string, GroupSetting>();
	}

	internal class WarningSettings
	{
		public bool Splint = true;
		public bool Surgery = true;
	}

	internal class Settings
	{
		public WarningSettings Warnings = new WarningSettings();
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
