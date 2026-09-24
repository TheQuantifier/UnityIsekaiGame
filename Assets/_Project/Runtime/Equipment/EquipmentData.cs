using System;
using UnityEngine;
using UnityIsekaiGame.Combat;

namespace UnityIsekaiGame.Equipment
{
    [Serializable]
    public sealed class EquipmentData
    {
        [SerializeField] private bool equippable;
        [SerializeField] private EquipmentSlotType slotType;
        [SerializeField] private StatModifiers statModifiers;
        [SerializeField] private ResistanceModifierDefinition[] resistanceModifiers;
        [SerializeField] private EquipmentViewData view;
        [SerializeField] private MeleeWeaponData meleeWeapon;
        [SerializeField] private RangedWeaponData rangedWeapon;

        public bool Equippable => equippable;
        public EquipmentSlotType SlotType => slotType;
        public StatModifiers StatModifiers => statModifiers;
        public System.Collections.Generic.IReadOnlyList<ResistanceModifierDefinition> ResistanceModifiers => resistanceModifiers ?? Array.Empty<ResistanceModifierDefinition>();
        public EquipmentViewData View => view;
        public MeleeWeaponData MeleeWeapon => meleeWeapon;
        public RangedWeaponData RangedWeapon => rangedWeapon;

        public void Enable()
        {
            equippable = true;
        }

        public void EnableMeleeWeapon()
        {
            equippable = true;
            meleeWeapon ??= new MeleeWeaponData();
            meleeWeapon.SetEnabled(true);
        }

        public void EnableRangedWeapon()
        {
            equippable = true;
            rangedWeapon ??= new RangedWeaponData();
            rangedWeapon.SetEnabled(true);
        }

        public void RemoveMeleeWeapon()
        {
            meleeWeapon = null;
        }

        public void RemoveRangedWeapon()
        {
            rangedWeapon = null;
        }

        public void PruneInactiveCapabilities()
        {
            if (meleeWeapon != null && !meleeWeapon.IsWeapon) meleeWeapon = null;
            if (rangedWeapon != null && !rangedWeapon.IsWeapon) rangedWeapon = null;
            if (view != null && !view.HasFirstPersonPrefab) view = null;
            if (resistanceModifiers != null && resistanceModifiers.Length == 0) resistanceModifiers = null;
        }

        public void Validate()
        {
            meleeWeapon?.Validate();
            rangedWeapon?.Validate();
            PruneInactiveCapabilities();
        }
    }
}
