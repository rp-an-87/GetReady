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

	// One special slot of the pockets: the first of Templates that the stash has goes in (a second one is a fallback)
	internal class SlotEntry
	{
		public string SlotId;
		// How many of it the slot held when saved (a flare stack, say)
		public int Count = 1;
		public List<ItemRef> Templates = new List<ItemRef>();
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
		// The places a group starts with (see GroupDef.PlaceOrder)
		public List<string> Destinations = new List<string>();
		// Every place the group can go to, in the order they are tried (the first with room wins), and the ones switched off, which are
		// skipped but keep their spot. Null until the group is first used: worked out from Destinations.
		public List<string> Places;
		public List<string> PlacesOff = new List<string>();
		// The group's items by template, best first (empty = the default order), and the ones switched off
		public List<string> Order = new List<string>();
		public List<string> Off = new List<string>();
		// The quick-slot key (4 to 9, 0 or G): null is the group's default, "" none
		public string Hotkey;
		// A second key, for the next best item of the group (two AI-2 kits: 4 and 6): null or "" none
		public string Hotkey2;
		// The most of one item to carry for the group, by template (missing or 0: no limit)
		public Dictionary<string, int> MaxPer = new Dictionary<string, int>();
		// Replace what is carried by better items from the stash (a fuller copy, a higher-ranked item, non-FiR); off leaves it as it is
		public bool ReplaceWorse = true;
		// When the missing amount is smaller than the next item (1 point missing, an Army bandage next), take it anyway
		public bool AllowOvershoot = true;
	}

	internal class SuppliesSettings
	{
		public bool HealingEnabled = true;
		public bool GrenadesEnabled = true;
		public bool CashEnabled = true;
		// Non-FiR copies of an item go before FiR ones, even when the non-FiR one is in a container and the FiR one lies loose. Between
		// equals the loose one always goes first. Off: loose before in a container, whatever FiR is.
		public bool NonFiRBeforeLoose = true;
		// A carried FiR item is swapped for a non-FiR copy of the same kind when the stash one is at least as full (grenades, cartridges,
		// kits at a tie). Off: FiR is ignored in swaps, a swap needs strictly more resource or a higher-ranked item.
		public bool SwapFiRForNonFiR = true;
		public Dictionary<string, GroupSetting> Groups = new Dictionary<string, GroupSetting>();
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
		// Kinds of ammo (by template) whose loose stacks are not filled up. They still count as spare ammo.
		public List<ItemRef> TopUpExcluded = new List<ItemRef>();
		// A loose stack that is short and cannot be filled (the stash has no more of it) is a problem, not just left as it is
		public bool LooseShortIsProblem;
		// Who decides, first match wins, for an empty spare magazine: loose ammo in the same container, the nearest magazine, an
		// equipped weapon that takes it
		public List<string> Pointers = new List<string> { "nearest", "others", "loose", "weapon", "anywhere" };
		// What the loose-ammo rules do when the loose ammo is of several kinds: conflict (nothing is guessed, the next rule is asked), or the
		// worst, the best (penetration, then damage) or a random one of them
		public string PointerConflict = "conflict";
		// Spare ammo (how many stacks and where to is the supplies group "spareammo"): when several kinds of ammo are in use for a
		// caliber, the one brought is the worst, the best (penetration, then damage) or a random one of them
		public string SparePick = "best";
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

	// The Insurance step: what you wear is insured at this trader (its id; empty until you pick one in Setup)
	internal class InsuranceSettings
	{
		public bool Enabled;
		public string TraderId = "";
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

	// One item of a case as it was recorded: what it is (by type), how many (a stack), and where it sat (grid, cell, rotation). The place is
	// only used by a case set to exact positions.
	internal class CaseItem : ItemRef
	{
		public int Count = 1;
		public int Grid;
		public int X;
		public int Y;
		public int Rotation;
	}

	// One container you carry (that very one, by item id), as you set it up in Setup > Cases. Recorded: the keys, cards and money it holds, so
	// they are brought back and anything else is taken out. The rest is about the case itself: where it is for and what it does to the cash.
	internal class CaseEntry
	{
		public string CaseId;
		public string CaseTemplate;
		public string CaseName;
		public string Tag;
		public int TagColor;
		// The contents are recorded (a case with only maps or the cash switch set has none)
		public bool Recorded;
		// What it holds, apart from money, item by item
		public List<CaseItem> Items = new List<CaseItem>();
		// Each item goes back where it was recorded (grid, cell, rotation); off: only the type and the count matter
		public bool Exact;
		// What is not in the template is taken out to the stash, money too (a recorded amount keeps that much, the extra goes out); off: only what is missing is brought and nothing is taken out
		public bool TakeOutRest = true;
		// While you carry this case the Cash step does nothing
		public bool StopsCash;
		// The maps it is for (names of MapTags.Maps); none: any
		public List<string> Maps = new List<string>();
	}

	internal class CasesSettings
	{
		public bool Enabled = true;
		// A change you make by hand in the stash updates the recorded contents of a case that was recorded
		public bool AutoUpdate = true;
		// The map screen warns about a case you carry that is for other maps
		public bool MapWarnings = true;
		public List<CaseEntry> Cases = new List<CaseEntry>();
		// The cases whose card is collapsed in Setup (by item id): it stays as you left it, also when the case is taken off and put on again
		public List<string> Collapsed = new List<string>();
	}

	internal class Settings
	{
		// The size of the window and the check box, 1 = as designed (Setup > Options)
		public float UiScale = 1f;
		public HealMeSettings HealMe = new HealMeSettings();
		public FullAutoSettings FullAuto = new FullAutoSettings();
		public RepairSettings Repair = new RepairSettings();
		public InsuranceSettings Insurance = new InsuranceSettings();
		public AmmoSettings Ammo = new AmmoSettings();
		public SuppliesSettings Supplies = new SuppliesSettings();
		public SpecialSlotsTemplate SpecialSlots = new SpecialSlotsTemplate();
		public CasesSettings Cases = new CasesSettings();
	}

	// One settings file per profile: BepInEx\config\GetReady\profiles\<profile id>.json. The profile is known once the game is logged in
	// (not when the plugin starts), so the file is picked on first use and again when another profile is logged in. Before that (no
	// session) the defaults are used and nothing is saved. 
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

			FlushIfDue(true);
			_profileId = id;
			_current = new Settings();
			Load();
			Plugin.Trace($"GetReady settings: profile {id} ({(File.Exists(FilePath) ? "loaded" : "no file yet, defaults")})");
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

		// Saves a little after the last change, not at every click (a row of clicks is one write); FlushIfDue writes it when the time has come
		private static DateTime _saveDue = DateTime.MaxValue;

		internal static void SaveSoon() => _saveDue = DateTime.UtcNow.AddMilliseconds(600);

		// force: now, whatever the time (the window closes, the game quits, another profile is loaded)
		internal static void FlushIfDue(bool force = false)
		{
			if (_saveDue != DateTime.MaxValue && (force || DateTime.UtcNow >= _saveDue))
			{
				Save();
			}
		}

		internal static void Save()
		{
			_saveDue = DateTime.MaxValue;
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
