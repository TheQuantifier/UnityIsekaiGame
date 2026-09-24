# Persistence Service Foundation

Feature 4.1 adds the first persistence layer. Feature 4.2 adds player-scoped inventory/equipment persistence, and Feature 4.3 adds player-scoped stats/vitals/status persistence through the same participant architecture. The service still does not persist quests, contracts, position, scene loading, or shared-world restoration.

Step 4 closeout documentation now lives in `Documentation/Step4PersistenceArchitecture.md`, with schema inventory, ownership matrix, limitations, regression checklist, and Step 5 migration guidance in the adjacent Step 4 documents.

## Architecture

`PersistenceService` is a plain C# coordinator in `UnityIsekaiGame.GameData.Persistence`.

It owns:

- save slot path resolution;
- participant registration;
- deterministic participant ordering;
- save envelope assembly;
- JSON serialization and parsing;
- checksum validation;
- atomic write and backup handling;
- metadata listing;
- validation-only reads;
- transaction IDs and phase diagnostics;
- participant dependency preflight;
- rollback snapshot capture;
- post-load consistency auditing;
- recovery source scanning;
- two-phase load orchestration;
- structured save, load, validation, and delete results;
- operation locking.

It does not own gameplay state. Runtime systems contribute state through explicit participants.

The current implementation is local and single-player, but the ownership model is multiplayer-compatible. A local save file must not be treated as if one player owns or can roll back the whole shared world.

## Participant Ownership

Participants implement `IPersistenceParticipant`.

Each participant owns:

- stable participant key;
- participant schema version;
- required/optional policy;
- persistence scope;
- optional owner ID;
- load phase and priority;
- DTO capture;
- payload parsing;
- payload validation;
- prepare state;
- commit state;
- discard behavior.

The service stores payloads as JSON strings and does not inspect private gameplay fields. Registration is explicit, not reflection-based. Duplicate keys are rejected.

Participant scopes are:

- `Player`: state owned by one player, such as future inventory, equipment, vitals, personal quest state, or player position.
- `SharedWorld`: server-owned world state shared by multiple players, such as public economy, global events, or shared faction state.
- `RegionOrScene`: state owned by a loaded region or scene shard, such as local spawned entities or interactable state.
- `Account`: account-level state outside one character/world session.
- `SessionOnly`: temporary state that may be coordinated during a session but is not normally durable.

Player-scoped participants should declare an owner ID. Shared-world participants should not be loaded as a side effect of loading one player's state.

On load, registered participant records are checked against the runtime participant's declared scope and owner ID before payload preparation. A save for another owner fails without mutating the local runtime state.

## Save Envelope

`GameSaveEnvelope` contains:

- format identifier: `UnityIsekaiGame.Save`;
- top-level schema version;
- game version string;
- save ID;
- slot ID;
- display name;
- optional world ID;
- optional player ID;
- optional account ID;
- created and last-written UTC timestamps;
- playtime placeholder;
- scene, place, and player summary placeholders;
- content checksum;
- participant records.

`SaveParticipantRecord` contains:

- participant key;
- participant schema version;
- required flag;
- persistence scope;
- optional owner ID;
- load phase;
- load priority;
- escaped participant payload JSON.

The envelope can be read for slot metadata without restoring gameplay state. Local prototype saves use `local-world`, `local-player`, and `local-account`.

The world ID identifies the world context for the save. It is not proof that the client is authoritative over the world. In a multiplayer deployment, shared-world state is normally written by the server to server-owned storage.

## Serializer Decision

Feature 4.1 uses Unity `JsonUtility`.

Reasons:

- already available in the Unity runtime;
- no new package dependency;
- works with explicit serializable DTOs and public fields;
- human-readable JSON during development;
- compatible with the existing plain DTO style.

Limitations:

- no dictionary support;
- no polymorphic payload graph support;
- field-based DTOs are preferred;
- malformed JSON diagnostics are limited;
- complex future migrations may require a stronger serializer.

Participants therefore use explicit DTOs and lists rather than arbitrary dictionaries or object graphs.

## Schema Versioning

The current top-level schema version is `1`.

Future schema versions greater than the current version fail clearly before mutation. Versions below `1` also fail.

Each participant has an independent schema version. The prototype participant currently supports only version `1`.

`ISaveMigration` and `SaveMigrationRegistry` provide a migration extension point, but Feature 4.1 does not automatically migrate historical saves.

## File And Slot Layout

Saves are under:

`Application.persistentDataPath/UnityIsekaiGame/Saves/`

Test code injects a temporary root path.

Slot IDs may contain only letters, numbers, `-`, and `_`. Path traversal and invalid characters are rejected before resolving paths.

For slot `slot-0001`:

- primary: `slot-0001.json`
- backup: `slot-0001.backup.json`
- temporary: `slot-0001.tmp`

Runtime saves are not written inside `Assets` or the repository.

## Atomic Writes And Backups

Save flow:

1. reject overlapping operations;
2. assign a transaction ID;
3. validate participant dependencies;
4. capture and self-validate participant payloads;
5. assemble an envelope with transaction metadata;
6. serialize to memory;
7. create the save directory;
8. delete stale temporary file;
9. write the temporary file;
10. validate the temporary envelope;
11. copy the previous primary file to one backup;
12. replace the primary with the temporary file;
13. report a structured result.

Failed capture, serialization, temporary write, or validation does not replace the existing primary save.

One backup per slot is kept.

Feature 4.8 adds explicit save phases and a completed-write marker. The checksum intentionally remains scoped to authoritative save content so older Step 4 saves remain readable.

## Load Flow

Load is two-phase.

Phase 1:

1. reject overlapping operations;
2. read the primary or explicit backup file;
3. parse the envelope;
4. validate format, version, checksum, and duplicate participant keys;
5. verify required payloads;
6. ask participants to parse and prepare payloads;
7. fail without mutation if any prepare step fails.

Phase 2:

1. capture rollback payloads from the current live runtime state;
2. commit prepared payloads in deterministic dependency order;
3. run the consistency audit hook;
4. report success;
5. notify listeners.

Participant commits are expected not to fail after successful preparation. If one does, Feature 4.8 restores already committed participants in reverse order from the rollback snapshot. If rollback fails, the runtime is marked `Unsafe` and normal save/load is blocked until recovery or restart.

## Participant Ordering

Ordering is deterministic:

1. `PersistenceLoadPhase`;
2. participant priority;
3. participant key.

Load phases cover bootstrap, actor base, identity/progression, attributes, skills, inventory, equipment, statuses, vitals, quests/contracts, position/place, and notification.

`IPersistenceParticipantDependencies` lets each participant declare required and optional dependencies. There is no second hardcoded ordering table. Missing required dependencies and circular dependencies fail before capture or load mutation; missing optional dependencies are diagnostics.

## Required And Optional Participants

Required runtime participants must have payloads in the save. Required payloads from unknown future systems fail loading.

Optional unknown payloads can be ignored. Optional registered participants can be absent.

Feature 4.2 adds another required prototype participant: `player.inventory-equipment`. It is player-scoped, owned by `local-player`, and coordinates inventory/equipment restore as one aggregate payload.

Phase 3 uses required `player.status-effects` and `player.resources` participants with non-overlapping ownership. Status effects load after inventory/equipment; resources load after attributes and statuses.

Feature 4.4 adds `player.quests-contracts`. It is player-scoped, owned by `local-player`, loads after inventory/equipment, resources, and statuses, and restores personal quest log and accepted contract journal state.

Feature 4.5 adds optional `player.location`. It is player-scoped, owned by `local-player`, loads after quests/contracts in `PositionAndPlace`, and restores same-scene player scene/place/position without mutating shared-world state.

Feature 4.6 adds persistent world-entity identity infrastructure. It does not add a save participant yet. `WorldEntityIdentity`, `WorldEntityReference`, and `WorldEntityRegistry` provide stable handles for authored and runtime world objects so future `SharedWorld` or `RegionOrScene` participants can refer to exact loaded objects without serializing Unity object references.

## Player State Versus World State

Player-state saves and shared-world saves must remain separable.

Loading one player's data may restore that player's inventory, equipment, vitals, personal progression, or last safe location in later features. It must not imply restoring shared markets, shared faction state, shared world entities, region state, or other players.

Shared-world state now uses a separate service, context, directory, checkpoint slot, readiness report, and consistency audit. Client-controlled player save/load commands do not rewind it.

For local testing, Feature 4.1 still uses manual save/load. That manual flow is a development proof only, not the intended multiplayer UX.

## Future Server Flow

In multiplayer, persistence should be server-owned and normally automatic.

When one player disconnects:

1. the server captures that player's player-scoped participants;
2. the server validates and persists only that player's owned state;
3. the shared world remains live;
4. other connected players continue in the same world without rollback;
5. shared-world participants persist on server cadence, region unload, explicit server checkpoint, or controlled shutdown;
6. clients receive results or sync state, but do not authoritatively write shared-world saves.

If a player later reconnects, the server restores that player's state into the current world state instead of rewinding the world to the moment that player disconnected.

Future offline-world progression may be supported by server simulation ticks, scheduled jobs, or region catch-up logic. That system should be explicit and server-authored; it should not be approximated by a client loading an old world save.

## Results

Structured result types:

- `PersistenceSaveResult`
- `PersistenceLoadResult`
- `PersistenceValidationResult`
- `PersistenceDeleteResult`

Statuses distinguish invalid slots, missing files, malformed JSON, wrong format, unsupported version, checksum mismatch, duplicate participant keys, missing payloads, missing runtime participants, participant prepare/commit failures, backup availability, write failures, backup failures, replacement failures, delete failures, and unknown exceptions.

Callers do not need to parse exception strings.

## Development Tools

Editor menu commands:

- `Tools > Persistence > Save Manual Slot 1`
- `Tools > Persistence > Load Manual Slot 1`
- `Tools > Persistence > Load Manual Slot 1 Backup`
- `Tools > Persistence > Validate Manual Slot 1`
- `Tools > Persistence > List Save Slots`
- `Tools > Persistence > Delete Manual Slot 1`
- `Tools > Persistence > Force Autosave`
- `Tools > Persistence > List Save Slot Descriptors`
- `Tools > Persistence > Increment Prototype Value`
- `Tools > Persistence > Toggle Prototype Flag`
- `Tools > Persistence > Corrupt Prototype Primary File`
- `Tools > Persistence > Integration Diagnostics`
- `Tools > Persistence > Run Recovery Scan`
- `Tools > Persistence > Promote Manual Slot 1 Backup`
- `Tools > Persistence > Open Prototype Save Folder`

These commands require Play Mode. If no prototype persistence root exists, the commands create one scene-local `Prototype Persistence` GameObject. It is not `DontDestroyOnLoad`; scene-loading persistence is deferred.

Feature 4.7 adds the player-facing Save/Load Tab menu page. That page uses five manual slots and three rotating autosaves through the same service APIs. See `Documentation/SaveSlotsAutosaveAndLoadUI.md`.

## Corruption And Recovery

Normal load checks the primary file. If the primary fails validation and a valid backup exists, the result reports backup availability. It does not silently hide corruption.

Backup load is explicit through the service API or development menu.

Corrupt files are not deleted automatically. Feature 4.8 adds recovery scanning, explicit backup promotion, explicit primary quarantine, and stale temporary save cleanup for development and future UI workflows.

## Main Thread Policy

Feature 4.1 is synchronous.

Unity object access, capture, prepare, and commit happen on the main thread. Future work may move serialization and disk I/O to background tasks after immutable DTO capture, but that is not implemented yet.

## Security Boundary

Local save files are user-editable. The checksum is only corruption detection, not anti-cheat, encryption, or security.

Future server-authoritative multiplayer persistence will require a different trust model.

Clients must not be allowed to become authoritative over `SharedWorld` or `RegionOrScene` persistence by uploading local save files.

World entity IDs are not proof of client ownership. They are object handles that future server-owned world persistence can validate against the authoritative loaded world/region state.

## Adding A Participant

1. Create a plain serializable DTO.
2. Implement `IPersistenceParticipant`.
3. Give it a stable unique key.
4. Assign a participant schema version.
5. Choose required or optional policy.
6. Choose persistence scope and owner ID policy.
7. Choose load phase and priority.
8. Capture only state owned by that runtime system.
9. Parse and validate payloads in `PreparePayload`.
10. Mutate live state only in `CommitPreparedPayload`.
11. Register the participant explicitly with `PersistenceService`.
12. Add tests for successful save/load and failed prepare leaving live state unchanged.

## Known Limitations

- No final save/load UI.
- No autosave.
- No cloud saves.
- No encryption or compression.
- No cross-scene loading.
- No full world-entity state persistence.
- No full gameplay persistence yet.
- No automatic migrations.
- No final player-facing recovery UI.
- Unity `JsonUtility` limits version-tolerant and polymorphic payload support.
