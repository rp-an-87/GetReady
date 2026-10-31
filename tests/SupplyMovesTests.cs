using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.Gear;

namespace GetReady.Tests
{
	// Bringing a list of items from the stash, swapping a carried item for a better one, and reading the plan straight from an inventory.
	public class SupplyMovesTests
	{
		private readonly Body _body = new Body();
		private readonly CompoundItem _backpack;

		public SupplyMovesTests()
		{
			SupplyFixture.Reset();
			_backpack = _body.Wear(EquipmentSlot.Backpack, NewContainer(ScavBackpack));
		}

		private static readonly List<string> Backpack = new List<string> { "Backpack" };

		private Item InStash(string template) => _body.InStash(RealItems.Any(template));

		private static List<KeyValuePair<Item, int>> Moves(params Item[] items) => items.Select(item => new KeyValuePair<Item, int>(item, 0)).ToList();

		// ---- bringing

		[Fact]
		public async Task Every_item_is_brought()
		{
			var one = InStash(RealItems.Afak);
			var two = InStash(RealItems.Salewa);

			(await SupplyMoves.MoveAll(_body.Controller, Moves(one, two), Backpack)).Should().BeNull();

			_body.Moves.Select(move => move.Item).Should().Equal(one, two);
		}

		[Fact]
		public async Task One_that_cannot_be_moved_does_not_stop_the_others()
		{
			var one = InStash(RealItems.Afak);
			var two = InStash(RealItems.Salewa);
			_body.Refuse = move => move.Item == one ? "no room" : null;

			var error = await SupplyMoves.MoveAll(_body.Controller, Moves(one, two), Backpack);

			error.Should().Be("1 of 2 could not be moved (no room)");
			_body.Moves.Single().Item.Should().BeSameAs(two);
		}

		[Fact]
		public async Task A_single_item_that_fails_gives_its_own_reason()
		{
			var one = InStash(RealItems.Afak);
			_body.Refuse = move => "no room";

			(await SupplyMoves.MoveAll(_body.Controller, Moves(one), Backpack)).Should().Be("no room");
		}

		[Fact]
		public async Task The_games_internals_are_left_out_of_the_reason()
		{
			var one = InStash(RealItems.Afak);
			_body.Refuse = move => "item thing (id: 5c0e530286f7747fa1419862) is not here";

			(await SupplyMoves.MoveAll(_body.Controller, Moves(one), Backpack)).Should().Be("item thing is not here");
		}

		[Fact]
		public async Task A_move_the_game_accepts_but_never_applies_is_called_failed()
		{
			var one = InStash(RealItems.Afak);
			Game.RunOperation = (controller, operation) => Task.FromResult<Comfort.Common.IResult>(Comfort.Common.SuccessfulResult.New); // accepted, nothing happens

			var error = await SupplyMoves.MoveAll(_body.Controller, Moves(one), Backpack);

			error.Should().Be("the game accepted the move but the item is still in the stash");
		}

		[Fact]
		public async Task Nothing_to_bring_is_not_an_error()
		{
			(await SupplyMoves.MoveAll(_body.Controller, Moves(), Backpack)).Should().BeNull();
		}

		// ---- swapping

		[Fact]
		public async Task A_swap_sends_the_worse_one_to_the_stash_and_brings_the_better_one()
		{
			var worse = RealItems.Any(RealItems.Afak).In(_backpack);
			var better = InStash(RealItems.Afak);

			(await SupplyMoves.Swap(_body.Controller, worse, better, Backpack)).Should().BeNull();

			_body.Moves.Select(move => move.Item).Should().Equal(worse, better);
			Body.Where(_body.Moves[0]).Should().Be(Gear.Stash);
			Body.Where(_body.Moves[1]).Should().Be(ScavBackpack);
		}

		[Fact]
		public async Task If_the_worse_one_cannot_go_to_the_stash_nothing_else_moves()
		{
			var worse = RealItems.Any(RealItems.Afak).In(_backpack);
			var better = InStash(RealItems.Afak);
			_body.Refuse = move => "stash full";

			(await SupplyMoves.Swap(_body.Controller, worse, better, Backpack)).Should().Be("stash full");
			_body.Moves.Should().BeEmpty();
		}

		[Fact]
		public async Task If_the_better_one_cannot_be_brought_the_worse_one_goes_back()
		{
			var worse = RealItems.Any(RealItems.Afak).In(_backpack);
			var better = InStash(RealItems.Afak);
			_body.Refuse = move => move.Item == better ? "cannot" : null;

			(await SupplyMoves.Swap(_body.Controller, worse, better, Backpack)).Should().Be("cannot");

			_body.Moves.Select(move => move.Item).Should().Equal(worse, worse);
			Body.Where(_body.Moves[1]).Should().Be(ScavBackpack);
		}

		// ---- how an item is named in the plan

		[Fact]
		public void An_item_with_a_resource_says_how_much_is_left()
		{
			var kit = RealItems.Any(RealItems.Afak);
			kit.GetItemComponent<MedKitComponent>().HpResource = 120;

			SupplyMoves.Describe(kit).Should().Be("AFAK (120 left)");
		}

		[Fact]
		public void An_item_without_a_resource_is_just_named()
		{
			SupplyMoves.Describe(RealItems.Any(RealItems.Roubles)).Should().Be("Roubles");
		}

		// ---- a plan read straight from an inventory

		[Fact]
		public async Task A_supplies_step_reads_what_you_carry_and_what_the_stash_has()
		{
			var step = new HealingStep();
			foreach (var def in step.Groups)
			{
				def.Setting.Target = def.Id == "health" ? 200 : 0;
			}

			var carried = RealItems.Any(RealItems.Afak).In(_backpack);
			carried.GetItemComponent<MedKitComponent>().HpResource = 50;
			var fresh = InStash(RealItems.Afak);

			var lines = step.Plan(_body.Controller);

			lines.Should().Contain(line => line.Run != null, "the stash has a fuller kit to bring or swap in");
			await Task.CompletedTask;
			fresh.Should().NotBeNull();
		}
	}
}
