# Step 4 Persistence Architecture

Feature 4.9 closes the Step 4 persistence milestone. The current implementation is a local prototype persistence stack for player-owned state. It is intentionally structured so future multiplayer persistence can keep player state, shared-world state, region state, account state, and session-only state separate.

Step 4 is implemented in code and documentation, but the milestone remains pending manual closeout until the final PrototypeScene checklist passes.

## Current Architecture

`PersistenceService` is the coordinator. It owns participant registration, dependency ordering, save envelope creation, checksum validation, atomic file writes, backup handling, slot metadata, load preflight, rollback coordination, recovery scans, and structured result reporting.

Runtime systems own gameplay state. They participate through `IPersistenceParticipant`:

- `CapturePayload` creates a plain DTO snapshot.
- `PreparePayload` parses and validates without mutating live state.
- `CommitPreparedPayload` mutates the owning runtime system only after all participants prepare.
- `DiscardPreparedPayload` releases prepared data.

`IPersistenceParticipantDependencies` explicitly declares ordering and readiness requirements. Required dependencies block save/load before mutation; the service no longer contains a parallel hardcoded dependency table.

## Participant Inventory

| Participant | Key | Schema | Scope | Owner | Required | Load Phase | Ordering Dependencies | Rollback | Runtime Owner | Payload |
| --- | --- | ---: | --- | --- | --- | --- | --- | --- | --- | --- |
| Inventory/equipment | `player.inventory-equipment` | 1 | Player | `local-player` | Yes | Inventory | None | Yes | `PlayerInventory`, `PlayerEquipment` | `PlayerInventoryEquipmentSaveData` |
| Status effects | `player.status-effects` | 1 | Player | `local-player` | Yes | Statuses | `player.inventory-equipment` | Yes | `StatusEffectController` | `PlayerStatusEffectsSaveData` |
| Current resources | `player.resources` | 1 | Player | `local-player` | Yes | Vitals | `player.attributes`, `player.status-effects` | Yes | `CharacterResourceCollection` | `PlayerResourcesSaveData` |
| Quests/contracts | `player.quests-contracts` | 2 | Player | `local-player` | Yes | QuestsAndContracts | inventory/equipment, status effects, resources | Yes | `PlayerQuestLog`, `PlayerContractJournal` | `PlayerQuestContractSaveData` |
| Location | `player.location` | 1 | Player | `local-player` | No | PositionAndPlace | `player.quests-contracts` when present | Same-scene only | `Transform`, `PlayerInputReader`, `CurrentPlaceTracker` | `PlayerLocationSaveData` |

Player and shared-world participants now run in separate persistence contexts and directories. World location/travel, economy, organizations, politics, social state, quests, dialogue, and narrative state continue independently of any one player session.

## Load Order

The enforced relationship for the full prototype player stack is:

1. Runtime services and definitions are available.
2. Inventory/equipment prepare.
3. Stats/vitals/status prepare.
4. Current resources prepare when present.
5. Quest/contract prepare.
6. Location prepare.
6. All participant payloads validate.
7. Rollback snapshot captures current live state.
8. Inventory/equipment commit.
9. Equipment modifiers rebuild from equipment events.
10. Statuses restore.
11. Status modifiers and resistance modifiers rebuild.
12. Current health, mana, and stamina restore and clamp.
13. Quests/contracts restore.
14. Objective listeners rebuild.
15. Same-scene location restores.
16. Reach-location suppression is lifted after restore.
17. Consistency audit runs.
18. UI/runtime summaries refresh through normal events.
19. Dirty state clears only after successful save/load operations.
20. Gameplay input resumes when menu/location guards release.

The dependency graph is deterministic and independent of registration order. Isolated participant tests can still register one participant because built-in player relationships are ordering hints unless explicitly declared as required dependencies.

## Save Transaction

Save phases:

1. `Eligibility`
2. `ResolveDependencies`
3. `Capture`
4. `BuildEnvelope`
5. `WriteTemporary`
6. `VerifyTemporary`
7. `PreservePrevious`
8. `PromotePrimary`
9. `UpdateMetadata`
10. `Finalize`

Every save has a transaction ID. The envelope stores transaction ID, optional parent transaction ID, save revision, and completed-write marker. The checksum covers authoritative content and intentionally does not include transaction metadata so prior Step 4 saves remain readable.

One failed participant aborts the entire save before primary replacement. Failed temp validation, backup preservation, or primary promotion reports a structured failure and preserves existing valid files where possible.

## Load Transaction

Load phases:

1. `Eligibility`
2. `Read`
3. `ValidateEnvelope`
4. `ResolveDependencies`
5. `PrepareParticipants`
6. `CaptureRollback`
7. `CommitParticipants`
8. `ConsistencyAudit`
9. `RollingBack` when needed
10. `Finalize`

All participants prepare before live mutation. Corrupt files, wrong format, unsupported versions, duplicate participant keys, missing required payloads, owner mismatch, prepare failure, and dependency failure leave live state unchanged.

If commit or consistency audit fails after mutation begins, the service restores already committed participants in reverse order from prepared rollback payloads. If rollback fails, runtime safety becomes `Unsafe` and normal save/load is blocked.

## Recovery Matrix

| Scenario | Current Behavior |
| --- | --- |
| Valid primary | Load primary explicitly. |
| Corrupt primary with valid backup | Normal load reports backup available; it does not silently load backup. |
| Explicit backup load | Loads backup after normal validation. |
| Backup promotion | Copies valid backup to primary and quarantines old primary when present. |
| Corrupt backup | Excluded from recovery recommendations. |
| Autosave newest valid | Listed as loadable autosave candidate. |
| Newest autosave corrupt, older valid | Older generation remains independently valid and listed. |
| Stale temp file | Reported by recovery scan; cleanup is explicit. |
| Interrupted transaction marker | Represented by incomplete/temp artifacts and completed-write marker diagnostics. |
| Unsafe runtime state | Save/load are blocked until restart or explicit recovery path. |
| Location fallback | Reported through location fallback result and HUD/test diagnostics. |

Recovery recommendations are explicit and ordered: promote backup, load backup, load autosave, quarantine primary, inspect temp, restart.

## Compatibility Policy

Step 4 is pre-release persistence. Current development saves are best-effort compatible.

- Save envelope schema is global and currently version `1`.
- Participant schemas version independently.
- Unsupported future versions are rejected.
- Old participant versions are migrated only through explicit migrations.
- Quest/contract schema version `1` remains rejected because it used order-derived objective keys.
- Stable definition IDs, stage IDs, objective IDs, scene keys, spawn IDs, world entity IDs, participant keys, save slot IDs, owner IDs, and transaction IDs are content or runtime contracts.
- Changing authored IDs may orphan save state.
- Feature 4.9 intentionally invalidates old local development saves that contain obsolete first-party prototype definition IDs instead of keeping permanent aliases. Delete generated local saves before final manual testing.
- Step 5 may intentionally invalidate development saves when the game model changes.

## Stable-ID Audit

Current identity domains:

- Definition IDs: validated by `DefinitionIdValidator` and definition catalog validation.
- Item instance IDs: canonical GUID strings.
- Quest runtime IDs: definition ID for non-repeatable quests, GUID path for repeatable runtime instances.
- Contract runtime IDs: GUID runtime instance IDs.
- Quest stage IDs: authored stable IDs unique within quest.
- Quest and contract objective IDs: authored stable IDs unique within owning stage/contract.
- Scene keys: `scene.prototype` for current prototype scene.
- Spawn-point IDs: `spawn.prototype.*`.
- World-entity IDs: `entity.<scene>.<local>` for authored, `entity.<world>.runtime.<guid>` for runtime.
- Participant keys: stable strings such as `player.inventory-equipment`.
- Save-slot IDs: `manual-1` through `manual-5`, `autosave-0` through `autosave-2`, `autosave-staging`.
- Owner IDs: `local-player`, `local-world`, `local-account` in the prototype.
- Transaction IDs: generated GUID strings per operation.

No implemented persistence participant restores by array position, GameObject name, hierarchy path, or property-access-generated identity. `PlayerSpawnPoint.SpawnPointId` can fall back to `name` if unset, so authored scene setup must keep spawn IDs assigned and validation should flag missing IDs before relying on scene names.

## Prototype Tooling

The Save/Load page is a client of `PrototypePersistenceServiceBehaviour` and `PersistenceService`. It displays slot metadata, operation state, current phase, runtime safety, revision, transaction ID, dirty state, and backup availability.

The Test Lab exercises persistence through the same central service: manual save/load, autosave, backup load, backup promotion, primary quarantine, stale temp cleanup, recovery scan, transaction diagnostics, fingerprints, and fault injection.

Editor menu commands remain development conveniences and require Play Mode.

## Final Validation Report

Feature 4.9 automated validation should include runtime build, EditMode test build, Unity EditMode tests where the runner emits result data, definition validation, world-entity validation, scene ID scans, generated-save scans, and the manual checklist in `Step4PersistenceRegressionChecklist.md`.

Do not create the `v0.4.0-persistence-foundation` tag until manual testing is approved.

## Milestone Tagging Instructions

After manual approval only:

1. Review final diff.
2. Verify no generated save/test/log/cache artifacts are included.
3. Stage intended files.
4. Commit as `chore: close Step 4 persistence milestone`.
5. Push the feature branch.
6. Switch to `main`.
7. Pull `main` with `--ff-only`.
8. Merge the feature branch.
9. Push `main`.
10. Verify `main` and `origin/main` hashes match.
11. Confirm clean working tree.
12. Create annotated tag `v0.4.0-persistence-foundation`.
13. Push the tag.
