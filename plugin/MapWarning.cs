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
				MapWarning.Picked(__instance, __instance.SelectedLocation?.Id);
				ReminderBars.Picked(__instance, __instance.SelectedLocation?.Id);
				NotReadyWarning.Picked(__instance);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Map warning failed: {e}");
			}
		}
	}

	internal static class MapWarning
	{
		private static readonly FieldInfo SideField = typeof(MatchMakerSelectionLocationScreen).GetField("_chosenSideType", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly FieldInfo ProfileField = typeof(MatchMakerSelectionLocationScreen).GetField("_profile", BindingFlags.Instance | BindingFlags.NonPublic);

		private static MatchMakerSelectionLocationScreen _screen;
		private static string _map;
		private static float _checkedAt = float.NegativeInfinity;

		internal static void Picked(MatchMakerSelectionLocationScreen screen, string locationId)
		{
			_screen = screen;
			_map = MapTags.MapOf(locationId);
			_checkedAt = float.NegativeInfinity;
		}

		// The ids of what you carry (the profile of the screen: the one you are about to raid with); null for a scav raid, which takes the scav's
		// own kit and nothing from your stash
		internal static bool IsScav(MatchMakerSelectionLocationScreen screen) => SideField != null && SideField.GetValue(screen) is ESideType side && side == ESideType.Savage;

		// The profile of the screen: the one you are about to raid with
		internal static Profile ProfileOf(MatchMakerSelectionLocationScreen screen) => ProfileField?.GetValue(screen) as Profile;

		private static HashSet<string> CarriedIds()
		{
			if (IsScav(_screen))
			{
				return null;
			}

			var profile = ProfileField?.GetValue(_screen) as Profile;
			if (profile?.Inventory == null)
			{
				return null;
			}

			return new HashSet<string>(Game.CarriedAndSecured(profile.Inventory).Select(item => item.Id.ToString()));
		}

		private static MapTags.Report _report = new MapTags.Report();

		// The wrong and missing containers for the map picked (shown as reminders, see ReminderBars). Looked at twice a second, in the layout
		// pass only, so the two passes of a frame see the same thing.
		internal static MapTags.Report Current()
		{
			var settings = SettingsStore.Current.Cases;
			if (_screen == null || !_screen.isActiveAndEnabled || !settings.MapWarnings || settings.Cases.Count == 0)
			{
				return new MapTags.Report();
			}

			if (Event.current.type == EventType.Layout && Time.unscaledTime - _checkedAt > 0.5f)
			{
				_checkedAt = Time.unscaledTime;
				var ids = CarriedIds();
				_report = ids == null ? new MapTags.Report() : MapTags.Check(settings.Cases, ids, _map, true);
			}

			return _report;
		}
	}
}
