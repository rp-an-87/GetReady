using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace GetReady.Tests
{
	// What the supply tests share: clean settings and steps, names without the game's localization, a plan for a step with the items
	// you carry and the items in the stash.
	internal static class SupplyFixture
	{
		internal static readonly Dictionary<string, string> Names = new Dictionary<string, string>
		{
			{ RealItems.Grizzly, "Grizzly" }, { RealItems.Salewa, "Salewa" }, { RealItems.Afak, "AFAK" }, { RealItems.Ifak, "IFAK" }, { RealItems.Car, "Car" },
			{ RealItems.Ai2, "AI-2" }, { RealItems.ArmyBandage, "Army bandage" }, { RealItems.AsepticBandage, "Aseptic bandage" }, { RealItems.Cat, "CAT" },
			{ RealItems.Esmarch, "Esmarch" }, { RealItems.CalokB, "CALOK-B" }, { RealItems.AluSplint, "Alu splint" },
			{ RealItems.ImmobilizingSplint, "Splint" }, { RealItems.Cms, "CMS" }, { RealItems.Surv12, "Surv12" },
			{ RealItems.M67, "M67" }, { RealItems.Rgd5, "RGD-5" }, { RealItems.F1, "F-1" }, { RealItems.Vog17, "VOG-17" }, { RealItems.Vog25, "VOG-25" },
			{ RealItems.Rgn, "RGN" }, { RealItems.Rgo, "RGO" }, { RealItems.GreenFlare, "Green flare" }, { RealItems.RedFlare, "Red flare" },
			{ RealItems.Roubles, "Roubles" }, { RealItems.Euros, "Euros" }, { RealItems.Dollars, "Dollars" }
		};

		// Clean settings and statics, readable names, and the random pick always the first
		internal static void Reset()
		{
			System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture; // "25,000", whatever the machine uses
			Plugin.Log = BepInEx.Logging.Logger.CreateLogSource("GetReady.Tests");
			SettingsStore.UseForTests(new Settings());
			Steps.Claimed.Clear();
			Steps.Owned.Clear();
			Steps.Bringing.Clear();
			Steps.Leaving.Clear();
			Game.LongNames.Clear();
			Game.IsInsideContainer = item => false;
			Game.IsInsideInjectorCase = item => false;
			Game.RandomIndex = count => 0;
			Game.Localize = key =>
			{
				string template = key.Substring(0, key.IndexOf(' '));
				string name;
				return Names.TryGetValue(template, out name) ? name : key;
			};
		}

		// The plan of the step; nothing is kept out of any place
		internal static List<PlanLine> Plan(SuppliesStep step, IEnumerable<Item> carried, IEnumerable<Item> stash) =>
			step.Plan(null, carried.ToList(), stash.ToList(), () => null);

		// Only this group has a target
		internal static GroupDef Only(SuppliesStep step, string id, int target)
		{
			foreach (var def in step.Groups)
			{
				def.Setting.Target = def.Id == id ? target : 0;
			}

			return step.Groups.First(def => def.Id == id);
		}

		internal static Item[] Many(string template, int count) => Enumerable.Range(0, count).Select(_ => RealItems.Any(template)).ToArray();

		internal static List<string> Texts(IEnumerable<PlanLine> lines) => lines.Select(line => line.Text).ToList();
	}
}
