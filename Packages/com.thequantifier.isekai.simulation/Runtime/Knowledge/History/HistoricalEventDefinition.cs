using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge.History
{
    [CreateAssetMenu(fileName = "HistoricalEventDefinition", menuName = "Unity Isekai Game/Knowledge/Historical Event Definition")]
    public sealed class HistoricalEventDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string eventDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private HistoricalEventCategory category = HistoricalEventCategory.CustomWorldEvent;
        [SerializeField] private KnowledgeVisibility defaultVisibility = KnowledgeVisibility.Public;
        [SerializeField] private HistoricalEventPayloadKind payloadKind = HistoricalEventPayloadKind.Generic;
        [Header("Life Event Projection")]
        [SerializeField] private bool lifeEventDefinition;
        [SerializeField] private LifeEventCategory lifeEventCategory = LifeEventCategory.None;
        [SerializeField] private LifeEventPayloadKind lifeEventPayloadKind = LifeEventPayloadKind.Generic;
        [SerializeField] private LifeEventParticipantRole[] requiredParticipantRoles;
        [SerializeField] private LifeEventParticipantRole[] optionalParticipantRoles;
        [SerializeField] private LifeEventSignificance defaultSignificance = LifeEventSignificance.Notable;
        [SerializeField] private LifeEventBiographyRelevance defaultBiographyRelevance = LifeEventBiographyRelevance.Optional;
        [SerializeField] private LifeEventPublicRecordRelevance defaultPublicRecordRelevance = LifeEventPublicRecordRelevance.PersonalOnly;
        [SerializeField] private bool mayBePrivate = true;
        [SerializeField] private bool mayBeSecret = true;
        [SerializeField] private bool mayBeCorrected = true;
        [SerializeField] private bool expectsCurrentStateTransition;
        [SerializeField] private string[] tags;
        [SerializeField] private string validationMetadata;

        public string Id => eventDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public HistoricalEventCategory Category => category;
        public KnowledgeVisibility DefaultVisibility => defaultVisibility;
        public HistoricalEventPayloadKind PayloadKind => payloadKind;
        public bool IsLifeEventDefinition => lifeEventDefinition;
        public LifeEventCategory LifeEventCategory => lifeEventCategory;
        public LifeEventPayloadKind LifeEventPayloadKind => lifeEventPayloadKind;
        public IReadOnlyList<LifeEventParticipantRole> RequiredParticipantRoles => requiredParticipantRoles ?? Array.Empty<LifeEventParticipantRole>();
        public IReadOnlyList<LifeEventParticipantRole> OptionalParticipantRoles => optionalParticipantRoles ?? Array.Empty<LifeEventParticipantRole>();
        public LifeEventSignificance DefaultSignificance => defaultSignificance;
        public LifeEventBiographyRelevance DefaultBiographyRelevance => defaultBiographyRelevance;
        public LifeEventPublicRecordRelevance DefaultPublicRecordRelevance => defaultPublicRecordRelevance;
        public bool MayBePrivate => mayBePrivate;
        public bool MayBeSecret => mayBeSecret;
        public bool MayBeCorrected => mayBeCorrected;
        public bool ExpectsCurrentStateTransition => expectsCurrentStateTransition;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            eventDefinitionId = eventDefinitionId?.Trim();
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            HistoricalEventCategory historicalCategory,
            KnowledgeVisibility visibility,
            HistoricalEventPayloadKind historicalPayloadKind,
            bool isLifeEvent,
            LifeEventCategory lifeCategory = LifeEventCategory.None,
            LifeEventPayloadKind lifePayloadKind = LifeEventPayloadKind.Generic,
            LifeEventSignificance significance = LifeEventSignificance.Notable,
            LifeEventBiographyRelevance biography = LifeEventBiographyRelevance.Optional,
            LifeEventPublicRecordRelevance publicRecord = LifeEventPublicRecordRelevance.PersonalOnly,
            LifeEventParticipantRole[] requiredRoles = null,
            LifeEventParticipantRole[] optionalRoles = null,
            string[] definitionTags = null)
        {
            eventDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            category = historicalCategory;
            defaultVisibility = visibility;
            payloadKind = historicalPayloadKind;
            lifeEventDefinition = isLifeEvent;
            lifeEventCategory = lifeCategory;
            lifeEventPayloadKind = lifePayloadKind;
            defaultSignificance = significance;
            defaultBiographyRelevance = biography;
            defaultPublicRecordRelevance = publicRecord;
            requiredParticipantRoles = requiredRoles ?? Array.Empty<LifeEventParticipantRole>();
            optionalParticipantRoles = optionalRoles ?? Array.Empty<LifeEventParticipantRole>();
            tags = definitionTags ?? Array.Empty<string>();
            mayBePrivate = true;
            mayBeSecret = true;
            mayBeCorrected = true;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Historical Event '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("history-event.", StringComparison.Ordinal))
            {
                report.AddWarning($"Historical Event '{Id}' should use the 'history-event.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(HistoricalEventCategory), category))
            {
                report.AddError($"Historical Event '{DisplayName}' has an invalid category.");
            }

            if (!Enum.IsDefined(typeof(KnowledgeVisibility), defaultVisibility))
            {
                report.AddError($"Historical Event '{DisplayName}' has an invalid default visibility.");
            }

            if (!Enum.IsDefined(typeof(HistoricalEventPayloadKind), payloadKind))
            {
                report.AddError($"Historical Event '{DisplayName}' has an invalid payload kind.");
            }

            if (!lifeEventDefinition)
            {
                return;
            }

            if (lifeEventCategory == LifeEventCategory.None || !Enum.IsDefined(typeof(LifeEventCategory), lifeEventCategory))
            {
                report.AddError($"Historical Event '{DisplayName}' is a life-event definition but has no valid life-event category.");
            }

            if (!Enum.IsDefined(typeof(LifeEventPayloadKind), lifeEventPayloadKind))
            {
                report.AddError($"Historical Event '{DisplayName}' has an invalid life-event payload kind.");
            }

            if (!Enum.IsDefined(typeof(LifeEventSignificance), defaultSignificance))
            {
                report.AddError($"Historical Event '{DisplayName}' has an invalid default life-event significance.");
            }

            if (!Enum.IsDefined(typeof(LifeEventBiographyRelevance), defaultBiographyRelevance))
            {
                report.AddError($"Historical Event '{DisplayName}' has an invalid default biography relevance.");
            }

            if (!Enum.IsDefined(typeof(LifeEventPublicRecordRelevance), defaultPublicRecordRelevance))
            {
                report.AddError($"Historical Event '{DisplayName}' has an invalid default public-record relevance.");
            }

            ValidateRoles(requiredParticipantRoles, "required", report);
            ValidateRoles(optionalParticipantRoles, "optional", report);

            if (!mayBePrivate && (defaultVisibility == KnowledgeVisibility.Private || defaultVisibility == KnowledgeVisibility.Confidential || defaultVisibility == KnowledgeVisibility.DiagnosticOnly))
            {
                report.AddError($"Historical Event '{DisplayName}' has private default visibility but does not allow private life events.");
            }

            if (!mayBeSecret && (defaultVisibility == KnowledgeVisibility.Hidden || defaultVisibility == KnowledgeVisibility.Secret || defaultVisibility == KnowledgeVisibility.DevelopmentOnly))
            {
                report.AddError($"Historical Event '{DisplayName}' has hidden/secret default visibility but does not allow secret life events.");
            }
        }

        private void ValidateRoles(IReadOnlyList<LifeEventParticipantRole> roles, string label, DefinitionValidationReport report)
        {
            HashSet<LifeEventParticipantRole> seen = new HashSet<LifeEventParticipantRole>();
            foreach (LifeEventParticipantRole role in roles ?? Array.Empty<LifeEventParticipantRole>())
            {
                if (role == LifeEventParticipantRole.Unknown || !Enum.IsDefined(typeof(LifeEventParticipantRole), role))
                {
                    report.AddError($"Historical Event '{DisplayName}' has an invalid {label} life-event participant role.");
                    continue;
                }

                if (!seen.Add(role))
                {
                    report.AddError($"Historical Event '{DisplayName}' has duplicate {label} life-event participant role '{role}'.");
                }
            }
        }
    }
}
