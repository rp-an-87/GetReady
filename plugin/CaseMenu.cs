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
	internal enum ECaseAction { Maps, Track, Update, StopTracking, Settings, Repair, TopUp }

	// The switches of a case in the submenu "Case settings"
	internal enum ECaseSetting { SkipCash, TakeOutRest, Exact }

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

		// The items our entry was added to, by id (the game hands the entry's id back when it makes the button), and whether each is a case
		private static readonly Dictionary<string, KeyValuePair<Item, bool>> Known = new Dictionary<string, KeyValuePair<Item, bool>>();

		// Whether there is anything to offer on this item: the submenu has an entry that is on
		internal static bool Offers(Item item, bool isCase)
		{
			var actions = new CaseActions(null, item, isCase);
			return Enum.GetValues(typeof(ECaseAction)).Cast<ECaseAction>().Any(action => actions.IsActive(action));
		}

		// Called when the game makes the interactions of an item: adds our entry when there is something to offer on it (a case you carry, armor
		// or a weapon, an ammo stack), while the stash is up
		internal static void Add(ItemContext itemContext, ContextInteractions<EItemInfoButton> interactions)
		{
			if (interactions == null || Plugin.ShowCaseMenu == null || !Plugin.ShowCaseMenu.Value || !ReadyButtonPatch.StashShown)
			{
				return;
			}

			var item = itemContext?.Item;
			if (item == null)
			{
				return;
			}

			string id = item.Id.ToString();
			bool isCase = item is CompoundItem && CaseById(id) != null;
			if (!Offers(item, isCase))
			{
				return;
			}

			Known[id] = new KeyValuePair<Item, bool>(item, isCase);
			interactions.method_2(id, MenuKey, null, ButtonIcon.Get());
		}

		// Our button, instead of the game's plain dynamic one: with the arrow, and the hover opening the submenu
		internal static void CreateTop(InteractionButtonsContainer container, DynamicContextInteraction interaction)
		{
			KeyValuePair<Item, bool> known;
			if (!Known.TryGetValue(interaction.Id, out known))
			{
				return;
			}

			var item = known.Key;
			bool isCase = known.Value;
			SimpleContextMenuButton button = null;
			Action open = () => Open(container, button, item, isCase);
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

		private static void Open(InteractionButtonsContainer container, SimpleContextMenuButton button, Item item, bool isCase)
		{
			SubMenuButtonField?.SetValue(container, button);
			container.CloseSubMenu();
			container.SetSubInteractions(new CaseActions(ItemUiContext.Instance, item, isCase));
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

		// A notification (it has a sound of its own, so none is added with it) or a click for what an entry did; the entry's work is done either way
		internal static void Feedback(string text, EUISoundType? sound)
		{
			try
			{
				ReadyRun.Say(text, sound);
			}
			catch (Exception e)
			{
				Plugin.Log?.LogWarning($"No feedback: {e.Message}");
			}
		}

		internal static SimpleContextMenu SubMenuOf(InteractionButtonsContainer container) => SubMenuField?.GetValue(container) as SimpleContextMenu;

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

	// The submenu of the "Get ready" entry: for a case, track or update, stop tracking, the maps and its settings; for armor or a weapon, leave it
	// out of the repairs; for an ammo stack, leave its kind out of the filling. What is on depends on the item and on what is switched on in Setup.
	internal class CaseActions : ContextInteractions<ECaseAction>
	{
		private readonly Item _item;
		private readonly CompoundItem _case;
		private readonly string _id;

		internal CaseActions(ItemUiContext uiContext, Item item, bool isCase = true) : base(uiContext)
		{
			_item = item;
			_case = isCase ? item as CompoundItem : null;
			_id = item.Id.ToString();
		}

		private bool CasesOn => _case != null && SettingsStore.Current.Cases.Enabled;

		private CaseEntry Entry => CasesStep.EntryOf(_id);

		private bool Tracked => Entry != null && Entry.Recorded;

		public override IEnumerable<ECaseAction> SubInteractions => new[] { ECaseAction.Maps, ECaseAction.Settings };

		public override Dictionary<ECaseAction, string> CustomNames => new Dictionary<ECaseAction, string>
		{
			{ ECaseAction.Track, "Track items inside" },
			{ ECaseAction.Update, "Update template" },
			{ ECaseAction.StopTracking, "Stop tracking items" },
			{ ECaseAction.Maps, "Maps" },
			{ ECaseAction.Settings, "Case settings" },
			{ ECaseAction.Repair, RepairStep.IsKept(_item) ? "Include in repairs" : "Exclude from repairs" },
			{ ECaseAction.TopUp, AmmoStep.IsTopUpExcluded(_item.TemplateId.ToString()) ? "Include in top-up" : "Leave out of top-up" }
		};

		public override bool TryGetCustomIcon(ECaseAction key, out Sprite icon)
		{
			string name;
			switch (key)
			{
				case ECaseAction.Track: name = "Tag"; break;
				case ECaseAction.Update: name = "TopUp"; break;
				case ECaseAction.StopTracking: name = "Discard"; break;
				case ECaseAction.Settings: name = "Modding"; break;
				case ECaseAction.Repair: name = "Repair"; break;
				case ECaseAction.TopUp: name = "TopUp"; break;
				default: name = "ViewMap"; break;
			}

			icon = CaseMenu.IconOf(name);
			return icon != null;
		}

		public override bool IsActive(ECaseAction button)
		{
			switch (button)
			{
				case ECaseAction.Track:
					return CasesOn && !Tracked;
				case ECaseAction.Update:
				case ECaseAction.StopTracking:
					return CasesOn && Tracked;
				case ECaseAction.Repair:
					return RepairStep.OffersExclusion(_item);
				case ECaseAction.TopUp:
					return AmmoStep.OffersTopUpExclusion(_item);
				default:
					return CasesOn;
			}
		}

		// Update is greyed out when nothing changed since the case was tracked, like its button in Setup
		public override IResult IsInteractive(ECaseAction button)
		{
			if (button == ECaseAction.Update && Entry != null && _case != null)
			{
				var change = CasesStep.Diff(_case, Entry);
				if (change != null && !change.Any)
				{
					return new FailedResult("Nothing has changed since it was tracked", 0);
				}
			}

			return SuccessfulResult.New;
		}

		public override void CreateSubInteractions(ECaseAction parentInteraction, ISubInteractionsWrapper wrapper)
		{
			if (_case == null)
			{
				return;
			}

			if (parentInteraction == ECaseAction.Settings)
			{
				wrapper.SetSubInteractions(new SettingActions(UIContext, _case));
			}
			else
			{
				wrapper.SetSubInteractions(new MapActions(UIContext, _case));
			}
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
						var saved = CasesStep.Record(_case);
						ReadyPlan._status = $"Tracking {saved.Items.Count} item(s) in {Game.NameOf(_case)}.";
						CaseMenu.Feedback(ReadyPlan._status, null);
						break;
					}
					case ECaseAction.StopTracking:
						CasesStep.Forget(_id);
						ReadyPlan._status = $"Stopped tracking items in {Game.NameOf(_case)}.";
						CaseMenu.Feedback(ReadyPlan._status, null);
						break;
					case ECaseAction.Repair:
						RepairStep.ToggleKept(_item);
						CaseMenu.Feedback(RepairStep.IsKept(_item) ? $"{Game.NameOf(_item)} left out of the repairs." : $"{Game.NameOf(_item)} is repaired again.", null);
						break;
					case ECaseAction.TopUp:
						AmmoStep.ToggleTopUpExcluded(_item.TemplateId.ToString(), Game.NameOf(_item));
						CaseMenu.Feedback(AmmoStep.IsTopUpExcluded(_item.TemplateId.ToString()) ? $"{Game.NameOf(_item)} left out of the top-up." : $"{Game.NameOf(_item)} is topped up again.", null);
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

	// The switches of a case, in the submenu "Case settings": each switches on or off and the menu stays open. The ones that are on carry a mark.
	internal class SettingActions : ContextInteractions<ECaseSetting>
	{
		private readonly CompoundItem _item;
		private readonly string _id;
		// The menu reads the names once and keeps that dictionary: so it is one object, changed in place by Refresh()
		private readonly Dictionary<ECaseSetting, string> _names = new Dictionary<ECaseSetting, string>();

		internal SettingActions(ItemUiContext uiContext, CompoundItem item) : base(uiContext)
		{
			_item = item;
			_id = item.Id.ToString();
		}

		private CaseEntry Entry => CasesStep.EntryOf(_id);

		private bool Tracked => Entry != null && Entry.Recorded;

		private static string NameOf(ECaseSetting setting) => setting == ECaseSetting.SkipCash ? "Skip cash step" : setting == ECaseSetting.TakeOutRest ? "Take out the rest" : "Exact positions";

		private bool IsOn(ECaseSetting setting)
		{
			var entry = Entry;
			if (entry == null)
			{
				return false;
			}

			return setting == ECaseSetting.SkipCash ? entry.StopsCash : setting == ECaseSetting.TakeOutRest ? entry.TakeOutRest : entry.Exact;
		}

		public override Dictionary<ECaseSetting, string> CustomNames
		{
			get
			{
				Refresh();
				return _names;
			}
		}

		private void Refresh()
		{
			foreach (ECaseSetting setting in Enum.GetValues(typeof(ECaseSetting)))
			{
				_names[setting] = IsOn(setting) ? "<color=#c9a0ff>" + NameOf(setting) + "</color>" : NameOf(setting);
			}
		}

		public override bool TryGetCustomIcon(ECaseSetting key, out Sprite icon)
		{
			icon = CaseMenu.IconOf(IsOn(key) ? "AddToFavorites" : "RemoveFromFavorites");
			return icon != null;
		}

		// Like Setup: the cash switch only while the Cash step is on, the other two only for a case that is tracked
		public override bool IsActive(ECaseSetting button) => button == ECaseSetting.SkipCash ? SettingsStore.Current.Supplies.CashEnabled : Tracked;

		public override IResult IsInteractive(ECaseSetting button) => SuccessfulResult.New;

		public override void ExecuteInteractionInternal(ECaseSetting interaction)
		{
			try
			{
				if (interaction == ECaseSetting.SkipCash)
				{
					var target = Entry ?? CasesStep.Touch(_item);
					target.StopsCash = !target.StopsCash;
					CasesStep.Prune(target);
				}
				else if (Entry != null)
				{
					if (interaction == ECaseSetting.TakeOutRest)
					{
						Entry.TakeOutRest = !Entry.TakeOutRest;
					}
					else
					{
						Entry.Exact = !Entry.Exact;
					}
				}

				// The menu stays open (CaseMenuKeepOpen): have it draw the marks and names again
				Refresh();
				method_0();
				CaseMenu.Feedback(null, EUISoundType.ButtonClick);
				SetupPage.Changed();
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Case settings menu failed: {e}");
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

		// The maps the case is for: the game's own filled and outlined heart icons (filled: on), and the name in the colour of the Get ready button.
		// The menu reads the names once, when it opens, and keeps that dictionary: so it is one object, and Refresh() changes it in place.
		private readonly Dictionary<EMapAction, string> _names = new Dictionary<EMapAction, string>();

		private string Caption(EMapAction map) => IsOn(map) ? "<color=#c9a0ff>" + NameOf(map) + "</color>" : NameOf(map);

		public override Dictionary<EMapAction, string> CustomNames
		{
			get
			{
				Refresh();
				return _names;
			}
		}

		private void Refresh()
		{
			foreach (EMapAction map in Enum.GetValues(typeof(EMapAction)))
			{
				_names[map] = Caption(map);
			}
		}

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
				// The menu stays open (CaseMenuKeepOpen): have it draw the hearts and names again
				Refresh();
				method_0();
				CaseMenu.Feedback(null, EUISoundType.ButtonClick);
				SetupPage.Changed();
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Case map menu failed: {e}");
			}
		}
	}

	// The maps menu and the case settings menu stay open after a click, so several can be switched in a row. The game closes a menu in two places: every context menu
	// closes itself when a mouse button is released (SimpleContextMenu.Update), and a clicked button closes its container (autoClose). Both are
	// skipped while the pointer is over that menu; the menus above it (the item's, the case's) are skipped too, or they would close it with them.
	internal static class CaseMenuKeepOpen
	{
		private static readonly FieldInfo ContainerField = typeof(SimpleContextMenu).GetField("_interactionButtonsContainer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		// The last submenu on screen (the maps or the case settings), set when the game opens it (see MapMenuSidePatch, SettingMenuSidePatch)
		internal static SimpleContextMenu Leaf;

		internal static bool PointerOnLeaf()
		{
			if (Leaf == null || !Leaf.gameObject.activeInHierarchy)
			{
				return false;
			}

			var rect = (RectTransform)Leaf.transform;
			var canvas = Leaf.GetComponentInParent<Canvas>();
			var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
		}

		internal static bool IsLeafContainer(InteractionButtonsContainer container) => Leaf != null && container != null && ContainerField?.GetValue(Leaf) == container;
	}

	internal class CaseMenuKeepOpenUpdatePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(SimpleContextMenu).GetMethod(nameof(SimpleContextMenu.Update), BindingFlags.Instance | BindingFlags.Public);

		[PatchPrefix]
		private static bool Prefix()
		{
			try
			{
				if (CaseMenuKeepOpen.Leaf != null && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2)) && CaseMenuKeepOpen.PointerOnLeaf())
				{
					return false;
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Case menu keep-open failed: {e}");
			}

			return true;
		}
	}

	internal class CaseMenuKeepOpenClosePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(InteractionButtonsContainer).GetMethod(nameof(InteractionButtonsContainer.Close), BindingFlags.Instance | BindingFlags.Public);

		[PatchPrefix]
		private static bool Prefix(InteractionButtonsContainer __instance) => !(CaseMenuKeepOpen.IsLeafContainer(__instance) && CaseMenuKeepOpen.PointerOnLeaf());
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
		private static void Postfix(InteractionButtonsContainer __instance)
		{
			CaseMenuKeepOpen.Leaf = CaseMenu.SubMenuOf(__instance);
			CaseMenuSidePatch.Run(__instance);
		}
	}

	internal class SettingMenuSidePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(InteractionButtonsContainer).GetMethod(nameof(InteractionButtonsContainer.SetSubInteractions), BindingFlags.Instance | BindingFlags.Public).MakeGenericMethod(typeof(ECaseSetting));

		[PatchPostfix]
		private static void Postfix(InteractionButtonsContainer __instance)
		{
			CaseMenuKeepOpen.Leaf = CaseMenu.SubMenuOf(__instance);
			CaseMenuSidePatch.Run(__instance);
		}
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
