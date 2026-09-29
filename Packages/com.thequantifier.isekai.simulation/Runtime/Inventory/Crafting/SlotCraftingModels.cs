using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Inventory.Production;

namespace UnityIsekaiGame.Inventory.Crafting
{
    public enum CraftingSlotKind
    {
        RequiredInput = 0,
        OptionalCatalyst = 10
    }

    [Serializable]
    public sealed class CraftingSlotEntryData
    {
        public string slotId;
        public CraftingSlotKind kind;
        public string recipeInputId;
        public string itemDefinitionId;
        public string itemInstanceId;
        public int quantity = 1;

        public CraftingSlotEntryData Clone()
        {
            return new CraftingSlotEntryData
            {
                slotId = slotId ?? string.Empty,
                kind = kind,
                recipeInputId = recipeInputId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                quantity = quantity
            };
        }
    }

    [Serializable]
    public sealed class SlotCraftingRequest
    {
        public string recipeId;
        public List<CraftingSlotEntryData> slots = new List<CraftingSlotEntryData>();

        public SlotCraftingRequest Clone()
        {
            return new SlotCraftingRequest
            {
                recipeId = recipeId ?? string.Empty,
                slots = (slots ?? new List<CraftingSlotEntryData>()).Select(slot => slot?.Clone()).Where(slot => slot != null).ToList()
            };
        }
    }

    [Serializable]
    public sealed class CraftingCatalystUseData
    {
        public string slotId;
        public string itemDefinitionId;
        public string itemInstanceId;
        public int quantity = 1;
        public bool consumed;
        public string effectDefinitionId;
        public string affixDefinitionId;
        public string affixTierId;
        public float effectChance;
        public float effectRoll;
        public bool effectApplied;

        public CraftingCatalystUseData Clone()
        {
            return new CraftingCatalystUseData
            {
                slotId = slotId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                quantity = quantity,
                consumed = consumed,
                effectDefinitionId = effectDefinitionId ?? string.Empty,
                affixDefinitionId = affixDefinitionId ?? string.Empty,
                affixTierId = affixTierId ?? string.Empty,
                effectChance = effectChance,
                effectRoll = effectRoll,
                effectApplied = effectApplied
            };
        }
    }
}
