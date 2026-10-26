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

		private void Awake()
		{
			Log = Logger;
			// Everything else is set in the window and kept in BepInEx\config\GetReady\settings.json
			ShowButton = Config.Bind("1. General", "1. Show the Get ready button in the stash", true,
				"Adds a button above the pin button of your stash (hideout and main menu) that opens the Get ready window. Takes effect the next time the stash opens.");
			OpenHotkey = Config.Bind("1. General", "2. Open the window with a key", KeyboardShortcut.Empty,
				"Opens and closes the window without the button. Empty by default.");
			SettingsStore.Load();
			new ReadyButtonPatch().Enable();
			Log.LogInfo($"GetReady {PluginVersion} loaded");
		}

		private void Update()
		{
			ReadyWindow.Tick();
			if (OpenHotkey.Value.MainKey != KeyCode.None && OpenHotkey.Value.IsDown())
			{
				ReadyWindow.Toggle();
			}
		}

		private void OnGUI()
		{
			ReadyWindow.Draw();
		}
	}
}
