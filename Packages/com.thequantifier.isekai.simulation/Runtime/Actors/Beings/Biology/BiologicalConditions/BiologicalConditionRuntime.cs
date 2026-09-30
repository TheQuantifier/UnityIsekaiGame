using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.Recovery;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.BiologicalConditions
{
    public sealed class BiologicalConditionRuntime : IDisposable
    {
        private const int MaximumRememberedTransactions = 128;

        private readonly Dictionary<string, BiologicalConditionDefinition> definitionsById = new Dictionary<string, BiologicalConditionDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, BiologicalConditionTreatmentDefinition> treatmentsById = new Dictionary<string, BiologicalConditionTreatmentDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, BiologicalTransmissionProfileDefinition> transmissionsById = new Dictionary<string, BiologicalTransmissionProfileDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConditionInstanceState> instancesById = new Dictionary<string, ConditionInstanceState>(StringComparer.Ordinal);
        private readonly Dictionary<string, ImmunityMemoryState> immunityById = new Dictionary<string, ImmunityMemoryState>(StringComparer.Ordinal);
        private readonly List<string> processedTransactionIds = new List<string>();

        private DefinitionRegistry registry;
        private string actorBodyId;
        private long bodyRevision;
        private long anatomyRevision;
        private long conditionRevision;
        private long vitalRevision;
        private long hazardRevision;
        private long compatibilityRevision;

        public BiologicalConditionReadinessState Readiness { get; private set; } = BiologicalConditionReadinessState.Uninitialized;
        public long BiologicalConditionRevision { get; private set; }
        public bool IsDirty { get; private set; }
        public string ActorBodyId => actorBodyId ?? string.Empty;
        public bool IsReady => Readiness == BiologicalConditionReadinessState.Ready;

        public BiologicalConditionResult BuildForBody(BodySnapshot body, DefinitionRegistry definitionRegistry, bool restoring = false, bool preserveRevision = true)
        {
            registry = definitionRegistry ?? registry;
            RefreshDefinitions();
            if (body == null || string.IsNullOrWhiteSpace(body.ActorBodyId))
            {
                Readiness = BiologicalConditionReadinessState.WaitingForBody;
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.MissingBody, "Biological Conditions require an exact body snapshot.", CreateSnapshot());
            }

            actorBodyId = body.ActorBodyId;
            bodyRevision = body.BodyRevision;
            anatomyRevision = body.Anatomy == null ? 0L : body.Anatomy.AnatomyRevision;
            conditionRevision = body.Condition == null ? 0L : body.Condition.ConditionRevision;
            vitalRevision = body.VitalProcesses == null ? 0L : body.VitalProcesses.VitalRevision;
            hazardRevision = body.BiologicalHazards == null ? 0L : body.BiologicalHazards.HazardRevision;
            compatibilityRevision = body.BiologicalCompatibility == null ? 0L : body.BiologicalCompatibility.CompatibilityRevision;
            if (definitionsById.Count == 0)
            {
                Readiness = BiologicalConditionReadinessState.WaitingForDefinitions;
                return BiologicalConditionResult.Success("No Biological Condition definitions are registered; runtime is waiting for condition content.", string.Empty, 0f, null, CreateSnapshot());
            }

            if (body.BiologicalCompatibility == null)
            {
                Readiness = BiologicalConditionReadinessState.WaitingForCompatibility;
                return BiologicalConditionResult.Success("Biological Conditions are waiting for a compatibility snapshot.", string.Empty, 0f, null, CreateSnapshot());
            }

            if (!preserveRevision)
            {
                BiologicalConditionRevision++;
            }

            Readiness = BiologicalConditionReadinessState.Ready;
            IsDirty = !restoring && IsDirty;
            return BiologicalConditionResult.Success("Biological Conditions initialized.", string.Empty, 0f, null, CreateSnapshot());
        }

        public BiologicalConditionResult PreviewExposure(BiologicalConditionExposureRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility)
        {
            return ResolveExposure(request?.AsPreview(), body, compatibility, mutate: false);
        }

        public BiologicalConditionResult ApplyExposure(BiologicalConditionExposureRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility)
        {
            return ResolveExposure(request, body, compatibility, mutate: true);
        }

        public BiologicalConditionTickResult PreviewTick(BiologicalConditionTickRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility)
        {
            return ResolveTick(request, body, compatibility, mutate: false);
        }

        public BiologicalConditionTickResult ApplyTick(BiologicalConditionTickRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility)
        {
            return ResolveTick(request, body, compatibility, mutate: true);
        }

        public BiologicalConditionConsequenceExecutionResult PreviewTickConsequences(BiologicalConditionConsequenceExecutionRequest request)
        {
            return ResolveTickConsequences(request?.AsPreview(), commit: false);
        }

        public BiologicalConditionConsequenceExecutionResult ApplyTickConsequences(BiologicalConditionConsequenceExecutionRequest request)
        {
            return ResolveTickConsequences(request, commit: true);
        }

        public BiologicalConditionResult ApplyTreatment(BiologicalConditionTreatmentRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility)
        {
            return ResolveTreatment(request, body, compatibility, mutate: !request.Preview);
        }

        public BiologicalConditionResult PreviewTreatment(BiologicalConditionTreatmentRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility)
        {
            return ResolveTreatment(new BiologicalConditionTreatmentRequest(request.ActorBodyId, request.InstanceId, request.TreatmentDefinitionId, request.TransactionId, request.Dose, preview: true, request.SourceId), body, compatibility, mutate: false);
        }

        public BiologicalConditionTransmissionPlan PreviewTransmission(BiologicalConditionTransmissionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.InstanceId) || !instancesById.TryGetValue(request.InstanceId, out ConditionInstanceState instance))
            {
                return new BiologicalConditionTransmissionPlan(string.Empty, string.Empty, string.Empty, null, "No active condition instance can transmit.");
            }

            BiologicalTransmissionProfileDefinition profile = transmissionsById.Values.FirstOrDefault(candidate => candidate.ConditionDefinitionId == instance.ConditionDefinitionId && candidate.AlphaEnabled);
            if (profile == null)
            {
                return new BiologicalConditionTransmissionPlan(string.Empty, instance.ActorBodyId, request.TargetActorBodyId, null, "No transmission profile is authored for the condition.");
            }

            BiologicalConditionExposureRequest exposure = new BiologicalConditionExposureRequest(
                request.TargetActorBodyId,
                instance.ConditionDefinitionId,
                request.TransactionId,
                profile.ExposureRoute,
                profile.TransferredDose,
                instance.StrainId,
                sourceId: instance.InstanceId,
                sourceBodyId: instance.ActorBodyId,
                sourceEventId: request.TransactionId,
                sourceCategory: BiologicalConditionSourceCategory.Transmission,
                preview: true,
                authority: "authoritative-character-simulation");
            return new BiologicalConditionTransmissionPlan(profile.Id, instance.ActorBodyId, request.TargetActorBodyId, exposure, "Transmission produced an exposure plan only.");
        }

        public BiologicalConditionResult ClearInstance(string instanceId, string transactionId, bool restoring = false)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || !instancesById.TryGetValue(instanceId, out ConditionInstanceState instance))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.MissingInstance, "No active Biological Condition instance exists.", CreateSnapshot());
            }

            if (!string.IsNullOrWhiteSpace(transactionId) && processedTransactionIds.Contains(transactionId, StringComparer.Ordinal))
            {
                return BiologicalConditionResult.Success("Duplicate condition clear ignored.", instanceId, 0f, null, CreateSnapshot(), duplicate: true);
            }

            instance.Stage = BiologicalConditionStage.Cleared;
            instance.Load = 0f;
            instance.Revision++;
            if (definitionsById.TryGetValue(instance.ConditionDefinitionId, out BiologicalConditionDefinition definition) && definition.GrantsImmunityMemoryOnClear)
            {
                AddImmunityMemory(instance, 1f);
            }

            RememberTransaction(transactionId);
            BiologicalConditionRevision++;
            IsDirty = !restoring;
            return BiologicalConditionResult.Success("Biological Condition cleared.", instanceId, 0f, null, CreateSnapshot());
        }

        public BiologicalConditionResult ReconcileForCurrentBody(BodySnapshot body, BiologicalCompatibilityRuntime compatibility, bool bodyReplacement, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.RuntimeNotReady, "Biological Condition runtime is not Ready.", CreateSnapshot());
            }

            List<string> removed = new List<string>();
            foreach (ConditionInstanceState instance in instancesById.Values.ToArray())
            {
                if (!definitionsById.TryGetValue(instance.ConditionDefinitionId, out BiologicalConditionDefinition definition))
                {
                    removed.Add(instance.InstanceId);
                    instancesById.Remove(instance.InstanceId);
                    continue;
                }

                BiologicalConditionReconciliationPolicy policy = bodyReplacement ? definition.BodyReplacementPolicy : definition.TransformationPolicy;
                if (policy == BiologicalConditionReconciliationPolicy.Clear)
                {
                    removed.Add(instance.InstanceId);
                    instancesById.Remove(instance.InstanceId);
                    continue;
                }

                if (policy == BiologicalConditionReconciliationPolicy.PreserveIfCompatible)
                {
                    BiologicalInteractionEvaluationResult evaluated = EvaluateCompatibility(definition, body, compatibility, instance.TargetAnatomyNodeId, instance.SourceId, $"reconcile.{instance.InstanceId}", preview: true);
                    if (evaluated == null || !evaluated.Compatible)
                    {
                        removed.Add(instance.InstanceId);
                        instancesById.Remove(instance.InstanceId);
                    }
                }
            }

            if (removed.Count > 0)
            {
                BiologicalConditionRevision++;
                IsDirty = !restoring;
            }

            return BiologicalConditionResult.Success($"Biological Condition reconciliation removed {removed.Count} instance(s).", string.Empty, 0f, null, CreateSnapshot(), duplicate: removed.Count == 0);
        }

        public BiologicalConditionSaveData CreateSaveData()
        {
            return new BiologicalConditionSaveData
            {
                schemaVersion = BiologicalConditionSaveData.CurrentSchemaVersion,
                actorBodyId = ActorBodyId,
                biologicalConditionRevision = BiologicalConditionRevision,
                instances = instancesById.Values.OrderBy(instance => instance.InstanceId, StringComparer.Ordinal).Select(instance => instance.ToSaveData()).ToArray(),
                immunityMemory = immunityById.Values.OrderBy(memory => memory.MemoryId, StringComparer.Ordinal).Select(memory => memory.ToSaveData()).ToArray(),
                processedTransactionIds = processedTransactionIds.ToArray()
            };
        }

        public BiologicalConditionResult RestoreFromSaveData(BiologicalConditionSaveData saveData, BodySnapshot body, DefinitionRegistry definitionRegistry)
        {
            if (!ValidateSaveData(saveData, body, definitionRegistry, out string failureReason))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.RestoreFailed, failureReason, CreateSnapshot());
            }

            BiologicalConditionSaveData rollback = CreateSaveData();
            try
            {
                registry = definitionRegistry ?? registry;
                RefreshDefinitions();
                Readiness = BiologicalConditionReadinessState.Restoring;
                actorBodyId = saveData.actorBodyId;
                bodyRevision = body.BodyRevision;
                anatomyRevision = body.Anatomy == null ? 0L : body.Anatomy.AnatomyRevision;
                conditionRevision = body.Condition == null ? 0L : body.Condition.ConditionRevision;
                vitalRevision = body.VitalProcesses == null ? 0L : body.VitalProcesses.VitalRevision;
                hazardRevision = body.BiologicalHazards == null ? 0L : body.BiologicalHazards.HazardRevision;
                compatibilityRevision = body.BiologicalCompatibility == null ? 0L : body.BiologicalCompatibility.CompatibilityRevision;
                BiologicalConditionRevision = Math.Max(0L, saveData.biologicalConditionRevision);
                instancesById.Clear();
                foreach (BiologicalConditionInstanceSaveData entry in saveData.instances ?? Array.Empty<BiologicalConditionInstanceSaveData>())
                {
                    ConditionInstanceState instance = ConditionInstanceState.FromSaveData(entry);
                    if (definitionsById.TryGetValue(instance.ConditionDefinitionId, out BiologicalConditionDefinition definition))
                    {
                        instance.Family = definition.Family;
                    }

                    instancesById[instance.InstanceId] = instance;
                }

                immunityById.Clear();
                foreach (BiologicalConditionImmunityMemorySaveData entry in saveData.immunityMemory ?? Array.Empty<BiologicalConditionImmunityMemorySaveData>())
                {
                    ImmunityMemoryState memory = ImmunityMemoryState.FromSaveData(entry);
                    immunityById[memory.MemoryId] = memory;
                }

                processedTransactionIds.Clear();
                foreach (string transactionId in saveData.processedTransactionIds ?? Array.Empty<string>())
                {
                    RememberTransaction(transactionId);
                }

                Readiness = BiologicalConditionReadinessState.Ready;
                IsDirty = false;
                return BiologicalConditionResult.Success("Biological Conditions restored without replay.", string.Empty, 0f, null, CreateSnapshot());
            }
            catch (Exception exception)
            {
                RestoreRaw(rollback);
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.RestoreFailed, $"Biological Condition restore rolled back: {exception.Message}", CreateSnapshot());
            }
        }

        public static bool ValidateSaveData(BiologicalConditionSaveData saveData, BodySnapshot body, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Biological Condition save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > BiologicalConditionSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Biological Condition schema version {saveData.schemaVersion}.";
                return false;
            }

            if (body == null || string.IsNullOrWhiteSpace(body.ActorBodyId))
            {
                failureReason = "Biological Condition restore requires a body snapshot.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(saveData.actorBodyId) && !string.Equals(saveData.actorBodyId, body.ActorBodyId, StringComparison.Ordinal))
            {
                failureReason = $"Saved Biological Condition body '{saveData.actorBodyId}' does not match runtime body '{body.ActorBodyId}'.";
                return false;
            }

            HashSet<string> instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BiologicalConditionInstanceSaveData entry in saveData.instances ?? Array.Empty<BiologicalConditionInstanceSaveData>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.instanceId))
                {
                    failureReason = "Biological Condition save contains an instance without a stable ID.";
                    return false;
                }

                if (!instanceIds.Add(entry.instanceId))
                {
                    failureReason = $"Duplicate Biological Condition instance ID '{entry.instanceId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(entry.conditionDefinitionId, out BiologicalConditionDefinition definition) || definition == null)
                {
                    failureReason = $"Saved Biological Condition references missing definition '{entry.conditionDefinitionId}'.";
                    return false;
                }
            }

            return true;
        }

        public BiologicalConditionRuntimeSnapshot CreateSnapshot()
        {
            List<string> diagnostics = new List<string>();
            bool coherent = true;
            if (Readiness == BiologicalConditionReadinessState.Ready && string.IsNullOrWhiteSpace(ActorBodyId))
            {
                coherent = false;
                diagnostics.Add("Biological Condition runtime is Ready without an Actor/body ID.");
            }

            BiologicalConditionInstanceSnapshot[] instances = instancesById.Values
                .OrderBy(instance => instance.InstanceId, StringComparer.Ordinal)
                .Select(ToSnapshot)
                .ToArray();
            BiologicalConditionImmunityMemorySnapshot[] memory = immunityById.Values
                .OrderBy(entry => entry.MemoryId, StringComparer.Ordinal)
                .Select(entry => new BiologicalConditionImmunityMemorySnapshot(entry.MemoryId, entry.ActorBodyId, entry.ConditionDefinitionId, entry.StrainId, entry.Strength, entry.SourceInstanceId, entry.Revision))
                .ToArray();

            return new BiologicalConditionRuntimeSnapshot(ActorBodyId, Readiness, bodyRevision, anatomyRevision, conditionRevision, vitalRevision, hazardRevision, compatibilityRevision, BiologicalConditionRevision, instances, memory, processedTransactionIds, IsDirty, coherent, diagnostics);
        }

        public void Dispose()
        {
            Readiness = BiologicalConditionReadinessState.Disposed;
            definitionsById.Clear();
            treatmentsById.Clear();
            transmissionsById.Clear();
            instancesById.Clear();
            immunityById.Clear();
            processedTransactionIds.Clear();
        }

        private BiologicalConditionResult ResolveExposure(BiologicalConditionExposureRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility, bool mutate)
        {
            if (!IsReady)
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.RuntimeNotReady, "Biological Condition runtime is not Ready.", CreateSnapshot());
            }

            BiologicalConditionResultCode validation = ValidateBodyContext(request?.ActorBodyId, body, out string failure);
            if (validation != BiologicalConditionResultCode.Success)
            {
                return BiologicalConditionResult.Failure(validation, failure, CreateSnapshot());
            }

            if (request == null || string.IsNullOrWhiteSpace(request.ConditionDefinitionId) || string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.InvalidRequest, "Biological Condition exposure requires a definition ID and transaction ID.", CreateSnapshot());
            }

            if (mutate && processedTransactionIds.Contains(request.TransactionId, StringComparer.Ordinal))
            {
                string duplicateInstanceId = FindInstanceKey(request);
                return BiologicalConditionResult.Success("Duplicate Biological Condition exposure ignored.", duplicateInstanceId, 0f, null, CreateSnapshot(), duplicate: true);
            }

            if (!definitionsById.TryGetValue(request.ConditionDefinitionId, out BiologicalConditionDefinition definition))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.MissingDefinition, $"Biological Condition '{request.ConditionDefinitionId}' is not registered.", CreateSnapshot());
            }

            if (request.Dose <= 0f)
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.InvalidDose, "Biological Condition exposure requires a positive dose.", CreateSnapshot());
            }

            if (!definition.AllowsRoute(request.Route))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.InvalidRoute, $"Route '{request.Route}' is not allowed for '{definition.Id}'.", CreateSnapshot());
            }

            AnatomyNodeSnapshot targetNode = ResolveTargetNode(definition, request.TargetAnatomyNodeId, body, out BiologicalConditionResultCode nodeCode, out string nodeFailure);
            if (nodeCode != BiologicalConditionResultCode.Success)
            {
                return BiologicalConditionResult.Failure(nodeCode, nodeFailure, CreateSnapshot());
            }

            if (definition.RequiresActiveInjury && !HasRequiredInjury(body.Condition, targetNode))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.MissingRequiredInjury, $"Biological Condition '{definition.Id}' requires an active compatible injury.", CreateSnapshot());
            }

            BiologicalInteractionEvaluationResult evaluated = EvaluateCompatibility(definition, body, compatibility, targetNode?.NodeId ?? string.Empty, request.SourceId, request.TransactionId, request.Preview || !mutate);
            BiologicalConditionResultCode compatibilityCode = TranslateCompatibility(evaluated);
            if (compatibilityCode != BiologicalConditionResultCode.Success)
            {
                return BiologicalConditionResult.Failure(compatibilityCode, evaluated == null ? "Compatibility evaluation failed." : evaluated.Message, CreateSnapshot(), evaluated);
            }

            BiologicalConditionResultCode staleCode = ValidateExpectedRevisions(request, body, evaluated, out string staleFailure);
            if (staleCode != BiologicalConditionResultCode.Success)
            {
                return BiologicalConditionResult.Failure(staleCode, staleFailure, CreateSnapshot(), evaluated);
            }

            float memoryMultiplier = ResolveImmunityMemoryMultiplier(definition.Id, string.IsNullOrWhiteSpace(request.StrainId) ? definition.StrainId : request.StrainId);
            float effectiveDose = Mathf.Min(evaluated.MaximumSeverity, request.Dose * Mathf.Max(0f, request.Intensity) * evaluated.RateMultiplier * evaluated.SeverityMultiplier * memoryMultiplier);
            string instanceId = FindInstanceKey(request, definition);
            ConditionInstanceState current = instancesById.TryGetValue(instanceId, out ConditionInstanceState existing) ? existing.Clone() : CreateInstance(instanceId, request, definition, body, effectiveDose);
            ApplyDose(current, definition, effectiveDose);

            if (mutate)
            {
                instancesById[instanceId] = current;
                RememberTransaction(request.TransactionId);
                BiologicalConditionRevision++;
                IsDirty = true;
            }

            return BiologicalConditionResult.Success(mutate ? "Biological Condition exposure applied." : "Biological Condition exposure preview resolved.", instanceId, effectiveDose, evaluated, mutate ? CreateSnapshot() : SnapshotWith(instanceId, current), preview: !mutate || request.Preview);
        }

        private BiologicalConditionTickResult ResolveTick(BiologicalConditionTickRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility, bool mutate)
        {
            BiologicalConditionResultCode validation = ValidateBodyContext(request.ActorBodyId, body, out string failure);
            if (!IsReady || validation != BiologicalConditionResultCode.Success)
            {
                return BiologicalConditionTickResult.Failure(request, !IsReady ? BiologicalConditionResultCode.RuntimeNotReady : validation, !IsReady ? "Biological Condition runtime is not Ready." : failure, CreateSnapshot());
            }

            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return BiologicalConditionTickResult.Failure(request, BiologicalConditionResultCode.InvalidRequest, "Biological Condition tick requires a transaction ID.", CreateSnapshot());
            }

            if (mutate && processedTransactionIds.Contains(request.TransactionId, StringComparer.Ordinal))
            {
                return BiologicalConditionTickResult.Success(request, Array.Empty<BiologicalConditionConsequencePlanSnapshot>(), CreateSnapshot(), duplicate: true);
            }

            List<BiologicalConditionConsequencePlanSnapshot> consequences = new List<BiologicalConditionConsequencePlanSnapshot>();
            Dictionary<string, ConditionInstanceState> next = instancesById.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
            foreach (ConditionInstanceState instance in next.Values.Where(instance => instance.Active).OrderBy(instance => instance.InstanceId, StringComparer.Ordinal))
            {
                if (!definitionsById.TryGetValue(instance.ConditionDefinitionId, out BiologicalConditionDefinition definition))
                {
                    continue;
                }

                BiologicalInteractionEvaluationResult evaluated = EvaluateCompatibility(definition, body, compatibility, instance.TargetAnatomyNodeId, instance.SourceId, request.TransactionId, request.Preview || !mutate);
                BiologicalConditionResultCode code = TranslateCompatibility(evaluated);
                if (code == BiologicalConditionResultCode.Suppressed)
                {
                    instance.Suppressed = true;
                    instance.Stage = BiologicalConditionStage.Suppressed;
                    instance.Revision++;
                    continue;
                }

                if (code != BiologicalConditionResultCode.Success)
                {
                    continue;
                }

                instance.Suppressed = false;
                float delta = request.ElapsedGameSeconds * definition.BaseProgressionRate * evaluated.RateMultiplier / 60f;
                instance.ProgressionProgress += delta;
                instance.Load = Mathf.Max(0f, Mathf.Min(evaluated.MaximumSeverity, instance.Load + delta - definition.ImmuneClearanceRate * ResolveImmunityStrength(definition.Id, instance.StrainId)));
                ApplyStageFromLoad(instance, definition);
                instance.LastTickTransactionId = request.TransactionId;
                instance.Revision++;
                consequences.Add(BuildConsequencePlan(definition, instance, evaluated.ConsequenceMultiplier));
                if (instance.Stage == BiologicalConditionStage.Cleared && definition.GrantsImmunityMemoryOnClear)
                {
                    AddImmunityMemory(instance, 1f);
                }
            }

            if (mutate)
            {
                instancesById.Clear();
                foreach (KeyValuePair<string, ConditionInstanceState> pair in next)
                {
                    instancesById[pair.Key] = pair.Value;
                }

                RememberTransaction(request.TransactionId);
                BiologicalConditionRevision++;
                IsDirty = true;
            }

            return BiologicalConditionTickResult.Success(request, consequences, mutate ? CreateSnapshot() : SnapshotWith(next), duplicate: false);
        }

        private BiologicalConditionConsequenceExecutionResult ResolveTickConsequences(BiologicalConditionConsequenceExecutionRequest request, bool commit)
        {
            if (request == null)
            {
                return BiologicalConditionConsequenceExecutionResult.Failure(BiologicalConditionResultCode.InvalidRequest, "Biological Condition consequence execution requires a request.", preview: !commit);
            }

            BiologicalConditionRuntimeSnapshot beforeSnapshot = CreateSnapshot();
            BiologicalConditionTickRequest previewTickRequest = new BiologicalConditionTickRequest(request.Tick.ActorBodyId, request.Tick.ElapsedGameSeconds, request.Tick.TransactionId, preview: true, request.Tick.Reason);
            BiologicalConditionTickResult previewTick = ResolveTick(previewTickRequest, request.Body, request.Compatibility, mutate: false);
            if (!previewTick.Succeeded)
            {
                return BiologicalConditionConsequenceExecutionResult.Failure(previewTick.Code, previewTick.Message, preview: true, conditionTick: previewTick);
            }

            List<VitalResourceMutationResult> vitalResults = new List<VitalResourceMutationResult>();
            List<BiologicalHazardOperationResult> hazardResults = new List<BiologicalHazardOperationResult>();
            List<BiologicalRecoveryResult> recoveryResults = new List<BiologicalRecoveryResult>();
            List<DamageApplicationResult> damageResults = new List<DamageApplicationResult>();
            if (!PreviewOwnerConsequences(request, previewTick, beforeSnapshot, vitalResults, hazardResults, recoveryResults, damageResults, out string ownerFailure))
            {
                return BiologicalConditionConsequenceExecutionResult.Failure(BiologicalConditionResultCode.InvalidRequest, ownerFailure, preview: true, previewTick, vitalResults, hazardResults, recoveryResults, damageResults);
            }

            if (!commit)
            {
                return BiologicalConditionConsequenceExecutionResult.Success("Biological Condition consequences previewed through owning systems.", preview: true, duplicate: false, previewTick, vitalResults, hazardResults, recoveryResults, damageResults);
            }

            BiologicalConditionSaveData conditionRollback = CreateSaveData();
            VitalProcessSaveData vitalRollback = request.VitalProcesses == null ? null : request.VitalProcesses.CreateSaveData();
            BiologicalHazardSaveData hazardRollback = request.Hazards == null ? null : request.Hazards.CreateSaveData();
            BiologicalRecoverySaveData recoveryRollback = request.Recovery == null ? null : request.Recovery.CreateSaveData();

            vitalResults.Clear();
            hazardResults.Clear();
            recoveryResults.Clear();
            damageResults.Clear();

            if (!CommitOwnerConsequences(request, previewTick, beforeSnapshot, vitalResults, hazardResults, recoveryResults, damageResults, out ownerFailure))
            {
                RollbackConsequences(conditionRollback, vitalRollback, hazardRollback, recoveryRollback, request);
                return BiologicalConditionConsequenceExecutionResult.Failure(BiologicalConditionResultCode.InvalidRequest, ownerFailure, preview: false, previewTick, vitalResults, hazardResults, recoveryResults, damageResults);
            }

            BiologicalConditionTickResult committedTick = ResolveTick(new BiologicalConditionTickRequest(request.Tick.ActorBodyId, request.Tick.ElapsedGameSeconds, request.Tick.TransactionId, preview: false, request.Tick.Reason), request.Body, request.Compatibility, mutate: true);
            if (!committedTick.Succeeded && !committedTick.Duplicate)
            {
                RollbackConsequences(conditionRollback, vitalRollback, hazardRollback, recoveryRollback, request);
                return BiologicalConditionConsequenceExecutionResult.Failure(committedTick.Code, committedTick.Message, preview: false, committedTick, vitalResults, hazardResults, recoveryResults, damageResults);
            }

            return BiologicalConditionConsequenceExecutionResult.Success("Biological Condition tick and consequences committed through owning systems.", preview: false, duplicate: committedTick.Duplicate, committedTick, vitalResults, hazardResults, recoveryResults, damageResults);
        }

        private bool PreviewOwnerConsequences(
            BiologicalConditionConsequenceExecutionRequest request,
            BiologicalConditionTickResult previewTick,
            BiologicalConditionRuntimeSnapshot beforeSnapshot,
            List<VitalResourceMutationResult> vitalResults,
            List<BiologicalHazardOperationResult> hazardResults,
            List<BiologicalRecoveryResult> recoveryResults,
            List<DamageApplicationResult> damageResults,
            out string failure)
        {
            return ExecuteOwnerConsequences(request, previewTick, beforeSnapshot, mutate: false, vitalResults, hazardResults, recoveryResults, damageResults, out failure);
        }

        private bool CommitOwnerConsequences(
            BiologicalConditionConsequenceExecutionRequest request,
            BiologicalConditionTickResult previewTick,
            BiologicalConditionRuntimeSnapshot beforeSnapshot,
            List<VitalResourceMutationResult> vitalResults,
            List<BiologicalHazardOperationResult> hazardResults,
            List<BiologicalRecoveryResult> recoveryResults,
            List<DamageApplicationResult> damageResults,
            out string failure)
        {
            return ExecuteOwnerConsequences(request, previewTick, beforeSnapshot, mutate: true, vitalResults, hazardResults, recoveryResults, damageResults, out failure);
        }

        private bool ExecuteOwnerConsequences(
            BiologicalConditionConsequenceExecutionRequest request,
            BiologicalConditionTickResult previewTick,
            BiologicalConditionRuntimeSnapshot beforeSnapshot,
            bool mutate,
            List<VitalResourceMutationResult> vitalResults,
            List<BiologicalHazardOperationResult> hazardResults,
            List<BiologicalRecoveryResult> recoveryResults,
            List<DamageApplicationResult> damageResults,
            out string failure)
        {
            failure = string.Empty;
            Dictionary<string, BiologicalConditionInstanceSnapshot> nextActive = previewTick.Snapshot.ActiveInstances.ToDictionary(instance => instance.InstanceId, StringComparer.Ordinal);
            foreach (BiologicalConditionInstanceSnapshot previous in beforeSnapshot.ActiveInstances)
            {
                if (nextActive.ContainsKey(previous.InstanceId))
                {
                    continue;
                }

                if (!RemoveConditionOwnedConsequences(request, previous, mutate, hazardResults, recoveryResults, out failure))
                {
                    return false;
                }
            }

            foreach (BiologicalConditionInstanceSnapshot instance in nextActive.Values.OrderBy(instance => instance.InstanceId, StringComparer.Ordinal))
            {
                if (!definitionsById.TryGetValue(instance.ConditionDefinitionId, out BiologicalConditionDefinition definition))
                {
                    continue;
                }

                BiologicalConditionConsequencePlanSnapshot plan = instance.ConsequencePlan;
                if (plan == null || plan.Flags == BiologicalConditionConsequenceFlags.None)
                {
                    continue;
                }

                if (!ExecuteVitalConsequence(request, instance, plan, mutate, vitalResults, out failure)
                    || !ExecuteHazardConsequence(request, instance, plan, mutate, hazardResults, out failure)
                    || !ExecuteRecoveryConsequence(request, instance, plan, mutate, recoveryResults, out failure)
                    || !ExecuteDamageConsequence(request, instance, definition, plan, mutate, damageResults, out failure))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ExecuteVitalConsequence(BiologicalConditionConsequenceExecutionRequest request, BiologicalConditionInstanceSnapshot instance, BiologicalConditionConsequencePlanSnapshot plan, bool mutate, List<VitalResourceMutationResult> results, out string failure)
        {
            failure = string.Empty;
            if (!plan.Flags.HasFlag(BiologicalConditionConsequenceFlags.VitalPressure) || string.IsNullOrWhiteSpace(plan.VitalResourceId) || plan.VitalPressureAmount <= 0f)
            {
                return true;
            }

            if (request.VitalProcesses == null)
            {
                failure = "Biological Condition vital consequence requires a VitalProcessRuntime.";
                return false;
            }

            if (!request.VitalProcesses.TryGetResource(plan.VitalResourceId, out VitalResourceSnapshot resource) || resource == null || !resource.Active)
            {
                failure = $"Vital resource '{plan.VitalResourceId}' is missing or inactive.";
                return false;
            }

            float scale = Math.Max(1f, request.Tick.ElapsedGameSeconds / 60f);
            VitalResourceMutationOperation operation = resource.ModelType == BiologicalResourceModelType.TargetCenteredValue
                ? VitalResourceMutationOperation.Adjust
                : VitalResourceMutationOperation.Consume;
            VitalResourceMutationRequest mutation = new VitalResourceMutationRequest(
                request.Body.ActorBodyId,
                plan.VitalResourceId,
                operation,
                plan.VitalPressureAmount * scale,
                $"{request.Tick.TransactionId}.{Sanitize(instance.InstanceId)}.{Sanitize(plan.VitalResourceId)}.vital",
                instance.InstanceId,
                "Biological Condition consequence",
                request.Body.BodyRevision,
                request.Body.Anatomy?.AnatomyRevision ?? 0L,
                request.Body.Condition?.ConditionRevision ?? 0L);
            VitalResourceMutationResult result = mutate
                ? request.VitalProcesses.ApplyMutation(mutation, request.Body.Anatomy, request.Body.Condition, request.Restoring)
                : request.VitalProcesses.PreviewMutation(mutation, request.Body.Anatomy, request.Body.Condition);
            results.Add(result);
            if (!result.Succeeded && !result.Duplicate)
            {
                failure = result.Message;
                return false;
            }

            return true;
        }

        private bool ExecuteHazardConsequence(BiologicalConditionConsequenceExecutionRequest request, BiologicalConditionInstanceSnapshot instance, BiologicalConditionConsequencePlanSnapshot plan, bool mutate, List<BiologicalHazardOperationResult> results, out string failure)
        {
            failure = string.Empty;
            if (!plan.Flags.HasFlag(BiologicalConditionConsequenceFlags.HazardRequest) || string.IsNullOrWhiteSpace(plan.HazardDefinitionId))
            {
                return true;
            }

            if (request.Hazards == null || request.VitalProcesses == null)
            {
                failure = "Biological Condition hazard consequence requires Hazard and Vital runtimes.";
                return false;
            }

            BiologicalHazardSourceRequest source = new BiologicalHazardSourceRequest(
                request.Body.ActorBodyId,
                plan.HazardDefinitionId,
                HazardSourceId(instance, plan.HazardDefinitionId),
                BiologicalHazardSourceCategory.Condition,
                ToHazardSeverity(instance.Severity),
                rateMultiplier: Math.Max(0f, plan.HazardRateMultiplier),
                sourceObjectId: instance.InstanceId,
                reason: "Biological Condition consequence");
            BiologicalHazardOperationResult result = mutate
                ? request.Hazards.AddOrUpdateSource(source, request.VitalProcesses, request.Body.Anatomy, request.Body.Condition, request.Compatibility, request.Body, request.Restoring)
                : request.Hazards.PreviewAddOrUpdateSource(source, request.VitalProcesses, request.Body.Anatomy, request.Body.Condition, request.Compatibility, request.Body);
            results.Add(result);
            if (!result.Succeeded && !result.Duplicate)
            {
                failure = result.Message;
                return false;
            }

            return true;
        }

        private bool ExecuteRecoveryConsequence(BiologicalConditionConsequenceExecutionRequest request, BiologicalConditionInstanceSnapshot instance, BiologicalConditionConsequencePlanSnapshot plan, bool mutate, List<BiologicalRecoveryResult> results, out string failure)
        {
            failure = string.Empty;
            if (!plan.Flags.HasFlag(BiologicalConditionConsequenceFlags.RecoveryModifier) || Math.Abs(plan.RecoveryRateMultiplier - 1f) <= 0.0001f)
            {
                return true;
            }

            if (request.Recovery == null)
            {
                failure = "Biological Condition recovery consequence requires a BiologicalRecoveryRuntime.";
                return false;
            }

            RecoveryRateModifierRequest modifier = new RecoveryRateModifierRequest(request.Body.ActorBodyId, RecoverySourceId(instance), Math.Max(0f, plan.RecoveryRateMultiplier), $"{request.Tick.TransactionId}.{Sanitize(instance.InstanceId)}.recovery", "Biological Condition recovery modifier");
            BiologicalRecoveryResult result = mutate
                ? request.Recovery.AddOrUpdateRateModifier(modifier, request.Restoring)
                : request.Recovery.PreviewRateModifier(modifier);
            results.Add(result);
            if (!result.Succeeded && !result.Duplicate)
            {
                failure = result.Message;
                return false;
            }

            return true;
        }

        private bool ExecuteDamageConsequence(BiologicalConditionConsequenceExecutionRequest request, BiologicalConditionInstanceSnapshot instance, BiologicalConditionDefinition definition, BiologicalConditionConsequencePlanSnapshot plan, bool mutate, List<DamageApplicationResult> results, out string failure)
        {
            failure = string.Empty;
            if (!plan.Flags.HasFlag(BiologicalConditionConsequenceFlags.Step6DamagePlan) || definition.DamageType == null || plan.Step6DamageAmount <= 0f)
            {
                return true;
            }

            if (request.DamageHealing == null || request.DamageTargetObject == null)
            {
                failure = "Biological Condition damage consequence requires DamageHealingService and target GameObject.";
                return false;
            }

            float scale = Math.Max(1f, request.Tick.ElapsedGameSeconds / 60f);
            DamageApplicationRequest damage = new DamageApplicationRequest(
                $"{request.Tick.TransactionId}.{Sanitize(instance.InstanceId)}.damage",
                string.IsNullOrWhiteSpace(request.DamageSourceActorId) ? request.Body.ActorBodyId : request.DamageSourceActorId,
                request.DamageSourceObject,
                string.IsNullOrWhiteSpace(request.DamageTargetActorId) ? request.Body.ActorBodyId : request.DamageTargetActorId,
                request.DamageTargetObject,
                definition.DamageType,
                plan.Step6DamageAmount * scale,
                "Biological Condition consequence",
                authorityValidated: true);
            DamageApplicationResult result = mutate ? request.DamageHealing.ApplyDamage(damage) : request.DamageHealing.PreviewDamage(damage);
            results.Add(result);
            if (!result.Succeeded && !result.Duplicate)
            {
                failure = result.Message;
                return false;
            }

            return true;
        }

        private bool RemoveConditionOwnedConsequences(BiologicalConditionConsequenceExecutionRequest request, BiologicalConditionInstanceSnapshot previous, bool mutate, List<BiologicalHazardOperationResult> hazardResults, List<BiologicalRecoveryResult> recoveryResults, out string failure)
        {
            failure = string.Empty;
            BiologicalConditionConsequencePlanSnapshot plan = previous.ConsequencePlan;
            if (plan == null)
            {
                return true;
            }

            if (mutate && plan.Flags.HasFlag(BiologicalConditionConsequenceFlags.HazardRequest) && !string.IsNullOrWhiteSpace(plan.HazardDefinitionId) && request.Hazards != null)
            {
                BiologicalHazardOperationResult hazard = request.Hazards.RemoveSource(plan.HazardDefinitionId, HazardSourceId(previous, plan.HazardDefinitionId), request.Restoring);
                if (hazard.Succeeded || hazard.Code == BiologicalHazardResultCode.MissingSource)
                {
                    hazardResults.Add(hazard);
                }
                else
                {
                    failure = hazard.Message;
                    return false;
                }
            }

            if (mutate && plan.Flags.HasFlag(BiologicalConditionConsequenceFlags.RecoveryModifier) && request.Recovery != null)
            {
                BiologicalRecoveryResult recovery = request.Recovery.RemoveRateModifier(new RecoveryRateModifierRequest(request.Body.ActorBodyId, RecoverySourceId(previous), 1f, $"{request.Tick.TransactionId}.{Sanitize(previous.InstanceId)}.recovery.remove", "Biological Condition cleared"), request.Restoring);
                if (recovery.Succeeded || recovery.Duplicate)
                {
                    recoveryResults.Add(recovery);
                }
                else
                {
                    failure = recovery.Message;
                    return false;
                }
            }

            return true;
        }

        private void RollbackConsequences(BiologicalConditionSaveData conditionRollback, VitalProcessSaveData vitalRollback, BiologicalHazardSaveData hazardRollback, BiologicalRecoverySaveData recoveryRollback, BiologicalConditionConsequenceExecutionRequest request)
        {
            if (conditionRollback != null)
            {
                RestoreFromSaveData(conditionRollback, request.Body, registry);
            }

            if (vitalRollback != null && request.VitalProcesses != null)
            {
                request.VitalProcesses.RestoreFromSaveData(vitalRollback, registry == null ? null : registry.DefinitionsById.Values.OfType<SpeciesDefinition>().FirstOrDefault(species => species.Id == request.Body.SpeciesId), request.Body.Anatomy, request.Body.Condition, registry);
            }

            if (hazardRollback != null && request.Hazards != null && request.VitalProcesses != null)
            {
                request.Hazards.RestoreFromSaveData(hazardRollback, request.Body.ActorBodyId, request.VitalProcesses, registry);
            }

            if (recoveryRollback != null && request.Recovery != null)
            {
                request.Recovery.RestoreFromSaveData(recoveryRollback, request.Body, registry);
            }
        }

        private BiologicalConditionResult ResolveTreatment(BiologicalConditionTreatmentRequest request, BodySnapshot body, BiologicalCompatibilityRuntime compatibility, bool mutate)
        {
            BiologicalConditionResultCode validation = ValidateBodyContext(request.ActorBodyId, body, out string failure);
            if (!IsReady || validation != BiologicalConditionResultCode.Success)
            {
                return BiologicalConditionResult.Failure(!IsReady ? BiologicalConditionResultCode.RuntimeNotReady : validation, !IsReady ? "Biological Condition runtime is not Ready." : failure, CreateSnapshot());
            }

            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.InvalidRequest, "Biological Condition treatment requires a transaction ID.", CreateSnapshot());
            }

            if (mutate && processedTransactionIds.Contains(request.TransactionId, StringComparer.Ordinal))
            {
                return BiologicalConditionResult.Success("Duplicate Biological Condition treatment ignored.", request.InstanceId, 0f, null, CreateSnapshot(), duplicate: true);
            }

            if (!instancesById.TryGetValue(request.InstanceId, out ConditionInstanceState existing))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.MissingInstance, $"Biological Condition instance '{request.InstanceId}' is not active.", CreateSnapshot());
            }

            if (!definitionsById.TryGetValue(existing.ConditionDefinitionId, out BiologicalConditionDefinition definition))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.MissingDefinition, $"Biological Condition '{existing.ConditionDefinitionId}' is not registered.", CreateSnapshot());
            }

            if (!treatmentsById.TryGetValue(request.TreatmentDefinitionId, out BiologicalConditionTreatmentDefinition treatment))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.MissingTreatment, $"Treatment '{request.TreatmentDefinitionId}' is not registered.", CreateSnapshot());
            }

            if (!treatment.CanTreat(definition))
            {
                return BiologicalConditionResult.Failure(BiologicalConditionResultCode.TreatmentNotAllowed, $"Treatment '{treatment.Id}' is not authored for '{definition.Id}'.", CreateSnapshot());
            }

            BiologicalInteractionEvaluationResult evaluated = compatibility.Evaluate(body, treatment.BiologicalInteractionDefinitionId, MapCategory(definition.Family), sourceId: request.SourceId, transactionId: request.TransactionId, preview: request.Preview || !mutate);
            BiologicalConditionResultCode compatibilityCode = TranslateCompatibility(evaluated);
            if (compatibilityCode != BiologicalConditionResultCode.Success)
            {
                return BiologicalConditionResult.Failure(compatibilityCode, evaluated.Message, CreateSnapshot(), evaluated);
            }

            ConditionInstanceState next = existing.Clone();
            float effectiveReduction = request.Dose * treatment.LoadReduction * evaluated.RateMultiplier * evaluated.ConsequenceMultiplier;
            next.Load = Mathf.Max(0f, next.Load - effectiveReduction);
            next.RecoveryProgress += effectiveReduction;
            if (next.Load <= 0.001f && treatment.CanClearCondition)
            {
                next.Stage = BiologicalConditionStage.Cleared;
                next.Severity = BiologicalConditionSeverity.Trace;
                if (treatment.GrantsImmunityMemory || definition.GrantsImmunityMemoryOnClear)
                {
                    AddImmunityMemory(next, 1f);
                }
            }
            else if (next.Stage != BiologicalConditionStage.Cleared)
            {
                next.Stage = BiologicalConditionStage.Recovering;
                ApplyStageFromLoad(next, definition, preserveRecovering: true);
            }

            next.Revision++;
            if (mutate)
            {
                instancesById[next.InstanceId] = next;
                RememberTransaction(request.TransactionId);
                BiologicalConditionRevision++;
                IsDirty = true;
            }

            return BiologicalConditionResult.Success(mutate ? "Biological Condition treatment applied." : "Biological Condition treatment preview resolved.", next.InstanceId, effectiveReduction, evaluated, mutate ? CreateSnapshot() : SnapshotWith(next.InstanceId, next), preview: !mutate || request.Preview);
        }

        private void RefreshDefinitions()
        {
            definitionsById.Clear();
            treatmentsById.Clear();
            transmissionsById.Clear();
            if (registry == null)
            {
                return;
            }

            foreach (BiologicalConditionDefinition definition in registry.DefinitionsById.Values.OfType<BiologicalConditionDefinition>().Where(definition => definition != null && definition.AlphaEnabled))
            {
                definitionsById[definition.Id] = definition;
            }

            foreach (BiologicalConditionTreatmentDefinition treatment in registry.DefinitionsById.Values.OfType<BiologicalConditionTreatmentDefinition>().Where(treatment => treatment != null && treatment.AlphaEnabled))
            {
                treatmentsById[treatment.Id] = treatment;
            }

            foreach (BiologicalTransmissionProfileDefinition profile in registry.DefinitionsById.Values.OfType<BiologicalTransmissionProfileDefinition>().Where(profile => profile != null && profile.AlphaEnabled))
            {
                transmissionsById[profile.Id] = profile;
            }
        }

        private BiologicalInteractionEvaluationResult EvaluateCompatibility(BiologicalConditionDefinition definition, BodySnapshot body, BiologicalCompatibilityRuntime compatibility, string targetNodeId, string sourceId, string transactionId, bool preview)
        {
            if (compatibility == null || !compatibility.IsReady)
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.RuntimeNotReady, "Biological Conditions require a ready compatibility runtime.", body?.ActorBodyId, definition?.BiologicalInteractionDefinitionId);
            }

            AnatomyNodeSnapshot targetNode = string.IsNullOrWhiteSpace(targetNodeId) ? null : body?.Anatomy?.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, targetNodeId, StringComparison.Ordinal));
            return compatibility.Evaluate(body, definition.BiologicalInteractionDefinitionId, MapCategory(definition.Family), targetNode, sourceId, transactionId, preview);
        }

        private static BiologicalConditionResultCode TranslateCompatibility(BiologicalInteractionEvaluationResult evaluated)
        {
            if (evaluated == null)
            {
                return BiologicalConditionResultCode.MissingCompatibility;
            }

            if (evaluated.Code == BiologicalCompatibilityResultCode.MissingInteraction)
            {
                return BiologicalConditionResultCode.MissingInteraction;
            }

            if (evaluated.Code != BiologicalCompatibilityResultCode.Success)
            {
                return BiologicalConditionResultCode.MissingCompatibility;
            }

            if (evaluated.Immune)
            {
                return BiologicalConditionResultCode.Immune;
            }

            if (evaluated.Suppressed)
            {
                return BiologicalConditionResultCode.Suppressed;
            }

            return evaluated.Compatible ? BiologicalConditionResultCode.Success : BiologicalConditionResultCode.Incompatible;
        }

        private static BiologicalInteractionCategory MapCategory(BiologicalConditionFamily family)
        {
            switch (family)
            {
                case BiologicalConditionFamily.Disease:
                case BiologicalConditionFamily.Fever:
                case BiologicalConditionFamily.InflammatoryResponse:
                case BiologicalConditionFamily.AutoimmuneCondition:
                case BiologicalConditionFamily.ChronicCondition:
                case BiologicalConditionFamily.DormantCondition:
                case BiologicalConditionFamily.CarrierState:
                case BiologicalConditionFamily.ViralInfection:
                    return BiologicalInteractionCategory.Disease;
                case BiologicalConditionFamily.BacterialInfection:
                case BiologicalConditionFamily.FungalInfection:
                case BiologicalConditionFamily.GeneralInfection:
                    return BiologicalInteractionCategory.Infection;
                case BiologicalConditionFamily.ParasiticInfection:
                    return BiologicalInteractionCategory.Parasite;
                case BiologicalConditionFamily.Poison:
                case BiologicalConditionFamily.Venom:
                    return BiologicalInteractionCategory.Poison;
                case BiologicalConditionFamily.Toxin:
                case BiologicalConditionFamily.Intoxication:
                case BiologicalConditionFamily.Alcohol:
                case BiologicalConditionFamily.DrugEffect:
                case BiologicalConditionFamily.Deficiency:
                    return BiologicalInteractionCategory.Toxin;
                case BiologicalConditionFamily.MagicalCorruptionFoundation:
                    return BiologicalInteractionCategory.MagicalBiological;
                case BiologicalConditionFamily.RadiationLikeConditionFoundation:
                    return BiologicalInteractionCategory.Environmental;
                default:
                    return BiologicalInteractionCategory.Unknown;
            }
        }

        private BiologicalConditionResultCode ValidateBodyContext(string requestedBodyId, BodySnapshot body, out string failure)
        {
            failure = string.Empty;
            if (body == null || string.IsNullOrWhiteSpace(body.ActorBodyId))
            {
                failure = "Biological Condition operation requires a body snapshot.";
                return BiologicalConditionResultCode.MissingBody;
            }

            if (!string.Equals(body.ActorBodyId, ActorBodyId, StringComparison.Ordinal) || (!string.IsNullOrWhiteSpace(requestedBodyId) && !string.Equals(requestedBodyId, ActorBodyId, StringComparison.Ordinal)))
            {
                failure = $"Biological Condition request body '{requestedBodyId}' does not match runtime body '{ActorBodyId}'.";
                return BiologicalConditionResultCode.StaleBody;
            }

            if (body.BodyRevision != bodyRevision)
            {
                failure = $"Biological Condition body revision {body.BodyRevision} does not match runtime revision {bodyRevision}.";
                return BiologicalConditionResultCode.StaleBody;
            }

            return BiologicalConditionResultCode.Success;
        }

        private BiologicalConditionResultCode ValidateExpectedRevisions(BiologicalConditionExposureRequest request, BodySnapshot body, BiologicalInteractionEvaluationResult evaluated, out string failure)
        {
            failure = string.Empty;
            if (request.ExpectedBodyRevision > 0L && request.ExpectedBodyRevision != body.BodyRevision)
            {
                failure = $"Expected body revision {request.ExpectedBodyRevision} but current body is {body.BodyRevision}.";
                return BiologicalConditionResultCode.StaleBody;
            }

            if (request.ExpectedAnatomyRevision > 0L && request.ExpectedAnatomyRevision != (body.Anatomy?.AnatomyRevision ?? 0L))
            {
                failure = $"Expected anatomy revision {request.ExpectedAnatomyRevision} but current anatomy is {body.Anatomy?.AnatomyRevision ?? 0L}.";
                return BiologicalConditionResultCode.StaleDependency;
            }

            if (request.ExpectedConditionRevision > 0L && request.ExpectedConditionRevision != (body.Condition?.ConditionRevision ?? 0L))
            {
                failure = $"Expected structural condition revision {request.ExpectedConditionRevision} but current condition is {body.Condition?.ConditionRevision ?? 0L}.";
                return BiologicalConditionResultCode.StaleDependency;
            }

            if (request.ExpectedCompatibilityRevision > 0L && evaluated != null && request.ExpectedCompatibilityRevision != evaluated.CompatibilityRevision)
            {
                failure = $"Expected compatibility revision {request.ExpectedCompatibilityRevision} but current compatibility is {evaluated.CompatibilityRevision}.";
                return BiologicalConditionResultCode.StaleDependency;
            }

            return BiologicalConditionResultCode.Success;
        }

        private AnatomyNodeSnapshot ResolveTargetNode(BiologicalConditionDefinition definition, string requestedNodeId, BodySnapshot body, out BiologicalConditionResultCode code, out string failure)
        {
            code = BiologicalConditionResultCode.Success;
            failure = string.Empty;
            string nodeId = string.IsNullOrWhiteSpace(requestedNodeId) ? definition.RequiredAnatomyNodeId : requestedNodeId;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            AnatomyNodeSnapshot node = body?.Anatomy?.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
            if (node == null || !node.Present)
            {
                code = BiologicalConditionResultCode.InvalidAnatomyTarget;
                failure = $"Anatomy node '{nodeId}' is not present for Biological Condition '{definition.Id}'.";
                return null;
            }

            if (definition.TargetAnatomyTagIds.Count > 0 && !definition.TargetAnatomyTagIds.Any(required => node.DamageTagIds.Contains(required, StringComparer.Ordinal) || node.EquipmentTagIds.Contains(required, StringComparer.Ordinal)))
            {
                code = BiologicalConditionResultCode.InvalidAnatomyTarget;
                failure = $"Anatomy node '{nodeId}' does not satisfy target tags for Biological Condition '{definition.Id}'.";
            }

            return node;
        }

        private static bool HasRequiredInjury(BodyConditionSnapshot condition, AnatomyNodeSnapshot targetNode)
        {
            if (condition == null)
            {
                return false;
            }

            return condition.ActiveInjuries.Any(injury => targetNode == null || string.Equals(injury.TargetNodeId, targetNode.NodeId, StringComparison.Ordinal));
        }

        private static ConditionInstanceState CreateInstance(string instanceId, BiologicalConditionExposureRequest request, BiologicalConditionDefinition definition, BodySnapshot body, float effectiveDose)
        {
            return new ConditionInstanceState
            {
                InstanceId = instanceId,
                ActorBodyId = body.ActorBodyId,
                ConditionDefinitionId = definition.Id,
                StrainId = string.IsNullOrWhiteSpace(request.StrainId) ? definition.StrainId : request.StrainId,
                Family = definition.Family,
                SourceId = request.SourceId,
                SourceBodyId = request.SourceBodyId,
                SourceEventId = request.SourceEventId,
                SourceCategory = request.SourceCategory,
                ExposureRoute = request.Route,
                TargetAnatomyNodeId = request.TargetAnatomyNodeId,
                Stage = effectiveDose >= definition.EstablishmentThreshold ? BiologicalConditionStage.Active : BiologicalConditionStage.Exposed,
                Severity = BiologicalConditionSeverity.Trace,
                Load = 0f,
                AccumulatedDose = 0f,
                CreatedGameTime = request.DurationSeconds,
                Revision = 0L
            };
        }

        private static void ApplyDose(ConditionInstanceState instance, BiologicalConditionDefinition definition, float effectiveDose)
        {
            instance.AccumulatedDose += effectiveDose;
            instance.Load += effectiveDose;
            ApplyStageFromLoad(instance, definition);
            instance.Revision++;
        }

        private static void ApplyStageFromLoad(ConditionInstanceState instance, BiologicalConditionDefinition definition, bool preserveRecovering = false)
        {
            BiologicalConditionStageRule rule = definition.ResolveStage(instance.Load);
            if (rule != null)
            {
                if (!preserveRecovering)
                {
                    instance.Stage = rule.Stage;
                }

                instance.Severity = rule.Severity;
                if (rule.Stage == BiologicalConditionStage.Dormant)
                {
                    instance.Dormant = true;
                }

                if (rule.Stage == BiologicalConditionStage.Chronic)
                {
                    instance.Chronic = true;
                }

                if (rule.Stage == BiologicalConditionStage.Carrier)
                {
                    instance.Carrier = true;
                }
            }

            if (instance.Load <= 0.001f)
            {
                instance.Stage = BiologicalConditionStage.Cleared;
                instance.Severity = BiologicalConditionSeverity.Trace;
            }
        }

        private BiologicalConditionConsequencePlanSnapshot BuildConsequencePlan(BiologicalConditionDefinition definition, ConditionInstanceState instance, float consequenceMultiplier)
        {
            BiologicalConditionConsequenceFlags flags = BiologicalConditionConsequenceFlags.None;
            if (!string.IsNullOrWhiteSpace(definition.VitalResourceId) && definition.VitalPressurePerTick > 0f)
            {
                flags |= BiologicalConditionConsequenceFlags.VitalPressure;
            }

            if (!string.IsNullOrWhiteSpace(definition.HazardDefinitionId))
            {
                flags |= BiologicalConditionConsequenceFlags.HazardRequest;
            }

            if (definition.DamageType != null && definition.Step6DamagePerTick > 0f)
            {
                flags |= BiologicalConditionConsequenceFlags.Step6DamagePlan;
            }

            if (Math.Abs(definition.RecoveryRateMultiplier - 1f) > 0.0001f)
            {
                flags |= BiologicalConditionConsequenceFlags.RecoveryModifier;
            }

            if (definition.Family == BiologicalConditionFamily.Fever)
            {
                flags |= BiologicalConditionConsequenceFlags.Fever;
            }

            if (definition.Symptoms.Any(symptom => SymptomActive(symptom, instance)))
            {
                flags |= BiologicalConditionConsequenceFlags.Symptom;
            }

            if (definition.TransmissionMode != BiologicalConditionTransmissionMode.None)
            {
                flags |= BiologicalConditionConsequenceFlags.Transmission;
            }

            return new BiologicalConditionConsequencePlanSnapshot(flags, definition.VitalResourceId, definition.VitalPressurePerTick * consequenceMultiplier, definition.HazardDefinitionId, definition.HazardRateMultiplier, definition.DamageType == null ? string.Empty : definition.DamageType.Id, definition.Step6DamagePerTick * consequenceMultiplier, definition.RecoveryRateMultiplier, $"Biological Condition '{definition.Id}' requests consequences through owning systems.");
        }

        private BiologicalConditionInstanceSnapshot ToSnapshot(ConditionInstanceState instance)
        {
            definitionsById.TryGetValue(instance.ConditionDefinitionId, out BiologicalConditionDefinition definition);
            IReadOnlyList<BiologicalConditionSymptomSnapshot> symptoms = definition == null
                ? Array.Empty<BiologicalConditionSymptomSnapshot>()
                : definition.Symptoms.Where(symptom => SymptomActive(symptom, instance))
                    .OrderBy(symptom => symptom.SymptomId, StringComparer.Ordinal)
                    .Select(symptom => new BiologicalConditionSymptomSnapshot(symptom.SymptomId, symptom.DisplayName, string.IsNullOrWhiteSpace(symptom.SourceContributionId) ? $"{instance.InstanceId}.{symptom.SymptomId}" : symptom.SourceContributionId, instance.Severity))
                    .ToArray();
            BiologicalConditionConsequencePlanSnapshot plan = definition == null ? null : BuildConsequencePlan(definition, instance, 1f);
            return new BiologicalConditionInstanceSnapshot(instance.InstanceId, instance.ActorBodyId, instance.ConditionDefinitionId, instance.StrainId, instance.Family, instance.SourceId, instance.SourceBodyId, instance.SourceEventId, instance.SourceCategory, instance.ExposureRoute, instance.TargetAnatomyNodeId, instance.Stage, instance.Severity, instance.Load, instance.AccumulatedDose, instance.IncubationProgress, instance.ProgressionProgress, instance.RecoveryProgress, instance.Dormant, instance.Chronic, instance.Carrier, instance.Suppressed, instance.CreatedGameTime, instance.LastTickTransactionId, instance.Revision, symptoms, plan);
        }

        private static bool SymptomActive(BiologicalConditionSymptomDefinition symptom, ConditionInstanceState instance)
        {
            return symptom != null && instance.Stage >= symptom.MinimumStage && instance.Severity >= symptom.MinimumSeverity && instance.Active && !instance.Suppressed;
        }

        private BiologicalConditionRuntimeSnapshot SnapshotWith(string instanceId, ConditionInstanceState replacement)
        {
            Dictionary<string, ConditionInstanceState> copy = instancesById.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
            copy[instanceId] = replacement;
            return SnapshotWith(copy);
        }

        private BiologicalConditionRuntimeSnapshot SnapshotWith(Dictionary<string, ConditionInstanceState> replacement)
        {
            BiologicalConditionInstanceSnapshot[] instances = replacement.Values.OrderBy(instance => instance.InstanceId, StringComparer.Ordinal).Select(ToSnapshot).ToArray();
            BiologicalConditionImmunityMemorySnapshot[] memory = immunityById.Values.OrderBy(entry => entry.MemoryId, StringComparer.Ordinal).Select(entry => new BiologicalConditionImmunityMemorySnapshot(entry.MemoryId, entry.ActorBodyId, entry.ConditionDefinitionId, entry.StrainId, entry.Strength, entry.SourceInstanceId, entry.Revision)).ToArray();
            return new BiologicalConditionRuntimeSnapshot(ActorBodyId, Readiness, bodyRevision, anatomyRevision, conditionRevision, vitalRevision, hazardRevision, compatibilityRevision, BiologicalConditionRevision, instances, memory, processedTransactionIds, IsDirty, true, Array.Empty<string>());
        }

        private string FindInstanceKey(BiologicalConditionExposureRequest request)
        {
            return definitionsById.TryGetValue(request.ConditionDefinitionId, out BiologicalConditionDefinition definition)
                ? FindInstanceKey(request, definition)
                : string.Empty;
        }

        private static string FindInstanceKey(BiologicalConditionExposureRequest request, BiologicalConditionDefinition definition)
        {
            string strain = string.IsNullOrWhiteSpace(request.StrainId) ? definition.StrainId : request.StrainId;
            string source = definition.StackingPolicy == BiologicalConditionStackingPolicy.MergeBySource && !string.IsNullOrWhiteSpace(request.SourceId)
                ? $".{request.SourceId}"
                : string.Empty;
            return $"condition-instance.{request.ActorBodyId}.{definition.Id}.{strain}{source}";
        }

        private static string HazardSourceId(BiologicalConditionInstanceSnapshot instance, string hazardDefinitionId)
        {
            return $"{instance.InstanceId}.{Sanitize(hazardDefinitionId)}.hazard";
        }

        private static string RecoverySourceId(BiologicalConditionInstanceSnapshot instance)
        {
            return $"{instance.InstanceId}.recovery";
        }

        private static BiologicalHazardSeverity ToHazardSeverity(BiologicalConditionSeverity severity)
        {
            switch (severity)
            {
                case BiologicalConditionSeverity.Trace:
                    return BiologicalHazardSeverity.Trace;
                case BiologicalConditionSeverity.Minor:
                    return BiologicalHazardSeverity.Minor;
                case BiologicalConditionSeverity.Moderate:
                    return BiologicalHazardSeverity.Moderate;
                case BiologicalConditionSeverity.Serious:
                    return BiologicalHazardSeverity.Serious;
                case BiologicalConditionSeverity.Severe:
                    return BiologicalHazardSeverity.Severe;
                case BiologicalConditionSeverity.Critical:
                    return BiologicalHazardSeverity.Critical;
                case BiologicalConditionSeverity.Catastrophic:
                    return BiologicalHazardSeverity.Catastrophic;
                default:
                    return BiologicalHazardSeverity.Minor;
            }
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "missing" : value.Trim().Replace(' ', '-').Replace(':', '.').Replace('/', '.');
        }

        private void AddImmunityMemory(ConditionInstanceState instance, float strength)
        {
            string memoryId = $"immunity-memory.{instance.ActorBodyId}.{instance.ConditionDefinitionId}.{instance.StrainId}";
            immunityById[memoryId] = new ImmunityMemoryState
            {
                MemoryId = memoryId,
                ActorBodyId = instance.ActorBodyId,
                ConditionDefinitionId = instance.ConditionDefinitionId,
                StrainId = instance.StrainId,
                Strength = Math.Max(strength, ResolveImmunityStrength(instance.ConditionDefinitionId, instance.StrainId)),
                SourceInstanceId = instance.InstanceId,
                Revision = BiologicalConditionRevision + 1
            };
        }

        private float ResolveImmunityMemoryMultiplier(string conditionDefinitionId, string strainId)
        {
            float strength = ResolveImmunityStrength(conditionDefinitionId, strainId);
            return Mathf.Clamp01(1f - strength);
        }

        private float ResolveImmunityStrength(string conditionDefinitionId, string strainId)
        {
            return immunityById.Values
                .Where(memory => string.Equals(memory.ConditionDefinitionId, conditionDefinitionId, StringComparison.Ordinal) && string.Equals(memory.StrainId, strainId, StringComparison.Ordinal))
                .Select(memory => Mathf.Clamp01(memory.Strength))
                .DefaultIfEmpty(0f)
                .Max();
        }

        private void RememberTransaction(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || processedTransactionIds.Contains(transactionId, StringComparer.Ordinal))
            {
                return;
            }

            processedTransactionIds.Add(transactionId);
            while (processedTransactionIds.Count > MaximumRememberedTransactions)
            {
                processedTransactionIds.RemoveAt(0);
            }
        }

        private void RestoreRaw(BiologicalConditionSaveData saveData)
        {
            instancesById.Clear();
            immunityById.Clear();
            processedTransactionIds.Clear();
            if (saveData == null)
            {
                return;
            }

            actorBodyId = saveData.actorBodyId;
            BiologicalConditionRevision = saveData.biologicalConditionRevision;
            foreach (BiologicalConditionInstanceSaveData entry in saveData.instances ?? Array.Empty<BiologicalConditionInstanceSaveData>())
            {
                ConditionInstanceState instance = ConditionInstanceState.FromSaveData(entry);
                if (definitionsById.TryGetValue(instance.ConditionDefinitionId, out BiologicalConditionDefinition definition))
                {
                    instance.Family = definition.Family;
                }

                instancesById[instance.InstanceId] = instance;
            }

            foreach (BiologicalConditionImmunityMemorySaveData entry in saveData.immunityMemory ?? Array.Empty<BiologicalConditionImmunityMemorySaveData>())
            {
                ImmunityMemoryState memory = ImmunityMemoryState.FromSaveData(entry);
                immunityById[memory.MemoryId] = memory;
            }

            foreach (string transactionId in saveData.processedTransactionIds ?? Array.Empty<string>())
            {
                RememberTransaction(transactionId);
            }
        }

        private sealed class ConditionInstanceState
        {
            public string InstanceId;
            public string ActorBodyId;
            public string ConditionDefinitionId;
            public string StrainId;
            public BiologicalConditionFamily Family;
            public string SourceId;
            public string SourceBodyId;
            public string SourceEventId;
            public BiologicalConditionSourceCategory SourceCategory;
            public BiologicalExposureRoute ExposureRoute;
            public string TargetAnatomyNodeId;
            public BiologicalConditionStage Stage;
            public BiologicalConditionSeverity Severity;
            public float Load;
            public float AccumulatedDose;
            public float IncubationProgress;
            public float ProgressionProgress;
            public float RecoveryProgress;
            public bool Dormant;
            public bool Chronic;
            public bool Carrier;
            public bool Suppressed;
            public float CreatedGameTime;
            public string LastTickTransactionId;
            public long Revision;
            public bool Active => Stage != BiologicalConditionStage.Cleared && Stage != BiologicalConditionStage.Resolved && Stage != BiologicalConditionStage.Invalid;

            public ConditionInstanceState Clone()
            {
                return (ConditionInstanceState)MemberwiseClone();
            }

            public BiologicalConditionInstanceSaveData ToSaveData()
            {
                return new BiologicalConditionInstanceSaveData
                {
                    instanceId = InstanceId,
                    actorBodyId = ActorBodyId,
                    conditionDefinitionId = ConditionDefinitionId,
                    strainId = StrainId,
                    sourceId = SourceId,
                    sourceBodyId = SourceBodyId,
                    sourceEventId = SourceEventId,
                    sourceCategory = (int)SourceCategory,
                    exposureRoute = (int)ExposureRoute,
                    targetAnatomyNodeId = TargetAnatomyNodeId,
                    stage = (int)Stage,
                    severity = (int)Severity,
                    load = Load,
                    accumulatedDose = AccumulatedDose,
                    incubationProgress = IncubationProgress,
                    progressionProgress = ProgressionProgress,
                    recoveryProgress = RecoveryProgress,
                    dormant = Dormant,
                    chronic = Chronic,
                    carrier = Carrier,
                    suppressed = Suppressed,
                    createdGameTime = CreatedGameTime,
                    lastTickTransactionId = LastTickTransactionId,
                    revision = Revision
                };
            }

            public static ConditionInstanceState FromSaveData(BiologicalConditionInstanceSaveData data)
            {
                return new ConditionInstanceState
                {
                    InstanceId = data.instanceId ?? string.Empty,
                    ActorBodyId = data.actorBodyId ?? string.Empty,
                    ConditionDefinitionId = data.conditionDefinitionId ?? string.Empty,
                    StrainId = data.strainId ?? string.Empty,
                    SourceId = data.sourceId ?? string.Empty,
                    SourceBodyId = data.sourceBodyId ?? string.Empty,
                    SourceEventId = data.sourceEventId ?? string.Empty,
                    SourceCategory = (BiologicalConditionSourceCategory)data.sourceCategory,
                    ExposureRoute = (BiologicalExposureRoute)data.exposureRoute,
                    TargetAnatomyNodeId = data.targetAnatomyNodeId ?? string.Empty,
                    Stage = (BiologicalConditionStage)data.stage,
                    Severity = (BiologicalConditionSeverity)data.severity,
                    Load = Math.Max(0f, data.load),
                    AccumulatedDose = Math.Max(0f, data.accumulatedDose),
                    IncubationProgress = Math.Max(0f, data.incubationProgress),
                    ProgressionProgress = Math.Max(0f, data.progressionProgress),
                    RecoveryProgress = Math.Max(0f, data.recoveryProgress),
                    Dormant = data.dormant,
                    Chronic = data.chronic,
                    Carrier = data.carrier,
                    Suppressed = data.suppressed,
                    CreatedGameTime = Math.Max(0f, data.createdGameTime),
                    LastTickTransactionId = data.lastTickTransactionId ?? string.Empty,
                    Revision = Math.Max(0L, data.revision)
                };
            }
        }

        private sealed class ImmunityMemoryState
        {
            public string MemoryId;
            public string ActorBodyId;
            public string ConditionDefinitionId;
            public string StrainId;
            public float Strength;
            public string SourceInstanceId;
            public long Revision;

            public BiologicalConditionImmunityMemorySaveData ToSaveData()
            {
                return new BiologicalConditionImmunityMemorySaveData
                {
                    memoryId = MemoryId,
                    actorBodyId = ActorBodyId,
                    conditionDefinitionId = ConditionDefinitionId,
                    strainId = StrainId,
                    strength = Strength,
                    sourceInstanceId = SourceInstanceId,
                    revision = Revision
                };
            }

            public static ImmunityMemoryState FromSaveData(BiologicalConditionImmunityMemorySaveData data)
            {
                return new ImmunityMemoryState
                {
                    MemoryId = data.memoryId ?? string.Empty,
                    ActorBodyId = data.actorBodyId ?? string.Empty,
                    ConditionDefinitionId = data.conditionDefinitionId ?? string.Empty,
                    StrainId = data.strainId ?? string.Empty,
                    Strength = Math.Max(0f, data.strength),
                    SourceInstanceId = data.sourceInstanceId ?? string.Empty,
                    Revision = Math.Max(0L, data.revision)
                };
            }
        }
    }
}
