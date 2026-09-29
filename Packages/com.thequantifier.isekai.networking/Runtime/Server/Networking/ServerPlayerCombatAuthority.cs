using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Networking.Server
{
    [DisallowMultipleComponent]
    public sealed class ServerPlayerCombatAuthority : MonoBehaviour
    {
        private const float AttackOriginHeight = 1.4f;
        private readonly Dictionary<string, double> cooldowns = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly HashSet<string> knownSpellIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<PendingProjectile> projectiles = new List<PendingProjectile>();

        private NetworkPlayerCombat networkCombat;
        private NetworkPlayerVitals networkVitals;
        private ServerPlayerInventoryAuthority inventoryAuthority;
        private ServerCombatWorldAuthority worldAuthority;
        private DefinitionRegistry registry;
        private MeleeWeaponData unarmedAttack;
        private float sourceAttackPower;
        private bool configured;

        public void Configure(
            NetworkPlayerCombat replicatedCombat,
            NetworkPlayerVitals replicatedVitals,
            ServerPlayerInventoryAuthority authoritativeInventory,
            ServerCombatWorldAuthority authoritativeWorld,
            DefinitionRegistry definitionRegistry,
            MeleeWeaponData fallbackUnarmedAttack,
            IEnumerable<SpellDefinition> knownSpells,
            float authoritativeAttackPower)
        {
            if (configured) throw new InvalidOperationException("Server player combat authority is already configured.");
            networkCombat = replicatedCombat ?? throw new ArgumentNullException(nameof(replicatedCombat));
            networkVitals = replicatedVitals ?? throw new ArgumentNullException(nameof(replicatedVitals));
            inventoryAuthority = authoritativeInventory ?? throw new ArgumentNullException(nameof(authoritativeInventory));
            worldAuthority = authoritativeWorld ?? throw new ArgumentNullException(nameof(authoritativeWorld));
            registry = definitionRegistry ?? throw new ArgumentNullException(nameof(definitionRegistry));
            unarmedAttack = fallbackUnarmedAttack;
            sourceAttackPower = Mathf.Max(0f, authoritativeAttackPower);
            knownSpellIds.Clear();
            foreach (SpellDefinition spell in knownSpells ?? Array.Empty<SpellDefinition>())
            {
                if (spell != null && !string.IsNullOrWhiteSpace(spell.Id)) knownSpellIds.Add(spell.Id);
            }

            networkCombat.ServerCommandHandler = ExecuteCommand;
            configured = true;
        }

        private void OnDestroy()
        {
            if (networkCombat != null) networkCombat.ServerCommandHandler = null;
        }

        private void Update()
        {
            if (!configured || projectiles.Count == 0) return;
            float delta = Time.unscaledDeltaTime;
            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                PendingProjectile projectile = projectiles[i];
                if (now >= projectile.ExpiresAt)
                {
                    networkCombat.PublishServerResult(NetworkCombatCommandResult.Success(
                        projectile.CommandSequence, projectile.Spell.Id, string.Empty, 0f, $"{projectile.Spell.DisplayName} expired without hitting a target."));
                    projectiles.RemoveAt(i);
                    continue;
                }

                float distance = projectile.Speed * delta;
                if (TryFirstHit(projectile.Position, projectile.Direction, projectile.Radius, distance, out RaycastHit hit))
                {
                    ResolveProjectileImpact(projectile, hit);
                    projectiles.RemoveAt(i);
                    continue;
                }

                projectile.Position += projectile.Direction * distance;
                projectiles[i] = projectile;
            }
        }

        private NetworkCombatCommandResult ExecuteCommand(NetworkCombatCommand command)
        {
            try
            {
                if (networkVitals.IsDefeated)
                {
                    return Reject(command, CombatAuthorityFailure.ActionUnavailable, "A defeated player cannot act.");
                }

                if (!ValidateAimAgainstActor(command.AimDirection))
                {
                    return Reject(command, CombatAuthorityFailure.InvalidAim, "Aim direction is too far from the authoritative player facing.");
                }

                return command.CommandType switch
                {
                    CombatAuthorityCommandType.PrimaryAttack => ExecutePrimaryAttack(command),
                    CombatAuthorityCommandType.CastAbility => ExecuteAbility(command),
                    _ => Reject(command, CombatAuthorityFailure.InvalidCommand, "Unsupported combat command.")
                };
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return Reject(command, CombatAuthorityFailure.ServerRejected, "The server could not complete the combat action.");
            }
        }

        private NetworkCombatCommandResult ExecutePrimaryAttack(NetworkCombatCommand command)
        {
            CombatWeaponSelection selection = ResolveWeapon();
            if (!selection.IsValid)
            {
                return Reject(command, CombatAuthorityFailure.ActionUnavailable, "The authoritative attack has no valid weapon definition.");
            }

            if (selection.IsRanged)
            {
                return Reject(command, CombatAuthorityFailure.DeferredTransaction,
                    "Online ranged weapon attacks remain disabled until authoritative ammunition and world-projectile transactions are enabled.");
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (!TryBeginCooldown(selection.Execution, now, out string cooldownFailure))
            {
                return Reject(command, CombatAuthorityFailure.CooldownActive, cooldownFailure);
            }

            float staminaCost = selection.Melee.StaminaCost;
            if (staminaCost > 0f && !networkVitals.TrySpendStaminaServer(staminaCost))
            {
                CancelCooldown(selection.Execution);
                return Reject(command, CombatAuthorityFailure.InsufficientResource, "Not enough stamina for that attack.");
            }

            Vector3 origin = transform.position + Vector3.up * AttackOriginHeight;
            if (!TryFirstHit(origin, command.AimDirection, selection.Melee.HitRadius, selection.Melee.AttackRange, out RaycastHit hit)
                || !worldAuthority.TryResolveCombatant(hit.collider, out EnemyHealth target, out string targetId))
            {
                return NetworkCombatCommandResult.Success(command.Sequence, selection.Execution.Id, string.Empty, 0f, $"{selection.Melee.AttackName} missed.");
            }

            float amount = Mathf.Max(0f, selection.Melee.BaseDamage + sourceAttackPower);
            DamagePacket packet = DamagePacket.Single(gameObject, new DamageComponent(selection.Melee.DamageType, amount));
            DamageResult result = target.ApplyDamage(new DamageInfo(packet, hit.point, command.AimDirection));
            worldAuthority.PublishSnapshotNow();
            return result.Applied
                ? NetworkCombatCommandResult.Success(command.Sequence, selection.Execution.Id, targetId, result.AppliedAmount, result.Message)
                : Reject(command, CombatAuthorityFailure.InvalidTarget, result.Message);
        }

        private NetworkCombatCommandResult ExecuteAbility(NetworkCombatCommand command)
        {
            string spellId = command.ActionIdText;
            if (!knownSpellIds.Contains(spellId) || !registry.TryGet(spellId, out SpellDefinition spell) || spell?.Ability?.Execution == null)
            {
                return Reject(command, CombatAuthorityFailure.ActionUnavailable, "The requested spell is not known by this player.");
            }

            if (!TryValidateAbilityCosts(spell.Ability.Execution, out float manaCost, out string costFailure))
            {
                return Reject(command, CombatAuthorityFailure.DeferredTransaction, costFailure);
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (!TryBeginCooldown(spell.Ability.Execution, now, out string cooldownFailure))
            {
                return Reject(command, CombatAuthorityFailure.CooldownActive, cooldownFailure);
            }

            if (manaCost > 0f && !networkVitals.TrySpendManaServer(manaCost))
            {
                CancelCooldown(spell.Ability.Execution);
                return Reject(command, CombatAuthorityFailure.InsufficientResource, $"Not enough mana to cast {spell.DisplayName}.");
            }

            Vector3 origin = transform.position + Vector3.up * AttackOriginHeight;
            if (spell.Ability.DeliveryMode == AbilityDeliveryMode.Projectile)
            {
                AbilityProjectileDelivery delivery = spell.Ability.ProjectileDelivery;
                if (delivery == null)
                {
                    CancelCooldown(spell.Ability.Execution);
                    if (manaCost > 0f) networkVitals.TryRestoreManaServer(manaCost);
                    return Reject(command, CombatAuthorityFailure.ActionUnavailable, $"{spell.DisplayName} has no projectile delivery configuration.");
                }

                projectiles.Add(new PendingProjectile(
                    command.Sequence,
                    spell,
                    origin,
                    command.AimDirection,
                    delivery.ProjectileSpeed,
                    0.12f,
                    now + delivery.MaximumLifetime));
                return NetworkCombatCommandResult.Success(command.Sequence, spell.Id, string.Empty, 0f, $"Cast {spell.DisplayName}.");
            }

            float range = Mathf.Max(0.1f, spell.Ability.Range);
            if (!TryFirstHit(origin, command.AimDirection, 0.01f, range, out RaycastHit hit)
                || !worldAuthority.TryResolveCombatant(hit.collider, out EnemyHealth target, out string targetId))
            {
                return NetworkCombatCommandResult.Success(command.Sequence, spell.Id, string.Empty, 0f, $"{spell.DisplayName} found no valid target.");
            }

            AbilityExecutionResult result = ExecuteAbilityEffects(command.Sequence, spell, target, hit.point, command.AimDirection, out float appliedAmount);
            worldAuthority.PublishSnapshotNow();
            return result.Succeeded
                ? NetworkCombatCommandResult.Success(command.Sequence, spell.Id, targetId, appliedAmount, result.Message)
                : Reject(command, CombatAuthorityFailure.InvalidTarget, result.Message);
        }

        private void ResolveProjectileImpact(PendingProjectile projectile, RaycastHit hit)
        {
            if (!worldAuthority.TryResolveCombatant(hit.collider, out EnemyHealth target, out string targetId))
            {
                networkCombat.PublishServerResult(NetworkCombatCommandResult.Success(
                    projectile.CommandSequence, projectile.Spell.Id, string.Empty, 0f, $"{projectile.Spell.DisplayName} was blocked."));
                return;
            }

            AbilityExecutionResult result = ExecuteAbilityEffects(
                projectile.CommandSequence, projectile.Spell, target, hit.point, projectile.Direction, out float appliedAmount);
            worldAuthority.PublishSnapshotNow();
            networkCombat.PublishServerResult(result.Succeeded
                ? NetworkCombatCommandResult.Success(projectile.CommandSequence, projectile.Spell.Id, targetId, appliedAmount, result.Message)
                : NetworkCombatCommandResult.Reject(projectile.CommandSequence, CombatAuthorityFailure.InvalidTarget, result.Message, projectile.Spell.Id));
        }

        private AbilityExecutionResult ExecuteAbilityEffects(uint sequence, SpellDefinition spell, EnemyHealth target, Vector3 hitPoint, Vector3 direction, out float appliedAmount)
        {
            float before = target == null ? 0f : target.CurrentHealth;
            NetworkPlayerActor actor = GetComponent<NetworkPlayerActor>();
            EffectExecutionContext context = new EffectExecutionContext(
                spell.Ability,
                gameObject,
                target == null ? null : target.gameObject,
                transform.position,
                hitPoint,
                direction,
                executionId: $"network-combat.{actor?.ActorId ?? "player"}.{sequence}",
                sourceActorId: actor?.ActorId ?? string.Empty);
            AbilityExecutionResult result = AbilityEffectPipeline.Execute(in context, spell.Ability.Effects);
            appliedAmount = target == null ? 0f : Mathf.Max(0f, before - target.CurrentHealth);
            return result;
        }

        private bool TryFirstHit(Vector3 origin, Vector3 direction, float radius, float distance, out RaycastHit validHit)
        {
            RaycastHit[] hits = Physics.SphereCastAll(origin, Mathf.Max(0.01f, radius), direction, Mathf.Max(0.01f, distance), ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                validHit = hit;
                return true;
            }

            validHit = default;
            return false;
        }

        private bool ValidateAimAgainstActor(Vector3 aimDirection)
        {
            Vector3 planarAim = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return planarAim.sqrMagnitude <= 0.0001f
                || forward.sqrMagnitude <= 0.0001f
                || Vector3.Angle(forward, planarAim) <= CombatAuthorityLimits.MaximumAimYawDelta;
        }

        private bool TryBeginCooldown(CombatExecutionDefinition execution, double now, out string failure)
        {
            string key = execution == null ? string.Empty : execution.ResolveCooldownKey();
            if (execution == null || string.IsNullOrWhiteSpace(key))
            {
                failure = "Combat execution definition is unavailable.";
                return false;
            }

            if (cooldowns.TryGetValue(key, out double readyAt) && now < readyAt)
            {
                failure = $"{execution.DisplayName} is on cooldown for {readyAt - now:0.00} more seconds.";
                return false;
            }

            cooldowns[key] = now + execution.CooldownDuration;
            failure = string.Empty;
            return true;
        }

        private void CancelCooldown(CombatExecutionDefinition execution)
        {
            if (execution != null) cooldowns.Remove(execution.ResolveCooldownKey());
        }

        private static bool TryValidateAbilityCosts(CombatExecutionDefinition execution, out float manaCost, out string failure)
        {
            manaCost = 0f;
            foreach (CombatExecutionCostDefinition cost in execution.Costs)
            {
                if (!cost.Consumed || cost.Amount <= 0f) continue;
                if (cost.CostType == CombatExecutionCostType.Resource && cost.Resource != null && cost.Resource.Id == ResourceIds.Mana)
                {
                    manaCost += cost.Amount;
                    continue;
                }

                failure = $"{execution.DisplayName} uses a cost transaction that is not authoritative in this group yet.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private CombatWeaponSelection ResolveWeapon()
        {
            EquipmentSlotState mainHand = inventoryAuthority.Equipment.GetSlot(EquipmentSlotType.MainHand);
            if (mainHand != null && !mainHand.IsEmpty && mainHand.Item?.Equipment != null)
            {
                if (mainHand.Item.Equipment.RangedWeapon?.IsWeapon == true)
                    return CombatWeaponSelection.CreateRanged(mainHand.Item.Equipment.RangedWeapon);
                if (mainHand.Item.Equipment.MeleeWeapon?.IsWeapon == true)
                    return CombatWeaponSelection.CreateMelee(mainHand.Item.Equipment.MeleeWeapon);
            }

            return CombatWeaponSelection.CreateMelee(unarmedAttack);
        }

        private static NetworkCombatCommandResult Reject(NetworkCombatCommand command, CombatAuthorityFailure failure, string message)
            => NetworkCombatCommandResult.Reject(command.Sequence, failure, message, command.ActionIdText);

        private readonly struct CombatWeaponSelection
        {
            private CombatWeaponSelection(MeleeWeaponData melee, RangedWeaponData ranged)
            {
                Melee = melee;
                Ranged = ranged;
            }
            public MeleeWeaponData Melee { get; }
            public RangedWeaponData Ranged { get; }
            public bool IsRanged => Ranged?.IsWeapon == true;
            public bool IsValid => IsRanged ? Ranged.Execution != null && Ranged.DamageType != null : Melee?.IsWeapon == true && Melee.Execution != null && Melee.DamageType != null;
            public CombatExecutionDefinition Execution => IsRanged ? Ranged.Execution : Melee.Execution;
            public static CombatWeaponSelection CreateMelee(MeleeWeaponData melee) => new CombatWeaponSelection(melee, null);
            public static CombatWeaponSelection CreateRanged(RangedWeaponData ranged) => new CombatWeaponSelection(null, ranged);
        }

        private struct PendingProjectile
        {
            public PendingProjectile(uint commandSequence, SpellDefinition spell, Vector3 position, Vector3 direction, float speed, float radius, double expiresAt)
            {
                CommandSequence = commandSequence;
                Spell = spell;
                Position = position;
                Direction = direction.normalized;
                Speed = Mathf.Max(0.1f, speed);
                Radius = Mathf.Max(0.01f, radius);
                ExpiresAt = expiresAt;
            }
            public uint CommandSequence;
            public SpellDefinition Spell;
            public Vector3 Position;
            public Vector3 Direction;
            public float Speed;
            public float Radius;
            public double ExpiresAt;
        }
    }
}
