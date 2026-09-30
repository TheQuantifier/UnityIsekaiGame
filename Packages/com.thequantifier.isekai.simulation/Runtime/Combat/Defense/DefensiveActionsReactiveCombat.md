# Feature 6.6 Defensive Actions and Reactive Combat

Feature 6.6 adds a transient, data-driven active defense layer for Guard, Block, Parry, and Dodge.

## Ownership

- `AttackResolutionService` remains responsible for target validation, hit chance, hit/miss, critical calculation, duplicate attack protection, and attack execution events.
- `DefensiveActionService` owns active defensive state, activation/cancellation, active-defense chance checks, stamina costs for active defense, and defense events.
- `DamageHealingService` remains responsible for passive mitigation, resistances, immunities, and Health mutation.
- `CharacterResourceCollection` remains the only owner of Stamina mutation and duplicate resource-event protection.
- `ActorLifecycleController` remains the owner of defeated, unconscious, dead, recovery, and revival state. Defensive actions require an actor that can act.
- `CombatStateService` remains the owner of engagements and encounters. Defense events are combat events, but active defense state is not persisted as encounter state.

## Runtime Flow

Activation creates one active defense per defender actor/body. A new activation replaces the previous active defense for that actor. Preview activation validates and calculates the state but does not spend Stamina, register active state, or emit events.

Shield Block requires an actually equipped item tagged `tag.shield-compatible`. Weapon Parry requires an actually equipped weapon tagged `tag.parry-capable`. Activation stores the qualifying item definition ID, optional item instance ID, and defender body ID. Execution revalidates that the same qualifying item remains equipped and compatible. Stale, missing, unequipped, or incompatible equipment is rejected with machine-readable codes.

When an attack hits, `AttackResolutionService` asks `DefensiveActionService` to resolve the defender's active defense before calling `DamageHealingService`.

- Dodge and Parry fully prevent eligible attacks on success.
- Block and Guard reduce eligible attacks according to their definition.
- If damage remains, the reduced amount is passed to `DamageHealingService`.
- If all damage is actively prevented, `DamageHealingService` is not called.

Defense chance succeeds when `roll < finalDefenseChance`, using a deterministic roll supplied through attack metadata. Rolls must be finite and in `[0, 1)`. If no roll metadata is supplied, the prototype default is `0.5`.

Final chance is:

`baseChance + statContribution + skillContribution`, clamped by the definition minimum and maximum.

Stat contribution is `(calculatedStat / 100) * statContributionScale`. Skill contribution is `(SkillGradeIndex / 7) * skillContributionScale`, where F is 0 and AAA is 7. Missing skill collections or unlearned skills contribute 0.

## Stamina and Transactions

Defense activation and resolution spend Stamina through `CharacterResourceCollection.ApplyChange`.

`DefensiveActionService` tracks duplicate activation, cancellation, and resolution transactions only for defense operation idempotency. It does not duplicate resource-event de-duplication; resource mutation still uses the existing Current Resource transaction/event protection.

Preview calls do not call mutating resource APIs, do not consume transaction IDs, and do not emit execution events.

Parry and Dodge consume their window after a valid attempt whether the chance succeeds, the chance fails, or Stamina is insufficient at resolution. They do not consume on malformed requests, stale bodies, stale state IDs, incompatible equipment, or attacks that are not parryable/dodgeable. Block is also consumed after valid success/failure attempts. Guard is maintained until cancelled, replaced, lifecycle-invalidated, or cleared on restore.

## True Damage

True damage bypasses passive mitigation in `DamageHealingService`.

Active defense is separate:

- Dodge and Parry can prevent true damage only when the attack allows active true-damage defense and the defense definition allows it.
- Block and Guard affect true damage only if their definitions explicitly allow true-damage defense.

The prototype definitions allow true-damage defense for Basic Dodge and Weapon Parry, and disallow it for Shield Block and Basic Guard.

## Persistence

Active defense state is transient runtime combat state. It is intentionally not saved.

Save/load or restore flows should clear active defense state with `ClearTransientStateForRestore`. Future multiplayer persistence remains server-authoritative; clients may request defensive actions, but clients must not authoritatively persist or restore shared-world combat defense state.

Restore clearing is silent: it emits no activation, cancellation, attempt, or resolution events. Recovery and revival do not restore previous active defense state because lifecycle invalidation clears it before recovery.

## Lifecycle and Combat State

Active defenses are cleared when the exact subscribed actor lifecycle reports Defeated, Unconscious, or Dead. Replacement bodies with the same actor ID cannot use a previous body's active defense state.

`AttackResolutionService` can optionally receive a `CombatStateService`. When present, a processed non-duplicate attack records one logical combat refresh with the attack transaction ID. Active defense, partial damage, and full prevention are correlated under that attack result so partial Block followed by damage does not create an additional refresh from the attack path.

## Prototype Definitions

Canonical prototype definitions are under `Packages/com.thequantifier.isekai.content/Content/Combat/Defense/`:

- `defense-action.basic-guard`
- `defense-action.shield-block`
- `defense-action.weapon-parry`
- `defense-action.basic-dodge`

They are referenced by the prototype definition catalog for Test Lab and manual testing.
