# Client/server action authority audit

Audit date: 2026-10-02

This document records the end-to-end authority boundary for every action currently exposed by the vertical slice. The dedicated server owns all durable and world-affecting state. Clients submit intent and may predict presentation, but they do not commit authoritative results.

## Action matrix

| Area | Client to server | Server validation and mutation | Server to client |
| --- | --- | --- | --- |
| App connection | Versioned connection payload, build version, application token, client-instance ID | Capacity, protocol/build compatibility, fixed-time token comparison, unauthenticated timeout | Approval or transport disconnect reason |
| Account login/create | Size-bounded reliable-sequenced authentication message | Request schema, identifier/password policy, per-client backoff, one request in flight, salted PBKDF2 verifier, single active account session | Size-bounded reliable-sequenced response; player actor spawns only after success |
| Pause protection | Owner-only reliable state request | Known enum, ownership, rate limit | Server-written participation state replicated to all observers |
| Movement/sprint | Owner-only sequenced input; continuous input is unreliable and stop edges are reliable | Finite normalized input, monotonic sequence, rate limit, authoritative collision/motor/vitals, input timeout | Atomic movement state containing simulation tick, accepted input sequence, transform and velocity |
| Jump | Owner-only reliable event tied to an input sequence | Ownership, pause state, replay rejection and rate limit; server grounding decides whether it executes | Included in authoritative movement simulation/state |
| Vitals | No direct client mutation | Server-only damage, healing, spending, regeneration and pause immunity | Server-written atomic vitals state |
| Inventory | Owner-only reliable use/equip/unequip/drop/move command | Command shape, monotonic sequence, rate limit, ownership, current slot/equipment data, pause state, transactional rollback | Owner-only result plus generation-tagged committed inventory/equipment snapshot |
| World drop | Inventory command only | Item ownership, quantity, equipped state, server spawn and inventory removal as one rollback-capable transaction | Replicated authoritative pickup object and inventory snapshot |
| World pickup | Reliable request on the server-owned pickup object | Authenticated player/inventory resolution, rate limit, pause state, range, obstruction, identity, capacity and rollback | Requester-only result; successful pickup despawns for every client |
| Primary attack | Owner-only reliable command | Command shape, aim/facing, pause/defeat state, cooldown, stamina, equipped weapon, server physics and target authority | Owner-only result plus public combat-world snapshot |
| Spell cast/projectile | Owner-only reliable command | Known spell, aim/facing, pause/defeat state, cooldown, mana, supported costs, server projectile simulation and impact | Owner-only result; server combat/vitals snapshots; client projectile is presentation prediction only |
| Interaction/dialogue/quests | Owner-only reliable narrative command | Payload shape, monotonic sequence, rate limit, pause state, authoritative scene binding/range, short-lived source authorization and domain rules | Owner-only typed result plus generation-framed reconnect snapshot |
| Party actions | Owner-only reliable narrative command | Payload enum/field shape, membership, invitation, leadership and party-domain rules | Owner-only typed result plus authoritative party projection |
| Disconnect | Transport event | Profile save, target/session removal, auth state cleanup and per-client pickup limiter cleanup | Actor despawn and transport disconnect |

## Invariants checked

- Every client-to-server RPC is either owner-only or, for server-owned pickups, resolves the sender back to an authenticated player authority.
- Every replicated variable and list is server-write-only.
- Sensitive session, client-instance and opaque account IDs are owner-readable rather than broadcast to other clients.
- Reliable commands reject replayed/non-monotonic sequences and malformed action-specific fields.
- Continuous movement uses unreliable delivery, while stop and jump edges use reliable delivery.
- Inventory and combat list payloads carry the same generation as their commit revision; clients retry rather than applying mixed generations.
- Movement acknowledgement and transform are carried in one atomic state, preventing acknowledgement/position mismatches.
- Server command handlers are exception-contained and return sanitized failures.
- Paused-protected players cannot move, spend/receive world-interaction vitals changes, use inventory, collect pickups, fight, interact, or be selected as enemy targets.
- An app-authenticated connection may remain on the login screen for up to 10 minutes; a credential-verification request has a separate 15-second execution timeout. If the idle login connection expires, submitting Login or Create Account reconnects the app and resumes that request automatically. Failed-attempt throttling still prevents rapid credential retries.
- The connection protocol version is bumped whenever replicated wire shapes change.

## Scope note

The current transport is intentionally configured for local/offline use. Passwords are protected at rest with salted PBKDF2 verifiers, but the local NGO transport is not an Internet authentication channel. Before exposing the server beyond a trusted machine/LAN, place authentication behind a TLS-protected service or add transport encryption and replace the shared application token with short-lived signed admission tickets.
