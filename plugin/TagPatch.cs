using System;
using System.Reflection;
using EFT.InventoryLogic;
using SPT.Reflection.Patching;

namespace GetReady
{
	// Tagging or untagging a container changes how a case is shown everywhere (the plan, the map bar): the game applies every tag change
	// through TagComponent.Set, so that is where the saved case is told.
	internal class TagChangedPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(TagComponent).GetMethod(nameof(TagComponent.Set), BindingFlags.Instance | BindingFlags.Public);

		[PatchPostfix]
		private static void Postfix(TagComponent __instance, bool simulate)
		{
			if (simulate)
			{
				return;
			}

			try
			{
				if (CasesStep.OnTagChanged(__instance.Item.Id.ToString(), __instance.Name, __instance.Color))
				{
					ReadyWindow.PlanChanged();
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Tag change failed: {e}");
			}
		}
	}
}
