# Player Position, Scene, And Place Persistence

Feature 4.5 adds the first player-scoped location persistence participant.

For final Step 4 participant inventory, schema policy, ownership boundaries, and regression coverage, see `Documentation/Step4PersistenceArchitecture.md`, `Documentation/Step4PersistenceSchemaInventory.md`, `Documentation/Step4PersistenceOwnershipMatrix.md`, and `Documentation/Step4PersistenceRegressionChecklist.md`.

## Participant

- Key: `player.location`
- Scope: `Player`
- Owner: `local-player` in the local prototype
- Schema: `1`
- Load phase: `PositionAndPlace`, after inventory/equipment, stats/vitals/statuses, and quests/contracts

The participant captures the authoritative player root transform, stable scene key, most-specific tracked place ID, fallback spawn ID, and diagnostic scene metadata. It does not serialize `Transform`, `GameObject`, `SceneAsset`, movement velocity, jump state, sprint input, camera input, menus, or trigger references.

## Scene And Payload Policy

Prototype scene data uses stable scene key `scene.prototype`. Unity scene name, build index, or path are diagnostic/fallback metadata only.

`PlayerLocationSaveData` stores schema version, scene key, diagnostic scene data, place ID, world position, world rotation quaternion, fallback spawn ID, saved UTC time, and location mode/version metadata.

Feature 4.5 fully supports same-scene restoration. Cross-scene save data is validated and rejected clearly; asynchronous scene loading is deferred to a later feature.

Feature 4.6 world entity IDs complement scene and place keys. Scene keys identify the loaded scene context, place IDs identify authored location definitions, and world entity IDs identify specific scene/runtime objects inside that context. Player location persistence still stores only player-owned location state.

## Restore Flow

Restore is two-phase.

Prepare validates schema, scene key, duplicate loaded scene keys, place resolution, place/scene compatibility, finite position, prototype bounds, rotation, safe position, and fallback spawn availability.

Commit enters `LocationRestoreGuard`, blocks gameplay input, closes the Tab/Test Lab menu, disables `CharacterController`, teleports the player root, resets movement transients, syncs camera pitch, refreshes current place, and re-enables input.

If same-scene commit fails, the previous transform is restored where practical.

Feature 4.8 also captures this participant in the integrated service rollback snapshot. The participant remains last in the current default dependency chain so inventory, stats, quests, and contracts are coherent before a same-scene position/place restore can run.

## Safe Position And Fallback

Saved positions must be finite, within prototype bounds, and pass a ground probe. Unsafe positions use fallback order:

1. saved `spawnPointId`;
2. configured default spawn `spawn.prototype.default`;
3. any compatible loaded spawn point.

Fallback usage is reported through `LocationFallbackUsed` and the prototype HUD. The implementation never silently chooses world origin.

## Place Tracking And Quest Suppression

`CurrentPlaceTracker` tracks active place triggers on the player. Nested places use deepest-place-wins. Duplicate enter notifications are ignored by stable place ID; exiting a child returns to the active parent.

`QuestReachLocationReporter` updates the tracker on enter/stay/exit. During `LocationRestoreGuard`, it suppresses quest objective reporting and prototype HUD entered-area messages, so loading inside a Reach Location area does not progress that objective. Normal exit and re-entry after load progresses it once.

## Spawn Points

`PlayerSpawnPoint` is a scene runtime component with stable spawn ID, optional `PlaceDefinition`, priority, and purpose tags. Prototype setup creates stable IDs such as `spawn.prototype.default`, `spawn.prototype.items`, `spawn.prototype.combat`, and `spawn.prototype.quest-area`.

Spawn points are not registered in `DefinitionCatalog`.

## Multiplayer And Offline World Direction

Location is player-owned state. In future multiplayer, the server persists Player A's scene/place/position when Player A disconnects while shared-world and region state continues for other players. On reconnect, the server validates the saved location and collision, then spawns Player A at the saved point or a safe fallback. Clients must not authoritatively choose arbitrary saved positions.

Offline-world changes such as destroyed buildings, unavailable regions, ownership changes, dungeon resets, or unsafe terrain require server relocation policy. Future fallback destinations may include nearest safe spawn, home settlement, or another server-approved recovery point.

## Known Limitations

- Cross-scene restore is rejected, not loaded.
- World simulation uses a separate checkpoint context; spawned pickups, enemies, doors, containers, and NPC schedules still require dedicated participants.
- World entity IDs exist for future references, but world entity mutable state is not persisted by `player.location`.
- Safe-position validation is prototype-level and should be replaced by server/navmesh/world validation later.
- Cross-scene rollback and final player recovery UI are still deferred.
