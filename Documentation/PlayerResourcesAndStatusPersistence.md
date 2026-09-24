# Player Resources And Status-Effect Persistence

Phase 3 uses two non-overlapping player participants:

- `player.resources` is required and owns current Health, Mana, Stamina, resource history, initialization state, and processed resource-event IDs.
- `player.status-effects` is required and owns save-eligible active statuses plus actor-profile validation metadata.

Calculated maximums and stat modifiers are derived. They rebuild from definitions, attributes, equipment, traits, and active statuses rather than being stored as authoritative values.

## Load Order

1. Identity, attributes, skills, inventory, and equipment restore.
2. Status effects restore and rebuild their modifiers.
3. Current resources restore and clamp against rebuilt maximums.
4. Lifecycle, quests/contracts, and location restore.

`player.status-effects` depends on `player.inventory-equipment`. `player.resources` depends on `player.attributes` and `player.status-effects`. No participant contains fallback fields owned by the other.

## Clean-Break Policy

The project is still in development. Only envelope schema 3 and the current participant schemas are accepted. Older development saves are intentionally unsupported and should be deleted rather than migrated.

## Status Policy

Only statuses whose definition uses `SaveRemainingDuration` or `PersistentUntilRemoved` are saved. Instant, expired, removed, `DoNotSave`, and `ExpireOnLoad` statuses are excluded. Application IDs remain stable and duplicates are rejected.

## Multiplayer Direction

These participants are player-owned. A future authoritative server can save or restore one player without stopping or overwriting the separate shared-world persistence context.
