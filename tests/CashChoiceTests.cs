using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using static GetReady.Tests.Gear;
using static GetReady.Tests.SupplyFixture;

namespace GetReady.Tests
{
	// What a case accepts, as the Setup page shows it: RUB, EUR and USD on or off; all on is not saved, all off is "no cash".
	public class CashChoiceTests
	{
		private readonly List<CashExclusion> _list = new List<CashExclusion>();
		private readonly CashExclusion _wallet = new CashExclusion { CaseId = "wallet", CaseTemplate = "tpl", CaseName = "Wallet", Tag = "main", TagColor = 3 };

		public CashChoiceTests() => Reset();

		[Fact]
		public void A_case_with_nothing_saved_accepts_all_three_and_does_not_stop_cash()
		{
			CashChoice.IsDefault(_list, "wallet").Should().BeTrue();
			CashChoice.Currencies.Should().OnlyContain(currency => CashChoice.Accepts(_list, "wallet", currency));
			CashChoice.NoCash(_list, "wallet").Should().BeFalse();
		}

		[Fact]
		public void Turning_a_currency_off_keeps_that_money_out_of_the_case()
		{
			CashChoice.Toggle(_list, _wallet, "EUR");

			CashChoice.Accepts(_list, "wallet", "EUR").Should().BeFalse();
			CashChoice.Accepts(_list, "wallet", "RUB").Should().BeTrue();
			CashChoice.Accepts(_list, "wallet", "USD").Should().BeTrue();
			_list.Should().ContainSingle(entry => entry.Currency == "EUR" && entry.CaseId == "wallet");
		}

		[Fact]
		public void Turning_it_back_on_leaves_nothing_saved()
		{
			CashChoice.Toggle(_list, _wallet, "EUR");
			CashChoice.Toggle(_list, _wallet, "EUR");

			_list.Should().BeEmpty();
			CashChoice.IsDefault(_list, "wallet").Should().BeTrue();
		}

		[Fact]
		public void All_three_currencies_off_is_not_no_cash()
		{
			foreach (var currency in CashChoice.Currencies)
			{
				CashChoice.Toggle(_list, _wallet, currency);
			}

			CashChoice.NoCash(_list, "wallet").Should().BeFalse();
			_list.Select(entry => entry.Currency).Should().BeEquivalentTo("RUB", "EUR", "USD");
		}

		[Fact]
		public void No_cash_is_its_own_switch_that_leaves_the_currencies_as_they_are()
		{
			CashChoice.Toggle(_list, _wallet, "EUR");

			CashChoice.ToggleNoCash(_list, _wallet);

			CashChoice.NoCash(_list, "wallet").Should().BeTrue();
			CashChoice.Accepts(_list, "wallet", "EUR").Should().BeFalse();
			CashChoice.Accepts(_list, "wallet", "RUB").Should().BeTrue();
			_list.Select(entry => entry.Currency).Should().BeEquivalentTo("EUR", CashExclusion.All);
		}

		[Fact]
		public void Turning_no_cash_off_again_leaves_nothing_saved()
		{
			CashChoice.ToggleNoCash(_list, _wallet);
			CashChoice.ToggleNoCash(_list, _wallet);

			_list.Should().BeEmpty();
		}

		[Fact]
		public void The_details_of_the_case_are_kept_in_what_is_saved()
		{
			CashChoice.Toggle(_list, _wallet, "USD");

			var entry = _list.Single();
			entry.CaseTemplate.Should().Be("tpl");
			entry.CaseName.Should().Be("Wallet");
			entry.Tag.Should().Be("main");
			entry.TagColor.Should().Be(3);
		}

		[Fact]
		public void Another_case_is_left_alone()
		{
			var other = new CashExclusion { CaseId = "case", CaseName = "SICC" };
			CashChoice.Toggle(_list, other, "RUB");

			CashChoice.Toggle(_list, _wallet, "EUR");
			CashChoice.Toggle(_list, _wallet, "EUR");
			CashChoice.ToggleNoCash(_list, _wallet);
			CashChoice.ToggleNoCash(_list, _wallet);

			_list.Should().ContainSingle(entry => entry.CaseId == "case" && entry.Currency == "RUB");
			CashChoice.Accepts(_list, "case", "RUB").Should().BeFalse();
		}

		[Fact]
		public void A_no_cash_entry_saved_by_an_older_build_is_no_cash()
		{
			_list.Add(new CashExclusion { CaseId = "wallet", Currency = CashExclusion.All });

			CashChoice.NoCash(_list, "wallet").Should().BeTrue();
		}

		[Fact]
		public void A_saved_case_is_not_anywhere_so_its_name_loses_the_place()
		{
			CashChoice.BareName(new CashExclusion { CaseName = "Wallet in Beta 2" }).Should().Be("Wallet");
			CashChoice.BareName(new CashExclusion { CaseName = "Wallet" }).Should().Be("Wallet");
			CashChoice.BareName(new CashExclusion { CaseName = "" }).Should().BeEmpty();
		}

		[Fact]
		public void A_saved_case_with_its_template_is_named_by_the_template()
		{
			Names[M4A1] = "M4A1"; // the names are filled in by the first test that touches Gear: do not depend on the order
			CashChoice.BareName(new CashExclusion { CaseTemplate = M4A1, CaseName = "Whatever in Somewhere" }).Should().Be("M4A1");
		}
	}
}
