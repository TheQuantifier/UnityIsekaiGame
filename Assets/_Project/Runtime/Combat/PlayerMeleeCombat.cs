using System;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat
{
    public sealed class PlayerMeleeCombat : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private PrototypePersistenceServiceBehaviour runtimeServices;
        [SerializeField] private LayerMask damageMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private MeleeWeaponData unarmedAttack = new MeleeWeaponData();

        public event Action<MeleeAttackResult> AttackResolved;

        private CombatExecutionService Execution => runtimeServices == null ? null : runtimeServices.CombatExecution;

        private void Awake()
        {
            input = input == null ? GetComponent<PlayerInputReader>() : input;
            equipment = equipment == null ? GetComponent<PlayerEquipment>() : equipment;
            inventory = inventory == null ? GetComponent<PlayerInventory>() : inventory;
            runtimeServices = runtimeServices == null ? FindAnyObjectByType<PrototypePersistenceServiceBehaviour>() : runtimeServices;
            if (attackOrigin == null && Camera.main != null) attackOrigin = Camera.main.transform;
        }

        private void OnValidate() => unarmedAttack?.Validate();

        private void Update()
        {
            if (input != null && input.ConsumeAttack()) TryAttack();
        }

        public MeleeAttackResult TryAttack()
        {
            if (!ActorLifecycleUtility.CanAct(gameObject)) return Resolve(MeleeAttackResult.Failure("Cannot attack while defeated, unconscious, or dead."));
            if (attackOrigin == null) return Resolve(MeleeAttackResult.Failure("No attack origin is assigned."));
            if (Execution == null) return Resolve(MeleeAttackResult.Failure("Combat execution services are unavailable."));

            CombatWeaponSelection selection = GetCurrentWeaponData();
            if (!selection.HasWeapon || selection.Execution == null || selection.DamageType == null)
            {
                return Resolve(MeleeAttackResult.Failure("The selected attack is missing its weapon, execution, or canonical damage type."));
            }

            if (selection.IsRanged && selection.RangedWeapon.AmmoItem != null &&
                (inventory == null || inventory.CountItem(selection.RangedWeapon.AmmoItem) <= 0))
            {
                return Resolve(MeleeAttackResult.Failure($"No {selection.RangedWeapon.AmmoItem.DisplayName} available."));
            }

            float damageAmount = CombatStatUtility.CalculatePreMitigationDamage(selection.BaseDamage, gameObject, AttackPowerScalingPolicy.AddSourceAttackPower);
            HitCandidate hit = selection.IsRanged ? default : FindMeleeHit(selection.MeleeWeapon);
            AttackResolutionRequest? attack = hit.IsDamageable
                ? CreateAttackRequest(selection, hit.Target, damageAmount, hit.Distance, $"player-attack.{Guid.NewGuid():N}")
                : null;
            object payload = attack.HasValue ? attack.Value : null;
            CombatExecutionResult begin = Execution.BeginExecution(new CombatExecutionBeginRequest(
                $"player-execution.begin.{Guid.NewGuid():N}", selection.Execution, gameObject, now: Time.time, authorityValidated: true, payload: payload));
            if (!begin.Succeeded || begin.State == null) return Resolve(MeleeAttackResult.Failure(begin.Message));

            bool ammoConsumed = false;
            if (selection.IsRanged && selection.RangedWeapon.AmmoItem != null)
            {
                ammoConsumed = inventory.RemoveItem(selection.RangedWeapon.AmmoItem, 1);
                if (!ammoConsumed)
                {
                    Execution.CancelExecution(new CombatExecutionCancelRequest($"player-execution.cancel.{Guid.NewGuid():N}", begin.State.ExecutionInstanceId, gameObject, begin.ActorId, now: Time.time));
                    return Resolve(MeleeAttackResult.Failure($"Could not consume {selection.RangedWeapon.AmmoItem.DisplayName}."));
                }
            }

            CombatExecutionResult commit = Execution.CommitExecution(new CombatExecutionCommitRequest(
                $"player-execution.commit.{Guid.NewGuid():N}", begin.State.ExecutionInstanceId, gameObject, begin.ActorId,
                Mathf.Max(Time.time, begin.State.ReadyAt), authorityValidated: true));
            if (!commit.Succeeded)
            {
                if (ammoConsumed) inventory.AddItem(selection.RangedWeapon.AmmoItem, 1);
                return Resolve(MeleeAttackResult.Failure(commit.Message));
            }

            if (selection.IsRanged) return Resolve(FireProjectile(selection.RangedWeapon, selection.Execution, damageAmount));
            if (!hit.IsDamageable) return Resolve(MeleeAttackResult.Miss(selection.AttackName, damageAmount, hit.Collider == null ? $"{selection.AttackName} missed." : $"{selection.AttackName} was blocked."));
            if (commit.UnderlyingResult is not AttackResolutionResult attackResult) return Resolve(MeleeAttackResult.Failure("Attack execution returned no attack result."));

            DamageResult damageResult = DamageResult.FromApplication(attackResult.DamageResult, attackResult.Message);
            return Resolve(attackResult.DamageApplied
                ? MeleeAttackResult.Hit(selection.AttackName, damageAmount, hit.Target, damageResult, attackResult.Message)
                : MeleeAttackResult.Miss(selection.AttackName, damageAmount, attackResult.Message));
        }

        public void ResetCooldown()
        {
            if (Execution != null) Execution.ClearTransientStateForRestore(ResolveActorId(gameObject));
        }

        private HitCandidate FindMeleeHit(MeleeWeaponData weapon)
        {
            RaycastHit[] hits = Physics.SphereCastAll(attackOrigin.position, weapon.HitRadius, attackOrigin.forward, weapon.AttackRange, damageMask, triggerInteraction);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                CharacterResourceCollection resources = hit.collider.GetComponentInParent<CharacterResourceCollection>();
                return new HitCandidate(hit.collider, resources == null ? null : resources.gameObject, hit.distance);
            }
            return default;
        }

        private AttackResolutionRequest CreateAttackRequest(CombatWeaponSelection selection, GameObject target, float amount, float distance, string transactionId)
        {
            return new AttackResolutionRequest(
                transactionId, selection.IsUnarmed ? AttackSourceType.Unarmed : AttackSourceType.Weapon,
                gameObject, ResolveActorId(gameObject), target, ResolveActorId(target), selection.DamageType, amount,
                UnityEngine.Random.value, UnityEngine.Random.value, baseHitChance: 1f,
                hasSuppliedDistance: true, suppliedDistance: distance, hasMaximumRange: true, maximumRange: selection.AttackRange,
                suppliedLineOfSight: true, hasSuppliedLineOfSight: true,
                originatingActionId: selection.Execution.Id,
                originatingItemOrWeaponId: selection.EquippedItem == null ? "unarmed" : selection.EquippedItem.Id,
                authorityValidated: true);
        }

        private MeleeAttackResult FireProjectile(RangedWeaponData weapon, CombatExecutionDefinition execution, float damageAmount)
        {
            Vector3 origin = attackOrigin.TransformPoint(weapon.LaunchOffset);
            Vector3 direction = attackOrigin.forward.sqrMagnitude > 0f ? attackOrigin.forward.normalized : transform.forward;
            SpellProjectile projectile = weapon.ProjectilePrefab == null ? CreateRuntimeProjectile(origin, Quaternion.LookRotation(direction), weapon) : Instantiate(weapon.ProjectilePrefab, origin, Quaternion.LookRotation(direction));
            if (projectile == null) return MeleeAttackResult.Failure("Invalid ranged projectile configuration.");
            if (weapon.ProjectileVisualPrefab != null)
            {
                GameObject visual = Instantiate(weapon.ProjectileVisualPrefab, projectile.transform);
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            projectile.Initialize(gameObject, direction, weapon.ProjectileSpeed, weapon.ProjectileLifetime,
                (target, hitPoint) => ApplyRangedImpact(target, hitPoint, direction, weapon, execution, damageAmount));
            return MeleeAttackResult.Miss(weapon.AttackName, damageAmount, $"{weapon.AttackName} fired.");
        }

        private void ApplyRangedImpact(GameObject target, Vector3 hitPoint, Vector3 direction, RangedWeaponData weapon, CombatExecutionDefinition execution, float damageAmount)
        {
            if (target == null || runtimeServices?.AttackResolution == null) return;
            AttackResolutionRequest request = CreateAttackRequest(CombatWeaponSelection.Ranged(null, true, weapon), target, damageAmount,
                Vector3.Distance(transform.position, hitPoint), $"player-ranged-impact.{Guid.NewGuid():N}");
            AttackResolutionResult attack = runtimeServices.AttackResolution.ExecuteAttack(request);
            DamageResult damageResult = DamageResult.FromApplication(attack.DamageResult, attack.Message);
            Resolve(attack.DamageApplied
                ? MeleeAttackResult.Hit(weapon.AttackName, damageAmount, target, damageResult, attack.Message)
                : MeleeAttackResult.Miss(weapon.AttackName, damageAmount, attack.Message));
        }

        private static SpellProjectile CreateRuntimeProjectile(Vector3 origin, Quaternion rotation, RangedWeaponData weapon)
        {
            GameObject projectileObject = new GameObject($"{weapon.AttackName} Projectile");
            projectileObject.transform.SetPositionAndRotation(origin, rotation);
            projectileObject.AddComponent<SphereCollider>().radius = weapon.ProjectileHitRadius;
            Rigidbody body = projectileObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return projectileObject.AddComponent<SpellProjectile>();
        }

        private CombatWeaponSelection GetCurrentWeaponData()
        {
            EquipmentSlotState mainHand = equipment == null ? null : equipment.GetSlot(EquipmentSlotType.MainHand);
            if (mainHand != null && !mainHand.IsEmpty)
            {
                ItemDefinition item = mainHand.Item;
                if (item == null || !item.IsEquippable) return CombatWeaponSelection.Invalid(item, true);
                if (item.Equipment.RangedWeapon?.IsWeapon == true) return CombatWeaponSelection.Ranged(item, true, item.Equipment.RangedWeapon);
                if (item.Equipment.MeleeWeapon?.IsWeapon == true) return CombatWeaponSelection.Melee(item, true, item.Equipment.MeleeWeapon);
                return CombatWeaponSelection.Invalid(item, true);
            }
            return unarmedAttack?.IsWeapon == true ? CombatWeaponSelection.Melee(null, false, unarmedAttack) : CombatWeaponSelection.Invalid(null, false);
        }

        private static string ResolveActorId(GameObject actor)
        {
            CharacterSystemCoordinator character = actor == null ? null : actor.GetComponentInParent<CharacterSystemCoordinator>();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId)) return character.ActorId;
            WorldEntityIdentity identity = actor == null ? null : actor.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        private MeleeAttackResult Resolve(MeleeAttackResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Message)) Debug.Log(result.Message);
            AttackResolved?.Invoke(result);
            return result;
        }

        private readonly struct HitCandidate
        {
            public HitCandidate(Collider collider, GameObject target, float distance) { Collider = collider; Target = target; Distance = distance; }
            public Collider Collider { get; }
            public GameObject Target { get; }
            public float Distance { get; }
            public bool IsDamageable => Target != null;
        }

        private readonly struct CombatWeaponSelection
        {
            private CombatWeaponSelection(ItemDefinition item, bool equipped, MeleeWeaponData melee, RangedWeaponData ranged) { EquippedItem = item; HasEquippedMainHandItem = equipped; MeleeWeapon = melee; RangedWeapon = ranged; }
            public ItemDefinition EquippedItem { get; }
            public bool HasEquippedMainHandItem { get; }
            public MeleeWeaponData MeleeWeapon { get; }
            public RangedWeaponData RangedWeapon { get; }
            public bool IsRanged => RangedWeapon?.IsWeapon == true;
            public bool IsUnarmed => !IsRanged && EquippedItem == null;
            public bool HasWeapon => IsRanged || MeleeWeapon?.IsWeapon == true;
            public string AttackName => IsRanged ? RangedWeapon.AttackName : MeleeWeapon.AttackName;
            public float BaseDamage => IsRanged ? RangedWeapon.BaseDamage : MeleeWeapon.BaseDamage;
            public float AttackRange => IsRanged ? RangedWeapon.ProjectileSpeed * RangedWeapon.ProjectileLifetime : MeleeWeapon.AttackRange;
            public DamageTypeDefinition DamageType => IsRanged ? RangedWeapon.DamageType : MeleeWeapon.DamageType;
            public CombatExecutionDefinition Execution => IsRanged ? RangedWeapon.Execution : MeleeWeapon.Execution;
            public static CombatWeaponSelection Invalid(ItemDefinition item, bool equipped) => new CombatWeaponSelection(item, equipped, null, null);
            public static CombatWeaponSelection Melee(ItemDefinition item, bool equipped, MeleeWeaponData weapon) => new CombatWeaponSelection(item, equipped, weapon, null);
            public static CombatWeaponSelection Ranged(ItemDefinition item, bool equipped, RangedWeaponData weapon) => new CombatWeaponSelection(item, equipped, null, weapon);
        }
    }
}
