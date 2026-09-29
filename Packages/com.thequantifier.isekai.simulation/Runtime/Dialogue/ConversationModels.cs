using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Dialogue
{
    [Serializable]
    public sealed class ConversationProviderRequirementData
    {
        public ConversationProviderRequirementKind kind = ConversationProviderRequirementKind.None;
        public string requirementId;
        public bool hidden;

        public ConversationProviderRequirementData Clone()
        {
            return new ConversationProviderRequirementData
            {
                kind = kind,
                requirementId = requirementId ?? string.Empty,
                hidden = hidden
            };
        }
    }

    [Serializable]
    public sealed class ConversationDefinitionRecordData
    {
        public string definitionId;
        public string displayName;
        public ConversationCategory category = ConversationCategory.General;
        public ConversationVisibility defaultVisibility = ConversationVisibility.Public;
        public ConversationCoLocationPolicy coLocationPolicy = ConversationCoLocationPolicy.SameInteractionPoint;
        public ConversationOverlapPolicy overlapPolicy = ConversationOverlapPolicy.PreventParticipantOverlap;
        public ConversationProviderRequirementData[] providerRequirements = Array.Empty<ConversationProviderRequirementData>();
        public ConversationParticipantRole[] requiredRoles = Array.Empty<ConversationParticipantRole>();
        public string[] supportedQuestSourceDefinitionIds = Array.Empty<string>();
        public string[] supportedQuestTagIds = Array.Empty<string>();
        public string[] authorityRequirementIds = Array.Empty<string>();
        public string[] tagIds = Array.Empty<string>();

        public ConversationDefinitionRecordData Clone()
        {
            return new ConversationDefinitionRecordData
            {
                definitionId = definitionId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                category = category,
                defaultVisibility = defaultVisibility,
                coLocationPolicy = coLocationPolicy,
                overlapPolicy = overlapPolicy,
                providerRequirements = (providerRequirements ?? Array.Empty<ConversationProviderRequirementData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                requiredRoles = CleanEnums(requiredRoles),
                supportedQuestSourceDefinitionIds = Clean(supportedQuestSourceDefinitionIds),
                supportedQuestTagIds = Clean(supportedQuestTagIds),
                authorityRequirementIds = Clean(authorityRequirementIds),
                tagIds = Clean(tagIds)
            };
        }

        private static T[] CleanEnums<T>(IEnumerable<T> values) where T : struct, Enum
        {
            return (values ?? Array.Empty<T>()).Where(value => Convert.ToInt32(value) != 0).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
        }

        private static string[] Clean(IEnumerable<string> values) => QuestRuntimeModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class ConversationParticipantRecordData
    {
        public string participantId;
        public string personId;
        public ConversationParticipantRole role = ConversationParticipantRole.Listener;
        public string representedOrganizationId;
        public string representedOfficeId;
        public string representedGovernmentId;
        public string representedFactionId;
        public string representedBusinessId;
        public string currentLocationId;
        public string currentInteractionPointId;
        public bool required = true;
        public bool hidden;
        public string displayName;
        public string provenanceId;

        public ConversationParticipantRecordData Clone()
        {
            return new ConversationParticipantRecordData
            {
                participantId = participantId ?? string.Empty,
                personId = personId ?? string.Empty,
                role = role,
                representedOrganizationId = representedOrganizationId ?? string.Empty,
                representedOfficeId = representedOfficeId ?? string.Empty,
                representedGovernmentId = representedGovernmentId ?? string.Empty,
                representedFactionId = representedFactionId ?? string.Empty,
                representedBusinessId = representedBusinessId ?? string.Empty,
                currentLocationId = currentLocationId ?? string.Empty,
                currentInteractionPointId = currentInteractionPointId ?? string.Empty,
                required = required,
                hidden = hidden,
                displayName = displayName ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty
            };
        }

        public string StableKey => $"{role}:{personId ?? string.Empty}:{representedOrganizationId ?? string.Empty}:{representedOfficeId ?? string.Empty}";
    }

    [Serializable]
    public sealed class ConversationSubjectLinkData
    {
        public string linkId;
        public ConversationSubjectRole role = ConversationSubjectRole.Information;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public string questId;
        public string questSourceId;
        public string questListingId;
        public string locationId;
        public string interactionPointId;
        public string provenanceId;
        public bool hidden;

        public ConversationSubjectLinkData Clone()
        {
            return new ConversationSubjectLinkData
            {
                linkId = linkId ?? string.Empty,
                role = role,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                questId = questId ?? string.Empty,
                questSourceId = questSourceId ?? string.Empty,
                questListingId = questListingId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                interactionPointId = interactionPointId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                hidden = hidden
            };
        }

        public string StableKey => $"{role}:{subject?.subjectType}:{subject?.subjectId}:{questId}:{questSourceId}:{questListingId}:{locationId}:{interactionPointId}";
    }

    [Serializable]
    public sealed class ConversationRecordData
    {
        public string conversationId;
        public string conversationDefinitionId;
        public string worldId;
        public ConversationLifecycleState lifecycleState = ConversationLifecycleState.Active;
        public ConversationVisibility visibility = ConversationVisibility.Public;
        public ConversationParticipantRecordData[] participants = Array.Empty<ConversationParticipantRecordData>();
        public ConversationSubjectLinkData[] subjectLinks = Array.Empty<ConversationSubjectLinkData>();
        public string activeSpeakerPersonId;
        public string hostLocationId;
        public string hostInteractionPointId;
        public string questSourceId;
        public string questListingId;
        public string questId;
        public string operatingOrganizationId;
        public string operatingOfficeId;
        public string operatingGovernmentId;
        public string operatingFactionId;
        public string operatingBusinessId;
        public string sceneBindingKey;
        public string[] tagIds = Array.Empty<string>();
        public double startedWorldTime;
        public double endedWorldTime = -1d;
        public string provenanceId;
        public long revision = 1L;

        public ConversationRecordData Clone()
        {
            return new ConversationRecordData
            {
                conversationId = conversationId ?? string.Empty,
                conversationDefinitionId = conversationDefinitionId ?? string.Empty,
                worldId = worldId ?? string.Empty,
                lifecycleState = lifecycleState,
                visibility = visibility,
                participants = (participants ?? Array.Empty<ConversationParticipantRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.StableKey, StringComparer.Ordinal).ToArray(),
                subjectLinks = (subjectLinks ?? Array.Empty<ConversationSubjectLinkData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.StableKey, StringComparer.Ordinal).ToArray(),
                activeSpeakerPersonId = activeSpeakerPersonId ?? string.Empty,
                hostLocationId = hostLocationId ?? string.Empty,
                hostInteractionPointId = hostInteractionPointId ?? string.Empty,
                questSourceId = questSourceId ?? string.Empty,
                questListingId = questListingId ?? string.Empty,
                questId = questId ?? string.Empty,
                operatingOrganizationId = operatingOrganizationId ?? string.Empty,
                operatingOfficeId = operatingOfficeId ?? string.Empty,
                operatingGovernmentId = operatingGovernmentId ?? string.Empty,
                operatingFactionId = operatingFactionId ?? string.Empty,
                operatingBusinessId = operatingBusinessId ?? string.Empty,
                sceneBindingKey = sceneBindingKey ?? string.Empty,
                tagIds = QuestRuntimeModelUtility.Clean(tagIds),
                startedWorldTime = startedWorldTime,
                endedWorldTime = endedWorldTime,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ConversationEventData
    {
        public string eventId;
        public string transactionId;
        public ConversationEventKind eventKind = ConversationEventKind.ConversationStarted;
        public string conversationId;
        public string personId;
        public ConversationLifecycleState beforeState = ConversationLifecycleState.Unknown;
        public ConversationLifecycleState afterState = ConversationLifecycleState.Unknown;
        public double worldTime;
        public long runtimeRevision;
        public string provenanceId;

        public ConversationEventData Clone()
        {
            return new ConversationEventData
            {
                eventId = eventId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                eventKind = eventKind,
                conversationId = conversationId ?? string.Empty,
                personId = personId ?? string.Empty,
                beforeState = beforeState,
                afterState = afterState,
                worldTime = worldTime,
                runtimeRevision = runtimeRevision,
                provenanceId = provenanceId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ConversationTransactionData
    {
        public string transactionId;
        public string operation;
        public string conversationId;
        public ConversationOperationStatus status = ConversationOperationStatus.Succeeded;
        public long runtimeRevision;

        public ConversationTransactionData Clone()
        {
            return new ConversationTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                conversationId = conversationId ?? string.Empty,
                status = status,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class ConversationRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<ConversationRecordData> conversations = new List<ConversationRecordData>();
        public List<ConversationEventData> events = new List<ConversationEventData>();
        public List<ConversationTransactionData> transactions = new List<ConversationTransactionData>();

        public ConversationRuntimeSaveData Clone()
        {
            return new ConversationRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                conversations = (conversations ?? new List<ConversationRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.conversationId, StringComparer.Ordinal).ToList(),
                events = (events ?? new List<ConversationEventData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.eventId, StringComparer.Ordinal).ToList(),
                transactions = (transactions ?? new List<ConversationTransactionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.transactionId, StringComparer.Ordinal).ToList()
            };
        }
    }

    public sealed class ConversationStartRequest
    {
        public string transactionId;
        public string conversationId;
        public string conversationDefinitionId;
        public ConversationLifecycleState initialLifecycleState = ConversationLifecycleState.Active;
        public ConversationVisibility? visibility;
        public IEnumerable<ConversationParticipantRecordData> participants;
        public IEnumerable<ConversationSubjectLinkData> subjectLinks;
        public string activeSpeakerPersonId;
        public string hostLocationId;
        public string hostInteractionPointId;
        public string questSourceId;
        public string questListingId;
        public string questId;
        public string operatingOrganizationId;
        public string operatingOfficeId;
        public string operatingGovernmentId;
        public string operatingFactionId;
        public string operatingBusinessId;
        public string sceneBindingKey;
        public IEnumerable<string> tagIds;
        public double worldTime;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class ConversationLifecycleRequest
    {
        public string transactionId;
        public string conversationId;
        public ConversationLifecycleState targetState;
        public double worldTime;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class ConversationQuery
    {
        public ConversationAccessLevel access = ConversationAccessLevel.Public;
        public string requesterPersonId;
        public string conversationId;
        public string definitionId;
        public string personId;
        public string locationId;
        public string interactionPointId;
        public string questId;
        public string questSourceId;
        public string questListingId;
        public string organizationId;
        public string officeId;
        public string tagId;
        public bool includeInactive;
        public string worldId;
    }

    public sealed class ConversationSnapshot
    {
        private readonly ConversationRecordData data;

        public ConversationSnapshot(ConversationRecordData record)
        {
            data = record?.Clone() ?? new ConversationRecordData();
        }

        public string ConversationId => data.conversationId ?? string.Empty;
        public string ConversationDefinitionId => data.conversationDefinitionId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public ConversationLifecycleState LifecycleState => data.lifecycleState;
        public ConversationVisibility Visibility => data.visibility;
        public IReadOnlyList<ConversationParticipantRecordData> Participants => (data.participants ?? Array.Empty<ConversationParticipantRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<ConversationSubjectLinkData> SubjectLinks => (data.subjectLinks ?? Array.Empty<ConversationSubjectLinkData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        public string ActiveSpeakerPersonId => data.activeSpeakerPersonId ?? string.Empty;
        public string HostLocationId => data.hostLocationId ?? string.Empty;
        public string HostInteractionPointId => data.hostInteractionPointId ?? string.Empty;
        public string QuestSourceId => data.questSourceId ?? string.Empty;
        public string QuestListingId => data.questListingId ?? string.Empty;
        public string QuestId => data.questId ?? string.Empty;
        public string OperatingOrganizationId => data.operatingOrganizationId ?? string.Empty;
        public string OperatingOfficeId => data.operatingOfficeId ?? string.Empty;
        public string OperatingGovernmentId => data.operatingGovernmentId ?? string.Empty;
        public string OperatingFactionId => data.operatingFactionId ?? string.Empty;
        public string OperatingBusinessId => data.operatingBusinessId ?? string.Empty;
        public string SceneBindingKey => data.sceneBindingKey ?? string.Empty;
        public IReadOnlyList<string> TagIds => QuestRuntimeModelUtility.Clean(data.tagIds);
        public double StartedWorldTime => data.startedWorldTime;
        public double EndedWorldTime => data.endedWorldTime;
        public string ProvenanceId => data.provenanceId ?? string.Empty;
        public long Revision => data.revision;
        public ConversationRecordData ToSaveData() => data.Clone();

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return ConversationInformationSubject.Conversation(ConversationId, ConversationDefinitionId, Participants.FirstOrDefault(value => value.role == ConversationParticipantRole.Initiator)?.personId ?? string.Empty, OperatingOrganizationId, TagIds);
        }
    }

    public sealed class ConversationProjection
    {
        public ConversationProjection(ConversationSnapshot snapshot, bool redacted, bool concealed)
        {
            Snapshot = snapshot;
            Redacted = redacted;
            Concealed = concealed;
        }

        public ConversationSnapshot Snapshot { get; }
        public bool Redacted { get; }
        public bool Concealed { get; }
    }

    public sealed class ConversationOperationResult
    {
        private ConversationOperationResult(ConversationOperationStatus status, string message, ConversationSnapshot snapshot, bool preview, bool duplicate, long before, long after)
        {
            Status = status;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public ConversationOperationStatus Status { get; }
        public string Message { get; }
        public ConversationSnapshot Snapshot { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Succeeded => Status == ConversationOperationStatus.Succeeded || Status == ConversationOperationStatus.Preview || Status == ConversationOperationStatus.Duplicate;

        public static ConversationOperationResult Success(string message, long before, long after, ConversationRecordData record = null, bool preview = false, bool duplicate = false)
        {
            return new ConversationOperationResult(preview ? ConversationOperationStatus.Preview : duplicate ? ConversationOperationStatus.Duplicate : ConversationOperationStatus.Succeeded, message, record == null ? null : new ConversationSnapshot(record), preview, duplicate, before, after);
        }

        public static ConversationOperationResult Failure(ConversationOperationStatus status, string message, long revision)
        {
            return new ConversationOperationResult(status, message, null, false, false, revision, revision);
        }
    }

    public sealed class ConversationRuntimeValidationReport
    {
        public ConversationRuntimeValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
        public string Summary => $"Conversation validation finished with {Errors.Count} error(s), {Warnings.Count} warning(s).";
    }
}
