using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;
using static GetReady.ReadyPlan;
using static GetReady.ReadyRun;

namespace GetReady
{
	// The Setup page: the switch, template and settings of each step.
	internal static partial class ReadyWindow
	{
		// Groups whose item list is open
		private static readonly HashSet<string> Expanded = new HashSet<string>();

		private static string PlaceLabel(string place) => place == "Special slots" ? "Special" : place;

		// One width for every place chip, so all cards line up: the grip and the longest name
		private static float PlaceButtonWidth(bool compact)
		{
			float widest = 0;
			foreach (var place in new[] { "Pockets", "Vest", "Backpack", "Pouch", "Special slots", "Wallet", "WZ wallet", "Docs case", "SICC case" })
			{
				widest = Math.Max(widest, Ui.PickOn.CalcSize(new GUIContent(PlaceLabel(place))).x);
			}

			return Math.Max(compact ? 84 : 100, widest + 24);
		}

		// A supply group as a card: the item's icon, the name, the amount with - and +, where it goes (click the places in the
		// order you want them tried) and, opened with "Items", which items count and in what order
		private static bool GroupRow(GroupDef def, bool compact = false, float rowContent = 0)
		{
			var setting = def.Setting;
			var active = def.ActiveEntries();
			bool changed = false;
			bool on = setting.Target > 0;

			if (compact)
			{
				// The card is as tall as the tallest card of its row (rowContent is that height, measured on the previous frame)
				GUILayout.BeginVertical(Ui.Group, GUILayout.Width(CompactWidth), GUILayout.MinHeight(Math.Max(80, rowContent + Ui.Group.padding.vertical)), GUILayout.ExpandHeight(true));
			}
			else
			{
				GUILayout.BeginVertical(Ui.Group);
			}

			// The content on its own, not stretched, so its height is what this card needs
			GUILayout.BeginVertical();

			GUILayout.BeginHorizontal();
			if (def.Entries.Length > 0)
			{
				var shown = active.FirstOrDefault() ?? def.Entries[0];
				Icons.Slot(shown.Template, shown.Name, 44);
			}
			else if (def.IsQuota)
			{
				Icons.SlotUnknown("Any of your grenades, picked at random", 44);
			}

			GUILayout.BeginVertical();
			GUILayout.Label(def.Name, on ? Ui.Name : Ui.Muted);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("-", Ui.Small) && setting.Target > 0)
			{
				setting.Target = Math.Max(0, setting.Target - def.Step);
				changed = true;
			}

			if (def.Money)
			{
				// Typed in: the amounts are too big to click up to
				string typed = GUILayout.TextField(setting.Target.ToString(), 8, Ui.Field, GUILayout.Width(100));
				int parsed;
				if (typed != setting.Target.ToString() && int.TryParse(typed == "" ? "0" : typed, out parsed))
				{
					setting.Target = Math.Max(0, Math.Min(def.Max, parsed));
					changed = true;
				}

				GUILayout.Label(Game.Symbol(def.Unit) ?? def.Unit, Ui.Muted, GUILayout.Width(40));
			}
			else
			{
				GUILayout.Label(on ? Game.Amount(def.Unit, setting.Target) : "off", on ? Ui.Centered : Ui.Muted, GUILayout.Width(compact ? (def.IsQuota ? 180 : 110) : 170));
			}

			if (GUILayout.Button("+", Ui.Small) && setting.Target < def.Max)
			{
				setting.Target = Math.Min(def.Max, setting.Target + def.Step);
				changed = true;
			}

			GUILayout.EndHorizontal();
			GUILayout.EndVertical();

			GUILayout.FlexibleSpace();
			if (compact)
			{
				// The key and the items stack on the right of the amount
				GUILayout.BeginVertical();
			}

			if (def.Bindable)
			{
				string key = def.HotkeyChoice;
				if (GUILayout.Button(new GUIContent(Hotkeys.Label(key), "The quick-slot key this goes on. After getting ready, the best one you carry is put on it (only items in the pockets or the tactical vest can be)."), key == "" ? Ui.Pick : Ui.PickOn, GUILayout.Width(Fit(Hotkeys.Label(key), Ui.PickOn, 86))))
				{
					setting.Hotkey = Hotkeys.Next(key);
					changed = true;
				}
			}

			if (def.Entries.Length > 1)
			{
				bool open = Expanded.Contains(def.Id);
				if (GUILayout.Button(new GUIContent(open ? "Items  ▲" : "Items  ▼", "Which items count for this and in what order they are used"), open ? Ui.PickOn : Ui.Pick, GUILayout.Width(Fit("Items  ▼", Ui.PickOn, 86))))
				{
					if (open)
					{
						Expanded.Remove(def.Id);
					}
					else
					{
						Expanded.Add(def.Id);
					}
				}
			}

			if (compact)
			{
				GUILayout.EndVertical();
			}

			GUILayout.EndHorizontal();

			// The spare ammo is on for a model with a number of its own even when the generic number is 0: its places stay in reach
			if (on || def.Id == AmmoStep.Spare.Id && AmmoStep.AnyOwnStacks())
			{
				GUILayout.Space(10);
				GUILayout.BeginHorizontal();
				GUILayout.BeginHorizontal(GUILayout.Width(GoesToWidth));
				HelpTitle("Each item goes to the first place in the row that has room, tried from left to right. Drag a place onto another to swap them. Click a place to switch it off (a red cross, at the end, never used).", "Bring to", Ui.Muted, 28f);
				GUILayout.EndHorizontal();
				changed |= PlacePicker(def, compact);
				GUILayout.EndHorizontal();
			}

			if (def.Entries.Length > 1 && Expanded.Contains(def.Id))
			{
				changed |= GroupItems(def, compact);
			}

			GUILayout.EndVertical();
			if (compact && Event.current.type == EventType.Repaint)
			{
				CardContent[def.Id] = GUILayoutUtility.GetLastRect().height;
			}

			GUILayout.EndVertical();
			return changed;
		}

		// Where a group goes: every place as a chip, in the order they are tried (the first with room wins). A place switched off is
		// struck out and at the end. Click to switch it, drag to move it.
		private static bool PlacePicker(GroupDef def, bool compact)
		{
			var order = def.PlaceOrder();
			var chips = order.Select(place => new Chip
			{
				Key = place,
				Label = PlaceLabel(place),
				On = def.PlaceOn(place),
				Tip = def.PlaceOn(place) ? "On: used, in the order of the arrows. Click to switch it off, drag it onto another place to swap them." : "Off: not used. Click to switch it on, or drag it onto a place that is on to take its spot."
			}).ToList();
			return Chips("places-" + def.Id, chips, 26, PlaceButtonWidth(compact), compact ? 3 : 5,
				place => def.TogglePlace(place), (place, other) => def.SwapPlaces(place, other));
		}

		// The list of the items of a group (opened with "Items"): their order (drag by the grip), limits and switches. What is not used
		// is at the end.
		private static bool GroupItems(GroupDef def, bool compact = false)
		{
			bool changed = false;
			GUILayout.BeginVertical(Ui.Inner);
			GUILayout.Label(def.Random ? "Used, in any order" : "Used, best first (drag a row by its grip)", Ui.Muted);
			var entries = ShownEntries(def);
			var rowRects = new List<Rect>();
			for (int i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];
				bool off = def.Setting.Off.Contains(entry.Template);
				GUILayout.BeginVertical();
				changed |= compact ? CompactItem(def, entry, off) : ItemRow(def, entry, off);
				GUILayout.EndVertical();
				rowRects.Add(GUILayoutUtility.GetLastRect());
			}

			GUILayout.EndVertical();

			if (!def.Random)
			{
				int from, to;
				if (Dnd.RowDrop(def.Id, entries.Select(entry => entry.Template).ToList(), rowRects, out from, out to))
				{
					ReorderEntries(def, entries, from, to);
					changed = true;
				}

				Dnd.RowBar(def.Id, rowRects);
			}

			return changed;
		}

		// The most of an item to carry for the group: - any +
		private static bool MaxControls(GroupDef def, Entry entry)
		{
			bool changed = false;
			int max = def.MaxOf(entry.Template);
			GUILayout.Label(new GUIContent("Max", "The most of this item to carry for the group. If nothing else can make up the amount, more are still brought."), Ui.Muted, GUILayout.Width(30));
			if (GUILayout.Button("-", Ui.Small) && max > 0)
			{
				def.SetMax(entry.Template, max - 1);
				changed = true;
			}

			GUILayout.Label(max == 0 ? "any" : max.ToString(), Ui.Centered, GUILayout.Width(34));
			if (GUILayout.Button("+", Ui.Small) && max < 20)
			{
				def.SetMax(entry.Template, max + 1);
				changed = true;
			}

			return changed;
		}

		// One item of a group's list: the grip, the picture, the name, the limit and the Used switch
		private static bool ItemRow(GroupDef def, Entry entry, bool off)
		{
			bool changed = false;
			GUILayout.BeginHorizontal();
			if (!def.Random)
			{
				Dnd.Grip(30, "item", def.Id, entry.Template, entry.Name);
			}

			Icons.Slot(entry.Template, entry.Name, 30);
			GUILayout.Label(entry.Name, off ? Ui.Muted : Ui.Body, GUILayout.ExpandWidth(true), GUILayout.Height(30));
			if (off)
			{
				StrikeLast(entry.Name, Ui.Muted);
			}

			if (!def.Random)
			{
				changed |= MaxControls(def, entry);
			}

			if (GUILayout.Button(off ? "Not used" : "Used", off ? Ui.PickRow : Ui.PickOnRow, GUILayout.Width(Fit("Not used", Ui.PickOnRow, 90)), GUILayout.Height(30)))
			{
				def.ToggleEntry(entry.Template);
				changed = true;
			}

			GUILayout.EndHorizontal();
			return changed;
		}

		// The same in a narrow card: the grip, picture, name and Used switch on one line; the limit under it
		private static bool CompactItem(GroupDef def, Entry entry, bool off)
		{
			bool changed = false;
			GUILayout.BeginHorizontal();
			if (!def.Random)
			{
				Dnd.Grip(28, "item", def.Id, entry.Template, entry.Name);
			}

			Icons.Slot(entry.Template, entry.Name, 28);
			GUILayout.Label(entry.Name, off ? Ui.Muted : Ui.Body, GUILayout.ExpandWidth(true), GUILayout.Height(28));
			if (off)
			{
				StrikeLast(entry.Name, Ui.Muted);
			}

			if (GUILayout.Button(off ? "Not used" : "Used", off ? Ui.PickRow : Ui.PickOnRow, GUILayout.Width(Fit("Not used", Ui.PickOnRow, 76)), GUILayout.Height(28)))
			{
				def.ToggleEntry(entry.Template);
				changed = true;
			}

			GUILayout.EndHorizontal();
			if (!def.Random)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Space(34);
				changed |= MaxControls(def, entry);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}

			return changed;
		}

		// Groups two to a row, each card narrow; an opened item list is drawn inside its own card
		// (wide enough for "Bring to" and three place buttons in a row; the window widens to hold two of them)
		private const float GoesToWidth = 112f;

		private static float CompactWidth => Math.Max(326, Ui.Group.padding.horizontal + GoesToWidth + 3 * (PlaceButtonWidth(true) + 4) + ChipArrow + ChipOffGap + 8);

		// The narrowest the window can be without a horizontal scroll: two cards side by side, with the window's own padding
		internal static float MinWindowWidth => Math.Max(740, 2 * CompactWidth + Ui.Group.margin.horizontal * 2 + 96);

		// The height of each card's content as last drawn (what the card needs, not what it was stretched to)
		private static readonly Dictionary<string, float> CardContent = new Dictionary<string, float>();

		private static float ContentOf(GroupDef def)
		{
			float height;
			return CardContent.TryGetValue(def.Id, out height) ? height : 0;
		}

		private static bool GroupGrid(IReadOnlyList<GroupDef> defs)
		{
			bool changed = false;
			for (int i = 0; i < defs.Count; i += 2)
			{
				// A row is as tall as its tallest card: both cards are given the larger of the two heights
				float rowContent = Math.Max(ContentOf(defs[i]), i + 1 < defs.Count ? ContentOf(defs[i + 1]) : 0);
				GUILayout.BeginHorizontal();
				changed |= GroupRow(defs[i], true, rowContent);
				if (i + 1 < defs.Count)
				{
					changed |= GroupRow(defs[i + 1], true, rowContent);
				}

				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}

			return changed;
		}

		// The saved template as a row of item icons (hover for the name)
		private static void TemplateStrip(IEnumerable<KeyValuePair<string, string>> items, string empty) =>
			TemplateStrip(items.Select(item => new StripItem { Template = item.Key, Name = item.Value }), empty);

		// One icon of a template strip: the item, what it is called, and a count drawn on it (a stack of money, a stack of items), or none
		private class StripItem
		{
			public string Template;
			public string Name;
			public int Count;
			// The short name drawn in the top right corner, as the game does (none for money)
			public string Label;
		}

		private static GUIStyle _stackCount, _stackShadow, _shortName, _shortShadow;

		// The icons of a template in a row; a stack shows its amount in the corner, as the game does
		private static void TemplateStrip(IEnumerable<StripItem> items, string empty)
		{
			var list = items.ToList();
			if (list.Count == 0)
			{
				GUILayout.Label(empty, Ui.Muted);
				return;
			}

			if (_stackCount == null)
			{
				_stackCount = new GUIStyle(Ui.Centered) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight, padding = new RectOffset(0, 3, 0, 2), wordWrap = false };
				_stackCount.normal.textColor = new Color(0.55f, 0.9f, 0.45f); // green, as the game draws a stack
				_stackShadow = new GUIStyle(_stackCount);
				_stackShadow.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
				_shortName = new GUIStyle(_stackCount) { fontSize = 9, alignment = TextAnchor.UpperRight, padding = new RectOffset(0, 2, 1, 0), clipping = TextClipping.Clip };
				_shortName.normal.textColor = new Color(0.92f, 0.92f, 0.88f);
				_shortShadow = new GUIStyle(_shortName);
				_shortShadow.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
			}

			GUILayout.BeginHorizontal();
			foreach (var item in list)
			{
				Icons.Slot(item.Template, item.Name, 40);
				var rect = GUILayoutUtility.GetLastRect();
				if (!string.IsNullOrEmpty(item.Label))
				{
					GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), item.Label, _shortShadow);
					GUI.Label(rect, item.Label, _shortName);
				}

				if (item.Count > 1)
				{
					GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), item.Count.ToString(), _stackShadow);
					GUI.Label(rect, item.Count.ToString(), _stackCount);
				}
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		// A step as a section: its switch and status tag, then its template or settings, then what it is about to do
		private static bool StepSection(IStep step, InventoryController controller)
		{
			bool changed = false;
			GUILayout.BeginVertical(Ui.Section);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(step.Enabled ? "ON" : "OFF", step.Enabled ? Ui.SwitchOn : Ui.SwitchOff))
			{
				step.Enabled = !step.Enabled;
				changed = true;
			}

			StepTitle(step.Name);
			GUILayout.EndHorizontal();

			if (step.Enabled)
			{
				GUILayout.Box(GUIContent.none, Ui.Divider);
				if (step is SpecialSlotsStep)
				{
					var slots = SettingsStore.Current.SpecialSlots.Slots;
					TemplateStrip(slots.Select(entry => new KeyValuePair<string, string>(entry.Templates[0].Template, $"{entry.SlotId.Replace("SpecialSlot", "Slot ")}: {entry.Templates[0].Name}" + (entry.Count > 1 ? $" x{entry.Count}" : ""))), "Nothing saved yet. Put what you want in the special slots, then save them.");
					GUILayout.BeginHorizontal();
					if (controller != null && GUILayout.Button(new GUIContent("Save my current special slots as the template", "Remembers what is in each special slot of your pockets right now"), Ui.Button))
					{
						_status = $"Saved {SpecialSlotsStep.Capture(controller)} special slot(s).";
						RefreshSoon();
					}

					if (slots.Count > 0 && GUILayout.Button(new GUIContent("Clear", "Forgets the saved template (nothing moves)"), Ui.Button, GUILayout.Width(Fit("Clear", Ui.Button, 80))))
					{
						SpecialSlotsStep.Clear();
						_status = "Cleared the special slots template.";
						RefreshSoon();
					}

					GUILayout.EndHorizontal();
				}
				else if (step is AmmoStep)
				{
					var ammo = SettingsStore.Current.Ammo;
					TitleWithHelp("How the magazines are filled", "Follows what your magazines and containers already hold. Nothing moves until you press Get ready.");
					bool chamber = OptionRow(ammo.Chamber, "Chamber a round in weapons with an empty chamber", "The round is the same ammo as the weapon's magazine, taken from your gear first, then the stash");
					if (chamber != ammo.Chamber)
					{
						ammo.Chamber = chamber;
						changed = true;
					}

					bool topUp = OptionRow(ammo.TopUpLoose, "Keep the loose ammo stacks I carry full", "The stacks of loose ammo in your vest, pockets, backpack and pouch are filled up to a full stack with the same ammo from the stash. No new kinds of ammo are added.");
					if (topUp != ammo.TopUpLoose)
					{
						ammo.TopUpLoose = topUp;
						changed = true;
					}

					bool shortIsProblem = OptionRow(ammo.LooseShortIsProblem, "A loose stack that can't be filled is a problem", "When on, a loose stack that is short and that the stash has no more of is reported in red, and counted in the box at the top. When off it is just left as it is.");
					if (shortIsProblem != ammo.LooseShortIsProblem)
					{
						ammo.LooseShortIsProblem = shortIsProblem;
						changed = true;
					}

					changed |= PriorityList("What an empty spare magazine on you copies", AmmoRules.PointerOrder(),
						pointer => AmmoRules.PointerName(pointer), null, (pointer, step) => AmmoRules.MovePointer(pointer, step),
						"The first that applies wins. A magazine in the stash only copies what it holds or an equipped weapon. Rounds are taken from the stash first, the pouch last.");
				}
				else if (step is CasesStep)
				{
					changed |= CasesSetup(controller);
				}
				else if (step is HealMeStep)
				{
					changed |= HealMeSetup();
				}
				else if (step is FullAutoStep)
				{
					changed |= FullAutoSetup();
				}
				else if (step is RepairStep && controller != null)
				{
					changed |= RepairSetup(controller);
				}

				if (step is GrenadesStep)
				{
					changed |= GroupGrid(step.Groups);
				}
				else
				{
					// The spare ammo has one card of its own (the stacks, the places, the kind and the weapons); without the stash screen only the stacks
					foreach (var def in step.Groups.Where(def => !(step is AmmoStep && controller != null)))
					{
						changed |= GroupRow(def);
					}
				}

				if (step is AmmoStep && controller != null)
				{
					changed |= SpareAmmoSetup(controller);
				}

			}

			GUILayout.EndVertical();
			return changed;
		}

		// A switch with its explanation on one row
		private static bool OptionRow(bool value, string text, string tooltip)
		{
			GUILayout.BeginHorizontal();
			bool flip = GUILayout.Button(value ? "ON" : "OFF", value ? Ui.SwitchOn : Ui.SwitchOff);
			GUILayout.Label(new GUIContent(text, tooltip), Ui.Body);
			GUILayout.EndHorizontal();
			return flip ? !value : value;
		}

		// The Setup page: what to bring and where, per step
		private static bool SetupPage(InventoryController controller)
		{
			var supplies = SettingsStore.Current.Supplies;
			bool changed = false;
			var steps = Steps.DisplayOrder.ToList();
			JumpStrip(steps.Select(step => new KeyValuePair<string, bool>(step.Name, step.Enabled)).Concat(new[] { new KeyValuePair<string, bool>("Options", true) }).ToList());
			// No horizontal scrollbar at all: the window is widened to fit the cards instead
			_setupScroll = GUILayout.BeginScrollView(_setupScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
			for (int index = 0; index < steps.Count; index++)
			{
				changed |= StepSection(steps[index], controller);
				NoteSection(index);
			}


			GUILayout.BeginVertical(Ui.Section);
			GUILayout.Label("Options", Ui.SectionTitle);
			GUILayout.Box(GUIContent.none, Ui.Divider);
			GUILayout.BeginHorizontal();
			GUILayout.Label(new GUIContent("Window size", "Scales this window and the red check box. Handy on a big screen; the text gets a little softer above 100%."), Ui.Body, GUILayout.Width(110));
			float scale = Scale;
			if (GUILayout.Button("-", Ui.Small) && scale > 0.85f)
			{
				SettingsStore.Current.UiScale = Mathf.Round((scale - 0.1f) * 10f) / 10f;
				changed = true;
			}

			GUILayout.Label($"{Mathf.RoundToInt(scale * 100f)}%", Ui.Centered, GUILayout.Width(60));
			if (GUILayout.Button("+", Ui.Small) && scale < 1.55f)
			{
				SettingsStore.Current.UiScale = Mathf.Round((scale + 0.1f) * 10f) / 10f;
				changed = true;
			}

			GUILayout.EndHorizontal();
			bool overshoot = OptionRow(supplies.AllowOvershoot, "Take a bigger item when only a little is missing", "1 point short and the next item is an Army bandage (2): take it anyway");
			bool nonFir = OptionRow(supplies.NonFiRBeforeLoose, "Keep FiR items back, even if that means taking from a container", "On: non-FiR first, and between equals the one lying loose on the stash goes before one in a case or bag. Off: loose first, whatever FiR is");
			GUILayout.EndVertical();
			NoteSection(steps.Count);

			if (overshoot != supplies.AllowOvershoot || nonFir != supplies.NonFiRBeforeLoose)
			{
				supplies.AllowOvershoot = overshoot;
				supplies.NonFiRBeforeLoose = nonFir;
				changed = true;
			}

			GUILayout.EndScrollView();
			if (Event.current.type == EventType.Repaint)
			{
				_setupViewHeight = GUILayoutUtility.GetLastRect().height;
			}

			Dnd.DrawGhost();
			return changed;
		}
	}
}
