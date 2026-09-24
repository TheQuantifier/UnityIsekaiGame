# Step 4 Persistence Roadmap

Step 4 builds persistence incrementally on top of the Step 3 data and taxonomy foundation.

Feature 4.9 adds the closeout documentation and regression coverage for the milestone. Step 4 is implemented but remains pending manual closeout until `Documentation/Step4PersistenceRegressionChecklist.md` passes and the milestone tag is approved.

## Sequence

1. Feature 4.1: Save-File and Persistence Service Foundation
2. Feature 4.2: Player Inventory and Equipment Persistence
3. Feature 4.3: Player Stats, Vitals, and Status Persistence
4. Feature 4.4: Quest and Contract Persistence
5. Feature 4.5: Player Position, Scene, and Place Persistence
6. Feature 4.6: Persistent World-Entity Identity
7. Feature 4.7: Save Slots, Autosave, and Load UI
8. Feature 4.8: Persistence Integration and Recovery
9. Feature 4.9: Step 4 Closeout

## Direction

Each feature should add one runtime owner or closely related group of owners to the persistence service.

Preserve these constraints:

- static definitions resolve through stable IDs;
- ScriptableObject assets are never mutable save state;
- save DTOs are plain serializable data;
- participants validate before mutation;
- calculated values rebuild from authoritative state;
- equipment and status modifiers are saved only through their owning systems;
- direct scene object references are not serialized;
- save/load results remain structured.
- player-scoped state and shared-world state remain separable.
- clients are never authoritative over shared-world persistence.

## Multiplayer Ownership Direction

Feature 4.1 is still local and single-player, but every participant now declares a persistence scope and may carry an owner ID for player-scoped data.

Future multiplayer persistence should be server-owned and normally automatic. When one player disconnects, the server should persist that player's `Player`-scoped participants while the `SharedWorld` and active `RegionOrScene` state continue for other connected players. Reconnecting should restore the player into the current world, not roll the world back to the player's disconnect point.

Shared-world state should be saved by server checkpoints, region unloads, authoritative simulation events, or controlled shutdown. Client save/load UI should not upload or restore shared-world state directly.

Future offline-world progression can be modeled through server simulation, scheduled catch-up, or explicit region progression rules. It should not be hidden inside local client save files.

## Feature 4.2 Status

Feature 4.2 adds a combined `player.inventory-equipment` participant. It is `Player` scoped, owned by `local-player` in the prototype, required for current prototype saves, and preserves inventory slot contents, definition-only stacks, stateful item-instance IDs, quality/condition metadata, and equipped item identity.

The participant validates inventory and equipment together before commit so one item instance cannot appear in both inventory and equipment. It does not persist current vitals, statuses, quests, contracts, position, scene state, world pickups, enemies, or shared-world state.

Feature 4.3 restores current stats/vitals after equipment has rebuilt max-stat modifiers and after save-eligible statuses have rebuilt their modifiers. This ordering keeps equipment/status-derived maximums authoritative before current health, mana, or stamina values are applied.

## Feature 4.3 Status

Phase 3 replaces Feature 4.3's combined payload with required `player.status-effects` and `player.resources` participants. Status modifiers rebuild from restored statuses; current resource values restore afterward against rebuilt maximums.

Defeated prototype saves are rejected. Timed statuses restore with saved remaining duration; offline elapsed time is not applied yet.

## Feature 4.4 Status

Feature 4.4 adds `player.quests-contracts` after inventory/equipment and stats/vitals/statuses. It persists personal quests, accepted contracts, current quest stage, active objective progress, runtime instance IDs, and reward-claim state. Schema version 2 restores quest stages and quest/contract objectives by authored stable IDs instead of array position; schema version 1 is rejected rather than migrated.

Current objective keys use authored stage/objective indexes with stage ID validation where authored. Future content should add stable objective IDs before reordering shipped quest or contract objectives.

## Feature 4.5 Status

Feature 4.5 adds optional `player.location` after quests/contracts in the load order. It persists stable scene key `scene.prototype`, current place ID, player root position/rotation, diagnostic scene data, and fallback spawn ID. Same-scene restoration is supported with CharacterController-safe teleport, movement transient reset, current-place refresh, and Reach Location suppression during load. Cross-scene restoration is rejected clearly until a controlled asynchronous scene loading service is added.

## Feature 4.6 Status

Feature 4.6 adds persistent world-entity identity. Authored scene objects use stable IDs such as `entity.scene.prototype.enemy.primary`, runtime spawned objects use generated `entity.<world>.runtime.<guid>` IDs, and restored runtime objects preserve their saved IDs. `WorldEntityRegistry` rejects duplicates and resolves currently loaded identities, while `WorldEntityReference` provides a versioned serializable handle for future save participants.

This is not full world-state persistence. Pickups, enemies, NPCs, doors, containers, and region simulation still need explicit server/world/region participants before their mutable state can be saved. The identity layer exists so those later participants can reference exact world objects safely.

## Feature 4.7 Status

Feature 4.7 adds local prototype save-slot UX and autosave orchestration. The Tab menu now has a `Save/Load` page backed by five manual slots and three rotating autosaves. Slot IDs are stable (`manual-1` through `manual-5`, `autosave-0` through `autosave-2`), and the UI reads `SaveSlotDescriptor` metadata instead of owning persistence state.

`AutosaveCoordinator` performs timer autosaves, debounced progression autosaves, force autosaves, eligibility checks, and staging-to-generation rotation. `GameSaveDirtyTracker` records unsaved changes, and `PlayTimeTracker` feeds play-time metadata into the save envelope.

Manual overwrite, dirty-load, backup-load, and delete operations require explicit confirmation in the UI. Backup load remains opt-in; corruption or backup availability is reported rather than silently recovered.

This is still local proof infrastructure. It persists player-scoped prototype participants and player location only. Shared-world and region state remain future server/world-owned work.

## Feature 4.8 Status

Feature 4.8 hardens the integrated persistence pipeline with transaction IDs, explicit save/load phases, dependency preflight, deterministic dependency ordering, temporary-file validation, rollback snapshots, rollback guarded by `PersistenceRestorationGuard`, consistency-audit hooks, runtime safety state, recovery scanning, and explicit backup promotion/quarantine tools.

The dependency graph now preserves the intended player-state load order even when participants register in a different order. Current default ordering hints keep inventory/equipment before stats/vitals/statuses, stats before quests/contracts, and quests/contracts before player location when those participants are present. Explicit required dependencies can still block save/load before mutation.

Recovery remains explicit. Normal load does not silently choose a backup or temp file. The service reports valid backups, corrupt primaries, autosave candidates, and stale temporary files so editor tools, the Save/Load page, or Test Lab can recommend a controlled action.

This feature still does not persist shared-world or region simulation state. It creates the transaction, recovery, and safety foundation those later server-owned participants will need.

## Feature 4.9 Status

Feature 4.9 consolidates the final Step 4 architecture, schema inventory, ownership matrix, known limitations, regression checklist, and Step 5 migration guidance:

- `Documentation/Step4PersistenceArchitecture.md`
- `Documentation/Step4PersistenceSchemaInventory.md`
- `Documentation/Step4PersistenceOwnershipMatrix.md`
- `Documentation/Step4PersistenceKnownLimitations.md`
- `Documentation/Step4PersistenceRegressionChecklist.md`
- `Documentation/Step5PersistenceMigrationGuidance.md`

The milestone tag `v0.4.0-persistence-foundation` should be created only after manual closeout is approved.
