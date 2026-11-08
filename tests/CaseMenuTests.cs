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
	}
}
