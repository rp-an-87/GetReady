using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;

namespace GetReady
{
	// One kind of item in a group and how much it counts for: its health resource, a fixed number of points, one each, or the
	// items in its stack
	internal class Entry
	{
		public string Template;
		public Func<Item, float> Weight;
		public string Name => Game.TemplateName(Template);

		internal static Entry Count(string template) => new Entry { Template = template, Weight = item => 1f };
		internal static Entry Points(string template, float points) => new Entry { Template = template, Weight = item => points };
		internal static Entry Resource(string template) => new Entry { Template = template, Weight = Game.ResourceOf };
		internal static Entry Stack(string template) => new Entry { Template = template, Weight = item => item.StackObjectsCount };

		// Counts for its health resource, or one when it has none (a CAT, a plain bandage)
		internal static Entry Auto(string template) => new Entry { Template = template, Weight = item => Game.ResourceOf(item) > 0f ? Game.ResourceOf(item) : 1f };
	}

	// Something the owner can ask to carry: a target amount, the items that count towards it (best first), and where it goes.
	// The owner's choices live in its GroupSetting; the rest is fixed here.
	internal class GroupDef
	{
		// What it is and how it is shown
		public string Id;
		public string Name;
		public string Unit;
		public int DefaultTarget;
		public int Max = 10;
		public int Step = 1;
		public bool Money; // shown with thousands separators and as "Bring 25,000 RUB"
		public bool Bindable = true; // can go on a quick-slot key
		public string DefaultHotkey = "";

		// Where it goes
		public string[] DefaultDestinations = { "Pockets", "Vest" };
		public string[] Locations; // the places that can be picked (default: the usual ones)
		public bool AllowPouch = true; // the secured container and the special slots can hold it (not grenades)
		public bool NoFallback; // only the places picked, never the pockets, vest or backpack after them

		// How its items behave
		public bool Random; // the entries are equal: whichever the stash has, picked at random
		public bool Impact; // left out of the grenade quota's random picks
		public bool NotGrenade; // a group of the grenades step that is not a grenade (signal cartridges): the quota ignores it
		public bool IsQuota; // total grenades: topped up with random ones after the groups
		public bool Stack; // the items are stacks of which only the missing count is brought (money)
		public string[] DefaultOff = new string[0]; // items that start switched off

		// Where in the item tree to look, and which of those items belong: when set, the items are found in the game's item
		// database (modded ones too) and the ones listed in Entries come first, in that order
		public string[] Under;
		public Func<Item, bool> Match;

		private Entry[] _entries = new Entry[0];
		private bool _discovered;

		public Entry[] Entries
		{
			get
			{
				Discover();
				return _entries;
			}
			set => _entries = value;
		}

		private void Discover()
		{
			if (_discovered || Match == null)
			{
				return;
			}

			var factory = Comfort.Common.Singleton<ItemFactory>.Instance;
			if (factory == null)
			{
				return; // not loaded yet: the listed items stand in
			}

			_discovered = true;
			var timer = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				var found = new List<string>();
				foreach (var template in factory.ItemTemplates.Values)
				{
					// The branches themselves ("Medikit", "Medical item") and items nobody can get are not items to carry
					if (!Under.Any(parent => Icons.IsUnder(template, parent)) || Under.Contains(template._id.ToString()) || Game.NotAvailable.Contains(template._id.ToString()))
					{
						continue;
					}

					var item = Icons.ItemFor(template._id.ToString());
					if (item != null && Match(item))
					{
						found.Add(template._id.ToString());
					}
				}

				if (found.Count == 0)
				{
					return;
				}

				var preferred = _entries.Select(entry => entry.Template).ToList();
				var ordered = preferred.Where(found.Contains).Concat(found.Where(id => !preferred.Contains(id)).OrderBy(id => (id + " ShortName").Localized()));
				_entries = ordered.Select(Entry.Auto).ToArray();
				Plugin.Log.LogInfo($"{Id}: {_entries.Length} items found in {timer.ElapsedMilliseconds} ms");

				// Two items with the same short name ("Bandage") are told apart by their full names
				foreach (var group in _entries.GroupBy(entry => (entry.Template + " ShortName").Localized()).Where(group => group.Count() > 1))
				{
					foreach (var entry in group)
					{
						Game.LongNames.Add(entry.Template);
					}
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not list the items of {Id}: {e.Message}");
			}
		}

		// ---- The owner's choices

		public GroupSetting Setting
		{
			get
			{
				var groups = SettingsStore.Current.Supplies.Groups;
				GroupSetting setting;
				if (!groups.TryGetValue(Id, out setting))
				{
					setting = new GroupSetting { Target = DefaultTarget, Destinations = DefaultDestinations.ToList(), Off = DefaultOff.ToList() };
					groups[Id] = setting;
					StartPlaces(setting, false);
				}

				return setting;
			}
		}

		// The key it goes on: the owner's choice, else the default
		public string HotkeyChoice => Setting.Hotkey ?? DefaultHotkey;

		// The places that can be clicked on in the window: Special slots, Pockets, Vest, Backpack, Pouch, or the cases for money
		public IEnumerable<string> PickablePlaces =>
			(Locations ?? new[] { "Special slots", "Pockets", "Vest", "Backpack", "Pouch" })
				.Where(place => AllowPouch || (place != "Pouch" && place != "Special slots"));

		// Where the group can go: every place in the order the owner set (the first with room wins), the ones switched off skipped.
		// A new group starts with the defaults first, then every other place, the pouch last, all on; a group with no fallback (cash)
		// starts with the defaults only. A profile from before had only the places picked: those first and the pockets, vest and
		// backpack on after them (as they were tried), the rest off.
		public List<string> PlaceOrder()
		{
			var setting = Setting;
			if (setting.Places == null)
			{
				StartPlaces(setting, true);
			}

			var pickable = PickablePlaces.ToList();
			var order = setting.Places.Where(place => pickable.Contains(place)).Distinct().ToList();
			order.AddRange(pickable.Where(place => !order.Contains(place))); // a place added later
			return order.Where(place => !setting.PlacesOff.Contains(place)).Concat(order.Where(place => setting.PlacesOff.Contains(place))).ToList(); // the ones off last
		}

		private void StartPlaces(GroupSetting setting, bool carriedOver)
		{
			var pickable = PickablePlaces.ToList();
			var picks = setting.Destinations.Where(place => pickable.Contains(place)).Distinct().ToList();
			var order = picks.ToList();
			order.AddRange(pickable.Where(place => !order.Contains(place) && place != "Pouch"));
			if (pickable.Contains("Pouch") && !order.Contains("Pouch"))
			{
				order.Add("Pouch");
			}

			setting.Places = order;
			var usual = new[] { "Pockets", "Vest", "Backpack" };
			setting.PlacesOff = order.Where(place => !picks.Contains(place) && (NoFallback || (carriedOver && !usual.Contains(place)))).ToList();
		}

		public bool PlaceOn(string place) => !Setting.PlacesOff.Contains(place);

		// Switches a place off (it goes to the end of the row) or on (it goes after the ones that are on)
		public void TogglePlace(string place)
		{
			var setting = Setting;
			var order = PlaceOrder();
			if (setting.PlacesOff.Remove(place))
			{
				order.Remove(place);
				order.Insert(order.Count(other => !setting.PlacesOff.Contains(other)), place);
			}
			else
			{
				setting.PlacesOff.Add(place);
				order.Remove(place);
				order.Add(place);
			}

			setting.Places = order;
		}

		// Two places swap spots, and with them on or off (an off place dropped on an on one takes its place in the chain)
		public void SwapPlaces(string place, string other)
		{
			var setting = Setting;
			var order = PlaceOrder();
			int a = order.IndexOf(place);
			int b = order.IndexOf(other);
			if (a < 0 || b < 0 || a == b)
			{
				return;
			}

			bool placeOff = setting.PlacesOff.Contains(place);
			bool otherOff = setting.PlacesOff.Contains(other);
			order[a] = other;
			order[b] = place;
			setting.PlacesOff.Remove(place);
			setting.PlacesOff.Remove(other);
			if (placeOff)
			{
				setting.PlacesOff.Add(other);
			}

			if (otherOff)
			{
				setting.PlacesOff.Add(place);
			}

			setting.Places = order;
		}

		// The places to try, in order
		public List<string> PlacesToTry() => PlaceOrder().Where(place => PlaceOn(place)).ToList();

		// The items in the order the owner set (the rest in the default order after them)
		public List<Entry> OrderedEntries()
		{
			var order = Setting.Order;
			return Entries
				.Select((entry, index) => new { entry, key = order.IndexOf(entry.Template) is int k && k >= 0 ? k : 1000 + index })
				.OrderBy(pair => pair.key)
				.Select(pair => pair.entry)
				.ToList();
		}

		// What counts: ordered and without the items switched off
		public Entry[] ActiveEntries() => OrderedEntries().Where(entry => !Setting.Off.Contains(entry.Template)).ToArray();

		// Moves an item one place up or down, and writes the order out in full
		public void MoveEntry(string template, int step)
		{
			var templates = OrderedEntries().Select(entry => entry.Template).ToList();
			int index = templates.IndexOf(template);
			int target = index + step;
			if (index < 0 || target < 0 || target >= templates.Count)
			{
				return;
			}

			templates.RemoveAt(index);
			templates.Insert(target, template);
			Setting.Order = templates;
		}

		// The most of this item to carry for the group (0: no limit). It is a preference: when nothing else can make up the target,
		// more of it are brought.
		public int MaxOf(string template)
		{
			int max;
			return Setting.MaxPer.TryGetValue(template, out max) ? max : 0;
		}

		public void SetMax(string template, int max)
		{
			if (max <= 0)
			{
				Setting.MaxPer.Remove(template);
			}
			else
			{
				Setting.MaxPer[template] = max;
			}
		}

		// Switches an item off (it goes to the end of the list) or on (it goes after the ones that are on): a switched off item keeps
		// no spot
		public void ToggleEntry(string template)
		{
			var setting = Setting;
			var order = OrderedEntries().Select(entry => entry.Template).Where(other => other != template).ToList();
			bool nowOn = setting.Off.Remove(template);
			if (!nowOn)
			{
				setting.Off.Add(template);
			}

			var on = order.Where(other => !setting.Off.Contains(other)).ToList();
			var off = order.Where(other => setting.Off.Contains(other)).ToList();
			(nowOn ? on : off).Add(template);
			setting.Order = on.Concat(off).ToList();
		}

		// ---- Weighing items

		// What an item counts for, or 0 when it isn't one of the group's items
		public float WeightOf(Item item)
		{
			var template = item.TemplateId.ToString();
			var entry = ActiveEntries().FirstOrDefault(candidate => candidate.Template == template);
			return entry == null ? 0f : entry.Weight(item);
		}

		// Its place in the group's order (0 is best), -1 when it isn't one of the items
		public int PriorityOf(Item item)
		{
			if (Random)
			{
				return 0;
			}

			var template = item.TemplateId.ToString();
			return Array.FindIndex(ActiveEntries(), entry => entry.Template == template);
		}

		// The group's items among these, in priority order; within one template, loose on the stash before in a container, then
		// non-FiR first, then the smallest stack or the fullest resource
		public IEnumerable<Item> Candidates(List<Item> items)
		{
			foreach (var entry in Random ? ActiveEntries().OrderBy(_ => Guid.NewGuid()) : ActiveEntries().AsEnumerable())
			{
				foreach (var item in items
					.Where(item => item.TemplateId.ToString() == entry.Template && item.PinLockState != EItemPinLockState.Locked)
					.OrderBy(Game.LooseBeforeFiR)
					.ThenBy(item => item.MarkedAsSpawnedInSession ? 1 : 0)
					.ThenBy(Game.LooseAfterFiR)
					.ThenBy(item => Stack ? item.StackObjectsCount : 0)
					.ThenByDescending(Game.ResourceOf))
				{
					yield return item;
				}
			}
		}
	}
}
