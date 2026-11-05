using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.AmmoRules;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// What goes into a magazine: it follows what is already around it and never guesses, with real weapons, magazines and ammo.
	public class AmmoRulesTests
	{
		private readonly World _world;

		public AmmoRulesTests()
		{
			Reset();
			_world = new World();
		}

		private FillPlan Plan(Magazine magazine, AmmoStock stock = null, Weapon forWeapon = null) => PlanFill(null, stock ?? _world.Stock(), magazine, forWeapon);

		private Magazine SpareOnYou() => NewMagazine().In(_world.Backpack);

		private Magazine SpareInStash() => NewMagazine().In(_world.StashBox);

		private Ammo InStash(string template, int count) => NewAmmo(template, count).In(_world.StashBox);

		private Ammo OnYou(string template, int count) => NewAmmo(template, count).In(_world.Backpack);

		[Fact]
		public void Loose_stacks_in_the_stash_are_used_before_ammo_inside_a_case()
		{
			var holder = NewContainer(AlphaCase).In(_world.StashBox);
			var small = NewAmmo(M855, 20).In(holder);
			Game.IsInsideContainer = item => item == small;
			var loose = InStash(M855, 70);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)));

			var plan = Plan(SpareOnYou());

			plan.Allocation.Select(pair => pair.Key).Should().Equal(loose);
			plan.Allocation.Select(pair => pair.Key).Should().NotContain(small);
		}

		[Fact]
		public void A_spare_follows_loose_ammo_anywhere_you_wear_when_nothing_nearer_says_anything()
		{
			InStash(M855, 100);
			NewAmmo(M855, 20).In(_world.Vest);

			var plan = Plan(SpareOnYou());

			plan.Type.Value.ToString().Should().Be(M855);
			plan.Reason.Should().Be("loose ammo on you");
		}

		[Fact]
		public void Loose_ammo_in_the_stash_is_not_a_pointer()
		{
			InStash(M855, 100);

			Plan(SpareOnYou()).Problem.Should().Be("nothing to follow");
		}

		[Fact]
		public void Several_kinds_of_loose_ammo_are_not_guessed_by_default_anywhere_either()
			{
			InStash(M855, 100);
			InStash(M995, 100);
			NewAmmo(M855, 20).In(_world.Vest);
			NewAmmo(M995, 20).In(_world.Pouch);

			Plan(SpareOnYou()).Problem.Should().Be("nothing to follow");
		}

		[Theory]
		[InlineData("worst", M855)]
		[InlineData("best", M995)]
		public void The_owners_pick_decides_among_several_kinds_of_loose_ammo(string mode, string expected)
		{
			InStash(M855, 100);
			InStash(M995, 100);
			OnYou(M855, 20);
			OnYou(M995, 20);
			SettingsStore.Current.Ammo.PointerConflict = mode;

			var plan = Plan(SpareOnYou());

			plan.Type.Value.ToString().Should().Be(expected);
			plan.Reason.Should().StartWith($"the {mode} of the loose ammo");
		}

		[Theory]
		[InlineData("worst")]
		[InlineData("best")]
		[InlineData("random")]
		public void Kinds_of_different_calibers_are_never_picked_between(string mode)
		{
			var kinds = new List<Ammo> { NewAmmo(M855, 20), NewAmmo(Pst9x19, 20) };

			PickKind(kinds, mode).Should().BeNull();
		}

		[Fact]
		public void Kinds_of_one_caliber_are_picked_by_penetration()
		{
			var worse = NewAmmo(M855, 20);
			var better = NewAmmo(M995, 20);

			PickKind(new List<Ammo> { worse, better }, "best").Should().BeSameAs(better);
			PickKind(new List<Ammo> { worse, better }, "worst").Should().BeSameAs(worse);
			PickKind(new List<Ammo> { worse, better }, "conflict").Should().BeNull();
		}

		// ---- what is there to put in

		[Fact]
		public void With_no_ammo_anywhere_it_says_so()
		{
			var plan = Plan(SpareOnYou().Loaded(M855, 10));

			plan.Problem.Should().Be("no compatible ammo left");
			plan.Allocation.Should().BeEmpty();
		}

		[Fact]
		public void Ammo_of_another_caliber_is_not_compatible()
		{
			InStash(Pst9x19, 100);

			Plan(SpareOnYou().Loaded(M855, 10)).Problem.Should().Be("no compatible ammo left");
		}

		[Fact]
		public void Locked_ammo_is_left_alone()
		{
			var stack = InStash(M855, 100);
			stack.PinLockState = EItemPinLockState.Locked;

			Plan(SpareOnYou().Loaded(M855, 10)).Problem.Should().Be("no compatible ammo left");
		}

		[Fact]
		public void Ammo_you_have_not_examined_is_left_alone()
		{
			var stack = InStash(M855, 100);
			_world.NotExamined.Add(stack);

			Plan(SpareOnYou().Loaded(M855, 10)).Problem.Should().Be("no compatible ammo left");
		}

		[Fact]
		public void A_magazine_of_ten_rounds_has_twenty_free()
		{
			InStash(M855, 100);

			Plan(SpareOnYou().Loaded(M855, 10)).Free.Should().Be(20);
		}

		// ---- what it follows

		[Fact]
		public void It_follows_what_the_magazine_already_holds()
		{
			InStash(M855, 100);
			InStash(M995, 100);

			var plan = Plan(SpareOnYou().Loaded(M995, 10));

			plan.Type.Value.ToString().Should().Be(M995);
			plan.Reason.Should().Be("same as inside");
		}

		[Fact]
		public void A_magazine_in_the_stash_follows_the_equipped_weapon_that_takes_it()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M995, 30)));

			var plan = Plan(SpareInStash());

			plan.Type.Value.ToString().Should().Be(M995);
			plan.Reason.Should().Be("same as in your M4A1");
		}

		[Fact]
		public void A_magazine_in_the_stash_follows_the_chambered_round_when_the_weapon_has_no_magazine_ammo()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			_world.Equip(NewWeapon().With(NewMagazine()).Chambered(M855));

			var plan = Plan(SpareInStash());

			plan.Type.Value.ToString().Should().Be(M855);
			plan.Reason.Should().Be("same as the round chambered in your M4A1");
		}

		[Fact]
		public void A_magazine_in_the_stash_with_no_weapon_to_follow_follows_nothing()
		{
			InStash(M855, 100);
			InStash(M995, 100);

			var plan = Plan(SpareInStash());

			plan.Problem.Should().Be("nothing to follow");
			plan.Options.Should().Contain(M855).And.Contain(M995);
		}

		[Fact]
		public void A_magazine_in_the_stash_does_not_follow_the_loose_ammo_next_to_it()
		{
			var magazine = SpareInStash();
			InStash(M855, 100);

			Plan(magazine).Problem.Should().Be("nothing to follow");
		}

		[Fact]
		public void A_spare_on_you_follows_the_loose_ammo_in_the_same_container()
		{
			InStash(M995, 100);
			OnYou(M855, 20);

			var plan = Plan(SpareOnYou());

			plan.Type.Value.ToString().Should().Be(M855);
			plan.Reason.Should().Be("loose ammo in your Scav backpack");
		}

		[Fact]
		public void Loose_ammo_of_two_kinds_in_the_container_is_not_guessed()
		{
			InStash(M995, 100);
			OnYou(M855, 20);
			OnYou(M995, 20);

			Plan(SpareOnYou()).Problem.Should().Be("nothing to follow");
		}

		[Fact]
		public void A_spare_on_you_follows_the_nearest_magazine_when_no_loose_ammo_is_there()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			SpareOnYou().Loaded(M855, 5);
			var empty = SpareOnYou();

			var plan = Plan(empty);

			plan.Type.Value.ToString().Should().Be(M855);
			plan.Reason.Should().Be("same as the nearest magazine");
		}

		[Fact]
		public void A_spare_on_you_follows_an_equipped_weapon_when_nothing_in_its_container_says_anything()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M995, 30)));

			var plan = Plan(SpareOnYou());

			plan.Type.Value.ToString().Should().Be(M995);
			plan.Reason.Should().Be("same as in your M4A1");
		}

		[Fact]
		public void Two_weapons_that_disagree_leave_nothing_to_follow()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M995, 30)));
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)));

			Plan(SpareOnYou()).Problem.Should().Be("nothing to follow");
		}

		[Fact]
		public void Two_weapons_that_agree_are_followed()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M995, 30)));
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M995, 10)));

			Plan(SpareOnYou()).Type.Value.ToString().Should().Be(M995);
		}

		[Fact]
		public void A_weapon_with_no_ammo_among_the_weapons_that_take_it_leaves_nothing_to_follow()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M995, 30)));
			_world.Equip(NewWeapon().With(NewMagazine()));

			Plan(SpareInStash()).Problem.Should().Be("nothing to follow");
		}

		[Fact]
		public void The_only_ammo_you_carry_is_followed_by_the_last_rule()
		{
			InStash(M995, 100);
			NewAmmo(M855, 20).In(_world.Pouch);

			var plan = Plan(SpareOnYou());

			plan.Type.Value.ToString().Should().Be(M855);
			plan.Reason.Should().Be("loose ammo on you");
		}

		[Fact]
		public void Ammo_in_the_stash_alone_is_never_followed()
		{
			InStash(M995, 100);

			Plan(SpareOnYou()).Problem.Should().Be("nothing to follow");
		}

		// ---- the order of the owner's rules

		[Fact]
		public void The_owners_order_of_rules_decides_which_answer_wins()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			OnYou(M855, 20);
			SpareOnYou().Loaded(M995, 5);
			var empty = SpareOnYou();

			Plan(empty).Reason.Should().Be("same as the nearest magazine");

			SettingsStore.Current.Ammo.Pointers = new List<string> { "loose", "nearest", "others", "weapon" };
			Plan(empty).Reason.Should().Be("loose ammo in your Scav backpack");
		}

		[Fact]
		public void A_rule_that_has_no_answer_passes_to_the_next()
		{
			InStash(M855, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)));
			SettingsStore.Current.Ammo.Pointers = new List<string> { "nearest", "loose", "weapon" };

			Plan(SpareOnYou()).Reason.Should().Be("same as in your M4A1");
		}

		// ---- a magazine in a weapon

		[Fact]
		public void A_magazine_in_a_weapon_follows_a_spare_magazine_for_it()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			var inWeapon = NewMagazine();
			var weapon = _world.Equip(NewWeapon().With(inWeapon));
			SpareOnYou().Loaded(M995, 30);

			var plan = Plan(inWeapon);

			plan.Type.Value.ToString().Should().Be(M995);
			plan.Reason.Should().Be("same as your spare magazine");
		}

		[Fact]
		public void A_magazine_in_a_weapon_follows_the_chambered_round()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			var inWeapon = NewMagazine();
			_world.Equip(NewWeapon().With(inWeapon).Chambered(M995));

			var plan = Plan(inWeapon);

			plan.Type.Value.ToString().Should().Be(M995);
			plan.Reason.Should().Be("same as the round chambered");
		}

		[Fact]
		public void A_magazine_in_a_weapon_only_gets_ammo_of_the_weapons_caliber()
		{
			InStash(Pst9x19, 100);
			InStash(M855, 100);
			var inWeapon = NewMagazine().Loaded(M855, 10);
			_world.Equip(NewWeapon().With(inWeapon));

			Plan(inWeapon).Type.Value.ToString().Should().Be(M855);
		}

		// ---- where the rounds come from

		[Fact]
		public void The_smallest_stack_is_taken_first_and_the_rest_from_the_next()
		{
			var big = InStash(M855, 100);
			var small = InStash(M855, 10);
			var middle = InStash(M855, 50);

			var plan = Plan(SpareOnYou().Loaded(M855, 0).Loaded(M855, 1));

			plan.Free.Should().Be(29);
			plan.Allocation.Select(pair => pair.Key).Should().Equal(small, middle);
			plan.Allocation.Select(pair => pair.Value).Should().Equal(10, 19);
			plan.Short.Should().Be(0);
		}

		[Fact]
		public void When_the_stash_has_too_little_the_rest_is_short()
		{
			InStash(M855, 10);

			var plan = Plan(SpareOnYou().Loaded(M855, 10));

			plan.Allocation.Sum(pair => pair.Value).Should().Be(10);
			plan.Short.Should().Be(10);
		}

		[Fact]
		public void In_the_stash_screen_the_stash_comes_before_what_you_carry()
		{
			var carried = OnYou(M855, 100);
			var stashed = InStash(M855, 5);

			var plan = Plan(SpareOnYou().Loaded(M855, 10));

			plan.Allocation.Select(pair => pair.Key).Should().Equal(stashed, carried);
			plan.Sources.Should().Be("your gear and the stash");
		}

		[Fact]
		public void The_pouch_comes_last_in_the_stash_screen()
		{
			var pouch = NewAmmo(M855, 100).In(_world.Pouch);
			var carried = OnYou(M855, 100);

			var plan = Plan(SpareOnYou().Loaded(M855, 29));

			plan.Allocation.Select(pair => pair.Key).Should().Equal(carried);
			pouch.Should().NotBeNull();
		}

		[Fact]
		public void In_a_raid_the_container_of_the_magazine_comes_first_then_the_pouch_then_other_containers()
		{
			_world.InStashScreen = false;
			var home = _world.Backpack;
			var magazine = NewMagazine().Loaded(M855, 20).In(home);
			var inPouch = NewAmmo(M855, 4).In(_world.Pouch);
			var inHome = NewAmmo(M855, 4).In(home);

			var plan = Plan(magazine);

			plan.Allocation.Select(pair => pair.Key).Should().Equal(inHome, inPouch);
			plan.Allocation.Select(pair => pair.Value).Should().Equal(4, 4);
			plan.Short.Should().Be(2);
			plan.Sources.Should().Be("your gear");
		}

		[Fact]
		public void The_stash_is_not_a_source_in_a_raid()
		{
			_world.InStashScreen = false;
			InStash(M855, 100);
			var magazine = NewMagazine().Loaded(M855, 20).In(_world.Backpack);

			Plan(magazine).Problem.Should().Be("no compatible ammo left");
		}

		[Fact]
		public void The_next_magazine_sees_what_the_first_one_took()
		{
			InStash(M855, 40);
			var stock = _world.Stock();
			var first = SpareOnYou().Loaded(M855, 0);
			var second = SpareOnYou().Loaded(M855, 0);
			first.Loaded(M855, 1);
			second.Loaded(M855, 1);

			var one = Plan(first, stock);
			var two = Plan(second, stock);

			one.Allocation.Sum(pair => pair.Value).Should().Be(29);
			two.Allocation.Sum(pair => pair.Value).Should().Be(11);
			two.Short.Should().Be(18);
		}

		[Fact]
		public void A_full_magazine_has_nothing_to_fill()
		{
			InStash(M855, 100);

			var plan = Plan(SpareOnYou().Loaded(M855, 30));

			plan.Free.Should().Be(0);
			plan.Allocation.Should().BeEmpty();
		}

		[Fact]
		public void A_magazine_that_holds_one_caliber_is_never_mixed_with_another()
		{
			var glock = NewMagazine(GlockMag).Loaded(Pst9x19, 5).In(_world.Backpack);
			InStash(M855, 100);

			Plan(glock).Problem.Should().Be("no compatible ammo left");
		}

		// ---- the round for the chamber

		[Fact]
		public void The_chamber_gets_one_round_of_the_asked_type_and_the_stock_remembers()
		{
			var stack = InStash(M855, 3);
			var weapon = _world.Equip(NewWeapon().With(NewMagazine()));
			var stock = _world.Stock();
			var type = stack.TemplateId;

			var round = PickForChamber(null, stock, weapon, weapon.Chambers[0], weapon.GetCurrentMagazine(), type);

			round.Should().BeSameAs(stack);
			stock.Left(stack).Should().Be(2);
		}

		[Fact]
		public void The_chamber_gets_nothing_when_none_of_the_type_is_left()
		{
			var stack = InStash(M855, 1);
			var weapon = _world.Equip(NewWeapon().With(NewMagazine()));
			var stock = _world.Stock();
			PickForChamber(null, stock, weapon, weapon.Chambers[0], weapon.GetCurrentMagazine(), stack.TemplateId);

			PickForChamber(null, stock, weapon, weapon.Chambers[0], weapon.GetCurrentMagazine(), stack.TemplateId).Should().BeNull();
		}

		[Fact]
		public void The_chamber_takes_the_smallest_stack_of_the_type()
		{
			InStash(M855, 50);
			var small = InStash(M855, 2);
			var weapon = _world.Equip(NewWeapon().With(NewMagazine()));

			PickForChamber(null, _world.Stock(), weapon, weapon.Chambers[0], weapon.GetCurrentMagazine(), small.TemplateId).Should().BeSameAs(small);
		}

		// ---- the stock

		[Fact]
		public void The_stock_knows_where_the_stacks_are()
		{
			var stashed = InStash(M855, 5);
			var carried = OnYou(M855, 5);
			var pouched = NewAmmo(M855, 5).In(_world.Pouch);

			var stock = _world.Stock();

			stock.InStash.Should().BeTrue();
			stock.Stash.Should().Contain(stashed).And.NotContain(carried);
			stock.Carried.Should().Contain(carried).And.NotContain(pouched);
			stock.Pouch.Should().Contain(pouched);
			stock.Left(carried).Should().Be(5);
		}

		[Fact]
		public void Outside_the_stash_screen_there_is_no_stash_in_the_stock()
		{
			_world.InStashScreen = false;

			_world.Stock().InStash.Should().BeFalse();
			_world.Stock().Stash.Should().BeEmpty();
		}

		// ---- magazines in other containers

		// Another container on you: the vest
		private Magazine InACase() => NewMagazine().In(_world.Vest);

		[Fact]
		public void A_spare_follows_a_magazine_in_another_container_when_the_default_rules_find_nothing_nearer()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			InACase().Loaded(M855, 5);
			var empty = SpareOnYou();

			var plan = Plan(empty);

			plan.Type.Value.ToString().Should().Be(M855);
			plan.Reason.Should().Be("same as a magazine in your Vest");
		}

		[Fact]
		public void A_magazine_in_the_same_container_is_nearer_than_one_in_another()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			InACase().Loaded(M995, 5);
			SpareOnYou().Loaded(M855, 5);
			var empty = SpareOnYou();

			Plan(empty).Type.Value.ToString().Should().Be(M855);
			Plan(empty).Reason.Should().Be("same as the nearest magazine");
		}

		[Fact]
		public void The_rule_for_other_containers_can_be_put_before_the_one_for_the_same_container()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			InACase().Loaded(M995, 5);
			SpareOnYou().Loaded(M855, 5);
			var empty = SpareOnYou();
			SettingsStore.Current.Ammo.Pointers = new List<string> { "others", "nearest", "loose", "weapon" };

			Plan(empty).Type.Value.ToString().Should().Be(M995);
			Plan(empty).Reason.Should().Be("same as a magazine in your Vest");
		}

		[Fact]
		public void Empty_magazines_in_other_containers_say_nothing()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			InACase();

			Plan(SpareOnYou()).Problem.Should().Be("nothing to follow");
		}

		[Fact]
		public void A_magazine_in_the_stash_is_not_one_of_the_other_containers_on_you()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			SpareInStash().Loaded(M855, 5);

			Plan(SpareOnYou()).Problem.Should().Be("nothing to follow");
		}

		[Fact]
		public void With_the_rule_left_out_of_the_list_it_is_added_at_the_end()
		{
			SettingsStore.Current.Ammo.Pointers = new List<string> { "loose", "nearest", "weapon" };

			AmmoRules.PointerOrder().Should().Equal("loose", "nearest", "weapon", "others", "anywhere");
		}

		[Fact]
		public void The_rules_have_plain_names()
		{
			AmmoRules.PointerName("nearest").Should().Be("What magazines in the same container hold");
			AmmoRules.PointerName("others").Should().Be("What magazines in other containers hold");
			AmmoRules.PointerName("loose").Should().Be("Loose ammo in the same container");
			AmmoRules.PointerName("weapon").Should().Be("What an equipped weapon that takes it holds");
		}
	}
}
