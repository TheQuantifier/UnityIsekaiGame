using UnityEngine;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Abilities
{
    [CreateAssetMenu(fileName = "NewStatModifierEffect", menuName = "Unity Isekai Game/Abilities/Effects/Stat Modifier")]
    public sealed class StatModifierEffectDefinition : EffectDefinition
    {
        [SerializeField] private StatModifierDefinition[] runtimeModifiers;
        [SerializeField] private StatModifierSourceType sourceType = StatModifierSourceType.Ability;
        [SerializeField] private string sourceIdOverride;

        public System.Collections.Generic.IReadOnlyList<StatModifierDefinition> RuntimeModifiers => runtimeModifiers ?? System.Array.Empty<StatModifierDefinition>();

        public override EffectExecutionResult CanExecute(in EffectExecutionContext context)
        {
            if (context.Target == null)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.InvalidTarget, $"{DisplayName} has no target.");
            }

            if (context.Target.GetComponentInParent<IRuntimeStatReceiver>() == null)
            {
                return EffectExecutionResult.Failure(EffectExecutionStatus.UnsupportedTarget, $"{context.Target.name} has no runtime stat receiver.");
            }

            if (RuntimeModifiers.Count == 0)
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

            IRuntimeStatReceiver statReceiver = context.Target.GetComponentInParent<IRuntimeStatReceiver>();
            StatModifierSource source = new StatModifierSource(sourceType, ResolveSourceId(in context));
            int appliedCount = 0;

            for (int i = 0; i < RuntimeModifiers.Count; i++)
            {
                StatModifierDefinition modifier = RuntimeModifiers[i];
                if (modifier == null || !modifier.IsValid)
                {
                    statReceiver.RemoveModifiersFromSource(source);
                    return EffectExecutionResult.Failure(EffectExecutionStatus.InvalidConfiguration, $"{DisplayName} has an invalid modifier at index {i}.");
                }

                if (!statReceiver.AddModifier(modifier.CreateRuntimeModifier(source, 1)))
                {
                    statReceiver.RemoveModifiersFromSource(source);
                    return EffectExecutionResult.Failure(EffectExecutionStatus.NoStateChange, $"{DisplayName} could not apply modifier {i}.");
                }

                appliedCount++;
            }

            return appliedCount > 0
                ? EffectExecutionResult.Success($"Applied {appliedCount} stat modifier(s).", appliedCount)
                : EffectExecutionResult.Failure(EffectExecutionStatus.NoStateChange, $"{DisplayName} applied no modifiers.");
        }

        private string ResolveSourceId(in EffectExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(sourceIdOverride))
            {
                return sourceIdOverride;
            }

            string abilityId = context.Ability == null ? Id : context.Ability.Id;
            return $"{abilityId}:{Id}";
        }

    }
}
