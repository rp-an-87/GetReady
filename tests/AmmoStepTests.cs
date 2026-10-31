using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The Ammo step's plan: the magazine of each weapon, the chamber, the spare magazines, the loose stacks and the spare ammo.
	public class AmmoStepTests
	{
		private readonly World _world;
		private readonly AmmoStep _step = new AmmoStep();

		public AmmoStepTests()
		{
			Reset();
			_world = new World();
			AmmoStep.Spare.Setting.Target = 0; // the spare ammo has its own tests
		}

		private List<PlanLine> Plan() => _step.Plan(null);

		private static List<string> TextsOf(List<PlanLine> lines) => lines.Select(line => line.Text).ToList();

		private Ammo InStash(string template, int count) => NewAmmo(template, count).In(_world.StashBox);

		private Weapon Rifle(int rounds, string ammo = M855, bool chambered = false)
		{
			var weapon = NewWeapon().With(NewMagazine().Loaded(ammo, rounds));
			if (chambered)
			{
				weapon.Chambered(ammo);
			}

			return _world.Equip(weapon);
		}

		// ---- the magazine of each weapon

		[Fact]
		public void A_half_empty_magazine_is_filled_with_what_it_holds()
		{
			InStash(M855, 100);
			Rifle(10, chambered: true);

			var lines = Plan();

			lines[0].Text.Should().Be("M4A1: 10/30 rounds");
			lines[0].IsProgress.Should().BeTrue();
			lines[1].Text.Should().StartWith("M4A1's magazine: 10/30  +20 ").And.Contain("(same as inside, from the stash)");
			lines[1].Run.Should().NotBeNull();
			lines.Should().HaveCount(2);
		}

		[Fact]
		public void A_full_magazine_needs_nothing_but_the_chamber_is_checked()
		{
			InStash(M855, 100);
			Rifle(30, chambered: true);

			TextsOf(Plan()).Should().Equal("M4A1: 30/30 rounds");
		}

		[Fact]
		public void An_empty_chamber_gets_a_round_of_the_magazines_ammo()
		{
			InStash(M855, 100);
			Rifle(30);

			var lines = Plan();

			lines.Select(line => line.Text).Should().Contain(text => text.StartsWith("Chamber ") && text.Contains("in M4A1  (same as its magazine, from the stash)"));
		}

		[Fact]
		public void An_empty_chamber_and_no_ammo_left_to_chamber_is_a_problem()
		{
			Rifle(30);

			var lines = Plan();

			lines.Should().Contain(line => line.Problem && line.Text == "M4A1: empty chamber, and none of the ammo in its magazine is left to chamber");
		}

		[Fact]
		public void The_chamber_is_left_alone_when_the_option_is_off()
		{
			InStash(M855, 100);
			SettingsStore.Current.Ammo.Chamber = false;
			Rifle(30);

			TextsOf(Plan()).Should().Equal("M4A1: 30/30 rounds");
		}

		[Fact]
		public void A_locked_weapon_is_left_alone()
		{
			InStash(M855, 100);
			var weapon = Rifle(10);
			weapon.PinLockState = EItemPinLockState.Locked;

			TextsOf(Plan()).Should().Equal("M4A1: left alone (locked)");
		}

		[Fact]
		public void A_locked_magazine_is_left_alone()
		{
			InStash(M855, 100);
			var weapon = Rifle(10);
			weapon.GetCurrentMagazine().PinLockState = EItemPinLockState.Locked;

			TextsOf(Plan()).Should().Equal("M4A1: left alone (its magazine is locked)");
		}

		[Fact]
		public void A_weapon_with_no_magazine_gets_a_spare_from_your_gear()
		{
			var weapon = _world.Equip(NewWeapon());
			NewMagazine().Loaded(M855, 30).In(_world.Backpack);

			var lines = Plan();

			lines.Select(line => line.Text).Should().Contain("M4A1: 0/30 rounds");
			lines.Select(line => line.Text).Should().Contain(text => text.StartsWith("Put STANAG 30 from your Scav backpack into M4A1  (30/30"));
		}

		[Fact]
		public void A_weapon_with_no_magazine_to_use_has_no_card_of_its_own_and_ends_the_one_above()
		{
			InStash(M855, 100);
			Rifle(10, chambered: true);
			_world.Equip(NewWeapon(Glock18));

			var lines = Plan();

			lines.Single(line => line.Problem && line.Subject == "Glock 18C").Standalone.Should().BeTrue();
			ReadyWindow.StandingFor(lines, 0).ToString().Should().Be("Doable");
		}

		[Fact]
		public void A_locked_weapon_line_is_standalone_too()
		{
			var weapon = Rifle(10);
			weapon.PinLockState = EItemPinLockState.Locked;

			Plan().Single().Standalone.Should().BeTrue();
		}

		[Fact]
		public void A_weapon_with_no_magazine_and_none_on_you_says_so()
		{
			_world.Equip(NewWeapon());

			Plan().Should().Contain(line => line.Problem && line.Subject == "M4A1");
		}

		// ---- the spare magazines on you

		[Fact]
		public void Spare_magazines_on_you_are_counted_and_filled()
		{
			InStash(M855, 100);
			NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			var lines = Plan();

			lines[0].Text.Should().Be("Magazines on you: 0/1 full");
			lines[1].Text.Should().StartWith("STANAG 30 in Scav backpack: 10/30  +20 ").And.Contain("(same as inside, from the stash)");
		}

		[Fact]
		public void Full_spare_magazines_count_as_full()
		{
			NewMagazine().Loaded(M855, 30).In(_world.Backpack);

			TextsOf(Plan()).Should().Equal("Magazines on you: 1/1 full");
		}

		[Fact]
		public void A_magazine_that_can_only_be_partly_filled_reports_the_rounds_missing()
		{
			InStash(M855, 10);
			NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			Plan().Should().Contain(line => line.Problem && line.Text.EndsWith("missing 10 round(s), none left"));
		}

		[Fact]
		public void Rounds_run_out_across_the_magazines_and_the_one_left_over_is_reported()
		{
			InStash(M855, 20);
			NewMagazine().Loaded(M855, 10).In(_world.Backpack);
			NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			var lines = Plan();

			lines.Count(line => line.Text.Contains("+20 ")).Should().Be(1);
			lines.Should().Contain(line => line.Problem && line.Subject == "1 magazine: no ammo left for it");
		}

		[Fact]
		public void Empty_magazines_with_nothing_to_follow_are_reported_together()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			NewMagazine().In(_world.Backpack);
			NewMagazine().In(_world.Backpack);

			var lines = Plan();

			lines.Should().Contain(line => line.Problem && line.Subject == "2 empty magazines: which ammo?");
		}

		[Fact]
		public void Magazines_that_cannot_be_filled_for_lack_of_any_ammo_are_reported()
		{
			NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			Plan().Should().Contain(line => line.Problem && line.Text.Contains("no ammo left for it"));
		}

		[Fact]
		public void A_locked_spare_magazine_is_not_touched()
		{
			InStash(M855, 100);
			var magazine = NewMagazine().Loaded(M855, 10).In(_world.Backpack);
			magazine.PinLockState = EItemPinLockState.Locked;

			Plan().Should().BeEmpty();
		}

		// ---- the loose stacks you carry

		[Fact]
		public void A_loose_stack_you_carry_is_topped_up_from_the_stash()
		{
			InStash(M855, 100);
			NewAmmo(M855, 30).In(_world.Backpack);

			var lines = Plan();

			lines[0].Text.Should().Be("Loose ammo on you: 0/1 full");
			lines.Select(line => line.Text).Should().Contain(text => text.StartsWith("M4A1") == false && text.Contains("in Scav backpack: 30/60  +30 from the stash"));
		}

		[Fact]
		public void A_full_loose_stack_is_left_alone()
		{
			InStash(M855, 100);
			NewAmmo(M855, 60).In(_world.Backpack);

			TextsOf(Plan()).Should().Equal("Loose ammo on you: 1/1 full");
		}

		[Fact]
		public void Topping_up_loose_stacks_can_be_switched_off()
		{
			InStash(M855, 100);
			NewAmmo(M855, 30).In(_world.Backpack);
			SettingsStore.Current.Ammo.TopUpLoose = false;

			Plan().Should().BeEmpty();
		}

		[Fact]
		public void A_loose_stack_is_only_topped_up_with_the_same_ammo()
		{
			InStash(M995, 100);
			NewAmmo(M855, 30).In(_world.Backpack);

			Plan().Select(line => line.Text).Should().NotContain(text => text.Contains("from the stash"));
		}

		[Fact]
		public void A_locked_loose_stack_is_left_alone()
		{
			InStash(M855, 100);
			NewAmmo(M855, 30).In(_world.Backpack).PinLockState = EItemPinLockState.Locked;

			Plan().Should().BeEmpty();
		}

		[Fact]
		public void Topping_up_does_not_happen_outside_the_stash_screen()
		{
			_world.InStashScreen = false;
			NewAmmo(M855, 30).In(_world.Backpack);

			TextsOf(Plan()).Should().Equal("Loose ammo on you: 0/1 full");
		}

		// ---- spare ammo

		[Fact]
		public void Spare_ammo_is_not_planned_when_the_group_is_off()
		{
			InStash(M855, 100);
			Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 0;

			Plan().Select(line => line.Text).Should().NotContain(text => text.StartsWith("Spare ammo"));
		}

		[Fact]
		public void A_weapon_with_no_loose_ammo_on_you_gets_spare_ammo_of_the_kind_in_use()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 1;

			var lines = Plan();

			lines.Should().Contain(line => line.Text == "Spare ammo for M4A1: 0/1 spare ammo on you");
			lines.Select(line => line.Text).Should().Contain(text => text.StartsWith("Bring 60 ") && text.Contains("for M4A1") && text.Contains("the ammo in use"));
		}

		[Fact]
		public void A_weapon_that_already_has_loose_ammo_on_you_needs_no_spare()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;

			Plan().Should().Contain(line => line.Text == "Spare ammo for M4A1: 1/1 spare ammo on you");
		}

		private static void SparePlaces(params string[] on)
		{
			var def = AmmoStep.Spare;
			foreach (var place in def.PlaceOrder().ToList())
			{
				if (def.PlaceOn(place) != on.Contains(place))
				{
					def.TogglePlace(place);
				}
			}
		}

		[Fact]
		public void Loose_ammo_in_a_place_spare_ammo_does_not_go_to_is_not_spare_ammo()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Pouch");

			var lines = Plan();

			lines.Should().Contain(line => line.Text == "Spare ammo for M4A1: 0/1 spare ammo on you");
			lines.Should().Contain(line => line.Run != null && line.Text.StartsWith("Bring "));
		}

		[Fact]
		public void Loose_ammo_in_the_place_it_goes_to_counts()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Pouch);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Pouch");

			Plan().Should().Contain(line => line.Text == "Spare ammo for M4A1: 1/1 spare ammo on you");
		}

		[Fact]
		public void With_several_places_on_loose_ammo_in_any_of_them_counts()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Vest);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Pouch", "Vest");

			Plan().Should().Contain(line => line.Text == "Spare ammo for M4A1: 1/1 spare ammo on you");
		}

		[Fact]
		public void A_place_switched_off_does_not_count_even_though_it_is_in_the_list()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Vest);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Pouch", "Backpack");

			Plan().Should().Contain(line => line.Text == "Spare ammo for M4A1: 0/1 spare ammo on you");
		}

		[Fact]
		public void A_weapon_that_needs_no_spare_is_skipped()
		{
			InStash(M855, 200);
			var rifle = Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 1;
			SettingsStore.Current.Ammo.SpareIgnored.Add(new ItemRef { Template = rifle.TemplateId.ToString(), Name = "M4A1" });

			Plan().Select(line => line.Text).Should().NotContain(text => text.StartsWith("Spare ammo"));
		}

		[Fact]
		public void Spare_ammo_says_so_when_the_stash_has_none_of_the_ammo_in_use()
		{
			Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 1;

			Plan().Should().Contain(line => line.Problem && line.Detail.Contains("the stash has none of the ammo in use"));
		}

		[Fact]
		public void Spare_ammo_is_only_planned_in_the_stash_screen()
		{
			_world.InStashScreen = false;
			Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 1;

			Plan().Select(line => line.Text).Should().NotContain(text => text.StartsWith("Spare ammo"));
		}

		[Fact]
		public void With_several_kinds_in_use_the_owners_pick_decides()
		{
			InStash(M855, 200);
			InStash(M995, 200);
			Rifle(30, M855, chambered: true);
			NewMagazine().Loaded(M995, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;
			SettingsStore.Current.Ammo.SparePick = "best";

			Plan().Select(line => line.Text).Should().Contain(text => text.StartsWith("Bring ") && text.Contains("best of the 2 kinds in use"));
		}

		// ---- spare ammo right below its weapon

		private const string Hk416 = "5bb2475ed4351e00853264e3";

		private Weapon Loaded(string template, string ammo = M855)
		{
			Names[template] = template == Hk416 ? "HK416" : Names.GetValueOrDefault(template, template);
			var weapon = NewWeapon(template).With(NewMagazine().Loaded(ammo, 30)).Chambered(ammo);
			return _world.Equip(weapon);
		}

		private static System.Collections.Generic.Dictionary<string, string> Names => SupplyFixture.Names;

		[Fact]
		public void Spare_ammo_sits_right_below_the_card_of_its_weapon()
		{
			InStash(M855, 200);
			Loaded(M4A1);
			Loaded(Hk416);
			AmmoStep.Spare.Setting.Target = 1;

			var cards = Plan().Where(line => line.IsProgress).Select(line => line.Subject).ToList();

			cards.Should().Equal("M4A1", "Spare ammo for M4A1", "HK416", "Spare ammo for HK416");
		}

		[Fact]
		public void Two_weapons_of_one_caliber_each_show_the_spare_card_but_the_ammo_is_brought_once()
		{
			InStash(M855, 200);
			Loaded(M4A1);
			Loaded(Hk416);
			AmmoStep.Spare.Setting.Target = 1;

			var lines = Plan();

			lines.Count(line => line.Run != null && line.Text.StartsWith("Bring ")).Should().Be(1);
			lines.Where(line => line.IsProgress && line.Subject.StartsWith("Spare ammo")).Select(line => (line.Subject, line.Have, line.Want))
				.Should().Equal(("Spare ammo for M4A1", 0f, 1f), ("Spare ammo for HK416", 0f, 1f));
			lines.Should().Contain(line => line.Covered && line.Text == "Brought with the stack for M4A1");
		}

		[Fact]
		public void The_repeated_card_of_a_second_weapon_stands_like_the_first()
		{
			InStash(M855, 200);
			Loaded(M4A1);
			Loaded(Hk416);
			AmmoStep.Spare.Setting.Target = 1;

			var lines = Plan();
			var spareCards = Enumerable.Range(0, lines.Count).Where(index => lines[index].IsProgress && lines[index].Subject.StartsWith("Spare ammo")).ToList();

			spareCards.Select(index => ReadyWindow.StandingFor(lines, index).ToString()).Should().Equal("Doable", "Doable");
		}

		[Fact]
		public void Ammo_you_already_have_is_shown_on_both_cards_without_a_bring_line()
		{
			InStash(M855, 200);
			Loaded(M4A1);
			Loaded(Hk416);
			NewAmmo(M855, 10).In(_world.Pouch);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Pouch");

			var lines = Plan();

			lines.Where(line => line.IsProgress && line.Subject.StartsWith("Spare ammo")).Select(line => line.Have).Should().Equal(1f, 1f);
			lines.Should().NotContain(line => line.Covered);
			lines.Should().NotContain(line => line.Run != null && line.Text.StartsWith("Bring "));
		}

		[Fact]
		public void A_problem_for_the_first_weapon_is_repeated_for_the_second()
		{
			Loaded(M4A1);
			Loaded(Hk416);
			AmmoStep.Spare.Setting.Target = 1;

			var problems = Plan().Where(line => line.Problem && line.Subject != null && line.Subject.StartsWith("Spare ammo")).Select(line => line.Subject).ToList();

			problems.Should().Equal("Spare ammo for M4A1", "Spare ammo for HK416");
		}

		[Fact]
		public void A_weapon_skipped_for_spare_ammo_has_no_spare_card()
		{
			InStash(M855, 200);
			var rifle = Loaded(M4A1);
			Loaded(Hk416);
			AmmoStep.Spare.Setting.Target = 1;
			SettingsStore.Current.Ammo.SpareIgnored.Add(new ItemRef { Template = rifle.TemplateId.ToString(), Name = "M4A1" });

			Plan().Where(line => line.IsProgress && line.Subject.StartsWith("Spare ammo")).Select(line => line.Subject).Should().Equal("Spare ammo for HK416");
		}

		// ---- the order of everything about ammo

		[Fact]
		public void Everything_about_ammo_comes_in_one_order_weapons_with_their_spare_then_magazines_then_loose_ammo()
		{
			InStash(M855, 300);
			Loaded(M4A1);
			NewMagazine().Loaded(M855, 10).In(_world.Backpack);
			NewAmmo(M855, 30).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Pouch");

			var cards = Plan().Where(line => line.IsProgress).Select(line => line.Subject).ToList();

			cards.Should().Equal("M4A1", "Spare ammo for M4A1", "Magazines on you", "Loose ammo on you");
		}

		[Fact]
		public void The_spare_ammo_of_the_last_weapon_is_not_pushed_behind_the_magazines_and_loose_ammo()
		{
			InStash(M855, 300);
			Loaded(M4A1);
			Loaded(Hk416);
			NewMagazine().Loaded(M855, 10).In(_world.Backpack);
			NewAmmo(M855, 30).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Pouch");

			var cards = Plan().Where(line => line.IsProgress).Select(line => line.Subject).ToList();

			cards.Should().Equal("M4A1", "Spare ammo for M4A1", "HK416", "Spare ammo for HK416", "Magazines on you", "Loose ammo on you");
		}

		[Fact]
		public void Rounds_that_are_missing_are_not_put_in_a_weapons_card_when_there_is_no_magazines_card()
		{
			InStash(M855, 10);
			Rifle(10, chambered: true);

			var missing = Plan().Single(line => line.Problem && line.Subject != null && line.Subject.StartsWith("Ammo:"));

			missing.Standalone.Should().BeTrue();
		}
	}
}
