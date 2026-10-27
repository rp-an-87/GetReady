using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using EFT.InventoryLogic;
using EFT.UI;

namespace GetReady
{
	// What CustomRpTweaks' hotkey hint block asks for (it finds this class by name and binds For once, so keep the name and the
	// signature): the hotkeys of this plugin that apply to the hovered item, as key and action. The keys are the current settings.
	public static class QuickActionHints
	{
		private static readonly FieldInfo ControllerField = typeof(ItemUiContext).GetField("_inventoryController", BindingFlags.Instance | BindingFlags.NonPublic);

		public static List<KeyValuePair<KeyboardShortcut, string>> For(Item item)
		{
			var hints = new List<KeyValuePair<KeyboardShortcut, string>>();
			var uiContext = ItemUiContext.Instance;
			if (item == null || uiContext == null)
			{
				return hints;
			}

			var controller = ControllerField.GetValue(uiContext) as InventoryController;
			if (MagazineTopUp.IsApplicable(item) && Plugin.TopUpHotkey.Value.MainKey != UnityEngine.KeyCode.None)
			{
				hints.Add(new KeyValuePair<KeyboardShortcut, string>(Plugin.TopUpHotkey.Value, "top up"));
			}

			if (RepairWithKit.IsApplicable(uiContext, controller, item) && Plugin.RepairHotkey.Value.MainKey != UnityEngine.KeyCode.None)
			{
				hints.Add(new KeyValuePair<KeyboardShortcut, string>(Plugin.RepairHotkey.Value, "repair"));
			}

			return hints;
		}
	}
}
