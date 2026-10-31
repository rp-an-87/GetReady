using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace GetReady.Tests
{
	// More of a group's choices: moving an item, the most to carry of one, the order of preference, and a place that is on swapped with one that is off.
	public class GroupMoreTests
	{
		public GroupMoreTests()
		{
			SupplyFixture.Reset();
		}

		private static GroupDef Group(params string[] templates) => new GroupDef { Id = "test", Entries = templates.Select(Entry.Count).ToArray() };

		private static List<string> Templates(GroupDef def) => def.OrderedEntries().Select(entry => entry.Template).ToList();

		[Fact]
		public void An_item_moves_one_place_down()
		{
			var def = Group("a", "b", "c");

			def.MoveEntry("a", 1);

			Templates(def).Should().Equal("b", "a", "c");
		}

		[Fact]
		public void An_item_moves_one_place_up()
		{
			var def = Group("a", "b", "c");

			def.MoveEntry("c", -1);

			Templates(def).Should().Equal("a", "c", "b");
		}

		[Fact]
		public void The_first_item_cannot_move_up_nor_the_last_down()
		{
			var def = Group("a", "b", "c");

			def.MoveEntry("a", -1);
			def.MoveEntry("c", 1);

			Templates(def).Should().Equal("a", "b", "c");
			def.Setting.Order.Should().BeEmpty("nothing was written for a move that did not happen");
		}

		[Fact]
		public void An_item_that_is_not_in_the_group_does_not_move()
		{
			var def = Group("a", "b");

			def.MoveEntry("zzz", 1);

			Templates(def).Should().Equal("a", "b");
		}

		[Fact]
		public void No_limit_is_the_default_for_the_most_of_an_item()
		{
			Group("a").MaxOf("a").Should().Be(0);
		}

		[Fact]
		public void The_most_of_an_item_is_remembered()
		{
			var def = Group("a", "b");

			def.SetMax("a", 3);

			def.MaxOf("a").Should().Be(3);
			def.MaxOf("b").Should().Be(0);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-2)]
		public void Setting_the_most_to_zero_or_less_takes_the_limit_off(int value)
		{
			var def = Group("a");
			def.SetMax("a", 3);

			def.SetMax("a", value);

			def.MaxOf("a").Should().Be(0);
			def.Setting.MaxPer.Should().NotContainKey("a");
		}

		[Fact]
		public void An_item_that_is_on_swapped_with_a_place_that_is_off_trades_spots_and_state()
		{
			var def = new GroupDef { Id = "test" };
			def.TogglePlace("Vest");
			var before = def.PlaceOrder();

			def.SwapPlaces("Pockets", "Vest");

			def.PlaceOn("Pockets").Should().BeFalse();
			def.PlaceOn("Vest").Should().BeTrue();
			def.PlaceOrder().Where(def.PlaceOn).Should().Contain("Vest").And.NotContain("Pockets");
			before.Should().Contain("Vest");
		}

		[Fact]
		public void Two_places_that_are_both_off_keep_being_off_after_a_swap()
		{
			var def = new GroupDef { Id = "test" };
			def.TogglePlace("Vest");
			def.TogglePlace("Pockets");

			def.SwapPlaces("Pockets", "Vest");

			def.PlaceOn("Pockets").Should().BeFalse();
			def.PlaceOn("Vest").Should().BeFalse();
		}

		[Fact]
		public void An_item_outside_the_group_has_no_preference()
		{
			var def = Group("a", "b", "c");

			def.PriorityOf(RealItems.Any(RealItems.Afak)).Should().Be(-1, "not one of the group's items");
		}

		[Fact]
		public void In_a_random_group_every_item_is_equally_preferred()
		{
			var def = new GroupDef { Id = "test", Random = true, Entries = new[] { Entry.Count(RealItems.Afak), Entry.Count(RealItems.Salewa) } };

			def.PriorityOf(RealItems.Any(RealItems.Afak)).Should().Be(0);
			def.PriorityOf(RealItems.Any(RealItems.Salewa)).Should().Be(0);
		}

		[Fact]
		public void A_listed_item_is_preferred_by_its_position()
		{
			var def = new GroupDef { Id = "test", Entries = new[] { Entry.Count(RealItems.Afak), Entry.Count(RealItems.Salewa) } };

			def.PriorityOf(RealItems.Any(RealItems.Afak)).Should().Be(0);
			def.PriorityOf(RealItems.Any(RealItems.Salewa)).Should().Be(1);
		}

		[Fact]
		public void An_item_switched_off_is_not_preferred_at_all()
		{
			var def = new GroupDef { Id = "test", Entries = new[] { Entry.Count(RealItems.Afak), Entry.Count(RealItems.Salewa) } };
			def.ToggleEntry(RealItems.Afak);

			def.PriorityOf(RealItems.Any(RealItems.Afak)).Should().Be(-1);
			def.WeightOf(RealItems.Any(RealItems.Afak)).Should().Be(0);
		}
	}
}
