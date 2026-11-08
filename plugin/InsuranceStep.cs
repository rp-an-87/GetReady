using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI.Insurance;

namespace GetReady
{
	// Insures what you wear (armor, weapons, helmets, plates and mods: whatever the game lets you insure) plus the magazines in your
	// containers, at the trader chosen in Setup, the way the game's own insurance screen does it: the same items as its "insure all"
	// checkbox, the same call to the trader, the same bookkeeping afterwards. No prices are asked for here (the game keeps whatever it is
	// first told for the whole session, its own screen included): the server charges the price and refuses when the roubles are short.
	// The plan shows the cost only when the game already knows every price (from its own screen, say).
	internal class InsuranceStep : IStep
	{
		public string Name => "Insurance";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.Insurance.Enabled; set => SettingsStore.Current.Insurance.Enabled = value; }

		// Where the game's insurance and its traders come from (replaced by the tests)
		internal static Func<InsuranceCompany> CompanyOf = () =>
			SPT.Reflection.Utils.ClientAppUtils.GetMainApp()?.GetClientBackEndSession()?.InsuranceCompany;

		internal static Func<List<Trader>> InsurersOf = () =>
			SPT.Reflection.Utils.ClientAppUtils.GetMainApp()?.GetClientBackEndSession()?.Insurers?.ToList() ?? new List<Trader>();

		// The call the insurance screen makes to the trader (replaced by the tests)
		internal static Action<Trader, string[], Comfort.Common.Callback> InsureCall = (trader, ids, callback) => trader.InsureItems(ids, callback);

		private static InsuranceCompany Company() => CompanyOf();

		// The traders that insure (the game's own list)
		internal static List<Trader> Insurers() => InsurersOf();

		internal static Trader Chosen() => Insurers().FirstOrDefault(trader => trader.Id == SettingsStore.Current.Insurance.TraderId);

		// What you wear that can be insured, with the parts on it (mods, plates) and the magazines lying in your containers (vest, backpack,
		// pockets, cases inside them), but no other loose item. The game's own rule decides what can be insured at all (meds, ammo, knives,
		// locked slots and the like cannot).
		internal static List<InsuredItem> Insurable(Inventory inventory, InsuranceCompany company)
		{
			var found = new List<Item>();
			foreach (var slot in inventory.Equipment.Slots)
			{
				// The dog tag is not offered by the game's own "insure all" (the server would sell it a policy all the same)
				if (slot.ID == nameof(EquipmentSlot.Dogtag))
				{
					continue;
				}

				Collect(slot.ContainedItem, found);
			}

			return found.Select(item => InsuredItem.FindOrCreate(item)).Where(item => company.ItemTypeAvailableForInsurance(item)).ToList();
		}

		private static void Collect(Item item, List<Item> found)
		{
			if (item == null)
			{
				return;
			}

			// The pockets are not offered either (what is in them is: the magazines)
			if (!(item is Pockets))
			{
				found.Add(item);
			}

			var compound = item as CompoundItem;
			if (compound == null)
			{
				return;
			}

			foreach (var slot in compound.Slots)
			{
				Collect(slot.ContainedItem, found);
			}

			CollectMagazines(compound, found);
		}

		// The magazines in the grids of a container, and in the containers inside it
		private static void CollectMagazines(CompoundItem container, List<Item> found)
		{
			foreach (var grid in container.Grids)
			{
				foreach (var inside in grid.Items)
				{
					if (inside is Magazine)
					{
						found.Add(inside);
					}
					else if (inside is CompoundItem nested && !(inside is Weapon))
					{
						CollectMagazines(nested, found);
					}
				}
			}
		}

		// "Equipped gear: 3/7 insured", with the picture of the first one still to insure (else the first one)
		private static PlanLine Progress(List<InsuredItem> gear, List<InsuredItem> open)
		{
			var shown = open.FirstOrDefault() ?? gear[0];
			return Steps.Progress("Equipped gear", gear.Count - open.Count, gear.Count, "insured", shown.TemplateId).WithItem(shown.Item);
		}

		// What the game already knows of the price at the trader (nothing asked, or not answered yet: not known)
		private static int? KnownCost(InsuranceCompany company, InsuredItem item, string traderId)
		{
			var price = company.InsureSummary.GetInsurePrice(item, traderId);
			return price != null && price.Loaded && price.Amount > 0 ? price.Amount : (int?)null;
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var company = Company();
			if (company == null || controller?.Inventory?.Equipment == null)
			{
				return new List<PlanLine> { Steps.Info("Insurance isn't available on this screen.") };
			}

			if (Insurers().Count == 0)
			{
				return new List<PlanLine> { Steps.Problem("Insurance", "no trader insures at the moment.") };
			}

			var trader = Chosen();
			if (trader == null)
			{
				return new List<PlanLine> { Steps.Problem("Insurance", string.IsNullOrEmpty(SettingsStore.Current.Insurance.TraderId) ? "pick the trader to insure at, in Setup." : "the trader chosen in Setup does not insure any more, pick another.") };
			}

			var gear = Insurable(controller.Inventory, company);
			if (gear.Count == 0)
			{
				return new List<PlanLine> { Steps.Info("Nothing equipped that can be insured.") };
			}

			var open = gear.Where(item => !company.Insured(item.Id)).ToList();
			var name = RepairWithKit.NameOfTrader(trader);
			var lines = new List<PlanLine> { Progress(gear, open) };
			if (open.Count == 0)
			{
				return lines;
			}

			string what = open.Count == 1 ? "1 item" : $"{open.Count} items";
			var costs = open.Select(item => KnownCost(company, item, trader.Id)).ToList();
			string text = $"Insure {what} at {name}";
			if (costs.All(cost => cost.HasValue))
			{
				int cost = costs.Sum(each => each.Value);
				int have = Math.Max(0, RepairWithKit.Roubles(controller) - Steps.Spent);
				if (cost > have)
				{
					lines.Add(Steps.Problem(text, $"needs {cost:N0} roubles, you have {have:N0}" + (Steps.Spent > 0 ? $" (after {Steps.Spent:N0} for repairs)" : "")));
					return lines;
				}

				Steps.Spent += cost;
				text += $" for {cost:N0} roubles";
			}

			lines.Add(new PlanLine
			{
				Text = text,
				Items = 0,
				Insured = open.Count,
				FailPrefix = "",
				Run = () => Insure(controller, trader)
			});
			return lines;
		}

		// The server's refusal in words: it says "Not enough money to complete transaction cost of: 12345" when the stash roubles are short
		internal static string Explained(string error, string traderName)
		{
			if (string.IsNullOrEmpty(error))
			{
				return "the server refused";
			}

			if (error.IndexOf("not enough money", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				var digits = new string(error.Substring(error.LastIndexOf(':') + 1).Where(char.IsDigit).ToArray());
				int cost;
				return int.TryParse(digits, out cost) ? $"not enough roubles in the stash, {traderName} wants {cost:N0}" : $"not enough roubles in the stash to insure at {traderName}";
			}

			return error;
		}

		// Insures what is still not insured, at the trader, the way the insurance screen does it: the same call, the same local bookkeeping
		private static async Task<string> Insure(InventoryController controller, Trader trader)
		{
			var company = Company();
			if (company == null)
			{
				return "insurance isn't available";
			}

			var items = Insurable(controller.Inventory, company).Where(item => !company.Insured(item.Id)).ToList();
			if (items.Count == 0)
			{
				return null;
			}

			var done = new TaskCompletionSource<string>();
			Plugin.Trace($"Insure: {items.Count} item(s) at {RepairWithKit.NameOfTrader(trader)}");
			InsureCall(trader, items.Select(item => item.Id).ToArray(), new Comfort.Common.Callback(result =>
			{
				if (result.Succeed)
				{
					foreach (var item in items)
					{
						item.InsurerId = trader.Id;
						company.InsuredItems.Add(item);
						company.OnItemInsured.Invoke(item);
					}
				}

				done.TrySetResult(result.Succeed ? null : Explained(result.Error, RepairWithKit.NameOfTrader(trader)));
			}));

			var first = await Task.WhenAny(done.Task, Task.Delay(20000));
			return first == done.Task ? done.Task.Result : "no answer from the server";
		}
	}
}
