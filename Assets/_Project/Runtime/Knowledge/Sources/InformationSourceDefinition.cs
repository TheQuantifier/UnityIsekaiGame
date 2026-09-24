using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge.Sources
{
    [CreateAssetMenu(fileName = "InformationSourceDefinition", menuName = "Unity Isekai Game/Knowledge/Information Source Definition")]
    public sealed class InformationSourceDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string sourceDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private InformationSourceCategory category = InformationSourceCategory.Unknown;
        [SerializeField] private ReliabilityProfileData defaultReliability = ReliabilityProfileData.Default();
        [SerializeField] private KnowledgeDomain[] supportedDomains;
        [SerializeField] private string[] supportedMethodIds;
        [SerializeField] private string[] authorityClassifications;
        [SerializeField, Range(0, 1000)] private int defaultErrorRisk = 250;
        [SerializeField, Range(0, 1000)] private int defaultDeceptionRisk = 150;
        [SerializeField, Range(0, 1000)] private int defaultBiasRisk = 150;
        [SerializeField] private KnowledgeStalenessPolicy stalenessPolicy = KnowledgeStalenessPolicy.NeverStale;
        [SerializeField, Min(0)] private double stalenessHalfLifeSeconds;
        [SerializeField, Range(0, 1000)] private int transmissionPenaltyPerHop = 80;
        [SerializeField] private bool allowsAnonymous;
        [SerializeField] private bool allowsCopying = true;
        [SerializeField] private bool allowsTranslation = true;
        [SerializeField] private bool allowsSummary = true;
        [SerializeField] private bool requiresIdentityVerification;
        [SerializeField] private string[] tags;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => sourceDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public InformationSourceCategory Category => category;
        public ReliabilityProfileData DefaultReliability => defaultReliability?.Clone() ?? ReliabilityProfileData.Default();
        public IReadOnlyList<KnowledgeDomain> SupportedDomains => supportedDomains ?? Array.Empty<KnowledgeDomain>();
        public IReadOnlyList<string> SupportedMethodIds => supportedMethodIds ?? Array.Empty<string>();
        public IReadOnlyList<string> AuthorityClassifications => authorityClassifications ?? Array.Empty<string>();
        public int DefaultErrorRisk => KnowledgeConfidence.Clamp(defaultErrorRisk);
        public int DefaultDeceptionRisk => KnowledgeConfidence.Clamp(defaultDeceptionRisk);
        public int DefaultBiasRisk => KnowledgeConfidence.Clamp(defaultBiasRisk);
        public KnowledgeStalenessPolicy StalenessPolicy => stalenessPolicy;
        public double StalenessHalfLifeSeconds => Math.Max(0d, stalenessHalfLifeSeconds);
        public int TransmissionPenaltyPerHop => KnowledgeConfidence.Clamp(transmissionPenaltyPerHop);
        public bool AllowsAnonymous => allowsAnonymous;
        public bool AllowsCopying => allowsCopying;
        public bool AllowsTranslation => allowsTranslation;
        public bool AllowsSummary => allowsSummary;
        public bool RequiresIdentityVerification => requiresIdentityVerification;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => Math.Max(1, version);

        private void OnValidate()
        {
            sourceDefinitionId = sourceDefinitionId?.Trim();
            defaultErrorRisk = KnowledgeConfidence.Clamp(defaultErrorRisk);
            defaultDeceptionRisk = KnowledgeConfidence.Clamp(defaultDeceptionRisk);
            defaultBiasRisk = KnowledgeConfidence.Clamp(defaultBiasRisk);
            transmissionPenaltyPerHop = KnowledgeConfidence.Clamp(transmissionPenaltyPerHop);
            stalenessHalfLifeSeconds = Math.Max(0d, stalenessHalfLifeSeconds);
            version = Math.Max(1, version);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DevelopmentConfigure(
            string id,
            string label,
            InformationSourceCategory sourceCategory,
            ReliabilityProfileData reliability,
            KnowledgeStalenessPolicy policy = KnowledgeStalenessPolicy.NeverStale,
            double halfLifeSeconds = 0d,
            int transmissionPenalty = 80,
            bool identityVerification = false)
        {
            sourceDefinitionId = id;
            displayName = label;
            category = sourceCategory;
            defaultReliability = reliability?.Clone() ?? ReliabilityProfileData.Default();
            stalenessPolicy = policy;
            stalenessHalfLifeSeconds = Math.Max(0d, halfLifeSeconds);
            transmissionPenaltyPerHop = KnowledgeConfidence.Clamp(transmissionPenalty);
            requiresIdentityVerification = identityVerification;
            version = Math.Max(1, version);
        }
#endif

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Information Source '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("information-source.", StringComparison.Ordinal))
            {
                report.AddWarning($"Information Source '{Id}' should use the 'information-source.' namespace prefix.");
            }

            if (category == InformationSourceCategory.Unknown)
            {
                report.AddError($"Information Source '{DisplayName}' must declare a concrete source category.");
            }

            if (requiresIdentityVerification && allowsAnonymous)
            {
                report.AddError($"Information Source '{DisplayName}' cannot require identity verification while also allowing anonymous use.");
            }

            string failure = string.Empty;
            if (defaultReliability == null || !defaultReliability.IsValid(out failure))
            {
                report.AddError($"Information Source '{DisplayName}' has invalid default reliability: {failure}");
            }

            ValidateEnum(category, nameof(InformationSourceCategory), report);
            ValidateEnum(stalenessPolicy, nameof(KnowledgeStalenessPolicy), report);

            foreach (string methodId in SupportedMethodIds)
            {
                if (!string.IsNullOrWhiteSpace(methodId) && definitionsById != null && !definitionsById.ContainsKey(methodId))
                {
                    report.AddError($"Information Source '{Id}' references missing supported method '{methodId}'.");
                }
            }
        }

        private void ValidateEnum<T>(T value, string enumName, DefinitionValidationReport report)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                report.AddError($"Information Source '{DisplayName}' has an invalid {enumName} value.");
            }
        }
    }
}
