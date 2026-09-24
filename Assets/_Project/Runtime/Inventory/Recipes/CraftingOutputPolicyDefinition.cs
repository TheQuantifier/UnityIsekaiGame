using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory.Recipes
{
    public enum CraftingOutputPolicyKind
    {
        Unknown = 0,
        CompositionTransfer = 1,
        QualityGeneration = 2,
        AffixGeneration = 3,
        DurabilityInitialization = 4
    }

    [CreateAssetMenu(fileName = "CraftingOutputPolicyDefinition", menuName = "Unity Isekai Game/Inventory/Crafting Output Policy Definition")]
    public sealed class CraftingOutputPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string policyId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CraftingOutputPolicyKind kind;
        [SerializeField, Range(0f, 1f)] private float baseQualityNormalized = 0.5f;
        [SerializeField, Range(0f, 1f)] private float initialDurabilityNormalized = 1f;
        [SerializeField, Range(0f, 1f)] private float affixChance;
        [SerializeField, Min(0)] private int maximumAffixCount = 1;

        public string Id => policyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public CraftingOutputPolicyKind Kind => kind;
        public float BaseQualityNormalized => Mathf.Clamp01(baseQualityNormalized);
        public float InitialDurabilityNormalized => Mathf.Clamp01(initialDurabilityNormalized);
        public float AffixChance => Mathf.Clamp01(affixChance);
        public int MaximumAffixCount => Mathf.Max(0, maximumAffixCount);

        private void OnValidate()
        {
            policyId = policyId?.Trim();
            baseQualityNormalized = Mathf.Clamp01(baseQualityNormalized);
            initialDurabilityNormalized = Mathf.Clamp01(initialDurabilityNormalized);
            affixChance = Mathf.Clamp01(affixChance);
            maximumAffixCount = Mathf.Max(0, maximumAffixCount);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Crafting output policy '{name}' is missing an ID.");
            }

            if (!Enum.IsDefined(typeof(CraftingOutputPolicyKind), kind) || kind == CraftingOutputPolicyKind.Unknown)
            {
                report.AddError($"Crafting output policy '{DisplayName}' must declare a concrete kind.");
            }

            if (kind != CraftingOutputPolicyKind.AffixGeneration && (affixChance > 0f || maximumAffixCount != 0))
            {
                report.AddError($"Crafting output policy '{DisplayName}' declares affix settings but is not an affix-generation policy.");
            }
        }
    }
}
