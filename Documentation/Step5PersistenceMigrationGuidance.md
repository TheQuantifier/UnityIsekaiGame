# Step 5 Persistence Migration Guidance

Step 5 will expand core game models. Use this guidance before changing definitions, runtime state, or persistence DTOs.

## Decision Rules

- No save change: display-only text, non-persisted UI layout, or derived values that rebuild from existing saved state.
- Definition validation update: new required authored IDs, new classification rules, or new definition relationships.
- Participant DTO extension: new runtime state owned by an existing participant.
- Participant schema bump: old payload interpretation changes or new required fields cannot safely default.
- Migration: old saves can be transformed safely and deterministically.
- Development-save invalidation: old saves cannot prove identity or semantics after the change.
- Test Lab update: new runtime state needs prototype exercise or diagnostics.
- Fingerprint update: new participant-owned state must affect round-trip/rollback diagnostics.
- Consistency-audit update: new cross-system invariant can fail after integrated load.

## Change Guidance

| Change Area | Persistence Impact |
| --- | --- |
| Character stats | Definition-only derived stat changes usually need validation/tests only. Persisted current vitals or permanent stat state require DTO extension and possible schema bump. |
| Skills | New static skill definitions need ID validation. Learned/unlocked skill runtime state needs a player participant or extension. |
| Traits | Static trait definitions are ID references. Acquired traits need player-state DTOs, schema versioning, Test Lab controls, and audit checks. |
| Item properties | Derived display/combat properties need definition validation. Persisted per-instance properties require `ItemInstanceSaveData` extension and inventory/equipment schema bump. |
| Combat | Formula-only changes may not affect saves. Persisted combat state, cooldowns, wounds, or threat need explicit owner and likely new participant. |
| Abilities | Definition changes need stable IDs. Learned loadouts, cooldowns, charges, or prepared spells need player-state persistence. |
| Beings | Static being/profile changes need validation. Runtime actor state needs actor/world participant and server authority planning. |
| People | Static person definitions are content. NPC schedules, dialogue memory, and relationship state require separate owner decisions. |
| Places | New place definitions need stable IDs and scene-key validation. Dynamic place ownership or state is shared-world/region state. |
| Factions | Static faction definitions are content. Reputation may be player/account state; wars/control/economy are shared-world state. |
| Economy and ownership | Treat as shared-world/server-owned unless explicitly player-owned inventory. Do not put market state in local player saves. |
| Identity and progression | `player.identity-progression` is player-owned. Account/player/person/world-entity IDs must remain distinct. Missing Feature 5.1 payloads are rejected for current development saves. |
| Base Attributes and Calculated Stats | `player.attributes` persists permanent Base Attribute source/growth records. Calculated Stats are derived caches and should not be saved as authoritative values. Resource maximum metadata is definition data. |
| Current Resources | Required `player.resources` owns current Health, Stamina, and Mana. Future resource changes belong there rather than in Calculated Stats or status payloads. |
| Traits, Capabilities, and Requirements | Feature 5.5 adds optional `player.traits` for acquired Trait runtime records. Capabilities are rebuilt aggregates and Requirements are pure checks, so neither owns persisted state by default. |
| Character System coordinator | Feature 5.6 adds `CharacterSystemCoordinator`, snapshots, queries, readiness, and integrity diagnostics. It is not a new persistence participant. Existing subsystem participants remain authoritative and the coordinator becomes `Ready` only after restore/rebuild ordering completes. |

## Required Before Merging Step 5 Persistence Changes

1. Identify the authoritative owner: Player, SharedWorld, RegionOrScene, Account, or SessionOnly.
2. Decide whether current saves remain compatible.
3. Add or update stable IDs before data ships.
4. Extend DTOs only through the owning participant.
5. Bump participant schema when defaults are not enough.
6. Add migration only when old identity and semantics are provably safe.
7. Update definition validation for new authored contracts.
8. Update Test Lab controls or diagnostics for prototype coverage.
9. Update runtime fingerprints if the new state must round-trip.
10. Update consistency audit checks for cross-system invariants.
11. Document any intentional development-save break.

## Save Break Policy

Before alpha, development saves may be invalidated when the model changes. Every intentional break should say which schema changed, why migration is unsafe or not worth building yet, and which manual test data should be recreated.

## Feature 5.1 Break

Feature 5.1 introduces required participant `player.identity-progression` at schema version 1. Saves from Step 4 and earlier are intentionally rejected because they do not contain person identity, origin assignment, starting reward application flags, wallet balances, role lifecycle history, social status history, or birth-gift reward state. Recreate local prototype saves after validating definitions and generating a new origin in the Test Lab.

## Feature 5.2 Break

Feature 5.2 introduces required participant `player.attributes` at schema version 1. Pre-5.2 development saves are intentionally rejected because they do not contain authoritative Base Attribute source contributions or action-growth event history. Calculated Stats are not saved as authoritative values; they rebuild from Base Attribute records and active source-owned systems after load. Delete old local prototype saves before final Feature 5.2 manual testing.

## Feature 5.3 Break

Feature 5.3 introduces required participant `player.skills` at schema version 1. Pre-5.3 development saves are intentionally rejected because they do not contain hidden Skill learning progress, learned Skill records, consumed action-event keys, direct Skill grants, promotion history, or deterministic Skill effect rebuild metadata. Delete old local prototype saves before final Feature 5.3 manual testing and recreate saves after definition validation.

## Feature 5.4a Compatibility

Feature 5.4a does not bump `player.attributes` and does not add a new participant. It refines terminology to Base Attributes and adds Calculated Stat purpose/resource metadata in definitions. Feature 5.2/5.3 development saves should remain compatible when they use canonical IDs. Pre-5.2 saves remain rejected.

## Phase 3 Clean Break

Phase 3 requires `player.resources` and `player.status-effects`, uses envelope schema 3, and does not retain migration or fallback paths for development saves. Delete and recreate older local saves.

## Feature 5.5 Compatibility

Feature 5.5 introduces optional `player.traits` schema version 1. Saves without it remain compatible and load with no player Traits. Saves containing `player.traits` reject unknown Trait IDs, duplicate Trait records, duplicate source records, and invalid lifecycle/discovery values. Capabilities and Requirements do not persist their own state; active Trait effects rebuild after load.

## Feature 5.6 Compatibility

Feature 5.6 does not add a new save payload or schema version. It finalizes restore order and readiness around existing Step 5 participants. If a future change makes the coordinator persist aggregate state, that state must be split back into the owning participants or explicitly justified as a new participant; derived snapshots and revision caches should not become authoritative save data.
