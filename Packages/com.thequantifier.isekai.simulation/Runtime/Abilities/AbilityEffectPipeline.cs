using System;
using System.Collections.Generic;
using UnityEngine;

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

                EffectExecutionResult result;
                try
                {
                    result = effect.CanExecute(in context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    result = EffectExecutionResult.Failure(
                        EffectExecutionStatus.InvalidConfiguration,
                        $"Effect at index {i} threw during validation.");
                }

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
            List<Action> rollbacks = new List<Action>();
            for (int i = 0; i < effects.Count; i++)
            {
                EffectExecutionContext effectContext = context.WithExecutionId(string.IsNullOrWhiteSpace(context.ExecutionId) ? $"effect.{i}" : $"{context.ExecutionId}.{i}");
                EffectExecutionResult result;
                try
                {
                    result = effects[i].Execute(in effectContext);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    result = EffectExecutionResult.Failure(
                        EffectExecutionStatus.InvalidConfiguration,
                        $"Effect at index {i} threw during execution.");
                }

                if (!result.Succeeded)
                {
                    int rollbackFailureCount = 0;
                    for (int rollbackIndex = rollbacks.Count - 1; rollbackIndex >= 0; rollbackIndex--)
                    {
                        try
                        {
                            rollbacks[rollbackIndex]?.Invoke();
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                            rollbackFailureCount++;
                        }
                    }

                    string failureMessage = rollbackFailureCount == 0
                        ? result.Message
                        : $"{result.Message} {rollbackFailureCount} rollback operation(s) failed; authoritative state requires reconciliation.";
                    return AbilityExecutionResult.Failure(AbilityExecutionStatus.EffectExecutionFailure, failureMessage, i, result);
                }

                if (result.Rollback != null)
                {
                    rollbacks.Add(result.Rollback);
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
