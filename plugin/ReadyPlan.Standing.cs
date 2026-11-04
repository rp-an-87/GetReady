using System.Collections.Generic;
using System.Linq;

namespace GetReady
{
	// How a card or a set of lines stands (done, doable, a problem, or left out on purpose): the rules, with nothing drawn. Both windows use them.
	internal static partial class ReadyPlan
	{
		// How a card stands: red when something is wrong, else green when it is complete, else amber when Get ready can make it complete
		// Skipped: the step left this card out on purpose (cash while a no-cash case is carried), so it is neither good nor bad
		internal enum Standing { Done, Doable, Problem, Skipped }

		internal static Standing StandingOf(bool done, bool hasRun, bool hasProblem) =>
			hasProblem ? Standing.Problem : done ? Standing.Done : hasRun ? Standing.Doable : Standing.Problem;

		// The standing of the card the progress line at this index opens: what follows it up to the next progress line decides
		internal static Standing StandingFor(List<PlanLine> lines, int progressIndex)
		{
			var progress = lines[progressIndex];
			if (progress.Skipped)
			{
				return Standing.Skipped;
			}

			bool hasRun = false, hasProblem = false;
			for (int i = progressIndex + 1; i < lines.Count && !lines[i].IsProgress && !lines[i].Standalone; i++)
			{
				hasRun |= lines[i].Run != null || lines[i].Covered;
				hasProblem |= lines[i].Problem;
			}

			return StandingOf(progress.Have >= progress.Want, hasRun, hasProblem);
		}


		// How a set of lines stands: red when something is wrong, else green when every bar is full, else amber when Get ready can fill it
		internal static Standing StandingOfAll(List<PlanLine> lines)
		{
			var bars = lines.Where(line => line.IsProgress).ToList();
			return StandingOf(bars.Count > 0 && bars.All(line => line.Have >= line.Want), lines.Any(line => line.Run != null || line.Covered), lines.Any(line => line.Problem));
		}

		internal static string CaliberName(string caliber)
		{
			if (string.IsNullOrEmpty(caliber))
			{
				return "";
			}

			var name = Game.Localize(caliber);
			return string.IsNullOrEmpty(name) || name == caliber ? AmmoPanels.CaliberLabel(caliber) : name;
		}

		// The standing of the loose ammo card: it is only red when a line says something is wrong. A stack that can't be filled is
		// left as it is (green), unless the owner switched on that it is a problem (then it has a problem line).
		internal static Standing LooseStanding(List<PlanLine> lines, int progressIndex)
		{
			var standing = StandingFor(lines, progressIndex);
			if (standing != Standing.Problem)
			{
				return standing;
			}

			for (int i = progressIndex + 1; i < lines.Count && !lines[i].IsProgress && !lines[i].Standalone; i++)
			{
				if (lines[i].Problem)
				{
					return Standing.Problem;
				}
			}

			return Standing.Done;
		}
	}
}
