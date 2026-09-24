using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Knowledge.History
{
    public sealed class AuthoritativeHistoryRuntime
    {
        private readonly Dictionary<string, HistoricalEventRecordData> eventsById = new Dictionary<string, HistoricalEventRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> eventIdsByPerson = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> eventIdsByBody = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> eventIdsByLocation = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> eventIdsByOrganization = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> eventIdsByTag = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, BodyOccupationRecordData> occupationsById = new Dictionary<string, BodyOccupationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> occupationIdsByPerson = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, LifeEventSequenceData> lifeEventSequencesById = new Dictionary<string, LifeEventSequenceData>(StringComparer.Ordinal);
        private readonly HashSet<string> processedTransactions = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownBodyIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private bool suppressEvents;

        public event Action<AuthoritativeHistoryRuntime, HistoryOperationResult> HistoryChanged;

        public string WorldId { get; private set; } = "world.local";
        public long HistoryRevision { get; private set; }
        public long NextSequence { get; private set; } = 1L;

        public void Configure(DefinitionRegistry definitionRegistry, string worldId, IEnumerable<string> knownPersons = null, IEnumerable<string> knownBodies = null)
        {
            registry = definitionRegistry ?? registry;
            WorldId = string.IsNullOrWhiteSpace(worldId) ? WorldId : worldId;
            knownPersonIds = new HashSet<string>((knownPersons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            knownBodyIds = new HashSet<string>((knownBodies ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
        }

        public HistoryOperationResult PreviewRecordEvent(RecordHistoricalEventRequest request)
        {
            return RecordEvent(request, preview: true, restoring: false);
        }

        public HistoryOperationResult RecordEvent(RecordHistoricalEventRequest request, bool preview = false, bool restoring = false)
        {
            long priorRevision = HistoryRevision;
            string transactionKey = TransactionKey(request?.TransactionId);
            if (!preview
                && request != null
                && !string.IsNullOrWhiteSpace(request.TransactionId)
                && processedTransactions.Contains(transactionKey))
            {
                HistoricalEventRecord existingById = TryGetEvent(request.EventId, out HistoricalEventRecord duplicateRecord)
                    ? duplicateRecord
                    : null;
                return HistoryOperationResult.Success("Historical event transaction already processed.", request.TransactionId, existingById, null, null, priorRevision, HistoryRevision, duplicate: true);
            }

            if (!ValidateRecordRequest(request, out HistoricalEventDefinition definition, out string failure, out HistoryResultCode code))
            {
                return HistoryOperationResult.Failure(code, failure, request?.TransactionId, preview, HistoryRevision);
            }

            HistoricalEventRecordData data = CreateEventData(request, definition);
            HistoricalEventRecord snapshot = Wrap(data);
            if (preview)
            {
                return HistoryOperationResult.Success("Historical event preview succeeded.", request.TransactionId, snapshot, null, null, priorRevision, HistoryRevision, preview: true);
            }

            if (!string.IsNullOrWhiteSpace(data.supersedesEventId))
            {
                eventsById[data.supersedesEventId].status = HistoricalEventStatus.Superseded;
                eventsById[data.supersedesEventId].correctedByEventId = data.eventId;
                data.status = HistoricalEventStatus.Correction;
            }

            eventsById[data.eventId] = data;
            AddIndexes(data);
            processedTransactions.Add(transactionKey);
            HistoryRevision++;
            HistoryOperationResult result = HistoryOperationResult.Success("Historical event recorded.", request.TransactionId, Wrap(data), null, null, priorRevision, HistoryRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public HistoryOperationResult RecordBodyTransition(string transactionId, string eventId, string personId, string fromBodyId, string toBodyId, double occurredAtWorldTime, double recordedAtWorldTime, string reason, bool preview = false, bool restoring = false)
        {
            RecordHistoricalEventRequest request = new RecordHistoricalEventRequest
            {
                TransactionId = transactionId,
                EventId = eventId,
                EventDefinitionId = "history-event.body-transition",
                OccurredAtWorldTime = occurredAtWorldTime,
                RecordedAtWorldTime = recordedAtWorldTime,
                PrimaryPersonId = personId,
                ParticipantPersonIds = new[] { personId },
                BodyIds = new[] { fromBodyId, toBodyId }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
                Category = HistoricalEventCategory.BodyTransition,
                Visibility = KnowledgeVisibility.Private,
                SourceSystem = "BodyTransition",
                Provenance = string.IsNullOrWhiteSpace(reason) ? "Body transition recorded." : reason,
                Payload = new HistoricalEventPayloadData
                {
                    kind = HistoricalEventPayloadKind.BodyTransition,
                    fromBodyId = fromBodyId,
                    toBodyId = toBodyId,
                    note = reason
                },
                Tags = new[] { "body-transition", "identity-continuity" }
            };
            HistoryOperationResult result = RecordEvent(request, preview, restoring);
            if (!result.Succeeded || preview)
            {
                return result;
            }

            CloseOpenOccupation(personId, fromBodyId, occurredAtWorldTime, eventId);
            AddOccupation(new BodyOccupationRecordData
            {
                occupationId = $"occupation.{personId}.{toBodyId}.{eventId}",
                personId = personId,
                bodyId = toBodyId,
                startedAtWorldTime = occurredAtWorldTime,
                endedAtWorldTime = -1d,
                startEventId = eventId,
                reason = reason
            });
            return result;
        }

        public HistoryOperationResult PreviewRecordLifeEvent(RecordLifeEventRequest request)
        {
            return RecordLifeEvent(request, preview: true, restoring: false);
        }

        public HistoryOperationResult RecordLifeEvent(RecordLifeEventRequest request, bool preview = false, bool restoring = false)
        {
            string transactionKey = TransactionKey(request?.TransactionId);
            if (!preview
                && request != null
                && !string.IsNullOrWhiteSpace(request.TransactionId)
                && processedTransactions.Contains(transactionKey))
            {
                HistoricalEventRecord existing = TryGetEvent(request.EventId, out HistoricalEventRecord duplicateRecord)
                    ? duplicateRecord
                    : null;
                return HistoryOperationResult.Success(
                    "Life event transaction already processed.",
                    request.TransactionId,
                    existing,
                    null,
                    null,
                    HistoryRevision,
                    HistoryRevision,
                    duplicate: true);
            }

            if (!ValidateLifeEventRequest(request, out HistoricalEventDefinition definition, out string failure, out HistoryResultCode code))
            {
                return HistoryOperationResult.Failure(code, failure, request?.TransactionId, preview, HistoryRevision);
            }

            RecordHistoricalEventRequest eventRequest = BuildHistoricalEventRequest(request, definition);
            HistoryOperationResult result = RecordEvent(eventRequest, preview, restoring);
            if (!result.Succeeded || preview)
            {
                return result;
            }

            if (!string.IsNullOrWhiteSpace(request.SequenceId))
            {
                AddOrUpdateLifeEventSequence(request, result.Event);
            }

            return result;
        }

        public HistoryOperationResult RecordBirthOrCreation(string transactionId, string eventId, string personId, string bodyId, double worldTime, string methodId, bool preview = false)
        {
            return RecordLifeEvent(new RecordLifeEventRequest
            {
                TransactionId = transactionId,
                EventId = eventId,
                EventDefinitionId = "history-event.life.birth",
                Category = LifeEventCategory.BirthOrCreation,
                PayloadKind = LifeEventPayloadKind.BirthOrCreation,
                OccurredAtWorldTime = worldTime,
                RecordedAtWorldTime = worldTime,
                PrimaryPersonId = personId,
                BodyIds = new[] { bodyId },
                Participants = new[] { Participant(personId, LifeEventParticipantRole.Subject, bodyId) },
                Visibility = KnowledgeVisibility.Private,
                Significance = LifeEventSignificance.LifeDefining,
                BiographyRelevance = LifeEventBiographyRelevance.IdentityDefining,
                PublicRecordRelevance = LifeEventPublicRecordRelevance.PersonalOnly,
                Outcome = LifeEventOutcome.Confirmed,
                SourceSystem = "LifeEvent",
                Provenance = "Birth or creation recorded.",
                LifeEventPayload = new LifeEventPayloadData { kind = LifeEventPayloadKind.BirthOrCreation, createdPersonId = personId, subjectPersonId = personId, methodId = methodId },
                HistoricalPayload = new HistoricalEventPayloadData { kind = HistoricalEventPayloadKind.Generic, note = methodId },
                Tags = new[] { "life-event", "birth-or-creation" }
            }, preview);
        }

        public HistoryOperationResult RecordDeathOrDisappearance(string transactionId, string eventId, string personId, string bodyId, double worldTime, bool presumed, string causeId, bool preview = false)
        {
            return RecordLifeEvent(new RecordLifeEventRequest
            {
                TransactionId = transactionId,
                EventId = eventId,
                EventDefinitionId = presumed ? "history-event.life.presumed-death" : "history-event.life.death",
                Category = presumed ? LifeEventCategory.Disappearance : LifeEventCategory.Death,
                PayloadKind = LifeEventPayloadKind.DeathOrDisappearance,
                OccurredAtWorldTime = worldTime,
                RecordedAtWorldTime = worldTime,
                PrimaryPersonId = personId,
                BodyIds = new[] { bodyId },
                Participants = new[] { Participant(personId, LifeEventParticipantRole.Subject, bodyId) },
                Visibility = KnowledgeVisibility.Private,
                Significance = LifeEventSignificance.LifeDefining,
                BiographyRelevance = presumed ? LifeEventBiographyRelevance.RestrictedBiographyEvent : LifeEventBiographyRelevance.MajorBiographyEvent,
                PublicRecordRelevance = presumed ? LifeEventPublicRecordRelevance.OrganizationRecord : LifeEventPublicRecordRelevance.PublicRecord,
                Outcome = presumed ? LifeEventOutcome.Presumed : LifeEventOutcome.Confirmed,
                SourceSystem = "LifeEvent",
                Provenance = presumed ? "Presumed death or disappearance recorded." : "Death recorded.",
                LifeEventPayload = new LifeEventPayloadData { kind = LifeEventPayloadKind.DeathOrDisappearance, subjectPersonId = personId, causeId = causeId },
                HistoricalPayload = new HistoricalEventPayloadData { kind = HistoricalEventPayloadKind.Generic, note = causeId },
                Tags = new[] { "life-event", presumed ? "presumed-death" : "death" }
            }, preview);
        }

        public HistoryOperationResult RecordDiscoveryLifeEvent(string transactionId, string eventId, string discovererPersonId, string subjectId, double worldTime, string evidenceId, bool publicRecord, bool preview = false)
        {
            return RecordLifeEvent(new RecordLifeEventRequest
            {
                TransactionId = transactionId,
                EventId = eventId,
                EventDefinitionId = "history-event.life.discovery",
                Category = LifeEventCategory.Discovery,
                PayloadKind = LifeEventPayloadKind.Discovery,
                OccurredAtWorldTime = worldTime,
                RecordedAtWorldTime = worldTime,
                PrimaryPersonId = discovererPersonId,
                Participants = new[] { Participant(discovererPersonId, LifeEventParticipantRole.Discoverer) },
                RelatedEntityIds = new[] { subjectId, evidenceId },
                Visibility = publicRecord ? KnowledgeVisibility.Public : KnowledgeVisibility.Private,
                Significance = LifeEventSignificance.Notable,
                BiographyRelevance = publicRecord ? LifeEventBiographyRelevance.PublicBiographyEvent : LifeEventBiographyRelevance.Optional,
                PublicRecordRelevance = publicRecord ? LifeEventPublicRecordRelevance.PublicRecord : LifeEventPublicRecordRelevance.PersonalOnly,
                Outcome = LifeEventOutcome.Confirmed,
                SourceSystem = "LifeEvent",
                Provenance = "Discovery recorded.",
                LifeEventPayload = new LifeEventPayloadData { kind = LifeEventPayloadKind.Discovery, subjectPersonId = discovererPersonId, evidenceId = evidenceId, methodId = "observation" },
                HistoricalPayload = new HistoricalEventPayloadData { kind = HistoricalEventPayloadKind.Discovery, claimValueId = subjectId, note = evidenceId },
                Tags = new[] { "life-event", "discovery" }
            }, preview);
        }

        public bool TryGetEvent(string eventId, out HistoricalEventRecord record)
        {
            if (eventsById.TryGetValue(eventId ?? string.Empty, out HistoricalEventRecordData data))
            {
                record = Wrap(data);
                return true;
            }

            record = null;
            return false;
        }

        public bool TryGetAcceptedEvent(string eventId, out HistoricalEventRecord record)
        {
            record = null;
            if (!eventsById.TryGetValue(eventId ?? string.Empty, out HistoricalEventRecordData data))
            {
                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(data.correctedByEventId) && seen.Add(data.eventId) && eventsById.TryGetValue(data.correctedByEventId, out HistoricalEventRecordData corrected))
            {
                data = corrected;
            }

            record = Wrap(data);
            return true;
        }

        public IReadOnlyList<HistoricalEventRecord> QueryByPerson(string personId)
        {
            return RecordsFromIndex(eventIdsByPerson, personId);
        }

        public IReadOnlyList<HistoricalEventRecord> QueryByBody(string bodyId)
        {
            return RecordsFromIndex(eventIdsByBody, bodyId);
        }

        public IReadOnlyList<HistoricalEventRecord> QueryByLocation(string locationId)
        {
            return RecordsFromIndex(eventIdsByLocation, locationId);
        }

        public IReadOnlyList<HistoricalEventRecord> QueryByOrganization(string organizationId)
        {
            return RecordsFromIndex(eventIdsByOrganization, organizationId);
        }

        public IReadOnlyList<HistoricalEventRecord> QueryByTag(string tag)
        {
            return RecordsFromIndex(eventIdsByTag, tag);
        }

        public IReadOnlyList<HistoricalEventRecord> QueryByTimeRange(double inclusiveStart, double inclusiveEnd)
        {
            if (inclusiveEnd < inclusiveStart)
            {
                return Array.Empty<HistoricalEventRecord>();
            }

            return eventsById.Values
                .Where(data => data.occurredAtWorldTime >= inclusiveStart && data.occurredAtWorldTime <= inclusiveEnd)
                .Select(Wrap)
                .OrderBy(HistoryOrdering.Key)
                .ToArray();
        }

        public IReadOnlyList<HistoricalEventRecord> QueryByCategory(HistoricalEventCategory category)
        {
            return eventsById.Values
                .Where(data => data.category == category)
                .Select(Wrap)
                .OrderBy(HistoryOrdering.Key)
                .ToArray();
        }

        public IReadOnlyList<HistoricalEventRecord> QueryPersonAccessible(string personId, PersonMemoryRuntime memoryRuntime = null, bool privileged = false)
        {
            if (privileged)
            {
                return CreateSnapshot().Events;
            }

            HashSet<string> remembered = new HashSet<string>((memoryRuntime?.CreateSnapshot().AccessibleMemories ?? Array.Empty<HistoryMemoryRecord>()).Select(memory => memory.HistoricalEventId), StringComparer.Ordinal);
            return eventsById.Values
                .Where(data => IsVisibleToPerson(data, personId, remembered))
                .Select(Wrap)
                .OrderBy(HistoryOrdering.Key)
                .ToArray();
        }

        public IReadOnlyList<LifeEventRecord> QueryLifeEventsForPerson(string personId, PersonMemoryRuntime memoryRuntime = null, bool privileged = true)
        {
            IEnumerable<HistoricalEventRecord> source = privileged
                ? QueryByPerson(personId)
                : QueryPersonAccessible(personId, memoryRuntime, privileged: false);
            return source
                .Where(record => record.IsLifeEvent)
                .Select(record => new LifeEventRecord(record))
                .OrderBy(record => HistoryOrdering.Key(record.Event))
                .ToArray();
        }

        public IReadOnlyList<LifeEventRecord> QueryLifeEventsByRole(string personId, LifeEventParticipantRole role)
        {
            return QueryLifeEventsForPerson(personId)
                .Where(record => record.Participants.Any(participant => string.Equals(participant.personId, personId, StringComparison.Ordinal) && participant.role == role))
                .ToArray();
        }

        public IReadOnlyList<LifeEventRecord> QueryLifeEventsByCategory(LifeEventCategory category)
        {
            return eventsById.Values
                .Where(data => data.isLifeEvent && data.lifeEventCategory == category)
                .Select(data => new LifeEventRecord(Wrap(data)))
                .OrderBy(record => HistoryOrdering.Key(record.Event))
                .ToArray();
        }

        public IReadOnlyList<LifeEventRecord> QueryLifeEventsByDefinition(string definitionId)
        {
            return eventsById.Values
                .Where(data => data.isLifeEvent && string.Equals(data.eventDefinitionId, definitionId, StringComparison.Ordinal))
                .Select(data => new LifeEventRecord(Wrap(data)))
                .OrderBy(record => HistoryOrdering.Key(record.Event))
                .ToArray();
        }

        public IReadOnlyList<LifeEventRecord> QueryLifeEventsBySignificance(LifeEventSignificance minimumSignificance)
        {
            return eventsById.Values
                .Where(data => data.isLifeEvent && data.significance >= minimumSignificance)
                .Select(data => new LifeEventRecord(Wrap(data)))
                .OrderBy(record => HistoryOrdering.Key(record.Event))
                .ToArray();
        }

        public IReadOnlyList<LifeEventRecord> QueryLifeEventsByBiographyRelevance(LifeEventBiographyRelevance minimumRelevance)
        {
            return eventsById.Values
                .Where(data => data.isLifeEvent && data.biographyRelevance >= minimumRelevance)
                .Select(data => new LifeEventRecord(Wrap(data)))
                .OrderBy(record => HistoryOrdering.Key(record.Event))
                .ToArray();
        }

        public IReadOnlyList<LifeEventRecord> QueryLifeEventsByRelatedId(string relatedId)
        {
            if (string.IsNullOrWhiteSpace(relatedId))
            {
                return Array.Empty<LifeEventRecord>();
            }

            return eventsById.Values
                .Where(data => data.isLifeEvent && LifeEventReferences(data, relatedId))
                .Select(data => new LifeEventRecord(Wrap(data)))
                .OrderBy(record => HistoryOrdering.Key(record.Event))
                .ToArray();
        }

        public IReadOnlyList<LifeEventRecord> QueryRelatedLifeEvents(string eventId, LifeEventRelationshipType? relationshipType = null)
        {
            if (!eventsById.TryGetValue(eventId ?? string.Empty, out HistoricalEventRecordData data))
            {
                return Array.Empty<LifeEventRecord>();
            }

            return (data.lifeEventRelationships ?? Array.Empty<LifeEventRelationshipData>())
                .Where(relationship => !relationshipType.HasValue || relationship.relationshipType == relationshipType.Value)
                .Where(relationship => eventsById.ContainsKey(relationship.targetEventId ?? string.Empty))
                .Select(relationship => new LifeEventRecord(Wrap(eventsById[relationship.targetEventId])))
                .OrderBy(record => HistoryOrdering.Key(record.Event))
                .ToArray();
        }

        public LifeEventRecord QueryEarliestLifeEvent(string personId, LifeEventCategory category)
        {
            return QueryLifeEventsForPerson(personId).FirstOrDefault(record => record.Category == category);
        }

        public LifeEventRecord QueryMostRecentLifeEvent(string personId, LifeEventCategory category)
        {
            return QueryLifeEventsForPerson(personId).LastOrDefault(record => record.Category == category);
        }

        public IReadOnlyList<BiographyTimelineEntry> QueryBiography(string personId, PersonMemoryRuntime memoryRuntime = null, bool publicOnly = false, bool personKnown = false, bool personRemembered = false, bool privileged = false)
        {
            HashSet<string> remembered = new HashSet<string>((memoryRuntime?.CreateSnapshot().AccessibleMemories ?? Array.Empty<HistoryMemoryRecord>()).Select(memory => memory.HistoricalEventId), StringComparer.Ordinal);
            IEnumerable<LifeEventRecord> source = privileged
                ? QueryLifeEventsForPerson(personId)
                : QueryLifeEventsForPerson(personId, memoryRuntime, privileged: false);
            if (publicOnly)
            {
                source = source.Where(record => record.Visibility == KnowledgeVisibility.Public || record.PublicRecordRelevance >= LifeEventPublicRecordRelevance.PublicRecord);
            }

            if (personKnown)
            {
                source = source.Where(record => IsVisibleToPerson(record.Event.Data, personId, remembered));
            }

            if (personRemembered)
            {
                source = source.Where(record => remembered.Contains(record.EventId));
            }

            return source
                .Where(record => record.BiographyRelevance != LifeEventBiographyRelevance.NotRelevant)
                .Select(record => new BiographyTimelineEntry(record, RoleFor(record, personId), known: IsVisibleToPerson(record.Event.Data, personId, remembered), remembered: remembered.Contains(record.EventId)))
                .OrderBy(entry => HistoryOrdering.Key(entry.LifeEvent.Event))
                .ToArray();
        }

        public InformationAccessProjection<HistoricalEventRecord> GetHistoryProjection(string eventId, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId = "", bool recordAudit = false)
        {
            if (!TryGetEvent(eventId, out HistoricalEventRecord record))
            {
                return new InformationAccessProjection<HistoricalEventRecord>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, $"Historical event '{eventId}' was not found.");
            }

            return ProjectHistoricalEvent(record, accessRuntime, accessContext, policyId, recordAudit);
        }

        public IReadOnlyList<InformationAccessProjection<HistoricalEventRecord>> QueryHistoryProjectionsByPerson(string personId, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, PersonMemoryRuntime memoryRuntime = null, bool publicOnly = false, string policyId = "")
        {
            IEnumerable<HistoricalEventRecord> source = publicOnly
                ? QueryPersonAccessible(personId, memoryRuntime, privileged: false)
                : QueryByPerson(personId);
            return source
                .Select(record => ProjectHistoricalEvent(record, accessRuntime, accessContext, policyId, recordAudit: false))
                .Where(projection => projection.Succeeded)
                .OrderBy(projection => projection.Record == null ? double.MaxValue : projection.Record.OccurredAtWorldTime)
                .ThenBy(projection => projection.Record == null ? long.MaxValue : projection.Record.Sequence)
                .ThenBy(projection => projection.Record == null ? string.Empty : projection.Record.EventId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<InformationAccessProjection<BiographyTimelineEntry>> GetBiographyProjection(string personId, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, PersonMemoryRuntime memoryRuntime = null, bool publicOnly = false, bool personKnown = false, bool personRemembered = false, string policyId = "")
        {
            return QueryBiography(personId, memoryRuntime, publicOnly, personKnown, personRemembered, privileged: true)
                .Select(entry => ProjectBiographyEntry(entry, accessRuntime, accessContext, policyId))
                .Where(projection => projection.Succeeded)
                .OrderBy(projection => projection.Record == null ? double.MaxValue : projection.Record.LifeEvent.OccurredAtWorldTime)
                .ThenBy(projection => projection.Record == null ? string.Empty : projection.Record.LifeEvent.EventId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<LifeEventRecord> QueryMajorLifeMilestones(string personId)
        {
            return QueryLifeEventsForPerson(personId)
                .Where(record => record.Significance >= LifeEventSignificance.Major || record.BiographyRelevance >= LifeEventBiographyRelevance.MajorBiographyEvent)
                .ToArray();
        }

        public bool TryGetLifeEventSequence(string sequenceId, out LifeEventSequenceRecord sequence)
        {
            RebuildMissingLifeEventSequencesFromEvents();
            sequence = null;
            if (!lifeEventSequencesById.TryGetValue(sequenceId ?? string.Empty, out LifeEventSequenceData data))
            {
                return false;
            }

            IReadOnlyList<LifeEventRecord> events = (data.eventIds ?? Array.Empty<string>())
                .Where(id => eventsById.ContainsKey(id))
                .Select(id => new LifeEventRecord(Wrap(eventsById[id])))
                .ToArray();
            sequence = new LifeEventSequenceRecord(data, events);
            return true;
        }

        public IReadOnlyList<BodyOccupationRecord> QueryBodyOccupations(string personId)
        {
            return OccupationsFromIndex(personId);
        }

        public HistorySnapshot CreateSnapshot()
        {
            return new HistorySnapshot(WorldId, HistoryRevision, eventsById.Values.Select(Wrap).ToArray(), occupationsById.Values.Select(data => new BodyOccupationRecord(data)).ToArray());
        }

        public AuthoritativeHistorySaveData CreateSaveData()
        {
            RebuildMissingLifeEventSequencesFromEvents();
            return new AuthoritativeHistorySaveData
            {
                schemaVersion = AuthoritativeHistorySaveData.CurrentSchemaVersion,
                worldId = WorldId,
                nextSequence = NextSequence,
                historyRevision = HistoryRevision,
                events = eventsById.Values.OrderBy(data => data.occurredAtWorldTime).ThenBy(data => data.sequence).ThenBy(data => data.eventId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                bodyOccupations = occupationsById.Values.OrderBy(data => data.startedAtWorldTime).ThenBy(data => data.occupationId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                lifeEventSequences = lifeEventSequencesById.Values.OrderBy(data => data.sequenceId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                processedTransactions = processedTransactions.OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
        }

        public HistoryOperationResult RestoreFromSaveData(AuthoritativeHistorySaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> knownPersons, IEnumerable<string> knownBodies, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, knownPersons, knownBodies, out string failureReason))
            {
                return HistoryOperationResult.Failure(HistoryResultCode.RestoreFailed, failureReason, revision: HistoryRevision);
            }

            AuthoritativeHistorySaveData rollback = CreateSaveData();
            try
            {
                suppressEvents = restoring;
                Configure(definitionRegistry, saveData.worldId, knownPersons, knownBodies);
                eventsById.Clear();
                ClearIndexes();
                occupationsById.Clear();
                occupationIdsByPerson.Clear();
                lifeEventSequencesById.Clear();
                processedTransactions.Clear();

                foreach (HistoricalEventRecordData data in saveData.events ?? Array.Empty<HistoricalEventRecordData>())
                {
                    HistoricalEventRecordData clone = data.Clone();
                    eventsById[clone.eventId] = clone;
                    AddIndexes(clone);
                }

                foreach (BodyOccupationRecordData data in saveData.bodyOccupations ?? Array.Empty<BodyOccupationRecordData>())
                {
                    AddOccupation(data.Clone());
                }

                foreach (LifeEventSequenceData data in saveData.lifeEventSequences ?? Array.Empty<LifeEventSequenceData>())
                {
                    lifeEventSequencesById[data.sequenceId] = data.Clone();
                }

                RebuildMissingLifeEventSequencesFromEvents();

                foreach (string transaction in saveData.processedTransactions ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(transaction))
                    {
                        processedTransactions.Add(transaction);
                    }
                }

                NextSequence = Math.Max(1L, saveData.nextSequence);
                HistoryRevision = Math.Max(0L, saveData.historyRevision);
                return HistoryOperationResult.Success("Authoritative history restored.", string.Empty, null, null, null, HistoryRevision, HistoryRevision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, registry, knownPersonIds, knownBodyIds, restoring: true);
                return HistoryOperationResult.Failure(HistoryResultCode.RestoreFailed, exception.Message, revision: HistoryRevision);
            }
            finally
            {
                suppressEvents = false;
            }
        }

        public static bool ValidateSaveData(AuthoritativeHistorySaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> knownPersons, IEnumerable<string> knownBodies, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Authoritative history save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != AuthoritativeHistorySaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Authoritative History schema version {saveData.schemaVersion}.";
                return false;
            }

            AuthoritativeHistoryRuntime validator = new AuthoritativeHistoryRuntime();
            validator.Configure(definitionRegistry, saveData.worldId, knownPersons, knownBodies);
            foreach (HistoricalEventRecordData eventData in saveData.events ?? Array.Empty<HistoricalEventRecordData>())
            {
                RecordHistoricalEventRequest request = RequestFromData(eventData);
                request.SupersedesEventId = string.Empty;
                if (!validator.ValidateRecordRequest(request, out _, out failureReason, out _))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(eventData.correctedByEventId) && !saveData.events.Any(data => string.Equals(data.eventId, eventData.correctedByEventId, StringComparison.Ordinal)))
                {
                    failureReason = $"Historical event '{eventData.eventId}' references missing correctedBy event '{eventData.correctedByEventId}'.";
                    return false;
                }

                validator.eventsById[eventData.eventId] = eventData.Clone();
            }

            foreach (LifeEventSequenceData sequenceData in saveData.lifeEventSequences ?? Array.Empty<LifeEventSequenceData>())
            {
                if (!string.IsNullOrWhiteSpace(sequenceData?.sequenceId))
                {
                    validator.lifeEventSequencesById[sequenceData.sequenceId] = sequenceData.Clone();
                }
            }

            validator.RebuildMissingLifeEventSequencesFromEvents();

            foreach (HistoricalEventRecordData eventData in saveData.events ?? Array.Empty<HistoricalEventRecordData>())
            {
                if (!validator.ValidateSavedCorrectionLink(eventData, out failureReason))
                {
                    return false;
                }

                if (!validator.ValidateSavedLifeEvent(eventData, out failureReason))
                {
                    return false;
                }
            }

            HashSet<string> occupationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BodyOccupationRecordData occupation in saveData.bodyOccupations ?? Array.Empty<BodyOccupationRecordData>())
            {
                if (!ValidateOccupation(occupation, validator.knownPersonIds, validator.knownBodyIds, out failureReason) || !occupationIds.Add(occupation.occupationId ?? string.Empty))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? $"Missing or duplicate body occupation ID '{occupation?.occupationId}'." : failureReason;
                    return false;
                }
            }

            HashSet<string> sequenceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LifeEventSequenceData sequence in saveData.lifeEventSequences ?? Array.Empty<LifeEventSequenceData>())
            {
                if (!ValidateLifeEventSequence(sequence, validator.eventsById, validator.knownPersonIds, out failureReason) || !sequenceIds.Add(sequence.sequenceId ?? string.Empty))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? $"Missing or duplicate life-event sequence ID '{sequence?.sequenceId}'." : failureReason;
                    return false;
                }
            }

            return true;
        }

        private bool ValidateSavedCorrectionLink(HistoricalEventRecordData eventData, out string failure)
        {
            failure = string.Empty;
            if (eventData == null)
            {
                failure = "Historical event save entry is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(eventData.correctedByEventId))
            {
                if (!eventsById.TryGetValue(eventData.correctedByEventId, out HistoricalEventRecordData correction))
                {
                    failure = $"Historical event '{eventData.eventId}' references missing correctedBy event '{eventData.correctedByEventId}'.";
                    return false;
                }

                if (!string.Equals(correction.supersedesEventId, eventData.eventId, StringComparison.Ordinal))
                {
                    failure = $"Historical event '{eventData.eventId}' correctedBy event '{eventData.correctedByEventId}' does not supersede it.";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(eventData.supersedesEventId))
            {
                return true;
            }

            if (string.Equals(eventData.eventId, eventData.supersedesEventId, StringComparison.Ordinal))
            {
                failure = "Historical event cannot supersede itself.";
                return false;
            }

            if (!eventsById.TryGetValue(eventData.supersedesEventId, out HistoricalEventRecordData target))
            {
                failure = $"Superseded historical event '{eventData.supersedesEventId}' is missing.";
                return false;
            }

            if (!string.Equals(target.correctedByEventId, eventData.eventId, StringComparison.Ordinal))
            {
                failure = $"Superseded historical event '{eventData.supersedesEventId}' does not point back to correction '{eventData.eventId}'.";
                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { eventData.supersedesEventId };
            string nextId = eventData.correctedByEventId;
            while (!string.IsNullOrWhiteSpace(nextId))
            {
                if (!seen.Add(nextId))
                {
                    failure = $"Historical event correction chain for '{eventData.supersedesEventId}' is circular.";
                    return false;
                }

                if (!eventsById.TryGetValue(nextId, out HistoricalEventRecordData next))
                {
                    failure = $"Historical event correction chain references missing event '{nextId}'.";
                    return false;
                }

                nextId = next.correctedByEventId;
            }

            return true;
        }

        private bool ValidateRecordRequest(RecordHistoricalEventRequest request, out HistoricalEventDefinition definition, out string failure, out HistoryResultCode code)
        {
            definition = null;
            failure = string.Empty;
            code = HistoryResultCode.InvalidRequest;
            if (request == null)
            {
                failure = "Historical event request is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.TransactionId) || string.IsNullOrWhiteSpace(request.EventId))
            {
                failure = "Historical event requires transaction and event IDs.";
                return false;
            }

            if (eventsById.ContainsKey(request.EventId))
            {
                failure = $"Historical event '{request.EventId}' already exists.";
                return false;
            }

            if (registry == null || !registry.TryGet(request.EventDefinitionId, out definition))
            {
                code = HistoryResultCode.MissingDefinition;
                failure = $"Historical event definition '{request.EventDefinitionId}' is missing.";
                return false;
            }

            if (double.IsNaN(request.OccurredAtWorldTime) || double.IsInfinity(request.OccurredAtWorldTime) || request.RecordedAtWorldTime < request.OccurredAtWorldTime)
            {
                code = HistoryResultCode.InvalidTimeRange;
                failure = "Historical event has invalid occurrence or recorded time.";
                return false;
            }

            if (!ValidateKnownPerson(request.PrimaryPersonId, required: false, out failure))
            {
                code = HistoryResultCode.MissingPerson;
                return false;
            }

            foreach (string person in Distinct(request.ParticipantPersonIds))
            {
                if (!ValidateKnownPerson(person, required: true, out failure))
                {
                    code = HistoryResultCode.MissingPerson;
                    return false;
                }
            }

            foreach (string body in Distinct(request.BodyIds))
            {
                if (!ValidateKnownBody(body, out failure))
                {
                    code = HistoryResultCode.MissingBody;
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(request.SupersedesEventId) && !CanSupersede(request.EventId, request.SupersedesEventId, out failure))
            {
                code = failure.Contains("circular", StringComparison.OrdinalIgnoreCase) ? HistoryResultCode.CircularCorrection : HistoryResultCode.InvalidCorrection;
                return false;
            }

            return true;
        }

        private static RecordHistoricalEventRequest RequestFromData(HistoricalEventRecordData data)
        {
            return new RecordHistoricalEventRequest
            {
                TransactionId = $"restore.{data?.eventId}",
                EventId = data?.eventId,
                EventDefinitionId = data?.eventDefinitionId,
                OccurredAtWorldTime = data?.occurredAtWorldTime ?? 0d,
                RecordedAtWorldTime = data?.recordedAtWorldTime ?? 0d,
                Sequence = data?.sequence,
                PrimaryPersonId = data?.primaryPersonId,
                ParticipantPersonIds = data?.participantPersonIds,
                BodyIds = data?.bodyIds,
                LocationId = data?.locationId,
                OrganizationId = data?.organizationId,
                RelatedEntityIds = data?.relatedEntityIds,
                Category = data?.category,
                Visibility = data?.visibility,
                SourceSystem = data?.sourceSystem,
                Provenance = data?.provenance,
                SupersedesEventId = data?.supersedesEventId,
                CorrelationId = data?.correlationId,
                Tags = data?.tags,
                Payload = data?.payload
            };
        }

        private HistoricalEventRecordData CreateEventData(RecordHistoricalEventRequest request, HistoricalEventDefinition definition)
        {
            string[] persons = Distinct((request.ParticipantPersonIds ?? Array.Empty<string>()).Concat(new[] { request.PrimaryPersonId })).ToArray();
            HistoricalEventCategory category = request.Category ?? definition.Category;
            KnowledgeVisibility visibility = request.Visibility ?? definition.DefaultVisibility;
            long sequence = request.Sequence ?? NextSequence++;
            HistoricalEventRecordData data = new HistoricalEventRecordData
            {
                eventId = request.EventId,
                eventDefinitionId = definition.Id,
                occurredAtWorldTime = request.OccurredAtWorldTime,
                recordedAtWorldTime = request.RecordedAtWorldTime,
                sequence = sequence,
                primaryPersonId = request.PrimaryPersonId,
                participantPersonIds = persons,
                bodyIds = Distinct(request.BodyIds).ToArray(),
                locationId = request.LocationId,
                organizationId = request.OrganizationId,
                relatedEntityIds = Distinct(request.RelatedEntityIds).ToArray(),
                category = category,
                visibility = visibility,
                status = string.IsNullOrWhiteSpace(request.SupersedesEventId) ? HistoricalEventStatus.Active : HistoricalEventStatus.Correction,
                sourceSystem = request.SourceSystem,
                provenance = request.Provenance,
                supersedesEventId = request.SupersedesEventId,
                correlationId = request.CorrelationId,
                tags = Distinct((request.Tags ?? Array.Empty<string>()).Concat(definition.Tags)).ToArray(),
                payload = request.Payload?.Clone() ?? new HistoricalEventPayloadData { kind = definition.PayloadKind }
            };

            if (request is LifeEventHistoricalEventRequest lifeRequest)
            {
                data.isLifeEvent = true;
                data.lifeEventCategory = lifeRequest.LifeEventCategory;
                data.significance = lifeRequest.Significance;
                data.biographyRelevance = lifeRequest.BiographyRelevance;
                data.publicRecordRelevance = lifeRequest.PublicRecordRelevance;
                data.lifeEventOutcome = lifeRequest.LifeEventOutcome;
                data.lifeEventParticipants = lifeRequest.LifeEventParticipants == null ? Array.Empty<LifeEventParticipantData>() : lifeRequest.LifeEventParticipants.Select(participant => participant?.Clone()).Where(participant => participant != null).ToArray();
                data.lifeEventRelationships = lifeRequest.LifeEventRelationships == null ? Array.Empty<LifeEventRelationshipData>() : lifeRequest.LifeEventRelationships.Select(relationship => relationship?.Clone()).Where(relationship => relationship != null).ToArray();
                data.lifeEventSequenceId = lifeRequest.LifeEventSequenceId;
                data.lifeEventSequenceOrder = lifeRequest.LifeEventSequenceOrder;
                data.relatedRoleId = lifeRequest.RelatedRoleId;
                data.relatedTitleId = lifeRequest.RelatedTitleId;
                data.relatedSocialStatusId = lifeRequest.RelatedSocialStatusId;
                data.relatedConditionId = lifeRequest.RelatedConditionId;
                data.relatedInjuryId = lifeRequest.RelatedInjuryId;
                data.relatedDiseaseId = lifeRequest.RelatedDiseaseId;
                data.relatedTreatmentId = lifeRequest.RelatedTreatmentId;
                data.relatedCombatEncounterId = lifeRequest.RelatedCombatEncounterId;
                data.relatedQuestId = lifeRequest.RelatedQuestId;
                data.relatedLegalRecordId = lifeRequest.RelatedLegalRecordId;
                data.relatedRelationshipId = lifeRequest.RelatedRelationshipId;
                data.lifeEventPayload = lifeRequest.LifeEventPayload?.Clone() ?? new LifeEventPayloadData { kind = definition.LifeEventPayloadKind };
            }

            return data;
        }

        private bool ValidateLifeEventRequest(RecordLifeEventRequest request, out HistoricalEventDefinition definition, out string failure, out HistoryResultCode code)
        {
            definition = null;
            failure = string.Empty;
            code = HistoryResultCode.InvalidLifeEvent;
            if (request == null)
            {
                failure = "Life-event request is missing.";
                return false;
            }

            if (registry == null || !registry.TryGet(request.EventDefinitionId, out definition))
            {
                code = HistoryResultCode.MissingDefinition;
                failure = $"Life-event definition '{request.EventDefinitionId}' is missing.";
                return false;
            }

            LifeEventCategory category = request.Category ?? definition.LifeEventCategory;
            if (category == LifeEventCategory.None || !Enum.IsDefined(typeof(LifeEventCategory), category))
            {
                failure = $"Life event '{request.EventId}' has an invalid category.";
                return false;
            }

            if (!definition.IsLifeEventDefinition && request.Category == null)
            {
                failure = $"Historical event definition '{definition.Id}' is not marked as a life-event definition.";
                return false;
            }

            if (!ValidateLifeEventVisibility(request.Visibility ?? definition.DefaultVisibility, definition, out failure))
            {
                return false;
            }

            LifeEventParticipantData[] participants = NormalizeLifeEventParticipants(request);
            if (participants.Length == 0)
            {
                failure = $"Life event '{request.EventId}' must declare at least one participant role.";
                return false;
            }

            foreach (LifeEventParticipantData participant in participants)
            {
                if (participant.role == LifeEventParticipantRole.Unknown || !Enum.IsDefined(typeof(LifeEventParticipantRole), participant.role))
                {
                    code = HistoryResultCode.InvalidParticipantRole;
                    failure = $"Life event '{request.EventId}' has an invalid participant role.";
                    return false;
                }

                if (!ValidateKnownPerson(participant.personId, required: true, out failure))
                {
                    code = HistoryResultCode.MissingPerson;
                    return false;
                }

                if (!ValidateKnownBody(participant.bodyId, out failure))
                {
                    code = HistoryResultCode.MissingBody;
                    return false;
                }
            }

            foreach (LifeEventParticipantRole role in definition.RequiredParticipantRoles)
            {
                if (!participants.Any(participant => participant.role == role))
                {
                    code = HistoryResultCode.InvalidParticipantRole;
                    failure = $"Life event '{request.EventId}' is missing required role '{role}' for definition '{definition.Id}'.";
                    return false;
                }
            }

            if (participants.Count(participant => participant.role == LifeEventParticipantRole.Subject) > 1)
            {
                code = HistoryResultCode.InvalidParticipantRole;
                failure = $"Life event '{request.EventId}' has more than one primary Subject role.";
                return false;
            }

            foreach (LifeEventRelationshipData relationship in request.Relationships ?? Array.Empty<LifeEventRelationshipData>())
            {
                if (!ValidateLifeEventRelationship(request.EventId, relationship, out failure))
                {
                    code = HistoryResultCode.InvalidRelationship;
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(request.SequenceId) && !ValidateLifeEventSequenceMembership(request, out failure))
            {
                code = HistoryResultCode.InvalidSequence;
                return false;
            }

            RecordHistoricalEventRequest eventRequest = BuildHistoricalEventRequest(request, definition);
            return ValidateRecordRequest(eventRequest, out _, out failure, out code);
        }

        private LifeEventHistoricalEventRequest BuildHistoricalEventRequest(RecordLifeEventRequest request, HistoricalEventDefinition definition)
        {
            LifeEventParticipantData[] participants = NormalizeLifeEventParticipants(request);
            return new LifeEventHistoricalEventRequest
            {
                TransactionId = request.TransactionId,
                EventId = request.EventId,
                EventDefinitionId = request.EventDefinitionId,
                OccurredAtWorldTime = request.OccurredAtWorldTime,
                RecordedAtWorldTime = request.RecordedAtWorldTime,
                Sequence = request.Sequence,
                PrimaryPersonId = string.IsNullOrWhiteSpace(request.PrimaryPersonId) ? participants.FirstOrDefault(participant => participant.role == LifeEventParticipantRole.Subject)?.personId : request.PrimaryPersonId,
                ParticipantPersonIds = participants.Select(participant => participant.personId).ToArray(),
                BodyIds = Distinct((request.BodyIds ?? Array.Empty<string>()).Concat(participants.Select(participant => participant.bodyId))).ToArray(),
                LocationId = request.LocationId,
                OrganizationId = request.OrganizationId,
                RelatedEntityIds = request.RelatedEntityIds,
                Category = MapLifeEventCategory(request.Category ?? definition.LifeEventCategory),
                Visibility = request.Visibility,
                SourceSystem = request.SourceSystem,
                Provenance = request.Provenance,
                SupersedesEventId = request.SupersedesEventId,
                CorrelationId = request.CorrelationId,
                Tags = Distinct((request.Tags ?? Array.Empty<string>()).Concat(new[] { "life-event", (request.Category ?? definition.LifeEventCategory).ToString() })).ToArray(),
                Payload = request.HistoricalPayload ?? new HistoricalEventPayloadData { kind = definition.PayloadKind, note = request.LifeEventPayload?.note },
                LifeEventCategory = request.Category ?? definition.LifeEventCategory,
                Significance = request.Significance ?? definition.DefaultSignificance,
                BiographyRelevance = request.BiographyRelevance ?? definition.DefaultBiographyRelevance,
                PublicRecordRelevance = request.PublicRecordRelevance ?? definition.DefaultPublicRecordRelevance,
                LifeEventOutcome = request.Outcome,
                LifeEventParticipants = participants,
                LifeEventRelationships = request.Relationships,
                LifeEventSequenceId = request.SequenceId,
                LifeEventSequenceOrder = request.SequenceOrder,
                RelatedRoleId = request.RelatedRoleId,
                RelatedTitleId = request.RelatedTitleId,
                RelatedSocialStatusId = request.RelatedSocialStatusId,
                RelatedConditionId = request.RelatedConditionId,
                RelatedInjuryId = request.RelatedInjuryId,
                RelatedDiseaseId = request.RelatedDiseaseId,
                RelatedTreatmentId = request.RelatedTreatmentId,
                RelatedCombatEncounterId = request.RelatedCombatEncounterId,
                RelatedQuestId = request.RelatedQuestId,
                RelatedLegalRecordId = request.RelatedLegalRecordId,
                RelatedRelationshipId = request.RelatedRelationshipId,
                LifeEventPayload = request.LifeEventPayload ?? new LifeEventPayloadData { kind = request.PayloadKind ?? definition.LifeEventPayloadKind }
            };
        }

        private LifeEventParticipantData[] NormalizeLifeEventParticipants(RecordLifeEventRequest request)
        {
            List<LifeEventParticipantData> participants = (request.Participants ?? Array.Empty<LifeEventParticipantData>())
                .Where(participant => participant != null && !string.IsNullOrWhiteSpace(participant.personId))
                .Select(participant => participant.Clone())
                .ToList();

            if (!string.IsNullOrWhiteSpace(request.PrimaryPersonId) && !participants.Any(participant => string.Equals(participant.personId, request.PrimaryPersonId, StringComparison.Ordinal) && participant.role == LifeEventParticipantRole.Subject))
            {
                participants.Add(Participant(request.PrimaryPersonId, LifeEventParticipantRole.Subject));
            }

            return participants
                .OrderBy(participant => participant.role)
                .ThenBy(participant => participant.personId, StringComparer.Ordinal)
                .ThenBy(participant => participant.bodyId, StringComparer.Ordinal)
                .ToArray();
        }

        private bool ValidateLifeEventVisibility(KnowledgeVisibility visibility, HistoricalEventDefinition definition, out string failure)
        {
            failure = string.Empty;
            if (!Enum.IsDefined(typeof(KnowledgeVisibility), visibility))
            {
                failure = $"Life-event definition '{definition?.Id}' has invalid visibility.";
                return false;
            }

            if (definition == null)
            {
                return true;
            }

            if (!definition.MayBePrivate && (visibility == KnowledgeVisibility.Private || visibility == KnowledgeVisibility.Confidential || visibility == KnowledgeVisibility.DiagnosticOnly))
            {
                failure = $"Life-event definition '{definition.Id}' does not allow private events.";
                return false;
            }

            if (!definition.MayBeSecret && (visibility == KnowledgeVisibility.Hidden || visibility == KnowledgeVisibility.Secret || visibility == KnowledgeVisibility.DevelopmentOnly))
            {
                failure = $"Life-event definition '{definition.Id}' does not allow hidden or secret events.";
                return false;
            }

            return true;
        }

        private bool ValidateSavedLifeEvent(HistoricalEventRecordData eventData, out string failure)
        {
            failure = string.Empty;
            if (eventData == null || !eventData.isLifeEvent)
            {
                return true;
            }

            if (eventData.lifeEventCategory == LifeEventCategory.None || !Enum.IsDefined(typeof(LifeEventCategory), eventData.lifeEventCategory))
            {
                failure = $"Life event '{eventData?.eventId}' has an invalid life-event category.";
                return false;
            }

            foreach (LifeEventParticipantData participant in eventData.lifeEventParticipants ?? Array.Empty<LifeEventParticipantData>())
            {
                if (participant == null || string.IsNullOrWhiteSpace(participant.personId) || participant.role == LifeEventParticipantRole.Unknown || !Enum.IsDefined(typeof(LifeEventParticipantRole), participant.role))
                {
                    failure = $"Life event '{eventData.eventId}' has an invalid participant role.";
                    return false;
                }
            }

            foreach (LifeEventRelationshipData relationship in eventData.lifeEventRelationships ?? Array.Empty<LifeEventRelationshipData>())
            {
                if (!ValidateLifeEventRelationship(eventData.eventId, relationship, out failure))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(eventData.lifeEventSequenceId) && !lifeEventSequencesById.ContainsKey(eventData.lifeEventSequenceId))
            {
                failure = $"Life event '{eventData.eventId}' references missing sequence '{eventData.lifeEventSequenceId}'.";
                return false;
            }

            return true;
        }

        private bool ValidateLifeEventRelationship(string eventId, LifeEventRelationshipData relationship, out string failure)
        {
            failure = string.Empty;
            if (relationship == null || string.IsNullOrWhiteSpace(relationship.targetEventId))
            {
                failure = $"Life event '{eventId}' has a malformed relationship.";
                return false;
            }

            if (string.Equals(eventId, relationship.targetEventId, StringComparison.Ordinal))
            {
                failure = $"Life event '{eventId}' cannot relate to itself.";
                return false;
            }

            if (!eventsById.ContainsKey(relationship.targetEventId))
            {
                failure = $"Life event '{eventId}' references missing related event '{relationship.targetEventId}'.";
                return false;
            }

            if (relationship.requiresAcyclic && WouldCreateRelationshipCycle(eventId, relationship.targetEventId, relationship.relationshipType))
            {
                failure = $"Life event relationship from '{eventId}' to '{relationship.targetEventId}' would create a cycle.";
                return false;
            }

            return true;
        }

        private bool ValidateLifeEventSequenceMembership(RecordLifeEventRequest request, out string failure)
        {
            failure = string.Empty;
            if (request.SequenceOrder < 0)
            {
                failure = $"Life event '{request.EventId}' has an invalid sequence order.";
                return false;
            }

            if (lifeEventSequencesById.TryGetValue(request.SequenceId, out LifeEventSequenceData sequence))
            {
                if (!string.IsNullOrWhiteSpace(sequence.primaryPersonId) && !string.Equals(sequence.primaryPersonId, request.PrimaryPersonId, StringComparison.Ordinal))
                {
                    failure = $"Life event '{request.EventId}' sequence '{request.SequenceId}' belongs to Person '{sequence.primaryPersonId}'.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateLifeEventSequence(LifeEventSequenceData sequence, Dictionary<string, HistoricalEventRecordData> eventsById, HashSet<string> knownPersons, out string failure)
        {
            failure = string.Empty;
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.sequenceId))
            {
                failure = "Life-event sequence is missing a stable sequence ID.";
                return false;
            }

            if (knownPersons.Count > 0 && !string.IsNullOrWhiteSpace(sequence.primaryPersonId) && !knownPersons.Contains(sequence.primaryPersonId))
            {
                failure = $"Life-event sequence '{sequence.sequenceId}' references unknown Person '{sequence.primaryPersonId}'.";
                return false;
            }

            if (sequence.endedAtWorldTime >= 0d && sequence.endedAtWorldTime < sequence.startedAtWorldTime)
            {
                failure = $"Life-event sequence '{sequence.sequenceId}' has invalid time order.";
                return false;
            }

            foreach (string eventId in sequence.eventIds ?? Array.Empty<string>())
            {
                if (!eventsById.ContainsKey(eventId ?? string.Empty))
                {
                    failure = $"Life-event sequence '{sequence.sequenceId}' references missing event '{eventId}'.";
                    return false;
                }
            }

            return true;
        }

        private bool WouldCreateRelationshipCycle(string sourceEventId, string targetEventId, LifeEventRelationshipType relationshipType)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal) { sourceEventId };
            string current = targetEventId;
            while (!string.IsNullOrWhiteSpace(current) && eventsById.TryGetValue(current, out HistoricalEventRecordData data))
            {
                if (!visited.Add(current))
                {
                    return true;
                }

                LifeEventRelationshipData next = (data.lifeEventRelationships ?? Array.Empty<LifeEventRelationshipData>())
                    .FirstOrDefault(relationship => relationship.relationshipType == relationshipType && relationship.requiresAcyclic);
                current = next?.targetEventId;
            }

            return false;
        }

        private void AddOrUpdateLifeEventSequence(RecordLifeEventRequest request, HistoricalEventRecord eventRecord)
        {
            if (!lifeEventSequencesById.TryGetValue(request.SequenceId, out LifeEventSequenceData sequence))
            {
                sequence = new LifeEventSequenceData
                {
                    sequenceId = request.SequenceId,
                    sequenceTypeId = request.SequenceTypeId,
                    primaryPersonId = request.PrimaryPersonId,
                    status = request.SequenceStatus,
                    startedAtWorldTime = eventRecord.OccurredAtWorldTime,
                    endedAtWorldTime = -1d,
                    correlationId = request.CorrelationId,
                    eventIds = Array.Empty<string>()
                };
                lifeEventSequencesById[request.SequenceId] = sequence;
            }

            sequence.status = request.SequenceStatus;
            sequence.eventIds = Distinct((sequence.eventIds ?? Array.Empty<string>()).Concat(new[] { eventRecord.EventId })).ToArray();
            sequence.startedAtWorldTime = Math.Min(sequence.startedAtWorldTime, eventRecord.OccurredAtWorldTime);
            if (request.SequenceStatus == LifeEventSequenceStatus.Completed)
            {
                sequence.endedAtWorldTime = Math.Max(sequence.endedAtWorldTime, eventRecord.OccurredAtWorldTime);
            }
        }

        private void RebuildMissingLifeEventSequencesFromEvents()
        {
            foreach (HistoricalEventRecordData eventData in eventsById.Values
                .Where(data => data != null && data.isLifeEvent && !string.IsNullOrWhiteSpace(data.lifeEventSequenceId))
                .OrderBy(data => data.lifeEventSequenceId, StringComparer.Ordinal)
                .ThenBy(data => data.lifeEventSequenceOrder)
                .ThenBy(data => data.occurredAtWorldTime)
                .ThenBy(data => data.sequence)
                .ThenBy(data => data.eventId, StringComparer.Ordinal))
            {
                if (!lifeEventSequencesById.TryGetValue(eventData.lifeEventSequenceId, out LifeEventSequenceData sequence))
                {
                    sequence = new LifeEventSequenceData
                    {
                        sequenceId = eventData.lifeEventSequenceId,
                        sequenceTypeId = $"{eventData.lifeEventSequenceId}.type",
                        primaryPersonId = eventData.primaryPersonId,
                        status = LifeEventSequenceStatus.Active,
                        startedAtWorldTime = eventData.occurredAtWorldTime,
                        endedAtWorldTime = -1d,
                        correlationId = eventData.correlationId,
                        eventIds = Array.Empty<string>()
                    };
                    lifeEventSequencesById[eventData.lifeEventSequenceId] = sequence;
                }

                if (string.IsNullOrWhiteSpace(sequence.primaryPersonId))
                {
                    sequence.primaryPersonId = eventData.primaryPersonId;
                }

                if (string.IsNullOrWhiteSpace(sequence.sequenceTypeId))
                {
                    sequence.sequenceTypeId = $"{sequence.sequenceId}.type";
                }

                if (string.IsNullOrWhiteSpace(sequence.correlationId))
                {
                    sequence.correlationId = eventData.correlationId;
                }

                sequence.eventIds = Distinct((sequence.eventIds ?? Array.Empty<string>()).Concat(new[] { eventData.eventId })).ToArray();
                sequence.eventIds = sequence.eventIds
                    .Where(id => eventsById.ContainsKey(id))
                    .OrderBy(id => eventsById[id].lifeEventSequenceOrder)
                    .ThenBy(id => eventsById[id].occurredAtWorldTime)
                    .ThenBy(id => eventsById[id].sequence)
                    .ThenBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                sequence.startedAtWorldTime = sequence.eventIds.Length == 0 ? eventData.occurredAtWorldTime : sequence.eventIds.Min(id => eventsById[id].occurredAtWorldTime);
                if (sequence.status == LifeEventSequenceStatus.Completed)
                {
                    sequence.endedAtWorldTime = sequence.eventIds.Length == 0 ? eventData.occurredAtWorldTime : sequence.eventIds.Max(id => eventsById[id].occurredAtWorldTime);
                }
            }
        }

        private static HistoricalEventCategory MapLifeEventCategory(LifeEventCategory category)
        {
            return category switch
            {
                LifeEventCategory.BirthOrCreation => HistoricalEventCategory.BirthOrCreation,
                LifeEventCategory.BodyTransition => HistoricalEventCategory.BodyTransition,
                LifeEventCategory.Travel or LifeEventCategory.Migration => HistoricalEventCategory.Travel,
                LifeEventCategory.RelationshipMilestone => HistoricalEventCategory.Relationship,
                LifeEventCategory.Affiliation => HistoricalEventCategory.Affiliation,
                LifeEventCategory.Employment or LifeEventCategory.Role or LifeEventCategory.Title => HistoricalEventCategory.EmploymentOrRole,
                LifeEventCategory.Combat => HistoricalEventCategory.Combat,
                LifeEventCategory.Injury => HistoricalEventCategory.Injury,
                LifeEventCategory.Recovery => HistoricalEventCategory.Recovery,
                LifeEventCategory.Disease => HistoricalEventCategory.Disease,
                LifeEventCategory.Diagnosis => HistoricalEventCategory.Diagnosis,
                LifeEventCategory.Treatment => HistoricalEventCategory.Treatment,
                LifeEventCategory.Crime => HistoricalEventCategory.Crime,
                LifeEventCategory.Discovery => HistoricalEventCategory.Discovery,
                LifeEventCategory.Ownership or LifeEventCategory.Property => HistoricalEventCategory.Ownership,
                LifeEventCategory.Political => HistoricalEventCategory.Political,
                LifeEventCategory.SocialStatus => HistoricalEventCategory.Social,
                LifeEventCategory.Death or LifeEventCategory.Disappearance or LifeEventCategory.ReturnOrResurrection => HistoricalEventCategory.DeathOrDisappearance,
                LifeEventCategory.QuestRelated => HistoricalEventCategory.QuestRelevant,
                _ => HistoricalEventCategory.CustomWorldEvent
            };
        }

        private static LifeEventParticipantData Participant(string personId, LifeEventParticipantRole role, string bodyId = "")
        {
            return new LifeEventParticipantData { personId = personId, role = role, bodyId = bodyId };
        }

        private bool CanSupersede(string eventId, string targetEventId, out string failure)
        {
            failure = string.Empty;
            if (string.Equals(eventId, targetEventId, StringComparison.Ordinal))
            {
                failure = "Historical event cannot supersede itself.";
                return false;
            }

            if (!eventsById.TryGetValue(targetEventId ?? string.Empty, out HistoricalEventRecordData target))
            {
                failure = $"Superseded historical event '{targetEventId}' is missing.";
                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { eventId };
            while (!string.IsNullOrWhiteSpace(target.correctedByEventId))
            {
                if (!seen.Add(target.correctedByEventId))
                {
                    failure = $"Historical event correction chain for '{targetEventId}' is circular.";
                    return false;
                }

                if (!eventsById.TryGetValue(target.correctedByEventId, out target))
                {
                    break;
                }
            }

            return true;
        }

        private bool ValidateKnownPerson(string personId, bool required, out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(personId))
            {
                if (!required)
                {
                    return true;
                }

                failure = "Historical event references an empty Person ID.";
                return false;
            }

            if (knownPersonIds.Count > 0 && !knownPersonIds.Contains(personId))
            {
                failure = $"Historical event references unknown Person '{personId}'.";
                return false;
            }

            return true;
        }

        private bool ValidateKnownBody(string bodyId, out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(bodyId))
            {
                return true;
            }

            if (knownBodyIds.Count > 0 && !knownBodyIds.Contains(bodyId))
            {
                failure = $"Historical event references unknown Body '{bodyId}'.";
                return false;
            }

            return true;
        }

        private static bool ValidateOccupation(BodyOccupationRecordData occupation, HashSet<string> knownPersons, HashSet<string> knownBodies, out string failure)
        {
            failure = string.Empty;
            if (occupation == null || string.IsNullOrWhiteSpace(occupation.occupationId) || string.IsNullOrWhiteSpace(occupation.personId) || string.IsNullOrWhiteSpace(occupation.bodyId))
            {
                failure = "Body occupation record is missing required IDs.";
                return false;
            }

            if (occupation.endedAtWorldTime >= 0d && occupation.endedAtWorldTime < occupation.startedAtWorldTime)
            {
                failure = $"Body occupation '{occupation.occupationId}' has an invalid time range.";
                return false;
            }

            if (knownPersons.Count > 0 && !knownPersons.Contains(occupation.personId))
            {
                failure = $"Body occupation '{occupation.occupationId}' references unknown Person '{occupation.personId}'.";
                return false;
            }

            if (knownBodies.Count > 0 && !knownBodies.Contains(occupation.bodyId))
            {
                failure = $"Body occupation '{occupation.occupationId}' references unknown Body '{occupation.bodyId}'.";
                return false;
            }

            return true;
        }

        private void AddIndexes(HistoricalEventRecordData data)
        {
            foreach (string person in Distinct(data.participantPersonIds))
            {
                AddIndex(eventIdsByPerson, person, data.eventId);
            }

            foreach (string body in Distinct(data.bodyIds))
            {
                AddIndex(eventIdsByBody, body, data.eventId);
            }

            AddIndex(eventIdsByLocation, data.locationId, data.eventId);
            AddIndex(eventIdsByOrganization, data.organizationId, data.eventId);
            foreach (string tag in Distinct(data.tags))
            {
                AddIndex(eventIdsByTag, tag, data.eventId);
            }
        }

        private void AddOccupation(BodyOccupationRecordData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.occupationId))
            {
                return;
            }

            occupationsById[data.occupationId] = data.Clone();
            AddIndex(occupationIdsByPerson, data.personId, data.occupationId);
        }

        private void CloseOpenOccupation(string personId, string bodyId, double endTime, string endEventId)
        {
            foreach (BodyOccupationRecordData occupation in occupationsById.Values.Where(record => string.Equals(record.personId, personId, StringComparison.Ordinal) && record.IsOpenEnded() && (string.IsNullOrWhiteSpace(bodyId) || string.Equals(record.bodyId, bodyId, StringComparison.Ordinal))).ToArray())
            {
                occupation.endedAtWorldTime = endTime;
                occupation.endEventId = endEventId;
            }
        }

        private IReadOnlyList<BodyOccupationRecord> OccupationsFromIndex(string personId)
        {
            if (!occupationIdsByPerson.TryGetValue(personId ?? string.Empty, out List<string> ids))
            {
                return Array.Empty<BodyOccupationRecord>();
            }

            return ids.Where(id => occupationsById.ContainsKey(id)).Select(id => new BodyOccupationRecord(occupationsById[id])).OrderBy(record => record.StartedAtWorldTime).ThenBy(record => record.OccupationId, StringComparer.Ordinal).ToArray();
        }

        private IReadOnlyList<HistoricalEventRecord> RecordsFromIndex(Dictionary<string, List<string>> index, string key)
        {
            if (!index.TryGetValue(key ?? string.Empty, out List<string> ids))
            {
                return Array.Empty<HistoricalEventRecord>();
            }

            return ids.Where(id => eventsById.ContainsKey(id)).Select(id => Wrap(eventsById[id])).OrderBy(HistoryOrdering.Key).ToArray();
        }

        private static void AddIndex(Dictionary<string, List<string>> index, string key, string eventId)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(eventId))
            {
                return;
            }

            if (!index.TryGetValue(key, out List<string> ids))
            {
                ids = new List<string>();
                index[key] = ids;
            }

            if (!ids.Contains(eventId, StringComparer.Ordinal))
            {
                ids.Add(eventId);
            }
        }

        private bool IsVisibleToPerson(HistoricalEventRecordData data, string personId, HashSet<string> rememberedEventIds)
        {
            if (data == null)
            {
                return false;
            }

            if (rememberedEventIds.Contains(data.eventId))
            {
                return true;
            }

            if (data.visibility == KnowledgeVisibility.Public || data.visibility == KnowledgeVisibility.PersonallyObservable)
            {
                return true;
            }

            return data.visibility == KnowledgeVisibility.Private
                && !string.IsNullOrWhiteSpace(personId)
                && (string.Equals(data.primaryPersonId, personId, StringComparison.Ordinal)
                    || (data.participantPersonIds ?? Array.Empty<string>()).Contains(personId, StringComparer.Ordinal));
        }

        private InformationAccessProjection<HistoricalEventRecord> ProjectHistoricalEvent(HistoricalEventRecord record, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId, bool recordAudit)
        {
            string[] detailIds = HistoricalEventDetailIds(record);
            InformationAccessContext context = InformationAccessProjectionUtility.BuildContext(
                accessContext,
                BuildHistorySubject(record),
                InformationAccessMode.Query,
                InformationAccessPurpose.Journal,
                detailIds,
                policyId);
            RedactedInformationProjection projection = accessRuntime?.Project(context, detailIds);
            if (projection == null)
            {
                return new InformationAccessProjection<HistoricalEventRecord>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Information access runtime is missing.");
            }

            if (recordAudit)
            {
                accessRuntime.RecordAudit(projection.Decision, context, gameplayAudit: false);
            }

            HistoricalEventRecord projected = projection.Decision.Denied ? null : MaterializeHistoricalEvent(record, projection.Details);
            string visibleSubjectId = projected == null || !InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.event") ? string.Empty : projected.EventId;
            return new InformationAccessProjection<HistoricalEventRecord>(projected, projection.Decision, projection.Details, visibleSubjectId, projection.Decision.VisibleReason);
        }

        private InformationAccessProjection<BiographyTimelineEntry> ProjectBiographyEntry(BiographyTimelineEntry entry, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId)
        {
            if (entry == null || entry.LifeEvent == null)
            {
                return new InformationAccessProjection<BiographyTimelineEntry>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Biography entry is missing.");
            }

            InformationAccessProjection<HistoricalEventRecord> eventProjection = ProjectHistoricalEvent(entry.LifeEvent.Event, accessRuntime, accessContext, policyId, recordAudit: false);
            if (eventProjection.Denied || eventProjection.Record == null)
            {
                return new InformationAccessProjection<BiographyTimelineEntry>(null, eventProjection.Decision, eventProjection.DetailStates, string.Empty, eventProjection.Message);
            }

            BiographyTimelineEntry projected = new BiographyTimelineEntry(new LifeEventRecord(eventProjection.Record), entry.ParticipantRole, entry.Known, entry.Remembered);
            return new InformationAccessProjection<BiographyTimelineEntry>(projected, eventProjection.Decision, eventProjection.DetailStates, eventProjection.VisibleSubjectId, eventProjection.Message);
        }

        private static InformationSubjectReferenceData BuildHistorySubject(HistoricalEventRecord record)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = record != null && record.IsLifeEvent ? InformationSubjectType.LifeEvent : InformationSubjectType.HistoricalEvent,
                subjectId = record?.EventId ?? string.Empty,
                ownerPersonId = record?.PrimaryPersonId ?? string.Empty,
                controllingEntityId = record?.OrganizationId ?? string.Empty,
                tags = record?.Tags.ToArray() ?? Array.Empty<string>()
            };
        }

        private static string[] HistoricalEventDetailIds(HistoricalEventRecord record)
        {
            List<string> details = new List<string>
            {
                "detail.summary",
                "detail.event",
                "detail.definition",
                "detail.time",
                "detail.primary-person",
                "detail.participants",
                "detail.bodies",
                "detail.location",
                "detail.organization",
                "detail.payload",
                "detail.provenance",
                "detail.source",
                "detail.correction",
                "detail.sequence",
                "detail.relationships"
            };
            details.AddRange(record?.Tags.Select(tag => $"detail.tag.{tag}") ?? Array.Empty<string>());
            return details.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private HistoricalEventRecord MaterializeHistoricalEvent(HistoricalEventRecord record, IReadOnlyDictionary<string, InformationRedactionState> details)
        {
            if (record == null)
            {
                return null;
            }

            if (details.Values.All(state => state == InformationRedactionState.Visible))
            {
                return record;
            }

            HistoricalEventRecordData data = record.Data.Clone();
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.event")) data.eventId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.definition")) data.eventDefinitionId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.time")) { data.occurredAtWorldTime = 0d; data.recordedAtWorldTime = 0d; data.sequence = 0L; }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.primary-person")) data.primaryPersonId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.participants")) { data.participantPersonIds = Array.Empty<string>(); data.lifeEventParticipants = Array.Empty<LifeEventParticipantData>(); }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.bodies")) data.bodyIds = Array.Empty<string>();
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.location")) data.locationId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.organization")) data.organizationId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.payload")) { data.payload = new HistoricalEventPayloadData(); data.lifeEventPayload = new LifeEventPayloadData(); data.relatedEntityIds = Array.Empty<string>(); }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.provenance")) data.provenance = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.source")) data.sourceSystem = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.correction")) { data.supersedesEventId = string.Empty; data.correctedByEventId = string.Empty; }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.sequence")) { data.lifeEventSequenceId = string.Empty; data.lifeEventSequenceOrder = 0; data.correlationId = string.Empty; }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.relationships")) data.lifeEventRelationships = Array.Empty<LifeEventRelationshipData>();
            return Wrap(data);
        }

        private HistoricalEventRecord Wrap(HistoricalEventRecordData data)
        {
            HistoricalEventDefinition definition = null;
            registry?.TryGet(data?.eventDefinitionId, out definition);
            return new HistoricalEventRecord(data, definition);
        }

        private void ClearIndexes()
        {
            eventIdsByPerson.Clear();
            eventIdsByBody.Clear();
            eventIdsByLocation.Clear();
            eventIdsByOrganization.Clear();
            eventIdsByTag.Clear();
        }

        private void RaiseChanged(HistoryOperationResult result, bool restoring)
        {
            if (!restoring && !suppressEvents)
            {
                HistoryChanged?.Invoke(this, result);
            }
        }

        private static IEnumerable<string> Distinct(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal);
        }

        private static string TransactionKey(string transactionId)
        {
            return transactionId ?? string.Empty;
        }

        private static bool LifeEventReferences(HistoricalEventRecordData data, string relatedId)
        {
            return (data.relatedEntityIds ?? Array.Empty<string>()).Contains(relatedId, StringComparer.Ordinal)
                || string.Equals(data.locationId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.organizationId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedRoleId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedTitleId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedSocialStatusId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedConditionId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedInjuryId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedDiseaseId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedTreatmentId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedCombatEncounterId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedQuestId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedLegalRecordId, relatedId, StringComparison.Ordinal)
                || string.Equals(data.relatedRelationshipId, relatedId, StringComparison.Ordinal)
                || (data.lifeEventParticipants ?? Array.Empty<LifeEventParticipantData>()).Any(participant => string.Equals(participant.relatedEntityId, relatedId, StringComparison.Ordinal))
                || (data.lifeEventRelationships ?? Array.Empty<LifeEventRelationshipData>()).Any(relationship => string.Equals(relationship.targetEventId, relatedId, StringComparison.Ordinal));
        }

        private static LifeEventParticipantRole RoleFor(LifeEventRecord record, string personId)
        {
            return record?.Participants.FirstOrDefault(participant => string.Equals(participant.personId, personId, StringComparison.Ordinal))?.role ?? LifeEventParticipantRole.Unknown;
        }

        private sealed class LifeEventHistoricalEventRequest : RecordHistoricalEventRequest
        {
            public LifeEventCategory LifeEventCategory { get; set; }
            public LifeEventSignificance Significance { get; set; }
            public LifeEventBiographyRelevance BiographyRelevance { get; set; }
            public LifeEventPublicRecordRelevance PublicRecordRelevance { get; set; }
            public LifeEventOutcome LifeEventOutcome { get; set; }
            public LifeEventParticipantData[] LifeEventParticipants { get; set; }
            public LifeEventRelationshipData[] LifeEventRelationships { get; set; }
            public string LifeEventSequenceId { get; set; }
            public int LifeEventSequenceOrder { get; set; }
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
            public LifeEventPayloadData LifeEventPayload { get; set; }
        }
    }

    internal static class BodyOccupationRuntimeExtensions
    {
        public static bool IsOpenEnded(this BodyOccupationRecordData data)
        {
            return data != null && data.endedAtWorldTime < 0d;
        }
    }
}
