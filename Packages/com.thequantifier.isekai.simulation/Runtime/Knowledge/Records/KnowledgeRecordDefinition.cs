using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Knowledge.Records
{
    [CreateAssetMenu(fileName = "KnowledgeRecordDefinition", menuName = "Unity Isekai Game/Knowledge/Record Definition")]
    public sealed class KnowledgeRecordDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string recordDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private KnowledgeRecordCategory category = KnowledgeRecordCategory.Unknown;
        [SerializeField] private InformationSubjectType[] allowedSubjectTypes = Array.Empty<InformationSubjectType>();
        [SerializeField] private KnowledgeRecordOwnerKind[] allowedOwnerKinds = Array.Empty<KnowledgeRecordOwnerKind>();
        [SerializeField] private KnowledgeRecordProjectionKind defaultProjectionKind = KnowledgeRecordProjectionKind.ExplicitRecord;
        [SerializeField] private string defaultAccessPolicyId;
        [SerializeField] private KnowledgeRecordPersistencePolicy defaultPersistencePolicy = KnowledgeRecordPersistencePolicy.ExplicitOnly;
        [SerializeField] private string[] defaultIndexingFields = Array.Empty<string>();
        [SerializeField] private string defaultSortingPolicy = "time-then-id";
        [SerializeField] private string defaultGroupingPolicy = "category";
        [SerializeField] private string defaultInclusionRequirementId;
        [SerializeField] private bool explicitDiscoveryRequired;
        [SerializeField] private bool explicitRecordingRequired = true;
        [SerializeField] private bool automaticProjectionAllowed = true;
        [SerializeField] private bool multipleEntriesPerSubjectAllowed = true;
        [SerializeField] private bool correctionsSupported = true;
        [SerializeField] private bool revisionsSupported = true;
        [SerializeField] private bool sourceReferencesRequired;
        [SerializeField] private bool evidenceReferencesRequired;
        [SerializeField] private bool uncertaintyShown = true;
        [SerializeField] private bool contradictionsShown = true;
        [SerializeField] private bool redactionSupported = true;
        [SerializeField] private string titleLocalizationKey;
        [SerializeField] private string summaryLocalizationKey;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int schemaVersion = 1;

        public string Id => recordDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public KnowledgeRecordCategory Category => category;
        public IReadOnlyList<InformationSubjectType> AllowedSubjectTypes => allowedSubjectTypes ?? Array.Empty<InformationSubjectType>();
        public IReadOnlyList<KnowledgeRecordOwnerKind> AllowedOwnerKinds => allowedOwnerKinds ?? Array.Empty<KnowledgeRecordOwnerKind>();
        public KnowledgeRecordProjectionKind DefaultProjectionKind => defaultProjectionKind;
        public string DefaultAccessPolicyId => defaultAccessPolicyId ?? string.Empty;
        public KnowledgeRecordPersistencePolicy DefaultPersistencePolicy => defaultPersistencePolicy;
        public IReadOnlyList<string> DefaultIndexingFields => defaultIndexingFields ?? Array.Empty<string>();
        public string DefaultSortingPolicy => string.IsNullOrWhiteSpace(defaultSortingPolicy) ? "time-then-id" : defaultSortingPolicy;
        public string DefaultGroupingPolicy => defaultGroupingPolicy ?? string.Empty;
        public string DefaultInclusionRequirementId => defaultInclusionRequirementId ?? string.Empty;
        public bool ExplicitDiscoveryRequired => explicitDiscoveryRequired;
        public bool ExplicitRecordingRequired => explicitRecordingRequired;
        public bool AutomaticProjectionAllowed => automaticProjectionAllowed;
        public bool MultipleEntriesPerSubjectAllowed => multipleEntriesPerSubjectAllowed;
        public bool CorrectionsSupported => correctionsSupported;
        public bool RevisionsSupported => revisionsSupported;
        public bool SourceReferencesRequired => sourceReferencesRequired;
        public bool EvidenceReferencesRequired => evidenceReferencesRequired;
        public bool UncertaintyShown => uncertaintyShown;
        public bool ContradictionsShown => contradictionsShown;
        public bool RedactionSupported => redactionSupported;
        public string TitleLocalizationKey => titleLocalizationKey ?? string.Empty;
        public string SummaryLocalizationKey => summaryLocalizationKey ?? string.Empty;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int SchemaVersion => Math.Max(1, schemaVersion);

        private void OnValidate()
        {
            recordDefinitionId = recordDefinitionId?.Trim();
            schemaVersion = Math.Max(1, schemaVersion);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            KnowledgeRecordCategory recordCategory,
            InformationSubjectType[] subjectTypes,
            KnowledgeRecordOwnerKind[] ownerKinds,
            KnowledgeRecordProjectionKind projectionKind = KnowledgeRecordProjectionKind.ExplicitRecord,
            KnowledgeRecordPersistencePolicy persistencePolicy = KnowledgeRecordPersistencePolicy.ExplicitOnly,
            string accessPolicyId = "")
        {
            recordDefinitionId = id;
            displayName = name;
            category = recordCategory;
            allowedSubjectTypes = subjectTypes ?? Array.Empty<InformationSubjectType>();
            allowedOwnerKinds = ownerKinds ?? Array.Empty<KnowledgeRecordOwnerKind>();
            defaultProjectionKind = projectionKind;
            defaultPersistencePolicy = persistencePolicy;
            defaultAccessPolicyId = accessPolicyId ?? string.Empty;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Knowledge Record Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("record-definition.", StringComparison.Ordinal))
            {
                report.AddWarning($"Knowledge Record Definition '{Id}' should use the 'record-definition.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(KnowledgeRecordCategory), category) || category == KnowledgeRecordCategory.Unknown)
            {
                report.AddError($"Knowledge Record Definition '{DisplayName}' must declare a concrete record category.");
            }

            if (!Enum.IsDefined(typeof(KnowledgeRecordProjectionKind), defaultProjectionKind) || defaultProjectionKind == KnowledgeRecordProjectionKind.Unknown)
            {
                report.AddError($"Knowledge Record Definition '{DisplayName}' must declare a concrete projection kind.");
            }

            if (!Enum.IsDefined(typeof(KnowledgeRecordPersistencePolicy), defaultPersistencePolicy) || defaultPersistencePolicy == KnowledgeRecordPersistencePolicy.Unknown)
            {
                report.AddError($"Knowledge Record Definition '{DisplayName}' must declare a concrete persistence policy.");
            }

            if (allowedSubjectTypes == null || allowedSubjectTypes.Length == 0)
            {
                report.AddError($"Knowledge Record Definition '{DisplayName}' must declare at least one allowed subject type.");
            }
            else
            {
                foreach (InformationSubjectType subjectType in allowedSubjectTypes)
                {
                    if (!Enum.IsDefined(typeof(InformationSubjectType), subjectType) || subjectType == InformationSubjectType.Unknown)
                    {
                        report.AddError($"Knowledge Record Definition '{DisplayName}' has an invalid allowed subject type.");
                    }
                }
            }

            if (allowedOwnerKinds == null || allowedOwnerKinds.Length == 0)
            {
                report.AddError($"Knowledge Record Definition '{DisplayName}' must declare at least one allowed owner kind.");
            }
            else
            {
                foreach (KnowledgeRecordOwnerKind ownerKind in allowedOwnerKinds)
                {
                    if (!Enum.IsDefined(typeof(KnowledgeRecordOwnerKind), ownerKind) || ownerKind == KnowledgeRecordOwnerKind.Unknown)
                    {
                        report.AddError($"Knowledge Record Definition '{DisplayName}' has an invalid allowed owner kind.");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(defaultAccessPolicyId)
                && definitionsById != null
                && !definitionsById.ContainsKey(defaultAccessPolicyId))
            {
                report.AddError($"Knowledge Record Definition '{DisplayName}' references missing access policy definition '{defaultAccessPolicyId}'.");
            }
        }
    }
}
