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

Those writers currently call `PlayerInventory` and `PlayerEquipment` directly. Adding UI-only RPCs would leave the other mutation paths unprotected, so Group 6 uses one command boundary for every connected-player inventory mutation.

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

The first Group 6 slice defines the transport-safe command types, limits, replay protection, inventory slot records, equipment reference records, and complete-snapshot validation. Equipment records must reference a matching, inventory-owned stateful item identity; equipment is not a second item container.

## Remaining implementation slices

1. Add the server-owned per-session inventory/equipment adapter and initial restore.
2. Replicate complete revisions atomically and reject partial or corrupt snapshots.
3. Put connected client inventory/equipment containers into external-replica mode.
4. Route use, equip, unequip, drop, pickup, reward, ammunition, and crafting requests through authority commands.
5. Integrate item identity, durability, quality, composition, and persistence transactions.
6. Add separate-process smoke coverage for use/equip/drop and reconnect restoration.

## Security and correctness rules

- Commands are owner-only and strictly sequenced, including unsigned wraparound.
- Slot indexes and quantities are range checked before domain lookup.
- A client never supplies item definitions, stat modifiers, durability, or effects.
- The server resolves all definition data by stable ID.
- Item instance IDs are unique across inventory slots.
- An equipped item remains inventory-owned and can occupy only one equipment slot.
- Snapshot application is all-or-nothing and revision ordered.
- Failed commands never consume or duplicate items.
