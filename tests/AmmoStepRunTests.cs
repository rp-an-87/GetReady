using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The Ammo step when its lines are run: what is moved, loaded and merged, and what is said when the game refuses.
	public class AmmoStepRunTests
	{
		private readonly Body _body = new Body();
		private readonly World _world;
		private readonly AmmoStep _step = new AmmoStep();
		private int _loadLimit = int.MaxValue;
		private int _loadCalls;
		// What one call adds at most (the game adds only what fits into the last stack of cartridges)
		private int _perCall = int.MaxValue;
		private string _loadError;

		public AmmoStepRunTests()
		{
			Reset();
			_world = new World(_body);
			AmmoStep.Spare.Setting.Target = 0;
			// The controller loads rounds from the stack into the magazine (as many as the test lets it)
			AmmoRules.LoadRounds = (controller, ammo, magazine, count) =>
			{
				if (_loadError != null)
				{
					return Task.FromResult<IResult>(new FailedResult(_loadError, 0));
				}

				// The game's own load moves what the stack has, up to the room in the magazine (it ignores the count)
				int take = System.Math.Min(System.Math.Min(System.Math.Min(count, _loadLimit), _perCall), magazine.MaxCount - magazine.Count);
				_loadLimit -= take;
				_loadCalls++;
				if (take > 0)
				{
					magazine.Cartridges.Add(NewAmmo(ammo.TemplateId.ToString(), take), false);
					ammo.StackObjectsCount -= take;
				}
				return Task.FromResult<IResult>(SuccessfulResult.New);
			};
		}

		private Ammo InStash(string template, int count) => NewAmmo(template, count).In(_world.StashBox);

		private List<PlanLine> Plan() => _step.Plan(_body.Controller);

		// ---- a magazine in a weapon

		[Fact]
		public async Task A_magazine_in_a_weapon_is_taken_out_filled_and_put_back()
		{
			InStash(M855, 100);
			var rifle = _world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 10)));
			var magazine = rifle.GetCurrentMagazine();

			var line = Plan().Single(candidate => candidate.Fill);
			var error = await line.Run();

			error.Should().BeNull();
			magazine.Count.Should().Be(30);
			_body.Moves.Select(move => move.Item).Should().Equal(magazine, magazine);
			Body.Where(_body.Moves[0]).Should().NotBe("mod_magazine");
			rifle.GetCurrentMagazine().Should().BeSameAs(magazine);
		}

		[Fact]
		public async Task If_the_magazine_cannot_be_taken_out_nothing_is_loaded()
		{
			InStash(M855, 100);
			var rifle = _world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 10)));
			var magazine = rifle.GetCurrentMagazine();
			_body.Refuse = move => "no room";

			var error = await Plan().Single(candidate => candidate.Fill).Run();

			error.Should().Be("could not take M4A1's magazine out of the weapon: no room");
			magazine.Count.Should().Be(10);
		}

		[Fact]
		public async Task If_the_magazine_cannot_go_back_the_message_says_where_it_is()
		{
			InStash(M855, 100);
			var rifle = _world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 10)));
			var magazine = rifle.GetCurrentMagazine();
			_body.Refuse = move => _body.Moves.Count >= 1 ? "slot refuses" : null;

			var error = await Plan().Single(candidate => candidate.Fill).Run();

			error.Should().Be("M4A1's magazine was filled but could not be put back in the weapon (it is in your gear): slot refuses");
			magazine.Count.Should().Be(30);
		}

		[Fact]
		public async Task A_load_that_comes_up_short_says_how_many_went_in()
		{
			InStash(M855, 100);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 10)));
			_loadLimit = 5;

			var error = await Plan().Single(candidate => candidate.Fill).Run();

			error.Should().StartWith("only 5 of 20 ").And.Contain(" could be loaded into M4A1's magazine");
		}

		[Fact]
		public async Task A_load_the_game_refuses_loads_nothing_and_says_so()
		{
			InStash(M855, 100);
			var rifle = _world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 10)));
			_loadError = "busy";

			var error = await Plan().Single(candidate => candidate.Fill).Run();

			error.Should().StartWith("only 0 of 20 ");
			rifle.GetCurrentMagazine().Count.Should().Be(10);
		}

		[Fact]
		public async Task A_fill_that_needs_several_stacks_loads_them_all()
		{
			var small = InStash(M855, 18);
			var big = InStash(M855, 100);
			var spare = NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			var error = await Plan().Single(candidate => candidate.Fill).Run();

			error.Should().BeNull();
			spare.Count.Should().Be(30);
			small.StackObjectsCount.Should().Be(0);
			big.StackObjectsCount.Should().Be(98);
		}

		[Fact]
		public async Task A_call_that_adds_only_part_of_a_stack_is_repeated_until_the_share_is_in()
		{
			var stack = InStash(M855, 100);
			var spare = NewMagazine().Loaded(M855, 10).In(_world.Backpack);
			_perCall = 8;

			var error = await Plan().Single(candidate => candidate.Fill).Run();

			error.Should().BeNull();
			spare.Count.Should().Be(30);
			stack.StackObjectsCount.Should().Be(80);
			_loadCalls.Should().Be(3, "8 + 8 + 4");
		}

		[Fact]
		public async Task Calls_stop_when_one_adds_nothing()
		{
			InStash(M855, 100);
			var spare = NewMagazine().Loaded(M855, 10).In(_world.Backpack);
			_loadLimit = 5;

			var error = await Plan().Single(candidate => candidate.Fill).Run();

			error.Should().StartWith("only 5 of 20 ");
			spare.Count.Should().Be(15);
			_loadCalls.Should().Be(2, "5, then 0");
		}

		// ---- a spare magazine on you

		[Fact]
		public async Task A_spare_magazine_is_filled_where_it_is()
		{
			InStash(M855, 100);
			var spare = NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			var error = await Plan().Single(candidate => candidate.Fill).Run();

			error.Should().BeNull();
			spare.Count.Should().Be(30);
			_body.Moves.Should().BeEmpty("a magazine that is not in a weapon is not taken out");
		}

		// ---- the chamber

		[Fact]
		public async Task A_round_is_chambered_by_moving_it_to_the_chamber()
		{
			var round = InStash(M855, 1);
			var rifle = _world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)));

			var line = Plan().Single(candidate => candidate.Chamber);
			var error = await line.Run();

			error.Should().BeNull();
			_body.Moves.Single().Item.Should().BeSameAs(round);
			rifle.Chambers[0].ContainedItem.Should().BeSameAs(round);
		}

		// ---- loose stacks

		[Fact]
		public async Task A_loose_stack_is_topped_up_by_adding_the_stash_stack_to_it()
		{
			var source = InStash(M855, 100);
			var stack = NewAmmo(M855, 30).In(_world.Backpack);

			var line = Plan().Single(candidate => candidate.Topped);
			var error = await line.Run();

			error.Should().BeNull();
			stack.StackObjectsCount.Should().Be(60);
			_body.Merges.Should().Equal(source);
		}

		[Fact]
		public async Task Topping_up_never_asks_for_more_than_the_stack_has_room_for()
		{
			var source = InStash(M855, 100);
			var stack = NewAmmo(M855, 30).In(_world.Backpack);
			var line = Plan().Single(candidate => candidate.Topped);
			stack.StackObjectsCount = 50; // something else filled most of it since the plan was made

			var error = await line.Run();

			error.Should().BeNull();
			stack.StackObjectsCount.Should().Be(60);
			source.StackObjectsCount.Should().Be(90);
		}

		[Fact]
		public async Task A_top_up_the_game_refuses_says_which_stack()
		{
			InStash(M855, 100);
			NewAmmo(M855, 30).In(_world.Backpack);
			var line = Plan().Single(candidate => candidate.Topped);
			_body.Refuse = move => "cannot merge";

			var error = await line.Run();

			error.Should().Contain("could not add to ").And.EndWith(": cannot merge");
		}

		// ---- spare ammo

		[Fact]
		public async Task Spare_ammo_is_brought_from_the_stash_to_the_places_of_the_group()
		{
			var source = InStash(M855, 200);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)).Chambered(M855));
			AmmoStep.Spare.Setting.Target = 1;
			var line = Plan().Single(candidate => candidate.Text.StartsWith("Bring "));

			var error = await line.Run();

			error.Should().BeNull();
			_body.Moves.Should().ContainSingle();
			_body.Moves[0].Item.Should().BeSameAs(source);
			_body.Moves[0].Count.Should().Be(60);
		}

		[Fact]
		public async Task Spare_ammo_that_cannot_be_moved_says_so()
		{
			InStash(M855, 200);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)).Chambered(M855));
			AmmoStep.Spare.Setting.Target = 1;
			var line = Plan().Single(candidate => candidate.Text.StartsWith("Bring "));
			_body.Refuse = move => "no room";

			var error = await line.Run();

			error.Should().StartWith("could not bring ").And.EndWith(": no room");
		}

		[Fact]
		public void Spare_ammo_when_the_stash_has_less_than_wanted_says_how_much()
		{
			InStash(M855, 20);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)).Chambered(M855));
			AmmoStep.Spare.Setting.Target = 1;

			Plan().Select(line => line.Text).Should().Contain(text => text.StartsWith("Bring 20 ") && text.Contains("the stash only has 20 of 60"));
		}

		[Fact]
		public void Spare_ammo_with_nothing_loaded_and_no_ammo_on_you_cannot_tell_which_to_bring()
		{
			InStash(M855, 200);
			_world.Equip(NewWeapon());
			AmmoStep.Spare.Setting.Target = 1;

			Plan().Should().Contain(line => line.Problem && line.Detail != null && line.Detail.Contains("nothing is loaded or chambered to tell which ammo to bring"));
		}

		[Fact]
		public void The_worst_pick_takes_the_least_penetrating_ammo_in_use()
		{
			InStash(M855, 200);
			InStash(M995, 200);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)).Chambered(M855));
			NewMagazine().Loaded(M995, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;
			SettingsStore.Current.Ammo.SparePick = "worst";

			var bring = Plan().Select(line => line.Text).Single(text => text.StartsWith("Bring "));

			bring.Should().Contain("worst of the 2 kinds in use");
			Plan().Single(line => line.Text.StartsWith("Bring ")).Template.Should().Be(M855);
		}

		[Fact]
		public void The_best_pick_takes_the_most_penetrating_ammo_in_use()
		{
			InStash(M855, 200);
			InStash(M995, 200);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)).Chambered(M855));
			NewMagazine().Loaded(M995, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;
			SettingsStore.Current.Ammo.SparePick = "best";

			Plan().Single(line => line.Text.StartsWith("Bring ")).Template.Should().Be(M995);
		}

		[Fact]
		public void The_random_pick_follows_the_dice()
		{
			InStash(M855, 200);
			InStash(M995, 200);
			_world.Equip(NewWeapon().With(NewMagazine().Loaded(M855, 30)).Chambered(M855));
			NewMagazine().Loaded(M995, 10).In(_world.Backpack);
			AmmoStep.Spare.Setting.Target = 1;
			SettingsStore.Current.Ammo.SparePick = "random";

			Game.RandomIndex = count => 0;
			var first = Plan().Single(line => line.Text.StartsWith("Bring ")).Template;
			Game.RandomIndex = count => count - 1;
			var last = Plan().Single(line => line.Text.StartsWith("Bring ")).Template;

			first.Should().NotBe(last);
			Plan().Single(line => line.Text.StartsWith("Bring ")).Text.Should().Contain("random of the 2 kinds in use");
		}
	}
}
