# Rarity, Quality, And Condition

This document describes the Step 3.4 foundation for rarity, quality, and condition.

## Core Distinction

Rarity, quality, and condition are separate concepts:

- Rarity describes static scarcity or narrative significance for a definition.
- Quality describes craftsmanship or generated excellence for an instance.
- Condition describes the current physical state of an instance.

Example:

```text
Item: Ancient Royal Sword
Rarity: Unique
Quality: Masterwork
Condition: Damaged
```

These are not one universal item tier. A unique ceremonial object can be rare without being powerful, and a common item can still be masterwork quality.

## Static Versus Runtime Ownership

Static data belongs in ScriptableObject definitions:

- `RarityDefinition`
- `QualityDefinition`
- `ConditionDefinition`
- `ItemDefinition.Rarity`

Runtime or saveable instance data belongs outside shared assets:

- selected item quality;
- current normalized condition;
- future enchantments;
- future ownership;
- future custom names;
- future unique serial identity.

Runtime item state now uses the Step 9 authority split. `ItemInstanceIdentityRuntime` owns stable identity and general condition, `ItemQualityAffixRuntime` owns quality and affixes, `ItemDurabilityRuntime` owns durability, wear, and repair, and `DisassemblyRuntime` owns item-recovery history. Inventory/equipment entries carry item-instance IDs and do not store a second nested metadata model.

## Definition Architecture

All three new definition assets implement `IGameDefinition` and are registered in the shared `DefinitionCatalog`.

Rarity contains a stable ID, display text, rank, optional default marker, and optional presentation metadata. Quality contains a stable ID, display text, rank, and optional default marker. Condition contains a stable ID, display text, rank, explicit normalized range, unusable metadata, and optional default marker.

`IHasRarity` is the optional reuse point for definitions that can expose static rarity. `ItemDefinition` implements it now; spells, abilities, creatures, artifacts, quests, and discoveries can opt in later without changing `IGameDefinition`.

## Stable ID Conventions

Use globally unique, namespaced IDs:

- `rarity.common`
- `quality.masterwork`
- `condition.damaged`

Do not rename IDs after other assets or saves reference them. ID renames are migrations.

## Ranks And Ordering

Ranks provide deterministic ordering for tools and comparison APIs.

Higher rarity rank means more scarce or significant, not necessarily stronger. Higher quality rank means higher craftsmanship or generated excellence, but Step 3.4 does not apply stat or value multipliers. Higher condition rank means better physical state. Broken/unusable is metadata only in this feature.

## Condition Range Rules

Condition definitions divide normalized values from `0` to `1`.

The current authored prototype bands are:

- `condition.destroyed`: `0.00` to `0.01%`
- `condition.near-failure`: `0.01%` to `10%`
- `condition.severely-damaged`: `10%` to `25%`
- `condition.damaged`: `25%` to `50%`
- `condition.worn`: `50%` to `70%`
- `condition.used`: `70%` to `85%`
- `condition.good`: `85%` to `95%`
- `condition.pristine`: `95%` to `100%`

`condition.broken` is assigned dynamically by `ItemDurabilityRuntime` when one of the per-percentage break rolls succeeds; it is not a fixed numeric band.

Ranges are resolved as half-open intervals, except the final range includes `1.00`. This makes shared boundaries deterministic: `0.31` resolves to Worn, not Damaged.

Validation reports invalid IDs, duplicate global IDs, duplicate ranks, multiple defaults, reversed ranges, overlapping ranges, gaps in condition coverage, and missing catalog references. The validator reports problems only. It does not modify assets.

## Runtime APIs

`RarityQualityConditionUtility` provides rarity lookup, rarity rank comparison, quality rank comparison, condition resolution, unusable condition metadata checks, and definition-only stack compatibility for the current inventory model.

Null definitions are handled safely. Unknown or absent definitions do not crash comparison calls.

## Item Integration

`ItemDefinition` now has an optional static `RarityDefinition` reference.

Current prototype item assignments:

- Health Potion: `rarity.common`
- Prototype Sword: `rarity.common`
- Prototype Helmet: `rarity.common`
- Prototype Iron Ore: `rarity.common`

Missing item rarity is a warning, not an error, so existing content remains compatible.

Quality and current condition are not fields on `ItemDefinition`. They are per-instance concepts and must stay out of shared immutable item assets.

## Stacking Policy

Current inventory stacking is preserved:

- a slot stores one `ItemDefinition` reference and quantity;
- stacking is definition-reference based;
- quality and condition are not part of current inventory slots;
- pickups, rewards, loot, use, and equipment behavior are unchanged.

Future item instances with materially different runtime state should not share one indistinguishable stack. Different quality, condition, enchantments, ownership, custom names, or unique serial identity can eventually prevent stacking.

`CanShareDefinitionOnlyStack` documents the current baseline. Step 3.5 adds `ItemInstanceStackingPolicy` for future instance-aware compatibility, but neither policy is wired into `PlayerInventory` yet.

## Creating New Assets

Rarity assets use `Unity Isekai Game/Game Data/Rarity`, `rarity.` IDs, display text, rank, and optional presentation metadata.

Quality assets use `Unity Isekai Game/Game Data/Quality`, `quality.` IDs, display text, rank, and an optional default marker.

Condition assets use `Unity Isekai Game/Game Data/Condition`, `condition.` IDs, display text, rank, an explicit normalized range, and optional unusable/default markers.

Add every new asset to the active `DefinitionCatalog`, then run `Tools/Game Data/Validate Definitions`.

## Known Limitations

Step 3.4 does not implement durability loss, repairs, broken-equipment behavior, quality-based stat changes, rarity-based stat changes, rarity-based loot generation, procedural item generation, affixes, enchantments, ownership, custom names, crafting, vendor pricing, inventory instance migration, comparison UI, durability UI, save/load, or multiplayer.

## Future Integration

Feature 3.5 adds item-instance identity, an item-instance save-data shape, restoration through `DefinitionRegistry`, and instance-aware stack compatibility policy. Future work should wire those models into inventory/equipment only after save/load ownership is explicit.
