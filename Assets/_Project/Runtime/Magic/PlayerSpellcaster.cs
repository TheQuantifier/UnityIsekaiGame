using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;

namespace UnityIsekaiGame.Magic
{
    public sealed class PlayerSpellcaster : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform castOrigin;
        [SerializeField] private PlayerSpellLoadout loadout;
        [SerializeField] private SpellDefinition primarySpell;
        [SerializeField] private PrototypePersistenceServiceBehaviour runtimeServices;
        [SerializeField] private LayerMask aimMask = ~0;
        [SerializeField] private QueryTriggerInteraction aimTriggerInteraction = QueryTriggerInteraction.Ignore;

        private readonly List<SpellProjectile> activeProjectiles = new List<SpellProjectile>();
        private string pendingExecutionId;
        private string pendingActorId;
        private SpellDefinition pendingSpell;
        private CharacterAbilityCollection abilities;

        public event Action<SpellDefinition, SpellCastResult> SpellCastResolved;

        private CombatExecutionService Execution => runtimeServices == null ? null : runtimeServices.CombatExecution;

        private void Awake()
        {
            input = input == null ? GetComponent<PlayerInputReader>() : input;
            loadout = loadout == null ? GetComponent<PlayerSpellLoadout>() : loadout;
            runtimeServices = runtimeServices == null ? FindAnyObjectByType<PrototypePersistenceServiceBehaviour>() : runtimeServices;
            abilities = GetComponent<CharacterAbilityCollection>();
            if (castOrigin == null && Camera.main != null)
            {
                castOrigin = Camera.main.transform;
            }
        }

        private void Update()
        {
            Execution?.ProcessExecutionTime(Time.time);
            CommitPendingExecutionWhenReady();
            if (input != null && input.ConsumeCastPrimarySpell())
            {
                TryCastPrimarySpell();
            }
        }

        public SpellCastResult TryCastPrimarySpell()
        {
            SpellDefinition spell = GetCurrentSpell();
            if (spell == null || spell.Ability == null)
            {
                return Resolve(spell, SpellCastResult.Failure("No spell ability is assigned."));
            }

            if (spell.Ability.Execution == null)
            {
                return Resolve(spell, SpellCastResult.Failure($"{spell.DisplayName} has no combat execution definition."));
            }

            abilities = abilities == null ? GetComponent<CharacterAbilityCollection>() : abilities;
            if (abilities != null && !abilities.CanUseAbility(spell.Ability.Id))
            {
                return Resolve(spell, SpellCastResult.Failure($"{spell.DisplayName} is not owned by this character."));
            }

            if (Execution == null)
            {
                return Resolve(spell, SpellCastResult.Failure("Combat execution services are unavailable."));
            }

            if (!string.IsNullOrWhiteSpace(pendingExecutionId))
            {
                return Resolve(spell, SpellCastResult.Failure("A spell is already being cast."));
            }

            AbilityExecutionContext context = CreateContext(spell);
            CombatExecutionResult begin = Execution.BeginExecution(new CombatExecutionBeginRequest(
                $"spell.begin.{spell.Id}.{Guid.NewGuid():N}",
                spell.Ability.Execution,
                gameObject,
                now: Time.time,
                authorityValidated: true,
                payload: context));
            if (!begin.Succeeded || begin.State == null)
            {
                return Resolve(spell, SpellCastResult.Failure(begin.Message));
            }

            pendingExecutionId = begin.State.ExecutionInstanceId;
            pendingActorId = begin.ActorId;
            pendingSpell = spell;
            return begin.State.ReadyAt <= Time.time
                ? CommitPendingExecution()
                : Resolve(spell, SpellCastResult.Success($"Began casting {spell.DisplayName}."));
        }

        public void ResetSpellcasting()
        {
            if (!string.IsNullOrWhiteSpace(pendingExecutionId) && Execution != null)
            {
                Execution.CancelExecution(new CombatExecutionCancelRequest(
                    $"spell.cancel.{Guid.NewGuid():N}", pendingExecutionId, gameObject, pendingActorId, now: Time.time));
            }

            ClearPending();
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                SpellProjectile projectile = activeProjectiles[i];
                if (projectile != null)
                {
                    projectile.Completed -= HandleProjectileCompleted;
                    Destroy(projectile.gameObject);
                }
            }

            activeProjectiles.Clear();
        }

        private void CommitPendingExecutionWhenReady()
        {
            if (string.IsNullOrWhiteSpace(pendingExecutionId) || Execution == null)
            {
                return;
            }

            CombatExecutionStateSnapshot state = Execution.GetExecutionState(pendingActorId, pendingExecutionId);
            if (state == null)
            {
                ClearPending();
            }
            else if (Time.time >= state.ReadyAt)
            {
                CommitPendingExecution();
            }
        }

        private SpellCastResult CommitPendingExecution()
        {
            SpellDefinition spell = pendingSpell;
            CombatExecutionResult commit = Execution.CommitExecution(new CombatExecutionCommitRequest(
                $"spell.commit.{Guid.NewGuid():N}", pendingExecutionId, gameObject, pendingActorId, Time.time, authorityValidated: true));
            ClearPending();
            return Resolve(spell, commit.Succeeded
                ? SpellCastResult.Success($"Cast {spell.DisplayName}.")
                : SpellCastResult.Failure(commit.Message));
        }

        private AbilityExecutionContext CreateContext(SpellDefinition spell)
        {
            Vector3 sourcePosition = castOrigin == null ? transform.position : castOrigin.position;
            Vector3 directionOrigin = GetDirectionOrigin(spell, sourcePosition);
            Vector3 direction = castOrigin == null ? transform.forward : GetCastDirection(directionOrigin, GetAimDistance(spell));
            return new AbilityExecutionContext(
                spell.Ability, gameObject, null, castOrigin, sourcePosition,
                sourcePosition + direction * Mathf.Max(1f, spell.Ability.Range), direction,
                gameplayBlocked: input != null && input.GameplayInputBlocked,
                projectileSpawned: RegisterProjectile);
        }

        private Vector3 GetDirectionOrigin(SpellDefinition spell, Vector3 fallbackPosition)
        {
            AbilityProjectileDelivery delivery = spell?.Ability?.ProjectileDelivery;
            return castOrigin == null || spell.Ability.DeliveryMode != AbilityDeliveryMode.Projectile || delivery == null
                ? fallbackPosition
                : castOrigin.TransformPoint(delivery.CastPointOffset);
        }

        private static float GetAimDistance(SpellDefinition spell)
        {
            AbilityProjectileDelivery delivery = spell?.Ability?.ProjectileDelivery;
            return delivery == null ? 1f : Mathf.Max(1f, delivery.ProjectileSpeed * delivery.MaximumLifetime);
        }

        private Vector3 GetCastDirection(Vector3 spawnPosition, float maxDistance)
        {
            if (castOrigin == null)
            {
                return transform.forward;
            }

            Vector3 aimPoint = castOrigin.position + castOrigin.forward * Mathf.Max(1f, maxDistance);
            foreach (RaycastHit hit in Physics.RaycastAll(castOrigin.position, castOrigin.forward, maxDistance, aimMask, aimTriggerInteraction).OrderBy(candidate => candidate.distance))
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                aimPoint = hit.point;
                break;
            }

            Vector3 direction = aimPoint - spawnPosition;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : castOrigin.forward;
        }

        private void RegisterProjectile(SpellProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.Completed += HandleProjectileCompleted;
            activeProjectiles.Add(projectile);
        }

        private SpellDefinition GetCurrentSpell() => loadout == null ? primarySpell : loadout.SelectedSpell;

        private void HandleProjectileCompleted(SpellProjectile projectile)
        {
            if (projectile != null)
            {
                projectile.Completed -= HandleProjectileCompleted;
                activeProjectiles.Remove(projectile);
            }
        }

        private SpellCastResult Resolve(SpellDefinition spell, SpellCastResult result)
        {
            if (!result.Succeeded)
            {
                Debug.Log(result.Message);
                PrototypeHudMessageBus.Show(result.Message);
            }

            SpellCastResolved?.Invoke(spell, result);
            return result;
        }

        private void ClearPending()
        {
            pendingExecutionId = string.Empty;
            pendingActorId = string.Empty;
            pendingSpell = null;
        }
    }
}
