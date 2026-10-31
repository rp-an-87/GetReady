using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using FluentAssertions;
using NSubstitute;
using Xunit;
using static GetReady.Tests.RealItems;

namespace GetReady.Tests
{
	// Treat injuries: the plan lines and the run, with a fake health controller and a stash of real items.
	public class HealMeStepTests
	{
		// A fake player: what is wrong, what is in the stash. Using an item clears what it treats (and the HP, for a kit) and uses the item up.
		private class Patient
		{
			public bool Heavy, Light, Fracture, HpMissing;
			public EBodyPart? Destroyed;
			public readonly List<Item> Stash = new List<Item>();
			public readonly List<string> Used = new List<string>();
			public readonly HashSet<string> Refused = new HashSet<string>(); // templates the game would not apply
			public readonly IHealthController Health = Substitute.For<IHealthController>();

			public Patient()
			{
				Health.GetBodyPartHealth(Arg.Any<EBodyPart>(), Arg.Any<bool>()).Returns(call =>
				{
					bool chest = (EBodyPart)call[0] == EBodyPart.Chest;
					return new ValueStruct { Current = HpMissing && chest ? 40 : 100, Maximum = 100 };
				});
				Health.IsBodyPartDestroyed(Arg.Any<EBodyPart>()).Returns(call => Destroyed.HasValue && (EBodyPart)call[0] == Destroyed.Value);
				Health.IsBodyPartBroken(Arg.Any<EBodyPart>()).Returns(false);
				Health.GetAllActiveEffects(Arg.Any<EBodyPart>()).Returns(call => Effects((EBodyPart)call[0]));
				Health.ApplyItem(Arg.Any<Item>(), EBodyPart.Common, Arg.Any<float?>()).Returns(call => Apply((Item)call[0]));
			}

			private static IHealthEffect Effect(Type type)
			{
				var effect = Substitute.For<IHealthEffect>();
				effect.Type.Returns(type);
				return effect;
			}

			private IEnumerable<IHealthEffect> Effects(EBodyPart part)
			{
				if (part == EBodyPart.LeftArm && Heavy)
				{
					yield return Effect(typeof(IHeavyBleeding));
				}

				if (part == EBodyPart.RightArm && Light)
				{
					yield return Effect(typeof(ILightBleeding));
				}

				if (part == EBodyPart.LeftLeg && Fracture)
				{
					yield return Effect(typeof(IFracture));
				}
			}

			private bool Apply(Item item)
			{
				string template = item.TemplateId.ToString();
				if (Refused.Contains(template))
				{
					return false;
				}

				bool changed = false;
				if (Heavy && Game.Treats(item, EDamageEffectType.HeavyBleeding)) { Heavy = false; changed = true; }
				if (Light && Game.Treats(item, EDamageEffectType.LightBleeding)) { Light = false; changed = true; }
				if (Fracture && Game.Treats(item, EDamageEffectType.Fracture)) { Fracture = false; changed = true; }
				if (HpMissing && HealMeStep.Troubles.First(trouble => trouble.Id == "hp").Treats(item)) { HpMissing = false; changed = true; }
				if (!changed)
				{
					return false;
				}

				Used.Add(template);
				Stash.Remove(item);
				return true;
			}

			public Patient With(params string[] templates)
			{
				Stash.AddRange(templates.Select(Meds));
				return this;
			}
		}

		public HealMeStepTests()
		{
			SettingsStore.UseForTests(new Settings());
			HealMeStep.ItemOf = MedsOrNull;
		}

		private static readonly HealMeStep Step = new HealMeStep();

		private static string Run(Patient patient) => HealMeStep.HealUp(patient.Health, () => patient.Stash, 0).GetAwaiter().GetResult();

		private static List<PlanLine> Plan(Patient patient) => Step.Plan(patient.Health, () => patient.Stash, template => template);

		// ---- the run

		[Fact]
		public void A_healthy_player_uses_nothing()
		{
			var patient = new Patient().With(Grizzly, Cat);

			Run(patient).Should().BeNull();

			patient.Used.Should().BeEmpty();
		}

		[Fact]
		public void A_heavy_bleed_is_stopped_by_the_dedicated_item_before_a_kit()
		{
			var patient = new Patient { Heavy = true }.With(Grizzly, Cat);

			Run(patient).Should().BeNull();

			patient.Used.Should().Equal(Cat);
			patient.Stash.Should().ContainSingle().Which.TemplateId.ToString().Should().Be(Grizzly);
		}

		[Fact]
		public void Everything_wrong_is_treated_with_one_Grizzly_when_it_comes_first()
		{
			var patient = new Patient { Heavy = true, Light = true, Fracture = true, HpMissing = true }.With(Cat, ArmyBandage, AluSplint, Grizzly);
			HealMeStep.Items.Setting.Order = new List<string> { Grizzly };

			Run(patient).Should().BeNull();

			patient.Used.Should().Equal(Grizzly);
		}

		[Fact]
		public void With_the_default_order_each_injury_gets_its_dedicated_item_and_the_kit_is_left_for_HP()
		{
			var patient = new Patient { Heavy = true, Light = true, Fracture = true, HpMissing = true }.With(Grizzly, Cat, ArmyBandage, AluSplint, Ai2);

			Run(patient).Should().BeNull();

			patient.Used.Should().Equal(Cat, ArmyBandage, AluSplint, Grizzly);
		}

		[Fact]
		public void The_most_urgent_injury_is_treated_first()
		{
			var patient = new Patient { Light = true, Heavy = true }.With(ArmyBandage, Cat);

			Run(patient);

			patient.Used.Should().Equal(Cat, ArmyBandage);
		}

		[Fact]
		public void An_item_that_is_switched_off_is_never_used()
		{
			var patient = new Patient { Heavy = true }.With(Cat, Esmarch);
			HealMeStep.Items.ToggleEntry(Cat);

			Run(patient).Should().BeNull();

			patient.Used.Should().Equal(Esmarch);
		}

		[Fact]
		public void A_locked_item_is_never_used()
		{
			var patient = new Patient { Heavy = true }.With(Cat, Esmarch);
			patient.Stash[0].PinLockState = EItemPinLockState.Locked;

			Run(patient);

			patient.Used.Should().Equal(Esmarch);
		}

		[Fact]
		public void An_item_the_game_refuses_is_skipped_and_the_next_one_is_tried()
		{
			var patient = new Patient { Heavy = true }.With(Cat, Esmarch);
			patient.Refused.Add(Cat);

			Run(patient).Should().BeNull();

			patient.Used.Should().Equal(Esmarch);
		}

		[Fact]
		public void When_nothing_in_the_stash_treats_an_injury_it_says_so_and_wastes_nothing()
		{
			var patient = new Patient { Fracture = true, Light = true }.With(ArmyBandage);

			var result = Run(patient);

			result.Should().Contain("still hurt");
			patient.Used.Should().Equal(ArmyBandage);
		}

		[Fact]
		public void Nothing_allowed_could_heal_says_so()
		{
			var patient = new Patient { Fracture = true }.With(ArmyBandage);

			Run(patient).Should().Contain("none of the medicine");
			patient.Used.Should().BeEmpty();
		}

		[Fact]
		public void An_empty_stash_uses_nothing_and_says_so()
		{
			var patient = new Patient { Heavy = true };

			Run(patient).Should().Contain("none of the medicine");
		}

		[Fact]
		public void HP_alone_is_healed_by_a_kit_and_not_by_a_dedicated_item()
		{
			var patient = new Patient { HpMissing = true }.With(Cat, Ai2);

			Run(patient).Should().BeNull();

			patient.Used.Should().Equal(Ai2);
		}

		[Fact]
		public void A_destroyed_part_is_left_alone_and_reported()
		{
			var patient = new Patient { Destroyed = EBodyPart.LeftLeg }.With(Grizzly, Cms);

			Run(patient).Should().Contain("none of the medicine");

			patient.Used.Should().BeEmpty();
		}

		[Fact]
		public void An_item_that_never_changes_anything_cannot_loop_forever()
		{
			var patient = new Patient { Heavy = true };
			patient.With(Cat);
			patient.Health.ApplyItem(Arg.Any<Item>(), EBodyPart.Common, Arg.Any<float?>()).Returns(true); // says yes, heals nothing

			Run(patient).Should().Contain("still hurt");
		}

		// ---- the plan

		[Fact]
		public void The_plan_of_a_healthy_player_is_the_health_and_no_afflictions()
		{
			var lines = Plan(new Patient().With(Grizzly));

			lines.Should().HaveCount(2);
			lines[0].IsProgress.Should().BeTrue();
			lines[1].Text.Should().Be("No afflictions");
		}

		[Fact]
		public void The_plan_lists_what_hurts_and_offers_to_heal_up()
		{
			var lines = Plan(new Patient { Heavy = true, Fracture = true }.With(Cat, AluSplint));

			lines[1].Text.Should().Contain("heavy bleeding").And.Contain("fracture");
			var heal = lines.Single(line => line.Run != null);
			heal.Text.Should().StartWith("Heal up with ");
			heal.Template.Should().Be(Cat);
			heal.Heal.Should().BeTrue();
		}

		[Fact]
		public void The_plan_warns_about_each_injury_nothing_in_the_stash_treats()
		{
			var lines = Plan(new Patient { Heavy = true, Fracture = true }.With(Cat));

			lines.Where(line => line.Problem).Select(line => line.Text).Should().Equal("Treat injuries: Fracture: no allowed medicine left in the stash");
			lines.Should().Contain(line => line.Run != null);
		}

		[Fact]
		public void The_plan_has_no_action_when_nothing_can_treat_anything()
		{
			var lines = Plan(new Patient { Heavy = true, Fracture = true }.With(ArmyBandage));

			lines.Count(line => line.Problem).Should().Be(2);
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void The_plan_for_a_destroyed_part_says_nothing_here_treats_it()
		{
			var lines = Plan(new Patient { Destroyed = EBodyPart.Head }.With(Grizzly));

			lines.Single(line => line.Problem).Text.Should().Contain("destroyed part");
			lines.Should().NotContain(line => line.Run != null);
		}

		[Fact]
		public void The_plan_names_the_first_three_items_it_would_use()
		{
			var lines = Plan(new Patient { Heavy = true, Light = true }.With(Cat, Esmarch, ArmyBandage, AsepticBandage));

			lines.Single(line => line.Run != null).Text.Should().Be("Heal up with " + string.Join(", ", HealMeStep.Items.OrderedEntries().Select(entry => entry.Template).Where(new[] { Cat, Esmarch, ArmyBandage, AsepticBandage }.Contains).Take(3)));
		}

		[Fact]
		public void The_plan_without_a_health_controller_says_so()
		{
			Step.Plan(null, () => new List<Item>(), template => template).Single().Text.Should().Contain("isn't available");
		}

		[Fact]
		public void The_action_of_the_plan_runs_the_treatment()
		{
			var patient = new Patient { Heavy = true }.With(Cat);

			Plan(patient).Single(line => line.Run != null).Run().GetAwaiter().GetResult().Should().BeNull();

			patient.Used.Should().Equal(Cat);
		}
	}
}
