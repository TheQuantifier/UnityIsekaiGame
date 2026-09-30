using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge
{
    [CreateAssetMenu(fileName = "KnowledgePolicyDefinition", menuName = "Unity Isekai Game/Knowledge/Knowledge Policy")]
    public sealed class KnowledgePolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        public const string DefaultPolicyId = "knowledge-policy.default";

        [SerializeField] private string policyId = DefaultPolicyId;
        [SerializeField] private string displayName = "Default Knowledge Policy";
        [SerializeField, Range(0, 1000)] private int suspectedThreshold = 1;
        [SerializeField, Range(0, 1000)] private int believedThreshold = 400;
        [SerializeField, Range(0, 1000)] private int stronglyBelievedThreshold = 600;
        [SerializeField, Range(0, 1000)] private int defaultKnownThreshold = 700;
        [SerializeField, Range(0, 1000)] private int defaultForgettingReduction = 250;
        [SerializeField, Range(0, 1000)] private int defaultObservationQuality = 550;
        [SerializeField, Min(0f)] private double memoryMaintenanceIntervalSeconds = 60d;
        [SerializeField, Range(0, 1000)] private int memoryConfidenceLossPerDay = 1;
        [SerializeField, Range(0, 1000)] private int memoryClarityLossPerDay = 2;
        [SerializeField, Range(0, 1000)] private int memorySalienceLossPerDay;

        public string Id => policyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int SuspectedThreshold => KnowledgeConfidence.Clamp(suspectedThreshold);
        public int BelievedThreshold => KnowledgeConfidence.Clamp(believedThreshold);
        public int StronglyBelievedThreshold => KnowledgeConfidence.Clamp(stronglyBelievedThreshold);
        public int DefaultKnownThreshold => KnowledgeConfidence.Clamp(defaultKnownThreshold);
        public int DefaultForgettingReduction => KnowledgeConfidence.Clamp(defaultForgettingReduction);
        public int DefaultObservationQuality => KnowledgeConfidence.Clamp(defaultObservationQuality);
        public double MemoryMaintenanceIntervalSeconds => Math.Max(0d, memoryMaintenanceIntervalSeconds);
        public int MemoryConfidenceLossPerDay => KnowledgeConfidence.Clamp(memoryConfidenceLossPerDay);
        public int MemoryClarityLossPerDay => KnowledgeConfidence.Clamp(memoryClarityLossPerDay);
        public int MemorySalienceLossPerDay => KnowledgeConfidence.Clamp(memorySalienceLossPerDay);

        private void OnValidate()
        {
            policyId = policyId?.Trim();
            suspectedThreshold = KnowledgeConfidence.Clamp(suspectedThreshold);
            believedThreshold = KnowledgeConfidence.Clamp(believedThreshold);
            stronglyBelievedThreshold = KnowledgeConfidence.Clamp(stronglyBelievedThreshold);
            defaultKnownThreshold = KnowledgeConfidence.Clamp(defaultKnownThreshold);
            defaultForgettingReduction = KnowledgeConfidence.Clamp(defaultForgettingReduction);
            defaultObservationQuality = KnowledgeConfidence.Clamp(defaultObservationQuality);
            memoryMaintenanceIntervalSeconds = Math.Max(0d, memoryMaintenanceIntervalSeconds);
            memoryConfidenceLossPerDay = KnowledgeConfidence.Clamp(memoryConfidenceLossPerDay);
            memoryClarityLossPerDay = KnowledgeConfidence.Clamp(memoryClarityLossPerDay);
            memorySalienceLossPerDay = KnowledgeConfidence.Clamp(memorySalienceLossPerDay);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Knowledge Policy is missing a stable ID.");
            }
            else if (!Id.StartsWith("knowledge-policy.", StringComparison.Ordinal))
            {
                report.AddWarning($"Knowledge Policy '{Id}' should use the 'knowledge-policy.' namespace prefix.");
            }

            if (!(SuspectedThreshold <= BelievedThreshold
                && BelievedThreshold <= StronglyBelievedThreshold
                && StronglyBelievedThreshold <= DefaultKnownThreshold))
            {
                report.AddError($"Knowledge Policy '{DisplayName}' belief thresholds must increase from suspected through known.");
            }
        }
    }
}
