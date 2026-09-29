using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge;

namespace UnityIsekaiGame.Knowledge.History
{
    [Serializable]
    public sealed class HistoricalEventPayloadData
    {
        public HistoricalEventPayloadKind kind = HistoricalEventPayloadKind.Generic;
        public string fromBodyId;
        public string toBodyId;
        public string speciesId;
        public string locationId;
        public string organizationId;
        public string itemId;
        public string conditionId;
        public string diseaseId;
        public string diagnosisId;
        public string claimValueId;
        public string qualitativeValue;
        public string note;

        public HistoricalEventPayloadData Clone()
        {
            return new HistoricalEventPayloadData
            {
                kind = kind,
                fromBodyId = fromBodyId,
                toBodyId = toBodyId,
                speciesId = speciesId,
                locationId = locationId,
                organizationId = organizationId,
                itemId = itemId,
                conditionId = conditionId,
                diseaseId = diseaseId,
                diagnosisId = diagnosisId,
                claimValueId = claimValueId,
                qualitativeValue = qualitativeValue,
                note = note
            };
        }
    }

    [Serializable]
    public sealed class LifeEventParticipantData
    {
        public string personId;
        public LifeEventParticipantRole role = LifeEventParticipantRole.Participant;
        public string bodyId;
        public string relatedEntityId;
        public string note;

        public LifeEventParticipantData Clone()
        {
            return (LifeEventParticipantData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class LifeEventRelationshipData
    {
        public string relationshipId;
        public LifeEventRelationshipType relationshipType = LifeEventRelationshipType.Related;
        public string targetEventId;
        public bool requiresAcyclic = true;
        public string note;

        public LifeEventRelationshipData Clone()
        {
            return (LifeEventRelationshipData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class LifeEventPayloadData
    {
        public LifeEventPayloadKind kind = LifeEventPayloadKind.Generic;
        public string createdPersonId;
        public string subjectPersonId;
        public string fromStateId;
        public string toStateId;
        public string methodId;
        public string causeId;
        public string severityId;
        public string authorityPersonId;
        public string encounterId;
        public string outcomeId;
        public string evidenceId;
        public string treatmentId;
        public string preTransitionReferenceId;
        public string postTransitionReferenceId;
        public string note;

        public LifeEventPayloadData Clone()
        {
            return (LifeEventPayloadData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class LifeEventSequenceData
    {
        public string sequenceId;
        public string sequenceTypeId;
        public string primaryPersonId;
        public LifeEventSequenceStatus status = LifeEventSequenceStatus.Active;
        public double startedAtWorldTime;
        public double endedAtWorldTime = -1d;
        public string[] eventIds;
        public string correlationId;
        public string note;

        public LifeEventSequenceData Clone()
        {
            return new LifeEventSequenceData
            {
                sequenceId = sequenceId,
                sequenceTypeId = sequenceTypeId,
                primaryPersonId = primaryPersonId,
                status = status,
                startedAtWorldTime = startedAtWorldTime,
                endedAtWorldTime = endedAtWorldTime,
                eventIds = eventIds == null ? Array.Empty<string>() : eventIds.ToArray(),
                correlationId = correlationId,
                note = note
            };
        }
    }

    [Serializable]
    public sealed class HistoricalEventRecordData
    {
        public string eventId;
        public string eventDefinitionId;
        public double occurredAtWorldTime;
        public double recordedAtWorldTime;
        public long sequence;
        public string primaryPersonId;
        public string[] participantPersonIds;
        public string[] bodyIds;
        public string locationId;
        public string organizationId;
        public string[] relatedEntityIds;
        public HistoricalEventCategory category;
        public KnowledgeVisibility visibility;
        public HistoricalEventStatus status;
        public string sourceSystem;
        public string provenance;
        public string supersedesEventId;
        public string correctedByEventId;
        public string correlationId;
        public string[] tags;
        public HistoricalEventPayloadData payload;
        public bool isLifeEvent;
        public LifeEventCategory lifeEventCategory;
        public LifeEventSignificance significance;
        public LifeEventBiographyRelevance biographyRelevance;
        public LifeEventPublicRecordRelevance publicRecordRelevance;
        public LifeEventOutcome lifeEventOutcome;
        public LifeEventParticipantData[] lifeEventParticipants;
        public LifeEventRelationshipData[] lifeEventRelationships;
        public string lifeEventSequenceId;
        public int lifeEventSequenceOrder;
        public string relatedRoleId;
        public string relatedTitleId;
        public string relatedSocialStatusId;
        public string relatedConditionId;
        public string relatedInjuryId;
        public string relatedDiseaseId;
        public string relatedTreatmentId;
        public string relatedCombatEncounterId;
        public string relatedQuestId;
        public string relatedLegalRecordId;
        public string relatedRelationshipId;
        public LifeEventPayloadData lifeEventPayload;

        public HistoricalEventRecordData Clone()
        {
            return new HistoricalEventRecordData
            {
                eventId = eventId,
                eventDefinitionId = eventDefinitionId,
                occurredAtWorldTime = occurredAtWorldTime,
                recordedAtWorldTime = recordedAtWorldTime,
                sequence = sequence,
                primaryPersonId = primaryPersonId,
                participantPersonIds = participantPersonIds == null ? Array.Empty<string>() : participantPersonIds.ToArray(),
                bodyIds = bodyIds == null ? Array.Empty<string>() : bodyIds.ToArray(),
                locationId = locationId,
                organizationId = organizationId,
                relatedEntityIds = relatedEntityIds == null ? Array.Empty<string>() : relatedEntityIds.ToArray(),
                category = category,
                visibility = visibility,
                status = status,
                sourceSystem = sourceSystem,
                provenance = provenance,
                supersedesEventId = supersedesEventId,
                correctedByEventId = correctedByEventId,
                correlationId = correlationId,
                tags = tags == null ? Array.Empty<string>() : tags.ToArray(),
                payload = payload?.Clone(),
                isLifeEvent = isLifeEvent,
                lifeEventCategory = lifeEventCategory,
                significance = significance,
                biographyRelevance = biographyRelevance,
                publicRecordRelevance = publicRecordRelevance,
                lifeEventOutcome = lifeEventOutcome,
                lifeEventParticipants = lifeEventParticipants == null ? Array.Empty<LifeEventParticipantData>() : lifeEventParticipants.Select(participant => participant?.Clone()).Where(participant => participant != null).ToArray(),
                lifeEventRelationships = lifeEventRelationships == null ? Array.Empty<LifeEventRelationshipData>() : lifeEventRelationships.Select(relationship => relationship?.Clone()).Where(relationship => relationship != null).ToArray(),
                lifeEventSequenceId = lifeEventSequenceId,
                lifeEventSequenceOrder = lifeEventSequenceOrder,
                relatedRoleId = relatedRoleId,
                relatedTitleId = relatedTitleId,
                relatedSocialStatusId = relatedSocialStatusId,
                relatedConditionId = relatedConditionId,
                relatedInjuryId = relatedInjuryId,
                relatedDiseaseId = relatedDiseaseId,
                relatedTreatmentId = relatedTreatmentId,
                relatedCombatEncounterId = relatedCombatEncounterId,
                relatedQuestId = relatedQuestId,
                relatedLegalRecordId = relatedLegalRecordId,
                relatedRelationshipId = relatedRelationshipId,
                lifeEventPayload = lifeEventPayload?.Clone()
            };
        }
    }

    [Serializable]
    public sealed class MemoryDetailData
    {
        public string detailId;
        public MemoryDetailKind kind;
        public MemoryDetailState state = MemoryDetailState.Remembered;
        public string value;
        public int confidence = 700;
        public string sourceId;
        public string alteredByRevisionId;

        public MemoryDetailData Clone()
        {
            return new MemoryDetailData
            {
                detailId = detailId,
                kind = kind,
                state = state,
                value = value,
                confidence = HistoryMath.ClampMetric(confidence),
                sourceId = sourceId,
                alteredByRevisionId = alteredByRevisionId
            };
        }
    }

    [Serializable]
    public sealed class MemorySuppressionData
    {
        public string suppressionId;
        public string memoryId;
        public string sourceId;
        public string reasonId;
        public double startedAtWorldTime;
        public double endedAtWorldTime = -1d;
        public bool removed;
        public double removedAtWorldTime = -1d;
        public bool allowsCueBypass;
        public bool privilegedVisible = true;
        public string provenance;

        public MemorySuppressionData Clone()
        {
            return (MemorySuppressionData)MemberwiseClone();
        }

        public bool IsActiveAt(double worldTime)
        {
            if (removed)
            {
                return false;
            }

            return startedAtWorldTime <= worldTime && (endedAtWorldTime < 0d || worldTime < endedAtWorldTime);
        }
    }

    [Serializable]
    public sealed class MemoryRevisionData
    {
        public string revisionId;
        public string previousRevisionId;
        public string transactionId;
        public MemoryAlterationType alterationType;
        public double worldTime;
        public string sourceId;
        public string description;
        public MemoryState state;
        public int confidence;
        public int clarity;
        public int salience;
        public string bodyAtTimeId;
        public MemoryDetailData[] details;

        public MemoryRevisionData Clone()
        {
            return new MemoryRevisionData
            {
                revisionId = revisionId,
                previousRevisionId = previousRevisionId,
                transactionId = transactionId,
                alterationType = alterationType,
                worldTime = worldTime,
                sourceId = sourceId,
                description = description,
                state = state,
                confidence = HistoryMath.ClampMetric(confidence),
                clarity = HistoryMath.ClampMetric(clarity),
                salience = HistoryMath.ClampMetric(salience),
                bodyAtTimeId = bodyAtTimeId,
                details = details == null ? Array.Empty<MemoryDetailData>() : details.Select(detail => detail?.Clone()).Where(detail => detail != null).ToArray()
            };
        }
    }

    [Serializable]
    public sealed class HistoryMemoryRecordData
    {
        public string memoryId;
        public string ownerPersonId;
        public string historicalEventId;
        public string beliefId;
        public string[] evidenceIds;
        public HistoryMemorySource source;
        public double formedAtWorldTime;
        public double rememberedOccurredAtWorldTime;
        public double lastRecalledWorldTime;
        public double lastRecallAttemptWorldTime;
        public double lastReinforcedWorldTime;
        public double lastDegradationEvaluatedWorldTime;
        public int recallCount;
        public int reinforcementCount;
        public int confidence;
        public int clarity;
        public int salience;
        public bool firstHand;
        public MemoryState state;
        public KnowledgeVisibility visibility;
        public MemoryState stateBeforeSuppression;
        public string identityAtTimeId;
        public string bodyAtTimeId;
        public string correctedByMemoryId;
        public string correctionOfMemoryId;
        public string currentRevisionId;
        public MemoryDetailData[] rememberedDetails;
        public MemorySuppressionData[] suppressions;
        public MemoryRevisionData[] revisions;
        public string[] tags;
        public string debugDescription;

        public HistoryMemoryRecordData Clone()
        {
            return new HistoryMemoryRecordData
            {
                memoryId = memoryId,
                ownerPersonId = ownerPersonId,
                historicalEventId = historicalEventId,
                beliefId = beliefId,
                evidenceIds = evidenceIds == null ? Array.Empty<string>() : evidenceIds.ToArray(),
                source = source,
                formedAtWorldTime = formedAtWorldTime,
                rememberedOccurredAtWorldTime = rememberedOccurredAtWorldTime,
                lastRecalledWorldTime = lastRecalledWorldTime,
                lastRecallAttemptWorldTime = lastRecallAttemptWorldTime,
                lastReinforcedWorldTime = lastReinforcedWorldTime,
                lastDegradationEvaluatedWorldTime = lastDegradationEvaluatedWorldTime,
                recallCount = Math.Max(0, recallCount),
                reinforcementCount = Math.Max(0, reinforcementCount),
                confidence = HistoryMath.ClampMetric(confidence),
                clarity = HistoryMath.ClampMetric(clarity),
                salience = HistoryMath.ClampMetric(salience),
                firstHand = firstHand,
                state = state,
                visibility = visibility,
                stateBeforeSuppression = stateBeforeSuppression,
                identityAtTimeId = identityAtTimeId,
                bodyAtTimeId = bodyAtTimeId,
                correctedByMemoryId = correctedByMemoryId,
                correctionOfMemoryId = correctionOfMemoryId,
                currentRevisionId = currentRevisionId,
                rememberedDetails = rememberedDetails == null ? Array.Empty<MemoryDetailData>() : rememberedDetails.Select(detail => detail?.Clone()).Where(detail => detail != null).ToArray(),
                suppressions = suppressions == null ? Array.Empty<MemorySuppressionData>() : suppressions.Select(suppression => suppression?.Clone()).Where(suppression => suppression != null).ToArray(),
                revisions = revisions == null ? Array.Empty<MemoryRevisionData>() : revisions.Select(revision => revision?.Clone()).Where(revision => revision != null).ToArray(),
                tags = tags == null ? Array.Empty<string>() : tags.ToArray(),
                debugDescription = debugDescription
            };
        }
    }

    [Serializable]
    public sealed class BodyOccupationRecordData
    {
        public string occupationId;
        public string personId;
        public string bodyId;
        public double startedAtWorldTime;
        public double endedAtWorldTime = -1d;
        public string startEventId;
        public string endEventId;
        public string reason;

        public BodyOccupationRecordData Clone()
        {
            return (BodyOccupationRecordData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class AuthoritativeHistorySaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long nextSequence;
        public long historyRevision;
        public HistoricalEventRecordData[] events;
        public BodyOccupationRecordData[] bodyOccupations;
        public LifeEventSequenceData[] lifeEventSequences;
        public string[] processedTransactions;
    }

    [Serializable]
    public sealed class PersonMemorySaveData
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public string personId;
        public long memoryRevision;
        public HistoryMemoryRecordData[] memories;
        public string[] processedTransactions;
    }

    public sealed class HistoricalEventRecord
    {
        public HistoricalEventRecord(HistoricalEventRecordData data, HistoricalEventDefinition definition)
        {
            Data = data == null ? new HistoricalEventRecordData() : data.Clone();
            Definition = definition;
        }

        public HistoricalEventRecordData Data { get; }
        public HistoricalEventDefinition Definition { get; }
        public string EventId => Data.eventId ?? string.Empty;
        public string EventDefinitionId => Data.eventDefinitionId ?? string.Empty;
        public double OccurredAtWorldTime => Data.occurredAtWorldTime;
        public double RecordedAtWorldTime => Data.recordedAtWorldTime;
        public long Sequence => Data.sequence;
        public string PrimaryPersonId => Data.primaryPersonId ?? string.Empty;
        public IReadOnlyList<string> ParticipantPersonIds => Data.participantPersonIds ?? Array.Empty<string>();
        public IReadOnlyList<string> BodyIds => Data.bodyIds ?? Array.Empty<string>();
        public string LocationId => Data.locationId ?? string.Empty;
        public string OrganizationId => Data.organizationId ?? string.Empty;
        public HistoricalEventCategory Category => Data.category;
        public KnowledgeVisibility Visibility => Data.visibility;
        public HistoricalEventStatus Status => Data.status;
        public string SupersedesEventId => Data.supersedesEventId ?? string.Empty;
        public string CorrectedByEventId => Data.correctedByEventId ?? string.Empty;
        public IReadOnlyList<string> Tags => Data.tags ?? Array.Empty<string>();
        public HistoricalEventPayloadData Payload => Data.payload?.Clone() ?? new HistoricalEventPayloadData();
        public bool IsLifeEvent => Data.isLifeEvent;
        public LifeEventCategory LifeEventCategory => Data.lifeEventCategory;
        public LifeEventSignificance Significance => Data.significance;
        public LifeEventBiographyRelevance BiographyRelevance => Data.biographyRelevance;
        public LifeEventPublicRecordRelevance PublicRecordRelevance => Data.publicRecordRelevance;
        public LifeEventOutcome LifeEventOutcome => Data.lifeEventOutcome;
        public IReadOnlyList<LifeEventParticipantData> LifeEventParticipants => Data.lifeEventParticipants == null ? Array.Empty<LifeEventParticipantData>() : Data.lifeEventParticipants.Select(participant => participant.Clone()).ToArray();
        public IReadOnlyList<LifeEventRelationshipData> LifeEventRelationships => Data.lifeEventRelationships == null ? Array.Empty<LifeEventRelationshipData>() : Data.lifeEventRelationships.Select(relationship => relationship.Clone()).ToArray();
        public string LifeEventSequenceId => Data.lifeEventSequenceId ?? string.Empty;
        public int LifeEventSequenceOrder => Data.lifeEventSequenceOrder;
        public string RelatedRoleId => Data.relatedRoleId ?? string.Empty;
        public string RelatedTitleId => Data.relatedTitleId ?? string.Empty;
        public string RelatedSocialStatusId => Data.relatedSocialStatusId ?? string.Empty;
        public string RelatedConditionId => Data.relatedConditionId ?? string.Empty;
        public string RelatedInjuryId => Data.relatedInjuryId ?? string.Empty;
        public string RelatedDiseaseId => Data.relatedDiseaseId ?? string.Empty;
        public string RelatedTreatmentId => Data.relatedTreatmentId ?? string.Empty;
        public string RelatedCombatEncounterId => Data.relatedCombatEncounterId ?? string.Empty;
        public string RelatedQuestId => Data.relatedQuestId ?? string.Empty;
        public string RelatedLegalRecordId => Data.relatedLegalRecordId ?? string.Empty;
        public string RelatedRelationshipId => Data.relatedRelationshipId ?? string.Empty;
        public LifeEventPayloadData LifeEventPayload => Data.lifeEventPayload?.Clone() ?? new LifeEventPayloadData();
    }

    public sealed class LifeEventRecord
    {
        public LifeEventRecord(HistoricalEventRecord eventRecord)
        {
            Event = eventRecord ?? new HistoricalEventRecord(null, null);
            EventId = Event.EventId;
            DefinitionId = Event.EventDefinitionId;
            PrimaryPersonId = Event.PrimaryPersonId;
            Category = Event.LifeEventCategory;
            OccurredAtWorldTime = Event.OccurredAtWorldTime;
            RecordedAtWorldTime = Event.RecordedAtWorldTime;
            Sequence = Event.Sequence;
            Visibility = Event.Visibility;
            Status = Event.Status;
            Significance = Event.Significance;
            BiographyRelevance = Event.BiographyRelevance;
            PublicRecordRelevance = Event.PublicRecordRelevance;
            Outcome = Event.LifeEventOutcome;
            Participants = Event.LifeEventParticipants.ToArray();
            Relationships = Event.LifeEventRelationships.ToArray();
            SequenceId = Event.LifeEventSequenceId;
            SequenceOrder = Event.LifeEventSequenceOrder;
            Tags = Event.Tags.ToArray();
            Payload = Event.LifeEventPayload;
        }

        public HistoricalEventRecord Event { get; }
        public string EventId { get; }
        public string DefinitionId { get; }
        public string PrimaryPersonId { get; }
        public LifeEventCategory Category { get; }
        public double OccurredAtWorldTime { get; }
        public double RecordedAtWorldTime { get; }
        public long Sequence { get; }
        public KnowledgeVisibility Visibility { get; }
        public HistoricalEventStatus Status { get; }
        public LifeEventSignificance Significance { get; }
        public LifeEventBiographyRelevance BiographyRelevance { get; }
        public LifeEventPublicRecordRelevance PublicRecordRelevance { get; }
        public LifeEventOutcome Outcome { get; }
        public IReadOnlyList<LifeEventParticipantData> Participants { get; }
        public IReadOnlyList<LifeEventRelationshipData> Relationships { get; }
        public string SequenceId { get; }
        public int SequenceOrder { get; }
        public IReadOnlyList<string> Tags { get; }
        public LifeEventPayloadData Payload { get; }
    }

    public sealed class LifeEventSequenceRecord
    {
        public LifeEventSequenceRecord(LifeEventSequenceData data, IReadOnlyList<LifeEventRecord> events)
        {
            Data = data == null ? new LifeEventSequenceData() : data.Clone();
            Events = (events ?? Array.Empty<LifeEventRecord>())
                .OrderBy(record => record.SequenceOrder)
                .ThenBy(record => HistoryOrdering.Key(record.Event))
                .ToArray();
        }

        public LifeEventSequenceData Data { get; }
        public string SequenceId => Data.sequenceId ?? string.Empty;
        public string SequenceTypeId => Data.sequenceTypeId ?? string.Empty;
        public string PrimaryPersonId => Data.primaryPersonId ?? string.Empty;
        public LifeEventSequenceStatus Status => Data.status;
        public double StartedAtWorldTime => Data.startedAtWorldTime;
        public double EndedAtWorldTime => Data.endedAtWorldTime;
        public IReadOnlyList<string> EventIds => Data.eventIds ?? Array.Empty<string>();
        public IReadOnlyList<LifeEventRecord> Events { get; }
    }

    public sealed class BiographyTimelineEntry
    {
        public BiographyTimelineEntry(LifeEventRecord lifeEvent, LifeEventParticipantRole participantRole, bool known, bool remembered)
        {
            LifeEvent = lifeEvent;
            EventId = lifeEvent?.EventId ?? string.Empty;
            DefinitionId = lifeEvent?.DefinitionId ?? string.Empty;
            Category = lifeEvent?.Category ?? LifeEventCategory.None;
            OccurredAtWorldTime = lifeEvent?.OccurredAtWorldTime ?? 0d;
            ParticipantRole = participantRole;
            Significance = lifeEvent?.Significance ?? LifeEventSignificance.Trivial;
            BiographyRelevance = lifeEvent?.BiographyRelevance ?? LifeEventBiographyRelevance.NotRelevant;
            Visibility = lifeEvent?.Visibility ?? KnowledgeVisibility.Public;
            Known = known;
            Remembered = remembered;
        }

        public LifeEventRecord LifeEvent { get; }
        public string EventId { get; }
        public string DefinitionId { get; }
        public LifeEventCategory Category { get; }
        public double OccurredAtWorldTime { get; }
        public LifeEventParticipantRole ParticipantRole { get; }
        public LifeEventSignificance Significance { get; }
        public LifeEventBiographyRelevance BiographyRelevance { get; }
        public KnowledgeVisibility Visibility { get; }
        public bool Known { get; }
        public bool Remembered { get; }
    }

    public sealed class HistoryMemoryRecord
    {
        public HistoryMemoryRecord(HistoryMemoryRecordData data)
        {
            Data = data == null ? new HistoryMemoryRecordData() : data.Clone();
        }

        public HistoryMemoryRecordData Data { get; }
        public string MemoryId => Data.memoryId ?? string.Empty;
        public string OwnerPersonId => Data.ownerPersonId ?? string.Empty;
        public string HistoricalEventId => Data.historicalEventId ?? string.Empty;
        public string BeliefId => Data.beliefId ?? string.Empty;
        public IReadOnlyList<string> EvidenceIds => Data.evidenceIds ?? Array.Empty<string>();
        public HistoryMemorySource Source => Data.source;
        public MemoryState State => Data.state;
        public int Confidence => HistoryMath.ClampMetric(Data.confidence);
        public int Clarity => HistoryMath.ClampMetric(Data.clarity);
        public int Salience => HistoryMath.ClampMetric(Data.salience);
        public double FormedAtWorldTime => Data.formedAtWorldTime;
        public double RememberedOccurredAtWorldTime => Data.rememberedOccurredAtWorldTime;
        public double LastRecalledWorldTime => Data.lastRecalledWorldTime;
        public double LastRecallAttemptWorldTime => Data.lastRecallAttemptWorldTime;
        public double LastReinforcedWorldTime => Data.lastReinforcedWorldTime;
        public int RecallCount => Math.Max(0, Data.recallCount);
        public int ReinforcementCount => Math.Max(0, Data.reinforcementCount);
        public double LastDegradationEvaluatedWorldTime => Data.lastDegradationEvaluatedWorldTime;
        public bool Accessible => State != MemoryState.Forgotten && State != MemoryState.Inaccessible && State != MemoryState.Suppressed && State != MemoryState.Dormant;
        public string BodyAtTimeId => Data.bodyAtTimeId ?? string.Empty;
        public string CurrentRevisionId => Data.currentRevisionId ?? string.Empty;
        public IReadOnlyList<MemoryDetailData> RememberedDetails => Data.rememberedDetails == null ? Array.Empty<MemoryDetailData>() : Data.rememberedDetails.Select(detail => detail.Clone()).ToArray();
        public IReadOnlyList<MemorySuppressionData> Suppressions => Data.suppressions == null ? Array.Empty<MemorySuppressionData>() : Data.suppressions.Select(suppression => suppression.Clone()).ToArray();
        public IReadOnlyList<MemoryRevisionData> Revisions => Data.revisions == null ? Array.Empty<MemoryRevisionData>() : Data.revisions.Select(revision => revision.Clone()).ToArray();
    }

    public sealed class BodyOccupationRecord
    {
        public BodyOccupationRecord(BodyOccupationRecordData data)
        {
            Data = data == null ? new BodyOccupationRecordData() : data.Clone();
        }

        public BodyOccupationRecordData Data { get; }
        public string OccupationId => Data.occupationId ?? string.Empty;
        public string PersonId => Data.personId ?? string.Empty;
        public string BodyId => Data.bodyId ?? string.Empty;
        public double StartedAtWorldTime => Data.startedAtWorldTime;
        public double EndedAtWorldTime => Data.endedAtWorldTime;
        public bool IsOpenEnded => EndedAtWorldTime < 0d;
    }

    public sealed class HistorySnapshot
    {
        public HistorySnapshot(string worldId, long revision, IReadOnlyList<HistoricalEventRecord> events, IReadOnlyList<BodyOccupationRecord> bodyOccupations)
        {
            WorldId = worldId ?? string.Empty;
            Revision = revision;
            Events = (events ?? Array.Empty<HistoricalEventRecord>()).OrderBy(HistoryOrdering.Key).ToArray();
            BodyOccupations = (bodyOccupations ?? Array.Empty<BodyOccupationRecord>()).OrderBy(record => record.StartedAtWorldTime).ThenBy(record => record.OccupationId, StringComparer.Ordinal).ToArray();
        }

        public string WorldId { get; }
        public long Revision { get; }
        public IReadOnlyList<HistoricalEventRecord> Events { get; }
        public IReadOnlyList<BodyOccupationRecord> BodyOccupations { get; }
    }

    public sealed class PersonMemorySnapshot
    {
        public PersonMemorySnapshot(string personId, long revision, IReadOnlyList<HistoryMemoryRecord> memories)
        {
            PersonId = personId ?? string.Empty;
            Revision = revision;
            Memories = (memories ?? Array.Empty<HistoryMemoryRecord>()).OrderBy(record => record.MemoryId, StringComparer.Ordinal).ToArray();
        }

        public string PersonId { get; }
        public long Revision { get; }
        public IReadOnlyList<HistoryMemoryRecord> Memories { get; }
        public IReadOnlyList<HistoryMemoryRecord> AccessibleMemories => Memories.Where(memory => memory.Accessible).ToArray();
    }

    public class RecordHistoricalEventRequest
    {
        public string TransactionId { get; set; }
        public string EventId { get; set; }
        public string EventDefinitionId { get; set; }
        public double OccurredAtWorldTime { get; set; }
        public double RecordedAtWorldTime { get; set; }
        public long? Sequence { get; set; }
        public string PrimaryPersonId { get; set; }
        public string[] ParticipantPersonIds { get; set; }
        public string[] BodyIds { get; set; }
        public string LocationId { get; set; }
        public string OrganizationId { get; set; }
        public string[] RelatedEntityIds { get; set; }
        public HistoricalEventCategory? Category { get; set; }
        public KnowledgeVisibility? Visibility { get; set; }
        public string SourceSystem { get; set; }
        public string Provenance { get; set; }
        public string SupersedesEventId { get; set; }
        public string CorrelationId { get; set; }
        public string[] Tags { get; set; }
        public HistoricalEventPayloadData Payload { get; set; }
    }

    public sealed class RecordLifeEventRequest
    {
        public string TransactionId { get; set; }
        public string EventId { get; set; }
        public string EventDefinitionId { get; set; }
        public LifeEventCategory? Category { get; set; }
        public LifeEventPayloadKind? PayloadKind { get; set; }
        public double OccurredAtWorldTime { get; set; }
        public double RecordedAtWorldTime { get; set; }
        public long? Sequence { get; set; }
        public string PrimaryPersonId { get; set; }
        public LifeEventParticipantData[] Participants { get; set; }
        public string[] BodyIds { get; set; }
        public string LocationId { get; set; }
        public string OrganizationId { get; set; }
        public string[] RelatedEntityIds { get; set; }
        public KnowledgeVisibility? Visibility { get; set; }
        public LifeEventSignificance? Significance { get; set; }
        public LifeEventBiographyRelevance? BiographyRelevance { get; set; }
        public LifeEventPublicRecordRelevance? PublicRecordRelevance { get; set; }
        public LifeEventOutcome Outcome { get; set; } = LifeEventOutcome.Unknown;
        public LifeEventRelationshipData[] Relationships { get; set; }
        public string SequenceId { get; set; }
        public int SequenceOrder { get; set; }
        public string SequenceTypeId { get; set; }
        public LifeEventSequenceStatus SequenceStatus { get; set; } = LifeEventSequenceStatus.Active;
        public string RelatedRoleId { get; set; }
        public string RelatedTitleId { get; set; }
        public string RelatedSocialStatusId { get; set; }
        public string RelatedConditionId { get; set; }
        public string RelatedInjuryId { get; set; }
        public string RelatedDiseaseId { get; set; }
        public string RelatedTreatmentId { get; set; }
        public string RelatedCombatEncounterId { get; set; }
        public string RelatedQuestId { get; set; }
        public string RelatedLegalRecordId { get; set; }
        public string RelatedRelationshipId { get; set; }
        public string SourceSystem { get; set; }
        public string Provenance { get; set; }
        public string SupersedesEventId { get; set; }
        public string CorrelationId { get; set; }
        public string[] Tags { get; set; }
        public HistoricalEventPayloadData HistoricalPayload { get; set; }
        public LifeEventPayloadData LifeEventPayload { get; set; }
    }

    public sealed class FormMemoryRequest
    {
        public string TransactionId { get; set; }
        public string MemoryId { get; set; }
        public string OwnerPersonId { get; set; }
        public string HistoricalEventId { get; set; }
        public string BeliefId { get; set; }
        public string[] EvidenceIds { get; set; }
        public HistoryMemorySource Source { get; set; } = HistoryMemorySource.DirectObservation;
        public double FormedAtWorldTime { get; set; }
        public double RememberedOccurredAtWorldTime { get; set; }
        public int Confidence { get; set; } = 700;
        public int Clarity { get; set; } = 700;
        public int Salience { get; set; } = 500;
        public bool FirstHand { get; set; }
        public KnowledgeVisibility Visibility { get; set; } = KnowledgeVisibility.Private;
        public string IdentityAtTimeId { get; set; }
        public string BodyAtTimeId { get; set; }
        public string DebugDescription { get; set; }
        public string[] Tags { get; set; }
        public bool CreateKnowledgeEvidence { get; set; }
    }

    public sealed class HistoryOperationResult
    {
        private HistoryOperationResult(bool succeeded, HistoryResultCode code, string message, string transactionId, bool preview, bool duplicate, HistoricalEventRecord eventRecord, HistoryMemoryRecord memory, KnowledgeOperationResult knowledgeResult, long priorRevision, long resultingRevision)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Event = eventRecord;
            Memory = memory;
            KnowledgeResult = knowledgeResult;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
        }

        public bool Succeeded { get; }
        public HistoryResultCode Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public HistoricalEventRecord Event { get; }
        public HistoryMemoryRecord Memory { get; }
        public KnowledgeOperationResult KnowledgeResult { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }

        public static HistoryOperationResult Success(string message, string transactionId, HistoricalEventRecord eventRecord, HistoryMemoryRecord memory, KnowledgeOperationResult knowledgeResult, long priorRevision, long resultingRevision, bool preview = false, bool duplicate = false)
        {
            return new HistoryOperationResult(true, duplicate ? HistoryResultCode.Duplicate : preview ? HistoryResultCode.Preview : HistoryResultCode.Success, message, transactionId, preview, duplicate, eventRecord, memory, knowledgeResult, priorRevision, resultingRevision);
        }

        public static HistoryOperationResult Failure(HistoryResultCode code, string message, string transactionId = "", bool preview = false, long revision = 0L)
        {
            return new HistoryOperationResult(false, code, message, transactionId, preview, false, null, null, null, revision, revision);
        }
    }

    public sealed class MemoryRecallCue
    {
        public MemoryCueKind Kind { get; set; }
        public string ReferenceId { get; set; }
        public int Strength { get; set; } = 500;
    }

    public sealed class MemoryRecallRequest
    {
        public string TransactionId { get; set; }
        public string RequestingPersonId { get; set; }
        public string MemoryId { get; set; }
        public string HistoricalEventId { get; set; }
        public string SubjectId { get; set; }
        public string BodyId { get; set; }
        public string LocationId { get; set; }
        public string OrganizationId { get; set; }
        public string[] Tags { get; set; }
        public MemoryRecallCue[] Cues { get; set; }
        public int MaxResults { get; set; } = 8;
        public double WorldTime { get; set; }
        public MemoryAccessContext AccessContext { get; set; } = MemoryAccessContext.OrdinaryRecall;
        public bool AttemptDifficult { get; set; }
        public bool AllowCueRecovery { get; set; }
        public bool ReinforceOnSuccess { get; set; }
        public bool MutateMetadata { get; set; } = true;
        public string DeterministicSeed { get; set; }
    }

    public sealed class MemoryRecallEntry
    {
        public MemoryRecallEntry(HistoryMemoryRecord memory, MemoryRecallOutcome outcome, IReadOnlyList<MemoryDetailData> recalledDetails, IReadOnlyList<MemoryDetailData> unavailableDetails, bool metadataUpdated, bool cueMatched, string reason)
        {
            Memory = memory;
            Outcome = outcome;
            RecalledDetails = (recalledDetails ?? Array.Empty<MemoryDetailData>()).Select(detail => detail.Clone()).ToArray();
            UnavailableDetails = (unavailableDetails ?? Array.Empty<MemoryDetailData>()).Select(detail => detail.Clone()).ToArray();
            MetadataUpdated = metadataUpdated;
            CueMatched = cueMatched;
            Reason = reason ?? string.Empty;
        }

        public HistoryMemoryRecord Memory { get; }
        public MemoryRecallOutcome Outcome { get; }
        public IReadOnlyList<MemoryDetailData> RecalledDetails { get; }
        public IReadOnlyList<MemoryDetailData> UnavailableDetails { get; }
        public bool MetadataUpdated { get; }
        public bool CueMatched { get; }
        public string Reason { get; }
    }

    public sealed class MemoryRecallResult
    {
        private MemoryRecallResult(bool succeeded, HistoryResultCode code, MemoryRecallOutcome outcome, string transactionId, string message, IReadOnlyList<MemoryRecallEntry> entries, long priorRevision, long resultingRevision, bool preview)
        {
            Succeeded = succeeded;
            Code = code;
            Outcome = outcome;
            TransactionId = transactionId ?? string.Empty;
            Message = message ?? string.Empty;
            Entries = (entries ?? Array.Empty<MemoryRecallEntry>()).ToArray();
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Preview = preview;
        }

        public bool Succeeded { get; }
        public HistoryResultCode Code { get; }
        public MemoryRecallOutcome Outcome { get; }
        public string TransactionId { get; }
        public string Message { get; }
        public IReadOnlyList<MemoryRecallEntry> Entries { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public bool Preview { get; }

        public static MemoryRecallResult Success(MemoryRecallOutcome outcome, string transactionId, string message, IReadOnlyList<MemoryRecallEntry> entries, long priorRevision, long resultingRevision, bool preview = false)
        {
            return new MemoryRecallResult(true, preview ? HistoryResultCode.Preview : HistoryResultCode.Success, preview ? MemoryRecallOutcome.Preview : outcome, transactionId, message, entries, priorRevision, resultingRevision, preview);
        }

        public static MemoryRecallResult Failure(HistoryResultCode code, MemoryRecallOutcome outcome, string transactionId, string message, long revision)
        {
            return new MemoryRecallResult(false, code, outcome, transactionId, message, Array.Empty<MemoryRecallEntry>(), revision, revision, preview: false);
        }
    }

    public sealed class MemoryReinforcementRequest
    {
        public string TransactionId { get; set; }
        public string OwnerPersonId { get; set; }
        public string MemoryId { get; set; }
        public double WorldTime { get; set; }
        public MemoryReinforcementSource Source { get; set; } = MemoryReinforcementSource.SuccessfulRecall;
        public int ConfidenceDelta { get; set; }
        public int ClarityDelta { get; set; }
        public int SalienceDelta { get; set; }
        public bool ImproveAccessibility { get; set; } = true;
        public string SourceId { get; set; }
    }

    public sealed class MemoryDegradationRequest
    {
        public string TransactionId { get; set; }
        public string OwnerPersonId { get; set; }
        public string MemoryId { get; set; }
        public double FromWorldTime { get; set; }
        public double ToWorldTime { get; set; }
        public int ConfidenceLossPerDay { get; set; }
        public int ClarityLossPerDay { get; set; }
        public int SalienceLossPerDay { get; set; }
        public int DifficultClarityThreshold { get; set; } = 350;
        public int InaccessibleClarityThreshold { get; set; } = 150;
        public int ForgottenClarityThreshold { get; set; } = 0;
        public bool CreateRevision { get; set; } = true;
    }

    public sealed class MemoryAlterationRequest
    {
        public string TransactionId { get; set; }
        public string OwnerPersonId { get; set; }
        public string MemoryId { get; set; }
        public double WorldTime { get; set; }
        public MemoryAlterationType AlterationType { get; set; }
        public MemoryState? ResultingState { get; set; }
        public string SourceId { get; set; }
        public string Description { get; set; }
        public string[] DetailIdsToForget { get; set; }
        public MemoryDetailData[] DetailsToAddOrReplace { get; set; }
        public int ConfidenceDelta { get; set; }
        public int ClarityDelta { get; set; }
        public int SalienceDelta { get; set; }
        public string BodyAtTimeId { get; set; }
    }

    public sealed class MemorySuppressionRequest
    {
        public string TransactionId { get; set; }
        public string OwnerPersonId { get; set; }
        public string MemoryId { get; set; }
        public string SuppressionId { get; set; }
        public string SourceId { get; set; }
        public string ReasonId { get; set; }
        public double StartedAtWorldTime { get; set; }
        public double EndedAtWorldTime { get; set; } = -1d;
        public bool AllowsCueBypass { get; set; }
        public string Provenance { get; set; }
    }

    public static class HistoryMath
    {
        public static int ClampMetric(int value)
        {
            return Math.Max(0, Math.Min(1000, value));
        }
    }

    public static class HistoryOrdering
    {
        public static Tuple<double, long, string> Key(HistoricalEventRecord record)
        {
            return Tuple.Create(record?.OccurredAtWorldTime ?? 0d, record?.Sequence ?? 0L, record?.EventId ?? string.Empty);
        }

        public static int Compare(HistoricalEventRecord left, HistoricalEventRecord right)
        {
            int time = (left?.OccurredAtWorldTime ?? 0d).CompareTo(right?.OccurredAtWorldTime ?? 0d);
            if (time != 0)
            {
                return time;
            }

            int sequence = (left?.Sequence ?? 0L).CompareTo(right?.Sequence ?? 0L);
            return sequence != 0 ? sequence : string.Compare(left?.EventId, right?.EventId, StringComparison.Ordinal);
        }
    }
}
