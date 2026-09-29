# Server-Authoritative Inventory and Equipment

## Group 6 objective

Group 6 moves player inventory, equipment, item use, and item removal behind a server-owned command and snapshot boundary. The client may select an item and request an action, but it must not directly change quantities, identities, equipment references, or item lifecycle state while connected.

## Initial audit

The current standalone gameplay path has several legitimate writers:

- the inventory menu uses, equips, unequips, and drops items;
- pickups and rewards add items;
- ranged combat consumes ammunition;
- crafting and decomposition consume or transform item instances;
- persistence restores inventory, equipment, identity, quality, and durability together.

Those writers previously called `PlayerInventory` and `PlayerEquipment` directly. Group 6 locks connected replicas against every ordinary mutation API. The inventory menu routes supported player actions through owner-only commands; later domain groups must expose server-owned reward, pickup, ammunition, and crafting operations before those features are enabled online.

## Boundary

```text
Client selection and presentation
              |
              v
Versioned inventory command with sequence number
              |
              v
Server validates ownership, slot, quantity, item state, and action
              |
              v
Server mutates canonical inventory/equipment/item-instance state
              |
              v
Validated inventory/equipment snapshot and command result
              |
              v
Client replaces its read-only presentation replica
```

Equipment records must reference a matching, inventory-owned stateful item identity; equipment is not a second item container. The server keeps the canonical `PlayerInventory` and `PlayerEquipment`, resolves item definitions and effects locally, and publishes a complete revision after each successful command. The client validates both protocol integrity and its definition catalog before applying inventory and equipment without exposing an intermediate half-revision to UI listeners.

## Completed Group 6 scope

1. Per-session server inventory/equipment creation and initial restore.
2. Complete revision replication with corrupt/partial snapshot rejection.
3. Read-only connected client replicas with a dedicated snapshot application path.
4. Inventory-menu use, equip, unequip, drop-one, and drop-all command routing.
5. Canonical item identity and equipment-reference preservation.
6. Server-held reconnect state for the lifetime of the dedicated-server process.
7. Separate-process drop/equip/unequip/use and reconnect verification.

World pickup creation is intentionally not faked by a client-owned object: Group 6 drop removes the item authoritatively. Replicated world entities, rewards, ammunition consumption, crafting transactions, durable database persistence, and durability/quality mutation enter through their later dedicated server-authority groups. Until then, connected replica guards reject those legacy local writes.

## Security and correctness rules

- Commands are owner-only and strictly sequenced, including unsigned wraparound.
- Inventory snapshots and command results are readable only by the owning client.
- Slot indexes and quantities are range checked before domain lookup.
- A client never supplies item definitions, stat modifiers, durability, or effects.
- The server resolves all definition data by stable ID.
- Item instance IDs are unique across inventory slots.
- An equipped item remains inventory-owned and can occupy only one equipment slot.
- Snapshot application is all-or-nothing and revision ordered.
- Failed commands never consume or duplicate items.

## Verification

Verified on September 29, 2026 with Unity `6000.5.4f1`:

- Focused authority tests: 7 passed, 0 failed.
- Full EditMode suite: 1,403 passed, 0 failed.
- Full PlayMode suite: 5 passed, 0 failed.
- Windows dedicated-server build: succeeded.
- Windows client build: succeeded.
- Separate-process smoke: drop, equip, unequip, use, graceful disconnect, same-player reconnect, and restored quantities all observed.

The opt-in smoke flags are `--inventory-smoke-seed` on the server and `--inventory-smoke` on the client. They only seed and exercise test data when explicitly supplied.
