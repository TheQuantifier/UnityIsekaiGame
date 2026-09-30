using UnityEngine;
using System.Collections.Generic;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.StatusEffects;

namespace UnityIsekaiGame.Abilities
{
    [CreateAssetMenu(fileName = "NewApplyStatusEffect", menuName = "Unity Isekai Game/Abilities/Effects/Apply Status Effect")]
    public sealed class ApplyStatusEffectDefinition : EffectDefinition
    {
        [SerializeField] private StatusEffectDefinition statusEffect;
        [SerializeField] private bool allowDurationOverride;
        [SerializeField, Min(0f)] private float durationOverride;

        public StatusEffectDefinition StatusEffect => statusEffect;
        public bool AllowDurationOverride => allowDurationOverride;
        public float DurationOverride => durationOverride;

        public override EffectExecutionResult CanExecute(in EffectExecutionContext context)
        {
            if (statusEffect == null)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.InvalidConfiguration, $"{DisplayName} has no status effect.");
            }

            if (context.Target == null)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.InvalidTarget, $"{DisplayName} has no target.");
            }

            if (context.Target.GetComponentInParent<IStatusEffectReceiver>() == null)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.UnsupportedTarget, $"{context.Target.name} cannot receive status effects.");
            }

            IStatusEffectReceiver receiver = context.Target.GetComponentInParent<IStatusEffectReceiver>();
            StatusEffectApplicationRequest request = CreateRequest(in context);
            StatusApplicationResult result = receiver.CanApplyStatus(request);
            return result.Succeeded
                ? EffectExecutionResult.Success($"{DisplayName} can apply {statusEffect.DisplayName}.")
                : EffectExecutionResult.Failure(EffectExecutionStatus.NoStateChange, result.Message);
        }

        public override EffectExecutionResult Execute(in EffectExecutionContext context)
        {
            EffectExecutionResult canExecute = CanExecute(in context);
            if (!canExecute.Succeeded)
            {
                return canExecute;
            }

            IStatusEffectReceiver receiver = context.Target.GetComponentInParent<IStatusEffectReceiver>();
            StatusEffectApplicationRequest request = CreateRequest(in context);
            StatusEffectTransactionSnapshot rollbackSnapshot = receiver.StatusController?.CaptureTransactionSnapshot(statusEffect.Id);

            StatusApplicationResult result = receiver.ApplyStatus(request);
            return result.Succeeded
                ? EffectExecutionResult.Success(result.Message, result.StatusEffect == null ? 0f : result.StatusEffect.StackCount,
                    rollbackSnapshot == null ? null : () => receiver.StatusController?.RestoreTransactionSnapshot(rollbackSnapshot))
                : EffectExecutionResult.Failure(EffectExecutionStatus.NoStateChange, result.Message);
        }

        private StatusEffectApplicationRequest CreateRequest(in EffectExecutionContext context)
        {
            string sourceId = string.IsNullOrWhiteSpace(context.SourceActorId) ? (context.Ability == null ? Id : context.Ability.Id) : context.SourceActorId;
            return new StatusEffectApplicationRequest(
                statusEffect,
                context.Source,
                sourceId,
                allowDurationOverride ? durationOverride : 0f,
                string.IsNullOrWhiteSpace(context.ExecutionId) ? string.Empty : $"{context.ExecutionId}.status.{statusEffect.Id}",
                Time.time);
        }

        public override void ValidateDefinition(UnityIsekaiGame.GameData.DefinitionValidationReport report)
        {
            base.ValidateDefinition(report);
            if (statusEffect == null)
            {
                report?.AddError($"Apply status effect '{DisplayName}' has no status reference.");
            }

            if (allowDurationOverride && durationOverride <= 0f)
            {
                report?.AddError($"Apply status effect '{DisplayName}' allows duration override but has no positive override.");
            }
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitionsById, report);
            if (statusEffect == null || definitionsById == null || report == null)
            {
                return;
            }

            if (!definitionsById.TryGetValue(statusEffect.Id, out IGameDefinition found) || !ReferenceEquals(found, statusEffect))
            {
                report.AddError($"Apply status effect '{DisplayName}' references status '{statusEffect.Id}', which is not in the configured catalog.");
            }
        }
    }
}
