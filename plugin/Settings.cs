using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

	// What one injector case should hold, cell for cell: a cell that is not in Cells must be empty (a case saved empty is kept empty)
	internal class SavedCase
	{
		// The very case (item ids stay the same for the same item, so another case of the same type is left alone)
		public string CaseId;
		public string CaseTemplate;
		public string CaseName;
		public string Tag;
		public int TagColor;
		public List<CaseCell> Cells = new List<CaseCell>();
	}

	internal class InjectorCaseTemplate
	{
		public bool Enabled = true;
		// One template per injector case
		public List<SavedCase> Cases = new List<SavedCase>();
		// The single template of the first version: it becomes the first of Cases when the file is read
		public string CaseId;
		public List<CaseCell> Cells = new List<CaseCell>();

		internal void Migrate()
		{
			if (Cases.Count == 0 && !string.IsNullOrEmpty(CaseId) && Cells.Count > 0)
			{
				Cases.Add(new SavedCase { CaseId = CaseId, CaseTemplate = Game.InjectorCase, CaseName = "Injector case", Cells = Cells.ToList() });
			}

			CaseId = null;
			Cells = new List<CaseCell>();
		}
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
		// The places picked before places were all on a list (kept to carry an old profile over, see GroupDef.PlaceOrder)
		public List<string> Destinations = new List<string>();
		// Every place the group can go to, in the order they are tried (the first with room wins), and the ones switched off, which are
		// skipped but keep their spot. Null in a profile from before: worked out from Destinations.
		public List<string> Places;
		public List<string> PlacesOff = new List<string>();
		// The group's items by template, best first (empty = the default order), and the ones switched off
		public List<string> Order = new List<string>();
		public List<string> Off = new List<string>();
		// The quick-slot key (4 to 9, 0 or G): null is the group's default, "" none
		public string Hotkey;
		// The most of one item to carry for the group, by template (missing or 0: no limit)
		public Dictionary<string, int> MaxPer = new Dictionary<string, int>();
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

		// A case that was kept out for all three currencies used to stop all cash while you carried it: that is now its own switch
		internal void Migrate()
		{
			foreach (var id in CashExclusions.Select(entry => entry.CaseId).Distinct().ToList())
			{
				var own = CashExclusions.Where(entry => entry.CaseId == id).ToList();
				if (own.Any(entry => entry.Currency == CashExclusion.All) || !new[] { "RUB", "EUR", "USD" }.All(currency => own.Any(entry => entry.Currency == currency)))
				{
					continue;
				}

				var first = own[0];
				CashExclusions.RemoveAll(entry => entry.CaseId == id);
				CashExclusions.Add(new CashExclusion { CaseId = id, CaseTemplate = first.CaseTemplate, CaseName = first.CaseName, Tag = first.Tag, TagColor = first.TagColor, Currency = CashExclusion.All });
			}
		}
	}

	// The Ammo step (the rules for loading ammo are fixed and shared with the T key, see AmmoRules)
	// A weapon model with its own number of spare ammo stacks
	internal class ModelStacks
	{
		public string Template;
		public string Name;
		public int Stacks;
	}

	internal class AmmoSettings
	{
		public bool Enabled;
		// A weapon with an empty chamber gets a round of the ammo in its magazine
		public bool Chamber = true;
		// The loose stacks of ammo you carry are filled up (from the stash) to a full stack
		public bool TopUpLoose = true;
		// A loose stack that is short and cannot be filled (the stash has no more of it) is a problem, not just left as it is
		public bool LooseShortIsProblem;
		// Who decides, first match wins, for an empty spare magazine: loose ammo in the same container, the nearest magazine, an
		// equipped weapon that takes it
		public List<string> Pointers = new List<string> { "nearest", "others", "loose", "weapon" };
		// Spare ammo (how many stacks and where to is the supplies group "spareammo"): when several kinds of ammo are in use for a
		// caliber, the one brought is the worst, the best (penetration, then damage) or a random one of them
		public string SparePick = "best";
		// Weapon models that need no spare ammo (a sidearm that is never reloaded)
		public List<ItemRef> SpareIgnored = new List<ItemRef>();
		// Weapon models that ask for another number of stacks than the generic one (by model: two of the same ask once)
		public List<ModelStacks> SpareStacks = new List<ModelStacks>();
	}

	// An armor piece excluded from repairs (left damaged on purpose): that very item (its id), with its name kept so it can be shown
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
		public string Currency; // RUB, EUR or USD, or All: while you carry this case no cash at all is brought
		internal const string All = "ALL";
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
		// The size of the window and the check box, 1 = as designed (Setup > Options)
		public float UiScale = 1f;
		public HealMeSettings HealMe = new HealMeSettings();
		public FullAutoSettings FullAuto = new FullAutoSettings();
		public RepairSettings Repair = new RepairSettings();
		public AmmoSettings Ammo = new AmmoSettings();
		public SuppliesSettings Supplies = new SuppliesSettings();
		public InjectorCaseTemplate InjectorCase = new InjectorCaseTemplate();
		public SpecialSlotsTemplate SpecialSlots = new SpecialSlotsTemplate();
	}

	// One settings file per profile: BepInEx\config\GetReady\profiles\<profile id>.json. The profile is known once the game is logged in
	// (not when the plugin starts), so the file is picked on first use and again when another profile is logged in. Before that (no
	// session) the defaults are used and nothing is saved. The old shared settings.json is no longer read; it was copied to the main
	// profile's file by hand.
	internal static class SettingsStore
	{
		// (the folder, who is logged in and the clock can be replaced, so the tests can play logging in and out with files of their own)
		internal static string FolderOverride;
		internal static Func<int> Clock = () => Environment.TickCount;
		private static string Folder => FolderOverride ?? Path.Combine(BepInEx.Paths.ConfigPath, "GetReady", "profiles");
		private static Settings _current = new Settings();
		private static bool _fixedForTests;
		private static string _profileId;
		private static int _checkedAt = int.MinValue;

		private static string FilePath => Path.Combine(Folder, _profileId + ".json");

		internal static Settings Current
		{
			get
			{
				Refresh();
				return _current;
			}
		}

		// For the tests: these settings stay, nobody is looked up and nothing is read or written
		internal static void UseForTests(Settings settings)
		{
			_current = settings;
			_fixedForTests = true;
		}

		internal static Func<string> LoggedInProfile = () =>
		{
			try
			{
				return SPT.Reflection.Utils.ClientAppUtils.GetMainApp()?.GetClientBackEndSession()?.Profile?.Id;
			}
			catch
			{
				return null;
			}
		};

		// For the tests: back to the start, nobody logged in, and the files looked up for real (in the folder given)
		internal static void StartOver(string folder, Func<string> loggedIn)
		{
			_fixedForTests = false;
			_profileId = null;
			_checkedAt = int.MinValue;
			_current = new Settings();
			FolderOverride = folder;
			LoggedInProfile = loggedIn;
		}

		// Looks at who is logged in at most twice a second; when it is another profile than the one loaded, loads that one's file
		private static void Refresh()
		{
			if (_fixedForTests)
			{
				return;
			}

			int now = Clock();
			if (_checkedAt != int.MinValue && now - _checkedAt < 500 && now >= _checkedAt)
			{
				return;
			}

			_checkedAt = now;
			var id = LoggedInProfile();
			if (id == null || id == _profileId)
			{
				return;
			}

			_profileId = id;
			_current = new Settings();
			Load();
			Plugin.Log.LogInfo($"GetReady settings: profile {id} ({(File.Exists(FilePath) ? "loaded" : "no file yet, defaults")})");
		}

		// The settings of a file's text
		internal static Settings Parse(string json)
		{
			// Replace, not the default Auto: a list that starts with default entries (the ammo pointers, the full auto slots) would get
			// the saved list appended to its defaults, so the owner's order and removals came back as the defaults on every start
			var settings = JsonConvert.DeserializeObject<Settings>(json, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }) ?? new Settings();
			// Files written while that was happening hold the lists twice over
			settings.Ammo.Pointers = settings.Ammo.Pointers.Distinct().ToList();
			settings.FullAuto.Slots = settings.FullAuto.Slots.Distinct().ToList();
			settings.InjectorCase.Migrate();
			settings.Supplies.Migrate();
			return settings;
		}

		private static void Load()
		{
			try
			{
				if (File.Exists(FilePath))
				{
					_current = Parse(File.ReadAllText(FilePath));
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Could not read {FilePath}, using defaults: {e}");
			}
		}

		internal static void Save()
		{
			if (_profileId == null || _fixedForTests)
			{
				return;
			}

			try
			{
				Directory.CreateDirectory(Folder);
				File.WriteAllText(FilePath, JsonConvert.SerializeObject(_current, Formatting.Indented));
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Could not write {FilePath}: {e}");
			}
		}
	}
}
