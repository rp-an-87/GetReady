using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The injector case: saving how it is filled, and putting the injectors back in the same cells.
	public class InjectorCaseTests
	{
		private const string Propital = "5c0e530286f7747fa1419862", Adrenaline = "5c10c8fd86f7743d7d706df3", Morphine = "544fb3f34bdc2d03748b456a";

		private readonly Body _body = new Body();
		private readonly InjectorCaseStep _step = new InjectorCaseStep();
		private readonly CompoundItem _case;

		public InjectorCaseTests()
		{
			Reset();
			Names[Propital] = "Propital";
			Names[Adrenaline] = "Adrenaline";
			Names[Morphine] = "Morphine";
			var backpack = _body.Wear(EquipmentSlot.Backpack, NewContainer(Gear.ScavBackpack));
			_case = NewContainer(Body.InjectorCase).In(backpack);
		}

		private static CompoundItem NewContainer(string template) => Gear.NewContainer(template);

		private Item Inject(string template, int x, int y)
		{
			var item = RealItems.Any(template);
			var grid = _case.Grids[0];
			var result = grid.Add(item, new LocationInGrid(x, y, ItemRotation.Horizontal), false);
			result.Succeeded.Should().BeTrue(result.Error?.ToString());
			return item;
		}

		private static CaseCell Cell(int x, int y, string template) => new CaseCell { X = x, Y = y, Template = template, Name = Game.TemplateName(template) };

		private SavedCase Saved(params CaseCell[] cells) => SavedFor(_case, cells);

		private static SavedCase SavedFor(CompoundItem injectorCase, params CaseCell[] cells)
		{
			var saved = new SavedCase { CaseId = injectorCase.Id.ToString(), CaseTemplate = Body.InjectorCase, CaseName = "Injector case" };
			saved.Cells.AddRange(cells);
			SettingsStore.Current.InjectorCase.Cases.Add(saved);
			return saved;
		}

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

		// ---- finding the case

		[Fact]
		public void Cases_on_you_are_found_by_their_template()
		{
			var pouch = _body.Wear(EquipmentSlot.SecuredContainer, NewContainer(Gear.AlphaCase));
			var second = NewContainer(Body.InjectorCase).In(pouch);
			RealItems.Any(RealItems.Grizzly).In(_body.Container(EquipmentSlot.Backpack));

			InjectorCaseStep.FindCases(_body.Inventory).Should().BeEquivalentTo(new[] { _case, second });
		}

		[Fact]
		public void A_case_in_the_stash_is_not_on_you()
		{
			NewContainer(Body.InjectorCase).In(_body.StashBox);

			InjectorCaseStep.FindCases(_body.Inventory).Should().Equal(_case);
		}

		[Fact]
		public void A_case_in_the_secured_container_counts()
		{
			var pouch = _body.Wear(EquipmentSlot.SecuredContainer, NewContainer(Gear.AlphaCase));
			var inPouch = NewContainer(Body.InjectorCase).In(pouch);

			InjectorCaseStep.FindCases(_body.Inventory).Should().Contain(inPouch);
		}

		[Fact]
		public void The_item_in_a_cell_is_found_by_its_place()
		{
			var one = Inject(Propital, 0, 0);
			var two = Inject(Adrenaline, 2, 1);
			var grid = _case.Grids[0];

			InjectorCaseStep.InCell(grid, 0, 0).Should().BeSameAs(one);
			InjectorCaseStep.InCell(grid, 2, 1).Should().BeSameAs(two);
			InjectorCaseStep.InCell(grid, 1, 1).Should().BeNull();
		}

		// ---- saving

		[Fact]
		public void Saving_records_every_injector_with_its_cell_and_binds_the_case()
		{
			Inject(Propital, 0, 0);
			Inject(Adrenaline, 2, 1);

			InjectorCaseStep.Capture(_case).Should().Be(2);

			var template = SettingsStore.Current.InjectorCase.Cases.Single();
			template.CaseId.Should().Be(_case.Id.ToString());
			template.Cells.Select(cell => (cell.X, cell.Y, cell.Template, cell.Name)).Should().BeEquivalentTo(new[]
			{
				(0, 0, Propital, "Propital"), (2, 1, Adrenaline, "Adrenaline")
			});
		}

		[Fact]
		public void Saving_replaces_the_cells_saved_before()
		{
			Saved(Cell(1, 1, Morphine));
			Inject(Propital, 0, 0);

			InjectorCaseStep.Capture(_case);

			SettingsStore.Current.InjectorCase.Cases.Should().ContainSingle().Which.Cells.Should().ContainSingle().Which.Template.Should().Be(Propital);
		}

		[Fact]
		public void Saving_an_empty_case_saves_an_empty_template()
		{
			InjectorCaseStep.Capture(_case).Should().Be(0);

			var template = SettingsStore.Current.InjectorCase.Cases.Single();
			template.CaseId.Should().Be(_case.Id.ToString());
			template.Cells.Should().BeEmpty();
		}

		// ---- the plan

		[Fact]
		public void With_nothing_saved_the_plan_says_so()
		{
			Plan().Should().ContainSingle().Which.Text.Should().StartWith("Nothing saved");
		}

		[Fact]
		public void A_saved_case_that_is_not_on_you_is_ignored_not_an_error()
		{
			Saved(Cell(0, 0, Propital));
			SettingsStore.Current.InjectorCase.Cases[0].CaseId = "someothercase";
			_body.InStash(RealItems.Any(Propital));

			var lines = Plan();

			lines.Should().ContainSingle().Which.Text.Should().Be("None of the saved injector cases is on you.");
			lines[0].Problem.Should().BeFalse();
			lines[0].Run.Should().BeNull();
		}

		[Fact]
		public void Cells_holding_the_right_injector_are_in_place()
		{
			Inject(Propital, 0, 0);
			Saved(Cell(0, 0, Propital));

			Texts(Plan()).Should().Equal("Injectors: 1/1 in place");
		}

		[Fact]
		public void An_empty_cell_gets_the_injector_from_the_stash()
		{
			var injector = _body.InStash(RealItems.Any(Propital));
			Saved(Cell(1, 2, Propital));

			var lines = Plan();

			Texts(lines).Should().Equal("Injectors: 0/1 in place", "Bring 1x Propital");
			RunAll(lines).Result.Should().BeNull();
			var move = _body.Moves.Single();
			move.Item.Should().BeSameAs(injector);
			Body.Where(move).Should().Be(Body.InjectorCase);
			InjectorCaseStep.InCell(_case.Grids[0], 1, 2).Should().BeSameAs(injector);
		}

		[Fact]
		public void The_same_injector_for_several_cells_is_brought_in_one_line()
		{
			_body.InStash(RealItems.Any(Propital));
			_body.InStash(RealItems.Any(Propital));
			Saved(Cell(0, 0, Propital), Cell(1, 0, Propital));

			var lines = Plan();

			Texts(lines).Should().Equal("Injectors: 0/2 in place", "Bring 2x Propital");
			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Should().HaveCount(2);
		}

		[Fact]
		public void Each_cell_takes_a_different_item()
		{
			var one = _body.InStash(RealItems.Any(Propital));
			var two = _body.InStash(RealItems.Any(Propital));
			Saved(Cell(0, 0, Propital), Cell(1, 0, Propital));

			RunAll(Plan()).Result.Should().BeNull();

			_body.Moves.Select(move => move.Item).Should().BeEquivalentTo(new[] { one, two });
		}

		[Fact]
		public void When_the_stash_has_fewer_than_the_cells_want_the_rest_is_missing()
		{
			_body.InStash(RealItems.Any(Propital));
			Saved(Cell(0, 0, Propital), Cell(1, 0, Propital));

			var lines = Plan();

			lines.Count(line => line.Run != null).Should().Be(1);
			lines.Should().Contain(line => line.Problem && line.Subject == "Propital" && line.Detail.Contains("Missing 1: none left in the stash"));
		}

		[Fact]
		public void A_locked_injector_in_the_stash_is_not_used()
		{
			_body.InStash(RealItems.Any(Propital)).PinLockState = EItemPinLockState.Locked;
			Saved(Cell(0, 0, Propital));

			Plan().Should().Contain(line => line.Problem && line.Detail.Contains("none left in the stash"));
		}

		[Fact]
		public void An_injector_another_step_claimed_is_not_used()
		{
			Steps.Claimed.Add(_body.InStash(RealItems.Any(Propital)));
			Saved(Cell(0, 0, Propital));

			Plan().Should().Contain(line => line.Problem && line.Detail.Contains("none left in the stash"));
		}

		[Fact]
		public void The_injector_brought_is_claimed_for_the_other_steps()
		{
			var injector = _body.InStash(RealItems.Any(Propital));
			Saved(Cell(0, 0, Propital));

			Plan();

			Steps.Claimed.Should().Contain(injector);
		}

		[Fact]
		public void A_wrong_injector_in_a_cell_goes_back_to_the_stash_and_the_right_one_takes_its_place()
		{
			var wrong = Inject(Adrenaline, 0, 0);
			var right = _body.InStash(RealItems.Any(Propital));
			Saved(Cell(0, 0, Propital));

			var lines = Plan();

			Texts(lines).Should().Equal("Injectors: 0/1 in place", "Bring 1x Propital (replaces Adrenaline)");
			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Select(move => move.Item).Should().Equal(wrong, right);
			Body.Where(_body.Moves[0]).Should().Be(Gear.Stash);
			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(right);
		}

		[Fact]
		public void The_replacement_line_says_what_it_replaces()
		{
			Inject(Adrenaline, 0, 0);
			_body.InStash(RealItems.Any(Propital));
			Saved(Cell(0, 0, Propital));

			var line = Plan().Single(candidate => candidate.Run != null);

			line.Text.Should().Contain("replaces Adrenaline");
		}

		[Fact]
		public void A_wrong_injector_with_no_replacement_in_the_stash_is_a_problem_and_stays()
		{
			Inject(Adrenaline, 0, 0);
			Saved(Cell(0, 0, Propital));

			var lines = Plan();

			lines.Should().Contain(line => line.Problem && line.Subject == "Cell 0,0" && line.Detail == "holds Adrenaline, expected Propital and the stash has none");
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void If_the_wrong_one_cannot_go_to_the_stash_nothing_else_is_moved()
		{
			Inject(Adrenaline, 0, 0);
			_body.InStash(RealItems.Any(Propital));
			Saved(Cell(0, 0, Propital));
			_body.Refuse = move => "stash says no";

			RunAll(Plan()).Result.Should().Be("stash says no");
			_body.Moves.Should().BeEmpty();
		}

		[Fact]
		public void A_refused_move_into_the_case_is_reported()
		{
			var injector = _body.InStash(RealItems.Any(Propital));
			_body.Refuse = move => move.Item == injector ? "case says no" : null;
			Saved(Cell(0, 0, Propital));

			RunAll(Plan()).Result.Should().Be("case says no");
		}

		[Fact]
		public void The_other_cases_you_carry_are_left_alone()
		{
			var other = NewContainer(Body.InjectorCase).In(_body.Wear(EquipmentSlot.SecuredContainer, NewContainer(Gear.AlphaCase)));
			Saved(Cell(0, 0, Propital));
			_body.InStash(RealItems.Any(Propital));

			RunAll(Plan()).Result.Should().BeNull();

			_body.Moves.Single().To.Container.ParentItem.Should().BeSameAs(_case);
			other.Grids[0].Items.Should().BeEmpty();
		}

		// ---- one template per case

		private CompoundItem SecondCase() => NewContainer(Body.InjectorCase).In(_body.Wear(EquipmentSlot.SecuredContainer, NewContainer(Gear.AlphaCase)));

		private static List<string> Lines(IEnumerable<PlanLine> lines) => lines.Select(line => line.Text).ToList();

		[Fact]
		public void Two_cases_have_two_templates()
		{
			var second = SecondCase();
			Inject(Propital, 0, 0);
			var secondItem = RealItems.Any(Adrenaline);
			second.Grids[0].Add(secondItem, new LocationInGrid(1, 1, ItemRotation.Horizontal), false);

			InjectorCaseStep.Capture(_case);
			InjectorCaseStep.Capture(second);

			SettingsStore.Current.InjectorCase.Cases.Select(saved => saved.CaseId).Should().Equal(_case.Id.ToString(), second.Id.ToString());
			InjectorCaseStep.TemplateOf(_case.Id.ToString()).Cells.Single().Template.Should().Be(Propital);
			InjectorCaseStep.TemplateOf(second.Id.ToString()).Cells.Single().Template.Should().Be(Adrenaline);
		}

		[Fact]
		public void Saving_a_case_again_updates_its_template_and_leaves_the_other_templates_alone()
		{
			var second = SecondCase();
			Saved(Cell(0, 0, Propital));
			SavedFor(second, Cell(2, 2, Morphine));
			Inject(Adrenaline, 1, 0);

			InjectorCaseStep.Capture(_case);

			SettingsStore.Current.InjectorCase.Cases.Should().HaveCount(2);
			InjectorCaseStep.TemplateOf(_case.Id.ToString()).Cells.Single().Template.Should().Be(Adrenaline);
			InjectorCaseStep.TemplateOf(second.Id.ToString()).Cells.Single().Template.Should().Be(Morphine);
		}

		[Fact]
		public void The_template_remembers_the_name_and_tag_of_its_case()
		{
			InjectorCaseStep.Capture(_case);

			var saved = InjectorCaseStep.TemplateOf(_case.Id.ToString());
			saved.CaseTemplate.Should().Be(Body.InjectorCase);
			saved.CaseName.Should().NotBeNullOrEmpty();
			InjectorCaseStep.LabelOf(saved).Should().Be(saved.CaseName);
		}

		[Fact]
		public void A_tag_is_what_the_case_is_called_when_it_has_one()
		{
			InjectorCaseStep.LabelOf(new SavedCase { CaseName = "Injector case", Tag = "labs" }).Should().Be("labs");
			InjectorCaseStep.LabelOf(new SavedCase { CaseName = "Injector case" }).Should().Be("Injector case");
			InjectorCaseStep.LabelOf(new SavedCase()).Should().Be("Injector case");
		}

		[Fact]
		public void A_template_is_deleted_by_its_case_even_when_the_case_is_not_on_you()
		{
			Saved(Cell(0, 0, Propital));
			var gone = new SavedCase { CaseId = "case-in-the-stash", CaseName = "Injector case" };
			SettingsStore.Current.InjectorCase.Cases.Add(gone);

			InjectorCaseStep.Delete("case-in-the-stash");

			SettingsStore.Current.InjectorCase.Cases.Should().ContainSingle().Which.CaseId.Should().Be(_case.Id.ToString());
		}

		[Fact]
		public void Deleting_a_template_leaves_the_case_as_it_is()
		{
			var held = Inject(Propital, 0, 0);
			Saved(Cell(0, 0, Propital));

			InjectorCaseStep.Delete(_case.Id.ToString());

			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(held);
			Plan().Should().ContainSingle().Which.Text.Should().StartWith("Nothing saved");
		}

		[Fact]
		public void A_case_saved_empty_is_emptied_to_the_stash()
		{
			var one = Inject(Propital, 0, 0);
			var two = Inject(Adrenaline, 2, 1);
			Saved();

			var lines = Plan();

			Lines(lines).Should().Equal("Injectors: 0/2 in place", "Take Propital out of Injector case to the stash", "Take Adrenaline out of Injector case to the stash");
			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Select(move => move.Item).Should().BeEquivalentTo(new[] { one, two });
			_body.Moves.Select(move => Body.Where(move)).Should().OnlyContain(place => place == Gear.Stash);
		}

		[Fact]
		public void An_empty_case_with_an_empty_template_needs_nothing()
		{
			Saved();

			Lines(Plan()).Should().Equal("Injectors: 0/0 in place");
		}

		[Fact]
		public void An_injector_in_a_cell_the_template_leaves_empty_goes_to_the_stash_and_the_rest_stays()
		{
			Inject(Propital, 0, 0);
			var extra = Inject(Adrenaline, 1, 0);
			Saved(Cell(0, 0, Propital));

			var lines = Plan();

			Lines(lines).Should().Equal("Injectors: 1/2 in place", "Take Adrenaline out of Injector case to the stash");
			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Single().Item.Should().BeSameAs(extra);
		}

		[Fact]
		public void If_the_stash_cannot_take_what_comes_out_the_line_fails()
		{
			Inject(Adrenaline, 0, 0);
			Saved();
			_body.Refuse = move => "stash full";

			RunAll(Plan()).Result.Should().Be("stash full");
		}

		[Fact]
		public void Each_case_with_a_template_gets_its_own_card()
		{
			var second = SecondCase();
			Saved(Cell(0, 0, Propital));
			SavedFor(second, Cell(1, 1, Adrenaline));
			_body.InStash(RealItems.Any(Propital));
			_body.InStash(RealItems.Any(Adrenaline));

			var lines = Plan();

			lines.Where(line => line.IsProgress).Select(line => line.Subject).Should().Equal("Injectors (Injector case)", "Injectors (Injector case)");
			lines.Count(line => line.Run != null).Should().Be(2);
			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Select(move => move.Item.TemplateId.ToString()).Should().BeEquivalentTo(new[] { Propital, Adrenaline });
		}

		[Fact]
		public void The_card_of_a_tagged_case_carries_its_tag()
		{
			var second = SecondCase();
			var first = Saved(Cell(0, 0, Propital));
			first.Tag = "regular";
			var other = SavedFor(second, Cell(1, 1, Adrenaline));
			other.Tag = "labs";

			Plan().Where(line => line.IsProgress).Select(line => line.Subject).Should().Equal("Injectors (regular)", "Injectors (labs)");
		}

		[Fact]
		public void Two_cases_do_not_take_the_same_injector_from_the_stash()
		{
			var second = SecondCase();
			Saved(Cell(0, 0, Propital));
			SavedFor(second, Cell(0, 0, Propital));
			_body.InStash(RealItems.Any(Propital));

			var lines = Plan();

			lines.Count(line => line.Run != null).Should().Be(1);
			lines.Should().Contain(line => line.Problem && line.Detail.Contains("Missing 1: none left in the stash"));
		}

		[Fact]
		public void A_template_of_a_case_that_is_not_on_you_is_ignored_while_another_case_is_planned()
		{
			var away = SavedFor(NewContainer(Body.InjectorCase), Cell(0, 0, Adrenaline));
			away.CaseId = "left-at-home";
			Saved(Cell(0, 0, Propital));
			_body.InStash(RealItems.Any(Propital));
			_body.InStash(RealItems.Any(Adrenaline));

			var lines = Plan();

			lines.Where(line => line.IsProgress).Should().ContainSingle();
			lines.Should().NotContain(line => line.Problem);
			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Single().Item.TemplateId.ToString().Should().Be(Propital);
		}

		[Fact]
		public void A_case_without_a_template_is_left_alone_even_when_it_holds_things()
		{
			var second = SecondCase();
			second.Grids[0].Add(RealItems.Any(Adrenaline), new LocationInGrid(0, 0, ItemRotation.Horizontal), false);
			Saved();

			Plan().Where(line => line.Run != null).Should().BeEmpty();
		}

		[Fact]
		public void Saved_cells_that_cannot_be_filled_are_a_problem_not_silently_dropped()
		{
			Saved(Cell(0, 0, Propital), Cell(1, 0, Adrenaline));

			var lines = Plan();

			lines.Count(line => line.Problem).Should().Be(2);
		}

		[Fact]
		public void The_template_of_the_first_version_becomes_the_first_template()
		{
			var json = "{\"InjectorCase\":{\"Enabled\":true,\"CaseId\":\"abc\",\"Cells\":[{\"X\":1,\"Y\":2,\"Template\":\"" + Propital + "\",\"Name\":\"Propital\"}]}}";

			var settings = SettingsStore.Parse(json);

			var saved = settings.InjectorCase.Cases.Single();
			saved.CaseId.Should().Be("abc");
			saved.Cells.Single().Template.Should().Be(Propital);
			settings.InjectorCase.CaseId.Should().BeNull();
			settings.InjectorCase.Cells.Should().BeEmpty();
		}

		[Fact]
		public void A_file_that_already_has_templates_keeps_them_as_they_are()
		{
			var json = "{\"InjectorCase\":{\"Cases\":[{\"CaseId\":\"one\",\"Cells\":[]},{\"CaseId\":\"two\",\"Cells\":[]}]}}";

			SettingsStore.Parse(json).InjectorCase.Cases.Select(saved => saved.CaseId).Should().Equal("one", "two");
		}

		[Fact]
		public void An_old_template_with_no_cells_is_nothing_saved()
		{
			var json = "{\"InjectorCase\":{\"CaseId\":\"abc\",\"Cells\":[]}}";

			SettingsStore.Parse(json).InjectorCase.Cases.Should().BeEmpty();
		}

		// ---- an injector in the wrong cell of the same case

		[Fact]
		public void An_injector_in_a_cell_the_template_leaves_empty_is_moved_to_its_own_cell()
		{
			var held = Inject(Adrenaline, 1, 0);
			Saved(Cell(0, 0, Adrenaline));

			var lines = Plan();

			Lines(lines).Should().Equal("Injectors: 0/1 in place", "Move Adrenaline to cell 0,0 of Injector case");
			lines.Should().NotContain(line => line.Problem);
			RunAll(lines).Result.Should().BeNull();
			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(held);
			InjectorCaseStep.InCell(_case.Grids[0], 1, 0).Should().BeNull();
		}

		[Fact]
		public void The_one_in_the_case_is_used_before_one_from_the_stash()
		{
			var held = Inject(Adrenaline, 1, 0);
			var spare = _body.InStash(RealItems.Any(Adrenaline));
			Saved(Cell(0, 0, Adrenaline));

			RunAll(Plan()).Result.Should().BeNull();

			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(held);
			_body.Moves.Select(move => move.Item).Should().NotContain(spare);
			Steps.Claimed.Should().NotContain(spare);
		}

		[Fact]
		public void A_wrong_injector_in_a_saved_cell_is_swapped_for_the_right_one_from_another_cell_of_the_case()
		{
			var wrong = Inject(Adrenaline, 0, 0);
			var right = Inject(Propital, 1, 0);
			Saved(Cell(0, 0, Propital));

			var lines = Plan();

			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Select(move => move.Item).Should().Equal(wrong, right);
			Body.Where(_body.Moves[0]).Should().Be(Gear.Stash);
			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(right);
		}

		[Fact]
		public void Only_the_injectors_that_are_not_wanted_elsewhere_go_to_the_stash()
		{
			Inject(Adrenaline, 1, 0);
			var stray = Inject(Morphine, 2, 0);
			Saved(Cell(0, 0, Adrenaline));

			var lines = Plan();

			Lines(lines).Should().Equal("Injectors: 0/2 in place", "Move Adrenaline to cell 0,0 of Injector case", "Take Morphine out of Injector case to the stash");
			RunAll(lines).Result.Should().BeNull();
			_body.Moves.Select(move => move.Item).Should().Contain(stray);
		}

		[Fact]
		public void Two_wanted_in_the_wrong_cells_are_both_moved_and_none_is_taken_from_the_stash()
		{
			var one = Inject(Propital, 2, 2);
			var two = Inject(Adrenaline, 1, 1);
			Saved(Cell(0, 0, Propital), Cell(1, 0, Adrenaline));

			RunAll(Plan()).Result.Should().BeNull();

			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(one);
			InjectorCaseStep.InCell(_case.Grids[0], 1, 0).Should().BeSameAs(two);
			_body.Moves.Select(move => Body.Where(move)).Should().OnlyContain(place => place == Body.InjectorCase);
		}

		[Fact]
		public void Two_injectors_in_each_others_cells_are_swapped()
		{
			var adrenaline = Inject(Adrenaline, 0, 0);
			var propital = Inject(Propital, 1, 0);
			Saved(Cell(0, 0, Propital), Cell(1, 0, Adrenaline));

			var lines = Plan();

			lines.Should().NotContain(line => line.Problem);
			RunAll(lines).Result.Should().BeNull();
			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(propital);
			InjectorCaseStep.InCell(_case.Grids[0], 1, 0).Should().BeSameAs(adrenaline);
		}

		[Fact]
		public void Three_injectors_in_each_others_cells_are_all_put_right()
		{
			var a = Inject(Adrenaline, 0, 0);
			var p = Inject(Propital, 1, 0);
			var m = Inject(Morphine, 2, 0);
			Saved(Cell(0, 0, Propital), Cell(1, 0, Morphine), Cell(2, 0, Adrenaline));

			var lines = Plan();

			lines.Should().NotContain(line => line.Problem);
			RunAll(lines).Result.Should().BeNull();
			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(p);
			InjectorCaseStep.InCell(_case.Grids[0], 1, 0).Should().BeSameAs(m);
			InjectorCaseStep.InCell(_case.Grids[0], 2, 0).Should().BeSameAs(a);
		}

		[Fact]
		public void Several_strays_are_each_moved_to_their_own_cell()
		{
			var a = Inject(Adrenaline, 2, 2);
			var p = Inject(Propital, 1, 2);
			var m = Inject(Morphine, 0, 2);
			Saved(Cell(0, 0, Adrenaline), Cell(1, 0, Propital), Cell(2, 0, Morphine));

			var lines = Plan();

			Lines(lines).Where(text => text.StartsWith("Move ")).Should().HaveCount(3);
			RunAll(lines).Result.Should().BeNull();
			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(a);
			InjectorCaseStep.InCell(_case.Grids[0], 1, 0).Should().BeSameAs(p);
			InjectorCaseStep.InCell(_case.Grids[0], 2, 0).Should().BeSameAs(m);
		}

		[Fact]
		public void A_swap_that_needs_one_the_case_does_not_have_brings_it_from_the_stash()
		{
			var adrenaline = Inject(Adrenaline, 0, 0);
			var fromStash = _body.InStash(RealItems.Any(Propital));
			Saved(Cell(0, 0, Propital), Cell(1, 0, Adrenaline));

			RunAll(Plan()).Result.Should().BeNull();

			InjectorCaseStep.InCell(_case.Grids[0], 0, 0).Should().BeSameAs(fromStash);
			InjectorCaseStep.InCell(_case.Grids[0], 1, 0).Should().BeSameAs(adrenaline);
		}

		[Fact]
		public void A_moved_injector_counts_as_one_in_the_progress_not_as_extra_work()
		{
			Inject(Adrenaline, 2, 2);
			Saved(Cell(0, 0, Adrenaline));

			Lines(Plan()).First().Should().Be("Injectors: 0/1 in place");
		}
	}
}
