using System;
using System.Collections.Generic;
using System.Linq;

namespace GetReady
{
	// The maps a case is for (set per case in Setup > Cases) against the map picked on the map screen. Only the negative case is shown: a case
	// with no map set is for any.
	internal static class MapTags
	{
		internal class MapDef
		{
			public string Name;
			public string[] Ids;
		}

		// The maps of the map screen (the game's AvailableMaps); a night factory or a Ground Zero level variant is the same map
		internal static readonly MapDef[] Maps =
		{
			new MapDef { Name = "Customs", Ids = new[] { "bigmap" } },
			new MapDef { Name = "Factory", Ids = new[] { "factory4_day", "factory4_night" } },
			new MapDef { Name = "Woods", Ids = new[] { "Woods" } },
			new MapDef { Name = "Interchange", Ids = new[] { "Interchange" } },
			new MapDef { Name = "Shoreline", Ids = new[] { "Shoreline" } },
			new MapDef { Name = "Reserve", Ids = new[] { "RezervBase" } },
			new MapDef { Name = "Lighthouse", Ids = new[] { "Lighthouse" } },
			new MapDef { Name = "Streets", Ids = new[] { "TarkovStreets" } },
			new MapDef { Name = "Ground Zero", Ids = new[] { "Sandbox", "Sandbox_high" } },
			new MapDef { Name = "Labs", Ids = new[] { "laboratory" } }
		};

		// The rule's name of the map with this location id, or null for one that has none (a map the rules do not know)
		internal static string MapOf(string locationId) =>
			string.IsNullOrEmpty(locationId) ? null : Maps.FirstOrDefault(map => map.Ids.Contains(locationId, StringComparer.OrdinalIgnoreCase))?.Name;

		// The map as the game names it ("Streets of Tarkov"), else the short name
		internal static string FullName(string map)
		{
			var def = Maps.FirstOrDefault(candidate => candidate.Name.Equals(map, StringComparison.OrdinalIgnoreCase));
			if (def == null)
			{
				return map;
			}

			var key = def.Ids[0] + " Name";
			var name = Game.Localize(key);
			return string.IsNullOrEmpty(name) || name == key ? def.Name : name;
		}

		// What is wrong for the map picked: the cases you carry that are for other maps, and the cases set for this map that are not on you
		internal class Report
		{
			public List<CaseEntry> Wrong = new List<CaseEntry>();
			public List<CaseEntry> Missing = new List<CaseEntry>();
			public bool Any => Wrong.Count > 0 || Missing.Count > 0;
		}

		// A case with no map set is for any map and is never mentioned. A case set for this map that is not on you is missing, unless you carry
		// another case of the same kind that is set for this map too (two SICCs for one map: one is enough).
		internal static Report Check(IEnumerable<CaseEntry> entries, ICollection<string> carriedIds, string map, bool enabled)
		{
			var report = new Report();
			if (!enabled || map == null)
			{
				return report;
			}

			Func<CaseEntry, bool> isFor = entry => entry.Maps.Contains(map, StringComparer.OrdinalIgnoreCase);
			var list = entries.Where(entry => entry.Maps.Count > 0).ToList();
			var carried = list.Where(entry => carriedIds.Contains(entry.CaseId)).ToList();
			report.Wrong = carried.Where(entry => !isFor(entry)).ToList();
			report.Missing = list.Where(entry => !carriedIds.Contains(entry.CaseId) && isFor(entry)
				&& !carried.Any(other => isFor(other) && other.CaseTemplate == entry.CaseTemplate)).ToList();
			return report;
		}
	}
}
