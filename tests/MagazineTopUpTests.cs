using System.Threading.Tasks;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The top-up key on a hovered magazine: which message you get, and what is loaded.
	public class MagazineTopUpTests
	{
		private readonly World _world;
		private int _calls;
		private bool _failLoad;

		public MagazineTopUpTests()
		{
			Reset();
			_world = new World();
			// The controller loads rounds from the stack into the magazine
			AmmoRules.LoadRounds = (controller, ammo, magazine, count) =>
			{
				_calls++;
				if (_failLoad)
				{
					return Task.FromResult<Comfort.Common.IResult>(new Comfort.Common.FailedResult("no", 0));
				}

				magazine.Cartridges.Add(NewAmmo(ammo.TemplateId.ToString(), count), false);
				ammo.StackObjectsCount -= count;
				return Task.FromResult<Comfort.Common.IResult>(Comfort.Common.SuccessfulResult.New);
			};
		}

		private Ammo InStash(string template, int count) => NewAmmo(template, count).In(_world.StashBox);

		private Task<string> TopUp(Magazine magazine) => MagazineTopUp.TopUp(null, magazine);

		[Fact]
		public void A_magazine_that_is_not_full_can_be_topped_up()
		{
			MagazineTopUp.IsApplicable(NewMagazine().Loaded(M855, 10)).Should().BeTrue();
			MagazineTopUp.IsApplicable(NewMagazine()).Should().BeTrue();
		}

		[Fact]
		public void A_full_magazine_cannot()
		{
			MagazineTopUp.IsApplicable(NewMagazine().Loaded(M855, 30)).Should().BeFalse();
		}

		[Fact]
		public void Something_that_is_not_a_magazine_cannot()
		{
			MagazineTopUp.IsApplicable(NewAmmo(M855, 10)).Should().BeFalse();
			MagazineTopUp.IsApplicable(NewWeapon()).Should().BeFalse();
		}

		[Fact]
		public async Task A_full_magazine_says_nothing_and_loads_nothing()
		{
			InStash(M855, 100);

			(await TopUp(NewMagazine().Loaded(M855, 30).In(_world.Backpack))).Should().BeNull();
			_calls.Should().Be(0);
		}

		[Fact]
		public async Task A_magazine_is_filled_with_what_it_holds_from_the_stash()
		{
			InStash(M855, 100);
			var magazine = NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			var message = await TopUp(magazine);

			magazine.Count.Should().Be(30);
			message.Should().StartWith("Top-up: loaded 20 ").And.Contain(" from the stash. Followed: same as inside");
		}

		[Fact]
		public async Task With_less_ammo_than_needed_it_loads_what_there_is()
		{
			InStash(M855, 5);
			var magazine = NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			var message = await TopUp(magazine);

			magazine.Count.Should().Be(15);
			message.Should().StartWith("Top-up: loaded 5 ");
		}

		[Fact]
		public async Task With_no_ammo_at_all_it_says_none_was_found()
		{
			var magazine = NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			(await TopUp(magazine)).Should().Be("Top-up: no compatible ammo found.");
			_calls.Should().Be(0);
		}

		[Fact]
		public async Task An_empty_magazine_with_two_kinds_around_and_nothing_to_follow_asks_you_to_decide()
		{
			InStash(M855, 100);
			InStash(M995, 100);
			var magazine = NewMagazine().In(_world.Backpack);

			var message = await TopUp(magazine);

			message.Should().StartWith("Top-up: nothing to follow for this magazine. Compatible ammo: ").And.Contain(M855).And.Contain(M995); // (the game localizes the names; here they are the keys)
			magazine.Count.Should().Be(0);
			_calls.Should().Be(0);
		}

		[Fact]
		public async Task A_refused_load_says_nothing_could_be_loaded()
		{
			InStash(M855, 100);
			_failLoad = true;
			var magazine = NewMagazine().Loaded(M855, 10).In(_world.Backpack);

			(await TopUp(magazine)).Should().Be("Top-up: nothing could be loaded.");
			magazine.Count.Should().Be(10);
		}
	}
}
