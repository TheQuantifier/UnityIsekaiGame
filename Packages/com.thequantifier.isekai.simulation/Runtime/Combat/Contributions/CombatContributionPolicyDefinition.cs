using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Combat.Contributions
{
    [CreateAssetMenu(fileName = "CombatContributionPolicyDefinition", menuName = "Unity Isekai Game/Combat/Contribution Policy")]
    public sealed class CombatContributionPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string policyId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField, Min(0f)] private float contributionWindowSeconds = 30f;
        [SerializeField, Min(0f)] private float minimumDamageContribution = 0.001f;
        [SerializeField, Min(0f)] private float minimumHealingAssistContribution = 5f;
        [SerializeField, Min(0f)] private float minimumDefensiveAssistContribution = 1f;
        [SerializeField, Min(0f)] private float participationTimeoutSeconds = 30f;
        [SerializeField] private bool encounterParticipationQualifies;
        [SerializeField] private bool selfDamageCountsForHostileCredit;
        [SerializeField] private bool selfHealingCountsAsSupport;
        [SerializeField] private bool environmentalCreditCanBeUnassigned = true;
        [SerializeField, Min(1)] private int maximumRetainedRecordsPerLedger = 256;
        [SerializeField, Min(0f)] private float damageScoreWeight = 1f;
        [SerializeField, Min(0f)] private float healingScoreWeight = 0.5f;
        [SerializeField, Min(0f)] private float defensiveScoreWeight = 0.5f;
        [SerializeField] private CombatContributionRecordCompressionPolicy recordCompressionPolicy = CombatContributionRecordCompressionPolicy.KeepAllUntilFinalized;
        [SerializeField] private string[] eligibilityCategories = { "future-experience", "future-skill-progression", "future-quest-hook", "future-loot-eligibility", "diagnostic-only" };

        public string Id => policyId;
        public string PolicyId => policyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public float ContributionWindowSeconds => contributionWindowSeconds;
        public float MinimumDamageContribution => minimumDamageContribution;
        public float MinimumHealingAssistContribution => minimumHealingAssistContribution;
        public float MinimumDefensiveAssistContribution => minimumDefensiveAssistContribution;
        public float ParticipationTimeoutSeconds => participationTimeoutSeconds;
        public bool EncounterParticipationQualifies => encounterParticipationQualifies;
        public bool SelfDamageCountsForHostileCredit => selfDamageCountsForHostileCredit;
        public bool SelfHealingCountsAsSupport => selfHealingCountsAsSupport;
        public bool EnvironmentalCreditCanBeUnassigned => environmentalCreditCanBeUnassigned;
        public int MaximumRetainedRecordsPerLedger => Mathf.Max(1, maximumRetainedRecordsPerLedger);
        public float DamageScoreWeight => damageScoreWeight;
        public float HealingScoreWeight => healingScoreWeight;
        public float DefensiveScoreWeight => defensiveScoreWeight;
        public CombatContributionRecordCompressionPolicy RecordCompressionPolicy => recordCompressionPolicy;
        public IReadOnlyList<string> EligibilityCategories => eligibilityCategories ?? Array.Empty<string>();

        private void OnValidate()
        {
            policyId = policyId?.Trim();
            contributionWindowSeconds = Mathf.Max(0f, contributionWindowSeconds);
            minimumDamageContribution = Mathf.Max(0f, minimumDamageContribution);
            minimumHealingAssistContribution = Mathf.Max(0f, minimumHealingAssistContribution);
            minimumDefensiveAssistContribution = Mathf.Max(0f, minimumDefensiveAssistContribution);
            participationTimeoutSeconds = Mathf.Max(0f, participationTimeoutSeconds);
            maximumRetainedRecordsPerLedger = Mathf.Max(1, maximumRetainedRecordsPerLedger);
            damageScoreWeight = Mathf.Max(0f, damageScoreWeight);
            healingScoreWeight = Mathf.Max(0f, healingScoreWeight);
            defensiveScoreWeight = Mathf.Max(0f, defensiveScoreWeight);
            if (eligibilityCategories != null)
            {
                for (int i = 0; i < eligibilityCategories.Length; i++)
                {
                    eligibilityCategories[i] = eligibilityCategories[i]?.Trim();
                }
            }
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("combat-contribution-policy.", StringComparison.Ordinal))
            {
                report.AddWarning($"CombatContributionPolicyDefinition '{DisplayName}' should use the 'combat-contribution-policy.' namespace prefix.");
            }

            if (!definitionsById.ContainsKey("combat-contribution-policy.alpha"))
            {
                report.AddError("Definition catalog is missing canonical alpha combat contribution policy 'combat-contribution-policy.alpha'.");
            }

            if (!IsFinite(contributionWindowSeconds) || contributionWindowSeconds <= 0f)
            {
                report.AddError($"CombatContributionPolicyDefinition '{DisplayName}' must use a positive finite contribution window.");
            }

            if (!IsFinite(participationTimeoutSeconds) || participationTimeoutSeconds < 0f)
            {
                report.AddError($"CombatContributionPolicyDefinition '{DisplayName}' has an invalid participation timeout.");
            }

            ValidateNonNegativeFinite(minimumDamageContribution, nameof(minimumDamageContribution), report);
            ValidateNonNegativeFinite(minimumHealingAssistContribution, nameof(minimumHealingAssistContribution), report);
            ValidateNonNegativeFinite(minimumDefensiveAssistContribution, nameof(minimumDefensiveAssistContribution), report);
            ValidateNonNegativeFinite(damageScoreWeight, nameof(damageScoreWeight), report);
            ValidateNonNegativeFinite(healingScoreWeight, nameof(healingScoreWeight), report);
            ValidateNonNegativeFinite(defensiveScoreWeight, nameof(defensiveScoreWeight), report);

            if (maximumRetainedRecordsPerLedger < 1)
            {
                report.AddError($"CombatContributionPolicyDefinition '{DisplayName}' must retain at least one record.");
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string category in EligibilityCategories)
            {
                if (string.IsNullOrWhiteSpace(category))
                {
                    report.AddError($"CombatContributionPolicyDefinition '{DisplayName}' has an empty eligibility category.");
                }
                else if (!seen.Add(category))
                {
                    report.AddWarning($"CombatContributionPolicyDefinition '{DisplayName}' has duplicate eligibility category '{category}'.");
                }
            }
        }

        private void ValidateNonNegativeFinite(float value, string label, DefinitionValidationReport report)
        {
            if (!IsFinite(value) || value < 0f)
            {
                report.AddError($"CombatContributionPolicyDefinition '{DisplayName}' has invalid {label}.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
