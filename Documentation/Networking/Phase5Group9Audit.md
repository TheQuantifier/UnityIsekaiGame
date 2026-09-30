# Phase 5 Group 9 Audit

Group 9 began from commit `17bb917` on `phase-5/group-9-server-persistence-session-restore`.

The pre-change audit found that player sessions already had stable actor IDs, but narrative state used one prototype Person ID, inventory survived only in an in-memory dictionary, vitals and position reset to prototype defaults, world checkpoints were not owned by the dedicated-server lifecycle, and the client reconstructed quest/party presentation by replaying successful commands locally.

Group 9 replaces those temporary boundaries with stable per-player Person identity, durable checksummed player profiles, scheduled/disconnect/shutdown persistence, server-owned world checkpoint loading/saving, and owner-only reconnect projections. Offline prototype persistence remains available, while connected manual save/load is disabled.

The physical world-drop boundary remains intentionally registered in `DeferredAuthorityBoundaries.md`; Group 9 does not disguise an inventory discard as a spawned world pickup.
