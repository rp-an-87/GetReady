using System.Reflection;
using EFT;
using EFT.Hideout;
using EFT.InputSystem;
using EFT.UI;
using SPT.Reflection.Patching;

namespace GetReady
{
	// Esc should close this window (or its alert box) and nothing else, as it does for the game's own windows. The game's stash
	// screens answer Esc themselves by closing the whole screen, so they are asked first: while the window or the alert is up they let
	// it have the key and report it as handled.
	internal static class EscapeKey
	{
		// true when the key was taken
		internal static bool Handle(ECommand command, ref InputNode.ETranslateResult result)
		{
			if (!command.IsCommand(ECommand.Escape) || !ReadyInput.ConsumeEscape())
			{
				return false;
			}

			result = InputNode.ETranslateResult.BlockAll;
			return true;
		}
	}

	// The stash in the main menu and in the hideout
	internal class InventoryScreenEscapePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(InventoryScreen).GetMethod(nameof(InventoryScreen.TranslateCommand), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		[PatchPrefix]
		private static bool Prefix(ECommand command, ref InputNode.ETranslateResult __result) => !EscapeKey.Handle(command, ref __result);
	}

	internal class HideoutOverlayEscapePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(HideoutScreenOverlay).GetMethod(nameof(HideoutScreenOverlay.TranslateCommand), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		[PatchPrefix]
		private static bool Prefix(ECommand command, ref InputNode.ETranslateResult __result) => !EscapeKey.Handle(command, ref __result);
	}
}
