using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;

namespace GetReady.Tests
{
	// The count on the stash button, its tooltip, doing the plan and what is said afterwards.
	public class ReadyWindowTests
	{
		private class FakeStep : IStep
		{
			public string Name { get; set; } = "Step";
			public bool Enabled { get; set; } = true;
			public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
			public List<PlanLine> Lines = new List<PlanLine>();
			public List<PlanLine> Plan(InventoryController controller) => Lines;
		}

		public ReadyWindowTests()
		{
			SupplyFixture.Reset();
			ReadyPlan.Plan.Clear();
			ReadyRun.Failures.Clear();
			ReadyRun._moved = ReadyRun._repaired = ReadyRun._filled = ReadyRun._chambered = ReadyRun._topped = ReadyRun._keys = ReadyRun._healed = ReadyRun._modes = 0;
		}

		private static PlanLine Todo(string text = "do", int items = 1) => new PlanLine { Text = text, Items = items, Run = () => Task.FromResult<string>(null) };

		private static PlanLine Wrong(string subject = "thing", string detail = "wrong") => new PlanLine { Problem = true, Subject = subject, Detail = detail, Text = subject + ": " + detail };

		private static PlanLine Group(string text = "Group") => new PlanLine { IsProgress = true, Text = text };

		private static void Add(string name, params PlanLine[] lines) => ReadyPlan.Plan.Add(new KeyValuePair<IStep, List<PlanLine>>(new FakeStep { Name = name }, lines.ToList()));

		private static (int toBring, int problems) Tally()
		{
			ReadyPlan.Tally(out var toBring, out var problems);
			return (toBring, problems);
		}

		// ---- the count on the stash button

		[Fact]
		public void An_empty_plan_counts_nothing()
		{
			Tally().Should().Be((0, 0));
		}

		[Fact]
		public void A_group_with_something_to_bring_counts_once_however_many_lines()
		{
			Add("Supplies", Group(), Todo(), Todo(), Todo());

			Tally().Should().Be((1, 0));
		}

		[Fact]
		public void A_group_with_a_problem_counts_once_however_many_problems()
		{
			Add("Supplies", Group(), Wrong(), Wrong());

			Tally().Should().Be((0, 1));
		}

		[Fact]
		public void A_group_with_both_counts_in_both()
		{
			Add("Supplies", Group(), Todo(), Wrong());

			Tally().Should().Be((1, 1));
		}

		[Fact]
		public void A_group_with_nothing_wrong_and_nothing_to_do_counts_nothing()
		{
			Add("Supplies", Group());

			Tally().Should().Be((0, 0));
		}

		[Fact]
		public void Each_group_is_counted_on_its_own()
		{
			Add("Supplies", Group("A"), Todo(), Group("B"), Wrong(), Group("C"), Todo(), Todo(), Group("D"));

			Tally().Should().Be((2, 1));
		}

		[Fact]
		public void Lines_without_a_group_count_one_by_one()
		{
			Add("Special slots", Todo(), Todo(), Wrong());

			Tally().Should().Be((2, 1));
		}

		[Fact]
		public void Lines_before_the_first_group_count_apart_from_the_groups()
		{
			Add("Mixed", Todo(), Group(), Todo(), Todo());

			Tally().Should().Be((2, 0));
		}

		[Fact]
		public void Steps_add_up()
		{
			Add("One", Group(), Todo());
			Add("Two", Group(), Wrong());

			Tally().Should().Be((1, 1));
		}

		[Fact]
		public void Information_lines_count_for_nothing()
		{
			Add("Info", new PlanLine { Text = "just so you know" });

			Tally().Should().Be((0, 0));
		}

		// ---- the problems

		[Fact]
		public void Problems_come_with_the_name_of_their_step()
		{
			Add("Repair", Group(), Wrong("M4A1", "needs repair"));
			Add("Ammo", Wrong("Magazine", "empty"));

			ReadyPlan.Problems().Select(pair => pair.Key + ": " + pair.Value.Subject).Should().Equal("Repair: M4A1", "Ammo: Magazine");
		}

		// ---- the tooltip

		[Fact]
		public void With_nothing_switched_on_the_tooltip_says_so()
		{
			ReadyPlan.Summary().Should().Contain("Nothing is switched on");
		}

		[Fact]
		public void With_nothing_to_do_and_no_problems_you_are_all_set()
		{
			Add("Supplies", Group());

			ReadyPlan.Summary().Should().Contain("You are all set");
		}

		[Fact]
		public void One_thing_to_fulfil_is_said_in_the_singular()
		{
			Add("Supplies", Group(), Todo());

			ReadyPlan.Summary().Should().Contain("1 thing to fulfil").And.NotContain("1 things");
		}

		[Fact]
		public void Several_things_to_fulfil_are_counted()
		{
			Add("Supplies", Group("A"), Todo(), Group("B"), Todo(), Group("C"), Todo());

			ReadyPlan.Summary().Should().Contain("3 things to fulfil");
		}

		[Fact]
		public void Only_a_problem_asks_for_your_attention()
		{
			Add("Supplies", Group(), Wrong("Grenade", "none left"));

			var text = ReadyPlan.Summary();

			text.Should().Contain("Needs your attention").And.Contain("1 problem").And.Contain("Grenade").And.Contain("none left");
		}

		[Fact]
		public void With_both_the_problem_comes_first_and_decides_the_heading()
		{
			Add("Supplies", Group(), Todo(), Wrong());

			var text = ReadyPlan.Summary();

			text.Should().Contain("Needs your attention").And.Contain("1 thing to fulfil").And.Contain("1 problem");
			text.IndexOf("Needs your attention").Should().BeLessThan(text.IndexOf("1 thing to fulfil"));
		}

		[Fact]
		public void Only_things_to_do_is_amber_without_any_attention_heading()
		{
			Add("Supplies", Group(), Todo());

			ReadyPlan.Summary().Should().Contain("1 thing to fulfil").And.NotContain("Needs your attention");
		}

		[Fact]
		public void Only_the_first_five_problems_are_listed_and_the_rest_counted()
		{
			Add("Supplies", Group(), Wrong("p1"), Wrong("p2"), Wrong("p3"), Wrong("p4"), Wrong("p5"), Wrong("p6"), Wrong("p7"));

			var text = ReadyPlan.Summary();

			text.Should().Contain("7 problems").And.Contain("p5").And.NotContain("p6").And.Contain("and 2 more");
		}

		[Fact]
		public void A_long_problem_is_cut_short()
		{
			Add("Supplies", Wrong(new string('x', 80), new string('y', 80)));

			var text = ReadyPlan.Summary();

			text.Should().NotContain(new string('x', 40)).And.NotContain(new string('y', 50)).And.Contain("…");
		}

		[Fact]
		public void The_tooltip_tells_what_the_clicks_do()
		{
			ReadyPlan.Summary().Should().Contain("[Left click]").And.Contain("get ready").And.Contain("[Right click]").And.Contain("configure");
		}

		// ---- doing the plan

		private static PlanLine Failing(string error) => new PlanLine { Text = "fail", Template = "t", Run = () => Task.FromResult(error) };

		[Fact]
		public async Task Every_line_that_can_run_is_run_in_order_and_its_items_added_up()
		{
			var order = new List<string>();
			PlanLine Ran(string name, int items) => new PlanLine { Text = name, Items = items, Run = () => { order.Add(name); return Task.FromResult<string>(null); } };
			Add("One", Ran("a", 2), Ran("b", 3));
			Add("Two", Ran("c", 1));

			await ReadyRun.RunPlan();

			order.Should().Equal("a", "b", "c");
			ReadyRun._moved.Should().Be(6);
			ReadyRun.Failures.Should().BeEmpty();
		}

		[Fact]
		public async Task Lines_without_a_run_are_skipped()
		{
			Add("One", Wrong(), Group());

			await ReadyRun.RunPlan();

			ReadyRun._moved.Should().Be(0);
		}

		[Fact]
		public async Task Each_kind_of_work_is_counted_apart()
		{
			PlanLine Kind(System.Action<PlanLine> set)
			{
				var line = Todo(items: 0);
				set(line);
				return line;
			}

			Add("Work",
				Kind(l => l.Repair = true), Kind(l => l.Repair = true),
				Kind(l => l.Fill = true), Kind(l => l.Chamber = true), Kind(l => l.Topped = true),
				Kind(l => l.Heal = true), Kind(l => l.Mode = true), Kind(l => l.Mode = true), Kind(l => l.Mode = true));

			await ReadyRun.RunPlan();

			(ReadyRun._repaired, ReadyRun._filled, ReadyRun._chambered, ReadyRun._topped, ReadyRun._healed, ReadyRun._modes).Should().Be((2, 1, 1, 1, 1, 3));
			ReadyRun._moved.Should().Be(0);
		}

		[Fact]
		public async Task A_line_that_fails_is_reported_as_a_problem_and_the_rest_still_run()
		{
			var later = false;
			Add("Supplies", Failing("no room"), new PlanLine { Text = "later", Run = () => { later = true; return Task.FromResult<string>(null); } });

			await ReadyRun.RunPlan();

			later.Should().BeTrue();
			var failure = ReadyRun.Failures.Single();
			failure.Key.Should().Be("Supplies");
			failure.Value.Problem.Should().BeTrue();
			failure.Value.Subject.Should().Be("fail");
			failure.Value.Detail.Should().Be("Could not move it: no room");
			failure.Value.Template.Should().Be("t");
			ReadyRun._moved.Should().Be(1);
		}

		[Fact]
		public async Task A_failed_line_adds_nothing_to_the_counts()
		{
			var line = Failing("no");
			line.Repair = true;
			line.Items = 4;
			Add("Repair", line);

			await ReadyRun.RunPlan();

			ReadyRun._moved.Should().Be(0);
			ReadyRun._repaired.Should().Be(0);
		}

		[Fact]
		public async Task Failures_of_an_earlier_run_are_forgotten()
		{
			Add("One", Failing("no"));
			await ReadyRun.RunPlan();
			ReadyPlan.Plan.Clear();
			Add("One", Todo());

			await ReadyRun.RunPlan();

			ReadyRun.Failures.Should().BeEmpty();
		}

		[Fact]
		public async Task Without_a_stash_no_keys_are_set()
		{
			Add("One", Todo());

			await ReadyRun.RunPlan();

			ReadyRun._keys.Should().Be(0);
		}

		// ---- what is said afterwards

		[Fact]
		public void Nothing_done_says_you_already_have_everything()
		{
			ReadyRun.Report().Should().Be("Ready for raid: you already have everything.");
		}

		[Fact]
		public void The_report_lists_what_was_done_in_a_fixed_order()
		{
			ReadyRun._moved = 3;
			ReadyRun._repaired = 2;
			ReadyRun._filled = 1;
			ReadyRun._chambered = 4;
			ReadyRun._topped = 5;
			ReadyRun._keys = 6;
			ReadyRun._healed = 1;
			ReadyRun._modes = 2;

			ReadyRun.Report().Should().Be("Ready for raid: 3 item(s) brought, 2 piece(s) of gear repaired, 1 magazine(s) filled, 4 round(s) chambered, 5 ammo stack(s) topped up, 6 quick-slot key(s) set, healed, 2 weapon(s) on full auto.");
		}

		[Fact]
		public void Only_what_happened_is_in_the_report()
		{
			ReadyRun._keys = 2;
			ReadyRun._healed = 1;

			ReadyRun.Report().Should().Be("Ready for raid: 2 quick-slot key(s) set, healed.");
		}

		// ---- the stash button

		[Theory]
		[InlineData(UnityEngine.EventSystems.PointerEventData.InputButton.Left, "GetReady")]
		[InlineData(UnityEngine.EventSystems.PointerEventData.InputButton.Right, "Configure")]
		[InlineData(UnityEngine.EventSystems.PointerEventData.InputButton.Middle, "None")]
		public void The_buttons_of_the_mouse_do_one_thing_each(UnityEngine.EventSystems.PointerEventData.InputButton button, string expected)
		{
			ReadyButtonPatch.ActionOf(button).ToString().Should().Be(expected);
		}

		// ---- the colour of a card

		private static PlanLine Progress(float have, float want) => new PlanLine { IsProgress = true, Have = have, Want = want };

		private static string Standing(params PlanLine[] lines) => ReadyPlan.StandingFor(lines.ToList(), 0).ToString();

		[Fact]
		public void A_card_with_what_it_wants_is_done()
		{
			Standing(Progress(2, 2)).Should().Be("Done");
		}

		[Fact]
		public void A_card_with_nothing_yet_but_something_to_bring_is_doable_not_red()
		{
			Standing(Progress(0, 300), Todo("Bring 1x AFAK")).Should().Be("Doable");
		}

		[Fact]
		public void A_card_partly_there_with_something_to_bring_is_doable()
		{
			Standing(Progress(1, 3), Todo()).Should().Be("Doable");
		}

		[Fact]
		public void A_card_with_a_problem_is_red_even_when_part_of_it_can_be_brought()
		{
			Standing(Progress(0, 5), Todo(), Wrong("Splints", "Missing 1")).Should().Be("Problem");
		}

		[Fact]
		public void A_card_that_is_complete_but_has_a_problem_is_red()
		{
			Standing(Progress(1, 1), Wrong("MCX", "empty chamber")).Should().Be("Problem");
		}

		[Fact]
		public void A_card_short_with_nothing_to_bring_is_red()
		{
			Standing(Progress(0, 2)).Should().Be("Problem");
		}

		[Fact]
		public void A_card_only_looks_at_the_lines_up_to_the_next_card()
		{
			var lines = new List<PlanLine> { Progress(2, 2), Progress(0, 2), Wrong() };

			ReadyPlan.StandingFor(lines, 0).ToString().Should().Be("Done");
			ReadyPlan.StandingFor(lines, 1).ToString().Should().Be("Problem");
		}

		[Fact]
		public void A_card_the_step_left_out_on_purpose_is_grey_not_red()
		{
			var skipped = Progress(0, 75000);
			skipped.Skipped = true;

			Standing(skipped, new PlanLine { Text = "Would bring 75,000 RUB" }).Should().Be("Skipped");
			ReadyWindow.StandingColor(ReadyPlan.Standing.Skipped).Should().Be(UiColors.Dim);
		}

		[Fact]
		public void A_skipped_card_is_not_counted_as_a_problem_or_a_thing_to_do()
		{
			var skipped = Progress(0, 75000);
			skipped.Skipped = true;

			Step(skipped, new PlanLine { Text = "Would bring 75,000 RUB" }).Should().Be((0, 0));
		}

		// ---- the jump tabs of Setup

		[Theory]
		[InlineData(0f, 0)]
		[InlineData(80f, 0)]
		[InlineData(100f, 1)]      // within the small margin of the top
		[InlineData(250f, 1)]
		[InlineData(700f, 2)]
		public void The_tab_lit_is_the_last_section_that_has_reached_the_top(float scroll, int expected)
		{
			ReadyWindow.ActiveSection(new[] { 0f, 100f, 400f }, scroll, 300f, 1000f).Should().Be(expected);
		}

		[Fact]
		public void At_the_end_of_the_page_the_last_tab_is_lit_even_when_its_section_is_short()
		{
			// the page is 700 high in a view of 300: the end is 400, where the last section (at 650) never reaches the top
			ReadyWindow.ActiveSection(new[] { 0f, 100f, 650f }, 400f, 300f, 700f).Should().Be(2);
		}

		[Fact]
		public void A_page_that_fits_the_view_lights_the_first_tab()
		{
			ReadyWindow.ActiveSection(new[] { 0f, 100f }, 0f, 500f, 400f).Should().Be(0);
		}

		[Fact]
		public void No_sections_measured_yet_lights_nothing()
		{
			ReadyWindow.ActiveSection(new float[0], 0f, 300f, 0f).Should().Be(-1);
		}

		[Fact]
		public void Tabs_break_into_rows_when_they_do_not_fit()
		{
			var rows = ReadyWindow.TabRows(new[] { 100f, 100f, 100f, 100f, 100f }, 250f);

			rows.Select(row => row.ToArray()).Should().BeEquivalentTo(new[] { new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4 } });
		}

		[Fact]
		public void A_tab_wider_than_the_strip_still_gets_a_row_of_its_own()
		{
			var rows = ReadyWindow.TabRows(new[] { 400f, 50f }, 250f);

			rows.Select(row => row.ToArray()).Should().BeEquivalentTo(new[] { new[] { 0 }, new[] { 1 } });
		}

		// ---- a line that is not part of the card above it

		private static PlanLine Alone(PlanLine line)
		{
			line.Standalone = true;
			return line;
		}

		[Fact]
		public void A_standalone_problem_does_not_belong_to_the_card_above()
		{
			var lines = new List<PlanLine> { Progress(0, 30), Todo(), Alone(Wrong("Glock", "no magazine")) };

			ReadyPlan.StandingFor(lines, 0).ToString().Should().Be("Doable");
		}

		[Fact]
		public void A_standalone_problem_counts_on_its_own_and_the_card_above_still_counts_its_work()
		{
			Add("Ammo", Progress(0, 30), Todo(), Alone(Wrong("Glock", "no magazine")), Progress(0, 4), Todo());

			Tally().Should().Be((2, 1));
		}

		[Fact]
		public void A_standalone_line_before_any_card_counts_alone_as_before()
		{
			Add("Ammo", Alone(Wrong("MCX", "no magazine")), Progress(0, 30), Todo());

			Tally().Should().Be((1, 1));
		}

		[Fact]
		public void Lines_after_a_standalone_one_do_not_join_the_earlier_card()
		{
			Add("Ammo", Progress(1, 1), Alone(Wrong("Glock", "no magazine")), Wrong("Ammo: M855", "missing 5"));

			Tally().Should().Be((0, 2));
		}

		[Fact]
		public void The_stripe_of_a_card_is_green_amber_or_red()
		{
			ReadyWindow.StandingColor(ReadyPlan.Standing.Done).Should().Be(UiColors.Ok);
			ReadyWindow.StandingColor(ReadyPlan.Standing.Doable).Should().Be(UiColors.Warn);
			ReadyWindow.StandingColor(ReadyPlan.Standing.Problem).Should().Be(UiColors.Bad);
		}

		// ---- the badge of one step counts cards, like the summary

		private static (int moves, int problems) Step(params PlanLine[] lines)
		{
			int moves = 0, problems = 0;
			ReadyPlan.TallyLines(lines.ToList(), ref moves, ref problems);
			return (moves, problems);
		}

		[Fact]
		public void A_step_with_five_lines_of_work_in_one_card_is_one_thing_to_do()
		{
			Step(Group(), Todo(), Todo(), Todo(), Todo(), Todo()).Should().Be((1, 0));
		}

		[Fact]
		public void A_step_counts_its_problem_cards_and_its_work_cards_apart()
		{
			Step(Group("A"), Wrong(), Wrong(), Group("B"), Todo(), Group("C"), Todo(), Group("D")).Should().Be((2, 1));
		}

		[Fact]
		public void A_step_with_nothing_to_do_and_nothing_wrong_counts_nothing()
		{
			Step(Group()).Should().Be((0, 0));
		}

		[Fact]
		public void The_steps_badges_add_up_to_the_summary()
		{
			var one = new[] { Group("A"), Todo(), Group("B"), Wrong() };
			var two = new[] { Group("C"), Todo(), Todo(), Wrong() };
			Add("One", one);
			Add("Two", two);

			var (m1, p1) = Step(one);
			var (m2, p2) = Step(two);

			Tally().Should().Be((m1 + m2, p1 + p2));
		}

		// ---- what the two summary boxes say on hover

		[Fact]
		public void The_red_box_lists_every_problem_by_step_with_what_is_wrong()
		{
			Add("Ammo", Group(), Wrong("Glock 18C", "no magazine in it"));
			Add("Cash", Wrong("Euros", "set to bring to Wallet, which you are not carrying"));

			var text = ReadyPlan.ProblemsTooltip();

			text.Should().StartWith("Cannot be done as set up:");
			text.Should().Contain("- Ammo: Glock 18C").And.Contain("no magazine in it").And.Contain("- Cash: Euros").And.Contain("set to bring to Wallet, which you are not carrying");
		}

		[Fact]
		public void The_red_box_cuts_a_long_list_and_counts_the_rest()
		{
			var lines = Enumerable.Range(1, 20).Select(number => Wrong("thing" + number, "wrong")).ToArray();
			Add("Supplies", lines);

			var text = ReadyPlan.ProblemsTooltip();

			text.Should().Contain("thing14").And.NotContain("thing15").And.Contain("... and 6 more");
		}

		[Fact]
		public void The_amber_box_lists_what_get_ready_will_do_by_step()
		{
			Add("Medicine to bring", Group(), Todo("Bring 1x AFAK"), Todo("Bring 1x Army bandage"));
			Add("Cash", Group(), Todo("Bring 100,000 RUB to Pockets"));

			var text = ReadyPlan.ToDoTooltip();

			text.Should().StartWith("Get ready will:");
			text.Should().Contain("Medicine to bring").And.Contain("+ Bring 1x AFAK").And.Contain("+ Bring 1x Army bandage").And.Contain("Cash").And.Contain("+ Bring 100,000 RUB to Pockets");
			text.IndexOf("Medicine to bring").Should().BeLessThan(text.IndexOf("Cash"));
		}

		[Fact]
		public void The_amber_box_leaves_out_steps_with_nothing_to_do_and_lines_that_do_nothing()
		{
			Add("Repair", Group(), Wrong("6B13", "needs repair"));
			Add("Cash", Group(), Todo("Bring 1 RUB"), new PlanLine { Text = "just so you know" });

			var text = ReadyPlan.ToDoTooltip();

			text.Should().NotContain("Repair").And.NotContain("just so you know").And.Contain("+ Bring 1 RUB");
		}

		[Fact]
		public void The_amber_box_cuts_a_long_list_and_counts_the_rest()
		{
			Add("Supplies", Enumerable.Range(1, 20).Select(number => Todo("do" + number)).ToArray());

			var text = ReadyPlan.ToDoTooltip();

			text.Should().Contain("do14").And.NotContain("do15").And.Contain("... and 6 more");
		}

		// ---- the standing of the loose ammo card

		private static List<PlanLine> LooseCard(params PlanLine[] below)
		{
			var lines = new List<PlanLine> { Steps.Progress(AmmoStep.LooseAmmo, 1, 2, "full") };
			lines.AddRange(below);
			return lines;
		}

		[Fact]
		public void A_loose_card_that_is_short_with_nothing_to_do_is_not_red()
		{
			ReadyPlan.LooseStanding(LooseCard(), 0).Should().Be(ReadyPlan.Standing.Done);
		}

		[Fact]
		public void A_loose_card_with_something_to_top_up_is_amber()
		{
			ReadyPlan.LooseStanding(LooseCard(new PlanLine { Text = "top up", Run = () => null }), 0).Should().Be(ReadyPlan.Standing.Doable);
		}

		[Fact]
		public void A_loose_card_with_a_problem_line_is_red()
		{
			ReadyPlan.LooseStanding(LooseCard(Steps.Problem("AP", "10/70, the stash has no more of it")), 0).Should().Be(ReadyPlan.Standing.Problem);
		}
	}
}
