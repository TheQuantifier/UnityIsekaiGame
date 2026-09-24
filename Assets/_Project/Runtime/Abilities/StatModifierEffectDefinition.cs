using UnityEngine;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Abilities
{
    [CreateAssetMenu(fileName = "NewStatModifierEffect", menuName = "Unity Isekai Game/Abilities/Effects/Stat Modifier")]
    public sealed class StatModifierEffectDefinition : EffectDefinition
    {
        [SerializeField] private CalculatedStatModifierDefinition[] calculatedStatModifiers;
        [SerializeField] private StatModifierSourceType sourceType = StatModifierSourceType.Ability;
        [SerializeField] private string sourceIdOverride;

        public System.Collections.Generic.IReadOnlyList<CalculatedStatModifierDefinition> CalculatedStatModifiers => calculatedStatModifiers ?? System.Array.Empty<CalculatedStatModifierDefinition>();

        public override EffectExecutionResult CanExecute(in EffectExecutionContext context)
        {
            if (context.Target == null)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.InvalidTarget, $"{DisplayName} has no target.");
            }

            if (context.Target.GetComponentInParent<IRuntimeCalculatedStatReceiver>() == null)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.UnsupportedTarget, $"{context.Target.name} has no runtime stat receiver.");
            }

            if (CalculatedStatModifiers.Count == 0)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.InvalidConfiguration, $"{DisplayName} has no stat modifiers.");
            }

            return EffectExecutionResult.Success($"{DisplayName} can apply stat modifiers.");
        }

        public override EffectExecutionResult Execute(in EffectExecutionContext context)
        {
            EffectExecutionResult canExecute = CanExecute(in context);
            if (!canExecute.Succeeded)
            {
                return canExecute;
            }

            IRuntimeCalculatedStatReceiver statReceiver = context.Target.GetComponentInParent<IRuntimeCalculatedStatReceiver>();
            StatModifierSource source = new StatModifierSource(sourceType, ResolveSourceId(in context));
            int appliedCount = 0;

            for (int i = 0; i < CalculatedStatModifiers.Count; i++)
            {
                CalculatedStatModifierDefinition modifier = CalculatedStatModifiers[i];
                if (modifier == null || !modifier.IsValid)
                {
                    statReceiver.RemoveCalculatedStatContributions(source);
                    return EffectExecutionResult.Failure(EffectExecutionStatus.InvalidConfiguration, $"{DisplayName} has an invalid modifier at index {i}.");
                }

                if (!statReceiver.AddCalculatedStatContribution(modifier.CreateRuntimeContribution(source, 1, $"{ResolveSourceId(in context)}.{i}.{modifier.Stat.Id}")))
                {
                    statReceiver.RemoveCalculatedStatContributions(source);
                    return EffectExecutionResult.Failure(EffectExecutionStatus.NoStateChange, $"{DisplayName} could not apply modifier {i}.");
                }

                appliedCount++;
            }

            return appliedCount > 0
                ? EffectExecutionResult.Success($"Applied {appliedCount} stat modifier(s).", appliedCount, () => statReceiver.RemoveCalculatedStatContributions(source))
                : EffectExecutionResult.Failure(EffectExecutionStatus.NoStateChange, $"{DisplayName} applied no modifiers.");
        }

        private string ResolveSourceId(in EffectExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(sourceIdOverride))
            {
                return sourceIdOverride;
            }

            string abilityId = context.Ability == null ? Id : context.Ability.Id;
            return string.IsNullOrWhiteSpace(context.ExecutionId)
                ? $"{abilityId}:{Id}"
                : $"{abilityId}:{Id}:{context.ExecutionId}";
        }

    }
}
