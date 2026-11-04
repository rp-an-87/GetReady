using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using TMPro;
using UnityEngine;

namespace GetReady
{
	// The Setup page of the new window: a section per step (its switch, then what it sets) and the options. Built again after every change.
	// A section that is not built here yet says so and opens the old window on its Setup page.
	internal static partial class SetupPage
	{
		// Where each section is on the page (in the order of the jump strip), so a click on the strip can scroll to it
		internal static readonly List<RectTransform> Sections = new List<RectTransform>();

		internal static List<string> Names() => Steps.DisplayOrder.Select(step => step.Name).Concat(new[] { "Options" }).ToList();

		internal static void Build(RectTransform content)
		{
			Sections.Clear();
			var controller = Game.Controller();
			foreach (var step in Steps.DisplayOrder)
			{
				Sections.Add(StepSection(content, step, controller));
			}

			Sections.Add(Options(content));
		}

		// A setting changed: keep it, plan again, and draw the page again
		internal static void Changed() => ReadyUiWindow.SettingChanged();

		private static RectTransform StepSection(RectTransform parent, IStep step, InventoryController controller)
		{
			var section = UiKit.VBox(parent, 6f, new RectOffset(12, 12, 10, 10), UiKit.PanelColor, UiKit.SectionEdge);
			var head = UiKit.HBox(section, 8f);
			Switch(head, step.Enabled, () =>
			{
				step.Enabled = !step.Enabled;
				Changed();
			});
			UiKit.Icon(head, Icons.StepSprite(step.Name), UiKit.Unit * 1.3f, step.Name);
			UiKit.Label(head, step.Name, 1f, Ui.Heading, bold: true);
			if (!step.Enabled)
			{
				return section;
			}

			UiKit.Line(section);
			if (step is AmmoStep)
			{
				AmmoSetup(section);
			}
			else
			{
				NotYet(section);
			}

			return section;
		}

		// For a section that is still only in the old window
		private static void NotYet(Transform parent)
		{
			var row = UiKit.HBox(parent, 10f);
			UiKit.Label(row, "This section is still in the old window.", 0.85f, Ui.Dim, flex: true);
			UiKit.PushButton(row, "Open it", ReadyWindow.ShowSetup, new Color(0.2f, 0.21f, 0.21f), new Color(0.27f, 0.28f, 0.27f), Ui.Text, height: UiKit.Unit * 1.7f);
		}

		// The ON / OFF switch of a section or of an option
		internal static void Switch(Transform parent, bool on, Action toggle)
		{
			var button = on
				? UiKit.PushButton(parent, "ON", toggle, new Color(0.24f, 0.36f, 0.19f), new Color(0.3f, 0.44f, 0.23f), Color.white, width: UiKit.Unit * 3.4f, height: UiKit.Unit * 1.6f)
				: UiKit.PushButton(parent, "OFF", toggle, new Color(0.16f, 0.165f, 0.165f), new Color(0.27f, 0.28f, 0.27f), Ui.Dim, width: UiKit.Unit * 3.4f, height: UiKit.Unit * 1.6f);
			button.GetComponentInChildren<TMP_Text>().fontStyle = FontStyles.Bold;
		}

		// A switch with its explanation on one row
		internal static void OptionRow(Transform parent, bool value, string text, string tip, Action<bool> set)
		{
			var row = UiKit.HBox(parent, 10f);
			Switch(row, value, () =>
			{
				set(!value);
				Changed();
			});
			UiKit.Tip(UiKit.Label(row, text, 0.9f, Ui.Text, flex: true), tip);
		}

		// A title with the round "?" before it
		internal static void HelpTitle(Transform parent, string title, string help, bool muted = false)
		{
			var row = UiKit.HBox(parent, 8f);
			UiKit.HelpMark(row, help);
			UiKit.Label(row, title, muted ? 0.85f : 0.9f, muted ? Ui.Dim : Ui.Text);
		}

		// The standard priority list: rows in the order of use, moved by dragging the grip. label gives the text of an item; move gets the
		// item and how many places up (negative) or down it went.
		internal static void PriorityList<T>(Transform parent, string title, IList<T> items, Func<T, string> label, Action<T, int> move, string help)
		{
			var box = UiKit.VBox(parent, 4f, new RectOffset(8, 8, 6, 6), new Color(0.095f, 0.1f, 0.1f), UiKit.InnerEdge);
			HelpTitle(box, title, help, true);
			var rows = new List<RectTransform>();
			for (int i = 0; i < items.Count; i++)
			{
				var row = UiKit.HBox(box, 8f);
				UiKit.Grip(row);
				UiKit.Label(row, label(items[i]), 0.9f, Ui.Text, flex: true);
				rows.Add(row);
			}

			var list = items.ToList();
			UiReorder.Attach(box, rows, (from, to) =>
			{
				move(list[from], to - from);
				Changed();
			});
		}

		private static RectTransform Options(RectTransform parent)
		{
			var supplies = SettingsStore.Current.Supplies;
			var section = UiKit.VBox(parent, 8f, new RectOffset(12, 12, 10, 10), UiKit.PanelColor, UiKit.SectionEdge);
			UiKit.Label(section, "Options", 1f, Ui.Heading, bold: true);
			UiKit.Line(section);

			var size = UiKit.HBox(section, 8f);
			UiKit.Tip(UiKit.Label(size, "Window size", 0.9f, Ui.Text), "Scales the text and pictures in this window and the red check box. Handy on a big screen. The window itself is resized by dragging its edges.");
			UiKit.PushButton(size, "-", () => Resize(-0.1f), new Color(0.2f, 0.21f, 0.21f), new Color(0.27f, 0.28f, 0.27f), Ui.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f);
			var percent = UiKit.Label(size, $"{Mathf.RoundToInt(ReadyWindow.Scale * 100f)}%", 0.9f, Ui.Text, align: TextAlignmentOptions.Center);
			UiKit.Size(percent, UiKit.Unit * 3.5f);
			UiKit.PushButton(size, "+", () => Resize(0.1f), new Color(0.2f, 0.21f, 0.21f), new Color(0.27f, 0.28f, 0.27f), Ui.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f);

			OptionRow(section, supplies.SwapWorse, "Replace worse kits with better ones from the stash", "A used or lower-priority kit you carry is swapped for a better one", value => supplies.SwapWorse = value);
			OptionRow(section, supplies.AllowOvershoot, "Take a bigger item when only a little is missing", "1 point short and the next item is an Army bandage (2): take it anyway", value => supplies.AllowOvershoot = value);
			OptionRow(section, supplies.NonFiRBeforeLoose, "Keep FiR items back, even if that means taking from a container", "On: non-FiR first, and between equals the one lying loose on the stash goes before one in a case or bag. Off: loose first, whatever FiR is", value => supplies.NonFiRBeforeLoose = value);
			return section;
		}

		private static void Resize(float step)
		{
			SettingsStore.Current.UiScale = Mathf.Clamp(Mathf.Round((ReadyWindow.Scale + step) * 10f) / 10f, 0.8f, 2f);
			SettingsStore.Save();
			ReadyUiWindow.ApplyScale();
		}
	}
}
