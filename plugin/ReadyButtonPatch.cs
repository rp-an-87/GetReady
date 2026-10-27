using System;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using SPT.Reflection.Patching;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GetReady
{
	// Adds a "Get ready" button above the pin button of the player's stash (hideout/menu only). It is a clone of the stash's
	// Meds filter tab, so it has the tab look, icon and hover; the clone isn't part of the tab group, so it only clicks.
	internal class ReadyButtonPatch : ModulePatch
	{
		private const string ButtonName = "GetReady Button";

		// The stash button: it is only active while the stash screen is up, so it tells whether the window still has a stash
		internal static GameObject Button;
		internal static bool StashShown => Button != null && Button.activeInHierarchy;

		private static GameObject _badge;
		private static Image _badgeImage;
		private static Text _badgeText;

		// A small count on the button: red with the number of problems, else gold with the number of things to bring
		internal static void SetBadge(int problems, int todo)
		{
			if (_badge == null)
			{
				return;
			}

			int count = problems > 0 ? problems : todo;
			_badge.SetActive(count > 0);
			_badgeImage.color = problems > 0 ? new Color(0.78f, 0.2f, 0.17f) : new Color(0.62f, 0.52f, 0.2f);
			_badgeText.text = count.ToString();
		}

		private static void AddBadge(Transform button)
		{
			_badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
			_badge.transform.SetParent(button, false);
			var rect = (RectTransform)_badge.transform;
			rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
			rect.sizeDelta = new Vector2(20f, 20f);
			rect.anchoredPosition = new Vector2(3f, 3f);
			_badgeImage = _badge.GetComponent<Image>();
			_badgeImage.raycastTarget = false;

			var label = new GameObject("Count", typeof(RectTransform), typeof(Text));
			label.transform.SetParent(_badge.transform, false);
			var labelRect = (RectTransform)label.transform;
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
			_badgeText = label.GetComponent<Text>();
			_badgeText.raycastTarget = false;
			_badgeText.alignment = TextAnchor.MiddleCenter;
			_badgeText.fontSize = 13;
			_badgeText.fontStyle = FontStyle.Bold;
			_badgeText.color = Color.white;
			_badgeText.font = Font.CreateDynamicFontFromOSFont("Arial", 13);
			_badge.SetActive(false);
		}
		private const int MedsTabIndex = 4; // order of FilterPanel's filter rules: All, Weapon, Magazine, Ammo, Meds

		protected override MethodBase GetTargetMethod() => typeof(SimpleStashPanel).GetMethod(nameof(SimpleStashPanel.Show), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		[PatchPostfix]
		private static void Postfix(SimpleStashPanel __instance, CompoundItem item, InventoryController inventoryController, bool inRaid)
		{
			try
			{
				var pin = __instance._pinModeButton;
				if (pin == null || pin.transform.parent == null)
				{
					return;
				}

				var existing = pin.transform.parent.Find(ButtonName);
				bool isPlayerStash = !inRaid && item is Stash && inventoryController?.Inventory?.Stash == item;
				bool show = isPlayerStash && Plugin.ShowButton.Value;
				if (isPlayerStash)
				{
					// The controller changes after a raid or a profile reload, and the window works with the latest one
					Game.StashController = inventoryController;
				}

				if (existing != null)
				{
					Button = existing.gameObject;
					existing.gameObject.SetActive(show);
					return;
				}

				var tabs = __instance._filterPanel?._filterTabs;
				if (!show || tabs == null || tabs.Length <= MedsTabIndex || tabs[MedsTabIndex] == null)
				{
					return;
				}

				var clone = UnityEngine.Object.Instantiate(tabs[MedsTabIndex].gameObject, pin.transform.parent, false);
				clone.name = ButtonName;
				Button = clone;
				AddBadge(clone.transform);
				clone.GetComponent<Tab>().SetInteractable(true);
				var icon = clone.GetComponent<FilterTab>()._filterIcon;
				icon.sprite = Icons.Button();
				icon.color = new Color(0.45f, 0.7f, 0.95f, icon.color.a);
				Place(clone.transform, pin.transform);

				var click = clone.AddComponent<ClickHandler>();
				click.OnClick = () =>
				{
					if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
					{
						ReadyWindow.Toggle();
					}
					else
					{
						ReadyWindow.QuickRun();
					}
				};

				var hover = clone.GetComponent<HoverTrigger>() ?? clone.AddComponent<HoverTrigger>();
				hover.OnHoverStart += _ => ItemUiContext.Instance.Tooltip.Show(ReadyWindow.Summary(), null, 0.25f, null);
				hover.OnHoverEnd += _ => ItemUiContext.Instance.Tooltip.Close();
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Get ready button failed: {e}");
			}
		}

		// Put the clone right before the pin button in a layout group, or one button height above it otherwise
		private static void Place(Transform clone, Transform pin)
		{
			var parent = pin.parent;
			if (parent.GetComponent<LayoutGroup>() != null)
			{
				clone.SetSiblingIndex(pin.GetSiblingIndex());
				return;
			}

			var cloneRect = (RectTransform)clone;
			var pinRect = (RectTransform)pin;
			cloneRect.anchoredPosition = pinRect.anchoredPosition + new Vector2(0f, pinRect.rect.height + 4f);
		}

		internal class ClickHandler : MonoBehaviour, IPointerClickHandler
		{
			internal Action OnClick;

			public void OnPointerClick(PointerEventData eventData) => OnClick?.Invoke();
		}
	}
}
