# Feature 5.4b Persistence and Migration

Feature 5.4b introduced player participant `player.resources`; Phase 3 now makes it required and authoritative.

- Scope: `Player`
- Owner: local prototype player ID
- Schema: `1`
- Load phase: `Vitals`
- Required: `true`

The participant saves `PlayerResourcesSaveData`, including resource definition IDs, current values, last known maximums, lifetime totals, initialization data, and processed event IDs.

`player.status-effects` separately owns active statuses. No fallback vital fields or pre-Phase-3 save compatibility remain. Delete and recreate development saves after this clean break.

Calculated Stats are still rebuilt and are not saved as authoritative values. Resource maximums are derived from rebuilt Calculated Stats during resource restore and reconciliation.

Future multiplayer persistence remains server-owned. Clients may request resource-affecting actions, but clients should not become authoritative over shared-world or player resource state. On disconnect, the future server should persist that player's resource records while the shared world continues for remaining players.
