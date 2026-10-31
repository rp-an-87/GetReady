using System.Linq;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.RealItems;

namespace GetReady.Tests
{
	// What the game's own data says each medical item treats, and how the plugin sorts them (real items from the item database).
	public class MedicalItemTests
	{
		private static bool Treats(string template, EDamageEffectType effect) => Game.Treats(Meds(template), effect);

		// Straight from the item database: only the Grizzly treats fractures among the kits; the Surv12 does too, besides a destroyed part
		[Theory]
		[InlineData(Grizzly, true, true, true, false)]
		[InlineData(Salewa, true, true, false, false)]
		[InlineData(Afak, true, true, false, false)]
		[InlineData(Ifak, true, true, false, false)]
		[InlineData(Car, false, true, false, false)]
		[InlineData(Ai2, false, false, false, false)]
		[InlineData(ArmyBandage, false, true, false, false)]
		[InlineData(AsepticBandage, false, true, false, false)]
		[InlineData(Cat, true, false, false, false)]
		[InlineData(Esmarch, true, false, false, false)]
		[InlineData(CalokB, true, false, false, false)]
		[InlineData(AluSplint, false, false, true, false)]
		[InlineData(ImmobilizingSplint, false, false, true, false)]
		[InlineData(Cms, false, false, false, true)]
		[InlineData(Surv12, false, false, true, true)]
		public void The_game_data_says_what_each_item_treats(string template, bool heavy, bool light, bool fracture, bool destroyed)
		{
			Treats(template, EDamageEffectType.HeavyBleeding).Should().Be(heavy);
			Treats(template, EDamageEffectType.LightBleeding).Should().Be(light);
			Treats(template, EDamageEffectType.Fracture).Should().Be(fracture);
			Treats(template, EDamageEffectType.DestroyedPart).Should().Be(destroyed);
		}

		[Fact]
		public void The_Grizzly_has_the_biggest_resource() => Game.ResourceOf(Meds(Grizzly)).Should().BeGreaterThan(Game.ResourceOf(Meds(Salewa)));

		[Theory]
		[InlineData(ArmyBandage)]
		[InlineData(AsepticBandage)]
		public void A_bandage_treats_only_a_light_bleed(string bandage)
		{
			Treats(bandage, EDamageEffectType.LightBleeding).Should().BeTrue();
			Treats(bandage, EDamageEffectType.HeavyBleeding).Should().BeFalse();
			Treats(bandage, EDamageEffectType.Fracture).Should().BeFalse();
			Game.IsBandage(Meds(bandage)).Should().BeTrue();
		}

		[Theory]
		[InlineData(Cat)]
		[InlineData(Esmarch)]
		[InlineData(CalokB)]
		public void A_tourniquet_treats_a_heavy_bleed(string tourniquet)
		{
			Treats(tourniquet, EDamageEffectType.HeavyBleeding).Should().BeTrue();
			Treats(tourniquet, EDamageEffectType.Fracture).Should().BeFalse();
			Game.IsTourniquet(Meds(tourniquet)).Should().BeTrue();
		}

		[Theory]
		[InlineData(AluSplint)]
		[InlineData(ImmobilizingSplint)]
		public void A_splint_treats_a_fracture(string splint)
		{
			Treats(splint, EDamageEffectType.Fracture).Should().BeTrue();
			Treats(splint, EDamageEffectType.HeavyBleeding).Should().BeFalse();
			Game.IsSplint(Meds(splint)).Should().BeTrue();
		}

		[Theory]
		[InlineData(Cms)]
		[InlineData(Surv12)]
		public void A_surgery_kit_treats_a_destroyed_part(string kit)
		{
			Treats(kit, EDamageEffectType.DestroyedPart).Should().BeTrue();
			Game.IsSurgeryKit(Meds(kit)).Should().BeTrue();
		}

		[Fact]
		public void The_Surv12_sets_bones_too_but_counts_as_a_surgery_kit_not_a_splint()
		{
			Treats(Surv12, EDamageEffectType.Fracture).Should().BeTrue();
			Game.IsSplint(Meds(Surv12)).Should().BeFalse();
		}

		[Fact]
		public void The_Grizzly_passes_the_kit_and_splint_tests_alone_it_is_the_item_tree_that_keeps_it_out_of_the_splint_group()
		{
			var grizzly = Meds(Grizzly);

			Game.IsHealingKit(grizzly).Should().BeTrue();
			Game.IsSplint(grizzly).Should().BeTrue();
			Game.IsBandage(grizzly).Should().BeFalse();
			Game.IsTourniquet(grizzly).Should().BeFalse();
			Game.IsSurgeryKit(grizzly).Should().BeFalse();
		}

		// ---- Treat injuries: what each injury accepts

		private static bool Accepts(string trouble, string template) => HealMeStep.Troubles.First(t => t.Id == trouble).Treats(Meds(template));

		[Theory]
		[InlineData("heavy", Grizzly, true)]
		[InlineData("heavy", Cat, true)]
		[InlineData("heavy", ArmyBandage, false)]
		[InlineData("light", ArmyBandage, true)]
		[InlineData("light", Cat, false)]
		[InlineData("fracture", AluSplint, true)]
		[InlineData("fracture", Surv12, true)]
		[InlineData("fracture", Cat, false)]
		public void Each_injury_accepts_what_the_game_says_treats_it(string trouble, string template, bool expected) =>
			Accepts(trouble, template).Should().Be(expected);
	}
}
