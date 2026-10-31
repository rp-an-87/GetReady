using System;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Configuration;
using EFT.Communications;
using SPT.Common.Http;
using UnityEngine;

namespace GetReady
{
	// Dev tool, the F12 button "Reset the test kit" (GetReady > 9. Advanced, shown with "advanced settings" on): asks the
	// CustomRpTweaks server mod to put the test kit back in this profile's stash (worn gear worn down again, used up items
	// back; only for profiles in its testKit.profiles) and then loads the profile from the server again. No restarts.
	internal static class TestKitReset
	{
		private const string Route = "/customrptweaks/testkit/reset";
		private const string HurtRoute = "/customrptweaks/testkit/hurt";
		private const string HealRoute = "/customrptweaks/testkit/heal";
		private static bool _running;

		internal static void Draw(ConfigEntryBase entry) => Button("Reset the test kit now", Route);
		internal static void DrawHurt(ConfigEntryBase entry) => Button("Hurt me (fracture, bleeds, low HP)", HurtRoute);
		internal static void DrawHeal(ConfigEntryBase entry) => Button("Heal me fully", HealRoute);

		private static void Button(string label, string route)
		{
			GUI.enabled = !_running;
			if (GUILayout.Button(_running ? "Working..." : label, GUILayout.ExpandWidth(true)))
			{
				Run(route);
			}

			GUI.enabled = true;
		}

		// The game's own full reload of the menu (what it does after a backend error): closes the screens, loads the profile from the
		// server again and builds the stash, hideout and the rest from it. Just loading the profile left the open screens on the old one.
		private static async Task ReloadMenu()
		{
			var app = SPT.Reflection.Utils.ClientAppUtils.GetMainApp();
			var field = app?.GetType().GetField("_menuOperation", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			var menu = field?.GetValue(app);
			var reload = menu?.GetType().GetMethod("AfterErrorHandler", BindingFlags.Instance | BindingFlags.Public);
			if (reload != null)
			{
				await (Task)reload.Invoke(menu, null);
				return;
			}

			Plugin.Log.LogWarning("Test kit reset: no menu operation to reload, loading the profile only");
			var session = app?.GetClientBackEndSession();
			if (session != null)
			{
				await session.GetProfiles();
			}
		}

		// The game's repair controller copies the profile's inventory when it is made and keeps it: after the menu reload it would go on
		// reading the old profile (old kit resources, no locks, kits that are gone). Point it at the new one.
		private static void RebindRepairController()
		{
			var session = SPT.Reflection.Utils.ClientAppUtils.GetMainApp()?.GetClientBackEndSession();
			var controller = session?.RepairController;
			var profile = session?.Profile;
			if (controller == null || profile == null)
			{
				Plugin.Log.LogWarning("Test kit reset: no repair controller to point at the new profile");
				return;
			}

			controller._profile = profile;
			controller._profileInventory = profile.Inventory;
		}

		private static async void Run(string route)
		{
			_running = true;
			try
			{
				string answer = RequestHandler.GetJson(route);
				await ReloadMenu();
				RebindRepairController();

				answer = (answer ?? "").Trim('"');
				Plugin.Log.LogInfo($"Test kit reset: {answer}");
				NotificationManager.DisplayMessageNotification($"Test kit: {answer}. The menu reloads.");
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Test kit reset failed: {e}");
				NotificationManager.DisplayMessageNotification("Test kit reset failed (is the CustomRpTweaks server mod up to date?): " + e.Message);
			}
			finally
			{
				_running = false;
			}
		}
	}
}
