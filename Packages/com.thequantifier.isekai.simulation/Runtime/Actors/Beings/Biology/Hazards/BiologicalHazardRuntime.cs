using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Hazards
{
    public sealed class BiologicalHazardRuntime
    {
        private readonly Dictionary<string, BiologicalHazardDefinition> definitionsById = new Dictionary<string, BiologicalHazardDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, HazardInstanceRecord> hazardsById = new Dictionary<string, HazardInstanceRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> committedTickTransactionIds = new HashSet<string>(StringComparer.Ordinal);
        private string actorBodyId;
        private long bodyRevision;
        private long vitalRevision;
        private bool suppressEvents;

        public event Action<BiologicalHazardRuntime, BiologicalHazardOperationResult, bool> HazardChanged;
        public event Action<BiologicalHazardRuntime, BiologicalHazardTickResult, bool> HazardTicked;

        public BiologicalHazardReadinessState Readiness { get; private set; } = BiologicalHazardReadinessState.Uninitialized;
        public long HazardRevision { get; private set; }
        public bool IsDirty { get; private set; }
        public string ActorBodyId => actorBodyId ?? string.Empty;
        public bool IsReady => Readiness == BiologicalHazardReadinessState.Ready;

        public BiologicalHazardOperationResult BuildForBody(string exactActorBodyId, VitalProcessRuntime vitalProcesses, DefinitionRegistry registry, bool restoring = false, bool preserveRevision = false)
        {
            if (string.IsNullOrWhiteSpace(exactActorBodyId))
            {
                Readiness = BiologicalHazardReadinessState.WaitingForBody;
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingActorBody, "Biological hazards require an exact Actor/body ID.", CreateSnapshot());
            }

            if (vitalProcesses == null || !vitalProcesses.IsReady)
            {
                Readiness = BiologicalHazardReadinessState.WaitingForVitals;
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingVitalProcesses, "Biological hazards require ready vital processes.", CreateSnapshot());
            }

            actorBodyId = exactActorBodyId;
            bodyRevision = vitalProcesses.CreateSnapshot().BodyRevision;
            vitalRevision = vitalProcesses.VitalRevision;
            definitionsById.Clear();
            foreach (BiologicalHazardDefinition definition in registry?.DefinitionsById.Values.OfType<BiologicalHazardDefinition>().Where(definition => definition != null && definition.AlphaEnabled).OrderBy(definition => definition.Id, StringComparer.Ordinal) ?? Enumerable.Empty<BiologicalHazardDefinition>())
            {
                definitionsById[definition.Id] = definition;
            }

            hazardsById.Clear();
            committedTickTransactionIds.Clear();
            if (!preserveRevision)
            {
                HazardRevision++;
            }

            IsDirty = !restoring;
            Readiness = BiologicalHazardReadinessState.Ready;
            return BiologicalHazardOperationResult.Success("Biological hazards initialized.", CreateSnapshot());
        }

        public BiologicalHazardOperationResult AddOrUpdateSource(BiologicalHazardSourceRequest request, VitalProcessRuntime vitalProcesses, AnatomySnapshot anatomy, BodyConditionSnapshot condition, BiologicalCompatibilityRuntime compatibility, BodySnapshot body, bool restoring = false)
        {
            BiologicalHazardOperationResult validation = ValidateSourceRequest(request, vitalProcesses, anatomy, condition);
            if (!validation.Succeeded)
            {
                return validation;
            }

            BiologicalHazardDefinition definition = definitionsById[request.HazardDefinitionId];
            if (!string.IsNullOrWhiteSpace(definition.TargetResourceId)
                && (!vitalProcesses.TryGetResource(definition.TargetResourceId, out VitalResourceSnapshot resource) || !resource.Active))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.InactiveResource, $"Hazard '{definition.Id}' requires active biological resource '{definition.TargetResourceId}'.", CreateSnapshot());
            }

            BiologicalHazardOperationResult compatibilityValidation = ValidateCompatibilityContext(compatibility, body);
            if (!compatibilityValidation.Succeeded)
            {
                return compatibilityValidation;
            }

            BiologicalInteractionEvaluationResult compatibilityResult = EvaluateHazardCompatibility(compatibility, body, definition, request.SourceContributionId, preview: true);
            if (compatibilityResult == null)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingInteraction, $"Hazard '{definition.Id}' does not map to a biological interaction.", CreateSnapshot());
            }

            if (compatibilityResult.Code != BiologicalCompatibilityResultCode.Success)
            {
                return BiologicalHazardOperationResult.Failure(compatibilityResult.Code == BiologicalCompatibilityResultCode.StaleBody ? BiologicalHazardResultCode.StaleBody : BiologicalHazardResultCode.MissingCompatibility, compatibilityResult.Message, CreateSnapshot());
            }

            if (!compatibilityResult.Compatible || compatibilityResult.CompatibilityState == BiologicalCompatibilityState.Incompatible || compatibilityResult.Immune || compatibilityResult.Suppressed)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.InvalidRequest, $"Hazard '{definition.Id}' is not biologically compatible: {compatibilityResult.Message}", CreateSnapshot());
            }

            if (!hazardsById.TryGetValue(definition.Id, out HazardInstanceRecord hazard))
            {
                hazard = new HazardInstanceRecord(InstanceIdFor(ActorBodyId, definition.Id), definition);
                hazardsById[definition.Id] = hazard;
            }

            bool changed = hazard.AddOrUpdateSource(request);
            CleanupHazardIfEmpty(definition.Id);
            if (changed)
            {
                HazardRevision++;
                IsDirty = !restoring;
            }

            BiologicalHazardOperationResult result = BiologicalHazardOperationResult.Success($"Hazard source '{request.SourceContributionId}' applied to '{definition.Id}'.", CreateSnapshot());
            if (changed)
            {
                RaiseChanged(result, restoring);
            }

            return result;
        }

        public BiologicalHazardOperationResult PreviewAddOrUpdateSource(BiologicalHazardSourceRequest request, VitalProcessRuntime vitalProcesses, AnatomySnapshot anatomy, BodyConditionSnapshot condition, BiologicalCompatibilityRuntime compatibility, BodySnapshot body)
        {
            BiologicalHazardSaveData previewState = CreateSaveData();
            bool dirtyBeforePreview = IsDirty;
            using (SuppressEvents())
            {
                BiologicalHazardOperationResult result = AddOrUpdateSource(request, vitalProcesses, anatomy, condition, compatibility, body, restoring: true);
                RestoreRuntimeState(previewState, dirtyBeforePreview);
                return result.Succeeded
                    ? BiologicalHazardOperationResult.Success(result.Message, CreateSnapshot(), preview: true, duplicate: result.Duplicate)
                    : BiologicalHazardOperationResult.Failure(result.Code, result.Message, CreateSnapshot());
            }
        }

        public BiologicalHazardOperationResult RemoveSource(string hazardDefinitionId, string sourceContributionId, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.RuntimeNotReady, "Biological hazard runtime is not Ready.", CreateSnapshot());
            }

            if (string.IsNullOrWhiteSpace(hazardDefinitionId) || string.IsNullOrWhiteSpace(sourceContributionId))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.InvalidRequest, "Hazard source removal requires hazard and source IDs.", CreateSnapshot());
            }

            if (!hazardsById.TryGetValue(hazardDefinitionId, out HazardInstanceRecord hazard) || !hazard.RemoveSource(sourceContributionId))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingSource, $"Hazard source '{sourceContributionId}' is not active on '{hazardDefinitionId}'.", CreateSnapshot());
            }

            CleanupHazardIfEmpty(hazardDefinitionId);
            HazardRevision++;
            IsDirty = !restoring;
            BiologicalHazardOperationResult result = BiologicalHazardOperationResult.Success($"Hazard source '{sourceContributionId}' removed from '{hazardDefinitionId}'.", CreateSnapshot());
            RaiseChanged(result, restoring);
            return result;
        }

        public BiologicalHazardOperationResult AddOrUpdateSuppression(BiologicalHazardSuppressionRequest request, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.RuntimeNotReady, "Biological hazard runtime is not Ready.", CreateSnapshot());
            }

            if (!string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.StaleBody, $"Request body '{request.ActorBodyId}' does not match runtime body '{ActorBodyId}'.", CreateSnapshot());
            }

            if (!hazardsById.TryGetValue(request.HazardDefinitionId, out HazardInstanceRecord hazard))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingHazardDefinition, $"Hazard '{request.HazardDefinitionId}' is not active.", CreateSnapshot());
            }

            if (string.IsNullOrWhiteSpace(request.SourceContributionId) || request.RateMultiplier < 0f || float.IsNaN(request.RateMultiplier) || float.IsInfinity(request.RateMultiplier))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.InvalidRequest, "Hazard suppression requires a source ID and finite non-negative multiplier.", CreateSnapshot());
            }

            hazard.AddOrUpdateSuppression(request);
            HazardRevision++;
            IsDirty = !restoring;
            BiologicalHazardOperationResult result = BiologicalHazardOperationResult.Success($"Hazard suppression '{request.SourceContributionId}' applied to '{request.HazardDefinitionId}'.", CreateSnapshot());
            RaiseChanged(result, restoring);
            return result;
        }

        public BiologicalHazardOperationResult RemoveSuppression(string hazardDefinitionId, string sourceContributionId, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.RuntimeNotReady, "Biological hazard runtime is not Ready.", CreateSnapshot());
            }

            if (!hazardsById.TryGetValue(hazardDefinitionId ?? string.Empty, out HazardInstanceRecord hazard) || !hazard.RemoveSuppression(sourceContributionId))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingSource, $"Hazard suppression '{sourceContributionId}' is not active on '{hazardDefinitionId}'.", CreateSnapshot());
            }

            HazardRevision++;
            IsDirty = !restoring;
            BiologicalHazardOperationResult result = BiologicalHazardOperationResult.Success($"Hazard suppression '{sourceContributionId}' removed from '{hazardDefinitionId}'.", CreateSnapshot());
            RaiseChanged(result, restoring);
            return result;
        }

        public BiologicalHazardOperationResult SynchronizeFromVitalProcesses(VitalProcessRuntime vitalProcesses, AnatomySnapshot anatomy, BodyConditionSnapshot condition, BiologicalCompatibilityRuntime compatibility, BodySnapshot body, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.RuntimeNotReady, "Biological hazard runtime is not Ready.", CreateSnapshot());
            }

            if (vitalProcesses == null || !vitalProcesses.IsReady)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingVitalProcesses, "Biological hazards require ready vital processes.", CreateSnapshot());
            }

            BiologicalHazardOperationResult compatibilityValidation = ValidateCompatibilityContext(compatibility, body);
            if (!compatibilityValidation.Succeeded)
            {
                return compatibilityValidation;
            }

            bool changed = false;
            changed |= SyncCritical(vitalProcesses, BiologicalResourceIds.Nutrition, VitalProcessState.CriticalLow, BiologicalHazardIds.Starvation, BiologicalHazardSeverity.Serious, compatibility, body);
            changed |= SyncCritical(vitalProcesses, BiologicalResourceIds.Hydration, VitalProcessState.CriticalLow, BiologicalHazardIds.Dehydration, BiologicalHazardSeverity.Severe, compatibility, body);
            changed |= SyncCritical(vitalProcesses, BiologicalResourceIds.Fatigue, VitalProcessState.CriticalHigh, BiologicalHazardIds.ExtremeFatigue, BiologicalHazardSeverity.Serious, compatibility, body);
            changed |= SyncCritical(vitalProcesses, BiologicalResourceIds.SleepNeed, VitalProcessState.CriticalHigh, BiologicalHazardIds.SleepDeprivation, BiologicalHazardSeverity.Serious, compatibility, body);
            changed |= SyncTemperature(vitalProcesses, compatibility, body);
            if (changed)
            {
                HazardRevision++;
                IsDirty = !restoring;
            }

            BiologicalHazardOperationResult result = BiologicalHazardOperationResult.Success("Biological hazards synchronized from vital process state.", CreateSnapshot());
            if (changed)
            {
                RaiseChanged(result, restoring);
            }

            return result;
        }

        public BiologicalHazardTickResult PreviewTick(BiologicalHazardTickRequest request, VitalProcessRuntime vitalProcesses, AnatomySnapshot anatomy, BodyConditionSnapshot condition, BiologicalCompatibilityRuntime compatibility, BodySnapshot body)
        {
            return Tick(new BiologicalHazardTickRequest(request.ActorBodyId, request.ElapsedGameSeconds, request.TransactionId, preview: true, request.Reason), vitalProcesses, anatomy, condition, restoring: false, compatibility, body);
        }

        public BiologicalHazardTickResult ApplyTick(BiologicalHazardTickRequest request, VitalProcessRuntime vitalProcesses, AnatomySnapshot anatomy, BodyConditionSnapshot condition, BiologicalCompatibilityRuntime compatibility, BodySnapshot body, bool restoring = false)
        {
            return Tick(new BiologicalHazardTickRequest(request.ActorBodyId, request.ElapsedGameSeconds, request.TransactionId, preview: false, request.Reason), vitalProcesses, anatomy, condition, restoring, compatibility, body);
        }

        public BiologicalHazardSaveData CreateSaveData()
        {
            return new BiologicalHazardSaveData
            {
                schemaVersion = BiologicalHazardSaveData.CurrentSchemaVersion,
                actorBodyId = ActorBodyId,
                bodyRevision = bodyRevision,
                vitalRevision = vitalRevision,
                hazardRevision = HazardRevision,
                activeHazards = hazardsById.Values.OrderBy(hazard => hazard.HazardDefinitionId, StringComparer.Ordinal).Select(hazard => hazard.ToSaveData()).ToArray(),
                committedTickTransactionIds = committedTickTransactionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            };
        }

        public BiologicalHazardOperationResult RestoreFromSaveData(BiologicalHazardSaveData saveData, string exactActorBodyId, VitalProcessRuntime vitalProcesses, DefinitionRegistry registry)
        {
            if (!ValidateSaveData(saveData, exactActorBodyId, registry, out string failureReason))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.InvalidRestore, failureReason, CreateSnapshot());
            }

            using (SuppressEvents())
            {
                BiologicalHazardOperationResult build = BuildForBody(exactActorBodyId, vitalProcesses, registry, restoring: true, preserveRevision: true);
                if (!build.Succeeded)
                {
                    return build;
                }

                hazardsById.Clear();
                foreach (BiologicalHazardInstanceSaveData instanceData in saveData.activeHazards ?? Array.Empty<BiologicalHazardInstanceSaveData>())
                {
                    if (instanceData == null || !definitionsById.TryGetValue(instanceData.hazardDefinitionId, out BiologicalHazardDefinition definition))
                    {
                        continue;
                    }

                    HazardInstanceRecord record = HazardInstanceRecord.FromSaveData(instanceData, definition);
                    if (record != null && record.Sources.Count > 0)
                    {
                        hazardsById[record.HazardDefinitionId] = record;
                    }
                }

                committedTickTransactionIds.Clear();
                foreach (string transactionId in saveData.committedTickTransactionIds ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(transactionId))
                    {
                        committedTickTransactionIds.Add(transactionId);
                    }
                }

                bodyRevision = saveData.bodyRevision;
                vitalRevision = saveData.vitalRevision;
                HazardRevision = Math.Max(1L, saveData.hazardRevision);
                Readiness = BiologicalHazardReadinessState.Ready;
                IsDirty = false;
            }

            return BiologicalHazardOperationResult.Success("Biological hazards restored.", CreateSnapshot());
        }

        public static bool ValidateSaveData(BiologicalHazardSaveData saveData, string expectedActorBodyId, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Biological hazard save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > BiologicalHazardSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported biological hazard schema version {saveData.schemaVersion}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(saveData.actorBodyId))
            {
                failureReason = "Biological hazard save data is missing an Actor/body ID.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedActorBodyId) && !string.Equals(saveData.actorBodyId, expectedActorBodyId, StringComparison.Ordinal))
            {
                failureReason = $"Saved biological hazard body '{saveData.actorBodyId}' does not match current body '{expectedActorBodyId}'.";
                return false;
            }

            HashSet<string> hazardIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> sourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BiologicalHazardInstanceSaveData hazard in saveData.activeHazards ?? Array.Empty<BiologicalHazardInstanceSaveData>())
            {
                if (hazard == null || string.IsNullOrWhiteSpace(hazard.instanceId) || string.IsNullOrWhiteSpace(hazard.hazardDefinitionId) || !hazardIds.Add(hazard.hazardDefinitionId))
                {
                    failureReason = "Saved biological hazards contain a missing or duplicate hazard identity.";
                    return false;
                }

                if (registry == null || !registry.TryGet(hazard.hazardDefinitionId, out BiologicalHazardDefinition definition) || definition == null)
                {
                    failureReason = $"Saved biological hazard '{hazard.hazardDefinitionId}' does not resolve.";
                    return false;
                }

                foreach (BiologicalHazardSourceSaveData source in hazard.sources ?? Array.Empty<BiologicalHazardSourceSaveData>())
                {
                    if (source == null || string.IsNullOrWhiteSpace(source.sourceContributionId) || !sourceIds.Add(source.sourceContributionId))
                    {
                        failureReason = $"Saved biological hazard '{hazard.hazardDefinitionId}' contains a missing or duplicate source contribution ID.";
                        return false;
                    }

                    if (source.rateMultiplier < 0f || float.IsNaN(source.rateMultiplier) || float.IsInfinity(source.rateMultiplier))
                    {
                        failureReason = $"Saved biological hazard source '{source.sourceContributionId}' has an invalid rate multiplier.";
                        return false;
                    }
                }
            }

            return true;
        }

        public BiologicalHazardSnapshot CreateSnapshot()
        {
            List<string> diagnostics = new List<string>();
            bool coherent = ValidateRuntime(out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                diagnostics.Add(failureReason);
            }

            IReadOnlyList<BiologicalHazardInstanceSnapshot> hazards = hazardsById.Values
                .OrderBy(hazard => hazard.HazardDefinitionId, StringComparer.Ordinal)
                .Select(hazard => hazard.CreateSnapshot())
                .ToArray();
            return new BiologicalHazardSnapshot(ActorBodyId, Readiness, bodyRevision, vitalRevision, HazardRevision, hazards, IsDirty, coherent, diagnostics);
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        public void Dispose()
        {
            Readiness = BiologicalHazardReadinessState.Disposed;
            hazardsById.Clear();
            committedTickTransactionIds.Clear();
            definitionsById.Clear();
        }

        private BiologicalHazardTickResult Tick(BiologicalHazardTickRequest request, VitalProcessRuntime vitalProcesses, AnatomySnapshot anatomy, BodyConditionSnapshot condition, bool restoring, BiologicalCompatibilityRuntime compatibility, BodySnapshot body)
        {
            if (!IsReady)
            {
                return BiologicalHazardTickResult.Failure(request, BiologicalHazardResultCode.RuntimeNotReady, "Biological hazard runtime is not Ready.", CreateSnapshot());
            }

            if (!string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return BiologicalHazardTickResult.Failure(request, BiologicalHazardResultCode.StaleBody, $"Request body '{request.ActorBodyId}' does not match runtime body '{ActorBodyId}'.", CreateSnapshot());
            }

            if (vitalProcesses == null || !vitalProcesses.IsReady)
            {
                return BiologicalHazardTickResult.Failure(request, BiologicalHazardResultCode.MissingVitalProcesses, "Biological hazard ticks require ready vital processes.", CreateSnapshot());
            }

            if (request.ElapsedGameSeconds < 0f || float.IsNaN(request.ElapsedGameSeconds) || float.IsInfinity(request.ElapsedGameSeconds))
            {
                return BiologicalHazardTickResult.Failure(request, BiologicalHazardResultCode.InvalidAmount, "Elapsed game seconds must be finite and non-negative.", CreateSnapshot());
            }

            BiologicalHazardOperationResult compatibilityValidation = ValidateCompatibilityContext(compatibility, body);
            if (!compatibilityValidation.Succeeded)
            {
                return BiologicalHazardTickResult.Failure(request, compatibilityValidation.Code, compatibilityValidation.Message, CreateSnapshot());
            }

            if (!request.Preview && !string.IsNullOrWhiteSpace(request.TransactionId) && committedTickTransactionIds.Contains(request.TransactionId))
            {
                return BiologicalHazardTickResult.Success(request, Array.Empty<BiologicalHazardConsequence>(), CreateSnapshot(), duplicate: true);
            }

            BiologicalHazardSaveData previewState = request.Preview ? CreateSaveData() : null;
            bool dirtyBeforePreview = IsDirty;
            List<BiologicalHazardConsequence> consequences = new List<BiologicalHazardConsequence>();
            float hours = request.ElapsedGameSeconds / 3600f;
            foreach (HazardInstanceRecord hazard in hazardsById.Values.OrderBy(hazard => hazard.HazardDefinitionId, StringComparer.Ordinal).ToArray())
            {
                BiologicalInteractionEvaluationResult compatibilityResult = EvaluateHazardCompatibility(compatibility, body, hazard.Definition, request.TransactionId, request.Preview);
                if (compatibilityResult == null)
                {
                    return BiologicalHazardTickResult.Failure(request, BiologicalHazardResultCode.MissingInteraction, $"Hazard '{hazard.HazardDefinitionId}' does not map to a biological interaction.", CreateSnapshot());
                }

                if (compatibilityResult.Code != BiologicalCompatibilityResultCode.Success)
                {
                    return BiologicalHazardTickResult.Failure(request, compatibilityResult.Code == BiologicalCompatibilityResultCode.StaleBody ? BiologicalHazardResultCode.StaleBody : BiologicalHazardResultCode.MissingCompatibility, compatibilityResult.Message, CreateSnapshot());
                }

                if (compatibilityResult.CompatibilityState == BiologicalCompatibilityState.Incompatible || compatibilityResult.Immune || compatibilityResult.Suppressed)
                {
                    consequences.Add(new BiologicalHazardConsequence(BiologicalHazardTickConsequenceKind.None, hazard.HazardDefinitionId, string.Empty, null, null, BiologicalHazardLifecycleRequestKind.None, $"Hazard skipped by biological compatibility: {compatibilityResult.Message}"));
                    continue;
                }

                float compatibilityRate = compatibilityResult.RateMultiplier;
                float compatibilityConsequence = compatibilityResult.ConsequenceMultiplier;
                float rate = hazard.EffectiveRatePerHour * compatibilityRate;
                if (rate > 0f && hours > 0f && !string.IsNullOrWhiteSpace(hazard.Definition.TargetResourceId))
                {
                    string vitalTransaction = $"{request.TransactionId}.{hazard.HazardDefinitionId}.{hazard.Definition.TargetResourceId}";
                    VitalResourceMutationRequest mutation = new VitalResourceMutationRequest(
                        ActorBodyId,
                        hazard.Definition.TargetResourceId,
                        hazard.Definition.ResourceOperation,
                        rate * hours,
                        vitalTransaction,
                        hazard.HazardDefinitionId,
                        request.Reason);
                    VitalResourceMutationResult vitalResult = request.Preview
                        ? vitalProcesses.PreviewMutation(mutation, anatomy, condition)
                        : vitalProcesses.ApplyMutation(mutation, anatomy, condition, restoring);
                    if (!vitalResult.Succeeded)
                    {
                        if (request.Preview)
                        {
                            RestoreRuntimeState(previewState, dirtyBeforePreview);
                            IsDirty = dirtyBeforePreview;
                        }

                        return BiologicalHazardTickResult.Failure(request, vitalResult.Code == VitalProcessResultCode.InactiveResource ? BiologicalHazardResultCode.InactiveResource : BiologicalHazardResultCode.InvalidRequest, vitalResult.Message, CreateSnapshot());
                    }

                    consequences.Add(new BiologicalHazardConsequence(BiologicalHazardTickConsequenceKind.VitalResourceMutation, hazard.HazardDefinitionId, hazard.Definition.TargetResourceId, vitalResult, null, BiologicalHazardLifecycleRequestKind.None, vitalResult.Message));
                }

                if (hazard.Definition.BaseDamagePerHour > 0f && hours > 0f)
                {
                    consequences.Add(new BiologicalHazardConsequence(
                        BiologicalHazardTickConsequenceKind.Step6DamagePlan,
                        hazard.HazardDefinitionId,
                        string.Empty,
                        null,
                        new BiologicalHazardDamagePlan(hazard.HazardDefinitionId, $"{request.TransactionId}.{hazard.HazardDefinitionId}.damage", ActorBodyId, hazard.Definition.DamageType, hazard.Definition.BaseDamagePerHour * hours * hazard.EffectiveSourceMultiplier * compatibilityConsequence, "Biological hazard damage must be committed by Step 6 DamageHealingService."),
                        BiologicalHazardLifecycleRequestKind.None,
                        "Step 6 damage plan created; Health was not mutated by the hazard runtime."));
                }

                if (hazard.Definition.LifecycleRequest != BiologicalHazardLifecycleRequestKind.None)
                {
                    consequences.Add(new BiologicalHazardConsequence(BiologicalHazardTickConsequenceKind.LifecycleEvaluationRequest, hazard.HazardDefinitionId, string.Empty, null, null, hazard.Definition.LifecycleRequest, "Lifecycle evaluation requested; hazard runtime did not change lifecycle state."));
                }

                if (!request.Preview)
                {
                    hazard.Advance(request.ElapsedGameSeconds);
                }
            }

            if (!request.Preview)
            {
                foreach (string hazardId in hazardsById.Where(pair => pair.Value.Sources.Count == 0).Select(pair => pair.Key).ToArray())
                {
                    hazardsById.Remove(hazardId);
                }

                if (!string.IsNullOrWhiteSpace(request.TransactionId))
                {
                    committedTickTransactionIds.Add(request.TransactionId);
                }

                HazardRevision++;
                vitalRevision = vitalProcesses.VitalRevision;
                IsDirty = !restoring;
            }

            BiologicalHazardTickResult result = BiologicalHazardTickResult.Success(request, consequences, CreateSnapshot());
            if (request.Preview)
            {
                RestoreRuntimeState(previewState, dirtyBeforePreview);
            }
            else
            {
                RaiseTicked(result, restoring);
            }

            return result;
        }

        private static BiologicalInteractionEvaluationResult EvaluateHazardCompatibility(BiologicalCompatibilityRuntime compatibility, BodySnapshot body, BiologicalHazardDefinition definition, string sourceId, bool preview)
        {
            if (compatibility == null || body == null || definition == null)
            {
                return null;
            }

            string interactionId = BiologicalInteractionIds.FromHazardId(definition.Id);
            if (string.IsNullOrWhiteSpace(interactionId))
            {
                return null;
            }

            return compatibility.Evaluate(body, interactionId, BiologicalInteractionCategory.Hazard, sourceId: sourceId, preview: preview);
        }

        private BiologicalHazardOperationResult ValidateCompatibilityContext(BiologicalCompatibilityRuntime compatibility, BodySnapshot body)
        {
            if (compatibility == null || body == null)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingCompatibility, "Biological hazard execution requires a biological compatibility runtime and current body snapshot.", CreateSnapshot());
            }

            if (!compatibility.IsReady)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingCompatibility, "Biological compatibility runtime is not Ready.", CreateSnapshot());
            }

            if (!string.Equals(compatibility.ActorBodyId, ActorBodyId, StringComparison.Ordinal)
                || !string.Equals(body.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.StaleBody, $"Compatibility body '{compatibility.ActorBodyId}' and snapshot body '{body.ActorBodyId}' must match hazard body '{ActorBodyId}'.", CreateSnapshot());
            }

            if (body.BodyRevision != bodyRevision)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.StaleBody, $"Body snapshot revision {body.BodyRevision} does not match hazard body revision {bodyRevision}.", CreateSnapshot());
            }

            return BiologicalHazardOperationResult.Success("Biological hazard compatibility context validated.", CreateSnapshot());
        }

        private BiologicalHazardOperationResult ValidateSourceRequest(BiologicalHazardSourceRequest request, VitalProcessRuntime vitalProcesses, AnatomySnapshot anatomy, BodyConditionSnapshot condition)
        {
            if (!IsReady)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.RuntimeNotReady, "Biological hazard runtime is not Ready.", CreateSnapshot());
            }

            if (!string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.StaleBody, $"Request body '{request.ActorBodyId}' does not match runtime body '{ActorBodyId}'.", CreateSnapshot());
            }

            if (vitalProcesses == null || !vitalProcesses.IsReady || anatomy == null || condition == null)
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingVitalProcesses, "Biological hazards require ready vital, anatomy, and condition snapshots.", CreateSnapshot());
            }

            if (string.IsNullOrWhiteSpace(request.SourceContributionId))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.InvalidRequest, "Hazard source requires a stable source contribution ID.", CreateSnapshot());
            }

            if (!definitionsById.ContainsKey(request.HazardDefinitionId))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.MissingHazardDefinition, $"Hazard definition '{request.HazardDefinitionId}' is not registered.", CreateSnapshot());
            }

            if (request.RateMultiplier < 0f || float.IsNaN(request.RateMultiplier) || float.IsInfinity(request.RateMultiplier) || request.DurationSeconds < 0f || float.IsNaN(request.DurationSeconds) || float.IsInfinity(request.DurationSeconds))
            {
                return BiologicalHazardOperationResult.Failure(BiologicalHazardResultCode.InvalidAmount, "Hazard rate and duration values must be finite and non-negative.", CreateSnapshot());
            }

            return BiologicalHazardOperationResult.Success("Hazard source request validated.", CreateSnapshot());
        }

        private bool SyncCritical(VitalProcessRuntime vitalProcesses, string resourceId, VitalProcessState criticalState, string hazardDefinitionId, BiologicalHazardSeverity severity, BiologicalCompatibilityRuntime compatibility, BodySnapshot body)
        {
            string sourceId = $"hazard.vital.{ActorBodyId}.{resourceId}";
            bool critical = vitalProcesses.TryGetResource(resourceId, out VitalResourceSnapshot resource) && resource.Active && resource.State == criticalState;
            if (critical && definitionsById.ContainsKey(hazardDefinitionId))
            {
                BiologicalInteractionEvaluationResult compatibilityResult = EvaluateHazardCompatibility(compatibility, body, definitionsById[hazardDefinitionId], sourceId, preview: true);
                if (compatibilityResult == null || !compatibilityResult.Compatible || compatibilityResult.CompatibilityState == BiologicalCompatibilityState.Incompatible || compatibilityResult.Immune || compatibilityResult.Suppressed)
                {
                    return RemoveHazardSource(hazardDefinitionId, sourceId);
                }

                HazardInstanceRecord hazard = hazardsById.TryGetValue(hazardDefinitionId, out HazardInstanceRecord existing)
                    ? existing
                    : hazardsById[hazardDefinitionId] = new HazardInstanceRecord(InstanceIdFor(ActorBodyId, hazardDefinitionId), definitionsById[hazardDefinitionId]);
                return hazard.AddOrUpdateSource(new BiologicalHazardSourceRequest(ActorBodyId, hazardDefinitionId, sourceId, BiologicalHazardSourceCategory.VitalProcess, severity, 1f, 0f, resourceId, "Critical vital resource state"));
            }

            return RemoveHazardSource(hazardDefinitionId, sourceId);
        }

        private bool SyncTemperature(VitalProcessRuntime vitalProcesses, BiologicalCompatibilityRuntime compatibility, BodySnapshot body)
        {
            string hotSource = $"hazard.vital.{ActorBodyId}.{BiologicalResourceIds.Temperature}.high";
            string coldSource = $"hazard.vital.{ActorBodyId}.{BiologicalResourceIds.Temperature}.low";
            bool changed = false;
            if (!vitalProcesses.TryGetResource(BiologicalResourceIds.Temperature, out VitalResourceSnapshot temperature) || !temperature.Active)
            {
                changed |= hazardsById.TryGetValue(BiologicalHazardIds.Overheating, out HazardInstanceRecord hot) && hot.RemoveSource(hotSource) && CleanupHazardIfEmpty(BiologicalHazardIds.Overheating);
                changed |= hazardsById.TryGetValue(BiologicalHazardIds.Hypothermia, out HazardInstanceRecord cold) && cold.RemoveSource(coldSource) && CleanupHazardIfEmpty(BiologicalHazardIds.Hypothermia);
                return changed;
            }

            if (temperature.State == VitalProcessState.CriticalHigh)
            {
                changed |= RemoveTemperatureSource(BiologicalHazardIds.Hypothermia, coldSource);
                changed |= AddTemperatureSource(BiologicalHazardIds.Overheating, hotSource, BiologicalHazardSeverity.Severe, compatibility, body);
            }
            else if (temperature.State == VitalProcessState.CriticalLow)
            {
                changed |= RemoveTemperatureSource(BiologicalHazardIds.Overheating, hotSource);
                changed |= AddTemperatureSource(BiologicalHazardIds.Hypothermia, coldSource, BiologicalHazardSeverity.Severe, compatibility, body);
            }
            else
            {
                changed |= RemoveTemperatureSource(BiologicalHazardIds.Overheating, hotSource);
                changed |= RemoveTemperatureSource(BiologicalHazardIds.Hypothermia, coldSource);
            }

            return changed;
        }

        private bool AddTemperatureSource(string hazardDefinitionId, string sourceId, BiologicalHazardSeverity severity, BiologicalCompatibilityRuntime compatibility, BodySnapshot body)
        {
            if (!definitionsById.ContainsKey(hazardDefinitionId))
            {
                return false;
            }

            BiologicalInteractionEvaluationResult compatibilityResult = EvaluateHazardCompatibility(compatibility, body, definitionsById[hazardDefinitionId], sourceId, preview: true);
            if (compatibilityResult == null || !compatibilityResult.Compatible || compatibilityResult.CompatibilityState == BiologicalCompatibilityState.Incompatible || compatibilityResult.Immune || compatibilityResult.Suppressed)
            {
                return RemoveHazardSource(hazardDefinitionId, sourceId);
            }

            HazardInstanceRecord hazard = hazardsById.TryGetValue(hazardDefinitionId, out HazardInstanceRecord existing)
                ? existing
                : hazardsById[hazardDefinitionId] = new HazardInstanceRecord(InstanceIdFor(ActorBodyId, hazardDefinitionId), definitionsById[hazardDefinitionId]);
            return hazard.AddOrUpdateSource(new BiologicalHazardSourceRequest(ActorBodyId, hazardDefinitionId, sourceId, BiologicalHazardSourceCategory.VitalProcess, severity, 1f, 0f, BiologicalResourceIds.Temperature, "Critical body temperature"));
        }

        private bool RemoveHazardSource(string hazardDefinitionId, string sourceId)
        {
            if (hazardsById.TryGetValue(hazardDefinitionId, out HazardInstanceRecord active) && active.RemoveSource(sourceId))
            {
                CleanupHazardIfEmpty(hazardDefinitionId);
                return true;
            }

            return false;
        }

        private bool RemoveTemperatureSource(string hazardDefinitionId, string sourceId)
        {
            if (hazardsById.TryGetValue(hazardDefinitionId, out HazardInstanceRecord hazard) && hazard.RemoveSource(sourceId))
            {
                CleanupHazardIfEmpty(hazardDefinitionId);
                return true;
            }

            return false;
        }

        private bool CleanupHazardIfEmpty(string hazardDefinitionId)
        {
            if (hazardsById.TryGetValue(hazardDefinitionId, out HazardInstanceRecord hazard) && hazard.Sources.Count == 0)
            {
                hazardsById.Remove(hazardDefinitionId);
                return true;
            }

            return false;
        }

        private bool ValidateRuntime(out string failureReason)
        {
            failureReason = string.Empty;
            if (Readiness == BiologicalHazardReadinessState.Disposed)
            {
                failureReason = "Biological hazard runtime is disposed.";
                return false;
            }

            if (Readiness == BiologicalHazardReadinessState.Uninitialized)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(ActorBodyId))
            {
                failureReason = "Biological hazard runtime is missing an Actor/body ID.";
                return false;
            }

            return true;
        }

        private void RestoreRuntimeState(BiologicalHazardSaveData saveData, bool isDirty)
        {
            if (saveData == null)
            {
                return;
            }

            actorBodyId = saveData.actorBodyId;
            bodyRevision = saveData.bodyRevision;
            vitalRevision = saveData.vitalRevision;
            HazardRevision = saveData.hazardRevision;
            hazardsById.Clear();
            foreach (BiologicalHazardInstanceSaveData instanceData in saveData.activeHazards ?? Array.Empty<BiologicalHazardInstanceSaveData>())
            {
                if (instanceData != null && definitionsById.TryGetValue(instanceData.hazardDefinitionId, out BiologicalHazardDefinition definition))
                {
                    HazardInstanceRecord record = HazardInstanceRecord.FromSaveData(instanceData, definition);
                    if (record != null && record.Sources.Count > 0)
                    {
                        hazardsById[record.HazardDefinitionId] = record;
                    }
                }
            }

            committedTickTransactionIds.Clear();
            foreach (string transactionId in saveData.committedTickTransactionIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(transactionId))
                {
                    committedTickTransactionIds.Add(transactionId);
                }
            }

            Readiness = BiologicalHazardReadinessState.Ready;
            IsDirty = isDirty;
        }

        private void RaiseChanged(BiologicalHazardOperationResult result, bool restoring)
        {
            if (!suppressEvents)
            {
                HazardChanged?.Invoke(this, result, restoring);
            }
        }

        private void RaiseTicked(BiologicalHazardTickResult result, bool restoring)
        {
            if (!suppressEvents)
            {
                HazardTicked?.Invoke(this, result, restoring);
            }
        }

        private IDisposable SuppressEvents()
        {
            suppressEvents = true;
            return new Suppression(this);
        }

        private static string InstanceIdFor(string actorBodyId, string hazardDefinitionId)
        {
            return $"hazard.instance.{actorBodyId}.{hazardDefinitionId}";
        }

        private sealed class HazardInstanceRecord
        {
            private readonly Dictionary<string, HazardSourceRecord> sourcesById = new Dictionary<string, HazardSourceRecord>(StringComparer.Ordinal);
            private readonly Dictionary<string, HazardSuppressionRecord> suppressionsById = new Dictionary<string, HazardSuppressionRecord>(StringComparer.Ordinal);

            public HazardInstanceRecord(string instanceId, BiologicalHazardDefinition definition)
            {
                InstanceId = instanceId ?? string.Empty;
                Definition = definition;
            }

            public string InstanceId { get; }
            public BiologicalHazardDefinition Definition { get; }
            public string HazardDefinitionId => Definition == null ? string.Empty : Definition.Id;
            public float ElapsedSeconds { get; private set; }
            public long Revision { get; private set; }
            public IReadOnlyCollection<HazardSourceRecord> Sources => sourcesById.Values;
            public float EffectiveSourceMultiplier => CalculateSourceMultiplier();
            public float EffectiveSuppressionMultiplier => CalculateSuppressionMultiplier();
            public float EffectiveRatePerHour => (Definition == null ? 0f : Definition.BaseResourceRatePerHour) * EffectiveSourceMultiplier * EffectiveSuppressionMultiplier;

            public bool AddOrUpdateSource(BiologicalHazardSourceRequest request)
            {
                string key = request.SourceContributionId;
                HazardSourceRecord next = new HazardSourceRecord(key, request.SourceCategory, request.Severity, request.RateMultiplier, request.DurationSeconds, request.SourceObjectId, request.Reason);
                if (Definition.StackingPolicy == BiologicalHazardStackingPolicy.NonStacking && sourcesById.Count > 0 && !sourcesById.ContainsKey(key))
                {
                    return false;
                }

                if (Definition.StackingPolicy == BiologicalHazardStackingPolicy.ReplaceSameSource && sourcesById.TryGetValue(key, out HazardSourceRecord existing))
                {
                    if (existing.Equals(next))
                    {
                        return false;
                    }

                    sourcesById[key] = next;
                    Revision++;
                    return true;
                }

                if (sourcesById.TryGetValue(key, out HazardSourceRecord current) && current.Equals(next))
                {
                    return false;
                }

                sourcesById[key] = next;
                Revision++;
                return true;
            }

            public bool RemoveSource(string sourceContributionId)
            {
                if (!sourcesById.Remove(sourceContributionId ?? string.Empty))
                {
                    return false;
                }

                Revision++;
                return true;
            }

            public void AddOrUpdateSuppression(BiologicalHazardSuppressionRequest request)
            {
                suppressionsById[request.SourceContributionId] = new HazardSuppressionRecord(request.SourceContributionId, request.Mode, request.RateMultiplier, request.Reason);
                Revision++;
            }

            public bool RemoveSuppression(string sourceContributionId)
            {
                if (!suppressionsById.Remove(sourceContributionId ?? string.Empty))
                {
                    return false;
                }

                Revision++;
                return true;
            }

            public void Advance(float elapsedSeconds)
            {
                ElapsedSeconds += Mathf.Max(0f, elapsedSeconds);
                bool changed = false;
                foreach (HazardSourceRecord source in sourcesById.Values.ToArray())
                {
                    if (!source.Timed)
                    {
                        continue;
                    }

                    float remaining = Mathf.Max(0f, source.RemainingSeconds - elapsedSeconds);
                    if (remaining <= 0f)
                    {
                        sourcesById.Remove(source.SourceContributionId);
                    }
                    else
                    {
                        sourcesById[source.SourceContributionId] = source.WithRemaining(remaining);
                    }

                    changed = true;
                }

                if (changed)
                {
                    Revision++;
                }
            }

            public BiologicalHazardInstanceSaveData ToSaveData()
            {
                return new BiologicalHazardInstanceSaveData
                {
                    instanceId = InstanceId,
                    hazardDefinitionId = HazardDefinitionId,
                    severity = ResolveSeverity(),
                    elapsedSeconds = ElapsedSeconds,
                    revision = Revision,
                    sources = sourcesById.Values.OrderBy(source => source.SourceContributionId, StringComparer.Ordinal).Select(source => source.ToSaveData()).ToArray(),
                    suppressions = suppressionsById.Values.OrderBy(source => source.SourceContributionId, StringComparer.Ordinal).Select(source => source.ToSaveData()).ToArray()
                };
            }

            public static HazardInstanceRecord FromSaveData(BiologicalHazardInstanceSaveData saveData, BiologicalHazardDefinition definition)
            {
                if (saveData == null || definition == null)
                {
                    return null;
                }

                HazardInstanceRecord record = new HazardInstanceRecord(saveData.instanceId, definition)
                {
                    ElapsedSeconds = Mathf.Max(0f, saveData.elapsedSeconds),
                    Revision = Math.Max(1L, saveData.revision)
                };
                foreach (BiologicalHazardSourceSaveData source in saveData.sources ?? Array.Empty<BiologicalHazardSourceSaveData>())
                {
                    if (source != null && !string.IsNullOrWhiteSpace(source.sourceContributionId))
                    {
                        record.sourcesById[source.sourceContributionId] = HazardSourceRecord.FromSaveData(source);
                    }
                }

                foreach (BiologicalHazardSuppressionSaveData suppression in saveData.suppressions ?? Array.Empty<BiologicalHazardSuppressionSaveData>())
                {
                    if (suppression != null && !string.IsNullOrWhiteSpace(suppression.sourceContributionId))
                    {
                        record.suppressionsById[suppression.sourceContributionId] = HazardSuppressionRecord.FromSaveData(suppression);
                    }
                }

                return record;
            }

            public BiologicalHazardInstanceSnapshot CreateSnapshot()
            {
                return new BiologicalHazardInstanceSnapshot(
                    InstanceId,
                    HazardDefinitionId,
                    Definition == null ? string.Empty : Definition.DisplayName,
                    ResolveSeverity(),
                    Definition == null ? BiologicalHazardStackingPolicy.MergeSources : Definition.StackingPolicy,
                    EffectiveRatePerHour,
                    ElapsedSeconds,
                    sourcesById.Values.OrderBy(source => source.SourceContributionId, StringComparer.Ordinal).Select(source => source.CreateSnapshot()).ToArray(),
                    suppressionsById.Values.OrderBy(source => source.SourceContributionId, StringComparer.Ordinal).Select(source => source.CreateSnapshot()).ToArray(),
                    Revision);
            }

            private BiologicalHazardSeverity ResolveSeverity()
            {
                if (sourcesById.Count == 0)
                {
                    return Definition == null ? BiologicalHazardSeverity.Trace : Definition.DefaultSeverity;
                }

                return sourcesById.Values.Max(source => source.Severity);
            }

            private float CalculateSourceMultiplier()
            {
                if (sourcesById.Count == 0)
                {
                    return 0f;
                }

                switch (Definition.StackingPolicy)
                {
                    case BiologicalHazardStackingPolicy.AdditiveRate:
                    case BiologicalHazardStackingPolicy.Independent:
                    case BiologicalHazardStackingPolicy.MergeSources:
                        return sourcesById.Values.Sum(source => Mathf.Max(0f, source.RateMultiplier));
                    case BiologicalHazardStackingPolicy.StrongestSource:
                    case BiologicalHazardStackingPolicy.MaximumSeverity:
                    case BiologicalHazardStackingPolicy.RefreshDuration:
                    case BiologicalHazardStackingPolicy.ReplaceSameSource:
                    case BiologicalHazardStackingPolicy.NonStacking:
                    default:
                        return sourcesById.Values.Max(source => Mathf.Max(0f, source.RateMultiplier));
                }
            }

            private float CalculateSuppressionMultiplier()
            {
                float multiplier = 1f;
                foreach (HazardSuppressionRecord suppression in suppressionsById.Values.OrderBy(source => source.SourceContributionId, StringComparer.Ordinal))
                {
                    if (suppression.Mode == BiologicalHazardSuppressionMode.Remove)
                    {
                        return 0f;
                    }

                    if (suppression.Mode == BiologicalHazardSuppressionMode.Pause)
                    {
                        multiplier = 0f;
                    }
                    else
                    {
                        multiplier *= Mathf.Clamp01(suppression.RateMultiplier);
                    }
                }

                return Mathf.Clamp01(multiplier);
            }
        }

        private readonly struct HazardSourceRecord
        {
            public HazardSourceRecord(string sourceContributionId, BiologicalHazardSourceCategory sourceCategory, BiologicalHazardSeverity severity, float rateMultiplier, float remainingSeconds, string sourceObjectId, string reason)
            {
                SourceContributionId = sourceContributionId ?? string.Empty;
                SourceCategory = sourceCategory;
                Severity = severity;
                RateMultiplier = Mathf.Max(0f, rateMultiplier);
                RemainingSeconds = Mathf.Max(0f, remainingSeconds);
                SourceObjectId = sourceObjectId ?? string.Empty;
                Reason = reason ?? string.Empty;
            }

            public string SourceContributionId { get; }
            public BiologicalHazardSourceCategory SourceCategory { get; }
            public BiologicalHazardSeverity Severity { get; }
            public float RateMultiplier { get; }
            public float RemainingSeconds { get; }
            public string SourceObjectId { get; }
            public string Reason { get; }
            public bool Timed => RemainingSeconds > 0f;

            public HazardSourceRecord WithRemaining(float remainingSeconds)
            {
                return new HazardSourceRecord(SourceContributionId, SourceCategory, Severity, RateMultiplier, remainingSeconds, SourceObjectId, Reason);
            }

            public BiologicalHazardSourceSaveData ToSaveData()
            {
                return new BiologicalHazardSourceSaveData
                {
                    sourceContributionId = SourceContributionId,
                    sourceCategory = SourceCategory,
                    severity = Severity,
                    rateMultiplier = RateMultiplier,
                    remainingSeconds = RemainingSeconds,
                    sourceObjectId = SourceObjectId,
                    reason = Reason
                };
            }

            public static HazardSourceRecord FromSaveData(BiologicalHazardSourceSaveData saveData)
            {
                return new HazardSourceRecord(saveData.sourceContributionId, saveData.sourceCategory, saveData.severity, saveData.rateMultiplier, saveData.remainingSeconds, saveData.sourceObjectId, saveData.reason);
            }

            public BiologicalHazardSourceSnapshot CreateSnapshot()
            {
                return new BiologicalHazardSourceSnapshot(SourceContributionId, SourceCategory, Severity, RateMultiplier, RemainingSeconds, SourceObjectId, Reason);
            }
        }

        private readonly struct HazardSuppressionRecord
        {
            public HazardSuppressionRecord(string sourceContributionId, BiologicalHazardSuppressionMode mode, float rateMultiplier, string reason)
            {
                SourceContributionId = sourceContributionId ?? string.Empty;
                Mode = mode;
                RateMultiplier = Mathf.Max(0f, rateMultiplier);
                Reason = reason ?? string.Empty;
            }

            public string SourceContributionId { get; }
            public BiologicalHazardSuppressionMode Mode { get; }
            public float RateMultiplier { get; }
            public string Reason { get; }

            public BiologicalHazardSuppressionSaveData ToSaveData()
            {
                return new BiologicalHazardSuppressionSaveData
                {
                    sourceContributionId = SourceContributionId,
                    mode = Mode,
                    rateMultiplier = RateMultiplier,
                    reason = Reason
                };
            }

            public static HazardSuppressionRecord FromSaveData(BiologicalHazardSuppressionSaveData saveData)
            {
                return new HazardSuppressionRecord(saveData.sourceContributionId, saveData.mode, saveData.rateMultiplier, saveData.reason);
            }

            public BiologicalHazardSuppressionSnapshot CreateSnapshot()
            {
                return new BiologicalHazardSuppressionSnapshot(SourceContributionId, Mode, RateMultiplier, Reason);
            }
        }

        private sealed class Suppression : IDisposable
        {
            private readonly BiologicalHazardRuntime owner;

            public Suppression(BiologicalHazardRuntime owner)
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
    }
}
