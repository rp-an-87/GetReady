using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;

namespace GetReady
{
	// One line of the plan: what is there, or what will be done. Run is null for lines that are only information.
	internal class PlanLine
	{
		public string Text;
		public bool Problem;
		public Func<Task<string>> Run; // null, or why it failed

		// For the window: what it is about (bold), what is wrong or what happens (plain) and an item whose icon goes with it
		public string Subject;
		public string Detail;
		public string Template;

		// The very item the line is about, when there is one: its icon shows what is on it (a weapon with its mods)
		public Item Item;
		public PlanLine WithItem(Item item)
		{
			Item = item;
			return this;
		}

		// How many items the line moves (the notification adds them up)
		public int Items = 1;

		// The line repairs a piece of gear rather than moving items (counted apart in the notification)
		public bool Repair;

		// The icon shows in the alert box only (the Ready page already has the card's icon right above)
		public bool AlertIconOnly;

		// A progress line that is left out on purpose: drawn grey with the note instead of the numbers (not a failure)
		public bool Skipped;
		public string Note;
		// The case behind a skipped line (the one that turns the cash off): its icon, tag and name are drawn after the note
		public CaseEntry By;

		// The line heals you with medicine (counted apart in the notification)
		public bool Heal;

		// The line changes a weapon's fire mode (counted apart in the notification)
		public bool Mode;

		// The line fills a magazine (counted apart in the notification)
		public bool Fill;

		// The line chambers a round (counted apart in the notification)
		public bool Chamber;

		// The line tops up a loose stack of ammo (counted apart in the notification)
		public bool Topped;

		// A line that stands for work another line of the plan does (a second weapon of the same caliber: the spare ammo is brought once)
		public bool Covered;

		// A line that is about something of its own, not a part of the card above it (a weapon with no magazine at all): it ends that card
		public bool Standalone;

		// For the window only (the ammo panels): which part of a weapon the line is about ("magazine", "put", "chamber", "spare"), how
		// much it moves, of what, and the template whose icon shows it
		public string Part;
		public int Amount;
		public string What;
		public string WhatTemplate;
		// Where the rounds of a fill come from: the container (its template and name) and how many rounds from it
		public List<PlaceCount> From = new List<PlaceCount>();
		// The magazines a "stuck" problem is about, and the ammo they could take (names)
		public List<MagInfo> Mags = new List<MagInfo>();
		public string Options;
		public string Why; // what decided the ammo of a fill ("the only compatible ammo you carry")
		public string Label; // what a fill or a top-up line is about, short ("STANAG 30 in Vest")
		public string Place; // where the thing is (a vest, a backpack): shown as that gear's icon
		public string PlaceTemplate;
		public string ItemTemplate; // the magazine the line fills
		public int Asked; // the spare ammo asked for
		public PlanLine InPart(string part)
		{
			Part = part;
			return this;
		}

		// A progress line: how much of what is wanted is there (drawn as a bar)
		public bool IsProgress;
		public float Have;
		public float Want;
		public string Unit;
	}

	// A magazine named in a problem: what it is, where it is (null: in a weapon) and how full
	internal class MagInfo
	{
		public string Template;
		public string Name;
		public string Place;
		public string PlaceTemplate;
		public int Have;
		public int Want;
	}

	// Rounds taken from one container (a vest, the stash)
	internal class PlaceCount
	{
		public string Template;
		public string Name;
		public int Count;
		// The container itself (null for the stash): its picture opens it
		public CompoundItem Container;
	}

	// One item a template wants and the gear lacks: what it is, where it goes, and how to bring it (null when the stash has none)
	internal class Need
	{
		public string Name;
		public string Where;
		public string Template;
		public Func<Task<string>> Run;
	}

	// A part of getting ready. Plan reads the inventory and returns what is missing; nothing moves until the lines are run.
	internal interface IStep
	{
		string Name { get; }
		bool Enabled { get; set; }
		// The amounts and destinations the window lets you set (none for the steps that work from a saved template)
		IReadOnlyList<GroupDef> Groups { get; }
		List<PlanLine> Plan(InventoryController controller);
	}

	internal static class Steps
	{
		internal static readonly IStep[] All =
		{
			new SpecialSlotsStep(), new CasesStep(), new HealingStep(), new GrenadesStep(), new CashStep(), new RepairStep(), new AmmoStep(), new FullAutoStep(), new HealMeStep()
		};

		// The order of the Ready and Setup pages (what you tweak most first). Running order is All: Heal me must stay last and the special slots
		// first, since later steps count what earlier ones bring.
		internal static IEnumerable<IStep> DisplayOrder
		{
			get
			{
				string[] order = { "Ammo", "Medicine to bring", "Grenades and flares", "Special slots", "Cases", "Repair", "Full auto", "Treat injuries", "Cash" };
				return All.OrderBy(step => { int index = Array.IndexOf(order, step.Name); return index < 0 ? order.Length : index; });
			}
		}

		// Stash items an earlier step has already planned to bring this pass, so a later step doesn't bring them a second time
		// (a Propital for a case's template also counts for the Propital supply). Cleared before every planning pass.
		internal static readonly List<Item> Claimed = new List<Item>();

		// Items already in a special slot of the template (managed by that step, so the supplies leave them alone but count
		// them) and the ones it is about to swap out (which stop counting)
		internal static readonly List<Item> Owned = new List<Item>();

		// What the special slots step brings: it counts for the supplies (what a case's template looks after doesn't, it keeps its own)
		internal static readonly List<Item> Bringing = new List<Item>();
		internal static readonly List<Item> Leaving = new List<Item>();

		// The money the cases you carry will hold after this pass, by currency (RUB, EUR, USD): it counts toward the Cash step's target, which
		// brings only what is still missing
		internal static readonly Dictionary<string, int> CaseMoney = new Dictionary<string, int>();

		// The money the cases give up to the stash this pass, by currency: the Cash step can bring it on from there
		internal static readonly Dictionary<string, int> Released = new Dictionary<string, int>();

		internal static int Of(Dictionary<string, int> money, string currency)
		{
			int amount;
			return money.TryGetValue(currency, out amount) ? amount : 0;
		}

		internal static void Add(Dictionary<string, int> money, string currency, int amount)
		{
			money[currency] = Of(money, currency) + amount;
		}

		internal static void NewPass()
		{
			CaseMoney.Clear();
			Released.Clear();
			Claimed.Clear();
			Bringing.Clear();
			Owned.Clear();
			Leaving.Clear();
		}

		internal static PlanLine Info(string text) => new PlanLine { Text = text };
		internal static PlanLine For(PlanLine line, Item item)
		{
			line.Item = item;
			return line;
		}

		internal static PlanLine Progress(string subject, float have, float want, string unit, string template = null) =>
			new PlanLine { Text = Game.Symbol(unit) is string sign ? $"{subject}: {sign} {have:0}/{want:0}" : $"{subject}: {have:0}/{want:0} {unit}", Subject = subject, Have = have, Want = want, Unit = unit, Template = template, IsProgress = true };

		internal static PlanLine Problem(string text) => new PlanLine { Text = text, Problem = true };

		internal static PlanLine Problem(string subject, string detail, string template = null) =>
			new PlanLine { Text = $"{subject}: {detail}", Problem = true, Subject = subject, Detail = detail, Template = template };

		// " (slot 4, slot 5)", or nothing when the items have no place worth naming
		private static string WhereOf(IEnumerable<Need> needs)
		{
			var places = needs.Where(need => need.Where != null).Select(need => need.Where).ToArray();
			return places.Length == 0 ? "" : $" ({string.Join(", ", places)})";
		}

		// "2/3 in place", then what to bring grouped by name ("Bring 2x Propital (cells 0,0, 1,0)"), then what the stash lacks
		internal static List<PlanLine> Summarize(int inPlace, int total, string unit, List<Need> needs, List<PlanLine> others, string title = null)
		{
			var lines = new List<PlanLine> { Progress(title ?? char.ToUpperInvariant(unit[0]) + unit.Substring(1), inPlace, total, "in place") };
			foreach (var group in needs.Where(need => need.Run != null).GroupBy(need => need.Name))
			{
				var runs = group.Select(need => need.Run).ToList();
				lines.Add(new PlanLine
				{
					Template = group.First().Template,
					Items = group.Count(),
					Text = $"Bring {group.Count()}x {group.Key}{WhereOf(group)}",
					Run = async () =>
					{
						foreach (var run in runs)
						{
							var error = await run();
							if (error != null)
							{
								return error;
							}
						}

						return null;
					}
				});
			}

			foreach (var group in needs.Where(need => need.Run == null).GroupBy(need => need.Name))
			{
				lines.Add(Problem(group.Key, $"Missing {group.Count()}: none left in the stash{WhereOf(group)}", group.First().Template));
			}

			lines.AddRange(others);
			return lines;
		}
	}
}
