using EFT.InventoryLogic;

namespace GetReady
{
	// What medical items are: found by what they treat, so modded ones count too.
	internal static partial class Game
	{
		// What an item treats (a bandage the light bleeding, a splint the fracture, a surgery kit a destroyed part)
		internal static bool Treats(Item item, EDamageEffectType effect) =>
			item.GetItemComponent<HealthEffectsComponent>()?.AffectsAny(effect) ?? false;

		// Where the medical items live in the item tree: medikits (AFAK, Salewa...) and the single-purpose "medical items"
		internal static readonly string[] UnderMedikit = { "5448f39d4bdc2d0a728b4568" };
		internal static readonly string[] UnderMedicalItem = { "5448f3ac4bdc2dce718b4569" };

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
