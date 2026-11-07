using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.Utilities;
using SPT.Reflection.Patching;
using UnityEngine;

namespace GetReady
{
	// A "Get ready" entry in the right-click menu of a case you carry (stash, hideout, main menu): it opens a submenu with what Setup > Cases
	// does for that case, and a second one for the maps. The game's menu is built from an enum of buttons; our entries are a button added as a
	// dynamic interaction (so the game lists it) whose hover opens a menu of our own, built the way the game builds its own submenus.
	internal enum ECaseAction { Maps, Track, Update, StopTracking }

	// The ten maps of MapTags.Maps, in that order
	internal enum EMapAction { Customs, Factory, Woods, Interchange, Shoreline, Reserve, Lighthouse, Streets, GroundZero, Labs }

	internal static class CaseMenu
	{
		internal const string MenuKey = "Get ready";

		internal static readonly Color Purple = new Color(0.3f, 0.14f, 0.45f);
		internal static readonly Color PurpleHover = new Color(0.42f, 0.2f, 0.62f);
		internal static readonly Color Red = new Color(0.4f, 0.1f, 0.09f);
		internal static readonly Color RedHover = new Color(0.55f, 0.14f, 0.12f);

		private static readonly FieldInfo SubMenuButtonField = typeof(InteractionButtonsContainer).GetField("_subMenuButton", BindingFlags.Instance | BindingFlags.NonPublic);

		internal static CompoundItem CaseById(string id)
		{
			var controller = Game.Controller();
			return controller == null ? null : CasesStep.FindCases(controller.Inventory).FirstOrDefault(item => item.Id.ToString() == id);
		}

		// Called when the game makes the interactions of an item: adds our entry for a case you carry, while the stash is up
		internal static void Add(ItemContext itemContext, ContextInteractions<EItemInfoButton> interactions)
		{
			if (interactions == null || Plugin.ShowCaseMenu == null || !Plugin.ShowCaseMenu.Value || !ReadyButtonPatch.StashShown)
			{
				return;
			}

			var item = itemContext?.Item as CompoundItem;
			if (item == null || CaseById(item.Id.ToString()) == null)
			{
				return;
			}

			interactions.method_2(item.Id.ToString(), MenuKey, null, ButtonIcon.Get());
		}

		// Our button, instead of the game's plain dynamic one: with the arrow, and the hover opening the submenu
		internal static void CreateTop(InteractionButtonsContainer container, DynamicContextInteraction interaction)
		{
			var item = CaseById(interaction.Id);
			if (item == null)
			{
				return;
			}

			SimpleContextMenuButton button = null;
			Action open = () => Open(container, button, item);
			button = container.CreateContextButton(interaction.Key, interaction.Key, container._buttonTemplate, container._buttonsContainer, interaction.Icon, open, open, true, false);
			button.SetButtonInteraction(SuccessfulResult.New);
			container.BindButton(button);
			// Above the last button (Discard, when it is there), not under it
			var rect = button.Transform;
			if (rect.parent != null && rect.parent.childCount >= 2)
			{
				rect.SetSiblingIndex(rect.parent.childCount - 2);
			}
		}

		private static void Open(InteractionButtonsContainer container, SimpleContextMenuButton button, CompoundItem item)
		{
			SubMenuButtonField?.SetValue(container, button);
			container.CloseSubMenu();
			container.SetSubInteractions(new CaseActions(ItemUiContext.Instance, item));
		}

		private static readonly FieldInfo SubMenuField = typeof(InteractionButtonsContainer).GetField("_subMenu", BindingFlags.Instance | BindingFlags.NonPublic);

		// The game puts a submenu to the right of its button, then asks whether placing it moved it at all (it rounds the position to whole
		// pixels, so a right edge that is not a whole pixel counts as "moved"); if so it puts the menu on the left. With our menus, whose width
		// follows their text, the edge is rarely whole, so they flipped left with plenty of room. After the game has placed one, put it back on
		// the right when it fits there.
		internal static void KeepRight(InteractionButtonsContainer container)
		{
			var sub = SubMenuField?.GetValue(container) as SimpleContextMenu;
			var button = SubMenuButtonField?.GetValue(container) as SimpleContextMenuButton;
			if (sub == null || button == null)
			{
				return;
			}

			var buttonRect = (RectTransform)button.transform;
			var size = buttonRect.rect.size;
			float rightEdge = buttonRect.TransformPoint(size - size * buttonRect.pivot).x;
			var rect = (RectTransform)sub.transform;
			float width = rect.rect.width * rect.lossyScale.x;
			float left = rect.position.x - rect.pivot.x * width;
			if (Mathf.Abs(left - rightEdge) < 2f * rect.lossyScale.x)
			{
				return;
			}

			var canvas = sub.GetComponentInParent<Canvas>();
			var root = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
			if (root == null)
			{
				return;
			}

			var corners = new Vector3[4];
			root.GetWorldCorners(corners);
			if (rightEdge + width > corners[2].x)
			{
				return;
			}

			var position = rect.position;
			rect.position = new Vector3(Mathf.Round(rightEdge + rect.pivot.x * width), position.y, position.z);
		}

		internal static Sprite IconOf(string name)
		{
			try
			{
				return ResourcesCache.Pop<Sprite>("Characteristics/Icons/" + name);
			}
			catch
			{
				return null;
			}
		}

		// The colour of our buttons, by the key they are made with
		internal static void Colour(SimpleContextMenuButton button, string key)
		{
			Color normal, hover;
			if (key == MenuKey)
			{
				normal = Purple;
				hover = PurpleHover;
			}
			else if (key == nameof(ECaseAction.StopTracking))
			{
				normal = Red;
				hover = RedHover;
			}
			else
			{
				return;
			}

			if (button == null || button.SingleInstance)
			{
				return;
			}

			button._defaultBackgroundColor = normal;
			button._selectedBackgroundColor = hover;
			if (button._backgroundImage != null)
			{
				button._backgroundImage.color = normal;
			}
		}
	}

	// The submenu of the case: track or update, stop tracking, and the maps
	internal class CaseActions : ContextInteractions<ECaseAction>
	{
		private readonly CompoundItem _item;
		private readonly string _id;

		internal CaseActions(ItemUiContext uiContext, CompoundItem item) : base(uiContext)
		{
			_item = item;
			_id = item.Id.ToString();
		}

		private CaseEntry Entry => CasesStep.EntryOf(_id);

		private bool Tracked => Entry != null && Entry.Recorded;

		public override IEnumerable<ECaseAction> SubInteractions => new[] { ECaseAction.Maps };

		public override Dictionary<ECaseAction, string> CustomNames => new Dictionary<ECaseAction, string>
		{
			{ ECaseAction.Track, "Track items inside" },
			{ ECaseAction.Update, "Update template" },
			{ ECaseAction.StopTracking, "Stop tracking items" },
			{ ECaseAction.Maps, "Maps" }
		};

		public override bool TryGetCustomIcon(ECaseAction key, out Sprite icon)
		{
			icon = CaseMenu.IconOf(key == ECaseAction.Track ? "Tag" : key == ECaseAction.Update ? "TopUp" : key == ECaseAction.StopTracking ? "Discard" : "ViewMap");
			return icon != null;
		}

		public override bool IsActive(ECaseAction button)
		{
			switch (button)
			{
				case ECaseAction.Track:
					return !Tracked;
				case ECaseAction.Update:
				case ECaseAction.StopTracking:
					return Tracked;
				default:
					return true;
			}
		}

		// Update is greyed out when nothing changed since the case was tracked, like its button in Setup
		public override IResult IsInteractive(ECaseAction button)
		{
			if (button == ECaseAction.Update && Entry != null)
			{
				var change = CasesStep.Diff(_item, Entry);
				if (change != null && !change.Any)
				{
					return new FailedResult("Nothing has changed since it was tracked", 0);
				}
			}

			return SuccessfulResult.New;
		}

		public override void CreateSubInteractions(ECaseAction parentInteraction, ISubInteractionsWrapper wrapper)
		{
			wrapper.SetSubInteractions(new MapActions(UIContext, _item));
		}

		public override void ExecuteInteractionInternal(ECaseAction interaction)
		{
			try
			{
				switch (interaction)
				{
					case ECaseAction.Track:
					case ECaseAction.Update:
					{
						var saved = CasesStep.Record(_item);
						ReadyPlan._status = $"Tracking {saved.Items.Count} item(s) in {Game.NameOf(_item)}.";
						break;
					}
					case ECaseAction.StopTracking:
						CasesStep.Forget(_id);
						ReadyPlan._status = $"Stopped tracking items in {Game.NameOf(_item)}.";
						break;
					default:
						return;
				}

				SetupPage.Changed();
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Case menu failed: {e}");
			}
		}
	}

	// The maps of a case: each switches on or off, the ones that are on carry a mark
	internal class MapActions : ContextInteractions<EMapAction>
	{
		private readonly CompoundItem _item;
		private readonly string _id;

		internal MapActions(ItemUiContext uiContext, CompoundItem item) : base(uiContext)
		{
			_item = item;
			_id = item.Id.ToString();
		}

		private static string NameOf(EMapAction map) => MapTags.Maps[(int)map].Name;

		private bool IsOn(EMapAction map) => CasesStep.EntryOf(_id)?.Maps.Contains(NameOf(map)) == true;

		// The maps the case is for: the game's own filled and outlined heart icons (filled: on), and the name in the colour of the Get ready button
		public override Dictionary<EMapAction, string> CustomNames =>
			Enum.GetValues(typeof(EMapAction)).Cast<EMapAction>().ToDictionary(map => map, map => IsOn(map) ? "<color=#c9a0ff>" + NameOf(map) + "</color>" : NameOf(map));

		public override bool TryGetCustomIcon(EMapAction key, out Sprite icon)
		{
			icon = CaseMenu.IconOf(IsOn(key) ? "AddToFavorites" : "RemoveFromFavorites");
			return icon != null;
		}

		public override bool IsActive(EMapAction button) => (int)button < MapTags.Maps.Length;

		public override IResult IsInteractive(EMapAction button) => SuccessfulResult.New;

		public override void ExecuteInteractionInternal(EMapAction interaction)
		{
			try
			{
				string name = NameOf(interaction);
				var target = CasesStep.EntryOf(_id) ?? CasesStep.Touch(_item);
				if (!target.Maps.Remove(name))
				{
					target.Maps.Add(name);
				}

				CasesStep.Prune(target);
				SetupPage.Changed();
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Case map menu failed: {e}");
			}
		}
	}

	internal class CaseMenuAddPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(ItemUiContext).GetMethod(nameof(ItemUiContext.GetItemContextInteractions), BindingFlags.Instance | BindingFlags.Public);

		[PatchPostfix]
		private static void Postfix(ItemContext itemContext, ContextInteractions<EItemInfoButton> __result)
		{
			try
			{
				CaseMenu.Add(itemContext, __result);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Case menu entry failed: {e}");
			}
		}
	}

	internal class CaseMenuButtonPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(InteractionButtonsContainer).GetMethod(nameof(InteractionButtonsContainer.CreateDynamicContextButton), BindingFlags.Instance | BindingFlags.Public);

		[PatchPrefix]
		private static bool Prefix(InteractionButtonsContainer __instance, DynamicContextInteraction interaction)
		{
			if (interaction == null || interaction.Key != CaseMenu.MenuKey)
			{
				return true;
			}

			try
			{
				CaseMenu.CreateTop(__instance, interaction);
				return false;
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Case menu button failed: {e}");
				return true;
			}
		}
	}

	// After the game has opened one of our submenus: keep it on the right when there is room (see CaseMenu.KeepRight)
	internal class CaseMenuSidePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(InteractionButtonsContainer).GetMethod(nameof(InteractionButtonsContainer.SetSubInteractions), BindingFlags.Instance | BindingFlags.Public).MakeGenericMethod(typeof(ECaseAction));

		[PatchPostfix]
		private static void Postfix(InteractionButtonsContainer __instance) => Run(__instance);

		internal static void Run(InteractionButtonsContainer container)
		{
			try
			{
				CaseMenu.KeepRight(container);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Case menu side failed: {e}");
			}
		}
	}

	internal class MapMenuSidePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(InteractionButtonsContainer).GetMethod(nameof(InteractionButtonsContainer.SetSubInteractions), BindingFlags.Instance | BindingFlags.Public).MakeGenericMethod(typeof(EMapAction));

		[PatchPostfix]
		private static void Postfix(InteractionButtonsContainer __instance) => CaseMenuSidePatch.Run(__instance);
	}

	// Every button the menus make passes here: ours are coloured by their key
	internal class CaseMenuColourPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(InteractionButtonsContainer).GetMethod(nameof(InteractionButtonsContainer.CreateContextButton), BindingFlags.Instance | BindingFlags.Public, null,
			new[] { typeof(string), typeof(string), typeof(SimpleContextMenuButton), typeof(RectTransform), typeof(Sprite), typeof(Action), typeof(Action), typeof(bool), typeof(bool) }, null);

		[PatchPostfix]
		private static void Postfix(string key, SimpleContextMenuButton __result)
		{
			try
			{
				CaseMenu.Colour(__result, key);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Case menu colour failed: {e}");
			}
		}
	}
}
