# Server-Authoritative Narrative, Parties, and Interactions

## Connected-play rule

While connected, a client may express an intent but may not directly mutate interaction, quest, dialogue, or party state. `LocalNarrativeAuthorityBridge` converts presentation input into a sequenced `NetworkNarrativeCommand`. `NetworkPlayerNarrative` accepts the owner-only RPC, rejects malformed or replayed input, and delegates the command to `ServerPlayerNarrativeAuthority`.

Offline play continues to use the existing local domain services.

## Interaction authorization

The interaction command contains only the authored interaction-point identifier. The server resolves that identifier in the loaded scene and verifies that:

1. the binding is active and complete;
2. the authoritative network actor is within the authored interaction range, including a small transport tolerance;
3. the interaction point and service are active;
4. the authoritative person is eligible to use the point.

Only then does the server invoke the destination runtime. The client opens the matching presentation after it receives success; presenting a panel does not repeat the mutation.

Quest-source access receives a short-lived proximity authorization. Browse and accept requests must match that authorized source and remain in range. This prevents a client from accepting an arbitrary listing by sending its identifier from elsewhere in the world.

## Quest and dialogue authority

Quest acceptance runs through the authoritative narrative coordinator and server-owned inventory. Authoritative inventory additions feed item-objective progress. Dialogue start, choice selection, and termination are likewise validated and advanced on the server before their client presentation is updated.

Reward-bearing dialogue choices and explicit reward claims are rejected online until reward, capacity, currency, and item identity changes can commit atomically. They are not allowed to fall back to local mutation.

## Party authority

Creation, invitation, acceptance, decline, removal, leadership transfer, readiness, settings, leave, and dissolution all execute against the server party service. The client party menu issues requests and refreshes its current projection only after a successful result.

## Current projection boundary

The client currently mirrors successful quest and party operations into its local presentation model. That model is disposable and is not a source of truth. Group 9 will add per-session person identity, authoritative persistence ownership, and reconnect snapshots so a newly connected client reconstructs the same view without replaying commands.

## Smoke test

The opt-in flags are `--narrative-smoke-seed` on the server and `--narrative-smoke` on the client. The smoke path validates a nearby interaction, browses its quest source, and creates a party through the network command boundary.
