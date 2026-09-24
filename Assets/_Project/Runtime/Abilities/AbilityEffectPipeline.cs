using System.Collections.Generic;

namespace UnityIsekaiGame.Abilities
{
    /// <summary>
    /// The single effect batch entry point. Every effect is preflighted before any
    /// mutation occurs, so invalid multi-effect abilities cannot partially apply.
    /// </summary>
    public static class AbilityEffectPipeline
    {
        public static AbilityExecutionResult Validate(in EffectExecutionContext context, IReadOnlyList<EffectDefinition> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.NoEffects, "Ability has no effects.");
            }

            for (int i = 0; i < effects.Count; i++)
            {
                EffectDefinition effect = effects[i];
                if (effect == null)
                {
                    EffectExecutionResult missing = EffectExecutionResult.Failure(EffectExecutionStatus.InvalidConfiguration, $"Missing effect at index {i}.");
                    return AbilityExecutionResult.Failure(AbilityExecutionStatus.EffectValidationFailure, missing.Message, i, missing);
                }

                EffectExecutionResult result = effect.CanExecute(in context);
                if (!result.Succeeded)
                {
                    return AbilityExecutionResult.Failure(AbilityExecutionStatus.EffectValidationFailure, result.Message, i, result);
                }
            }

            return AbilityExecutionResult.Success("Ability effects are valid.");
        }

        public static AbilityExecutionResult Execute(in EffectExecutionContext context, IReadOnlyList<EffectDefinition> effects)
        {
            AbilityExecutionResult validation = Validate(in context, effects);
            if (!validation.Succeeded)
            {
                return validation;
            }

            string message = string.Empty;
            for (int i = 0; i < effects.Count; i++)
            {
                EffectExecutionResult result = effects[i].Execute(in context);
                if (!result.Succeeded)
                {
                    return AbilityExecutionResult.Failure(AbilityExecutionStatus.EffectExecutionFailure, result.Message, i, result);
                }

                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    message = result.Message;
                }
            }

            return AbilityExecutionResult.Success(string.IsNullOrWhiteSpace(message) ? "Ability effects executed." : message);
        }
    }
}
