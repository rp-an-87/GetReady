using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The quick-slot keys: after getting ready the best carried item of a group goes on the key the group is set to.
	public class HotkeysTests
	{
		private readonly Body _body = new Body();
		private readonly CompoundItem _vest;

		public HotkeysTests()
		{
			Reset();
			_vest = _body.Wear(EquipmentSlot.TacticalVest, Gear.NewContainer(Body.Vest));
			Group("health").Setting.Target = 300;
			Group("bandage").Setting.Target = 2;
			Group("heavybleed").Setting.Target = 0;
			Group("propital").Setting.Target = 0;
		}

		private static GroupDef Group(string id) => Steps.All.OfType<SuppliesStep>().SelectMany(step => step.Groups).First(def => def.Id == id);

		private Item OnVest(string template) => RealItems.Any(template).In(_vest);

		private Task<int> Apply() => Hotkeys.Apply(_body.Controller);

		// ---- the choices

		[Fact]
		public void The_choices_are_no_key_then_4_to_9_then_0_then_G()
		{
			Hotkeys.Choices.Should().Equal("", "4", "5", "6", "7", "8", "9", "0", "G");
		}

		[Fact]
		public void Next_goes_through_the_choices_and_wraps_round()
		{
			Hotkeys.Next("").Should().Be("4");
			Hotkeys.Next("9").Should().Be("0");
			Hotkeys.Next("0").Should().Be("G");
			Hotkeys.Next("G").Should().Be("");
		}

		[Fact]
		public void An_unknown_choice_goes_to_no_key()
		{
			Hotkeys.Next("zzz").Should().Be("");
		}

		[Fact]
		public void No_key_has_a_readable_label()
		{
			Hotkeys.Label("").Should().Be("No key");
			Hotkeys.Label("6").Should().Be("6");
		}

		// ---- binding

		[Fact]
		public async Task The_best_carried_item_of_a_group_goes_on_its_key()
		{
			var kit = OnVest(RealItems.Afak);

			(await Apply()).Should().Be(1);

			_body.Binds.Should().ContainSingle().Which.Should().Be(new System.Collections.Generic.KeyValuePair<EBoundItem, Item>(EBoundItem.Item4, kit));
		}

		[Fact]
		public async Task A_group_with_its_own_key_setting_uses_it()
		{
			Group("health").Setting.Hotkey = "G";
			OnVest(RealItems.Afak);

			await Apply();

			_body.Binds.Single().Key.Should().Be(EBoundItem.ItemG);
		}

		[Theory]
		[InlineData("4", EBoundItem.Item4)]
		[InlineData("5", EBoundItem.Item5)]
		[InlineData("6", EBoundItem.Item6)]
		[InlineData("7", EBoundItem.Item7)]
		[InlineData("8", EBoundItem.Item8)]
		[InlineData("9", EBoundItem.Item9)]
		[InlineData("0", EBoundItem.Item10)]
		[InlineData("G", EBoundItem.ItemG)]
		public async Task Each_key_choice_is_its_own_slot(string choice, EBoundItem expected)
		{
			Group("health").Setting.Hotkey = choice;
			OnVest(RealItems.Afak);

			await Apply();

			_body.Binds.Single().Key.Should().Be(expected);
		}

		[Fact]
		public async Task A_group_set_to_no_key_binds_nothing()
		{
			Group("health").Setting.Hotkey = "";
			OnVest(RealItems.Afak);

			(await Apply()).Should().Be(0);
		}

		[Fact]
		public async Task A_group_with_nothing_to_bring_binds_nothing()
		{
			Group("health").Setting.Target = 0;
			OnVest(RealItems.Afak);

			(await Apply()).Should().Be(0);
		}

		[Fact]
		public async Task A_group_whose_step_is_off_binds_nothing()
		{
			SettingsStore.Current.Supplies.HealingEnabled = false;
			OnVest(RealItems.Afak);

			(await Apply()).Should().Be(0);
		}

		[Fact]
		public async Task Nothing_carried_binds_nothing()
		{
			(await Apply()).Should().Be(0);
		}

		[Fact]
		public async Task An_item_in_the_stash_is_not_bound()
		{
			_body.InStash(RealItems.Any(RealItems.Afak));

			(await Apply()).Should().Be(0);
		}

		[Fact]
		public async Task An_item_where_the_game_does_not_allow_a_key_is_not_bound()
		{
			var backpack = _body.Wear(EquipmentSlot.Backpack, Gear.NewContainer(Gear.ScavBackpack));
			RealItems.Any(RealItems.Afak).In(backpack);

			(await Apply()).Should().Be(0);
		}

		[Fact]
		public async Task An_item_that_is_not_in_the_group_is_not_bound()
		{
			OnVest(RealItems.ArmyBandage); // bandages are group "bandage", key 6, not the healing kits

			await Apply();

			_body.Binds.Should().ContainSingle().Which.Key.Should().Be(EBoundItem.Item6);
		}

		[Fact]
		public async Task The_item_the_group_puts_first_wins()
		{
			OnVest(RealItems.Afak);
			var salewa = OnVest(RealItems.Salewa);
			Group("health").Setting.Order = new System.Collections.Generic.List<string> { RealItems.Salewa, RealItems.Afak };

			await Apply();

			_body.Binds.Single(bind => bind.Key == EBoundItem.Item4).Value.Should().BeSameAs(salewa);
		}

		[Fact]
		public async Task Of_two_of_the_same_the_fuller_one_is_bound()
		{
			var used = OnVest(RealItems.Afak);
			used.GetItemComponent<MedKitComponent>().HpResource = 100;
			var full = OnVest(RealItems.Afak);

			await Apply();

			_body.Binds.Single(bind => bind.Key == EBoundItem.Item4).Value.Should().BeSameAs(full);
		}

		[Fact]
		public async Task A_key_that_already_holds_an_item_of_the_group_you_still_carry_is_left_alone()
		{
			var held = OnVest(RealItems.Salewa);
			var better = OnVest(RealItems.Afak);
			_body.Inventory.FastAccess.BoundItems[EBoundItem.Item4] = held;
			Group("health").Setting.Order = new System.Collections.Generic.List<string> { RealItems.Afak, RealItems.Salewa };

			(await Apply()).Should().Be(0);
			_body.Inventory.FastAccess.BoundItems[EBoundItem.Item4].Should().BeSameAs(held);
			better.Should().NotBeNull();
		}

		[Fact]
		public async Task A_key_holding_something_no_longer_carried_is_bound_again()
		{
			var gone = RealItems.Any(RealItems.Salewa).In(_body.StashBox);
			var kit = OnVest(RealItems.Afak);
			_body.Inventory.FastAccess.BoundItems[EBoundItem.Item4] = gone;

			(await Apply()).Should().Be(1);

			_body.Binds.Single().Value.Should().BeSameAs(kit);
		}

		[Fact]
		public async Task A_bind_the_game_refuses_is_not_counted_and_the_others_still_go()
		{
			OnVest(RealItems.Afak);
			var bandage = OnVest(RealItems.ArmyBandage);
			_body.Refuse = move => move.Item == bandage ? "no" : null;

			(await Apply()).Should().Be(1);
			_body.Binds.Should().ContainSingle().Which.Key.Should().Be(EBoundItem.Item4);
		}

		// ---- the second key

		[Fact]
		public async Task Without_a_second_key_only_the_best_item_is_bound()
		{
			OnVest(RealItems.Afak);
			OnVest(RealItems.Salewa);

			(await Apply()).Should().Be(1);
			_body.Binds.Should().ContainSingle().Which.Key.Should().Be(EBoundItem.Item4);
		}

		[Fact]
		public async Task The_best_goes_on_the_first_key_and_the_next_best_on_the_second()
		{
			Group("health").Setting.Hotkey2 = "6";
			Group("health").Setting.Order = new System.Collections.Generic.List<string> { RealItems.Afak, RealItems.Salewa };
			var afak = OnVest(RealItems.Afak);
			var salewa = OnVest(RealItems.Salewa);

			(await Apply()).Should().Be(2);

			_body.Binds.Single(bind => bind.Key == EBoundItem.Item4).Value.Should().BeSameAs(afak);
			_body.Binds.Single(bind => bind.Key == EBoundItem.Item6).Value.Should().BeSameAs(salewa);
		}

		[Fact]
		public async Task Two_of_the_same_kit_take_a_key_each()
		{
			Group("health").Setting.Hotkey2 = "6";
			var one = OnVest(RealItems.Afak);
			var two = OnVest(RealItems.Afak);

			(await Apply()).Should().Be(2);

			_body.Binds.Select(bind => bind.Value).Should().BeEquivalentTo(new[] { one, two });
			_body.Binds.Select(bind => bind.Key).Should().BeEquivalentTo(new[] { EBoundItem.Item4, EBoundItem.Item6 });
		}

		[Fact]
		public async Task A_second_run_leaves_both_keys_as_they_are()
		{
			Group("health").Setting.Hotkey2 = "6";
			OnVest(RealItems.Afak);
			OnVest(RealItems.Afak);
			await Apply();

			(await Apply()).Should().Be(0);
			_body.Binds.Should().HaveCount(2);
		}

		[Fact]
		public async Task With_one_item_only_the_second_key_stays_empty()
		{
			Group("health").Setting.Hotkey2 = "6";
			OnVest(RealItems.Afak);

			(await Apply()).Should().Be(1);
			_body.Binds.Should().ContainSingle().Which.Key.Should().Be(EBoundItem.Item4);
		}

		[Fact]
		public async Task A_second_key_that_is_the_same_as_the_first_is_ignored()
		{
			Group("health").Setting.Hotkey2 = "4";
			OnVest(RealItems.Afak);
			OnVest(RealItems.Afak);

			(await Apply()).Should().Be(1);
			_body.Binds.Should().ContainSingle().Which.Key.Should().Be(EBoundItem.Item4);
		}

		[Fact]
		public async Task A_second_key_holding_something_no_longer_carried_is_bound_again()
		{
			Group("health").Setting.Hotkey2 = "6";
			var gone = RealItems.Any(RealItems.Salewa).In(_body.StashBox);
			var first = OnVest(RealItems.Afak);
			var second = OnVest(RealItems.Afak);
			_body.Inventory.FastAccess.BoundItems[EBoundItem.Item4] = first;
			_body.Inventory.FastAccess.BoundItems[EBoundItem.Item6] = gone;

			(await Apply()).Should().Be(1);

			_body.Binds.Should().ContainSingle().Which.Should().Be(new System.Collections.Generic.KeyValuePair<EBoundItem, Item>(EBoundItem.Item6, second));
		}

		[Fact]
		public async Task A_group_with_no_first_key_has_no_second_one_either()
		{
			Group("health").Setting.Hotkey = "";
			Group("health").Setting.Hotkey2 = "6";
			OnVest(RealItems.Afak);
			OnVest(RealItems.Afak);

			// Only the bandage group (not carried) has a key left
			(await Apply()).Should().Be(1);
			_body.Binds.Should().ContainSingle().Which.Key.Should().Be(EBoundItem.Item6);
		}

		[Fact]
		public void A_key_another_group_has_is_a_clash_and_a_free_one_is_not()
		{
			// Bandages are on 6 by default
			Hotkeys.Clash(Group("health"), "6").Should().BeTrue();
			Hotkeys.Clash(Group("health"), "9").Should().BeFalse();
			Hotkeys.Clash(Group("health"), "").Should().BeFalse();
		}

		[Fact]
		public void The_second_key_of_another_group_counts_for_a_clash_too()
		{
			Group("bandage").Setting.Hotkey2 = "9";

			Hotkeys.Clash(Group("health"), "9").Should().BeTrue();
		}

		[Fact]
		public async Task Several_groups_bind_several_keys()
		{
			OnVest(RealItems.Afak);
			OnVest(RealItems.ArmyBandage);

			(await Apply()).Should().Be(2);
		}
	}
}
