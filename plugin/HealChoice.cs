using System;
using System.Collections.Generic;
using System.Linq;

namespace GetReady
{
	// What Treat injuries picks, with no game types in it so it can be tested on its own (mods\GetReady.Tests).
	internal static class HealChoice
	{
		// The injuries, most urgent first
		internal static readonly string[] Troubles = { "heavy", "light", "fracture", "hp" };

		// The item to use next: for the most urgent injury present that has an item left, the first one in the order that may be used for it.
		// usableFor(item, trouble): allowed, treats it and not switched off for it. inStock(item): there is one in the stash. Null when none.
		internal static string Next(IEnumerable<string> present, IList<string> order, Func<string, string, bool> usableFor, Func<string, bool> inStock)
		{
			var wanted = new HashSet<string>(present);
			foreach (var trouble in Troubles.Where(wanted.Contains))
			{
				var found = order.FirstOrDefault(item => usableFor(item, trouble) && inStock(item));
				if (found != null)
				{
					return found;
				}
			}

			return null;
		}

		// The injuries present that nothing in the stash may be used for
		internal static List<string> Missing(IEnumerable<string> present, IList<string> order, Func<string, string, bool> usableFor, Func<string, bool> inStock)
		{
			var wanted = new HashSet<string>(present);
			return Troubles.Where(wanted.Contains).Where(trouble => !order.Any(item => usableFor(item, trouble) && inStock(item))).ToList();
		}
	}
}
