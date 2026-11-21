using System.Linq;
using EFT.InventoryLogic;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// The money of a tracked case (a case whose template holds money) is not for paying: which stacks CashGuard picks to lock around a payment
	// and how many roubles the steps may count.
	public class CashGuardTests
	{
		private readonly Body _body = new Body();

		public CashGuardTests()
		{
			Reset();
		}

		private CompoundItem CaseInStash(int roubles, bool record)
		{
			var container = _body.InStash(Gear.NewContainer(Gear.AlphaCase));
			if (roubles > 0)
			{
				RealItems.Any(RealItems.Roubles, roubles).In(container);
			}

			if (record)
			{
				CasesStep.Record(container);
			}

			return container;
		}

		[Fact]
		public void The_money_of_a_recorded_case_in_the_stash_is_guarded()
		{
			var container = CaseInStash(30000, true);

			var stacks = CashGuard.Stacks(_body.Inventory);

			stacks.Should().ContainSingle().Which.StackObjectsCount.Should().Be(30000);
			stacks[0].Parent.Container.ParentItem.Should().BeSameAs(container);
			CashGuard.Roubles(_body.Inventory).Should().Be(30000);
		}

		[Fact]
		public void A_case_that_is_not_tracked_or_has_no_money_in_its_template_is_not_guarded()
		{
			CaseInStash(30000, false); // money in it, but no template
			var empty = CaseInStash(0, true); // a template without money
			RealItems.Any(RealItems.Roubles, 5000).In(empty); // money put in by hand later is not in the template

			CashGuard.Stacks(_body.Inventory).Should().BeEmpty();
			CashGuard.Roubles(_body.Inventory).Should().Be(0);
		}

		[Fact]
		public void Loose_money_is_not_guarded()
		{
			CaseInStash(30000, true);
			_body.InStash(RealItems.Any(RealItems.Roubles, 8000));

			CashGuard.Stacks(_body.Inventory).Should().ContainSingle();
			CashGuard.Roubles(_body.Inventory).Should().Be(30000);
		}

		[Fact]
		public void The_steps_count_only_the_roubles_outside_those_cases()
		{
			CaseInStash(30000, true);
			_body.InStash(RealItems.Any(RealItems.Roubles, 8000));

			RepairWithKit.Roubles(_body.Controller).Should().Be(8000);
		}
	}
}
