# Step 4 Persistence Schema Inventory

Step 4 uses one save envelope schema plus independent participant schemas. Do not treat participant payload versions as one global version.

## Save Envelope

- Type: `GameSaveEnvelope`
- Current version: `PersistenceService.CurrentSchemaVersion == 3`
- Required fields: format identifier, schema version, game version, save ID, slot ID, display name, timestamps, world ID, player ID, account ID, participant records, content checksum.
- Optional/diagnostic fields: play time, scene summary, place summary, player summary, transaction ID, parent transaction ID, save revision, completed-write marker.
- Compatibility: only the current schema, exact game version, exact context, slot, world, player/account identity, completed-write marker, and checksum are accepted.
- Migration: none during development; recreate saves after schema changes.
- Stable-ID dependencies: slot ID, participant keys, world ID, player ID, account ID.

## Slot Metadata

- Types: `SaveSlotMetadata`, `SaveSlotDescriptor`
- Version source: envelope schema plus descriptor fields.
- Required fields: slot ID, existence/validity state, schema version, compatibility/status, message.
- Optional/diagnostic fields: display name, timestamps, play time, scene/place/player summaries, transaction ID, save revision, backup availability.
- Compatibility: corrupt, missing, future-version, or incomplete slots remain listable with status instead of hidden.
- Migration: descriptors are rebuilt from save metadata on demand.
- Stable-ID dependencies: manual and autosave slot IDs.

## Inventory/Equipment

- Participant: `player.inventory-equipment`
- Payload: `PlayerInventoryEquipmentSaveData`
- Current version: `1`
- Required fields: schema version, inventory DTO, equipment DTO.
- Optional fields: item quality ID and condition where item metadata exists.
- Compatibility: unsupported participant payload versions are rejected.
- Migration: none.
- Stable-ID dependencies: item definition IDs, item instance GUIDs, quality IDs, equipment slot IDs.

## Base Attributes

- Participant: `player.attributes`
- Payload: `PlayerAttributesSaveData`
- Current version: `1`
- Required fields: schema version, player ID, person ID, permanent source contributions, training/growth events.
- Compatibility: missing pre-5.2 participant data is rejected for current development saves; Feature 5.4a does not bump the schema.
- Migration: none for pre-5.2 saves.
- Stable-ID dependencies: Base Attribute IDs, contribution source IDs, growth event IDs.
- Derived values: Calculated Stats, including maximum Health/Mana/Stamina, are rebuilt from Base Attributes and source-owned contributions and are not saved as authoritative payload values.

## Skills

- Participant: `player.skills`
- Payload: `PlayerSkillsSaveData`
- Current version: `1`
- Required fields: schema version, player ID, learned Skill records, hidden learning progress, consumed action-event keys.
- Compatibility: missing pre-5.3 participant data is rejected for current development saves; no migration is retained.
- Migration: none.
- Stable-ID dependencies: SkillDefinition IDs, ability/action unlock IDs, calculated stat IDs, action event IDs.

## Status Effects And Resources

- Participants: `player.status-effects`, `player.resources`
- Payloads: `PlayerStatusEffectsSaveData`, `PlayerResourcesSaveData`
- Current version: `1`
- Required fields: status schema/profile/status list; resource schema/player/person IDs/resource records.
- Compatibility: unsupported or missing current participant payloads are rejected; defeated saves are rejected.
- Migration: none.
- Stable-ID dependencies: actor profile, status definition/application/source IDs, resource definition IDs, player/person IDs.
- Ownership is non-overlapping: status effects own statuses; resources own Health, Mana, Stamina, and other resource values.

## Quest/Contract

- Participant: `player.quests-contracts`
- Payload: `PlayerQuestContractSaveData`
- Current version: `2`
- Required fields: schema version, quest list, contract list.
- Quest fields: quest definition ID, runtime instance ID, state, current stage ID, diagnostic stage index, active objective progress.
- Contract fields: contract definition ID, runtime instance ID, state, objective progress.
- Compatibility: schema version `1` is rejected because it used order-derived objective keys; future versions are rejected.
- Migration: none for version `1`; future migrations must prove stage/objective identity before restoring progress.
- Stable-ID dependencies: quest IDs, contract IDs, quest runtime IDs, contract runtime IDs, quest stage IDs, quest objective IDs, contract objective IDs.

## Location

- Participant: `player.location`
- Payload: `PlayerLocationSaveData`
- Current version: `1`
- Required fields: schema version, scene key, position, rotation, location mode.
- Optional/diagnostic fields: place ID, spawn point ID, saved UTC time, diagnostic scene name/path/build index.
- Compatibility: unsupported participant payload versions are rejected; cross-scene restoration is rejected clearly.
- Migration: none.
- Stable-ID dependencies: scene key, place ID, spawn point ID.

## World Entity Reference

- Type: `WorldEntityReference`
- Current version: `1`
- Required fields: schema version, entity ID, identity kind.
- Optional/diagnostic fields: scene key, world ID, definition ID, expected entity type.
- Compatibility: unsupported reference versions and transient references are rejected.
- Migration: none.
- Stable-ID dependencies: world entity ID, scene key, world ID, definition ID.

## Transaction Marker

- Fields: transaction ID, parent transaction ID, save revision, completed-write marker.
- Current version: envelope version `1`.
- Required for correctness: none; these are diagnostic transaction fields.
- Compatibility: old Step 4 saves without transaction metadata remain readable because checksum ignores these fields.
- Migration: not required.

## Autosave Metadata

- Slot IDs: `autosave-0`, `autosave-1`, `autosave-2`, `autosave-staging`.
- Current version: envelope version `1`.
- Required fields: same as envelope metadata after rotation.
- Compatibility: each generation validates independently.
- Migration: none.
- Stable-ID dependencies: autosave slot ID and generation order.

## Recovery And Quarantine

- Recovery report type: `SaveRecoveryScanReport`
- Candidate type: `SaveRecoveryCandidate`
- Quarantine files: `<slot>.json.quarantine.<utc>.json`
- Current version: diagnostic only.
- Compatibility: corrupt files are not loaded; valid backups/autosaves remain candidates.
- Migration: none.
- Stable-ID dependencies: slot ID and file naming convention.
