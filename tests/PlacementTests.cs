using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// One way to move anything: "places" are names, an item goes to the first place that takes it.
	public class PlacementTests
	{
		private readonly Body _body = new Body();
		private readonly CompoundItem _backpack;

		public PlacementTests()
		{
			Reset();
			_backpack = _body.Wear(EquipmentSlot.Backpack, NewContainer(ScavBackpack));
		}

		private Task<string> MoveTo(Item item, params string[] places) => Game.MoveTo(_body.Controller, item, places);

		private static string Place(Body.Move move) => Body.Where(move);

		// ---- which place takes it

		[Fact]
		public async Task An_item_goes_to_the_first_place_that_takes_it()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));

			(await MoveTo(cms, "Special slots", "Pockets")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be("SpecialSlot1");
		}

		[Fact]
		public async Task The_next_place_is_tried_when_the_first_has_no_room()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));
			for (int slot = 1; slot <= 3; slot++)
			{
				SpecialSlotsStep.SlotsOf(_body.Inventory).First(candidate => candidate.ID == "SpecialSlot" + slot).Add(RealItems.Any(RealItems.Cms));
			}

			(await MoveTo(cms, "Special slots", "Backpack")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be(ScavBackpack);
		}

		[Fact]
		public async Task A_place_you_do_not_have_is_skipped()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));

			(await MoveTo(cms, "Vest", "Backpack")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be(ScavBackpack);
		}

		[Fact]
		public async Task With_no_place_that_exists_the_last_one_is_named()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));

			(await MoveTo(cms, "Vest", "Pouch")).Should().Be("no Pouch on you");
			_body.Moves.Should().BeEmpty();
		}

		[Fact]
		public async Task With_no_places_at_all_there_is_nowhere_to_put_it()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));

			(await MoveTo(cms)).Should().Be("no place to put it");
		}

		[Fact]
		public async Task An_unknown_place_name_is_a_place_you_do_not_have()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));

			(await MoveTo(cms, "Nowhere")).Should().Be("no Nowhere on you");
		}

		[Fact]
		public async Task The_special_slots_do_not_take_a_plain_item()
		{
			var bandage = _body.InStash(RealItems.Any(RealItems.ArmyBandage));

			var error = await MoveTo(bandage, "Special slots");

			error.Should().NotBeNull();
			_body.Moves.Should().BeEmpty();
		}

		[Fact]
		public async Task The_pockets_take_it_in_a_grid_or_a_slot()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));

			(await MoveTo(cms, "Pockets")).Should().BeNull();

			_body.Moves.Should().ContainSingle();
		}

		[Fact]
		public async Task Full_pockets_say_there_is_no_room_not_what_a_special_slot_filters()
		{
			try
			{
				while (true)
				{
					_body.OnYou(RealItems.Any(RealItems.ArmyBandage));
				}
			}
			catch (System.InvalidOperationException)
			{
				// the pockets grid is full
			}

			var grenade = _body.InStash(RealItems.Any(RealItems.Rgd5));

			var error = await MoveTo(grenade, "Pockets");

			error.Should().NotBeNull();
			error.Should().NotContain("filter");
			_body.Moves.Should().BeEmpty();
		}

		[Fact]
		public async Task The_vest_is_the_tactical_vest()
		{
			var vest = _body.Wear(EquipmentSlot.TacticalVest, NewContainer(Body.Vest));
			var bandage = _body.InStash(RealItems.Any(RealItems.ArmyBandage));

			(await MoveTo(bandage, "Vest")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be(Body.Vest);
		}

		[Fact]
		public async Task The_pouch_is_the_secured_container()
		{
			_body.Wear(EquipmentSlot.SecuredContainer, NewContainer(AlphaCase));
			var bandage = _body.InStash(RealItems.Any(RealItems.ArmyBandage));

			(await MoveTo(bandage, "Pouch")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be(AlphaCase);
		}

		[Fact]
		public async Task The_stash_is_a_place_too()
		{
			var bandage = RealItems.Any(RealItems.ArmyBandage).In(_backpack);

			(await MoveTo(bandage, "Stash")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be(Gear.Stash);
		}

		// ---- adding to a stack

		[Fact]
		public async Task Money_is_added_to_a_stack_of_the_same_money_that_has_room()
		{
			var wallet = _backpack;
			var existing = RealItems.Any(RealItems.Roubles, 5000).In(wallet);
			var money = _body.InStash(RealItems.Any(RealItems.Roubles, 1000));

			(await MoveTo(money, "Backpack")).Should().BeNull();

			_body.Moves.Should().BeEmpty("merging is not a move to a spot");
			_body.Merges.Should().Equal(money);
			existing.StackObjectsCount.Should().Be(6000);
		}

		[Fact]
		public async Task Money_is_not_added_to_a_stack_that_is_locked()
		{
			var wallet = _backpack;
			RealItems.Any(RealItems.Roubles, 5000).In(wallet).PinLockState = EItemPinLockState.Locked;
			var money = _body.InStash(RealItems.Any(RealItems.Roubles, 1000));

			(await MoveTo(money, "Backpack")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be(ScavBackpack);
		}

		[Fact]
		public async Task Money_is_not_added_to_a_stack_of_another_kind_of_money()
		{
			var wallet = _backpack;
			RealItems.Any(RealItems.Euros, 5000).In(wallet);
			var money = _body.InStash(RealItems.Any(RealItems.Roubles, 1000));

			(await MoveTo(money, "Backpack")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be(ScavBackpack);
		}

		[Fact]
		public async Task A_stack_without_room_for_all_of_it_is_not_added_to()
		{
			var wallet = _backpack;
			var full = RealItems.Any(RealItems.Roubles, 1).In(wallet);
			full.StackObjectsCount = full.StackMaxSize - 500;
			var money = _body.InStash(RealItems.Any(RealItems.Roubles, 1000));

			(await MoveTo(money, "Backpack")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be(ScavBackpack);
		}

		[Fact]
		public async Task A_count_larger_than_the_stack_moves_the_whole_stack()
		{
			var money = _body.InStash(RealItems.Any(RealItems.Roubles, 1000));

			await Game.MoveTo(_body.Controller, money, new[] { "Backpack" }, 5000);

			_body.Moves.Single().Count.Should().Be(1000);
		}

		[Fact]
		public async Task A_count_of_zero_moves_the_whole_stack()
		{
			var money = _body.InStash(RealItems.Any(RealItems.Roubles, 1000));

			await Game.MoveTo(_body.Controller, money, new[] { "Backpack" }, 0);

			_body.Moves.Single().Count.Should().Be(1000);
		}

		// ---- a refused move

		[Fact]
		public async Task A_refusing_place_hands_over_to_the_next()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));
			_body.Refuse = move => Place(move) == "SpecialSlot1" ? "refused" : null;

			(await MoveTo(cms, "Special slots", "Backpack")).Should().BeNull();

			Place(_body.Moves.Single()).Should().Be(ScavBackpack);
		}

		[Fact]
		public async Task When_every_place_refuses_the_last_reason_is_given()
		{
			var cms = _body.InStash(RealItems.Any(RealItems.Cms));
			_body.Refuse = move => "refused at " + Place(move);

			(await MoveTo(cms, "Backpack")).Should().Be("refused at " + ScavBackpack);
		}

		// ---- money

		[Fact]
		public void Money_is_told_apart_by_its_template()
		{
			Game.CurrencyOf(RealItems.Roubles).Should().Be("RUB");
			Game.CurrencyOf(RealItems.Euros).Should().Be("EUR");
			Game.CurrencyOf(RealItems.Dollars).Should().Be("USD");
			Game.CurrencyOf(RealItems.Grizzly).Should().BeNull();
		}

		// ---- adding to a stack directly, and waiting

		[Fact]
		public async Task Merging_a_whole_stack_into_another_works()
		{
			var target = RealItems.Any(RealItems.Roubles, 500).In(_backpack);
			var source = _body.InStash(RealItems.Any(RealItems.Roubles, 1000));

			(await Game.MergeInto(_body.Controller, source, 1000, target)).Should().BeNull();
		}

		[Fact]
		public async Task Merging_into_a_different_item_fails_with_the_games_reason()
		{
			var target = RealItems.Any(RealItems.Euros, 500).In(_backpack);
			var source = _body.InStash(RealItems.Any(RealItems.Roubles, 1000));

			(await Game.MergeInto(_body.Controller, source, 1000, target)).Should().NotBeNull();
		}

		[Fact]
		public async Task Waiting_ends_at_once_when_the_item_is_not_in_the_stash()
		{
			var item = RealItems.Any(RealItems.Roubles, 5).In(_backpack);

			(await Game.WaitUntilCarried(_body.Controller, item)).Should().BeTrue();
		}

		[Fact]
		public async Task Waiting_gives_up_when_the_item_stays_in_the_stash()
		{
			var item = _body.InStash(RealItems.Any(RealItems.Roubles, 5));

			(await Game.WaitUntilCarried(_body.Controller, item)).Should().BeFalse();
		}

		// ---- which places you have

		[Fact]
		public void The_pockets_the_backpack_and_the_stash_are_places_you_have_and_an_unworn_vest_is_not()
		{
			Game.HasPlace(_body.Inventory, "Pockets").Should().BeTrue();
			Game.HasPlace(_body.Inventory, "Backpack").Should().BeTrue();
			Game.HasPlace(_body.Inventory, "Stash").Should().BeTrue();
			Game.HasPlace(_body.Inventory, "Vest").Should().BeFalse();
			Game.HasPlace(_body.Inventory, "Pouch").Should().BeFalse();
		}

		[Fact]
		public void A_name_that_is_no_place_is_not_one_you_have()
		{
			Game.HasPlace(_body.Inventory, "Nowhere").Should().BeFalse();
		}
	}
}
