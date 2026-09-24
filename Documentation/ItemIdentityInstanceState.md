# Feature 9.1 - Item Identity and Instance State

Feature 9.1 establishes persistent identity and authoritative state for specific item objects. Because the project is still pre-release, item identity is the only supported live contract.

## Item Definition Versus Item Instance

An item definition describes a reusable type such as `item.prototype-sword`.

An item instance describes one specific object of that type. Its stable item-instance ID is independent from Unity `GameObject` identity, scene hierarchy, display name, inventory slot, equipment slot, or world representation.

A logical item is not its `GameObject`. Destroying, disabling, unloading, or replacing a visual representation must not silently destroy the logical item record.

## Persistent Identity

Individually tracked items use canonical GUID item-instance IDs. The ID must remain stable across transfer, equipment, world placement, save/restore, naming, future repair, future modification, and future production flows.

Feature 9.1 uses `ItemInstanceIdentityRuntime` as the authoritative record owner for item instance state. Player inventory and equipment slots carry `itemInstanceId` directly and no deprecated nested item-instance model is supported.

## Fungible Versus Individually Tracked

Items may be classified as:

- Individually tracked
- Fungible
- Stackable while equivalent
- Batch tracked
- Unique
- Serialized
- World fixture
- Virtual

Stacking remains governed by inventory rules for equivalent items. Even stack slots have an identity-backed projection ID so persistence and synchronization can describe the exact stack aggregate. Feature 9.1 adds an identity-level stacking guard through `ItemIdentityInventoryBridge.CanShareStack`: identity records can only share a stack when they are fungible, share one definition, and have equivalent names, condition, quality, owner/custodian, provenance, maker mark, serial number, authenticity, access policy, and placement history.

Items must separate into distinct instances when they gain instance-specific condition, quality, ownership, provenance, naming, access restrictions, or other identity-relevant state.

## Lifecycle

The item identity runtime represents lifecycle states including created, active, stored, in inventory, equipped, placed in world, in transit, reserved, lost, missing, disputed, destroyed, consumed, depleted, broken, disassembled, archived, and quarantined.

Feature 9.1 defines the boundaries. Later systems implement breakage, durability loss, repair, disassembly, field salvage, and production behavior.

## Location and Containment

An item has one coherent current location state:

- Container
- Inventory
- Equipped
- World placement
- Transit
- Reserved
- Destroyed
- Consumed

The runtime rejects incompatible simultaneous locations. Inventory and equipment remain mutation surfaces for player actions, but their placement must be synchronized into item identity through `PlayerItemIdentitySynchronizer` or `ItemIdentityInventoryBridge.SynchronizeInventoryEquipmentRuntime`. `ItemIdentityInventoryBridge.ValidateSynchronizedProjection` detects divergence such as inventory containing an item whose identity says it is equipped or world-placed.

## Ownership Versus Custody

Ownership is who owns or claims the item.

Custody is who physically holds, carries, equips, stores, or controls it.

The runtime supports person ownership, organization ownership, shared ownership, disputed ownership, public/communal ownership, custodial-only possession, and unowned/unknown states. Ownership transfer does not automatically move custody. Custody transfer does not automatically change ownership.

## Condition Foundation

Feature 9.1 stores general condition state and optional normalized condition:

- Unknown
- Pristine
- Excellent
- Good
- Used
- Worn
- Damaged
- Severely damaged
- Broken
- Destroyed

Condition is not full durability. Durability degradation, wear, repair, and item recovery belong to later systems.

## Quality Foundation

Feature 9.1 stores foundational quality state:

- Tier
- Source
- Optional normalized value
- Workmanship label
- Quality definition reference
- Provenance reference

Foundational quality is not generated affixes, rarity balancing, crafting-skill output, or material-driven quality. Those remain deferred.

## Names, Marks, and Serials

The runtime records custom names, original names, maker marks, serial numbers, batch numbers, inscriptions, owner marks, organization marks, seals, public labels, hidden labels, authenticity status, and attribution status.

Mutable names and labels are never stable identity.

## Provenance

The runtime stores compact provenance references:

- Creation source
- Creator
- Manufacturer organization
- Creation location
- Production batch
- Source items
- Parent items
- Prior owners
- Prior custodians
- Transfer event IDs
- Historical event IDs
- Knowledge record IDs

Step 8 history remains the authoritative event log. The item runtime keeps item-centric references and current summary state.

`ItemIdentityHistoryIntegration` provides the representative Feature 9.1 contract for item creation, ownership transfer, and destruction events. It records history payloads with the exact item-instance ID and can preview required history before committing ownership transfer.

## Knowledge and Access

Person knowledge is not authoritative item state. A Person can misidentify an item, fail to know its owner, or see a redacted provenance view without changing the item truth.

Item projections use Step 8 information-access contracts by returning an `InformationSubjectReferenceData` with a stable item-instance subject ID and item tags. To preserve existing serialized enum compatibility, item subjects currently use `InformationSubjectType.Custom` with `domain.item`, `item.instance`, and `subject-type:item-instance` tags.

Protected projection fields include owner, custodian, serial, maker, provenance, authenticity, hidden name, access policy, and secret production source. Picking up an item must not imply knowledge of every protected field.

## World Representation

World representations should reference a stable item-instance ID and, for scene-authored items, a stable placement ID. A duplicate placement ID is invalid. A visual object can be unloaded without losing logical item state.

The representation contract now carries optional prefab/addressable references, interaction profile, physics profile, collider profile, persistence profile, equipment use, pickup adapter, validation profile, placement surface, layer/tag, default scale/orientation, ground offset, and trigger/physical/movable flags.

## Scene-Authored and Runtime-Created Items

Scene-authored items require unique stable placement identity. Runtime-created items receive run/world-stable item-instance IDs. Restore must not replay item creation or duplicate scene-authored records.

## Persistence and Projection

`ItemInstanceIdentityPersistenceParticipant` persists identity records as a world-scoped participant for current saves. Prepare validates the full payload before commit. Failed prepare leaves live runtime state unchanged. Commit uses rollback on unexpected restore failure.

`PlayerInventoryEquipmentPersistenceParticipant` writes the inventory/equipment DTO container used by UI and slot restore code. Entries are identity projections: `definitionId`, `itemInstanceId`, and quantity/slot. Capture synchronizes current inventory/equipment state into `ItemInstanceIdentityRuntime`; prepare validates the projection; commit restores inventory/equipment and then resynchronizes identity. The projection is not a second source of truth.

`ItemIdentityInventoryBridge.BuildInventoryEquipmentProjection` deterministically creates identity records from current inventory/equipment state:

- Stateful inventory entries keep their existing persistent item-instance IDs and become inventory-located identity records.
- Stateful equipment entries keep their existing persistent item-instance IDs and become equipped identity records.
- Definition-only stacks become fungible stack records with deterministic aggregate IDs and a stack quantity when no explicit `itemInstanceId` is present.

For current saves:

- Inventory stateful entries write `definitionId + itemInstanceId`.
- Inventory stack entries write `definitionId + itemInstanceId + quantity`.
- Equipment entries write `definitionId + itemInstanceId + slotType`.
There is no old-save conversion path. Pre-Group-6 saves should be deleted during development.

## Live Synchronization

`PlayerItemIdentitySynchronizer` is the central live adapter between player inventory/equipment and item identity. It listens for inventory/equipment changes, builds the current projection, creates missing identity records, updates equipped/inventory locations, preserves richer metadata already held by identity, and marks records that disappear from the player inventory/equipment projection as lost rather than leaving stale player-held locations.

New gameplay actions use current inventory/equipment APIs as mutation commands and rely on the synchronizer for identity state. Systems that need item truth read `ItemInstanceIdentityRuntime` snapshots or access-aware projections, not raw inventory/equipment slots.

## Validation

Validation rejects:

- Missing or duplicate item-instance IDs
- Unknown item-definition IDs when a registry is provided
- Invalid classifications, lifecycle states, condition states, quality states, and location states
- Multiple incompatible location references
- Destroyed or consumed lifecycle/location mismatches
- Self-referential parent/source item provenance
- Unknown parent/source item provenance
- Circular parent/source provenance chains
- Duplicate serial numbers within an item identity graph
- Invalid provenance time values
- Creation time after destruction time
- Invalid world representation scale or offset
- Duplicate world placement IDs during placement

## Test Lab and Automation

Feature 9.1 is designed to work with fixture-owned automation using run-scoped mutable item IDs. The code foundation is independent of active scene objects and can run in fresh runtime contexts. Automation covers distinct instances, ownership/custody, location validation, save/restore, current inventory/equipment synchronization, and access subject projection. Edit Mode coverage also verifies identity-projection-only saves, live synchronization, and persistence drift rejection.

## Deferred Features

Feature 9.1 intentionally does not implement:

- Material composition
- Generated affixes
- Rarity balancing
- Durability loss
- Wear simulation
- Repair
- Salvage
- Tool requirements
- Crafting stations
- Recipes
- Crafting execution
- Production queues
- Experimentation
- Economy
- Final inventory/equipment UI
- Final placement tooling

Those belong to Features 9.2 through 9.10.
