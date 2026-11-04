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
		internal static ConfigEntry<KeyboardShortcut> SpikeHotkey;
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
			SpikeHotkey = Config.Bind("9. Experiments", "1. Game window spike key", KeyboardShortcut.Empty,
				"Opens the experimental Get ready window, made from a copy of a game window (the Ready page only for now). Empty by default.");
			TopUpHotkey = Config.Bind("2. Quick actions", "1. Magazine top-up hotkey", new KeyboardShortcut(KeyCode.T),
				"Hover a magazine in the stash or in a raid inventory and press this to fill it up. It uses the ammo the magazine already holds, or else the highest-penetration ammo it accepts, taken from the stash and your gear. In a raid it is the normal timed load. Unassign the key to turn the feature off.");
			RepairHotkey = Config.Bind("2. Quick actions", "2. Repair with kit hotkey", new KeyboardShortcut(KeyCode.R),
				"Hover an item in the stash and press this to repair it fully with one of your repair kits, without opening the repair window (the same repair as the window's button, as far as the kits can pay for). It uses the kit with the least resource left that can repair the item. Does nothing if you have no suitable kit, or outside the hideout/menu stash. Unassign the key to turn the feature off.");
			new ReadyButtonPatch().Enable();
			new InventoryScreenEscapePatch().Enable();
			new HideoutOverlayEscapePatch().Enable();
			new MapScreenPatch().Enable();
			new TextWeightPatch().Enable();
			new TextWeightButtonPatch().Enable();
			new TagChangedPatch().Enable();
			Log.LogInfo($"GetReady {PluginVersion} loaded");
		}

		private void Update()
		{
			ReadyWindow.Tick();
			MagazineTopUp.Update();
			RepairWithKit.Update();
			ReadyUiWindow.Update();
			if (SpikeHotkey.Value.MainKey != KeyCode.None && SpikeHotkey.Value.IsDown())
			{
				ReadyUiWindow.Toggle();
			}

			if (OpenHotkey.Value.MainKey != KeyCode.None && OpenHotkey.Value.IsDown())
			{
				ReadyWindow.Toggle();
			}
		}

		private void OnGUI()
		{
			ReadyWindow.Draw();
			ReadyFlash.Draw();
			MapWarning.Draw();
		}
	}
}
