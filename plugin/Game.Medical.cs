using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI;

namespace GetReady
{
	// What medical items are: found by what they treat, so modded ones count too.
	internal static partial class Game
	{
		private static readonly FieldInfo HealthField = typeof(ItemUiContext).GetField("_healthController", BindingFlags.Instance | BindingFlags.NonPublic);

		// Your health, as the stash screen's own item menu uses it (null when it isn't there)
		internal static IHealthController HealthController()
		{
			var uiContext = ItemUiContext.Instance;
			return uiContext == null ? null : HealthField.GetValue(uiContext) as IHealthController;
		}

		// Something is hurt: a body part below its maximum, broken or destroyed, or bleeding or fractured
		internal static bool NeedsHealing(IHealthController health)
		{
			foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
			{
				if (part == EBodyPart.Common)
				{
					continue;
				}

				if (!health.GetBodyPartHealth(part).AtMaximum || health.IsBodyPartBroken(part) || health.IsBodyPartDestroyed(part))
				{
					return true;
				}

				if (health.GetAllActiveEffects(part).Any(effect => typeof(IBleeding).IsAssignableFrom(effect.Type) || typeof(IFracture).IsAssignableFrom(effect.Type)))
				{
					return true;
				}
			}

			return false;
		}

		// What an item treats (a bandage the light bleeding, a splint the fracture, a surgery kit a destroyed part)
		internal static bool Treats(Item item, EDamageEffectType effect) =>
			item.GetItemComponent<HealthEffectsComponent>()?.AffectsAny(effect) ?? false;

		// Where the medical items live in the item tree: medikits (AFAK, Salewa...) and the single-purpose "medical items"
		internal static readonly string[] UnderMedikit = { "5448f39d4bdc2d0a728b4568" };
		internal static readonly string[] UnderMedicalItem = { "5448f3ac4bdc2dce718b4569" };

		// Medical items that are in the item tree but that a player can't get (the Sanitar boss's own first aid and surgery kits): never listed
		internal static readonly HashSet<string> NotAvailable = new HashSet<string> { "5e99711486f7744bfc4af328", "5e99735686f7744bfc4af32c" };

		internal static bool IsHealingKit(Item item) => item.GetItemComponent<MedKitComponent>() != null && !Treats(item, EDamageEffectType.DestroyedPart);

		internal static bool IsBandage(Item item) =>
			Treats(item, EDamageEffectType.LightBleeding) && !Treats(item, EDamageEffectType.HeavyBleeding) && !Treats(item, EDamageEffectType.Fracture) && !Treats(item, EDamageEffectType.DestroyedPart);

		internal static bool IsTourniquet(Item item) =>
			Treats(item, EDamageEffectType.HeavyBleeding) && !Treats(item, EDamageEffectType.Fracture) && !Treats(item, EDamageEffectType.DestroyedPart);

		// Surv12 treats fractures too but counts as a surgery kit only
		internal static bool IsSplint(Item item) => Treats(item, EDamageEffectType.Fracture) && !Treats(item, EDamageEffectType.DestroyedPart);

		internal static bool IsSurgeryKit(Item item) => Treats(item, EDamageEffectType.DestroyedPart);
	}
}
