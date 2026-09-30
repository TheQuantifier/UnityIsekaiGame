# Feature 6.2 - Attack Resolution and Combat Outcomes

Feature 6.2 adds the logical attack-resolution layer above Feature 6.1.

Flow:

`Attack intent -> validation -> requirements -> supplied context -> Accuracy/Evasion -> deterministic hit roll -> miss OR critical evaluation -> DamageApplicationRequest -> DamageHealingService -> AttackResolutionResult -> post-resolution attack events`

## Runtime Ownership

`AttackResolutionService` is the production entry point for logical attacks:

- `PreviewAttack(request)` is advisory and read-only.
- `ExecuteAttack(request)` is the authoritative local alpha mutation boundary.
- Successful `Hit` and `CriticalHit` outcomes call Feature 6.1 through `IDamageHealingService`.
- Missed, invalid, and blocked attacks do not call damage execution.
- Feature 6.1 remains the owner of Health mutation, defense, resistance, immunity, true damage, resource events, and durable resource transaction IDs.

The service is constructible with an injected `IDamageHealingService`. It is not a singleton and does not depend on input, camera, HUD, animation, AI, or scene-specific hit detection.

## Request Lifecycle

`AttackResolutionRequest` carries:

- parent attack transaction ID;
- attacker and target actor/body IDs plus local GameObject references for current runtime resolution;
- attack source type;
- originating action, ability, item/weapon, spell/effect IDs;
- damage type and base damage;
- base hit chance;
- deterministic hit and critical rolls;
- critical chance and multiplier;
- optional supplied distance, maximum range, line-of-sight, and target-validity context;
- optional requirement set.

Ordinary `Weapon`, `Ability`, `Spell`, and `Unarmed` attacks require an attacker. `Environmental` and `Scripted` attacks may be source-less. Requests targeting stale actor/body IDs fail instead of redirecting to another body.

## Outcomes

Machine-readable outcomes:

- `Invalid`: malformed request or invalid runtime references.
- `Blocked`: structurally valid request prohibited by requirements or supplied context.
- `Miss`: legal attack resolved but did not connect.
- `Hit`: legal attack connected and sent ordinary damage to Feature 6.1.
- `CriticalHit`: legal attack connected, multiplied pre-mitigation damage, then sent damage to Feature 6.1.

A hit that applies zero Health damage because Feature 6.1 mitigated or prevented the damage remains `Hit` or `CriticalHit`; the nested `DamageApplicationResult` explains the zero damage.

## Accuracy and Evasion

Existing Accuracy and Evasion calculated stats are whole-number values. Feature 6.2 uses a documented percentage-point normalization:

`normalizedAccuracy = Accuracy / 100`

`normalizedEvasion = Evasion / 100`

Formula:

`unclampedHitChance = baseHitChance + normalizedAccuracy - normalizedEvasion`

`finalHitChance = clamp(unclampedHitChance, 0.05, 0.95)`

Roll rule:

`hit when hitRoll < finalHitChance`

A roll exactly equal to `finalHitChance` is a miss. The result reports the raw stat values, normalized contributions, unclamped chance, final chance, roll, and hit flag.

## Critical Hits

Critical resolution is owned by Feature 6.2 and only occurs after a successful hit.

Defaults:

- critical chance: `0`
- critical multiplier: `1.5`

Rule:

`critical when criticalRoll < criticalChance`

Damage sent to Feature 6.1:

`damageAfterCritical = baseDamage * criticalMultiplier` for critical hits, otherwise `baseDamage`.

Critical resistance, critical immunity, hit locations, critical tables, and critical mitigation are deferred.

## Deterministic Rolls

The service does not use `UnityEngine.Random`, wall-clock time, or frame-dependent randomness. Requests must carry explicit `hitRoll` and `criticalRoll` values in `[0, 1)`.

`AttackDeterministicRoll.FromTransaction(transactionId, channel)` provides a local deterministic helper for callers that need generated-but-repeatable values. Future server execution must replace client-supplied rolls with server-authoritative rolls.

## Transactions and Deduplication

The attack transaction ID is the parent ID. The child damage transaction ID is deterministic:

`{attackTransactionId}.damage`

The child ID is distinct from the parent and is passed to Feature 6.1. Feature 6.1 remains the owner of duplicate protection for actual Health mutations.

Feature 6.2 also keeps a bounded runtime-session cache of processed attack transaction IDs so duplicate misses, blocked attacks, and attack-level events do not process repeatedly. This cache is not static, is not persisted, and is intentionally runtime-session scoped. Save/load does not persist transient attack attempts.

## Events

Preview emits no execution events.

Execution events:

- `AttackProcessed`
- `AttackBlocked`
- `AttackMissed`
- `AttackHit`
- `CriticalHit`
- `AttackDamagePrevented`
- `AttackDamageApplied`

For hits and critical hits, attack-level hit events are emitted only after `DamageHealingService` returns. Damage-specific events remain owned by Feature 6.1.

Duplicate attack execution returns a duplicate result and emits no duplicate events.

## Range and Context Boundary

Feature 6.2 validates supplied logical context only:

- supplied distance;
- maximum range;
- supplied line-of-sight;
- supplied target-validity.

It does not perform raycasts, overlap checks, projectile simulation, collider processing, camera aiming, or animation timing. Those systems must create requests after their own physical context checks. In future multiplayer, the server must independently validate physical context and must not trust client-supplied distance, line-of-sight, target validity, or rolls.

## Requirements

When a `RequirementSetDefinition` is supplied, the service evaluates it through the existing `CharacterQueryService` and `CapabilityRequirementEvaluator`. Requirement evaluation is read-only. Resource costs, ammunition, durability, inventory costs, mana, stamina, and currency spending remain owned by action/ability execution systems and are not spent by Feature 6.2.

## Persistence

Attack previews, attack attempts, and processed attack IDs are transient and are not persisted. Health/resource persistence remains owned by the existing resource persistence participants. Preview does not dirty persistence because it does not call mutating resource APIs.

## Test Lab

The Combat section exposes Feature 6.2 controls:

- hit chance;
- hit roll;
- critical chance;
- critical roll;
- critical multiplier;
- supplied distance;
- maximum range;
- fresh transaction ID;
- preview and execute player-to-enemy;
- preview and execute enemy-to-player;
- reuse last transaction ID;
- source-less environmental attack;
- out-of-range proof.

The footer `Last Result` line shows the outcome, hit chance, rolls, critical state, damage-after-critical, duplicate status, and nested damage result.

## Existing Callers Audited

Migrated:

- No physical-detection caller was migrated fully in this feature because the current player melee, enemy melee, and projectile paths still depend on legacy `IDamageable` targets and physical hit detection that is intentionally deferred from Feature 6.2.

Already using Feature 6.1 directly and intentionally not lifted to attack resolution:

- `DamageEffectDefinition`: direct effect damage, not an attack-resolution intent.
- `RestoreVitalEffectDefinition`: healing path, outside attack resolution.
- `RestoreHealthItemUseEffect`: item healing path, outside attack resolution.

Remaining direct or legacy damage paths:

- `PlayerMeleeCombat`
- `EnemyMeleeAttack`
- `SpellProjectile`
- `PlayerHealth`
- `EnemyHealth`
- `PrototypeDamageableTarget`
- multi-component legacy `DamageEffectDefinition` fallback

They remain because safe migration requires a later combat-target unification pass that gives players, NPCs, enemies, companions, summons, and projectiles one resource-backed actor target model while preserving current physical hit detection.

## Deferred

Deferred systems include raycasts, projectile movement, hitboxes, animation events, combos, cooldown redesign, shields, parries, dodge, armor penetration, durability, ammunition, injuries, damage over time, defeat/death/revival, threat, AI selection, faction/PvP legality, rewards, combat HUD, VFX/audio, networking, prediction, and rollback.
