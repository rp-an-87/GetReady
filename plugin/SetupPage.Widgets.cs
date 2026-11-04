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

		// The thin edges of the old window's buttons: grey, and sand for the one that is lit
		internal static readonly Color ButtonEdge = new Color(0.38f, 0.39f, 0.37f), PickEdge = new Color(0.3f, 0.31f, 0.3f), PickOnEdge = new Color(0.72f, 0.65f, 0.42f);

		internal static readonly Color InnerPanel = new Color(0.095f, 0.1f, 0.1f);

		// - text +
		internal static void Stepper(Transform parent, string text, Color color, float width, Action minus, Action plus, string minusTip, string plusTip)
		{
			var less = UiKit.PushButton(parent, "-", minus, ButtonFill, ButtonHover, Ui.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
			UiKit.Tip(less, minusTip);
			var label = UiKit.Label(parent, text, 0.9f, color, align: TextAlignmentOptions.Center);
			UiKit.Size(label, UiKit.Unit * width);
			var more = UiKit.PushButton(parent, "+", plus, ButtonFill, ButtonHover, Ui.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
			UiKit.Tip(more, plusTip);
		}

		// One choice of a row: lit when it is the one chosen
		internal static void Pick(Transform parent, string text, bool on, Action choose, string tip, float width = -1f)
		{
			var button = UiKit.PushButton(parent, text, choose, on ? PickOnFill : PickFill, on ? PickOnHover : PickHover, on ? PickOnText : Ui.Text, width: width, height: UiKit.Unit * 1.7f, edge: on ? PickOnEdge : PickEdge);
			UiKit.Tip(button, tip);
		}

		internal class ChipData
		{
			public string Key;
			public string Label;
			public bool On;
			public string Tip;
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
					UiKit.Label(row, ">", 0.9f, Ui.Dim, align: TextAlignmentOptions.Center);
				}
				else if (!chip.On && afterOn)
				{
					UiKit.Gap(row, UiKit.Unit * 1.2f);
				}

				string key = chip.Key;
				SwapChip swapChip = null;
				var button = UiKit.PushButton(row, chip.On ? chip.Label : "× " + chip.Label, () =>
				{
					if (swapChip == null || !swapChip.JustDragged)
					{
						toggle(key);
					}
				}, chip.On ? PickOnFill : PickFill, chip.On ? PickOnHover : PickHover, chip.On ? PickOnText : Ui.Dim, width: UiKit.Unit * 6.5f, height: UiKit.Unit * 1.7f, edge: chip.On ? PickOnEdge : PickEdge);
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

		// The places of a group as chips
		internal static void PlacePicker(Transform parent, GroupDef def, int perRow)
		{
			var chips = def.PlaceOrder().Select(place => new ChipData
			{
				Key = place,
				Label = place == "Special slots" ? "Special" : place,
				On = def.PlaceOn(place),
				Tip = def.PlaceOn(place) ? "On: used, in the order of the arrows. Click to switch it off, drag it onto another place to swap them." : "Off: not used. Click to switch it on, or drag it onto a place that is on to take its spot."
			}).ToList();
			ChipPicker(parent, chips, perRow, place =>
			{
				def.TogglePlace(place);
				Changed();
			}, (place, other) => def.SwapPlaces(place, other));
		}

		// A panel with two sections that are always there: what is on you (equipped), and what is saved or configured but not on you
		internal static void TwoSections(Transform parent, string onLabel, string onEmpty, bool anyOn, Action<Transform> drawOn, string awayLabel, string awayEmpty, bool anyAway, Action<Transform> drawAway)
		{
			var box = UiKit.VBox(parent, 4f, new RectOffset(8, 8, 6, 6), InnerPanel, UiKit.InnerEdge);
			UiKit.Label(box, onLabel, 0.8f, Ui.Dim);
			if (anyOn)
			{
				drawOn(box);
			}
			else
			{
				UiKit.Label(box, onEmpty, 0.85f, Ui.Dim);
			}

			UiKit.Line(box);
			UiKit.Label(box, awayLabel, 0.8f, Ui.Dim);
			if (anyAway)
			{
				drawAway(box);
			}
			else
			{
				UiKit.Label(box, awayEmpty, 0.85f, Ui.Dim);
			}
		}
	}
}
