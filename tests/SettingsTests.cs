using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The settings file of a profile: what is written, what is read back, and old files.
	public class SettingsTests
	{
		public SettingsTests()
		{
			Reset();
		}

		private static string Write(Settings settings) => JsonConvert.SerializeObject(settings, Formatting.Indented);

		[Fact]
		public void An_empty_file_gives_the_defaults()
		{
			var settings = SettingsStore.Parse("{}");

			settings.UiScale.Should().Be(1f);
			settings.Ammo.Pointers.Should().Equal("nearest", "others", "loose", "weapon");
			settings.FullAuto.Slots.Should().Equal("FirstPrimaryWeapon", "SecondPrimaryWeapon");
			settings.Repair.Armor.Should().BeTrue();
			settings.Supplies.SwapWorse.Should().BeTrue();
			settings.SpecialSlots.Enabled.Should().BeTrue();
		}

		[Fact]
		public void A_file_with_nothing_in_it_gives_the_defaults() => SettingsStore.Parse("null").UiScale.Should().Be(1f);

		[Fact]
		public void What_is_written_is_read_back()
		{
			var settings = new Settings { UiScale = 1.3f };
			settings.HealMe.Enabled = true;
			settings.Ammo.Pointers = new List<string> { "weapon", "loose" };
			settings.Ammo.SparePick = "random";
			settings.Ammo.SpareIgnored.Add(new ItemRef { Template = "t1", Name = "Glock" });
			settings.Repair.TraderOrder = new List<string> { "b", "a" };
			settings.Repair.KeepDamaged.Add(new KeptItem { Id = "x", Name = "Armor" });
			settings.FullAuto.Slots = new List<string> { "Holster" };
			settings.Supplies.AllowOvershoot = false;
			settings.Supplies.CashExclusions.Add(new CashExclusion());

			var read = SettingsStore.Parse(Write(settings));

			read.UiScale.Should().Be(1.3f);
			read.HealMe.Enabled.Should().BeTrue();
			read.Ammo.Pointers.Should().Equal("weapon", "loose");
			read.Ammo.SparePick.Should().Be("random");
			read.Ammo.SpareIgnored.Should().ContainSingle().Which.Name.Should().Be("Glock");
			read.Repair.TraderOrder.Should().Equal("b", "a");
			read.Repair.KeepDamaged.Should().ContainSingle().Which.Id.Should().Be("x");
			read.FullAuto.Slots.Should().Equal("Holster");
			read.Supplies.AllowOvershoot.Should().BeFalse();
			read.Supplies.CashExclusions.Should().HaveCount(1);
		}

		[Fact]
		public void The_order_and_removals_of_a_list_are_not_mixed_with_its_defaults()
		{
			var settings = new Settings();
			settings.Ammo.Pointers = new List<string> { "weapon" };
			settings.FullAuto.Slots = new List<string>();

			var read = SettingsStore.Parse(Write(settings));

			read.Ammo.Pointers.Should().Equal("weapon");
			read.FullAuto.Slots.Should().BeEmpty();
		}

		[Fact]
		public void Lists_written_twice_over_by_an_old_version_are_cleaned()
		{
			var read = SettingsStore.Parse("{ \"Ammo\": { \"Pointers\": [\"loose\", \"nearest\", \"loose\", \"nearest\"] }, \"FullAuto\": { \"Slots\": [\"A\", \"A\", \"B\"] } }");

			read.Ammo.Pointers.Should().Equal("loose", "nearest");
			read.FullAuto.Slots.Should().Equal("A", "B");
		}

		[Fact]
		public void A_group_setting_keeps_its_amount_order_and_off_items()
		{
			var settings = new Settings();
			settings.Supplies.Groups["bandage"] = new GroupSetting { Target = 4, Order = new List<string> { "b", "a" }, Off = new List<string> { "c" }, Hotkey = "6" };
			settings.Supplies.Groups["bandage"].MaxPer["a"] = 3;

			var group = SettingsStore.Parse(Write(settings)).Supplies.Groups["bandage"];

			group.Target.Should().Be(4);
			group.Order.Should().Equal("b", "a");
			group.Off.Should().Equal("c");
			group.Hotkey.Should().Be("6");
			group.MaxPer["a"].Should().Be(3);
		}

		[Fact]
		public void The_places_of_a_group_are_kept()
		{
			var def = new GroupDef { Id = "g" };
			def.PlaceOrder();
			def.TogglePlace("Vest");
			SettingsStore.Current.Supplies.Groups.Count.Should().Be(1);

			var read = SettingsStore.Parse(Write(SettingsStore.Current));
			SettingsStore.UseForTests(read);

			def.PlaceOrder().Should().Equal("Pockets", "Special slots", "Backpack", "Pouch", "Vest");
			def.PlacesToTry().Should().Equal("Pockets", "Special slots", "Backpack", "Pouch");
		}

		[Fact]
		public void A_file_from_before_places_were_a_list_still_works()
		{
			var read = SettingsStore.Parse("{ \"Supplies\": { \"Groups\": { \"g\": { \"Target\": 2, \"Destinations\": [\"Vest\", \"Pouch\"] } } } }");
			SettingsStore.UseForTests(read);

			new GroupDef { Id = "g" }.PlacesToTry().Should().Equal("Vest", "Pouch", "Pockets", "Backpack");
		}

		[Fact]
		public void Settings_not_in_an_old_file_get_their_defaults()
		{
			var read = SettingsStore.Parse("{ \"UiScale\": 1.2 }");

			read.UiScale.Should().Be(1.2f);
			read.Repair.UseTraders.Should().BeTrue();
			read.Ammo.TopUpLoose.Should().BeTrue();
			read.Supplies.NonFiRBeforeLoose.Should().BeTrue();
		}

		[Fact]
		public void The_test_hook_keeps_the_settings_it_was_given()
		{
			var mine = new Settings { UiScale = 1.5f };
			SettingsStore.UseForTests(mine);

			SettingsStore.Current.Should().BeSameAs(mine);
		}

		[Fact]
		public void Saving_in_a_test_writes_nothing_and_does_not_throw()
		{
			System.Action save = () => SettingsStore.Save();

			save.Should().NotThrow();
		}
	}
}
