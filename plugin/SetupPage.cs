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

		// What fills each section, so one section can be made again without the rest of the page
		private static readonly List<Action> Fillers = new List<Action>();

		// The sections that show what you wear, and the slots of your equipment that tell when it changes (a piece taken off, a weapon swapped):
		// the game says so itself, so nothing is looked at again and again. The change is only noted; the window draws it on its next look.
		private static readonly HashSet<int> GearSections = new HashSet<int>(), GearStale = new HashSet<int>();
		private static List<Slot> _watching = new List<Slot>();

		private static void GearChanged(Item item) => GearStale.UnionWith(GearSections);

		private static int _casesSection = -1;

		// Raised by the plan when an item moved, grew, shrank or was used
		private static void ItemsChanged()
		{
			if (_casesSection >= 0)
			{
				GearStale.Add(_casesSection);
			}
		}

		private static void Watch(InventoryController controller)
		{
			StopWatching();
			if (controller?.Inventory?.Equipment == null)
			{
				return;
			}

			ReadyPlan.InventoryChanged += ItemsChanged;
			_watching = controller.Inventory.Equipment.Slots.ToList();
			foreach (var slot in _watching)
			{
				slot.OnAddOrRemoveItem += GearChanged;
			}
		}

		// When the window closes
		internal static void StopWatching()
		{
			ReadyPlan.InventoryChanged -= ItemsChanged;
			_casesSection = -1;
			foreach (var slot in _watching)
			{
				slot.OnAddOrRemoveItem -= GearChanged;
			}

			_watching.Clear();
			GearSections.Clear();
			GearStale.Clear();
		}

		// The section whose gear changed since it was drawn, or -1
		internal static int Outdated()
		{
			foreach (var index in GearStale)
			{
				GearStale.Remove(index);
				return index;
			}

			return -1;
		}

		internal static void Build(RectTransform content)
		{
			Sections.Clear();
			Fillers.Clear();
			Cards.Clear();
			_fillNext = 0;
			GearSections.Clear();
			GearStale.Clear();
			var controller = Game.Controller();
			Watch(controller);
			foreach (var step in Steps.DisplayOrder)
			{
				var current = step;
				var section = UiKit.VBox(content, 6f, new RectOffset(12, 12, 10, 10), UiKit.PanelColor, UiKit.SectionEdge);
				Action fill = () => FillStep(section, current, controller);
				if (step is RepairStep || step is AmmoStep)
				{
					GearSections.Add(Sections.Count);
				}

				// The cases you carry change with any move of an item, so that section follows the items
				if (step is CasesStep)
				{
					_casesSection = Sections.Count;
				}

				Sections.Add(section);
				Fillers.Add(fill);
			}

			var options = UiKit.VBox(content, 8f, new RectOffset(12, 12, 10, 10), UiKit.PanelColor, UiKit.SectionEdge);
			Action fillOptions = () => FillOptions(options);
			Sections.Add(options);
			Fillers.Add(fillOptions);
		}

		// The sections are made empty, and filled one after the other over several frames (see Pump), so opening the page is not one long frame
		private static int _fillNext;

		// Fills the next sections until the time given (in ms) is used; true when all are filled
		internal static bool Pump(float budgetMs)
		{
			var watch = System.Diagnostics.Stopwatch.StartNew();
			while (_fillNext < Fillers.Count)
			{
				Fillers[_fillNext]();
				_fillNext++;
				if (watch.Elapsed.TotalMilliseconds >= budgetMs)
				{
					break;
				}
			}

			return _fillNext >= Fillers.Count;
		}

		// A setting changed: draw the section it is in again (the one the mouse is over; -1 when that is not clear, and the whole page is
		// drawn instead). Only that section is made again: the rest of the page, and where it is scrolled to, stay as they are.
		internal static int Refresh(RectTransform content)
		{
			int index = SectionUnderMouse();
			if (index < 0)
			{
				return -1;
			}

			// The card the mouse is over, when it has one (a group's card), else the section
			if (RefreshCard(content, index))
			{
				LastRedraw = "card";
				return index;
			}

			LastRedraw = "section";
			return RefreshSection(content, index) ? index : -1;
		}

		// What the last redraw was, for the log
		internal static string LastRedraw = "";

		// The cards that can be drawn again on their own, and what fills each
		private static readonly List<KeyValuePair<RectTransform, Action>> Cards = new List<KeyValuePair<RectTransform, Action>>();

		private static bool RefreshCard(RectTransform content, int section)
		{
			Cards.RemoveAll(card => card.Key == null || !card.Key.gameObject.activeInHierarchy);
			foreach (var card in Cards)
			{
				if (!card.Key.IsChildOf(Sections[section]) || !Contains(card.Key))
				{
					continue;
				}

				for (int i = card.Key.childCount - 1; i >= 0; i--)
				{
					var child = card.Key.GetChild(i);
					child.gameObject.SetActive(false);
					UnityEngine.Object.Destroy(child.gameObject);
				}

				card.Value();
				UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
				return true;
			}

			return false;
		}

		private static bool Contains(RectTransform rect)
		{
			var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
			var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
		}

		// One section made again
		internal static bool RefreshSection(RectTransform content, int index)
		{
			if (index < 0 || index >= Fillers.Count || Sections[index] == null)
			{
				return false;
			}

			var section = Sections[index];
			for (int i = section.childCount - 1; i >= 0; i--)
			{
				var child = section.GetChild(i);
				// Switched off at once: a destroyed object stays until the end of the frame and would still take room in the layout
				child.gameObject.SetActive(false);
				UnityEngine.Object.Destroy(child.gameObject);
			}

			Fillers[index]();
			UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
			return true;
		}

		private static int SectionUnderMouse()
		{
			for (int i = 0; i < Sections.Count; i++)
			{
				if (Sections[i] == null)
				{
					continue;
				}

				var canvas = Sections[i].GetComponentInParent<Canvas>()?.rootCanvas;
				var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
				if (RectTransformUtility.RectangleContainsScreenPoint(Sections[i], Input.mousePosition, camera))
				{
					return i;
				}
			}

			return -1;
		}

		// A setting changed: keep it, plan again, and draw what changed again
		internal static void Changed() => ReadyUiWindow.SettingChanged();

		private static void FillStep(RectTransform section, IStep step, InventoryController controller)
		{
			var head = UiKit.HBox(section, 8f);
			Switch(head, step.Enabled, () =>
			{
				step.Enabled = !step.Enabled;
				Changed();
			});
			UiKit.Icon(head, Icons.StepSprite(step.Name), UiKit.Unit * 1.3f, step.Name);
			UiKit.Label(head, step.Name, 1f, UiColors.Heading, bold: true);
			if (!step.Enabled)
			{
				return;
			}

			UiKit.Line(section);
			if (step is AmmoStep)
			{
				AmmoSetup(section, controller);
			}
			else if (step is GrenadesStep)
			{
				GroupGrid(section, step.Groups);
			}
			else if (step is HealingStep)
			{
				GroupList(section, step.Groups);
			}
			else if (step is CashStep)
			{
				GroupList(section, step.Groups);
			}
			else if (step is SpecialSlotsStep)
			{
				SpecialSlots(section, controller);
			}
			else if (step is FullAutoStep)
			{
				FullAuto(section);
			}
			else if (step is CasesStep)
			{
				CasesSetup(section, controller);
			}
			else if (step is RepairStep)
			{
				RepairSetup(section, controller);
			}
			else if (step is HealMeStep)
			{
				HealMe(section);
			}
		}

		// The ON / OFF switch of a section or of an option
		internal static void Switch(Transform parent, bool on, Action toggle)
		{
			var button = on
				? UiKit.PushButton(parent, "ON", toggle, new Color(0.24f, 0.36f, 0.19f), new Color(0.3f, 0.44f, 0.23f), Color.white, width: UiKit.Unit * 3.4f, height: UiKit.Unit * 1.6f, edge: new Color(0.5f, 0.62f, 0.38f))
				: UiKit.PushButton(parent, "OFF", toggle, new Color(0.16f, 0.165f, 0.165f), new Color(0.27f, 0.28f, 0.27f), UiColors.Dim, width: UiKit.Unit * 3.4f, height: UiKit.Unit * 1.6f, edge: PickEdge);
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
			var label = UiKit.Label(row, text, 0.9f, UiColors.Text, flex: true);
			if (!string.IsNullOrEmpty(tip))
			{
				UiKit.Tip(label, tip);
			}
		}

		// A title with the round "?" before it
		internal static void HelpTitle(Transform parent, string title, string help, bool muted = false)
		{
			var row = UiKit.HBox(parent, 8f);
			UiKit.HelpMark(row, help);
			UiKit.Label(row, title, muted ? 0.85f : 0.9f, muted ? UiColors.Dim : UiColors.Text);
		}

		// The standard priority list: rows in the order of use, moved by dragging the grip. label gives the text of an item; move gets the
		// item and how many places up (negative) or down it went.
		internal static void PriorityList<T>(Transform parent, string title, IList<T> items, Func<T, string> label, Action<T, int> move, string help, bool numbered = false, Action<Transform, T> picture = null)
		{
			var box = UiKit.VBox(parent, 4f, new RectOffset(8, 8, 6, 6), new Color(0.095f, 0.1f, 0.1f), UiKit.InnerEdge);
			if (string.IsNullOrEmpty(help))
			{
				UiKit.Label(box, title, 0.85f, UiColors.Dim);
			}
			else
			{
				HelpTitle(box, title, help, true);
			}

			var rows = new List<RectTransform>();
			for (int i = 0; i < items.Count; i++)
			{
				var row = UiKit.HBox(box, 8f);
				if (numbered)
				{
					var number = UiKit.Label(row, (i + 1).ToString(), 0.85f, UiColors.Dim, align: TextAlignmentOptions.Center);
					UiKit.Size(number, UiKit.Unit * 1.2f);
				}

				UiKit.Grip(row);
				picture?.Invoke(row, items[i]);
				UiKit.Label(row, label(items[i]), 0.9f, UiColors.Text, flex: true);
				rows.Add(row);
			}

			var list = items.ToList();
			UiReorder.Attach(box, rows, (from, to) =>
			{
				move(list[from], to - from);
				Changed();
			});
		}

		private static void FillOptions(RectTransform section)
		{
			var supplies = SettingsStore.Current.Supplies;
			UiKit.Label(section, "Options", 1f, UiColors.Heading, bold: true);
			UiKit.Line(section);

			var size = UiKit.HBox(section, 8f);
			UiKit.Tip(UiKit.Label(size, "Window size", 0.9f, UiColors.Text), "Scales the text and pictures in this window and the red check box. Handy on a big screen. The window itself is resized by dragging its edges.");
			UiKit.PushButton(size, "-", () => Resize(-0.1f), new Color(0.2f, 0.21f, 0.21f), new Color(0.27f, 0.28f, 0.27f), UiColors.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f, edge: ButtonEdge);
			var percent = UiKit.Label(size, $"{Mathf.RoundToInt(UiKit.ChosenScale * 100f)}%", 0.9f, UiColors.Text, align: TextAlignmentOptions.Center);
			UiKit.Size(percent, UiKit.Unit * 3.5f);
			UiKit.PushButton(size, "+", () => Resize(0.1f), new Color(0.2f, 0.21f, 0.21f), new Color(0.27f, 0.28f, 0.27f), UiColors.Text, width: UiKit.Unit * 2f, height: UiKit.Unit * 1.7f, edge: ButtonEdge);

			OptionRow(section, supplies.NonFiRBeforeLoose, "Keep FiR items back, even if that means taking from a container", "Which copy of an item is taken from the stash when you have several.\n<b>On:</b> non-FiR first (it keeps your found-in-raid ones back), even when that one is inside a case or bag. Between equals, the one lying loose goes first.\n<b>Off:</b> the one lying loose goes first, whatever its FiR status.", value => supplies.NonFiRBeforeLoose = value);
		}

		private static void Resize(float step)
		{
			SettingsStore.Current.UiScale = Mathf.Clamp(Mathf.Round((UiKit.ChosenScale + step) * 10f) / 10f, 0.8f, 2f);
			SettingsStore.Save();
			ReadyUiWindow.ApplyScale();
		}
	}
}
