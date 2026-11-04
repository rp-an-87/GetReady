using System.Reflection;
using EFT.UI.DragAndDrop;
using SPT.Reflection.Patching;
using UnityEngine;

namespace GetReady
{
	// While an item is dragged in the stash the window gets out of the way: faded (and let the mouse through, so the item can be dropped on
	// what is behind it) or hidden, as chosen in F12. The game makes a DraggedItemView for the length of a drag and destroys it at the drop.
	internal enum DragFadeMode
	{
		Off,
		Fade,
		Hide
	}

	internal static class DragFade
	{
		private static int _dragging;

		internal static bool Active => _dragging > 0 && Plugin.DragFadeMode.Value != DragFadeMode.Off;

		internal static float Alpha => Plugin.DragFadeMode.Value == DragFadeMode.Hide ? 0f : Plugin.DragFadeOpacity.Value;

		internal static void Started()
		{
			_dragging++;
			ReadyUiWindow.ApplyDragFade();
		}

		internal static void Ended()
		{
			_dragging = Mathf.Max(0, _dragging - 1);
			ReadyUiWindow.ApplyDragFade();
		}
	}

	internal class DragStartPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(DraggedItemView).GetMethod("Construct");

		[PatchPostfix]
		private static void Postfix() => DragFade.Started();
	}

	internal class DragEndPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(DraggedItemView).GetMethod("OnDestroy");

		[PatchPostfix]
		private static void Postfix() => DragFade.Ended();
	}
}
