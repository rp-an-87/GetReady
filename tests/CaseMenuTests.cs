using System;
using System.Linq;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The right-click menu of a case (CaseMenu.cs): which entries are on, when Update is greyed out, and what each entry does to the case's record.
	// The drawing and the game's own menu plumbing are not covered; the decisions and the effects on the record are.
	public class CaseMenuTests
	{
		private readonly Body _body = new Body();
		private readonly CompoundItem _case;

		public CaseMenuTests()
		{
			Reset();
			Game.IsInsideTemplatedCase = Game.InTemplatedCase;
			var backpack = _body.Wear(EquipmentSlot.Backpack, Gear.NewContainer(Gear.Stash));
			_case = Gear.NewContainer(Gear.AlphaCase).In(backpack);
		}

		private CaseActions Actions() => new CaseActions(null, _case);

		private MapActions Maps() => new MapActions(null, _case);

		private string Id => _case.Id.ToString();

		[Fact]
		public void An_untracked_case_offers_track_and_nothing_to_update_or_stop()
		{
			var actions = Actions();

			actions.IsActive(ECaseAction.Track).Should().BeTrue();
			actions.IsActive(ECaseAction.Update).Should().BeFalse();
			actions.IsActive(ECaseAction.StopTracking).Should().BeFalse();
			actions.IsActive(ECaseAction.Maps).Should().BeTrue();
		}

		[Fact]
		public void A_tracked_case_offers_update_and_stop_but_not_track()
		{
			CasesStep.Record(_case);
			var actions = Actions();

			actions.IsActive(ECaseAction.Track).Should().BeFalse();
			actions.IsActive(ECaseAction.Update).Should().BeTrue();
			actions.IsActive(ECaseAction.StopTracking).Should().BeTrue();
		}

		[Fact]
		public void A_case_with_only_maps_set_is_not_tracked()
		{
			Maps().ExecuteInteractionInternal(EMapAction.Customs);

			CasesStep.EntryOf(Id).Recorded.Should().BeFalse();
			Actions().IsActive(ECaseAction.Track).Should().BeTrue();
		}

		[Fact]
		public void Track_records_what_the_case_holds()
		{
			RealItems.Any(RealItems.Salewa, 1).In(_case);

			Actions().ExecuteInteractionInternal(ECaseAction.Track);

			var entry = CasesStep.EntryOf(Id);
			entry.Recorded.Should().BeTrue();
			entry.Items.Should().HaveCount(1);
		}

		[Fact]
		public void Update_is_greyed_out_while_nothing_changed()
		{
			RealItems.Any(RealItems.Salewa, 1).In(_case);
			CasesStep.Record(_case);

			Actions().IsInteractive(ECaseAction.Update).Succeed.Should().BeFalse();
		}

		[Fact]
		public void Update_is_available_once_the_case_changed()
		{
			CasesStep.Record(_case);
			RealItems.Any(RealItems.Salewa, 1).In(_case);

			Actions().IsInteractive(ECaseAction.Update).Succeed.Should().BeTrue();
		}

		[Fact]
		public void Update_records_the_new_contents()
		{
			CasesStep.Record(_case);
			RealItems.Any(RealItems.Salewa, 1).In(_case);

			Actions().ExecuteInteractionInternal(ECaseAction.Update);

			CasesStep.EntryOf(Id).Items.Should().HaveCount(1);
		}

		[Fact]
		public void The_other_entries_are_always_interactive()
		{
			var actions = Actions();

			actions.IsInteractive(ECaseAction.Track).Succeed.Should().BeTrue();
			actions.IsInteractive(ECaseAction.StopTracking).Succeed.Should().BeTrue();
			actions.IsInteractive(ECaseAction.Maps).Succeed.Should().BeTrue();
		}

		[Fact]
		public void Stop_tracking_forgets_the_case()
		{
			CasesStep.Record(_case);

			Actions().ExecuteInteractionInternal(ECaseAction.StopTracking);

			CasesStep.EntryOf(Id).Should().BeNull();
		}

		[Fact]
		public void Stop_tracking_keeps_the_maps_of_the_case()
		{
			Maps().ExecuteInteractionInternal(EMapAction.Customs);
			CasesStep.Record(_case);

			Actions().ExecuteInteractionInternal(ECaseAction.StopTracking);

			CasesStep.EntryOf(Id).Should().NotBeNull();
			CasesStep.EntryOf(Id).Recorded.Should().BeFalse();
			CasesStep.EntryOf(Id).Maps.Should().Equal(MapTags.Maps[(int)EMapAction.Customs].Name);
		}

		[Fact]
		public void Every_map_of_the_menu_is_a_map_of_the_plugin_and_in_the_same_order()
		{
			var menu = Enum.GetValues(typeof(EMapAction)).Cast<EMapAction>().ToList();
			var maps = Maps();

			menu.Should().HaveCount(MapTags.Maps.Length);
			menu.All(map => maps.IsActive(map)).Should().BeTrue();
			maps.CustomNames.Values.Should().Equal(MapTags.Maps.Select(map => map.Name));
		}

		[Fact]
		public void A_map_switches_on_and_then_off_again()
		{
			var maps = Maps();
			var name = MapTags.Maps[(int)EMapAction.Woods].Name;

			maps.ExecuteInteractionInternal(EMapAction.Woods);
			CasesStep.EntryOf(Id).Maps.Should().Equal(name);
			maps.CustomNames[EMapAction.Woods].Should().Contain("<color=").And.Contain(name);
			maps.CustomNames[EMapAction.Customs].Should().NotContain("<color=");

			maps.ExecuteInteractionInternal(EMapAction.Woods);
			CasesStep.EntryOf(Id).Should().BeNull("a case with nothing set for it is not kept");
		}

		[Fact]
		public void The_menu_keeps_one_names_dictionary_and_a_click_updates_it_in_place()
		{
			// The game reads the names once when the menu opens and keeps that object; the menu stays open after a click, so it must change
			var maps = Maps();
			var kept = maps.CustomNames;

			maps.ExecuteInteractionInternal(EMapAction.Woods);

			maps.CustomNames.Should().BeSameAs(kept);
			kept[EMapAction.Woods].Should().Contain("<color=");

			maps.ExecuteInteractionInternal(EMapAction.Woods);
			kept[EMapAction.Woods].Should().NotContain("<color=");
		}

		[Fact]
		public void Maps_add_up_and_keep_a_tracked_case_when_the_last_one_goes()
		{
			CasesStep.Record(_case);
			var maps = Maps();

			maps.ExecuteInteractionInternal(EMapAction.Customs);
			maps.ExecuteInteractionInternal(EMapAction.Labs);
			CasesStep.EntryOf(Id).Maps.Should().HaveCount(2);

			maps.ExecuteInteractionInternal(EMapAction.Customs);
			maps.ExecuteInteractionInternal(EMapAction.Labs);
			CasesStep.EntryOf(Id).Should().NotBeNull("it is still tracked");
			CasesStep.EntryOf(Id).Maps.Should().BeEmpty();
		}

		[Fact]
		public void The_menu_only_belongs_to_a_case_you_carry()
		{
			CasesStep.FindCases(_body.Inventory).Should().Contain(_case);
			var inStash = Gear.NewContainer(Gear.AlphaCase).In(_body.StashBox);

			CasesStep.FindCases(_body.Inventory).Should().NotContain(inStash);
		}

		[Fact]
		public void The_buttons_are_coloured_by_their_key_only()
		{
			// No button: nothing to colour, and no failure
			Action nothing = () => CaseMenu.Colour(null, CaseMenu.MenuKey);
			nothing.Should().NotThrow();
		}

		// ---- the entries on armor, weapons and ammo, and what hides them

		private const string Armor = "5c0e5bab86f77461f55ed1f3";

		private static CaseActions Plain(EFT.InventoryLogic.Item item) => new CaseActions(null, item, false);

		private static System.Collections.Generic.IEnumerable<ECaseAction> ActiveOf(CaseActions actions) =>
			System.Enum.GetValues(typeof(ECaseAction)).Cast<ECaseAction>().Where(action => actions.IsActive(action)).ToList();

		[Fact]
		public void Armor_offers_leaving_it_out_of_the_repairs_only_while_repairing_armor_is_on()
		{
			var armor = RealItems.Any(Armor);
			SettingsStore.Current.Repair.Enabled = true;
			SettingsStore.Current.Repair.Armor = true;
			ActiveOf(Plain(armor)).Should().Equal(ECaseAction.Repair);

			SettingsStore.Current.Repair.Armor = false;
			ActiveOf(Plain(armor)).Should().BeEmpty();

			SettingsStore.Current.Repair.Armor = true;
			SettingsStore.Current.Repair.Enabled = false;
			ActiveOf(Plain(armor)).Should().BeEmpty("the Repair step is off");
		}

		[Fact]
		public void A_weapon_offers_leaving_it_out_of_the_repairs_only_while_repairing_weapons_is_on()
		{
			var weapon = Gear.NewWeapon();
			SettingsStore.Current.Repair.Enabled = true;
			SettingsStore.Current.Repair.Armor = false;
			SettingsStore.Current.Repair.Weapons = true;
			ActiveOf(Plain(weapon)).Should().Equal(ECaseAction.Repair);

			SettingsStore.Current.Repair.Weapons = false;
			ActiveOf(Plain(weapon)).Should().BeEmpty();
		}

		[Fact]
		public void An_item_that_is_not_repaired_gets_no_entry()
		{
			SettingsStore.Current.Repair.Enabled = true;
			SettingsStore.Current.Repair.Armor = true;
			SettingsStore.Current.Repair.Weapons = true;

			CaseMenu.Offers(RealItems.Any(RealItems.ArmyBandage), false).Should().BeFalse();
		}

		[Fact]
		public void The_repair_entry_leaves_the_piece_out_and_puts_it_back_with_its_wording()
		{
			var armor = RealItems.Any(Armor);
			SettingsStore.Current.Repair.Enabled = true;
			SettingsStore.Current.Repair.Armor = true;
			var actions = Plain(armor);
			actions.CustomNames[ECaseAction.Repair].Should().Be("Exclude from repairs");

			actions.ExecuteInteractionInternal(ECaseAction.Repair);
			RepairStep.IsKept(armor).Should().BeTrue();
			Plain(armor).CustomNames[ECaseAction.Repair].Should().Be("Include in repairs");

			actions.ExecuteInteractionInternal(ECaseAction.Repair);
			RepairStep.IsKept(armor).Should().BeFalse();
		}

		[Fact]
		public void Ammo_in_a_grid_offers_leaving_it_out_of_the_filling_only_while_the_filling_is_on()
		{
			var ammo = Gear.NewAmmo(Gear.M855, 30).In(_body.StashBox);
			SettingsStore.Current.Ammo.Enabled = true;
			SettingsStore.Current.Ammo.TopUpLoose = true;
			ActiveOf(Plain(ammo)).Should().Equal(ECaseAction.TopUp);

			SettingsStore.Current.Ammo.TopUpLoose = false;
			ActiveOf(Plain(ammo)).Should().BeEmpty();

			SettingsStore.Current.Ammo.TopUpLoose = true;
			SettingsStore.Current.Ammo.Enabled = false;
			ActiveOf(Plain(ammo)).Should().BeEmpty("the Ammo step is off");
		}

		[Fact]
		public void The_ammo_entry_leaves_its_kind_out_and_puts_it_back_with_its_wording()
		{
			var ammo = Gear.NewAmmo(Gear.M855, 30).In(_body.StashBox);
			SettingsStore.Current.Ammo.Enabled = true;
			var actions = Plain(ammo);
			actions.CustomNames[ECaseAction.TopUp].Should().Be("Leave out of top-up");

			actions.ExecuteInteractionInternal(ECaseAction.TopUp);
			AmmoStep.IsTopUpExcluded(Gear.M855).Should().BeTrue();
			Plain(ammo).CustomNames[ECaseAction.TopUp].Should().Be("Include in top-up");

			actions.ExecuteInteractionInternal(ECaseAction.TopUp);
			AmmoStep.IsTopUpExcluded(Gear.M855).Should().BeFalse();
		}

		[Fact]
		public void The_case_entries_are_hidden_while_the_Cases_step_is_off()
		{
			SettingsStore.Current.Cases.Enabled = false;

			ActiveOf(Actions()).Should().BeEmpty();
			CaseMenu.Offers(_case, true).Should().BeFalse();
		}

		[Fact]
		public void A_case_is_offered_its_entries_while_the_Cases_step_is_on()
		{
			ActiveOf(Actions()).Should().Contain(new[] { ECaseAction.Track, ECaseAction.Maps, ECaseAction.Settings });
			CaseMenu.Offers(_case, true).Should().BeTrue();
		}

		// ---- the case settings submenu

		private SettingActions Settings() => new SettingActions(null, _case);

		[Fact]
		public void The_take_out_and_exact_switches_are_only_there_for_a_tracked_case()
		{
			var settings = Settings();
			settings.IsActive(ECaseSetting.TakeOutRest).Should().BeFalse();
			settings.IsActive(ECaseSetting.Exact).Should().BeFalse();
			settings.IsActive(ECaseSetting.SkipCash).Should().BeTrue();

			CasesStep.Record(_case);
			settings.IsActive(ECaseSetting.TakeOutRest).Should().BeTrue();
			settings.IsActive(ECaseSetting.Exact).Should().BeTrue();
		}

		[Fact]
		public void The_skip_cash_switch_is_hidden_while_the_Cash_step_is_off()
		{
			SettingsStore.Current.Supplies.CashEnabled = false;

			Settings().IsActive(ECaseSetting.SkipCash).Should().BeFalse();
		}

		[Fact]
		public void The_skip_cash_switch_works_on_an_untracked_case_and_the_record_goes_when_it_is_switched_off()
		{
			var settings = Settings();

			settings.ExecuteInteractionInternal(ECaseSetting.SkipCash);
			CasesStep.EntryOf(Id).StopsCash.Should().BeTrue();

			settings.ExecuteInteractionInternal(ECaseSetting.SkipCash);
			CasesStep.EntryOf(Id).Should().BeNull("a case with nothing set for it is not kept");
		}

		[Fact]
		public void The_take_out_and_exact_switches_flip_the_record()
		{
			CasesStep.Record(_case);
			var settings = Settings();
			bool takeOut = CasesStep.EntryOf(Id).TakeOutRest;
			bool exact = CasesStep.EntryOf(Id).Exact;

			settings.ExecuteInteractionInternal(ECaseSetting.TakeOutRest);
			settings.ExecuteInteractionInternal(ECaseSetting.Exact);

			CasesStep.EntryOf(Id).TakeOutRest.Should().Be(!takeOut);
			CasesStep.EntryOf(Id).Exact.Should().Be(!exact);
		}

		[Fact]
		public void The_settings_menu_keeps_one_names_dictionary_and_a_click_updates_it_in_place()
		{
			var settings = Settings();
			var kept = settings.CustomNames;
			kept[ECaseSetting.SkipCash].Should().NotContain("<color=");

			settings.ExecuteInteractionInternal(ECaseSetting.SkipCash);

			settings.CustomNames.Should().BeSameAs(kept);
			kept[ECaseSetting.SkipCash].Should().Contain("<color=");
		}
	}
}
