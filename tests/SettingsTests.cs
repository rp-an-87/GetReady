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
			settings.Ammo.Pointers.Should().Equal("nearest", "others", "loose", "weapon", "anywhere");
			settings.FullAuto.Slots.Should().Equal("FirstPrimaryWeapon", "SecondPrimaryWeapon");
			settings.Repair.Armor.Should().BeTrue();
			new GroupSetting().ReplaceWorse.Should().BeTrue();
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
			settings.Ammo.SpareStacks.Add(new ModelStacks { Template = "t1", Name = "Glock", Stacks = 0 });
			settings.Repair.TraderOrder = new List<string> { "b", "a" };
			settings.Repair.KeepDamaged.Add(new KeptItem { Id = "x", Name = "Armor" });
			settings.FullAuto.Slots = new List<string> { "Holster" };
			settings.Supplies.Groups["bandage"] = new GroupSetting { AllowOvershoot = false };
			settings.Cases.Cases.Add(new CaseEntry { CaseId = "c", Maps = new List<string> { "Woods" } });
			settings.Cases.AutoUpdate = false;

			var read = SettingsStore.Parse(Write(settings));

			read.UiScale.Should().Be(1.3f);
			read.HealMe.Enabled.Should().BeTrue();
			read.Ammo.Pointers.Should().Equal("weapon", "loose");
			read.Ammo.SparePick.Should().Be("random");
			read.Ammo.SpareStacks.Should().ContainSingle().Which.Name.Should().Be("Glock");
			read.Repair.TraderOrder.Should().Equal("b", "a");
			read.Repair.KeepDamaged.Should().ContainSingle().Which.Id.Should().Be("x");
			read.FullAuto.Slots.Should().Equal("Holster");
			read.Supplies.Groups["bandage"].AllowOvershoot.Should().BeFalse();
			read.Cases.AutoUpdate.Should().BeFalse();
			read.Cases.Cases.Should().ContainSingle().Which.Maps.Should().Equal("Woods");
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
		public void Settings_not_in_an_old_file_get_their_defaults()
		{
			var read = SettingsStore.Parse("{ \"UiScale\": 1.2 }");

			read.UiScale.Should().Be(1.2f);
			read.Repair.UseTraders.Should().BeTrue();
			read.Ammo.TopUpLoose.Should().BeTrue();
			read.Ammo.LooseShortIsProblem.Should().BeFalse();
			read.Supplies.NonFiRBeforeLoose.Should().BeTrue();
			read.Supplies.SwapFiRForNonFiR.Should().BeTrue();
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

		[Fact]
		public void An_old_file_has_no_stacks_per_model()
		{
			SettingsStore.Parse("{ \"UiScale\": 1.2 }").Ammo.SpareStacks.Should().BeEmpty();
		}

		[Fact]
		public void The_stacks_per_model_are_read_from_the_file()
		{
			var read = SettingsStore.Parse("{ \"Ammo\": { \"SpareStacks\": [ { \"Template\": \"abc\", \"Name\": \"M4A1\", \"Stacks\": 3 } ] } }");

			read.Ammo.SpareStacks.Should().ContainSingle().Which.Stacks.Should().Be(3);
		}

		[Fact]
		public void Setting_the_stacks_of_a_model_adds_it_once_and_then_changes_it()
		{
			SettingsStore.UseForTests(new Settings());
			var generic = AmmoStep.Spare.Setting.Target;

			AmmoStep.SetStacks("abc", "M4A1", generic + 1);
			AmmoStep.SetStacks("abc", "M4A1", generic + 3);

			SettingsStore.Current.Ammo.SpareStacks.Should().ContainSingle().Which.Stacks.Should().Be(generic + 3);
		}

		[Fact]
		public void A_number_set_by_hand_that_equals_the_generic_one_stays_the_models_own_and_does_not_follow_it()
		{
			SettingsStore.UseForTests(new Settings());
			var generic = AmmoStep.Spare.Setting.Target;
			AmmoStep.SetStacks("abc", "M4A1", generic + 2);

			AmmoStep.SetStacks("abc", "M4A1", generic);

			SettingsStore.Current.Ammo.SpareStacks.Should().ContainSingle().Which.Stacks.Should().Be(generic);
			AmmoStep.HasOwnStacks("abc").Should().BeTrue();
			AmmoStep.StacksOf("abc").Should().Be(generic);
		}

		[Fact]
		public void Removing_a_model_takes_it_off_the_list_and_back_to_the_generic_number()
		{
			SettingsStore.UseForTests(new Settings());
			AmmoStep.SetStacks("abc", "M4A1", AmmoStep.Spare.Setting.Target);

			AmmoStep.RemoveModel("abc");

			SettingsStore.Current.Ammo.SpareStacks.Should().BeEmpty();
			AmmoStep.HasOwnStacks("abc").Should().BeFalse();
			AmmoStep.StacksOf("abc").Should().Be(AmmoStep.Spare.Setting.Target);
		}

		[Fact]
		public void None_is_zero_stacks_and_stays_on_the_list()
		{
			SettingsStore.UseForTests(new Settings());

			AmmoStep.SetStacks("abc", "Glock", 0);

			AmmoStep.StacksOf("abc").Should().Be(0);
			SettingsStore.Current.Ammo.SpareStacks.Should().ContainSingle().Which.Stacks.Should().Be(0);
		}
	}
}
