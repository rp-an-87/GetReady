using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The special slots of the pockets: saving what is in them, and putting it back from the stash.
	public class SpecialSlotsTests
	{
		private readonly Body _body;
		private readonly SpecialSlotsStep _step = new SpecialSlotsStep();

		public SpecialSlotsTests()
		{
			Reset();
			_body = new Body();
		}

		private Slot Special(int number) => SpecialSlotsStep.SlotsOf(_body.Inventory).First(slot => slot.ID == "SpecialSlot" + number);

		private Item Put(int number, string template, int stack = 1)
		{
			var item = RealItems.Any(template, stack);
			var result = Special(number).Add(item, false);
			result.Succeeded.Should().BeTrue(result.Error?.ToString());
			return item;
		}

		private static SlotEntry Wants(int number, int count, params string[] templates)
		{
			var entry = new SlotEntry { SlotId = "SpecialSlot" + number, Count = count };
			foreach (var template in templates)
			{
				entry.Templates.Add(new ItemRef { Template = template, Name = Game.TemplateName(template) });
			}

			return entry;
		}

		private void Saved(params SlotEntry[] entries) => SettingsStore.Current.SpecialSlots.Slots.AddRange(entries);

		private List<PlanLine> Plan() => _step.Plan(_body.Controller);

		private static async Task<string> RunAll(IEnumerable<PlanLine> lines)
		{
			foreach (var line in lines.Where(line => line.Run != null))
			{
				var error = await line.Run();
				if (error != null)
				{
					return error;
				}
			}

			return null;
		}

		// ---- the slots

		[Fact]
		public void The_special_slots_are_the_slots_of_the_pockets()
		{
			SpecialSlotsStep.SlotsOf(_body.Inventory).Select(slot => slot.ID).Should().Equal("SpecialSlot1", "SpecialSlot2", "SpecialSlot3");
		}

		[Fact]
		public void Pockets_without_special_slots_have_none()
		{
			var plain = new Body(Body.PlainPockets);

			SpecialSlotsStep.SlotsOf(plain.Inventory).Should().BeEmpty();
		}

		// ---- saving

		[Fact]
		public void Saving_records_what_each_slot_holds()
		{
			Put(1, RealItems.Cms);
			Put(3, RealItems.Surv12);

			SpecialSlotsStep.Capture(_body.Controller).Should().Be(2);

			var slots = SettingsStore.Current.SpecialSlots.Slots;
			slots.Select(entry => entry.SlotId).Should().Equal("SpecialSlot1", "SpecialSlot3");
			slots[0].Templates.Single().Template.Should().Be(RealItems.Cms);
			slots[0].Templates.Single().Name.Should().Be("CMS");
			slots[0].Count.Should().Be(1);
		}

		[Fact]
		public void Saving_replaces_what_was_saved_before()
		{
			Saved(Wants(2, 1, RealItems.Surv12));
			Put(1, RealItems.Cms);

			SpecialSlotsStep.Capture(_body.Controller);

			SettingsStore.Current.SpecialSlots.Slots.Select(entry => entry.SlotId).Should().Equal("SpecialSlot1");
		}

		[Fact]
		public void Saving_with_empty_slots_saves_nothing()
		{
			SpecialSlotsStep.Capture(_body.Controller).Should().Be(0);
		}

		[Fact]
		public void Clearing_forgets_the_template_and_leaves_the_slots_as_they_are()
		{
			var held = Put(1, RealItems.Cms);
			Saved(Wants(1, 1, RealItems.Cms), Wants(2, 1, RealItems.Surv12));

			SpecialSlotsStep.Clear();

			SettingsStore.Current.SpecialSlots.Slots.Should().BeEmpty();
			Special(1).ContainedItem.Should().BeSameAs(held);
			Plan().Should().ContainSingle().Which.Text.Should().StartWith("Nothing saved");
		}

		[Fact]
		public void After_clearing_nothing_is_brought_from_the_stash()
		{
			_body.InStash(RealItems.Any(RealItems.Cms));
			Saved(Wants(1, 1, RealItems.Cms));
			SpecialSlotsStep.Clear();

			Plan().Where(line => line.Run != null).Should().BeEmpty();
		}

		// ---- the plan

		[Fact]
		public void With_nothing_saved_the_plan_says_so()
		{
			var lines = Plan();

			lines.Should().ContainSingle().Which.Text.Should().StartWith("Nothing saved");
		}

		[Fact]
		public void A_slot_that_holds_what_was_saved_is_in_place()
		{
			Put(1, RealItems.Cms);
			Saved(Wants(1, 1, RealItems.Cms));

			Texts(Plan()).Should().Equal("Slots: 1/1 in place");
		}

		[Fact]
		public void An_empty_slot_gets_the_item_from_the_stash()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));
			Saved(Wants(1, 1, RealItems.Cms));

			var lines = Plan();

			Texts(lines).Should().Equal("Slots: 0/1 in place", "Bring 1x CMS (slot 1)");
			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Should().ContainSingle().Which.Item.Should().BeSameAs(cms);
			Body.Where(_body.Moves[0]).Should().Be("SpecialSlot1");
		}

		[Fact]
		public void Several_empty_slots_are_filled_one_by_one()
		{
			_body.InStash(RealItems.Any(RealItems.Cms));
			_body.InStash(RealItems.Any(RealItems.Surv12));
			Saved(Wants(1, 1, RealItems.Cms), Wants(2, 1, RealItems.Surv12));

			var lines = Plan();

			Texts(lines).Should().Equal("Slots: 0/2 in place", "Bring 1x CMS (slot 1)", "Bring 1x Surv12 (slot 2)");
		}

		[Fact]
		public void When_the_stash_has_none_the_plan_says_so()
		{
			Saved(Wants(1, 1, RealItems.Cms));

			var lines = Plan();

			lines[1].Problem.Should().BeTrue();
			lines[1].Subject.Should().Be("CMS");
			lines[1].Detail.Should().Contain("Missing 1: none left in the stash");
			lines[1].Run.Should().BeNull();
		}

		[Fact]
		public void The_alternatives_of_a_slot_are_tried_in_order()
		{
			var surv = _body.InStash(RealItems.Any(RealItems.Surv12));
			Saved(Wants(1, 1, RealItems.Cms, RealItems.Surv12));

			var lines = Plan();

			Texts(lines).Should().Equal("Slots: 0/1 in place", "Bring 1x Surv12 (slot 1)");
			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Single().Item.Should().BeSameAs(surv);
		}

		[Fact]
		public void The_first_alternative_wins_when_both_are_there()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));
			_body.InStash(RealItems.Any(RealItems.Surv12));
			Saved(Wants(1, 1, RealItems.Cms, RealItems.Surv12));

			RunAll(Plan()).Result.Should().BeNull();

			_body.Moves.Single().Item.Should().BeSameAs(cms);
		}

		[Fact]
		public void A_slot_holding_one_of_the_alternatives_is_in_place()
		{
			Put(1, RealItems.Surv12);
			Saved(Wants(1, 1, RealItems.Cms, RealItems.Surv12));

			Texts(Plan()).Should().Equal("Slots: 1/1 in place");
		}

		[Fact]
		public void A_slot_holding_something_else_is_a_problem_and_is_left_alone()
		{
			Put(1, RealItems.Surv12);
			Saved(Wants(1, 1, RealItems.Cms));

			var lines = Plan();

			lines.Should().Contain(line => line.Problem && line.Subject == "slot 1" && line.Detail == "holds Surv12, expected CMS");
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void A_slot_that_does_not_exist_on_your_pockets_is_a_problem()
		{
			var plain = new Body(Body.PlainPockets);
			Saved(Wants(1, 1, RealItems.Cms));

			var lines = Plan();

			lines.Should().Contain(line => line.Problem && line.Subject == "slot 1" && line.Detail == "this slot doesn't exist on your pockets");
		}

		[Fact]
		public void A_stack_smaller_than_saved_is_reported_not_topped_up()
		{
			Put(1, RealItems.RedFlare, 1);
			Saved(Wants(1, 3, RealItems.RedFlare));

			Plan().Should().Contain(line => line.Problem && line.Detail.StartsWith("holds 1x Red flare, expected 3"));
		}

		[Fact]
		public void A_locked_item_in_the_stash_is_not_brought()
		{
			_body.InStash(RealItems.Any(RealItems.Cms)).PinLockState = EItemPinLockState.Locked;
			Saved(Wants(1, 1, RealItems.Cms));

			Plan().Should().Contain(line => line.Problem && line.Detail.Contains("none left in the stash"));
		}

		[Fact]
		public void Something_another_step_already_claimed_is_not_taken()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));
			Steps.Claimed.Add(cms);
			Saved(Wants(1, 1, RealItems.Cms));

			Plan().Should().Contain(line => line.Problem && line.Detail.Contains("none left in the stash"));
		}

		[Fact]
		public void Two_slots_wanting_the_same_item_do_not_take_the_same_one()
		{
			_body.InStash(RealItems.Any(RealItems.Cms));
			Saved(Wants(1, 1, RealItems.Cms), Wants(2, 1, RealItems.Cms));

			var lines = Plan();

			lines.Count(line => line.Run != null).Should().Be(1);
			lines.Should().Contain(line => line.Problem && line.Detail.Contains("Missing 1"));
		}

		[Fact]
		public void What_is_brought_is_marked_for_the_other_steps()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));
			Saved(Wants(1, 1, RealItems.Cms));

			Plan();

			Steps.Claimed.Should().Contain(cms);
			Steps.Bringing.Should().Contain(cms);
		}

		[Fact]
		public void What_already_sits_in_a_slot_is_marked_as_the_slots_own()
		{
			var held = Put(1, RealItems.Cms);
			Saved(Wants(1, 1, RealItems.Cms));

			Plan();

			Steps.Owned.Should().Contain(held);
		}

		[Fact]
		public void A_refused_move_is_reported_by_the_line()
		{
			_body.InStash(RealItems.Any(RealItems.Cms));
			_body.Refuse = move => "no way";
			Saved(Wants(1, 1, RealItems.Cms));

			RunAll(Plan()).Result.Should().Be("no way");
		}

		// ---- a partly used kit

		[Fact]
		public void A_used_kit_is_swapped_for_a_fuller_one_from_the_stash()
		{
			var used = Put(1, RealItems.Cms);
			Use(used, 1);
			var fresh = _body.InStash(RealItems.Any(RealItems.Cms));
			Saved(Wants(1, 1, RealItems.Cms));

			var lines = Plan();

			lines.Select(line => line.Text).Should().Contain(text => text.StartsWith("slot 1: replace CMS 1/") && text.Contains("with CMS 3/3"));
			Steps.Leaving.Should().Contain(used);
			Steps.Bringing.Should().Contain(fresh);
		}

		[Fact]
		public void A_used_kit_with_no_fuller_one_is_kept_and_reported()
		{
			var used = Put(1, RealItems.Cms);
			Use(used, 1);
			Saved(Wants(1, 1, RealItems.Cms));

			var lines = Plan();

			lines.Should().Contain(line => line.Problem && line.Detail.EndsWith("no fuller one left in the stash"));
			lines[0].Text.Should().Be("Slots: 1/1 in place");
		}

		[Fact]
		public void A_full_kit_is_left_alone()
		{
			Put(1, RealItems.Cms);
			_body.InStash(RealItems.Any(RealItems.Cms));
			Saved(Wants(1, 1, RealItems.Cms));

			Texts(Plan()).Should().Equal("Slots: 1/1 in place");
		}

		[Fact]
		public void The_swap_puts_the_used_kit_in_the_stash_then_the_fresh_one_in_the_slot()
		{
			var used = Put(1, RealItems.Cms);
			Use(used, 1);
			var fresh = _body.InStash(RealItems.Any(RealItems.Cms));
			Saved(Wants(1, 1, RealItems.Cms));

			RunAll(Plan()).Result.Should().BeNull();

			_body.Moves.Select(move => move.Item).Should().Equal(used, fresh);
			Body.Where(_body.Moves[1]).Should().Be("SpecialSlot1");
		}

		[Fact]
		public void If_the_fresh_kit_cannot_be_placed_the_used_one_goes_back()
		{
			var used = Put(1, RealItems.Cms);
			Use(used, 1);
			var fresh = _body.InStash(RealItems.Any(RealItems.Cms));
			Saved(Wants(1, 1, RealItems.Cms));
			_body.Refuse = move => move.Item == fresh ? "refused" : null;

			var error = RunAll(Plan()).Result;

			error.Should().Be("refused");
			_body.Moves.Select(move => move.Item).Should().Equal(used, used);
		}

		private static void Use(Item kit, float left) => kit.GetItemComponent<MedKitComponent>().HpResource = left;
	}
}
