using UnityEngine;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Abilities
{
    [CreateAssetMenu(fileName = "NewRestoreVitalEffect", menuName = "Unity Isekai Game/Abilities/Effects/Restore Vital")]
    public sealed class RestoreVitalEffectDefinition : EffectDefinition
    {
        [SerializeField] private VitalType vitalType = VitalType.Health;
        [SerializeField, Min(0f)] private float amount = 25f;

        public VitalType VitalType => vitalType;
        public float Amount => amount;

        private void OnValidate()
        {
            amount = Mathf.Max(0f, amount);
        }

        public override EffectExecutionResult CanExecute(in EffectExecutionContext context)
        {
            if (amount <= 0f)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.InvalidConfiguration, $"{DisplayName} has no positive restore amount.");
            }

            if (context.Target == null)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.InvalidTarget, $"{DisplayName} has no target.");
            }

            return CanRestoreResource(context.Target, ResolveResourceId());
        }

        public override EffectExecutionResult Execute(in EffectExecutionContext context)
        {
            EffectExecutionResult canExecute = CanExecute(in context);
            if (!canExecute.Succeeded)
            {
                return canExecute;
            }

            return vitalType switch
            {
                VitalType.Health => RestoreHealth(in context),
                VitalType.Mana => RestoreResource(in context, ResourceIds.Mana),
                VitalType.Stamina => RestoreResource(in context, ResourceIds.Stamina),
                _ => EffectExecutionResult.Failure(EffectExecutionStatus.InvalidConfiguration, $"{DisplayName} has an invalid vital type.")
            };
        }

        public override void ValidateDefinition(UnityIsekaiGame.GameData.DefinitionValidationReport report)
        {
            base.ValidateDefinition(report);
            if (amount <= 0f)
            {
                report?.AddError($"Restore effect '{DisplayName}' must have a positive amount.");
            }
        }

        private EffectExecutionResult CanRestoreResource(GameObject target, string resourceId)
        {
            CharacterResourceCollection resources = target.GetComponentInParent<CharacterResourceCollection>();
            if (resources == null || string.IsNullOrWhiteSpace(resourceId) || !resources.TryGetResource(resourceId, out ResourceSnapshot snapshot))
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.UnsupportedTarget, $"{target.name} does not expose resource '{resourceId}'.");
            }

            return snapshot.Current >= snapshot.Maximum - CharacterResourceCollection.Epsilon
                ? EffectExecutionResult.Failure(EffectExecutionStatus.NoStateChange, $"{resourceId} is already full.")
                : EffectExecutionResult.Success($"{resourceId} can be restored.");
        }

        private EffectExecutionResult RestoreHealth(in EffectExecutionContext context)
        {
            GameObject target = context.Target;
            if (CanUseHealingPipeline(target))
            {
                float restoreAmount = amount * Mathf.Max(0f, context.MagnitudeMultiplier);
                HealingApplicationResult healingResult = new DamageHealingService().ApplyHealing(CreateHealingRequest(in context, restoreAmount, DisplayName));
                string rollbackSourceActorId = context.SourceActorId;
                string rollbackExecutionId = context.ExecutionId;
                return healingResult.Succeeded && healingResult.HealthChanged
                    ? EffectExecutionResult.Success(healingResult.Message, healingResult.FinalHealingAmount, () =>
                    {
                        CharacterResourceCollection resources = target.GetComponentInParent<CharacterResourceCollection>();
                        resources?.ApplyChange(new ResourceChangeRequest(ResourceIds.Health, ResourceChangeOperation.Damage, healingResult.FinalHealingAmount, ResourceChangeSourceCategory.Ability, rollbackSourceActorId, $"Rollback {DisplayName}", $"{rollbackExecutionId}.rollback.{Id}", allowPartial: true, authorityValidated: true));
                    })
                    : EffectExecutionResult.Failure(EffectExecutionStatus.NoStateChange, healingResult.Message);
            }

            return EffectExecutionResult.Failure(EffectExecutionStatus.UnsupportedTarget, $"{target.name} does not expose canonical Health and identity.");
        }

        private EffectExecutionResult RestoreResource(in EffectExecutionContext context, string resourceId)
        {
            GameObject target = context.Target;
            CharacterResourceCollection resources = target.GetComponentInParent<CharacterResourceCollection>();
            ResourceChangeResult result = resources?.ApplyChange(new ResourceChangeRequest(
                resourceId,
                ResourceChangeOperation.Gain,
                amount * Mathf.Max(0f, context.MagnitudeMultiplier),
                ResourceChangeSourceCategory.Ability,
                context.SourceActorId,
                DisplayName,
                $"{context.ExecutionId}.effect.{Id}",
                allowPartial: true,
                authorityValidated: true));
            string rollbackSourceActorId = context.SourceActorId;
            string rollbackExecutionId = context.ExecutionId;
            return result != null && result.Succeeded && result.AppliedAmount > CharacterResourceCollection.Epsilon
                ? EffectExecutionResult.Success(result.Message, result.AppliedAmount, () =>
                {
                    resources.ApplyChange(new ResourceChangeRequest(resourceId, ResourceChangeOperation.Spend, result.AppliedAmount, ResourceChangeSourceCategory.Ability, rollbackSourceActorId, $"Rollback {DisplayName}", $"{rollbackExecutionId}.rollback.{Id}", allowPartial: true, authorityValidated: true));
                })
                : EffectExecutionResult.Failure(EffectExecutionStatus.NoStateChange, result?.Message ?? $"Resource '{resourceId}' is unavailable.");
        }

        private static bool CanUseHealingPipeline(GameObject target)
        {
            return target != null
                && target.GetComponentInParent<CharacterResourceCollection>()?.HasResource(ResourceIds.Health) == true
                && !string.IsNullOrWhiteSpace(AbilityActorIdentityUtility.ResolveActorId(target));
        }

        private string ResolveResourceId()
        {
            return vitalType switch
            {
                VitalType.Health => ResourceIds.Health,
                VitalType.Mana => ResourceIds.Mana,
                VitalType.Stamina => ResourceIds.Stamina,
                _ => string.Empty
            };
        }

        private static HealingApplicationRequest CreateHealingRequest(in EffectExecutionContext context, float restoreAmount, string reason)
        {
            return new HealingApplicationRequest(
                string.IsNullOrWhiteSpace(context.ExecutionId) ? $"ability-healing.{System.Guid.NewGuid():N}" : $"{context.ExecutionId}.effect.healing",
                context.SourceActorId,
                context.Source,
                context.TargetActorId,
                context.Target,
                restoreAmount,
                reason,
                authorityValidated: true);
        }

    }
}
