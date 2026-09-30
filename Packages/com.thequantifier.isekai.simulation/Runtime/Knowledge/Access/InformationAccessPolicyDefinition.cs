using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge.Access
{
    [CreateAssetMenu(fileName = "NewInformationAccessPolicy", menuName = "Unity Isekai Game/Knowledge/Information Access Policy")]
    public sealed class InformationAccessPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string policyId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private InformationSubjectType subjectType = InformationSubjectType.Custom;
        [SerializeField] private InformationVisibilityClassification classification = InformationVisibilityClassification.Public;
        [SerializeField] private InformationDisclosurePolicy disclosurePolicy = InformationDisclosurePolicy.SameAsAccess;
        [SerializeField] private InformationResharingPolicy resharingPolicy = InformationResharingPolicy.FreelyReshareable;
        [SerializeField] private InformationSourceVisibilityPolicy sourceVisibilityPolicy = InformationSourceVisibilityPolicy.Reveal;
        [SerializeField] private InformationDetailVisibilityPolicy detailVisibilityPolicy = InformationDetailVisibilityPolicy.All;
        [SerializeField] private InformationAuditPolicy auditPolicy = InformationAuditPolicy.None;
        [SerializeField] private string[] defaultVisibleDetails;
        [SerializeField] private string[] defaultRedactedDetails;
        [SerializeField] private string[] defaultHiddenDetails;
        [SerializeField] private bool discoveryRequired;
        [SerializeField] private bool redactedAccessAcceptable = true;

        public string Id => policyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public InformationSubjectType SubjectType => subjectType;
        public InformationVisibilityClassification Classification => classification;
        public InformationDisclosurePolicy DisclosurePolicy => disclosurePolicy;
        public InformationResharingPolicy ResharingPolicy => resharingPolicy;
        public InformationSourceVisibilityPolicy SourceVisibilityPolicy => sourceVisibilityPolicy;
        public InformationDetailVisibilityPolicy DetailVisibilityPolicy => detailVisibilityPolicy;
        public InformationAuditPolicy AuditPolicy => auditPolicy;

        public InformationAccessPolicyData CreatePolicyData(InformationSubjectReferenceData subject, string ownerId = "", string controllingEntityId = "")
        {
            InformationSubjectReferenceData clonedSubject = subject?.Clone() ?? new InformationSubjectReferenceData();
            if (clonedSubject.subjectType == InformationSubjectType.Unknown)
            {
                clonedSubject.subjectType = subjectType;
            }

            if (string.IsNullOrWhiteSpace(clonedSubject.ownerPersonId))
            {
                clonedSubject.ownerPersonId = ownerId ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(clonedSubject.controllingEntityId))
            {
                clonedSubject.controllingEntityId = controllingEntityId ?? string.Empty;
            }

            return new InformationAccessPolicyData
            {
                policyId = Id,
                subject = clonedSubject,
                classification = classification,
                disclosurePolicy = disclosurePolicy,
                resharingPolicy = resharingPolicy,
                sourceVisibilityPolicy = sourceVisibilityPolicy,
                detailVisibilityPolicy = detailVisibilityPolicy,
                auditPolicy = auditPolicy,
                defaultVisibleDetails = InformationAccessPolicyData.CloneArray(defaultVisibleDetails),
                defaultRedactedDetails = InformationAccessPolicyData.CloneArray(defaultRedactedDetails),
                defaultHiddenDetails = InformationAccessPolicyData.CloneArray(defaultHiddenDetails),
                discoveryRequired = discoveryRequired,
                redactedAccessAcceptable = redactedAccessAcceptable,
                revision = 1L
            };
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Information Access Policy '{DisplayName}' must declare a stable policy ID.");
            }
            else if (!Id.StartsWith("information-access.", StringComparison.Ordinal))
            {
                report.AddWarning($"Information Access Policy '{DisplayName}' should use the 'information-access.' namespace prefix.");
            }

            ValidateEnum(subjectType, nameof(InformationSubjectType), report);
            ValidateEnum(classification, nameof(InformationVisibilityClassification), report);
            ValidateEnum(disclosurePolicy, nameof(InformationDisclosurePolicy), report);
            ValidateEnum(resharingPolicy, nameof(InformationResharingPolicy), report);
            ValidateEnum(sourceVisibilityPolicy, nameof(InformationSourceVisibilityPolicy), report);
            ValidateEnum(detailVisibilityPolicy, nameof(InformationDetailVisibilityPolicy), report);
            ValidateEnum(auditPolicy, nameof(InformationAuditPolicy), report);

            HashSet<string> details = new HashSet<string>(StringComparer.Ordinal);
            ValidateDetails(defaultVisibleDetails, "visible", details, report);
            ValidateDetails(defaultRedactedDetails, "redacted", details, report);
            ValidateDetails(defaultHiddenDetails, "hidden", details, report);

            if (detailVisibilityPolicy == InformationDetailVisibilityPolicy.Selected && (defaultVisibleDetails == null || defaultVisibleDetails.Length == 0))
            {
                report.AddError($"Information Access Policy '{DisplayName}' uses Selected detail visibility but declares no visible detail IDs.");
            }

            if (disclosurePolicy == InformationDisclosurePolicy.FreelyDisclose && resharingPolicy == InformationResharingPolicy.NoResharing)
            {
                report.AddError($"Information Access Policy '{DisplayName}' cannot freely disclose while configured as no-resharing.");
            }
        }

        private static void ValidateEnum<T>(T value, string enumName, DefinitionValidationReport report)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                report.AddError($"Information Access Policy has invalid {enumName} value '{value}'.");
            }
        }

        private void ValidateDetails(string[] ids, string label, HashSet<string> seen, DefinitionValidationReport report)
        {
            if (ids == null)
            {
                return;
            }

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.AddError($"Information Access Policy '{DisplayName}' has an empty {label} detail ID at index {i}.");
                    continue;
                }

                if (!seen.Add(id.Trim()))
                {
                    report.AddError($"Information Access Policy '{DisplayName}' declares detail '{id}' in multiple detail sets.");
                }
            }
        }

        public void DevelopmentConfigure(
            string id,
            string display,
            InformationSubjectType type,
            InformationVisibilityClassification visibility,
            InformationDisclosurePolicy disclosure,
            InformationResharingPolicy resharing,
            InformationSourceVisibilityPolicy sourceVisibility,
            InformationDetailVisibilityPolicy detailVisibility,
            InformationAuditPolicy audit,
            string[] visibleDetails = null,
            string[] redactedDetails = null,
            string[] hiddenDetails = null,
            bool requiresDiscovery = false,
            bool allowRedactedAccess = true)
        {
            policyId = id;
            displayName = display;
            subjectType = type;
            classification = visibility;
            disclosurePolicy = disclosure;
            resharingPolicy = resharing;
            sourceVisibilityPolicy = sourceVisibility;
            detailVisibilityPolicy = detailVisibility;
            auditPolicy = audit;
            defaultVisibleDetails = visibleDetails ?? Array.Empty<string>();
            defaultRedactedDetails = redactedDetails ?? Array.Empty<string>();
            defaultHiddenDetails = hiddenDetails ?? Array.Empty<string>();
            discoveryRequired = requiresDiscovery;
            redactedAccessAcceptable = allowRedactedAccess;
        }
    }
}
