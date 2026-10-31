using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace GetReady
{
	// How the Ammo step's lines are shown: grouped by what drives them. Every equipped weapon is one panel (its magazine, chamber,
	// the magazine put in); weapons that take the same caliber sit in one group that shares the spare ammo of that caliber. What
	// belongs to no weapon (the spare magazines, the loose stacks, the magazines that cannot be filled) stays as the step plans it.
	// Display only: the lines are the step's, nothing in them is changed.
	internal static class AmmoPanels
	{
		// What the weapon panel is made of
		internal class WeaponPanel
		{
			public Item Item;
			public string Name;
			public string Slot;
			public PlanLine Head; // the magazine progress line, null for a weapon with nothing to fill (no magazine at all)
			public List<PlanLine> Lines = new List<PlanLine>();

			// The head with the lines that follow it: what decides how the panel stands
			public List<PlanLine> All() => (Head == null ? new List<PlanLine>() : new List<PlanLine> { Head }).Concat(Lines).ToList();
		}

		// The weapons of one caliber and the spare ammo they share
		internal class CaliberGroup
		{
			public string Caliber;
			public List<WeaponPanel> Weapons = new List<WeaponPanel>();
			public PlanLine SpareHead;
			public List<PlanLine> Spare = new List<PlanLine>();

			public List<PlanLine> All() => Weapons.SelectMany(weapon => weapon.All()).Concat(SpareHead == null ? new List<PlanLine>() : new List<PlanLine> { SpareHead }).Concat(Spare).ToList();
		}

		internal class Layout
		{
			public List<CaliberGroup> Groups = new List<CaliberGroup>();
			public List<PlanLine> Notes = new List<PlanLine>(); // about a weapon left alone (locked), no panel
			public List<PlanLine> Rest = new List<PlanLine>(); // spare magazines, loose ammo and what is wrong with them
		}

		internal const string SpareUnit = "spare ammo on you";
		internal const string MagazineUnit = "rounds";

		// The slot a weapon is carried in, as the owner calls it
		internal static string SlotLabel(Item weapon)
		{
			var address = weapon?.CurrentAddress as SlotItemAddress;
			switch (address?.Slot?.Name)
			{
				case "FirstPrimaryWeapon": return "Sling";
				case "SecondPrimaryWeapon": return "Back";
				case "Holster": return "Holster";
				case "Scabbard": return "Scabbard";
				default: return "";
			}
		}

		// "Bring 120 M855A1 for M4A1  (the ammo in use, from the stash)" -> "Bring 120 M855A1 for M4A1": the why and from where go in the tooltip
		internal static string Short(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			int cut = text.Length;
			foreach (var mark in new[] { "  (", "  [" })
			{
				int at = text.IndexOf(mark);
				if (at > 0 && at < cut)
				{
					cut = at;
				}
			}

			return text.Substring(0, cut);
		}

		private static string CaliberOf(Item item)
		{
			var weapon = item as Weapon;
			return weapon == null || string.IsNullOrEmpty(weapon.AmmoCaliber) ? "" : weapon.AmmoCaliber;
		}

		// "the stash", "your Vest": where rounds are taken from, with the number of rounds when there are several places
		internal static string SourcesText(List<PlaceCount> from)
		{
			if (from == null || from.Count == 0)
			{
				return "";
			}

			Func<PlaceCount, string> name = place => place.Name == "Stash" ? "the stash" : "your " + place.Name;
			return from.Count == 1 ? name(from[0]) : string.Join(", ", from.Select(place => $"{name(place)} ({place.Count:N0})").ToArray());
		}

		// The tooltip of a line that moves ammo, said once: what changes, where from, and why this ammo
		internal static string Tip(PlanLine line)
		{
			var text = new System.Text.StringBuilder();
			var sources = SourcesText(line.From);
			if (line.Fill && !string.IsNullOrEmpty(line.Label))
			{
				var after = Math.Min(line.Want, line.Have + line.Amount);
				text.Append($"{line.Label}{(line.Place == null ? "" : " in " + line.Place)}: {line.Have:N0}/{line.Want:N0} → {after:N0}/{line.Want:N0}");
				text.Append($"\n{line.Amount:N0} {line.What}{(sources == "" ? "" : ", taken from " + sources)}");
				if (!string.IsNullOrEmpty(line.Why))
				{
					text.Append("\nWhy this ammo: " + line.Why);
				}

				if (line.Place == null)
				{
					text.Append("\nThe magazine is taken out of the weapon, filled and put back.");
				}
			}
			else if (line.Topped)
			{
				var after = Math.Min(line.Want, line.Have + line.Amount);
				text.Append($"{line.Label} in {line.Place}: {line.Have:N0}/{line.Want:N0} → {after:N0}/{line.Want:N0}");
				text.Append($"\nTopped up with {line.Amount:N0} from the stash.");
			}
			else if (line.Part == "chamber" && !line.Problem)
			{
				text.Append($"One round of {line.What} goes in the chamber");
				if (!string.IsNullOrEmpty(line.Place))
				{
					text.Append(", taken from " + (line.Place == "Stash" ? "the stash" : "your " + line.Place));
				}

				if (!string.IsNullOrEmpty(line.Why))
				{
					text.Append("\nWhy this ammo: " + line.Why);
				}
			}
			else
			{
				return line.Text;
			}

			return text.ToString();
		}

		// The magazine whose icon goes with the "In the gun" line: the one that will be filled, the one in the gun, the one that will go in
		internal static string MagazineTemplateOf(WeaponPanel panel)
		{
			var filled = panel.Lines.FirstOrDefault(line => line.Part == "magazine" && !string.IsNullOrEmpty(line.ItemTemplate));
			if (filled != null)
			{
				return filled.ItemTemplate;
			}

			var held = (panel.Item as Weapon)?.GetCurrentMagazine();
			if (held != null)
			{
				return held.TemplateId.ToString();
			}

			return panel.Lines.FirstOrDefault(line => line.Part == "put")?.Template;
		}

		// The round in the chamber of the weapon now (template, name), or null when the chamber is empty; HasChamber says whether it has one
		internal static bool HasChamber(WeaponPanel panel) => (panel.Item as Weapon)?.Chambers.Length > 0;

		internal static KeyValuePair<string, string>? ChamberedAmmo(WeaponPanel panel)
		{
			var round = (panel.Item as Weapon)?.Chambers.Select(slot => slot.ContainedItem as Ammo).FirstOrDefault(ammo => ammo != null && !ammo.IsUsed);
			return round == null ? (KeyValuePair<string, string>?)null : new KeyValuePair<string, string>(round.TemplateId.ToString(), Game.NameOf(round));
		}

		// The ammo types in the magazine of the weapon, the one with the most rounds first (template, name)
		internal static List<KeyValuePair<string, string>> HeldAmmo(WeaponPanel panel)
		{
			var held = (panel.Item as Weapon)?.GetCurrentMagazine()?.Cartridges.Items.OfType<Ammo>();
			if (held == null)
			{
				return new List<KeyValuePair<string, string>>();
			}

			return held.GroupBy(ammo => ammo.TemplateId)
				.OrderByDescending(kind => kind.Sum(ammo => ammo.StackObjectsCount))
				.Select(kind => new KeyValuePair<string, string>(kind.Key.ToString(), Game.NameOf(kind.First())))
				.ToList();
		}

		// What is wrong, short for the panel line (the whole sentence is in the tooltip)
		internal static string ShortProblem(string detail)
		{
			if (string.IsNullOrEmpty(detail))
			{
				return detail;
			}

			if (detail.StartsWith("no magazine in it, and none of the magazines you carry fits it"))
			{
				return "No magazine in it, none you carry fits";
			}

			if (detail.StartsWith("no magazine in it, and the magazines you carry that fit it hold ammo of another caliber"))
			{
				return "No magazine in it, those that fit hold other ammo";
			}

			if (detail.StartsWith("no magazine in it, and nothing tells which ammo"))
			{
				return "No magazine in it: which ammo goes in an empty one?";
			}

			if (detail.StartsWith("no magazine in it, and there is no ammo left"))
			{
				return "No magazine in it, no ammo left to fill one";
			}

			if (detail.StartsWith("no magazine in it, and none of yours can be put in"))
			{
				return "No magazine in it, none of yours can be put in";
			}

			if (detail.StartsWith("empty chamber"))
			{
				return "Nothing left to chamber";
			}

			if (detail.StartsWith("no loose ammo on you, and nothing is loaded"))
			{
				return "Can't tell which ammo to bring";
			}

			if (detail.StartsWith("no loose ammo on you, and the stash has none"))
			{
				return "None of the ammo in use is in the stash";
			}

			return detail;
		}

		// The calibers the owner knows by another name than the game's key
		private static readonly Dictionary<string, string> Calibers = new Dictionary<string, string>
		{
			{ "762x35", ".300 BLK" }, { "556x45NATO", "5.56x45" }, { "545x39", "5.45x39" }, { "762x39", "7.62x39" }, { "762x51", "7.62x51" },
			{ "762x54R", "7.62x54R" }, { "762x25TT", "7.62x25" }, { "9x19PARA", "9x19" }, { "9x18PM", "9x18" }, { "9x21", "9x21" },
			{ "9x39", "9x39" }, { "1143x23ACP", ".45 ACP" }, { "57x28", "5.7x28" }, { "46x30", "4.6x30" }, { "127x55", "12.7x55" },
			{ "86x70", ".338 LM" }, { "366TKM", ".366 TKM" }, { "93x64", "9.3x64" }, { "12g", "12 gauge" }, { "20g", "20 gauge" }, { "23x75", "23x75" }
		};

		// What the caliber is called: by the owner's table, else the key without its prefix
		internal static string CaliberLabel(string caliber)
		{
			if (string.IsNullOrEmpty(caliber))
			{
				return "";
			}

			var key = caliber.StartsWith("Caliber") ? caliber.Substring("Caliber".Length) : caliber;
			string name;
			return Calibers.TryGetValue(key, out name) ? name : key;
		}

		// The ammo whose icon stands for the caliber of the group: what Get ready will put in, else what you carry, else what the weapon holds
		internal static string AmmoTemplateOf(CaliberGroup group)
		{
			var moving = group.All().FirstOrDefault(line => !line.Problem && line.Run != null && !string.IsNullOrEmpty(line.Template)
				&& (line.Part == "magazine" || line.Part == "chamber" || line.Part == "spare"));
			if (moving != null)
			{
				return moving.Template;
			}

			if (!string.IsNullOrEmpty(group.SpareHead?.WhatTemplate))
			{
				return group.SpareHead.WhatTemplate;
			}

			foreach (var panel in group.Weapons)
			{
				var held = (panel.Item as Weapon)?.GetCurrentMagazine()?.Cartridges.Items.FirstOrDefault();
				if (held != null)
				{
					return held.TemplateId.ToString();
				}
			}

			return null;
		}

		internal static Layout Build(List<PlanLine> lines)
		{
			var layout = new Layout();
			var groups = new Dictionary<string, CaliberGroup>();
			WeaponPanel weapon = null;
			CaliberGroup spare = null;
			bool rest = false;

			CaliberGroup GroupOf(Item item)
			{
				var key = CaliberOf(item);
				if (key == "")
				{
					key = "weapon:" + item?.TemplateId; // no caliber known: the weapon is a group of its own
				}

				CaliberGroup group;
				if (!groups.TryGetValue(key, out group))
				{
					group = new CaliberGroup { Caliber = CaliberOf(item) };
					groups[key] = group;
					layout.Groups.Add(group);
				}

				return group;
			}

			WeaponPanel PanelFor(CaliberGroup group, Item item)
			{
				var panel = new WeaponPanel { Item = item, Name = item == null ? "" : Game.NameOf(item), Slot = SlotLabel(item) };
				group.Weapons.Add(panel);
				return panel;
			}

			foreach (var line in lines)
			{
				if (rest)
				{
					layout.Rest.Add(line);
					continue;
				}

				if (line.IsProgress && line.Unit == MagazineUnit && line.Item is Weapon)
				{
					weapon = PanelFor(GroupOf(line.Item), line.Item);
					weapon.Head = line;
					spare = null;
				}
				else if (line.IsProgress && line.Unit == SpareUnit && line.Item is Weapon)
				{
					var group = GroupOf(line.Item);
					weapon = null;
					if (group.SpareHead == null)
					{
						group.SpareHead = line;
						spare = group;
					}
					else
					{
						spare = null; // a weapon of the same caliber: the card is the same, it is shown once
					}
				}
				else if (line.IsProgress)
				{
					rest = true;
					layout.Rest.Add(line);
				}
				else if (line.Standalone && line.Item is Weapon)
				{
					// A weapon with no magazine at all: a panel of its own with what is wrong
					weapon = PanelFor(GroupOf(line.Item), line.Item);
					weapon.Lines.Add(line);
					weapon = null;
					spare = null;
				}
				else if (line.Standalone && !line.Problem)
				{
					layout.Notes.Add(line);
					weapon = null;
					spare = null;
				}
				else if (line.Standalone)
				{
					rest = true; // the magazines that cannot be filled, when there is no spare magazine card to hold them
					layout.Rest.Add(line);
				}
				else if (spare != null)
				{
					spare.Spare.Add(line);
				}
				else if (weapon != null)
				{
					weapon.Lines.Add(line);
				}
				else if (line.Covered || line.Problem)
				{
					// The repeat of a shared card (its problem, or the "brought with" line) for a second weapon: the group shows it once
				}
				else
				{
					layout.Notes.Add(line);
				}
			}

			return layout;
		}
	}
}
