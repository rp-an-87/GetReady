using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The small pure pieces: the game's error text, the ammo rules' order, the step order, the plan lines.
	public class SmallLogicTests
	{
		public SmallLogicTests()
		{
			Reset();
		}

		// ---- the game's error text

		[Theory]
		[InlineData(null, null)]
		[InlineData("", "")]
		[InlineData("Cannot move item (id: 0123456789abcdef01234567) here", "Cannot move item here")]
		[InlineData("  no room (id: 0123456789ABCDEF01234567)  ", "no room")]
		[InlineData("plain error", "plain error")]
		public void The_game_error_loses_its_ids(string error, string expected) => Game.Friendly(error).Should().Be(expected);

		[Theory]
		[InlineData("Cannot move a locked item")]
		[InlineData("LOCKED ITEM in the way")]
		public void A_locked_item_error_says_it_is_locked_in_the_stash(string error) => Game.Friendly(error).Should().Be("it is locked in your stash");

		// ---- the ammo rules' order

		[Fact]
		public void The_ammo_rules_start_in_their_usual_order() => AmmoRules.PointerOrder().Should().Equal("nearest", "others", "loose", "weapon");

		[Fact]
		public void The_ammo_rules_follow_the_saved_order()
		{
			SettingsStore.Current.Ammo.Pointers = new List<string> { "weapon", "loose", "nearest" };

			AmmoRules.PointerOrder().Should().Equal("weapon", "loose", "nearest", "others");
		}

		[Fact]
		public void A_rule_missing_from_the_saved_order_comes_last_and_unknown_or_repeated_ones_are_dropped()
		{
			SettingsStore.Current.Ammo.Pointers = new List<string> { "nearest", "bogus", "nearest" };

			AmmoRules.PointerOrder().Should().Equal("nearest", "others", "loose", "weapon");
		}

		[Fact]
		public void Moving_a_rule_down_and_up_changes_the_saved_order()
		{
			AmmoRules.MovePointer("loose", -1);
			AmmoRules.PointerOrder().Should().Equal("nearest", "loose", "others", "weapon");

			AmmoRules.MovePointer("weapon", -3);
			AmmoRules.PointerOrder().Should().Equal("weapon", "nearest", "loose", "others");
		}

		[Theory]
		[InlineData("nearest", -1)]
		[InlineData("weapon", 1)]
		[InlineData("loose", 5)]
		[InlineData("bogus", 1)]
		public void Moving_a_rule_off_the_list_or_an_unknown_one_changes_nothing(string key, int step)
		{
			AmmoRules.MovePointer(key, step);

			AmmoRules.PointerOrder().Should().Equal("nearest", "others", "loose", "weapon");
		}

		[Fact]
		public void Every_rule_has_a_name() => AmmoRules.PointerKeys.Select(AmmoRules.PointerName).Should().OnlyContain(name => !string.IsNullOrEmpty(name)).And.OnlyHaveUniqueItems();

		// ---- the order of the steps

		[Fact]
		public void The_special_slots_run_first_and_treating_injuries_last()
		{
			Steps.All.First().Name.Should().Be("Special slots");
			Steps.All.Last().Name.Should().Be("Treat injuries");
		}

		[Fact]
		public void The_pages_show_the_steps_in_the_order_you_tweak_them_most()
		{
			Steps.DisplayOrder.Select(step => step.Name).Should().Equal("Ammo", "Medicine to bring", "Grenades and flares", "Special slots", "Cases", "Repair", "Full auto", "Treat injuries", "Cash");
		}

		[Fact]
		public void Every_step_is_shown()
		{
			Steps.DisplayOrder.Should().HaveCount(Steps.All.Length);
			Steps.All.Select(step => step.Name).Should().OnlyHaveUniqueItems();
		}

		// ---- plan lines

		[Fact]
		public void A_progress_line_shows_have_and_want()
		{
			var line = Steps.Progress("Bandages", 1.4f, 2, "points", "abc");

			line.Text.Should().Be("Bandages: 1/2 points");
			line.IsProgress.Should().BeTrue();
			line.Template.Should().Be("abc");
			line.Have.Should().Be(1.4f);
			line.Want.Should().Be(2);
		}

		[Fact]
		public void A_problem_line_is_a_problem_with_subject_and_detail()
		{
			var line = Steps.Problem("Repair", "no kit", "abc");

			line.Problem.Should().BeTrue();
			line.Text.Should().Be("Repair: no kit");
			line.Subject.Should().Be("Repair");
			line.Detail.Should().Be("no kit");
		}

		[Fact]
		public void An_info_line_is_only_text()
		{
			var line = Steps.Info("No afflictions");

			line.Text.Should().Be("No afflictions");
			line.Problem.Should().BeFalse();
			line.Run.Should().BeNull();
		}

		// ---- the summary of what a template step needs

		private static Need Bring(string name, string where = null, System.Func<Task<string>> run = null) =>
			new Need { Name = name, Where = where, Template = "t-" + name, Run = run ?? (() => Task.FromResult<string>(null)) };

		private static Need Missing(string name, string where = null) => new Need { Name = name, Where = where, Template = "t-" + name };

		[Fact]
		public void The_summary_starts_with_how_many_are_in_place()
		{
			var lines = Steps.Summarize(2, 3, "special slots", new List<Need>(), new List<PlanLine>());

			lines.Should().ContainSingle().Which.Text.Should().Be("Special slots: 2/3 in place");
		}

		[Fact]
		public void Things_to_bring_are_grouped_by_name_with_where_they_go()
		{
			var needs = new List<Need> { Bring("Propital", "slot 4"), Bring("Propital", "slot 5"), Bring("Salewa") };

			var lines = Steps.Summarize(0, 3, "slots", needs, new List<PlanLine>());

			lines.Select(line => line.Text).Should().Equal("Slots: 0/3 in place", "Bring 2x Propital (slot 4, slot 5)", "Bring 1x Salewa");
			lines[1].Items.Should().Be(2);
			lines[1].Template.Should().Be("t-Propital");
		}

		[Fact]
		public void What_the_stash_lacks_is_a_problem_per_name()
		{
			var needs = new List<Need> { Missing("Propital", "slot 4"), Missing("Propital", "slot 5") };

			var lines = Steps.Summarize(0, 2, "slots", needs, new List<PlanLine>());

			lines.Last().Problem.Should().BeTrue();
			lines.Last().Text.Should().Be("Propital: Missing 2: none left in the stash (slot 4, slot 5)");
		}

		[Fact]
		public void Other_lines_come_last()
		{
			var lines = Steps.Summarize(1, 1, "slots", new List<Need>(), new List<PlanLine> { Steps.Info("note") });

			lines.Last().Text.Should().Be("note");
		}

		[Fact]
		public void The_bring_line_runs_every_move_in_turn_and_stops_at_the_first_error()
		{
			var order = new List<string>();
			var needs = new List<Need>
			{
				Bring("Propital", run: () => { order.Add("one"); return Task.FromResult<string>(null); }),
				Bring("Propital", run: () => { order.Add("two"); return Task.FromResult("no room"); }),
				Bring("Propital", run: () => { order.Add("three"); return Task.FromResult<string>(null); })
			};

			var error = Steps.Summarize(0, 3, "slots", needs, new List<PlanLine>())[1].Run().GetAwaiter().GetResult();

			error.Should().Be("no room");
			order.Should().Equal("one", "two");
		}

		[Fact]
		public void The_bring_line_runs_clean_when_every_move_works()
		{
			var needs = new List<Need> { Bring("Propital"), Bring("Propital") };

			Steps.Summarize(0, 2, "slots", needs, new List<PlanLine>())[1].Run().GetAwaiter().GetResult().Should().BeNull();
		}
	}
}
