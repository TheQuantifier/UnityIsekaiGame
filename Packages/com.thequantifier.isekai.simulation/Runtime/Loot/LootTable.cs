using System.Collections.Generic;
using UnityEngine;

namespace UnityIsekaiGame.Loot
{
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "Unity Isekai Game/Loot/Loot Table")]
    public sealed class LootTable : ScriptableObject
    {
        [SerializeField] private LootEntry[] entries;

        public IReadOnlyList<LootEntry> Entries => entries;

        private void OnValidate()
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                entries[i]?.Validate();
            }
        }

        public List<LootRoll> Roll(ILootRandom random)
        {
            List<LootRoll> rolls = new List<LootRoll>();
            if (entries == null || random == null)
            {
                return rolls;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                LootEntry entry = entries[i];
                if (entry == null || !entry.TryRoll(random, out var item, out int quantity))
                {
                    continue;
                }

                rolls.Add(new LootRoll(item, quantity));
            }

            return rolls;
        }
    }
}
