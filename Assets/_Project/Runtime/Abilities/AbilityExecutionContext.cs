using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Abilities
{
    public readonly struct AbilityExecutionContext
    {
        public AbilityExecutionContext(
            AbilityDefinition ability,
            GameObject source,
            GameObject target,
            Transform deliveryOrigin,
            Vector3 sourcePosition,
            Vector3 targetPosition,
            Vector3 direction,
            bool gameplayBlocked = false,
            ItemDefinition sourceItem = null,
            string sourceItemInstanceId = "",
            float magnitudeMultiplier = 1f,
            Action<UnityIsekaiGame.Magic.SpellProjectile> projectileSpawned = null)
        {
            Ability = ability;
            Source = source;
            Target = target;
            DeliveryOrigin = deliveryOrigin;
            SourcePosition = sourcePosition;
            TargetPosition = targetPosition;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            GameplayBlocked = gameplayBlocked;
            SourceItem = sourceItem;
            SourceItemInstanceId = sourceItemInstanceId ?? string.Empty;
            MagnitudeMultiplier = magnitudeMultiplier;
            ProjectileSpawned = projectileSpawned;
        }

        public AbilityDefinition Ability { get; }
        public GameObject Source { get; }
        public GameObject Target { get; }
        public Transform DeliveryOrigin { get; }
        public Vector3 SourcePosition { get; }
        public Vector3 TargetPosition { get; }
        public Vector3 Direction { get; }
        public bool GameplayBlocked { get; }
        public ItemDefinition SourceItem { get; }
        public string SourceItemInstanceId { get; }
        public float MagnitudeMultiplier { get; }
        public Action<UnityIsekaiGame.Magic.SpellProjectile> ProjectileSpawned { get; }

        public EffectExecutionContext ToEffectContext(GameObject targetOverride = null, Vector3 targetPositionOverride = default)
        {
            GameObject resolvedTarget = targetOverride == null ? Target : targetOverride;
            Vector3 resolvedTargetPosition = targetPositionOverride == default ? TargetPosition : targetPositionOverride;
            return new EffectExecutionContext(
                Ability,
                Source,
                resolvedTarget,
                SourcePosition,
                resolvedTargetPosition,
                Direction,
                SourceItem,
                SourceItemInstanceId,
                MagnitudeMultiplier);
        }
    }
}
