using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Condition
{
    public sealed class BodyConditionRuntime
    {
        private readonly Dictionary<string, StructureConditionRecord> structuresByNodeId = new Dictionary<string, StructureConditionRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, InjuryRecord> injuriesById = new Dictionary<string, InjuryRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> committedTransactionIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string actorBodyId;
        private string anatomyDefinitionId;
        private long bodyRevision;
        private long anatomyRevision;
        private long nextInjurySequence = 1L;
        private bool suppressEvents;

        public event Action<BodyConditionRuntime, LocalizedStructuralDamageResult, bool> ConditionChanged;

        public BodyConditionReadinessState Readiness { get; private set; } = BodyConditionReadinessState.Uninitialized;
        public long ConditionRevision { get; private set; }
        public bool IsDirty { get; private set; }
        public string ActorBodyId => actorBodyId ?? string.Empty;
        public string AnatomyDefinitionId => anatomyDefinitionId ?? string.Empty;
        public bool IsReady => Readiness == BodyConditionReadinessState.Ready;

        public LocalizedStructuralDamageResult BuildHealthy(
            string exactActorBodyId,
            AnatomySnapshot anatomy,
            DefinitionRegistry definitionRegistry,
            bool restoring = false,
            bool preserveRevision = false)
        {
            registry = definitionRegistry ?? registry;
            if (string.IsNullOrWhiteSpace(exactActorBodyId))
            {
                Readiness = BodyConditionReadinessState.WaitingForBody;
                return LocalizedStructuralDamageResult.Failure(null, LocalizedDamageResultCode.MissingActorBody, "Body condition requires an exact Actor/body ID.", CreateSnapshot());
            }

            if (anatomy == null || anatomy.Readiness != AnatomyReadinessState.Ready || !anatomy.Coherent)
            {
                Readiness = BodyConditionReadinessState.WaitingForAnatomy;
                return LocalizedStructuralDamageResult.Failure(null, LocalizedDamageResultCode.MissingAnatomy, "Body condition requires a Ready Anatomy runtime.", CreateSnapshot());
            }

            Readiness = restoring ? BodyConditionReadinessState.Restoring : BodyConditionReadinessState.BuildingConditionState;
            actorBodyId = exactActorBodyId;
            anatomyDefinitionId = anatomy.AnatomyDefinitionId;
            bodyRevision = anatomy.BodyRevision;
            anatomyRevision = anatomy.AnatomyRevision;
            structuresByNodeId.Clear();
            injuriesById.Clear();
            committedTransactionIds.Clear();
            nextInjurySequence = 1L;

            foreach (AnatomyNodeSnapshot node in anatomy.Nodes.OrderBy(node => node.NodeId, StringComparer.Ordinal))
            {
                structuresByNodeId[node.NodeId] = StructureConditionRecord.CreateHealthy(node);
            }

            if (!preserveRevision)
            {
                ConditionRevision++;
            }

            IsDirty = !restoring;
            Readiness = BodyConditionReadinessState.Ready;
            return LocalizedStructuralDamageResult.Success(
                new LocalizedStructuralDamageRequest { TargetActorBodyId = actorBodyId, TransactionId = "condition.rebuild" },
                string.Empty,
                0,
                0,
                0,
                InjurySeverity.Trivial,
                StructureFunctionalState.Normal,
                StructureDamageState.Intact,
                RuntimeStructurePresenceState.Present,
                CreateSnapshot(),
                preview: false,
                duplicate: false);
        }

        public LocalizedStructuralDamageResult PreviewLocalizedDamage(LocalizedStructuralDamageRequest request, AnatomySnapshot anatomy, BiologicalCompatibilityRuntime compatibility, BodySnapshot body)
        {
            return ResolveDamage(request, anatomy, preview: true, out _, out _, out _, out _, out _, compatibility, body);
        }

        public LocalizedStructuralDamageResult ApplyLocalizedDamage(LocalizedStructuralDamageRequest request, AnatomySnapshot anatomy, BiologicalCompatibilityRuntime compatibility, BodySnapshot body, bool restoring = false)
        {
            LocalizedStructuralDamageResult duplicateReplay = TryResolveCommittedDuplicate(request);
            if (duplicateReplay != null)
            {
                return duplicateReplay;
            }

            LocalizedStructuralDamageResult resolved = ResolveDamage(request, anatomy, preview: false, out InjuryTypeDefinition injuryDefinition, out StructureConditionRecord structure, out int requestedDamage, out InjurySeverity severity, out DamageTypeDefinition damageType, compatibility, body);
            if (!resolved.Succeeded)
            {
                return resolved;
            }

            int previousIntegrity = structure.CurrentIntegrity;
            int appliedDamage = Math.Min(previousIntegrity, requestedDamage);
            string injuryId = BuildInjuryId(request.TransactionId, structure.NodeId, injuryDefinition.Id);
            InjuryRecord injury = new InjuryRecord(
                injuryId,
                ActorBodyId,
                structure.NodeId,
                injuryDefinition.Id,
                request.SourceActorBodyId,
                request.TransactionId,
                damageType == null ? string.Empty : damageType.Id,
                severity,
                appliedDamage,
                injuryDefinition.FunctionalImpact,
                injuryDefinition.StructuralImpact,
                InjuryRecordState.Active,
                nextInjurySequence++,
                1L);

            structure.ApplyInjury(injury, injuryDefinition, appliedDamage);
            injuriesById[injuryId] = injury;
            committedTransactionIds.Add(request.TransactionId);
            ConditionRevision++;
            IsDirty = !restoring;

            LocalizedStructuralDamageResult result = LocalizedStructuralDamageResult.Success(
                request,
                injuryId,
                appliedDamage,
                previousIntegrity,
                structure.CurrentIntegrity,
                severity,
                structure.FunctionalState,
                structure.StructuralState,
                structure.RuntimePresence,
                CreateSnapshot());
            RaiseChanged(result, restoring);
            return result;
        }

        private LocalizedStructuralDamageResult TryResolveCommittedDuplicate(LocalizedStructuralDamageRequest request)
        {
            if (!IsReady || request == null || string.IsNullOrWhiteSpace(request.TransactionId) || !committedTransactionIds.Contains(request.TransactionId))
            {
                return null;
            }

            if (!string.Equals(request.TargetActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return null;
            }

            InjuryRecord existing = injuriesById.Values.FirstOrDefault(injury => string.Equals(injury.SourceTransactionId, request.TransactionId, StringComparison.Ordinal));
            StructureConditionRecord structure = null;
            if (existing != null)
            {
                structuresByNodeId.TryGetValue(existing.TargetNodeId, out structure);
            }

            return LocalizedStructuralDamageResult.Success(
                request,
                existing == null ? string.Empty : existing.InjuryId,
                0,
                structure == null ? 0 : structure.CurrentIntegrity,
                structure == null ? 0 : structure.CurrentIntegrity,
                existing == null ? InjurySeverity.Trivial : existing.Severity,
                structure == null ? StructureFunctionalState.Unknown : structure.FunctionalState,
                structure == null ? StructureDamageState.Unknown : structure.StructuralState,
                structure == null ? RuntimeStructurePresenceState.Unknown : structure.RuntimePresence,
                CreateSnapshot(),
                duplicate: true);
        }

        public LocalizedStructuralDamageResult RemoveInjury(string injuryId, bool restoring = false)
        {
            if (string.IsNullOrWhiteSpace(injuryId) || !injuriesById.TryGetValue(injuryId, out InjuryRecord injury))
            {
                return LocalizedStructuralDamageResult.Failure(null, LocalizedDamageResultCode.InvalidRequest, $"Injury '{injuryId}' does not exist.", CreateSnapshot());
            }

            injury.State = InjuryRecordState.Resolved;
            injury.Revision++;
            if (structuresByNodeId.TryGetValue(injury.TargetNodeId, out StructureConditionRecord structure))
            {
                structure.RemoveInjury(injuryId, injuriesById.Values.Where(candidate => candidate.State == InjuryRecordState.Active));
            }

            ConditionRevision++;
            IsDirty = !restoring;
            LocalizedStructuralDamageResult result = LocalizedStructuralDamageResult.Success(
                new LocalizedStructuralDamageRequest { TransactionId = $"condition.remove.{injuryId}", TargetActorBodyId = ActorBodyId, TargetNodeId = injury.TargetNodeId, InjuryDefinitionId = injury.InjuryDefinitionId },
                injuryId,
                0,
                structure == null ? 0 : structure.CurrentIntegrity,
                structure == null ? 0 : structure.CurrentIntegrity,
                injury.Severity,
                structure == null ? StructureFunctionalState.Unknown : structure.FunctionalState,
                structure == null ? StructureDamageState.Unknown : structure.StructuralState,
                structure == null ? RuntimeStructurePresenceState.Unknown : structure.RuntimePresence,
                CreateSnapshot());
            RaiseChanged(result, restoring);
            return result;
        }

        public StructuralRecoveryResult PreviewStructuralRecovery(StructuralRecoveryRequest request)
        {
            return ResolveStructuralRecovery(request, preview: true, out _, out _, out _, out _, out _);
        }

        public StructuralRecoveryResult ApplyStructuralRecovery(StructuralRecoveryRequest request, bool restoring = false)
        {
            StructuralRecoveryResult duplicate = TryResolveStructuralRecoveryDuplicate(request);
            if (duplicate != null)
            {
                return duplicate;
            }

            StructuralRecoveryResult resolved = ResolveStructuralRecovery(request, preview: false, out StructureConditionRecord structure, out InjuryRecord injury, out int restoreAmount, out int newIntegrity, out bool resolveInjury);
            if (!resolved.Succeeded)
            {
                return resolved;
            }

            int previousIntegrity = structure.CurrentIntegrity;
            structure.ApplyRecovery(newIntegrity, resolveInjury ? StructureFunctionalState.Normal : ProjectRecoveredFunctionalState(newIntegrity, structure.MaximumIntegrity), resolveInjury ? StructureDamageState.Intact : ProjectRecoveredStructuralState(newIntegrity, structure.MaximumIntegrity));
            if (resolveInjury && injury != null)
            {
                injury.State = InjuryRecordState.Resolved;
                injury.Revision++;
                structure.RemoveInjury(injury.InjuryId, injuriesById.Values.Where(candidate => candidate.State == InjuryRecordState.Active));
            }

            if (!string.IsNullOrWhiteSpace(request.TransactionId))
            {
                committedTransactionIds.Add(request.TransactionId);
            }

            ConditionRevision++;
            IsDirty = !restoring;
            StructuralRecoveryResult result = StructuralRecoveryResult.Success(
                request,
                Math.Max(0, structure.CurrentIntegrity - previousIntegrity),
                previousIntegrity,
                structure.CurrentIntegrity,
                structure.FunctionalState,
                structure.StructuralState,
                structure.RuntimePresence,
                CreateSnapshot());
            RaiseChanged(LocalizedStructuralDamageResult.Success(
                new LocalizedStructuralDamageRequest { TransactionId = request.TransactionId, TargetActorBodyId = ActorBodyId, TargetNodeId = structure.NodeId, InjuryDefinitionId = injury == null ? string.Empty : injury.InjuryDefinitionId },
                injury == null ? string.Empty : injury.InjuryId,
                0,
                previousIntegrity,
                structure.CurrentIntegrity,
                injury == null ? InjurySeverity.Trivial : injury.Severity,
                structure.FunctionalState,
                structure.StructuralState,
                structure.RuntimePresence,
                CreateSnapshot()),
                restoring);
            return result;
        }

        public BodyConditionSaveData CreateSaveData()
        {
            return new BodyConditionSaveData
            {
                schemaVersion = BodyConditionSaveData.CurrentSchemaVersion,
                actorBodyId = ActorBodyId,
                anatomyDefinitionId = AnatomyDefinitionId,
                bodyRevision = bodyRevision,
                anatomyRevision = anatomyRevision,
                conditionRevision = ConditionRevision,
                structures = structuresByNodeId.Values.OrderBy(structure => structure.NodeId, StringComparer.Ordinal).Select(structure => structure.ToSaveData()).ToArray(),
                injuries = injuriesById.Values.OrderBy(injury => injury.InjuryId, StringComparer.Ordinal).Select(injury => injury.ToSaveData()).ToArray(),
                committedTransactionIds = committedTransactionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            };
        }

        public LocalizedStructuralDamageResult RestoreFromSaveData(BodyConditionSaveData saveData, AnatomySnapshot anatomy, DefinitionRegistry definitionRegistry)
        {
            if (!ValidateSaveData(saveData, anatomy, definitionRegistry, out string failureReason))
            {
                return LocalizedStructuralDamageResult.Failure(null, LocalizedDamageResultCode.InvalidRestore, failureReason, CreateSnapshot());
            }

            registry = definitionRegistry ?? registry;
            using (SuppressEvents())
            {
                BuildHealthy(saveData.actorBodyId, anatomy, registry, restoring: true, preserveRevision: true);
                injuriesById.Clear();
                committedTransactionIds.Clear();

                foreach (StructureConditionSaveData structureData in saveData.structures ?? Array.Empty<StructureConditionSaveData>())
                {
                    if (structureData == null || string.IsNullOrWhiteSpace(structureData.nodeId) || !structuresByNodeId.TryGetValue(structureData.nodeId, out StructureConditionRecord structure))
                    {
                        continue;
                    }

                    structure.Restore(structureData);
                }

                foreach (InjuryRecordSaveData injuryData in saveData.injuries ?? Array.Empty<InjuryRecordSaveData>())
                {
                    InjuryRecord injury = InjuryRecord.FromSaveData(injuryData);
                    injuriesById[injury.InjuryId] = injury;
                    nextInjurySequence = Math.Max(nextInjurySequence, injury.Sequence + 1L);
                }

                foreach (string transactionId in saveData.committedTransactionIds ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(transactionId))
                    {
                        committedTransactionIds.Add(transactionId);
                    }
                }

                ConditionRevision = Math.Max(1L, saveData.conditionRevision);
                bodyRevision = saveData.bodyRevision;
                anatomyRevision = saveData.anatomyRevision;
                IsDirty = false;
                Readiness = BodyConditionReadinessState.Ready;
            }

            return LocalizedStructuralDamageResult.Success(
                new LocalizedStructuralDamageRequest { TargetActorBodyId = ActorBodyId, TransactionId = "condition.restore" },
                string.Empty,
                0,
                0,
                0,
                InjurySeverity.Trivial,
                StructureFunctionalState.Normal,
                StructureDamageState.Intact,
                RuntimeStructurePresenceState.Present,
                CreateSnapshot());
        }

        public static bool ValidateSaveData(BodyConditionSaveData saveData, AnatomySnapshot anatomy, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Body condition save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > BodyConditionSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported body condition schema version {saveData.schemaVersion}.";
                return false;
            }

            if (anatomy == null || !anatomy.Coherent)
            {
                failureReason = "Body condition restore requires a coherent Anatomy snapshot.";
                return false;
            }

            if (!string.Equals(saveData.actorBodyId, anatomy.ActorBodyId, StringComparison.Ordinal))
            {
                failureReason = $"Saved condition body '{saveData.actorBodyId}' does not match Anatomy body '{anatomy.ActorBodyId}'.";
                return false;
            }

            HashSet<string> nodeIds = new HashSet<string>(anatomy.Nodes.Select(node => node.NodeId), StringComparer.Ordinal);
            HashSet<string> injuryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (InjuryRecordSaveData injury in saveData.injuries ?? Array.Empty<InjuryRecordSaveData>())
            {
                if (injury == null || string.IsNullOrWhiteSpace(injury.injuryId) || !injuryIds.Add(injury.injuryId))
                {
                    failureReason = "Saved condition contains a missing or duplicate injury ID.";
                    return false;
                }

                if (!nodeIds.Contains(injury.targetNodeId))
                {
                    failureReason = $"Saved injury '{injury.injuryId}' references missing Anatomy node '{injury.targetNodeId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(injury.injuryDefinitionId, out InjuryTypeDefinition definition) || definition == null)
                {
                    failureReason = $"Saved injury '{injury.injuryId}' references unknown Injury definition '{injury.injuryDefinitionId}'.";
                    return false;
                }
            }

            foreach (StructureConditionSaveData structure in saveData.structures ?? Array.Empty<StructureConditionSaveData>())
            {
                if (structure == null || !nodeIds.Contains(structure.nodeId))
                {
                    failureReason = $"Saved condition references missing Anatomy node '{structure?.nodeId ?? string.Empty}'.";
                    return false;
                }

                if (structure.currentIntegrity < 0 || structure.maximumIntegrity < 0 || structure.currentIntegrity > structure.maximumIntegrity)
                {
                    failureReason = $"Saved structure '{structure.nodeId}' has invalid integrity {structure.currentIntegrity}/{structure.maximumIntegrity}.";
                    return false;
                }
            }

            return true;
        }

        public BodyConditionSnapshot CreateSnapshot()
        {
            List<string> diagnostics = new List<string>();
            bool coherent = ValidateCondition(out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                diagnostics.Add(failureReason);
            }

            return new BodyConditionSnapshot(
                ActorBodyId,
                AnatomyDefinitionId,
                Readiness,
                bodyRevision,
                anatomyRevision,
                ConditionRevision,
                structuresByNodeId.Values.OrderBy(structure => structure.NodeId, StringComparer.Ordinal).Select(structure => structure.CreateSnapshot()).ToArray(),
                injuriesById.Values.OrderBy(injury => injury.InjuryId, StringComparer.Ordinal).Select(injury => injury.CreateSnapshot()).ToArray(),
                coherent,
                diagnostics);
        }

        public bool TryGetStructure(string nodeId, out StructureConditionSnapshot structure)
        {
            structure = null;
            if (!structuresByNodeId.TryGetValue(nodeId ?? string.Empty, out StructureConditionRecord record))
            {
                return false;
            }

            structure = record.CreateSnapshot();
            return true;
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        public void Dispose()
        {
            Readiness = BodyConditionReadinessState.Disposed;
            structuresByNodeId.Clear();
            injuriesById.Clear();
            committedTransactionIds.Clear();
        }

        private LocalizedStructuralDamageResult ResolveDamage(
            LocalizedStructuralDamageRequest request,
            AnatomySnapshot anatomy,
            bool preview,
            out InjuryTypeDefinition injuryDefinition,
            out StructureConditionRecord structure,
            out int requestedDamage,
            out InjurySeverity severity,
            out DamageTypeDefinition damageType,
            BiologicalCompatibilityRuntime compatibility = null,
            BodySnapshot body = null)
        {
            injuryDefinition = null;
            structure = null;
            requestedDamage = 0;
            severity = InjurySeverity.Trivial;
            damageType = null;

            if (!IsReady)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.RuntimeNotReady, "Body condition runtime is not Ready.", CreateSnapshot());
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.InvalidRequest, "Localized structural damage requires a transaction ID.", CreateSnapshot());
            }

            if (!string.Equals(request.TargetActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.StaleBody, $"Request target body '{request.TargetActorBodyId}' does not match runtime body '{ActorBodyId}'.", CreateSnapshot());
            }

            if (request.ExpectedBodyRevision > 0L && request.ExpectedBodyRevision != bodyRevision)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.StaleBody, $"Request expected body revision {request.ExpectedBodyRevision} but runtime is {bodyRevision}.", CreateSnapshot());
            }

            if (request.ExpectedAnatomyRevision > 0L && request.ExpectedAnatomyRevision != anatomyRevision)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.StaleAnatomy, $"Request expected anatomy revision {request.ExpectedAnatomyRevision} but runtime is {anatomyRevision}.", CreateSnapshot());
            }

            if (anatomy == null || !anatomy.Coherent || !string.Equals(anatomy.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.MissingAnatomy, "Localized structural damage requires the current coherent Anatomy snapshot.", CreateSnapshot());
            }

            AnatomyNodeSnapshot node = anatomy.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, request.TargetNodeId, StringComparison.Ordinal));
            if (node == null || !structuresByNodeId.TryGetValue(request.TargetNodeId, out structure))
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.MissingAnatomyNode, $"Anatomy node '{request.TargetNodeId}' does not exist.", CreateSnapshot());
            }

            if (!structure.Present && !request.AllowUnavailableTarget)
            {
                return LocalizedStructuralDamageResult.Failure(request, structure.RuntimePresence == RuntimeStructurePresenceState.Destroyed ? LocalizedDamageResultCode.AlreadyDestroyed : LocalizedDamageResultCode.TargetUnavailable, $"Anatomy node '{request.TargetNodeId}' is not available ({structure.RuntimePresence}).", CreateSnapshot());
            }

            if (registry == null || !registry.TryGet(request.InjuryDefinitionId, out injuryDefinition) || injuryDefinition == null)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.MissingInjuryDefinition, $"Injury definition '{request.InjuryDefinitionId}' does not exist.", CreateSnapshot());
            }

            if (!string.IsNullOrWhiteSpace(request.DamageTypeId))
            {
                registry.TryGet(request.DamageTypeId, out damageType);
            }

            if (!injuryDefinition.IsCompatibleWith(node, damageType))
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.IncompatibleInjury, $"Injury '{injuryDefinition.Id}' is incompatible with node '{node.NodeId}' and damage '{damageType?.Id ?? string.Empty}'.", CreateSnapshot());
            }

            LocalizedStructuralDamageResult compatibilityValidation = ValidateCompatibilityContext(request, compatibility, body);
            if (!compatibilityValidation.Succeeded)
            {
                return compatibilityValidation;
            }

            BiologicalInteractionEvaluationResult compatibilityResult = EvaluateInjuryCompatibility(compatibility, body, injuryDefinition, node, request.TransactionId, preview);
            if (compatibilityResult == null)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.MissingInteraction, $"Injury '{injuryDefinition.Id}' does not map to a biological interaction.", CreateSnapshot());
            }

            if (compatibilityResult.Code != BiologicalCompatibilityResultCode.Success)
            {
                return LocalizedStructuralDamageResult.Failure(request, compatibilityResult.Code == BiologicalCompatibilityResultCode.StaleBody ? LocalizedDamageResultCode.StaleBody : LocalizedDamageResultCode.MissingCompatibility, compatibilityResult.Message, CreateSnapshot());
            }

            if (compatibilityResult.CompatibilityState == BiologicalCompatibilityState.Incompatible || compatibilityResult.Immune || compatibilityResult.Suppressed)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.IncompatibleInjury, $"Injury '{injuryDefinition.Id}' is not biologically compatible: {compatibilityResult.Message}", CreateSnapshot());
            }

            requestedDamage = Math.Max(0, request.StructuralDamage <= 0 ? injuryDefinition.BaseIntegrityDamage : request.StructuralDamage);
            requestedDamage = (int)Math.Round(requestedDamage * compatibilityResult.ConsequenceMultiplier);

            severity = DeriveSeverity(requestedDamage, structure.MaximumIntegrity, node.Vital);
            int projected = Math.Max(0, structure.CurrentIntegrity - requestedDamage);
            if (preview)
            {
                return LocalizedStructuralDamageResult.Success(
                    request,
                    BuildInjuryId(request.TransactionId, structure.NodeId, injuryDefinition.Id),
                    Math.Min(structure.CurrentIntegrity, requestedDamage),
                    structure.CurrentIntegrity,
                    projected,
                    severity,
                    ProjectFunctionalState(structure, injuryDefinition, projected),
                    ProjectStructuralState(structure, injuryDefinition, projected),
                    ProjectPresenceState(structure, injuryDefinition, projected),
                    CreateSnapshot(),
                    preview: true);
            }

            return LocalizedStructuralDamageResult.Success(
                request,
                BuildInjuryId(request.TransactionId, structure.NodeId, injuryDefinition.Id),
                Math.Min(structure.CurrentIntegrity, requestedDamage),
                structure.CurrentIntegrity,
                projected,
                severity,
                ProjectFunctionalState(structure, injuryDefinition, projected),
                ProjectStructuralState(structure, injuryDefinition, projected),
                ProjectPresenceState(structure, injuryDefinition, projected),
                CreateSnapshot());
        }

        private LocalizedStructuralDamageResult ValidateCompatibilityContext(LocalizedStructuralDamageRequest request, BiologicalCompatibilityRuntime compatibility, BodySnapshot body)
        {
            if (compatibility == null || body == null)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.MissingCompatibility, "Localized structural damage requires a biological compatibility runtime and current body snapshot.", CreateSnapshot());
            }

            if (!compatibility.IsReady)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.MissingCompatibility, "Biological compatibility runtime is not Ready.", CreateSnapshot());
            }

            if (!string.Equals(compatibility.ActorBodyId, ActorBodyId, StringComparison.Ordinal)
                || !string.Equals(body.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.StaleBody, $"Compatibility body '{compatibility.ActorBodyId}' and snapshot body '{body.ActorBodyId}' must match condition body '{ActorBodyId}'.", CreateSnapshot());
            }

            if (body.BodyRevision != bodyRevision)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.StaleBody, $"Body snapshot revision {body.BodyRevision} does not match condition body revision {bodyRevision}.", CreateSnapshot());
            }

            if (body.Anatomy == null || body.Anatomy.AnatomyRevision != anatomyRevision)
            {
                return LocalizedStructuralDamageResult.Failure(request, LocalizedDamageResultCode.StaleAnatomy, $"Body snapshot anatomy revision {body.Anatomy?.AnatomyRevision ?? 0L} does not match condition anatomy revision {anatomyRevision}.", CreateSnapshot());
            }

            return LocalizedStructuralDamageResult.Success(
                request,
                string.Empty,
                0,
                0,
                0,
                InjurySeverity.Trivial,
                StructureFunctionalState.Normal,
                StructureDamageState.Intact,
                RuntimeStructurePresenceState.Present,
                CreateSnapshot(),
                preview: true);
        }

        private bool ValidateCondition(out string failureReason)
        {
            failureReason = string.Empty;
            if (Readiness == BodyConditionReadinessState.Disposed)
            {
                failureReason = "Body condition runtime is disposed.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ActorBodyId))
            {
                failureReason = "Body condition runtime is missing an exact Actor/body ID.";
                return false;
            }

            foreach (StructureConditionRecord structure in structuresByNodeId.Values)
            {
                if (structure.CurrentIntegrity < 0 || structure.MaximumIntegrity < 0 || structure.CurrentIntegrity > structure.MaximumIntegrity)
                {
                    failureReason = $"Structure '{structure.NodeId}' has invalid integrity {structure.CurrentIntegrity}/{structure.MaximumIntegrity}.";
                    return false;
                }
            }

            foreach (InjuryRecord injury in injuriesById.Values)
            {
                if (!structuresByNodeId.ContainsKey(injury.TargetNodeId))
                {
                    failureReason = $"Injury '{injury.InjuryId}' targets missing node '{injury.TargetNodeId}'.";
                    return false;
                }
            }

            return Readiness == BodyConditionReadinessState.Ready || Readiness == BodyConditionReadinessState.Uninitialized;
        }

        private StructuralRecoveryResult TryResolveStructuralRecoveryDuplicate(StructuralRecoveryRequest request)
        {
            if (!IsReady || request == null || string.IsNullOrWhiteSpace(request.TransactionId) || !committedTransactionIds.Contains(request.TransactionId))
            {
                return null;
            }

            if (!string.Equals(request.TargetActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return null;
            }

            structuresByNodeId.TryGetValue(request.TargetNodeId ?? string.Empty, out StructureConditionRecord structure);
            return StructuralRecoveryResult.Success(
                request,
                0,
                structure == null ? 0 : structure.CurrentIntegrity,
                structure == null ? 0 : structure.CurrentIntegrity,
                structure == null ? StructureFunctionalState.Unknown : structure.FunctionalState,
                structure == null ? StructureDamageState.Unknown : structure.StructuralState,
                structure == null ? RuntimeStructurePresenceState.Unknown : structure.RuntimePresence,
                CreateSnapshot(),
                duplicate: true);
        }

        private StructuralRecoveryResult ResolveStructuralRecovery(
            StructuralRecoveryRequest request,
            bool preview,
            out StructureConditionRecord structure,
            out InjuryRecord injury,
            out int restoreAmount,
            out int newIntegrity,
            out bool resolveInjury)
        {
            structure = null;
            injury = null;
            restoreAmount = 0;
            newIntegrity = 0;
            resolveInjury = false;

            if (!IsReady)
            {
                return StructuralRecoveryResult.Failure(request, StructuralRecoveryResultCode.RuntimeNotReady, "Body condition runtime is not Ready.", CreateSnapshot());
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId) || string.IsNullOrWhiteSpace(request.RecoveryMethodId))
            {
                return StructuralRecoveryResult.Failure(request, StructuralRecoveryResultCode.InvalidRequest, "Structural recovery requires a transaction ID and Recovery Method ID.", CreateSnapshot());
            }

            if (!string.Equals(request.TargetActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return StructuralRecoveryResult.Failure(request, StructuralRecoveryResultCode.StaleBody, $"Request target body '{request.TargetActorBodyId}' does not match runtime body '{ActorBodyId}'.", CreateSnapshot());
            }

            if (request.ExpectedConditionRevision > 0L && request.ExpectedConditionRevision != ConditionRevision)
            {
                return StructuralRecoveryResult.Failure(request, StructuralRecoveryResultCode.StaleCondition, $"Request expected condition revision {request.ExpectedConditionRevision} but runtime is {ConditionRevision}.", CreateSnapshot());
            }

            if (!structuresByNodeId.TryGetValue(request.TargetNodeId ?? string.Empty, out structure))
            {
                return StructuralRecoveryResult.Failure(request, StructuralRecoveryResultCode.MissingAnatomyNode, $"Structure '{request.TargetNodeId}' does not exist.", CreateSnapshot());
            }

            if (!string.IsNullOrWhiteSpace(request.TargetInjuryId) && !injuriesById.TryGetValue(request.TargetInjuryId, out injury))
            {
                return StructuralRecoveryResult.Failure(request, StructuralRecoveryResultCode.MissingInjury, $"Injury '{request.TargetInjuryId}' does not exist.", CreateSnapshot());
            }

            if (!StructureCanRecover(structure, request, out string unavailableReason))
            {
                return StructuralRecoveryResult.Failure(request, StructuralRecoveryResultCode.TargetUnavailable, unavailableReason, CreateSnapshot());
            }

            if (request.IntegrityRestoration <= 0)
            {
                return StructuralRecoveryResult.Failure(request, StructuralRecoveryResultCode.InvalidAmount, "Structural recovery amount must be positive.", CreateSnapshot());
            }

            int recoveryLimit = request.MaximumRecoverableIntegrity <= 0 ? structure.MaximumIntegrity : Math.Min(structure.MaximumIntegrity, request.MaximumRecoverableIntegrity);
            if (structure.CurrentIntegrity >= recoveryLimit)
            {
                return StructuralRecoveryResult.Failure(request, StructuralRecoveryResultCode.RecoveryLimitReached, $"Structure '{structure.NodeId}' is already at recovery limit {recoveryLimit}.", CreateSnapshot());
            }

            newIntegrity = Math.Min(recoveryLimit, structure.CurrentIntegrity + request.IntegrityRestoration);
            restoreAmount = Math.Max(0, newIntegrity - structure.CurrentIntegrity);
            resolveInjury = request.ResolveInjuryOnCompletion && newIntegrity >= recoveryLimit;
            return StructuralRecoveryResult.Success(
                request,
                restoreAmount,
                structure.CurrentIntegrity,
                newIntegrity,
                resolveInjury ? StructureFunctionalState.Normal : ProjectRecoveredFunctionalState(newIntegrity, structure.MaximumIntegrity),
                resolveInjury ? StructureDamageState.Intact : ProjectRecoveredStructuralState(newIntegrity, structure.MaximumIntegrity),
                structure.RuntimePresence,
                CreateSnapshot(),
                preview);
        }

        private static bool StructureCanRecover(StructureConditionRecord structure, StructuralRecoveryRequest request, out string reason)
        {
            reason = string.Empty;
            if (structure == null)
            {
                reason = "Structure is missing.";
                return false;
            }

            if (structure.RuntimePresence == RuntimeStructurePresenceState.AuthoredAbsent)
            {
                reason = $"Structure '{structure.NodeId}' is authored absent.";
                return false;
            }

            if (structure.RuntimePresence == RuntimeStructurePresenceState.Destroyed && !request.AllowDestroyedStructure)
            {
                reason = $"Structure '{structure.NodeId}' is destroyed and this recovery method does not allow destroyed-structure restoration.";
                return false;
            }

            if (structure.RuntimePresence == RuntimeStructurePresenceState.Missing && !request.AllowMissingStructure)
            {
                reason = $"Structure '{structure.NodeId}' is missing and this recovery method does not allow missing-structure restoration.";
                return false;
            }

            if (structure.RuntimePresence == RuntimeStructurePresenceState.Severed && !request.AllowSeveredStructure)
            {
                reason = $"Structure '{structure.NodeId}' is severed and this recovery method does not allow severed-structure restoration.";
                return false;
            }

            return true;
        }

        private static StructureFunctionalState ProjectRecoveredFunctionalState(int integrity, int maximumIntegrity)
        {
            if (maximumIntegrity <= 0 || integrity <= 0)
            {
                return StructureFunctionalState.Disabled;
            }

            float ratio = (float)integrity / maximumIntegrity;
            if (ratio >= 1f)
            {
                return StructureFunctionalState.Normal;
            }

            if (ratio >= 0.5f)
            {
                return StructureFunctionalState.Reduced;
            }

            return StructureFunctionalState.SeverelyReduced;
        }

        private static StructureDamageState ProjectRecoveredStructuralState(int integrity, int maximumIntegrity)
        {
            if (maximumIntegrity <= 0 || integrity <= 0)
            {
                return StructureDamageState.Destroyed;
            }

            return integrity >= maximumIntegrity ? StructureDamageState.Intact : StructureDamageState.Damaged;
        }

        private void RaiseChanged(LocalizedStructuralDamageResult result, bool restoring)
        {
            if (!suppressEvents)
            {
                ConditionChanged?.Invoke(this, result, restoring);
            }
        }

        private IDisposable SuppressEvents()
        {
            suppressEvents = true;
            return new Suppression(this);
        }

        private static string BuildInjuryId(string transactionId, string nodeId, string injuryDefinitionId)
        {
            return $"injury-record.{Sanitize(transactionId)}.{Sanitize(nodeId)}.{Sanitize(injuryDefinitionId)}";
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "missing" : value.Trim().Replace(' ', '-');
        }

        private static InjurySeverity DeriveSeverity(int damage, int maximumIntegrity, bool vital)
        {
            if (damage <= 0 || maximumIntegrity <= 0)
            {
                return InjurySeverity.Trivial;
            }

            float ratio = (float)damage / maximumIntegrity;
            if (vital && ratio >= 0.75f)
            {
                return InjurySeverity.Catastrophic;
            }

            if (ratio >= 1f)
            {
                return InjurySeverity.Catastrophic;
            }

            if (ratio >= 0.75f)
            {
                return InjurySeverity.Critical;
            }

            if (ratio >= 0.5f)
            {
                return InjurySeverity.Severe;
            }

            if (ratio >= 0.3f)
            {
                return InjurySeverity.Serious;
            }

            if (ratio >= 0.15f)
            {
                return InjurySeverity.Moderate;
            }

            return ratio >= 0.05f ? InjurySeverity.Minor : InjurySeverity.Trivial;
        }

        private static BiologicalInteractionEvaluationResult EvaluateInjuryCompatibility(
            BiologicalCompatibilityRuntime compatibility,
            BodySnapshot body,
            InjuryTypeDefinition injuryDefinition,
            AnatomyNodeSnapshot node,
            string transactionId,
            bool preview)
        {
            if (compatibility == null || body == null || injuryDefinition == null)
            {
                return null;
            }

            string interactionId = BiologicalInteractionIds.FromInjuryTypeId(injuryDefinition.Id);
            if (string.IsNullOrWhiteSpace(interactionId))
            {
                return null;
            }

            return compatibility.Evaluate(body, interactionId, BiologicalInteractionCategory.Injury, node, injuryDefinition.Id, transactionId, preview);
        }

        private static StructureFunctionalState ProjectFunctionalState(StructureConditionRecord structure, InjuryTypeDefinition injury, int projectedIntegrity)
        {
            if (structure.RuntimePresence == RuntimeStructurePresenceState.AuthoredAbsent)
            {
                return StructureFunctionalState.Absent;
            }

            if (projectedIntegrity <= 0)
            {
                return injury.CanCauseStructuralFailure ? StructureFunctionalState.Destroyed : StructureFunctionalState.Disabled;
            }

            float ratio = structure.MaximumIntegrity <= 0 ? 0f : (float)projectedIntegrity / structure.MaximumIntegrity;
            if (ratio <= 0.25f)
            {
                return StructureFunctionalState.Disabled;
            }

            if (ratio <= 0.5f)
            {
                return StructureFunctionalState.SeverelyReduced;
            }

            if (ratio < 1f)
            {
                return injury.FunctionalImpact > StructureFunctionalState.Normal ? injury.FunctionalImpact : StructureFunctionalState.Reduced;
            }

            return StructureFunctionalState.Normal;
        }

        private static StructureDamageState ProjectStructuralState(StructureConditionRecord structure, InjuryTypeDefinition injury, int projectedIntegrity)
        {
            if (projectedIntegrity <= 0 && injury.CanCauseStructuralFailure)
            {
                return injury.StructuralImpact == StructureDamageState.Severed ? StructureDamageState.Severed : StructureDamageState.Destroyed;
            }

            return injury.StructuralImpact == StructureDamageState.Unknown ? StructureDamageState.Damaged : injury.StructuralImpact;
        }

        private static RuntimeStructurePresenceState ProjectPresenceState(StructureConditionRecord structure, InjuryTypeDefinition injury, int projectedIntegrity)
        {
            if (structure.RuntimePresence == RuntimeStructurePresenceState.AuthoredAbsent)
            {
                return RuntimeStructurePresenceState.AuthoredAbsent;
            }

            if (projectedIntegrity <= 0 && injury.CanCauseRuntimeAbsence)
            {
                return injury.StructuralImpact == StructureDamageState.Severed ? RuntimeStructurePresenceState.Severed : RuntimeStructurePresenceState.Missing;
            }

            if (projectedIntegrity <= 0 && injury.CanCauseStructuralFailure)
            {
                return RuntimeStructurePresenceState.Destroyed;
            }

            return structure.RuntimePresence;
        }

        private sealed class Suppression : IDisposable
        {
            private readonly BodyConditionRuntime owner;

            public Suppression(BodyConditionRuntime owner)
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

        private sealed class StructureConditionRecord
        {
            private readonly List<string> activeInjuryIds = new List<string>();

            private StructureConditionRecord(string nodeId, string runtimeNodeId, string displayName, int maximumIntegrity, int currentIntegrity, StructureFunctionalState functionalState, StructureDamageState structuralState, RuntimeStructurePresenceState runtimePresence, long revision)
            {
                NodeId = nodeId ?? string.Empty;
                RuntimeNodeId = runtimeNodeId ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                MaximumIntegrity = Math.Max(0, maximumIntegrity);
                CurrentIntegrity = Math.Max(0, currentIntegrity);
                FunctionalState = functionalState;
                StructuralState = structuralState;
                RuntimePresence = runtimePresence;
                Revision = revision;
            }

            public string NodeId { get; }
            public string RuntimeNodeId { get; }
            public string DisplayName { get; }
            public int MaximumIntegrity { get; private set; }
            public int CurrentIntegrity { get; private set; }
            public StructureFunctionalState FunctionalState { get; private set; }
            public StructureDamageState StructuralState { get; private set; }
            public RuntimeStructurePresenceState RuntimePresence { get; private set; }
            public bool Present => RuntimePresence == RuntimeStructurePresenceState.Present;
            public long Revision { get; private set; }

            public static StructureConditionRecord CreateHealthy(AnatomyNodeSnapshot node)
            {
                bool present = node.Present;
                int maximumIntegrity = present && node.Category != AnatomyStructuralCategory.Region ? 100 : 0;
                return new StructureConditionRecord(
                    node.NodeId,
                    node.RuntimeNodeId,
                    node.DisplayName,
                    maximumIntegrity,
                    maximumIntegrity,
                    present ? StructureFunctionalState.Normal : StructureFunctionalState.Absent,
                    present ? StructureDamageState.Intact : StructureDamageState.Unknown,
                    present ? RuntimeStructurePresenceState.Present : RuntimeStructurePresenceState.AuthoredAbsent,
                    1L);
            }

            public void ApplyInjury(InjuryRecord injury, InjuryTypeDefinition definition, int damage)
            {
                if (!activeInjuryIds.Contains(injury.InjuryId, StringComparer.Ordinal))
                {
                    activeInjuryIds.Add(injury.InjuryId);
                    activeInjuryIds.Sort(StringComparer.Ordinal);
                }

                CurrentIntegrity = Math.Max(0, CurrentIntegrity - Math.Max(0, damage));
                FunctionalState = ProjectFunctionalState(this, definition, CurrentIntegrity);
                StructuralState = ProjectStructuralState(this, definition, CurrentIntegrity);
                RuntimePresence = ProjectPresenceState(this, definition, CurrentIntegrity);
                Revision++;
            }

            public void RemoveInjury(string injuryId, IEnumerable<InjuryRecord> activeInjuries)
            {
                activeInjuryIds.RemoveAll(id => string.Equals(id, injuryId, StringComparison.Ordinal));
                if (activeInjuryIds.Count == 0 && CurrentIntegrity > 0 && RuntimePresence == RuntimeStructurePresenceState.Present)
                {
                    StructuralState = CurrentIntegrity == MaximumIntegrity ? StructureDamageState.Intact : StructureDamageState.Damaged;
                    FunctionalState = CurrentIntegrity == MaximumIntegrity ? StructureFunctionalState.Normal : StructureFunctionalState.Reduced;
                }

                Revision++;
            }

            public void ApplyRecovery(int newIntegrity, StructureFunctionalState functionalState, StructureDamageState structuralState)
            {
                CurrentIntegrity = Math.Max(0, Math.Min(MaximumIntegrity, newIntegrity));
                FunctionalState = functionalState;
                StructuralState = structuralState;
                Revision++;
            }

            public void Restore(StructureConditionSaveData saveData)
            {
                MaximumIntegrity = Math.Max(0, saveData.maximumIntegrity);
                CurrentIntegrity = Math.Max(0, Math.Min(MaximumIntegrity, saveData.currentIntegrity));
                FunctionalState = saveData.functionalState;
                StructuralState = saveData.structuralState;
                RuntimePresence = saveData.runtimePresence;
                activeInjuryIds.Clear();
                activeInjuryIds.AddRange((saveData.activeInjuryIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).OrderBy(id => id, StringComparer.Ordinal));
                Revision = Math.Max(1L, saveData.revision);
            }

            public StructureConditionSaveData ToSaveData()
            {
                return new StructureConditionSaveData
                {
                    nodeId = NodeId,
                    maximumIntegrity = MaximumIntegrity,
                    currentIntegrity = CurrentIntegrity,
                    functionalState = FunctionalState,
                    structuralState = StructuralState,
                    runtimePresence = RuntimePresence,
                    activeInjuryIds = activeInjuryIds.ToArray(),
                    revision = Revision
                };
            }

            public StructureConditionSnapshot CreateSnapshot()
            {
                return new StructureConditionSnapshot(NodeId, RuntimeNodeId, DisplayName, MaximumIntegrity, CurrentIntegrity, FunctionalState, StructuralState, RuntimePresence, activeInjuryIds, Revision);
            }
        }

        private sealed class InjuryRecord
        {
            public InjuryRecord(string injuryId, string actorBodyId, string targetNodeId, string injuryDefinitionId, string sourceActorBodyId, string sourceTransactionId, string damageTypeId, InjurySeverity severity, int appliedStructuralDamage, StructureFunctionalState functionalImpact, StructureDamageState structuralImpact, InjuryRecordState state, long sequence, long revision)
            {
                InjuryId = injuryId ?? string.Empty;
                ActorBodyId = actorBodyId ?? string.Empty;
                TargetNodeId = targetNodeId ?? string.Empty;
                InjuryDefinitionId = injuryDefinitionId ?? string.Empty;
                SourceActorBodyId = sourceActorBodyId ?? string.Empty;
                SourceTransactionId = sourceTransactionId ?? string.Empty;
                DamageTypeId = damageTypeId ?? string.Empty;
                Severity = severity;
                AppliedStructuralDamage = Math.Max(0, appliedStructuralDamage);
                FunctionalImpact = functionalImpact;
                StructuralImpact = structuralImpact;
                State = state;
                Sequence = sequence;
                Revision = revision;
            }

            public string InjuryId { get; }
            public string ActorBodyId { get; }
            public string TargetNodeId { get; }
            public string InjuryDefinitionId { get; }
            public string SourceActorBodyId { get; }
            public string SourceTransactionId { get; }
            public string DamageTypeId { get; }
            public InjurySeverity Severity { get; }
            public int AppliedStructuralDamage { get; }
            public StructureFunctionalState FunctionalImpact { get; }
            public StructureDamageState StructuralImpact { get; }
            public InjuryRecordState State { get; set; }
            public long Sequence { get; }
            public long Revision { get; set; }

            public InjuryRecordSaveData ToSaveData()
            {
                return new InjuryRecordSaveData
                {
                    injuryId = InjuryId,
                    actorBodyId = ActorBodyId,
                    targetNodeId = TargetNodeId,
                    injuryDefinitionId = InjuryDefinitionId,
                    sourceActorBodyId = SourceActorBodyId,
                    sourceTransactionId = SourceTransactionId,
                    damageTypeId = DamageTypeId,
                    severity = Severity,
                    appliedStructuralDamage = AppliedStructuralDamage,
                    functionalImpact = FunctionalImpact,
                    structuralImpact = StructuralImpact,
                    state = State,
                    sequence = Sequence,
                    revision = Revision
                };
            }

            public InjuryRecordSnapshot CreateSnapshot()
            {
                return new InjuryRecordSnapshot(InjuryId, ActorBodyId, TargetNodeId, InjuryDefinitionId, SourceActorBodyId, SourceTransactionId, DamageTypeId, Severity, AppliedStructuralDamage, FunctionalImpact, StructuralImpact, State, Sequence, Revision);
            }

            public static InjuryRecord FromSaveData(InjuryRecordSaveData saveData)
            {
                return new InjuryRecord(saveData.injuryId, saveData.actorBodyId, saveData.targetNodeId, saveData.injuryDefinitionId, saveData.sourceActorBodyId, saveData.sourceTransactionId, saveData.damageTypeId, saveData.severity, saveData.appliedStructuralDamage, saveData.functionalImpact, saveData.structuralImpact, saveData.state, Math.Max(1L, saveData.sequence), Math.Max(1L, saveData.revision));
            }
        }
    }
}
