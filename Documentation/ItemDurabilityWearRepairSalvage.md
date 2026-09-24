# Item Durability, Wear, Repair, and Breakage

Feature 9.4 introduces `ItemDurabilityRuntime` as the authoritative owner of physical item condition.

## Ownership Boundary

`ItemInstanceIdentityRuntime` owns identity and lifecycle only. Gameplay systems read and mutate physical condition exclusively through `ItemDurabilityRuntime`.

The item runtime ownership model is:

- `ItemInstanceIdentityRuntime`: stable identity, ownership, custody, location, and lifecycle.
- `ItemCompositionRuntime`: materials, components, and physical structure.
- `ItemQualityAffixRuntime`: workmanship, quality, defects, rarity, affixes, and stat modifiers.
- `ItemDurabilityRuntime`: current/max/original durability, wear, damage channels, repair history, breakage, and functional contribution.
- `DisassemblyRuntime`: item recovery operations, component return rolls, and salvage-pickup history.

## Default Initialization

When a durability record is missing, `EnsureDefaultDurability` creates one from the current item instance, composition, quality, authored defaults, and material durability. No deprecated condition record is consulted.

## Gameplay Effects

Damage and wear reduce current durability. Permanent damage also lowers maximum durability relative to original maximum durability. Repair restores current durability but may add permanent capacity loss depending on repair quality.

Below 10%, each whole durability percentage crossed receives one authoritative break roll. The chance rises as structure approaches failure. These values, the critical/immediate thresholds, environmental timers, and broken-drop behavior live in the catalog's `durability-policy.standard` asset rather than on a scene component:

| Durability reached | Break chance |
|---:|---:|
| 10% | 5% |
| 9% | 7% |
| 8% | 10% |
| 7% | 14% |
| 6% | 19% |
| 5% | 25% |

The percentage cursor and roll sequence persist, so saving/loading cannot repeat a check. A successful roll stops the damage at that percentage and leaves the same item in the Broken state. Broken items remain items rather than silently becoming resources. Repairing above 10% clears the broken state and starts a fresh descent.

Each durability record stores the policy ID that governed it. The runtime resolves that policy from `DefinitionRegistry`, making balance edits data-driven and catalog-validated while retaining deterministic server-authoritative rolls.

If every roll through 5% fails, durability stops at 5% and requests forced natural decomposition. An equipped item is removed and its recovered pieces drop beside the character as Salvager-eligible pickups. Materials from an inventory or storage container remain in that inventory/container. A loose world item produces loose Salvager-eligible pieces at its world position.

Functional state is derived from item and component durability:

- fully functional items contribute normally;
- impaired and partially disabled items can be scaled by consumers;
- broken or destroyed items contribute no equipment stat modifiers;
- a completed disassembly closes the durability record through `MarkDestroyedByItemRecovery`; durability does not calculate or create recovered outputs.

## Components

Durability can be tracked at the item level and at composition component level. Component IDs are validated against the item composition when composition data is available. Critical or essential broken components can make the whole item broken.

## Persistence

`ItemDurabilityPersistenceParticipant` persists durability as shared world state after identity, composition, and quality records are available. Restore validates item references, component references, duplicate durability records, and schema version before committing. Failed restore uses runtime rollback.

## Access Projection

Durability projections expose Step 8 information subjects. Redacted projections can hide repair history, structural weakness, hidden damage, maintenance provenance, and item-recovery yield information while still returning a stable projection object.

## Test Lab

Feature 9.4 automation runs in the item runtime fixture bundle. The fixture snapshots and restores item identity, composition, quality, and durability together so durability mutations do not leak between scenarios.

## Current Limitations

This feature intentionally does not implement full repair stations, tool quality requirements, crafting queues, merchant pricing, or final UI. Item recovery is documented separately in `DisassemblyAndSalvage.md`.
