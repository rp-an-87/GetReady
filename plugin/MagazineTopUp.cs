using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using UnityEngine;

namespace GetReady
{
	// Hotkey on a hovered magazine (stash and raid): fills it up with the ammo it already holds, or else with the best other ammo it accepts.
	// Uses the controller's own LoadMagazine, so in a raid it is the timed load that ContinuousLoadAmmo carries on outside the inventory.
	internal static class MagazineTopUp
	{
		private static readonly FieldInfo ControllerField = typeof(ItemUiContext).GetField("_inventoryController", BindingFlags.Instance | BindingFlags.NonPublic);

		private static bool _running;

		// A magazine that isn't full (whether there is ammo to fill it is looked for when the hotkey is pressed)
		internal static bool IsApplicable(Item item) => item is Magazine magazine && magazine.Count < magazine.MaxCount;

		internal static void Update()
		{
			if (!Plugin.TopUpHotkey.Value.IsDown())
			{
				return;
			}

			var uiContext = ItemUiContext.Instance;
			var magazine = uiContext?.CurrentItemContext?.Item as Magazine;
			var controller = uiContext == null ? null : ControllerField.GetValue(uiContext) as InventoryController;
			if (magazine != null && controller != null)
			{
				Run(controller, magazine);
			}
		}

		private static async void Run(InventoryController controller, Magazine magazine)
		{
			if (_running)
			{
				return;
			}

			_running = true;
			try
			{
				string message = await TopUp(controller, magazine);
				if (message != null)
				{
					NotificationManager.DisplayMessageNotification(message);
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Magazine top-up failed: {e}");
			}
			finally
			{
				_running = false;
			}
		}

		internal static async Task<string> TopUp(InventoryController controller, Magazine magazine)
		{
			if (magazine.Count >= magazine.MaxCount)
			{
				return null;
			}

			var stacks = FindAmmo(controller, magazine);
			if (stacks.Count == 0)
			{
				return "Top-up: no compatible ammo found.";
			}

			var chosen = ChooseType(stacks, magazine);

			// The best ammo goes in last, so the first rounds out of the magazine are the best ones (the last one loaded fires first)
			var best = stacks[0].TemplateId;
			int onTop = chosen == best ? 0 : Math.Min(Mathf.CeilToInt(magazine.MaxCount * SettingsStore.Current.Ammo.BestOnTopPercent / 100f), magazine.MaxCount - magazine.Count);
			var parts = new List<string>();
			await LoadType(controller, magazine, stacks, chosen, magazine.MaxCount - magazine.Count - onTop, parts);
			if (onTop > 0)
			{
				await LoadType(controller, magazine, stacks, best, Math.Min(onTop, magazine.MaxCount - magazine.Count), parts);
			}

			return parts.Count > 0 ? $"Top-up: loaded {string.Join(" and ", parts)}." : "Top-up: nothing could be loaded.";
		}

		// Loads up to limit rounds of one ammo type from the stacks, and says how many went in
		private static async Task LoadType(InventoryController controller, Magazine magazine, List<Ammo> stacks, MongoID type, int limit, List<string> parts)
		{
			int loaded = 0;
			string name = null;
			foreach (var ammo in stacks.Where(ammo => ammo.TemplateId == type))
			{
				int free = Math.Min(limit - loaded, magazine.MaxCount - magazine.Count);
				if (free <= 0)
				{
					break;
				}

				int count = Mathf.Min(ammo.StackObjectsCount, free);
				name = ammo.LocalizedShortName();
				int before = magazine.Count;
				var result = await controller.LoadMagazine(ammo, magazine, count);
				loaded += magazine.Count - before;
				if (result.Failed)
				{
					break;
				}
			}

			if (loaded > 0)
			{
				parts.Add($"{loaded} {name}");
			}
		}

		// What the magazine holds, else loose ammo in the same container (not the stash), else what the nearest magazine
		// in the same container holds, else the best ammo (stacks are sorted best first).
		private static MongoID ChooseType(List<Ammo> stacks, Magazine magazine)
		{
			bool Available(Item item) => item is Ammo ammo && stacks.Any(stack => stack.TemplateId == ammo.TemplateId);

			var held = magazine.Cartridges.Items.FirstOrDefault(Available);
			if (held != null)
			{
				return held.TemplateId;
			}

			var address = magazine.CurrentAddress as GridItemAddress;
			if (address == null)
			{
				return stacks[0].TemplateId;
			}

			var grid = address.Grid;
			var siblings = grid.ParentItem.Grids.SelectMany(g => g.Items).ToList();
			if (!(grid.ParentItem is Stash))
			{
				var loose = siblings.FirstOrDefault(Available);
				if (loose != null)
				{
					return loose.TemplateId;
				}
			}

			// Other grids of the same container (rigs have several) count as farther away than any cell of this grid.
			var nearest = siblings
				.Where(item => item is Magazine && item != magazine && item.CurrentAddress is GridItemAddress)
				.OrderBy(item =>
				{
					var other = (GridItemAddress)item.CurrentAddress;
					var location = other.LocationInGrid;
					return (other.Grid == grid ? 0 : 1000) + Mathf.Abs(location.x - address.LocationInGrid.x) + Mathf.Abs(location.y - address.LocationInGrid.y);
				})
				.Select(item => ((Magazine)item).Cartridges.Items.FirstOrDefault(Available))
				.FirstOrDefault(ammo => ammo != null);
			return nearest != null ? nearest.TemplateId : stacks[0].TemplateId;
		}

		// Ammo lying in a container grid (not in magazines, boxes or chambers), compatible with the magazine, best first.
		private static List<Ammo> FindAmmo(InventoryController controller, Magazine magazine)
		{
			var mask = controller.Inventory.Stash != null ? EPlayerItems.Stash | EPlayerItems.Equipment : EPlayerItems.Equipment;
			var found = controller.Inventory.GetPlayerItems(mask)
				.OfType<Ammo>()
				.Where(ammo => ammo.Parent?.Container is Grid && controller.Examined(ammo) && magazine.CheckCompatibility(ammo))
				.Where(ammo => !magazine.Cartridges.Items.OfType<Ammo>().Any(held => held.Caliber != ammo.Caliber))
				.ToList();

			found.Sort((a, b) =>
			{
				int result = b.PenetrationPower.CompareTo(a.PenetrationPower);
				return result != 0 ? result : a.StackObjectsCount.CompareTo(b.StackObjectsCount);
			});
			return found;
		}
	}
}
