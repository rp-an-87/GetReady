using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace GetReady.Tests
{
	// The order of a group's items and of the places it goes to: switching on and off, swapping, and what an older profile turns into.
	public class GroupOrderTests
	{
		public GroupOrderTests()
		{
			SettingsStore.UseForTests(new Settings());
		}

		private static GroupDef Group(params string[] templates) => new GroupDef { Id = "test", Entries = templates.Select(Entry.Count).ToArray() };

		private static List<string> Templates(GroupDef def) => def.OrderedEntries().Select(entry => entry.Template).ToList();

		// ---- the items of a group

		[Fact]
		public void Items_start_in_the_order_they_are_listed() => Templates(Group("a", "b", "c")).Should().Equal("a", "b", "c");

		[Fact]
		public void An_order_that_names_only_some_items_puts_those_first()
		{
			var def = Group("a", "b", "c");
			def.Setting.Order = new List<string> { "c" };

			Templates(def).Should().Equal("c", "a", "b");
		}

		[Fact]
		public void Switching_an_item_off_sends_it_to_the_end()
		{
			var def = Group("a", "b", "c");

			def.ToggleEntry("a");

			Templates(def).Should().Equal("b", "c", "a");
			def.Setting.Off.Should().Equal("a");
		}

		[Fact]
		public void An_item_switched_off_goes_to_the_top_of_the_unused_ones()
		{
			var def = Group("a", "b", "c", "d");

			def.ToggleEntry("a");
			def.ToggleEntry("c");

			Templates(def).Should().Equal("b", "d", "c", "a");
		}

		[Fact]
		public void Switching_an_item_back_on_does_not_give_it_its_old_spot_back()
		{
			var def = Group("a", "b", "c", "d");

			def.ToggleEntry("b");
			def.ToggleEntry("b");

			Templates(def).Should().Equal("a", "c", "d", "b");
			def.Setting.Off.Should().BeEmpty();
		}

		[Fact]
		public void An_item_switched_on_goes_after_the_used_ones_and_before_the_unused_ones()
		{
			var def = Group("a", "b", "c");

			def.ToggleEntry("a");
			def.ToggleEntry("b");
			def.ToggleEntry("a");

			Templates(def).Should().Equal("c", "a", "b");
			def.Setting.Off.Should().Equal("b");
		}

		[Fact]
		public void Active_entries_leave_out_what_is_off()
		{
			var def = Group("a", "b", "c");
			def.ToggleEntry("b");

			def.ActiveEntries().Select(entry => entry.Template).Should().Equal("a", "c");
		}

		// ---- the places of a group

		private static readonly string[] Usual = { "Pockets", "Vest", "Special slots", "Backpack", "Pouch" };

		[Fact]
		public void A_new_group_starts_with_every_place_on_the_pouch_last()
		{
			var def = Group("a");

			def.PlaceOrder().Should().Equal(Usual);
			def.PlacesToTry().Should().Equal(Usual);
		}

		[Fact]
		public void The_defaults_of_the_group_come_first()
		{
			var def = new GroupDef { Id = "test", DefaultDestinations = new[] { "Special slots", "Pockets" } };

			def.PlaceOrder().Should().Equal("Special slots", "Pockets", "Vest", "Backpack", "Pouch");
		}

		[Fact]
		public void A_group_that_cannot_use_the_pouch_has_neither_the_pouch_nor_the_special_slots()
		{
			var def = new GroupDef { Id = "test", AllowPouch = false };

			def.PlaceOrder().Should().Equal("Pockets", "Vest", "Backpack");
		}

		[Fact]
		public void A_group_with_no_fallback_starts_with_only_its_defaults_on()
		{
			var def = new GroupDef
			{
				Id = "test", NoFallback = true, DefaultDestinations = new[] { "Wallet" },
				Locations = new[] { "Wallet", "Docs case", "Pockets", "Pouch" }
			};

			def.PlaceOrder().Should().Equal("Wallet", "Docs case", "Pockets", "Pouch");
			def.PlacesToTry().Should().Equal("Wallet");
		}

		[Fact]
		public void Switching_a_place_off_sends_it_to_the_end_and_skips_it()
		{
			var def = Group("a");

			def.TogglePlace("Vest");

			def.PlaceOrder().Should().Equal("Pockets", "Special slots", "Backpack", "Pouch", "Vest");
			def.PlacesToTry().Should().Equal("Pockets", "Special slots", "Backpack", "Pouch");
			def.PlaceOn("Vest").Should().BeFalse();
		}

		[Fact]
		public void Switching_a_place_back_on_puts_it_after_the_ones_that_are_on()
		{
			var def = Group("a");

			def.TogglePlace("Pockets");
			def.TogglePlace("Vest");
			def.TogglePlace("Pockets");

			def.PlaceOrder().Should().Equal("Special slots", "Backpack", "Pouch", "Pockets", "Vest");
			def.PlacesToTry().Should().Equal("Special slots", "Backpack", "Pouch", "Pockets");
		}

		[Fact]
		public void Swapping_two_places_swaps_their_spots_and_whether_they_are_on()
		{
			var def = Group("a");
			def.TogglePlace("Backpack");

			def.SwapPlaces("Pouch", "Backpack");

			def.PlaceOrder().Should().Equal("Pockets", "Vest", "Special slots", "Backpack", "Pouch");
			def.PlaceOn("Backpack").Should().BeTrue();
			def.PlaceOn("Pouch").Should().BeFalse();
		}

		[Fact]
		public void Swapping_two_places_that_are_on_only_changes_the_order()
		{
			var def = Group("a");

			def.SwapPlaces("Pockets", "Pouch");

			def.PlacesToTry().Should().Equal("Pouch", "Vest", "Special slots", "Backpack", "Pockets");
		}

		[Fact]
		public void Swapping_with_itself_or_with_an_unknown_place_changes_nothing()
		{
			var def = Group("a");

			def.SwapPlaces("Vest", "Vest");
			def.SwapPlaces("Vest", "Nowhere");

			def.PlaceOrder().Should().Equal(Usual);
		}

		[Fact]
		public void A_place_the_group_no_longer_offers_is_dropped()
		{
			var def = Group("a");
			def.Setting.Places = new List<string> { "Bogus", "Vest", "Pockets" };

			def.PlaceOrder().Should().Equal("Vest", "Pockets", "Special slots", "Backpack", "Pouch");
		}

		// ---- a profile from before places were a list

		private static GroupDef Old(params string[] picks)
		{
			var def = Group("a");
			SettingsStore.Current.Supplies.Groups["test"] = new GroupSetting { Destinations = picks.ToList() };
			return def;
		}

		[Fact]
		public void An_old_group_with_no_fallback_only_uses_what_it_had_picked()
		{
			var def = new GroupDef
			{
				Id = "test", NoFallback = true, DefaultDestinations = new[] { "Wallet" },
				Locations = new[] { "Wallet", "Docs case", "Pockets", "Pouch" }
			};
			SettingsStore.Current.Supplies.Groups["test"] = new GroupSetting { Destinations = new List<string> { "Docs case" } };

			def.PlacesToTry().Should().Equal("Docs case");
		}

	}
}
