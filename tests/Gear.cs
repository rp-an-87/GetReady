using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using static GetReady.AmmoRules;

namespace GetReady.Tests
{
	// Real weapons, magazines, ammo and containers (made from the item database, put together with the game's own container code) and a
	// "world": where everything is, as the ammo rules see it.
	internal static class Gear
	{
		internal const string M4A1 = "5447a9cd4bdc2dbd208b4567", Stanag30 = "55d4887d4bdc2d962f8b4570", M855 = "54527a984bdc2d4e668b4567",
			M855A1 = "54527ac44bdc2d36668b4567", M995 = "59e690b686f7746c9f75e848", Mcx = "5fbcc1d9016cce60e8341ab3", Glock18 = "5b1fa9b25acfc40018633c01",
			GlockMag = "5a718b548dc32e000d46d262", Pst9x19 = "56d59d3ad2720bdb418b4577", ScavBackpack = "56e335e4d2720b6c058b456d",
			Stash = "566abbc34bdc2d92178b4576", AlphaCase = "544a11ac4bdc2d470e8b456a";

		static Gear()
		{
			SupplyFixture.Names[M4A1] = "M4A1";
			SupplyFixture.Names[Stanag30] = "STANAG 30";
			SupplyFixture.Names[M855] = "M855";
			SupplyFixture.Names[M855A1] = "M855A1";
			SupplyFixture.Names[M995] = "M995";
			SupplyFixture.Names[Mcx] = "MCX";
			SupplyFixture.Names[Glock18] = "Glock 18C";
			SupplyFixture.Names[GlockMag] = "Glock mag";
			SupplyFixture.Names[Pst9x19] = "PST";
			SupplyFixture.Names[ScavBackpack] = "Scav backpack";
			SupplyFixture.Names[Stash] = "Stash";
			SupplyFixture.Names[AlphaCase] = "Alpha";
			SupplyFixture.Names[Body.Vest] = "Vest";
		}

		internal static Weapon NewWeapon(string template = M4A1) => (Weapon)RealItems.Any(template);

		internal static Magazine NewMagazine(string template = Stanag30) => (Magazine)RealItems.Any(template);

		internal static Ammo NewAmmo(string template, int count) => (Ammo)RealItems.Any(template, count);

		internal static CompoundItem NewContainer(string template) => (CompoundItem)RealItems.Any(template);

		// The magazine with this many rounds of the ammo in it
		internal static Magazine Loaded(this Magazine magazine, string ammoTemplate, int rounds)
		{
			if (rounds > 0)
			{
				var result = magazine.Cartridges.Add(NewAmmo(ammoTemplate, rounds), false);
				if (!result.Succeeded)
				{
					throw new InvalidOperationException("could not load the magazine: " + result.Error);
				}
			}

			return magazine;
		}

		// The magazine in the weapon's magazine slot
		internal static Weapon With(this Weapon weapon, Magazine magazine)
		{
			var result = weapon.GetMagazineSlot().Add(magazine, false);
			if (!result.Succeeded)
			{
				throw new InvalidOperationException("could not put the magazine in the weapon: " + result.Error);
			}

			return weapon;
		}

		// A round in the weapon's chamber
		internal static Weapon Chambered(this Weapon weapon, string ammoTemplate)
		{
			var result = weapon.Chambers[0].Add(NewAmmo(ammoTemplate, 1), false);
			if (!result.Succeeded)
			{
				throw new InvalidOperationException("could not chamber a round: " + result.Error);
			}

			return weapon;
		}

		// The item in the first free space of the container (the first grid that has one)
		internal static T In<T>(this T item, CompoundItem container) where T : Item
		{
			foreach (var grid in container.Grids)
			{
				var location = grid.FindFreeSpace(item);
				if (location != null)
				{
					var result = grid.Add(item, location, false);
					if (result.Succeeded)
					{
						return item;
					}
				}
			}

			throw new InvalidOperationException("no room in " + container.TemplateId);
		}

		// Where everything is: the stash (when you are in the stash screen), what you carry, the pouch and the equipped weapons
		internal class World
		{
			public CompoundItem StashBox = NewContainer(Stash);
			public CompoundItem Backpack = NewContainer(ScavBackpack);
			public CompoundItem Pouch = NewContainer(AlphaCase);
			public CompoundItem Vest = NewContainer(Body.Vest);
			public List<Weapon> Weapons = new List<Weapon>();
			public bool InStashScreen = true;
			public HashSet<Item> NotExamined = new HashSet<Item>();

			private readonly Body _body;
			private static readonly EquipmentSlot[] WeaponSlots = { EquipmentSlot.FirstPrimaryWeapon, EquipmentSlot.SecondPrimaryWeapon, EquipmentSlot.Holster };

			// With a body, the same things sit in a real inventory (so the moves a run makes can happen): the stash, the backpack, the pouch and the weapons worn
			public World(Body body) : this()
			{
				_body = body;
				StashBox = body.StashBox;
				Backpack = body.Wear(EquipmentSlot.Backpack, NewContainer(ScavBackpack));
				Pouch = body.Wear(EquipmentSlot.SecuredContainer, NewContainer(AlphaCase));
				Vest = body.Wear(EquipmentSlot.TacticalVest, NewContainer(Body.Vest));
			}

			public World()
			{
				AmmoView.Of = controller => View();
				// What you wear sits in an equipment slot, as in the game (the rules walk up from an item to the slot)
				var anchor = NewWeapon(Mcx);
				Backpack.CurrentAddress = anchor.Slots[0].CreateItemAddress();
				Pouch.CurrentAddress = anchor.Slots[1].CreateItemAddress();
				Vest.CurrentAddress = anchor.Slots[2].CreateItemAddress();
			}

			private static IEnumerable<Item> Inside(CompoundItem container) => container.GetAllItems().Where(item => item != container);

			private AmmoView View() => new AmmoView
			{
				HasStash = InStashScreen,
				Stash = () => Inside(StashBox),
				Equipment = () => Weapons.SelectMany(weapon => weapon.GetAllItems()).Concat(Inside(Backpack)).Concat(Inside(Vest)).Concat(Inside(Pouch)),
				Carried = () => Inside(Backpack).Concat(Inside(Vest)),
				Secured = () => Inside(Pouch),
				InPlace = place => place == "Backpack" ? Inside(Backpack) : place == "Pouch" ? Inside(Pouch) : place == "Vest" ? Inside(Vest) : Enumerable.Empty<Item>(),
				Weapons = () => Weapons,
				Examined = item => !NotExamined.Contains(item)
			};

			internal AmmoStock Stock() => AmmoStock.Build(null);

			internal Weapon Equip(Weapon weapon)
			{
				if (_body != null)
				{
					_body.Wear(WeaponSlots[Weapons.Count], weapon);
				}

				Weapons.Add(weapon);
				return weapon;
			}
		}
	}
}
