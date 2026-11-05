using EFT.InventoryLogic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace GetReady
{
	// The small Setup sections: the special slots template and the full auto slots
	internal static partial class SetupPage
	{
		// The saved template as a row of item pictures (hover for the name), with the buttons to save it from what is in the slots now
		private static void SpecialSlots(Transform section, InventoryController controller)
		{
			var slots = SettingsStore.Current.SpecialSlots.Slots;
			if (slots.Count == 0)
			{
				EmptyNote(section, "Nothing saved yet. Put what you want in the special slots, then save them.");
			}
			else
			{
				var strip = UiKit.HBox(section, 6f);
				foreach (var entry in slots)
				{
					var first = entry.Templates[0];
					string name = $"{entry.SlotId.Replace("SpecialSlot", "Slot ")}: {first.Name}" + (entry.Count > 1 ? $" x{entry.Count}" : "");
					TemplateIcon(strip, first.Template, UiKit.Unit * 2.6f, name);
					if (entry.Count > 1)
					{
						// The amount in the corner, in the green the game draws a stack in
						var count = UiKit.Label(strip.GetChild(strip.childCount - 1), entry.Count.ToString(), 0.6f, new Color(0.55f, 0.9f, 0.45f), bold: true, align: TextAlignmentOptions.BottomRight);
						UiKit.Stretch(count.rectTransform, 0f, 0f, 4f, 2f);
					}
				}
			}

			// The buttons share the width equally: the save button alone is full width, with Clear beside it they are half each
			var buttons = UiKit.HBox(section, 8f);
			buttons.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth = true;
			if (controller != null)
			{
				var save = UiKit.PushButton(buttons, "Save my current special slots as the template", () =>
				{
					SpecialSlotsStep.Capture(controller);
					Changed();
				}, ButtonFill, ButtonHover, Ui.Text, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
				UiKit.Size(save, width: 1f, flexWidth: 1f).minWidth = 0f;
				UiKit.Tip(save, "Remembers what is in each special slot of your pockets right now");
			}

			if (slots.Count > 0)
			{
				var clear = UiKit.PushButton(buttons, "Clear", () =>
				{
					SpecialSlotsStep.Clear();
					Changed();
				}, ButtonFill, ButtonHover, Ui.Text, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
				UiKit.Size(clear, width: 1f, flexWidth: 1f).minWidth = 0f;
				UiKit.Tip(clear, "Forgets the saved template (nothing moves)");
			}
		}

		// A switch per weapon slot
		private static void FullAuto(Transform section)
		{
			HelpTitle(section, "Weapon slots to put on full auto", "Puts the weapon in each picked slot on full auto, if it has that mode.");
			foreach (var pair in FullAutoStep.Slots.ToList())
			{
				var key = pair.Key;
				OptionRow(section, FullAutoStep.IsSelected(key), pair.Value, "Set the weapon in this slot to full auto", on => FullAutoStep.Toggle(key));
			}
		}
	}
}
