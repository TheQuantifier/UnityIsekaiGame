# Item Instance And Serialization Foundation

This document describes the current item-instance contract. The Step 9 runtimes are the only supported implementation; there is no compatibility path for the earlier nested item/metadata model.

## Definition Versus Instance Identity

`ItemDefinition` describes a reusable item type. `ItemInstanceIdentityRuntime` owns each live item-instance record, including its stable ID, definition ID, lifecycle, location, ownership, custody, stack quantity, names, marks, and provenance.

Composition, quality/affixes, and durability are separate authorities keyed by the same item-instance ID:

- `ItemCompositionRuntime`
- `ItemQualityAffixRuntime`
- `ItemDurabilityRuntime`

This avoids duplicated condition or quality fields in inventory entries and keeps each kind of state independently validatable.

## Creation And Mutation

Items are created through `ItemInstanceIdentityRuntime.CreateItem`. Individually tracked, unique, and serialized items always have quantity one. Fungible or equivalent-stack records may carry a larger `stackQuantity`.

Partial crafting consumption uses `ConsumeStackQuantity`. It leaves the unconsumed quantity on the original identity and creates a terminal consumed identity for the exact amount used, retaining provenance back to the source stack. Whole-stack consumption terminates the original identity.

Location, ownership, custody, world placement, lifecycle, labels, and other identity changes go through explicit runtime operations. Gameplay code must not mutate save DTOs directly.

## Inventory And Equipment Projection

`PlayerInventory` and `PlayerEquipment` store item-definition references plus item-instance IDs and quantities needed by their UI and gameplay APIs. `PlayerItemIdentitySynchronizer` keeps that projection aligned with `ItemInstanceIdentityRuntime`.

`ItemIdentityInventoryBridge.BuildInventoryEquipmentProjection` creates or validates the identity-side projection. It is not an old-save migration layer. The project is pre-release, so incompatible development saves are deleted rather than translated.

## Serialization

Each authority produces its own versioned save data:

- `ItemInstanceRuntimeSaveData`
- `ItemCompositionRuntimeSaveData`
- `ItemQualityAffixRuntimeSaveData`
- `ItemDurabilityRuntimeSaveData`

Save data stores stable definition and instance IDs rather than Unity object-instance IDs. Restore resolves definitions through `DefinitionRegistry`, validates the complete payload before commit, and rejects missing definitions, malformed IDs, invalid lifecycle/location combinations, and broken cross-runtime references.

The player inventory/equipment participant stores only the projection needed to rebuild its slots. World-scoped item participants remain authoritative for the richer item graph.

## Stacking

Stack compatibility is evaluated from item-definition rules plus identity state. Items can share a stack only when their definition, classification, ownership/custody, quality, condition, provenance, naming, access, and other identity-relevant state are compatible. Stateful equipment remains individually tracked.

Crafting consumes exact stack quantities and creates outputs with their authored quantities. Stackable outputs use one aggregate identity with the matching `stackQuantity`; non-stackable outputs are emitted one per output record.

## Validation And Tests

The Step 9 integration validator checks identity, composition, quality, durability, crafting, production, recipe-knowledge, and experimentation references as a single graph. Edit Mode tests cover serialization and partial-stack behavior, while Prototype Play Mode validation exercises live inventory synchronization, crafting, pickup interactions, and persistence capture.

For the detailed identity contract, see `Documentation/ItemIdentityInstanceState.md`. For the complete authority and persistence order, see `Documentation/Step9ItemCraftingIntegration.md`.
