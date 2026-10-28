using System.Linq;

namespace GetReady
{
	// What can be asked for. Each step is a list of groups; the planning is in SuppliesStep.

	internal class HealingStep : SuppliesStep
	{
		public override string Name => "Healing";
		public override bool Enabled { get => SettingsStore.Current.Supplies.HealingEnabled; set => SettingsStore.Current.Supplies.HealingEnabled = value; }

		private static readonly GroupDef[] Definitions =
		{
			new GroupDef
			{
				Under = Game.UnderMedikit, Match = Game.IsHealingKit, Id = "health", Name = "Healing kits", Unit = "HP", DefaultTarget = 300, Max = 2000, Step = 50, DefaultHotkey = "4",
				Entries = new[]
				{
					Entry.Resource("60098ad7c2240c0fe85c570a"), // AFAK
					Entry.Resource("544fb45d4bdc2dee738b4568"), // Salewa
					Entry.Resource("590c678286f77426c9660122"), // IFAK
					Entry.Resource("590c661e86f7741e566b646a"), // Car
					Entry.Resource("5755356824597772cb798962")  // AI-2
				}
			},
			new GroupDef
			{
				Under = Game.UnderMedicalItem, Match = Game.IsBandage, Id = "bandage", Name = "Bandages", Unit = "bandage points", DefaultTarget = 2, Max = 20, DefaultHotkey = "6",
				Entries = new[]
				{
					Entry.Resource("5751a25924597722c463c472"), // Army bandage: its remaining uses (2 new)
					Entry.Points("544fb25a4bdc2dfb738b4567", 1f) // Aseptic bandage
				}
			},
			new GroupDef
			{
				Under = Game.UnderMedicalItem, Match = Game.IsTourniquet, Id = "heavybleed", Name = "Heavy bleed", Unit = "tourniquet points", DefaultTarget = 2, Max = 20, DefaultHotkey = "5",
				Entries = new[]
				{
					Entry.Resource("5e8488fa988a8701445df1e4"), // CALOK-B: its remaining uses (3 new)
					Entry.Points("60098af40accd37ef2175f27", 1f), // CAT
					Entry.Points("5e831507ea0a7c419c2f9bd9", 1f)  // Esmarch
				}
			},
			new GroupDef
			{
				Under = Game.UnderMedicalItem, Match = Game.IsSplint, Id = "splint", Name = "Splints", Unit = "splint uses", DefaultTarget = 5, Max = 50, Step = 5,
				DefaultDestinations = new[] { "Vest", "Pouch" },
				Entries = new[]
				{
					Entry.Resource("5af0454c86f7746bf20992e8"), // Aluminum splint: 5 uses
					Entry.Resource("6937ecf8628ee476240c07cb"), // Tigz'Resq splint (WTT Content Backport)
					Entry.Points("544fb3364bdc2d34748b456a", 1f) // Immobilizing splint
				}
			},
			new GroupDef
			{
				Under = Game.UnderMedicalItem, Match = Game.IsSurgeryKit, Id = "surgery", Name = "Surgery kits", Unit = "surgery uses", DefaultTarget = 0, Max = 60, Step = 1, Typed = true,
				Entries = new[]
				{
					Entry.Resource("5d02778e86f774203e7dedbe"), // CMS: 3 uses
					Entry.Resource("5d02797c86f774203f38e30a")  // Surv12: 9 uses
				}
			},
			new GroupDef
			{
				Id = "propital", Name = "Propital", Unit = "Propital", DefaultTarget = 1, DefaultHotkey = "7",
				Entries = new[] { Entry.Count("5c0e530286f7747fa1419862") }
			}
		};

		protected override GroupDef[] Defs => Definitions;
	}

	internal class GrenadesStep : SuppliesStep
	{
		public override string Name => "Grenades and flares";
		public override bool Enabled { get => SettingsStore.Current.Supplies.GrenadesEnabled; set => SettingsStore.Current.Supplies.GrenadesEnabled = value; }

		// A grenade type; a type with several templates takes whichever the stash has, at random
		private static GroupDef Grenade(string id, string name, bool impact, params string[] templates) => new GroupDef
		{
			Id = id, Name = name, Unit = "grenades", Impact = impact, AllowPouch = false, Random = templates.Length > 1, DefaultTarget = 0,
			Entries = templates.Select(Entry.Count).ToArray()
		};

		private static GroupDef Cartridge(string id, string name, string template) => new GroupDef
		{
			Id = id, Name = name, Unit = "cartridges", DefaultTarget = 0, Max = 10, Bindable = false,
			DefaultDestinations = new[] { "Special slots", "Pockets" },
			Entries = new[] { Entry.Count(template) }
		};

		private static readonly GroupDef[] Definitions =
		{
			Grenade("m67", "M67", false, "58d3db5386f77426186285a0"),
			Grenade("rgd5", "RGD-5", false, "5448be9a4bdc2dfd2f8b456a"),
			Grenade("f1", "F-1", false, "5710c24ad2720bc3458b45a3"),
			Grenade("vog", "VOG-17/25", false, "5e32f56fcb6d5863cc5e5ee4", "5e340dcdcb6d5863cc5e5efb"),
			Grenade("impact", "Impact RGN/RGO", true, "617fd91e5539a84ec44ce155", "618a431df1eb8e24b8741deb"),
			Cartridge("flares", "Green signal cartridges", "6217726288ed9f0845317459"), // RSP-30 reactive signal cartridge (Green)
			Cartridge("flaresred", "Red signal cartridges", "62178c4d4ecf221597654e3d"), // RSP-30 reactive signal cartridge (Red)
			new GroupDef { Id = "grenadequota", Name = "Any grenades (random)", Unit = "grenades in total", IsQuota = true, Bindable = false, AllowPouch = false, DefaultTarget = 2 }
		};

		protected override GroupDef[] Defs => Definitions;
	}

	// Money to carry, and which case it goes in: roubles in a wallet early on, a docs or SICC case later, euros for coop extracts
	internal class CashStep : SuppliesStep
	{
		public override string Name => "Cash";
		public override bool Enabled { get => SettingsStore.Current.Supplies.CashEnabled; set => SettingsStore.Current.Supplies.CashEnabled = value; }

		private static readonly string[] Places = { "Wallet", "WZ wallet", "Docs case", "SICC case", "Pockets", "Vest", "Backpack", "Pouch" };

		private static GroupDef Currency(string id, string name, string unit, string template, int step, int max) => new GroupDef
		{
			Id = id, Name = name, Unit = unit, Stack = true, Money = true, Bindable = false, NoFallback = true, DefaultTarget = 0, Max = max, Step = step,
			DefaultDestinations = new[] { "Wallet" }, Locations = Places,
			Entries = new[] { Entry.Stack(template) }
		};

		private static readonly GroupDef[] Definitions =
		{
			Currency("cash_rub", "Roubles", "RUB", "5449016a4bdc2d6f028b456f", 25000, 10000000),
			Currency("cash_eur", "Euros", "EUR", "569668774bdc2da2298b4568", 500, 1000000),
			Currency("cash_usd", "Dollars", "USD", "5696686a4bdc2da3298b456a", 500, 1000000)
		};

		protected override GroupDef[] Defs => Definitions;
	}
}
