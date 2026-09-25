using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;

namespace UnityIsekaiGame.Social.Rumors
{
    public sealed class RumorRuntime : IDisposable
    {
        private readonly Dictionary<string, RumorRecordData> rumorsById = new Dictionary<string, RumorRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RumorTransmissionRecordData> transmissionsById = new Dictionary<string, RumorTransmissionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RumorProcessedTransactionData> processedTransactions = new Dictionary<string, RumorProcessedTransactionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> rumorIdsByRoot = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> rumorIdsByParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> rumorIdsByClaim = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> transmissionIdsByRumor = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> transmissionIdsByRoot = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> transmissionIdsBySpeaker = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> transmissionIdsByListener = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> transmissionIdsByEvent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> awareRumorIdsByPerson = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private Func<string, PersonKnowledgeRuntime> knowledgeProvider;
        private Func<string, PersonMemoryRuntime> memoryProvider;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => registry != null && !disposed;
        public int RumorCount => rumorsById.Count;
        public int TransmissionCount => transmissionsById.Count;

        public void Configure(DefinitionRegistry definitionRegistry, IEnumerable<string> knownPersons = null, Func<string, PersonKnowledgeRuntime> personKnowledgeProvider = null, Func<string, PersonMemoryRuntime> personMemoryProvider = null)
        {
            registry = definitionRegistry ?? registry;
            knownPersonIds = new HashSet<string>((knownPersons ?? knownPersonIds).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            knowledgeProvider = personKnowledgeProvider ?? knowledgeProvider;
            memoryProvider = personMemoryProvider ?? memoryProvider;
        }

        public RumorOperationResult CreateRumor(RumorCreateRequest request)
        {
            long priorRevision = Revision;
            if (request != null
                && !request.Preview
                && !string.IsNullOrWhiteSpace(request.TransactionId)
                && processedTransactions.TryGetValue(request.TransactionId, out RumorProcessedTransactionData processedCreate))
            {
                return DuplicateResult(processedCreate, request.TransactionId);
            }

            if (!ValidateCreate(request, out RumorDefinition definition, out string failure, out RumorOperationStatus status))
            {
                return RumorOperationResult.Failure(status, failure, request?.TransactionId, request?.Preview ?? false, Revision);
            }

            RumorRecordData data = new RumorRecordData
            {
                rumorId = request.RumorId,
                rootRumorId = request.RumorId,
                parentRumorId = string.Empty,
                definitionId = request.DefinitionId,
                claim = request.Claim.Clone(),
                claimIdentity = KnowledgeProposition.BuildIdentity(request.Claim),
                subjectIds = ClaimSubjects(request.Claim),
                originatorPersonId = request.OriginatorPersonId ?? string.Empty,
                originatingEventId = request.OriginatingEventId ?? string.Empty,
                originatingEvidenceId = request.OriginatingEvidenceId ?? string.Empty,
                sourceAttributionPersonId = string.IsNullOrWhiteSpace(request.SourceAttributionPersonId) ? request.OriginatorPersonId ?? string.Empty : request.SourceAttributionPersonId,
                sourceNamed = request.SourceNamed,
                confidence = KnowledgeConfidence.Clamp(request.Confidence),
                salience = KnowledgeConfidence.Clamp(request.Salience),
                memorability = KnowledgeConfidence.Clamp(request.Memorability),
                disclosure = request.DisclosureOverride ?? definition.DefaultDisclosure,
                authenticity = request.Authenticity,
                originCategory = request.OriginCategory,
                distortionOperations = Array.Empty<RumorDistortionOperation>(),
                derivationReason = request.OriginCategory.ToString(),
                creationWorldTime = Math.Max(0d, request.WorldTime),
                revision = Revision + (request.Preview ? 0L : 1L),
                lifecycleState = RumorLifecycleState.Active,
                tags = Clean(request.Tags)
            };

            RumorSnapshot snapshot = new RumorSnapshot(data);
            if (request.Preview)
            {
                return RumorOperationResult.Success("Rumor creation preview succeeded.", request.TransactionId, snapshot, null, null, null, priorRevision, priorRevision, preview: true);
            }

            rumorsById[data.rumorId] = data.Clone();
            AddRumorIndexes(data);
            RememberAwareness(data.originatorPersonId, data.rumorId);
            Revision++;
            IsDirty = true;
            processedTransactions[request.TransactionId] = new RumorProcessedTransactionData { transactionId = request.TransactionId, status = RumorOperationStatus.Succeeded, rumorId = data.rumorId, revision = Revision };
            return RumorOperationResult.Success("Rumor created.", request.TransactionId, new RumorSnapshot(data), null, null, null, priorRevision, Revision);
        }

        public RumorOperationResult Transmit(RumorTransmissionRequest request)
        {
            long priorRevision = Revision;
            RumorRuntimeSaveData rollback = CreateSaveData();
            if (request != null
                && !request.Preview
                && !string.IsNullOrWhiteSpace(request.TransactionId)
                && processedTransactions.TryGetValue(request.TransactionId, out RumorProcessedTransactionData processedTransmit))
            {
                return DuplicateResult(processedTransmit, request.TransactionId);
            }

            if (!ValidateTransmission(request, out RumorRecordData sourceRumor, out RumorDefinition definition, out RumorCommunicationChannelDefinition channel, out string failure, out RumorOperationStatus status))
            {
                return RumorOperationResult.Failure(status, failure, request?.TransactionId, request?.Preview ?? false, Revision);
            }

            bool alreadyAware = IsAwareOfRoot(request.ListenerPersonId, sourceRumor.rootRumorId);
            RumorRecordData deliveredRumor = sourceRumor;
            if (RequiresDistortion(request.RequestedDistortionPolicy, definition))
            {
                deliveredRumor = CreateDerivedRumor(sourceRumor, definition, request);
            }

            RumorTransmissionOutcome outcome = alreadyAware && request.RequestedOutcome == RumorTransmissionOutcome.Heard
                ? RumorTransmissionOutcome.AlreadyKnown
                : NormalizeOutcome(request.RequestedOutcome);
            KnowledgeOperationResult knowledgeResult = null;
            HistoryOperationResult memoryResult = null;
            string evidenceId = string.Empty;
            string beliefId = string.Empty;
            string memoryId = string.Empty;

            bool shouldCreateKnowledge = request.CreateKnowledgeEvidence && outcome != RumorTransmissionOutcome.AlreadyKnown && outcome != RumorTransmissionOutcome.BlockedByDisclosure;
            bool shouldCreateMemory = request.CreateMemory && outcome != RumorTransmissionOutcome.BlockedByDisclosure;

            if (shouldCreateKnowledge)
            {
                knowledgeResult = ApplyKnowledge(request, deliveredRumor, outcome, preview: true);
                if (knowledgeResult != null && !knowledgeResult.Succeeded)
                {
                    RestoreInternal(rollback, registry, knownPersonIds);
                    return RumorOperationResult.Failure(RumorOperationStatus.KnowledgeRejected, knowledgeResult.Message, request.TransactionId, request.Preview, Revision);
                }

                evidenceId = knowledgeResult?.Evidence?.EvidenceId ?? string.Empty;
                beliefId = knowledgeResult?.ResultingBelief?.BeliefId ?? string.Empty;
            }

            if (shouldCreateMemory)
            {
                memoryResult = ApplyMemory(request, deliveredRumor, evidenceId, beliefId, preview: true);
                if (memoryResult != null && !memoryResult.Succeeded)
                {
                    RestoreInternal(rollback, registry, knownPersonIds);
                    return RumorOperationResult.Failure(RumorOperationStatus.MemoryRejected, memoryResult.Message, request.TransactionId, request.Preview, Revision);
                }

                memoryId = memoryResult?.Memory?.MemoryId ?? string.Empty;
            }

            if (!request.Preview && shouldCreateKnowledge)
            {
                knowledgeResult = ApplyKnowledge(request, deliveredRumor, outcome, preview: false);
                if (knowledgeResult != null && !knowledgeResult.Succeeded)
                {
                    RestoreInternal(rollback, registry, knownPersonIds);
                    return RumorOperationResult.Failure(RumorOperationStatus.KnowledgeRejected, knowledgeResult.Message, request.TransactionId, request.Preview, Revision);
                }

                evidenceId = knowledgeResult?.Evidence?.EvidenceId ?? string.Empty;
                beliefId = knowledgeResult?.ResultingBelief?.BeliefId ?? string.Empty;
            }

            if (!request.Preview && shouldCreateMemory)
            {
                memoryResult = ApplyMemory(request, deliveredRumor, evidenceId, beliefId, preview: false);
                if (memoryResult != null && !memoryResult.Succeeded)
                {
                    RestoreInternal(rollback, registry, knownPersonIds);
                    return RumorOperationResult.Failure(RumorOperationStatus.MemoryRejected, memoryResult.Message, request.TransactionId, request.Preview, Revision);
                }

                memoryId = memoryResult?.Memory?.MemoryId ?? string.Empty;
            }

            RumorTransmissionRecordData transmission = new RumorTransmissionRecordData
            {
                transmissionId = request.TransmissionId,
                transactionId = request.TransactionId,
                rumorVersionId = sourceRumor.rumorId,
                rootRumorId = sourceRumor.rootRumorId,
                speakerPersonId = request.SpeakerPersonId,
                listenerPersonId = request.ListenerPersonId,
                transmissionWorldTime = Math.Max(0d, request.WorldTime),
                channelId = request.ChannelId,
                placeId = request.PlaceId ?? string.Empty,
                interactionContextId = request.InteractionContextId ?? string.Empty,
                sourceNamed = request.NameSource && definition.AnonymousSourcingAllowed,
                speakerConfidence = KnowledgeConfidence.Clamp(request.SpeakerConfidence),
                outcome = outcome,
                resultingRumorVersionId = deliveredRumor.rumorId,
                evidenceId = evidenceId,
                beliefId = beliefId,
                memoryId = memoryId,
                failureReason = string.Empty,
                revision = Revision + (request.Preview ? 0L : 1L)
            };

            if (request.Preview)
            {
                RestoreInternal(rollback, registry, knownPersonIds);
                return RumorOperationResult.Success("Rumor transmission preview succeeded.", request.TransactionId, new RumorSnapshot(deliveredRumor), new RumorTransmissionSnapshot(transmission), knowledgeResult, memoryResult, priorRevision, priorRevision, preview: true);
            }

            if (!rumorsById.ContainsKey(deliveredRumor.rumorId))
            {
                rumorsById[deliveredRumor.rumorId] = deliveredRumor.Clone();
                AddRumorIndexes(deliveredRumor);
            }

            transmissionsById[transmission.transmissionId] = transmission.Clone();
            AddTransmissionIndexes(transmission);
            RememberAwareness(request.ListenerPersonId, deliveredRumor.rumorId);
            Revision++;
            IsDirty = true;
            processedTransactions[request.TransactionId] = new RumorProcessedTransactionData { transactionId = request.TransactionId, status = RumorOperationStatus.Succeeded, rumorId = deliveredRumor.rumorId, transmissionId = transmission.transmissionId, revision = Revision };
            return RumorOperationResult.Success("Rumor transmission recorded.", request.TransactionId, new RumorSnapshot(deliveredRumor), new RumorTransmissionSnapshot(transmission), knowledgeResult, memoryResult, priorRevision, Revision);
        }

        public RumorPropagationResult Propagate(RumorPropagationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return new RumorPropagationResult(request?.TransactionId, false, request?.Preview ?? false, Array.Empty<RumorOperationResult>(), "Propagation requires a transaction ID.");
            }

            string[] listeners = (request.ListenerPersonIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .Take(Math.Max(0, request.MaximumTransmissions))
                .ToArray();
            List<RumorOperationResult> results = new List<RumorOperationResult>();
            int index = 0;
            foreach (string listener in listeners)
            {
                results.Add(Transmit(new RumorTransmissionRequest
                {
                    TransactionId = $"{request.TransactionId}.listener.{index:D3}",
                    TransmissionId = $"rumor-transmission.{DeterministicToken(request.TransactionId + listener)}",
                    RumorVersionId = request.RumorVersionId,
                    SpeakerPersonId = request.SpeakerPersonId,
                    ListenerPersonId = listener,
                    WorldTime = request.WorldTime,
                    ChannelId = request.ChannelId,
                    RequestedDistortionPolicy = RumorDistortionPolicy.None,
                    DeterministicSeed = $"{request.DeterministicSeed}.{index}",
                    Preview = request.Preview
                }));
                index++;
            }

            return new RumorPropagationResult(request.TransactionId, results.All(result => result.Succeeded), request.Preview, results, "Bounded propagation completed.");
        }

        public bool TryGetRumor(string rumorId, out RumorSnapshot snapshot)
        {
            if (rumorsById.TryGetValue(rumorId ?? string.Empty, out RumorRecordData data))
            {
                snapshot = new RumorSnapshot(data);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetTransmission(string transmissionId, out RumorTransmissionSnapshot snapshot)
        {
            if (transmissionsById.TryGetValue(transmissionId ?? string.Empty, out RumorTransmissionRecordData data))
            {
                snapshot = new RumorTransmissionSnapshot(data);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool IsAware(string personId, string rumorId)
        {
            return awareRumorIdsByPerson.TryGetValue(personId ?? string.Empty, out HashSet<string> ids) && ids.Contains(rumorId ?? string.Empty);
        }

        public bool IsAwareOfRoot(string personId, string rootRumorId)
        {
            return awareRumorIdsByPerson.TryGetValue(personId ?? string.Empty, out HashSet<string> ids)
                && ids.Any(id => rumorsById.TryGetValue(id, out RumorRecordData rumor) && string.Equals(rumor.rootRumorId, rootRumorId, StringComparison.Ordinal));
        }

        public IReadOnlyList<RumorSnapshot> QueryByRoot(string rootRumorId)
        {
            return QueryRumors(rumorIdsByRoot, rootRumorId);
        }

        public IReadOnlyList<RumorSnapshot> QueryByClaim(string claimIdentity)
        {
            return QueryRumors(rumorIdsByClaim, claimIdentity);
        }

        public IReadOnlyList<RumorTransmissionSnapshot> QueryTransmissionsByRoot(string rootRumorId)
        {
            return QueryTransmissions(transmissionIdsByRoot, rootRumorId);
        }

        public IReadOnlyList<RumorTransmissionSnapshot> QueryTransmissionsBySpeaker(string personId)
        {
            return QueryTransmissions(transmissionIdsBySpeaker, personId);
        }

        public IReadOnlyList<RumorTransmissionSnapshot> QueryTransmissionsByListener(string personId)
        {
            return QueryTransmissions(transmissionIdsByListener, personId);
        }

        public IReadOnlyList<RumorTransmissionSnapshot> QueryTransmissionsByEvent(string eventId)
        {
            return QueryTransmissions(transmissionIdsByEvent, eventId);
        }

        public RumorPropagationMetrics GetMetrics(string rootRumorId)
        {
            int versions = QueryByRoot(rootRumorId).Count;
            IReadOnlyList<RumorTransmissionSnapshot> transmissions = QueryTransmissionsByRoot(rootRumorId);
            HashSet<string> aware = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, HashSet<string>> pair in awareRumorIdsByPerson)
            {
                if (pair.Value.Any(id => rumorsById.TryGetValue(id, out RumorRecordData rumor) && string.Equals(rumor.rootRumorId, rootRumorId, StringComparison.Ordinal)))
                {
                    aware.Add(pair.Key);
                }
            }

            return new RumorPropagationMetrics(
                rootRumorId,
                versions,
                transmissions.Count,
                aware.Count,
                transmissions.Count(item => item.Outcome == RumorTransmissionOutcome.Believed),
                transmissions.Count(item => item.Outcome == RumorTransmissionOutcome.Uncertain || item.Outcome == RumorTransmissionOutcome.PartiallyBelieved),
                transmissions.Count(item => item.Outcome == RumorTransmissionOutcome.Rejected || item.Outcome == RumorTransmissionOutcome.ContradictedByExistingBelief));
        }

        public RumorRuntimeSaveData CreateSaveData()
        {
            return new RumorRuntimeSaveData
            {
                schemaVersion = RumorRuntimeSaveData.CurrentSchemaVersion,
                revision = Revision,
                rumors = rumorsById.Values.OrderBy(item => item.rumorId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                transmissions = transmissionsById.Values.OrderBy(item => item.transmissionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                processedTransactions = processedTransactions.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray()
            };
        }

        public int PruneHistory(int maximumTransmissions)
        {
            string[] remove = transmissionsById.Values
                .OrderByDescending(item => item.transmissionWorldTime)
                .ThenByDescending(item => item.revision)
                .Skip(Math.Max(0, maximumTransmissions))
                .Select(item => item.transmissionId)
                .ToArray();
            foreach (string id in remove) transmissionsById.Remove(id);
            HashSet<string> retained = new HashSet<string>(transmissionsById.Keys, StringComparer.Ordinal);
            foreach (string tx in processedTransactions.Values.Where(item => !string.IsNullOrWhiteSpace(item.transmissionId) && !retained.Contains(item.transmissionId)).Select(item => item.transactionId).ToArray()) processedTransactions.Remove(tx);
            if (remove.Length > 0) { Revision++; IsDirty = true; RebuildIndexes(); }
            return remove.Length;
        }

        public RumorOperationResult RestoreFromSaveData(RumorRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> knownPersons, bool restoringState = false)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, knownPersons, out string failure))
            {
                return RumorOperationResult.Failure(RumorOperationStatus.RestoreFailed, failure, revision: Revision);
            }

            RestoreInternal(saveData, definitionRegistry, knownPersons);
            IsDirty = !restoringState;
            return RumorOperationResult.Success("Rumors restored.", string.Empty, null, null, null, null, Revision, Revision);
        }

        public static bool ValidateSaveData(RumorRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> knownPersons, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Rumor save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > RumorRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported Rumor schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> known = new HashSet<string>((knownPersons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            HashSet<string> rumorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RumorRecordData rumor in saveData.rumors ?? Array.Empty<RumorRecordData>())
            {
                if (rumor == null || string.IsNullOrWhiteSpace(rumor.rumorId) || !rumorIds.Add(rumor.rumorId))
                {
                    failure = $"Missing or duplicate rumor ID '{rumor?.rumorId}'.";
                    return false;
                }

                if (definitionRegistry == null || !definitionRegistry.TryGet(rumor.definitionId, out RumorDefinition _))
                {
                    failure = $"Rumor '{rumor.rumorId}' references missing definition '{rumor.definitionId}'.";
                    return false;
                }

                if (known.Count > 0 && !string.IsNullOrWhiteSpace(rumor.originatorPersonId) && !known.Contains(rumor.originatorPersonId))
                {
                    failure = $"Rumor '{rumor.rumorId}' references unknown originator '{rumor.originatorPersonId}'.";
                    return false;
                }

                if (definitionRegistry == null || !definitionRegistry.TryGet(rumor.claim?.factDefinitionId, out KnowledgeFactDefinition fact) || !KnowledgeProposition.Validate(rumor.claim, fact, out failure))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Rumor '{rumor.rumorId}' has an invalid claim." : failure;
                    return false;
                }
            }

            foreach (RumorRecordData rumor in saveData.rumors ?? Array.Empty<RumorRecordData>())
            {
                if (!string.Equals(rumor.rumorId, rumor.rootRumorId, StringComparison.Ordinal) && !rumorIds.Contains(rumor.rootRumorId ?? string.Empty))
                {
                    failure = $"Rumor '{rumor.rumorId}' references missing root '{rumor.rootRumorId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(rumor.parentRumorId) && !rumorIds.Contains(rumor.parentRumorId))
                {
                    failure = $"Rumor '{rumor.rumorId}' references missing parent '{rumor.parentRumorId}'.";
                    return false;
                }
            }

            HashSet<string> transmissionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RumorTransmissionRecordData transmission in saveData.transmissions ?? Array.Empty<RumorTransmissionRecordData>())
            {
                if (transmission == null || string.IsNullOrWhiteSpace(transmission.transmissionId) || !transmissionIds.Add(transmission.transmissionId))
                {
                    failure = $"Missing or duplicate rumor transmission ID '{transmission?.transmissionId}'.";
                    return false;
                }

                if (!rumorIds.Contains(transmission.rumorVersionId ?? string.Empty) || !rumorIds.Contains(transmission.resultingRumorVersionId ?? string.Empty))
                {
                    failure = $"Transmission '{transmission.transmissionId}' references a missing rumor version.";
                    return false;
                }

                if (known.Count > 0 && (!known.Contains(transmission.speakerPersonId ?? string.Empty) || !known.Contains(transmission.listenerPersonId ?? string.Empty)))
                {
                    failure = $"Transmission '{transmission.transmissionId}' references an unknown speaker or listener.";
                    return false;
                }
            }

            return true;
        }

        public void Clear()
        {
            rumorsById.Clear();
            transmissionsById.Clear();
            processedTransactions.Clear();
            ClearIndexes();
            Revision = 0L;
            IsDirty = false;
        }

        public void Dispose()
        {
            disposed = true;
            Clear();
        }

        private bool ValidateCreate(RumorCreateRequest request, out RumorDefinition definition, out string failure, out RumorOperationStatus status)
        {
            definition = null;
            failure = string.Empty;
            status = RumorOperationStatus.InvalidRequest;
            if (disposed)
            {
                status = RumorOperationStatus.Disposed;
                failure = "Rumor runtime is disposed.";
                return false;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId) || string.IsNullOrWhiteSpace(request.RumorId))
            {
                failure = "Rumor creation requires transaction and rumor IDs.";
                return false;
            }

            if (!request.Preview && rumorsById.ContainsKey(request.RumorId))
            {
                status = RumorOperationStatus.DuplicateRumor;
                failure = $"Rumor '{request.RumorId}' already exists.";
                return false;
            }

            if (!TryKnownPerson(request.OriginatorPersonId))
            {
                status = RumorOperationStatus.MissingPerson;
                failure = $"Rumor creation references unknown originator '{request.OriginatorPersonId}'.";
                return false;
            }

            if (registry == null || !registry.TryGet(request.DefinitionId, out definition))
            {
                status = RumorOperationStatus.MissingDefinition;
                failure = $"Rumor definition '{request.DefinitionId}' is missing.";
                return false;
            }

            if (!registry.TryGet(request.Claim?.factDefinitionId, out KnowledgeFactDefinition fact) || !KnowledgeProposition.Validate(request.Claim, fact, out failure))
            {
                status = RumorOperationStatus.InvalidRequest;
                failure = string.IsNullOrWhiteSpace(failure) ? "Rumor claim is invalid." : failure;
                return false;
            }

            status = RumorOperationStatus.Succeeded;
            return true;
        }

        private bool ValidateTransmission(RumorTransmissionRequest request, out RumorRecordData rumor, out RumorDefinition definition, out RumorCommunicationChannelDefinition channel, out string failure, out RumorOperationStatus status)
        {
            rumor = null;
            definition = null;
            channel = null;
            failure = string.Empty;
            status = RumorOperationStatus.InvalidRequest;
            if (disposed)
            {
                status = RumorOperationStatus.Disposed;
                failure = "Rumor runtime is disposed.";
                return false;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId) || string.IsNullOrWhiteSpace(request.TransmissionId))
            {
                failure = "Rumor transmission requires transaction and transmission IDs.";
                return false;
            }

            if (!request.Preview && transmissionsById.ContainsKey(request.TransmissionId))
            {
                status = RumorOperationStatus.DuplicateTransmission;
                failure = $"Rumor transmission '{request.TransmissionId}' already exists.";
                return false;
            }

            if (!rumorsById.TryGetValue(request.RumorVersionId ?? string.Empty, out rumor))
            {
                status = RumorOperationStatus.MissingRumor;
                failure = $"Rumor '{request.RumorVersionId}' is missing.";
                return false;
            }

            if (!TryKnownPerson(request.SpeakerPersonId) || !TryKnownPerson(request.ListenerPersonId) || string.Equals(request.SpeakerPersonId, request.ListenerPersonId, StringComparison.Ordinal))
            {
                status = RumorOperationStatus.MissingPerson;
                failure = "Rumor transmission requires distinct known speaker and listener Persons.";
                return false;
            }

            if (!IsAware(request.SpeakerPersonId, rumor.rumorId) && !IsAwareOfRoot(request.SpeakerPersonId, rumor.rootRumorId))
            {
                status = RumorOperationStatus.SpeakerUnaware;
                failure = $"Speaker '{request.SpeakerPersonId}' is not aware of rumor '{rumor.rumorId}'.";
                return false;
            }

            if (registry == null || !registry.TryGet(rumor.definitionId, out definition))
            {
                status = RumorOperationStatus.MissingDefinition;
                failure = $"Rumor definition '{rumor.definitionId}' is missing.";
                return false;
            }

            if (registry == null || !registry.TryGet(request.ChannelId, out channel))
            {
                status = RumorOperationStatus.MissingDefinition;
                failure = $"Rumor channel '{request.ChannelId}' is missing.";
                return false;
            }

            if (rumor.disclosure >= RumorDisclosure.Private && !channel.SupportsPrivateRumors && !request.BypassDisclosure)
            {
                status = RumorOperationStatus.DisclosureBlocked;
                failure = $"Channel '{channel.Id}' cannot carry private rumor '{rumor.rumorId}'.";
                return false;
            }

            if (!request.NameSource && !definition.AnonymousSourcingAllowed)
            {
                status = RumorOperationStatus.DisclosureBlocked;
                failure = $"Rumor definition '{definition.Id}' does not allow anonymous sourcing.";
                return false;
            }

            if (request.WorldTime < rumor.creationWorldTime)
            {
                failure = "Rumor transmission cannot occur before rumor creation.";
                return false;
            }

            status = RumorOperationStatus.Succeeded;
            return true;
        }

        private RumorRecordData CreateDerivedRumor(RumorRecordData source, RumorDefinition definition, RumorTransmissionRequest request)
        {
            RumorDistortionOperation operation = DistortionOperation(request.RequestedDistortionPolicy == RumorDistortionPolicy.None ? definition.DefaultDistortionPolicy : request.RequestedDistortionPolicy, request.DeterministicSeed);
            string derivedId = string.IsNullOrWhiteSpace(request.DerivedRumorId)
                ? $"rumor.version.{DeterministicToken($"{source.rumorId}.{request.TransactionId}.{operation}")}"
                : request.DerivedRumorId;
            if (rumorsById.TryGetValue(derivedId, out RumorRecordData existing))
            {
                return existing.Clone();
            }

            RumorRecordData derived = source.Clone();
            derived.rumorId = derivedId;
            derived.parentRumorId = source.rumorId;
            derived.rootRumorId = source.rootRumorId;
            derived.creationWorldTime = Math.Max(source.creationWorldTime, request.WorldTime);
            derived.revision = Revision + 1L;
            derived.derivationReason = operation.ToString();
            derived.distortionOperations = source.distortionOperations.Concat(new[] { operation }).Where(item => item != RumorDistortionOperation.None).ToArray();
            if (operation == RumorDistortionOperation.ConfidenceDecreased || operation == RumorDistortionOperation.UncertaintyAdded)
            {
                derived.confidence = KnowledgeConfidence.Clamp(derived.confidence - 100);
            }
            else if (operation == RumorDistortionOperation.ConfidenceIncreased || operation == RumorDistortionOperation.UncertaintyRemoved)
            {
                derived.confidence = KnowledgeConfidence.Clamp(derived.confidence + 100);
            }
            else if (operation == RumorDistortionOperation.SourceConcealed)
            {
                derived.sourceNamed = false;
            }

            return derived;
        }

        private KnowledgeOperationResult ApplyKnowledge(RumorTransmissionRequest request, RumorRecordData rumor, RumorTransmissionOutcome outcome, bool preview)
        {
            PersonKnowledgeRuntime knowledge = knowledgeProvider?.Invoke(request.ListenerPersonId);
            if (knowledge == null)
            {
                return null;
            }

            int strength = outcome switch
            {
                RumorTransmissionOutcome.Believed => 650,
                RumorTransmissionOutcome.PartiallyBelieved => 450,
                RumorTransmissionOutcome.Uncertain => 300,
                RumorTransmissionOutcome.Rejected => 250,
                RumorTransmissionOutcome.ContradictedByExistingBelief => 300,
                _ => Math.Max(150, request.SpeakerConfidence / 2)
            };
            KnowledgeObservationRequest observation = new KnowledgeObservationRequest
            {
                PersonId = request.ListenerPersonId,
                TransactionId = $"{request.TransactionId}.knowledge",
                Proposition = rumor.claim.Clone(),
                AcquisitionSource = KnowledgeAcquisitionSource.Testimony,
                Provenance = KnowledgeProvenance.Testimony,
                Direction = outcome == RumorTransmissionOutcome.Rejected || outcome == RumorTransmissionOutcome.ContradictedByExistingBelief ? KnowledgeEvidenceDirection.Opposes : KnowledgeEvidenceDirection.Supports,
                Strength = strength,
                Credibility = KnowledgeConfidence.Clamp(request.SpeakerConfidence),
                GameTimeSeconds = request.WorldTime,
                SourceId = request.SpeakerPersonId,
                EvidenceId = $"evidence.{request.TransmissionId}",
                Visibility = rumor.disclosure >= RumorDisclosure.Private ? KnowledgeVisibility.Private : KnowledgeVisibility.Public,
                PrivateAccessAuthorized = request.BypassDisclosure,
                RelatedEventId = rumor.originatingEventId,
                Tags = new[] { "rumor", rumor.rumorId, rumor.rootRumorId, request.TransmissionId }
            };

            return preview ? knowledge.PreviewObservation(observation) : knowledge.RecordObservation(observation);
        }

        private HistoryOperationResult ApplyMemory(RumorTransmissionRequest request, RumorRecordData rumor, string evidenceId, string beliefId, bool preview)
        {
            PersonMemoryRuntime memory = memoryProvider?.Invoke(request.ListenerPersonId);
            if (memory == null)
            {
                return null;
            }

            return memory.FormMemory(new FormMemoryRequest
            {
                TransactionId = $"{request.TransactionId}.memory",
                MemoryId = $"memory.{request.TransmissionId}",
                OwnerPersonId = request.ListenerPersonId,
                HistoricalEventId = string.Empty,
                BeliefId = beliefId,
                EvidenceIds = string.IsNullOrWhiteSpace(evidenceId) ? Array.Empty<string>() : new[] { evidenceId },
                Source = HistoryMemorySource.WitnessTestimony,
                FormedAtWorldTime = request.WorldTime,
                RememberedOccurredAtWorldTime = Math.Min(request.WorldTime, Math.Max(0d, rumor.creationWorldTime)),
                Confidence = rumor.confidence,
                Clarity = 550,
                Salience = rumor.salience,
                FirstHand = false,
                Visibility = rumor.disclosure >= RumorDisclosure.Private ? KnowledgeVisibility.Private : KnowledgeVisibility.Public,
                DebugDescription = $"Heard rumor '{rumor.rumorId}' from '{request.SpeakerPersonId}'.",
                Tags = new[] { "rumor", rumor.rumorId, rumor.rootRumorId, request.TransmissionId },
                CreateKnowledgeEvidence = false
            }, null, preview: preview, restoring: false);
        }

        private void RestoreInternal(RumorRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> knownPersons)
        {
            registry = definitionRegistry ?? registry;
            knownPersonIds = new HashSet<string>((knownPersons ?? knownPersonIds).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            rumorsById.Clear();
            transmissionsById.Clear();
            processedTransactions.Clear();
            ClearIndexes();

            foreach (RumorRecordData rumor in saveData.rumors ?? Array.Empty<RumorRecordData>())
            {
                rumorsById[rumor.rumorId] = rumor.Clone();
                AddRumorIndexes(rumor);
                RememberAwareness(rumor.originatorPersonId, rumor.rumorId);
            }

            foreach (RumorTransmissionRecordData transmission in saveData.transmissions ?? Array.Empty<RumorTransmissionRecordData>())
            {
                transmissionsById[transmission.transmissionId] = transmission.Clone();
                AddTransmissionIndexes(transmission);
                RememberAwareness(transmission.listenerPersonId, transmission.resultingRumorVersionId);
            }

            foreach (RumorProcessedTransactionData transaction in saveData.processedTransactions ?? Array.Empty<RumorProcessedTransactionData>())
            {
                processedTransactions[transaction.transactionId ?? string.Empty] = transaction.Clone();
            }

            Revision = saveData.revision;
            IsDirty = false;
        }

        private RumorOperationResult DuplicateResult(RumorProcessedTransactionData processed, string transactionId)
        {
            RumorSnapshot rumor = !string.IsNullOrWhiteSpace(processed.rumorId) && rumorsById.TryGetValue(processed.rumorId, out RumorRecordData rumorData) ? new RumorSnapshot(rumorData) : null;
            RumorTransmissionSnapshot transmission = !string.IsNullOrWhiteSpace(processed.transmissionId) && transmissionsById.TryGetValue(processed.transmissionId, out RumorTransmissionRecordData transmissionData) ? new RumorTransmissionSnapshot(transmissionData) : null;
            return RumorOperationResult.Success("Rumor transaction already processed.", transactionId, rumor, transmission, null, null, Revision, Revision, duplicate: true);
        }

        private void AddRumorIndexes(RumorRecordData rumor)
        {
            AddIndex(rumorIdsByRoot, rumor.rootRumorId, rumor.rumorId);
            AddIndex(rumorIdsByParent, rumor.parentRumorId, rumor.rumorId);
            AddIndex(rumorIdsByClaim, rumor.claimIdentity, rumor.rumorId);
        }

        private void AddTransmissionIndexes(RumorTransmissionRecordData transmission)
        {
            AddIndex(transmissionIdsByRumor, transmission.rumorVersionId, transmission.transmissionId);
            AddIndex(transmissionIdsByRoot, transmission.rootRumorId, transmission.transmissionId);
            AddIndex(transmissionIdsBySpeaker, transmission.speakerPersonId, transmission.transmissionId);
            AddIndex(transmissionIdsByListener, transmission.listenerPersonId, transmission.transmissionId);
            if (rumorsById.TryGetValue(transmission.resultingRumorVersionId, out RumorRecordData rumor) && !string.IsNullOrWhiteSpace(rumor.originatingEventId))
            {
                AddIndex(transmissionIdsByEvent, rumor.originatingEventId, transmission.transmissionId);
            }
        }

        private IReadOnlyList<RumorSnapshot> QueryRumors(Dictionary<string, List<string>> index, string key)
        {
            return index.TryGetValue(key ?? string.Empty, out List<string> ids)
                ? ids.Where(id => rumorsById.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).Select(id => new RumorSnapshot(rumorsById[id])).ToArray()
                : Array.Empty<RumorSnapshot>();
        }

        private IReadOnlyList<RumorTransmissionSnapshot> QueryTransmissions(Dictionary<string, List<string>> index, string key)
        {
            return index.TryGetValue(key ?? string.Empty, out List<string> ids)
                ? ids.Where(id => transmissionsById.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).Select(id => new RumorTransmissionSnapshot(transmissionsById[id])).ToArray()
                : Array.Empty<RumorTransmissionSnapshot>();
        }

        private void RememberAwareness(string personId, string rumorId)
        {
            if (string.IsNullOrWhiteSpace(personId) || string.IsNullOrWhiteSpace(rumorId))
            {
                return;
            }

            if (!awareRumorIdsByPerson.TryGetValue(personId, out HashSet<string> ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                awareRumorIdsByPerson[personId] = ids;
            }

            ids.Add(rumorId);
        }

        private bool TryKnownPerson(string personId)
        {
            return !string.IsNullOrWhiteSpace(personId) && (knownPersonIds.Count == 0 || knownPersonIds.Contains(personId));
        }

        private static bool RequiresDistortion(RumorDistortionPolicy policy, RumorDefinition definition)
        {
            RumorDistortionPolicy resolved = policy == RumorDistortionPolicy.None ? definition.DefaultDistortionPolicy : policy;
            return resolved != RumorDistortionPolicy.None;
        }

        private static RumorDistortionOperation DistortionOperation(RumorDistortionPolicy policy, string seed)
        {
            return policy switch
            {
                RumorDistortionPolicy.ForcedConfidenceDecrease => RumorDistortionOperation.ConfidenceDecreased,
                RumorDistortionPolicy.ForcedConfidenceIncrease => RumorDistortionOperation.ConfidenceIncreased,
                RumorDistortionPolicy.ForcedAnonymousSource => RumorDistortionOperation.SourceConcealed,
                RumorDistortionPolicy.DeterministicMetadataOnly => (DeterministicToken(seed).Length % 2 == 0 ? RumorDistortionOperation.UncertaintyAdded : RumorDistortionOperation.UncertaintyRemoved),
                _ => RumorDistortionOperation.None
            };
        }

        private static RumorTransmissionOutcome NormalizeOutcome(RumorTransmissionOutcome outcome)
        {
            return outcome == RumorTransmissionOutcome.NotDelivered || outcome == RumorTransmissionOutcome.Invalid
                ? RumorTransmissionOutcome.Heard
                : outcome;
        }

        private static string[] ClaimSubjects(KnowledgePropositionData claim)
        {
            return new[] { claim?.subjectId, claim?.objectId, claim?.locationContextId, claim?.sourceContextId }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static void AddIndex(Dictionary<string, List<string>> index, string key, string id)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (!index.TryGetValue(key, out List<string> values))
            {
                values = new List<string>();
                index[key] = values;
            }

            if (!values.Contains(id, StringComparer.Ordinal))
            {
                values.Add(id);
                values.Sort(StringComparer.Ordinal);
            }
        }

        private void ClearIndexes()
        {
            rumorIdsByRoot.Clear();
            rumorIdsByParent.Clear();
            rumorIdsByClaim.Clear();
            transmissionIdsByRumor.Clear();
            transmissionIdsByRoot.Clear();
            transmissionIdsBySpeaker.Clear();
            transmissionIdsByListener.Clear();
            transmissionIdsByEvent.Clear();
            awareRumorIdsByPerson.Clear();
        }

        private void RebuildIndexes()
        {
            ClearIndexes();
            foreach (RumorRecordData rumor in rumorsById.Values)
            {
                AddRumorIndexes(rumor);
                RememberAwareness(rumor.originatorPersonId, rumor.rumorId);
            }
            foreach (RumorTransmissionRecordData transmission in transmissionsById.Values)
            {
                AddTransmissionIndexes(transmission);
                RememberAwareness(transmission.listenerPersonId, transmission.resultingRumorVersionId);
            }
        }

        private static string DeterministicToken(string seed)
        {
            using MD5 md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(seed ?? string.Empty));
            return new Guid(bytes).ToString("N");
        }
    }
}
