# Combat Effects, Triggers, and Reactions

Feature 6.8 adds a reusable reaction layer for committed combat events. It is intentionally not a replacement for damage, healing, attack resolution, defensive actions, lifecycle, ongoing effects, combat state, execution commitments, resources, statuses, or abilities.

## Ownership

- `CombatReactionDefinition` is the authored definition.
- `CombatReactionSourceRegistration` binds a definition to a concrete owner actor and source instance, such as a trait, skill, item, equipment, status, ability, ongoing effect, actor profile, or development tool.
- `CombatReactionService` owns transient trigger-chain execution and duplicate root protection.
- The current local prototype registers sources from the Test Lab. Production systems should register and unregister sources explicitly when their owning runtime state becomes active or inactive.

## Trigger Context

Only committed runtime events should create contexts. Preview, failed, duplicate, passive query, save, restore, and Test Lab reset paths must not create reaction contexts.

Supported trigger identifiers are represented by `CombatReactionTriggerType`, including attack, damage, healing, defense, ongoing-effect, lifecycle, combat-state, encounter, and combat-execution events. Helper factories exist for damage, healing, attack, defense, ongoing-effect, and execution results.

## Determinism

Eligible reactions are processed in this order:

1. Reaction priority descending.
2. Source priority descending.
3. Reaction definition stable ID ascending.
4. Source stable ID ascending.
5. Source instance ID ascending.

Proc rolls are deterministic from the root transaction, trigger type, reaction ID, source IDs, and reaction index. A reaction succeeds when `roll < procChance`; rolls are required to be in `[0,1)`.

## Chain Limits

The default chain depth is 8 and the default reaction count limit is 64. By default, a reaction definition/source pair executes once per chain. Definitions can opt into limited repeat execution with `maximumExecutionsPerChain`, but the chain limits still apply.

Child transaction IDs are deterministic and include the root transaction, chain depth, index, reaction ID, and source IDs.

## Operations

Implemented operations:

- `ApplyDamage`: delegates to `DamageHealingService.ApplyDamage`.
- `ApplyHealing`: delegates to `DamageHealingService.ApplyHealing`.
- `ApplyOngoingEffect`: delegates to `OngoingEffectService.ApplyOngoingEffect`.
- `ModifyResource`: delegates to `CharacterResourceCollection.ApplyChange`.
- `NoOpDiagnostic`: records the reaction without mutating gameplay state.

Deferred operations fail explicitly with `UnsupportedOperation` until safe production APIs exist:

- `ApplyStatusEffect`
- `RemoveStatusEffect`
- `ApplyCondition`
- `RemoveCondition`
- `TriggerImmediateAbility`

## Canonical Alpha Definitions

The prototype catalog includes:

- `combat-reaction.basic-thorns`
- `combat-reaction.basic-lifesteal`
- `combat-reaction.poison-on-hit`
- `combat-reaction.mana-on-critical`
- `combat-reaction.stamina-on-parry`

## Manual Test Steps

1. Open PrototypeScene and press Tab.
2. Select `Test Lab`.
3. Select `Reactions 6.8`.
4. Choose a reaction with the `Reaction` selector.
5. Press `Register Player` or `Register Enemy`.
6. Press `Preview Damage` and verify the readout says preview and Health does not change.
7. Press `Execute Damage` and verify the reaction result appears in the readout and any supported operation applies once.
8. Press `Duplicate Proof` and verify the second root trigger reports `Duplicate`.
9. Use the Automation section to run suite `feature.6.8.combat-reactions`.

## Current Limitations

Area, cone, radius, and spatial targeting are intentionally excluded. Status, condition, and immediate ability reaction operations are explicit deferred failures because this feature does not add a new authoritative API for those systems.
