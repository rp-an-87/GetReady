using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace GetReady.Tests
{
	// One settings file per profile: which file is read when you log in, when another profile logs in, and what is written.
	public class SettingsStoreTests : IDisposable
	{
		private readonly string _folder = Path.Combine(Path.GetTempPath(), "getready-tests-" + Guid.NewGuid().ToString("N"));
		private string _loggedIn;
		private int _now;

		public SettingsStoreTests()
		{
			SupplyFixture.Reset();
			SettingsStore.StartOver(_folder, () => _loggedIn);
			SettingsStore.Clock = () => _now;
		}

		public void Dispose()
		{
			SettingsStore.Clock = () => Environment.TickCount;
			SettingsStore.FolderOverride = null;
			SettingsStore.LoggedInProfile = () => null;
			SettingsStore.UseForTests(new Settings());
			if (Directory.Exists(_folder))
			{
				Directory.Delete(_folder, true);
			}
		}

		private string FileOf(string profile) => Path.Combine(_folder, profile + ".json");

		private void Login(string profile)
		{
			_loggedIn = profile;
			_now += 1000; // the store looks at who is logged in at most twice a second
		}

		[Fact]
		public void Before_anyone_is_logged_in_the_defaults_are_used()
		{
			SettingsStore.Current.Repair.Armor.Should().BeTrue();
			File.Exists(FileOf("a")).Should().BeFalse();
		}

		[Fact]
		public void Nothing_is_written_before_anyone_is_logged_in()
		{
			SettingsStore.Current.Repair.UseTraders = false;

			SettingsStore.Save();

			Directory.Exists(_folder).Should().BeFalse();
		}

		[Fact]
		public void Logging_in_with_no_file_yet_starts_from_the_defaults_and_save_creates_it()
		{
			Login("profileA");
			SettingsStore.Current.Repair.UseTraders = false;

			SettingsStore.Save();

			File.Exists(FileOf("profileA")).Should().BeTrue();
		}

		[Fact]
		public void Logging_in_reads_that_profiles_file()
		{
			Directory.CreateDirectory(_folder);
			File.WriteAllText(FileOf("profileA"), "{\"Repair\":{\"UseTraders\":false}}");

			Login("profileA");

			SettingsStore.Current.Repair.UseTraders.Should().BeFalse();
		}

		[Fact]
		public void What_was_saved_comes_back_on_the_next_login()
		{
			Login("profileA");
			SettingsStore.Current.Repair.UseTraders = false;
			SettingsStore.Current.Repair.TraderOrder.AddRange(new[] { "x", "y" });
			SettingsStore.Save();

			SettingsStore.StartOver(_folder, () => "profileA");
			SettingsStore.Clock = () => _now;

			SettingsStore.Current.Repair.UseTraders.Should().BeFalse();
			SettingsStore.Current.Repair.TraderOrder.Should().Equal("x", "y");
		}

		[Fact]
		public void Each_profile_has_its_own_settings()
		{
			Login("profileA");
			SettingsStore.Current.Repair.UseTraders = false;
			SettingsStore.Save();

			Login("profileB");

			SettingsStore.Current.Repair.UseTraders.Should().BeTrue("profile B has no file and gets the defaults");

			Login("profileA");

			SettingsStore.Current.Repair.UseTraders.Should().BeFalse("profile A's file is read again");
		}

		[Fact]
		public void Switching_profile_does_not_carry_the_old_ones_settings_over()
		{
			Login("profileA");
			SettingsStore.Current.Ammo.Chamber = false;
			SettingsStore.Save();

			Login("profileB");

			SettingsStore.Current.Ammo.Chamber.Should().BeTrue();
		}

		[Fact]
		public void A_save_goes_to_the_profile_that_is_logged_in()
		{
			Login("profileA");
			SettingsStore.Current.Repair.UseTraders = false;
			Login("profileB");
			SettingsStore.Current.Repair.Armor = false;

			SettingsStore.Save();

			File.Exists(FileOf("profileB")).Should().BeTrue();
			File.Exists(FileOf("profileA")).Should().BeFalse("profile A's changes were never saved");
		}

		[Fact]
		public void The_login_is_not_looked_at_more_than_twice_a_second()
		{
			Login("profileA");
			SettingsStore.Current.Repair.UseTraders = false;
			SettingsStore.Save();

			_loggedIn = "profileB";
			_now += 100;

			SettingsStore.Current.Repair.UseTraders.Should().BeFalse("still profile A's: too soon to look again");

			_now += 500;

			SettingsStore.Current.Repair.UseTraders.Should().BeTrue("now it looks, and profile B has no file");
		}

		[Fact]
		public void The_settings_are_not_reloaded_while_the_same_profile_stays_logged_in()
		{
			Login("profileA");
			SettingsStore.Current.Repair.UseTraders = false; // not saved yet

			_now += 5000;

			SettingsStore.Current.Repair.UseTraders.Should().BeFalse();
		}

		[Fact]
		public void Logging_out_keeps_the_settings_as_they_are()
		{
			Login("profileA");
			SettingsStore.Current.Repair.UseTraders = false;

			Login(null);

			SettingsStore.Current.Repair.UseTraders.Should().BeFalse();
		}

		[Fact]
		public void A_file_that_cannot_be_read_falls_back_to_the_defaults()
		{
			Directory.CreateDirectory(_folder);
			File.WriteAllText(FileOf("profileA"), "this is not json {{{");

			Login("profileA");

			SettingsStore.Current.Repair.UseTraders.Should().BeTrue();
		}

		[Fact]
		public void A_clock_that_wraps_round_does_not_stop_the_check()
		{
			Login("profileA");
			SettingsStore.Current.Repair.UseTraders = false;
			SettingsStore.Save();
			_now = int.MinValue + 10; // TickCount wraps to a negative number after weeks

			_loggedIn = "profileB";

			SettingsStore.Current.Repair.UseTraders.Should().BeTrue();
		}
	}
}
