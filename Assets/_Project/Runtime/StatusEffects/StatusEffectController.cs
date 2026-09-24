using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.StatusEffects
{
    public sealed class StatusEffectController : MonoBehaviour, IStatusEffectReceiver
    {
        private readonly List<RuntimeStatusEffect> activeStatuses = new List<RuntimeStatusEffect>();
        private readonly Dictionary<string, List<string>> ongoingInstanceIdsByStatus = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private IRuntimeCalculatedStatReceiver statReceiver;
        private IDamageResistanceReceiver resistanceReceiver;
        [SerializeField] private OngoingEffectService ongoingEffects;
        [SerializeField] private bool useLocalTime = true;
        private float lastAuthoritativeTime;
        private bool hasAuthoritativeTime;

        public IReadOnlyList<RuntimeStatusEffect> ActiveStatuses => activeStatuses;
        public StatusEffectController StatusController => this;
        public event Action<RuntimeStatusEffect> StatusAdded;
        public event Action<RuntimeStatusEffect> StatusChanged;
        public event Action<RuntimeStatusEffect> StatusRemoved;
        public event Action<RuntimeStatusEffect> StatusExpired;

        private void Awake()
        {
            statReceiver = GetComponentInParent<IRuntimeCalculatedStatReceiver>();
            resistanceReceiver = GetComponentInParent<IDamageResistanceReceiver>();
            ongoingEffects ??= GetComponentInParent<OngoingEffectService>();
        }

        private void Update()
        {
            if (useLocalTime)
            {
                AdvanceTo(Time.time);
            }
        }

        public void AdvanceTo(float authoritativeTime)
        {
            if (float.IsNaN(authoritativeTime) || float.IsInfinity(authoritativeTime) || authoritativeTime < 0f)
            {
                return;
            }

            if (!hasAuthoritativeTime)
            {
                lastAuthoritativeTime = authoritativeTime;
                hasAuthoritativeTime = true;
                return;
            }

            float delta = Mathf.Max(0f, authoritativeTime - lastAuthoritativeTime);
            lastAuthoritativeTime = Mathf.Max(lastAuthoritativeTime, authoritativeTime);
            UpdateStatuses(delta);
        }

        public void ResetAuthoritativeClock(float authoritativeTime = 0f)
        {
            lastAuthoritativeTime = Mathf.Max(0f, authoritativeTime);
            hasAuthoritativeTime = true;
        }

        public StatusApplicationResult CanApplyStatus(StatusEffectApplicationRequest request)
        {
            StatusApplicationResult validation = ValidateRequest(request);
            if (!validation.Succeeded)
            {
                return validation;
            }

            if (request.Definition.DurationModel == StatusDurationModel.Instant)
            {
                return ValidateInstantEffects(request);
            }

            RuntimeStatusEffect existing = FindFirstActive(request.Definition.Id);
            if (existing == null || request.Definition.StackingPolicy == StatusStackingPolicy.IndependentInstances)
            {
                return StatusApplicationResult.Success(existing, $"{request.Definition.DisplayName} can apply.");
            }

            return EvaluateExistingStatus(existing, request.Definition);
        }

        public StatusApplicationResult ApplyStatus(StatusEffectApplicationRequest request)
        {
            StatusApplicationResult validation = CanApplyStatus(request);
            if (!validation.Succeeded)
            {
                return validation;
            }

            if (request.Definition.DurationModel == StatusDurationModel.Instant)
            {
                return ExecuteInstantEffects(request);
            }

            RuntimeStatusEffect existing = FindFirstActive(request.Definition.Id);
            if (existing != null && request.Definition.StackingPolicy != StatusStackingPolicy.IndependentInstances)
            {
                return ApplyStackingPolicy(existing, request);
            }

            RuntimeStatusEffect created = CreateRuntimeStatus(request);
            if (!RegisterModifiers(created))
            {
                return StatusApplicationResult.Failure(StatusApplicationStatus.InvalidModifier, $"Could not register modifiers for {request.Definition.DisplayName}.");
            }

            if (!RegisterOngoingEffects(created))
            {
                UnregisterModifiers(created);
                return StatusApplicationResult.Failure(StatusApplicationStatus.InvalidModifier, $"Could not register ongoing effects for {request.Definition.DisplayName}.");
            }

            activeStatuses.Add(created);
            StatusAdded?.Invoke(created);
            return StatusApplicationResult.Success(created, $"Applied {request.Definition.DisplayName}.");
        }

        public bool RemoveStatus(string applicationId)
        {
            RuntimeStatusEffect status = FindByApplicationId(applicationId);
            return status != null && status.Definition.CanBeRemoved && RemoveStatusInternal(status);
        }

        public bool ForceRemoveStatus(string applicationId)
        {
            RuntimeStatusEffect status = FindByApplicationId(applicationId);
            return RemoveStatusInternal(status);
        }

        public bool RemoveStatusesByDefinition(string definitionId)
        {
            bool removedAny = false;
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                if (activeStatuses[i].Definition.Id == definitionId)
                {
                    removedAny |= RemoveStatus(activeStatuses[i].ApplicationId);
                }
            }

            return removedAny;
        }

        public bool RemoveStatusesBySource(string sourceId)
        {
            bool removedAny = false;
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                if (string.Equals(activeStatuses[i].SourceId, sourceId, StringComparison.Ordinal))
                {
                    removedAny |= RemoveStatus(activeStatuses[i].ApplicationId);
                }
            }

            return removedAny;
        }

        public void ClearTemporaryStatuses()
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                RuntimeStatusEffect status = activeStatuses[i];
                if (status.Definition.DurationModel == StatusDurationModel.Timed || status.Definition.DurationModel == StatusDurationModel.Instant)
                {
                    ForceRemoveStatus(status.ApplicationId);
                }
            }
        }

        public void ClearAllStatuses()
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                ForceRemoveStatus(activeStatuses[i].ApplicationId);
            }
        }

        public void UpdateStatuses(float deltaTime)
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                RuntimeStatusEffect status = activeStatuses[i];
                if (!status.Advance(deltaTime))
                {
                    continue;
                }

                UnregisterModifiers(status);
                UnregisterOngoingEffects(status);
                activeStatuses.RemoveAt(i);
                StatusExpired?.Invoke(status);
            }
        }

        public StatusEffectTransactionSnapshot CaptureTransactionSnapshot(string definitionId)
        {
            List<StatusEffectTransactionSnapshot.Entry> entries = new List<StatusEffectTransactionSnapshot.Entry>();
            for (int i = 0; i < activeStatuses.Count; i++)
            {
                RuntimeStatusEffect status = activeStatuses[i];
                if (status.Definition != null && string.Equals(status.Definition.Id, definitionId, StringComparison.Ordinal))
                {
                    entries.Add(new StatusEffectTransactionSnapshot.Entry
                    {
                        Definition = status.Definition,
                        ApplicationId = status.ApplicationId,
                        SourceId = status.SourceId,
                        Source = status.Source,
                        RemainingDuration = status.RemainingDuration,
                        ElapsedDuration = status.ElapsedDuration,
                        StackCount = status.StackCount,
                        AppliedAt = status.AppliedAt
                    });
                }
            }
            return new StatusEffectTransactionSnapshot(definitionId, entries);
        }

        public void RestoreTransactionSnapshot(StatusEffectTransactionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                if (activeStatuses[i].Definition != null && string.Equals(activeStatuses[i].Definition.Id, snapshot.DefinitionId, StringComparison.Ordinal))
                {
                    ForceRemoveStatus(activeStatuses[i].ApplicationId);
                }
            }

            foreach (StatusEffectTransactionSnapshot.Entry entry in snapshot.Entries)
            {
                RuntimeStatusEffect restored = new RuntimeStatusEffect(entry.Definition, entry.ApplicationId, entry.SourceId, entry.Source, gameObject, entry.RemainingDuration, entry.AppliedAt);
                restored.RestoreStackCount(entry.StackCount);
                restored.RestoreElapsed(entry.ElapsedDuration);
                if (!RegisterModifiers(restored) || !RegisterOngoingEffects(restored))
                {
                    UnregisterModifiers(restored);
                    UnregisterOngoingEffects(restored);
                    throw new InvalidOperationException($"Could not restore status transaction snapshot '{snapshot.DefinitionId}'.");
                }
                activeStatuses.Add(restored);
                StatusAdded?.Invoke(restored);
            }
        }

        public void HandleRest()
        {
            RemoveByDurationModel(StatusDurationModel.UntilRest);
        }

        public void HandleDeath()
        {
            RemoveByDurationModel(StatusDurationModel.UntilDeath);
        }

        public void HandleAreaExit(string sourceId)
        {
            RemoveByDurationModelAndSource(StatusDurationModel.WhileInArea, sourceId);
        }

        public void HandleUnequipped(string sourceId)
        {
            RemoveByDurationModelAndSource(StatusDurationModel.WhileEquipped, sourceId);
        }

        public void EvaluateConditionalStatuses(Func<RuntimeStatusEffect, bool> shouldRemainActive)
        {
            if (shouldRemainActive == null) return;
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                RuntimeStatusEffect status = activeStatuses[i];
                if (status.Definition.DurationModel == StatusDurationModel.Conditional && !shouldRemainActive(status))
                {
                    ForceRemoveStatus(status.ApplicationId);
                }
            }
        }

        public RuntimeStatusEffect FindByApplicationId(string applicationId)
        {
            for (int i = 0; i < activeStatuses.Count; i++)
            {
                if (string.Equals(activeStatuses[i].ApplicationId, applicationId, StringComparison.Ordinal))
                {
                    return activeStatuses[i];
                }
            }

            return null;
        }

        public RuntimeStatusEffect FindFirstActive(string definitionId)
        {
            for (int i = 0; i < activeStatuses.Count; i++)
            {
                RuntimeStatusEffect status = activeStatuses[i];
                if (status.IsActive && status.Definition.Id == definitionId)
                {
                    return status;
                }
            }

            return null;
        }

        public List<StatusEffectSaveData> CreateSaveData()
        {
            return CreateSaveData(saveEligibleOnly: false);
        }

        public List<StatusEffectSaveData> CreateSaveData(bool saveEligibleOnly)
        {
            List<StatusEffectSaveData> saveData = new List<StatusEffectSaveData>(activeStatuses.Count);
            for (int i = 0; i < activeStatuses.Count; i++)
            {
                if (saveEligibleOnly && !ShouldSaveStatus(activeStatuses[i]))
                {
                    continue;
                }

                saveData.Add(activeStatuses[i].CreateSaveData());
            }

            return saveData;
        }

        public void RefreshStatusModifiers(RuntimeStatusEffect status)
        {
            if (status == null || !activeStatuses.Contains(status))
            {
                return;
            }

            RebuildModifiers(status);
            StatusChanged?.Invoke(status);
        }

        private StatusApplicationResult ValidateRequest(StatusEffectApplicationRequest request)
        {
            if (request.Definition == null)
            {
                return StatusApplicationResult.Failure(StatusApplicationStatus.MissingDefinition, "Missing status effect definition.");
            }

            float duration = request.Definition.ResolveDuration(request.DurationOverride);
            if (request.Definition.DurationModel == StatusDurationModel.Timed && duration <= 0f)
            {
                return StatusApplicationResult.Failure(StatusApplicationStatus.InvalidDuration, $"{request.Definition.DisplayName} has no positive duration.");
            }

            if (FindByApplicationId(request.ApplicationId) != null)
            {
                return StatusApplicationResult.Failure(StatusApplicationStatus.DuplicateApplicationId, $"Status application ID '{request.ApplicationId}' is already active.");
            }

            if (!CanReceiveModifiers(request.Definition))
            {
                return StatusApplicationResult.Failure(StatusApplicationStatus.TargetLacksStat, $"{name} lacks a required runtime stat receiver.");
            }

            return StatusApplicationResult.Success(null, "Status can apply.");
        }

        private StatusApplicationResult ApplyStackingPolicy(RuntimeStatusEffect existing, StatusEffectApplicationRequest request)
        {
            switch (request.Definition.StackingPolicy)
            {
                case StatusStackingPolicy.RejectDuplicate:
                    return StatusApplicationResult.Failure(StatusApplicationStatus.DuplicateRejected, $"{request.Definition.DisplayName} is already active.");
                case StatusStackingPolicy.RefreshDuration:
                    Refresh(existing, request);
                    return StatusApplicationResult.Success(existing, $"Refreshed {request.Definition.DisplayName}.");
                case StatusStackingPolicy.ReplaceExisting:
                    ForceRemoveStatus(existing.ApplicationId);
                    return ApplyStatus(CreateReplacementRequest(request));
                case StatusStackingPolicy.AddStack:
                    if (!existing.AddStack())
                    {
                        return StatusApplicationResult.Failure(StatusApplicationStatus.MaximumStacksReached, $"{request.Definition.DisplayName} is already at maximum stacks.");
                    }

                    RebuildModifiers(existing);
                    RebuildOngoingEffects(existing);
                    Refresh(existing, request);
                    StatusChanged?.Invoke(existing);
                    return StatusApplicationResult.Success(existing, $"Added a stack of {request.Definition.DisplayName}.");
                default:
                    return StatusApplicationResult.Failure(StatusApplicationStatus.DuplicateRejected, $"{request.Definition.DisplayName} is already active.");
            }
        }

        private StatusApplicationResult EvaluateExistingStatus(RuntimeStatusEffect existing, StatusEffectDefinition definition)
        {
            switch (definition.StackingPolicy)
            {
                case StatusStackingPolicy.RejectDuplicate:
                    return StatusApplicationResult.Failure(StatusApplicationStatus.DuplicateRejected, $"{definition.DisplayName} is already active.");
                case StatusStackingPolicy.AddStack:
                    return existing.StackCount >= definition.MaximumStacks
                        ? StatusApplicationResult.Failure(StatusApplicationStatus.MaximumStacksReached, $"{definition.DisplayName} is already at maximum stacks.")
                        : StatusApplicationResult.Success(existing, $"{definition.DisplayName} can add a stack.");
                case StatusStackingPolicy.RefreshDuration:
                case StatusStackingPolicy.ReplaceExisting:
                    return StatusApplicationResult.Success(existing, $"{definition.DisplayName} can update an existing status.");
                default:
                    return StatusApplicationResult.Failure(StatusApplicationStatus.DuplicateRejected, $"{definition.DisplayName} is already active.");
            }
        }

        private RuntimeStatusEffect CreateRuntimeStatus(StatusEffectApplicationRequest request)
        {
            float duration = request.Definition.DurationModel == StatusDurationModel.Timed
                ? request.Definition.ResolveDuration(request.DurationOverride)
                : 0f;
            string applicationId = string.IsNullOrWhiteSpace(request.ApplicationId) ? Guid.NewGuid().ToString("D") : request.ApplicationId;
            string sourceId = string.IsNullOrWhiteSpace(request.SourceId) ? applicationId : request.SourceId;
            return new RuntimeStatusEffect(request.Definition, applicationId, sourceId, request.Source, gameObject, duration, request.Now);
        }

        private static StatusEffectApplicationRequest CreateReplacementRequest(StatusEffectApplicationRequest request)
        {
            return new StatusEffectApplicationRequest(request.Definition, request.Source, request.SourceId, request.DurationOverride, string.Empty, request.Now);
        }

        private void Refresh(RuntimeStatusEffect status, StatusEffectApplicationRequest request)
        {
            if (request.Definition.RefreshPolicy == StatusRefreshPolicy.None)
            {
                return;
            }

            float duration = request.Definition.ResolveDuration(request.DurationOverride);
            if (request.Definition.RefreshPolicy == StatusRefreshPolicy.ExtendByDefaultDuration)
            {
                duration = status.RemainingDuration + duration;
            }

            status.Refresh(duration);
            StatusChanged?.Invoke(status);
        }

        private bool CanReceiveModifiers(StatusEffectDefinition definition)
        {
            if (definition.CalculatedStatModifiers.Count == 0 && definition.ResistanceModifiers.Count == 0)
            {
                return true;
            }

            if (definition.CalculatedStatModifiers.Count > 0)
            {
                statReceiver ??= GetComponentInParent<IRuntimeCalculatedStatReceiver>();
                if (statReceiver == null)
                {
                    return false;
                }

                for (int i = 0; i < definition.CalculatedStatModifiers.Count; i++)
                {
                    CalculatedStatModifierDefinition modifier = definition.CalculatedStatModifiers[i];
                    if (modifier == null || !modifier.IsValid || !statReceiver.HasCalculatedStat(modifier.Stat.Id))
                    {
                        return false;
                    }
                }
            }

            if (definition.ResistanceModifiers.Count > 0)
            {
                resistanceReceiver ??= GetComponentInParent<IDamageResistanceReceiver>();
                if (resistanceReceiver == null)
                {
                    return false;
                }

                for (int i = 0; i < definition.ResistanceModifiers.Count; i++)
                {
                    ResistanceModifierDefinition modifier = definition.ResistanceModifiers[i];
                    if (modifier == null || !modifier.IsValid)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool RegisterModifiers(RuntimeStatusEffect status)
        {
            if (status.Definition.CalculatedStatModifiers.Count == 0 && status.Definition.ResistanceModifiers.Count == 0)
            {
                return true;
            }

            if (!RegisterStatModifiers(status))
            {
                return false;
            }

            if (!RegisterResistanceModifiers(status))
            {
                UnregisterModifiers(status);
                return false;
            }

            return true;
        }

        private static bool ShouldSaveStatus(RuntimeStatusEffect status)
        {
            if (status == null || !status.IsActive || status.Definition == null)
            {
                return false;
            }

            if (status.Definition.DurationModel == StatusDurationModel.Instant)
            {
                return false;
            }

            return status.Definition.PersistencePolicy == StatusPersistencePolicy.SaveRemainingDuration
                || status.Definition.PersistencePolicy == StatusPersistencePolicy.PersistentUntilRemoved;
        }

        private bool RegisterStatModifiers(RuntimeStatusEffect status)
        {
            if (status.Definition.CalculatedStatModifiers.Count == 0)
            {
                return true;
            }

            statReceiver ??= GetComponentInParent<IRuntimeCalculatedStatReceiver>();
            if (statReceiver == null)
            {
                return false;
            }

            for (int i = 0; i < status.Definition.CalculatedStatModifiers.Count; i++)
            {
                CalculatedStatModifierDefinition modifier = status.Definition.CalculatedStatModifiers[i];
                if (!statReceiver.AddCalculatedStatContribution(modifier.CreateRuntimeContribution(status.ModifierSource, status.StackCount, $"status.{status.ApplicationId}.{i}.{modifier.Stat.Id}")))
                {
                    statReceiver.RemoveCalculatedStatContributions(status.ModifierSource);
                    return false;
                }
            }

            return true;
        }

        private bool RegisterResistanceModifiers(RuntimeStatusEffect status)
        {
            if (status.Definition.ResistanceModifiers.Count == 0)
            {
                return true;
            }

            resistanceReceiver ??= GetComponentInParent<IDamageResistanceReceiver>();
            if (resistanceReceiver == null)
            {
                return false;
            }

            for (int i = 0; i < status.Definition.ResistanceModifiers.Count; i++)
            {
                RuntimeResistanceModifier modifier = status.Definition.ResistanceModifiers[i].CreateRuntimeModifier(status.ModifierSource, status.StackCount);
                if (!resistanceReceiver.AddResistanceModifier(modifier))
                {
                    resistanceReceiver.RemoveResistanceModifiersFromSource(status.ModifierSource);
                    return false;
                }
            }

            return true;
        }

        private void UnregisterModifiers(RuntimeStatusEffect status)
        {
            statReceiver ??= GetComponentInParent<IRuntimeCalculatedStatReceiver>();
            statReceiver?.RemoveCalculatedStatContributions(status.ModifierSource);
            resistanceReceiver ??= GetComponentInParent<IDamageResistanceReceiver>();
            resistanceReceiver?.RemoveResistanceModifiersFromSource(status.ModifierSource);
        }

        private void RebuildModifiers(RuntimeStatusEffect status)
        {
            UnregisterModifiers(status);
            RegisterModifiers(status);
        }

        private StatusApplicationResult ValidateInstantEffects(StatusEffectApplicationRequest request)
        {
            if (request.Definition.InstantEffects.Count == 0)
            {
                return StatusApplicationResult.Failure(StatusApplicationStatus.InvalidModifier, $"Instant status {request.Definition.DisplayName} has no effects.");
            }

            EffectExecutionContext context = CreateEffectContext(request);
            for (int i = 0; i < request.Definition.InstantEffects.Count; i++)
            {
                EffectDefinition effect = request.Definition.InstantEffects[i];
                if (effect == null)
                {
                    return StatusApplicationResult.Failure(StatusApplicationStatus.InvalidModifier, $"Instant status {request.Definition.DisplayName} has a missing effect at index {i}.");
                }

                EffectExecutionResult validation = effect.CanExecute(in context);
                if (!validation.Succeeded)
                {
                    return StatusApplicationResult.Failure(StatusApplicationStatus.InvalidModifier, validation.Message);
                }
            }

            return StatusApplicationResult.Success(null, $"Can apply instant status {request.Definition.DisplayName}.");
        }

        private StatusApplicationResult ExecuteInstantEffects(StatusEffectApplicationRequest request)
        {
            EffectExecutionContext context = CreateEffectContext(request);
            for (int i = 0; i < request.Definition.InstantEffects.Count; i++)
            {
                EffectExecutionResult result = request.Definition.InstantEffects[i].Execute(in context);
                if (!result.Succeeded)
                {
                    return StatusApplicationResult.Failure(StatusApplicationStatus.InvalidModifier, result.Message);
                }
            }

            return StatusApplicationResult.Success(null, $"Applied instant status {request.Definition.DisplayName}.");
        }

        private EffectExecutionContext CreateEffectContext(StatusEffectApplicationRequest request)
        {
            Vector3 sourcePosition = request.Source == null ? transform.position : request.Source.transform.position;
            Vector3 direction = transform.position - sourcePosition;
            if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
            return new EffectExecutionContext(null, request.Source, gameObject, sourcePosition, transform.position, direction);
        }

        private bool RegisterOngoingEffects(RuntimeStatusEffect status)
        {
            if (status.Definition.OngoingEffects.Count == 0)
            {
                return true;
            }

            ongoingEffects ??= GetComponentInParent<OngoingEffectService>();
            if (ongoingEffects == null)
            {
                return false;
            }

            string sourceActorId = ResolveActorId(status.Source);
            string targetActorId = ResolveActorId(gameObject);
            if (string.IsNullOrWhiteSpace(targetActorId))
            {
                return false;
            }

            List<string> instanceIds = new List<string>();
            for (int i = 0; i < status.Definition.OngoingEffects.Count; i++)
            {
                OngoingEffectDefinition definition = status.Definition.OngoingEffects[i];
                OngoingEffectApplicationResult result = ongoingEffects.ApplyOngoingEffect(new OngoingEffectApplicationRequest(
                    $"status.{status.ApplicationId}.ongoing.{i}",
                    definition,
                    sourceActorId,
                    status.Source,
                    targetActorId,
                    gameObject,
                    status.ApplicationId,
                    durationOverride: status.Definition.DurationModel == StatusDurationModel.Timed ? status.RemainingDuration : 0f,
                    stackCount: status.StackCount,
                    authorityValidated: true));
                if (!result.Succeeded)
                {
                    for (int applied = 0; applied < instanceIds.Count; applied++)
                    {
                        ongoingEffects.CancelOngoingEffect(new OngoingEffectCancellationRequest($"status.{status.ApplicationId}.rollback.{applied}", instanceIds[applied], targetActorId, gameObject, "Status application rolled back.", true));
                    }

                    return false;
                }

                if (!string.IsNullOrWhiteSpace(result.InstanceId)) instanceIds.Add(result.InstanceId);
            }

            ongoingInstanceIdsByStatus[status.ApplicationId] = instanceIds;
            return true;
        }

        private void UnregisterOngoingEffects(RuntimeStatusEffect status)
        {
            if (status == null || ongoingEffects == null || !ongoingInstanceIdsByStatus.TryGetValue(status.ApplicationId, out List<string> instanceIds))
            {
                return;
            }

            string targetActorId = ResolveActorId(gameObject);
            for (int i = 0; i < instanceIds.Count; i++)
            {
                ongoingEffects.CancelOngoingEffect(new OngoingEffectCancellationRequest($"status.{status.ApplicationId}.cancel.{i}.{Guid.NewGuid():N}", instanceIds[i], targetActorId, gameObject, "Owning status ended.", true));
            }

            ongoingInstanceIdsByStatus.Remove(status.ApplicationId);
        }

        private void RebuildOngoingEffects(RuntimeStatusEffect status)
        {
            UnregisterOngoingEffects(status);
            RegisterOngoingEffects(status);
        }

        private bool RemoveStatusInternal(RuntimeStatusEffect status)
        {
            if (status == null || !status.Remove())
            {
                return false;
            }

            UnregisterModifiers(status);
            UnregisterOngoingEffects(status);
            activeStatuses.Remove(status);
            StatusRemoved?.Invoke(status);
            return true;
        }

        private void RemoveByDurationModel(StatusDurationModel durationModel)
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                if (activeStatuses[i].Definition.DurationModel == durationModel)
                {
                    ForceRemoveStatus(activeStatuses[i].ApplicationId);
                }
            }
        }

        private void RemoveByDurationModelAndSource(StatusDurationModel durationModel, string sourceId)
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                RuntimeStatusEffect status = activeStatuses[i];
                if (status.Definition.DurationModel == durationModel && string.Equals(status.SourceId, sourceId, StringComparison.Ordinal))
                {
                    ForceRemoveStatus(status.ApplicationId);
                }
            }
        }

        private static string ResolveActorId(GameObject actor)
        {
            if (actor == null) return string.Empty;
            CharacterSystemCoordinator character = actor.GetComponentInParent<CharacterSystemCoordinator>();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId)) return character.ActorId;
            WorldEntityIdentity identity = actor.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }
    }
}
