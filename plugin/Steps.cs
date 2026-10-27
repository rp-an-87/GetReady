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

		// How many items the line moves (the notification adds them up)
		public int Items = 1;

		// A progress line: how much of what is wanted is there (drawn as a bar)
		public bool IsProgress;
		public float Have;
		public float Want;
		public string Unit;
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
			new SpecialSlotsStep(), new InjectorCaseStep(), new HealingStep(), new GrenadesStep(), new CashStep()
		};

		// Stash items an earlier step has already planned to bring this pass, so a later step doesn't bring them a second time
		// (a Propital for the injector case also counts for the Propital supply). Cleared before every planning pass.
		internal static readonly List<Item> Claimed = new List<Item>();

		// Items already in a special slot of the template (managed by that step, so the supplies leave them alone but count
		// them) and the ones it is about to swap out (which stop counting)
		internal static readonly List<Item> Owned = new List<Item>();

		// What the special slots step brings: it counts for the supplies (an injector case's contents don't, it keeps its own)
		internal static readonly List<Item> Bringing = new List<Item>();
		internal static readonly List<Item> Leaving = new List<Item>();

		internal static void NewPass()
		{
			Claimed.Clear();
			Bringing.Clear();
			Owned.Clear();
			Leaving.Clear();
		}

		internal static PlanLine Info(string text) => new PlanLine { Text = text };
		internal static PlanLine Progress(string subject, float have, float want, string unit, string template = null) =>
			new PlanLine { Text = $"{subject}: {have:0}/{want:0} {unit}", Subject = subject, Have = have, Want = want, Unit = unit, Template = template, IsProgress = true };

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
		internal static List<PlanLine> Summarize(int inPlace, int total, string unit, List<Need> needs, List<PlanLine> others)
		{
			var lines = new List<PlanLine> { Progress(char.ToUpperInvariant(unit[0]) + unit.Substring(1), inPlace, total, "in place") };
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
