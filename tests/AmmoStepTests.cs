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
			lines[1].Text.Should().StartWith("M4A1's magazine: 10/30  +20 ").And.Contain("(same as inside; taken from the stash)");
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

			lines.Select(line => line.Text).Should().Contain(text => text.StartsWith("Chamber ") && text.Contains("in M4A1  (same as its magazine; taken from the stash)"));
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

			lines[0].Text.Should().Be("Extra magazines on you: 0/1 full");
			lines[1].Text.Should().StartWith("STANAG 30 in Scav backpack: 10/30  +20 ").And.Contain("(same as inside; taken from the stash)");
		}

		[Fact]
		public void Full_spare_magazines_count_as_full()
		{
			NewMagazine().Loaded(M855, 30).In(_world.Backpack);

			TextsOf(Plan()).Should().Equal("Extra magazines on you: 1/1 full");
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
			lines.Select(line => line.Text).Should().Contain(text => text.StartsWith("M4A1") == false && text.Contains("in Scav backpack: 30/60  topped up with 30 from the stash"));
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
		public void Loose_ammo_in_a_place_spare_ammo_does_not_go_to_still_counts_as_spare_ammo()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Pouch");

			var lines = Plan();

			lines.Should().Contain(line => line.Text == "Spare ammo for M4A1: 1/1 spare ammo on you");
			lines.Should().NotContain(line => line.Run != null && line.Text.StartsWith("Bring "));
		}

		[Fact]
		public void Loose_ammo_of_another_caliber_is_not_spare_ammo()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			NewAmmo(Pst9x19, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;

			Plan().Should().Contain(line => line.Text == "Spare ammo for M4A1: 0/1 spare ammo on you");
		}

		[Fact]
		public void Loose_ammo_in_the_pouch_counts_whatever_the_places_are()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Pouch);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Backpack");

			Plan().Should().Contain(line => line.Text == "Spare ammo for M4A1: 1/1 spare ammo on you");
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
		public void The_places_only_say_where_ammo_is_brought_to_when_there_is_none()
		{
			InStash(M855, 200);
			Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 1;
			SparePlaces("Pouch", "Backpack");

			var lines = Plan();

			lines.Should().Contain(line => line.Text == "Spare ammo for M4A1: 0/1 spare ammo on you");
			lines.Should().Contain(line => line.Run != null && line.Text.StartsWith("Bring "));
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

			cards.Should().Equal("M4A1", "Spare ammo for M4A1", "Extra magazines on you", "Loose ammo on you");
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

			cards.Should().Equal("M4A1", "Spare ammo for M4A1", "HK416", "Spare ammo for HK416", "Extra magazines on you", "Loose ammo on you");
		}

		[Fact]
		public void Rounds_that_are_missing_are_not_put_in_a_weapons_card_when_there_is_no_magazines_card()
		{
			InStash(M855, 10);
			Rifle(10, chambered: true);

			var missing = Plan().Single(line => line.Problem && line.Subject != null && line.Subject.StartsWith("Ammo:"));

			missing.Standalone.Should().BeTrue();
		}

		// ---- spare ammo is a total: what you carry (and the top-up adds) counts, the rest is brought, and short is a problem

		private List<PlanLine> SpareLines()
		{
			var lines = Plan();
			return lines.Skip(lines.FindIndex(line => line.Part == "spare")).TakeWhile(line => line.Part == "spare").ToList();
		}

		[Fact]
		public void Less_than_asked_on_you_and_nothing_to_bring_is_a_problem_with_the_numbers()
		{
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Vest);
			AmmoStep.Spare.Setting.Target = 1;
			_world.Equip(NewWeapon()); // a second weapon is irrelevant here: the stash is empty

			var lines = SpareLines();

			lines.Should().ContainSingle(line => line.Problem && line.Detail.Contains("10 of 60") && line.Detail.Contains("the stash has no more of it"));
			lines.Should().NotContain(line => line.Run != null);
			lines[0].Have.Should().Be(0);
		}

		[Fact]
		public void Two_stacks_asked_with_one_and_a_bit_on_you_and_an_empty_stash_is_a_problem()
		{
			Rifle(30, chambered: true);
			NewAmmo(M855, 60).In(_world.Pouch);
			NewAmmo(M855, 10).In(_world.Vest);
			AmmoStep.Spare.Setting.Target = 2;

			var lines = SpareLines();

			lines.Should().ContainSingle(line => line.Problem && line.Detail.Contains("70 of 120"));
			lines[0].Amount.Should().Be(70);
			lines[0].Asked.Should().Be(120);
		}

		[Fact]
		public void Only_the_shortfall_is_brought_when_the_stash_has_it()
		{
			InStash(M855, 400);
			Rifle(30, chambered: true);
			NewAmmo(M855, 60).In(_world.Pouch);
			AmmoStep.Spare.Setting.Target = 2;

			var lines = SpareLines();

			lines.Should().ContainSingle(line => line.Run != null && line.Amount == 60 && line.Asked == 120);
			lines.Should().NotContain(line => line.Problem);
		}

		[Fact]
		public void The_shortfall_brought_is_what_the_stash_has_and_the_rest_is_a_problem()
		{
			InStash(M855, 20);
			Rifle(30, chambered: true);
			NewAmmo(M855, 60).In(_world.Pouch);
			AmmoStep.Spare.Setting.Target = 2;

			var lines = SpareLines();

			lines.Should().ContainSingle(line => line.Run != null && line.Amount == 20);
			lines.Should().ContainSingle(line => line.Problem && line.Detail.Contains("80 of 120"));
		}

		[Fact]
		public void Enough_on_you_means_nothing_is_brought_and_nothing_is_wrong()
		{
			InStash(M855, 400);
			Rifle(30, chambered: true);
			NewAmmo(M855, 60).In(_world.Pouch);
			NewAmmo(M855, 60).In(_world.Vest);
			AmmoStep.Spare.Setting.Target = 2;

			var lines = SpareLines();

			lines.Should().ContainSingle().Which.Have.Should().Be(1);
		}

		[Fact]
		public void What_the_top_up_of_a_loose_stack_adds_counts_toward_the_spare_ammo()
		{
			InStash(M855, 400);
			Rifle(30, chambered: true);
			NewAmmo(M855, 50).In(_world.Pouch);
			AmmoStep.Spare.Setting.Target = 1;

			var lines = Plan();

			lines.Should().Contain(line => line.Topped && line.Amount == 10);
			lines.Where(line => line.Part == "spare").Should().NotContain(line => line.Run != null || line.Problem);
		}

		// ---- a loose stack that cannot be filled

		[Fact]
		public void A_short_loose_stack_the_stash_cannot_fill_is_left_as_it_is_by_default()
		{
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Vest);

			var lines = Plan();

			lines.Should().NotContain(line => line.Problem);
		}

		[Fact]
		public void A_short_loose_stack_the_stash_cannot_fill_is_a_problem_when_the_owner_says_so()
		{
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Vest);
			SettingsStore.Current.Ammo.LooseShortIsProblem = true;

			Plan().Should().ContainSingle(line => line.Problem && line.Detail.StartsWith("10/60") && line.Detail.Contains("the stash has no more of it"));
		}

		[Fact]
		public void A_loose_stack_the_stash_can_only_partly_fill_is_topped_up_and_reported_when_the_owner_says_so()
		{
			InStash(M855, 20);
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Vest);
			SettingsStore.Current.Ammo.LooseShortIsProblem = true;

			var lines = Plan();

			lines.Should().Contain(line => line.Topped && line.Amount == 20);
			lines.Should().ContainSingle(line => line.Problem && line.Detail.StartsWith("30/60"));
		}

		[Fact]
		public void A_loose_stack_that_will_be_full_is_never_a_problem_even_when_the_owner_says_so()
		{
			InStash(M855, 400);
			Rifle(30, chambered: true);
			NewAmmo(M855, 10).In(_world.Vest);
			SettingsStore.Current.Ammo.LooseShortIsProblem = true;

			Plan().Should().NotContain(line => line.Problem);
		}

		// ---- stacks per weapon model

		private static void Stacks(Weapon weapon, int stacks) =>
			SettingsStore.Current.Ammo.SpareStacks.Add(new ModelStacks { Template = weapon.TemplateId.ToString(), Name = "model", Stacks = stacks });

		[Fact]
		public void A_model_can_ask_for_more_stacks_than_the_generic_number()
		{
			InStash(M855, 1000);
			var rifle = Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 1;
			Stacks(rifle, 3);

			var lines = SpareLines();

			lines.Should().ContainSingle(line => line.Run != null && line.Amount == 180 && line.Asked == 180);
		}

		[Fact]
		public void A_model_can_also_ask_for_fewer_stacks_than_the_generic_number()
		{
			InStash(M855, 1000);
			var rifle = Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 3;
			Stacks(rifle, 1);

			SpareLines().Should().ContainSingle(line => line.Run != null && line.Amount == 60);
		}

		[Fact]
		public void Two_weapons_of_the_same_model_ask_once_not_twice()
		{
			InStash(M855, 1000);
			var first = Rifle(30, chambered: true);
			Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 1;
			Stacks(first, 3);

			var lines = Plan().Where(line => line.Part == "spare").ToList();

			lines.Should().ContainSingle(line => line.Run != null && line.Amount == 180);
		}

		[Fact]
		public void Models_that_share_a_caliber_ask_for_the_biggest_number()
		{
			InStash(M855, 1000);
			var m4 = Loaded(M4A1);
			Loaded(Hk416);
			AmmoStep.Spare.Setting.Target = 1;
			Stacks(m4, 3);

			Plan().Where(line => line.Part == "spare").Should().ContainSingle(line => line.Run != null && line.Amount == 180);
		}

		[Fact]
		public void The_biggest_number_wins_whatever_the_order_of_the_weapons()
		{
			InStash(M855, 1000);
			Loaded(Hk416);
			var m4 = Loaded(M4A1);
			AmmoStep.Spare.Setting.Target = 1;
			Stacks(m4, 3);

			Plan().Where(line => line.Part == "spare").Should().ContainSingle(line => line.Run != null && line.Amount == 180);
		}

		[Fact]
		public void With_the_generic_number_at_zero_only_the_models_with_their_own_number_get_spare_ammo()
		{
			InStash(M855, 1000);
			InStash(Pst9x19, 1000);
			var rifle = Rifle(30, chambered: true);
			_world.Equip(NewWeapon(Glock18).With(NewMagazine(GlockMag).Loaded(Pst9x19, 5)));
			AmmoStep.Spare.Setting.Target = 0;
			Stacks(rifle, 2);

			var lines = Plan().Where(line => line.Part == "spare").ToList();

			lines.Should().Contain(line => line.IsProgress && line.Subject == "Spare ammo for M4A1");
			lines.Should().NotContain(line => line.Subject != null && line.Subject.Contains("Glock"));
			lines.Should().ContainSingle(line => line.Run != null && line.Amount == 120);
		}

		[Fact]
		public void A_model_set_to_none_gets_no_spare_ammo_while_the_others_do()
		{
			InStash(M855, 1000);
			InStash(Pst9x19, 1000);
			var rifle = Rifle(30, chambered: true);
			var pistol = _world.Equip(NewWeapon(Glock18).With(NewMagazine(GlockMag).Loaded(Pst9x19, 5)));
			AmmoStep.Spare.Setting.Target = 1;
			Stacks(pistol, 0);

			var lines = Plan().Where(line => line.Part == "spare").ToList();

			lines.Should().Contain(line => line.IsProgress && line.Subject == "Spare ammo for M4A1");
			lines.Should().NotContain(line => line.Subject != null && line.Subject.Contains("Glock"));
		}

		[Fact]
		public void A_model_set_to_none_does_not_lower_the_request_of_the_others_of_its_caliber()
		{
			InStash(M855, 1000);
			var m4 = Loaded(M4A1);
			var hk = Loaded(Hk416);
			AmmoStep.Spare.Setting.Target = 2;
			Stacks(hk, 0);

			Plan().Where(line => line.Part == "spare").Should().ContainSingle(line => line.Run != null && line.Amount == 120);
		}

		[Fact]
		public void A_model_skipped_the_older_way_is_none()
		{
			InStash(M855, 1000);
			var rifle = Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 1;
			SettingsStore.Current.Ammo.SpareIgnored.Add(new ItemRef { Template = rifle.TemplateId.ToString(), Name = "M4A1" });

			Plan().Should().NotContain(line => line.Part == "spare");
		}

		[Fact]
		public void The_stacks_of_a_model_without_its_own_number_are_the_generic_ones()
		{
			var rifle = Rifle(30);
			AmmoStep.Spare.Setting.Target = 2;

			AmmoStep.StacksOf(rifle).Should().Be(2);
			AmmoStep.StacksFor(new[] { rifle }).Should().Be(2);
			AmmoStep.StacksFor(new Weapon[0]).Should().Be(0);
		}

		[Fact]
		public void Spare_ammo_is_on_for_a_model_with_its_own_number_even_with_the_generic_number_off()
		{
			var rifle = Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 0;
			AmmoStep.AnyOwnStacks().Should().BeFalse();

			Stacks(rifle, 2);

			AmmoStep.AnyOwnStacks().Should().BeTrue();
		}

		[Fact]
		public void A_model_set_to_none_alone_does_not_switch_spare_ammo_on()
		{
			var rifle = Rifle(30, chambered: true);
			AmmoStep.Spare.Setting.Target = 0;

			Stacks(rifle, 0);

			AmmoStep.AnyOwnStacks().Should().BeFalse();
		}

		// ---- a magazine that fits but holds ammo of another caliber

		private const string Blk = "5fbe3ffdf8b6a877a729ea82";

		[Fact]
		public void A_magazine_that_fits_but_holds_ammo_of_another_caliber_is_a_problem_not_a_ready_weapon()
		{
			Names[Blk] = "BLK";
			InStash(M855, 400);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(Blk, 30)).Chambered(M855));

			var lines = Plan();

			lines.Should().ContainSingle(line => line.Problem && line.Part == "magazine" && line.Detail.StartsWith("its magazine holds BLK (.300 BLK)") && line.Detail.EndsWith("this weapon takes 5.56x45"));
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void The_ammo_in_the_magazine_of_another_caliber_is_not_filled_up_or_chambered_over()
		{
			Names[Blk] = "BLK";
			InStash(M855, 400);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(Blk, 10)));

			Plan().Should().NotContain(line => line.Fill || line.Chamber);
		}

		[Fact]
		public void A_weapon_with_no_magazine_says_so_when_the_magazines_that_fit_hold_other_ammo()
		{
			Names[Blk] = "BLK";
			NewMagazine().Loaded(Blk, 30).In(_world.Vest);
			_world.Equip(NewWeapon());

			Plan().Should().ContainSingle(line => line.Problem && line.Detail == "no magazine in it, and the magazines you carry that fit it hold ammo of another caliber");
		}

		[Fact]
		public void A_weapon_with_no_magazine_says_so_when_none_of_the_magazines_you_carry_fits()
		{
			NewMagazine(GlockMag).Loaded(Pst9x19, 5).In(_world.Vest);
			_world.Equip(NewWeapon());

			Plan().Should().ContainSingle(line => line.Problem && line.Detail == "no magazine in it, and none of the magazines you carry fits it");
		}

		[Fact]
		public void The_magazine_that_goes_in_says_which_ammo_it_holds()
		{
			NewMagazine().Loaded(M855, 30).In(_world.Vest);
			_world.Equip(NewWeapon());

			var put = Plan().Single(line => line.Part == "put");

			put.WhatTemplate.Should().Be(M855);
			put.Label.Should().NotBeNullOrEmpty();
		}

		// ---- spare magazines that hold ammo none of the weapons they fit takes

		[Fact]
		public void Spare_magazines_with_ammo_your_weapons_do_not_take_are_a_problem_and_not_counted_as_full()
		{
			Names[Blk] = "BLK";
			Rifle(30, chambered: true);
			for (int i = 0; i < 3; i++)
			{
				NewMagazine().Loaded(Blk, 30).In(_world.Vest);
			}

			var lines = Plan();

			lines.Should().NotContain(line => line.IsProgress && line.Subject == "Extra magazines on you");
			lines.Should().ContainSingle(line => line.Problem && line.Subject == "3 magazines hold ammo your weapons don't take" && line.Detail.Contains("BLK (.300 BLK)") && line.Detail.EndsWith("take 5.56x45"));
			lines.Last(line => line.Problem).Standalone.Should().BeTrue();
		}

		[Fact]
		public void The_same_magazines_are_fine_when_another_weapon_you_carry_takes_that_ammo()
		{
			Names[Blk] = "BLK";
			Rifle(30, chambered: true);
			_world.Equip(NewWeapon(Mcx).With(NewMagazine().Loaded(Blk, 30)).Chambered(Blk));
			for (int i = 0; i < 3; i++)
			{
				NewMagazine().Loaded(Blk, 30).In(_world.Vest);
			}

			var lines = Plan();

			lines.Should().NotContain(line => line.Problem);
			lines.Should().Contain(line => line.IsProgress && line.Subject == "Extra magazines on you" && line.Have == 3 && line.Want == 3);
		}

		[Fact]
		public void A_spare_magazine_that_fits_none_of_your_weapons_is_left_alone()
		{
			Rifle(30, chambered: true);
			NewMagazine(GlockMag).Loaded(Pst9x19, 5).In(_world.Vest);

			var lines = Plan();

			lines.Should().NotContain(line => line.Problem && line.Subject != null && line.Subject.Contains("don't take"));
			lines.Should().Contain(line => line.IsProgress && line.Subject == "Extra magazines on you");
		}

		[Fact]
		public void Only_the_magazines_that_can_be_used_are_counted_in_the_card()
		{
			Names[Blk] = "BLK";
			Rifle(30, chambered: true);
			NewMagazine().Loaded(M855, 30).In(_world.Vest);
			NewMagazine().Loaded(Blk, 30).In(_world.Vest);

			var lines = Plan();

			lines.Should().Contain(line => line.IsProgress && line.Subject == "Extra magazines on you" && line.Have == 1 && line.Want == 1);
			lines.Should().ContainSingle(line => line.Problem && line.Subject == "1 magazine holds ammo your weapons don't take");
		}

		[Fact]
		public void A_magazine_with_ammo_that_is_not_taken_is_not_filled_up()
		{
			Names[Blk] = "BLK";
			InStash(M855, 400);
			Rifle(30, chambered: true);
			NewMagazine().Loaded(Blk, 10).In(_world.Vest);

			Plan().Should().NotContain(line => line.Fill && line.Run != null);
		}

		[Fact]
		public void Two_kinds_of_ammo_that_are_not_taken_are_two_lines()
		{
			Names[Blk] = "BLK";
			Rifle(30, chambered: true);
			NewMagazine().Loaded(Blk, 30).In(_world.Vest);
			NewMagazine().Loaded("5fd20ff893a8961fc660a954", 30).In(_world.Vest);

			Plan().Where(line => line.Problem).Should().HaveCount(2);
		}

		[Fact]
		public void The_put_line_says_how_full_the_magazine_will_be_after_filling_it()
		{
			InStash(M855, 100);
			NewMagazine().In(_world.Vest);
			NewAmmo(M855, 10).In(_world.Vest);
			_world.Equip(NewWeapon());

			Plan().Single(line => line.Part == "put").Text.Should().EndWith("(30/30, after filling it)");
		}

		[Fact]
		public void The_put_line_of_a_magazine_that_needs_no_filling_says_how_full_it_is()
		{
			NewMagazine().Loaded(M855, 30).In(_world.Vest);
			_world.Equip(NewWeapon());

			Plan().Single(line => line.Part == "put").Text.Should().EndWith("(30/30)");
		}

		// ---- why an empty magazine cannot be put in a weapon

		[Fact]
		public void A_weapon_with_no_magazine_says_when_nothing_tells_which_ammo_to_put_in_an_empty_one()
		{
			InStash(M855, 200);
			InStash(M995, 200);
			NewMagazine().In(_world.Vest);
			_world.Equip(NewWeapon());

			var problem = Plan().Single(line => line.Problem && line.Part == "magazine");

			problem.Detail.Should().StartWith("no magazine in it, and nothing tells which ammo to put in your empty magazines (").And.EndWith("load one of them yourself and the others follow");
		}

		[Fact]
		public void A_weapon_with_no_magazine_says_when_there_is_no_ammo_left_to_fill_one()
		{
			NewMagazine().In(_world.Vest);
			_world.Equip(NewWeapon());

			Plan().Single(line => line.Problem && line.Part == "magazine").Detail.Should().Be("no magazine in it, and there is no ammo left to fill an empty magazine for it");
		}

		[Fact]
		public void Loading_one_kind_into_one_empty_magazine_settles_it_for_the_weapon()
		{
			InStash(M855, 200);
			InStash(M995, 200);
			NewMagazine().Loaded(M855, 30).In(_world.Vest);
			NewMagazine().In(_world.Vest);
			_world.Equip(NewWeapon());

			Plan().Should().NotContain(line => line.Problem && line.Part == "magazine");
			Plan().Should().Contain(line => line.Part == "put");
		}

		// ---- the magazines that cannot be filled, as the card shows them

		[Fact]
		public void An_empty_magazine_with_several_kinds_to_choose_from_says_which_magazine_where_and_what_it_could_take()
		{
			InStash(M855, 200);
			InStash(M995, 200);
			_world.Equip(NewWeapon());
			NewMagazine().In(_world.Vest);

			var stuck = Plan().Single(line => line.Part == "stuck");

			stuck.Subject.Should().Be("1 empty magazine: which ammo?");
			stuck.Options.Should().NotBeNullOrEmpty();
			stuck.Detail.Should().Contain("They could take " + stuck.Options).And.EndWith("the others follow.");
			var mag = stuck.Mags.Should().ContainSingle().Subject;
			mag.Template.Should().Be(Stanag30);
			mag.Place.Should().Be("Vest");
			mag.PlaceTemplate.Should().Be(Body.Vest);
			mag.Have.Should().Be(0);
			mag.Want.Should().Be(30);
		}

		[Fact]
		public void Magazines_with_no_ammo_left_to_fill_them_have_no_options()
		{
			Rifle(30, chambered: true);
			NewMagazine().In(_world.Vest);

			var stuck = Plan().Single(line => line.Part == "stuck");

			stuck.Subject.Should().Be("1 magazine: no ammo left for it");
			stuck.Options.Should().BeNull();
			stuck.Mags.Should().ContainSingle();
		}

		[Fact]
		public void Each_stuck_magazine_is_listed_with_its_own_place()
		{
			InStash(M855, 200);
			InStash(M995, 200);
			_world.Equip(NewWeapon());
			NewMagazine().In(_world.Vest);
			NewMagazine().In(_world.Backpack);

			var stuck = Plan().Single(line => line.Part == "stuck");

			stuck.Mags.Select(mag => mag.Place).Should().BeEquivalentTo("Vest", "Scav backpack");
		}

		// ---- a magazine that goes into a weapon is for that weapon

		[Fact]
		public void An_empty_magazine_that_goes_into_a_weapon_follows_that_weapons_chambered_round_even_when_another_weapon_takes_the_same_magazine()
		{
			Names[Blk] = "BLK";
			InStash(M855, 200);
			var m4 = NewWeapon().Chambered(M855);
			_world.Equip(m4);
			_world.Equip(NewWeapon(Mcx).With(NewMagazine().Loaded(Blk, 30)));
			NewMagazine().In(_world.Vest);

			var lines = Plan();

			lines.Should().NotContain(line => line.Problem && line.Part == "magazine");
			var fill = lines.Single(line => line.Fill && line.Run != null && line.Part == "magazine" && line.What != null && line.Template == M855);
			fill.Why.Should().Be("same as the round chambered in your M4A1");
			lines.Should().Contain(line => line.Part == "put");
		}

		[Fact]
		public void An_empty_spare_magazine_that_is_not_going_into_a_weapon_still_does_not_guess_between_weapons()
		{
			Names[Blk] = "BLK";
			InStash(M855, 200);
			InStash(Blk, 200);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)));
			_world.Equip(NewWeapon(Mcx));
			NewMagazine().In(_world.Vest);

			var lines = Plan();

			lines.Should().Contain(line => line.Part == "stuck" || line.Problem);
		}
	}
}
