using System;
using EFT;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;

namespace GetReady
{
	// Icons: the game's own (item icons for the window, a type icon for the stash button), and a checklist drawn in code as
	// the button's fallback. White on transparent, tinted by the button.
	internal static class Icons
	{
		// One item of each template found in the stash or on you, to take the icon from, and the icons asked for so far
		private static readonly Dictionary<string, Item> Known = new Dictionary<string, Item>();

		private static readonly Dictionary<string, Item> Made = new Dictionary<string, Item>();

		private static readonly Dictionary<string, ItemIcon> Loaded = new Dictionary<string, ItemIcon>();

		private static InventoryController _controller;

		private static float _lastGenerated;

		private static readonly Dictionary<string, bool> Examined = new Dictionary<string, bool>();

		private static readonly Dictionary<object, Sprite> EffectSprites = new Dictionary<object, Sprite>();

		// The game's own icon of a kind of trouble in a framed square, the name is the tooltip. A type (IHeavyBleeding, IFracture...) is
		// the status icon of the health screen; the health factor (HP) is the game's health icon.
		internal static void DrawEffect(Rect rect, object trouble, string name)
		{
			var sprite = EffectSprite(trouble);
			GUI.Box(rect, GUIContent.none, Ui.Inner);
			DrawSprite(rect, sprite, name);
		}

		internal static Sprite EffectSprite(object trouble)
		{
			Sprite sprite;
			if (!EffectSprites.TryGetValue(trouble, out sprite))
			{
				try
				{
					var icons = EFTHardSettings.Instance?.StaticIcons;
					var type = trouble as Type;
					if (type != null)
					{
						icons.EffectIcons.EffectIcons.TryGetValue(type, out sprite);
					}
					else
					{
						sprite = icons?.GetAttributeIcon((Enum)trouble);
					}
				}
				catch (Exception e)
				{
					Plugin.Log.LogWarning($"No icon for {trouble}: {e.Message}");
					sprite = null;
				}

				EffectSprites[trouble] = sprite;
			}

			return sprite;
		}

		// The game's own small icon for a kind of item (the one the stash button uses), null when it has none
		internal static Sprite TypeSprite(EItemType type)
		{
			try
			{
				return EFTHardSettings.Instance?.StaticIcons?.GetItemTypeIcon(type);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"No icon for {type}: {e.Message}");
				return null;
			}
		}

		private static EItemType? TypeOfStep(string step)
		{
			switch (step)
			{
				case "Special slots": return EItemType.Special;
				case "Cases": return EItemType.Container;
				case "Medicine to bring": return EItemType.Meds;
				case "Treat injuries": return EItemType.Meds;
				case "Grenades and flares": return EItemType.Grenade;
				case "Cash": return EItemType.Barter;
				case "Repair": return EItemType.Armor;
				case "Ammo": return EItemType.Ammo;
				case "Full auto": return EItemType.Weapon;
				default: return null;
			}
		}

		// The game's small icon for a step (null when it has none)
		internal static Sprite StepSprite(string step)
		{
			// Options have the game's settings gear, Cash the rouble sign, Repair the icon of a part
			if (step == "Options")
			{
				return GameWidgets.SettingsGear();
			}

			if (step == "Cash")
			{
				try
				{
					return EFTHardSettings.Instance?.StaticIcons?.GetSmallCurrencySign(new MongoID(Game.Roubles)) ?? TypeSprite(EItemType.Barter);
				}
				catch (Exception e)
				{
					Plugin.Log.LogWarning($"No cash sign: {e.Message}");
					return TypeSprite(EItemType.Barter);
				}
			}

			if (step == "Repair")
			{
				return TypeSprite(EItemType.Armor);
			}

			var type = TypeOfStep(step);
			return type == null ? null : TypeSprite(type.Value);
		}

		// Whether the step has an icon type (the sprite may still be missing; Step then draws nothing)
		internal static bool HasStepIcon(string step) => TypeOfStep(step) != null;

		// A small framed icon for a step's title, like the item slots (just the space when the game has none)
		internal static void Step(Rect rect, string step)
		{
			var type = TypeOfStep(step);
			var sprite = type == null ? null : TypeSprite(type.Value);
			if (sprite == null)
			{
				return;
			}

			GUI.Box(rect, GUIContent.none, Ui.Inner);
			DrawSprite(rect, sprite, step);
		}

		// Whether the template is in the item tree below the parent
		internal static bool IsUnder(ItemTemplate template, string parent)
		{
			var node = template;
			for (int depth = 0; node != null && depth < 24; depth++, node = node.Parent)
			{
				if (node._id.ToString() == parent)
				{
					return true;
				}
			}

			return false;
		}

		// An item of the template: one you own, or a throwaway one made from the template (the game builds icons and reads
		// components from items, not templates). Null when it can't be made.
		internal static Item ItemFor(string template)
		{
			Item item;
			if (Known.TryGetValue(template, out item) || Made.TryGetValue(template, out item))
			{
				return item;
			}

			try
			{
				var factory = Comfort.Common.Singleton<ItemFactory>.Instance;
				// The game's default build of the template (a weapon with its usual mods), or a bare item when it has none
				item = factory?.GetPresetItem(template);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning($"Could not make {template}: {e.Message}");
			}

			Made[template] = item;
			return item;
		}

		// Items you haven't examined show as a question mark, like in the stash
		internal static bool IsExamined(string template)
		{
			bool known;
			if (!Examined.TryGetValue(template, out known))
			{
				var item = ItemFor(template);
				known = item != null && (_controller == null || _controller.Examined(item));
				Examined[template] = known;
			}

			return known;
		}

		// Remembers an item of every template in the inventory
		internal static void Index(InventoryController controller)
		{
			_controller = controller;
			LoadedItems.Clear();
			Examined.Clear();
			Known.Clear();
			foreach (var item in Game.Stash(controller.Inventory).Concat(Game.CarriedAndSecured(controller.Inventory)))
			{
				var template = item.TemplateId.ToString();
				if (!Known.ContainsKey(template))
				{
					Known[template] = item;
				}
			}
		}

		internal static Sprite Of(string template)
		{
			if (!IsExamined(template))
			{
				return null;
			}

			ItemIcon icon;
			if (!Loaded.TryGetValue(template, out icon))
			{
				var item = ItemFor(template);
				if (item == null)
				{
					return null;
				}

				// Icons of items you don't own are generated from the game's models: a few at a time, not all in one frame
				if (!Known.ContainsKey(template))
				{
					if (Time.unscaledTime - _lastGenerated < 0.2f)
					{
						return null;
					}

					_lastGenerated = Time.unscaledTime;
				}

				try
				{
					icon = ItemViewFactory.LoadItemIcon(item, 1, false);
				}
				catch (Exception e)
				{
					Plugin.Log.LogWarning($"No icon for {template}: {e.Message}");
					icon = null;
				}

				Loaded[template] = icon;
			}

			return icon?.Sprite;
		}

		// The icon of this very item, as the stash draws it: a weapon shows its mods, an armor its plates (cached by item id)
		private static readonly Dictionary<string, ItemIcon> LoadedItems = new Dictionary<string, ItemIcon>();

		internal static Sprite OfItem(Item item)
		{
			ItemIcon icon;
			if (!LoadedItems.TryGetValue(item.Id, out icon))
			{
				try
				{
					icon = ItemViewFactory.LoadItemIcon(item, 1, false);
				}
				catch (Exception e)
				{
					Plugin.Log.LogWarning($"No icon for {item.ShortName}: {e.Message}");
					icon = null;
				}

				LoadedItems[item.Id] = icon;
			}

			return icon?.Sprite;
		}

		// The item's icon fitted into the rect (an empty frame while it is loading or when you have none of it); the item's
		// name is the tooltip
		internal static void Draw(Rect rect, string template, string name)
		{
			GUI.Box(rect, GUIContent.none, Ui.Inner);
			var sprite = Of(template);
			if (sprite == null && !IsExamined(template))
			{
				GUI.Label(rect, "?", Ui.Centered);
			}

			DrawSprite(rect, sprite, name);
		}

		internal static void Draw(Rect rect, Item item, string name)
		{
			GUI.Box(rect, GUIContent.none, Ui.Inner);
			DrawSprite(rect, OfItem(item), name);
		}

		private static void DrawSprite(Rect rect, Sprite sprite, string name)
		{

			if (sprite != null && sprite.texture != null)
			{
				var source = sprite.textureRect;
				float scale = Math.Min((rect.width - 4) / source.width, (rect.height - 4) / source.height);
				var size = new Vector2(source.width * scale, source.height * scale);
				var target = new Rect(rect.x + (rect.width - size.x) / 2f, rect.y + (rect.height - size.y) / 2f, size.x, size.y);
				var texture = sprite.texture;
				GUI.DrawTextureWithTexCoords(target, texture, new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
			}

			GUI.Label(rect, new GUIContent("", name));
		}

		// A square of the given size in the layout with this very item's icon
		internal static void Slot(Item item, string name, float size)
		{
			Draw(GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size)), item, name);
		}

		// A square of the given size with a "?" like an item not inspected yet: for a group that stands for any of several items
		internal static void SlotUnknown(string name, float size)
		{
			var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
			GUI.Box(rect, GUIContent.none, Ui.Inner);
			GUI.Label(rect, "?", Ui.Centered);
			GUI.Label(rect, new GUIContent("", name));
		}

		// A square of the given size in the layout with the item's icon
		internal static void Slot(string template, string name, float size)
		{
			Draw(GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size)), template, name);
		}
	}
}
