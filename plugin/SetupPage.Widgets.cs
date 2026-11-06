using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GetReady
{
	// Pieces the sections of the Setup page share: a number with - and +, a row of choices, and the places picker (chips in the order they are
	// tried, clicked to switch and dragged onto each other to swap).
	internal static partial class SetupPage
	{
		private static readonly Color ButtonFill = new Color(0.2f, 0.21f, 0.21f), ButtonHover = new Color(0.27f, 0.28f, 0.27f);
		private static readonly Color PickFill = new Color(0.15f, 0.155f, 0.155f), PickHover = new Color(0.22f, 0.23f, 0.22f);
		private static readonly Color PickOnFill = new Color(0.25f, 0.23f, 0.15f), PickOnHover = new Color(0.31f, 0.29f, 0.19f);
		private static readonly Color PickOnText = new Color(0.95f, 0.9f, 0.72f);

		// The thin edges of the buttons: grey, and sand for the one that is lit
		internal static readonly Color ButtonEdge = new Color(0.38f, 0.39f, 0.37f), PickEdge = new Color(0.3f, 0.31f, 0.3f), PickOnEdge = new Color(0.72f, 0.65f, 0.42f);

		internal static readonly Color InnerPanel = new Color(0.095f, 0.1f, 0.1f);

		// The icon of an item template: its picture once the game has made it (the page is drawn again until then), or a "?" for one you have
		// not inspected yet, as the stash shows it
		internal static void TemplateIcon(Transform parent, string template, float size, string tip)
		{
			var sprite = Icons.Of(template);
			bool unknown = sprite == null && !Icons.IsExamined(template);
			if (sprite == null && !unknown)
			{
				ReadyUiWindow.IconPending(() => Icons.Of(template) != null || !Icons.IsExamined(template));
			}

			UiKit.Icon(parent, sprite, size, tip);
			if (unknown)
			{
				var mark = UiKit.Label(parent.GetChild(parent.childCount - 1), "?", size / Mathf.Max(UiKit.Unit, 1f) * 0.45f, UiColors.Dim, align: TextAlignmentOptions.Center);
				UiKit.Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);
			}
		}

		internal static void ItemIcon(Transform parent, EFT.InventoryLogic.Item item, float size, string tip)
		{
			var sprite = Icons.OfItem(item);
			if (sprite == null)
			{
				ReadyUiWindow.IconPending(() => Icons.OfItem(item) != null);
			}

			UiKit.Icon(parent, sprite, size, tip);
		}

		// - text +
		internal static void Stepper(Transform parent, string text, Color color, float width, Action minus, Action plus, string minusTip, string plusTip)
		{
			var less = UiKit.PushButton(parent, "-", minus, ButtonFill, ButtonHover, UiColors.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
			UiKit.Tip(less, minusTip);
			var label = UiKit.Label(parent, text, 0.9f, color, align: TextAlignmentOptions.Center);
			UiKit.Size(label, UiKit.Unit * width);
			var more = UiKit.PushButton(parent, "+", plus, ButtonFill, ButtonHover, UiColors.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
			UiKit.Tip(more, plusTip);
		}

		// One choice of a row: lit when it is the one chosen
		internal static void Pick(Transform parent, string text, bool on, Action choose, string tip, float width = -1f)
		{
			var button = UiKit.PushButton(parent, text, choose, on ? PickOnFill : PickFill, on ? PickOnHover : PickHover, on ? PickOnText : UiColors.Text, width: width, height: UiKit.Unit * 1.7f, edge: on ? PickOnEdge : PickEdge);
			UiKit.Tip(button, tip);
		}

		internal class ChipData
		{
			public string Key;
			public string Label;
			public bool On;
			public string Tip;
			// A picture instead of the name (the name is in the tooltip); null shows the name
			public Sprite Icon;
		}

		// Chips in the order of use: where the first that has room wins. A ">" between the ones that are on shows the order; one that is off is
		// at the end with a red cross. Click switches a chip, drag it onto another to swap them (and with them on or off).
		internal static void ChipPicker(Transform parent, IList<ChipData> chips, int perRow, Action<string> toggle, Action<string, string> swap)
		{
			var column = UiKit.VBox(parent, 6f);
			var made = new List<SwapChip>();
			RectTransform row = null;
			for (int i = 0; i < chips.Count; i++)
			{
				if (i % perRow == 0)
				{
					row = UiKit.HBox(column, 4f);
				}

				var chip = chips[i];
				bool afterOn = i % perRow > 0 && chips[i - 1].On;
				if (chip.On && afterOn)
				{
					UiKit.Label(row, ">", 0.9f, UiColors.Dim, align: TextAlignmentOptions.Center);
				}
				else if (!chip.On && afterOn)
				{
					UiKit.Gap(row, UiKit.Unit * 1.2f);
				}

				string key = chip.Key;
				SwapChip swapChip = null;
				var button = UiKit.PushButton(row, chip.Icon != null ? "" : chip.On ? chip.Label : "× " + chip.Label, () =>
				{
					if (swapChip == null || !swapChip.JustDragged)
					{
						toggle(key);
					}
				}, chip.On ? PickOnFill : PickFill, chip.On ? PickOnHover : PickHover, chip.On ? PickOnText : UiColors.Dim, width: UiKit.Unit * (chip.Icon != null ? 2.7f : 6.5f), height: UiKit.Unit * (chip.Icon != null ? 2f : 1.7f), edge: chip.On ? PickOnEdge : PickEdge);
				if (chip.Icon != null)
				{
					ChipPicture(button.transform, chip);
				}

				UiKit.Tip(button, chip.Tip);
				swapChip = button.gameObject.AddComponent<SwapChip>();
				swapChip.Key = key;
				swapChip.Dots = UiKit.DottedFrame((RectTransform)button.transform, new Color(0.8f, 0.72f, 0.45f));
				swapChip.Peers = made;
				swapChip.OnSwap = (from, to) =>
				{
					swap(from, to);
					Changed();
				};
				made.Add(swapChip);
			}
		}

		private static Sprite PlaceIconOf(string place)
		{
			var sprite = PlaceIcons.Of(place);
			if (sprite == null && PlaceIcons.Pending(place))
			{
				ReadyUiWindow.IconPending(() => !PlaceIcons.Pending(place));
			}

			return sprite;
		}

		// The picture on a chip: bright when the place is used, dim with a small red cross when it is not
		private static void ChipPicture(Transform button, ChipData chip)
		{
			var glyph = UiKit.Node("Glyph", button);
			glyph.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
			UiKit.Stretch(glyph, 5f, 3f, 5f, 3f);
			var image = glyph.gameObject.AddComponent<UnityEngine.UI.Image>();
			image.sprite = chip.Icon;
			image.preserveAspect = true;
			image.raycastTarget = false;
			image.color = chip.On ? new Color(0.95f, 0.9f, 0.72f) : new Color(0.62f, 0.63f, 0.6f, 0.5f);
			if (!chip.On)
			{
				var cross = UiKit.Label(button, "×", 0.9f, new Color(0.86f, 0.3f, 0.27f), true, TextAlignmentOptions.TopRight);
				cross.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
				UiKit.Stretch(cross.rectTransform, 0f, 0f, 3f, 0f);
			}
		}

		// "Bring to" in a dark panel: its heading, then every place as a chip in one row
		internal static RectTransform PlacesPanel(Transform parent, GroupDef def)
		{
			var panel = UiKit.VBox(parent, 6f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge);
			HelpTitle(panel, "Bring to", "Each item goes to the first place in the row that has room, tried from left to right. Drag a place onto another to swap them. Click a place to switch it off (a red cross, at the end, never used).", true);
			PlacePicker(panel, def, 99);
			return panel;
		}

		// The places of a group as chips
		internal static void PlacePicker(Transform parent, GroupDef def, int perRow)
		{
			var chips = def.PlaceOrder().Select(place => new ChipData
			{
				Key = place,
				Label = place == "Special slots" ? "Special" : place,
				On = def.PlaceOn(place),
				Icon = PlaceIconOf(place),
				Tip = place + "\n" + (def.PlaceOn(place) ? "<b>On:</b> used, in the order of the arrows.\nClick to switch it off, drag it onto another place to swap them." : "<b>Off:</b> not used.\nClick to switch it on, or drag it onto a place that is on to take its spot.")
			}).ToList();
			ChipPicker(parent, chips, perRow, place =>
			{
				def.TogglePlace(place);
				Changed();
			}, (place, other) => def.SwapPlaces(place, other));
		}

		// What is said where a list has nothing: in the middle of its width, with some room round it, so it is not mistaken for a row
		// The small heading that sets the items that are not used apart from the ones that are
		internal static void NotUsedHeading(Transform box)
		{
			var heading = UiKit.HBox(box, 0f, new RectOffset(0, 0, 8, 2));
			UiKit.Label(heading, "NOT USED", 0.75f, UiColors.Dim, bold: true);
		}

		// One step of an amount's target, down or up, kept between 0 and the group's most
		internal static void StepTarget(GroupDef def, int direction)
		{
			var setting = def.Setting;
			int target = Math.Max(0, Math.Min(def.Max, setting.Target + direction * def.Step));
			if (target != setting.Target)
			{
				setting.Target = target;
				Changed();
			}
		}

		// The Used / Not used button at the end of an item's row (a not used row fades but for it)
		internal static void UsedPick(RectTransform row, GroupDef def, string template, bool off)
		{
			Pick(row, off ? "Not used" : "Used", !off, () =>
			{
				def.ToggleEntry(template);
				Changed();
			}, null, UiKit.Unit * 6f);
			if (off)
			{
				DimUnused(row);
			}
		}

		// A row of an item that is not used fades, but for its last part (the Used / Not used button), which stays clear to click
		internal static void DimUnused(RectTransform row)
		{
			for (int i = 0; i < row.childCount - 1; i++)
			{
				var part = row.GetChild(i).gameObject;
				(part.GetComponent<CanvasGroup>() ?? part.AddComponent<CanvasGroup>()).alpha = 0.3f;
			}
		}

		internal static void EmptyNote(Transform parent, string text)
		{
			var row = UiKit.HBox(parent, 0f, new RectOffset(4, 4, 8, 8));
			UiKit.Label(row, text, 0.85f, UiColors.Dim, align: TMPro.TextAlignmentOptions.Center, wrap: true, flex: true);
		}

		// A panel with two sections that are always there: what is on you (equipped), and what is saved or configured but not on you
		internal static void TwoSections(Transform parent, string onLabel, string onEmpty, bool anyOn, Action<Transform> drawOn, string awayLabel, string awayEmpty, bool anyAway, Action<Transform> drawAway)
		{
			var box = UiKit.VBox(parent, 4f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge);
			UiKit.Label(box, onLabel, 0.8f, UiColors.Dim);
			if (anyOn)
			{
				drawOn(box);
			}
			else
			{
				EmptyNote(box, onEmpty);
			}

			UiKit.Line(box);
			UiKit.Label(box, awayLabel, 0.8f, UiColors.Dim);
			if (anyAway)
			{
				drawAway(box);
			}
			else
			{
				EmptyNote(box, awayEmpty);
			}
		}
	}
}
