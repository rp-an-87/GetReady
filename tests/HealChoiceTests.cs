using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using GetReady;
using Xunit;

namespace GetReady.Tests
{
	// What Treat injuries picks (HealChoice). g = Grizzly and s = Salewa treat everything, b = bandage (light bleed), c = CAT (heavy
	// bleed), p = splint (fracture), a = AI-2 (HP).
	public class HealChoiceTests
	{
		private static readonly Dictionary<string, string[]> Treats = new Dictionary<string, string[]>
		{
			{ "g", new[] { "heavy", "light", "fracture", "hp" } },
			{ "s", new[] { "heavy", "light", "fracture", "hp" } },
			{ "b", new[] { "light" } },
			{ "c", new[] { "heavy" } },
			{ "p", new[] { "fracture" } },
			{ "a", new[] { "hp" } }
		};

		private static readonly string[] GrizzlyFirst = { "g", "c", "b", "p", "s", "a" };
		private static readonly string[] SavingKits = { "c", "b", "p", "g", "s", "a" };
		private static readonly string[] Everything = { "g", "c", "b", "p", "s", "a" };

		private static string Pick(string[] present, string[] order, string[] stock, params string[] off) =>
			HealChoice.Next(present, order, (item, trouble) => Treats[item].Contains(trouble) && !off.Contains(item), stock.Contains);

		private static List<string> Missing(string[] present, string[] order, string[] stock) =>
			HealChoice.Missing(present, order, (item, trouble) => Treats[item].Contains(trouble), stock.Contains);

		private static string[] Without(string item) => Everything.Where(other => other != item).ToArray();

		[Fact]
		public void Nothing_hurt_picks_nothing() => Pick(new string[0], GrizzlyFirst, Everything).Should().BeNull();

		[Fact]
		public void A_Grizzly_first_treats_everything() => Pick(new[] { "light", "fracture", "hp" }, GrizzlyFirst, Everything).Should().Be("g");

		[Fact]
		public void The_most_urgent_injury_comes_first() => Pick(new[] { "hp", "heavy", "light" }, GrizzlyFirst, Without("g")).Should().Be("c");

		[Theory]
		[InlineData("heavy", "c")]
		[InlineData("light", "b")]
		[InlineData("fracture", "p")]
		[InlineData("hp", "s")]
		public void Without_a_Grizzly_each_injury_gets_its_own_item(string injury, string expected) =>
			Pick(new[] { injury }, GrizzlyFirst, Without("g")).Should().Be(expected);

		[Fact]
		public void A_dedicated_item_listed_first_beats_a_kit() => Pick(new[] { "light", "hp" }, SavingKits, Everything).Should().Be("b");

		[Fact]
		public void A_kit_is_the_fallback_when_the_dedicated_item_is_gone() => Pick(new[] { "light" }, SavingKits, new[] { "g", "s" }).Should().Be("g");

		[Fact]
		public void HP_goes_to_the_first_kit_when_dedicated_items_come_first() => Pick(new[] { "hp" }, SavingKits, Everything).Should().Be("g");

		[Fact]
		public void An_item_that_is_off_is_never_used() => Pick(new[] { "hp" }, GrizzlyFirst, Everything, "g").Should().Be("s");

		[Fact]
		public void An_item_that_cannot_treat_the_injury_is_not_picked() => Pick(new[] { "heavy" }, new[] { "b" }, new[] { "b" }).Should().BeNull();

		[Fact]
		public void An_empty_stash_picks_nothing() => Pick(new[] { "heavy", "hp" }, GrizzlyFirst, new string[0]).Should().BeNull();

		[Fact]
		public void Nothing_is_missing_when_every_injury_has_an_item() => Missing(new[] { "light", "hp" }, GrizzlyFirst, Everything).Should().BeEmpty();

		[Fact]
		public void A_fracture_with_no_splint_and_no_kit_is_missing() => Missing(new[] { "fracture", "light" }, new[] { "b", "p" }, new[] { "b" }).Should().Equal("fracture");

		[Fact]
		public void Missing_comes_out_most_urgent_first() => Missing(new[] { "hp", "heavy" }, GrizzlyFirst, new string[0]).Should().Equal("heavy", "hp");
	}
}
