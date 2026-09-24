using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Progression
{
    [CreateAssetMenu(fileName = "OriginGenerationPolicy", menuName = "Unity Isekai Game/Progression/Origin Generation Policy")]
    public sealed class OriginGenerationPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        public const string DefaultPolicyId = "origin-generation.default";

        [SerializeField] private string policyId = DefaultPolicyId;
        [SerializeField] private string displayName = "Default Origin Generation";
        [SerializeField, Min(0f)] private float favoredGiftWeightMultiplier = 2f;
        [SerializeField, Min(0f)] private float unfavoredGiftWeightMultiplier = 0.75f;

        public string Id => policyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public float FavoredGiftWeightMultiplier => Mathf.Max(0f, favoredGiftWeightMultiplier);
        public float UnfavoredGiftWeightMultiplier => Mathf.Max(0f, unfavoredGiftWeightMultiplier);

        private void OnValidate()
        {
            favoredGiftWeightMultiplier = Mathf.Max(0f, favoredGiftWeightMultiplier);
            unfavoredGiftWeightMultiplier = Mathf.Max(0f, unfavoredGiftWeightMultiplier);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!string.Equals(Id, DefaultPolicyId, System.StringComparison.Ordinal))
            {
                report.AddError($"Origin generation policy must use the canonical ID '{DefaultPolicyId}'.");
            }

            if (!IsFinitePositive(favoredGiftWeightMultiplier))
            {
                report.AddError("Origin generation favored-gift multiplier must be finite and greater than zero.");
            }

            if (!IsFinitePositive(unfavoredGiftWeightMultiplier))
            {
                report.AddError("Origin generation unfavored-gift multiplier must be finite and greater than zero.");
            }

            if (favoredGiftWeightMultiplier <= unfavoredGiftWeightMultiplier)
            {
                report.AddError("Origin generation favored gifts must have a greater multiplier than unfavored gifts.");
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
