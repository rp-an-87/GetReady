using FluentAssertions;
using Xunit;

namespace GetReady.Tests
{
	// The loading bar's timing and progress (ReadyLoading.cs, LoadingModel). The drawing is not covered.
	public class LoadingBarTests
	{
		private static LoadingModel Started(int total = 4)
		{
			var model = new LoadingModel();
			model.Begin(10f, total);
			return model;
		}

		// The run ends at this time: the bar is drawn from then on until it is full (each frame, here every 10 ms)
		private static void End(LoadingModel model, float at)
		{
			model.Finish(at);
			for (float now = at; now < at + 5f && model.FullAt < 0f; now += 0.01f)
			{
				model.Tick(now);
			}
		}

		[Fact]
		public void Even_a_run_that_ends_at_once_shows_the_bar()
		{
			var model = Started();
			End(model, 10.01f);

			model.Alpha(10.2f).Should().BeGreaterThan(0f);
			model.IsOver(10.2f).Should().BeFalse();
			model.IsOver(10f + LoadingModel.MinShown + LoadingModel.Hold + LoadingModel.FadeOut + 0.01f).Should().BeTrue();
		}

		[Fact]
		public void The_bar_fades_in_from_the_start()
		{
			var model = Started();

			model.Alpha(10f).Should().Be(0f);
			model.Alpha(10f + LoadingModel.ShowAfter + LoadingModel.FadeIn / 2f).Should().BeApproximately(0.5f, 0.01f);
			model.Alpha(10f + LoadingModel.ShowAfter + LoadingModel.FadeIn + 1f).Should().Be(1f);
		}

		[Fact]
		public void A_bar_that_was_shown_stays_for_the_minimum_time_even_if_the_run_ends_at_once()
		{
			var model = Started();
			End(model, 10f + LoadingModel.ShowAfter + 0.01f);

			// Still fully visible just before the minimum has passed
			model.Alpha(10f + LoadingModel.ShowAfter + LoadingModel.MinShown - 0.05f).Should().Be(1f);
			model.IsOver(10f + LoadingModel.ShowAfter + LoadingModel.MinShown).Should().BeFalse();
		}

		[Fact]
		public void After_the_run_ends_the_bar_holds_then_fades_out_and_is_over()
		{
			var model = Started();
			End(model, 20f);
			float fadeStart = model.FullAt + LoadingModel.Hold;

			model.Alpha(fadeStart - 0.01f).Should().Be(1f);
			model.Alpha(fadeStart + LoadingModel.FadeOut / 2f).Should().BeApproximately(0.5f, 0.01f);
			model.IsOver(fadeStart + LoadingModel.FadeOut - 0.01f).Should().BeFalse();
			model.IsOver(fadeStart + LoadingModel.FadeOut).Should().BeTrue();
		}

		[Fact]
		public void The_target_follows_the_lines_done_and_never_fills_before_the_run_ends()
		{
			var model = Started(4);
			model.Target.Should().Be(0f);

			model.LineDone();
			model.LineDone();
			model.Target.Should().BeApproximately(0.5f, 0.001f);

			model.LineDone();
			model.LineDone();
			model.Target.Should().Be(LoadingModel.RunningCap);

			model.Finish(11f);
			model.Target.Should().Be(1f);
		}

		[Fact]
		public void A_plan_with_nothing_to_run_has_no_progress_until_it_ends()
		{
			var model = Started(0);

			model.Target.Should().Be(0f);
		}

		[Fact]
		public void The_bar_slides_towards_the_target_at_a_limited_speed()
		{
			var model = Started(2);
			model.LineDone();

			model.Tick(10.05f);
			model.Displayed.Should().BeApproximately(LoadingModel.Speed * 0.05f, 0.001f);

			// Long after, it has arrived at the target and no further
			model.Tick(20f);
			model.Displayed.Should().BeApproximately(0.5f, 0.001f);
		}

		[Fact]
		public void The_step_label_is_kept_and_cleared_by_a_new_run()
		{
			var model = Started();
			model.Step("Repair", 10.1f);
			model.Label.Should().Be("Repair");

			model.Begin(30f, 2);
			model.Label.Should().Be("");
			model.Displayed.Should().Be(0f);
		}

		// ---- the steps one after the other

		private static LoadingModel Sequenced()
		{
			var model = new LoadingModel();
			model.Begin(10f, 4, true);
			return model;
		}

		[Fact]
		public void Quick_steps_are_each_named_for_a_moment_in_order_once_the_bar_is_there()
		{
			var model = Sequenced();
			float shown = 10f + LoadingModel.ShowAfter;
			// All three came in the first few milliseconds
			model.Step("Supplies", 10.001f);
			model.Step("Ammo", 10.002f);
			model.Step("Repair", 10.003f);

			model.LabelAt(shown - 0.05f).Should().Be("");
			model.LabelAt(shown + 0.01f).Should().Be("Supplies");
			model.LabelAt(shown + LoadingModel.StepTime + 0.01f).Should().Be("Ammo");
			model.LabelAt(shown + 2 * LoadingModel.StepTime + 0.01f).Should().Be("Repair");
		}

		[Fact]
		public void A_step_that_comes_late_is_named_when_it_comes()
		{
			var model = Sequenced();
			model.Step("Supplies", 10.001f);
			model.Step("Repair", 11.5f);

			model.LabelAt(11.4f).Should().Be("Supplies");
			model.LabelAt(11.51f).Should().Be("Repair");
		}

		[Fact]
		public void The_same_step_twice_in_a_row_is_named_once()
		{
			var model = Sequenced();
			model.Step("Ammo", 10.001f);
			model.Step("Ammo", 10.002f);
			model.Step("Repair", 10.003f);

			model.LabelAt(10f + LoadingModel.ShowAfter + LoadingModel.StepTime + 0.01f).Should().Be("Repair");
		}

		[Fact]
		public void The_bar_stays_until_the_last_name_has_had_its_time()
		{
			var model = Sequenced();
			model.Step("Supplies", 10.001f);
			model.Step("Ammo", 10.002f);
			model.Step("Repair", 10.003f);
			End(model, 10.4f);
			float lastDone = 10f + LoadingModel.ShowAfter + 3 * LoadingModel.StepTime;

			// Longer than MinShown would keep it
			model.Alpha(lastDone + LoadingModel.Hold - 0.01f).Should().Be(1f);
			model.IsOver(lastDone + LoadingModel.Hold + LoadingModel.FadeOut + 0.01f).Should().BeTrue();
		}

		[Fact]
		public void Many_steps_are_squeezed_to_end_within_the_most_a_row_may_take()
		{
			var model = Sequenced();
			for (int i = 0; i < 20; i++)
			{
				model.Step("Step " + i, 10.001f);
			}

			End(model, 10.4f);

			model.LabelAt(10f + LoadingModel.ShowAfter + LoadingModel.MaxQueue - 0.01f).Should().Be("Step 19");
			model.IsOver(10f + LoadingModel.ShowAfter + LoadingModel.MaxQueue + LoadingModel.Hold + LoadingModel.FadeOut + 0.01f).Should().BeTrue();
		}

		[Fact]
		public void Without_the_sequence_the_label_is_only_the_step_it_is_at_and_the_timing_is_unchanged()
		{
			var model = Started();
			model.Step("Supplies", 10.001f);
			model.Step("Repair", 10.002f);
			model.LabelAt(10.2f).Should().Be("Repair");

			End(model, 10.01f + LoadingModel.ShowAfter);
			model.IsOver(10f + LoadingModel.ShowAfter + LoadingModel.MinShown + LoadingModel.Hold + LoadingModel.FadeOut + 0.01f).Should().BeTrue();
		}

		[Fact]
		public void A_new_run_forgets_the_names_of_the_one_before()
		{
			var model = Sequenced();
			model.Step("Repair", 10.001f);

			model.Begin(30f, 2, true);

			model.LabelAt(31f).Should().Be("");
		}

		[Fact]
		public void The_label_says_done_when_the_run_is_over_the_bar_is_full_and_the_last_name_has_had_its_time()
		{
			var model = Sequenced();
			model.Step("Ammo", 10.001f);
			End(model, 10.1f);
			float done = System.Math.Max(model.FullAt, 10f + LoadingModel.StepTime);

			model.LabelAt(done - 0.01f).Should().Be("Ammo");
			model.LabelAt(done + 0.01f).Should().Be("Done");
		}

		[Fact]
		public void The_label_does_not_say_done_while_the_bar_is_not_full()
		{
			var model = Sequenced();
			model.Step("Ammo", 10.001f);
			model.Finish(10.1f);

			// The run is over, but the bar has not been drawn full yet
			model.LabelAt(11f).Should().Be("Ammo");
			model.Alpha(11f).Should().Be(1f);
			model.IsOver(20f).Should().BeFalse();
		}

		[Fact]
		public void Without_the_sequence_the_label_says_done_once_the_run_is_over_and_the_bar_is_full()
		{
			var model = Started();
			model.Step("Ammo", 10.001f);

			model.LabelAt(10.2f).Should().Be("Ammo");
			End(model, 10.3f);
			model.LabelAt(model.FullAt - 0.01f).Should().Be("Ammo");
			model.LabelAt(model.FullAt + 0.01f).Should().Be("Done");
		}

		[Fact]
		public void Reset_makes_it_inactive()
		{
			var model = Started();
			model.Active.Should().BeTrue();

			model.Reset();
			model.Active.Should().BeFalse();
			model.Alpha(50f).Should().Be(0f);
		}
	}
}
