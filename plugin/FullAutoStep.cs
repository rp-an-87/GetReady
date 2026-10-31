using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;

namespace GetReady
{
	// Puts the weapons you carry in the slots you pick on full auto, when they have that mode
	internal class FullAutoStep : IStep
	{
		public string Name => "Full auto";
		public IReadOnlyList<GroupDef> Groups => new GroupDef[0];
		public bool Enabled { get => SettingsStore.Current.FullAuto.Enabled; set => SettingsStore.Current.FullAuto.Enabled = value; }

		internal static readonly KeyValuePair<EquipmentSlot, string>[] Slots =
		{
			new KeyValuePair<EquipmentSlot, string>(EquipmentSlot.FirstPrimaryWeapon, "On sling"),
			new KeyValuePair<EquipmentSlot, string>(EquipmentSlot.SecondPrimaryWeapon, "On back"),
			new KeyValuePair<EquipmentSlot, string>(EquipmentSlot.Holster, "Holster")
		};

		internal static bool IsSelected(EquipmentSlot slot) => SettingsStore.Current.FullAuto.Slots.Contains(slot.ToString());

		internal static void Toggle(EquipmentSlot slot)
		{
			var list = SettingsStore.Current.FullAuto.Slots;
			if (!list.Remove(slot.ToString()))
			{
				list.Add(slot.ToString());
			}
		}

		public List<PlanLine> Plan(InventoryController controller)
		{
			var lines = new List<PlanLine>();
			int total = 0, inPlace = 0;
			Weapon shown = null;
			foreach (var pair in Slots.Where(pair => IsSelected(pair.Key)))
			{
				var weapon = Game.InventoryOf(controller).Equipment.GetSlot(pair.Key).ContainedItem as Weapon;
				var mode = weapon?.FireMode;
				if (mode == null || !mode.AvailableEFireModes.Contains(Weapon.EFireMode.fullauto))
				{
					continue; // no weapon there, or it has no full auto
				}

				total++;
				shown = shown ?? weapon;
				if (mode.FireMode == Weapon.EFireMode.fullauto)
				{
					inPlace++;
					continue;
				}

				lines.Add(new PlanLine
				{
					Template = weapon.TemplateId.ToString(),
					Text = $"Set {Game.NameOf(weapon)} to full auto",
					Items = 0,
					Mode = true,
					Run = () =>
					{
						mode.SetFireMode(Weapon.EFireMode.fullauto);
						return Task.FromResult(mode.FireMode == Weapon.EFireMode.fullauto ? null : "the weapon refused full auto");
					}
				}.WithItem(weapon));
			}

			if (total == 0)
			{
				return new List<PlanLine> { Steps.Info("No weapon in the picked slots has full auto.") };
			}

			lines.Insert(0, Steps.Progress("Weapons on full auto", inPlace, total, "weapons", shown.TemplateId.ToString()).WithItem(shown));
			return lines;
		}
	}
}
