using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace GetReady
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "RPAN87.GetReady";
		public const string PluginName = "GetReady";
		public const string PluginVersion = "0.1.0";

		internal static ManualLogSource Log;
		internal static ConfigEntry<bool> ShowButton;
		internal static ConfigEntry<KeyboardShortcut> OpenHotkey;
		internal static ConfigEntry<bool> ShowFlash;
		internal static ConfigEntry<DragFadeMode> DragFadeMode;
		internal static ConfigEntry<float> DragFadeOpacity;
		internal static ConfigEntry<bool> DebugLogging;

		// A line for the log, only when "Log details" is on (F12): what the window does and how long it takes
		internal static void Trace(string text)
		{
			if (DebugLogging != null && DebugLogging.Value)
			{
				Log.LogInfo(text);
			}
		}
		internal static ConfigEntry<KeyboardShortcut> TopUpHotkey;
		internal static ConfigEntry<KeyboardShortcut> RepairHotkey;

		private void Awake()
		{
			Log = Logger;
			// Everything else is set in the window and kept per profile in BepInEx\config\GetReady\profiles\<profile id>.json
			ShowButton = Config.Bind("1. General", "1. Show the Get ready button in the stash", true,
				"Adds a button above the pin button of your stash (hideout and main menu) that opens the Get ready window. Takes effect the next time the stash opens.");
			OpenHotkey = Config.Bind("1. General", "2. Open the window with a key", KeyboardShortcut.Empty,
				"Opens and closes the window without the button. Empty by default.");
			ShowFlash = Config.Bind("1. General", "3. READY flash after a successful run", true,
				"A light green vignette and a green READY on screen for a moment when Get ready has brought everything. Just for show.");
			DragFadeMode = Config.Bind("1. General", "4. While dragging an item", GetReady.DragFadeMode.Fade,
				"What the new Get ready window does while you drag an item in the stash: Off leaves it as it is, Fade makes it see-through (and lets the mouse through, so you can drop the item on what is behind it), Hide takes it away until you drop.");
			DragFadeOpacity = Config.Bind("1. General", "5. Opacity while dragging", 0.05f,
				new ConfigDescription("How see-through the window is while dragging an item, when the choice above is Fade.", new AcceptableValueRange<float>(0.05f, 0.5f)));
			DebugLogging = Config.Bind("8. Debugging", "Log details", false,
				new ConfigDescription("Writes what GetReady does, and how long it takes, to the BepInEx log (only errors and warnings are written otherwise). For finding out why something is slow or wrong.", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
			// A button in F12 that shows a made-up alert, for looking at it
			Config.Bind("8. Debugging", "Show a test alert", false,
				new ConfigDescription("Shows an alert like the one a run leaves behind when something is wrong, with made-up problems.", null,
					new ConfigurationManagerAttributes { IsAdvanced = true, CustomDrawer = entry => { if (GUILayout.Button("Show a test alert")) { ReadyAlert.ShowTest(); } } }));
			TopUpHotkey = Config.Bind("2. Quick actions", "1. Magazine top-up hotkey", new KeyboardShortcut(KeyCode.T),
				"Hover a magazine in the stash or in a raid inventory and press this to fill it up. It uses the ammo the magazine already holds, or else the highest-penetration ammo it accepts, taken from the stash and your gear. In a raid it is the normal timed load. Unassign the key to turn the feature off.");
			RepairHotkey = Config.Bind("2. Quick actions", "2. Repair with kit hotkey", new KeyboardShortcut(KeyCode.R),
				"Hover an item in the stash and press this to repair it fully with one of your repair kits, without opening the repair window (the same repair as the window's button, as far as the kits can pay for). It uses the kit with the least resource left that can repair the item. Does nothing if you have no suitable kit, or outside the hideout/menu stash. Unassign the key to turn the feature off.");
			new ReadyButtonPatch().Enable();
			new InventoryScreenEscapePatch().Enable();
			new HideoutOverlayEscapePatch().Enable();
			new MapScreenPatch().Enable();
			new TextWeightPatch().Enable();
			new DragStartPatch().Enable();
			new DragEndPatch().Enable();
			new TextWeightButtonPatch().Enable();
			new TagChangedPatch().Enable();
			Trace($"GetReady {PluginVersion} loaded");
		}

		private void OnApplicationQuit() => SettingsStore.FlushIfDue(true);

		private void Update()
		{
			SettingsStore.FlushIfDue();
			ReadyWindow.Tick();
			MagazineTopUp.Update();
			RepairWithKit.Update();
			ReadyUiWindow.Update();
			ReadyAlert.Update();
			if (OpenHotkey.Value.MainKey != KeyCode.None && OpenHotkey.Value.IsDown())
			{
				ReadyUiWindow.Toggle();
			}
		}

		private void OnGUI()
		{
			ReadyFlash.Draw();
			MapWarning.Draw();
		}
	}
}
