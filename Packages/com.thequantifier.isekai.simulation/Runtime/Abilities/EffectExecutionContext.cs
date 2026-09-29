using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Abilities
{
    public readonly struct EffectExecutionContext
    {
        public EffectExecutionContext(
            AbilityDefinition ability,
            GameObject source,
            GameObject target,
            Vector3 sourcePosition,
            Vector3 targetPosition,
            Vector3 direction,
            ItemDefinition sourceItem,
            string sourceItemInstanceId,
            float magnitudeMultiplier)
            : this(ability, source, target, sourcePosition, targetPosition, direction, sourceItem, sourceItemInstanceId, magnitudeMultiplier, string.Empty, string.Empty, string.Empty)
        {
        }

        public EffectExecutionContext(
            AbilityDefinition ability,
            GameObject source,
            GameObject target,
            Vector3 sourcePosition,
            Vector3 targetPosition,
            Vector3 direction,
            ItemDefinition sourceItem = null,
            string sourceItemInstanceId = "",
            float magnitudeMultiplier = 1f,
            string executionId = "",
            string sourceActorId = "",
            string targetActorId = "")
        {
            Ability = ability;
            Source = source;
            Target = target;
            SourcePosition = sourcePosition;
            TargetPosition = targetPosition;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            SourceItem = sourceItem;
            SourceItemInstanceId = sourceItemInstanceId ?? string.Empty;
            MagnitudeMultiplier = magnitudeMultiplier;
            ExecutionId = executionId ?? string.Empty;
            SourceActorId = string.IsNullOrWhiteSpace(sourceActorId) ? AbilityActorIdentityUtility.ResolveActorId(source) : sourceActorId;
            TargetActorId = string.IsNullOrWhiteSpace(targetActorId) ? AbilityActorIdentityUtility.ResolveActorId(target) : targetActorId;
        }

        public AbilityDefinition Ability { get; }
        public GameObject Source { get; }
        public GameObject Target { get; }
        public Vector3 SourcePosition { get; }
        public Vector3 TargetPosition { get; }
        public Vector3 Direction { get; }
        public ItemDefinition SourceItem { get; }
        public string SourceItemInstanceId { get; }
        public float MagnitudeMultiplier { get; }
        public string ExecutionId { get; }
        public string SourceActorId { get; }
        public string TargetActorId { get; }

        public EffectExecutionContext WithExecutionId(string executionId)
        {
            return new EffectExecutionContext(Ability, Source, Target, SourcePosition, TargetPosition, Direction, SourceItem, SourceItemInstanceId, MagnitudeMultiplier, executionId, SourceActorId, TargetActorId);
        }
    }
}
