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

	internal class FlaresTemplate
	{
		public bool Enabled;
		public ItemRef Item = new ItemRef { Template = Game.GreenFlare, Name = "Signal flare (Green)" };
		public int Count = 1;
		// Where flares go, first with room wins: Pockets, TacticalVest, ArmorVest, Backpack
		public List<string> Destinations = new List<string> { "TacticalVest", "Pockets", "Backpack" };
	}

	internal class Settings
	{
		public InjectorCaseTemplate InjectorCase = new InjectorCaseTemplate();
		public SpecialSlotsTemplate SpecialSlots = new SpecialSlotsTemplate();
		public FlaresTemplate Flares = new FlaresTemplate();
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
