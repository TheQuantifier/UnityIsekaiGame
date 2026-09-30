using System;

namespace UnityIsekaiGame.Magic
{
    public readonly struct SpellLoadoutSlotChangedEventArgs
    {
        public SpellLoadoutSlotChangedEventArgs(int slotIndex, SpellDefinition spell)
        {
            SlotIndex = slotIndex;
            Spell = spell;
        }

        public int SlotIndex { get; }
        public SpellDefinition Spell { get; }
    }
}
