using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.RealItems;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The plan of the Medicine to bring step: what is carried counts, the stash fills the rest in order, and what can't be filled is said.
	public class MedicineToBringTests
	{
		private readonly HealingStep _step = new HealingStep();

		public MedicineToBringTests()
		{
			Reset();
		}

		private GroupDef Bandages(int target) => Only(_step, "bandage", target);

		private static Item[] Items(params string[] templates) => templates.Select(template => Any(template)).ToArray();

		[Fact]
		public void Nothing_is_planned_when_no_group_has_a_target()
		{
			foreach (var def in _step.Groups)
			{
				def.Setting.Target = 0;
			}

			Plan(_step, new Item[0], Items(ArmyBandage)).Should().BeEmpty();
		}

		[Fact]
		public void The_progress_line_says_what_is_carried_out_of_the_target()
		{
			Bandages(2);

			var lines = Plan(_step, new Item[0], new Item[0]);

			lines[0].IsProgress.Should().BeTrue();
			lines[0].Text.Should().Be("Bandages: 0/2 bandage points");
		}

		[Fact]
		public void One_Army_bandage_covers_a_target_of_two()
		{
			Bandages(2);

			var lines = Plan(_step, new Item[0], Items(ArmyBandage, ArmyBandage));

			Texts(lines).Should().Equal("Bandages: 0/2 bandage points", "Bring 1x Army bandage");
		}

		[Fact]
		public void What_is_carried_counts_towards_the_target()
		{
			Bandages(2);

			var lines = Plan(_step, Items(ArmyBandage), Items(ArmyBandage));

			Texts(lines).Should().Equal("Bandages: 2/2 bandage points");
		}

		[Fact]
		public void Part_of_the_target_carried_only_the_rest_is_brought()
		{
			Bandages(2);

			var lines = Plan(_step, Items(AsepticBandage), Items(AsepticBandage, AsepticBandage));

			Texts(lines).Should().Equal("Bandages: 1/2 bandage points", "Bring 1x Aseptic bandage");
		}

		[Fact]
		public void Items_are_brought_in_the_order_of_the_group()
		{
			var def = Bandages(2);
			def.Setting.Order = new List<string> { AsepticBandage, ArmyBandage };

			// No single item is left to fill it alone here, so the ranking decides
			var lines = Plan(_step, new Item[0], Items(AsepticBandage, AsepticBandage));

			Texts(lines).Should().Equal("Bandages: 0/2 bandage points", "Bring 2x Aseptic bandage");
		}

		[Fact]
		public void An_item_that_is_switched_off_is_not_brought()
		{
			var def = Bandages(2);
			def.ToggleEntry(ArmyBandage);

			var lines = Plan(_step, new Item[0], Items(ArmyBandage, AsepticBandage, AsepticBandage));

			Texts(lines).Should().Equal("Bandages: 0/2 bandage points", "Bring 2x Aseptic bandage");
		}

		[Fact]
		public void When_the_stash_has_too_little_it_brings_what_it_has_and_says_what_is_missing()
		{
			Bandages(6);

			var lines = Plan(_step, new Item[0], Items(ArmyBandage, ArmyBandage));

			Texts(lines).Should().Equal("Bandages: 0/6 bandage points", "Bring 2x Army bandage", "Bandages: Missing 2 bandage points: none left in the stash");
			lines.Last().Problem.Should().BeTrue();
		}

		[Fact]
		public void An_empty_stash_says_none_left()
		{
			Bandages(2);

			Texts(Plan(_step, new Item[0], new Item[0])).Should().Equal("Bandages: 0/2 bandage points", "Bandages: Missing 2 bandage points: none left in the stash");
		}

		[Fact]
		public void Locked_items_are_not_brought_and_are_counted_in_the_message()
		{
			Bandages(2);
			var locked = Items(ArmyBandage);
			locked[0].PinLockState = EItemPinLockState.Locked;

			var lines = Plan(_step, new Item[0], locked);

			lines.Last().Text.Should().Be("Bandages: Missing 2 bandage points: none left in the stash that you haven't locked (1 locked)");
		}

		[Fact]
		public void A_group_with_every_item_switched_off_says_so()
		{
			var def = Bandages(2);
			def.ToggleEntry(ArmyBandage);
			def.ToggleEntry(AsepticBandage);

			Plan(_step, new Item[0], Items(ArmyBandage)).Last().Text.Should().Contain("every item of this group is switched off");
		}

		[Fact]
		public void A_bigger_item_is_taken_when_only_a_little_is_missing_if_the_option_is_on()
		{
			Bandages(1).Setting.AllowOvershoot = true;

			Texts(Plan(_step, new Item[0], Items(ArmyBandage))).Should().Equal("Bandages: 0/1 bandage points", "Bring 1x Army bandage");
		}

		[Fact]
		public void Two_copies_are_never_brought_when_one_copy_fills_the_need()
		{
			Bandages(2).Setting.AllowOvershoot = true;
			var part = Any(ArmyBandage);
			part.GetItemComponent<MedKitComponent>().HpResource = 1;
			var full = Any(ArmyBandage);

			Plan(_step, new Item[0], new[] { part, full }).Where(line => line.Run != null).Should().ContainSingle()
				.Which.Text.Should().Be("Bring 1x Army bandage");
		}

		[Fact]
		public void The_ranking_still_decides_between_different_items_when_a_later_one_would_fill_it_alone()
		{
			var def = Bandages(2);
			def.Setting.Order = new List<string> { AsepticBandage, ArmyBandage };

			Texts(Plan(_step, new Item[0], Items(AsepticBandage, ArmyBandage))).Should().Contain("Bring 1x Aseptic bandage");
		}

		[Fact]
		public void A_bigger_item_is_not_taken_when_the_option_is_off_and_the_message_names_it()
		{
			Bandages(1).Setting.AllowOvershoot = false;

			var lines = Plan(_step, new Item[0], Items(ArmyBandage));

			lines.Should().NotContain(line => line.Run != null);
			lines.Last().Text.Should().Contain("the smallest in the stash, Army bandage (2), is more than is missing").And.Contain("is off");
		}

		[Fact]
		public void The_limit_of_an_item_is_kept_until_nothing_else_can_make_up_the_amount()
		{
			var def = Bandages(4);
			def.SetMax(ArmyBandage, 1);

			var lines = Plan(_step, new Item[0], Items(ArmyBandage, ArmyBandage));

			Texts(lines).Should().Equal("Bandages: 0/4 bandage points", "Bring 2x Army bandage (over your limit of 1, nothing else is left)");
		}

		[Fact]
		public void The_limit_makes_the_plan_take_another_item_first()
		{
			var def = Bandages(4);
			def.SetMax(ArmyBandage, 1);

			var lines = Plan(_step, new Item[0], Items(ArmyBandage, ArmyBandage, AsepticBandage, AsepticBandage));

			Texts(lines).Should().Equal("Bandages: 0/4 bandage points", "Bring 1x Army bandage", "Bring 2x Aseptic bandage");
		}

		[Fact]
		public void Items_claimed_by_an_earlier_step_are_not_taken()
		{
			Bandages(2);
			var stash = Items(ArmyBandage);
			Steps.Claimed.Add(stash[0]);

			Plan(_step, new Item[0], stash).Last().Text.Should().Contain("none left in the stash");
		}

		[Fact]
		public void Each_group_is_planned_on_its_own_and_in_order()
		{
			foreach (var def in _step.Groups)
			{
				def.Setting.Target = 0;
			}

			_step.Groups.First(def => def.Id == "heavybleed").Setting.Target = 1;
			_step.Groups.First(def => def.Id == "bandage").Setting.Target = 1;
			foreach (var def in _step.Groups)
			{
				def.Setting.AllowOvershoot = false;
			}

			var lines = Plan(_step, new Item[0], Items(AsepticBandage, Cat));

			Texts(lines).Should().Equal("Bandages: 0/1 bandage points", "Bring 1x Aseptic bandage", "Heavy bleed: 0/1 tourniquet points", "Bring 1x CAT");
		}

		// ---- the order the stash is taken in

		private static List<Item> Candidates(GroupDef def, params Item[] stash) => def.Candidates(stash.ToList()).ToList();

		[Fact]
		public void Loose_items_come_before_items_in_a_container()
		{
			var def = Bandages(2);
			var inCase = Any(AsepticBandage);
			var loose = Any(AsepticBandage);
			Game.IsInsideContainer = item => item == inCase;

			Candidates(def, inCase, loose).Should().Equal(loose, inCase);
		}

		[Fact]
		public void A_fuller_copy_in_a_container_is_brought_instead_of_a_loose_partly_used_one()
		{
			var def = Only(_step, "heavybleed", 2);
			def.Setting.ReplaceWorse = true;
			def.Setting.AllowOvershoot = true;
			var loose = Any(CalokB);
			loose.GetItemComponent<MedKitComponent>().HpResource = 2;
			var inCase = Any(CalokB);
			inCase.GetItemComponent<MedKitComponent>().HpResource = 3;
			Game.IsInsideContainer = item => item == inCase;

			var lines = Plan(_step, new Item[0], new[] { loose, inCase });

			Texts(lines).Should().Contain("Bring 1x CALOK-B");
			lines.Should().ContainSingle(line => line.Text.StartsWith("Bring"));
			Plan(_step, new[] { inCase }, new[] { loose }).Should().NotContain(line => line.Text.StartsWith("Replace"));
		}

		[Fact]
		public void Non_FiR_comes_before_FiR_and_then_loose_before_container()
		{
			var def = Bandages(2);
			var firLoose = Any(AsepticBandage);
			firLoose.SpawnedInSession = true;
			var plainInCase = Any(AsepticBandage);
			Game.IsInsideContainer = item => item == plainInCase;

			Candidates(def, firLoose, plainInCase).Should().Equal(plainInCase, firLoose);
		}

		[Fact]
		public void With_the_option_off_loose_comes_before_a_container_whatever_FiR_is()
		{
			var def = Bandages(2);
			SettingsStore.Current.Supplies.NonFiRBeforeLoose = false;
			var firLoose = Any(AsepticBandage);
			firLoose.SpawnedInSession = true;
			var plainInCase = Any(AsepticBandage);
			Game.IsInsideContainer = item => item == plainInCase;

			Candidates(def, plainInCase, firLoose).Should().Equal(firLoose, plainInCase);
		}

		[Fact]
		public void The_fullest_resource_comes_first_for_the_same_item()
		{
			var def = _step.Groups.First(group => group.Id == "health");
			var half = Any(Salewa);
			half.GetItemComponent<MedKitComponent>().HpResource = 100;
			var full = Any(Salewa);

			Candidates(def, half, full).Should().Equal(full, half);
		}

		[Fact]
		public void Locked_items_are_not_candidates()
		{
			var def = Bandages(2);
			var locked = Any(AsepticBandage);
			locked.PinLockState = EItemPinLockState.Locked;

			Candidates(def, locked, Any(AsepticBandage)).Should().HaveCount(1).And.NotContain(locked);
		}

		// ---- replacing worse carried items by better ones

		[Fact]
		public void A_used_up_kit_is_replaced_by_a_full_one_from_the_stash()
		{
			var def = Only(_step, "health", 400);
			var worn = Any(Salewa);
			worn.GetItemComponent<MedKitComponent>().HpResource = 100;
			var full = Any(Salewa);

			var lines = Plan(_step, new[] { worn }, new[] { full });

			lines.Should().Contain(line => line.Text == "Replace Salewa (100 left) with Salewa (400 left)");
		}

		[Fact]
		public void Nothing_is_replaced_when_the_row_is_set_not_to()
		{
			Only(_step, "health", 400).Setting.ReplaceWorse = false;
			var worn = Any(Salewa);
			worn.GetItemComponent<MedKitComponent>().HpResource = 100;

			var lines = Plan(_step, new[] { worn }, new[] { Any(Salewa) });

			lines.Should().NotContain(line => line.Text.StartsWith("Replace"));
		}

		[Fact]
		public void A_carried_FiR_kit_is_swapped_for_an_equally_full_non_FiR_one()
		{
			Only(_step, "health", 400);
			var carried = Any(Salewa);
			carried.SpawnedInSession = true;

			Plan(_step, new[] { carried }, new[] { Any(Salewa) }).Should().Contain(line => line.Text.StartsWith("Replace"));
		}

		[Fact]
		public void With_the_swap_option_off_a_carried_FiR_kit_stays_on_a_tie()
		{
			Only(_step, "health", 400);
			SettingsStore.Current.Supplies.SwapFiRForNonFiR = false;
			var carried = Any(Salewa);
			carried.SpawnedInSession = true;

			Plan(_step, new[] { carried }, new[] { Any(Salewa) }).Should().NotContain(line => line.Text.StartsWith("Replace"));
		}

		[Fact]
		public void With_the_swap_option_off_a_fuller_kit_still_replaces_a_FiR_one()
		{
			Only(_step, "health", 400);
			SettingsStore.Current.Supplies.SwapFiRForNonFiR = false;
			var carried = Any(Salewa);
			carried.SpawnedInSession = true;
			carried.GetItemComponent<MedKitComponent>().HpResource = 100;

			Plan(_step, new[] { carried }, new[] { Any(Salewa) }).Should().Contain(line => line.Text.StartsWith("Replace"));
		}

		[Fact]
		public void A_kit_that_already_meets_the_target_is_not_replaced()
		{
			Only(_step, "health", 300);

			var lines = Plan(_step, new[] { Any(Salewa) }, new[] { Any(Salewa) });

			lines.Should().NotContain(line => line.Text.StartsWith("Replace"));
		}

		// ---- splints with fewer uses than the vanilla five

		private static Item Splint(float uses)
		{
			var splint = Any(AluSplint);
			splint.GetItemComponent<MedKitComponent>().HpResource = uses;
			return splint;
		}

		[Fact]
		public void The_splint_amount_can_be_set_one_use_at_a_time()
		{
			var def = _step.Groups.First(group => group.Id == "splint");

			def.Step.Should().Be(1);
			def.DefaultTarget.Should().Be(5);
			def.Max.Should().Be(50);
		}

		[Fact]
		public void A_splint_counts_for_the_uses_it_has_left()
		{
			var def = _step.Groups.First(group => group.Id == "splint");

			def.WeightOf(Splint(3)).Should().Be(3);
			def.WeightOf(Splint(5)).Should().Be(5);
		}

		[Fact]
		public void Six_uses_of_three_use_splints_bring_two_of_them()
		{
			Only(_step, "splint", 6);

			Texts(Plan(_step, new Item[0], new[] { Splint(3), Splint(3), Splint(3) })).Should().Equal("Splints: 0/6 splint uses", "Bring 2x Alu splint");
		}
	}
}
