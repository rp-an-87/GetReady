using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.UI.Matchmaker;
using SPT.Reflection.Patching;
using UnityEngine;

namespace GetReady
{
	// The map screen tells you when a case you carry is for other maps (see MapTags). The screen reports every map you pick through the
	// setter of SelectedLocation, so that is what is patched; the warning is drawn over the screen like the rest of this plugin's windows.
	internal class MapScreenPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(MatchMakerSelectionLocationScreen).GetProperty(nameof(MatchMakerSelectionLocationScreen.SelectedLocation), BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

		[PatchPostfix]
		private static void Postfix(MatchMakerSelectionLocationScreen __instance)
		{
			try
			{
				ReminderBars.Picked(__instance, __instance.SelectedLocation?.Id);
				NotReadyWarning.Picked(__instance);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Map warning failed: {e}");
			}
		}
	}

	// The map screen's own facts (which raid you are about to start, with whose profile), read from the game's screen
	internal static class MapScreen
	{
		private static readonly FieldInfo SideField = typeof(MatchMakerSelectionLocationScreen).GetField("_chosenSideType", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly FieldInfo ProfileField = typeof(MatchMakerSelectionLocationScreen).GetField("_profile", BindingFlags.Instance | BindingFlags.NonPublic);

		// A scav raid takes the scav's own kit and nothing from your stash
		internal static bool IsScav(MatchMakerSelectionLocationScreen screen) => SideField != null && SideField.GetValue(screen) is ESideType side && side == ESideType.Savage;

		// The profile of the screen: the one you are about to raid with
		internal static Profile ProfileOf(MatchMakerSelectionLocationScreen screen) => ProfileField?.GetValue(screen) as Profile;
	}
}
