# Step 6 Combat Architecture

## Directory Structure

- `Packages/com.thequantifier.isekai.simulation/Runtime/Combat/` keeps the production combat surface.
- `Packages/com.thequantifier.isekai.simulation/Runtime/Combat/CombatState/` owns engagement, encounter, merge, split, and combat activity tracking.
- `Packages/com.thequantifier.isekai.simulation/Runtime/Combat/Contributions/` owns contribution ledgers, credit resolution, reward eligibility hooks, and event bridges.
- `Packages/com.thequantifier.isekai.simulation/Runtime/Combat/Defense/` owns Guard, Dodge, Parry, Block definitions, runtime state, equipment checks, and defense resolution.
- `Packages/com.thequantifier.isekai.simulation/Runtime/Combat/Execution/` owns committed combat actions, costs, cooldowns, charges, and execution persistence.
- `Packages/com.thequantifier.isekai.simulation/Runtime/Combat/OngoingEffects/` owns timed damage, healing, resource effects, ticking, and transient ongoing runtime state.
- `Packages/com.thequantifier.isekai.simulation/Runtime/Combat/Reactions/` owns trigger source registration and reaction chain execution.
- `Packages/com.thequantifier.isekai.simulation/Runtime/Combat/Integration/` owns the Step 6 facade, readiness/snapshot models, transaction validation, and integrity validation.

No broad file moves or serialized asset renames were performed during Feature 6.10. Existing root-level combat files remain in place to avoid breaking serialized script references and asset GUIDs.

## Facade Ownership

`CombatRuntimeFacade` is the high-level production entry point for Step 6 integration. It composes the existing services rather than replacing them:

- `DamageHealingService`
- `AttackResolutionService`
- `DefensiveActionService`
- `CombatExecutionService`
- `CombatStateService`
- `OngoingEffectService`
- `CombatReactionService`
- `CombatContributionService`

The facade is not a singleton. Scene/bootstrap code chooses its lifetime and passes the concrete services in. This keeps future server-owned combat authority possible without coupling production runtime code to PrototypeScene or Test Lab.

## Mutation Boundaries

Preview methods call the same calculation paths as execution, but do not call mutating resource APIs, mark persistence dirty, emit execution events, consume transaction IDs, or change revisions.

Execution methods revalidate and delegate to the existing authoritative service exactly once. Duplicate transaction protection remains owned by the existing resource, attack, defense, execution, reaction, contribution, and combat-state services. Feature 6.10 does not add an independent de-duplication layer.

## Readiness And Snapshot

`CombatRuntimeReadinessState` reports:

- `Uninitialized`
- `ResolvingDependencies`
- `Ready`
- `Restoring`
- `Invalid`
- `Disposed`

`CombatRuntimeSnapshot` is immutable and scene-object-free for integration-owned models. It summarizes actor identity, body identity, person identity, lifecycle state, Health/Stamina/Mana, combat stats, active defense, active execution, active ongoing effects, combat state, engagements, recent opponents, reaction source summaries, contribution ledger snapshots, revision counters, and the most recent transaction trace.

## Transaction Hierarchy

`CombatTransactionValidator` records root, execution, attack, defense, damage, reaction, and contribution transaction IDs. Child transaction IDs are expected to be deterministic children of the logical root where a service creates child IDs. Unexpected ancestry is reported as a diagnostic warning, not a second duplicate-protection system.

## Event Ordering

The intended order for a committed attack is:

1. execution commitment, if the attack came from `CombatExecutionService`;
2. attack resolution;
3. defense resolution;
4. damage/healing resource mutation;
5. lifecycle transition, if Health reaches a threshold;
6. combat-state refresh;
7. reactions;
8. contribution record;
9. credit/reward eligibility resolution.

Restore paths clear transient combat state silently and must not emit defense activation, cancellation, resolution, execution, reaction, contribution, or gameplay damage events.

## Body And Species Boundary

Step 6 combat code must treat body/species data as a query input, not as combat-owned state. Combat systems should depend on `BodySnapshot`, biological capabilities, lifecycle state, damage types, and authored combat definitions. They should not hardcode individual Species IDs except in explicit content/test fixtures. Future Species-specific resistances, healing rules, defeat policies, or reaction eligibility should enter combat through definitions and capability/query surfaces owned outside the combat runtime.

## Persistence And Restore

Persistent data remains owned by the dedicated participants:

- resources and vitals through resource/lifecycle participants;
- combat execution cooldowns and charges through the combat execution participant;
- ongoing effect save data through the ongoing effect participant.

Transient combat state is cleared on restore:

- active defense windows;
- active execution commitments;
- active combat-state engagement runtime;
- reaction source registrations;
- contribution ledgers;
- runtime-only ongoing effects unless restored by their owning participant.

Offline progression is intentionally not applied during restore.

## Prototype And Compatibility Paths

Test Lab now uses `CombatRuntimeFacade` for Feature 6.10 overview/probes while preserving the older Feature 6.1 through 6.9 buttons. Existing direct production paths that remain are compatibility paths for older prototype combat scripts and low-level services. They are not removed in this cleanup because doing so would require prefab/script reference migration beyond the approved Feature 6.10 scope.

## Organization Cleanup Report

Moved or renamed files/assets: none.

Namespaces/types added:

- `UnityIsekaiGame.Combat.Integration.CombatRuntimeFacade`
- `CombatRuntimeReadinessState`
- `CombatRuntimeSnapshot`
- `CombatRuntimeDiagnostic`
- `CombatIntegrityReport`
- `CombatIntegrityValidator`
- `CombatTransactionValidator`

Duplicate or obsolete systems consolidated: no files were deleted. The practical consolidation is service composition through `CombatRuntimeFacade` and a shared `DamageHealingService` instance in Test Lab.

Compatibility paths retained: root-level combat scripts and all 6.1-6.9 services remain callable.

Serialized/GUID compatibility measures: no existing assets or scripts were moved or renamed; existing `.meta` GUIDs are preserved. New files receive new GUIDs only.

Remaining organization limitations: several older prototype-named runtime scripts still live under production combat folders. They should be moved or retired only in a later migration that updates scenes, prefabs, and serialized references deliberately.

## Step 7 Contracts

Step 7 systems should call the facade for high-level combat actions when they need a coordinated view of attack, defense, damage, reaction, combat-state, and contribution behavior. Low-level services remain valid for isolated calculations, tests, and owned subsystem persistence.
