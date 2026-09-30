# Feature 6.1 Damage and Healing Pipeline

## Runtime ownership

`DamageHealingService` is the high-level production entry point for immediate Health damage and healing. It validates actor identity, calculates preview and execution results through the same code path, and commits execution through `CharacterResourceCollection`.

Preview calls do not call mutating Resource APIs, emit execution events, consume transaction IDs, mark persistence dirty, or change character/resource revision state.

Execution revalidates the target and commits exactly once through the existing Current Resource API. Duplicate transaction ownership remains in `CharacterResourceCollection` through `ResourceChangeRequest.EventId` and `ResourceChangeResult.DuplicateEvent`; the damage/healing service does not keep an independent transaction cache.

## Damage calculation

Damage requests require a target object, resolved actor/body identity, a damage type, and a finite non-negative amount.

For non-True damage:

1. Choose defense by `DamageTypeDefinition.Family`.
2. Physical family uses `calculated-stat.physical-defense`.
3. Magical, Elemental, Spiritual, Toxin, and Prototype families use `calculated-stat.magical-defense`.
4. Apply defense as `max(0, requestedAmount - applicableDefense)`.
5. Apply numeric resistance capability after defense.
6. Apply boolean immunity capability before resource mutation.

True damage uses `DamageTypeDefinition.IsTrueDamage` and ignores defense, resistance, and immunity.

## Healing calculation

Healing requests require a target object, resolved actor/body identity, and a finite non-negative amount. Healing clamps to missing Health and reports overheal without overfilling the resource.

## Damage types

Canonical alpha damage type assets live in `Packages/com.thequantifier.isekai.content/Content/Combat/DamageTypes`:

- `damage.physical`
- `damage.physical.slashing`
- `damage.physical.piercing`
- `damage.physical.blunt`
- `damage.magic.fire`
- `damage.magic.cold`
- `damage.magic.lightning`
- `damage.magic.poison`
- `damage.magic.arcane`
- `damage.magic.holy`
- `damage.magic.necrotic`
- `damage.true`

Existing stable IDs and asset GUIDs were preserved. `damage.magic` remains as a broad parent type for existing references but is not marked as a canonical alpha damage type.

## Migrated callers

The following production callers now prefer `DamageHealingService` when the target is resource-backed and has a resolved actor identity:

- `DamageEffectDefinition` for single typed damage effects.
- `RestoreVitalEffectDefinition` for Health restoration.
- `RestoreHealthItemUseEffect` when no `RestoreVitalEffectDefinition` is assigned and the user is resource-backed.

## Remaining direct paths

These paths still use legacy `IDamageable`, `DamageCalculator`, `PlayerHealth`, or `EnemyHealth` directly:

- `PlayerMeleeCombat`
- `EnemyMeleeAttack`
- `SpellProjectile`
- `EnemyHealth`
- `PlayerHealth`
- `PrototypeDamageInteractable`
- multi-component `DamageEffectDefinition` effects

They remain direct because the prototype enemy and projectile/melee flows still target legacy `IDamageable` health components that are not consistently resource-backed actor bodies. Migrating them safely requires a later combat-target unification pass so production enemies, projectiles, melee hit resolution, and current resources share one authoritative actor target model.

## Test Lab manual steps

1. Open `Assets/_Project/Scenes/Prototype/PrototypeScene.unity`.
2. Enter Play Mode.
3. Press `Tab`.
4. Open `Test Lab`.
5. In `Overview`, select a damage type and set `Amount` to `25`.
6. Open `Combat`.
7. Click `Preview 6.1` and confirm the history line reports final damage while Health stays unchanged.
8. Click `Damage 6.1` and confirm player Health decreases by the reported final amount.
9. Click `Heal 6.1` and confirm player Health restores without exceeding maximum Health.
10. Click `Duplicate 6.1` and confirm the second transaction reports duplicate protection and does not change Health.
11. Select `True` damage, click `Damage 6.1`, and confirm it ignores defense/resistance mitigation.
12. Use existing legacy `Damage Enemy` separately to confirm older prototype enemy combat still works.
