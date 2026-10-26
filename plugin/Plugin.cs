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
		internal static ConfigEntry<KeyboardShortcut> OpenHotkey;

		private void Awake()
		{
			Log = Logger;
			// Everything else is set in the window and kept in BepInEx\config\GetReady\settings.json
			OpenHotkey = Config.Bind("1. General", "1. Open the Get ready window", new KeyboardShortcut(KeyCode.F9),
				"Opens and closes the window. It works in the hideout and the main menu, where your stash is available.");
			SettingsStore.Load();
			Log.LogInfo($"GetReady {PluginVersion} loaded");
		}

		private void Update()
		{
			if (OpenHotkey.Value.IsDown())
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
