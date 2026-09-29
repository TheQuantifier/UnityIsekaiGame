# Server Persistence and Session Restore

## Group 9 objective

Phase 5 Group 9 makes connected-play persistence server-owned. A client owns neither save files nor restore decisions. Reconnecting with the same validated player ID reconstructs the same stable Person and actor identity, position, vitals, inventory, equipment, quest journal, party state, invitations, and active dialogue identity.

## Durable player profiles

`ServerPlayerProfileStore` writes one profile beneath the server save root for each canonical player ID. The payload includes stable player, Person, and actor IDs plus position/yaw, authoritative vitals, inventory, and equipment. Profile files use a versioned envelope, SHA-256 payload checksum, temporary-file write, atomic replacement, and one previous-generation backup.

Profiles are captured on authoritative inventory mutations, scheduled autosave, disconnect, and server shutdown. The server validates that a loaded profile belongs to the connecting session before using any state. A corrupt primary profile falls back to the valid backup; an identity mismatch or two invalid generations is rejected rather than partially restored.

## Stable session identity

`PlayerSessionRegistry` now derives two stable IDs from the admitted player ID:

- `person.player.<canonical-player-id>` for narrative, quest, party, and social ownership;
- `actor.player.<canonical-player-id>` for the network-controlled body.

The transient session ID still changes on every connection. `NetworkPlayerActor` replicates all three identities so client presentation can verify that owner-only snapshots belong to the correct Person.

## World persistence ownership

The dedicated server loads the current world checkpoint before accepting gameplay state as durable. It saves the world checkpoint on an unscaled scheduled interval and during orderly shutdown. Quest, party, dialogue, narrative, social, location, economy, and other registered world participants remain in the existing dependency-validated world persistence graph.

The Save/Load menu becomes read-only while a client is connected and explicitly explains that persistence is server-managed. Offline prototype play retains its existing manual-save workflow.

## Reconnect projections

The server publishes an owner-only narrative snapshot after actor spawn and after authoritative narrative or party changes. It contains:

- the Person's quest assignments, quest records, objectives, outcomes, and rewards;
- party identity, roster, leader, settings, readiness, and pending invitations;
- the active authoritative dialogue-flow identity.

The connected quest journal, quest tracker, and party menu consume this projection. They no longer depend on replaying successful client commands into a disposable local authority model, so a fresh client process can reconstruct the same presentation after reconnect.

## Verification

- Unity script compilation completed without errors.
- `UnityIsekaiGame.Networking.Server`, `UnityIsekaiGame.UI`, and `UnityIsekaiGame.EditModeTests` build with zero warnings and zero errors.
- 1,417 EditMode tests passed, including durable profile round-trip, backup recovery, identity mismatch rejection, and stable Person identity across sessions.
- 5 PlayMode tests passed.
- Fresh Windows dedicated-server and client builds completed successfully.

Group 10 subsequently completed package/content extraction. Only Group 11, creation of the physically separate Client and Server Unity projects, remains.
