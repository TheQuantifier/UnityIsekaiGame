using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Social.Interactions
{
    public sealed class SocialInteractionRuntime : IDisposable
    {
        private readonly Dictionary<string, SocialInteractionRecordData> recordsById = new Dictionary<string, SocialInteractionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialPendingInteractionData> pendingById = new Dictionary<string, SocialPendingInteractionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialPromiseData> promisesById = new Dictionary<string, SocialPromiseData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialInteractionProcessedTransactionData> processedTransactions = new Dictionary<string, SocialInteractionProcessedTransactionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialInteractionCooldownData> cooldownsByKey = new Dictionary<string, SocialInteractionCooldownData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> recordIdsByPerson = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> recordIdsByDefinition = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private RelationshipRuntime relationships;
        private InterpersonalAttitudeRuntime attitudes;
        private ReputationRuntime reputation;
        private RumorRuntime rumors;
        private bool disposed;
        private bool restoring;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => registry != null && !disposed;
        public int Count => recordsById.Count;
        public int PendingCount => pendingById.Count;
        public int PromiseCount => promisesById.Count;
        public IReadOnlyList<SocialInteractionSnapshot> Snapshots => Ordered(recordsById.Values).Select(record => new SocialInteractionSnapshot(record)).ToArray();
        public bool KnowsPerson(string personId) => !string.IsNullOrWhiteSpace(personId) && knownPersonIds.Contains(personId.Trim());

        public void Configure(
            DefinitionRegistry definitionRegistry,
            IEnumerable<string> knownPersons,
            RelationshipRuntime relationshipRuntime = null,
            InterpersonalAttitudeRuntime attitudeRuntime = null,
            ReputationRuntime reputationRuntime = null,
            RumorRuntime rumorRuntime = null)
        {
            registry = definitionRegistry ?? registry;
            knownPersonIds = new HashSet<string>((knownPersons ?? knownPersonIds).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
            relationships = relationshipRuntime ?? relationships;
            attitudes = attitudeRuntime ?? attitudes;
            reputation = reputationRuntime ?? reputation;
            rumors = rumorRuntime ?? rumors;
            disposed = false;
            RebuildIndexes();
        }

        public SocialInteractionResult Preview(SocialInteractionRequest request)
        {
            SocialInteractionRequest clone = request?.Clone() ?? new SocialInteractionRequest();
            clone.Preview = true;
            return Execute(clone);
        }

        public SocialInteractionResult Execute(SocialInteractionRequest request)
        {
            request ??= new SocialInteractionRequest();
            long before = Revision;
            if (!IsReady || restoring)
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.RuntimeNotReady, "Social Interaction runtime is not ready.", request.TransactionId, before);
            }

            string transactionId = Clean(request.TransactionId);
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.MissingTransactionId, "Social interaction requires a transaction ID.", transactionId, before);
            }

            if (!request.Preview && processedTransactions.TryGetValue(transactionId, out SocialInteractionProcessedTransactionData processed))
            {
                SocialInteractionSnapshot duplicateSnapshot = TryGetSnapshot(processed.interactionRecordId, out SocialInteractionSnapshot existing) ? existing : null;
                return SocialInteractionResult.Success(SocialInteractionStatus.Duplicate, "Social interaction transaction was already processed.", transactionId, duplicateSnapshot, null, null, Array.Empty<SocialConsequenceRecordData>(), before, before, duplicate: true);
            }

            if (!ValidateRequest(request, out SocialInteractionDefinition definition, out SocialInteractionStatus status, out string failure))
            {
                return SocialInteractionResult.Failure(status, failure, transactionId, before);
            }

            string recordId = string.IsNullOrWhiteSpace(request.InteractionRecordId)
                ? BuildStableId("social-interaction-record", transactionId)
                : Clean(request.InteractionRecordId);
            if (!request.Preview && recordsById.ContainsKey(recordId))
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.DuplicateRecordId, $"Social Interaction record '{recordId}' already exists.", transactionId, before);
            }

            SocialInteractionOutcome outcome = ResolveOutcome(definition, request.Response);
            int roll = DeterministicRoll(definition.Id, transactionId, request.DeterministicSeed, request.InitiatorPersonId, request.TargetPersonId, request.WorldTime);
            SocialConsequenceRecordData[] plan = BuildConsequencePlan(definition, request, recordId, outcome).ToArray();
            SocialInteractionRecordData record = BuildRecord(definition, request, recordId, outcome, roll, plan);
            SocialPendingInteractionData pending = null;
            SocialPromiseData promise = null;

            if (definition.RequiresResponse && request.Response == SocialInteractionResponse.None)
            {
                pending = CreatePending(definition, request, recordId, transactionId);
                record.pendingInteractionId = pending.pendingInteractionId;
                record.outcome = SocialInteractionOutcome.Pending;
                record.consequences = Array.Empty<SocialConsequenceRecordData>();
            }

            if (request.Preview)
            {
                return SocialInteractionResult.Success(SocialInteractionStatus.Preview, "Social interaction preview succeeded.", transactionId, new SocialInteractionSnapshot(record), pending == null ? null : new SocialPendingInteractionSnapshot(pending), null, record.consequences, before, before, preview: true);
            }

            if (!TryCheckCooldown(definition, request, before, out SocialInteractionResult cooldownFailure))
            {
                return cooldownFailure;
            }

            SocialInteractionRuntimeSaveData rollback = CreateSaveData();
            RelationshipRuntimeSaveData relationshipRollback = plan.Any(item => item.targetRuntime == SocialConsequenceTargetRuntime.Relationship) ? relationships?.CreateSaveData() : null;
            InterpersonalAttitudeRuntimeSaveData attitudeRollback = plan.Any(item => item.targetRuntime == SocialConsequenceTargetRuntime.Attitude) ? attitudes?.CreateSaveData() : null;
            ReputationRuntimeSaveData reputationRollback = plan.Any(item => item.targetRuntime == SocialConsequenceTargetRuntime.Reputation) ? reputation?.CreateSaveData() : null;
            RumorRuntimeSaveData rumorRollback = plan.Any(item => item.targetRuntime == SocialConsequenceTargetRuntime.Rumor) ? rumors?.CreateSaveData() : null;

            if (pending == null && !CommitConsequences(record, request, out promise, out SocialInteractionStatus consequenceStatus, out string consequenceFailure))
            {
                RestoreInternal(rollback);
                RestoreExternal(relationshipRollback, attitudeRollback, reputationRollback, rumorRollback);
                return SocialInteractionResult.Failure(consequenceStatus, consequenceFailure, transactionId, before);
            }

            if (promise != null)
            {
                record.promiseId = promise.promiseId;
            }

            recordsById[record.interactionRecordId] = record.Clone();
            if (pending != null)
            {
                pendingById[pending.pendingInteractionId] = pending.Clone();
            }

            if (promise != null)
            {
                promisesById[promise.promiseId] = promise.Clone();
            }

            Revision++;
            record.revision = Revision;
            recordsById[record.interactionRecordId] = record.Clone();
            processedTransactions[transactionId] = new SocialInteractionProcessedTransactionData
            {
                transactionId = transactionId,
                interactionRecordId = record.interactionRecordId,
                status = SocialInteractionStatus.Succeeded,
                revision = Revision
            };
            StoreCooldown(definition, request, record.interactionRecordId);
            IsDirty = true;
            RebuildIndexes();

            SocialInteractionStatus resultStatus = pending == null ? SocialInteractionStatus.Succeeded : SocialInteractionStatus.Pending;
            return SocialInteractionResult.Success(resultStatus, pending == null ? "Social interaction executed." : "Social interaction is pending a response.", transactionId, new SocialInteractionSnapshot(record), pending == null ? null : new SocialPendingInteractionSnapshot(pending), promise == null ? null : new SocialPromiseSnapshot(promise), record.consequences, before, Revision);
        }

        public SocialInteractionResult RespondToPending(string transactionId, string pendingInteractionId, SocialInteractionResponse response, double worldTime, string deterministicSeed = "", bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.MissingTransactionId, "Pending response requires a transaction ID.", transactionId, before);
            }

            if (string.IsNullOrWhiteSpace(pendingInteractionId) || !pendingById.TryGetValue(pendingInteractionId.Trim(), out SocialPendingInteractionData pending))
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.PendingNotFound, $"Pending social interaction '{pendingInteractionId}' does not exist.", transactionId, before);
            }

            if (pending.status != SocialInteractionStatus.Pending)
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.PendingAlreadyResolved, $"Pending social interaction '{pendingInteractionId}' is already resolved.", transactionId, before);
            }

            if (pending.expirationWorldTime >= 0d && worldTime > pending.expirationWorldTime)
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.PendingExpired, $"Pending social interaction '{pendingInteractionId}' has expired.", transactionId, before);
            }

            if (!pending.availableResponses.Contains(response))
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.InvalidResponse, $"Response '{response}' is not allowed for pending social interaction '{pendingInteractionId}'.", transactionId, before);
            }

            SocialInteractionRequest request = new SocialInteractionRequest
            {
                TransactionId = transactionId,
                InteractionRecordId = BuildStableId("social-interaction-response", $"{pendingInteractionId}.{transactionId}"),
                InteractionDefinitionId = pending.interactionDefinitionId,
                InitiatorPersonId = pending.initiatorPersonId,
                TargetPersonId = pending.targetPersonId,
                Subject = pending.subject?.Clone() ?? new SocialInteractionSubjectData(),
                Response = response,
                WorldTime = worldTime,
                DeterministicSeed = deterministicSeed,
                OriginatingReferenceId = pending.interactionRecordId,
                Preview = preview
            };

            SocialInteractionRuntimeSaveData rollback = CreateSaveData();
            SocialInteractionResult result = Execute(request);
            if (!result.Succeeded || preview)
            {
                RestoreInternal(rollback);
                return result;
            }

            SocialPendingInteractionData committedPending = pendingById[pendingInteractionId.Trim()];
            committedPending.status = response == SocialInteractionResponse.Accept || response == SocialInteractionResponse.Forgive || response == SocialInteractionResponse.Acknowledge
                ? SocialInteractionStatus.Succeeded
                : SocialInteractionStatus.Refused;
            committedPending.revision++;
            Revision++;
            IsDirty = true;
            return result;
        }

        public SocialInteractionResult ResolvePromise(string transactionId, string promiseId, SocialPromiseStatus status, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.MissingTransactionId, "Promise resolution requires a transaction ID.", transactionId, before);
            }

            if (string.IsNullOrWhiteSpace(promiseId) || !promisesById.TryGetValue(promiseId.Trim(), out SocialPromiseData promise))
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.InvalidRequest, $"Promise '{promiseId}' does not exist.", transactionId, before);
            }

            SocialPromiseData resolved = promise.Clone();
            resolved.status = status;
            resolved.resolvedWorldTime = worldTime;
            resolved.resolvedByInteractionRecordId = transactionId;
            resolved.revision++;
            if (preview)
            {
                return SocialInteractionResult.Success(SocialInteractionStatus.Preview, "Promise resolution preview succeeded.", transactionId, null, null, new SocialPromiseSnapshot(resolved), Array.Empty<SocialConsequenceRecordData>(), before, before, preview: true);
            }

            promisesById[promiseId.Trim()] = resolved;
            Revision++;
            IsDirty = true;
            return SocialInteractionResult.Success(SocialInteractionStatus.Succeeded, "Promise resolved.", transactionId, null, null, new SocialPromiseSnapshot(resolved), Array.Empty<SocialConsequenceRecordData>(), before, Revision);
        }

        public bool TryGetSnapshot(string interactionRecordId, out SocialInteractionSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(interactionRecordId) && recordsById.TryGetValue(interactionRecordId.Trim(), out SocialInteractionRecordData record))
            {
                snapshot = new SocialInteractionSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetPending(string pendingInteractionId, out SocialPendingInteractionSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(pendingInteractionId) && pendingById.TryGetValue(pendingInteractionId.Trim(), out SocialPendingInteractionData pending))
            {
                snapshot = new SocialPendingInteractionSnapshot(pending);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetPromise(string promiseId, out SocialPromiseSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(promiseId) && promisesById.TryGetValue(promiseId.Trim(), out SocialPromiseData promise))
            {
                snapshot = new SocialPromiseSnapshot(promise);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<SocialInteractionSnapshot> QueryByPerson(string personId)
        {
            if (string.IsNullOrWhiteSpace(personId) || !recordIdsByPerson.TryGetValue(personId.Trim(), out List<string> ids))
            {
                return Array.Empty<SocialInteractionSnapshot>();
            }

            return ids.Where(recordsById.ContainsKey).Select(id => new SocialInteractionSnapshot(recordsById[id])).ToArray();
        }

        public IReadOnlyList<SocialInteractionSnapshot> QueryByDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || !recordIdsByDefinition.TryGetValue(definitionId.Trim(), out List<string> ids))
            {
                return Array.Empty<SocialInteractionSnapshot>();
            }

            return ids.Where(recordsById.ContainsKey).Select(id => new SocialInteractionSnapshot(recordsById[id])).ToArray();
        }

        public SocialInteractionRuntimeSaveData CreateSaveData()
        {
            return new SocialInteractionRuntimeSaveData
            {
                schemaVersion = SocialInteractionRuntimeSaveData.CurrentSchemaVersion,
                revision = Revision,
                records = Ordered(recordsById.Values).Select(record => record.Clone()).ToList(),
                pendingInteractions = pendingById.Values.OrderBy(item => item.pendingInteractionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                promises = promisesById.Values.OrderBy(item => item.promiseId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                processedTransactions = processedTransactions.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                cooldowns = cooldownsByKey.Values.OrderBy(item => item.cooldownKey, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public int PruneHistory(int maximumRecords, int maximumTransactions)
        {
            maximumRecords = Math.Max(0, maximumRecords);
            maximumTransactions = Math.Max(maximumRecords, maximumTransactions);
            HashSet<string> protectedRecordIds = new HashSet<string>(
                pendingById.Values.Where(item => item.status == SocialInteractionStatus.Pending).Select(item => item.interactionRecordId)
                    .Concat(promisesById.Values.Where(item => item.status == SocialPromiseStatus.Proposed).Select(item => item.sourceInteractionRecordId)),
                StringComparer.Ordinal);
            string[] removable = recordsById.Values
                .Where(item => !protectedRecordIds.Contains(item.interactionRecordId))
                .OrderByDescending(item => item.worldTime)
                .ThenByDescending(item => item.revision)
                .Skip(Math.Max(0, maximumRecords - protectedRecordIds.Count))
                .Select(item => item.interactionRecordId)
                .ToArray();
            foreach (string recordId in removable)
            {
                recordsById.Remove(recordId);
            }

            HashSet<string> retainedRecordIds = new HashSet<string>(recordsById.Keys, StringComparer.Ordinal);
            foreach (string transactionId in processedTransactions.Values
                .Where(item => !retainedRecordIds.Contains(item.interactionRecordId))
                .OrderByDescending(item => item.revision)
                .Skip(maximumTransactions)
                .Select(item => item.transactionId)
                .ToArray())
            {
                processedTransactions.Remove(transactionId);
            }

            foreach (string cooldownKey in cooldownsByKey.Values
                .Where(item => !retainedRecordIds.Contains(item.sourceInteractionRecordId))
                .Select(item => item.cooldownKey)
                .ToArray())
            {
                cooldownsByKey.Remove(cooldownKey);
            }

            if (removable.Length > 0)
            {
                Revision++;
                IsDirty = true;
                RebuildIndexes();
            }

            return removable.Length;
        }

        public SocialInteractionResult RestoreFromSaveData(SocialInteractionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoringState = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failureReason))
            {
                return SocialInteractionResult.Failure(SocialInteractionStatus.RestoreFailed, failureReason, string.Empty, before);
            }

            Configure(definitionRegistry, persons, relationships, attitudes, reputation, rumors);
            restoring = true;
            RestoreInternal(saveData ?? new SocialInteractionRuntimeSaveData());
            restoring = false;
            IsDirty = !restoringState;
            return SocialInteractionResult.Success(SocialInteractionStatus.Succeeded, "Social interactions restored.", string.Empty, null, null, null, Array.Empty<SocialConsequenceRecordData>(), before, Revision);
        }

        public static bool ValidateSaveData(SocialInteractionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, out string failureReason)
        {
            failureReason = string.Empty;
            SocialInteractionRuntimeSaveData effective = saveData ?? new SocialInteractionRuntimeSaveData();
            if (effective.schemaVersion != SocialInteractionRuntimeSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Social Interaction save schema version {effective.schemaVersion}.";
                return false;
            }

            if (definitionRegistry == null)
            {
                failureReason = "Social Interaction runtime requires a definition registry.";
                return false;
            }

            HashSet<string> known = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialInteractionRecordData record in effective.records ?? new List<SocialInteractionRecordData>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.interactionRecordId) || !ids.Add(record.interactionRecordId))
                {
                    failureReason = $"Social Interaction save contains duplicate or empty record ID '{record?.interactionRecordId}'.";
                    return false;
                }

                if (!definitionRegistry.TryGet(record.interactionDefinitionId, out SocialInteractionDefinition _))
                {
                    failureReason = $"Social Interaction record '{record.interactionRecordId}' references missing definition '{record.interactionDefinitionId}'.";
                    return false;
                }

                if (!ValidateStaticPerson(record.initiatorPersonId, known, out failureReason)
                    || !ValidateStaticPerson(record.targetPersonId, known, out failureReason))
                {
                    return false;
                }

                if (!Enum.IsDefined(typeof(SocialInteractionOutcome), record.outcome)
                    || !Enum.IsDefined(typeof(SocialInteractionResponse), record.response)
                    || !Enum.IsDefined(typeof(SocialInteractionVisibility), record.visibility))
                {
                    failureReason = $"Social Interaction record '{record.interactionRecordId}' contains invalid enum data.";
                    return false;
                }
            }

            HashSet<string> pendingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialPendingInteractionData pending in effective.pendingInteractions ?? new List<SocialPendingInteractionData>())
            {
                if (pending == null || string.IsNullOrWhiteSpace(pending.pendingInteractionId) || !pendingIds.Add(pending.pendingInteractionId))
                {
                    failureReason = $"Social Interaction save contains duplicate or empty pending ID '{pending?.pendingInteractionId}'.";
                    return false;
                }

                if (!ids.Contains(pending.interactionRecordId))
                {
                    failureReason = $"Pending Social Interaction '{pending.pendingInteractionId}' references missing interaction record '{pending.interactionRecordId}'.";
                    return false;
                }
            }

            HashSet<string> promiseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialPromiseData promise in effective.promises ?? new List<SocialPromiseData>())
            {
                if (promise == null || string.IsNullOrWhiteSpace(promise.promiseId) || !promiseIds.Add(promise.promiseId))
                {
                    failureReason = $"Social Interaction save contains duplicate or empty promise ID '{promise?.promiseId}'.";
                    return false;
                }

                if (!ids.Contains(promise.sourceInteractionRecordId))
                {
                    failureReason = $"Promise '{promise.promiseId}' references missing interaction record '{promise.sourceInteractionRecordId}'.";
                    return false;
                }
            }

            return true;
        }

        public void Clear()
        {
            recordsById.Clear();
            pendingById.Clear();
            promisesById.Clear();
            processedTransactions.Clear();
            cooldownsByKey.Clear();
            RebuildIndexes();
            Revision++;
            IsDirty = true;
        }

        public void Dispose()
        {
            disposed = true;
            recordsById.Clear();
            pendingById.Clear();
            promisesById.Clear();
            processedTransactions.Clear();
            cooldownsByKey.Clear();
            RebuildIndexes();
        }

        private bool ValidateRequest(SocialInteractionRequest request, out SocialInteractionDefinition definition, out SocialInteractionStatus status, out string failure)
        {
            definition = null;
            status = SocialInteractionStatus.Succeeded;
            failure = string.Empty;
            if (registry == null)
            {
                status = SocialInteractionStatus.MissingDefinitionRegistry;
                failure = "Social Interaction runtime requires a definition registry.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.InteractionDefinitionId) || !registry.TryGet(request.InteractionDefinitionId.Trim(), out definition))
            {
                status = SocialInteractionStatus.MissingDefinition;
                failure = $"Social Interaction definition '{request.InteractionDefinitionId}' is missing.";
                return false;
            }

            if (!ValidatePerson(request.InitiatorPersonId, SocialInteractionStatus.MissingInitiator, out status, out failure)
                || !ValidatePerson(request.TargetPersonId, SocialInteractionStatus.MissingTarget, out status, out failure))
            {
                return false;
            }

            if (!definition.AllowSelfTarget && string.Equals(request.InitiatorPersonId?.Trim(), request.TargetPersonId?.Trim(), StringComparison.Ordinal))
            {
                status = SocialInteractionStatus.SelfTargetNotAllowed;
                failure = "Social interaction cannot target the initiator.";
                return false;
            }

            foreach (string witnessId in request.WitnessPersonIds ?? Array.Empty<string>())
            {
                if (!ValidatePerson(witnessId, SocialInteractionStatus.UnknownPerson, out status, out failure))
                {
                    return false;
                }
            }

            if (definition.RequiresResponse && request.Response != SocialInteractionResponse.None && !definition.AllowsResponse(request.Response))
            {
                status = SocialInteractionStatus.InvalidResponse;
                failure = $"Response '{request.Response}' is not allowed for Social Interaction definition '{definition.Id}'.";
                return false;
            }

            return true;
        }

        private bool ValidatePerson(string personId, SocialInteractionStatus missingStatus, out SocialInteractionStatus status, out string failure)
        {
            status = SocialInteractionStatus.Succeeded;
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(personId))
            {
                status = missingStatus;
                failure = "Social interaction participant Person ID is required.";
                return false;
            }

            if (knownPersonIds != null && knownPersonIds.Count > 0 && !knownPersonIds.Contains(personId.Trim()))
            {
                status = SocialInteractionStatus.UnknownPerson;
                failure = $"Person '{personId}' is unknown to the Social Interaction runtime.";
                return false;
            }

            return true;
        }

        private static bool ValidateStaticPerson(string personId, HashSet<string> known, out string failure)
        {
            if (string.IsNullOrWhiteSpace(personId))
            {
                failure = "Social Interaction save contains a blank Person ID.";
                return false;
            }

            if (known != null && known.Count > 0 && !known.Contains(personId.Trim()))
            {
                failure = $"Social Interaction save references unknown Person '{personId}'.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static SocialInteractionOutcome ResolveOutcome(SocialInteractionDefinition definition, SocialInteractionResponse response)
        {
            if (definition.RequiresResponse)
            {
                if (response == SocialInteractionResponse.Accept || response == SocialInteractionResponse.Forgive || response == SocialInteractionResponse.Acknowledge)
                {
                    return definition.AcceptedOutcome;
                }

                if (response == SocialInteractionResponse.Refuse || response == SocialInteractionResponse.Reject || response == SocialInteractionResponse.Ignore || response == SocialInteractionResponse.Deny)
                {
                    return definition.RefusedOutcome;
                }
            }

            return definition.BaseOutcome;
        }

        private SocialInteractionRecordData BuildRecord(SocialInteractionDefinition definition, SocialInteractionRequest request, string recordId, SocialInteractionOutcome outcome, int roll, IReadOnlyList<SocialConsequenceRecordData> consequences)
        {
            string initiator = Clean(request.InitiatorPersonId);
            string target = Clean(request.TargetPersonId);
            List<SocialInteractionParticipantData> participants = new List<SocialInteractionParticipantData>
            {
                new SocialInteractionParticipantData { role = SocialInteractionRole.Initiator, personId = initiator },
                new SocialInteractionParticipantData { role = SocialInteractionRole.Target, personId = target }
            };
            participants.AddRange((request.WitnessPersonIds ?? Array.Empty<string>()).Select(value => new SocialInteractionParticipantData { role = SocialInteractionRole.Witness, personId = Clean(value) }));
            return new SocialInteractionRecordData
            {
                interactionRecordId = recordId,
                transactionId = Clean(request.TransactionId),
                interactionDefinitionId = definition.Id,
                initiatorPersonId = initiator,
                targetPersonId = target,
                participants = participants.ToArray(),
                subject = request.Subject?.Clone() ?? new SocialInteractionSubjectData(),
                placeId = Clean(request.PlaceId),
                audienceId = Clean(string.IsNullOrWhiteSpace(request.AudienceId) ? PrototypeReputationDefinitionFactory.GlobalPublicAudienceId : request.AudienceId),
                channel = request.Channel,
                visibility = request.VisibilityOverride ?? definition.DefaultVisibility,
                response = request.Response,
                outcome = outcome,
                worldTime = Math.Max(0d, request.WorldTime),
                deterministicSeed = Clean(request.DeterministicSeed),
                deterministicRoll = roll,
                pendingInteractionId = string.Empty,
                promiseId = string.Empty,
                historicalEventId = $"history.social.{recordId}",
                memoryReferenceId = $"memory.social.{recordId}",
                consequences = consequences.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                diagnostics = Array.Empty<string>(),
                tags = new[] { "social-interaction", definition.Category.ToString() },
                revision = Revision + 1L
            };
        }

        private SocialPendingInteractionData CreatePending(SocialInteractionDefinition definition, SocialInteractionRequest request, string recordId, string transactionId)
        {
            return new SocialPendingInteractionData
            {
                pendingInteractionId = BuildStableId("social-pending", recordId),
                interactionRecordId = recordId,
                transactionId = transactionId,
                interactionDefinitionId = definition.Id,
                initiatorPersonId = Clean(request.InitiatorPersonId),
                targetPersonId = Clean(request.TargetPersonId),
                subject = request.Subject?.Clone() ?? new SocialInteractionSubjectData(),
                availableResponses = definition.AllowedResponses.ToArray(),
                status = SocialInteractionStatus.Pending,
                createdWorldTime = Math.Max(0d, request.WorldTime),
                expirationWorldTime = -1d,
                revision = Revision + 1L
            };
        }

        private IEnumerable<SocialConsequenceRecordData> BuildConsequencePlan(SocialInteractionDefinition definition, SocialInteractionRequest request, string recordId, SocialInteractionOutcome outcome)
        {
            foreach (SocialInteractionConsequenceDefinitionData consequence in definition.Consequences.OrderBy(item => item.consequenceId, StringComparer.Ordinal))
            {
                SocialInteractionOutcome[] appliesToOutcomes = consequence.appliesToOutcomes ?? Array.Empty<SocialInteractionOutcome>();
                if (appliesToOutcomes.Length > 0 && !appliesToOutcomes.Contains(outcome))
                {
                    continue;
                }

                SocialInteractionVisibility visibility = request.VisibilityOverride ?? definition.DefaultVisibility;
                bool witnessed = (request.WitnessPersonIds ?? Array.Empty<string>()).Any() || visibility == SocialInteractionVisibility.Witnessed || visibility == SocialInteractionVisibility.Public;
                bool isPublic = visibility == SocialInteractionVisibility.Public || !string.IsNullOrWhiteSpace(request.AudienceId);
                if (consequence.onlyWhenWitnessed && !witnessed)
                {
                    continue;
                }

                if (consequence.onlyWhenPublic && !isPublic)
                {
                    continue;
                }

                string actor = ResolveRole(request, consequence.actorRole);
                string subject = ResolveRole(request, consequence.subjectRole);
                yield return new SocialConsequenceRecordData
                {
                    consequenceId = consequence.consequenceId,
                    targetRuntime = consequence.targetRuntime,
                    operation = consequence.operation,
                    sourceId = $"{recordId}.{consequence.consequenceId}",
                    actorPersonId = actor,
                    subjectPersonId = subject,
                    dimensionId = consequence.dimensionId,
                    audienceId = string.IsNullOrWhiteSpace(consequence.audienceId) ? (string.IsNullOrWhiteSpace(request.AudienceId) ? PrototypeReputationDefinitionFactory.GlobalPublicAudienceId : request.AudienceId) : consequence.audienceId,
                    relationshipDefinitionId = consequence.relationshipDefinitionId,
                    rumorDefinitionId = consequence.rumorDefinitionId,
                    rumorChannelId = consequence.rumorChannelId,
                    affectedRecordId = string.Empty,
                    transactionId = $"{Clean(request.TransactionId)}.{consequence.consequenceId}",
                    amount = consequence.amount,
                    required = consequence.required,
                    committed = false,
                    status = SocialInteractionStatus.Pending.ToString(),
                    message = string.Empty
                };
            }
        }

        private bool CommitConsequences(SocialInteractionRecordData record, SocialInteractionRequest request, out SocialPromiseData promise, out SocialInteractionStatus status, out string failure)
        {
            promise = null;
            status = SocialInteractionStatus.Succeeded;
            failure = string.Empty;
            SocialConsequenceRecordData[] consequences = record.consequences ?? Array.Empty<SocialConsequenceRecordData>();
            for (int index = 0; index < consequences.Length; index++)
            {
                SocialConsequenceRecordData consequence = consequences[index].Clone();
                bool committed = consequence.targetRuntime switch
                {
                    SocialConsequenceTargetRuntime.Attitude => ApplyAttitude(consequence, record, preview: false, out failure),
                    SocialConsequenceTargetRuntime.Reputation => ApplyReputation(consequence, record, preview: false, out failure),
                    SocialConsequenceTargetRuntime.Relationship => ApplyRelationship(consequence, record, preview: false, out failure),
                    SocialConsequenceTargetRuntime.Rumor => ApplyRumor(consequence, record, request, preview: false, out failure),
                    SocialConsequenceTargetRuntime.Promise => ApplyPromise(consequence, record, request, out promise, out failure),
                    SocialConsequenceTargetRuntime.History or SocialConsequenceTargetRuntime.Memory => ApplyReference(consequence, record, out failure),
                    _ => true
                };

                consequence.committed = committed;
                consequence.status = committed ? SocialInteractionStatus.Succeeded.ToString() : SocialInteractionStatus.ConsequenceRejected.ToString();
                consequence.message = failure;
                consequences[index] = consequence;
                if (!committed && consequence.required)
                {
                    status = SocialInteractionStatus.ConsequenceRejected;
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Required consequence '{consequence.consequenceId}' failed." : failure;
                    record.consequences = consequences;
                    return false;
                }
            }

            record.consequences = consequences;
            return true;
        }

        private bool ApplyAttitude(SocialConsequenceRecordData consequence, SocialInteractionRecordData record, bool preview, out string failure)
        {
            failure = string.Empty;
            if (attitudes == null || !attitudes.IsReady)
            {
                failure = "Interpersonal Attitude runtime is unavailable.";
                return false;
            }

            AttitudeMutationResult result = attitudes.Mutate(new AttitudeMutationRequest
            {
                transactionId = consequence.transactionId,
                observerPersonId = consequence.actorPersonId,
                subjectPersonId = consequence.subjectPersonId,
                dimensionId = consequence.dimensionId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                value = consequence.amount,
                sourceId = consequence.sourceId,
                sourceCategory = AttitudeContributionSourceCategory.Dialogue,
                historicalEventId = record.historicalEventId,
                worldTime = record.worldTime,
                preview = preview
            });
            consequence.affectedRecordId = result.RecordId;
            failure = result.Message;
            return result.Succeeded || result.Status == AttitudeOperationStatus.Duplicate;
        }

        private bool ApplyReputation(SocialConsequenceRecordData consequence, SocialInteractionRecordData record, bool preview, out string failure)
        {
            failure = string.Empty;
            if (reputation == null || !reputation.IsReady)
            {
                failure = "Reputation runtime is unavailable.";
                return false;
            }

            bool verified = record.visibility == SocialInteractionVisibility.Public || (record.participants ?? Array.Empty<SocialInteractionParticipantData>()).Any(item => item.role == SocialInteractionRole.Witness);
            ReputationMutationResult result = reputation.Mutate(new ReputationMutationRequest
            {
                transactionId = consequence.transactionId,
                subjectPersonId = consequence.subjectPersonId,
                audienceId = consequence.audienceId,
                dimensionId = consequence.dimensionId,
                mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                value = consequence.amount,
                sourceId = consequence.sourceId,
                sourceCategory = record.visibility == SocialInteractionVisibility.Public ? ReputationContributionSourceCategory.PublicSpeech : ReputationContributionSourceCategory.WitnessedDeed,
                authenticity = verified ? ReputationAuthenticity.Verified : ReputationAuthenticity.Alleged,
                historicalEventId = record.historicalEventId,
                supportingReferenceId = record.interactionRecordId,
                worldTime = record.worldTime,
                preview = preview
            });
            consequence.affectedRecordId = result.RecordId;
            failure = result.Message;
            return result.Succeeded || result.Status == ReputationOperationStatus.Duplicate;
        }

        private bool ApplyRelationship(SocialConsequenceRecordData consequence, SocialInteractionRecordData record, bool preview, out string failure)
        {
            failure = string.Empty;
            if (relationships == null)
            {
                failure = "Relationship runtime is unavailable.";
                return false;
            }

            string relationshipId = BuildStableId("relationship.social", consequence.sourceId);
            RelationshipOperationResult result = relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = relationshipId,
                relationshipDefinitionId = string.IsNullOrWhiteSpace(consequence.relationshipDefinitionId) ? PrototypeRelationshipDefinitionFactory.FriendRelationshipId : consequence.relationshipDefinitionId,
                firstPersonId = consequence.actorPersonId,
                firstRoleId = "friend",
                secondPersonId = consequence.subjectPersonId,
                secondRoleId = "friend",
                sourceEventId = record.historicalEventId,
                sourceRecordId = record.interactionRecordId,
                startWorldTime = record.worldTime,
                tags = new[] { "social-interaction" },
                transactionId = consequence.transactionId,
                preview = preview
            });
            consequence.affectedRecordId = result.Snapshot?.RecordId ?? relationshipId;
            failure = result.Message;
            return result.Succeeded || result.Status == RelationshipOperationStatus.Duplicate;
        }

        private bool ApplyRumor(SocialConsequenceRecordData consequence, SocialInteractionRecordData record, SocialInteractionRequest request, bool preview, out string failure)
        {
            failure = string.Empty;
            if (rumors == null || !rumors.IsReady)
            {
                failure = "Rumor runtime is unavailable.";
                return false;
            }

            string rumorId = request.Subject?.kind == SocialInteractionSubjectKind.Rumor ? request.Subject.subjectId : request.Subject?.subjectId;
            if (string.IsNullOrWhiteSpace(rumorId))
            {
                failure = "Rumor consequence requires the interaction subject to reference an existing rumor.";
                return false;
            }

            RumorOperationResult result = rumors.Transmit(new RumorTransmissionRequest
            {
                TransactionId = consequence.transactionId,
                TransmissionId = BuildStableId("rumor-transmission.social", consequence.sourceId),
                RumorVersionId = rumorId,
                SpeakerPersonId = record.initiatorPersonId,
                ListenerPersonId = record.targetPersonId,
                WorldTime = record.worldTime,
                ChannelId = string.IsNullOrWhiteSpace(consequence.rumorChannelId) ? PrototypeRumorDefinitionFactory.ConversationChannelId : consequence.rumorChannelId,
                PlaceId = record.placeId,
                InteractionContextId = record.interactionRecordId,
                NameSource = true,
                SpeakerClaimsFirsthand = false,
                IntentionalSharing = true,
                BypassDisclosure = true,
                Preview = preview
            });
            consequence.affectedRecordId = result.Transmission?.TransmissionId ?? string.Empty;
            record.rumorTransmissionId = consequence.affectedRecordId;
            failure = result.Message;
            return result.Succeeded || result.Status == RumorOperationStatus.Duplicate;
        }

        private bool ApplyPromise(SocialConsequenceRecordData consequence, SocialInteractionRecordData record, SocialInteractionRequest request, out SocialPromiseData promise, out string failure)
        {
            failure = string.Empty;
            promise = new SocialPromiseData
            {
                promiseId = BuildStableId("social-promise", consequence.sourceId),
                sourceInteractionRecordId = record.interactionRecordId,
                promisorPersonId = consequence.actorPersonId,
                promiseePersonId = consequence.subjectPersonId,
                subject = request.Subject?.Clone() ?? new SocialInteractionSubjectData(),
                status = SocialPromiseStatus.Active,
                createdWorldTime = record.worldTime,
                resolvedWorldTime = -1d,
                resolvedByInteractionRecordId = string.Empty,
                revision = Revision + 1L
            };
            consequence.affectedRecordId = promise.promiseId;
            return true;
        }

        private static bool ApplyReference(SocialConsequenceRecordData consequence, SocialInteractionRecordData record, out string failure)
        {
            failure = string.Empty;
            consequence.affectedRecordId = consequence.targetRuntime == SocialConsequenceTargetRuntime.History
                ? record.historicalEventId
                : $"{record.memoryReferenceId}.{consequence.actorPersonId}";
            return true;
        }

        private bool TryCheckCooldown(SocialInteractionDefinition definition, SocialInteractionRequest request, long before, out SocialInteractionResult result)
        {
            result = null;
            if (definition.CooldownScope == SocialInteractionCooldownScope.None || definition.CooldownSeconds <= 0d)
            {
                return true;
            }

            string key = BuildCooldownKey(definition, request);
            if (cooldownsByKey.TryGetValue(key, out SocialInteractionCooldownData cooldown)
                && Math.Max(0d, request.WorldTime) < cooldown.lastWorldTime + definition.CooldownSeconds)
            {
                result = SocialInteractionResult.Failure(SocialInteractionStatus.CooldownActive, $"Social interaction cooldown '{key}' is active.", request.TransactionId, before);
                return false;
            }

            return true;
        }

        private void StoreCooldown(SocialInteractionDefinition definition, SocialInteractionRequest request, string recordId)
        {
            if (definition.CooldownScope == SocialInteractionCooldownScope.None || definition.CooldownSeconds <= 0d)
            {
                return;
            }

            string key = BuildCooldownKey(definition, request);
            cooldownsByKey[key] = new SocialInteractionCooldownData
            {
                cooldownKey = key,
                lastWorldTime = Math.Max(0d, request.WorldTime),
                sourceInteractionRecordId = recordId
            };
        }

        private static string BuildCooldownKey(SocialInteractionDefinition definition, SocialInteractionRequest request)
        {
            string initiator = Clean(request.InitiatorPersonId);
            string target = Clean(request.TargetPersonId);
            return definition.CooldownScope switch
            {
                SocialInteractionCooldownScope.InitiatorDefinition => $"{definition.Id}|{initiator}",
                SocialInteractionCooldownScope.InitiatorTargetDefinition => $"{definition.Id}|{initiator}|{target}",
                SocialInteractionCooldownScope.InitiatorTargetSubjectDefinition => $"{definition.Id}|{initiator}|{target}|{request.Subject?.kind}|{request.Subject?.subjectId}",
                _ => definition.Id
            };
        }

        private void RestoreExternal(RelationshipRuntimeSaveData relationshipSave, InterpersonalAttitudeRuntimeSaveData attitudeSave, ReputationRuntimeSaveData reputationSave, RumorRuntimeSaveData rumorSave)
        {
            if (relationships != null && relationshipSave != null)
            {
                relationships.RestoreFromSaveData(relationshipSave, registry, knownPersonIds, restoring: true);
            }

            if (attitudes != null && attitudeSave != null)
            {
                attitudes.RestoreFromSaveData(attitudeSave, registry, knownPersonIds, restoringState: true);
            }

            if (reputation != null && reputationSave != null)
            {
                reputation.RestoreFromSaveData(reputationSave, registry, knownPersonIds, restoringState: true);
            }

            if (rumors != null && rumorSave != null)
            {
                rumors.RestoreFromSaveData(rumorSave, registry, knownPersonIds, restoringState: true);
            }
        }

        private void RestoreInternal(SocialInteractionRuntimeSaveData saveData)
        {
            recordsById.Clear();
            pendingById.Clear();
            promisesById.Clear();
            processedTransactions.Clear();
            cooldownsByKey.Clear();
            foreach (SocialInteractionRecordData record in saveData?.records ?? new List<SocialInteractionRecordData>())
            {
                SocialInteractionRecordData clone = record.Clone();
                recordsById[clone.interactionRecordId] = clone;
            }

            foreach (SocialPendingInteractionData pending in saveData?.pendingInteractions ?? new List<SocialPendingInteractionData>())
            {
                SocialPendingInteractionData clone = pending.Clone();
                pendingById[clone.pendingInteractionId] = clone;
            }

            foreach (SocialPromiseData promise in saveData?.promises ?? new List<SocialPromiseData>())
            {
                SocialPromiseData clone = promise.Clone();
                promisesById[clone.promiseId] = clone;
            }

            foreach (SocialInteractionProcessedTransactionData processed in saveData?.processedTransactions ?? new List<SocialInteractionProcessedTransactionData>())
            {
                SocialInteractionProcessedTransactionData clone = processed.Clone();
                processedTransactions[clone.transactionId] = clone;
            }

            foreach (SocialInteractionCooldownData cooldown in saveData?.cooldowns ?? new List<SocialInteractionCooldownData>())
            {
                SocialInteractionCooldownData clone = cooldown.Clone();
                cooldownsByKey[clone.cooldownKey] = clone;
            }

            Revision = saveData?.revision ?? 0L;
            RebuildIndexes();
        }

        private void RebuildIndexes()
        {
            recordIdsByPerson.Clear();
            recordIdsByDefinition.Clear();
            foreach (SocialInteractionRecordData record in recordsById.Values)
            {
                AddIndex(recordIdsByDefinition, record.interactionDefinitionId, record.interactionRecordId);
                AddIndex(recordIdsByPerson, record.initiatorPersonId, record.interactionRecordId);
                AddIndex(recordIdsByPerson, record.targetPersonId, record.interactionRecordId);
                foreach (SocialInteractionParticipantData participant in record.participants ?? Array.Empty<SocialInteractionParticipantData>())
                {
                    AddIndex(recordIdsByPerson, participant.personId, record.interactionRecordId);
                }
            }
        }

        private static void AddIndex(Dictionary<string, List<string>> index, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            key = key.Trim();
            if (!index.TryGetValue(key, out List<string> values))
            {
                values = new List<string>();
                index[key] = values;
            }

            if (!values.Contains(value))
            {
                values.Add(value);
                values.Sort(StringComparer.Ordinal);
            }
        }

        private static IEnumerable<SocialInteractionRecordData> Ordered(IEnumerable<SocialInteractionRecordData> records)
        {
            return (records ?? Array.Empty<SocialInteractionRecordData>())
                .Where(record => record != null)
                .OrderBy(record => record.worldTime)
                .ThenBy(record => record.interactionDefinitionId, StringComparer.Ordinal)
                .ThenBy(record => record.interactionRecordId, StringComparer.Ordinal);
        }

        private static string ResolveRole(SocialInteractionRequest request, SocialInteractionRole role)
        {
            return role switch
            {
                SocialInteractionRole.Target or SocialInteractionRole.Subject or SocialInteractionRole.Recipient => Clean(request.TargetPersonId),
                SocialInteractionRole.Witness => Clean((request.WitnessPersonIds ?? Array.Empty<string>()).FirstOrDefault()),
                _ => Clean(request.InitiatorPersonId)
            };
        }

        private static int DeterministicRoll(string definitionId, string transactionId, string seed, string initiator, string target, double worldTime)
        {
            string material = $"{definitionId}|{transactionId}|{seed}|{initiator}|{target}|{worldTime:0.###}";
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(material));
            return BitConverter.ToUInt16(hash, 0) % 10000;
        }

        public static string BuildStableId(string prefix, string material)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(material ?? string.Empty));
            string suffix = BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
            return $"{prefix}.{suffix}";
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
