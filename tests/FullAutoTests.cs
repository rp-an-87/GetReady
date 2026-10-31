using System.Linq;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// Full auto: the weapons in the slots you picked are put on full auto, when they have it.
	public class FullAutoTests
	{
		private readonly Body _body = new Body();
		private readonly FullAutoStep _step = new FullAutoStep();

		public FullAutoTests()
		{
			Reset();
			SettingsStore.Current.FullAuto.Slots.Clear();
		}

		private const string Glock17 = "5a7ae0c351dfba0017554310";

		private Weapon Wear(EquipmentSlot slot, string template)
		{
			var weapon = NewWeapon(template);
			return _body.Wear(slot, weapon);
		}

		private static void Pick(params EquipmentSlot[] slots) => SettingsStore.Current.FullAuto.Slots.AddRange(slots.Select(slot => slot.ToString()));

		[Fact]
		public void The_three_weapon_slots_are_offered()
		{
			FullAutoStep.Slots.Select(pair => pair.Value).Should().Equal("On sling", "On back", "Holster");
		}

		[Fact]
		public void A_slot_is_selected_when_it_is_in_the_settings()
		{
			Pick(EquipmentSlot.Holster);

			FullAutoStep.IsSelected(EquipmentSlot.Holster).Should().BeTrue();
			FullAutoStep.IsSelected(EquipmentSlot.FirstPrimaryWeapon).Should().BeFalse();
		}

		[Fact]
		public void Toggling_selects_then_unselects()
		{
			FullAutoStep.Toggle(EquipmentSlot.Holster);
			FullAutoStep.IsSelected(EquipmentSlot.Holster).Should().BeTrue();

			FullAutoStep.Toggle(EquipmentSlot.Holster);
			FullAutoStep.IsSelected(EquipmentSlot.Holster).Should().BeFalse();
		}

		[Fact]
		public void With_no_slot_picked_the_plan_says_no_weapon_has_full_auto()
		{
			Wear(EquipmentSlot.FirstPrimaryWeapon, M4A1);

			Texts(_step.Plan(_body.Controller)).Should().Equal("No weapon in the picked slots has full auto.");
		}

		[Fact]
		public void A_picked_slot_with_no_weapon_is_skipped()
		{
			Pick(EquipmentSlot.FirstPrimaryWeapon);

			Texts(_step.Plan(_body.Controller)).Should().Equal("No weapon in the picked slots has full auto.");
		}

		[Fact]
		public void A_weapon_without_full_auto_is_skipped()
		{
			Wear(EquipmentSlot.Holster, Glock17);
			Pick(EquipmentSlot.Holster);

			Texts(_step.Plan(_body.Controller)).Should().Equal("No weapon in the picked slots has full auto.");
		}

		[Fact]
		public void A_weapon_on_semi_auto_is_set_to_full_auto()
		{
			var rifle = Wear(EquipmentSlot.FirstPrimaryWeapon, M4A1);
			rifle.FireMode.SetFireMode(Weapon.EFireMode.single);
			Pick(EquipmentSlot.FirstPrimaryWeapon);

			var lines = _step.Plan(_body.Controller);

			Texts(lines).Should().Equal("Weapons on full auto: 0/1 weapons", "Set M4A1 to full auto");
			lines[1].Mode.Should().BeTrue();
			lines[1].Run().Result.Should().BeNull();
			rifle.FireMode.FireMode.Should().Be(Weapon.EFireMode.fullauto);
		}

		[Fact]
		public void A_weapon_already_on_full_auto_is_in_place()
		{
			var rifle = Wear(EquipmentSlot.FirstPrimaryWeapon, M4A1);
			rifle.FireMode.SetFireMode(Weapon.EFireMode.fullauto);
			Pick(EquipmentSlot.FirstPrimaryWeapon);

			Texts(_step.Plan(_body.Controller)).Should().Equal("Weapons on full auto: 1/1 weapons");
		}

		[Fact]
		public void Several_weapons_are_counted_together()
		{
			var rifle = Wear(EquipmentSlot.FirstPrimaryWeapon, M4A1);
			rifle.FireMode.SetFireMode(Weapon.EFireMode.fullauto);
			var pistol = Wear(EquipmentSlot.Holster, Glock18);
			pistol.FireMode.SetFireMode(Weapon.EFireMode.single);
			Wear(EquipmentSlot.SecondPrimaryWeapon, Mcx);
			Pick(EquipmentSlot.FirstPrimaryWeapon, EquipmentSlot.Holster);

			var lines = _step.Plan(_body.Controller);

			Texts(lines).Should().Equal("Weapons on full auto: 1/2 weapons", "Set Glock 18C to full auto");
		}

		[Fact]
		public void A_slot_not_picked_is_left_alone()
		{
			var rifle = Wear(EquipmentSlot.FirstPrimaryWeapon, M4A1);
			rifle.FireMode.SetFireMode(Weapon.EFireMode.single);
			var pistol = Wear(EquipmentSlot.Holster, Glock18);
			pistol.FireMode.SetFireMode(Weapon.EFireMode.single);
			Pick(EquipmentSlot.Holster);

			Texts(_step.Plan(_body.Controller)).Should().Equal("Weapons on full auto: 0/1 weapons", "Set Glock 18C to full auto");
		}
	}
}
