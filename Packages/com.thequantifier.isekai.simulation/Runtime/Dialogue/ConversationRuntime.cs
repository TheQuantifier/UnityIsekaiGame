using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Dialogue
{
    public sealed class ConversationRuntime : IDisposable
    {
        private readonly Dictionary<string, ConversationRecordData> conversationsById = new Dictionary<string, ConversationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConversationTransactionData> transactionsById = new Dictionary<string, ConversationTransactionData>(StringComparer.Ordinal);
        private readonly List<ConversationEventData> events = new List<ConversationEventData>();
        private readonly Dictionary<string, List<string>> conversationsByPerson = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> conversationsByLocation = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> conversationsByInteractionPoint = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> conversationsByQuest = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> conversationsBySource = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> conversationsByListing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> conversationsByOrganization = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> conversationsByOffice = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private string worldId;
        private long revision;
        private bool disposed;

        public event Action<ConversationEventData> EventCommitted;

        public ConversationRuntime(DefinitionRegistry definitionRegistry = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            Configure(definitionRegistry, runtimeWorldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int Count => conversationsById.Count;
        public IReadOnlyList<ConversationEventData> Events => events.Select(value => value.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            registry = definitionRegistry;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId;
        }

        public ConversationOperationResult StartConversation(ConversationStartRequest request)
        {
            if (disposed) return Fail(ConversationOperationStatus.Disposed, "Conversation runtime is disposed.");
            request ??= new ConversationStartRequest();
            if (!ValidateRevision(request.expectedRevision, out ConversationOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out ConversationOperationResult duplicate)) return duplicate;
            if (registry == null) return Fail(ConversationOperationStatus.MissingDefinitionRegistry, "Conversation runtime has no definition registry.");
            if (string.IsNullOrWhiteSpace(request.conversationDefinitionId)) return Fail(ConversationOperationStatus.InvalidRequest, "Conversation start requires a definition ID.");
            if (!registry.TryGet(N(request.conversationDefinitionId), out ConversationDefinition definition)) return Fail(ConversationOperationStatus.MissingDefinition, $"Conversation definition '{N(request.conversationDefinitionId)}' is missing.");

            ConversationParticipantRecordData[] participants = NormalizeParticipants(request.participants);
            if (participants.Length == 0) return Fail(ConversationOperationStatus.MissingParticipant, "Conversation start requires at least one participant.");
            ConversationOperationResult validationFailure = ValidateDefinitionRules(definition, request, participants);
            if (validationFailure != null) return validationFailure;

            string conversationId = string.IsNullOrWhiteSpace(request.conversationId)
                ? BuildConversationId(definition.Id, request.hostInteractionPointId, request.questId, transactionId)
                : N(request.conversationId);
            if (conversationsById.ContainsKey(conversationId)) return Fail(ConversationOperationStatus.InvalidRequest, $"Conversation '{conversationId}' already exists.");

            ConversationRecordData record = new ConversationRecordData
            {
                conversationId = conversationId,
                conversationDefinitionId = definition.Id,
                worldId = worldId,
                lifecycleState = request.initialLifecycleState == ConversationLifecycleState.Unknown ? ConversationLifecycleState.Active : request.initialLifecycleState,
                visibility = request.visibility ?? definition.DefaultVisibility,
                participants = participants,
                subjectLinks = NormalizeSubjects(request.subjectLinks, request),
                activeSpeakerPersonId = string.IsNullOrWhiteSpace(request.activeSpeakerPersonId) ? participants.First().personId : N(request.activeSpeakerPersonId),
                hostLocationId = N(request.hostLocationId),
                hostInteractionPointId = N(request.hostInteractionPointId),
                questSourceId = N(request.questSourceId),
                questListingId = N(request.questListingId),
                questId = N(request.questId),
                operatingOrganizationId = N(request.operatingOrganizationId),
                operatingOfficeId = N(request.operatingOfficeId),
                operatingGovernmentId = N(request.operatingGovernmentId),
                operatingFactionId = N(request.operatingFactionId),
                operatingBusinessId = N(request.operatingBusinessId),
                sceneBindingKey = N(request.sceneBindingKey),
                tagIds = QuestRuntimeModelUtility.Clean((request.tagIds ?? Array.Empty<string>()).Concat(definition.TagIds)),
                startedWorldTime = request.worldTime,
                provenanceId = N(request.provenanceId),
                revision = 1L
            };

            if (request.preview) return ConversationOperationResult.Success("Conversation previewed.", revision, revision, record, preview: true);

            long before = revision;
            conversationsById[conversationId] = record.Clone();
            revision++;
            RecordTransaction(transactionId, "StartConversation", conversationId, ConversationOperationStatus.Succeeded);
            ConversationEventData committed = RecordEvent(transactionId, ConversationEventKind.ConversationStarted, conversationId, record.activeSpeakerPersonId, ConversationLifecycleState.Unknown, record.lifecycleState, request.worldTime, request.provenanceId);
            RebuildIndexes();
            EventCommitted?.Invoke(committed.Clone());
            return ConversationOperationResult.Success("Conversation started.", before, revision, record);
        }

        public ConversationOperationResult TransitionLifecycle(ConversationLifecycleRequest request)
        {
            if (disposed) return Fail(ConversationOperationStatus.Disposed, "Conversation runtime is disposed.");
            request ??= new ConversationLifecycleRequest();
            if (!ValidateRevision(request.expectedRevision, out ConversationOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out ConversationOperationResult duplicate)) return duplicate;
            if (!conversationsById.TryGetValue(N(request.conversationId), out ConversationRecordData record)) return Fail(ConversationOperationStatus.InvalidRequest, $"Conversation '{N(request.conversationId)}' is missing.");
            if (request.targetState == ConversationLifecycleState.Unknown) return Fail(ConversationOperationStatus.InvalidRequest, "Conversation lifecycle transition requires a concrete state.");

            ConversationRecordData changed = record.Clone();
            ConversationLifecycleState beforeState = changed.lifecycleState;
            changed.lifecycleState = request.targetState;
            if (IsInactive(request.targetState)) changed.endedWorldTime = request.worldTime;
            changed.revision++;
            if (request.preview) return ConversationOperationResult.Success("Conversation lifecycle previewed.", revision, revision, changed, preview: true);

            long before = revision;
            conversationsById[changed.conversationId] = changed;
            revision++;
            RecordTransaction(transactionId, "TransitionLifecycle", changed.conversationId, ConversationOperationStatus.Succeeded);
            ConversationEventData committed = RecordEvent(transactionId, ConversationEventKind.ConversationLifecycleChanged, changed.conversationId, changed.activeSpeakerPersonId, beforeState, changed.lifecycleState, request.worldTime, request.provenanceId);
            RebuildIndexes();
            EventCommitted?.Invoke(committed.Clone());
            return ConversationOperationResult.Success("Conversation lifecycle changed.", before, revision, changed);
        }

        public bool TryGetSnapshot(string conversationId, out ConversationSnapshot snapshot)
        {
            if (conversationsById.TryGetValue(N(conversationId), out ConversationRecordData record))
            {
                snapshot = new ConversationSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<ConversationProjection> Query(ConversationQuery query)
        {
            query ??= new ConversationQuery();
            IEnumerable<ConversationRecordData> records = conversationsById.Values;

            if (!string.IsNullOrWhiteSpace(query.conversationId)) records = records.Where(value => string.Equals(value.conversationId, N(query.conversationId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.definitionId)) records = records.Where(value => string.Equals(value.conversationDefinitionId, N(query.definitionId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.worldId)) records = records.Where(value => string.Equals(value.worldId, N(query.worldId), StringComparison.Ordinal));
            if (!query.includeInactive) records = records.Where(value => !IsInactive(value.lifecycleState));
            if (!string.IsNullOrWhiteSpace(query.personId)) records = records.Where(value => HasParticipant(value, query.personId));
            if (!string.IsNullOrWhiteSpace(query.locationId)) records = records.Where(value => string.Equals(value.hostLocationId, N(query.locationId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.interactionPointId)) records = records.Where(value => string.Equals(value.hostInteractionPointId, N(query.interactionPointId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.questId)) records = records.Where(value => string.Equals(value.questId, N(query.questId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.questSourceId)) records = records.Where(value => string.Equals(value.questSourceId, N(query.questSourceId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.questListingId)) records = records.Where(value => string.Equals(value.questListingId, N(query.questListingId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.organizationId)) records = records.Where(value => string.Equals(value.operatingOrganizationId, N(query.organizationId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.officeId)) records = records.Where(value => string.Equals(value.operatingOfficeId, N(query.officeId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.tagId)) records = records.Where(value => (value.tagIds ?? Array.Empty<string>()).Contains(N(query.tagId), StringComparer.Ordinal));

            return records
                .OrderBy(value => value.startedWorldTime)
                .ThenBy(value => value.conversationId, StringComparer.Ordinal)
                .Select(value => Project(value, query))
                .Where(value => value != null)
                .ToArray();
        }

        public ConversationRuntimeSaveData CreateSaveData()
        {
            return new ConversationRuntimeSaveData
            {
                schemaVersion = ConversationRuntimeSaveData.CurrentSchemaVersion,
                worldId = WorldId,
                revision = revision,
                conversations = conversationsById.Values.Select(value => value.Clone()).OrderBy(value => value.conversationId, StringComparer.Ordinal).ToList(),
                events = events.Select(value => value.Clone()).OrderBy(value => value.eventId, StringComparer.Ordinal).ToList(),
                transactions = transactionsById.Values.Select(value => value.Clone()).OrderBy(value => value.transactionId, StringComparer.Ordinal).ToList()
            };
        }

        public ConversationOperationResult RestoreFromSaveData(ConversationRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            if (disposed) return Fail(ConversationOperationStatus.Disposed, "Conversation runtime is disposed.");
            if (!ValidateSaveData(saveData, definitionRegistry, expectedWorldId, out string failure)) return Fail(ConversationOperationStatus.RestoreFailed, failure);

            conversationsById.Clear();
            transactionsById.Clear();
            events.Clear();
            worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId;
            registry = definitionRegistry;
            revision = saveData.revision;
            foreach (ConversationRecordData conversation in saveData.conversations ?? new List<ConversationRecordData>()) conversationsById[conversation.conversationId] = conversation.Clone();
            foreach (ConversationTransactionData transaction in saveData.transactions ?? new List<ConversationTransactionData>()) transactionsById[transaction.transactionId] = transaction.Clone();
            events.AddRange((saveData.events ?? new List<ConversationEventData>()).Where(value => value != null).Select(value => value.Clone()));
            RebuildIndexes();
            return ConversationOperationResult.Success("Conversations restored.", revision, revision);
        }

        public ConversationRuntimeValidationReport ValidateRuntime()
        {
            ValidateSaveData(CreateSaveData(), registry, WorldId, out _, out ConversationRuntimeValidationReport report);
            return report;
        }

        public static bool ValidateSaveData(ConversationRuntimeSaveData saveData, DefinitionRegistry registry, string expectedWorldId, out string failure)
        {
            return ValidateSaveData(saveData, registry, expectedWorldId, out failure, out _);
        }

        public static bool ValidateSaveData(ConversationRuntimeSaveData saveData, DefinitionRegistry registry, string expectedWorldId, out string failure, out ConversationRuntimeValidationReport report)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            if (saveData == null)
            {
                errors.Add("Conversation save data is missing.");
            }
            else
            {
                if (saveData.schemaVersion != ConversationRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported conversation save schema version {saveData.schemaVersion}.");
                string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId;
                if (!string.Equals(saveData.worldId ?? string.Empty, world, StringComparison.Ordinal)) errors.Add($"Conversation save world '{saveData.worldId}' does not match expected world '{world}'.");
                if (registry == null) errors.Add("Conversation save validation requires a DefinitionRegistry.");

                HashSet<string> conversationIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (ConversationRecordData record in saveData.conversations ?? new List<ConversationRecordData>())
                {
                    if (record == null) continue;
                    if (string.IsNullOrWhiteSpace(record.conversationId)) errors.Add("Conversation record has no ID.");
                    else if (!conversationIds.Add(record.conversationId)) errors.Add($"Duplicate Conversation record '{record.conversationId}'.");
                    if (string.IsNullOrWhiteSpace(record.conversationDefinitionId)) errors.Add($"Conversation '{record.conversationId}' has no definition ID.");
                    else if (registry != null && !registry.TryGet(record.conversationDefinitionId, out ConversationDefinition _)) errors.Add($"Conversation '{record.conversationId}' references missing Conversation definition '{record.conversationDefinitionId}'.");
                    if (record.lifecycleState == ConversationLifecycleState.Unknown) errors.Add($"Conversation '{record.conversationId}' has unknown lifecycle state.");
                    if (record.visibility == ConversationVisibility.Unknown) errors.Add($"Conversation '{record.conversationId}' has unknown visibility.");
                    if (!string.Equals(record.worldId ?? string.Empty, world, StringComparison.Ordinal)) errors.Add($"Conversation '{record.conversationId}' belongs to wrong world '{record.worldId}'.");
                    if ((record.participants ?? Array.Empty<ConversationParticipantRecordData>()).Length == 0) errors.Add($"Conversation '{record.conversationId}' has no participants.");
                    foreach (ConversationParticipantRecordData participant in record.participants ?? Array.Empty<ConversationParticipantRecordData>())
                    {
                        if (participant == null) continue;
                        if (string.IsNullOrWhiteSpace(participant.personId)) errors.Add($"Conversation '{record.conversationId}' has a participant with no Person ID.");
                        if (participant.role == ConversationParticipantRole.Unknown) errors.Add($"Conversation '{record.conversationId}' has participant '{participant.personId}' with unknown role.");
                    }
                }

                foreach (ConversationEventData evt in saveData.events ?? new List<ConversationEventData>())
                {
                    if (evt == null) continue;
                    if (string.IsNullOrWhiteSpace(evt.eventId)) errors.Add("Conversation event has no ID.");
                    if (evt.eventKind == ConversationEventKind.Unknown) errors.Add($"Conversation event '{evt.eventId}' has unknown kind.");
                    if (!string.IsNullOrWhiteSpace(evt.conversationId) && !conversationIds.Contains(evt.conversationId)) warnings.Add($"Conversation event '{evt.eventId}' references missing Conversation '{evt.conversationId}'.");
                }
            }

            report = new ConversationRuntimeValidationReport(errors, warnings);
            failure = string.Join(" | ", report.Errors);
            return report.Succeeded;
        }

        public void Dispose()
        {
            disposed = true;
            EventCommitted = null;
        }

        private ConversationOperationResult ValidateDefinitionRules(ConversationDefinition definition, ConversationStartRequest request, ConversationParticipantRecordData[] participants)
        {
            foreach (ConversationParticipantRole role in definition.RequiredRoles)
            {
                if (!participants.Any(value => value.role == role)) return Fail(ConversationOperationStatus.MissingParticipant, $"Conversation definition '{definition.Id}' requires participant role '{role}'.");
            }

            foreach (ConversationProviderRequirementData requirement in definition.ProviderRequirements)
            {
                if (requirement.kind == ConversationProviderRequirementKind.None) continue;
                if (!ProviderRequirementSatisfied(requirement, request, participants)) return Fail(ConversationOperationStatus.MissingProvider, $"Conversation definition '{definition.Id}' requires provider '{requirement.kind}:{requirement.requirementId}'.");
            }

            if (!CoLocationSatisfied(definition.CoLocationPolicy, request, participants)) return Fail(ConversationOperationStatus.CoLocationRejected, "Conversation participants do not satisfy co-location requirements.");
            if (!OverlapSatisfied(definition.OverlapPolicy, participants)) return Fail(ConversationOperationStatus.OverlapRejected, "One or more participants are already in an active overlapping conversation.");
            if (definition.SupportedQuestSourceDefinitionIds.Count > 0 && string.IsNullOrWhiteSpace(request.questSourceId) && string.IsNullOrWhiteSpace(request.questId)) return Fail(ConversationOperationStatus.MissingContext, "Conversation requires quest source or quest context.");
            return null;
        }

        private bool ProviderRequirementSatisfied(ConversationProviderRequirementData requirement, ConversationStartRequest request, ConversationParticipantRecordData[] participants)
        {
            string id = N(requirement.requirementId);
            return requirement.kind switch
            {
                ConversationProviderRequirementKind.Person => participants.Any(value => value.role == ConversationParticipantRole.Provider && string.Equals(value.personId, id, StringComparison.Ordinal)),
                ConversationProviderRequirementKind.Organization => string.Equals(N(request.operatingOrganizationId), id, StringComparison.Ordinal) || participants.Any(value => string.Equals(value.representedOrganizationId, id, StringComparison.Ordinal)),
                ConversationProviderRequirementKind.OrganizationMembership => string.Equals(N(request.operatingOrganizationId), id, StringComparison.Ordinal) || participants.Any(value => string.Equals(value.representedOrganizationId, id, StringComparison.Ordinal)),
                ConversationProviderRequirementKind.Office => string.Equals(N(request.operatingOfficeId), id, StringComparison.Ordinal) || participants.Any(value => string.Equals(value.representedOfficeId, id, StringComparison.Ordinal)),
                ConversationProviderRequirementKind.Authority => (request.tagIds ?? Array.Empty<string>()).Contains(id, StringComparer.Ordinal) || participants.Any(value => string.Equals(value.provenanceId, id, StringComparison.Ordinal)),
                ConversationProviderRequirementKind.Government => string.Equals(N(request.operatingGovernmentId), id, StringComparison.Ordinal) || participants.Any(value => string.Equals(value.representedGovernmentId, id, StringComparison.Ordinal)),
                ConversationProviderRequirementKind.Faction => string.Equals(N(request.operatingFactionId), id, StringComparison.Ordinal) || participants.Any(value => string.Equals(value.representedFactionId, id, StringComparison.Ordinal)),
                ConversationProviderRequirementKind.Business => string.Equals(N(request.operatingBusinessId), id, StringComparison.Ordinal) || participants.Any(value => string.Equals(value.representedBusinessId, id, StringComparison.Ordinal)),
                ConversationProviderRequirementKind.Custom => (request.tagIds ?? Array.Empty<string>()).Contains(id, StringComparer.Ordinal),
                _ => false
            };
        }

        private static bool CoLocationSatisfied(ConversationCoLocationPolicy policy, ConversationStartRequest request, ConversationParticipantRecordData[] participants)
        {
            if (policy == ConversationCoLocationPolicy.NotRequired || policy == ConversationCoLocationPolicy.RemoteAllowed || policy == ConversationCoLocationPolicy.PrivilegedBypass) return true;
            if (policy == ConversationCoLocationPolicy.SameLocation)
            {
                string location = N(request.hostLocationId);
                return !string.IsNullOrWhiteSpace(location) && participants.All(value => string.IsNullOrWhiteSpace(value.currentLocationId) || string.Equals(value.currentLocationId, location, StringComparison.Ordinal));
            }

            if (policy == ConversationCoLocationPolicy.SameInteractionPoint)
            {
                string point = N(request.hostInteractionPointId);
                return !string.IsNullOrWhiteSpace(point) && participants.All(value => string.IsNullOrWhiteSpace(value.currentInteractionPointId) || string.Equals(value.currentInteractionPointId, point, StringComparison.Ordinal));
            }

            return false;
        }

        private bool OverlapSatisfied(ConversationOverlapPolicy policy, ConversationParticipantRecordData[] participants)
        {
            if (policy == ConversationOverlapPolicy.AllowConcurrent) return true;
            HashSet<string> ids = new HashSet<string>(participants.Select(value => value.personId).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            foreach (ConversationRecordData conversation in conversationsById.Values.Where(value => !IsInactive(value.lifecycleState)))
            {
                if (policy == ConversationOverlapPolicy.PreventParticipantOverlap && (conversation.participants ?? Array.Empty<ConversationParticipantRecordData>()).Any(value => ids.Contains(value.personId))) return false;
                if (policy == ConversationOverlapPolicy.PreventProviderOverlap && (conversation.participants ?? Array.Empty<ConversationParticipantRecordData>()).Any(value => ids.Contains(value.personId) && value.role == ConversationParticipantRole.Provider)) return false;
            }

            return true;
        }

        private ConversationProjection Project(ConversationRecordData record, ConversationQuery query)
        {
            if (!CanSee(record, query.access, query.requesterPersonId)) return null;
            bool redacted = ShouldRedact(record.visibility, query.access, query.requesterPersonId, record);
            ConversationRecordData projected = record.Clone();
            if (redacted)
            {
                projected.participants = projected.participants.Where(value => !value.hidden && (query.access == ConversationAccessLevel.PrivilegedDiagnostic || string.Equals(value.personId, N(query.requesterPersonId), StringComparison.Ordinal))).ToArray();
                projected.subjectLinks = projected.subjectLinks.Where(value => !value.hidden).ToArray();
                projected.provenanceId = string.Empty;
            }

            return new ConversationProjection(new ConversationSnapshot(projected), redacted, false);
        }

        private static bool CanSee(ConversationRecordData record, ConversationAccessLevel access, string requesterPersonId)
        {
            if (access == ConversationAccessLevel.PrivilegedDiagnostic) return true;
            if (record.visibility == ConversationVisibility.Public || record.visibility == ConversationVisibility.LocallyKnown) return true;
            bool participant = HasParticipant(record, requesterPersonId);
            if (record.visibility == ConversationVisibility.ParticipantKnown || record.visibility == ConversationVisibility.Private) return participant || access == ConversationAccessLevel.Participant;
            if (record.visibility == ConversationVisibility.OrganizationMembers || record.visibility == ConversationVisibility.GovernmentOfficial || record.visibility == ConversationVisibility.OfficeRestricted) return access == ConversationAccessLevel.ControllingEntity || participant;
            return false;
        }

        private static bool ShouldRedact(ConversationVisibility visibility, ConversationAccessLevel access, string requesterPersonId, ConversationRecordData record)
        {
            if (access == ConversationAccessLevel.PrivilegedDiagnostic) return false;
            return visibility == ConversationVisibility.Private
                || visibility == ConversationVisibility.Secret
                || visibility == ConversationVisibility.Hidden
                || visibility == ConversationVisibility.OfficeRestricted
                || visibility == ConversationVisibility.GovernmentOfficial
                || ((visibility == ConversationVisibility.ParticipantKnown || visibility == ConversationVisibility.OrganizationMembers) && !HasParticipant(record, requesterPersonId));
        }

        private static bool HasParticipant(ConversationRecordData record, string personId)
        {
            string id = N(personId);
            return !string.IsNullOrWhiteSpace(id) && (record.participants ?? Array.Empty<ConversationParticipantRecordData>()).Any(value => string.Equals(value.personId, id, StringComparison.Ordinal));
        }

        private ConversationParticipantRecordData[] NormalizeParticipants(IEnumerable<ConversationParticipantRecordData> participants)
        {
            return (participants ?? Array.Empty<ConversationParticipantRecordData>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.personId))
                .Select((value, index) =>
                {
                    ConversationParticipantRecordData clone = value.Clone();
                    clone.personId = N(clone.personId);
                    clone.participantId = string.IsNullOrWhiteSpace(clone.participantId) ? $"conversation-participant.{clone.personId}.{index:000}" : N(clone.participantId);
                    clone.role = clone.role == ConversationParticipantRole.Unknown ? ConversationParticipantRole.Listener : clone.role;
                    return clone;
                })
                .OrderBy(value => value.StableKey, StringComparer.Ordinal)
                .ToArray();
        }

        private static ConversationSubjectLinkData[] NormalizeSubjects(IEnumerable<ConversationSubjectLinkData> links, ConversationStartRequest request)
        {
            List<ConversationSubjectLinkData> normalized = (links ?? Array.Empty<ConversationSubjectLinkData>()).Where(value => value != null).Select(value => value.Clone()).ToList();
            if (!string.IsNullOrWhiteSpace(request.questId))
            {
                normalized.Add(new ConversationSubjectLinkData { role = ConversationSubjectRole.Quest, questId = N(request.questId), subject = QuestInformationSubject.Quest(N(request.questId), string.Empty) });
            }
            if (!string.IsNullOrWhiteSpace(request.questSourceId))
            {
                normalized.Add(new ConversationSubjectLinkData { role = ConversationSubjectRole.QuestSource, questSourceId = N(request.questSourceId), subject = QuestInformationSubject.Source(N(request.questSourceId)) });
            }
            if (!string.IsNullOrWhiteSpace(request.questListingId))
            {
                normalized.Add(new ConversationSubjectLinkData { role = ConversationSubjectRole.QuestListing, questListingId = N(request.questListingId), subject = QuestInformationSubject.Listing(N(request.questListingId), N(request.questSourceId), N(request.questId)) });
            }

            int i = 0;
            return normalized
                .Select(value =>
                {
                    value.linkId = string.IsNullOrWhiteSpace(value.linkId) ? $"conversation-subject.{++i:000}" : N(value.linkId);
                    value.questId = N(value.questId);
                    value.questSourceId = N(value.questSourceId);
                    value.questListingId = N(value.questListingId);
                    value.locationId = N(value.locationId);
                    value.interactionPointId = N(value.interactionPointId);
                    value.role = value.role == ConversationSubjectRole.Unknown ? ConversationSubjectRole.Information : value.role;
                    value.subject ??= new InformationSubjectReferenceData();
                    return value;
                })
                .GroupBy(value => value.StableKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(value => value.StableKey, StringComparer.Ordinal)
                .ToArray();
        }

        private void RecordTransaction(string transactionId, string operation, string conversationId, ConversationOperationStatus status)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactionsById[transactionId] = new ConversationTransactionData { transactionId = transactionId, operation = operation, conversationId = conversationId, status = status, runtimeRevision = revision };
        }

        private ConversationEventData RecordEvent(string transactionId, ConversationEventKind kind, string conversationId, string personId, ConversationLifecycleState beforeState, ConversationLifecycleState afterState, double worldTime, string provenanceId)
        {
            ConversationEventData evt = new ConversationEventData
            {
                eventId = $"conversation-event.{events.Count + 1:000000}.{kind.ToString().ToLowerInvariant()}",
                transactionId = transactionId,
                eventKind = kind,
                conversationId = conversationId,
                personId = personId ?? string.Empty,
                beforeState = beforeState,
                afterState = afterState,
                worldTime = worldTime,
                runtimeRevision = revision,
                provenanceId = provenanceId ?? string.Empty
            };
            events.Add(evt.Clone());
            return evt;
        }

        private bool TryDuplicate(string transactionId, out ConversationOperationResult result)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || !transactionsById.TryGetValue(transactionId, out ConversationTransactionData transaction))
            {
                result = null;
                return false;
            }

            conversationsById.TryGetValue(transaction.conversationId, out ConversationRecordData record);
            result = ConversationOperationResult.Success("Duplicate Conversation transaction ignored.", revision, revision, record, duplicate: true);
            return true;
        }

        private bool ValidateRevision(long expectedRevision, out ConversationOperationResult failure)
        {
            if (expectedRevision >= 0L && expectedRevision != revision)
            {
                failure = Fail(ConversationOperationStatus.RevisionConflict, $"Expected revision {expectedRevision}, actual {revision}.");
                return false;
            }

            failure = null;
            return true;
        }

        private void RebuildIndexes()
        {
            conversationsByPerson.Clear();
            conversationsByLocation.Clear();
            conversationsByInteractionPoint.Clear();
            conversationsByQuest.Clear();
            conversationsBySource.Clear();
            conversationsByListing.Clear();
            conversationsByOrganization.Clear();
            conversationsByOffice.Clear();
            foreach (ConversationRecordData record in conversationsById.Values)
            {
                AddIndex(conversationsByLocation, record.hostLocationId, record.conversationId);
                AddIndex(conversationsByInteractionPoint, record.hostInteractionPointId, record.conversationId);
                AddIndex(conversationsByQuest, record.questId, record.conversationId);
                AddIndex(conversationsBySource, record.questSourceId, record.conversationId);
                AddIndex(conversationsByListing, record.questListingId, record.conversationId);
                AddIndex(conversationsByOrganization, record.operatingOrganizationId, record.conversationId);
                AddIndex(conversationsByOffice, record.operatingOfficeId, record.conversationId);
                foreach (ConversationParticipantRecordData participant in record.participants ?? Array.Empty<ConversationParticipantRecordData>())
                {
                    AddIndex(conversationsByPerson, participant.personId, record.conversationId);
                }
            }
        }

        private static void AddIndex(IDictionary<string, List<string>> index, string key, string id)
        {
            key = N(key);
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!index.TryGetValue(key, out List<string> ids))
            {
                ids = new List<string>();
                index[key] = ids;
            }
            if (!ids.Contains(id)) ids.Add(id);
            ids.Sort(StringComparer.Ordinal);
        }

        private ConversationOperationResult Fail(ConversationOperationStatus status, string message)
        {
            return ConversationOperationResult.Failure(status, message, revision);
        }

        private static bool IsInactive(ConversationLifecycleState state)
        {
            return state == ConversationLifecycleState.Completed || state == ConversationLifecycleState.Cancelled || state == ConversationLifecycleState.Interrupted || state == ConversationLifecycleState.Expired || state == ConversationLifecycleState.Historical || state == ConversationLifecycleState.Invalid;
        }

        private static string BuildConversationId(string definitionId, string interactionPointId, string questId, string transactionId)
        {
            string seed = QuestRuntimeModelUtility.Clean(new[] { definitionId, interactionPointId, questId, transactionId }).DefaultIfEmpty(Guid.NewGuid().ToString("N")).Aggregate((a, b) => $"{a}.{b}");
            return $"conversation.{Sanitize(seed)}";
        }

        private static string Sanitize(string value)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            return new string(value.Select(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' ? ch : '-').ToArray()).Trim('-');
        }

        private static string N(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
