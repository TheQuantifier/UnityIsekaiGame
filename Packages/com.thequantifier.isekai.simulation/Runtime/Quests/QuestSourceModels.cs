using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Quests
{
    [Serializable]
    public sealed class QuestSourceFilterData
    {
        public QuestCategory[] allowedQuestCategories = Array.Empty<QuestCategory>();
        public string[] requiredQuestTagIds = Array.Empty<string>();
        public string[] allowedIssuerIds = Array.Empty<string>();
        public QuestDefinitionRepeatabilityPolicy[] allowedRepeatabilityPolicies = Array.Empty<QuestDefinitionRepeatabilityPolicy>();

        public QuestSourceFilterData Clone()
        {
            return new QuestSourceFilterData
            {
                allowedQuestCategories = CleanEnums(allowedQuestCategories),
                requiredQuestTagIds = QuestRuntimeModelUtility.Clean(requiredQuestTagIds),
                allowedIssuerIds = QuestRuntimeModelUtility.Clean(allowedIssuerIds),
                allowedRepeatabilityPolicies = CleanEnums(allowedRepeatabilityPolicies)
            };
        }

        private static T[] CleanEnums<T>(IEnumerable<T> values) where T : struct, Enum
        {
            return (values ?? Array.Empty<T>())
                .Where(value => Convert.ToInt32(value) != 0)
                .Distinct()
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
        }
    }

    [Serializable]
    public sealed class QuestSourceProviderRequirementData
    {
        public QuestSourceProviderRequirementKind kind = QuestSourceProviderRequirementKind.NoProvider;
        public string requirementId;
        public bool hidden;

        public QuestSourceProviderRequirementData Clone()
        {
            return new QuestSourceProviderRequirementData
            {
                kind = kind,
                requirementId = requirementId ?? string.Empty,
                hidden = hidden
            };
        }
    }

    [Serializable]
    public sealed class QuestSourcePublicationPolicyData
    {
        public int maxActiveListings = -1;
        public QuestListingDuplicatePolicy duplicatePolicy = QuestListingDuplicatePolicy.RejectActiveDuplicate;
        public QuestListingExpirationPolicy expirationPolicy = QuestListingExpirationPolicy.NeverExpires;
        public double defaultListingDuration = -1d;
        public QuestAcceptedListingDisplayPolicy acceptedListingPolicy = QuestAcceptedListingDisplayPolicy.HideWhenAccepted;
        public QuestRepeatableListingDisplayPolicy repeatableListingPolicy = QuestRepeatableListingDisplayPolicy.KeepListed;

        public QuestSourcePublicationPolicyData Clone()
        {
            return new QuestSourcePublicationPolicyData
            {
                maxActiveListings = maxActiveListings,
                duplicatePolicy = duplicatePolicy,
                expirationPolicy = expirationPolicy,
                defaultListingDuration = defaultListingDuration,
                acceptedListingPolicy = acceptedListingPolicy,
                repeatableListingPolicy = repeatableListingPolicy
            };
        }
    }

    [Serializable]
    public sealed class QuestSourceDefinitionRecordData
    {
        public string definitionId;
        public string displayName;
        public QuestSourceCategory category = QuestSourceCategory.QuestBoard;
        public QuestSourceVisibility defaultVisibility = QuestSourceVisibility.Public;
        public QuestSourceDiscoveryPolicy discoveryPolicy = QuestSourceDiscoveryPolicy.RequiresInteraction;
        public QuestListingDiscoveryPolicy listingDiscoveryPolicy = QuestListingDiscoveryPolicy.BrowseRevealsListing;
        public QuestEligibilityDisplayPolicy eligibilityDisplayPolicy = QuestEligibilityDisplayPolicy.VisibleIneligibleWithPublicReason;
        public QuestSourcePublicationPolicyData publicationPolicy = new QuestSourcePublicationPolicyData();
        public QuestSourceFilterData filters = new QuestSourceFilterData();
        public QuestSourceProviderRequirementData[] providerRequirements = Array.Empty<QuestSourceProviderRequirementData>();
        public string[] publicationAuthorityRequirementIds = Array.Empty<string>();
        public string[] sourceRoleIds = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();

        public QuestSourceDefinitionRecordData Clone()
        {
            return new QuestSourceDefinitionRecordData
            {
                definitionId = definitionId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                category = category,
                defaultVisibility = defaultVisibility,
                discoveryPolicy = discoveryPolicy,
                listingDiscoveryPolicy = listingDiscoveryPolicy,
                eligibilityDisplayPolicy = eligibilityDisplayPolicy,
                publicationPolicy = publicationPolicy?.Clone() ?? new QuestSourcePublicationPolicyData(),
                filters = filters?.Clone() ?? new QuestSourceFilterData(),
                providerRequirements = (providerRequirements ?? Array.Empty<QuestSourceProviderRequirementData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                publicationAuthorityRequirementIds = QuestRuntimeModelUtility.Clean(publicationAuthorityRequirementIds),
                sourceRoleIds = QuestRuntimeModelUtility.Clean(sourceRoleIds),
                tags = QuestRuntimeModelUtility.Clean(tags)
            };
        }
    }

    [Serializable]
    public sealed class QuestSourceRecordData
    {
        public string questSourceId;
        public string questSourceDefinitionId;
        public string worldId;
        public QuestSourceLifecycleState lifecycleState = QuestSourceLifecycleState.Active;
        public string hostLocationId;
        public string interactionPointId;
        public string operatingOrganizationId;
        public string operatingGovernmentId;
        public string operatingFactionId;
        public string operatingBusinessId;
        public string operatingOfficeId;
        public QuestSourceVisibility visibility = QuestSourceVisibility.Public;
        public double createdWorldTime;
        public double retiredWorldTime = -1d;
        public string sceneBindingKey;
        public string provenanceId;
        public long revision = 1L;

        public QuestSourceRecordData Clone()
        {
            return new QuestSourceRecordData
            {
                questSourceId = questSourceId ?? string.Empty,
                questSourceDefinitionId = questSourceDefinitionId ?? string.Empty,
                worldId = worldId ?? string.Empty,
                lifecycleState = lifecycleState,
                hostLocationId = hostLocationId ?? string.Empty,
                interactionPointId = interactionPointId ?? string.Empty,
                operatingOrganizationId = operatingOrganizationId ?? string.Empty,
                operatingGovernmentId = operatingGovernmentId ?? string.Empty,
                operatingFactionId = operatingFactionId ?? string.Empty,
                operatingBusinessId = operatingBusinessId ?? string.Empty,
                operatingOfficeId = operatingOfficeId ?? string.Empty,
                visibility = visibility,
                createdWorldTime = createdWorldTime,
                retiredWorldTime = retiredWorldTime,
                sceneBindingKey = sceneBindingKey ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class QuestListingRecordData
    {
        public string questListingId;
        public string questId;
        public string questSourceId;
        public string worldId;
        public QuestListingLifecycleState lifecycleState = QuestListingLifecycleState.Published;
        public double publishedWorldTime;
        public double expirationWorldTime = -1d;
        public double endedWorldTime = -1d;
        public int priority;
        public QuestSourceVisibility visibility = QuestSourceVisibility.Public;
        public QuestEligibilityDisplayPolicy eligibilityDisplayPolicy = QuestEligibilityDisplayPolicy.VisibleIneligibleWithPublicReason;
        public QuestAcceptedListingDisplayPolicy acceptedDisplayPolicy = QuestAcceptedListingDisplayPolicy.HideWhenAccepted;
        public QuestRepeatableListingDisplayPolicy repeatableDisplayPolicy = QuestRepeatableListingDisplayPolicy.KeepListed;
        public QuestRecipientReferenceData intendedAudience = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open };
        public string publisherPersonId;
        public string publisherAuthorityId;
        public string claimedAssignmentId;
        public string sourceEventId;
        public string provenanceId;
        public long sourceRevisionAtPublication;
        public long revision = 1L;

        public QuestListingRecordData Clone()
        {
            return new QuestListingRecordData
            {
                questListingId = questListingId ?? string.Empty,
                questId = questId ?? string.Empty,
                questSourceId = questSourceId ?? string.Empty,
                worldId = worldId ?? string.Empty,
                lifecycleState = lifecycleState,
                publishedWorldTime = publishedWorldTime,
                expirationWorldTime = expirationWorldTime,
                endedWorldTime = endedWorldTime,
                priority = priority,
                visibility = visibility,
                eligibilityDisplayPolicy = eligibilityDisplayPolicy,
                acceptedDisplayPolicy = acceptedDisplayPolicy,
                repeatableDisplayPolicy = repeatableDisplayPolicy,
                intendedAudience = intendedAudience?.Clone() ?? new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                publisherPersonId = publisherPersonId ?? string.Empty,
                publisherAuthorityId = publisherAuthorityId ?? string.Empty,
                claimedAssignmentId = claimedAssignmentId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                sourceRevisionAtPublication = sourceRevisionAtPublication,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class QuestSourceDiscoveryRecordData
    {
        public string discoveryId;
        public string personId;
        public string questSourceId;
        public string questListingId;
        public string questId;
        public QuestSourceDiscoveryKind discoveryKind = QuestSourceDiscoveryKind.SourceKnown;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public string knowledgeReferenceId;
        public double worldTime;
        public string transactionId;
        public string provenanceId;
        public long revision = 1L;

        public QuestSourceDiscoveryRecordData Clone()
        {
            return new QuestSourceDiscoveryRecordData
            {
                discoveryId = discoveryId ?? string.Empty,
                personId = personId ?? string.Empty,
                questSourceId = questSourceId ?? string.Empty,
                questListingId = questListingId ?? string.Empty,
                questId = questId ?? string.Empty,
                discoveryKind = discoveryKind,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                knowledgeReferenceId = knowledgeReferenceId ?? string.Empty,
                worldTime = worldTime,
                transactionId = transactionId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class QuestSourceAssociationRecordData
    {
        public string associationId;
        public QuestSourceRole role = QuestSourceRole.Listing;
        public string questSourceId;
        public string questListingId;
        public string questId;
        public string assignmentId;
        public string terminalOutcomeId;
        public string rewardEntitlementId;
        public string interactionPointId;
        public double worldTime;
        public string transactionId;
        public string provenanceId;
        public long revision = 1L;

        public QuestSourceAssociationRecordData Clone()
        {
            return new QuestSourceAssociationRecordData
            {
                associationId = associationId ?? string.Empty,
                role = role,
                questSourceId = questSourceId ?? string.Empty,
                questListingId = questListingId ?? string.Empty,
                questId = questId ?? string.Empty,
                assignmentId = assignmentId ?? string.Empty,
                terminalOutcomeId = terminalOutcomeId ?? string.Empty,
                rewardEntitlementId = rewardEntitlementId ?? string.Empty,
                interactionPointId = interactionPointId ?? string.Empty,
                worldTime = worldTime,
                transactionId = transactionId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class QuestSourceEventData
    {
        public string eventId;
        public string transactionId;
        public QuestSourceEventKind eventKind;
        public string questSourceId;
        public string questListingId;
        public string questId;
        public QuestSourceLifecycleState sourceBeforeState;
        public QuestSourceLifecycleState sourceAfterState;
        public QuestListingLifecycleState listingBeforeState;
        public QuestListingLifecycleState listingAfterState;
        public double worldTime;
        public string provenanceId;
        public long runtimeRevision;

        public QuestSourceEventData Clone()
        {
            return new QuestSourceEventData
            {
                eventId = eventId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                eventKind = eventKind,
                questSourceId = questSourceId ?? string.Empty,
                questListingId = questListingId ?? string.Empty,
                questId = questId ?? string.Empty,
                sourceBeforeState = sourceBeforeState,
                sourceAfterState = sourceAfterState,
                listingBeforeState = listingBeforeState,
                listingAfterState = listingAfterState,
                worldTime = worldTime,
                provenanceId = provenanceId ?? string.Empty,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestSourceTransactionData
    {
        public string transactionId;
        public string operation;
        public string questSourceId;
        public string questListingId;
        public string questId;
        public long runtimeRevision;

        public QuestSourceTransactionData Clone()
        {
            return new QuestSourceTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                questSourceId = questSourceId ?? string.Empty,
                questListingId = questListingId ?? string.Empty,
                questId = questId ?? string.Empty,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestSourceRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<QuestSourceRecordData> sources = new List<QuestSourceRecordData>();
        public List<QuestListingRecordData> listings = new List<QuestListingRecordData>();
        public List<QuestSourceDiscoveryRecordData> discoveries = new List<QuestSourceDiscoveryRecordData>();
        public List<QuestSourceAssociationRecordData> associations = new List<QuestSourceAssociationRecordData>();
        public List<QuestSourceEventData> events = new List<QuestSourceEventData>();
        public List<QuestSourceTransactionData> transactions = new List<QuestSourceTransactionData>();

        public QuestSourceRuntimeSaveData Clone()
        {
            return new QuestSourceRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                sources = (sources ?? new List<QuestSourceRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                listings = (listings ?? new List<QuestListingRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                discoveries = (discoveries ?? new List<QuestSourceDiscoveryRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                associations = (associations ?? new List<QuestSourceAssociationRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                events = (events ?? new List<QuestSourceEventData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                transactions = (transactions ?? new List<QuestSourceTransactionData>()).Where(value => value != null).Select(value => value.Clone()).ToList()
            };
        }
    }

    public sealed class QuestSourceCreateRequest
    {
        public string transactionId;
        public string questSourceId;
        public string questSourceDefinitionId;
        public QuestSourceLifecycleState initialLifecycleState = QuestSourceLifecycleState.Active;
        public string hostLocationId;
        public string interactionPointId;
        public string operatingOrganizationId;
        public string operatingGovernmentId;
        public string operatingFactionId;
        public string operatingBusinessId;
        public string operatingOfficeId;
        public QuestSourceVisibility? visibility;
        public double worldTime;
        public string sceneBindingKey;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestSourceLifecycleRequest
    {
        public string transactionId;
        public string questSourceId;
        public QuestSourceLifecycleState targetState;
        public double worldTime;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestListingPublishRequest
    {
        public string transactionId;
        public string questListingId;
        public string questSourceId;
        public string questId;
        public double worldTime;
        public double expirationWorldTime = -1d;
        public int priority;
        public QuestSourceVisibility? visibility;
        public QuestRecipientReferenceData intendedAudience;
        public string publisherPersonId;
        public string publisherAuthorityId;
        public IEnumerable<string> publisherAuthorityIds;
        public string sourceEventId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestListingLifecycleRequest
    {
        public string transactionId;
        public string questListingId;
        public string actorPersonId;
        public string authorityId;
        public QuestListingLifecycleState targetState;
        public double worldTime;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestSourceBrowseRequest
    {
        public string questSourceId;
        public string requesterPersonId;
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public QuestEligibilityContext eligibilityContext;
        public double worldTime;
        public string categoryFilterTag;
        public int offset;
        public int limit = 50;
        public long expectedSourceRevision = -1L;
        public bool includeHistorical;
        public bool includeIneligible = true;
        public bool recordDiscovery;
        public string transactionId;
    }

    public sealed class QuestListingInspectRequest
    {
        public string questListingId;
        public string questSourceId;
        public string requesterPersonId;
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public QuestEligibilityContext eligibilityContext;
        public double worldTime;
        public bool includeRewardPreview;
        public bool recordDiscovery;
        public string transactionId;
    }

    public sealed class QuestSourceAcceptRequest
    {
        public string transactionId;
        public string questListingId;
        public string personId;
        public bool explicitConsent = true;
        public string consentRecordId;
        public string authorityBasisId;
        public QuestEligibilityContext eligibilityContext;
        public double worldTime;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestSourceDiscoveryRequest
    {
        public string transactionId;
        public string personId;
        public string questSourceId;
        public string questListingId;
        public string questId;
        public QuestSourceDiscoveryKind discoveryKind;
        public InformationSubjectReferenceData subject;
        public string knowledgeReferenceId;
        public double worldTime;
        public string provenanceId;
        public bool preview;
    }

    public sealed class QuestSourceAssociationRequest
    {
        public string transactionId;
        public QuestSourceRole role;
        public string questSourceId;
        public string questListingId;
        public string questId;
        public string assignmentId;
        public string terminalOutcomeId;
        public string rewardEntitlementId;
        public string interactionPointId;
        public double worldTime;
        public string provenanceId;
        public bool preview;
    }

    public sealed class QuestSourceQuery
    {
        public string questSourceId;
        public string definitionId;
        public QuestSourceCategory? category;
        public string locationId;
        public string interactionPointId;
        public string organizationId;
        public string governmentId;
        public string factionId;
        public string officeId;
        public string worldId;
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public string requesterPersonId;
        public bool includeHistorical;
    }

    public sealed class QuestListingQuery
    {
        public string questListingId;
        public string questSourceId;
        public string questId;
        public string worldId;
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public string requesterPersonId;
        public bool includeHistorical;
        public bool includeHidden;
    }

    public sealed class QuestSourceSnapshot
    {
        private readonly QuestSourceRecordData data;

        public QuestSourceSnapshot(QuestSourceRecordData record, bool redacted = false)
        {
            data = record?.Clone() ?? new QuestSourceRecordData();
            Redacted = redacted;
        }

        public string QuestSourceId => data.questSourceId ?? string.Empty;
        public string QuestSourceDefinitionId => data.questSourceDefinitionId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public QuestSourceLifecycleState LifecycleState => data.lifecycleState;
        public string HostLocationId => Redacted ? string.Empty : data.hostLocationId ?? string.Empty;
        public string InteractionPointId => Redacted ? string.Empty : data.interactionPointId ?? string.Empty;
        public string OperatingOrganizationId => Redacted ? string.Empty : data.operatingOrganizationId ?? string.Empty;
        public string OperatingGovernmentId => Redacted ? string.Empty : data.operatingGovernmentId ?? string.Empty;
        public string OperatingFactionId => Redacted ? string.Empty : data.operatingFactionId ?? string.Empty;
        public string OperatingBusinessId => Redacted ? string.Empty : data.operatingBusinessId ?? string.Empty;
        public string OperatingOfficeId => Redacted ? string.Empty : data.operatingOfficeId ?? string.Empty;
        public QuestSourceVisibility Visibility => data.visibility;
        public double CreatedWorldTime => Redacted ? -1d : data.createdWorldTime;
        public double RetiredWorldTime => Redacted ? -1d : data.retiredWorldTime;
        public string SceneBindingKey => Redacted ? string.Empty : data.sceneBindingKey ?? string.Empty;
        public string ProvenanceId => Redacted ? string.Empty : data.provenanceId ?? string.Empty;
        public long Revision => data.revision;
        public bool Redacted { get; }
        public QuestSourceRecordData ToSaveData() => data.Clone();
    }

    public sealed class QuestListingSnapshot
    {
        private readonly QuestListingRecordData data;

        public QuestListingSnapshot(QuestListingRecordData record, bool redacted = false)
        {
            data = record?.Clone() ?? new QuestListingRecordData();
            Redacted = redacted;
        }

        public string QuestListingId => data.questListingId ?? string.Empty;
        public string QuestId => data.questId ?? string.Empty;
        public string QuestSourceId => data.questSourceId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public QuestListingLifecycleState LifecycleState => data.lifecycleState;
        public double PublishedWorldTime => Redacted ? -1d : data.publishedWorldTime;
        public double ExpirationWorldTime => Redacted ? -1d : data.expirationWorldTime;
        public double EndedWorldTime => Redacted ? -1d : data.endedWorldTime;
        public int Priority => data.priority;
        public QuestSourceVisibility Visibility => data.visibility;
        public QuestEligibilityDisplayPolicy EligibilityDisplayPolicy => data.eligibilityDisplayPolicy;
        public QuestAcceptedListingDisplayPolicy AcceptedDisplayPolicy => data.acceptedDisplayPolicy;
        public QuestRepeatableListingDisplayPolicy RepeatableDisplayPolicy => data.repeatableDisplayPolicy;
        public QuestRecipientReferenceData IntendedAudience => Redacted ? new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open } : data.intendedAudience?.Clone() ?? new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open };
        public string PublisherPersonId => Redacted ? string.Empty : data.publisherPersonId ?? string.Empty;
        public string PublisherAuthorityId => Redacted ? string.Empty : data.publisherAuthorityId ?? string.Empty;
        public string ClaimedAssignmentId => Redacted ? string.Empty : data.claimedAssignmentId ?? string.Empty;
        public string SourceEventId => Redacted ? string.Empty : data.sourceEventId ?? string.Empty;
        public long SourceRevisionAtPublication => data.sourceRevisionAtPublication;
        public long Revision => data.revision;
        public bool Redacted { get; }
        public QuestListingRecordData ToSaveData() => data.Clone();
    }

    public sealed class QuestVisibleListingSnapshot
    {
        public QuestVisibleListingSnapshot(QuestListingSnapshot listing, QuestSnapshot quest, QuestAvailabilityResult availability, QuestEligibilityResult eligibility, bool ineligibleReasonRedacted, bool taken)
        {
            Listing = listing;
            Quest = quest;
            Availability = availability;
            Eligibility = eligibility;
            IneligibleReasonRedacted = ineligibleReasonRedacted;
            Taken = taken;
        }

        public QuestListingSnapshot Listing { get; }
        public QuestSnapshot Quest { get; }
        public QuestAvailabilityResult Availability { get; }
        public QuestEligibilityResult Eligibility { get; }
        public bool Eligible => Eligibility?.Eligible ?? false;
        public bool IneligibleReasonRedacted { get; }
        public bool Taken { get; }
    }

    public sealed class QuestSourceBrowseResult
    {
        public QuestSourceBrowseResult(QuestSourceOperationStatus status, string message, QuestSourceSnapshot source, IEnumerable<QuestVisibleListingSnapshot> listings, int nextOffset, long revision)
        {
            Status = status;
            Message = message ?? string.Empty;
            Source = source;
            Listings = (listings ?? Array.Empty<QuestVisibleListingSnapshot>()).Where(value => value != null).ToArray();
            NextOffset = nextOffset;
            Revision = revision;
        }

        public QuestSourceOperationStatus Status { get; }
        public string Message { get; }
        public QuestSourceSnapshot Source { get; }
        public IReadOnlyList<QuestVisibleListingSnapshot> Listings { get; }
        public int VisibleCount => Listings.Count;
        public int NextOffset { get; }
        public long Revision { get; }
        public bool Succeeded => Status == QuestSourceOperationStatus.Succeeded || Status == QuestSourceOperationStatus.Preview || Status == QuestSourceOperationStatus.Duplicate;
    }

    public sealed class QuestListingInspectionResult
    {
        public QuestListingInspectionResult(QuestSourceOperationStatus status, string message, QuestSourceSnapshot source, QuestVisibleListingSnapshot listing, IReadOnlyList<QuestRewardEntitlementSnapshot> rewards, IReadOnlyList<QuestSourceDiscoveryRecordData> discoveries, long revision)
        {
            Status = status;
            Message = message ?? string.Empty;
            Source = source;
            Listing = listing;
            Rewards = rewards ?? Array.Empty<QuestRewardEntitlementSnapshot>();
            Discoveries = discoveries ?? Array.Empty<QuestSourceDiscoveryRecordData>();
            Revision = revision;
        }

        public QuestSourceOperationStatus Status { get; }
        public string Message { get; }
        public QuestSourceSnapshot Source { get; }
        public QuestVisibleListingSnapshot Listing { get; }
        public IReadOnlyList<QuestRewardEntitlementSnapshot> Rewards { get; }
        public IReadOnlyList<QuestSourceDiscoveryRecordData> Discoveries { get; }
        public long Revision { get; }
        public bool Succeeded => Status == QuestSourceOperationStatus.Succeeded || Status == QuestSourceOperationStatus.Preview || Status == QuestSourceOperationStatus.Duplicate;
    }

    public sealed class QuestSourceOperationResult
    {
        private QuestSourceOperationResult(QuestSourceOperationStatus status, string message, QuestSourceSnapshot source, QuestListingSnapshot listing, QuestSourceDiscoveryRecordData discovery, QuestSourceAssociationRecordData association, QuestAssignmentSnapshot assignment, bool preview, bool duplicate, long before, long after)
        {
            Status = status;
            Message = message ?? string.Empty;
            Source = source;
            Listing = listing;
            Discovery = discovery?.Clone();
            Association = association?.Clone();
            Assignment = assignment;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public QuestSourceOperationStatus Status { get; }
        public string Message { get; }
        public QuestSourceSnapshot Source { get; }
        public QuestListingSnapshot Listing { get; }
        public QuestSourceDiscoveryRecordData Discovery { get; }
        public QuestSourceAssociationRecordData Association { get; }
        public QuestAssignmentSnapshot Assignment { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Succeeded => Status == QuestSourceOperationStatus.Succeeded || Status == QuestSourceOperationStatus.Preview || Status == QuestSourceOperationStatus.Duplicate;

        public static QuestSourceOperationResult Success(string message, long before, long after, QuestSourceRecordData source = null, QuestListingRecordData listing = null, QuestSourceDiscoveryRecordData discovery = null, QuestSourceAssociationRecordData association = null, QuestAssignmentSnapshot assignment = null, bool preview = false, bool duplicate = false)
        {
            return new QuestSourceOperationResult(preview ? QuestSourceOperationStatus.Preview : duplicate ? QuestSourceOperationStatus.Duplicate : QuestSourceOperationStatus.Succeeded, message, source == null ? null : new QuestSourceSnapshot(source), listing == null ? null : new QuestListingSnapshot(listing), discovery, association, assignment, preview, duplicate, before, after);
        }

        public static QuestSourceOperationResult Failure(QuestSourceOperationStatus status, string message, long revision)
        {
            return new QuestSourceOperationResult(status, message, null, null, null, null, null, false, false, revision, revision);
        }
    }

    public sealed class QuestSourceRuntimeValidationReport
    {
        public QuestSourceRuntimeValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
        public string Summary => $"Quest source validation finished with {Errors.Count} error(s), {Warnings.Count} warning(s).";
    }
}
