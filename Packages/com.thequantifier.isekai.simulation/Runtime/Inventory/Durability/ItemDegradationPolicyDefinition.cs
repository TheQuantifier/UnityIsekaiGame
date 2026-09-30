using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory.Durability
{
    [Serializable]
    public sealed class ItemBreakChanceEntryData
    {
        [Range(0, 100)] public int durabilityPercent;
        [Range(0f, 1f)] public float breakChance;

        public ItemBreakChanceEntryData Clone()
        {
            return new ItemBreakChanceEntryData
            {
                durabilityPercent = durabilityPercent,
                breakChance = breakChance
            };
        }
    }

    [CreateAssetMenu(fileName = "ItemDegradationPolicy", menuName = "Unity Isekai Game/Inventory/Item Degradation Policy")]
    public sealed class ItemDegradationPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        public const string StandardPolicyId = "durability-policy.standard";
        public const float StandardFastThreshold = 0.10f;
        public const float StandardImmediateThreshold = 0.05f;
        public const float StandardSlowDurationSeconds = 3600f;
        public const float StandardFastDurationSeconds = 300f;

        [SerializeField] private string policyId = StandardPolicyId;
        [SerializeField] private string displayName = "Standard Item Degradation";
        [SerializeField, Range(0f, 1f)] private float fastDecompositionThreshold = StandardFastThreshold;
        [SerializeField, Range(0f, 1f)] private float immediateDecompositionThreshold = StandardImmediateThreshold;
        [SerializeField, Min(0f)] private float slowDecompositionSeconds = StandardSlowDurationSeconds;
        [SerializeField, Min(0f)] private float fastDecompositionSeconds = StandardFastDurationSeconds;
        [SerializeField] private bool brokenWorldItemsContinueDecay = true;
        [SerializeField] private ItemBreakChanceEntryData[] breakChances = Array.Empty<ItemBreakChanceEntryData>();

        public string Id => policyId;
        public string DisplayName => displayName;
        public float FastDecompositionThreshold => Mathf.Clamp01(fastDecompositionThreshold);
        public float ImmediateDecompositionThreshold => Mathf.Clamp01(immediateDecompositionThreshold);
        public float SlowDecompositionSeconds => Mathf.Max(0f, slowDecompositionSeconds);
        public float FastDecompositionSeconds => Mathf.Max(0f, fastDecompositionSeconds);
        public bool BrokenWorldItemsContinueDecay => brokenWorldItemsContinueDecay;
        public IReadOnlyList<ItemBreakChanceEntryData> BreakChances => breakChances ?? Array.Empty<ItemBreakChanceEntryData>();

        public float BreakChanceForPercent(int durabilityPercent)
        {
            ItemBreakChanceEntryData entry = BreakChances.FirstOrDefault(value => value != null && value.durabilityPercent == durabilityPercent);
            return entry == null ? 0f : Mathf.Clamp01(entry.breakChance);
        }

        public static ItemDegradationPolicyDefinition Resolve(DefinitionRegistry registry)
        {
            if (registry != null && registry.TryGet(StandardPolicyId, out ItemDegradationPolicyDefinition standard))
            {
                return standard;
            }

            return registry?.DefinitionsById.Values
                .OfType<ItemDegradationPolicyDefinition>()
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        public static ItemBreakChanceEntryData[] CreateStandardBreakChances()
        {
            return new[]
            {
                Chance(10, 0.05f),
                Chance(9, 0.07f),
                Chance(8, 0.10f),
                Chance(7, 0.14f),
                Chance(6, 0.19f),
                Chance(5, 0.25f)
            };
        }

        public static float StandardBreakChanceForPercent(int durabilityPercent)
        {
            ItemBreakChanceEntryData entry = CreateStandardBreakChances().FirstOrDefault(value => value.durabilityPercent == durabilityPercent);
            return entry?.breakChance ?? 0f;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (!DefinitionIdValidator.IsValid(Id) || !Id.StartsWith("durability-policy.", StringComparison.Ordinal))
            {
                report?.AddError($"Item degradation policy '{Id}' must use durability-policy.<name>.");
            }

            if (ImmediateDecompositionThreshold <= 0f || ImmediateDecompositionThreshold >= FastDecompositionThreshold)
            {
                report?.AddError($"Item degradation policy '{Id}' must place its immediate threshold above zero and below its fast threshold.");
            }
            if (SlowDecompositionSeconds <= FastDecompositionSeconds || FastDecompositionSeconds <= 0f)
            {
                report?.AddError($"Item degradation policy '{Id}' must use a positive fast duration shorter than its slow duration.");
            }

            int highestPercent = Mathf.RoundToInt(FastDecompositionThreshold * 100f);
            int lowestPercent = Mathf.RoundToInt(ImmediateDecompositionThreshold * 100f);
            HashSet<int> seen = new HashSet<int>();
            foreach (ItemBreakChanceEntryData entry in BreakChances.Where(value => value != null))
            {
                if (!seen.Add(entry.durabilityPercent)) report?.AddError($"Item degradation policy '{Id}' repeats break chance {entry.durabilityPercent}%.");
                if (entry.durabilityPercent < lowestPercent || entry.durabilityPercent > highestPercent) report?.AddError($"Item degradation policy '{Id}' has an out-of-range break chance at {entry.durabilityPercent}%.");
                if (entry.breakChance < 0f || entry.breakChance > 1f) report?.AddError($"Item degradation policy '{Id}' has an invalid break chance at {entry.durabilityPercent}%.");
            }
            for (int percent = lowestPercent; percent <= highestPercent; percent++)
            {
                if (!seen.Contains(percent)) report?.AddError($"Item degradation policy '{Id}' is missing a break chance for {percent}% durability.");
            }
        }

        private static ItemBreakChanceEntryData Chance(int percent, float chance)
        {
            return new ItemBreakChanceEntryData { durabilityPercent = percent, breakChance = chance };
        }
    }
}
