using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Recovery
{
    public sealed class BiologicalRecoveryRuntime
    {
        private readonly Dictionary<string, RecoveryProcessRecord> processesById = new Dictionary<string, RecoveryProcessRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, RecoveryRateModifierRecord> rateModifiersBySource = new Dictionary<string, RecoveryRateModifierRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> committedTransactionIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> committedTickIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string actorBodyId;
        private string personId;
        private string speciesId;
        private string profileId;
        private long bodyRevision;
        private long conditionRevision;
        private long vitalRevision;
        private long hazardRevision;
        private long compatibilityRevision;
        private long nextProcessSequence = 1L;
        private RecoveryRestContextSnapshot restContext = new RecoveryRestContextSnapshot(string.Empty, RecoveryRestType.NotResting, string.Empty, string.Empty, 0f, Array.Empty<string>());
        private bool suppressEvents;

        public event Action<BiologicalRecoveryRuntime, BiologicalRecoveryResult, bool> RecoveryChanged;

        public RecoveryReadinessState Readiness { get; private set; } = RecoveryReadinessState.Uninitialized;
        public long RecoveryRevision { get; private set; }
        public bool IsDirty { get; private set; }
        public string ActorBodyId => actorBodyId ?? string.Empty;
        public string PersonId => personId ?? string.Empty;
        public string SpeciesId => speciesId ?? string.Empty;
        public string ProfileId => profileId ?? string.Empty;
        public bool IsReady => Readiness == RecoveryReadinessState.Ready;

        public BiologicalRecoveryResult BuildForBody(BodySnapshot body, DefinitionRegistry definitionRegistry, bool restoring = false, bool preserveRevision = false)
        {
            registry = definitionRegistry ?? registry;
            if (!ValidateBodySnapshot(body, out BiologicalRecoveryResult failure))
            {
                return failure;
            }

            BiologicalRecoveryProfileDefinition profile = ResolveProfile(body);
            if (profile == null)
            {
                Readiness = RecoveryReadinessState.ResolvingDefinitions;
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingProfile, $"No Biological Recovery Profile resolves for body Species '{body.SpeciesId}'.", actorBodyId: body.ActorBodyId, snapshot: CreateSnapshot());
            }

            actorBodyId = body.ActorBodyId;
            personId = body.PersonId;
            speciesId = body.SpeciesId;
            profileId = profile.Id;
            bodyRevision = body.BodyRevision;
            conditionRevision = body.Condition.ConditionRevision;
            vitalRevision = body.VitalProcesses.VitalRevision;
            hazardRevision = body.BiologicalHazards.HazardRevision;
            compatibilityRevision = body.BiologicalCompatibility.CompatibilityRevision;
            processesById.Clear();
            rateModifiersBySource.Clear();
            committedTransactionIds.Clear();
            committedTickIds.Clear();
            restContext = new RecoveryRestContextSnapshot(actorBodyId, RecoveryRestType.NotResting, string.Empty, string.Empty, 0f, Array.Empty<string>());
            Readiness = restoring ? RecoveryReadinessState.Restoring : RecoveryReadinessState.BuildingRecoveryState;

            if (!preserveRevision)
            {
                RecoveryRevision++;
            }

            IsDirty = !restoring;
            Readiness = RecoveryReadinessState.Ready;
            return BiologicalRecoveryResult.Success("recovery.build", ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), message: "Biological recovery runtime built.");
        }

        public BiologicalRecoveryResult PreviewRateModifier(RecoveryRateModifierRequest request)
        {
            return ResolveRateModifier(request, remove: false, preview: true, restoring: false);
        }

        public BiologicalRecoveryResult AddOrUpdateRateModifier(RecoveryRateModifierRequest request, bool restoring = false)
        {
            return ResolveRateModifier(request, remove: false, preview: false, restoring);
        }

        public BiologicalRecoveryResult RemoveRateModifier(RecoveryRateModifierRequest request, bool restoring = false)
        {
            return ResolveRateModifier(request, remove: true, preview: false, restoring);
        }

        public BiologicalRecoveryResult PreviewStartProcess(RecoveryProcessStartRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility)
        {
            return ResolveStartProcess(request, body, compatibility, preview: true, out _, out _, out _);
        }

        public BiologicalRecoveryResult StartProcess(RecoveryProcessStartRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.RuntimeNotReady, "Biological recovery runtime is not Ready.", request?.TransactionId, request?.ActorBodyId, request?.RecoveryMethodId, snapshot: CreateSnapshot());
            }

            if (request != null && !string.IsNullOrWhiteSpace(request.TransactionId) && committedTransactionIds.Contains(request.TransactionId))
            {
                RecoveryProcessRecord duplicateRecord = processesById.Values.FirstOrDefault(process => string.Equals(process.SourceTransactionId, request.TransactionId, StringComparison.Ordinal));
                return BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, request.RecoveryMethodId, duplicateRecord == null ? string.Empty : duplicateRecord.ProcessId, duplicateRecord == null ? 0f : duplicateRecord.CurrentProgress, duplicateRecord == null ? 0f : duplicateRecord.CurrentProgress, 0f, duplicateRecord == null ? RecoveryProcessState.Unknown : duplicateRecord.State, duplicateRecord == null ? RecoveryProcessState.Unknown : duplicateRecord.State, null, null, null, CreateSnapshot(), duplicate: true);
            }

            BiologicalRecoveryResult resolved = ResolveStartProcess(request, body, compatibility, preview: false, out RecoveryMethodDefinition method, out BiologicalInteractionEvaluationResult evaluation, out RecoveryProcessRecord process);
            if (!resolved.Succeeded)
            {
                return resolved;
            }

            processesById[process.ProcessId] = process;
            committedTransactionIds.Add(request.TransactionId);
            RecoveryRevision++;
            IsDirty = !restoring;
            BiologicalRecoveryResult result = BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, method.Id, process.ProcessId, 0f, process.CurrentProgress, 0f, RecoveryProcessState.Pending, process.State, evaluation, null, null, CreateSnapshot(), message: "Recovery process started.");
            RaiseChanged(result, restoring);
            return result;
        }

        public BiologicalRecoveryResult PreviewTick(RecoveryTickRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility, BodyConditionRuntime condition, VitalProcessRuntime vitalProcesses)
        {
            BiologicalRecoverySaveData previewState = CreateSaveData();
            bool dirtyBefore = IsDirty;
            BiologicalRecoveryResult result = ApplyTick(request, body, compatibility, condition, vitalProcesses, preview: true);
            RestoreRuntimeState(previewState, dirtyBefore, body, registry);
            return result;
        }

        public BiologicalRecoveryResult ApplyTick(RecoveryTickRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility, BodyConditionRuntime condition, VitalProcessRuntime vitalProcesses, bool preview = false, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.RuntimeNotReady, "Biological recovery runtime is not Ready.", request?.TickId, request?.ActorBodyId, snapshot: CreateSnapshot());
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TickId))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.InvalidRequest, "Recovery tick requires a stable tick ID.", actorBodyId: ActorBodyId, snapshot: CreateSnapshot());
            }

            if (!string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleBody, $"Recovery tick body '{request.ActorBodyId}' does not match runtime body '{ActorBodyId}'.", request.TickId, request.ActorBodyId, snapshot: CreateSnapshot());
            }

            if (request.ElapsedGameSeconds < 0f || float.IsNaN(request.ElapsedGameSeconds) || float.IsInfinity(request.ElapsedGameSeconds))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.InvalidElapsedTime, "Recovery elapsed game seconds must be finite and non-negative.", request.TickId, ActorBodyId, snapshot: CreateSnapshot());
            }

            if (!preview && committedTickIds.Contains(request.TickId))
            {
                return BiologicalRecoveryResult.Success(request.TickId, ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), duplicate: true);
            }

            if (!ValidateBodySnapshot(body, out BiologicalRecoveryResult failure, updateReadiness: !preview) || compatibility == null || condition == null || vitalProcesses == null)
            {
                return failure ?? BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingBody, "Recovery tick requires body, compatibility, condition, and vital runtimes.", request.TickId, ActorBodyId, snapshot: CreateSnapshot());
            }

            if (request.ExpectedRecoveryRevision > 0L && request.ExpectedRecoveryRevision != RecoveryRevision)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleDependency, $"Expected recovery revision {request.ExpectedRecoveryRevision} but runtime is {RecoveryRevision}.", request.TickId, ActorBodyId, snapshot: CreateSnapshot());
            }

            BiologicalRecoveryResult dependencyFailure = ValidateTickDependencies(request, body);
            if (dependencyFailure != null)
            {
                return dependencyFailure;
            }

            float hours = request.ElapsedGameSeconds / 3600f;
            bool changed = false;
            BiologicalRecoveryResult lastResult = null;
            RecoveryAllocationPolicy allocationPolicy = ResolveAllocationPolicy(body);
            RecoveryProcessRecord[] tickProcesses = SelectProcessesForTick(body, allocationPolicy).ToArray();
            bool exclusiveAllocation = UsesExclusiveAllocation(allocationPolicy);
            bool exclusiveProcessAdvanced = false;
            foreach (RecoveryProcessRecord process in tickProcesses)
            {
                if (!CanTickProcess(process))
                {
                    continue;
                }

                if (exclusiveAllocation && exclusiveProcessAdvanced)
                {
                    continue;
                }

                float allocatedHours = ResolveAllocatedHours(hours, tickProcesses.Length, allocationPolicy);
                BiologicalRecoveryResult processResult = ApplyProcessTick(process, request, body, compatibility, condition, vitalProcesses, allocatedHours, preview, restoring);
                lastResult = processResult;
                if (!processResult.Succeeded)
                {
                    if (processResult.Code == BiologicalRecoveryResultCode.Blocked || processResult.Code == BiologicalRecoveryResultCode.Suppressed || processResult.Code == BiologicalRecoveryResultCode.RecoveryLimitReached)
                    {
                        if (!preview)
                        {
                            process.State = processResult.Code == BiologicalRecoveryResultCode.RecoveryLimitReached
                                ? RecoveryProcessState.Completed
                                : processResult.Code == BiologicalRecoveryResultCode.Suppressed ? RecoveryProcessState.Suppressed : RecoveryProcessState.Paused;
                            process.Revision++;
                        }

                        changed = !preview;
                        continue;
                    }

                    return processResult;
                }

                changed = changed || processResult.AppliedProgress > 0f || processResult.NewState != processResult.PreviousState;
                exclusiveProcessAdvanced = exclusiveProcessAdvanced || processResult.AppliedProgress > 0f;
            }

            if (!preview)
            {
                committedTickIds.Add(request.TickId);
                if (changed)
                {
                    RecoveryRevision++;
                    IsDirty = !restoring;
                    if (lastResult != null)
                    {
                        RaiseChanged(lastResult, restoring);
                    }
                }
            }

            return lastResult ?? BiologicalRecoveryResult.Success(request.TickId, ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), preview: preview, message: "Recovery tick had no active processes.");
        }

        public BiologicalRecoveryResult SetRestContext(RecoveryRestContextRequest request, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.RuntimeNotReady, "Biological recovery runtime is not Ready.", request?.TransactionId, request?.ActorBodyId, snapshot: CreateSnapshot());
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId) || !string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.InvalidRequest, "Rest context requires matching body and transaction IDs.", request?.TransactionId, request?.ActorBodyId, snapshot: CreateSnapshot());
            }

            if (committedTransactionIds.Contains(request.TransactionId))
            {
                return BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), duplicate: true);
            }

            restContext = new RecoveryRestContextSnapshot(ActorBodyId, request.RestType, request.SourceId, request.TransactionId, Mathf.Max(0f, request.Quality), request.Tags);
            committedTransactionIds.Add(request.TransactionId);
            RecoveryRevision++;
            IsDirty = !restoring;
            BiologicalRecoveryResult result = BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), message: "Recovery rest context changed.");
            RaiseChanged(result, restoring);
            return result;
        }

        public BiologicalRecoveryResult CancelProcess(RecoveryCancellationRequest request, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.RuntimeNotReady, "Biological recovery runtime is not Ready.", request?.TransactionId, request?.ActorBodyId, snapshot: CreateSnapshot());
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId) || string.IsNullOrWhiteSpace(request.ProcessId) || !string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.InvalidRequest, "Recovery cancellation requires matching body, process, and transaction IDs.", request?.TransactionId, request?.ActorBodyId, snapshot: CreateSnapshot());
            }

            if (committedTransactionIds.Contains(request.TransactionId))
            {
                return BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, string.Empty, request.ProcessId, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), duplicate: true);
            }

            if (!processesById.TryGetValue(request.ProcessId, out RecoveryProcessRecord process))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingTarget, $"Recovery process '{request.ProcessId}' does not exist.", request.TransactionId, ActorBodyId, processId: request.ProcessId, snapshot: CreateSnapshot());
            }

            RecoveryProcessState previous = process.State;
            process.State = RecoveryProcessState.Cancelled;
            process.Revision++;
            committedTransactionIds.Add(request.TransactionId);
            RecoveryRevision++;
            IsDirty = !restoring;
            BiologicalRecoveryResult result = BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, process.RecoveryMethodId, process.ProcessId, process.CurrentProgress, process.CurrentProgress, 0f, previous, process.State, null, null, null, CreateSnapshot(), message: "Recovery process cancelled.");
            RaiseChanged(result, restoring);
            return result;
        }

        public BiologicalRecoverySaveData CreateSaveData()
        {
            return new BiologicalRecoverySaveData
            {
                schemaVersion = BiologicalRecoverySaveData.CurrentSchemaVersion,
                actorBodyId = ActorBodyId,
                personId = PersonId,
                speciesDefinitionId = SpeciesId,
                profileDefinitionId = ProfileId,
                bodyRevision = bodyRevision,
                conditionRevision = conditionRevision,
                vitalRevision = vitalRevision,
                hazardRevision = hazardRevision,
                compatibilityRevision = compatibilityRevision,
                recoveryRevision = RecoveryRevision,
                restContext = RestToSaveData(restContext),
                processes = processesById.Values.OrderBy(process => process.ProcessId, StringComparer.Ordinal).Select(process => process.ToSaveData()).ToArray(),
                rateModifiers = rateModifiersBySource.Values.OrderBy(modifier => modifier.SourceId, StringComparer.Ordinal).Select(modifier => modifier.ToSaveData()).ToArray(),
                committedTransactionIds = committedTransactionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                committedTickIds = committedTickIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            };
        }

        public BiologicalRecoveryResult RestoreFromSaveData(BiologicalRecoverySaveData saveData, BodySnapshot body, DefinitionRegistry definitionRegistry)
        {
            if (!ValidateSaveData(saveData, body, definitionRegistry, out string failureReason))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.InvalidRestore, failureReason, actorBodyId: body?.ActorBodyId, snapshot: CreateSnapshot());
            }

            BiologicalRecoverySaveData rollback = CreateSaveData();
            bool dirtyBefore = IsDirty;
            try
            {
                registry = definitionRegistry ?? registry;
                using (SuppressEvents())
                {
                    BuildForBody(body, registry, restoring: true, preserveRevision: true);
                    RestoreRuntimeState(saveData, false, body, registry);
                }

                IsDirty = false;
                Readiness = RecoveryReadinessState.Ready;
                return BiologicalRecoveryResult.Success("recovery.restore", ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), message: "Biological recovery restored.");
            }
            catch (Exception exception)
            {
                RestoreRuntimeState(rollback, dirtyBefore, body, definitionRegistry);
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.InvalidRestore, $"Recovery restore rolled back: {exception.Message}", actorBodyId: body?.ActorBodyId, snapshot: CreateSnapshot());
            }
        }

        public static bool ValidateSaveData(BiologicalRecoverySaveData saveData, BodySnapshot body, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Biological recovery save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > BiologicalRecoverySaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported biological recovery schema version {saveData.schemaVersion}.";
                return false;
            }

            if (body == null || !string.Equals(saveData.actorBodyId, body.ActorBodyId, StringComparison.Ordinal))
            {
                failureReason = "Saved recovery body does not match current body.";
                return false;
            }

            if (!string.Equals(saveData.speciesDefinitionId, body.SpeciesId, StringComparison.Ordinal))
            {
                failureReason = "Saved recovery Species does not match current body Species.";
                return false;
            }

            if (registry == null || !registry.TryGet(saveData.profileDefinitionId, out BiologicalRecoveryProfileDefinition profile) || profile == null)
            {
                failureReason = $"Saved recovery profile '{saveData.profileDefinitionId}' does not resolve.";
                return false;
            }

            HashSet<string> processIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecoveryProcessSaveData process in saveData.processes ?? Array.Empty<RecoveryProcessSaveData>())
            {
                if (process == null || string.IsNullOrWhiteSpace(process.processId) || !processIds.Add(process.processId))
                {
                    failureReason = "Saved recovery contains a missing or duplicate process ID.";
                    return false;
                }

                if (!string.Equals(process.actorBodyId, body.ActorBodyId, StringComparison.Ordinal))
                {
                    failureReason = $"Recovery process '{process.processId}' targets another body.";
                    return false;
                }

                if (!registry.TryGet(process.recoveryMethodId, out RecoveryMethodDefinition method) || method == null)
                {
                    failureReason = $"Recovery process '{process.processId}' references missing method '{process.recoveryMethodId}'.";
                    return false;
                }

                if (process.currentProgress < 0f || process.requiredProgress < 0f)
                {
                    failureReason = $"Recovery process '{process.processId}' has invalid progress.";
                    return false;
                }
            }

            HashSet<string> modifierIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecoveryRateModifierSaveData modifier in saveData.rateModifiers ?? Array.Empty<RecoveryRateModifierSaveData>())
            {
                if (modifier == null || string.IsNullOrWhiteSpace(modifier.sourceId) || !modifierIds.Add(modifier.sourceId))
                {
                    failureReason = "Saved recovery rate modifiers contain a missing or duplicate source ID.";
                    return false;
                }

                if (modifier.rateMultiplier < 0f || float.IsNaN(modifier.rateMultiplier) || float.IsInfinity(modifier.rateMultiplier))
                {
                    failureReason = $"Recovery rate modifier '{modifier.sourceId}' has an invalid multiplier.";
                    return false;
                }
            }

            return true;
        }

        public BiologicalRecoverySnapshot CreateSnapshot()
        {
            bool coherent = ValidateRuntime(out string failureReason);
            List<string> diagnostics = new List<string>();
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                diagnostics.Add(failureReason);
            }

            return new BiologicalRecoverySnapshot(
                ActorBodyId,
                PersonId,
                SpeciesId,
                ProfileId,
                Readiness,
                bodyRevision,
                conditionRevision,
                vitalRevision,
                hazardRevision,
                compatibilityRevision,
                RecoveryRevision,
                restContext,
                processesById.Values.OrderBy(process => process.ProcessId, StringComparer.Ordinal).Select(process => process.CreateSnapshot()).ToArray(),
                rateModifiersBySource.Values.OrderBy(modifier => modifier.SourceId, StringComparer.Ordinal).Select(modifier => modifier.CreateSnapshot()).ToArray(),
                IsDirty,
                coherent,
                diagnostics);
        }

        public bool TryGetProcess(string processId, out RecoveryProcessSnapshot snapshot)
        {
            snapshot = null;
            if (!processesById.TryGetValue(processId ?? string.Empty, out RecoveryProcessRecord process))
            {
                return false;
            }

            snapshot = process.CreateSnapshot();
            return true;
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        public void Dispose()
        {
            Readiness = RecoveryReadinessState.Disposed;
            processesById.Clear();
            rateModifiersBySource.Clear();
            committedTransactionIds.Clear();
            committedTickIds.Clear();
        }

        private BiologicalRecoveryResult ResolveRateModifier(RecoveryRateModifierRequest request, bool remove, bool preview, bool restoring)
        {
            if (!IsReady)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.RuntimeNotReady, "Biological recovery runtime is not Ready.", request.TransactionId, request.ActorBodyId, snapshot: CreateSnapshot());
            }

            if (string.IsNullOrWhiteSpace(request.ActorBodyId) || !string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(request.SourceId) || string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.InvalidRequest, "Recovery rate modifier requires matching body, source, and transaction IDs.", request.TransactionId, request.ActorBodyId, snapshot: CreateSnapshot());
            }

            if (!preview && committedTransactionIds.Contains(request.TransactionId))
            {
                return BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), duplicate: true);
            }

            if (remove)
            {
                if (!rateModifiersBySource.ContainsKey(request.SourceId))
                {
                    return BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), preview: preview, duplicate: true, message: "Recovery rate modifier was already absent.");
                }

                if (!preview)
                {
                    rateModifiersBySource.Remove(request.SourceId);
                    committedTransactionIds.Add(request.TransactionId);
                    RecoveryRevision++;
                    IsDirty = !restoring;
                }

                BiologicalRecoveryResult removeResult = BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), preview: preview, message: "Recovery rate modifier removed.");
                if (!preview)
                {
                    RaiseChanged(removeResult, restoring);
                }

                return removeResult;
            }

            if (request.RateMultiplier < 0f || float.IsNaN(request.RateMultiplier) || float.IsInfinity(request.RateMultiplier))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.InvalidRequest, "Recovery rate modifier multiplier must be finite and non-negative.", request.TransactionId, ActorBodyId, snapshot: CreateSnapshot());
            }

            bool changed = !rateModifiersBySource.TryGetValue(request.SourceId, out RecoveryRateModifierRecord existing)
                || Math.Abs(existing.RateMultiplier - request.RateMultiplier) > 0.0001f
                || !string.Equals(existing.Reason, request.Reason, StringComparison.Ordinal);
            if (!preview && changed)
            {
                rateModifiersBySource[request.SourceId] = new RecoveryRateModifierRecord(request.SourceId, request.RateMultiplier, request.Reason, (existing?.Revision ?? 0L) + 1L);
                committedTransactionIds.Add(request.TransactionId);
                RecoveryRevision++;
                IsDirty = !restoring;
            }
            else if (!preview)
            {
                committedTransactionIds.Add(request.TransactionId);
            }

            BiologicalRecoveryResult result = BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, string.Empty, string.Empty, 0f, 0f, 0f, RecoveryProcessState.Unknown, RecoveryProcessState.Unknown, null, null, null, CreateSnapshot(), preview: preview, duplicate: !changed, message: changed ? "Recovery rate modifier applied." : "Recovery rate modifier unchanged.");
            if (!preview && changed)
            {
                RaiseChanged(result, restoring);
            }

            return result;
        }

        private BiologicalRecoveryResult ResolveStartProcess(
            RecoveryProcessStartRequest request,
            BodySnapshot body,
            BiologicalCompatibilityRuntime compatibility,
            bool preview,
            out RecoveryMethodDefinition method,
            out BiologicalInteractionEvaluationResult evaluation,
            out RecoveryProcessRecord process)
        {
            method = null;
            evaluation = null;
            process = null;
            if (!IsReady)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.RuntimeNotReady, "Biological recovery runtime is not Ready.", request?.TransactionId, request?.ActorBodyId, request?.RecoveryMethodId, snapshot: CreateSnapshot());
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId) || string.IsNullOrWhiteSpace(request.RecoveryMethodId) || request.Target == null)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.InvalidRequest, "Recovery process start requires transaction, method, and target.", request?.TransactionId, request?.ActorBodyId, request?.RecoveryMethodId, snapshot: CreateSnapshot());
            }

            if (!ValidateBodySnapshot(body, out BiologicalRecoveryResult failure, updateReadiness: !preview) || compatibility == null || !compatibility.IsReady)
            {
                return failure ?? BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingCompatibility, "Recovery requires a Ready biological compatibility runtime.", request.TransactionId, request.ActorBodyId, request.RecoveryMethodId, snapshot: CreateSnapshot());
            }

            if (!string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal) || !string.Equals(request.Target.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleBody, "Recovery process must target the exact owning Actor/body.", request.TransactionId, request.ActorBodyId, request.RecoveryMethodId, snapshot: CreateSnapshot());
            }

            if (request.ExpectedBodyRevision > 0L && request.ExpectedBodyRevision != body.BodyRevision)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleBody, $"Expected body revision {request.ExpectedBodyRevision} but snapshot is {body.BodyRevision}.", request.TransactionId, request.ActorBodyId, request.RecoveryMethodId, snapshot: CreateSnapshot());
            }

            if (registry == null || !registry.TryGet(request.RecoveryMethodId, out method) || method == null)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingMethod, $"Recovery Method '{request.RecoveryMethodId}' does not resolve.", request.TransactionId, request.ActorBodyId, request.RecoveryMethodId, snapshot: CreateSnapshot());
            }

            if (!method.AlphaExecutionEnabled || !method.SupportsTarget(request.Target.TargetCategory))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.UnsupportedTarget, $"Recovery Method '{method.Id}' does not support target '{request.Target.TargetCategory}'.", request.TransactionId, request.ActorBodyId, method.Id, snapshot: CreateSnapshot());
            }

            BiologicalRecoveryProfileDefinition profile = ResolveProfile(body);
            RecoveryProfileMethodEntry profileEntry = profile == null ? null : profile.GetMethodEntry(method.Id);
            if (profileEntry == null)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.RequirementFailed, $"Recovery Profile '{ProfileId}' does not enable method '{method.Id}'.", request.TransactionId, request.ActorBodyId, method.Id, snapshot: CreateSnapshot());
            }

            if (!ValidateTarget(request.Target, body, method, out string targetFailure, out AnatomyNodeSnapshot node))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingTarget, targetFailure, request.TransactionId, request.ActorBodyId, method.Id, snapshot: CreateSnapshot());
            }

            if (method.RequiresRestContext && !RestContextAllowed(method))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.RequirementFailed, $"Recovery Method '{method.Id}' requires an authorized rest context.", request.TransactionId, request.ActorBodyId, method.Id, snapshot: CreateSnapshot());
            }

            evaluation = compatibility.Evaluate(body, method.BiologicalInteractionDefinitionId, ToInteractionCategory(method.Category), node, request.Target.GetStableKey(), request.TransactionId, preview);
            if (evaluation.Code != BiologicalCompatibilityResultCode.Success)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingCompatibility, evaluation.Message, request.TransactionId, request.ActorBodyId, method.Id, compatibility: evaluation, snapshot: CreateSnapshot());
            }

            if (!evaluation.Compatible)
            {
                BiologicalRecoveryResultCode code = evaluation.Suppressed ? BiologicalRecoveryResultCode.Suppressed : BiologicalRecoveryResultCode.Incompatible;
                return BiologicalRecoveryResult.Failure(code, $"Recovery Method '{method.Id}' is not biologically compatible: {evaluation.Message}", request.TransactionId, request.ActorBodyId, method.Id, compatibility: evaluation, snapshot: CreateSnapshot());
            }

            float requiredProgress = ResolveRequiredProgress(request.Target, body, method);
            float baseRate = ResolveBaseRate(method, request.Target) * profileEntry.RateMultiplier;
            float effectiveRate = baseRate * evaluation.RateMultiplier * (restContext.Active ? Math.Max(0.1f, restContext.Quality) : 1f);
            string processId = BuildProcessId(request.TransactionId, ActorBodyId, method.Id, request.Target.GetStableKey());
            process = new RecoveryProcessRecord(
                processId,
                ActorBodyId,
                method.Id,
                string.IsNullOrWhiteSpace(request.SourceId) ? "recovery.runtime" : request.SourceId,
                request.TransactionId,
                CloneTarget(request.Target),
                0f,
                requiredProgress,
                baseRate,
                effectiveRate,
                RecoveryProcessState.Active,
                method.InterruptionPolicy,
                method.RecoveryLimit,
                method.PermanentOutcome,
                FormatCompatibility(evaluation),
                preview ? nextProcessSequence : nextProcessSequence++,
                string.Empty,
                1L);

            return BiologicalRecoveryResult.Success(request.TransactionId, ActorBodyId, method.Id, process.ProcessId, 0f, 0f, 0f, RecoveryProcessState.Pending, process.State, evaluation, null, null, CreateSnapshot(), preview: preview);
        }

        private BiologicalRecoveryResult ApplyProcessTick(RecoveryProcessRecord process, RecoveryTickRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility, BodyConditionRuntime condition, VitalProcessRuntime vitalProcesses, float hours, bool preview, bool restoring)
        {
            if (registry == null || !registry.TryGet(process.RecoveryMethodId, out RecoveryMethodDefinition method) || method == null)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingMethod, $"Recovery Method '{process.RecoveryMethodId}' no longer resolves.", request.TickId, ActorBodyId, process.RecoveryMethodId, process.ProcessId, snapshot: CreateSnapshot());
            }

            if (!ValidateTarget(process.Target, body, method, out string targetFailure, out AnatomyNodeSnapshot node))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleTarget, targetFailure, request.TickId, ActorBodyId, method.Id, process.ProcessId, snapshot: CreateSnapshot());
            }

            if (method.RequiresRestContext && !RestContextAllowed(method))
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.Blocked, $"Recovery Method '{method.Id}' requires active rest context.", request.TickId, ActorBodyId, method.Id, process.ProcessId, snapshot: CreateSnapshot());
            }

            BiologicalInteractionEvaluationResult evaluation = compatibility.Evaluate(body, method.BiologicalInteractionDefinitionId, ToInteractionCategory(method.Category), node, process.Target.GetStableKey(), request.TickId, preview);
            if (evaluation.Code != BiologicalCompatibilityResultCode.Success || !evaluation.Compatible)
            {
                return BiologicalRecoveryResult.Failure(evaluation.Suppressed ? BiologicalRecoveryResultCode.Suppressed : BiologicalRecoveryResultCode.Incompatible, evaluation.Message, request.TickId, ActorBodyId, method.Id, process.ProcessId, evaluation, CreateSnapshot());
            }

            float previousProgress = process.CurrentProgress;
            RecoveryProcessState previousState = process.State;
            float amount = Mathf.Max(0f, process.EffectiveRatePerHour * evaluation.RateMultiplier * ResolveRateModifierMultiplier() * hours);
            if (Mathf.Approximately(amount, 0f))
            {
                return BiologicalRecoveryResult.Success(request.TickId, ActorBodyId, method.Id, process.ProcessId, previousProgress, previousProgress, 0f, previousState, process.State, evaluation, null, null, CreateSnapshot(), preview: preview, message: "Recovery tick produced no progress.");
            }

            StructuralRecoveryResult structural = null;
            VitalResourceMutationResult vital = null;
            if (process.Target.TargetCategory == RecoveryTargetCategory.VitalResource)
            {
                string resourceTransaction = $"{request.TickId}.{process.ProcessId}.resource";
                VitalResourceMutationRequest mutation = new VitalResourceMutationRequest(
                    ActorBodyId,
                    process.Target.ResourceDefinitionId,
                    VitalResourceMutationOperation.Restore,
                    Math.Min(amount, process.RequiredProgress - process.CurrentProgress),
                    resourceTransaction,
                    process.ProcessId,
                    method.Id,
                    body.BodyRevision,
                    body.Anatomy?.AnatomyRevision ?? 0L,
                    body.Condition?.ConditionRevision ?? 0L);
                vital = preview
                    ? vitalProcesses.PreviewMutation(mutation, body.Anatomy, body.Condition)
                    : vitalProcesses.ApplyMutation(mutation, body.Anatomy, body.Condition, restoring);
                if (!vital.Succeeded && !vital.Duplicate)
                {
                    return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.OwningSystemFailure, vital.Message, request.TickId, ActorBodyId, method.Id, process.ProcessId, evaluation, CreateSnapshot());
                }

                amount = vital.AppliedAmount;
            }
            else
            {
                string conditionTransaction = $"{request.TickId}.{process.ProcessId}.structure";
                StructureConditionSnapshot structure = body.Condition.Structures.FirstOrDefault(candidate => string.Equals(candidate.NodeId, process.Target.AnatomyNodeId, StringComparison.Ordinal));
                int limit = structure == null ? 0 : Mathf.RoundToInt(structure.MaximumIntegrity * method.MaximumRecoverableIntegrityPercent);
                StructuralRecoveryRequest structuralRequest = new StructuralRecoveryRequest
                {
                    TransactionId = conditionTransaction,
                    SourceId = process.ProcessId,
                    TargetActorBodyId = ActorBodyId,
                    TargetNodeId = process.Target.AnatomyNodeId,
                    TargetInjuryId = process.Target.InjuryId,
                    RecoveryMethodId = method.Id,
                    IntegrityRestoration = Mathf.Max(1, Mathf.RoundToInt(Math.Min(amount, process.RequiredProgress - process.CurrentProgress))),
                    MaximumRecoverableIntegrity = limit,
                    ResolveInjuryOnCompletion = method.ResolvesInjuryOnCompletion,
                    AllowDestroyedStructure = method.CanRestoreDestroyedStructure,
                    AllowMissingStructure = method.CanRestoreMissingStructure,
                    AllowSeveredStructure = method.CanRestoreSeveredStructure,
                    ExpectedConditionRevision = body.Condition.ConditionRevision,
                    Context = request.AuthorityContext
                };
                structural = preview ? condition.PreviewStructuralRecovery(structuralRequest) : condition.ApplyStructuralRecovery(structuralRequest, restoring);
                if (!structural.Succeeded && !structural.Duplicate)
                {
                    return BiologicalRecoveryResult.Failure(structural.Code == StructuralRecoveryResultCode.RecoveryLimitReached ? BiologicalRecoveryResultCode.RecoveryLimitReached : BiologicalRecoveryResultCode.OwningSystemFailure, structural.Message, request.TickId, ActorBodyId, method.Id, process.ProcessId, evaluation, CreateSnapshot());
                }

                amount = structural.IntegrityRestored;
            }

            float newProgress = Mathf.Min(process.RequiredProgress, previousProgress + amount);
            if (!preview)
            {
                process.CurrentProgress = newProgress;
                process.EffectiveRatePerHour = ResolveBaseRate(method, process.Target) * evaluation.RateMultiplier;
                process.CompatibilitySummary = FormatCompatibility(evaluation);
                process.LastCommittedTickId = request.TickId;
                process.State = newProgress >= process.RequiredProgress ? RecoveryProcessState.Completed : RecoveryProcessState.Active;
                process.Revision++;
            }

            return BiologicalRecoveryResult.Success(request.TickId, ActorBodyId, method.Id, process.ProcessId, previousProgress, newProgress, amount, previousState, newProgress >= process.RequiredProgress ? RecoveryProcessState.Completed : RecoveryProcessState.Active, evaluation, structural, vital, CreateSnapshot(), preview: preview);
        }

        private float ResolveRateModifierMultiplier()
        {
            float multiplier = 1f;
            foreach (RecoveryRateModifierRecord modifier in rateModifiersBySource.Values)
            {
                multiplier *= Math.Max(0f, modifier.RateMultiplier);
            }

            return multiplier;
        }

        private bool ValidateBodySnapshot(BodySnapshot body, out BiologicalRecoveryResult failure, bool updateReadiness = true)
        {
            failure = null;
            if (body == null || !body.Coherent)
            {
                if (updateReadiness)
                {
                    Readiness = RecoveryReadinessState.WaitingForBody;
                }

                failure = BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingBody, "Biological recovery requires a coherent body snapshot.", actorBodyId: body?.ActorBodyId, snapshot: CreateSnapshot());
                return false;
            }

            if (body.Anatomy == null || !body.Anatomy.Coherent)
            {
                if (updateReadiness)
                {
                    Readiness = RecoveryReadinessState.WaitingForAnatomy;
                }

                failure = BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingBody, "Biological recovery requires a coherent Anatomy snapshot.", actorBodyId: body.ActorBodyId, snapshot: CreateSnapshot());
                return false;
            }

            if (body.Condition == null || !body.Condition.Coherent)
            {
                if (updateReadiness)
                {
                    Readiness = RecoveryReadinessState.WaitingForBodyCondition;
                }

                failure = BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingBody, "Biological recovery requires a coherent Body Condition snapshot.", actorBodyId: body.ActorBodyId, snapshot: CreateSnapshot());
                return false;
            }

            if (body.VitalProcesses == null || !body.VitalProcesses.Coherent)
            {
                if (updateReadiness)
                {
                    Readiness = RecoveryReadinessState.WaitingForVitalProcesses;
                }

                failure = BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingBody, "Biological recovery requires a coherent Vital Process snapshot.", actorBodyId: body.ActorBodyId, snapshot: CreateSnapshot());
                return false;
            }

            if (body.BiologicalHazards == null || !body.BiologicalHazards.Coherent)
            {
                if (updateReadiness)
                {
                    Readiness = RecoveryReadinessState.WaitingForHazards;
                }

                failure = BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingBody, "Biological recovery requires a coherent Biological Hazard snapshot.", actorBodyId: body.ActorBodyId, snapshot: CreateSnapshot());
                return false;
            }

            if (body.BiologicalCompatibility == null || !body.BiologicalCompatibility.Coherent)
            {
                if (updateReadiness)
                {
                    Readiness = RecoveryReadinessState.WaitingForCompatibility;
                }

                failure = BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.MissingCompatibility, "Biological recovery requires a coherent Biological Compatibility snapshot.", actorBodyId: body.ActorBodyId, snapshot: CreateSnapshot());
                return false;
            }

            return true;
        }

        private BiologicalRecoveryProfileDefinition ResolveProfile(BodySnapshot body)
        {
            if (registry == null || body == null)
            {
                return null;
            }

            return registry.DefinitionsById.Values
                .OfType<BiologicalRecoveryProfileDefinition>()
                .Where(profile => profile.IsCompatibleWith(body))
                .OrderBy(profile => profile.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private bool ValidateTarget(RecoveryTargetReference target, BodySnapshot body, RecoveryMethodDefinition method, out string failureReason, out AnatomyNodeSnapshot node)
        {
            failureReason = string.Empty;
            node = null;
            if (target == null || !string.Equals(target.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                failureReason = "Recovery target does not belong to the exact body.";
                return false;
            }

            if (target.TargetCategory == RecoveryTargetCategory.VitalResource)
            {
                VitalResourceSnapshot resource = body.VitalProcesses.Resources.FirstOrDefault(candidate => string.Equals(candidate.ResourceId, target.ResourceDefinitionId, StringComparison.Ordinal));
                if (resource == null)
                {
                    failureReason = $"Vital resource '{target.ResourceDefinitionId}' does not exist.";
                    return false;
                }

                if (!resource.Active)
                {
                    failureReason = $"Vital resource '{target.ResourceDefinitionId}' is inactive.";
                    return false;
                }

                if (!method.RestoresResource(target.ResourceDefinitionId))
                {
                    failureReason = $"Recovery Method '{method.Id}' does not restore resource '{target.ResourceDefinitionId}'.";
                    return false;
                }

                return true;
            }

            node = body.Anatomy.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, target.AnatomyNodeId, StringComparison.Ordinal));
            if (node == null)
            {
                failureReason = $"Anatomy node '{target.AnatomyNodeId}' does not exist.";
                return false;
            }

            if (target.TargetCategory == RecoveryTargetCategory.Injury && !body.Condition.Injuries.Any(injury => string.Equals(injury.InjuryId, target.InjuryId, StringComparison.Ordinal) && injury.State == InjuryRecordState.Active))
            {
                failureReason = $"Active injury '{target.InjuryId}' does not exist.";
                return false;
            }

            return true;
        }

        private bool RestContextAllowed(RecoveryMethodDefinition method)
        {
            if (!restContext.Active)
            {
                return false;
            }

            return method.AllowedRestTypes.Count == 0 || method.AllowedRestTypes.Contains(restContext.RestType);
        }

        private BiologicalRecoveryResult ValidateTickDependencies(RecoveryTickRequest request, BodySnapshot body)
        {
            if (request.ExpectedBodyRevision > 0L && request.ExpectedBodyRevision != body.BodyRevision)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleBody, $"Expected body revision {request.ExpectedBodyRevision} but current body is {body.BodyRevision}.", request.TickId, ActorBodyId, snapshot: CreateSnapshot());
            }

            if (request.ExpectedConditionRevision > 0L && request.ExpectedConditionRevision != body.Condition.ConditionRevision)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleDependency, $"Expected condition revision {request.ExpectedConditionRevision} but current condition is {body.Condition.ConditionRevision}.", request.TickId, ActorBodyId, snapshot: CreateSnapshot());
            }

            if (request.ExpectedVitalRevision > 0L && request.ExpectedVitalRevision != body.VitalProcesses.VitalRevision)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleDependency, $"Expected vital revision {request.ExpectedVitalRevision} but current vitals are {body.VitalProcesses.VitalRevision}.", request.TickId, ActorBodyId, snapshot: CreateSnapshot());
            }

            if (request.ExpectedHazardRevision > 0L && request.ExpectedHazardRevision != body.BiologicalHazards.HazardRevision)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleDependency, $"Expected hazard revision {request.ExpectedHazardRevision} but current hazards are {body.BiologicalHazards.HazardRevision}.", request.TickId, ActorBodyId, snapshot: CreateSnapshot());
            }

            if (request.ExpectedCompatibilityRevision > 0L && request.ExpectedCompatibilityRevision != body.BiologicalCompatibility.CompatibilityRevision)
            {
                return BiologicalRecoveryResult.Failure(BiologicalRecoveryResultCode.StaleDependency, $"Expected compatibility revision {request.ExpectedCompatibilityRevision} but current compatibility is {body.BiologicalCompatibility.CompatibilityRevision}.", request.TickId, ActorBodyId, snapshot: CreateSnapshot());
            }

            return null;
        }

        private RecoveryAllocationPolicy ResolveAllocationPolicy(BodySnapshot body)
        {
            BiologicalRecoveryProfileDefinition profile = ResolveProfile(body);
            return profile == null ? RecoveryAllocationPolicy.ExplicitTargetOnly : profile.AllocationPolicy;
        }

        private IEnumerable<RecoveryProcessRecord> SelectProcessesForTick(BodySnapshot body, RecoveryAllocationPolicy policy)
        {
            IEnumerable<RecoveryProcessRecord> processes = processesById.Values.Where(CanTickProcess);
            switch (policy)
            {
                case RecoveryAllocationPolicy.OldestInjuryFirst:
                    return processes.OrderBy(process => process.CreatedSequence).ThenBy(process => process.ProcessId, StringComparer.Ordinal);
                case RecoveryAllocationPolicy.NewestInjuryFirst:
                    return processes.OrderByDescending(process => process.CreatedSequence).ThenBy(process => process.ProcessId, StringComparer.Ordinal);
                case RecoveryAllocationPolicy.VitalStructuresFirst:
                    return processes.OrderByDescending(process => IsVitalTarget(body, process)).ThenBy(process => process.TargetSortKey, StringComparer.Ordinal).ThenBy(process => process.ProcessId, StringComparer.Ordinal);
                case RecoveryAllocationPolicy.HighestSeverityFirst:
                    return processes.OrderByDescending(process => GetTargetSeverity(body, process)).ThenBy(process => process.TargetSortKey, StringComparer.Ordinal).ThenBy(process => process.ProcessId, StringComparer.Ordinal);
                case RecoveryAllocationPolicy.LowestSeverityFirst:
                    return processes.OrderBy(process => GetTargetSeverity(body, process)).ThenBy(process => process.TargetSortKey, StringComparer.Ordinal).ThenBy(process => process.ProcessId, StringComparer.Ordinal);
                default:
                    return processes.OrderBy(process => process.TargetSortKey, StringComparer.Ordinal).ThenBy(process => process.ProcessId, StringComparer.Ordinal);
            }
        }

        private static bool CanTickProcess(RecoveryProcessRecord process)
        {
            return process != null
                && (process.State == RecoveryProcessState.Active
                    || process.State == RecoveryProcessState.Eligible
                    || process.State == RecoveryProcessState.Paused
                    || process.State == RecoveryProcessState.Suppressed
                    || process.State == RecoveryProcessState.Blocked);
        }

        private static float ResolveAllocatedHours(float hours, int processCount, RecoveryAllocationPolicy policy)
        {
            if (processCount <= 1 || UsesExclusiveAllocation(policy))
            {
                return hours;
            }

            return hours / processCount;
        }

        private static bool UsesExclusiveAllocation(RecoveryAllocationPolicy policy)
        {
            return policy == RecoveryAllocationPolicy.HighestSeverityFirst
                || policy == RecoveryAllocationPolicy.LowestSeverityFirst
                || policy == RecoveryAllocationPolicy.VitalStructuresFirst
                || policy == RecoveryAllocationPolicy.OldestInjuryFirst
                || policy == RecoveryAllocationPolicy.NewestInjuryFirst
                || policy == RecoveryAllocationPolicy.ExplicitTargetOnly
                || policy == RecoveryAllocationPolicy.PriorityWeighted;
        }

        private static bool IsVitalTarget(BodySnapshot body, RecoveryProcessRecord process)
        {
            if (body == null || process?.Target == null || process.Target.TargetCategory == RecoveryTargetCategory.VitalResource)
            {
                return true;
            }

            return body.Anatomy?.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, process.Target.AnatomyNodeId, StringComparison.Ordinal))?.Vital ?? false;
        }

        private static int GetTargetSeverity(BodySnapshot body, RecoveryProcessRecord process)
        {
            if (body?.Condition == null || process?.Target == null)
            {
                return 0;
            }

            if (process.Target.TargetCategory == RecoveryTargetCategory.VitalResource)
            {
                VitalResourceSnapshot resource = body.VitalProcesses?.Resources.FirstOrDefault(candidate => string.Equals(candidate.ResourceId, process.Target.ResourceDefinitionId, StringComparison.Ordinal));
                return resource == null ? 0 : Mathf.RoundToInt(Mathf.Abs(resource.EffectiveMaximumValue - resource.CurrentValue));
            }

            StructureConditionSnapshot structure = body.Condition.Structures.FirstOrDefault(candidate => string.Equals(candidate.NodeId, process.Target.AnatomyNodeId, StringComparison.Ordinal));
            return structure == null ? 0 : Mathf.Max(0, structure.MaximumIntegrity - structure.CurrentIntegrity);
        }

        private static float ResolveRequiredProgress(RecoveryTargetReference target, BodySnapshot body, RecoveryMethodDefinition method)
        {
            if (target.TargetCategory == RecoveryTargetCategory.VitalResource)
            {
                VitalResourceSnapshot resource = body.VitalProcesses.Resources.FirstOrDefault(candidate => string.Equals(candidate.ResourceId, target.ResourceDefinitionId, StringComparison.Ordinal));
                if (resource == null)
                {
                    return 0f;
                }

                if (resource.ModelType == BiologicalResourceModelType.AccumulatingNeed)
                {
                    return Math.Max(0f, resource.CurrentValue - resource.MinimumValue);
                }

                return Math.Max(0f, resource.EffectiveMaximumValue - resource.CurrentValue);
            }

            StructureConditionSnapshot structure = body.Condition.Structures.FirstOrDefault(candidate => string.Equals(candidate.NodeId, target.AnatomyNodeId, StringComparison.Ordinal));
            if (structure == null)
            {
                return 0f;
            }

            int limit = Mathf.RoundToInt(structure.MaximumIntegrity * method.MaximumRecoverableIntegrityPercent);
            return Math.Max(0, limit - structure.CurrentIntegrity);
        }

        private static float ResolveBaseRate(RecoveryMethodDefinition method, RecoveryTargetReference target)
        {
            if (target.TargetCategory == RecoveryTargetCategory.VitalResource)
            {
                return method.VitalResourcePerHour > 0f ? method.VitalResourcePerHour : method.BaseProgressPerHour;
            }

            return method.StructuralIntegrityPerHour > 0f ? method.StructuralIntegrityPerHour : method.BaseProgressPerHour;
        }

        private static BiologicalInteractionCategory ToInteractionCategory(RecoveryCategory category)
        {
            switch (category)
            {
                case RecoveryCategory.ConstructRepair:
                    return BiologicalInteractionCategory.Repair;
                case RecoveryCategory.MagicalHealing:
                case RecoveryCategory.HolyHealing:
                case RecoveryCategory.NecroticRestoration:
                    return BiologicalInteractionCategory.Healing;
                default:
                    return BiologicalInteractionCategory.Recovery;
            }
        }

        private bool ValidateRuntime(out string failureReason)
        {
            failureReason = string.Empty;
            if (Readiness == RecoveryReadinessState.Disposed)
            {
                failureReason = "Biological recovery runtime is disposed.";
                return false;
            }

            if (Readiness == RecoveryReadinessState.Uninitialized)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(ActorBodyId))
            {
                failureReason = "Biological recovery runtime is missing an exact Actor/body ID.";
                return false;
            }

            foreach (RecoveryProcessRecord process in processesById.Values)
            {
                if (!string.Equals(process.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
                {
                    failureReason = $"Recovery process '{process.ProcessId}' belongs to another body.";
                    return false;
                }

                if (process.CurrentProgress < 0f || process.RequiredProgress < 0f)
                {
                    failureReason = $"Recovery process '{process.ProcessId}' has invalid progress.";
                    return false;
                }
            }

            return Readiness == RecoveryReadinessState.Ready || Readiness == RecoveryReadinessState.Restoring;
        }

        private void RestoreRuntimeState(BiologicalRecoverySaveData saveData, bool dirty, BodySnapshot body, DefinitionRegistry definitionRegistry)
        {
            if (saveData == null)
            {
                return;
            }

            registry = definitionRegistry ?? registry;
            actorBodyId = saveData.actorBodyId;
            personId = saveData.personId;
            speciesId = saveData.speciesDefinitionId;
            profileId = saveData.profileDefinitionId;
            bodyRevision = saveData.bodyRevision;
            conditionRevision = saveData.conditionRevision;
            vitalRevision = saveData.vitalRevision;
            hazardRevision = saveData.hazardRevision;
            compatibilityRevision = saveData.compatibilityRevision;
            RecoveryRevision = Math.Max(0L, saveData.recoveryRevision);
            restContext = RestFromSaveData(saveData.restContext, actorBodyId);
            processesById.Clear();
            nextProcessSequence = 1L;
            foreach (RecoveryProcessSaveData processData in saveData.processes ?? Array.Empty<RecoveryProcessSaveData>())
            {
                RecoveryProcessRecord process = RecoveryProcessRecord.FromSaveData(processData);
                processesById[process.ProcessId] = process;
                nextProcessSequence = Math.Max(nextProcessSequence, process.CreatedSequence + 1L);
            }

            rateModifiersBySource.Clear();
            foreach (RecoveryRateModifierSaveData modifierData in saveData.rateModifiers ?? Array.Empty<RecoveryRateModifierSaveData>())
            {
                RecoveryRateModifierRecord modifier = RecoveryRateModifierRecord.FromSaveData(modifierData);
                if (modifier != null)
                {
                    rateModifiersBySource[modifier.SourceId] = modifier;
                }
            }

            committedTransactionIds.Clear();
            foreach (string transactionId in saveData.committedTransactionIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(transactionId))
                {
                    committedTransactionIds.Add(transactionId);
                }
            }

            committedTickIds.Clear();
            foreach (string tickId in saveData.committedTickIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(tickId))
                {
                    committedTickIds.Add(tickId);
                }
            }

            Readiness = RecoveryReadinessState.Ready;
            IsDirty = dirty;
        }

        private void RaiseChanged(BiologicalRecoveryResult result, bool restoring)
        {
            if (!suppressEvents)
            {
                RecoveryChanged?.Invoke(this, result, restoring);
            }
        }

        private IDisposable SuppressEvents()
        {
            suppressEvents = true;
            return new Suppression(this);
        }

        private static RecoveryTargetReference CloneTarget(RecoveryTargetReference target)
        {
            return new RecoveryTargetReference
            {
                TargetCategory = target.TargetCategory,
                ActorBodyId = target.ActorBodyId,
                AnatomyNodeId = target.AnatomyNodeId,
                InjuryId = target.InjuryId,
                ResourceDefinitionId = target.ResourceDefinitionId,
                HazardInstanceId = target.HazardInstanceId,
                StableTargetKey = target.StableTargetKey,
                OwningSystemRevision = target.OwningSystemRevision
            };
        }

        private static RecoveryTargetSaveData TargetToSaveData(RecoveryTargetReference target)
        {
            return new RecoveryTargetSaveData
            {
                targetCategory = target.TargetCategory,
                actorBodyId = target.ActorBodyId,
                anatomyNodeId = target.AnatomyNodeId,
                injuryId = target.InjuryId,
                resourceDefinitionId = target.ResourceDefinitionId,
                hazardInstanceId = target.HazardInstanceId,
                stableTargetKey = target.StableTargetKey,
                owningSystemRevision = target.OwningSystemRevision
            };
        }

        private static RecoveryTargetReference TargetFromSaveData(RecoveryTargetSaveData target)
        {
            return new RecoveryTargetReference
            {
                TargetCategory = target == null ? RecoveryTargetCategory.Unknown : target.targetCategory,
                ActorBodyId = target == null ? string.Empty : target.actorBodyId,
                AnatomyNodeId = target == null ? string.Empty : target.anatomyNodeId,
                InjuryId = target == null ? string.Empty : target.injuryId,
                ResourceDefinitionId = target == null ? string.Empty : target.resourceDefinitionId,
                HazardInstanceId = target == null ? string.Empty : target.hazardInstanceId,
                StableTargetKey = target == null ? string.Empty : target.stableTargetKey,
                OwningSystemRevision = target == null ? 0L : target.owningSystemRevision
            };
        }

        private static RecoveryRestContextSaveData RestToSaveData(RecoveryRestContextSnapshot snapshot)
        {
            return new RecoveryRestContextSaveData
            {
                actorBodyId = snapshot == null ? string.Empty : snapshot.ActorBodyId,
                restType = snapshot == null ? RecoveryRestType.NotResting : snapshot.RestType,
                sourceId = snapshot == null ? string.Empty : snapshot.SourceId,
                transactionId = snapshot == null ? string.Empty : snapshot.TransactionId,
                quality = snapshot == null ? 0f : snapshot.Quality,
                tags = snapshot == null ? Array.Empty<string>() : snapshot.Tags.ToArray()
            };
        }

        private static RecoveryRestContextSnapshot RestFromSaveData(RecoveryRestContextSaveData saveData, string fallbackActorBodyId)
        {
            return saveData == null
                ? new RecoveryRestContextSnapshot(fallbackActorBodyId, RecoveryRestType.NotResting, string.Empty, string.Empty, 0f, Array.Empty<string>())
                : new RecoveryRestContextSnapshot(saveData.actorBodyId, saveData.restType, saveData.sourceId, saveData.transactionId, Mathf.Max(0f, saveData.quality), saveData.tags);
        }

        private static string FormatCompatibility(BiologicalInteractionEvaluationResult evaluation)
        {
            if (evaluation == null)
            {
                return string.Empty;
            }

            return $"{evaluation.InteractionDefinitionId} State={evaluation.CompatibilityState} Rate={evaluation.RateMultiplier:0.###} Suppressed={evaluation.Suppressed} Immune={evaluation.Immune} Affinity={evaluation.Affinity}";
        }

        private static string BuildProcessId(string transactionId, string actorBodyId, string methodId, string targetKey)
        {
            return $"recovery-process.{Sanitize(actorBodyId)}.{Sanitize(methodId)}.{Sanitize(targetKey)}.{Sanitize(transactionId)}";
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "missing" : value.Trim().Replace(' ', '-').Replace(':', '.').Replace('/', '.');
        }

        private sealed class Suppression : IDisposable
        {
            private readonly BiologicalRecoveryRuntime owner;

            public Suppression(BiologicalRecoveryRuntime owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (owner != null)
                {
                    owner.suppressEvents = false;
                }
            }
        }

        private sealed class RecoveryProcessRecord
        {
            public RecoveryProcessRecord(string processId, string actorBodyId, string recoveryMethodId, string sourceId, string sourceTransactionId, RecoveryTargetReference target, float currentProgress, float requiredProgress, float baseRatePerHour, float effectiveRatePerHour, RecoveryProcessState state, RecoveryInterruptionPolicy interruptionPolicy, RecoveryLimit recoveryLimit, RecoveryPermanentOutcome projectedPermanentOutcome, string compatibilitySummary, long createdSequence, string lastCommittedTickId, long revision)
            {
                ProcessId = processId ?? string.Empty;
                ActorBodyId = actorBodyId ?? string.Empty;
                RecoveryMethodId = recoveryMethodId ?? string.Empty;
                SourceId = sourceId ?? string.Empty;
                SourceTransactionId = sourceTransactionId ?? string.Empty;
                Target = CloneTarget(target);
                CurrentProgress = Mathf.Max(0f, currentProgress);
                RequiredProgress = Mathf.Max(0f, requiredProgress);
                BaseRatePerHour = Mathf.Max(0f, baseRatePerHour);
                EffectiveRatePerHour = Mathf.Max(0f, effectiveRatePerHour);
                State = state;
                InterruptionPolicy = interruptionPolicy;
                RecoveryLimit = recoveryLimit;
                ProjectedPermanentOutcome = projectedPermanentOutcome;
                CompatibilitySummary = compatibilitySummary ?? string.Empty;
                CreatedSequence = Math.Max(1L, createdSequence);
                LastCommittedTickId = lastCommittedTickId ?? string.Empty;
                Revision = Math.Max(1L, revision);
            }

            public string ProcessId { get; }
            public string ActorBodyId { get; }
            public string RecoveryMethodId { get; }
            public string SourceId { get; }
            public string SourceTransactionId { get; }
            public RecoveryTargetReference Target { get; }
            public string TargetSortKey => Target == null ? string.Empty : Target.GetStableKey();
            public float CurrentProgress { get; set; }
            public float RequiredProgress { get; }
            public float BaseRatePerHour { get; }
            public float EffectiveRatePerHour { get; set; }
            public RecoveryProcessState State { get; set; }
            public RecoveryInterruptionPolicy InterruptionPolicy { get; }
            public RecoveryLimit RecoveryLimit { get; }
            public RecoveryPermanentOutcome ProjectedPermanentOutcome { get; }
            public string CompatibilitySummary { get; set; }
            public long CreatedSequence { get; }
            public string LastCommittedTickId { get; set; }
            public long Revision { get; set; }

            public RecoveryProcessSnapshot CreateSnapshot()
            {
                RecoveryTargetSnapshot target = new RecoveryTargetSnapshot(Target.TargetCategory, Target.ActorBodyId, Target.AnatomyNodeId, Target.InjuryId, Target.ResourceDefinitionId, Target.HazardInstanceId, Target.GetStableKey(), Target.OwningSystemRevision);
                return new RecoveryProcessSnapshot(ProcessId, ActorBodyId, RecoveryMethodId, SourceId, target, CurrentProgress, RequiredProgress, BaseRatePerHour, EffectiveRatePerHour, State, InterruptionPolicy, RecoveryLimit, ProjectedPermanentOutcome, CompatibilitySummary, LastCommittedTickId, Revision);
            }

            public RecoveryProcessSaveData ToSaveData()
            {
                return new RecoveryProcessSaveData
                {
                    processId = ProcessId,
                    actorBodyId = ActorBodyId,
                    recoveryMethodId = RecoveryMethodId,
                    sourceId = SourceId,
                    sourceTransactionId = SourceTransactionId,
                    target = TargetToSaveData(Target),
                    currentProgress = CurrentProgress,
                    requiredProgress = RequiredProgress,
                    baseRatePerHour = BaseRatePerHour,
                    effectiveRatePerHour = EffectiveRatePerHour,
                    state = State,
                    interruptionPolicy = InterruptionPolicy,
                    recoveryLimit = RecoveryLimit,
                    projectedPermanentOutcome = ProjectedPermanentOutcome,
                    compatibilitySummary = CompatibilitySummary,
                    createdSequence = CreatedSequence,
                    lastCommittedTickId = LastCommittedTickId,
                    revision = Revision
                };
            }

            public static RecoveryProcessRecord FromSaveData(RecoveryProcessSaveData saveData)
            {
                return new RecoveryProcessRecord(
                    saveData.processId,
                    saveData.actorBodyId,
                    saveData.recoveryMethodId,
                    saveData.sourceId,
                    saveData.sourceTransactionId,
                    TargetFromSaveData(saveData.target),
                    saveData.currentProgress,
                    saveData.requiredProgress,
                    saveData.baseRatePerHour,
                    saveData.effectiveRatePerHour,
                    saveData.state,
                    saveData.interruptionPolicy,
                    saveData.recoveryLimit,
                    saveData.projectedPermanentOutcome,
                    saveData.compatibilitySummary,
                    saveData.createdSequence,
                    saveData.lastCommittedTickId,
                    saveData.revision);
            }
        }

        private sealed class RecoveryRateModifierRecord
        {
            public RecoveryRateModifierRecord(string sourceId, float rateMultiplier, string reason, long revision)
            {
                SourceId = sourceId ?? string.Empty;
                RateMultiplier = Math.Max(0f, rateMultiplier);
                Reason = reason ?? string.Empty;
                Revision = Math.Max(0L, revision);
            }

            public string SourceId { get; }
            public float RateMultiplier { get; }
            public string Reason { get; }
            public long Revision { get; }

            public RecoveryRateModifierSnapshot CreateSnapshot()
            {
                return new RecoveryRateModifierSnapshot(SourceId, RateMultiplier, Reason, Revision);
            }

            public RecoveryRateModifierSaveData ToSaveData()
            {
                return new RecoveryRateModifierSaveData
                {
                    sourceId = SourceId,
                    rateMultiplier = RateMultiplier,
                    reason = Reason,
                    revision = Revision
                };
            }

            public static RecoveryRateModifierRecord FromSaveData(RecoveryRateModifierSaveData saveData)
            {
                return saveData == null || string.IsNullOrWhiteSpace(saveData.sourceId)
                    ? null
                    : new RecoveryRateModifierRecord(saveData.sourceId, saveData.rateMultiplier, saveData.reason, saveData.revision);
            }
        }
    }
}
