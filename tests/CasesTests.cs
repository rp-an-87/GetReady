using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The cases you carry: recording what a case holds, bringing back what is missing, taking out the rest, money, the cash switch, and the
	// record following your hand in the stash. Any item stands in for a key: the step goes by type.
	public class CasesTests
	{
		// roomy containers without an item filter stand in for the Scav backpack and the docs case
		private const string Sicc = Gear.ScavBackpack, DocsCase = Gear.AlphaCase;
		private const string Key = RealItems.Grizzly, Card = RealItems.Salewa, Other = RealItems.Afak;

		private readonly Body _body = new Body();
		private readonly CasesStep _step = new CasesStep();
		private readonly CompoundItem _backpack;
		private readonly CompoundItem _case;

		public CasesTests()
		{
			Reset();
			Game.IsInsideTemplatedCase = Game.InTemplatedCase; // the real rule: the other tests do without items that have a place
			_backpack = _body.Wear(EquipmentSlot.Backpack, Gear.NewContainer(Gear.Stash)); // plenty of room for the cases
			_case = Gear.NewContainer(Sicc).In(_backpack);
		}

		private static CompoundItem NewCase(string template) => Gear.NewContainer(template);

		private Item Put(string template, CompoundItem into, int stack = 1) => RealItems.Any(template, stack).In(into);

		private List<PlanLine> Plan() => _step.Plan(_body.Controller);

		private static List<string> Lines(IEnumerable<PlanLine> lines) => lines.Select(line => line.Text).ToList();

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

		private CaseEntry Recorded(CompoundItem container) => CasesStep.Record(container);

		private static void Take(Item item)
		{
			item.CurrentAddress.Remove(item, false).Succeeded.Should().BeTrue();
		}

		// ---- which containers are cases

		[Fact]
		public void Cases_are_the_containers_in_your_gear_but_not_the_gear()
		{
			var pouch = _body.Wear(EquipmentSlot.SecuredContainer, Gear.NewContainer(Gear.Stash));
			var inPouch = NewCase(DocsCase).In(pouch);

			CasesStep.FindCases(_body.Inventory).Should().BeEquivalentTo(new[] { _case, inPouch });
		}

		[Fact]
		public void A_case_in_the_stash_is_not_on_you()
		{
			NewCase(DocsCase).In(_body.StashBox);

			CasesStep.FindCases(_body.Inventory).Should().Equal(_case);
		}

		[Fact]
		public void The_injector_case_is_a_case_too()
		{
			var injector = Gear.NewContainer(Body.InjectorCase).In(_backpack);

			CasesStep.FindCases(_body.Inventory).Should().BeEquivalentTo(new[] { _case, injector });
		}

		// ---- recording

		[Fact]
		public void Recording_keeps_what_is_in_the_case_by_type_with_counts()
		{
			Put(Key, _case);
			Put(Card, _case);
			Put(Card, _case);

			var entry = Recorded(_case);

			entry.Recorded.Should().BeTrue();
			entry.CaseId.Should().Be(_case.Id.ToString());
			entry.CaseName.Should().Be("Scav backpack");
			entry.Items.Select(item => item.Template).Should().BeEquivalentTo(new[] { Key, Card, Card });
			CasesStep.ItemCount(entry).Should().Be(3);
		}

		[Fact]
		public void Update_would_add_what_is_new_and_remove_what_is_gone()
		{
			Put(Key, _case);
			var gone = Put(Card, _case);
			var entry = Recorded(_case);
			Take(gone);
			Put(Other, _case);

			var change = CasesStep.Diff(_case, entry);

			change.Added.Should().ContainSingle().Which.Template.Should().Be(Other);
			change.Removed.Should().ContainSingle().Which.Template.Should().Be(Card);
			change.AddedCount.Should().Be(1);
			change.RemovedCount.Should().Be(1);
		}

		[Fact]
		public void Update_changes_nothing_when_the_case_is_as_recorded()
		{
			Put(Key, _case);
			var entry = Recorded(_case);

			var change = CasesStep.Diff(_case, entry);

			change.Added.Should().BeEmpty();
			change.Removed.Should().BeEmpty();
		}

		[Fact]
		public void Nothing_to_update_means_the_button_has_no_change_to_show()
		{
			Put(Key, _case);
			var entry = Recorded(_case);

			CasesStep.Diff(_case, entry).Any.Should().BeFalse();
		}

		[Fact]
		public void An_added_item_is_a_change()
		{
			Put(Key, _case);
			var entry = Recorded(_case);
			Put(Other, _case);

			CasesStep.Diff(_case, entry).Any.Should().BeTrue();
		}

		[Fact]
		public void Recording_keeps_where_each_item_sat()
		{
			var grid = _case.Grids[0];
			grid.Add(RealItems.Any(Key), new LocationInGrid(2, 1, ItemRotation.Horizontal), false).Succeeded.Should().BeTrue();

			var entry = Recorded(_case);

			var item = entry.Items.Single();
			(item.Grid, item.X, item.Y, item.Rotation).Should().Be((0, 2, 1, (int)ItemRotation.Horizontal));
		}

		[Fact]
		public void A_new_template_takes_out_the_rest_by_type_and_count()
		{
			var entry = Recorded(_case);

			entry.TakeOutRest.Should().BeTrue();
			entry.Exact.Should().BeFalse();
		}

		[Fact]
		public void Recording_keeps_the_money_apart_from_the_items()
		{
			Put(Key, _case);
			Put(RealItems.Roubles, _case, 30000);
			Put(RealItems.Dollars, _case, 500);

			var entry = Recorded(_case);

			entry.Items.Should().ContainSingle().Which.Template.Should().Be(Key);
			entry.Money.Should().Equal(new Dictionary<string, int> { { "RUB", 30000 }, { "USD", 500 } });
		}

		[Fact]
		public void Recording_an_empty_case_is_a_record_too()
		{
			var entry = Recorded(_case);

			entry.Recorded.Should().BeTrue();
			entry.Items.Should().BeEmpty();
			entry.Money.Should().BeEmpty();
		}

		[Fact]
		public void Recording_again_replaces_the_contents_and_keeps_the_other_settings()
		{
			Put(Key, _case);
			var entry = Recorded(_case);
			entry.Maps.Add("Woods");
			entry.StopsCash = true;
			Put(Card, _case);

			var again = Recorded(_case);

			again.Should().BeSameAs(entry);
			CasesStep.Entries.Should().ContainSingle();
			again.Items.Select(item => item.Template).Should().BeEquivalentTo(new[] { Key, Card });
			again.Maps.Should().Equal("Woods");
			again.StopsCash.Should().BeTrue();
		}

		[Fact]
		public void Another_case_of_the_same_type_has_a_record_of_its_own()
		{
			var second = NewCase(Sicc).In(_backpack);
			Put(Key, _case);
			Recorded(_case);
			Recorded(second);

			CasesStep.Entries.Should().HaveCount(2);
			CasesStep.EntryOf(second.Id.ToString()).Items.Should().BeEmpty();
		}

		[Fact]
		public void Forgetting_keeps_the_maps_and_the_cash_switch_and_drops_a_case_with_nothing_set()
		{
			Put(Key, _case);
			var entry = Recorded(_case);
			entry.Maps.Add("Woods");

			CasesStep.Forget(entry.CaseId);

			entry.Recorded.Should().BeFalse();
			entry.Items.Should().BeEmpty();
			CasesStep.Entries.Should().ContainSingle();

			entry.Maps.Clear();
			CasesStep.Prune(entry);
			CasesStep.Entries.Should().BeEmpty();
		}

		// ---- the plan

		[Fact]
		public void With_nothing_recorded_there_is_nothing_to_say()
		{
			Put(Key, _case);

			Plan().Should().BeEmpty();
		}

		[Fact]
		public void A_recorded_case_that_is_not_on_you_is_left_alone()
		{
			var away = NewCase(DocsCase).In(_body.StashBox);
			Put(Key, away);
			Recorded(away);

			Plan().Should().BeEmpty();
		}

		[Fact]
		public void A_case_as_recorded_is_all_in_place()
		{
			Put(Key, _case);
			Put(Card, _case);
			Recorded(_case);

			var lines = Plan();

			lines.Should().ContainSingle().Which.Text.Should().Be("Scav backpack: 2/2 in place");
		}

		[Fact]
		public async Task A_missing_item_is_brought_from_the_stash()
		{
			Put(Key, _case);
			Put(Card, _case);
			Recorded(_case);
			Take(_case.Grids[0].Items.First(item => item.TemplateId.ToString() == Card));
			var inStash = _body.InStash(RealItems.Any(Card));

			var lines = Plan();

			Lines(lines).Should().Equal("Scav backpack: 1/2 in place", "Bring 1x Salewa to Scav backpack");
			(await RunAll(lines)).Should().BeNull();
			_body.Moves.Single().Item.Should().BeSameAs(inStash);
			_body.Moves.Single().To.Container.ParentItem.Should().BeSameAs(_case);
		}

		[Fact]
		public void A_recorded_item_the_stash_does_not_have_is_a_problem()
		{
			Put(Key, _case);
			Recorded(_case);
			Take(_case.Grids[0].Items.Single());

			var lines = Plan();

			lines.Should().ContainSingle(line => line.Problem).Which.Text.Should().Be("Grizzly: Missing 1: none left in the stash (for Scav backpack)");
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void A_locked_item_in_the_stash_is_not_brought()
		{
			Put(Key, _case);
			Recorded(_case);
			Take(_case.Grids[0].Items.Single());
			_body.InStash(RealItems.Any(Key)).PinLockState = EItemPinLockState.Locked;

			Plan().Should().Contain(line => line.Problem);
		}

		[Fact]
		public async Task Whatever_is_not_recorded_is_taken_out_to_the_stash()
		{
			Put(Key, _case);
			Recorded(_case);
			var stray = Put(Other, _case);

			var lines = Plan();

			Lines(lines).Should().Equal("Scav backpack: 1/1 in place", "Take AFAK out of Scav backpack to the stash");
			(await RunAll(lines)).Should().BeNull();
			_body.Moves.Single().Item.Should().BeSameAs(stray);
			_body.Moves.Single().To.Container.Should().BeSameAs(_body.StashBox.Grids[0]);
		}

		[Fact]
		public void A_case_recorded_empty_is_emptied()
		{
			Recorded(_case);
			Put(Key, _case);
			Put(Card, _case);

			Lines(Plan()).Should().Equal("Scav backpack: 0/0 in place", "Take Grizzly out of Scav backpack to the stash", "Take Salewa out of Scav backpack to the stash");
		}

		[Fact]
		public void Extra_copies_beyond_the_recorded_count_are_taken_out()
		{
			Put(Key, _case);
			Recorded(_case);
			Put(Key, _case);

			Lines(Plan()).Should().Equal("Scav backpack: 1/1 in place", "Take Grizzly out of Scav backpack to the stash");
		}

		[Fact]
		public void Taking_out_comes_before_bringing_so_there_is_room()
		{
			Put(Key, _case);
			Recorded(_case);
			Take(_case.Grids[0].Items.Single());
			Put(Other, _case);
			_body.InStash(RealItems.Any(Key));

			Lines(Plan()).Should().Equal("Scav backpack: 0/1 in place", "Take AFAK out of Scav backpack to the stash", "Bring 1x Grizzly to Scav backpack");
		}

		[Fact]
		public void The_stash_item_is_not_promised_to_two_cases()
		{
			var second = NewCase(DocsCase).In(_backpack);
			Put(Key, _case);
			Put(Key, second);
			Recorded(_case);
			Recorded(second);
			Take(_case.Grids[0].Items.Single());
			Take(second.Grids[0].Items.Single());
			_body.InStash(RealItems.Any(Key));

			var lines = Plan();

			lines.Count(line => line.Run != null).Should().Be(1);
			lines.Count(line => line.Problem).Should().Be(1);
		}

		// ---- what a template looks after is its own business

		[Fact]
		public void Items_in_a_case_with_a_template_are_not_counted_by_the_supplies()
		{
			Put(RealItems.Afak, _case);
			var healing = new HealingStep();
			Only(healing, "health", 300);
			string Heading() => Texts(SupplyFixture.Plan(healing, Game.CarriedAndSecured(_body.Inventory).ToList(), new Item[0])).First();

			var before = Heading();
			Recorded(_case);

			Heading().Should().Be("Healing kits: 0/300 HP");
			before.Should().NotBe("Healing kits: 0/300 HP");
		}

		[Fact]
		public void Items_in_a_nested_container_of_a_templated_case_are_not_counted_either()
		{
			var holder = NewCase(Body.Wallet).In(_case);
			Put(RealItems.Roubles, holder, 1000);
			Recorded(_case);

			Game.InsideTemplatedCase(holder.Grids[0].Items.Single()).Should().BeTrue();
		}

		[Fact]
		public void Items_in_a_case_without_a_template_are_counted_as_before()
		{
			var item = Put(RealItems.Afak, _case);

			Game.InsideTemplatedCase(item).Should().BeFalse();
		}

		[Fact]
		public void An_item_inside_another_case_with_a_template_is_not_taken_from_the_stash()
		{
			var away = NewCase(DocsCase).In(_body.StashBox);
			Put(Key, away);
			Recorded(away);
			Put(Key, _case);
			Recorded(_case);
			Take(_case.Grids[0].Items.Single());

			var lines = Plan();

			lines.Should().Contain(line => line.Problem);
			lines.Should().NotContain(line => line.Run != null);
		}

		// ---- containers inside a case

		[Fact]
		public void A_recorded_holder_stays_and_what_is_inside_it_is_not_the_cases_business()
		{
			var holder = NewCase(Body.Wallet).In(_case);
			Put(RealItems.Roubles, holder, 1000);
			Recorded(_case);

			var lines = Plan();

			lines.Should().ContainSingle().Which.Text.Should().Be("Scav backpack: 1/1 in place");
		}

		[Fact]
		public void A_holder_that_is_not_recorded_is_taken_out_whole()
		{
			Put(Key, _case);
			Recorded(_case);
			NewCase(Body.Wallet).In(_case);

			Lines(Plan()).Should().HaveCount(2).And.Contain(text => text.StartsWith("Take ") && text.EndsWith("out of Scav backpack to the stash"));
		}

		[Fact]
		public void A_holder_has_a_record_of_its_own()
		{
			var holder = NewCase(Body.Wallet).In(_case);
			Put(RealItems.Roubles, holder, 1000);
			var inner = Recorded(holder);
			Recorded(_case);

			inner.Money.Should().Equal(new Dictionary<string, int> { { "RUB", 1000 } });
			CasesStep.EntryOf(_case.Id.ToString()).Items.Should().ContainSingle().Which.Template.Should().Be(Body.Wallet);
		}

		// ---- money

		[Fact]
		public void Money_is_topped_up_to_the_recorded_amount()
		{
			Put(RealItems.Roubles, _case, 100000);
			Recorded(_case);
			_case.Grids[0].Items.Single().StackObjectsCount = 30000;
			_body.InStash(RealItems.Any(RealItems.Roubles, 150000));

			Lines(Plan()).Should().Equal("Scav backpack: 0/1 in place", "Bring ₽ 70,000 to Scav backpack");
		}

		[Fact]
		public void Money_the_stash_cannot_cover_is_a_problem_with_the_amount()
		{
			Put(RealItems.Roubles, _case, 100000);
			Recorded(_case);
			_case.Grids[0].Items.Single().StackObjectsCount = 30000;
			_body.InStash(RealItems.Any(RealItems.Roubles, 20000));

			Lines(Plan()).Should().Equal("Scav backpack: 0/1 in place", "Bring ₽ 20,000 to Scav backpack", "₽: Missing ₽ 50,000: not enough in the stash (for Scav backpack)");
		}

		[Fact]
		public void Extra_money_goes_out_with_take_out_the_rest_and_is_left_without_it()
		{
			Put(RealItems.Roubles, _case, 100000);
			var entry = Recorded(_case);
			_case.Grids[0].Items.Single().StackObjectsCount = 150000;

			Lines(Plan()).Should().Equal("Scav backpack: 1/1 in place", "Take ₽ 50,000 out of Scav backpack to the stash");

			entry.TakeOutRest = false;

			Lines(Plan()).Should().Equal("Scav backpack: 1/1 in place");
		}

		[Fact]
		public void Money_that_is_not_recorded_goes_out_whole()
		{
			Recorded(_case);
			Put(RealItems.Euros, _case, 700);

			Lines(Plan()).Should().Equal("Scav backpack: 0/0 in place", "Take € 700 out of Scav backpack to the stash");
		}

		[Fact]
		public void A_case_recorded_without_cash_gives_up_the_cash_it_holds_and_the_cash_step_can_use_it()
		{
			var entry = Recorded(_case);
			Put(RealItems.Roubles, _case, 77230);

			Lines(Plan()).Should().Equal("Scav backpack: 0/0 in place", "Take ₽ 77,230 out of Scav backpack to the stash");
			Steps.Released["RUB"].Should().Be(77230);
			Steps.Of(Steps.CaseMoney, "RUB").Should().Be(0);
			entry.Money.Should().BeEmpty();
		}

		[Fact]
		public void What_a_case_will_hold_is_what_the_cash_step_counts()
		{
			Put(RealItems.Roubles, _case, 100000);
			Recorded(_case);
			_case.Grids[0].Items.Single().StackObjectsCount = 150000;
			Plan();
			Steps.Of(Steps.CaseMoney, "RUB").Should().Be(100000);

			Steps.NewPass();
			CasesStep.EntryOf(_case.Id.ToString()).TakeOutRest = false;
			Plan();
			Steps.Of(Steps.CaseMoney, "RUB").Should().Be(150000);
		}

		[Fact]
		public void A_case_that_cannot_be_topped_up_counts_for_what_it_will_hold()
		{
			Put(RealItems.Roubles, _case, 100000);
			Recorded(_case);
			_case.Grids[0].Items.Single().StackObjectsCount = 30000;
			_body.InStash(RealItems.Any(RealItems.Roubles, 20000));
			Plan();

			Steps.Of(Steps.CaseMoney, "RUB").Should().Be(50000);
		}

		[Fact]
		public void Money_is_counted_per_currency()
		{
			Put(RealItems.Roubles, _case, 1000);
			Put(RealItems.Dollars, _case, 200);
			Recorded(_case);

			Lines(Plan()).Should().Equal("Scav backpack: 2/2 in place");
		}

		// ---- the cash step is skipped while a case says so, per currency

		private CaseEntry Set(CompoundItem container, System.Action<CaseEntry> set)
		{
			var entry = CasesStep.Touch(container);
			set(entry);
			return entry;
		}

		[Fact]
		public void A_case_you_carry_that_skips_the_cash_step_skips_every_currency()
		{
			var entry = Set(_case, e => { e.StopsCash = true; e.Tag = "qwe"; });

			foreach (var currency in CasesStep.Currencies)
			{
				CasesStep.CashMutedBy(_body.Inventory, currency).Should().BeSameAs(entry);
			}

			CasesStep.CashMuteText(entry).Should().Be("Cash step skipped while you carry");
			CasesStep.LabelOf(entry).Should().Be("qwe");
		}

		[Fact]
		public void A_case_in_the_pouch_counts_too()
		{
			var pouch = _body.Wear(EquipmentSlot.SecuredContainer, Gear.NewContainer(Gear.Stash));
			var inPouch = NewCase(DocsCase).In(pouch);
			var entry = Set(inPouch, e => e.StopsCash = true);

			CasesStep.CashMutedBy(_body.Inventory, "RUB").Should().BeSameAs(entry);
		}

		[Fact]
		public void A_case_in_the_stash_stops_nothing()
		{
			var away = NewCase(DocsCase).In(_body.StashBox);
			Set(away, entry => entry.StopsCash = true);

			CasesStep.CashMutedBy(_body.Inventory, "RUB").Should().BeNull();
		}

		[Fact]
		public void A_case_that_holds_money_does_not_skip_the_cash_step()
		{
			Put(RealItems.Roubles, _case, 5000);
			Recorded(_case);
			Plan();

			foreach (var currency in CasesStep.Currencies)
			{
				CasesStep.CashMutedBy(_body.Inventory, currency).Should().BeNull();
			}
		}

		[Fact]
		public void The_cash_step_brings_only_what_the_case_does_not_hold()
		{
			Put(RealItems.Roubles, _case, 5000);
			Recorded(_case);
			Plan();
			var cash = new CashStep();
			Only(cash, "cash_rub", 75000);
			cash.Groups.First(group => group.Id == "cash_eur").Setting.Target = 500;
			var stash = new[] { RealItems.Any(RealItems.Roubles, 100000), RealItems.Any(RealItems.Euros, 1000) };

			var lines = cash.Plan(null, new List<Item>(), stash.ToList(), currency => CasesStep.CashMutedBy(_body.Inventory, currency));

			lines.Where(line => line.IsProgress).Select(line => line.Skipped).Should().Equal(false, false);
			lines.Should().Contain(line => line.Text == "Bring € 500 to Pockets");
			lines.Should().Contain(line => line.Text == "Bring ₽ 70,000 to Pockets");
		}

		// ---- clearing a case, and what is saved for it

		[Fact]
		public void Clearing_a_case_drops_its_template_maps_and_cash_switch_together()
		{
			var entry = Recorded(_case);
			entry.Maps.Add("Woods");
			entry.StopsCash = true;
			CasesStep.OnItemEvent(Inside(_case));

			CasesStep.Clear(entry.CaseId);

			CasesStep.Entries.Should().BeEmpty();
			CasesStep.Pending.Should().BeEmpty();
		}

		[Fact]
		public void Clearing_a_case_that_was_never_saved_changes_nothing()
		{
			Recorded(_case);

			CasesStep.Clear("somewhere-else");

			CasesStep.Entries.Should().ContainSingle();
		}

		[Fact]
		public void The_summary_says_why_a_case_is_in_the_list()
		{
			Put(Key, _case);
			Put(Card, _case);
			var entry = Recorded(_case);
			CasesStep.Summary(entry).Should().Be("tracking 2 items");

			entry.Maps.Add("Shoreline");
			entry.Maps.Add("Woods");
			entry.StopsCash = true;
			CasesStep.Summary(entry).Should().Be("tracking 2 items · maps: Shoreline, Woods · cash step skipped");

			CasesStep.Forget(entry.CaseId);
			CasesStep.Summary(entry).Should().Be("maps: Shoreline, Woods · cash step skipped");
		}

		[Fact]
		public void The_summary_counts_money_with_the_template()
		{
			Put(RealItems.Roubles, _case, 1000);

			CasesStep.Summary(Recorded(_case)).Should().Be("tracking 0 items and money");
		}

		[Fact]
		public void The_summary_says_item_not_items_for_one()
		{
			Put(Key, _case);

			CasesStep.Summary(Recorded(_case)).Should().Be("tracking 1 item");
		}

		// ---- a card stays collapsed until you open it

		[Fact]
		public void A_case_is_open_until_you_collapse_it_and_stays_collapsed()
		{
			var id = _case.Id.ToString();
			CasesStep.IsCollapsed(id).Should().BeFalse();

			CasesStep.ToggleCollapsed(id);

			CasesStep.IsCollapsed(id).Should().BeTrue();
			// taken off and put on again: nothing here forgets it
			_case.CurrentAddress.Remove(_case, false).Succeeded.Should().BeTrue();
			_case.In(_backpack);
			CasesStep.IsCollapsed(id).Should().BeTrue();

			CasesStep.ToggleCollapsed(id);

			CasesStep.IsCollapsed(id).Should().BeFalse();
		}

		[Fact]
		public void Collapsing_does_not_need_anything_saved_and_saves_nothing_by_itself()
		{
			CasesStep.ToggleCollapsed(_case.Id.ToString());

			CasesStep.Entries.Should().BeEmpty();
		}

		[Fact]
		public void Clearing_a_case_forgets_that_it_was_collapsed()
		{
			var entry = Recorded(_case);
			CasesStep.ToggleCollapsed(entry.CaseId);

			CasesStep.Clear(entry.CaseId);

			CasesStep.IsCollapsed(entry.CaseId).Should().BeFalse();
		}

		// ---- tagging a case changes how it is shown

		[Fact]
		public void Tagging_a_saved_case_updates_its_tag()
		{
			var entry = Recorded(_case);

			CasesStep.OnTagChanged(entry.CaseId, "shrline", 4).Should().BeTrue();

			entry.Tag.Should().Be("shrline");
			entry.TagColor.Should().Be(4);
			CasesStep.LabelOf(entry).Should().Be("shrline");
		}

		[Fact]
		public void Untagging_a_saved_case_clears_its_tag_and_the_label_is_its_name_again()
		{
			var entry = Recorded(_case);
			CasesStep.OnTagChanged(entry.CaseId, "shrline", 4);

			CasesStep.OnTagChanged(entry.CaseId, "", 0).Should().BeTrue();

			entry.Tag.Should().BeNull();
			CasesStep.LabelOf(entry).Should().Be("Scav backpack");
		}

		[Fact]
		public void The_same_tag_again_or_a_case_nobody_saved_changes_nothing()
		{
			var entry = Recorded(_case);
			CasesStep.OnTagChanged(entry.CaseId, "shrline", 4);

			CasesStep.OnTagChanged(entry.CaseId, "shrline", 4).Should().BeFalse();
			CasesStep.OnTagChanged("not-a-saved-case", "x", 1).Should().BeFalse();
		}

		[Fact]
		public void A_case_with_only_maps_set_follows_its_tag_too()
		{
			var entry = Set(_case, e => e.Maps.Add("Woods"));

			CasesStep.OnTagChanged(entry.CaseId, "w", 2).Should().BeTrue();

			entry.Tag.Should().Be("w");
		}

		// ---- taking out the rest, as a switch

		[Fact]
		public void With_take_out_the_rest_off_extra_items_stay_and_only_the_missing_are_brought()
		{
			Put(Key, _case);
			var entry = Recorded(_case);
			entry.TakeOutRest = false;
			Put(Other, _case);
			Put(Key, _case);

			Lines(Plan()).Should().Equal("Scav backpack: 1/1 in place");
		}

		[Fact]
		public void With_take_out_the_rest_off_a_missing_item_is_still_brought()
		{
			Put(Key, _case);
			var entry = Recorded(_case);
			entry.TakeOutRest = false;
			Take(_case.Grids[0].Items.Single());
			Put(Other, _case);
			_body.InStash(RealItems.Any(Key));

			Lines(Plan()).Should().Equal("Scav backpack: 0/1 in place", "Bring 1x Grizzly to Scav backpack");
		}

		// ---- exact positions

		[Fact]
		public void With_exact_positions_an_item_in_the_wrong_cell_is_moved_to_its_own()
		{
			var item = RealItems.Any(Key);
			_case.Grids[0].Add(item, new LocationInGrid(0, 0, ItemRotation.Horizontal), false).Succeeded.Should().BeTrue();
			var entry = Recorded(_case);
			entry.Exact = true;
			Take(item);
			_case.Grids[0].Add(item, new LocationInGrid(2, 1, ItemRotation.Horizontal), false).Succeeded.Should().BeTrue();

			Lines(Plan()).Should().Equal("Scav backpack: 0/1 in place", "Move Grizzly to cell 0,0 of Scav backpack");
		}

		[Fact]
		public void Without_exact_positions_the_same_item_in_another_cell_is_in_place()
		{
			var item = RealItems.Any(Key);
			_case.Grids[0].Add(item, new LocationInGrid(0, 0, ItemRotation.Horizontal), false).Succeeded.Should().BeTrue();
			Recorded(_case);
			Take(item);
			_case.Grids[0].Add(item, new LocationInGrid(2, 1, ItemRotation.Horizontal), false).Succeeded.Should().BeTrue();

			Lines(Plan()).Should().Equal("Scav backpack: 1/1 in place");
		}

		[Fact]
		public void With_exact_positions_and_take_out_the_rest_off_a_stray_stays_but_a_wrong_item_in_a_cell_still_goes()
		{
			var item = RealItems.Any(Key);
			_case.Grids[0].Add(item, new LocationInGrid(0, 0, ItemRotation.Horizontal), false).Succeeded.Should().BeTrue();
			var entry = Recorded(_case);
			entry.Exact = true;
			entry.TakeOutRest = false;
			Take(item);
			var wrong = RealItems.Any(Other);
			_case.Grids[0].Add(wrong, new LocationInGrid(0, 0, ItemRotation.Horizontal), false).Succeeded.Should().BeTrue();
			Put(Card, _case);
			_body.InStash(RealItems.Any(Key));

			Lines(Plan()).Should().Equal("Scav backpack: 0/1 in place", "Bring 1x Grizzly (replaces AFAK)");
		}

		[Fact]
		public void Money_follows_its_own_rules_in_an_exact_case_too()
		{
			Put(RealItems.Roubles, _case, 100000);
			var entry = Recorded(_case);
			entry.Exact = true;
			_case.Grids[0].Items.Single().StackObjectsCount = 30000;
			_body.InStash(RealItems.Any(RealItems.Roubles, 150000));

			Lines(Plan()).Should().Equal("Scav backpack: 0/1 in place", "Bring ₽ 70,000 to Scav backpack");
		}

		// ---- the record follows your hand in the stash

		private static ItemAddress Inside(CompoundItem container) => container.Grids[0].CreateItemAddress(new LocationInGrid(0, 0, ItemRotation.Horizontal));

		[Fact]
		public void A_change_in_a_recorded_case_is_noted()
		{
			Recorded(_case);

			CasesStep.OnItemEvent(Inside(_case));

			CasesStep.Pending.Should().Equal(_case.Id.ToString());
		}

		[Fact]
		public void A_change_in_a_case_you_never_recorded_is_ignored()
		{
			CasesStep.OnItemEvent(Inside(_case));

			CasesStep.Pending.Should().BeEmpty();
		}

		[Fact]
		public void A_case_with_only_maps_set_is_not_a_recorded_case()
		{
			Set(_case, entry => entry.Maps.Add("Woods"));

			CasesStep.OnItemEvent(Inside(_case));

			CasesStep.Pending.Should().BeEmpty();
		}

		[Fact]
		public void Nothing_is_noted_with_the_switch_off()
		{
			Recorded(_case);
			SettingsStore.Current.Cases.AutoUpdate = false;

			CasesStep.OnItemEvent(Inside(_case));

			CasesStep.Pending.Should().BeEmpty();
		}

		[Fact]
		public void A_change_somewhere_else_is_ignored()
		{
			Recorded(_case);

			CasesStep.OnItemEvent(Inside(_backpack));
			CasesStep.OnItemEvent(null);

			CasesStep.Pending.Should().BeEmpty();
		}

		[Fact]
		public void Syncing_records_the_case_again_and_keeps_its_other_settings()
		{
			Put(Key, _case);
			var entry = Recorded(_case);
			entry.Maps.Add("Woods");
			entry.StopsCash = true;
			Put(Card, _case);
			CasesStep.OnItemEvent(Inside(_case));

			CasesStep.SyncPending(_body.Inventory).Should().Be(1);

			entry.Items.Select(item => item.Template).Should().BeEquivalentTo(new[] { Key, Card });
			entry.Maps.Should().Equal("Woods");
			entry.StopsCash.Should().BeTrue();
			CasesStep.Pending.Should().BeEmpty();
		}

		[Fact]
		public void Taking_something_out_by_hand_removes_it_from_the_record()
		{
			Put(Key, _case);
			Put(Card, _case);
			var entry = Recorded(_case);
			Take(_case.Grids[0].Items.First(item => item.TemplateId.ToString() == Card));
			CasesStep.OnItemEvent(Inside(_case));

			CasesStep.SyncPending(_body.Inventory);

			entry.Items.Should().ContainSingle().Which.Template.Should().Be(Key);
		}

		[Fact]
		public void A_recorded_case_lying_in_the_stash_is_synced_too()
		{
			var away = NewCase(DocsCase).In(_body.StashBox);
			var entry = Recorded(away);
			Put(Key, away);
			CasesStep.OnItemEvent(Inside(away));

			CasesStep.SyncPending(_body.Inventory).Should().Be(1);

			entry.Items.Should().ContainSingle().Which.Template.Should().Be(Key);
		}

		[Fact]
		public void A_case_that_is_gone_is_dropped_from_the_waiting_list_without_harm()
		{
			var entry = Recorded(_case);
			CasesStep.OnItemEvent(Inside(_case));
			Take(_case);

			CasesStep.SyncPending(_body.Inventory).Should().Be(0);

			CasesStep.Pending.Should().BeEmpty();
			entry.Recorded.Should().BeTrue();
		}
	}

	// The maps a case is for against the map picked on the map screen
	public class MapTagsTests
	{
		public MapTagsTests() => Reset();

		private static CaseEntry Case(string id, string template, params string[] maps) => new CaseEntry { CaseId = id, CaseName = id, CaseTemplate = template, Maps = maps.ToList() };

		private static MapTags.Report Check(string map, string[] carried, params CaseEntry[] entries) => MapTags.Check(entries, carried, map, true);

		[Theory]
		[InlineData("bigmap", "Customs")]
		[InlineData("factory4_day", "Factory")]
		[InlineData("factory4_night", "Factory")]
		[InlineData("Sandbox", "Ground Zero")]
		[InlineData("Sandbox_high", "Ground Zero")]
		[InlineData("RezervBase", "Reserve")]
		[InlineData("TarkovStreets", "Streets")]
		[InlineData("laboratory", "Labs")]
		public void A_location_of_the_game_is_a_map_of_the_cases(string location, string map)
		{
			MapTags.MapOf(location).Should().Be(map);
		}

		[Fact]
		public void A_location_that_has_no_name_here_is_no_map()
		{
			MapTags.MapOf("develop").Should().BeNull();
			MapTags.MapOf(null).Should().BeNull();
			MapTags.MapOf("").Should().BeNull();
		}

		[Fact]
		public void Every_map_of_the_games_list_has_a_name()
		{
			foreach (var id in new[] { "Woods", "factory4_day", "factory4_night", "bigmap", "Shoreline", "Interchange", "RezervBase", "laboratory", "Lighthouse", "TarkovStreets", "Sandbox", "Sandbox_high" })
			{
				MapTags.MapOf(id).Should().NotBeNull(id);
			}
		}

		[Fact]
		public void The_full_name_is_the_one_the_game_gives_else_the_short_one()
		{
			MapTags.FullName("Streets").Should().Be("Streets");

			Game.Localize = key => key == "TarkovStreets Name" ? "Streets of Tarkov" : key;

			MapTags.FullName("Streets").Should().Be("Streets of Tarkov");
			MapTags.FullName("Woods").Should().Be("Woods");
			MapTags.FullName("Nowhere").Should().Be("Nowhere");
		}

		[Fact]
		public void A_case_you_carry_that_is_for_another_map_is_wrong()
		{
			var sicc = Case("a", "sicc", "Reserve");

			var report = Check("Streets", new[] { "a" }, sicc);

			report.Wrong.Should().Equal(sicc);
			report.Missing.Should().BeEmpty();
		}

		[Theory]
		[InlineData("Customs")]
		[InlineData("Woods")]
		public void A_case_you_carry_that_is_for_the_map_is_not_mentioned(string map)
		{
			Check(map, new[] { "a" }, Case("a", "sicc", "Customs", "Woods")).Any.Should().BeFalse();
		}

		[Fact]
		public void A_case_for_the_map_that_is_not_on_you_is_missing()
		{
			var docs = Case("d", "docs", "Streets");

			var report = Check("Streets", new string[0], docs);

			report.Missing.Should().Equal(docs);
			report.Wrong.Should().BeEmpty();
		}

		[Fact]
		public void Forgetting_to_swap_is_both_the_wrong_case_and_the_missing_one()
		{
			var reserve = Case("a", "sicc", "Reserve");
			var streets = Case("b", "sicc", "Streets");

			var report = Check("Streets", new[] { "a" }, reserve, streets);

			report.Wrong.Should().Equal(reserve);
			report.Missing.Should().Equal(streets);
		}

		[Fact]
		public void A_case_of_the_same_kind_for_the_map_that_you_carry_makes_up_for_the_missing_one()
		{
			var one = Case("a", "sicc", "Streets");
			var two = Case("b", "sicc", "Streets");

			Check("Streets", new[] { "a" }, one, two).Any.Should().BeFalse();
		}

		[Fact]
		public void A_case_of_another_kind_does_not_make_up_for_the_missing_one()
		{
			var sicc = Case("a", "sicc", "Streets");
			var docs = Case("b", "docs", "Streets");

			Check("Streets", new[] { "a" }, sicc, docs).Missing.Should().Equal(docs);
		}

		[Fact]
		public void A_case_with_no_map_is_for_any_and_is_never_mentioned()
		{
			var none = Case("a", "sicc");

			Check("Streets", new[] { "a" }, none).Any.Should().BeFalse();
			Check("Streets", new string[0], none).Any.Should().BeFalse();
		}

		[Fact]
		public void Each_case_is_reported_on_its_own()
		{
			var report = Check("Shoreline", new[] { "a", "c" }, Case("a", "sicc", "Customs"), Case("b", "docs", "Shoreline"), Case("c", "key", "Customs", "Woods"));

			report.Wrong.Select(entry => entry.CaseId).Should().Equal("a", "c");
			report.Missing.Select(entry => entry.CaseId).Should().Equal("b");
		}

		[Fact]
		public void Nothing_is_reported_when_it_is_off_or_the_map_is_unknown()
		{
			var entries = new[] { Case("a", "sicc", "Customs") };

			MapTags.Check(entries, new[] { "a" }, null, true).Any.Should().BeFalse();
			MapTags.Check(entries, new[] { "a" }, "Streets", false).Any.Should().BeFalse();
		}
	}
}
