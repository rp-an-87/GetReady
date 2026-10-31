using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// How the Ammo step is shown: one panel per equipped weapon, the weapons of one caliber together with the spare ammo they share,
	// and what belongs to no weapon left as it was. The lines come from the real step; the panels only arrange them.
	public class AmmoPanelsTests
	{
		private readonly World _world;
		private readonly AmmoStep _step = new AmmoStep();

		public AmmoPanelsTests()
		{
			Reset();
			_world = new World();
			AmmoStep.Spare.Setting.Target = 0;
		}

		private AmmoPanels.Layout Layout() => AmmoPanels.Build(_step.Plan(null));

		private Ammo InStash(string template, int count) => NewAmmo(template, count).In(_world.StashBox);

		private Weapon Rifle(int rounds, string ammo = M855, bool chambered = true)
		{
			var weapon = NewWeapon().With(NewMagazine().Loaded(ammo, rounds));
			return _world.Equip(chambered ? weapon.Chambered(ammo) : weapon);
		}

		private Weapon Pistol(int rounds) => _world.Equip(NewWeapon(Glock18).With(NewMagazine(GlockMag).Loaded(Pst9x19, rounds)));

		// ---- one panel per weapon

		[Fact]
		public void One_weapon_is_one_panel_with_its_fill_in_it()
		{
			InStash(M855, 100);
			Rifle(10);

			var layout = Layout();

			layout.Groups.Should().HaveCount(1);
			var panel = layout.Groups[0].Weapons.Single();
			panel.Name.Should().Be("M4A1");
			panel.Head.Text.Should().Be("M4A1: 10/30 rounds");
			panel.Lines.Should().ContainSingle(line => line.Fill && line.Run != null);
			layout.Rest.Should().BeEmpty();
			layout.Notes.Should().BeEmpty();
		}

		[Fact]
		public void Two_weapons_of_the_same_model_are_two_panels_in_one_group()
		{
			InStash(M855, 100);
			Rifle(10);
			Rifle(20);

			var group = Layout().Groups.Should().ContainSingle().Subject;

			group.Weapons.Should().HaveCount(2);
			group.Weapons[0].Head.Text.Should().Be("M4A1: 10/30 rounds");
			group.Weapons[1].Head.Text.Should().Be("M4A1: 20/30 rounds");
		}

		[Fact]
		public void Weapons_of_different_calibers_are_different_groups()
		{
			InStash(M855, 100);
			Rifle(10);
			Pistol(5);

			var layout = Layout();

			layout.Groups.Should().HaveCount(2);
			layout.Groups.Select(group => group.Weapons.Single().Name).Should().Equal("M4A1", "Glock 18C");
		}

		[Fact]
		public void A_weapon_with_no_magazine_is_a_panel_with_what_is_wrong_and_nothing_else_in_it()
		{
			_world.Equip(NewWeapon());

			var panel = Layout().Groups.Single().Weapons.Single();

			panel.Head.Should().BeNull();
			panel.Lines.Should().ContainSingle(line => line.Problem && line.Detail.Contains("no magazine in it"));
		}

		// ---- the spare ammo of a caliber

		[Fact]
		public void The_spare_ammo_sits_in_the_group_of_its_weapon_not_in_the_next_one()
		{
			InStash(M855, 400);
			InStash(Pst9x19, 400);
			Rifle(30);
			Pistol(17);
			AmmoStep.Spare.Setting.Target = 1;

			var layout = Layout();

			layout.Groups.Should().HaveCount(2);
			layout.Groups[0].SpareHead.Subject.Should().Be("Spare ammo for M4A1");
			layout.Groups[1].SpareHead.Subject.Should().Be("Spare ammo for Glock 18C");
			layout.Groups[0].Spare.Should().ContainSingle(line => line.Run != null && line.Text.Contains("for M4A1"));
			layout.Groups[1].Spare.Should().ContainSingle(line => line.Run != null && line.Text.Contains("for Glock 18C"));
		}

		[Fact]
		public void Weapons_that_share_a_caliber_show_the_spare_ammo_once_and_without_the_repeats()
		{
			InStash(M855, 400);
			Rifle(30);
			Rifle(30);
			AmmoStep.Spare.Setting.Target = 1;

			var group = Layout().Groups.Should().ContainSingle().Subject;

			group.Weapons.Should().HaveCount(2);
			group.SpareHead.Should().NotBeNull();
			group.Spare.Should().ContainSingle(line => line.Run != null);
			group.Spare.Should().NotContain(line => line.Covered);
		}

		[Fact]
		public void A_problem_with_the_shared_spare_ammo_is_shown_once()
		{
			Rifle(30);
			Rifle(30);
			AmmoStep.Spare.Setting.Target = 1;

			var group = Layout().Groups.Should().ContainSingle().Subject;

			group.Spare.Where(line => line.Problem).Should().HaveCount(1);
		}

		// ---- nothing is lost

		[Fact]
		public void Every_line_of_the_step_is_shown_once_but_the_repeats_of_a_shared_card()
		{
			InStash(M855, 400);
			InStash(Pst9x19, 400);
			Rifle(10);
			Rifle(20);
			Pistol(5);
			NewMagazine().Loaded(M855, 10).In(_world.Vest);
			AmmoStep.Spare.Setting.Target = 1;
			var lines = _step.Plan(null);

			var layout = AmmoPanels.Build(lines);
			var shown = layout.Groups.SelectMany(group => group.All()).Concat(layout.Notes).Concat(layout.Rest).ToList();

			shown.Should().OnlyHaveUniqueItems();
			var left = lines.Except(shown).ToList();
			left.Should().OnlyContain(line => line.Covered || line.Problem || line.IsProgress && line.Unit == AmmoPanels.SpareUnit);
		}

		// ---- what is not about a weapon

		[Fact]
		public void The_spare_magazines_and_the_loose_stacks_stay_outside_the_weapon_panels()
		{
			InStash(M855, 100);
			Rifle(30);
			NewMagazine().Loaded(M855, 10).In(_world.Vest);
			NewAmmo(M855, 10).In(_world.Backpack);

			var layout = Layout();

			layout.Groups.Should().HaveCount(1);
			layout.Groups[0].All().Should().NotContain(line => line.Subject == "Extra magazines on you" || line.Subject == "Loose ammo on you");
			layout.Rest.Where(line => line.IsProgress).Select(line => line.Subject).Should().Equal("Extra magazines on you", "Loose ammo on you");
		}

		[Fact]
		public void A_magazine_that_cannot_be_filled_stays_with_the_spare_magazines_not_in_a_weapon_panel()
		{
			Rifle(30);
			NewMagazine().Loaded(M855, 10).In(_world.Vest);

			var layout = Layout();

			layout.Groups.Single().All().Should().NotContain(line => line.Problem);
			layout.Rest.Should().Contain(line => line.Problem);
		}

		[Fact]
		public void An_empty_magazine_in_a_weapon_with_no_ammo_left_is_reported_beside_the_panels_not_in_one()
		{
			Rifle(0, chambered: false);

			var layout = Layout();

			layout.Groups.Single().Weapons.Single().Lines.Should().NotContain(line => line.Problem);
			layout.Rest.Should().ContainSingle(line => line.Problem);
		}

		// ---- the words

		[Theory]
		[InlineData("Bring 120 M855A1 for M4A1  (the ammo in use, from the stash)", "Bring 120 M855A1 for M4A1")]
		[InlineData("M4A1's magazine: 10/30  +20 M855  (same as inside, from the stash)  [taken out and put back]", "M4A1's magazine: 10/30  +20 M855")]
		[InlineData("Chamber M855 in M4A1  (same as its magazine, from the stash)", "Chamber M855 in M4A1")]
		[InlineData("Nothing to cut", "Nothing to cut")]
		[InlineData("", "")]
		public void The_why_and_the_from_where_are_left_for_the_tooltip(string text, string expected) => AmmoPanels.Short(text).Should().Be(expected);

		[Fact]
		public void Short_text_keeps_the_start_when_the_note_comes_first()
		{
			AmmoPanels.Short("A  [b]  (c)").Should().Be("A");
		}

		// ---- the slot the weapon is carried in

		[Theory]
		[InlineData(0, "Sling")]
		[InlineData(1, "Back")]
		[InlineData(2, "Holster")]
		public void The_panel_says_which_slot_the_weapon_is_in(int index, string expected)
		{
			var body = new Body();
			var world = new World(body);
			var weapons = new[] { world.Equip(NewWeapon()), world.Equip(NewWeapon()), world.Equip(NewWeapon(Glock18)) };

			AmmoPanels.SlotLabel(weapons[index]).Should().Be(expected);
		}

		[Fact]
		public void A_weapon_that_is_not_in_a_slot_has_no_label()
		{
			AmmoPanels.SlotLabel(NewWeapon()).Should().BeEmpty();
		}

		// ---- how a card stands

		private static PlanLine Bar(float have, float want) => Steps.Progress("x", have, want, "u");

		private static PlanLine Doing() => new PlanLine { Text = "Bring", Run = () => null };

		[Fact]
		public void A_card_with_full_bars_and_nothing_to_do_is_done()
		{
			ReadyWindow.StandingOfAll(new List<PlanLine> { Bar(30, 30) }).Should().Be(ReadyWindow.Standing.Done);
		}

		[Fact]
		public void A_card_that_get_ready_can_fill_is_doable()
		{
			ReadyWindow.StandingOfAll(new List<PlanLine> { Bar(10, 30), Doing() }).Should().Be(ReadyWindow.Standing.Doable);
		}

		[Fact]
		public void A_problem_beats_what_can_be_done()
		{
			ReadyWindow.StandingOfAll(new List<PlanLine> { Bar(10, 30), Doing(), Steps.Problem("x", "wrong") }).Should().Be(ReadyWindow.Standing.Problem);
		}

		[Fact]
		public void A_card_that_is_not_full_and_has_nothing_to_do_is_a_problem()
		{
			ReadyWindow.StandingOfAll(new List<PlanLine> { Bar(10, 30) }).Should().Be(ReadyWindow.Standing.Problem);
		}

		[Fact]
		public void The_caliber_is_named_by_the_game_or_by_its_key_without_the_prefix()
		{
			Game.Localize = key => key == "Caliber556x45NATO" ? "5.56x45mm NATO" : key;

			ReadyWindow.CaliberName("Caliber556x45NATO").Should().Be("5.56x45mm NATO");
			ReadyWindow.CaliberName("Caliber9x19PARA").Should().Be("9x19");
			ReadyWindow.CaliberName("").Should().BeEmpty();
		}

		// ---- what the rows are made of

		[Fact]
		public void The_fill_line_says_how_many_rounds_of_what_it_adds()
		{
			InStash(M855, 100);
			Rifle(10);

			var fill = Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Run != null);

			fill.Part.Should().Be("magazine");
			fill.Amount.Should().Be(20);
			fill.What.Should().NotBeNullOrEmpty();
			fill.Text.Should().Contain("+20 " + fill.What);
			fill.Template.Should().Be(M855);
		}

		[Fact]
		public void A_chamber_to_fill_is_its_own_part()
		{
			InStash(M855, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)));

			var chamber = Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Part == "chamber");

			chamber.What.Should().NotBeNullOrEmpty();
			chamber.Text.Should().StartWith("Chamber " + chamber.What);
			chamber.Run.Should().NotBeNull();
		}

		[Fact]
		public void A_magazine_that_goes_in_is_its_own_part()
		{
			NewMagazine().Loaded(M855, 30).In(_world.Vest);
			_world.Equip(NewWeapon());

			var put = Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Part == "put");

			put.What.Should().Be("STANAG 30");
			put.Place.Should().Be("Vest");
			put.PlaceTemplate.Should().Be(Body.Vest);
			put.Have.Should().Be(30);
			put.Want.Should().Be(30);
			put.Run.Should().NotBeNull();
		}

		[Fact]
		public void The_problem_of_a_weapon_with_no_magazine_is_about_the_magazine()
		{
			_world.Equip(NewWeapon());

			Layout().Groups.Single().Weapons.Single().Lines.Single().Part.Should().Be("magazine");
		}

		[Fact]
		public void The_ammo_to_bring_for_spare_says_how_much_of_what()
		{
			InStash(M855, 200);
			Rifle(30);
			AmmoStep.Spare.Setting.Target = 1;

			var bring = Layout().Groups.Single().Spare.Single(line => line.Run != null);

			bring.Part.Should().Be("spare");
			bring.Amount.Should().Be(60);
			bring.What.Should().Be("M855");
			bring.Template.Should().Be(M855);
		}

		[Fact]
		public void Spare_ammo_you_already_carry_says_which_ammo_it_is()
		{
			InStash(M855, 200);
			Rifle(30);
			NewAmmo(M855, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;

			var head = Layout().Groups.Single().SpareHead;

			head.What.Should().Be("M855");
			head.WhatTemplate.Should().Be(M855);
		}

		// ---- the bullet of the caliber

		[Fact]
		public void The_bullet_of_a_card_is_the_ammo_that_will_be_put_in()
		{
			InStash(M855, 100);
			Rifle(10);

			AmmoPanels.AmmoTemplateOf(Layout().Groups.Single()).Should().Be(M855);
		}

		[Fact]
		public void The_bullet_of_a_card_with_nothing_to_do_is_what_the_weapon_holds()
		{
			Rifle(30);

			AmmoPanels.AmmoTemplateOf(Layout().Groups.Single()).Should().Be(M855);
		}

		[Fact]
		public void A_card_with_no_ammo_anywhere_has_no_bullet()
		{
			_world.Equip(NewWeapon());

			AmmoPanels.AmmoTemplateOf(Layout().Groups.Single()).Should().BeNull();
		}

		[Theory]
		[InlineData("762x35", ".300 BLK")]
		[InlineData("Caliber556x45NATO", "5.56x45")]
		[InlineData("Caliber9x19PARA", "9x19")]
		[InlineData("Caliber77x77", "77x77")]
		[InlineData("", "")]
		public void The_caliber_is_called_by_its_usual_name(string key, string expected) => AmmoPanels.CaliberLabel(key).Should().Be(expected);

		// ---- the lines of the extra magazines and the loose stacks

		[Fact]
		public void The_extra_magazines_card_is_called_so_and_its_fill_says_which_magazine_gets_what()
		{
			InStash(M855, 100);
			Rifle(30);
			NewMagazine().Loaded(M855, 10).In(_world.Vest);

			var rest = Layout().Rest;

			rest.First().Subject.Should().Be("Extra magazines on you");
			var fill = rest.Single(line => line.Fill);
			fill.Label.Should().Be("STANAG 30");
			fill.Place.Should().Be("Vest");
			fill.PlaceTemplate.Should().Be(Body.Vest);
			fill.Have.Should().Be(10);
			fill.Want.Should().Be(30);
			fill.ItemTemplate.Should().Be(Stanag30);
			fill.Template.Should().Be(M855);
			fill.Amount.Should().Be(20);
		}

		[Fact]
		public void A_top_up_line_says_which_stack_goes_from_how_many_to_how_many()
		{
			InStash(M855, 100);
			Rifle(30);
			NewAmmo(M855, 10).In(_world.Backpack);

			var top = Layout().Rest.Single(line => line.Topped);

			top.Label.Should().Be("M855");
			top.Place.Should().NotBeNullOrEmpty();
			top.PlaceTemplate.Should().NotBeNullOrEmpty();
			top.Have.Should().Be(10);
			top.Want.Should().Be(60);
			top.Amount.Should().Be(50);
		}

		[Fact]
		public void The_spare_ammo_on_you_says_how_much_there_is_and_how_much_is_asked()
		{
			InStash(M855, 200);
			Rifle(30);
			NewAmmo(M855, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;

			var head = Layout().Groups.Single().SpareHead;

			head.Amount.Should().Be(60, "the 10 on you and the 50 the top-up of that stack adds");
			head.Asked.Should().Be(60);
		}

		[Fact]
		public void The_spare_ammo_to_bring_says_how_much_is_asked()
		{
			InStash(M855, 200);
			Rifle(30);
			AmmoStep.Spare.Setting.Target = 2;

			Layout().Groups.Single().Spare.Single(line => line.Run != null).Asked.Should().Be(120);
		}

		// ---- the icons and the short words of a weapon panel

		[Fact]
		public void The_magazine_of_a_panel_is_the_one_that_gets_filled()
		{
			InStash(M855, 100);
			Rifle(10);

			AmmoPanels.MagazineTemplateOf(Layout().Groups.Single().Weapons.Single()).Should().Be(Stanag30);
		}

		[Fact]
		public void The_magazine_of_a_full_panel_is_the_one_in_the_gun()
		{
			Rifle(30);

			AmmoPanels.MagazineTemplateOf(Layout().Groups.Single().Weapons.Single()).Should().Be(Stanag30);
		}

		[Fact]
		public void A_weapon_with_no_magazine_has_no_magazine_icon()
		{
			_world.Equip(NewWeapon());

			AmmoPanels.MagazineTemplateOf(Layout().Groups.Single().Weapons.Single()).Should().BeNull();
		}

		[Theory]
		[InlineData("no magazine in it, and none of the magazines you carry fits it", "No magazine in it, none you carry fits")]
		[InlineData("no magazine in it, and none of yours can be put in and filled for it", "No magazine in it, none of yours can be put in")]
		[InlineData("no magazine in it, and nothing tells which ammo to put in your empty magazines (M855A1, M995): load one of them yourself and the others follow", "No magazine in it: which ammo goes in an empty one?")]
		[InlineData("no magazine in it, and there is no ammo left to fill an empty magazine for it", "No magazine in it, no ammo left to fill one")]
		[InlineData("empty chamber, and none of the ammo in its magazine is left to chamber", "Nothing left to chamber")]
		[InlineData("no loose ammo on you, and nothing is loaded or chambered to tell which ammo to bring", "Can't tell which ammo to bring")]
		[InlineData("no loose ammo on you, and the stash has none of the ammo in use (M855)", "None of the ammo in use is in the stash")]
		[InlineData("something else", "something else")]
		[InlineData("", "")]
		public void A_problem_is_said_short_in_the_panel(string detail, string expected) => AmmoPanels.ShortProblem(detail).Should().Be(expected);

		[Fact]
		public void Every_problem_the_step_words_for_a_weapon_has_a_short_form()
		{
			Rifle(30);
			Pistol(5);
			_world.Equip(NewWeapon());
			AmmoStep.Spare.Setting.Target = 1;

			var problems = Layout().Groups.SelectMany(group => group.All()).Where(line => line.Problem && line.Detail != null).ToList();

			problems.Should().NotBeEmpty();
			problems.Should().OnlyContain(line => AmmoPanels.ShortProblem(line.Detail) != line.Detail);
		}

		[Fact]
		public void The_ammo_in_the_gun_is_listed_with_the_commonest_first()
		{
			var weapon = NewWeapon().With(NewMagazine().Loaded(M855, 20));
			weapon.GetCurrentMagazine().Cartridges.Add(NewAmmo(M995, 5), false);
			_world.Equip(weapon);

			var held = AmmoPanels.HeldAmmo(Layout().Groups.Single().Weapons.Single());

			held.Select(pair => pair.Key).Should().Equal(M855, M995);
		}

		[Fact]
		public void A_weapon_with_no_magazine_holds_no_ammo()
		{
			_world.Equip(NewWeapon());

			AmmoPanels.HeldAmmo(Layout().Groups.Single().Weapons.Single()).Should().BeEmpty();
		}

		[Fact]
		public void The_new_wording_for_magazines_with_the_wrong_ammo_has_a_short_form()
		{
			AmmoPanels.ShortProblem("no magazine in it, and the magazines you carry that fit it hold ammo of another caliber").Should().Be("No magazine in it, those that fit hold other ammo");
		}

		[Fact]
		public void A_fill_says_what_decided_the_ammo()
		{
			InStash(M855, 100);
			Rifle(10);

			Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Run != null && line.Part == "magazine").Why.Should().Be("same as inside");
		}

		[Fact]
		public void A_spare_magazine_filled_for_an_empty_weapon_says_why_that_ammo()
		{
			InStash(M855, 100);
			NewMagazine().In(_world.Vest);
			NewAmmo(M855, 10).In(_world.Vest);
			_world.Equip(NewWeapon());

			var fill = Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Fill && line.Run != null);

			fill.Why.Should().NotBeNullOrEmpty();
			fill.Text.Should().Contain(fill.Why);
		}

		// ---- where the ammo comes from

		[Fact]
		public void A_fill_says_which_containers_the_rounds_come_from_and_how_many()
		{
			InStash(M855, 100);
			NewAmmo(M855, 4).In(_world.Vest);
			Rifle(10);

			var from = Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Run != null && line.Part == "magazine").From;

			// In the stash screen the stash is used first (loose rounds on you stay where they are)
			from.Should().ContainSingle().Which.Name.Should().Be("Stash");
			from[0].Count.Should().Be(20);
			from[0].Template.Should().BeNull("the stash has no icon: the window writes its name");
		}

		[Fact]
		public void The_chamber_round_says_where_it_comes_from_and_why()
		{
			InStash(M855, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)));

			var chamber = Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Part == "chamber");

			chamber.Place.Should().Be("Stash");
			chamber.PlaceTemplate.Should().BeNull();
			chamber.Why.Should().Be("same as its magazine");
		}

		[Fact]
		public void The_extra_magazines_fill_says_where_the_rounds_come_from()
		{
			InStash(M855, 100);
			Rifle(30);
			NewMagazine().Loaded(M855, 10).In(_world.Vest);

			var fill = Layout().Rest.Single(line => line.Fill);

			fill.From.Should().ContainSingle(place => place.Name == "Stash" && place.Count == 20);
			fill.Why.Should().Be("same as inside");
		}

		[Fact]
		public void The_fill_of_a_magazine_in_the_gun_names_the_magazine_and_has_no_place()
		{
			InStash(M855, 100);
			Rifle(7);

			var fill = Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Run != null && line.Part == "magazine");

			fill.Label.Should().Be("STANAG 30");
			fill.ItemTemplate.Should().Be(Stanag30);
			fill.Place.Should().BeNull();
			fill.Have.Should().Be(7);
			fill.Want.Should().Be(30);
		}

		// ---- the tooltips of the lines that move ammo

		[Fact]
		public void The_tooltip_of_a_fill_says_what_changes_where_from_and_why_once()
		{
			InStash(M855, 100);
			NewMagazine().Loaded(M855, 10).In(_world.Vest);
			Rifle(30);

			var tip = AmmoPanels.Tip(Layout().Rest.Single(line => line.Fill));

			tip.Should().Contain("in Vest: 10/30 → 30/30").And.Contain("20 ").And.Contain("taken from the stash").And.Contain("Why this ammo: same as inside");
			tip.Split(new[] { "same as inside" }, System.StringSplitOptions.None).Length.Should().Be(2, "the reason is said once");
		}

		[Fact]
		public void The_tooltip_of_a_fill_in_the_gun_says_the_magazine_is_taken_out_and_put_back()
		{
			InStash(M855, 100);
			Rifle(10);

			var tip = AmmoPanels.Tip(Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Run != null && line.Part == "magazine"));

			tip.Should().Contain("10/30 → 30/30").And.Contain("taken out of the weapon, filled and put back");
			tip.Should().NotContain(" in ", "a magazine in the gun has no place");
		}

		[Fact]
		public void The_tooltip_names_each_place_with_its_rounds_when_there_are_several()
		{
			AmmoPanels.SourcesText(new List<PlaceCount> { new PlaceCount { Name = "Stash", Count = 20 }, new PlaceCount { Name = "Vest", Count = 4 } }).Should().Be("the stash (20), your Vest (4)");
			AmmoPanels.SourcesText(new List<PlaceCount> { new PlaceCount { Name = "Stash", Count = 20 } }).Should().Be("the stash");
			AmmoPanels.SourcesText(new List<PlaceCount>()).Should().BeEmpty();
		}

		[Fact]
		public void The_tooltip_of_a_top_up_says_it_is_topped_up_from_the_stash()
		{
			InStash(M855, 100);
			Rifle(30);
			NewAmmo(M855, 10).In(_world.Backpack);

			var tip = AmmoPanels.Tip(Layout().Rest.Single(line => line.Topped));

			tip.Should().Contain("10/60 → 60/60").And.Contain("Topped up with 50 from the stash");
		}

		[Fact]
		public void The_tooltip_of_the_chamber_says_where_the_round_is_taken_from_and_why()
		{
			InStash(M855, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)));

			var tip = AmmoPanels.Tip(Layout().Groups.Single().Weapons.Single().Lines.Single(line => line.Part == "chamber"));

			tip.Should().Contain("goes in the chamber, taken from the stash").And.Contain("Why this ammo: same as its magazine");
		}

		[Fact]
		public void A_line_that_moves_no_ammo_keeps_its_own_text_as_the_tooltip()
		{
			var line = new PlanLine { Text = "Put STANAG into M4A1", Part = "put" };

			AmmoPanels.Tip(line).Should().Be("Put STANAG into M4A1");
		}

		// ---- the chamber as it is now

		[Fact]
		public void A_chambered_round_is_shown_whatever_it_is()
		{
			Rifle(30, chambered: false);
			_world.Weapons[0].Chambered(M995);

			AmmoPanels.ChamberedAmmo(Layout().Groups.Single().Weapons.Single()).Should().Be(new KeyValuePair<string, string>(M995, Game.NameOf(NewAmmo(M995, 1))));
		}

		[Fact]
		public void An_empty_chamber_shows_nothing_but_the_weapon_has_one()
		{
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)));
			AmmoStep_ChamberOff();

			var panel = Layout().Groups.Single().Weapons.Single();

			AmmoPanels.ChamberedAmmo(panel).Should().BeNull();
			AmmoPanels.HasChamber(panel).Should().BeTrue();
		}

		private static void AmmoStep_ChamberOff() => SettingsStore.Current.Ammo.Chamber = false;
	}
}
