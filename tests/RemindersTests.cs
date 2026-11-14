using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using static GetReady.Reminders;

namespace GetReady.Tests
{
	// The soft reminders of the map screen: what is said for which facts. Plain facts in, reminders out.
	public class RemindersTests
	{
		private static Facts On(string map, params string[] carried) => new Facts { Map = map, Carried = new HashSet<string>(carried) };

		private static RemindersSettings Settings() => new RemindersSettings();

		[Fact]
		public void Everything_is_off_by_default()
		{
			var facts = On("Customs");
			facts.HydrationPercent = 1f;
			facts.EnergyPercent = 1f;
			facts.Armors.Add(new WornArmor { Name = "Vest", MissingPlate = true });
			Check(Settings(), facts).Should().BeEmpty();
		}

		[Fact]
		public void Green_flare_is_asked_for_on_its_maps_only()
		{
			var settings = Settings();
			settings.GreenFlare.Enabled = true;
			settings.GreenFlare.Maps.Add("Woods");
			Check(settings, On("Woods")).Should().ContainSingle(r => r.Kind == Kind.GreenFlare && r.Icons.SequenceEqual(new[] { GreenFlare }));
			Check(settings, On("Customs")).Should().BeEmpty();
			Check(settings, On("Woods", GreenFlare)).Should().BeEmpty();
			Check(settings, On(null)).Should().BeEmpty();
		}

		[Fact]
		public void Green_flare_with_no_map_picked_says_nothing()
		{
			var settings = Settings();
			settings.GreenFlare.Enabled = true;
			Check(settings, On("Woods")).Should().BeEmpty();
		}

		[Fact]
		public void Map_names_match_without_regard_to_case()
		{
			var settings = Settings();
			settings.GreenFlare.Enabled = true;
			settings.GreenFlare.Maps.Add("woods");
			Check(settings, On("Woods")).Should().HaveCount(1);
		}

		private static RemindersSettings Cord(bool pairOnly = true, bool armor = false)
		{
			var settings = Settings();
			settings.Paracord.Enabled = true;
			settings.Paracord.PairOnly = pairOnly;
			settings.Paracord.WarnArmor = armor;
			settings.Paracord.Maps.Add("Lighthouse");
			return settings;
		}

		[Fact]
		public void Pair_only_asks_for_the_other_one_of_the_pair()
		{
			Check(Cord(), On("Lighthouse", Paracord)).Single().Icons.Should().Equal(RedRebel);
			Check(Cord(), On("Lighthouse", RedRebel)).Single().Icons.Should().Equal(Paracord);
		}

		[Fact]
		public void Pair_only_leaves_neither_and_both_alone()
		{
			Check(Cord(), On("Lighthouse")).Should().BeEmpty();
			Check(Cord(), On("Lighthouse", Paracord, RedRebel)).Should().BeEmpty();
		}

		[Fact]
		public void Without_pair_only_every_missing_one_is_asked_for()
		{
			Check(Cord(false), On("Lighthouse")).Single().Icons.Should().Equal(Paracord, RedRebel);
			Check(Cord(false), On("Lighthouse", Paracord)).Single().Icons.Should().Equal(RedRebel);
			Check(Cord(false), On("Lighthouse", Paracord, RedRebel)).Should().BeEmpty();
		}

		[Fact]
		public void Paracord_is_for_its_maps_only()
		{
			Check(Cord(false), On("Customs")).Should().BeEmpty();
		}

		[Fact]
		public void Armor_with_the_pair_is_named_as_incompatible()
		{
			var facts = On("Lighthouse", Paracord, RedRebel);
			facts.Armors.Add(new WornArmor { Name = "Slick" });
			var found = Check(Cord(armor: true), facts);
			found.Should().ContainSingle(r => r.Kind == Kind.Armor && r.Text.Contains("Slick") && r.Text.Contains("INCOMPATIBLE"));
			Check(Cord(armor: false), facts).Should().BeEmpty();
		}

		[Fact]
		public void Armor_needs_both_of_the_pair_and_some_armor_worn()
		{
			var facts = On("Lighthouse", Paracord);
			facts.Armors.Add(new WornArmor { Name = "Slick" });
			Check(Cord(armor: true), facts).Should().NotContain(r => r.Kind == Kind.Armor);
			Check(Cord(armor: true), On("Lighthouse", Paracord, RedRebel)).Should().BeEmpty();
		}

		private static RemindersSettings Needs(bool onlyWithout = true)
		{
			var settings = Settings();
			settings.Needs.Enabled = true;
			settings.Needs.OnlyIfNoSupplies = onlyWithout;
			return settings;
		}

		[Fact]
		public void Low_hydration_and_energy_are_one_reminder()
		{
			var facts = On("Customs");
			facts.HydrationPercent = 10f;
			facts.EnergyPercent = 20f;
			var found = Check(Needs(), facts).Single();
			found.Kind.Should().Be(Kind.Needs);
			found.Text.Should().Contain("HYDRATION").And.Contain("ENERGY");
		}

		[Fact]
		public void A_need_at_or_over_the_threshold_is_fine()
		{
			var facts = On("Customs");
			facts.HydrationPercent = 30f;
			facts.EnergyPercent = 90f;
			Check(Needs(), facts).Should().BeEmpty();
		}

		[Fact]
		public void Unknown_needs_say_nothing()
		{
			Check(Needs(), On("Customs")).Should().BeEmpty();
		}

		[Fact]
		public void A_drink_covers_hydration_and_food_covers_energy()
		{
			var facts = On("Customs");
			facts.HydrationPercent = 5f;
			facts.EnergyPercent = 5f;
			facts.HasDrink = true;
			var found = Check(Needs(), facts).Single();
			found.Text.Should().Contain("ENERGY").And.NotContain("HYDRATION");
			facts.HasFood = true;
			Check(Needs(), facts).Should().BeEmpty();
			Check(Needs(false), facts).Should().HaveCount(1);
		}

		[Fact]
		public void Each_need_can_be_switched_off_and_has_its_own_threshold()
		{
			var settings = Needs();
			settings.Needs.WarnEnergy = false;
			settings.Needs.HydrationBelow = 60;
			var facts = On("Customs");
			facts.HydrationPercent = 50f;
			facts.EnergyPercent = 1f;
			Check(settings, facts).Single().Text.Should().Contain("HYDRATION").And.NotContain("ENERGY");
		}

		[Fact]
		public void An_armor_with_an_empty_plate_slot_has_missing_pieces()
		{
			var settings = Settings();
			settings.Equipment.Enabled = true;
			var facts = On("Customs");
			facts.Armors.Add(new WornArmor { Name = "Slick", MissingPlate = true });
			facts.Armors.Add(new WornArmor { Name = "Fort", MissingPlate = false });
			var found = Check(settings, facts).Single();
			found.Kind.Should().Be(Kind.Plates);
			found.Text.Should().Contain("Slick").And.NotContain("Fort");
		}

		[Fact]
		public void Only_front_and_back_plates_count_whatever_the_case_of_the_id()
		{
			IsFrontOrBackPlate("Front_plate").Should().BeTrue();
			IsFrontOrBackPlate("back_plate").Should().BeTrue();
			IsFrontOrBackPlate("Left_side_plate").Should().BeFalse();
			IsFrontOrBackPlate("Soft_armor_front").Should().BeFalse();
			IsFrontOrBackPlate("Groin").Should().BeFalse();
		}

		[Fact]
		public void The_bars_come_in_a_fixed_order_one_per_kind()
		{
			var settings = Cord(false, true);
			settings.GreenFlare.Enabled = true;
			settings.GreenFlare.Maps.Add("Lighthouse");
			settings.Needs.Enabled = true;
			settings.Equipment.Enabled = true;
			var facts = On("Lighthouse", Paracord);
			facts.HydrationPercent = 1f;
			facts.Armors.Add(new WornArmor { Name = "Slick", MissingPlate = true });
			Check(settings, facts).Select(r => r.Kind).Should().Equal(Kind.GreenFlare, Kind.Paracord, Kind.Needs, Kind.Plates);
		}

		[Fact]
		public void The_settings_survive_the_file()
		{
			var settings = new Settings();
			settings.Reminders.Paracord.Enabled = true;
			settings.Reminders.Paracord.Maps.Add("Lighthouse");
			settings.Reminders.Needs.HydrationBelow = 45;
			var back = SettingsStore.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(settings));
			back.Reminders.Paracord.Enabled.Should().BeTrue();
			back.Reminders.Paracord.Maps.Should().Equal("Lighthouse");
			back.Reminders.Paracord.PairOnly.Should().BeTrue();
			back.Reminders.Needs.HydrationBelow.Should().Be(45);
			new Settings().Reminders.Needs.OnlyIfNoSupplies.Should().BeTrue();
		}
	}
}
