using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Abilities;

namespace UnityIsekaiGame.Magic
{
    public sealed class PlayerSpellLoadout : MonoBehaviour
    {
        [SerializeField, Min(4)] private int slotCount = 4;
        [SerializeField] private SpellDefinition[] knownSpells;
        [SerializeField] private SpellDefinition[] quickSlots;
        [SerializeField, Min(0)] private int selectedSlotIndex;
        [SerializeField] private CharacterAbilityCollection abilities;

        public IReadOnlyList<SpellDefinition> KnownSpells => knownSpells ?? Array.Empty<SpellDefinition>();
        public IReadOnlyList<SpellDefinition> QuickSlots => quickSlots ?? Array.Empty<SpellDefinition>();
        public int SlotCount => quickSlots == null ? 0 : quickSlots.Length;
        public int SelectedSlotIndex => selectedSlotIndex;
        public SpellDefinition SelectedSpell => IsValidSlotIndex(selectedSlotIndex) ? quickSlots[selectedSlotIndex] : null;

        public event Action<SpellLoadoutSlotChangedEventArgs> SlotChanged;
        public event Action<int, SpellDefinition> ActiveSlotChanged;

        private void Awake()
        {
            EnsureSlotCapacity();
            abilities = abilities == null ? GetComponent<CharacterAbilityCollection>() : abilities;
        }

        private void OnValidate()
        {
            slotCount = Mathf.Max(4, slotCount);
            EnsureSlotCapacity();
            selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, quickSlots.Length - 1);
        }

        public bool KnowsSpell(SpellDefinition spell)
        {
            if (spell == null || knownSpells == null)
            {
                return false;
            }

            for (int i = 0; i < knownSpells.Length; i++)
            {
                if (knownSpells[i] == spell)
                {
                    return true;
                }
            }

            return false;
        }

        public SpellLoadoutOperationResult LearnSpell(SpellDefinition spell)
        {
            EnsureSlotCapacity();
            if (spell == null)
            {
                return SpellLoadoutOperationResult.Failure("Spell is missing.");
            }

            if (KnowsSpell(spell))
            {
                return SpellLoadoutOperationResult.Success($"{spell.DisplayName} is already known.");
            }

            List<SpellDefinition> updated = new List<SpellDefinition>(knownSpells ?? Array.Empty<SpellDefinition>());
            updated.Add(spell);
            knownSpells = updated.ToArray();
            return SpellLoadoutOperationResult.Success($"Learned spell {spell.DisplayName}.");
        }

        public void SynchronizeAuthoredAbilityGrants(bool restoring = false)
        {
            abilities = abilities == null ? GetComponent<CharacterAbilityCollection>() : abilities;
            if (abilities == null)
            {
                return;
            }

            const string sourceId = "spell-loadout.authored";
            abilities.RemoveSource(AbilityGrantSourceCategory.AuthoredLoadout, sourceId, restoring);
            foreach (SpellDefinition spell in knownSpells ?? Array.Empty<SpellDefinition>())
            {
                if (spell?.Ability != null)
                {
                    abilities.Grant(spell.Ability, AbilityGrantSourceCategory.AuthoredLoadout, sourceId, restoring);
                }
            }
        }

        public SpellDefinition GetSlotSpell(int slotIndex)
        {
            EnsureSlotCapacity();
            return IsValidSlotIndex(slotIndex) ? quickSlots[slotIndex] : null;
        }

        public SpellLoadoutOperationResult AssignSpell(int slotIndex, SpellDefinition spell)
        {
            EnsureSlotCapacity();
            if (!IsValidSlotIndex(slotIndex))
            {
                return SpellLoadoutOperationResult.Failure("Invalid spell slot.");
            }

            if (!KnowsSpell(spell) || (abilities != null && spell?.Ability != null && !abilities.HasAbility(spell.Ability.Id)))
            {
                return SpellLoadoutOperationResult.Failure("Spell is not owned by this character.");
            }

            quickSlots[slotIndex] = spell;
            SlotChanged?.Invoke(new SpellLoadoutSlotChangedEventArgs(slotIndex, spell));
            return SpellLoadoutOperationResult.Success($"Assigned {spell.DisplayName} to spell slot {slotIndex + 1}.");
        }

        public SpellLoadoutOperationResult ClearSlot(int slotIndex)
        {
            EnsureSlotCapacity();
            if (!IsValidSlotIndex(slotIndex))
            {
                return SpellLoadoutOperationResult.Failure("Invalid spell slot.");
            }

            quickSlots[slotIndex] = null;
            SlotChanged?.Invoke(new SpellLoadoutSlotChangedEventArgs(slotIndex, null));
            return SpellLoadoutOperationResult.Success($"Cleared spell slot {slotIndex + 1}.");
        }

        public SpellLoadoutOperationResult SelectSlot(int slotIndex)
        {
            EnsureSlotCapacity();
            if (!IsValidSlotIndex(slotIndex))
            {
                return SpellLoadoutOperationResult.Failure("Invalid spell slot.");
            }

            selectedSlotIndex = slotIndex;
            ActiveSlotChanged?.Invoke(selectedSlotIndex, quickSlots[selectedSlotIndex]);

            SpellDefinition spell = quickSlots[selectedSlotIndex];
            string spellName = spell == null ? "Empty" : spell.DisplayName;
            return SpellLoadoutOperationResult.Success($"Selected spell slot {selectedSlotIndex + 1}: {spellName}.");
        }

        public SpellLoadoutOperationResult SelectNextAssignedSlot(int direction)
        {
            EnsureSlotCapacity();
            if (quickSlots.Length == 0)
            {
                return SpellLoadoutOperationResult.Failure("No spell slots configured.");
            }

            int step = direction >= 0 ? 1 : -1;
            for (int offset = 1; offset <= quickSlots.Length; offset++)
            {
                int candidate = (selectedSlotIndex + offset * step + quickSlots.Length) % quickSlots.Length;
                if (quickSlots[candidate] != null)
                {
                    return SelectSlot(candidate);
                }
            }

            return SpellLoadoutOperationResult.Failure("No assigned spells.");
        }

        private bool IsValidSlotIndex(int slotIndex)
        {
            return quickSlots != null && slotIndex >= 0 && slotIndex < quickSlots.Length;
        }

        private void EnsureSlotCapacity()
        {
            if (quickSlots == null || quickSlots.Length != slotCount)
            {
                Array.Resize(ref quickSlots, slotCount);
            }

            knownSpells ??= Array.Empty<SpellDefinition>();
        }
    }
}
