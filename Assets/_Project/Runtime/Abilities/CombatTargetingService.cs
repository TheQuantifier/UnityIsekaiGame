using System.Linq;
using UnityEngine;

namespace UnityIsekaiGame.Abilities
{
    public static class CombatTargetingService
    {
        public static AbilityExecutionResult Validate(in AbilityExecutionContext context)
        {
            AbilityDefinition ability = context.Ability;
            if (ability == null)
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.MissingAbility, "Missing ability.");
            }

            if (context.Source == null)
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.InvalidSource, "Missing ability source.");
            }

            if (ability.TargetingMode == AbilityTargetingMode.DirectTarget && context.Target == null)
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.InvalidTarget, $"{ability.DisplayName} requires a target.");
            }

            if (context.Target == null)
            {
                return AbilityExecutionResult.Success("Directional target is valid.");
            }

            if (!ability.AllowSelfTarget &&
                (context.Target.transform.IsChildOf(context.Source.transform) ||
                 context.Source.transform.IsChildOf(context.Target.transform)))
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.InvalidTarget, $"{ability.DisplayName} cannot target its source.");
            }

            Vector3 offset = context.TargetPosition - context.SourcePosition;
            float distance = offset.magnitude;
            if (ability.Range > 0f && distance > ability.Range + 0.001f)
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.OutOfRange, $"{ability.DisplayName} is out of range.");
            }

            if (!ability.RequiresLineOfSight || distance <= 0.001f)
            {
                return AbilityExecutionResult.Success("Target is valid.");
            }

            Transform sourceTransform = context.Source.transform;
            RaycastHit blocker = Physics.RaycastAll(context.SourcePosition, offset.normalized, distance, ability.TargetingMask, ability.TargetingTriggerInteraction)
                .OrderBy(hit => hit.distance)
                .FirstOrDefault(hit => hit.collider != null && !hit.collider.transform.IsChildOf(sourceTransform));
            if (blocker.collider != null && !blocker.collider.transform.IsChildOf(context.Target.transform) && !context.Target.transform.IsChildOf(blocker.collider.transform))
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.InvalidTarget, $"{ability.DisplayName} has no line of sight to its target.");
            }

            return AbilityExecutionResult.Success("Target is valid.");
        }
    }
}
