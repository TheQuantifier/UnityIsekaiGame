using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;

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

            string sourceActorId = context.SourceActorId;
            string targetActorId = context.TargetActorId;
            if (!ability.AllowSelfTarget &&
                ((!string.IsNullOrWhiteSpace(sourceActorId) && string.Equals(sourceActorId, targetActorId, System.StringComparison.Ordinal)) ||
                 context.Target.transform.IsChildOf(context.Source.transform) ||
                 context.Source.transform.IsChildOf(context.Target.transform)))
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.InvalidTarget, $"{ability.DisplayName} cannot target its source.");
            }

            if (!IsInTargetingMask(context.Target, ability.TargetingMask))
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.InvalidTarget, $"{ability.DisplayName} cannot target this object's collision layer.");
            }

            if (!ability.AllowInactiveTargets && ActorLifecycleUtility.GetState(context.Target) != ActorLifecycleState.Active)
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.InvalidTarget, $"{ability.DisplayName} cannot target an inactive actor.");
            }

            Vector3 resolvedTargetPosition = ResolveClosestTargetPoint(context.Target, context.SourcePosition, context.TargetPosition);
            Vector3 offset = resolvedTargetPosition - context.SourcePosition;
            float distance = offset.magnitude;
            if (ability.Range > 0f && distance > ability.Range + 0.001f)
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.OutOfRange, $"{ability.DisplayName} is out of range.");
            }

            if (!ability.RequiresLineOfSight || distance <= 0.001f)
            {
                return AbilityExecutionResult.Success("Target is valid.");
            }

            GameObject sourceObject = context.Source;
            GameObject targetObject = context.Target;
            RaycastHit blocker = Physics.RaycastAll(context.SourcePosition, offset.normalized, distance, ability.ObstructionMask, ability.TargetingTriggerInteraction)
                .OrderBy(hit => hit.distance)
                .FirstOrDefault(hit => hit.collider != null && !IsPartOf(hit.collider.gameObject, sourceObject) && !IsPartOf(hit.collider.gameObject, targetObject));
            if (blocker.collider != null)
            {
                return AbilityExecutionResult.Failure(AbilityExecutionStatus.InvalidTarget, $"{ability.DisplayName} has no line of sight to its target.");
            }

            return AbilityExecutionResult.Success("Target is valid.");
        }

        private static bool IsInTargetingMask(GameObject target, LayerMask mask)
        {
            if (target == null || mask.value == 0)
            {
                return false;
            }

            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            return colliders.Length == 0
                ? (mask.value & (1 << target.layer)) != 0
                : colliders.Any(collider => collider != null && (mask.value & (1 << collider.gameObject.layer)) != 0);
        }

        private static Vector3 ResolveClosestTargetPoint(GameObject target, Vector3 sourcePosition, Vector3 fallback)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            return colliders.Length == 0
                ? fallback
                : colliders.Where(collider => collider != null)
                    .Select(collider => collider.ClosestPoint(sourcePosition))
                    .OrderBy(point => (point - sourcePosition).sqrMagnitude)
                    .FirstOrDefault();
        }

        private static bool IsPartOf(GameObject candidate, GameObject actor)
        {
            if (candidate == null || actor == null)
            {
                return false;
            }

            string candidateId = AbilityActorIdentityUtility.ResolveActorId(candidate);
            string actorId = AbilityActorIdentityUtility.ResolveActorId(actor);
            return (!string.IsNullOrWhiteSpace(candidateId) && string.Equals(candidateId, actorId, System.StringComparison.Ordinal))
                || candidate.transform.IsChildOf(actor.transform)
                || actor.transform.IsChildOf(candidate.transform);
        }
    }
}
