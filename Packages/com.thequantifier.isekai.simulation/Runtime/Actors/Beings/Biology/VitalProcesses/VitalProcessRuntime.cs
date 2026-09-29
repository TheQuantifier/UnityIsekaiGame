using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.VitalProcesses
{
    public sealed class VitalProcessRuntime
    {
        private readonly Dictionary<string, VitalResourceRecord> resourcesById = new Dictionary<string, VitalResourceRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> committedTransactionIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string actorBodyId;
        private string speciesId;
        private string profileId;
        private long bodyRevision;
        private long anatomyRevision;
        private long conditionRevision;
        private bool suppressEvents;

        public event Action<VitalProcessRuntime, VitalResourceMutationResult, bool> VitalResourceChanged;

        public VitalProcessReadinessState Readiness { get; private set; } = VitalProcessReadinessState.Uninitialized;
        public long VitalRevision { get; private set; }
        public bool IsDirty { get; private set; }
        public string ActorBodyId => actorBodyId ?? string.Empty;
        public string SpeciesId => speciesId ?? string.Empty;
        public string ProfileId => profileId ?? string.Empty;
        public bool IsReady => Readiness == VitalProcessReadinessState.Ready;

        public VitalResourceMutationResult BuildForBody(string exactActorBodyId, SpeciesDefinition species, AnatomySnapshot anatomy, BodyConditionSnapshot condition, DefinitionRegistry definitionRegistry, bool restoring = false, bool preserveRevision = false)
        {
            registry = definitionRegistry ?? registry;
            if (string.IsNullOrWhiteSpace(exactActorBodyId))
            {
                Readiness = VitalProcessReadinessState.WaitingForBody;
                return VitalResourceMutationResult.Failure(default, VitalProcessResultCode.MissingActorBody, "Vital processes require an exact Actor/body ID.", CreateSnapshot());
            }

            if (species == null)
            {
                Readiness = VitalProcessReadinessState.ResolvingProfile;
                return VitalResourceMutationResult.Failure(default, VitalProcessResultCode.MissingProfile, "Vital processes require a Species definition.", CreateSnapshot());
            }

            if (anatomy == null || !anatomy.Coherent)
            {
                Readiness = VitalProcessReadinessState.WaitingForAnatomy;
                return VitalResourceMutationResult.Failure(default, VitalProcessResultCode.MissingAnatomy, "Vital processes require a coherent Anatomy snapshot.", CreateSnapshot());
            }

            if (condition == null || !condition.Coherent)
            {
                Readiness = VitalProcessReadinessState.WaitingForCondition;
                return VitalResourceMutationResult.Failure(default, VitalProcessResultCode.MissingBodyCondition, "Vital processes require a coherent Body Condition snapshot.", CreateSnapshot());
            }

            VitalProcessProfileDefinition profile = ResolveProfile(species);
            if (profile == null)
            {
                Readiness = VitalProcessReadinessState.ResolvingProfile;
                return VitalResourceMutationResult.Failure(default, VitalProcessResultCode.MissingProfile, $"No Vital Process Profile resolves for Species '{species.Id}'.", CreateSnapshot());
            }

            actorBodyId = exactActorBodyId;
            speciesId = species.Id;
            profileId = profile.Id;
            bodyRevision = anatomy.BodyRevision;
            anatomyRevision = anatomy.AnatomyRevision;
            conditionRevision = condition.ConditionRevision;
            resourcesById.Clear();
            committedTransactionIds.Clear();
            Readiness = restoring ? VitalProcessReadinessState.Restoring : VitalProcessReadinessState.ResolvingProfile;

            foreach (VitalResourceProfileEntry entry in profile.Resources.OrderBy(entry => entry.ResourceDefinitionId, StringComparer.Ordinal))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceDefinitionId))
                {
                    continue;
                }

                registry.TryGet(entry.ResourceDefinitionId, out BiologicalResourceDefinition definition);
                if (definition == null)
                {
                    continue;
                }

                resourcesById[entry.ResourceDefinitionId] = VitalResourceRecord.Create(definition, entry);
            }

            RecalculateCapacities(anatomy, condition, preservingCurrent: true);
            if (!preserveRevision)
            {
                VitalRevision++;
            }

            IsDirty = !restoring;
            Readiness = VitalProcessReadinessState.Ready;
            VitalResourceMutationRequest request = new VitalResourceMutationRequest(actorBodyId, string.Empty, VitalResourceMutationOperation.Set, 0f, "vital.build", "vital.process", "Build vital processes");
            return VitalResourceMutationResult.Success(request, 0f, 0f, 0f, VitalProcessState.Normal, VitalProcessState.Normal, preview: false, duplicate: false, CreateSnapshot());
        }

        public VitalResourceMutationResult PreviewMutation(VitalResourceMutationRequest request, AnatomySnapshot anatomy, BodyConditionSnapshot condition)
        {
            return ResolveMutation(request, anatomy, condition, preview: true, out _, out _, out _, out _);
        }

        public VitalResourceMutationResult ApplyMutation(VitalResourceMutationRequest request, AnatomySnapshot anatomy, BodyConditionSnapshot condition, bool restoring = false)
        {
            VitalResourceMutationResult duplicate = TryResolveDuplicate(request);
            if (duplicate != null)
            {
                return duplicate;
            }

            VitalResourceMutationResult resolved = ResolveMutation(request, anatomy, condition, preview: false, out VitalResourceRecord resource, out float newValue, out float appliedAmount, out VitalProcessState newState);
            if (!resolved.Succeeded)
            {
                return resolved;
            }

            VitalProcessState previousState = resource.State;
            float previousValue = resource.CurrentValue;
            resource.CurrentValue = newValue;
            resource.State = newState;
            resource.Revision++;
            if (!string.IsNullOrWhiteSpace(request.TransactionId))
            {
                committedTransactionIds.Add(request.TransactionId);
            }

            VitalRevision++;
            IsDirty = !restoring;
            VitalResourceMutationResult result = VitalResourceMutationResult.Success(request, previousValue, newValue, appliedAmount, previousState, newState, preview: false, duplicate: false, CreateSnapshot());
            RaiseChanged(result, restoring);
            return result;
        }

        public VitalResourceMutationResult PreviewProcessUpdate(float elapsedGameSeconds, string transactionId, AnatomySnapshot anatomy, BodyConditionSnapshot condition)
        {
            return ApplyProcessUpdate(elapsedGameSeconds, transactionId, anatomy, condition, preview: true);
        }

        public VitalResourceMutationResult ApplyProcessUpdate(float elapsedGameSeconds, string transactionId, AnatomySnapshot anatomy, BodyConditionSnapshot condition, bool preview = false, bool restoring = false)
        {
            if (!IsReady)
            {
                return VitalResourceMutationResult.Failure(default, VitalProcessResultCode.RuntimeNotReady, "Vital process runtime is not Ready.", CreateSnapshot());
            }

            if (elapsedGameSeconds < 0f || float.IsNaN(elapsedGameSeconds) || float.IsInfinity(elapsedGameSeconds))
            {
                return VitalResourceMutationResult.Failure(default, VitalProcessResultCode.InvalidAmount, "Elapsed game seconds must be finite and non-negative.", CreateSnapshot());
            }

            if (!preview && !string.IsNullOrWhiteSpace(transactionId) && !committedTransactionIds.Add(transactionId))
            {
                VitalResourceMutationRequest duplicateRequest = new VitalResourceMutationRequest(ActorBodyId, string.Empty, VitalResourceMutationOperation.Adjust, elapsedGameSeconds, transactionId, "vital.process", "Duplicate process update");
                return VitalResourceMutationResult.Success(duplicateRequest, 0f, 0f, 0f, VitalProcessState.Normal, VitalProcessState.Normal, preview: false, duplicate: true, CreateSnapshot());
            }

            VitalProcessSaveData previewState = preview ? CreateSaveData() : null;
            bool dirtyBeforePreview = IsDirty;
            RecalculateCapacities(anatomy, condition, preservingCurrent: true);
            float hours = elapsedGameSeconds / 3600f;
            bool changed = false;
            foreach (VitalResourceRecord resource in resourcesById.Values.OrderBy(resource => resource.ResourceId, StringComparer.Ordinal))
            {
                if (!resource.Active || Mathf.Approximately(hours, 0f))
                {
                    continue;
                }

                float delta = 0f;
                if (resource.ModelType == BiologicalResourceModelType.DepletingPool)
                {
                    delta = -resource.ConsumptionPerHour * hours;
                }
                else if (resource.ModelType == BiologicalResourceModelType.AccumulatingNeed)
                {
                    delta = resource.ConsumptionPerHour * hours;
                }

                if (Mathf.Approximately(delta, 0f))
                {
                    continue;
                }

                float old = resource.CurrentValue;
                float projected = ClampValue(resource, old + delta);
                if (!preview && !Mathf.Approximately(old, projected))
                {
                    resource.CurrentValue = projected;
                    resource.State = Classify(resource, projected);
                    resource.Revision++;
                    changed = true;
                }
                else if (preview && !Mathf.Approximately(old, projected))
                {
                    changed = true;
                }
            }

            VitalResourceMutationRequest request = new VitalResourceMutationRequest(ActorBodyId, string.Empty, VitalResourceMutationOperation.Adjust, elapsedGameSeconds, transactionId, "vital.process", "Deterministic vital process update");
            if (!preview && changed)
            {
                VitalRevision++;
                IsDirty = !restoring;
            }

            VitalResourceMutationResult result = VitalResourceMutationResult.Success(request, 0f, 0f, 0f, VitalProcessState.Normal, VitalProcessState.Normal, preview, duplicate: false, CreateSnapshot());
            if (preview)
            {
                RestoreRuntimeState(previewState, dirtyBeforePreview);
            }

            if (!preview && changed)
            {
                RaiseChanged(result, restoring);
            }

            return result;
        }

        public void RecalculateCapacities(AnatomySnapshot anatomy, BodyConditionSnapshot condition, bool preservingCurrent)
        {
            if (anatomy != null)
            {
                bodyRevision = anatomy.BodyRevision;
                anatomyRevision = anatomy.AnatomyRevision;
            }

            if (condition != null)
            {
                conditionRevision = condition.ConditionRevision;
            }

            bool changed = false;
            foreach (VitalResourceRecord resource in resourcesById.Values)
            {
                float previousCurrent = resource.CurrentValue;
                float previousEffectiveMaximum = resource.EffectiveMaximumValue;
                VitalProcessState previousState = resource.State;
                resource.RecalculateCapacity(anatomy, condition, preservingCurrent);
                if (!Mathf.Approximately(previousCurrent, resource.CurrentValue)
                    || !Mathf.Approximately(previousEffectiveMaximum, resource.EffectiveMaximumValue)
                    || previousState != resource.State)
                {
                    resource.Revision++;
                    changed = true;
                }
            }

            if (changed)
            {
                VitalRevision++;
                IsDirty = true;
            }
        }

        public VitalProcessSaveData CreateSaveData()
        {
            return new VitalProcessSaveData
            {
                schemaVersion = VitalProcessSaveData.CurrentSchemaVersion,
                actorBodyId = ActorBodyId,
                speciesDefinitionId = SpeciesId,
                profileDefinitionId = ProfileId,
                bodyRevision = bodyRevision,
                anatomyRevision = anatomyRevision,
                conditionRevision = conditionRevision,
                vitalRevision = VitalRevision,
                resources = resourcesById.Values.OrderBy(resource => resource.ResourceId, StringComparer.Ordinal).Select(resource => resource.ToSaveData()).ToArray(),
                committedTransactionIds = committedTransactionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            };
        }

        public VitalResourceMutationResult RestoreFromSaveData(VitalProcessSaveData saveData, SpeciesDefinition species, AnatomySnapshot anatomy, BodyConditionSnapshot condition, DefinitionRegistry definitionRegistry)
        {
            if (!ValidateSaveData(saveData, species, anatomy, condition, definitionRegistry, out string failureReason))
            {
                return VitalResourceMutationResult.Failure(default, VitalProcessResultCode.InvalidRestore, failureReason, CreateSnapshot());
            }

            registry = definitionRegistry ?? registry;
            using (SuppressEvents())
            {
                VitalResourceMutationResult build = BuildForBody(saveData.actorBodyId, species, anatomy, condition, registry, restoring: true, preserveRevision: true);
                if (!build.Succeeded)
                {
                    return build;
                }

                foreach (VitalResourceSaveData resourceData in saveData.resources ?? Array.Empty<VitalResourceSaveData>())
                {
                    if (resourceData == null || !resourcesById.TryGetValue(resourceData.resourceDefinitionId, out VitalResourceRecord resource))
                    {
                        continue;
                    }

                    resource.Restore(resourceData);
                }

                committedTransactionIds.Clear();
                foreach (string transactionId in saveData.committedTransactionIds ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(transactionId))
                    {
                        committedTransactionIds.Add(transactionId);
                    }
                }

                VitalRevision = Math.Max(1L, saveData.vitalRevision);
                bodyRevision = saveData.bodyRevision;
                anatomyRevision = saveData.anatomyRevision;
                conditionRevision = saveData.conditionRevision;
                Readiness = VitalProcessReadinessState.Ready;
                IsDirty = false;
            }

            return VitalResourceMutationResult.Success(new VitalResourceMutationRequest(ActorBodyId, string.Empty, VitalResourceMutationOperation.Set, 0f, "vital.restore", "vital.process", "Restore vital processes"), 0f, 0f, 0f, VitalProcessState.Normal, VitalProcessState.Normal, preview: false, duplicate: false, CreateSnapshot());
        }

        public static bool ValidateSaveData(VitalProcessSaveData saveData, SpeciesDefinition species, AnatomySnapshot anatomy, BodyConditionSnapshot condition, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Vital process save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > VitalProcessSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported vital process schema version {saveData.schemaVersion}.";
                return false;
            }

            if (species == null || !string.Equals(saveData.speciesDefinitionId, species.Id, StringComparison.Ordinal))
            {
                failureReason = "Saved vital process Species does not match restored body Species.";
                return false;
            }

            if (anatomy == null || condition == null || !string.Equals(saveData.actorBodyId, anatomy.ActorBodyId, StringComparison.Ordinal) || !string.Equals(saveData.actorBodyId, condition.ActorBodyId, StringComparison.Ordinal))
            {
                failureReason = "Saved vital process body does not match restored anatomy and condition.";
                return false;
            }

            HashSet<string> resourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (VitalResourceSaveData resource in saveData.resources ?? Array.Empty<VitalResourceSaveData>())
            {
                if (resource == null || string.IsNullOrWhiteSpace(resource.resourceDefinitionId) || !resourceIds.Add(resource.resourceDefinitionId))
                {
                    failureReason = "Saved vital process resources contain a missing or duplicate resource ID.";
                    return false;
                }

                if (registry == null || !registry.TryGet(resource.resourceDefinitionId, out BiologicalResourceDefinition definition) || definition == null)
                {
                    failureReason = $"Saved vital resource '{resource.resourceDefinitionId}' does not resolve.";
                    return false;
                }

                if (resource.absoluteMaximum < resource.absoluteMinimum || resource.currentValue < resource.absoluteMinimum - 0.001f || resource.currentValue > resource.absoluteMaximum + 0.001f)
                {
                    failureReason = $"Saved vital resource '{resource.resourceDefinitionId}' has invalid bounds or current value.";
                    return false;
                }
            }

            return true;
        }

        public VitalProcessSnapshot CreateSnapshot()
        {
            List<string> diagnostics = new List<string>();
            bool coherent = ValidateRuntime(out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                diagnostics.Add(failureReason);
            }

            IReadOnlyList<VitalResourceSnapshot> resources = resourcesById.Values
                .OrderBy(resource => resource.ResourceId, StringComparer.Ordinal)
                .Select(resource => resource.CreateSnapshot())
                .ToArray();
            return new VitalProcessSnapshot(ActorBodyId, SpeciesId, ProfileId, Readiness, bodyRevision, anatomyRevision, conditionRevision, VitalRevision, resources, CalculateLifecyclePressure(resources), IsDirty, coherent, diagnostics);
        }

        public bool TryGetResource(string resourceId, out VitalResourceSnapshot snapshot)
        {
            snapshot = null;
            if (!resourcesById.TryGetValue(resourceId ?? string.Empty, out VitalResourceRecord resource))
            {
                return false;
            }

            snapshot = resource.CreateSnapshot();
            return true;
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        public void Dispose()
        {
            Readiness = VitalProcessReadinessState.Disposed;
            resourcesById.Clear();
            committedTransactionIds.Clear();
        }

        private VitalResourceMutationResult ResolveMutation(VitalResourceMutationRequest request, AnatomySnapshot anatomy, BodyConditionSnapshot condition, bool preview, out VitalResourceRecord resource, out float newValue, out float appliedAmount, out VitalProcessState newState)
        {
            resource = null;
            newValue = 0f;
            appliedAmount = 0f;
            newState = VitalProcessState.Invalid;
            if (!IsReady)
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.RuntimeNotReady, "Vital process runtime is not Ready.", CreateSnapshot());
            }

            if (string.IsNullOrWhiteSpace(request.ActorBodyId) || !string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.StaleBody, $"Request body '{request.ActorBodyId}' does not match runtime body '{ActorBodyId}'.", CreateSnapshot());
            }

            if (anatomy == null || !anatomy.Coherent)
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.MissingAnatomy, "Vital resource mutation requires a coherent Anatomy snapshot.", CreateSnapshot());
            }

            if (condition == null || !condition.Coherent)
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.MissingBodyCondition, "Vital resource mutation requires a coherent Body Condition snapshot.", CreateSnapshot());
            }

            if (request.ExpectedBodyRevision > 0L && request.ExpectedBodyRevision != anatomy.BodyRevision)
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.StaleBody, $"Expected body revision {request.ExpectedBodyRevision} but provided Anatomy is {anatomy.BodyRevision}.", CreateSnapshot());
            }

            if (request.ExpectedAnatomyRevision > 0L && request.ExpectedAnatomyRevision != anatomy.AnatomyRevision)
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.StaleAnatomy, $"Expected anatomy revision {request.ExpectedAnatomyRevision} but provided Anatomy is {anatomy.AnatomyRevision}.", CreateSnapshot());
            }

            if (request.ExpectedConditionRevision > 0L && request.ExpectedConditionRevision != condition.ConditionRevision)
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.StaleCondition, $"Expected condition revision {request.ExpectedConditionRevision} but provided Body Condition is {condition.ConditionRevision}.", CreateSnapshot());
            }

            if (!resourcesById.TryGetValue(request.ResourceId ?? string.Empty, out resource))
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.MissingResource, $"Vital resource '{request.ResourceId}' is not configured.", CreateSnapshot());
            }

            if (!resource.Active)
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.InactiveResource, $"Vital resource '{resource.ResourceId}' is inactive for body '{ActorBodyId}'.", CreateSnapshot());
            }

            if (float.IsNaN(request.Amount) || float.IsInfinity(request.Amount))
            {
                return VitalResourceMutationResult.Failure(request, VitalProcessResultCode.InvalidAmount, "Vital resource mutation amount must be finite.", CreateSnapshot());
            }

            VitalProcessSaveData previewState = preview ? CreateSaveData() : null;
            bool dirtyBeforePreview = IsDirty;
            RecalculateCapacities(anatomy, condition, preservingCurrent: true);
            float target = CalculateTarget(resource, request);
            newValue = ClampValue(resource, target);
            appliedAmount = Mathf.Abs(newValue - resource.CurrentValue);
            newState = Classify(resource, newValue);
            VitalResourceMutationResult result = VitalResourceMutationResult.Success(request, resource.CurrentValue, newValue, appliedAmount, resource.State, newState, preview, duplicate: false, CreateSnapshot());
            if (preview)
            {
                RestoreRuntimeState(previewState, dirtyBeforePreview);
            }

            return result;
        }

        private VitalResourceMutationResult TryResolveDuplicate(VitalResourceMutationRequest request)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(request.TransactionId) || !committedTransactionIds.Contains(request.TransactionId) || !string.Equals(request.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return null;
            }

            return VitalResourceMutationResult.Success(request, 0f, 0f, 0f, VitalProcessState.Normal, VitalProcessState.Normal, preview: false, duplicate: true, CreateSnapshot());
        }

        private VitalProcessProfileDefinition ResolveProfile(SpeciesDefinition species)
        {
            if (registry == null || species == null)
            {
                return null;
            }

            return registry.DefinitionsById.Values
                .OfType<VitalProcessProfileDefinition>()
                .Where(profile => profile.IsCompatibleWith(species))
                .OrderBy(profile => profile.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private void RestoreRuntimeState(VitalProcessSaveData saveData, bool isDirty)
        {
            if (saveData == null)
            {
                return;
            }

            actorBodyId = saveData.actorBodyId;
            speciesId = saveData.speciesDefinitionId;
            profileId = saveData.profileDefinitionId;
            bodyRevision = saveData.bodyRevision;
            anatomyRevision = saveData.anatomyRevision;
            conditionRevision = saveData.conditionRevision;
            VitalRevision = saveData.vitalRevision;
            committedTransactionIds.Clear();
            foreach (string transactionId in saveData.committedTransactionIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(transactionId))
                {
                    committedTransactionIds.Add(transactionId);
                }
            }

            foreach (VitalResourceSaveData resourceData in saveData.resources ?? Array.Empty<VitalResourceSaveData>())
            {
                if (resourceData != null && resourcesById.TryGetValue(resourceData.resourceDefinitionId, out VitalResourceRecord resource))
                {
                    resource.Restore(resourceData);
                }
            }

            Readiness = VitalProcessReadinessState.Ready;
            IsDirty = isDirty;
        }

        private bool ValidateRuntime(out string failureReason)
        {
            failureReason = string.Empty;
            if (Readiness == VitalProcessReadinessState.Disposed)
            {
                failureReason = "Vital process runtime is disposed.";
                return false;
            }

            if (Readiness == VitalProcessReadinessState.Uninitialized)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(ActorBodyId))
            {
                failureReason = "Vital process runtime is missing an exact Actor/body ID.";
                return false;
            }

            foreach (VitalResourceRecord resource in resourcesById.Values)
            {
                if (!resource.IsCoherent(out failureReason))
                {
                    return false;
                }
            }

            return Readiness == VitalProcessReadinessState.Ready;
        }

        private void RaiseChanged(VitalResourceMutationResult result, bool restoring)
        {
            if (!suppressEvents)
            {
                VitalResourceChanged?.Invoke(this, result, restoring);
            }
        }

        private IDisposable SuppressEvents()
        {
            suppressEvents = true;
            return new Suppression(this);
        }

        private static float CalculateTarget(VitalResourceRecord resource, VitalResourceMutationRequest request)
        {
            switch (request.Operation)
            {
                case VitalResourceMutationOperation.Consume:
                    return resource.ModelType == BiologicalResourceModelType.AccumulatingNeed ? resource.CurrentValue + Mathf.Abs(request.Amount) : resource.CurrentValue - Mathf.Abs(request.Amount);
                case VitalResourceMutationOperation.Restore:
                    return resource.ModelType == BiologicalResourceModelType.AccumulatingNeed ? resource.CurrentValue - Mathf.Abs(request.Amount) : resource.CurrentValue + Mathf.Abs(request.Amount);
                case VitalResourceMutationOperation.Adjust:
                    return resource.CurrentValue + request.Amount;
                case VitalResourceMutationOperation.Set:
                    return request.Amount;
                default:
                    return resource.CurrentValue;
            }
        }

        private static float ClampValue(VitalResourceRecord resource, float value)
        {
            float max = resource.ModelType == BiologicalResourceModelType.TargetCenteredValue ? resource.AbsoluteMaximum : Math.Min(resource.AbsoluteMaximum, resource.EffectiveMaximumValue);
            return Mathf.Clamp(value, resource.AbsoluteMinimum, max);
        }

        private static VitalProcessState Classify(VitalResourceRecord resource, float value)
        {
            if (!resource.Active || resource.ModelType == BiologicalResourceModelType.Inactive)
            {
                return VitalProcessState.Inactive;
            }

            if (resource.ModelType == BiologicalResourceModelType.TargetCenteredValue)
            {
                if (value <= resource.CriticalLow)
                {
                    return VitalProcessState.CriticalLow;
                }

                if (value >= resource.CriticalHigh)
                {
                    return VitalProcessState.CriticalHigh;
                }

                if (value < resource.SafeMinimum || value <= resource.StrainedLow)
                {
                    return VitalProcessState.StrainedLow;
                }

                if (value > resource.SafeMaximum || value >= resource.StrainedHigh)
                {
                    return VitalProcessState.StrainedHigh;
                }

                return VitalProcessState.Normal;
            }

            if (resource.ModelType == BiologicalResourceModelType.AccumulatingNeed)
            {
                if (value >= resource.CriticalHigh)
                {
                    return VitalProcessState.CriticalHigh;
                }

                return value >= resource.StrainedHigh ? VitalProcessState.StrainedHigh : VitalProcessState.Normal;
            }

            if (value <= resource.CriticalLow)
            {
                return VitalProcessState.CriticalLow;
            }

            return value <= resource.StrainedLow ? VitalProcessState.StrainedLow : VitalProcessState.Normal;
        }

        private static LifecyclePressureFlags CalculateLifecyclePressure(IReadOnlyList<VitalResourceSnapshot> resources)
        {
            LifecyclePressureFlags flags = LifecyclePressureFlags.None;
            foreach (VitalResourceSnapshot resource in resources ?? Array.Empty<VitalResourceSnapshot>())
            {
                if (!resource.Critical)
                {
                    continue;
                }

                if (resource.ResourceId == BiologicalResourceIds.Blood)
                {
                    flags |= LifecyclePressureFlags.BloodCritical;
                }
                else if (resource.ResourceId == BiologicalResourceIds.Breath)
                {
                    flags |= LifecyclePressureFlags.BreathCritical;
                }
                else if (resource.ResourceId == BiologicalResourceIds.Temperature)
                {
                    flags |= LifecyclePressureFlags.TemperatureCritical;
                }
                else if (resource.ResourceId == BiologicalResourceIds.Nutrition)
                {
                    flags |= LifecyclePressureFlags.NutritionCritical;
                }
                else if (resource.ResourceId == BiologicalResourceIds.Hydration)
                {
                    flags |= LifecyclePressureFlags.HydrationCritical;
                }
                else if (resource.ResourceId == BiologicalResourceIds.SleepNeed)
                {
                    flags |= LifecyclePressureFlags.SleepNeedCritical;
                }
                else if (resource.ResourceId == BiologicalResourceIds.Fatigue)
                {
                    flags |= LifecyclePressureFlags.FatigueCritical;
                }
            }

            return flags;
        }

        private sealed class Suppression : IDisposable
        {
            private readonly VitalProcessRuntime owner;

            public Suppression(VitalProcessRuntime owner)
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

        private sealed class VitalResourceRecord
        {
            private readonly List<VitalCapacityContributionSnapshot> capacityContributions = new List<VitalCapacityContributionSnapshot>();

            private VitalResourceRecord(BiologicalResourceDefinition definition, VitalResourceProfileEntry entry)
            {
                Definition = definition;
                ResourceId = definition.Id;
                DisplayName = definition.DisplayName;
                ModelType = entry.Active ? definition.ModelType : BiologicalResourceModelType.Inactive;
                Active = entry.Active;
                MinimumValue = entry.MinimumValue;
                MaximumValue = Math.Max(entry.MinimumValue, entry.MaximumValue);
                EffectiveMaximumValue = MaximumValue;
                CurrentValue = entry.Active ? Mathf.Clamp(entry.InitialValue, entry.AbsoluteMinimum, entry.AbsoluteMaximum) : 0f;
                IdealValue = entry.IdealValue;
                SafeMinimum = entry.SafeMinimum;
                SafeMaximum = entry.SafeMaximum;
                StrainedLow = entry.StrainedLow;
                StrainedHigh = entry.StrainedHigh;
                CriticalLow = entry.CriticalLow;
                CriticalHigh = entry.CriticalHigh;
                AbsoluteMinimum = entry.AbsoluteMinimum;
                AbsoluteMaximum = Math.Max(entry.AbsoluteMinimum, entry.AbsoluteMaximum);
                ConsumptionPerHour = Math.Max(0f, entry.ConsumptionPerHour);
                RestorationPerHour = Math.Max(0f, entry.RestorationPerHour);
                State = Classify(this, CurrentValue);
                Revision = 1L;
            }

            public BiologicalResourceDefinition Definition { get; }
            public string ResourceId { get; }
            public string DisplayName { get; }
            public BiologicalResourceModelType ModelType { get; private set; }
            public bool Active { get; private set; }
            public float CurrentValue { get; set; }
            public float MinimumValue { get; private set; }
            public float MaximumValue { get; private set; }
            public float EffectiveMaximumValue { get; private set; }
            public float IdealValue { get; private set; }
            public float SafeMinimum { get; private set; }
            public float SafeMaximum { get; private set; }
            public float StrainedLow { get; private set; }
            public float StrainedHigh { get; private set; }
            public float CriticalLow { get; private set; }
            public float CriticalHigh { get; private set; }
            public float AbsoluteMinimum { get; private set; }
            public float AbsoluteMaximum { get; private set; }
            public float ConsumptionPerHour { get; private set; }
            public float RestorationPerHour { get; private set; }
            public VitalProcessState State { get; set; }
            public long Revision { get; set; }

            public static VitalResourceRecord Create(BiologicalResourceDefinition definition, VitalResourceProfileEntry entry)
            {
                return new VitalResourceRecord(definition, entry);
            }

            public void RecalculateCapacity(AnatomySnapshot anatomy, BodyConditionSnapshot condition, bool preservingCurrent)
            {
                capacityContributions.Clear();
                capacityContributions.Add(new VitalCapacityContributionSnapshot($"vital.profile.{ResourceId}", ResourceId, MaximumValue, "Profile base capacity"));
                float factor = 1f;
                if (Active && ResourceId == BiologicalResourceIds.Breath)
                {
                    factor -= LungCapacityLoss(condition, "organ.lung.left");
                    factor -= LungCapacityLoss(condition, "organ.lung.right");
                }
                else if (Active && ResourceId == BiologicalResourceIds.Blood)
                {
                    factor -= StructureCapacityLoss(condition, "organ.heart", 0.5f);
                }

                factor = Mathf.Clamp01(factor);
                EffectiveMaximumValue = MaximumValue * factor;
                if (!Mathf.Approximately(factor, 1f))
                {
                    capacityContributions.Add(new VitalCapacityContributionSnapshot($"body-condition.{ResourceId}", ResourceId, EffectiveMaximumValue - MaximumValue, "Body condition capacity reduction"));
                }

                if (preservingCurrent)
                {
                    CurrentValue = ClampValue(this, CurrentValue);
                    State = Classify(this, CurrentValue);
                }
            }

            public VitalResourceSaveData ToSaveData()
            {
                return new VitalResourceSaveData
                {
                    resourceDefinitionId = ResourceId,
                    active = Active,
                    modelType = ModelType,
                    currentValue = CurrentValue,
                    minimumValue = MinimumValue,
                    maximumValue = MaximumValue,
                    effectiveMaximumValue = EffectiveMaximumValue,
                    idealValue = IdealValue,
                    safeMinimum = SafeMinimum,
                    safeMaximum = SafeMaximum,
                    strainedLow = StrainedLow,
                    strainedHigh = StrainedHigh,
                    criticalLow = CriticalLow,
                    criticalHigh = CriticalHigh,
                    absoluteMinimum = AbsoluteMinimum,
                    absoluteMaximum = AbsoluteMaximum,
                    state = State,
                    revision = Revision
                };
            }

            public void Restore(VitalResourceSaveData saveData)
            {
                Active = saveData.active;
                ModelType = saveData.modelType;
                MinimumValue = saveData.minimumValue;
                MaximumValue = saveData.maximumValue;
                EffectiveMaximumValue = saveData.effectiveMaximumValue;
                IdealValue = saveData.idealValue;
                SafeMinimum = saveData.safeMinimum;
                SafeMaximum = saveData.safeMaximum;
                StrainedLow = saveData.strainedLow;
                StrainedHigh = saveData.strainedHigh;
                CriticalLow = saveData.criticalLow;
                CriticalHigh = saveData.criticalHigh;
                AbsoluteMinimum = saveData.absoluteMinimum;
                AbsoluteMaximum = saveData.absoluteMaximum;
                CurrentValue = ClampValue(this, saveData.currentValue);
                State = Classify(this, CurrentValue);
                Revision = Math.Max(1L, saveData.revision);
            }

            public VitalResourceSnapshot CreateSnapshot()
            {
                return new VitalResourceSnapshot(ResourceId, DisplayName, ModelType, Active, CurrentValue, MinimumValue, MaximumValue, EffectiveMaximumValue, IdealValue, SafeMinimum, SafeMaximum, StrainedLow, StrainedHigh, CriticalLow, CriticalHigh, AbsoluteMinimum, AbsoluteMaximum, ConsumptionPerHour, RestorationPerHour, State, capacityContributions, Revision);
            }

            public bool IsCoherent(out string failureReason)
            {
                failureReason = string.Empty;
                if (AbsoluteMaximum < AbsoluteMinimum)
                {
                    failureReason = $"Vital resource '{ResourceId}' has invalid absolute bounds.";
                    return false;
                }

                if (CurrentValue < AbsoluteMinimum - 0.001f || CurrentValue > AbsoluteMaximum + 0.001f)
                {
                    failureReason = $"Vital resource '{ResourceId}' current value {CurrentValue} is outside absolute bounds.";
                    return false;
                }

                return true;
            }

            private static float LungCapacityLoss(BodyConditionSnapshot condition, string nodeId)
            {
                return StructureCapacityLoss(condition, nodeId, 0.25f);
            }

            private static float StructureCapacityLoss(BodyConditionSnapshot condition, string nodeId, float maximumLoss)
            {
                StructureConditionSnapshot structure = condition == null ? null : condition.Structures.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
                if (structure == null || !structure.Present || structure.Failed)
                {
                    return maximumLoss;
                }

                return maximumLoss * (1f - Mathf.Clamp01(structure.IntegrityPercent));
            }
        }
    }
}
