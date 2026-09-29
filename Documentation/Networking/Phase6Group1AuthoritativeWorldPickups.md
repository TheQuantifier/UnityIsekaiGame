# Phase 6 Group 1: Authoritative Replicated World Pickups

## Outcome

Online item drops are physical server-owned entities rather than permanent discards. A connected player can drop an item, see the replicated pickup, interact with it, and receive the item back only after server validation.

## Transaction flow

```text
Owner drop request
  -> server validates slot, quantity, equipment state
  -> server prepares exact pickup identity
  -> server spawns registered NetworkWorldItemPickup
  -> server removes inventory quantity
  -> server publishes and persists the inventory revision
  -> any failure despawns the pickup and restores inventory

Client pickup interaction
  -> non-owner reliable RPC identifies the sending client
  -> server resolves that client's authoritative player actor
  -> server validates pickup availability and distance
  -> server restores the exact identity and storage mode to inventory
  -> server publishes and persists the inventory revision
  -> pickup despawns only after the inventory transaction succeeds
```

Full-stack drops retain their existing item instance ID. Partial stack drops create one new canonical split identity so the remaining inventory stack and world stack never share an identity. Definition stacks return as definition stacks; they do not silently become stateful equipment instances.

## Runtime assets

- Shared network behavior: `NetworkWorldItemPickup`
- Server coordinator: `ServerWorldItemAuthority`
- Registered prefab: `Packages/com.thequantifier.isekai.content/Content/Networking/Prefabs/NetworkWorldItemPickup.prefab`
- Registration: both client and dedicated-server `DefaultNetworkPrefabs.asset` files

The pickup uses a trigger collider so it participates in the existing camera interaction system without physically blocking the player. Netcode replicates the server-authored spawn transform and item payload to all observers.

## Security and correctness rules

- Clients supply no definition, identity, quantity, or transform during pickup.
- Requests may target a non-owned pickup, but sender identity comes from Netcode RPC metadata.
- The server rejects missing sessions, stale/despawned pickups, concurrent collection, excessive distance, duplicate identity, invalid storage mode, and insufficient capacity.
- A successful pickup is removed from the world only after authoritative inventory publication and persistence complete.
- Connected client inventory remains read-only.

## Verification

Verified on September 29, 2026 with Unity `6000.5.4f1`:

- Focused authority/identity tests: 11 passed, 0 failed.
- Complete client EditMode suite: 1,428 passed, 0 failed.
- Complete client PlayMode suite: 5 passed, 0 failed.
- Dedicated-server EditMode suite: 8 passed, 0 failed.
- Windows client and dedicated-server builds: succeeded.
- Separate-process loopback: the client dropped one item from a two-item stack, observed a new replicated split identity, requested collection, observed the exact pickup return to authoritative inventory, then completed equip, unequip, and use operations.
