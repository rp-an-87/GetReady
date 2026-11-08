using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI.Insurance;
using FluentAssertions;
using NSubstitute;
using Xunit;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// Insurance: what counts as gear to insure (worn items, their parts, magazines in containers, never the dog tag, the pockets or the
	// stash's), the plan (trader, count, and the cost when the game already knows every price) and the run, which makes the call the game's
	// insurance screen makes and keeps the same books. The game's own insurance company is real; its session is a stand-in that never
	// answers, and the call to the trader is a stand-in too (the tests say what the server answers).
	public class InsuranceTests
	{
		private const string Prapor = "54cb50c76803fa8b248b4571", Therapist = "54cb57776803fa99248b456e", Armor = "5c0e5bab86f77461f55ed1f3", MagazineCase = "5c052cea86f7746b2101e8d8";

		private readonly Body _body = new Body();
		private readonly ITradingSession _session = Substitute.For<ITradingSession>();
		private readonly InsuranceCompany _company;
		private readonly Trader _prapor = NewTrader(Prapor);
		private readonly Trader _therapist = NewTrader(Therapist);
		private int _roubles = 1000000;
		private readonly List<(Trader Trader, string[] Ids)> _calls = new List<(Trader, string[])>();
		private IResult _answer = SuccessfulResult.New;

		public InsuranceTests()
		{
			Reset();
			_company = new InsuranceCompany(_session, null);
			_session.Insurers.Returns(new[] { _prapor, _therapist }.AsEnumerable());
			InsuranceStep.CompanyOf = () => _company;
			InsuranceStep.InsurersOf = () => new List<Trader> { _prapor, _therapist };
			InsuranceStep.InsureCall = (trader, ids, callback) =>
			{
				_calls.Add((trader, ids));
				callback.Invoke(_answer);
			};
			Steps.NewPass();
			RepairWithKit.RoublesIn = controller => _roubles;
			RepairWithKit.NameOfTrader = trader => trader.Id == Prapor ? "Prapor" : "Therapist";
			SettingsStore.Current.Insurance.Enabled = true;
			SettingsStore.Current.Insurance.TraderId = Prapor;
			Names[Armor] = "6B23";
		}

		private static Trader NewTrader(string id)
		{
#pragma warning disable SYSLIB0050
			var trader = (Trader)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Trader));
#pragma warning restore SYSLIB0050
			typeof(Trader).GetField("Id").SetValue(trader, id);
			return trader;
		}

		// What the tests wear: body armor, a rifle with its magazine, a vest, a backpack and a secure container
		private class Worn
		{
			public Item Armor, Rifle, RifleMag, Vest, Backpack, Secure;
		}

		private Worn Dress()
		{
			var worn = new Worn();
			worn.Armor = _body.Wear(EquipmentSlot.ArmorVest, RealItems.Any(Armor));
			worn.RifleMag = NewMagazine();
			worn.Rifle = _body.Wear(EquipmentSlot.FirstPrimaryWeapon, NewWeapon().With((Magazine)worn.RifleMag));
			worn.Vest = _body.Wear(EquipmentSlot.TacticalVest, NewContainer(Body.Vest));
			worn.Backpack = _body.Wear(EquipmentSlot.Backpack, NewContainer(ScavBackpack));
			worn.Secure = _body.Wear(EquipmentSlot.SecuredContainer, NewContainer(AlphaCase));
			return worn;
		}

		private List<Item> Gear() => InsuranceStep.Insurable(_body.Inventory, _company).Select(item => item.Item).ToList();

		// The trader's answer for items: this price each (-1: the trader will not insure it)
		private void Price(string traderId, int amount, IEnumerable<Item> items)
		{
			foreach (var item in items)
			{
				var price = _company.InsureSummary.AddToItemCache(InsuredItem.FindOrCreate(item), traderId);
				price.Loaded = true;
				price.Amount = amount;
			}
		}

		private List<PlanLine> Plan() => new InsuranceStep().Plan(_body.Controller);

		// ---- what is gear to insure

		[Fact]
		public void Worn_items_and_the_parts_on_them_are_gear_to_insure()
		{
			var worn = Dress();
			Gear().Should().Contain(new[] { worn.Armor, worn.Rifle, worn.RifleMag, worn.Vest, worn.Backpack });
		}

		[Fact]
		public void Magazines_in_your_containers_are_insured_too_also_in_a_case_inside_a_container()
		{
			var worn = Dress();
			var inVest = NewMagazine().In((CompoundItem)worn.Vest);
			var inBackpack = NewMagazine().In((CompoundItem)worn.Backpack);
			var caseInBackpack = NewContainer(MagazineCase).In((CompoundItem)worn.Backpack);
			var inCase = NewMagazine().In(caseInBackpack);
			Gear().Should().Contain(new Item[] { inVest, inBackpack, inCase });
		}

		[Fact]
		public void The_dog_tag_and_the_pockets_are_not_offered_like_in_the_games_insure_all()
		{
			Dress();
			var tag = _body.Wear(EquipmentSlot.Dogtag, RealItems.Any("59f32bb586f774757e1e8442"));
			var pockets = _body.Inventory.Equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem;
			pockets.Should().NotBeNull();
			_company.ItemTypeAvailableForInsurance(InsuredItem.FindOrCreate(tag)).Should().BeTrue("the game's own filter lets a dog tag through, so only our rule keeps it out");

			Gear().Should().NotContain(new[] { tag, pockets });
		}

		[Fact]
		public void Other_loose_items_and_everything_in_the_stash_are_left_out()
		{
			var worn = Dress();
			var rounds = NewAmmo(M855, 30).In((CompoundItem)worn.Vest);
			var medkit = RealItems.Any(Body.Medkit).In((CompoundItem)worn.Vest);
			var stashMag = _body.InStash(NewMagazine());
			var stashVest = _body.InStash(NewContainer(Body.Vest));
			var magInStashVest = NewMagazine().In(stashVest);
			Gear().Should().NotContain(new Item[] { rounds, medkit, stashMag, stashVest, magInStashVest });
		}

		// ---- the plan

		[Fact]
		public void The_plan_says_the_trader_how_many_items_and_the_whole_cost_and_insures_them_all()
		{
			var worn = Dress();
			var inVest = NewMagazine().In((CompoundItem)worn.Vest);
			var gear = Gear();
			Price(Prapor, 1000, gear);

			var lines = Plan();
			var run = lines.Single(line => line.Run != null);
			run.Text.Should().Be($"Insure {gear.Count} items at Prapor for {gear.Count * 1000:N0} roubles");
			run.Insured.Should().Be(gear.Count);
			gear.Should().Contain(inVest);
			lines.Should().NotContain(line => line.Problem);
			lines.First().IsProgress.Should().BeTrue();
			lines.First().Have.Should().Be(0);
			lines.First().Want.Should().Be(gear.Count);
		}

		[Fact]
		public void Not_enough_roubles_for_all_of_it_fails_the_plan_and_insures_nothing()
		{
			Dress();
			var gear = Gear();
			Price(Prapor, 1000, gear);
			_roubles = gear.Count * 1000 - 1;

			var lines = Plan();
			lines.Should().NotContain(line => line.Run != null);
			var problem = lines.Single(line => line.Problem);
			problem.Subject.Should().Contain("Prapor");
			problem.Detail.Should().Contain($"needs {gear.Count * 1000:N0} roubles").And.Contain($"you have {gear.Count * 1000 - 1:N0}");
		}

		[Fact]
		public void Exactly_enough_roubles_is_enough()
		{
			Dress();
			var gear = Gear();
			Price(Prapor, 1000, gear);
			_roubles = gear.Count * 1000;
			Plan().Should().Contain(line => line.Run != null).And.NotContain(line => line.Problem);
		}

		[Fact]
		public void What_the_repairs_at_traders_take_is_not_there_for_insurance()
		{
			Dress();
			var gear = Gear();
			Price(Prapor, 1000, gear);
			_roubles = gear.Count * 1000 + 500;
			Steps.Spent = 600;

			var problem = Plan().Single(line => line.Problem);
			problem.Detail.Should().Contain("you have " + (gear.Count * 1000 - 100).ToString("N0")).And.Contain("600 for repairs");
		}

		[Fact]
		public void The_insurance_it_plans_is_counted_as_spent_for_what_runs_after_it()
		{
			Dress();
			var gear = Gear();
			Price(Prapor, 1000, gear);
			Plan();
			Steps.Spent.Should().Be(gear.Count * 1000);
		}

		[Fact]
		public void Items_already_insured_are_not_insured_again_and_are_counted()
		{
			var worn = Dress();
			var gear = InsuranceStep.Insurable(_body.Inventory, _company);
			Price(Prapor, 1000, gear.Select(item => item.Item));
			var done = gear.Where(item => item.Item == worn.Armor || item.Item == worn.Rifle).ToList();
			foreach (var item in done)
			{
				_company.InsuredItems.Add(item);
			}

			var lines = Plan();
			lines.First().Have.Should().Be(2);
			lines.Single(line => line.Run != null).Insured.Should().Be(gear.Count - 2);
		}

		[Fact]
		public void Everything_insured_leaves_nothing_to_do_and_no_problem()
		{
			Dress();
			var gear = InsuranceStep.Insurable(_body.Inventory, _company);
			Price(Prapor, 1000, gear.Select(item => item.Item));
			foreach (var item in gear)
			{
				_company.InsuredItems.Add(item);
			}

			var lines = Plan();
			lines.Should().NotContain(line => line.Run != null || line.Problem);
			lines.First().Have.Should().Be(gear.Count);
		}

		[Fact]
		public void Without_a_trader_chosen_the_plan_says_to_pick_one()
		{
			Dress();
			SettingsStore.Current.Insurance.TraderId = "";
			var line = Plan().Single();
			line.Problem.Should().BeTrue();
			line.Detail.Should().Contain("pick the trader");
		}

		[Fact]
		public void A_trader_that_no_longer_insures_is_a_problem()
		{
			Dress();
			SettingsStore.Current.Insurance.TraderId = "5ac3b934156ae10c4430e83c";
			Plan().Single().Detail.Should().Contain("does not insure any more");
		}

		[Fact]
		public void Nothing_worn_is_not_a_problem()
		{
			var lines = Plan();
			lines.Should().NotContain(line => line.Problem || line.Run != null);
		}

		[Fact]
		public void The_chosen_trader_is_the_one_whose_price_counts()
		{
			Dress();
			var gear = Gear();
			Price(Prapor, 1000, gear);
			Price(Therapist, 400, gear);
			SettingsStore.Current.Insurance.TraderId = Therapist;
			Plan().Single(line => line.Run != null).Text.Should().Be($"Insure {gear.Count} items at Therapist for {gear.Count * 400:N0} roubles");
		}

		// ---- prices the game does not know

		private int moves, problems;

		[Fact]
		public void Without_prices_the_plan_still_says_what_to_insure_and_nothing_is_asked_for()
		{
			Dress();
			var count = Gear().Count;

			var lines = Plan();
			lines.Should().NotContain(line => line.Problem);
			var run = lines.Single(line => line.Run != null);
			run.Text.Should().Be($"Insure {count} items at Prapor");
			run.Insured.Should().Be(count);
			ReadyPlan.TallyLines(lines, ref moves, ref problems);
			moves.Should().Be(1);
			problems.Should().Be(0);
			_session.DidNotReceiveWithAnyArgs().GetInsurancePrice(default, default, default);
		}

		[Fact]
		public void The_cost_is_only_shown_when_every_price_is_known()
		{
			var worn = Dress();
			var gear = Gear();
			Price(Prapor, 1000, gear.Where(item => item != worn.Armor));

			Plan().Single(line => line.Run != null).Text.Should().Be($"Insure {gear.Count} items at Prapor");
		}

		[Fact]
		public void A_price_the_trader_refused_does_not_leave_an_item_out_or_fail_the_plan()
		{
			var worn = Dress();
			var gear = Gear();
			Price(Prapor, 1000, gear);
			Price(Prapor, -1, new[] { worn.Armor });

			var lines = Plan();
			lines.Should().NotContain(line => line.Problem);
			lines.Single(line => line.Run != null).Insured.Should().Be(gear.Count);
		}

		// ---- the run

		[Fact]
		public async Task Run_makes_the_call_of_the_insurance_screen_for_what_is_not_insured_and_keeps_its_books()
		{
			var worn = Dress();
			var gear = InsuranceStep.Insurable(_body.Inventory, _company);
			var done = gear.First(item => item.Item == worn.Armor);
			_company.InsuredItems.Add(done);

			var error = await Plan().Single(line => line.Run != null).Run();

			error.Should().BeNull();
			_calls.Should().HaveCount(1);
			_calls[0].Trader.Should().BeSameAs(_prapor);
			_calls[0].Ids.Should().BeEquivalentTo(gear.Where(item => item != done).Select(item => item.Id));
			gear.Should().OnlyContain(item => _company.Insured(item.Id));
			gear.Where(item => item != done).Should().OnlyContain(item => item.InsurerId == Prapor);
		}

		[Fact]
		public async Task Run_uses_the_trader_chosen_in_setup()
		{
			Dress();
			SettingsStore.Current.Insurance.TraderId = Therapist;

			await Plan().Single(line => line.Run != null).Run();

			_calls.Single().Trader.Should().BeSameAs(_therapist);
		}

		[Fact]
		public async Task A_refusal_of_the_server_is_the_error_and_nothing_is_marked_insured()
		{
			Dress();
			_answer = new FailedResult("not enough roubles", 0);

			var error = await Plan().Single(line => line.Run != null).Run();

			error.Should().Contain("not enough roubles");
			Gear().Should().NotContain(item => _company.Insured(InsuredItem.FindOrCreate(item).Id));
		}

		[Fact]
		public async Task Not_enough_roubles_is_said_in_words_with_what_the_trader_wants()
		{
			Dress();
			_answer = new FailedResult("Not enough money to complete transaction cost of: 128163", 0);

			var line = Plan().Single(each => each.Run != null);
			var error = await line.Run();

			error.Should().Be("not enough roubles in the stash, Prapor wants 128,163");
			line.FailPrefix.Should().BeEmpty("the window shows the words as they are, not as a failed move");
		}

		[Fact]
		public void Other_refusals_are_passed_on_as_they_are_and_an_empty_one_is_named()
		{
			InsuranceStep.Explained("Item not found", "Prapor").Should().Be("Item not found");
			InsuranceStep.Explained(null, "Prapor").Should().Be("the server refused");
			InsuranceStep.Explained("Not enough money", "Prapor").Should().Be("not enough roubles in the stash to insure at Prapor");
		}

		[Fact]
		public async Task Run_with_everything_insured_makes_no_call()
		{
			Dress();
			var line = Plan().Single(each => each.Run != null);
			foreach (var item in InsuranceStep.Insurable(_body.Inventory, _company))
			{
				_company.InsuredItems.Add(item);
			}

			(await line.Run()).Should().BeNull();
			_calls.Should().BeEmpty();
		}
	}
}
