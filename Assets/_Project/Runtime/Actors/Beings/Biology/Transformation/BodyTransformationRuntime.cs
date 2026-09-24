using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Transformation
{
    public sealed class BodyTransformationRuntime : IDisposable
    {
        private const int MaximumRememberedTransactions = 64;

        private readonly Dictionary<string, BodyTransformationResult> committedResultsByTransactionId = new Dictionary<string, BodyTransformationResult>(StringComparer.Ordinal);
        private readonly List<string> processedTransactionIds = new List<string>();
        private readonly List<TransformationProfileDefinition> profiles = new List<TransformationProfileDefinition>();

        private DefinitionRegistry registry;
        private ActorBodyRuntime sourceBody;
        private BodySaveData reversionBodyState;
        private string actorBodyId;
        private string personId;
        private string activeMethodId;
        private string activeTransactionId;
        private string originalSpeciesId;
        private string transformedSpeciesId;
        private string targetBodyId;

        public event Action<BodyTransformationRuntime, BodyTransformationResult, bool> TransformationChanged;

        public TransformationReadinessState Readiness { get; private set; } = TransformationReadinessState.Uninitialized;
        public long TransformationRevision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => Readiness == TransformationReadinessState.Ready;
        public bool HasActiveTemporaryTransformation => reversionBodyState != null;
        public string ActorBodyId => actorBodyId ?? string.Empty;
        public string PersonId => personId ?? string.Empty;

        public void Configure(ActorBodyRuntime body, DefinitionRegistry definitionRegistry, bool restoring = false, bool preserveRevision = true)
        {
            sourceBody = body;
            registry = definitionRegistry ?? registry;
            actorBodyId = body == null ? string.Empty : body.ActorBodyId;
            personId = body == null ? string.Empty : body.PersonId;
            RefreshProfiles();
            if (!preserveRevision)
            {
                TransformationRevision++;
            }

            IsDirty = !restoring && IsDirty;
            Readiness = ValidateReadiness();
        }

        public BodyTransformationResult Preview(BodyTransformationRequest request, ActorBodyRuntime targetBody = null)
        {
            BodyTransformationPlan plan = BuildPlan(request?.AsPreview(), targetBody);
            return plan.Eligible
                ? BodyTransformationResult.Success("Transformation preview created without mutation.", plan, CreateSnapshot(), preview: true)
                : BodyTransformationResult.Failure(plan.Code, plan.Message, plan, CreateSnapshot(), preview: true);
        }

        public BodyTransformationResult Execute(BodyTransformationRequest request, ActorBodyRuntime targetBody = null, bool restoring = false)
        {
            if (request == null)
            {
                return BodyTransformationResult.Failure(TransformationResultCode.InvalidRequest, "Transformation request is missing.", snapshot: CreateSnapshot());
            }

            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return BodyTransformationResult.Failure(TransformationResultCode.InvalidRequest, "Transformation execution requires a transaction ID.", snapshot: CreateSnapshot());
            }

            if (committedResultsByTransactionId.TryGetValue(request.TransactionId, out BodyTransformationResult duplicateResult))
            {
                return BodyTransformationResult.Success("Duplicate transformation transaction ignored.", duplicateResult.Plan, CreateSnapshot(), duplicateResult.BodyOperation, duplicate: true);
            }

            BodyTransformationPlan plan = BuildPlan(request, targetBody);
            if (!plan.Eligible)
            {
                return BodyTransformationResult.Failure(plan.Code, plan.Message, plan, CreateSnapshot());
            }

            BodySaveData rollback = sourceBody.CreateSaveData();
            BodySaveData previousReversion = reversionBodyState;
            string previousActiveMethod = activeMethodId;
            string previousActiveTransaction = activeTransactionId;
            string previousOriginalSpecies = originalSpeciesId;
            string previousTransformedSpecies = transformedSpeciesId;
            string previousTargetBodyId = targetBodyId;
            long previousRevision = TransformationRevision;
            TransformationReadinessState previousReadiness = Readiness;

            try
            {
                Readiness = TransformationReadinessState.Executing;
                BodyOperationResult bodyOperation = ExecutePlan(plan, targetBody, restoring);
                if (bodyOperation != null && !bodyOperation.Succeeded)
                {
                    throw new InvalidOperationException(bodyOperation.Message);
                }

                if (plan.IsTemporary)
                {
                    reversionBodyState = rollback;
                    activeMethodId = plan.Method.Id;
                    activeTransactionId = request.TransactionId;
                    originalSpeciesId = plan.SourceBody.SpeciesId;
                    transformedSpeciesId = plan.TargetSpecies == null ? plan.SourceBody.SpeciesId : plan.TargetSpecies.Id;
                    targetBodyId = plan.TargetBody == null ? string.Empty : plan.TargetBody.ActorBodyId;
                }
                else if (plan.Method.ReversionPolicy == TransformationReversionPolicy.None)
                {
                    ClearActiveTemporaryState();
                }

                RememberTransaction(request.TransactionId);
                TransformationRevision++;
                Readiness = TransformationReadinessState.Ready;
                IsDirty = !restoring;
                BodyTransformationResult result = BodyTransformationResult.Success("Transformation committed.", plan, CreateSnapshot(), bodyOperation);
                committedResultsByTransactionId[request.TransactionId] = result;
                RaiseChanged(result, restoring);
                return result;
            }
            catch (Exception exception)
            {
                BodyOperationResult rollbackResult = sourceBody.RestoreFromSaveData(rollback, registry, rollback.actorBodyId, rollback.personId, restoring: true);
                reversionBodyState = previousReversion;
                activeMethodId = previousActiveMethod;
                activeTransactionId = previousActiveTransaction;
                originalSpeciesId = previousOriginalSpecies;
                transformedSpeciesId = previousTransformedSpecies;
                targetBodyId = previousTargetBodyId;
                TransformationRevision = previousRevision;
                Readiness = previousReadiness;
                IsDirty = !restoring && IsDirty;
                if (!rollbackResult.Succeeded)
                {
                    return BodyTransformationResult.Failure(TransformationResultCode.RollbackFailed, $"Transformation failed and rollback failed: {exception.Message} Rollback={rollbackResult.Message}", plan, CreateSnapshot(), rollbackResult);
                }

                return BodyTransformationResult.Failure(TransformationResultCode.ExecutionFailed, $"Transformation rolled back: {exception.Message}", plan, CreateSnapshot(), rollbackResult);
            }
        }

        public BodyTransformationResult RevertTemporaryTransformation(string transactionId, bool restoring = false)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return BodyTransformationResult.Failure(TransformationResultCode.InvalidRequest, "Transformation revert requires a transaction ID.", snapshot: CreateSnapshot());
            }

            if (committedResultsByTransactionId.TryGetValue(transactionId, out BodyTransformationResult duplicate))
            {
                return BodyTransformationResult.Success("Duplicate transformation revert ignored.", duplicate.Plan, CreateSnapshot(), duplicate.BodyOperation, duplicate: true);
            }

            if (reversionBodyState == null)
            {
                return BodyTransformationResult.Failure(TransformationResultCode.NoActiveTransformation, "No active temporary transformation exists.", snapshot: CreateSnapshot());
            }

            Readiness = TransformationReadinessState.Reverting;
            BodyOperationResult result = sourceBody.RestoreFromSaveData(reversionBodyState, registry, reversionBodyState.actorBodyId, reversionBodyState.personId, restoring: true);
            if (!result.Succeeded)
            {
                Readiness = TransformationReadinessState.Ready;
                return BodyTransformationResult.Failure(TransformationResultCode.ExecutionFailed, result.Message, snapshot: CreateSnapshot(), bodyOperation: result);
            }

            ClearActiveTemporaryState();
            RememberTransaction(transactionId);
            TransformationRevision++;
            Readiness = TransformationReadinessState.Ready;
            IsDirty = !restoring;
            BodyTransformationResult transformationResult = BodyTransformationResult.Success("Temporary transformation reverted.", null, CreateSnapshot(), result);
            committedResultsByTransactionId[transactionId] = transformationResult;
            RaiseChanged(transformationResult, restoring);
            return transformationResult;
        }

        public BodyTransformationPlan BuildPlan(BodyTransformationRequest request, ActorBodyRuntime targetBody = null)
        {
            if (request == null)
            {
                return FailurePlan(null, null, null, null, TransformationResultCode.InvalidRequest, "Transformation request is missing.");
            }

            BodySnapshot sourceSnapshot = sourceBody == null ? null : sourceBody.CreateSnapshot();
            if (sourceSnapshot == null || string.IsNullOrWhiteSpace(sourceSnapshot.ActorBodyId))
            {
                return FailurePlan(request, null, null, null, TransformationResultCode.MissingSourceBody, "Transformation requires an exact source body.");
            }

            if (!string.IsNullOrWhiteSpace(request.SourceActorBodyId) && !string.Equals(request.SourceActorBodyId, sourceSnapshot.ActorBodyId, StringComparison.Ordinal))
            {
                return FailurePlan(request, sourceSnapshot, null, null, TransformationResultCode.StaleBody, $"Request source body '{request.SourceActorBodyId}' does not match runtime body '{sourceSnapshot.ActorBodyId}'.");
            }

            if (!string.IsNullOrWhiteSpace(request.PersonId) && !string.Equals(request.PersonId, sourceSnapshot.PersonId, StringComparison.Ordinal))
            {
                return FailurePlan(request, sourceSnapshot, null, null, TransformationResultCode.MissingPerson, $"Request person '{request.PersonId}' does not match body person '{sourceSnapshot.PersonId}'.");
            }

            if (request.ExpectedBodyRevision > 0L && request.ExpectedBodyRevision != sourceSnapshot.BodyRevision)
            {
                return FailurePlan(request, sourceSnapshot, null, null, TransformationResultCode.StaleBody, $"Expected body revision {request.ExpectedBodyRevision} but current body is {sourceSnapshot.BodyRevision}.");
            }

            long currentAnatomyRevision = sourceSnapshot.Anatomy == null ? 0L : sourceSnapshot.Anatomy.AnatomyRevision;
            if (request.ExpectedAnatomyRevision > 0L && request.ExpectedAnatomyRevision != currentAnatomyRevision)
            {
                return FailurePlan(request, sourceSnapshot, null, null, TransformationResultCode.StaleDependency, $"Expected anatomy revision {request.ExpectedAnatomyRevision} but current anatomy is {currentAnatomyRevision}.");
            }

            if (request.ExpectedCompatibilityRevision > 0L && sourceSnapshot.BiologicalCompatibility != null && request.ExpectedCompatibilityRevision != sourceSnapshot.BiologicalCompatibility.CompatibilityRevision)
            {
                return FailurePlan(request, sourceSnapshot, null, null, TransformationResultCode.StaleDependency, $"Expected compatibility revision {request.ExpectedCompatibilityRevision} but current compatibility is {sourceSnapshot.BiologicalCompatibility.CompatibilityRevision}.");
            }

            if (registry == null || !registry.TryGet(request.MethodDefinitionId, out TransformationMethodDefinition method) || method == null)
            {
                return FailurePlan(request, sourceSnapshot, null, null, TransformationResultCode.MissingMethod, $"Transformation Method '{request.MethodDefinitionId}' is not registered.");
            }

            if (!method.AlphaExecutionEnabled)
            {
                return FailurePlan(request, sourceSnapshot, method, null, TransformationResultCode.UnsupportedCategory, $"Transformation Method '{method.Id}' is disabled for alpha execution.");
            }

            if (!ProfileEnables(sourceSnapshot, method.Id))
            {
                return FailurePlan(request, sourceSnapshot, method, null, TransformationResultCode.Incompatible, $"No active Transformation Profile enables method '{method.Id}'.");
            }

            SpeciesDefinition targetSpecies = ResolveTargetSpecies(request, method, sourceSnapshot, out TransformationResultCode speciesCode, out string speciesFailure);
            if (speciesCode != TransformationResultCode.Success)
            {
                return FailurePlan(request, sourceSnapshot, method, null, speciesCode, speciesFailure);
            }

            BodyFormDefinition targetBodyForm = ResolveTargetBodyForm(request, targetSpecies, sourceSnapshot, out TransformationResultCode formCode, out string formFailure);
            if (formCode != TransformationResultCode.Success)
            {
                return FailurePlan(request, sourceSnapshot, method, targetSpecies, formCode, formFailure);
            }

            BodySnapshot targetSnapshot = targetBody == null ? null : targetBody.CreateSnapshot();
            TransformationResultCode requestCode = ValidateRequestShape(request, method, sourceSnapshot, targetSnapshot);
            if (requestCode != TransformationResultCode.Success)
            {
                return FailurePlan(request, sourceSnapshot, method, targetSpecies, requestCode, $"Request shape is invalid for category '{method.Category}'.");
            }

            BiologicalInteractionEvaluationResult compatibility = EvaluateCompatibility(sourceSnapshot, method, request);
            if (compatibility == null)
            {
                return FailurePlan(request, sourceSnapshot, method, targetSpecies, TransformationResultCode.MissingCompatibility, "Compatibility evaluation did not return a result.");
            }

            if (compatibility.Code != BiologicalCompatibilityResultCode.Success)
            {
                return FailurePlan(request, sourceSnapshot, method, targetSpecies, TransformationResultCode.MissingCompatibility, compatibility.Message, compatibility);
            }

            if (compatibility.Immune)
            {
                return FailurePlan(request, sourceSnapshot, method, targetSpecies, TransformationResultCode.Immune, compatibility.Message, compatibility);
            }

            if (compatibility.Suppressed)
            {
                return FailurePlan(request, sourceSnapshot, method, targetSpecies, TransformationResultCode.Suppressed, compatibility.Message, compatibility);
            }

            if (!compatibility.Compatible)
            {
                return FailurePlan(request, sourceSnapshot, method, targetSpecies, TransformationResultCode.Incompatible, compatibility.Message, compatibility);
            }

            TransformationPlanFlags flags = BuildFlags(method, sourceSnapshot, targetSnapshot, targetSpecies, targetBodyForm);
            IReadOnlyList<BodyTransformationDecision> decisions = BuildDecisions(method, sourceSnapshot, targetSnapshot, targetSpecies, targetBodyForm);
            return new BodyTransformationPlan(request, method, sourceSnapshot, targetSnapshot, targetSpecies, targetBodyForm, compatibility, flags, decisions, true, TransformationResultCode.Success, "Transformation plan is eligible.");
        }

        public BodyTransformationSaveData CreateSaveData()
        {
            return new BodyTransformationSaveData
            {
                schemaVersion = BodyTransformationSaveData.CurrentSchemaVersion,
                actorBodyId = ActorBodyId,
                personId = PersonId,
                transformationRevision = TransformationRevision,
                activeTemporaryTransformation = HasActiveTemporaryTransformation,
                activeMethodId = activeMethodId,
                activeTransactionId = activeTransactionId,
                originalSpeciesId = originalSpeciesId,
                transformedSpeciesId = transformedSpeciesId,
                targetBodyId = targetBodyId,
                reversionBodyState = BodyTransformationReversionSaveData.FromBodySaveData(reversionBodyState),
                processedTransactionIds = processedTransactionIds.ToArray()
            };
        }

        public BodyTransformationResult RestoreFromSaveData(BodyTransformationSaveData saveData, ActorBodyRuntime body, DefinitionRegistry definitionRegistry)
        {
            if (!ValidateSaveData(saveData, body, definitionRegistry, out string failureReason))
            {
                return BodyTransformationResult.Failure(TransformationResultCode.RestoreFailed, failureReason, snapshot: CreateSnapshot());
            }

            sourceBody = body;
            registry = definitionRegistry ?? registry;
            Readiness = TransformationReadinessState.Restoring;
            actorBodyId = saveData.actorBodyId;
            personId = saveData.personId;
            TransformationRevision = Math.Max(0L, saveData.transformationRevision);
            reversionBodyState = saveData.activeTemporaryTransformation ? saveData.reversionBodyState?.ToBodySaveData() : null;
            activeMethodId = saveData.activeMethodId;
            activeTransactionId = saveData.activeTransactionId;
            originalSpeciesId = saveData.originalSpeciesId;
            transformedSpeciesId = saveData.transformedSpeciesId;
            targetBodyId = saveData.targetBodyId;
            processedTransactionIds.Clear();
            if (saveData.processedTransactionIds != null)
            {
                foreach (string id in saveData.processedTransactionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
                {
                    processedTransactionIds.Add(id);
                }
            }

            committedResultsByTransactionId.Clear();
            RefreshProfiles();
            Readiness = ValidateReadiness();
            IsDirty = false;
            return BodyTransformationResult.Success("Transformation runtime restored without replay.", null, CreateSnapshot());
        }

        public static bool ValidateSaveData(BodyTransformationSaveData saveData, ActorBodyRuntime body, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Transformation save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > BodyTransformationSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported transformation schema version {saveData.schemaVersion}.";
                return false;
            }

            if (body == null)
            {
                failureReason = "Transformation restore requires a body runtime.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(saveData.actorBodyId) && !string.Equals(saveData.actorBodyId, body.ActorBodyId, StringComparison.Ordinal))
            {
                failureReason = $"Saved transformation body '{saveData.actorBodyId}' does not match runtime body '{body.ActorBodyId}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(saveData.personId) && !string.Equals(saveData.personId, body.PersonId, StringComparison.Ordinal))
            {
                failureReason = $"Saved transformation person '{saveData.personId}' does not match runtime person '{body.PersonId}'.";
                return false;
            }

            if (saveData.activeTemporaryTransformation)
            {
                if (string.IsNullOrWhiteSpace(saveData.activeMethodId) || registry == null || !registry.TryGet(saveData.activeMethodId, out TransformationMethodDefinition method) || method == null)
                {
                    failureReason = $"Active transformation method '{saveData.activeMethodId}' is not registered.";
                    return false;
                }

                if (saveData.reversionBodyState == null)
                {
                    failureReason = "Active temporary transformation is missing reversion body state.";
                    return false;
                }

                BodySaveData reversionBody = saveData.reversionBodyState.ToBodySaveData();
                if (!ActorBodyRuntime.ValidateSaveData(reversionBody, registry, reversionBody.actorBodyId, reversionBody.personId, out failureReason))
                {
                    return false;
                }
            }

            return true;
        }

        public BodyTransformationSnapshot CreateSnapshot()
        {
            List<string> diagnostics = new List<string>();
            bool coherent = true;
            if (Readiness == TransformationReadinessState.Ready && string.IsNullOrWhiteSpace(ActorBodyId))
            {
                coherent = false;
                diagnostics.Add("Transformation runtime is Ready without an Actor/body ID.");
            }

            if (HasActiveTemporaryTransformation && string.IsNullOrWhiteSpace(activeMethodId))
            {
                coherent = false;
                diagnostics.Add("Temporary transformation is active without a method ID.");
            }

            return new BodyTransformationSnapshot(ActorBodyId, PersonId, Readiness, TransformationRevision, HasActiveTemporaryTransformation, activeMethodId, activeTransactionId, originalSpeciesId, transformedSpeciesId, targetBodyId, processedTransactionIds, IsDirty, coherent, diagnostics);
        }

        public void Dispose()
        {
            Readiness = TransformationReadinessState.Disposed;
            committedResultsByTransactionId.Clear();
            processedTransactionIds.Clear();
            profiles.Clear();
            sourceBody = null;
            registry = null;
            reversionBodyState = null;
        }

        private BodyOperationResult ExecutePlan(BodyTransformationPlan plan, ActorBodyRuntime targetBody, bool restoring)
        {
            switch (plan.Method.Category)
            {
                case TransformationCategory.TemporaryPolymorph:
                case TransformationCategory.PermanentSpeciesChange:
                case TransformationCategory.BodyFormChange:
                case TransformationCategory.Reincarnation:
                case TransformationCategory.ResurrectionBody:
                case TransformationCategory.Embodiment:
                case TransformationCategory.Disembodiment:
                    if (plan.TargetSpecies == null || string.Equals(plan.SourceBody.SpeciesId, plan.TargetSpecies.Id, StringComparison.Ordinal))
                    {
                        return BodyOperationResult.Success("Transformation did not require a Species mutation.", sourceBody.CreateSnapshot(), duplicate: true);
                    }

                    return sourceBody.AssignSpecies(plan.TargetSpecies.Id, restoring, $"Transformation {plan.Method.Id}");

                case TransformationCategory.StructureReplacement:
                case TransformationCategory.OrganReplacement:
                case TransformationCategory.LimbReplacement:
                case TransformationCategory.ConstructComponentReplacement:
                    if (string.IsNullOrWhiteSpace(plan.Request.TargetAnatomyNodeId))
                    {
                        return BodyOperationResult.Failure(BodyOperationResultCode.InvalidRequest, "Structure replacement requires an anatomy node.");
                    }

                    return sourceBody.SetAnatomyPresenceOverride(plan.Request.TargetAnatomyNodeId, AnatomyPresenceState.Present, restoring);

                case TransformationCategory.BodyReplacement:
                case TransformationCategory.BodySwap:
                case TransformationCategory.Possession:
                    return BodyOperationResult.Success("Identity association plan recorded; concrete controller/body reassignment is deferred to owning actor systems.", sourceBody.CreateSnapshot());

                default:
                    return BodyOperationResult.Failure(BodyOperationResultCode.InvalidRequest, $"Transformation category '{plan.Method.Category}' is not executable.");
            }
        }

        private BiologicalInteractionEvaluationResult EvaluateCompatibility(BodySnapshot sourceSnapshot, TransformationMethodDefinition method, BodyTransformationRequest request)
        {
            if (sourceBody == null || sourceBody.BiologicalCompatibility == null || !sourceBody.BiologicalCompatibility.IsReady)
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.RuntimeNotReady, "Transformation requires a ready biological compatibility runtime.", sourceSnapshot?.ActorBodyId, method?.BiologicalInteractionDefinitionId);
            }

            return sourceBody.BiologicalCompatibility.Evaluate(sourceSnapshot, method.BiologicalInteractionDefinitionId, BiologicalInteractionCategory.Transformation, null, request.SourceId, request.TransactionId, preview: true);
        }

        private SpeciesDefinition ResolveTargetSpecies(BodyTransformationRequest request, TransformationMethodDefinition method, BodySnapshot sourceSnapshot, out TransformationResultCode code, out string failure)
        {
            code = TransformationResultCode.Success;
            failure = string.Empty;
            string requestedSpeciesId = string.IsNullOrWhiteSpace(request.TargetSpeciesId) ? sourceSnapshot.SpeciesId : request.TargetSpeciesId;
            if (string.IsNullOrWhiteSpace(requestedSpeciesId) || registry == null || !registry.TryGet(requestedSpeciesId, out SpeciesDefinition species) || species == null)
            {
                code = TransformationResultCode.MissingTargetSpecies;
                failure = $"Target Species '{requestedSpeciesId}' is not registered.";
                return null;
            }

            if (!method.AllowSameSpecies && string.Equals(sourceSnapshot.SpeciesId, species.Id, StringComparison.Ordinal)
                && (method.Category == TransformationCategory.PermanentSpeciesChange || method.Category == TransformationCategory.TemporaryPolymorph))
            {
                code = TransformationResultCode.InvalidRequest;
                failure = $"Transformation Method '{method.Id}' does not allow same-Species transformation.";
                return null;
            }

            if (!method.AllowsTargetSpecies(species.Id))
            {
                code = TransformationResultCode.Incompatible;
                failure = $"Transformation Method '{method.Id}' does not allow target Species '{species.Id}'.";
                return null;
            }

            return species;
        }

        private BodyFormDefinition ResolveTargetBodyForm(BodyTransformationRequest request, SpeciesDefinition targetSpecies, BodySnapshot sourceSnapshot, out TransformationResultCode code, out string failure)
        {
            code = TransformationResultCode.Success;
            failure = string.Empty;
            string requestedFormId = string.IsNullOrWhiteSpace(request.TargetBodyFormId) ? targetSpecies?.BodyForm?.Id ?? sourceSnapshot.BodyFormId : request.TargetBodyFormId;
            if (string.IsNullOrWhiteSpace(requestedFormId) || registry == null || !registry.TryGet(requestedFormId, out BodyFormDefinition bodyForm) || bodyForm == null)
            {
                code = TransformationResultCode.MissingTargetBodyForm;
                failure = $"Target body form '{requestedFormId}' is not registered.";
                return null;
            }

            if (targetSpecies != null && targetSpecies.BodyForm != null && !string.Equals(targetSpecies.BodyForm.Id, bodyForm.Id, StringComparison.Ordinal))
            {
                code = TransformationResultCode.Incompatible;
                failure = $"Target Species '{targetSpecies.Id}' uses body form '{targetSpecies.BodyForm.Id}', not '{bodyForm.Id}'.";
                return null;
            }

            return bodyForm;
        }

        private TransformationResultCode ValidateRequestShape(BodyTransformationRequest request, TransformationMethodDefinition method, BodySnapshot sourceSnapshot, BodySnapshot targetSnapshot)
        {
            switch (method.Category)
            {
                case TransformationCategory.BodyReplacement:
                case TransformationCategory.BodySwap:
                case TransformationCategory.Possession:
                    return string.IsNullOrWhiteSpace(request.TargetActorBodyId) && targetSnapshot == null ? TransformationResultCode.MissingTargetBody : TransformationResultCode.Success;
                case TransformationCategory.StructureReplacement:
                case TransformationCategory.OrganReplacement:
                case TransformationCategory.LimbReplacement:
                case TransformationCategory.ConstructComponentReplacement:
                    return string.IsNullOrWhiteSpace(request.TargetAnatomyNodeId) ? TransformationResultCode.InvalidRequest : TransformationResultCode.Success;
                default:
                    return TransformationResultCode.Success;
            }
        }

        private TransformationPlanFlags BuildFlags(TransformationMethodDefinition method, BodySnapshot source, BodySnapshot target, SpeciesDefinition targetSpecies, BodyFormDefinition targetBodyForm)
        {
            TransformationPlanFlags flags = TransformationPlanFlags.None;
            if (targetSpecies != null && !string.Equals(source.SpeciesId, targetSpecies.Id, StringComparison.Ordinal))
            {
                flags |= TransformationPlanFlags.SpeciesChange;
            }

            if (targetBodyForm != null && !string.Equals(source.BodyFormId, targetBodyForm.Id, StringComparison.Ordinal))
            {
                flags |= TransformationPlanFlags.BodyFormChange;
            }

            if (target != null)
            {
                flags |= TransformationPlanFlags.ExistingBodyTarget;
            }

            if (method.Temporary)
            {
                flags |= TransformationPlanFlags.Temporary | TransformationPlanFlags.RequiresReversionState;
            }

            if (method.Category == TransformationCategory.StructureReplacement || method.Category == TransformationCategory.OrganReplacement || method.Category == TransformationCategory.LimbReplacement || method.Category == TransformationCategory.ConstructComponentReplacement)
            {
                flags |= TransformationPlanFlags.StructureReplacement;
            }

            if (method.ControllerPolicy != TransformationControllerPolicy.PreserveController)
            {
                flags |= TransformationPlanFlags.ControllerReassignment;
            }

            if (method.AssociationPolicy != TransformationAssociationPolicy.PreserveBody)
            {
                flags |= TransformationPlanFlags.PersonBodyReassociation;
            }

            return flags;
        }

        private IReadOnlyList<BodyTransformationDecision> BuildDecisions(TransformationMethodDefinition method, BodySnapshot source, BodySnapshot target, SpeciesDefinition targetSpecies, BodyFormDefinition targetBodyForm)
        {
            List<BodyTransformationDecision> decisions = new List<BodyTransformationDecision>
            {
                new BodyTransformationDecision("Person identity", TransformationStateOwnership.PersonOwned, TransformationReconciliationPolicy.PreserveIfCompatible, true, source.PersonId, "Person identity remains owned by the person system and is preserved by body transformation."),
                new BodyTransformationDecision("Anatomy", TransformationStateOwnership.BodyOwned, method.AnatomyPolicy, false, targetSpecies?.AnatomyDefinition?.Id ?? source.Anatomy?.AnatomyDefinitionId ?? string.Empty, "Anatomy is rebuilt or remapped by stable anatomy-node IDs."),
                new BodyTransformationDecision("Body Condition", TransformationStateOwnership.BodyOwned, method.ConditionPolicy, false, source.ActorBodyId, "Injuries and structural integrity are body-owned and do not transfer by default."),
                new BodyTransformationDecision("Vital Processes", TransformationStateOwnership.BodyOwned, method.VitalPolicy, false, source.ActorBodyId, "Biological resources are body-owned and are reinitialized or restored by policy."),
                new BodyTransformationDecision("Biological Hazards", TransformationStateOwnership.BodyOwned, method.HazardPolicy, false, source.ActorBodyId, "Hazards are body-owned and compatibility-gated."),
                new BodyTransformationDecision("Recovery Processes", TransformationStateOwnership.BodyOwned, method.RecoveryPolicy, false, source.ActorBodyId, "Recovery progress is exact-body-owned and never silently transfers."),
                new BodyTransformationDecision("Traits and Capabilities", TransformationStateOwnership.PersonOwned, TransformationReconciliationPolicy.Rebuild, true, targetSpecies?.Id ?? source.SpeciesId, "Species/classification sources are rebuilt while learned/person-owned sources remain."),
                new BodyTransformationDecision("Equipment", TransformationStateOwnership.PersonOwned, TransformationReconciliationPolicy.PreserveIfCompatible, true, source.PersonId, $"Equipment policy is {method.EquipmentPolicy}."),
                new BodyTransformationDecision("Lifecycle", TransformationStateOwnership.ActorOwned, TransformationReconciliationPolicy.PreserveIfCompatible, true, source.ActorBodyId, $"Lifecycle policy is {method.LifecyclePolicy}."),
                new BodyTransformationDecision("Controller", TransformationStateOwnership.ControllerOwned, TransformationReconciliationPolicy.PreserveIfCompatible, true, source.PersonId, $"Controller policy is {method.ControllerPolicy}.")
            };

            if (target != null)
            {
                decisions.Add(new BodyTransformationDecision("Target body", TransformationStateOwnership.BodyOwned, TransformationReconciliationPolicy.PreserveIfCompatible, false, target.ActorBodyId, "Existing target body keeps its own body-owned state unless an owning actor system commits reassociation."));
            }

            return decisions;
        }

        private BodyTransformationPlan FailurePlan(BodyTransformationRequest request, BodySnapshot source, TransformationMethodDefinition method, SpeciesDefinition targetSpecies, TransformationResultCode code, string message, BiologicalInteractionEvaluationResult compatibility = null)
        {
            BodyFormDefinition targetBodyForm = targetSpecies == null ? null : targetSpecies.BodyForm;
            return new BodyTransformationPlan(request, method, source, null, targetSpecies, targetBodyForm, compatibility, TransformationPlanFlags.None, Array.Empty<BodyTransformationDecision>(), false, code, message);
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
                string removed = processedTransactionIds[0];
                processedTransactionIds.RemoveAt(0);
                committedResultsByTransactionId.Remove(removed);
            }
        }

        private void ClearActiveTemporaryState()
        {
            reversionBodyState = null;
            activeMethodId = string.Empty;
            activeTransactionId = string.Empty;
            originalSpeciesId = string.Empty;
            transformedSpeciesId = string.Empty;
            targetBodyId = string.Empty;
        }

        private bool ProfileEnables(BodySnapshot body, string methodId)
        {
            RefreshProfiles();
            TransformationProfileDefinition[] matching = profiles.Where(profile => profile.AppliesTo(body)).ToArray();
            return matching.Length == 0 || matching.Any(profile => profile.EnablesMethod(methodId));
        }

        private void RefreshProfiles()
        {
            profiles.Clear();
            profiles.AddRange(registry?.DefinitionsById.Values.OfType<TransformationProfileDefinition>().Where(profile => profile != null).OrderBy(profile => profile.Id, StringComparer.Ordinal) ?? Enumerable.Empty<TransformationProfileDefinition>());
        }

        private TransformationReadinessState ValidateReadiness()
        {
            if (sourceBody == null || string.IsNullOrWhiteSpace(sourceBody.ActorBodyId))
            {
                return TransformationReadinessState.WaitingForSourceBody;
            }

            if (string.IsNullOrWhiteSpace(sourceBody.PersonId))
            {
                return TransformationReadinessState.WaitingForPerson;
            }

            if (registry == null)
            {
                return TransformationReadinessState.WaitingForDefinitions;
            }

            if (sourceBody.BiologicalCompatibility == null || !sourceBody.BiologicalCompatibility.IsReady)
            {
                return TransformationReadinessState.WaitingForCompatibility;
            }

            return TransformationReadinessState.Ready;
        }

        private void RaiseChanged(BodyTransformationResult result, bool restoring)
        {
            if (!restoring)
            {
                TransformationChanged?.Invoke(this, result, restoring);
            }
        }
    }
}
