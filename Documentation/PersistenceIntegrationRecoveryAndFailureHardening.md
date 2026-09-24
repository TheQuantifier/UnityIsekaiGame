# Persistence Integration, Recovery, And Failure Hardening

Feature 4.8 hardens the existing local prototype persistence pipeline without adding shared-world persistence or networking.

For final Step 4 transaction, recovery, compatibility, and regression closeout, see `Documentation/Step4PersistenceArchitecture.md`, `Documentation/Step4PersistenceSchemaInventory.md`, and `Documentation/Step4PersistenceRegressionChecklist.md`.

## Architecture

`PersistenceService` remains the central coordinator. Participants still own their own DTO capture, validation, prepare, and commit logic. The service now owns transaction phases, dependency validation, rollback capture, consistency auditing, recovery source scanning, and structured diagnostics.

## Save Phases

Save transactions use:

- `Eligibility`;
- `ResolveDependencies`;
- `Capture`;
- `BuildEnvelope`;
- `WriteTemporary`;
- `VerifyTemporary`;
- `PreservePrevious`;
- `PromotePrimary`;
- `UpdateMetadata`;
- `Finalize`.

Every save receives a transaction ID. The save envelope stores `transactionId`, `parentTransactionId`, `saveRevision`, and `completedWriteMarker`. These are diagnostic transaction fields. The content checksum still covers the authoritative save content and remains compatible with prior Step 4 saves.

## Load Phases

Load transactions use:

- `Eligibility`;
- `Read`;
- `ValidateEnvelope`;
- `ResolveDependencies`;
- `PrepareParticipants`;
- `CaptureRollback`;
- `CommitParticipants`;
- `ConsistencyAudit`;
- `RollingBack`;
- `Finalize`.

All participants prepare before any participant commits. Required missing participants, owner/scope mismatch, unsupported schema, corrupt checksum, malformed JSON, and dependency failures reject the load before live mutation.

## Dependency Graph

Participants may optionally implement `IPersistenceParticipantDependencies`.

The service also provides default ordering dependencies for current player participants:

- `player.status-effects` depends on `player.inventory-equipment`;
- `player.resources` depends on `player.attributes` and `player.status-effects`;
- `player.quests-contracts` depends on inventory/equipment, resources, and status effects;
- `player.location` depends on `player.quests-contracts`.

The graph rejects explicitly declared missing required dependencies and circular dependencies. Missing ordering-only dependencies are reported as optional diagnostics, so focused participant tests can still exercise one participant in isolation. Ordering is deterministic and independent of registration order when related participants are registered together.

## Rollback

Before commit, the service captures an in-memory rollback snapshot by asking every ordered participant to capture and prepare its current state.

If a participant commit fails, the service restores already committed participants in reverse order using the rollback prepared payloads. Rollback is guarded by `PersistenceRestorationGuard` so systems can distinguish normal gameplay, restoration, and rollback.

Same-scene rollback is the current supported model. Cross-scene rollback is still not considered complete until a controlled scene loading service exists.

## Consistency Audit

After commit, the service runs a consistency audit provider if one is installed. A missing provider is treated as success for the prototype. A critical audit finding fails the load and triggers rollback.

Current participant-level validation already checks item-instance duplication, equipment compatibility, status restore validity, quest/contract objective restore validity, and location restore validity. Feature 4.8 adds a central audit hook so later systems can promote those checks into one post-load report.

## Runtime Safety

`PersistenceRuntimeSafety` can be:

- `Safe`;
- `Restoring`;
- `RolledBack`;
- `Degraded`;
- `Unsafe`.

If rollback fails, the runtime becomes `Unsafe`. Normal save/load is blocked so the prototype does not write a known-corrupt live state.

## Recovery Sources

`PersistenceService.ScanRecoverySources` inspects:

- primary saves;
- backups;
- autosave generations;
- stale temporary files.

Normal load never silently switches source. Recovery recommendations are explicit. Valid backups can be promoted through `PromoteBackup`; corrupt primaries can be moved aside through `QuarantinePrimary`; stale temp files can be cleaned through `CleanupStaleTemporaryFiles`.

## Fault Injection

`PersistenceFaultInjection` is disabled by default and one-shot. Development tools can inject:

- load prepare failure;
- load commit failure;
- consistency audit failure;
- temporary verification failure;
- backup preservation failure;
- primary promotion failure;
- rollback commit failure.

This exists for tests and Test Lab proof only.

## Fingerprints

`BuildRuntimeStateFingerprint` captures ordered participant payloads and hashes them. It is deterministic diagnostic data for round-trip and rollback comparisons. It is not save data and is not authoritative.

## UI And Test Lab

The Save/Load page displays operation phase, runtime safety, revision, and transaction ID for the selected slot.

The Test Lab Persistence section exposes:

- fingerprint;
- recovery scan;
- backup promotion;
- primary quarantine;
- stale temp cleanup;
- prepare/commit/audit fault injection;
- transaction/dependency diagnostics.

## Multiplayer Direction

The same concepts map to future server persistence: server-owned transaction IDs, durable storage transactions or event logs, server-side ownership checks, explicit recovery recommendations, and idempotent retry handling. Clients should receive results but should not control shared-world recovery or become authoritative over shared-world state.

## Known Limitations

- Shared-world systems have their own context and checkpoint; individual spawned actors, pickups, doors, and containers still need dedicated participants.
- Cross-scene rollback remains limited.
- Required player-authority and world-location/scene-binding consistency validators run after restore; additional domain-specific checks can be added independently.
- Recovery actions are explicit development/prototype operations, not final UI.
- Checksums detect accidental corruption and interrupted writes, not malicious tampering.
