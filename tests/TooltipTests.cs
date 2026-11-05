using FluentAssertions;
using UnityEngine;
using Xunit;

namespace GetReady.Tests
{
	// Where the tooltip box goes and when it shows, and how wide the injector template panels are.
	public class TooltipTests
	{
		private static readonly Vector2 Screen = new Vector2(1920, 1080);

		[Fact]
		public void The_box_goes_right_and_below_the_mouse()
		{
			var rect = Tooltips.Place(new Vector2(500, 300), new Vector2(200, 60), Screen);

			rect.x.Should().Be(516);
			rect.y.Should().Be(322);
			rect.size.Should().Be(new Vector2(200, 60));
		}

		[Fact]
		public void Near_the_right_edge_it_goes_to_the_left_of_the_mouse()
		{
			var rect = Tooltips.Place(new Vector2(1800, 300), new Vector2(200, 60), Screen);

			rect.xMax.Should().BeLessThan(1800);
			rect.xMax.Should().BeLessOrEqualTo(1916);
		}

		[Fact]
		public void Near_the_bottom_edge_it_goes_above_the_mouse()
		{
			var rect = Tooltips.Place(new Vector2(500, 1060), new Vector2(200, 60), Screen);

			rect.yMax.Should().BeLessThan(1060);
		}

		[Fact]
		public void In_the_bottom_right_corner_it_goes_above_and_to_the_left()
		{
			var rect = Tooltips.Place(new Vector2(1915, 1075), new Vector2(200, 60), Screen);

			rect.xMax.Should().BeLessThan(1915);
			rect.yMax.Should().BeLessThan(1075);
		}

		[Fact]
		public void It_never_leaves_the_screen()
		{
			foreach (var mouse in new[] { new Vector2(0, 0), new Vector2(1919, 0), new Vector2(0, 1079), new Vector2(1919, 1079), new Vector2(960, 540) })
			{
				var rect = Tooltips.Place(mouse, new Vector2(340, 120), Screen);

				rect.xMin.Should().BeGreaterOrEqualTo(4);
				rect.yMin.Should().BeGreaterOrEqualTo(4);
				rect.xMax.Should().BeLessOrEqualTo(Screen.x - 4);
				rect.yMax.Should().BeLessOrEqualTo(Screen.y - 4);
			}
		}

		[Fact]
		public void A_box_larger_than_the_screen_sits_in_the_corner()
		{
			var rect = Tooltips.Place(new Vector2(50, 50), new Vector2(3000, 2000), Screen);

			rect.x.Should().Be(4);
			rect.y.Should().Be(4);
		}

		[Fact]
		public void The_box_shows_only_after_the_wait()
		{
			Tooltips.IsDue(10f, 10f + Tooltips.Delay / 2).Should().BeFalse();
			Tooltips.IsDue(10f, 10f + Tooltips.Delay).Should().BeTrue();
			Tooltips.IsDue(10f, 12f).Should().BeTrue();
		}

		[Fact]
		public void The_wait_is_as_short_as_it_can_be_without_flicker()
		{
			Tooltips.Delay.Should().BeInRange(0f, 0.1f);
		}

		// ---- the panels of the templates

		private static CaseEntry Holding(int cells)
		{
			var saved = new CaseEntry();
			for (int i = 0; i < cells; i++)
			{
				saved.Items.Add(new CaseItem { X = i % 3, Y = i / 3, Template = "t" + i });
			}

			return saved;
		}
	}
}
