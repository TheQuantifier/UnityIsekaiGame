# Quest And Contract Persistence

Feature 4.4 adds player-scoped persistence for personal quest and accepted contract state.

For final Step 4 participant inventory, schema policy, and regression coverage, see `Documentation/Step4PersistenceArchitecture.md`, `Documentation/Step4PersistenceSchemaInventory.md`, and `Documentation/Step4PersistenceRegressionChecklist.md`.

## Ownership

Personal quest logs and accepted contract journals are `Player` scoped state.

- Participant key: `player.quests-contracts`
- Scope: `Player`
- Owner ID: `local-player`
- Schema version: `2`
- Load phase: `QuestsAndContracts`

Loading this participant restores only the current player's accepted quest/contract progress and history. It does not restore shared guild boards, faction state, world events, NPC runtime state, enemies, player position, scenes, or world pickups.

Future party quests, faction-wide contracts, player-created postings, guild boards, and global events should use separate ownership and likely shared-world or party-scoped persistence.

## Architecture

`PlayerQuestContractPersistenceParticipant` is a combined participant. Quests and contracts share objective infrastructure, both appear in the Journal, and combined validation prevents duplicate runtime IDs before commit.

Player location persistence loads after this participant. During location restoration, `LocationRestoreGuard` suppresses Reach Location reports so loading into an area does not complete an incomplete objective. Normal movement after load still uses the existing signal bus and can complete the objective once.

The participant coordinates DTO capture, validation, prepare, and commit. `PlayerQuestLog` remains the gameplay owner for quests, and `PlayerContractJournal` remains the gameplay owner for contracts.

## Load Ordering

The participant loads after:

1. `player.inventory-equipment`;
2. `player.status-effects` and `player.resources`.

This ensures inventory-backed collect objectives see restored inventory and quest/contract state exists before later position/place restoration can fire location triggers.

## Payload

`PlayerQuestContractSaveData` contains:

```csharp
public int schemaVersion;
public List<QuestInstanceSaveData> quests;
public List<ContractInstanceSaveData> contracts;
```

Quest entries store quest definition ID, runtime instance ID, quest state, current stage ID, legacy/current stage index for diagnostics, and active-stage objective progress.

Contract entries store contract definition ID, runtime instance ID, contract state, and objective progress.

The payload does not store ScriptableObject references, Journal UI selection, highlighted rows, dialogue panel state, contract-board menu state, cached description text, reward display text, or event subscriptions.

## Runtime Identity

Non-repeatable narrative quests use the quest definition ID as their runtime identity. Future repeatable quests use GUID runtime instance IDs.

Contracts always receive GUID runtime instance IDs so repeatable/generated contract support has a stable path later.

Runtime IDs are not registered in `DefinitionCatalog`. Duplicate quest or contract runtime IDs are rejected during prepare.

## Stages And Objective Keys

Schema version 2 restores quest stages and persisted objectives by explicit authored IDs rather than array position.

- quest stages use `QuestStageDefinition.StageId`;
- quest objectives use `ContractObjectiveDefinition.ObjectiveId`, unique within the owning quest stage;
- contract objectives use `ContractObjectiveDefinition.ObjectiveId`, unique within the owning contract.

Stage indexes and objective indexes remain in the payload only as ordering, presentation, diagnostic, and future migration metadata. They are not authoritative for restore.

Stages and objectives are not registered in `DefinitionCatalog` because they are not independently referenced by global systems. Their IDs only need to be unique within the owning quest or contract scope.

Schema version 1 is rejected rather than migrated. Version 1 quest objective progress used order-derived keys such as `stage.0.objective.0`, and contract objective progress used keys such as `objective.0`. Those keys cannot safely prove that saved progress still belongs to the same authored objective after reordering, insertion, removal, or type changes. Rejecting v1 avoids silently restoring progress to a different objective.

Definition validation rejects missing or duplicate quest stage IDs, missing or duplicate quest objective IDs within a stage, and missing or duplicate contract objective IDs within a contract. Restore also verifies that a saved objective ID resolves to the expected objective type.

## Objective Policies

Defeat objectives save defeated count and completion state. Loading does not replay enemy defeat events.

Current-held collect objectives recalculate progress from restored inventory and do not treat saved numeric progress as authoritative.

Delivery objectives save delivered quantity and completion state. Loading does not consume items.

Talk objectives save completion state. Loading does not simulate NPC conversation.

Reach-location objectives save completion state. Loading does not simulate trigger entry.

Active incomplete objectives attach listeners once through restore-specific activation. Completed, failed, abandoned, or reward-claimed instances do not attach active objective listeners.

## Rewards

Reward-claim state is stored as quest/contract state.

Completed-but-unclaimed entries remain claimable after load. Reward-claimed entries cannot claim again. Loading never grants rewards automatically, and failed reward claims leave state unchanged.

## Two-Phase Restore

Prepare validates schema version, quest and contract definitions, duplicate runtime IDs, non-repeatable duplicate quests, active contract limits, state values, stable stage IDs, stable objective IDs, objective type, progress ranges, and temporary runtime reconstruction.

No live quest log, contract journal, inventory, or UI state changes during prepare.

Commit snapshots current quest/contract state, restores quests, restores contracts, and emits one quest-log and one contract-journal refresh through the runtime owners. If an unexpected commit failure occurs, the participant attempts to restore the pre-load snapshots.

Feature 4.8 adds service-level rollback around this participant as part of the integrated load transaction. If quest/contract commit or a later consistency audit fails, the service uses prepared rollback payloads and `PersistenceRestorationGuard` so previous player state can be restored without replaying gameplay signals.

## Event Safety

Restoration rebuilds runtime instances directly rather than replaying gameplay events. Old objective listeners are disposed before live collections are replaced. Restore-specific activation attaches listeners without calling initial progress refresh, preventing load from auto-advancing quests or contracts.

## Integration

The prototype persistence root registers `player.quests-contracts` with references to `PlayerQuestLog`, `PlayerContractJournal`, and `PlayerInventory`.

Dialogue, contract board, and Journal UI continue reading runtime quest/contract state. They do not own or mutate persistence DTOs.

## Known Limitations

- No player position, scene, current place, world pickup, enemy, NPC, shared guild-board, or shared-world contract persistence.
- No party quest, faction contract, player-created contract, global event, autosave, cloud save, or multiplayer server persistence.
- Schema version 1 local prototype saves are not migrated and should be discarded after this Feature 4.4 change.
- Offline objective progression and timed contract expiration are not implemented.
- Recovery UI is development-focused; final player recovery workflows are still deferred.
