using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.Repairing;
using EFT.Trading;
using FluentAssertions;
using NSubstitute;
using Xunit;
using static GetReady.RepairWithKit;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// Repair: the decision for each piece (kits, a trader, or why not), doing it, and the Repair step's plan. The game's repair strategy
	// (what it costs, what it accepts) is played by a small stand-in with the same rules of arithmetic: a kit pays one resource per point
	// and a trader asks 1000 roubles a point.
	public class RepairTests
	{
		private const string Armor = "5c0e5bab86f77461f55ed1f3", ArmorKit = "591094e086f7747caa7bb2ef", Prapor = "54cb50c76803fa8b248b4571", Therapist = "54cb57776803fa99248b456e";

		// The state of the piece the stand-in strategy answers for
		private class Piece
		{
			public float Need;
			public float Max = 100;
			public List<RepairKitsCollection> Collections = new List<RepairKitsCollection>();
			public List<Trader> Traders = new List<Trader>();
			public string Error;
			public int Requests;
			public List<float> Amounts = new List<float>();
			public Trader Current;
		}

		private static readonly EFT.Repairing.RepairController Rc = (EFT.Repairing.RepairController)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EFT.Repairing.RepairController));
		private readonly Body _body = new Body();
		private readonly Dictionary<Item, Piece> _pieces = new Dictionary<Item, Piece>();
		private readonly List<RepairKit> _kits = new List<RepairKit>();
		private int _roubles = 100000;

		public RepairTests()
		{
			Reset();
			SettingsStore.Current.Repair.Armor = true;
			SettingsStore.Current.Repair.Weapons = true;
			SettingsStore.Current.Repair.UseTraders = true;
			Names[Armor] = "6B23";
			StrategyOf = (item, controller) => Strategy(_pieces.TryGetValue(item, out var piece) ? piece : new Piece());
			Pause = () => Task.CompletedTask;
			RoublesIn = controller => _roubles;
			NameOfTrader = trader => trader.Id == Prapor ? "Prapor" : "Therapist";
			RepairWithKit.Controller = () => (EFT.Repairing.RepairController)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EFT.Repairing.RepairController));
		}

		private static Trader NewTrader(string id)
		{
			var trader = (Trader)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Trader));
			typeof(Trader).GetField("Id").SetValue(trader, id);
			return trader;
		}

		private IRepairStrategy Strategy(Piece piece)
		{
			var strategy = Substitute.For<IRepairStrategy>();
			strategy.HowMuchRepairScoresCanAccept().Returns(call => piece.Need);
			strategy.Durability().Returns(call => piece.Max - piece.Need);
			strategy.RepairKitsCollections.Returns(piece.Collections);
			strategy.Repairers.Returns(call => piece.Traders.Cast<IRepairer>().Concat(piece.Collections));
			strategy.GetRepairPrice(Arg.Any<float>(), Arg.Any<RepairKitsCollection>()).Returns(call => (double)call.Arg<float>());
			strategy.GetCurrencyPrice(Arg.Any<float>()).Returns(call => (int)(call.Arg<float>() * 1000));
			strategy.RepairItem(Arg.Any<float>(), Arg.Any<RepairKit>()).Returns(call =>
			{
				piece.Requests++;
				if (piece.Error != null)
				{
					return Task.FromResult<IResult>(new FailedResult(piece.Error, 0));
				}

				float amount = call.Arg<float>();
				piece.Amounts.Add(amount);
				piece.Need = Math.Max(0, piece.Need - amount);
				var kit = call.Arg<RepairKit>();
				if (kit != null)
				{
					kit.GetItemComponent<RepairKitComponent>().Resource -= amount;
				}
				else
				{
					_roubles -= (int)(amount * 1000);
				}

				return Task.FromResult<IResult>(SuccessfulResult.New);
			});
			return strategy;
		}

		// A weapon that needs this many points of repair
		private Weapon Damaged(float need, string template = M4A1)
		{
			var weapon = NewWeapon(template);
			_pieces[weapon] = new Piece { Need = need };
			return weapon;
		}

		private RepairKit Kit(Piece piece, float resource)
		{
			var kit = (RepairKit)RealItems.Any(ArmorKit);
			kit.GetItemComponent<RepairKitComponent>().Resource = resource;
			var collection = piece.Collections.FirstOrDefault();
			if (collection == null)
			{
				collection = new RepairKitsCollection((RepairKitTemplate)kit.Template, null);
				piece.Collections.Add(collection);
			}

			collection.AddRepairKit(kit);
			_kits.Add(kit);
			return kit;
		}

		private static Trader Offer(Piece piece, string id)
		{
			var trader = NewTrader(id);
			piece.Traders.Add(trader);
			return trader;
		}

		private RepairDecision Decide(Item item) => RepairWithKit.Decide(item, null, _body.Controller, 0);

		// ---- the decision

		[Fact]
		public void Something_with_nothing_to_repair_needs_nothing()
		{
			RepairWithKit.Decide(RealItems.Any(RealItems.Afak), null, _body.Controller, 0).Kind.Should().Be(RepairKind.Nothing);
		}

		[Fact]
		public void A_piece_that_is_whole_needs_nothing()
		{
			var weapon = Damaged(0.05f);

			Decide(weapon).Kind.Should().Be(RepairKind.Nothing);
		}

		[Fact]
		public void A_piece_with_a_point_to_repair_is_repaired_with_the_kits()
		{
			var weapon = Damaged(10);
			Kit(_pieces[weapon], 500);

			Decide(weapon).Kind.Should().Be(RepairKind.Kits);
		}

		[Fact]
		public void A_kit_with_hardly_anything_left_does_not_count()
		{
			var weapon = Damaged(10);
			Kit(_pieces[weapon], 1.5f);
			Offer(_pieces[weapon], Prapor);

			Decide(weapon).Kind.Should().Be(RepairKind.Trader);
		}

		[Fact]
		public void A_locked_kit_does_not_count()
		{
			var weapon = Damaged(10);
			Kit(_pieces[weapon], 500).PinLockState = EItemPinLockState.Locked;
			Offer(_pieces[weapon], Prapor);

			Decide(weapon).Kind.Should().Be(RepairKind.Trader);
		}

		[Fact]
		public void Without_kits_and_with_traders_off_it_is_a_problem()
		{
			SettingsStore.Current.Repair.UseTraders = false;
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Prapor);

			var decision = Decide(weapon);

			decision.Kind.Should().Be(RepairKind.Problem);
			decision.Problem.Should().Be("needs repair, but you have no repair kit with resource left (traders are switched off)");
		}

		[Fact]
		public void Without_kits_a_trader_who_repairs_it_is_asked()
		{
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Prapor);

			var decision = Decide(weapon);

			decision.Kind.Should().Be(RepairKind.Trader);
			decision.Quote.Price.Should().Be(10000);
			decision.Quote.Money.Should().Be(100000);
			decision.Quote.Trader.Id.Should().Be(Prapor);
		}

		[Fact]
		public void Kits_come_before_a_trader()
		{
			var weapon = Damaged(10);
			Kit(_pieces[weapon], 500);
			Offer(_pieces[weapon], Prapor);

			Decide(weapon).Kind.Should().Be(RepairKind.Kits);
		}

		[Fact]
		public void The_first_trader_in_your_order_is_the_one_asked()
		{
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Prapor);
			Offer(_pieces[weapon], Therapist);
			SettingsStore.Current.Repair.TraderOrder = new List<string> { Therapist, Prapor };

			Decide(weapon).Quote.Trader.Id.Should().Be(Therapist);
		}

		[Fact]
		public void Traders_not_in_your_order_come_after_the_ones_that_are_in_the_game_order()
		{
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Therapist);
			Offer(_pieces[weapon], Prapor);
			SettingsStore.Current.Repair.TraderOrder = new List<string> { Prapor };

			TradersFor(weapon, null).Select(trader => trader.Id).Should().Equal(Prapor, Therapist);
		}

		[Fact]
		public void With_no_trader_that_repairs_it_it_is_a_problem()
		{
			var weapon = Damaged(10);

			var decision = Decide(weapon);

			decision.Kind.Should().Be(RepairKind.Problem);
			decision.Problem.Should().Be("needs repair, but you have no repair kit with resource left and no trader repairs it");
		}

		[Fact]
		public void Not_enough_roubles_is_a_problem_that_says_how_many()
		{
			_roubles = 4000;
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Prapor);

			var decision = Decide(weapon);

			decision.Kind.Should().Be(RepairKind.Problem);
			decision.Problem.Should().Be("needs repair: Prapor wants 10,000 roubles, you have 4,000");
		}

		[Fact]
		public void Roubles_earlier_pieces_already_take_are_not_counted_again()
		{
			_roubles = 12000;
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Prapor);

			var decision = RepairWithKit.Decide(weapon, null, _body.Controller, 5000);

			decision.Kind.Should().Be(RepairKind.Problem);
			decision.Problem.Should().Contain("you have 7,000");
		}

		[Fact]
		public void Exactly_enough_roubles_is_enough()
		{
			_roubles = 10000;
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Prapor);

			Decide(weapon).Kind.Should().Be(RepairKind.Trader);
		}

		[Fact]
		public void When_the_game_cannot_say_what_a_piece_needs_it_is_left_alone()
		{
			var weapon = NewWeapon();
			StrategyOf = (item, controller) => throw new InvalidOperationException("boom");

			Decide(weapon).Kind.Should().Be(RepairKind.Nothing);
		}

		[Fact]
		public void When_asking_the_traders_fails_it_is_a_problem_not_a_crash()
		{
			var weapon = Damaged(10);
			var piece = _pieces[weapon];
			var calls = 0;
			StrategyOf = (item, controller) =>
			{
				// the first questions (does it need repair) work, then asking about the traders blows up
				if (++calls > 1)
				{
					throw new InvalidOperationException("boom");
				}

				return Strategy(piece);
			};

			var decision = Decide(weapon);

			decision.Kind.Should().Be(RepairKind.Problem);
			decision.Problem.Should().Contain("no trader repairs it");
		}

		// ---- what is left over

		[Fact]
		public void A_piece_that_was_repaired_in_full_has_no_shortfall()
		{
			Shortfall(new RepairDecision { Kind = RepairKind.Kits }, new RepairOutcome { Whole = true }).Should().BeNull();
		}

		[Fact]
		public void A_failed_request_is_the_shortfall()
		{
			Shortfall(new RepairDecision { Kind = RepairKind.Kits }, new RepairOutcome { Error = "server said no" }).Should().Be("server said no");
		}

		[Fact]
		public void Kits_that_ran_out_say_where_the_piece_is()
		{
			Shortfall(new RepairDecision { Kind = RepairKind.Kits }, new RepairOutcome { Durability = 82.5f }).Should().Be("the repair kits ran out, it is at 82.5");
		}

		[Fact]
		public void A_trader_that_left_it_says_so()
		{
			Shortfall(new RepairDecision { Kind = RepairKind.Trader }, new RepairOutcome { Durability = 82.5f }).Should().Be("the trader left it at 82.5");
		}

		// ---- repairing with kits

		[Fact]
		public async Task One_kit_with_enough_repairs_the_piece_in_full()
		{
			var weapon = Damaged(10);
			var kit = Kit(_pieces[weapon], 500);

			var outcome = await RepairAll(Rc, weapon);

			outcome.Whole.Should().BeTrue();
			outcome.Steps.Should().Be(1);
			outcome.Error.Should().BeNull();
			kit.Resource.Should().Be(490);
		}

		[Fact]
		public async Task A_kit_is_never_taken_past_what_it_holds()
		{
			var weapon = Damaged(100);
			var kit = Kit(_pieces[weapon], 40);

			var outcome = await RepairAll(Rc, weapon);

			outcome.Whole.Should().BeFalse();
			kit.Resource.Should().BeGreaterOrEqualTo(1f);
			_pieces[weapon].Amounts.Sum().Should().BeLessThan(40);
		}

		[Fact]
		public async Task A_kit_that_is_nearly_used_up_is_left_alone()
		{
			var weapon = Damaged(10);
			Kit(_pieces[weapon], 1.5f);

			var outcome = await RepairAll(Rc, weapon);

			outcome.Steps.Should().Be(0);
			outcome.Whole.Should().BeFalse();
			_pieces[weapon].Requests.Should().Be(0);
		}

		[Fact]
		public async Task A_locked_kit_is_not_used()
		{
			var weapon = Damaged(10);
			Kit(_pieces[weapon], 500).PinLockState = EItemPinLockState.Locked;

			var outcome = await RepairAll(Rc, weapon);

			outcome.Steps.Should().Be(0);
			_pieces[weapon].Requests.Should().Be(0);
		}

		[Fact]
		public async Task The_kit_with_least_left_is_used_first()
		{
			var weapon = Damaged(10);
			var big = Kit(_pieces[weapon], 500);
			var small = Kit(_pieces[weapon], 100);

			await RepairAll(Rc, weapon);

			small.Resource.Should().Be(90);
			big.Resource.Should().Be(500);
		}

		[Fact]
		public async Task When_the_small_kit_cannot_finish_the_next_one_does()
		{
			var weapon = Damaged(120);
			var big = Kit(_pieces[weapon], 500);
			var small = Kit(_pieces[weapon], 60);

			var outcome = await RepairAll(Rc, weapon);

			outcome.Whole.Should().BeTrue();
			outcome.Steps.Should().Be(2);
			small.Resource.Should().BeGreaterOrEqualTo(1f);
			big.Resource.Should().BeLessThan(500);
		}

		[Fact]
		public async Task A_failed_request_stops_the_repair_and_is_reported()
		{
			var weapon = Damaged(10);
			Kit(_pieces[weapon], 500);
			_pieces[weapon].Error = "server error";

			var outcome = await RepairAll(Rc, weapon);

			outcome.Error.Should().Be("server error");
			outcome.Steps.Should().Be(0);
			outcome.Whole.Should().BeFalse();
		}

		[Fact]
		public async Task A_crash_in_the_game_is_reported_not_thrown()
		{
			var weapon = Damaged(10);
			StrategyOf = (item, controller) => throw new InvalidOperationException("boom");

			var outcome = await RepairAll(Rc, weapon);

			outcome.Error.Should().Be("boom");
		}

		[Fact]
		public async Task A_piece_that_needs_nothing_takes_no_request()
		{
			var weapon = Damaged(0);
			Kit(_pieces[weapon], 500);

			var outcome = await RepairAll(Rc, weapon);

			outcome.Whole.Should().BeTrue();
			outcome.Steps.Should().Be(0);
			_pieces[weapon].Requests.Should().Be(0);
		}

		// ---- repairing at a trader

		[Fact]
		public async Task A_trader_repairs_the_whole_piece_for_the_price()
		{
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Prapor);

			var outcome = await RepairAtTrader(Rc, _body.Controller, weapon, Prapor);

			outcome.Whole.Should().BeTrue();
			outcome.Steps.Should().Be(1);
			_roubles.Should().Be(90000);
		}

		[Fact]
		public async Task A_trader_that_no_longer_repairs_it_is_an_error()
		{
			var weapon = Damaged(10);

			var outcome = await RepairAtTrader(Rc, _body.Controller, weapon, Prapor);

			outcome.Error.Should().Be("the trader can not repair it any more");
		}

		[Fact]
		public async Task Not_enough_roubles_is_an_error_and_nothing_is_paid()
		{
			_roubles = 100;
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Prapor);

			var outcome = await RepairAtTrader(Rc, _body.Controller, weapon, Prapor);

			outcome.Error.Should().Be("Prapor wants 10,000 roubles, you have 100");
			_pieces[weapon].Requests.Should().Be(0);
		}

		[Fact]
		public async Task A_piece_already_whole_costs_nothing_at_the_trader()
		{
			var weapon = Damaged(0);
			Offer(_pieces[weapon], Prapor);

			var outcome = await RepairAtTrader(Rc, _body.Controller, weapon, Prapor);

			outcome.Whole.Should().BeTrue();
			outcome.Steps.Should().Be(0);
			_roubles.Should().Be(100000);
		}

		[Fact]
		public async Task A_failed_request_at_the_trader_is_reported()
		{
			var weapon = Damaged(10);
			Offer(_pieces[weapon], Prapor);
			_pieces[weapon].Error = "server error";

			var outcome = await RepairAtTrader(Rc, _body.Controller, weapon, Prapor);

			outcome.Error.Should().Be("server error");
			outcome.Steps.Should().Be(0);
		}

		[Fact]
		public async Task Execute_does_what_the_decision_said()
		{
			var weapon = Damaged(10);
			var kit = Kit(_pieces[weapon], 500);
			Offer(_pieces[weapon], Prapor);

			await Execute(Decide(weapon), Rc, _body.Controller, weapon);

			kit.Resource.Should().Be(490);
			_roubles.Should().Be(100000);
		}

		// ---- the Repair step's plan

		private readonly RepairStep _step = new RepairStep();

		private Weapon Wear(Weapon weapon, EquipmentSlot slot = EquipmentSlot.FirstPrimaryWeapon) => _body.Wear(slot, weapon);

		private List<PlanLine> Plan() => _step.Plan(_body.Controller);

		[Fact]
		public void Without_a_repair_controller_the_step_says_it_is_not_available()
		{
			RepairWithKit.Controller = () => null;

			Texts(Plan()).Should().Equal("Repair isn't available on this screen.");
		}

		[Fact]
		public void With_armor_and_weapons_both_off_the_step_says_so()
		{
			SettingsStore.Current.Repair.Armor = false;
			SettingsStore.Current.Repair.Weapons = false;

			Texts(Plan()).Should().Equal("Armor and weapons are both switched off.");
		}

		[Fact]
		public void With_nothing_equipped_the_step_says_so()
		{
			Texts(Plan()).Should().Equal("No armor or weapon equipped.");
		}

		[Fact]
		public void Gear_in_shape_is_counted()
		{
			Wear(Damaged(0));

			Texts(Plan()).Should().Equal("Gear: 1/1 in shape");
		}

		[Fact]
		public void A_damaged_piece_with_a_kit_is_repaired_with_it()
		{
			var weapon = Wear(Damaged(10));
			Kit(_pieces[weapon], 500);

			var lines = Plan();

			Texts(lines).Should().Equal("Gear: 0/1 in shape", "Repair M4A1");
			lines[1].Repair.Should().BeTrue();
		}

		[Fact]
		public async Task Running_the_line_repairs_the_piece()
		{
			var weapon = Wear(Damaged(10));
			var kit = Kit(_pieces[weapon], 500);

			var error = await Plan()[1].Run();

			error.Should().BeNull();
			kit.Resource.Should().Be(490);
		}

		[Fact]
		public async Task Running_a_line_that_cannot_finish_says_why()
		{
			var weapon = Wear(Damaged(100));
			Kit(_pieces[weapon], 40);

			var error = await Plan()[1].Run();

			error.Should().StartWith("the repair kits ran out, it is at ");
		}

		[Fact]
		public void A_piece_for_a_trader_says_who_and_what_it_costs()
		{
			var weapon = Wear(Damaged(10));
			Offer(_pieces[weapon], Prapor);

			Texts(Plan()).Should().Equal("Gear: 0/1 in shape", "Repair M4A1 at Prapor for 10,000 roubles");
		}

		[Fact]
		public void A_piece_nobody_can_repair_is_a_problem()
		{
			Wear(Damaged(10));

			var lines = Plan();

			lines.Should().Contain(line => line.Problem && line.Subject == "M4A1");
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void Roubles_are_shared_out_across_the_pieces_in_order()
		{
			_roubles = 15000;
			var first = Wear(Damaged(10), EquipmentSlot.FirstPrimaryWeapon);
			Offer(_pieces[first], Prapor);
			var second = Wear(Damaged(10, Glock18), EquipmentSlot.Holster);
			Offer(_pieces[second], Prapor);

			var lines = Plan();

			lines.Count(line => line.Run != null).Should().Be(1);
			lines.Should().Contain(line => line.Problem && line.Detail.Contains("you have 5,000"));
		}

		[Fact]
		public void A_piece_on_the_no_repair_list_is_left_damaged_and_counts_as_fine()
		{
			var weapon = Wear(Damaged(10));
			Kit(_pieces[weapon], 500);
			RepairStep.ToggleKept(weapon);

			var lines = Plan();

			Texts(lines).Should().Equal("Gear: 1/1 in shape", "M4A1: excluded from repairs (on your no-repair list)");
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void The_no_repair_list_is_the_very_piece_not_the_type()
		{
			var kept = Damaged(10);
			RepairStep.ToggleKept(kept);
			var other = Wear(Damaged(10));
			Kit(_pieces[other], 500);

			RepairStep.IsKept(kept).Should().BeTrue();
			RepairStep.IsKept(other).Should().BeFalse();
			Plan().Should().Contain(line => line.Repair);
		}

		[Fact]
		public void Toggling_the_list_twice_takes_the_piece_off_again()
		{
			var weapon = Damaged(10);

			RepairStep.ToggleKept(weapon);
			RepairStep.IsKept(weapon).Should().BeTrue();
			SettingsStore.Current.Repair.KeepDamaged.Single().Name.Should().Be("M4A1");

			RepairStep.ToggleKept(weapon);
			RepairStep.IsKept(weapon).Should().BeFalse();
		}

		[Fact]
		public void Weapons_are_only_looked_at_when_that_is_switched_on()
		{
			SettingsStore.Current.Repair.Weapons = false;
			var weapon = Wear(Damaged(10));
			Kit(_pieces[weapon], 500);

			Texts(Plan()).Should().Equal("No armor or weapon equipped.");
		}

		[Fact]
		public void Armor_is_only_looked_at_when_that_is_switched_on()
		{
			SettingsStore.Current.Repair.Armor = false;
			_body.Wear(EquipmentSlot.ArmorVest, RealItems.Any(Armor));

			Texts(Plan()).Should().Equal("No armor or weapon equipped.");
		}

		[Fact]
		public void Worn_armor_is_gear()
		{
			var armor = _body.Wear(EquipmentSlot.ArmorVest, RealItems.Any(Armor));

			RepairStep.EquippedGear(_body.Inventory).Should().Equal(armor);
		}

		[Fact]
		public void Weapons_in_the_weapon_slots_are_gear_when_switched_on()
		{
			var rifle = Wear(NewWeapon());

			RepairStep.EquippedGear(_body.Inventory).Should().Equal(rifle);
		}

		[Fact]
		public void The_progress_line_shows_the_first_piece_that_needs_work()
		{
			Wear(Damaged(0), EquipmentSlot.FirstPrimaryWeapon);
			var pistol = Wear(Damaged(10, Glock18), EquipmentSlot.Holster);
			Kit(_pieces[pistol], 500);

			Plan()[0].Template.Should().Be(Glock18);
		}

		// ---- the trader order

		private static List<Trader> ControllerWith(params string[] ids) => ids.Select(NewTrader).ToList();

		[Fact]
		public void Traders_follow_your_order_and_new_ones_come_last()
		{
			SettingsStore.Current.Repair.TraderOrder = new List<string> { Therapist, Prapor };

			RepairStep.OrderedTraders(ControllerWith("new1", Prapor, Therapist, "new2")).Select(trader => trader.Id).Should().Equal(Therapist, Prapor, "new1", "new2");
		}

		[Fact]
		public void Moving_a_trader_down_swaps_it_with_the_next()
		{
			var controller = ControllerWith(Prapor, Therapist);

			RepairStep.MoveTrader(controller, Prapor, 1);

			SettingsStore.Current.Repair.TraderOrder.Should().Equal(Therapist, Prapor);
		}

		[Fact]
		public void Moving_the_first_trader_up_or_the_last_down_changes_nothing()
		{
			var controller = ControllerWith(Prapor, Therapist);

			RepairStep.MoveTrader(controller, Prapor, -1);
			RepairStep.MoveTrader(controller, Therapist, 1);

			SettingsStore.Current.Repair.TraderOrder.Should().BeEmpty();
		}

		[Fact]
		public void Moving_a_trader_that_is_not_there_changes_nothing()
		{
			RepairStep.MoveTrader(ControllerWith(Prapor, Therapist), "ghost", 1);

			SettingsStore.Current.Repair.TraderOrder.Should().BeEmpty();
		}
	}
}
