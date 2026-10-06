using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityIsekaiGame.GameData.Persistence
{
    public sealed class PersistenceService
    {
        public const string FormatIdentifier = "UnityIsekaiGame.Save";
        public const int CurrentSchemaVersion = 3;
        public const string LocalWorldId = "local-world";
        public const string LocalPlayerId = "local-player";
        public const string LocalAccountId = "local-account";

        private readonly PersistencePathProvider pathProvider;
        private readonly ISaveSerializer serializer;
        private readonly List<IPersistenceParticipant> participants = new List<IPersistenceParticipant>();
        private readonly Dictionary<string, IPersistenceParticipant> participantsByKey = new Dictionary<string, IPersistenceParticipant>(StringComparer.Ordinal);
        private readonly Dictionary<string, PersistenceParticipantDescriptor> descriptorsByKey = new Dictionary<string, PersistenceParticipantDescriptor>(StringComparer.Ordinal);
        private readonly List<IPersistenceConsistencyValidator> consistencyValidators = new List<IPersistenceConsistencyValidator>();
        private readonly Dictionary<string, SaveEnvelopeHeader> savedEnvelopeHeaders = new Dictionary<string, SaveEnvelopeHeader>(StringComparer.Ordinal);
        private readonly Dictionary<string, CachedParticipantPayload> participantPayloadCache = new Dictionary<string, CachedParticipantPayload>(StringComparer.Ordinal);
        private bool operationInProgress;
        private PersistenceOperationState operationState = PersistenceOperationState.Idle;
        private PersistenceTransactionPhase currentPhase = PersistenceTransactionPhase.Idle;
        private PersistenceRuntimeSafety runtimeSafety = PersistenceRuntimeSafety.Safe;
        private string currentTransactionId = string.Empty;
        private string lastRecoveryRecommendation = string.Empty;
        private string lastConsistencyAudit = "Not run.";
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private const int StreamingIoBufferBytes = 64 * 1024;

        public PersistenceService(
            PersistencePathProvider pathProvider = null,
            string gameVersion = "0.4.1-prototype",
            string worldId = LocalWorldId,
            string playerId = LocalPlayerId,
            string accountId = LocalAccountId,
            PersistenceContextKind contextKind = PersistenceContextKind.Player,
            ISaveSerializer serializer = null)
        {
            this.pathProvider = pathProvider ?? new PersistencePathProvider();
            this.serializer = serializer ?? PersistenceSerialization.Serializer;
            ContextKind = contextKind;
            GameVersion = gameVersion;
            WorldId = string.IsNullOrWhiteSpace(worldId) ? LocalWorldId : worldId;
            PlayerId = contextKind == PersistenceContextKind.World
                ? string.Empty
                : string.IsNullOrWhiteSpace(playerId) ? LocalPlayerId : playerId;
            AccountId = string.IsNullOrWhiteSpace(accountId) ? LocalAccountId : accountId;
        }

        public event Action<PersistenceSaveResult> SaveCompleted;
        public event Action<PersistenceLoadResult> LoadCompleted;
        public event Action SaveStarted;
        public event Action LoadStarted;
        public event Action SaveSlotsChanged;
        public event Action<string, PersistenceTransactionPhase> SaveTransactionPhaseChanged;
        public event Action<string, PersistenceTransactionPhase> LoadTransactionPhaseChanged;
        public event Action<PersistenceConsistencyAuditReport> ConsistencyAuditCompleted;
        public event Action<SaveRecoveryScanReport> RecoveryScanCompleted;

        public string GameVersion { get; }
        public string WorldId { get; }
        public string PlayerId { get; }
        public string AccountId { get; }
        public PersistenceContextKind ContextKind { get; }
        public bool OperationInProgress => operationInProgress;
        public PersistenceOperationState OperationState => operationState;
        public PersistenceTransactionPhase CurrentPhase => currentPhase;
        public PersistenceRuntimeSafety RuntimeSafety => runtimeSafety;
        public string CurrentTransactionId => currentTransactionId;
        public int ParticipantCount => participants.Count;
        public PersistencePathProvider PathProvider => pathProvider;
        public Func<double> PlaytimeSecondsProvider { get; set; }
        public ISaveMetadataProvider MetadataProvider { get; set; }
        public PersistenceFaultInjection FaultInjection { get; } = new PersistenceFaultInjection();

        public bool RegisterParticipant(IPersistenceParticipant participant, out string failureReason)
        {
            failureReason = string.Empty;
            if (participant == null)
            {
                failureReason = "Cannot register a null persistence participant.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(participant.ParticipantKey))
            {
                failureReason = "Cannot register a persistence participant with no key.";
                return false;
            }

            if (participantsByKey.ContainsKey(participant.ParticipantKey))
            {
                failureReason = $"Persistence participant '{participant.ParticipantKey}' is already registered.";
                return false;
            }

            if (!AcceptsScope(participant.Scope))
            {
                failureReason = $"Persistence participant '{participant.ParticipantKey}' with scope {participant.Scope} cannot be registered in the {ContextKind} persistence context.";
                return false;
            }

            PersistenceParticipantDescriptor descriptor = PersistenceParticipantDescriptor.From(participant);
            if (descriptor.SchemaVersion <= 0)
            {
                failureReason = $"Persistence participant '{descriptor.Key}' has invalid schema version {descriptor.SchemaVersion}.";
                return false;
            }

            participants.Add(participant);
            participantsByKey.Add(participant.ParticipantKey, participant);
            descriptorsByKey.Add(participant.ParticipantKey, descriptor);
            participants.Sort(CompareParticipants);
            return true;
        }

        public bool RegisterConsistencyValidator(IPersistenceConsistencyValidator validator, out string failureReason)
        {
            failureReason = string.Empty;
            if (validator == null || string.IsNullOrWhiteSpace(validator.ValidatorKey))
            {
                failureReason = "Cannot register a missing or unnamed persistence consistency validator.";
                return false;
            }

            if (consistencyValidators.Exists(candidate => string.Equals(candidate.ValidatorKey, validator.ValidatorKey, StringComparison.Ordinal)))
            {
                failureReason = $"Persistence consistency validator '{validator.ValidatorKey}' is already registered.";
                return false;
            }

            consistencyValidators.Add(validator);
            consistencyValidators.Sort((a, b) => string.CompareOrdinal(a.ValidatorKey, b.ValidatorKey));
            return true;
        }

        public void UnregisterParticipant(IPersistenceParticipant participant)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.ParticipantKey))
            {
                return;
            }

            participants.Remove(participant);
            if (participantsByKey.TryGetValue(participant.ParticipantKey, out IPersistenceParticipant found) && ReferenceEquals(found, participant))
            {
                participantsByKey.Remove(participant.ParticipantKey);
                descriptorsByKey.Remove(participant.ParticipantKey);
            }
        }

        public void ResetParticipants()
        {
            participants.Clear();
            participantsByKey.Clear();
            descriptorsByKey.Clear();
            consistencyValidators.Clear();
            operationInProgress = false;
            operationState = PersistenceOperationState.Idle;
            currentPhase = PersistenceTransactionPhase.Idle;
            currentTransactionId = string.Empty;
            runtimeSafety = PersistenceRuntimeSafety.Safe;
        }

        public PersistenceSaveResult Save(string slotId, string displayName = null)
        {
            if (operationInProgress)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.OperationAlreadyRunning, slotId, string.Empty, "A save or load operation is already running.");
            }

            if (runtimeSafety == PersistenceRuntimeSafety.Unsafe)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.UnsafeRuntimeState, slotId, string.Empty, "Persistence runtime is unsafe after a failed rollback. Restart or use an explicit recovery path before saving.", transactionId: currentTransactionId, phase: currentPhase);
            }

            if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out string pathFailure))
            {
                return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.InvalidSlotId, slotId, string.Empty, pathFailure));
            }

            if (participants.Count == 0)
            {
                return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.NoParticipants, slotId, paths.PrimaryPath, "No persistence participants are registered."));
            }

            string transactionId = Guid.NewGuid().ToString("N");
            SetOperation(PersistenceOperationState.Capturing, PersistenceTransactionPhase.Eligibility, transactionId, isSave: true);
            SaveStarted?.Invoke();

            try
            {
                if (!Faulted(PersistenceFaultInjectionPoint.SaveCapture, out string injectedSaveFailure))
                {
                    PersistenceDependencyReport dependencyReport = BuildParticipantDependencyReport();
                    if (!dependencyReport.succeeded)
                    {
                        return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.DependencyValidationFailed, slotId, paths.PrimaryPath, dependencyReport.message, transactionId: transactionId, phase: PersistenceTransactionPhase.ResolveDependencies));
                    }
                }
                else
                {
                    return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.ParticipantCaptureFailed, slotId, paths.PrimaryPath, injectedSaveFailure, transactionId: transactionId, phase: PersistenceTransactionPhase.Capture));
                }

                SetOperation(PersistenceOperationState.Capturing, PersistenceTransactionPhase.Capture, transactionId, isSave: true);
                GameSaveEnvelope envelope = BuildEnvelope(slotId, displayName, paths, transactionId, computeChecksum: true);
                SetOperation(PersistenceOperationState.Capturing, PersistenceTransactionPhase.BuildEnvelope, transactionId, isSave: true);
                string serialized;
                try
                {
                    serialized = serializer.Serialize(envelope, false);
                    if (string.IsNullOrWhiteSpace(serialized))
                    {
                        return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.SerializationFailed, slotId, paths.PrimaryPath, "Save envelope serialized to empty JSON.", transactionId: transactionId, phase: PersistenceTransactionPhase.BuildEnvelope));
                    }
                }
                catch (Exception exception)
                {
                    return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.SerializationFailed, slotId, paths.PrimaryPath, "Save serialization failed.", exception, transactionId, PersistenceTransactionPhase.BuildEnvelope));
                }

                try
                {
                    pathProvider.EnsureDirectory();
                }
                catch (Exception exception)
                {
                    return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.DirectoryCreationFailed, slotId, paths.PrimaryPath, "Could not create save directory.", exception, transactionId, PersistenceTransactionPhase.WriteTemporary));
                }

                SetOperation(PersistenceOperationState.Writing, PersistenceTransactionPhase.WriteTemporary, transactionId, isSave: true);
                PersistenceSaveResult writeResult = WriteAtomically(paths, serialized, transactionId);
                if (!writeResult.Succeeded)
                {
                    return FinishSave(writeResult);
                }

                SetOperation(PersistenceOperationState.Writing, PersistenceTransactionPhase.UpdateMetadata, transactionId, isSave: true);
                SaveSlotsChanged?.Invoke();
                RememberHeader(envelope);
                return FinishSave(PersistenceSaveResult.Success(slotId, paths.PrimaryPath, $"Saved slot '{slotId}'.", transactionId));
            }
            catch (ParticipantSaveException exception)
            {
                return FinishSave(PersistenceSaveResult.Failure(exception.Status, slotId, paths.PrimaryPath, exception.Message, exception, transactionId, currentPhase));
            }
            catch (Exception exception)
            {
                return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.UnknownException, slotId, paths.PrimaryPath, "Unexpected save failure.", exception, transactionId, currentPhase));
            }
        }

        /// <summary>
        /// Captures an immutable save snapshot while runtime state is only accessed from the caller's
        /// simulation thread. Call <see cref="WritePreparedSave"/> on a background worker, then call
        /// <see cref="CompletePreparedSave"/> back on the simulation thread.
        /// </summary>
        public bool TryPrepareSave(
            string slotId,
            string displayName,
            out PreparedPersistenceSave prepared,
            out PersistenceSaveResult failure)
        {
            prepared = null;
            if (!TryBeginSaveCapture(slotId, displayName, out PreparedPersistenceSaveCapture capture, out failure))
            {
                return false;
            }

            return TryContinueSaveCapture(capture, int.MaxValue, out prepared, out failure) && prepared != null;
        }

        /// <summary>
        /// Starts a save snapshot transaction without walking participant state. The caller can then
        /// capture a bounded number of participants per simulation tick with <see cref="TryContinueSaveCapture"/>.
        /// </summary>
        public bool TryBeginSaveCapture(
            string slotId,
            string displayName,
            out PreparedPersistenceSaveCapture capture,
            out PersistenceSaveResult failure)
        {
            capture = null;
            failure = null;
            if (operationInProgress)
            {
                failure = PersistenceSaveResult.Failure(PersistenceSaveStatus.OperationAlreadyRunning, slotId, string.Empty, "A save or load operation is already running.");
                return false;
            }

            if (runtimeSafety == PersistenceRuntimeSafety.Unsafe)
            {
                failure = PersistenceSaveResult.Failure(PersistenceSaveStatus.UnsafeRuntimeState, slotId, string.Empty, "Persistence runtime is unsafe after a failed rollback. Restart or use an explicit recovery path before saving.", transactionId: currentTransactionId, phase: currentPhase);
                return false;
            }

            if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out string pathFailure))
            {
                failure = PersistenceSaveResult.Failure(PersistenceSaveStatus.InvalidSlotId, slotId, string.Empty, pathFailure);
                return false;
            }

            if (participants.Count == 0)
            {
                failure = PersistenceSaveResult.Failure(PersistenceSaveStatus.NoParticipants, slotId, paths.PrimaryPath, "No persistence participants are registered.");
                return false;
            }

            string transactionId = Guid.NewGuid().ToString("N");
            SetOperation(PersistenceOperationState.Capturing, PersistenceTransactionPhase.Eligibility, transactionId, isSave: true);
            SaveStarted?.Invoke();
            try
            {
                if (!Faulted(PersistenceFaultInjectionPoint.SaveCapture, out string injectedSaveFailure))
                {
                    PersistenceDependencyReport dependencyReport = BuildParticipantDependencyReport();
                    if (!dependencyReport.succeeded)
                    {
                        failure = FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.DependencyValidationFailed, slotId, paths.PrimaryPath, dependencyReport.message, transactionId: transactionId, phase: PersistenceTransactionPhase.ResolveDependencies));
                        return false;
                    }
                }
                else
                {
                    failure = FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.ParticipantCaptureFailed, slotId, paths.PrimaryPath, injectedSaveFailure, transactionId: transactionId, phase: PersistenceTransactionPhase.Capture));
                    return false;
                }

                SetOperation(PersistenceOperationState.Capturing, PersistenceTransactionPhase.Capture, transactionId, isSave: true);
                capture = new PreparedPersistenceSaveCapture(
                    this,
                    paths,
                    CreateEnvelope(slotId, displayName, paths, transactionId),
                    GetOrderedParticipants(),
                    transactionId);
                return true;
            }
            catch (ParticipantSaveException exception)
            {
                failure = FinishSave(PersistenceSaveResult.Failure(exception.Status, slotId, paths.PrimaryPath, exception.Message, exception, transactionId, currentPhase));
                return false;
            }
            catch (Exception exception)
            {
                failure = FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.UnknownException, slotId, paths.PrimaryPath, "Unexpected save snapshot failure.", exception, transactionId, currentPhase));
                return false;
            }
        }

        /// <summary>
        /// Captures at most <paramref name="maximumParticipants"/> immutable participant snapshots.
        /// A non-null prepared result means capture is complete and ready for the background writer.
        /// </summary>
        public bool TryContinueSaveCapture(
            PreparedPersistenceSaveCapture capture,
            int maximumParticipants,
            out PreparedPersistenceSave prepared,
            out PersistenceSaveResult failure)
        {
            prepared = null;
            failure = null;
            if (capture == null
                || !ReferenceEquals(capture.Owner, this)
                || !string.Equals(currentTransactionId, capture.TransactionId, StringComparison.Ordinal))
            {
                failure = PersistenceSaveResult.Failure(
                    PersistenceSaveStatus.UnknownException,
                    capture?.SlotId ?? string.Empty,
                    capture?.Paths?.PrimaryPath ?? string.Empty,
                    "Save capture does not match the active persistence transaction.");
                return false;
            }

            try
            {
                int remaining = Math.Max(1, maximumParticipants);
                while (!capture.IsComplete && remaining-- > 0)
                {
                    IPersistenceParticipant participant = capture.Participants[capture.NextParticipantIndex];
                    capture.LastCapturedParticipantKeyValue = participant?.ParticipantKey ?? string.Empty;
                    if (capture.ActiveIncrementalCapture != null)
                    {
                        if (!capture.ActiveIncrementalCapture.TryContinue(
                                out bool completed,
                                out DeferredPersistenceParticipantCapture completedCapture,
                                out string failureReason))
                        {
                            throw new ParticipantSaveException(
                                PersistenceSaveStatus.ParticipantCaptureFailed,
                                $"Participant '{participant?.ParticipantKey}' failed incremental snapshot capture: {failureReason}");
                        }

                        if (!completed) continue;
                        AddDeferredParticipantCapture(participant, completedCapture, capture.Envelope, capture.Captures);
                        capture.ActiveIncrementalCapture = null;
                        capture.ActiveIncrementalParticipant = null;
                        capture.NextParticipantIndex++;
                        continue;
                    }

                    if (TryUseCachedParticipantPayload(participant, capture.Envelope, capture.Captures))
                    {
                        capture.NextParticipantIndex++;
                        continue;
                    }

                    if (participant is IIncrementalPersistenceParticipant incremental)
                    {
                        capture.ActiveIncrementalParticipant = participant;
                        capture.ActiveIncrementalCapture = incremental.BeginIncrementalCapture();
                        if (capture.ActiveIncrementalCapture == null)
                        {
                            throw new ParticipantSaveException(
                                PersistenceSaveStatus.ParticipantCaptureFailed,
                                $"Participant '{participant.ParticipantKey}' returned no incremental capture.");
                        }

                        if (!capture.ActiveIncrementalCapture.TryContinue(
                                out bool completed,
                                out DeferredPersistenceParticipantCapture completedCapture,
                                out string failureReason))
                        {
                            throw new ParticipantSaveException(
                                PersistenceSaveStatus.ParticipantCaptureFailed,
                                $"Participant '{participant.ParticipantKey}' failed incremental snapshot capture: {failureReason}");
                        }

                        if (!completed) continue;
                        AddDeferredParticipantCapture(participant, completedCapture, capture.Envelope, capture.Captures);
                        capture.ActiveIncrementalCapture = null;
                        capture.ActiveIncrementalParticipant = null;
                        capture.NextParticipantIndex++;
                        continue;
                    }

                    CaptureDeferredParticipant(participant, capture.Envelope, capture.Captures);
                    capture.NextParticipantIndex++;
                }

                if (!capture.IsComplete) return true;
                capture.Envelope.participants.Sort(CompareRecords);
                prepared = new PreparedPersistenceSave(this, capture.Paths, capture.Envelope, capture.Captures, capture.TransactionId)
                {
                    ReusedParticipantCount = capture.Captures.Count(item => item != null && item.WasReused)
                };
                SetOperation(PersistenceOperationState.Writing, PersistenceTransactionPhase.BuildEnvelope, capture.TransactionId, isSave: true);
                return true;
            }
            catch (ParticipantSaveException exception)
            {
                failure = FinishSave(PersistenceSaveResult.Failure(exception.Status, capture.SlotId, capture.Paths.PrimaryPath, exception.Message, exception, capture.TransactionId, currentPhase));
                return false;
            }
            catch (Exception exception)
            {
                failure = FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.UnknownException, capture.SlotId, capture.Paths.PrimaryPath, "Unexpected incremental save snapshot failure.", exception, capture.TransactionId, currentPhase));
                return false;
            }
        }

        /// <summary>
        /// Performs CPU-heavy envelope finalization and atomic file I/O without touching live runtime
        /// participants. This method is safe to call from a single background persistence worker.
        /// </summary>
        public PersistenceSaveResult WritePreparedSave(PreparedPersistenceSave prepared)
        {
            if (prepared == null || !ReferenceEquals(prepared.Owner, this) || prepared.Paths == null || prepared.Envelope == null)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.UnknownException, prepared?.SlotId ?? string.Empty, prepared?.Path ?? string.Empty, "The prepared save does not belong to this persistence service.");
            }

            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var phaseTimer = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < prepared.Participants.Count; i++)
                {
                    PreparedPersistenceParticipant participant = prepared.Participants[i];
                    if (participant?.Record == null || participant.DeferredCapture == null) continue;
                    PersistenceParticipantSaveResult payload = participant.DeferredCapture.SerializePayload();
                    if (payload == null || !payload.Succeeded || string.IsNullOrWhiteSpace(payload.PayloadJson))
                    {
                        return PersistenceSaveResult.Failure(
                            PersistenceSaveStatus.SerializationFailed,
                            prepared.SlotId,
                            prepared.Path,
                            payload?.Message ?? "Deferred participant serialization returned no payload.",
                            transactionId: prepared.TransactionId,
                            phase: PersistenceTransactionPhase.BuildEnvelope);
                    }

                    participant.Record.payloadJson = NormalizePayloadJsonStrict(payload.PayloadJson);
                }
                phaseTimer.Stop();
                prepared.DeferredSerializationMilliseconds = phaseTimer.Elapsed.TotalMilliseconds;

                phaseTimer.Restart();
                prepared.Envelope.contentChecksum = ComputeChecksumFromNormalizedPayloads(prepared.Envelope);
                phaseTimer.Stop();
                double checksumMilliseconds = phaseTimer.Elapsed.TotalMilliseconds;

                try
                {
                    pathProvider.EnsureDirectory();
                }
                catch (Exception exception)
                {
                    return PersistenceSaveResult.Failure(PersistenceSaveStatus.DirectoryCreationFailed, prepared.SlotId, prepared.Path, "Could not create save directory.", exception, prepared.TransactionId, PersistenceTransactionPhase.WriteTemporary);
                }

                PersistenceSaveResult result;
                if (serializer is IStreamingSaveSerializer streamingSerializer)
                {
                    result = WritePreparedSaveStreaming(
                        prepared.Paths,
                        prepared.Envelope,
                        streamingSerializer,
                        prepared.TransactionId,
                        out double serializationMilliseconds,
                        out double atomicWriteMilliseconds,
                        out long serializedBytes);
                    prepared.EnvelopeSerializationMilliseconds = checksumMilliseconds + serializationMilliseconds;
                    prepared.AtomicWriteMilliseconds = atomicWriteMilliseconds;
                    prepared.SerializedBytes = serializedBytes;
                }
                else
                {
                    phaseTimer.Restart();
                    string serialized = serializer.Serialize(prepared.Envelope, false);
                    if (string.IsNullOrWhiteSpace(serialized))
                    {
                        return PersistenceSaveResult.Failure(PersistenceSaveStatus.SerializationFailed, prepared.SlotId, prepared.Path, "Save envelope serialized to empty JSON.", transactionId: prepared.TransactionId, phase: PersistenceTransactionPhase.BuildEnvelope);
                    }
                    phaseTimer.Stop();
                    prepared.EnvelopeSerializationMilliseconds = checksumMilliseconds + phaseTimer.Elapsed.TotalMilliseconds;
                    prepared.SerializedBytes = Encoding.UTF8.GetByteCount(serialized);

                    phaseTimer.Restart();
                    result = WriteAtomically(prepared.Paths, serialized, prepared.TransactionId, updateOperationState: false);
                    phaseTimer.Stop();
                    prepared.AtomicWriteMilliseconds = phaseTimer.Elapsed.TotalMilliseconds;
                }
                totalTimer.Stop();
                prepared.TotalWriteMilliseconds = totalTimer.Elapsed.TotalMilliseconds;
                return result;
            }
            catch (Exception exception)
            {
                totalTimer.Stop();
                prepared.TotalWriteMilliseconds = totalTimer.Elapsed.TotalMilliseconds;
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.UnknownException, prepared.SlotId, prepared.Path, "Unexpected prepared save failure.", exception, prepared.TransactionId, PersistenceTransactionPhase.Failed);
            }
        }

        /// <summary>
        /// Finalizes a background save on the simulation thread and publishes normal persistence events.
        /// </summary>
        public PersistenceSaveResult CompletePreparedSave(PreparedPersistenceSave prepared, PersistenceSaveResult result)
        {
            if (prepared == null || !ReferenceEquals(prepared.Owner, this) || result == null)
            {
                return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.UnknownException, prepared?.SlotId ?? string.Empty, prepared?.Path ?? string.Empty, "Cannot complete an invalid prepared save."));
            }

            if (!string.Equals(currentTransactionId, prepared.TransactionId, StringComparison.Ordinal))
            {
                return FinishSave(PersistenceSaveResult.Failure(PersistenceSaveStatus.UnknownException, prepared.SlotId, prepared.Path, "Prepared save completion does not match the active persistence transaction.", transactionId: prepared.TransactionId));
            }

            if (result.Succeeded)
            {
                RememberParticipantPayloads(prepared);
                RememberHeader(prepared.Envelope);
                SaveSlotsChanged?.Invoke();
            }

            return FinishSave(result);
        }

        public PersistenceLoadResult Load(string slotId, bool loadBackup = false)
        {
            if (operationInProgress)
            {
                return PersistenceLoadResult.Failure(PersistenceLoadStatus.OperationAlreadyRunning, slotId, string.Empty, "A save or load operation is already running.");
            }

            if (runtimeSafety == PersistenceRuntimeSafety.Unsafe)
            {
                return PersistenceLoadResult.Failure(PersistenceLoadStatus.UnsafeRuntimeState, slotId, string.Empty, "Persistence runtime is unsafe after a failed rollback. Restart or use an explicit recovery path before loading.", transactionId: currentTransactionId, phase: currentPhase, runtimeSafety: runtimeSafety);
            }

            if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out string pathFailure))
            {
                return FinishLoad(PersistenceLoadResult.Failure(PersistenceLoadStatus.InvalidSlotId, slotId, string.Empty, pathFailure));
            }

            string path = loadBackup ? paths.BackupPath : paths.PrimaryPath;
            participantPayloadCache.Clear();
            string transactionId = Guid.NewGuid().ToString("N");
            SetOperation(PersistenceOperationState.PreparingLoad, PersistenceTransactionPhase.Eligibility, transactionId, isSave: false);
            LoadStarted?.Invoke();

            try
            {
                PersistenceDependencyReport dependencyReport = BuildParticipantDependencyReport();
                if (!dependencyReport.succeeded)
                {
                    return FinishLoad(PersistenceLoadResult.Failure(PersistenceLoadStatus.DependencyValidationFailed, slotId, path, dependencyReport.message, transactionId: transactionId, phase: PersistenceTransactionPhase.ResolveDependencies));
                }

                SetOperation(PersistenceOperationState.PreparingLoad, PersistenceTransactionPhase.Read, transactionId, isSave: false);
                PersistenceValidationResult validation = ValidatePath(slotId, path, loadBackup, validateParticipants: true);
                if (!validation.Succeeded)
                {
                    bool backupAvailable = !loadBackup && IsBackupValid(slotId, paths.BackupPath);
                    PersistenceLoadStatus status = backupAvailable ? PersistenceLoadStatus.BackupAvailable : MapValidationToLoadStatus(validation.Status);
                    return FinishLoad(PersistenceLoadResult.Failure(status, slotId, path, validation.Message, backupAvailable, validation.Exception, transactionId, PersistenceTransactionPhase.ValidateEnvelope));
                }

                RememberHeader(validation.Envelope);

                SetOperation(PersistenceOperationState.PreparingLoad, PersistenceTransactionPhase.PrepareParticipants, transactionId, isSave: false);
                PersistenceLoadResult prepared = PrepareAndCommit(slotId, path, validation.Envelope, loadBackup, transactionId);
                return FinishLoad(prepared);
            }
            catch (Exception exception)
            {
                return FinishLoad(PersistenceLoadResult.Failure(loadBackup ? PersistenceLoadStatus.BackupLoadFailed : PersistenceLoadStatus.UnknownException, slotId, path, "Unexpected load failure.", false, exception, transactionId, currentPhase, runtimeSafety: runtimeSafety));
            }
        }

        public PersistenceValidationResult ValidateSlot(string slotId, bool validateBackup = false)
        {
            if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out string pathFailure))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.InvalidSlotId, slotId, string.Empty, pathFailure);
            }

            string path = validateBackup ? paths.BackupPath : paths.PrimaryPath;
            PersistenceValidationResult result = ValidatePath(slotId, path, validateBackup, validateParticipants: true);
            if (result.Succeeded)
            {
                return result;
            }

            bool backupAvailable = !validateBackup && IsBackupValid(slotId, paths.BackupPath);
            if (backupAvailable)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.BackupAvailable, slotId, path, result.Message, true, result.Exception);
            }

            return result;
        }

        /// <summary>
        /// Reads and validates the save envelope and checksum without preparing participant payloads.
        /// This is intended for boot-time discovery of dynamic identities that participant validators
        /// must know before the normal, strict validation and load transaction begins.
        /// </summary>
        public PersistenceValidationResult InspectSlotEnvelope(string slotId, bool inspectBackup = false)
        {
            if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out string pathFailure))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.InvalidSlotId, slotId, string.Empty, pathFailure);
            }

            string path = inspectBackup ? paths.BackupPath : paths.PrimaryPath;
            return ValidatePath(slotId, path, inspectBackup, validateParticipants: false);
        }

        public PersistenceDeleteResult DeleteSlot(string slotId)
        {
            if (operationInProgress)
            {
                return PersistenceDeleteResult.Failure(PersistenceDeleteStatus.OperationAlreadyRunning, slotId, "A save or load operation is already running.");
            }

            if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out string pathFailure))
            {
                return PersistenceDeleteResult.Failure(PersistenceDeleteStatus.InvalidSlotId, slotId, pathFailure);
            }

            operationInProgress = true;
            operationState = PersistenceOperationState.Deleting;
            try
            {
                DeleteIfExists(paths.PrimaryPath);
                DeleteIfExists(paths.BackupPath);
                DeleteIfExists(paths.TemporaryPath);
                savedEnvelopeHeaders.Remove(slotId);
                SaveSlotsChanged?.Invoke();
                return PersistenceDeleteResult.Success(slotId, $"Deleted save slot '{slotId}'.");
            }
            catch (Exception exception)
            {
                return PersistenceDeleteResult.Failure(PersistenceDeleteStatus.DeleteFailed, slotId, "Could not delete every file for the save slot.", exception);
            }
            finally
            {
                operationInProgress = false;
                operationState = PersistenceOperationState.Idle;
            }
        }

        public IReadOnlyList<SaveSlotMetadata> ListSaveSlots()
        {
            List<SaveSlotMetadata> metadata = new List<SaveSlotMetadata>();
            foreach (SaveSlotPaths paths in pathProvider.EnumerateKnownSlots())
            {
                metadata.Add(ReadMetadata(paths));
            }

            return metadata;
        }

        public PersistenceDependencyReport BuildParticipantDependencyReport()
        {
            Dictionary<string, IPersistenceParticipant> registered = new Dictionary<string, IPersistenceParticipant>(StringComparer.Ordinal);
            foreach (IPersistenceParticipant participant in participants)
            {
                if (participant == null || string.IsNullOrWhiteSpace(participant.ParticipantKey))
                {
                    continue;
                }

                registered[participant.ParticipantKey] = participant;
            }

            List<PersistenceParticipantDependencyMetadata> metadata = new List<PersistenceParticipantDependencyMetadata>();
            List<string> missingRequired = new List<string>();
            List<string> missingOptional = new List<string>();
            foreach (IPersistenceParticipant participant in participants)
            {
                PersistenceParticipantDependencyMetadata entry = BuildDependencyMetadata(participant);
                metadata.Add(entry);

                foreach (string required in entry.requiredDependencies)
                {
                    if (!registered.ContainsKey(required))
                    {
                        missingRequired.Add($"{participant.ParticipantKey}->{required}");
                    }
                }

                foreach (string optional in entry.optionalDependencies)
                {
                    if (!registered.ContainsKey(optional))
                    {
                        missingOptional.Add($"{participant.ParticipantKey}->{optional}");
                    }
                }
            }

            if (missingRequired.Count > 0)
            {
                return new PersistenceDependencyReport
                {
                    succeeded = false,
                    message = "Missing required persistence dependencies: " + string.Join(", ", missingRequired),
                    missingRequiredDependencies = missingRequired.ToArray(),
                    missingOptionalDependencies = missingOptional.ToArray(),
                    participants = metadata.ToArray()
                };
            }

            List<IPersistenceParticipant> ordered = new List<IPersistenceParticipant>();
            List<string> circular = new List<string>();
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            List<IPersistenceParticipant> deterministic = new List<IPersistenceParticipant>(participants);
            deterministic.Sort(CompareParticipants);

            foreach (IPersistenceParticipant participant in deterministic)
            {
                VisitParticipant(participant, registered, ordered, visiting, visited, circular);
            }

            if (circular.Count > 0)
            {
                return new PersistenceDependencyReport
                {
                    succeeded = false,
                    message = "Circular persistence dependencies detected: " + string.Join(", ", circular),
                    circularDependencies = circular.ToArray(),
                    missingOptionalDependencies = missingOptional.ToArray(),
                    participants = metadata.ToArray()
                };
            }

            string[] orderedKeys = new string[ordered.Count];
            for (int i = 0; i < ordered.Count; i++)
            {
                orderedKeys[i] = ordered[i].ParticipantKey;
            }

            return new PersistenceDependencyReport
            {
                succeeded = true,
                message = missingOptional.Count == 0 ? "Persistence participant dependency graph is valid." : "Persistence participant dependency graph is valid with missing optional dependencies.",
                orderedParticipantKeys = orderedKeys,
                missingOptionalDependencies = missingOptional.ToArray(),
                participants = metadata.ToArray()
            };
        }

        public PersistenceTransactionDiagnostics BuildTransactionDiagnostics()
        {
            PersistenceDependencyReport dependencies = BuildParticipantDependencyReport();
            return new PersistenceTransactionDiagnostics
            {
                transactionId = currentTransactionId ?? string.Empty,
                phase = currentPhase,
                operationState = operationState,
                runtimeSafety = runtimeSafety,
                operationInProgress = operationInProgress,
                participantOrder = dependencies.orderedParticipantKeys ?? Array.Empty<string>(),
                lastRecoveryRecommendation = lastRecoveryRecommendation ?? string.Empty,
                lastConsistencyAudit = lastConsistencyAudit ?? string.Empty
            };
        }

        public IReadOnlyList<PersistenceParticipantDescriptor> BuildParticipantManifest()
        {
            List<PersistenceParticipantDescriptor> manifest = new List<PersistenceParticipantDescriptor>(descriptorsByKey.Values);
            manifest.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return manifest;
        }

        public string BuildRuntimeStateFingerprint()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("world=").Append(WorldId).Append("|player=").Append(PlayerId).Append("|account=").Append(AccountId);
            foreach (IPersistenceParticipant participant in GetOrderedParticipants())
            {
                PersistenceParticipantSaveResult capture = participant.CapturePayload();
                builder.Append('|').Append(participant.ParticipantKey).Append(':').Append(participant.ParticipantSchemaVersion).Append(':');
                builder.Append(capture == null || !capture.Succeeded ? "capture-failed" : capture.PayloadJson);
            }

            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                hex.Append(hash[i].ToString("x2"));
            }

            return hex.ToString();
        }

        public SaveRecoveryScanReport ScanRecoverySources()
        {
            List<SaveRecoveryCandidate> candidates = new List<SaveRecoveryCandidate>();
            List<string> staleTemps = new List<string>();
            List<string> corruptFiles = new List<string>();

            if (Directory.Exists(pathProvider.RootDirectory))
            {
                foreach (string tempPath in Directory.GetFiles(pathProvider.RootDirectory, "*.tmp"))
                {
                    string slotId = Path.GetFileNameWithoutExtension(tempPath);
                    if (!pathProvider.IsValidSlotId(slotId))
                    {
                        staleTemps.Add(tempPath);
                        continue;
                    }

                    PersistenceValidationResult tempValidation = ValidatePath(slotId, tempPath, isBackup: false, validateParticipants: false);
                    staleTemps.Add(tempPath);
                    candidates.Add(new SaveRecoveryCandidate
                    {
                        source = SaveRecoverySource.ValidTemporary,
                        action = tempValidation.Succeeded ? RecoveryRecommendationAction.InspectTemporary : RecoveryRecommendationAction.None,
                        slotId = slotId,
                        path = tempPath,
                        valid = tempValidation.Succeeded,
                        message = tempValidation.Message
                    });
                }
            }

            foreach (SaveSlotPaths paths in pathProvider.EnumerateKnownSlots())
            {
                AddRecoveryCandidate(candidates, corruptFiles, paths.SlotId, paths.PrimaryPath, SaveRecoverySource.RequestedPrimary, RecoveryRecommendationAction.None);
                AddRecoveryCandidate(candidates, corruptFiles, paths.SlotId, paths.BackupPath, SaveRecoverySource.RequestedBackup, RecoveryRecommendationAction.LoadBackup);
            }

            AddAutosaveRecoveryCandidate(candidates, corruptFiles, PrototypeSaveSlotCatalog.AutosaveSlotId(0), SaveRecoverySource.AutosaveNewest);
            for (int i = 1; i < PrototypeSaveSlotCatalog.DefaultAutosaveSlotCount; i++)
            {
                AddAutosaveRecoveryCandidate(candidates, corruptFiles, PrototypeSaveSlotCatalog.AutosaveSlotId(i), SaveRecoverySource.AutosavePrevious);
            }

            SaveRecoveryCandidate recommendation = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].valid && candidates[i].action != RecoveryRecommendationAction.None)
                {
                    if (recommendation == null || RecoveryRecommendationPriority(candidates[i].action) > RecoveryRecommendationPriority(recommendation.action))
                    {
                        recommendation = candidates[i];
                    }
                }
            }

            lastRecoveryRecommendation = recommendation == null
                ? "No explicit recovery source is currently recommended."
                : $"{recommendation.action}: {recommendation.slotId} ({recommendation.source})";

            SaveRecoveryScanReport report = new SaveRecoveryScanReport
            {
                scannedAtUtc = DateTime.UtcNow.ToString("o"),
                candidates = candidates.ToArray(),
                staleTemporaryFiles = staleTemps.ToArray(),
                corruptFiles = corruptFiles.ToArray(),
                recommendation = lastRecoveryRecommendation
            };
            RecoveryScanCompleted?.Invoke(report);
            return report;
        }

        private static int RecoveryRecommendationPriority(RecoveryRecommendationAction action)
        {
            return action switch
            {
                RecoveryRecommendationAction.PromoteBackup => 500,
                RecoveryRecommendationAction.LoadBackup => 400,
                RecoveryRecommendationAction.LoadAutosave => 300,
                RecoveryRecommendationAction.QuarantinePrimary => 200,
                RecoveryRecommendationAction.InspectTemporary => 100,
                RecoveryRecommendationAction.RestartPlayMode => 50,
                _ => 0
            };
        }

        public PersistenceDeleteResult CleanupStaleTemporaryFiles()
        {
            try
            {
                if (!Directory.Exists(pathProvider.RootDirectory))
                {
                    return PersistenceDeleteResult.Success("*", "Save directory does not exist.");
                }

                int deleted = 0;
                foreach (string tempPath in Directory.GetFiles(pathProvider.RootDirectory, "*.tmp"))
                {
                    File.Delete(tempPath);
                    deleted++;
                }

                return PersistenceDeleteResult.Success("*", $"Deleted {deleted} stale temporary save file(s).");
            }
            catch (Exception exception)
            {
                return PersistenceDeleteResult.Failure(PersistenceDeleteStatus.DeleteFailed, "*", "Failed to clean stale temporary save files.", exception);
            }
        }

        public PersistenceSaveResult PromoteBackup(string slotId)
        {
            if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out string failureReason))
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.InvalidSlotId, slotId, string.Empty, failureReason);
            }

            PersistenceValidationResult backupValidation = ValidatePath(slotId, paths.BackupPath, isBackup: true, validateParticipants: false);
            if (!backupValidation.Succeeded)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.ReplacementFailed, slotId, paths.BackupPath, $"Backup promotion rejected: {backupValidation.Message}");
            }

            try
            {
                if (File.Exists(paths.PrimaryPath))
                {
                    string quarantinePath = BuildQuarantinePath(paths.PrimaryPath);
                    File.Move(paths.PrimaryPath, quarantinePath);
                }

                File.Copy(paths.BackupPath, paths.PrimaryPath, true);
                SaveSlotsChanged?.Invoke();
                return PersistenceSaveResult.Success(slotId, paths.PrimaryPath, $"Promoted backup for '{slotId}' to primary.");
            }
            catch (Exception exception)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.ReplacementFailed, slotId, paths.PrimaryPath, "Backup promotion failed.", exception);
            }
        }

        public PersistenceSaveResult QuarantinePrimary(string slotId)
        {
            if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out string failureReason))
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.InvalidSlotId, slotId, string.Empty, failureReason);
            }

            if (!File.Exists(paths.PrimaryPath))
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.ReplacementFailed, slotId, paths.PrimaryPath, "Primary save file is missing.");
            }

            try
            {
                string quarantinePath = BuildQuarantinePath(paths.PrimaryPath);
                File.Move(paths.PrimaryPath, quarantinePath);
                SaveSlotsChanged?.Invoke();
                return PersistenceSaveResult.Success(slotId, quarantinePath, $"Quarantined primary save for '{slotId}'.");
            }
            catch (Exception exception)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.ReplacementFailed, slotId, paths.PrimaryPath, "Could not quarantine primary save.", exception);
            }
        }

        public SaveSlotMetadata GetSlotMetadata(string slotId)
        {
            return pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out _)
                ? ReadMetadata(paths)
                : new SaveSlotMetadata
                {
                    slotId = slotId,
                    displayName = slotId,
                    status = PersistenceValidationStatus.InvalidSlotId.ToString(),
                    message = $"Invalid save slot ID '{slotId}'."
                };
        }

        public PersistenceSaveResult RotateAutosaveSlots(string stagingSlotId, IReadOnlyList<string> generationSlotIds)
        {
            if (operationInProgress)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.OperationAlreadyRunning, stagingSlotId, string.Empty, "A save or load operation is already running.");
            }

            if (generationSlotIds == null || generationSlotIds.Count == 0)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.InvalidSlotId, stagingSlotId, string.Empty, "Autosave rotation requires at least one generation slot.");
            }

            if (!pathProvider.TryGetPaths(stagingSlotId, out SaveSlotPaths stagingPaths, out string stagingFailure))
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.InvalidSlotId, stagingSlotId, string.Empty, stagingFailure);
            }

            if (!File.Exists(stagingPaths.PrimaryPath))
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.TemporaryWriteFailed, stagingSlotId, stagingPaths.PrimaryPath, "Autosave staging slot is missing.");
            }

            List<SaveSlotPaths> generations = new List<SaveSlotPaths>();
            foreach (string slotId in generationSlotIds)
            {
                if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out string failure))
                {
                    return PersistenceSaveResult.Failure(PersistenceSaveStatus.InvalidSlotId, slotId, string.Empty, failure);
                }

                generations.Add(paths);
            }

            SetOperation(PersistenceOperationState.RotatingAutosaves, PersistenceTransactionPhase.PromotePrimary, Guid.NewGuid().ToString("N"), isSave: true);
            try
            {
                for (int i = generations.Count - 1; i > 0; i--)
                {
                    CopySlotFiles(generations[i - 1], generations[i]);
                }

                CopySlotFiles(stagingPaths, generations[0]);
                DeleteIfExists(stagingPaths.PrimaryPath);
                DeleteIfExists(stagingPaths.BackupPath);
                DeleteIfExists(stagingPaths.TemporaryPath);
                SaveSlotsChanged?.Invoke();
                return PersistenceSaveResult.Success(generations[0].SlotId, generations[0].PrimaryPath, "Autosave rotation completed.");
            }
            catch (Exception exception)
            {
                operationState = PersistenceOperationState.Failed;
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.ReplacementFailed, generations[0].SlotId, generations[0].PrimaryPath, "Autosave rotation failed.", exception);
            }
            finally
            {
                operationInProgress = false;
                if (operationState != PersistenceOperationState.Failed)
                {
                    operationState = PersistenceOperationState.Idle;
                    currentPhase = PersistenceTransactionPhase.Idle;
                    currentTransactionId = string.Empty;
                }
            }
        }

        private GameSaveEnvelope BuildEnvelope(string slotId, string displayName, SaveSlotPaths paths, string transactionId, bool computeChecksum)
        {
            GameSaveEnvelope envelope = CreateEnvelope(slotId, displayName, paths, transactionId);

            IReadOnlyList<IPersistenceParticipant> orderedParticipants = GetOrderedParticipants();
            foreach (IPersistenceParticipant participant in orderedParticipants)
            {
                PersistenceParticipantDescriptor descriptor = descriptorsByKey[participant.ParticipantKey];
                PersistenceParticipantSaveResult result = participant.CapturePayload();
                if (result == null || !result.Succeeded)
                {
                    throw new ParticipantSaveException(PersistenceSaveStatus.ParticipantCaptureFailed, $"Participant '{participant.ParticipantKey}' failed capture: {result?.Message ?? "No result."}");
                }

                if (string.IsNullOrWhiteSpace(result.PayloadJson))
                {
                    throw new ParticipantSaveException(PersistenceSaveStatus.ParticipantValidationFailed, $"Participant '{participant.ParticipantKey}' produced an empty payload.");
                }

                PersistenceParticipantPrepareResult prepareResult = participant.PreparePayload(result.PayloadJson, participant.ParticipantSchemaVersion);
                if (prepareResult == null || !prepareResult.Succeeded)
                {
                    throw new ParticipantSaveException(PersistenceSaveStatus.ParticipantValidationFailed, $"Participant '{participant.ParticipantKey}' failed self-validation: {prepareResult?.Message ?? "No result."}");
                }

                participant.DiscardPreparedPayload(prepareResult.PreparedPayload);
                envelope.participants.Add(new SaveParticipantRecord
                {
                    participantKey = participant.ParticipantKey,
                    participantSchemaVersion = descriptor.SchemaVersion,
                    required = descriptor.Required,
                    persistenceScope = (int)descriptor.Scope,
                    ownerId = descriptor.OwnerId,
                    loadPhase = (int)descriptor.LoadPhase,
                    loadPriority = descriptor.LoadPriority,
                    payloadJson = computeChecksum ? NormalizePayloadJson(result.PayloadJson) : result.PayloadJson
                });
            }

            envelope.participants.Sort(CompareRecords);
            if (computeChecksum)
            {
                envelope.contentChecksum = ComputeChecksumFromNormalizedPayloads(envelope);
            }
            return envelope;
        }

        private void CaptureDeferredParticipant(
            IPersistenceParticipant participant,
            GameSaveEnvelope envelope,
            List<PreparedPersistenceParticipant> captures)
        {
            SaveParticipantRecord record = CreateParticipantRecord(participant);
            long? capturedRevision = GetParticipantRevision(participant);

            if (participant is IDeferredPersistenceParticipant deferred)
            {
                DeferredPersistenceParticipantCapture capture = deferred.CaptureDeferredPayload();
                if (capture == null || !capture.Succeeded)
                {
                    throw new ParticipantSaveException(
                        PersistenceSaveStatus.ParticipantCaptureFailed,
                        $"Participant '{participant.ParticipantKey}' failed snapshot capture: {capture?.Message ?? "No result."}");
                }

                captures.Add(new PreparedPersistenceParticipant(record, capture, capturedRevision));
            }
            else
            {
                PersistenceParticipantSaveResult result = participant.CapturePayload();
                if (result == null || !result.Succeeded || string.IsNullOrWhiteSpace(result.PayloadJson))
                {
                    throw new ParticipantSaveException(
                        PersistenceSaveStatus.ParticipantCaptureFailed,
                        $"Participant '{participant.ParticipantKey}' failed capture: {result?.Message ?? "No result."}");
                }

                PersistenceParticipantPrepareResult prepareResult = participant.PreparePayload(result.PayloadJson, participant.ParticipantSchemaVersion);
                if (prepareResult == null || !prepareResult.Succeeded)
                {
                    throw new ParticipantSaveException(
                        PersistenceSaveStatus.ParticipantValidationFailed,
                        $"Participant '{participant.ParticipantKey}' failed self-validation: {prepareResult?.Message ?? "No result."}");
                }

                participant.DiscardPreparedPayload(prepareResult.PreparedPayload);
                record.payloadJson = NormalizePayloadJsonStrict(result.PayloadJson);
                captures.Add(new PreparedPersistenceParticipant(record, null, capturedRevision));
            }

            envelope.participants.Add(record);
        }

        private void AddDeferredParticipantCapture(
            IPersistenceParticipant participant,
            DeferredPersistenceParticipantCapture deferredCapture,
            GameSaveEnvelope envelope,
            List<PreparedPersistenceParticipant> captures)
        {
            if (deferredCapture == null || !deferredCapture.Succeeded)
            {
                throw new ParticipantSaveException(
                    PersistenceSaveStatus.ParticipantCaptureFailed,
                    $"Participant '{participant?.ParticipantKey}' failed incremental snapshot capture: {deferredCapture?.Message ?? "No result."}");
            }

            SaveParticipantRecord record = CreateParticipantRecord(participant);
            captures.Add(new PreparedPersistenceParticipant(record, deferredCapture, GetParticipantRevision(participant)));
            envelope.participants.Add(record);
        }

        private bool TryUseCachedParticipantPayload(
            IPersistenceParticipant participant,
            GameSaveEnvelope envelope,
            List<PreparedPersistenceParticipant> captures)
        {
            long? revision = GetParticipantRevision(participant);
            if (!revision.HasValue
                || !participantPayloadCache.TryGetValue(participant.ParticipantKey, out CachedParticipantPayload cached)
                || cached.Revision != revision.Value
                || cached.SchemaVersion != participant.ParticipantSchemaVersion
                || string.IsNullOrWhiteSpace(cached.PayloadJson))
            {
                return false;
            }

            SaveParticipantRecord record = CreateParticipantRecord(participant);
            record.payloadJson = cached.PayloadJson;
            captures.Add(new PreparedPersistenceParticipant(record, null, revision, wasReused: true));
            envelope.participants.Add(record);
            return true;
        }

        private SaveParticipantRecord CreateParticipantRecord(IPersistenceParticipant participant)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            PersistenceParticipantDescriptor descriptor = descriptorsByKey[participant.ParticipantKey];
            return new SaveParticipantRecord
            {
                participantKey = participant.ParticipantKey,
                participantSchemaVersion = descriptor.SchemaVersion,
                required = descriptor.Required,
                persistenceScope = (int)descriptor.Scope,
                ownerId = descriptor.OwnerId,
                loadPhase = (int)descriptor.LoadPhase,
                loadPriority = descriptor.LoadPriority,
                payloadJson = string.Empty
            };
        }

        private static long? GetParticipantRevision(IPersistenceParticipant participant)
        {
            return participant is IPersistenceRevisionParticipant revisionParticipant
                ? revisionParticipant.PersistenceRevision
                : null;
        }

        private void RememberParticipantPayloads(PreparedPersistenceSave prepared)
        {
            if (prepared?.Participants == null) return;
            for (int i = 0; i < prepared.Participants.Count; i++)
            {
                PreparedPersistenceParticipant participant = prepared.Participants[i];
                if (participant?.Record == null
                    || !participant.CapturedRevision.HasValue
                    || string.IsNullOrWhiteSpace(participant.Record.payloadJson))
                {
                    continue;
                }

                participantPayloadCache[participant.Record.participantKey] = new CachedParticipantPayload(
                    participant.Record.participantSchemaVersion,
                    participant.CapturedRevision.Value,
                    participant.Record.payloadJson);
            }
        }

        private GameSaveEnvelope CreateEnvelope(string slotId, string displayName, SaveSlotPaths paths, string transactionId)
        {
            string now = DateTime.UtcNow.ToString("o");
            SaveEnvelopeHeader previous = paths == null ? null : TryReadEnvelopeHeader(paths);
            GameSaveEnvelope envelope = new GameSaveEnvelope
            {
                formatIdentifier = FormatIdentifier,
                schemaVersion = CurrentSchemaVersion,
                persistenceContext = (int)ContextKind,
                gameVersion = GameVersion,
                saveId = string.IsNullOrWhiteSpace(previous?.SaveId) ? Guid.NewGuid().ToString("N") : previous.SaveId,
                slotId = slotId,
                displayName = string.IsNullOrWhiteSpace(displayName) ? slotId : displayName,
                worldId = WorldId,
                playerId = PlayerId,
                accountId = AccountId,
                createdUtc = string.IsNullOrWhiteSpace(previous?.CreatedUtc) ? now : previous.CreatedUtc,
                lastWrittenUtc = now,
                playtimeSeconds = PlaytimeSecondsProvider == null ? previous?.PlaytimeSeconds ?? 0 : Math.Max(0d, PlaytimeSecondsProvider.Invoke()),
                sceneSummary = string.Empty,
                placeSummary = string.Empty,
                playerSummary = string.Empty,
                transactionId = string.IsNullOrWhiteSpace(transactionId) ? Guid.NewGuid().ToString("N") : transactionId,
                parentTransactionId = previous?.TransactionId ?? string.Empty,
                saveRevision = Math.Max(0, previous?.SaveRevision ?? 0) + 1,
                completedWriteMarker = true
            };

            SaveMetadataSnapshot metadata = MetadataProvider?.CaptureMetadata();
            if (metadata != null)
            {
                envelope.sceneSummary = metadata.SceneId ?? string.Empty;
                envelope.placeSummary = metadata.PlaceId ?? string.Empty;
                envelope.playerSummary = metadata.PlayerSummary ?? string.Empty;
            }

            return envelope;
        }

        private PersistenceSaveResult WriteAtomically(SaveSlotPaths paths, string serialized, string transactionId, bool updateOperationState = true)
        {
            try
            {
                if (updateOperationState) SetOperation(PersistenceOperationState.Writing, PersistenceTransactionPhase.WriteTemporary, transactionId, isSave: true);
                DeleteIfExists(paths.TemporaryPath);
                File.WriteAllText(paths.TemporaryPath, serialized, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.TemporaryWriteFailed, paths.SlotId, paths.PrimaryPath, "Could not write temporary save file.", exception, transactionId, PersistenceTransactionPhase.WriteTemporary);
            }

            if (updateOperationState) SetOperation(PersistenceOperationState.Writing, PersistenceTransactionPhase.VerifyTemporary, transactionId, isSave: true);
            if (updateOperationState && Faulted(PersistenceFaultInjectionPoint.TemporaryVerification, out string tempFailure))
            {
                DeleteIfExists(paths.TemporaryPath);
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.TemporaryWriteFailed, paths.SlotId, paths.PrimaryPath, tempFailure, transactionId: transactionId, phase: PersistenceTransactionPhase.VerifyTemporary);
            }

            PersistenceValidationResult tempValidation = ValidatePath(paths.SlotId, paths.TemporaryPath, isBackup: false, validateParticipants: false);
            if (!tempValidation.Succeeded)
            {
                DeleteIfExists(paths.TemporaryPath);
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.TemporaryWriteFailed, paths.SlotId, paths.PrimaryPath, $"Temporary save validation failed: {tempValidation.Message}", transactionId: transactionId, phase: PersistenceTransactionPhase.VerifyTemporary);
            }

            try
            {
                if (updateOperationState) SetOperation(PersistenceOperationState.Writing, PersistenceTransactionPhase.PreservePrevious, transactionId, isSave: true);
                if (updateOperationState && Faulted(PersistenceFaultInjectionPoint.BackupPreservation, out string backupFailure))
                {
                    DeleteIfExists(paths.TemporaryPath);
                    return PersistenceSaveResult.Failure(PersistenceSaveStatus.BackupFailed, paths.SlotId, paths.PrimaryPath, backupFailure, transactionId: transactionId, phase: PersistenceTransactionPhase.PreservePrevious);
                }

                if (File.Exists(paths.PrimaryPath))
                {
                    File.Copy(paths.PrimaryPath, paths.BackupPath, true);
                }
            }
            catch (Exception exception)
            {
                DeleteIfExists(paths.TemporaryPath);
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.BackupFailed, paths.SlotId, paths.PrimaryPath, "Could not preserve previous save as backup.", exception, transactionId, PersistenceTransactionPhase.PreservePrevious);
            }

            try
            {
                if (updateOperationState) SetOperation(PersistenceOperationState.Writing, PersistenceTransactionPhase.PromotePrimary, transactionId, isSave: true);
                if (updateOperationState && Faulted(PersistenceFaultInjectionPoint.PrimaryPromotion, out string promotionFailure))
                {
                    DeleteIfExists(paths.TemporaryPath);
                    return PersistenceSaveResult.Failure(PersistenceSaveStatus.ReplacementFailed, paths.SlotId, paths.PrimaryPath, promotionFailure, transactionId: transactionId, phase: PersistenceTransactionPhase.PromotePrimary);
                }

                if (File.Exists(paths.PrimaryPath))
                {
                    File.Delete(paths.PrimaryPath);
                }

                File.Move(paths.TemporaryPath, paths.PrimaryPath);
                return PersistenceSaveResult.Success(paths.SlotId, paths.PrimaryPath, "Atomic save write completed.", transactionId, PersistenceTransactionPhase.PromotePrimary);
            }
            catch (Exception exception)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.ReplacementFailed, paths.SlotId, paths.PrimaryPath, "Could not replace primary save file.", exception, transactionId, PersistenceTransactionPhase.PromotePrimary);
            }
        }

        private PersistenceLoadResult PrepareAndCommit(string slotId, string path, GameSaveEnvelope envelope, bool loadedBackup, string transactionId)
        {
            List<PreparedParticipant> prepared = new List<PreparedParticipant>();
            Dictionary<string, SaveParticipantRecord> records = BuildRecordMap(envelope, out PersistenceLoadResult mapFailure);
            if (mapFailure != null)
            {
                return mapFailure;
            }

            IReadOnlyList<IPersistenceParticipant> orderedParticipants = GetOrderedParticipants();
            foreach (IPersistenceParticipant participant in orderedParticipants)
            {
                if (!records.TryGetValue(participant.ParticipantKey, out SaveParticipantRecord record))
                {
                    if (participant.IsRequired)
                    {
                        DiscardPrepared(prepared);
                        return PersistenceLoadResult.Failure(PersistenceLoadStatus.MissingRequiredParticipantPayload, slotId, path, $"Save is missing required participant '{participant.ParticipantKey}'.", transactionId: transactionId, phase: PersistenceTransactionPhase.PrepareParticipants);
                    }

                    continue;
                }

                if (!ValidateRuntimeParticipantRecord(participant, record, out string ownershipFailure))
                {
                    DiscardPrepared(prepared);
                    return PersistenceLoadResult.Failure(PersistenceLoadStatus.ParticipantPrepareFailed, slotId, path, ownershipFailure, transactionId: transactionId, phase: PersistenceTransactionPhase.PrepareParticipants);
                }

                if (Faulted(PersistenceFaultInjectionPoint.LoadPrepare, out string prepareFailure))
                {
                    DiscardPrepared(prepared);
                    return PersistenceLoadResult.Failure(PersistenceLoadStatus.ParticipantPrepareFailed, slotId, path, prepareFailure, transactionId: transactionId, phase: PersistenceTransactionPhase.PrepareParticipants);
                }

                PersistenceParticipantPrepareResult prepare = participant.PreparePayload(record.payloadJson, record.participantSchemaVersion);
                if (prepare == null || !prepare.Succeeded)
                {
                    DiscardPrepared(prepared);
                    return PersistenceLoadResult.Failure(PersistenceLoadStatus.ParticipantPrepareFailed, slotId, path, $"Participant '{participant.ParticipantKey}' failed prepare: {prepare?.Message ?? "No result."}", transactionId: transactionId, phase: PersistenceTransactionPhase.PrepareParticipants);
                }

                prepared.Add(new PreparedParticipant(participant, prepare.PreparedPayload));
            }

            foreach (SaveParticipantRecord record in envelope.participants)
            {
                if (!participantsByKey.TryGetValue(record.participantKey, out _) && record.required)
                {
                    DiscardPrepared(prepared);
                    return PersistenceLoadResult.Failure(PersistenceLoadStatus.MissingRuntimeParticipant, slotId, path, $"Required save participant '{record.participantKey}' is not registered at runtime.", transactionId: transactionId, phase: PersistenceTransactionPhase.PrepareParticipants);
                }
            }

            SetOperation(PersistenceOperationState.PreparingLoad, PersistenceTransactionPhase.CaptureRollback, transactionId, isSave: false);
            List<PreparedParticipant> rollbackPrepared;
            PersistenceLoadResult rollbackCapture = CaptureRollbackSnapshot(slotId, path, transactionId, out rollbackPrepared);
            if (rollbackCapture != null)
            {
                DiscardPrepared(prepared);
                return rollbackCapture;
            }

            List<PreparedParticipant> committed = new List<PreparedParticipant>();
            runtimeSafety = PersistenceRuntimeSafety.Restoring;
            SetOperation(PersistenceOperationState.CommittingParticipants, PersistenceTransactionPhase.CommitParticipants, transactionId, isSave: false);
            using (PersistenceRestorationGuard.Enter(transactionId, rollback: false))
            {
                for (int i = 0; i < prepared.Count; i++)
                {
                    if (Faulted(PersistenceFaultInjectionPoint.LoadCommit, out string injectedCommitFailure))
                    {
                        return HandleCommitFailure(slotId, path, transactionId, $"Injected commit failure before '{prepared[i].Participant.ParticipantKey}': {injectedCommitFailure}", prepared, rollbackPrepared, committed, PersistenceLoadStatus.ParticipantCommitFailedRollbackSucceeded, PersistenceLoadStatus.ParticipantCommitFailedRollbackFailed);
                    }

                    PersistenceParticipantCommitResult commit = prepared[i].Participant.CommitPreparedPayload(prepared[i].PreparedPayload);
                    if (commit == null || !commit.Succeeded)
                    {
                        return HandleCommitFailure(slotId, path, transactionId, $"Participant '{prepared[i].Participant.ParticipantKey}' failed commit: {commit?.Message ?? "No result."}", prepared, rollbackPrepared, committed, PersistenceLoadStatus.ParticipantCommitFailedRollbackSucceeded, PersistenceLoadStatus.ParticipantCommitFailedRollbackFailed);
                    }

                    committed.Add(prepared[i]);
                }
            }

            SetOperation(PersistenceOperationState.ConsistencyAudit, PersistenceTransactionPhase.ConsistencyAudit, transactionId, isSave: false);
            PersistenceConsistencyAuditReport audit = RunConsistencyAudit();
            if (audit.HasCriticalFinding || !audit.succeeded)
            {
                return HandleCommitFailure(slotId, path, transactionId, $"Consistency audit failed: {audit.message}", prepared, rollbackPrepared, committed, PersistenceLoadStatus.ConsistencyAuditFailedRollbackSucceeded, PersistenceLoadStatus.ConsistencyAuditFailedRollbackFailed);
            }

            DiscardPrepared(rollbackPrepared);
            runtimeSafety = PersistenceRuntimeSafety.Safe;
            DiscardPrepared(prepared);
            return PersistenceLoadResult.Success(slotId, path, loadedBackup, loadedBackup ? $"Loaded backup save slot '{slotId}'." : $"Loaded save slot '{slotId}'.", transactionId);
        }

        private PersistenceLoadResult CaptureRollbackSnapshot(string slotId, string path, string transactionId, out List<PreparedParticipant> rollbackPrepared)
        {
            rollbackPrepared = new List<PreparedParticipant>();
            try
            {
                IReadOnlyList<IPersistenceParticipant> orderedParticipants = GetOrderedParticipants();
                foreach (IPersistenceParticipant participant in orderedParticipants)
                {
                    PersistenceParticipantSaveResult capture = participant.CapturePayload();
                    if (capture == null || !capture.Succeeded || string.IsNullOrWhiteSpace(capture.PayloadJson))
                    {
                        DiscardPrepared(rollbackPrepared);
                        return PersistenceLoadResult.Failure(PersistenceLoadStatus.RollbackCaptureFailed, slotId, path, $"Rollback capture failed for '{participant.ParticipantKey}': {capture?.Message ?? "No result."}", transactionId: transactionId, phase: PersistenceTransactionPhase.CaptureRollback);
                    }

                    PersistenceParticipantPrepareResult prepare = participant.PreparePayload(capture.PayloadJson, participant.ParticipantSchemaVersion);
                    if (prepare == null || !prepare.Succeeded)
                    {
                        DiscardPrepared(rollbackPrepared);
                        return PersistenceLoadResult.Failure(PersistenceLoadStatus.RollbackCaptureFailed, slotId, path, $"Rollback prepare failed for '{participant.ParticipantKey}': {prepare?.Message ?? "No result."}", transactionId: transactionId, phase: PersistenceTransactionPhase.CaptureRollback);
                    }

                    rollbackPrepared.Add(new PreparedParticipant(participant, prepare.PreparedPayload));
                }
            }
            catch (Exception exception)
            {
                DiscardPrepared(rollbackPrepared);
                return PersistenceLoadResult.Failure(PersistenceLoadStatus.RollbackCaptureFailed, slotId, path, "Unexpected rollback capture failure.", exception: exception, transactionId: transactionId, phase: PersistenceTransactionPhase.CaptureRollback);
            }

            return null;
        }

        private PersistenceLoadResult HandleCommitFailure(
            string slotId,
            string path,
            string transactionId,
            string failureMessage,
            List<PreparedParticipant> prepared,
            List<PreparedParticipant> rollbackPrepared,
            List<PreparedParticipant> committed,
            PersistenceLoadStatus rollbackSucceededStatus,
            PersistenceLoadStatus rollbackFailedStatus)
        {
            bool rollbackSucceeded = TryRollback(transactionId, rollbackPrepared, committed, out string rollbackMessage);
            DiscardPrepared(prepared);
            DiscardPrepared(rollbackPrepared);

            if (rollbackSucceeded)
            {
                runtimeSafety = PersistenceRuntimeSafety.RolledBack;
                return PersistenceLoadResult.Failure(rollbackSucceededStatus, slotId, path, $"{failureMessage} Rollback succeeded. {rollbackMessage}", transactionId: transactionId, phase: PersistenceTransactionPhase.RollingBack, rollbackAttempted: true, rollbackSucceeded: true, runtimeSafety: runtimeSafety);
            }

            runtimeSafety = PersistenceRuntimeSafety.Unsafe;
            return PersistenceLoadResult.Failure(rollbackFailedStatus, slotId, path, $"{failureMessage} Rollback failed. {rollbackMessage}", transactionId: transactionId, phase: PersistenceTransactionPhase.RollingBack, rollbackAttempted: true, rollbackSucceeded: false, runtimeSafety: runtimeSafety);
        }

        private bool TryRollback(string transactionId, List<PreparedParticipant> rollbackPrepared, List<PreparedParticipant> committed, out string message)
        {
            message = "No participants required rollback.";
            SetOperation(PersistenceOperationState.RollingBack, PersistenceTransactionPhase.RollingBack, transactionId, isSave: false);
            if (committed == null || committed.Count == 0)
            {
                return true;
            }

            Dictionary<string, PreparedParticipant> rollbackByKey = new Dictionary<string, PreparedParticipant>(StringComparer.Ordinal);
            for (int i = 0; i < rollbackPrepared.Count; i++)
            {
                rollbackByKey[rollbackPrepared[i].Participant.ParticipantKey] = rollbackPrepared[i];
            }

            using (PersistenceRestorationGuard.Enter(transactionId, rollback: true))
            {
                for (int i = committed.Count - 1; i >= 0; i--)
                {
                    string key = committed[i].Participant.ParticipantKey;
                    if (!rollbackByKey.TryGetValue(key, out PreparedParticipant rollback))
                    {
                        message = $"No rollback payload was available for '{key}'.";
                        return false;
                    }

                    if (Faulted(PersistenceFaultInjectionPoint.RollbackCommit, out string injectedFailure))
                    {
                        message = injectedFailure;
                        return false;
                    }

                    PersistenceParticipantCommitResult result = rollback.Participant.CommitPreparedPayload(rollback.PreparedPayload);
                    if (result == null || !result.Succeeded)
                    {
                        message = $"Rollback failed for '{key}': {result?.Message ?? "No result."}";
                        return false;
                    }
                }
            }

            message = "Committed participants were restored in reverse order.";
            return true;
        }

        private PersistenceConsistencyAuditReport RunConsistencyAudit()
        {
            PersistenceConsistencyAuditReport report;
            if (Faulted(PersistenceFaultInjectionPoint.ConsistencyAudit, out string injectedFailure))
            {
                report = PersistenceConsistencyAuditReport.Critical("InjectedConsistencyAuditFailure", injectedFailure);
            }
            else
            {
                report = RunRegisteredConsistencyValidators();
            }

            lastConsistencyAudit = report.message ?? string.Empty;
            ConsistencyAuditCompleted?.Invoke(report);
            return report;
        }

        private PersistenceConsistencyAuditReport RunRegisteredConsistencyValidators()
        {
            if (consistencyValidators.Count == 0)
            {
                return PersistenceConsistencyAuditReport.Success("No domain consistency validators are registered for this persistence context.");
            }

            List<PersistenceConsistencyFinding> findings = new List<PersistenceConsistencyFinding>();
            bool requiredFailure = false;
            for (int i = 0; i < consistencyValidators.Count; i++)
            {
                IPersistenceConsistencyValidator validator = consistencyValidators[i];
                PersistenceConsistencyAuditReport result;
                try
                {
                    result = validator.Validate();
                }
                catch (Exception exception)
                {
                    result = PersistenceConsistencyAuditReport.Critical(
                        "ValidatorException",
                        $"Consistency validator '{validator.ValidatorKey}' threw {exception.GetType().Name}: {exception.Message}");
                }

                if (result == null)
                {
                    result = PersistenceConsistencyAuditReport.Critical("MissingAuditReport", $"Consistency validator '{validator.ValidatorKey}' returned no report.");
                }

                if (result.findings != null)
                {
                    findings.AddRange(result.findings);
                }

                requiredFailure |= validator.IsRequired && (!result.succeeded || result.HasCriticalFinding);
            }

            return new PersistenceConsistencyAuditReport
            {
                succeeded = !requiredFailure,
                message = requiredFailure
                    ? $"{ContextKind} persistence consistency validation failed."
                    : $"{ContextKind} persistence consistency validation passed.",
                findings = findings.ToArray()
            };
        }

        private PersistenceValidationResult ValidatePath(string slotId, string path, bool isBackup, bool validateParticipants)
        {
            if (!File.Exists(path))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.FileMissing, slotId, path, isBackup ? "Backup save file is missing." : "Save file is missing.");
            }

            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.ReadFailed, slotId, path, "Could not read save file.", false, exception);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.MalformedJson, slotId, path, "Save file is empty.");
            }

            GameSaveEnvelope envelope;
            try
            {
                envelope = serializer.Deserialize<GameSaveEnvelope>(json);
            }
            catch (Exception exception)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.MalformedJson, slotId, path, "Save file is malformed JSON.", false, exception);
            }

            if (envelope == null)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.MalformedJson, slotId, path, "Save file did not contain a readable envelope.");
            }

            PersistenceValidationResult envelopeResult = ValidateEnvelope(slotId, path, envelope, validateParticipants);
            return envelopeResult.Succeeded
                ? PersistenceValidationResult.Success(slotId, path, envelope, "Save file is valid.")
                : envelopeResult;
        }

        private PersistenceValidationResult ValidateEnvelope(string slotId, string path, GameSaveEnvelope envelope, bool validateParticipants)
        {
            if (envelope.formatIdentifier != FormatIdentifier)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.WrongFormatIdentifier, slotId, path, $"Wrong save format '{envelope.formatIdentifier}'.");
            }

            if (envelope.schemaVersion != CurrentSchemaVersion)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.UnsupportedSchemaVersion, slotId, path, $"Unsupported save schema version {envelope.schemaVersion}.");
            }

            if (!string.Equals(envelope.gameVersion, GameVersion, StringComparison.Ordinal))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.UnsupportedGameVersion, slotId, path, $"Save game version '{envelope.gameVersion}' does not match runtime version '{GameVersion}'.");
            }

            if (envelope.persistenceContext != (int)ContextKind)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.WrongSaveContext, slotId, path, $"Save context {(PersistenceContextKind)envelope.persistenceContext} cannot be loaded by the {ContextKind} service.");
            }

            if (!string.Equals(envelope.slotId, slotId, StringComparison.Ordinal))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.WrongSlot, slotId, path, $"Save declares slot '{envelope.slotId}' instead of requested slot '{slotId}'.");
            }

            if (!string.Equals(envelope.worldId, WorldId, StringComparison.Ordinal))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.WrongWorld, slotId, path, $"Save belongs to world '{envelope.worldId}', not '{WorldId}'.");
            }

            if (!string.Equals(envelope.playerId ?? string.Empty, PlayerId ?? string.Empty, StringComparison.Ordinal))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.WrongPlayer, slotId, path, $"Save belongs to player '{envelope.playerId}', not '{PlayerId}'.");
            }

            if (!string.Equals(envelope.accountId, AccountId, StringComparison.Ordinal))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.WrongAccount, slotId, path, $"Save belongs to account '{envelope.accountId}', not '{AccountId}'.");
            }

            if (!envelope.completedWriteMarker)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.IncompleteWrite, slotId, path, "Save does not contain a completed-write marker.");
            }

            envelope.participants?.Sort(CompareRecords);
            string expectedChecksum = ComputeChecksumFromNormalizedPayloads(envelope);
            if (!string.Equals(envelope.contentChecksum, expectedChecksum, StringComparison.Ordinal))
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.ChecksumMismatch, slotId, path, "Save checksum does not match the envelope content.");
            }

            Dictionary<string, SaveParticipantRecord> records = BuildRecordMap(envelope, out PersistenceLoadResult mapFailure);
            if (mapFailure != null)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.DuplicateParticipantKey, slotId, path, mapFailure.Message);
            }

            if (!validateParticipants)
            {
                return PersistenceValidationResult.Success(slotId, path, envelope, "Save envelope is valid.");
            }

            PersistenceDependencyReport dependencyReport = BuildParticipantDependencyReport();
            if (!dependencyReport.succeeded)
            {
                return PersistenceValidationResult.Failure(PersistenceValidationStatus.DependencyValidationFailed, slotId, path, dependencyReport.message);
            }

            foreach (IPersistenceParticipant participant in GetOrderedParticipants())
            {
                if (!records.TryGetValue(participant.ParticipantKey, out SaveParticipantRecord record))
                {
                    if (participant.IsRequired)
                    {
                        return PersistenceValidationResult.Failure(PersistenceValidationStatus.MissingRequiredParticipantPayload, slotId, path, $"Save is missing required participant '{participant.ParticipantKey}'.");
                    }

                    continue;
                }

                if (!ValidateRuntimeParticipantRecord(participant, record, out string ownershipFailure))
                {
                    return PersistenceValidationResult.Failure(PersistenceValidationStatus.ParticipantPrepareFailed, slotId, path, ownershipFailure);
                }

                PersistenceParticipantPrepareResult prepare = participant.PreparePayload(record.payloadJson, record.participantSchemaVersion);
                if (prepare == null || !prepare.Succeeded)
                {
                    return PersistenceValidationResult.Failure(PersistenceValidationStatus.ParticipantPrepareFailed, slotId, path, $"Participant '{participant.ParticipantKey}' failed validation: {prepare?.Message ?? "No result."}");
                }

                participant.DiscardPreparedPayload(prepare.PreparedPayload);
            }

            foreach (SaveParticipantRecord record in envelope.participants)
            {
                if (!participantsByKey.ContainsKey(record.participantKey) && record.required)
                {
                    return PersistenceValidationResult.Failure(PersistenceValidationStatus.MissingRuntimeParticipant, slotId, path, $"Required save participant '{record.participantKey}' is not registered.");
                }
            }

            return PersistenceValidationResult.Success(slotId, path, envelope, "Save file is valid.");
        }

        private SaveSlotMetadata ReadMetadata(SaveSlotPaths paths)
        {
            FileInfo primary = new FileInfo(paths.PrimaryPath);
            PersistenceValidationResult validation = ValidatePath(paths.SlotId, paths.PrimaryPath, isBackup: false, validateParticipants: false);
            GameSaveEnvelope envelope = validation.Envelope;
            return new SaveSlotMetadata
            {
                slotId = paths.SlotId,
                displayName = envelope == null ? paths.SlotId : envelope.displayName,
                saveId = envelope == null ? string.Empty : envelope.saveId,
                createdUtc = envelope == null ? string.Empty : envelope.createdUtc,
                lastWrittenUtc = envelope == null ? string.Empty : envelope.lastWrittenUtc,
                modifiedUtc = primary.Exists ? primary.LastWriteTimeUtc.ToString("o") : string.Empty,
                playtimeSeconds = envelope?.playtimeSeconds ?? 0,
                sceneSummary = envelope == null ? string.Empty : envelope.sceneSummary,
                currentPlaceSummary = envelope == null ? string.Empty : envelope.placeSummary,
                playerSummary = envelope == null ? string.Empty : envelope.playerSummary,
                worldId = envelope == null ? string.Empty : envelope.worldId,
                playerId = envelope == null ? string.Empty : envelope.playerId,
                accountId = envelope == null ? string.Empty : envelope.accountId,
                schemaVersion = envelope?.schemaVersion ?? 0,
                gameVersion = envelope == null ? string.Empty : envelope.gameVersion,
                transactionId = envelope == null ? string.Empty : envelope.transactionId,
                parentTransactionId = envelope == null ? string.Empty : envelope.parentTransactionId,
                saveRevision = envelope?.saveRevision ?? 0,
                completedWriteMarker = envelope != null && envelope.completedWriteMarker,
                fileSizeBytes = primary.Exists ? primary.Length : 0,
                hasPrimary = primary.Exists,
                hasBackup = File.Exists(paths.BackupPath),
                isValid = validation.Succeeded,
                status = validation.Status.ToString(),
                message = validation.Message
            };
        }

        private bool IsBackupValid(string slotId, string backupPath)
        {
            return ValidatePath(slotId, backupPath, isBackup: true, validateParticipants: false).Succeeded;
        }

        private IReadOnlyList<IPersistenceParticipant> GetOrderedParticipants()
        {
            PersistenceDependencyReport report = BuildParticipantDependencyReport();
            if (!report.succeeded || report.orderedParticipantKeys == null || report.orderedParticipantKeys.Length == 0)
            {
                List<IPersistenceParticipant> fallback = new List<IPersistenceParticipant>(participants);
                fallback.Sort(CompareParticipants);
                return fallback;
            }

            List<IPersistenceParticipant> ordered = new List<IPersistenceParticipant>();
            for (int i = 0; i < report.orderedParticipantKeys.Length; i++)
            {
                if (participantsByKey.TryGetValue(report.orderedParticipantKeys[i], out IPersistenceParticipant participant))
                {
                    ordered.Add(participant);
                }
            }

            return ordered;
        }

        private static void VisitParticipant(
            IPersistenceParticipant participant,
            Dictionary<string, IPersistenceParticipant> registered,
            List<IPersistenceParticipant> ordered,
            HashSet<string> visiting,
            HashSet<string> visited,
            List<string> circular)
        {
            if (participant == null || visited.Contains(participant.ParticipantKey))
            {
                return;
            }

            if (!visiting.Add(participant.ParticipantKey))
            {
                circular.Add(participant.ParticipantKey);
                return;
            }

            PersistenceParticipantDependencyMetadata metadata = BuildDependencyMetadata(participant);
            string[] dependencies = MergeDependencies(metadata.requiredDependencies, metadata.optionalDependencies);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (registered.TryGetValue(dependencies[i], out IPersistenceParticipant dependency))
                {
                    VisitParticipant(dependency, registered, ordered, visiting, visited, circular);
                }
            }

            visiting.Remove(participant.ParticipantKey);
            visited.Add(participant.ParticipantKey);
            if (!ordered.Contains(participant))
            {
                ordered.Add(participant);
            }
        }

        private static PersistenceParticipantDependencyMetadata BuildDependencyMetadata(IPersistenceParticipant participant)
        {
            PersistenceParticipantDescriptor descriptor = PersistenceParticipantDescriptor.From(participant);

            return new PersistenceParticipantDependencyMetadata
            {
                participantKey = descriptor?.Key ?? string.Empty,
                requiredDependencies = ToArray(descriptor?.RequiredDependencies),
                optionalDependencies = ToArray(descriptor?.OptionalDependencies),
                supportsRollback = descriptor?.SupportsRollback ?? false,
                requiresSceneReadiness = descriptor?.RequiresSceneReadiness ?? false,
                requiresDefinitionRegistry = descriptor?.RequiresDefinitionRegistry ?? false,
                requiresWorldEntityRegistry = descriptor?.RequiresWorldEntityRegistry ?? false
            };
        }

        private static string[] MergeDependencies(string[] defaults, IReadOnlyList<string> provided)
        {
            HashSet<string> merged = new HashSet<string>(StringComparer.Ordinal);
            if (defaults != null)
            {
                for (int i = 0; i < defaults.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(defaults[i]))
                    {
                        merged.Add(defaults[i]);
                    }
                }
            }

            if (provided != null)
            {
                for (int i = 0; i < provided.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(provided[i]))
                    {
                        merged.Add(provided[i]);
                    }
                }
            }

            string[] result = new string[merged.Count];
            merged.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private bool AcceptsScope(PersistenceScope scope)
        {
            return ContextKind switch
            {
                PersistenceContextKind.Player => scope == PersistenceScope.Player,
                PersistenceContextKind.World => scope == PersistenceScope.SharedWorld || scope == PersistenceScope.RegionOrScene,
                PersistenceContextKind.Account => scope == PersistenceScope.Account,
                _ => false
            };
        }

        private static string[] ToArray(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> result = new List<string>();
            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    result.Add(values[i]);
                }
            }

            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private void AddAutosaveRecoveryCandidate(List<SaveRecoveryCandidate> candidates, List<string> corruptFiles, string slotId, SaveRecoverySource source)
        {
            if (!pathProvider.TryGetPaths(slotId, out SaveSlotPaths paths, out _))
            {
                return;
            }

            AddRecoveryCandidate(candidates, corruptFiles, slotId, paths.PrimaryPath, source, RecoveryRecommendationAction.LoadAutosave);
        }

        private void AddRecoveryCandidate(
            List<SaveRecoveryCandidate> candidates,
            List<string> corruptFiles,
            string slotId,
            string path,
            SaveRecoverySource source,
            RecoveryRecommendationAction action)
        {
            if (!File.Exists(path))
            {
                return;
            }

            PersistenceValidationResult validation = ValidatePath(slotId, path, source == SaveRecoverySource.RequestedBackup, validateParticipants: false);
            if (!validation.Succeeded)
            {
                corruptFiles.Add(path);
            }

            candidates.Add(new SaveRecoveryCandidate
            {
                source = source,
                action = validation.Succeeded ? action : RecoveryRecommendationAction.None,
                slotId = slotId,
                path = path,
                valid = validation.Succeeded,
                message = validation.Message
            });
        }

        private static string BuildQuarantinePath(string path)
        {
            return path + ".quarantine." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".json";
        }

        private Dictionary<string, SaveParticipantRecord> BuildRecordMap(GameSaveEnvelope envelope, out PersistenceLoadResult failure)
        {
            failure = null;
            Dictionary<string, SaveParticipantRecord> records = new Dictionary<string, SaveParticipantRecord>(StringComparer.Ordinal);
            IReadOnlyList<SaveParticipantRecord> participantRecords = envelope.participants == null
                ? Array.Empty<SaveParticipantRecord>()
                : envelope.participants;
            for (int i = 0; i < participantRecords.Count; i++)
            {
                SaveParticipantRecord record = participantRecords[i];
                if (record == null || string.IsNullOrWhiteSpace(record.participantKey))
                {
                    failure = PersistenceLoadResult.Failure(PersistenceLoadStatus.DuplicateParticipantKey, envelope.slotId, string.Empty, "Save contains an empty participant key.");
                    return records;
                }

                if (records.ContainsKey(record.participantKey))
                {
                    failure = PersistenceLoadResult.Failure(PersistenceLoadStatus.DuplicateParticipantKey, envelope.slotId, string.Empty, $"Save contains duplicate participant '{record.participantKey}'.");
                    return records;
                }

                records.Add(record.participantKey, record);
            }

            return records;
        }

        private static bool ValidateRuntimeParticipantRecord(IPersistenceParticipant participant, SaveParticipantRecord record, out string failureReason)
        {
            failureReason = string.Empty;
            if (participant == null || record == null)
            {
                failureReason = "Cannot validate a missing persistence participant record.";
                return false;
            }

            if (record.persistenceScope != (int)participant.Scope)
            {
                failureReason = $"Participant '{participant.ParticipantKey}' expected scope {participant.Scope} but save record has {(PersistenceScope)record.persistenceScope}.";
                return false;
            }

            string expectedOwner = participant.OwnerId ?? string.Empty;
            string savedOwner = record.ownerId ?? string.Empty;
            if (!string.Equals(savedOwner, expectedOwner, StringComparison.Ordinal))
            {
                failureReason = $"Participant '{participant.ParticipantKey}' expected owner '{expectedOwner}' but save record has owner '{savedOwner}'.";
                return false;
            }

            return true;
        }

        public static string ComputeChecksum(GameSaveEnvelope envelope)
        {
            return ComputeChecksum(envelope, normalizePayloads: true);
        }

        private PersistenceSaveResult WritePreparedSaveStreaming(
            SaveSlotPaths paths,
            GameSaveEnvelope envelope,
            IStreamingSaveSerializer streamingSerializer,
            string transactionId,
            out double serializationMilliseconds,
            out double atomicWriteMilliseconds,
            out long serializedBytes)
        {
            serializationMilliseconds = 0d;
            atomicWriteMilliseconds = 0d;
            serializedBytes = 0L;
            try
            {
                DeleteIfExists(paths.TemporaryPath);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                using (var stream = new FileStream(
                           paths.TemporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           StreamingIoBufferBytes,
                           FileOptions.SequentialScan))
                using (var writer = new StreamWriter(stream, Utf8WithoutBom, StreamingIoBufferBytes, leaveOpen: true))
                {
                    streamingSerializer.Serialize(writer, envelope, false);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                    serializedBytes = stream.Length;
                }
                timer.Stop();
                serializationMilliseconds = timer.Elapsed.TotalMilliseconds;
                if (serializedBytes <= 2L)
                {
                    DeleteIfExists(paths.TemporaryPath);
                    return PersistenceSaveResult.Failure(
                        PersistenceSaveStatus.TemporaryWriteFailed,
                        paths.SlotId,
                        paths.PrimaryPath,
                        "Streamed save file was empty.",
                        transactionId: transactionId,
                        phase: PersistenceTransactionPhase.WriteTemporary);
                }

                timer.Restart();
                if (File.Exists(paths.PrimaryPath))
                {
                    DeleteIfExists(paths.BackupPath);
                    try
                    {
                        // File.Replace performs a same-volume atomic promotion and preserves the old
                        // checkpoint without copying its full contents through managed memory.
                        File.Replace(paths.TemporaryPath, paths.PrimaryPath, paths.BackupPath, ignoreMetadataErrors: true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(paths.PrimaryPath, paths.BackupPath, true);
                        File.Delete(paths.PrimaryPath);
                        File.Move(paths.TemporaryPath, paths.PrimaryPath);
                    }
                }
                else
                {
                    File.Move(paths.TemporaryPath, paths.PrimaryPath);
                }
                timer.Stop();
                atomicWriteMilliseconds = timer.Elapsed.TotalMilliseconds;
                return PersistenceSaveResult.Success(
                    paths.SlotId,
                    paths.PrimaryPath,
                    "Atomic streamed save write completed.",
                    transactionId,
                    PersistenceTransactionPhase.PromotePrimary);
            }
            catch (JsonException exception)
            {
                DeleteIfExists(paths.TemporaryPath);
                return PersistenceSaveResult.Failure(
                    PersistenceSaveStatus.SerializationFailed,
                    paths.SlotId,
                    paths.PrimaryPath,
                    "Could not stream the save envelope.",
                    exception,
                    transactionId,
                    PersistenceTransactionPhase.BuildEnvelope);
            }
            catch (Exception exception)
            {
                DeleteIfExists(paths.TemporaryPath);
                return PersistenceSaveResult.Failure(
                    PersistenceSaveStatus.TemporaryWriteFailed,
                    paths.SlotId,
                    paths.PrimaryPath,
                    "Could not stream or promote the temporary save file.",
                    exception,
                    transactionId,
                    PersistenceTransactionPhase.WriteTemporary);
            }
        }

        private static string ComputeChecksumFromNormalizedPayloads(GameSaveEnvelope envelope)
        {
            return ComputeChecksum(envelope, normalizePayloads: false);
        }

        private static string ComputeChecksum(GameSaveEnvelope envelope, bool normalizePayloads)
        {
            if (envelope == null)
            {
                return string.Empty;
            }

            using SHA256 sha = SHA256.Create();
            byte[] utf8Buffer = new byte[16 * 1024];
            AppendCanonical(sha, utf8Buffer, envelope.formatIdentifier);
            AppendCanonical(sha, utf8Buffer, envelope.schemaVersion.ToString(CultureInfo.InvariantCulture));
            AppendCanonical(sha, utf8Buffer, envelope.persistenceContext.ToString(CultureInfo.InvariantCulture));
            AppendCanonical(sha, utf8Buffer, envelope.gameVersion);
            AppendCanonical(sha, utf8Buffer, envelope.saveId);
            AppendCanonical(sha, utf8Buffer, envelope.slotId);
            AppendCanonical(sha, utf8Buffer, envelope.displayName);
            AppendCanonical(sha, utf8Buffer, envelope.worldId);
            AppendCanonical(sha, utf8Buffer, envelope.playerId);
            AppendCanonical(sha, utf8Buffer, envelope.accountId);
            AppendCanonical(sha, utf8Buffer, envelope.createdUtc);
            AppendCanonical(sha, utf8Buffer, envelope.lastWrittenUtc);
            AppendCanonical(sha, utf8Buffer, envelope.playtimeSeconds.ToString("R", CultureInfo.InvariantCulture));
            AppendCanonical(sha, utf8Buffer, envelope.sceneSummary);
            AppendCanonical(sha, utf8Buffer, envelope.placeSummary);
            AppendCanonical(sha, utf8Buffer, envelope.playerSummary);
            AppendCanonical(sha, utf8Buffer, envelope.transactionId);
            AppendCanonical(sha, utf8Buffer, envelope.parentTransactionId);
            AppendCanonical(sha, utf8Buffer, envelope.saveRevision.ToString(CultureInfo.InvariantCulture));
            AppendCanonical(sha, utf8Buffer, envelope.completedWriteMarker ? "1" : "0");

            IReadOnlyList<SaveParticipantRecord> records = envelope.participants == null
                ? Array.Empty<SaveParticipantRecord>()
                : envelope.participants.OrderBy(record => record?.participantKey, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < records.Count; i++)
            {
                SaveParticipantRecord record = records[i];
                AppendCanonical(sha, utf8Buffer, record?.participantKey);
                AppendCanonical(sha, utf8Buffer, (record?.participantSchemaVersion ?? 0).ToString(CultureInfo.InvariantCulture));
                AppendCanonical(sha, utf8Buffer, record?.required == true ? "1" : "0");
                AppendCanonical(sha, utf8Buffer, (record?.persistenceScope ?? 0).ToString(CultureInfo.InvariantCulture));
                AppendCanonical(sha, utf8Buffer, record?.ownerId);
                AppendCanonical(sha, utf8Buffer, (record?.loadPhase ?? 0).ToString(CultureInfo.InvariantCulture));
                AppendCanonical(sha, utf8Buffer, (record?.loadPriority ?? 0).ToString(CultureInfo.InvariantCulture));
                AppendCanonical(sha, utf8Buffer, normalizePayloads ? NormalizePayloadJson(record?.payloadJson) : record?.payloadJson);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            byte[] hash = sha.Hash;
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                hex.Append(hash[i].ToString("x2"));
            }

            return hex.ToString();
        }

        private static string NormalizePayloadJson(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return string.Empty;
            }

            try
            {
                return JToken.Parse(payloadJson).ToString(Formatting.None);
            }
            catch (JsonException)
            {
                return payloadJson;
            }
        }

        private static string NormalizePayloadJsonStrict(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new JsonSerializationException("Participant payload JSON is empty.");
            }

            return JToken.Parse(payloadJson).ToString(Formatting.None);
        }

        private static void AppendCanonical(HashAlgorithm hash, byte[] utf8Buffer, string value)
        {
            string normalized = value ?? string.Empty;
            AppendUtf8(hash, utf8Buffer, normalized.Length.ToString(CultureInfo.InvariantCulture));
            AppendUtf8(hash, utf8Buffer, ":");
            AppendUtf8(hash, utf8Buffer, normalized);
        }

        private static void AppendUtf8(HashAlgorithm hash, byte[] buffer, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            const int MaximumCharactersPerBlock = 4096;
            int characterOffset = 0;
            while (characterOffset < value.Length)
            {
                int characterCount = Math.Min(MaximumCharactersPerBlock, value.Length - characterOffset);
                if (characterOffset + characterCount < value.Length
                    && char.IsHighSurrogate(value[characterOffset + characterCount - 1]))
                {
                    characterCount--;
                }

                int byteCount = Encoding.UTF8.GetBytes(value, characterOffset, characterCount, buffer, 0);
                hash.TransformBlock(buffer, 0, byteCount, buffer, 0);
                characterOffset += characterCount;
            }
        }

        private SaveEnvelopeHeader TryReadEnvelopeHeader(SaveSlotPaths paths)
        {
            if (paths == null)
            {
                return null;
            }

            if (savedEnvelopeHeaders.TryGetValue(paths.SlotId, out SaveEnvelopeHeader cached))
            {
                return cached;
            }

            if (!File.Exists(paths.PrimaryPath))
            {
                return null;
            }

            try
            {
                GameSaveEnvelope envelope = serializer.Deserialize<GameSaveEnvelope>(File.ReadAllText(paths.PrimaryPath, Encoding.UTF8));
                RememberHeader(envelope);
                return savedEnvelopeHeaders.TryGetValue(paths.SlotId, out SaveEnvelopeHeader loaded) ? loaded : null;
            }
            catch
            {
                return null;
            }
        }

        private static void NormalizeParticipantPayloads(GameSaveEnvelope envelope)
        {
            if (envelope?.participants == null) return;
            for (int i = 0; i < envelope.participants.Count; i++)
            {
                SaveParticipantRecord record = envelope.participants[i];
                if (record != null)
                {
                    record.payloadJson = NormalizePayloadJson(record.payloadJson);
                }
            }
        }

        private void RememberHeader(GameSaveEnvelope envelope)
        {
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.slotId)) return;
            savedEnvelopeHeaders[envelope.slotId] = new SaveEnvelopeHeader(
                envelope.saveId,
                envelope.createdUtc,
                envelope.transactionId,
                envelope.saveRevision,
                envelope.playtimeSeconds);
        }

        private sealed class SaveEnvelopeHeader
        {
            public SaveEnvelopeHeader(string saveId, string createdUtc, string transactionId, int saveRevision, double playtimeSeconds)
            {
                SaveId = saveId ?? string.Empty;
                CreatedUtc = createdUtc ?? string.Empty;
                TransactionId = transactionId ?? string.Empty;
                SaveRevision = saveRevision;
                PlaytimeSeconds = playtimeSeconds;
            }

            public string SaveId { get; }
            public string CreatedUtc { get; }
            public string TransactionId { get; }
            public int SaveRevision { get; }
            public double PlaytimeSeconds { get; }
        }

        private PersistenceSaveResult FinishSave(PersistenceSaveResult result)
        {
            operationInProgress = false;
            operationState = result.Succeeded ? PersistenceOperationState.Idle : PersistenceOperationState.Failed;
            currentPhase = result.Succeeded ? PersistenceTransactionPhase.Finalize : result.Phase;
            SaveCompleted?.Invoke(result);
            if (result.Succeeded)
            {
                operationState = PersistenceOperationState.Idle;
                currentPhase = PersistenceTransactionPhase.Idle;
                currentTransactionId = string.Empty;
            }

            return result;
        }

        private PersistenceLoadResult FinishLoad(PersistenceLoadResult result)
        {
            operationInProgress = false;
            operationState = result.Succeeded ? PersistenceOperationState.Idle : PersistenceOperationState.Failed;
            currentPhase = result.Succeeded ? PersistenceTransactionPhase.Finalize : result.Phase;
            LoadCompleted?.Invoke(result);
            if (result.Succeeded)
            {
                operationState = PersistenceOperationState.Idle;
                currentPhase = PersistenceTransactionPhase.Idle;
                currentTransactionId = string.Empty;
            }

            return result;
        }

        private void SetOperation(PersistenceOperationState state, PersistenceTransactionPhase phase, string transactionId, bool isSave)
        {
            operationInProgress = true;
            operationState = state;
            currentPhase = phase;
            currentTransactionId = transactionId ?? string.Empty;
            if (isSave)
            {
                SaveTransactionPhaseChanged?.Invoke(currentTransactionId, phase);
            }
            else
            {
                LoadTransactionPhaseChanged?.Invoke(currentTransactionId, phase);
            }
        }

        private bool Faulted(PersistenceFaultInjectionPoint point, out string failureMessage)
        {
            failureMessage = string.Empty;
            return FaultInjection != null && FaultInjection.Consume(point, out failureMessage);
        }

        private static PersistenceLoadStatus MapValidationToLoadStatus(PersistenceValidationStatus status)
        {
            return status switch
            {
                PersistenceValidationStatus.FileMissing => PersistenceLoadStatus.FileMissing,
                PersistenceValidationStatus.ReadFailed => PersistenceLoadStatus.ReadFailed,
                PersistenceValidationStatus.MalformedJson => PersistenceLoadStatus.MalformedJson,
                PersistenceValidationStatus.WrongFormatIdentifier => PersistenceLoadStatus.WrongFormatIdentifier,
                PersistenceValidationStatus.UnsupportedSchemaVersion => PersistenceLoadStatus.UnsupportedSchemaVersion,
                PersistenceValidationStatus.UnsupportedGameVersion => PersistenceLoadStatus.UnsupportedGameVersion,
                PersistenceValidationStatus.WrongSaveContext => PersistenceLoadStatus.WrongSaveContext,
                PersistenceValidationStatus.WrongSlot => PersistenceLoadStatus.WrongSlot,
                PersistenceValidationStatus.WrongWorld => PersistenceLoadStatus.WrongWorld,
                PersistenceValidationStatus.WrongPlayer => PersistenceLoadStatus.WrongPlayer,
                PersistenceValidationStatus.WrongAccount => PersistenceLoadStatus.WrongAccount,
                PersistenceValidationStatus.IncompleteWrite => PersistenceLoadStatus.IncompleteWrite,
                PersistenceValidationStatus.ChecksumMismatch => PersistenceLoadStatus.ChecksumMismatch,
                PersistenceValidationStatus.DuplicateParticipantKey => PersistenceLoadStatus.DuplicateParticipantKey,
                PersistenceValidationStatus.DependencyValidationFailed => PersistenceLoadStatus.DependencyValidationFailed,
                PersistenceValidationStatus.MissingRequiredParticipantPayload => PersistenceLoadStatus.MissingRequiredParticipantPayload,
                PersistenceValidationStatus.MissingRuntimeParticipant => PersistenceLoadStatus.MissingRuntimeParticipant,
                PersistenceValidationStatus.ParticipantPrepareFailed => PersistenceLoadStatus.ParticipantPrepareFailed,
                PersistenceValidationStatus.BackupAvailable => PersistenceLoadStatus.BackupAvailable,
                _ => PersistenceLoadStatus.UnknownException
            };
        }

        private static int CompareParticipants(IPersistenceParticipant a, IPersistenceParticipant b)
        {
            int phase = a.LoadPhase.CompareTo(b.LoadPhase);
            if (phase != 0)
            {
                return phase;
            }

            int priority = a.LoadPriority.CompareTo(b.LoadPriority);
            return priority != 0 ? priority : string.CompareOrdinal(a.ParticipantKey, b.ParticipantKey);
        }

        private static int CompareRecords(SaveParticipantRecord a, SaveParticipantRecord b)
        {
            int phase = a.loadPhase.CompareTo(b.loadPhase);
            if (phase != 0)
            {
                return phase;
            }

            int priority = a.loadPriority.CompareTo(b.loadPriority);
            return priority != 0 ? priority : string.CompareOrdinal(a.participantKey, b.participantKey);
        }

        private static void DiscardPrepared(List<PreparedParticipant> prepared)
        {
            for (int i = 0; i < prepared.Count; i++)
            {
                prepared[i].Participant.DiscardPreparedPayload(prepared[i].PreparedPayload);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private void CopySlotFiles(SaveSlotPaths source, SaveSlotPaths target)
        {
            DeleteIfExists(target.PrimaryPath);
            DeleteIfExists(target.BackupPath);
            DeleteIfExists(target.TemporaryPath);

            CopyEnvelopeFile(source.PrimaryPath, target.PrimaryPath, target.SlotId);
            CopyEnvelopeFile(source.BackupPath, target.BackupPath, target.SlotId);
        }

        private void CopyEnvelopeFile(string sourcePath, string targetPath, string targetSlotId)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            try
            {
                GameSaveEnvelope envelope = serializer.Deserialize<GameSaveEnvelope>(File.ReadAllText(sourcePath, Encoding.UTF8));
                if (envelope != null && envelope.formatIdentifier == FormatIdentifier)
                {
                    envelope.slotId = targetSlotId;
                    envelope.contentChecksum = ComputeChecksum(envelope);
                    File.WriteAllText(targetPath, serializer.Serialize(envelope, true), Encoding.UTF8);
                    return;
                }
            }
            catch
            {
                // Preserve unreadable files exactly; validation/reporting owns the resulting error.
            }

            File.Copy(sourcePath, targetPath, true);
        }

        private sealed class PreparedParticipant
        {
            public PreparedParticipant(IPersistenceParticipant participant, object preparedPayload)
            {
                Participant = participant;
                PreparedPayload = preparedPayload;
            }

            public IPersistenceParticipant Participant { get; }
            public object PreparedPayload { get; }
        }

        private sealed class CachedParticipantPayload
        {
            public CachedParticipantPayload(int schemaVersion, long revision, string payloadJson)
            {
                SchemaVersion = schemaVersion;
                Revision = revision;
                PayloadJson = payloadJson ?? string.Empty;
            }

            public int SchemaVersion { get; }
            public long Revision { get; }
            public string PayloadJson { get; }
        }

        private sealed class ParticipantSaveException : Exception
        {
            public ParticipantSaveException(PersistenceSaveStatus status, string message)
                : base(message)
            {
                Status = status;
            }

            public PersistenceSaveStatus Status { get; }
        }
    }
}
