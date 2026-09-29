using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Observation;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Knowledge.Sharing
{
    [CreateAssetMenu(fileName = "InformationTransferDefinition", menuName = "Unity Isekai Game/Knowledge/Information Transfer Definition")]
    public sealed class InformationTransferDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string transferDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private InformationTransferMode mode = InformationTransferMode.DirectTestimony;
        [SerializeField] private KnowledgeDomain[] supportedDomains;
        [SerializeField] private InformationSourceCategory[] allowedSourceCategories;
        [SerializeField] private bool recallRequired;
        [SerializeField] private bool directKnowledgeAccessPermitted = true;
        [SerializeField] private bool writtenPersistenceInvolved;
        [SerializeField] private bool publicAllowed = true;
        [SerializeField] private bool privateAllowed = true;
        [SerializeField] private bool secretAllowed;
        [SerializeField] private bool summarizationAllowed;
        [SerializeField] private bool translationAllowed;
        [SerializeField] private bool demonstrationAllowed;
        [SerializeField, Range(0, 1000)] private int defaultFidelity = 800;
        [SerializeField, Range(0, 1000)] private int defaultCompleteness = 800;
        [SerializeField, Range(0, 1000)] private int defaultTransmissionCost = 100;
        [SerializeField, Range(0, 1000)] private int defaultEvidenceStrength = 650;
        [SerializeField] private TransferInheritedConfidencePolicy inheritedConfidencePolicy = TransferInheritedConfidencePolicy.SourceReliabilityAdjusted;
        [SerializeField] private TransferMemoryPolicy memoryPolicy = TransferMemoryPolicy.FormCommunicationMemory;
        [SerializeField] private TransferEvidencePolicy evidencePolicy = TransferEvidencePolicy.CreateRecipientEvidence;
        [SerializeField] private TransferPrivacyScope defaultPrivacy = TransferPrivacyScope.RecipientOnly;
        [SerializeField] private string requiredCapabilityId;
        [SerializeField] private string requiredMethodId;
        [SerializeField] private string versionMetadata = "1";
        [SerializeField] private string[] tags;

        public string Id => transferDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public InformationTransferMode Mode => mode;
        public IReadOnlyList<KnowledgeDomain> SupportedDomains => supportedDomains ?? Array.Empty<KnowledgeDomain>();
        public IReadOnlyList<InformationSourceCategory> AllowedSourceCategories => allowedSourceCategories ?? Array.Empty<InformationSourceCategory>();
        public bool RecallRequired => recallRequired;
        public bool DirectKnowledgeAccessPermitted => directKnowledgeAccessPermitted;
        public bool WrittenPersistenceInvolved => writtenPersistenceInvolved;
        public bool PublicAllowed => publicAllowed;
        public bool PrivateAllowed => privateAllowed;
        public bool SecretAllowed => secretAllowed;
        public bool SummarizationAllowed => summarizationAllowed;
        public bool TranslationAllowed => translationAllowed;
        public bool DemonstrationAllowed => demonstrationAllowed;
        public int DefaultFidelity => KnowledgeConfidence.Clamp(defaultFidelity);
        public int DefaultCompleteness => KnowledgeConfidence.Clamp(defaultCompleteness);
        public int DefaultTransmissionCost => KnowledgeConfidence.Clamp(defaultTransmissionCost);
        public int DefaultEvidenceStrength => KnowledgeConfidence.Clamp(defaultEvidenceStrength);
        public TransferInheritedConfidencePolicy InheritedConfidencePolicy => inheritedConfidencePolicy;
        public TransferMemoryPolicy MemoryPolicy => memoryPolicy;
        public TransferEvidencePolicy EvidencePolicy => evidencePolicy;
        public TransferPrivacyScope DefaultPrivacy => defaultPrivacy;
        public string RequiredCapabilityId => requiredCapabilityId ?? string.Empty;
        public string RequiredMethodId => requiredMethodId ?? string.Empty;
        public string VersionMetadata => versionMetadata ?? string.Empty;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

        private void OnValidate()
        {
            transferDefinitionId = transferDefinitionId?.Trim();
            defaultFidelity = KnowledgeConfidence.Clamp(defaultFidelity);
            defaultCompleteness = KnowledgeConfidence.Clamp(defaultCompleteness);
            defaultTransmissionCost = KnowledgeConfidence.Clamp(defaultTransmissionCost);
            defaultEvidenceStrength = KnowledgeConfidence.Clamp(defaultEvidenceStrength);
        }

        public void DevelopmentConfigure(
            string id,
            string label,
            InformationTransferMode transferMode,
            KnowledgeDomain[] domains,
            InformationSourceCategory[] sourceCategories,
            bool requiresRecall,
            bool allowsSummary,
            bool allowsTranslation,
            bool allowsDemonstration,
            int fidelity,
            int completeness,
            TransferMemoryPolicy transferMemoryPolicy,
            TransferEvidencePolicy transferEvidencePolicy)
        {
            transferDefinitionId = id;
            displayName = label;
            mode = transferMode;
            supportedDomains = domains ?? Array.Empty<KnowledgeDomain>();
            allowedSourceCategories = sourceCategories ?? Array.Empty<InformationSourceCategory>();
            recallRequired = requiresRecall;
            summarizationAllowed = allowsSummary;
            translationAllowed = allowsTranslation;
            demonstrationAllowed = allowsDemonstration;
            defaultFidelity = KnowledgeConfidence.Clamp(fidelity);
            defaultCompleteness = KnowledgeConfidence.Clamp(completeness);
            memoryPolicy = transferMemoryPolicy;
            evidencePolicy = transferEvidencePolicy;
            versionMetadata = "development";
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Information Transfer '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("information-transfer.", StringComparison.Ordinal))
            {
                report.AddWarning($"Information Transfer '{Id}' should use the 'information-transfer.' namespace prefix.");
            }

            ValidateEnum(mode, nameof(InformationTransferMode), report);
            ValidateEnum(inheritedConfidencePolicy, nameof(TransferInheritedConfidencePolicy), report);
            ValidateEnum(memoryPolicy, nameof(TransferMemoryPolicy), report);
            ValidateEnum(evidencePolicy, nameof(TransferEvidencePolicy), report);
            ValidateEnum(defaultPrivacy, nameof(TransferPrivacyScope), report);

            if (mode == InformationTransferMode.Unknown)
            {
                report.AddError($"Information Transfer '{DisplayName}' must declare a concrete transfer mode.");
            }

            if (supportedDomains == null || supportedDomains.Length == 0)
            {
                report.AddError($"Information Transfer '{DisplayName}' must support at least one Knowledge domain.");
            }

            if (allowedSourceCategories == null || allowedSourceCategories.Length == 0)
            {
                report.AddError($"Information Transfer '{DisplayName}' must allow at least one Information Source category.");
            }

            if (!publicAllowed && !privateAllowed && !secretAllowed)
            {
                report.AddError($"Information Transfer '{DisplayName}' does not allow any privacy scope.");
            }

            if (mode == InformationTransferMode.Demonstration && !demonstrationAllowed)
            {
                report.AddError($"Information Transfer '{DisplayName}' uses Demonstration mode but demonstrations are disabled.");
            }

            if ((mode == InformationTransferMode.Summary || mode == InformationTransferMode.RumorRetelling) && !summarizationAllowed)
            {
                report.AddError($"Information Transfer '{DisplayName}' uses a summary-like mode but summarization is disabled.");
            }

            if (mode == InformationTransferMode.Translation && !translationAllowed)
            {
                report.AddError($"Information Transfer '{DisplayName}' uses Translation mode but translation is disabled.");
            }

            if (defaultFidelity < KnowledgeConfidence.Minimum || defaultCompleteness < KnowledgeConfidence.Minimum)
            {
                report.AddError($"Information Transfer '{DisplayName}' has invalid fidelity or completeness.");
            }

            MethodValidation.ValidateOptionalReference(RequiredCapabilityId, definitionsById, "capability", Id, report);
            MethodValidation.ValidateOptionalReference(RequiredMethodId, definitionsById, "method", Id, report);
        }

        private void ValidateEnum<T>(T value, string enumName, DefinitionValidationReport report)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                report.AddError($"Information Transfer '{DisplayName}' has an invalid {enumName} value.");
            }
        }
    }
}
