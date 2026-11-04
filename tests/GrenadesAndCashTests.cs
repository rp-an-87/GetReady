using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.RealItems;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// Grenades and flares: specific groups, the random total (quota), and what is left out of it.
	public class GrenadesTests
	{
		private readonly GrenadesStep _step = new GrenadesStep();

		public GrenadesTests()
		{
			Reset();
		}

		private static Item[] Items(params string[] templates) => templates.Select(template => Any(template)).ToArray();

		[Fact]
		public void A_specific_grenade_brings_as_many_as_asked()
		{
			Only(_step, "m67", 2);

			Texts(Plan(_step, new Item[0], Many(M67, 3))).Should().Equal("M67: 0/2 grenades", "Bring 2x M67");
		}

		[Fact]
		public void The_grenades_you_carry_count()
		{
			Only(_step, "m67", 2);

			Texts(Plan(_step, Items(M67), Many(M67, 3))).Should().Equal("M67: 1/2 grenades", "Bring 1x M67");
		}

		[Fact]
		public void Two_specific_groups_are_each_planned()
		{
			Only(_step, "m67", 1).Setting.Target = 1;
			_step.Groups.First(def => def.Id == "rgd5").Setting.Target = 1;

			Texts(Plan(_step, new Item[0], Items(M67, Rgd5))).Should().Equal("M67: 0/1 grenades", "Bring 1x M67", "RGD-5: 0/1 grenades", "Bring 1x RGD-5");
		}

		[Fact]
		public void A_group_of_two_kinds_takes_whichever_the_stash_has()
		{
			Only(_step, "vog", 2);

			var lines = Plan(_step, new Item[0], Items(Vog17, Vog25));

			lines.Select(line => line.Text).Where(text => text.StartsWith("Bring")).Should().BeEquivalentTo("Bring 1x VOG-17", "Bring 1x VOG-25");
		}

		[Fact]
		public void The_grenades_of_a_group_with_every_item_off_are_not_brought_and_it_says_why()
		{
			var def = Only(_step, "vog", 1);
			def.ToggleEntry(Vog17);
			def.ToggleEntry(Vog25);

			Plan(_step, new Item[0], Items(Vog17)).Last().Text.Should().Contain("every item of this group is switched off");
		}

		[Fact]
		public void Missing_grenades_are_reported()
		{
			Only(_step, "m67", 3);

			Plan(_step, new Item[0], Items(M67)).Last().Text.Should().Be("M67: Missing 2 grenades: none left in the stash");
		}

		[Fact]
		public void A_locked_grenade_is_not_brought()
		{
			Only(_step, "m67", 1);
			var stash = Items(M67);
			stash[0].PinLockState = EItemPinLockState.Locked;

			Plan(_step, new Item[0], stash).Last().Text.Should().Contain("1 locked");
		}

		[Fact]
		public void Signal_cartridges_are_planned_like_any_group()
		{
			Only(_step, "flaresred", 2);

			Texts(Plan(_step, new Item[0], Many(RedFlare, 2))).Should().Equal("Red signal cartridges: 0/2 cartridges", "Bring 2x Red flare");
		}

		// ---- the random total

		[Fact]
		public void The_total_is_made_up_with_random_grenades_one_pile_after_another()
		{
			Only(_step, "grenadequota", 3);

			var lines = Plan(_step, new Item[0], Items(M67, M67, Rgd5, Rgd5, F1));

			Texts(lines).Should().Equal("Any grenades (random): 0/3 grenades", "Bring 2x M67", "Bring 1x RGD-5");
		}

		[Fact]
		public void The_random_pick_is_by_type_so_a_type_with_many_does_not_win_more_often()
		{
			Only(_step, "grenadequota", 2);
			int call = 0;
			Game.RandomIndex = count => call++ == 0 ? count - 1 : 0; // the last pile first, then the first

			var lines = Plan(_step, new Item[0], Items(M67, M67, M67, M67, F1));

			Texts(lines).Should().Equal("Any grenades (random): 0/2 grenades", "Bring 1x F-1", "Bring 1x M67");
		}

		[Fact]
		public void The_grenades_you_carry_and_the_ones_about_to_be_brought_count_towards_the_total()
		{
			Only(_step, "m67", 1);
			_step.Groups.First(def => def.Id == "grenadequota").Setting.Target = 3;

			var lines = Plan(_step, Items(F1), Items(M67, M67, Rgd5, Rgd5));

			Texts(lines).Should().Equal("M67: 0/1 grenades", "Bring 1x M67", "Any grenades (random): 0/3 grenades".Replace("0/3", "2/3"), "Bring 1x M67".Replace("1x M67", "1x M67"));
		}

		[Fact]
		public void A_total_that_is_already_met_brings_nothing()
		{
			Only(_step, "grenadequota", 2);

			Texts(Plan(_step, Items(M67, F1), Items(M67))).Should().Equal("Any grenades (random): 2/2 grenades");
		}

		[Fact]
		public void Impact_grenades_are_never_picked_for_the_total()
		{
			Only(_step, "grenadequota", 2);

			Texts(Plan(_step, new Item[0], Items(Rgn, Rgo))).Should().Equal("Any grenades (random): 0/2 grenades", "Any grenades (random): Missing 2: no other grenade left in the stash");
		}

		[Fact]
		public void Impact_grenades_you_carry_still_count_towards_the_total()
		{
			Only(_step, "grenadequota", 2);

			Texts(Plan(_step, Items(Rgn), Items(M67, Rgd5))).Should().Equal("Any grenades (random): 1/2 grenades", "Bring 1x M67");
		}

		[Fact]
		public void Signal_cartridges_are_not_grenades_for_the_total()
		{
			Only(_step, "grenadequota", 1);

			Plan(_step, new Item[0], Items(RedFlare, GreenFlare)).Last().Text.Should().Be("Any grenades (random): Missing 1: no other grenade left in the stash");
		}

		[Fact]
		public void A_locked_grenade_is_not_picked_for_the_total()
		{
			Only(_step, "grenadequota", 1);
			var stash = Items(M67);
			stash[0].PinLockState = EItemPinLockState.Locked;

			Plan(_step, new Item[0], stash).Last().Text.Should().Contain("Missing 1");
		}

		[Fact]
		public void A_specific_grenade_with_every_place_switched_off_is_reported_not_brought()
		{
			var def = Only(_step, "m67", 1);
			foreach (var place in def.PlaceOrder().ToList())
			{
				def.TogglePlace(place);
			}

			var lines = Plan(_step, new Item[0], Many(M67, 2));

			lines.Last().Problem.Should().BeTrue();
			lines.Last().Detail.Should().Be("every place is switched off: switch at least one on in Setup");
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void The_random_total_with_every_place_switched_off_is_reported_not_brought()
		{
			var quota = Only(_step, "grenadequota", 1);
			foreach (var place in quota.PlaceOrder().ToList())
			{
				quota.TogglePlace(place);
			}

			var lines = Plan(_step, new Item[0], Items(M67));

			lines.Last().Problem.Should().BeTrue();
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void The_random_total_with_no_place_you_have_is_a_problem()
		{
			Only(_step, "grenadequota", 1);

			var lines = _step.Plan(null, new List<Item>(), Items(M67).ToList(), currency => null, place => false);

			lines.Should().Contain(line => line.Problem && line.Detail != null && line.Detail.StartsWith("set to bring to"));
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void A_grenade_type_that_is_switched_off_is_not_picked_for_the_total()
		{
			Only(_step, "grenadequota", 1);
			var vog = _step.Groups.First(def => def.Id == "vog");
			vog.ToggleEntry(Vog17);
			vog.ToggleEntry(Vog25);

			Plan(_step, new Item[0], Items(Vog17)).Last().Text.Should().Contain("Missing 1");
		}
	}

	// Cash: roubles, euros and dollars in the places that take them, and the places money is kept out of.
	public class CashTests
	{
		private readonly CashStep _step = new CashStep();

		public CashTests()
		{
			Reset();
		}

		private static Item Money(string template, int amount) => Any(template, amount);

		[Fact]
		public void The_target_is_brought_from_a_stack()
		{
			Only(_step, "cash_rub", 25000);

			Texts(Plan(_step, new Item[0], new[] { Money(Roubles, 30000) })).Should().Equal("Roubles: ₽ 0/25000", "Bring ₽ 25,000 to Pockets");
		}

		private System.Collections.Generic.List<PlanLine> PlanWithout(string missingPlace, params Item[] stash) =>
			_step.Plan(null, new System.Collections.Generic.List<Item>(), stash.ToList(), currency => null, place => place != missingPlace);

		// Exactly these places on, in this order
		private static void PlacesOn(GroupDef def, params string[] on)
		{
			foreach (var place in def.PlaceOrder().ToList())
			{
				if (def.PlaceOn(place) != on.Contains(place))
				{
					def.TogglePlace(place);
				}
			}

			foreach (var place in on.Reverse())
			{
				def.SwapPlaces(place, def.PlaceOrder().First());
				if (def.PlaceOrder().First() != place)
				{
					def.SwapPlaces(place, def.PlaceOrder().First());
				}
			}
		}

		[Fact]
		public void Money_for_a_place_you_do_not_have_is_a_problem_before_anything_runs()
		{
			var def = Only(_step, "cash_eur", 500);
			PlacesOn(def, "Pockets");

			var lines = PlanWithout("Pockets", Money(Euros, 1000));

			lines.Should().Contain(line => line.Problem && line.Subject == "Euros" && line.Detail == "set to bring to Pockets, which you are not carrying: carry one, or add another place under \"Bring to\" in Setup");
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void A_card_with_that_problem_is_red_not_amber()
		{
			var def = Only(_step, "cash_eur", 500);
			PlacesOn(def, "Pockets");

			var lines = PlanWithout("Pockets", Money(Euros, 1000));

			ReadyPlan.StandingFor(lines, 0).ToString().Should().Be("Problem");
		}

		[Fact]
		public void Money_goes_to_the_first_place_you_do_have_and_the_line_says_so()
		{
			var def = Only(_step, "cash_rub", 100000);
			PlacesOn(def, "Vest", "Pockets");

			var lines = PlanWithout("Vest", Money(Roubles, 150000));

			Texts(lines).Should().Equal("Roubles: ₽ 0/100000", "Bring ₽ 100,000 to Pockets");
		}

		[Fact]
		public void One_card_can_be_fine_while_another_cannot()
		{
			var rub = Only(_step, "cash_rub", 100000);
			PlacesOn(rub, "Vest", "Pockets");
			var eur = _step.Groups.First(group => group.Id == "cash_eur");
			eur.Setting.Target = 500;
			PlacesOn(eur, "Vest");

			var lines = PlanWithout("Vest", Money(Roubles, 150000), Money(Euros, 1000));

			lines.Count(line => line.Run != null).Should().Be(1);
			lines.Count(line => line.Problem).Should().Be(1);
		}

		[Fact]
		public void No_cash_with_nothing_missing_adds_no_explaining_line()
		{
			Only(_step, "cash_rub", 25000);

			var lines = _step.Plan(null, new System.Collections.Generic.List<Item> { Money(Roubles, 30000) }, new System.Collections.Generic.List<Item>(), currency => new CaseEntry { CaseName = "Wallet", StopsCash = true });

			lines.Should().ContainSingle().Which.Skipped.Should().BeTrue();
		}

		[Fact]
		public void The_money_you_carry_counts()
		{
			Only(_step, "cash_rub", 25000);

			Texts(Plan(_step, new[] { Money(Roubles, 10000) }, new[] { Money(Roubles, 30000) })).Should().Equal("Roubles: ₽ 10000/25000", "Bring ₽ 15,000 to Pockets");
		}

		[Fact]
		public void Several_stacks_make_up_the_amount_and_are_one_line()
		{
			Only(_step, "cash_rub", 25000);

			Texts(Plan(_step, new Item[0], new[] { Money(Roubles, 10000), Money(Roubles, 20000) })).Should().Equal("Roubles: ₽ 0/25000", "Bring ₽ 25,000 to Pockets");
		}

		[Fact]
		public void A_stash_with_too_little_says_how_much_is_missing()
		{
			Only(_step, "cash_rub", 25000);

			var lines = Plan(_step, new Item[0], new[] { Money(Roubles, 20000) });

			Texts(lines).Should().Equal("Roubles: ₽ 0/25000", "Bring ₽ 20,000 to Pockets", "Roubles: Missing ₽ 5,000: none left in the stash");
		}

		[Fact]
		public void No_money_in_the_stash_is_reported()
		{
			Only(_step, "cash_rub", 25000);

			Plan(_step, new Item[0], new Item[0]).Last().Text.Should().Be("Roubles: Missing ₽ 25,000: none left in the stash");
		}

		[Fact]
		public void Euros_and_roubles_are_separate_groups()
		{
			Only(_step, "cash_rub", 25000);
			_step.Groups.First(def => def.Id == "cash_eur").Setting.Target = 500;

			var lines = Plan(_step, new Item[0], new[] { Money(Roubles, 30000), Money(Euros, 1000) });

			Texts(lines).Should().Equal("Roubles: ₽ 0/25000", "Bring ₽ 25,000 to Pockets", "Euros: € 0/500", "Bring € 500 to Pockets");
		}

		[Fact]
		public void Money_goes_to_the_first_place_that_is_on()
		{
			var def = Only(_step, "cash_rub", 25000);
			def.TogglePlace("Vest");

			Texts(Plan(_step, new Item[0], new[] { Money(Roubles, 30000) })).Last().Should().Be("Bring ₽ 25,000 to Pockets");
		}

		[Fact]
		public void Money_goes_to_another_place_when_it_is_first_in_the_row()
		{
			var def = Only(_step, "cash_rub", 25000);
			def.TogglePlace("Vest");
			def.SwapPlaces("Pockets", "Vest");

			Texts(Plan(_step, new Item[0], new[] { Money(Roubles, 30000) })).Last().Should().Be("Bring ₽ 25,000 to Vest");
		}

		[Fact]
		public void Money_with_no_place_switched_on_is_reported_not_a_crash()
		{
			var def = Only(_step, "cash_rub", 25000);
			def.TogglePlace("Pockets");

			Plan(_step, new Item[0], new[] { Money(Roubles, 30000) }).Last().Problem.Should().BeTrue();
		}

		[Fact]
		public void A_case_that_stops_all_cash_stops_it_and_says_why()
		{
			Only(_step, "cash_rub", 25000);

			var lines = _step.Plan(null, new List<Item>(), new List<Item> { Money(Roubles, 30000) }, currency => new CaseEntry { CaseName = "Wallet", CaseTemplate = Body.Wallet, StopsCash = true });

			lines.Should().HaveCount(2);
			lines[0].Skipped.Should().BeTrue();
			lines[0].Note.Should().Be("Cash step skipped while you carry");
			lines[0].By.CaseName.Should().Be("Wallet");
			lines[1].Text.Should().Be("Would bring ₽ 25,000, but the Cash step is skipped while you carry Wallet");
			lines[1].Problem.Should().BeFalse();
			lines[1].Run.Should().BeNull();
		}

		[Fact]
		public void A_skipped_cash_step_says_so_even_when_every_place_is_switched_off()
		{
			var def = Only(_step, "cash_rub", 25000);
			foreach (var place in def.PlaceOrder().Where(def.PlaceOn).ToList())
			{
				def.TogglePlace(place);
			}

			var lines = _step.Plan(null, new List<Item>(), new List<Item> { Money(Roubles, 30000) }, currency => new CaseEntry { CaseName = "Wallet", StopsCash = true });

			lines.Should().NotContain(line => line.Problem);
			lines[0].Skipped.Should().BeTrue();
		}

		[Fact]
		public void Without_a_skipping_case_no_place_switched_on_is_still_a_problem()
		{
			var def = Only(_step, "cash_rub", 25000);
			foreach (var place in def.PlaceOrder().Where(def.PlaceOn).ToList())
			{
				def.TogglePlace(place);
			}

			Plan(_step, new Item[0], new[] { Money(Roubles, 30000) }).Should().Contain(line => line.Problem && line.Detail.StartsWith("every place is switched off"));
		}

		[Fact]
		public void Money_is_shown_with_the_sign_the_game_uses_and_other_units_after_the_number()
		{
			Game.Amount("RUB", 75000).Should().Be("₽ 75,000");
			Game.Amount("EUR", 250).Should().Be("€ 250");
			Game.Amount("USD", 4324).Should().Be("$ 4,324");
			Game.Amount("HP", 300).Should().Be("300 HP");
			Game.Ratio(10, 25, "RUB").Should().Be("₽ 10/25");
			Game.Ratio(2, 5, "bandage points").Should().Be("2/5 bandage points");
			Game.Symbol("HP").Should().BeNull();
		}

		[Fact]
		public void The_money_a_case_holds_counts_and_only_the_rest_is_brought()
		{
			Only(_step, "cash_rub", 100000);
			Steps.CaseMoney["RUB"] = 77230;

			var lines = Plan(_step, new Item[0], new[] { Money(Roubles, 150000) });

			lines[0].Text.Should().Be("Roubles: ₽ 77230/100000");
			lines[0].Skipped.Should().BeFalse();
			lines.Last().Text.Should().Be("Bring ₽ 22,770 to Pockets");
		}

		[Fact]
		public void A_case_that_holds_the_whole_target_leaves_nothing_to_bring()
		{
			Only(_step, "cash_rub", 100000);
			Steps.CaseMoney["RUB"] = 100000;

			Plan(_step, new Item[0], new[] { Money(Roubles, 150000) }).Should().ContainSingle().Which.Text.Should().Be("Roubles: ₽ 100000/100000");
		}

		[Fact]
		public void The_money_a_case_holds_of_another_currency_is_not_counted()
		{
			Only(_step, "cash_rub", 25000);
			Steps.CaseMoney["EUR"] = 900;

			Texts(Plan(_step, new Item[0], new[] { Money(Roubles, 30000) })).Last().Should().Be("Bring ₽ 25,000 to Pockets");
		}

		[Fact]
		public void Money_a_case_gives_up_is_brought_on_from_the_stash_in_the_same_run()
		{
			Only(_step, "cash_rub", 100000);
			Steps.Released["RUB"] = 77230;

			// the stash holds only 20,000 now: the rest is what the case is about to put there
			var lines = Plan(_step, new Item[0], new[] { Money(Roubles, 20000) });

			Texts(lines).Should().Equal("Roubles: ₽ 0/100000", "Bring ₽ 20,000 to Pockets", "Bring ₽ 77,230 to Pockets", "Roubles: Missing ₽ 2,770: none left in the stash");
			lines[2].Run.Should().NotBeNull();
			lines.Last().Problem.Should().BeTrue();
		}

		[Fact]
		public void Released_money_is_brought_only_as_far_as_the_target_needs()
		{
			Only(_step, "cash_rub", 50000);
			Steps.Released["RUB"] = 77230;

			Texts(Plan(_step, new Item[0], new Item[0])).Should().Equal("Roubles: ₽ 0/50000", "Bring ₽ 50,000 to Pockets");
		}

		[Fact]
		public void Locked_money_is_not_brought()
		{
			Only(_step, "cash_rub", 25000);
			var stack = Money(Roubles, 30000);
			stack.PinLockState = EItemPinLockState.Locked;

			Plan(_step, new Item[0], new[] { stack }).Last().Text.Should().Contain("1 locked");
		}

		[Fact]
		public void The_message_about_places_you_do_not_carry_talks_about_what_was_set_up()
		{
			SuppliesStep.NoPlaceOnYou(new[] { "Pockets" }).Should().Be("set to bring to Pockets, which you are not carrying: carry one, or add another place under \"Bring to\" in Setup");
			SuppliesStep.NoPlaceOnYou(new[] { "Vest", "Backpack" }).Should().Be("set to bring to Vest, Backpack, none of which you are carrying: carry one, or add another place under \"Bring to\" in Setup");
		}
	}
}
