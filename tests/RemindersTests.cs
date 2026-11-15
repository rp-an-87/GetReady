using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
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
			facts.Armors.Add(new WornArmor { Name = "Vest", MissingPlates = { "FRONT" } });
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
		public void A_map_without_such_an_extract_is_never_asked_even_if_listed()
		{
			var settings = Cord(false);
			settings.Paracord.Maps.Add("Customs");
			Check(settings, On("Customs")).Should().BeEmpty();
			settings = Settings();
			settings.GreenFlare.Enabled = true;
			settings.GreenFlare.Maps.Add("Lighthouse");
			Check(settings, On("Lighthouse")).Should().BeEmpty();
		}

		[Fact]
		public void The_extract_maps_are_all_real_maps()
		{
			MapTags.Only(MapTags.FlareMaps).Select(map => map.Name).Should().BeEquivalentTo(MapTags.FlareMaps);
			MapTags.Only(MapTags.AlpinistMaps).Select(map => map.Name).Should().BeEquivalentTo(MapTags.AlpinistMaps);
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
			facts.Armors.Add(new WornArmor { Name = "Slick", BodyArmor = true });
			var found = Check(Cord(armor: true), facts);
			found.Should().ContainSingle(r => r.Kind == Kind.Armor && r.Text.Contains("Slick") && r.Text.Contains("INCOMPATIBLE"));
			Check(Cord(armor: false), facts).Should().BeEmpty();
		}

		[Fact]
		public void An_armored_rig_does_not_block_the_extract()
		{
			var facts = On("Lighthouse", Paracord, RedRebel);
			facts.Armors.Add(new WornArmor { Name = "AVS" });
			Check(Cord(armor: true), facts).Should().BeEmpty();
		}

		[Fact]
		public void Armor_needs_both_of_the_pair_and_some_armor_worn()
		{
			var facts = On("Lighthouse", Paracord);
			facts.Armors.Add(new WornArmor { Name = "Slick", BodyArmor = true });
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
			facts.Armors.Add(new WornArmor { Name = "Slick", Template = "t1", MissingPlates = { "FRONT", "BACK" } });
			facts.Armors.Add(new WornArmor { Name = "Fort" });
			var found = Check(settings, facts).Single();
			found.Kind.Should().Be(Kind.Plates);
			found.Text.Should().Be("Slick IS MISSING THE FRONT AND BACK PLATE!");
			found.Icons.Should().Equal("t1");
		}

		[Fact]
		public void No_armor_worn_warns_only_when_the_switch_is_on_and_the_inventory_is_known()
		{
			var settings = Settings();
			var facts = On("Customs");
			facts.ArmorKnown = true;
			Check(settings, facts).Should().BeEmpty();
			settings.Equipment.NoArmor = true;
			Check(settings, facts).Single().Kind.Should().Be(Kind.NoArmor);
			facts.ArmorKnown = false;
			Check(settings, facts).Should().BeEmpty();
		}

		[Fact]
		public void Any_worn_armor_body_or_rig_is_armor()
		{
			var settings = Settings();
			settings.Equipment.NoArmor = true;
			var facts = On("Customs");
			facts.ArmorKnown = true;
			facts.Armors.Add(new WornArmor { Name = "Rig" });
			Check(settings, facts).Should().BeEmpty();
			facts.Armors.Clear();
			facts.Armors.Add(new WornArmor { Name = "Slick", BodyArmor = true });
			Check(settings, facts).Should().BeEmpty();
		}

		[Fact]
		public void A_rig_is_armor_only_when_it_takes_plates()
		{
			var body = new Body();
			WornArmors(body.Inventory).Should().BeEmpty();
			body.Wear(EquipmentSlot.TacticalVest, RealItems.Any("5648a69d4bdc2ded0b8b457b")); // a plain rig
			WornArmors(body.Inventory).Should().BeEmpty();
			var carrier = new Body();
			carrier.Wear(EquipmentSlot.TacticalVest, RealItems.Any("5ab8dced86f774646209ec87")); // a plate carrier, empty
			var worn = WornArmors(carrier.Inventory).Single();
			worn.BodyArmor.Should().BeFalse();
			worn.MissingPlates.Should().Contain("FRONT").And.Contain("BACK");
			var built = new Body();
			built.Wear(EquipmentSlot.TacticalVest, RealItems.Any("5c0e3eb886f7742015526062")); // armor built in, no plate slots
			WornArmors(built.Inventory).Should().BeEmpty();
		}

		[Fact]
		public void Insurance_to_do_is_a_reminder_that_counts_the_items()
		{
			var lines = new[] { new PlanLine { Text = "Equipped gear" }, new PlanLine { Text = "Insure 3 items", Insured = 3, Run = () => Task.FromResult<string>(null) } };
			InsuranceText(lines).Should().Be("3 ITEMS NOT INSURED! DO IT ON THE NEXT SCREEN.");
			InsuranceText(new[] { new PlanLine { Insured = 1, Run = () => Task.FromResult<string>(null) } }).Should().StartWith("1 ITEM NOT");
			InsuranceText(new[] { new PlanLine { Text = "Equipped gear" } }).Should().BeNull();
			InsuranceText(null).Should().BeNull();
			var facts = On("Customs");
			facts.InsuranceText = InsuranceText(lines);
			Check(Settings(), facts).Single().Kind.Should().Be(Kind.Insurance);
		}

		[Fact]
		public void Insurance_that_cannot_be_done_says_why()
		{
			var line = Steps.Problem("Insure 3 items at Prapor", "needs 12,000 roubles, you have 500");
			InsuranceText(new[] { line }).Should().Be("INSURANCE: NEEDS 12,000 ROUBLES, YOU HAVE 500!");
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
			settings.Needs.Enabled = true;
			settings.Equipment.Enabled = true;
			var facts = On("Lighthouse", Paracord);
			facts.HydrationPercent = 1f;
			facts.Armors.Add(new WornArmor { Name = "Slick", MissingPlates = { "BACK" } });
			Check(settings, facts).Select(r => r.Kind).Should().Equal(Kind.Paracord, Kind.Needs, Kind.Plates);
		}

		[Fact]
		public void A_weapon_in_the_scabbard_counts_as_on_you()
		{
			var body = new Body();
			body.Wear(EquipmentSlot.Scabbard, RealItems.Any(RedRebel));
			OnYou(body.Inventory).Select(item => item.TemplateId.ToString()).Should().Contain(RedRebel);
		}

		[Fact]
		public void The_equipment_itself_is_not_an_item_on_you()
		{
			var body = new Body();
			OnYou(body.Inventory).Should().NotContain(body.Equipment);
		}

		private static CaseEntry Case(string id, params string[] maps) => new CaseEntry { CaseId = id, CaseTemplate = "t" + id, Tag = id, Maps = maps.ToList() };

		private static Facts WithCases(string map, string[] carriedIds, params CaseEntry[] cases)
		{
			var facts = On(map);
			facts.Cases = cases.ToList();
			facts.CarriedIds = new HashSet<string>(carriedIds);
			return facts;
		}

		[Fact]
		public void A_case_for_another_map_is_a_wrong_container()
		{
			var found = Check(Settings(), WithCases("Customs", new[] { "a" }, Case("a", "Woods"))).Single();
			found.Kind.Should().Be(Kind.WrongContainers);
			found.Cases.Select(entry => entry.CaseId).Should().Equal("a");
			found.TextFirst.Should().BeFalse();
		}

		[Fact]
		public void A_case_for_this_map_that_is_not_on_you_is_a_missing_container()
		{
			var found = Check(Settings(), WithCases("Customs", new string[0], Case("a", "Customs"))).Single();
			found.Kind.Should().Be(Kind.MissingContainers);
			found.Cases.Select(entry => entry.CaseId).Should().Equal("a");
			found.TextFirst.Should().BeTrue();
		}

		[Fact]
		public void The_container_kinds_each_have_their_switch()
		{
			var facts = WithCases("Customs", new[] { "a" }, Case("a", "Woods"), Case("b", "Customs"));
			Check(Settings(), facts).Select(r => r.Kind).Should().Equal(Kind.WrongContainers, Kind.MissingContainers);
			var settings = Settings();
			settings.WrongContainers = false;
			Check(settings, facts).Select(r => r.Kind).Should().Equal(Kind.MissingContainers);
			settings.WrongContainers = true;
			settings.MissingContainers = false;
			Check(settings, facts).Select(r => r.Kind).Should().Equal(Kind.WrongContainers);
			settings.WrongContainers = false;
			Check(settings, facts).Should().BeEmpty();
		}

		[Fact]
		public void Containers_come_before_the_other_reminders()
		{
			var settings = Needs();
			var facts = WithCases("Customs", new string[0], Case("b", "Customs"));
			facts.HydrationPercent = 1f;
			Check(settings, facts).Select(r => r.Kind).Should().Equal(Kind.MissingContainers, Kind.Needs);
		}

		[Fact]
		public void A_case_with_no_map_set_is_never_mentioned()
		{
			Check(Settings(), WithCases("Customs", new[] { "a" }, Case("a"))).Should().BeEmpty();
		}

		[Fact]
		public void The_container_kinds_can_be_asked_for_apart()
		{
			var report = new MapTags.Report();
			report.Wrong.Add(new CaseEntry { CaseId = "w" });
			report.Missing.Add(new CaseEntry { CaseId = "m" });
			report.Only(true, true).Any.Should().BeTrue();
			report.Only(true, false).Missing.Should().BeEmpty();
			report.Only(true, false).Wrong.Should().HaveCount(1);
			report.Only(false, true).Wrong.Should().BeEmpty();
			report.Only(false, true).Missing.Should().HaveCount(1);
			report.Only(false, false).Any.Should().BeFalse();
			report.Wrong.Should().HaveCount(1, "the report itself is left as it was");
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
			new Settings().Reminders.WrongContainers.Should().BeTrue();
			new Settings().Reminders.MissingContainers.Should().BeTrue();
		}
	}
}
