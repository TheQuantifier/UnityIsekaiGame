using System;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat
{
    public sealed class EnemyMeleeAttack : MonoBehaviour
    {
        [SerializeField] private EnemyHealth health;
        [SerializeField] private CombatExecutionDefinition execution;
        [SerializeField] private PrototypePersistenceServiceBehaviour runtimeServices;
        [SerializeField, Min(0f)] private float damage = 12f;
        [SerializeField] private AttackPowerScalingPolicy attackPowerScaling = AttackPowerScalingPolicy.AddSourceAttackPower;
        [SerializeField] private DamageTypeDefinition damageType;
        [SerializeField, Min(0.1f)] private float attackRange = 1.6f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        public float AttackRange => attackRange;
        public event Action<DamageResult> AttackResolved;

        private CombatExecutionService Execution => runtimeServices == null ? null : runtimeServices.CombatExecution;

        private void Awake()
        {
            health = health == null ? GetComponent<EnemyHealth>() : health;
            runtimeServices = runtimeServices == null ? FindAnyObjectByType<PrototypePersistenceServiceBehaviour>() : runtimeServices;
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            attackRange = Mathf.Max(0.1f, attackRange);
        }

        public bool CanAttack(Transform target)
        {
            if (!CanAttempt(target, out _))
            {
                return false;
            }

            AttackResolutionRequest payload = CreateAttackRequest(target, $"enemy-melee.preview.{Guid.NewGuid():N}");
            CombatExecutionResult preview = Execution.PreviewBeginExecution(new CombatExecutionBeginRequest(
                $"enemy-execution.preview.{Guid.NewGuid():N}", execution, gameObject, now: Time.time, authorityValidated: true, payload: payload));
            return preview.Succeeded;
        }

        public DamageResult TryAttack(Transform target)
        {
            if (!CanAttempt(target, out string failure))
            {
                return Resolve(DamageResult.Failure(damage, failure));
            }

            AttackResolutionRequest payload = CreateAttackRequest(target, $"enemy-melee.{Guid.NewGuid():N}");
            CombatExecutionResult begin = Execution.BeginExecution(new CombatExecutionBeginRequest(
                $"enemy-execution.begin.{Guid.NewGuid():N}", execution, gameObject, now: Time.time, authorityValidated: true, payload: payload));
            if (!begin.Succeeded || begin.State == null)
            {
                return Resolve(DamageResult.Failure(damage, begin.Message));
            }

            CombatExecutionResult commit = Execution.CommitExecution(new CombatExecutionCommitRequest(
                $"enemy-execution.commit.{Guid.NewGuid():N}", begin.State.ExecutionInstanceId, gameObject, begin.ActorId,
                Mathf.Max(Time.time, begin.State.ReadyAt), authorityValidated: true));
            if (!commit.Succeeded || commit.UnderlyingResult is not AttackResolutionResult attack)
            {
                return Resolve(DamageResult.Failure(damage, commit.Message));
            }

            DamageResult result = ToDamageResult(attack);
            Debug.Log(result.Applied ? $"{name} attacked {target.name} for {result.AppliedAmount:0.#} damage." : result.Message);
            return Resolve(result);
        }

        public void ResetCooldown()
        {
            if (Execution != null && execution != null)
            {
                Execution.ClearTransientStateForRestore(ResolveActorId(gameObject));
            }
        }

        private bool CanAttempt(Transform target, out string failure)
        {
            failure = string.Empty;
            if (target == null) failure = "Enemy attack has no target.";
            else if (PrototypeGameplayModalState.IsModalActive) failure = "Enemy attack is paused by a modal screen.";
            else if (health != null && health.IsDefeated) failure = $"{name} is defeated and cannot attack.";
            else if (!ActorLifecycleUtility.CanAct(gameObject)) failure = $"{name} cannot act.";
            else if (execution == null) failure = "Enemy attack has no combat execution definition.";
            else if (damageType == null) failure = "Enemy attack has no canonical damage type.";
            else if (Execution == null) failure = "Combat execution services are unavailable.";
            else if (GetPlanarDistanceTo(target) > attackRange) failure = "Enemy target is outside attack range.";
            else if (!HasLineOfSight(target)) failure = "Enemy target is blocked by line of sight.";
            return string.IsNullOrWhiteSpace(failure);
        }

        private AttackResolutionRequest CreateAttackRequest(Transform target, string transactionId)
        {
            float amount = CombatStatUtility.CalculatePreMitigationDamage(damage, gameObject, attackPowerScaling);
            return new AttackResolutionRequest(
                transactionId, AttackSourceType.Unarmed, gameObject, ResolveActorId(gameObject), target.gameObject, ResolveActorId(target.gameObject),
                damageType, amount, UnityEngine.Random.value, UnityEngine.Random.value,
                baseHitChance: 1f, hasSuppliedDistance: true, suppliedDistance: GetPlanarDistanceTo(target),
                hasMaximumRange: true, maximumRange: attackRange,
                suppliedLineOfSight: true, hasSuppliedLineOfSight: true,
                originatingActionId: execution.Id, authorityValidated: true);
        }

        private bool HasLineOfSight(Transform target)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 destination = target.position + Vector3.up;
            Vector3 offset = destination - origin;
            RaycastHit hit = Physics.RaycastAll(origin, offset.normalized, offset.magnitude, lineOfSightMask, QueryTriggerInteraction.Ignore)
                .OrderBy(candidate => candidate.distance)
                .FirstOrDefault(candidate => candidate.collider != null && !candidate.collider.transform.IsChildOf(transform));
            return hit.collider == null || hit.collider.transform.IsChildOf(target) || target.IsChildOf(hit.collider.transform);
        }

        private float GetPlanarDistanceTo(Transform target)
        {
            Vector3 offset = target.position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        private static string ResolveActorId(GameObject actor)
        {
            CharacterSystemCoordinator character = actor == null ? null : actor.GetComponentInParent<CharacterSystemCoordinator>();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId)) return character.ActorId;
            WorldEntityIdentity identity = actor == null ? null : actor.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        private static DamageResult ToDamageResult(AttackResolutionResult attack)
        {
            DamageApplicationResult damageResult = attack.DamageResult;
            if (!attack.Succeeded || damageResult == null)
            {
                return DamageResult.Failure(attack.RequestedBaseDamage, attack.Message);
            }

            return DamageResult.FromApplication(damageResult, attack.Message);
        }

        private DamageResult Resolve(DamageResult result)
        {
            AttackResolved?.Invoke(result);
            return result;
        }
    }
}
