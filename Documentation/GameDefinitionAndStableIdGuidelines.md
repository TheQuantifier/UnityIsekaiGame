# Game Definition And Stable ID Guidelines

This document defines the Step 3.1 foundation for static game definitions, stable IDs, validation, and lookup.

## Static Definitions

A static game definition describes what something is. It is authored data, usually stored in a ScriptableObject asset.

Examples:

- `ItemDefinition`: item ID, display name, description, stack rules, use effects, equipment configuration.
- `SpellDefinition`: spell ID, display name, mana cost, cooldown, projectile configuration.
- `PersonDefinition`: person ID, display name, title, role metadata.
- `QuestDefinition`: quest ID, title, stages, prerequisites, reward.
- `ContractDefinition`: contract ID, title, objectives, reward.

Runtime state describes the current state of a particular instance.

Examples:

- `InventorySlot`: item definition reference plus current quantity.
- `QuestInstance`: quest definition reference plus current stage, objective state, and reward state.
- `ContractInstance`: contract definition reference plus objective progress and reward state.
- `PersonIdentity`: currently loaded scene presence for a static `PersonDefinition`.
- Future faction state: faction definition reference plus mutable leadership, memberships, ranks, reputation, law, diplomacy, and owned resources.

ScriptableObject definitions must not store mutable runtime gameplay state.

## Shared Contract

Step 3.1 uses a small interface:

```csharp
public interface IGameDefinition
{
    string Id { get; }
    string DisplayName { get; }
}
```

No shared ScriptableObject base class was introduced. Existing definition assets use different serialized field names such as `itemId`, `personId`, `questId`, and `contractId`. A base class would require field migration or duplicate serialized identity fields. The interface provides shared lookup behavior without changing asset inheritance or serialized data layout.

Definitions migrated now:

- `ItemDefinition`
- `SpellDefinition`
- `PersonDefinition`
- `QuestDefinition`
- `ContractDefinition`

Definitions deliberately deferred:

- `DialogueNodeDefinition`, because dialogue nodes currently have no stable ID and are graph nodes rather than globally registered world definitions.
- `ContractObjectiveDefinition` subclasses, because they are reusable objective configuration assets but not yet independent world definitions.
- `ContractRewardDefinition` and `QuestStageDefinition`, because they are embedded serializable configuration rather than independent ScriptableObject definitions.

## Stable ID Format

Definition IDs use this accepted format:

- lowercase letters;
- digits;
- periods;
- hyphens;
- underscores;
- no whitespace;
- no leading or trailing whitespace;
- no leading or trailing separator;
- no repeated separator of the same character.

Examples for new content:

- `item.health-potion`
- `spell.arcane-bolt`
- `ability.arcane-bolt`
- `effect.restore-health`
- `status.prototype-might`
- `stat.attack-power`
- `being.person`
- `actor-profile.player-prototype`
- `place.settlement.prototype-town`
- `place.poi.disturbance-site`
- `person.prototype-npc`
- `faction.guild.adventurers`
- `faction.kingdom.prototype`
- `quest.prototype-strange-disturbance`
- `contract.prototype-enemy-elimination`

Feature 4.9 canonicalized first-party prototype definition IDs to this preferred format. Obsolete pre-alpha IDs such as `health_potion`, `prototype_npc`, `prototype_strange_disturbance`, and `status.beneficial` are not retained as silent aliases.

## ID Immutability

Stable IDs must:

- remain unchanged after saves or other assets reference them;
- not depend on display names;
- not depend on GameObject names;
- not depend on scene paths;
- not be regenerated during ordinary imports;
- be unique in configured definition catalogs.

Renaming an ID is a migration, not a refactor. Future save data should store stable IDs and resolve definitions through catalogs during load.

## Uniqueness Rule

The selected rule is globally unique IDs inside a definition catalog. New IDs should be namespaced by domain, such as `item.` or `quest.`, so generic future systems can resolve them without carrying a separate type key.

The current validator still allows non-namespaced IDs with a warning for external or transitional content. First-party prototype content should not rely on that warning path.

## Catalog And Lookup

Static definition lookup is explicit and asset-driven:

- `DefinitionCatalog` is a ScriptableObject containing authored definition asset references.
- `DefinitionCatalogValidator` validates missing references, incompatible types, invalid IDs, duplicate asset references, and duplicate IDs.
- `DefinitionRegistry` is initialized from a catalog or known definitions and supports safe lookup by ID and by expected definition type.

The lookup system does not use scene searches, `GameObject.Find`, `Resources.FindObjectsOfTypeAll`, or `AssetDatabase` in runtime code.

The prototype catalog lives at:

- `Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset`

It includes representative top-level prototype definitions for items, spells, the prototype NPC, the staged quest, and prototype contracts.

Step 3.2 also registers category and tag definitions in the same catalog. Categories and tags are ordinary static definitions with stable IDs, but classification data is exposed through optional interfaces rather than by expanding `IGameDefinition`.

Step 3.3 adds object and item taxonomy interfaces on top of the same catalog. Item definitions remain registered static definitions, while inventory quantities, equipped state, and future per-instance item state remain runtime data.

Step 3.4 adds `RarityDefinition`, `QualityDefinition`, and `ConditionDefinition` as ordinary catalog definitions. Rarity may be referenced by static definitions through `IHasRarity`; quality and condition are available for future runtime instance metadata and should not be stored as mutable state on shared ScriptableObject assets.

Step 3.7 adds `AbilityDefinition` and reusable `EffectDefinition` assets. They are catalog definitions with stable IDs, but runtime cooldowns, resource values, selected targets, and active projectile state are not catalog data.

Step 3.8 adds `StatusEffectDefinition` assets and stable stat IDs. Status definitions are catalog data; active status applications, remaining duration, stack count, and modifier source IDs are runtime state.

`PersonRegistry` remains separate. It tracks currently loaded `PersonIdentity` scene instances, while the definition catalog tracks static `PersonDefinition` assets.

## Creating A New Definition Type

1. Decide whether the asset is an independently referenceable static definition.
2. Add stable ID and display name fields using names that fit the domain.
3. Implement `IGameDefinition` by mapping `Id` and `DisplayName` to those fields.
4. Keep mutable runtime state in runtime classes or MonoBehaviours, not in the ScriptableObject.
5. Add the asset to an explicit `DefinitionCatalog` when it should be globally resolvable.
6. Run `Tools/Game Data/Validate Definitions`.

Do not add rarity, faction, prefab, icon, value, or other domain-specific fields to `IGameDefinition`. Category, tag, and rarity metadata should use optional interfaces such as `ICategorizableDefinition`, `ITaggedDefinition`, and `IHasRarity`.

## Creating A New Definition Asset

1. Create the asset through its `CreateAssetMenu`.
2. Assign a stable ID before referencing it elsewhere.
3. Prefer the domain-prefixed format, such as `item.new-example`.
4. Assign a display name for UI.
5. Add the asset to the appropriate catalog.
6. Validate definitions before committing.

## Safe Migration Rules

- Do not rename existing serialized ID fields unless necessary.
- Use `FormerlySerializedAs` if a serialized field must be renamed.
- Do not regenerate IDs.
- Do not change IDs only to match the preferred new format unless the change is handled as an explicit content/save compatibility event.
- Treat ID renames as content migrations that require reference and save compatibility review.
- Keep old string fallback fields until serialized assets no longer need them.

## Feature 4.9 Prototype ID Compatibility Break

Feature 4.9 intentionally invalidates old local development saves that store obsolete prototype definition IDs instead of carrying permanent aliases. The project is still pre-alpha, and those saves should be deleted before final manual testing.

Renamed first-party prototype IDs include:

- `health_potion` -> `item.health-potion`
- `prototype_sword` -> `item.prototype-sword`
- `prototype_helmet` -> `item.prototype-helmet`
- `prototype_npc` -> `person.prototype-npc`
- `prototype_strange_disturbance` -> `quest.prototype-strange-disturbance`
- `prototype_enemy_elimination` -> `contract.prototype-enemy-elimination`
- `prototype_potion_collection` -> `contract.prototype-potion-collection`
- `prototype_potion_delivery` -> `contract.prototype-potion-delivery`
- `status.beneficial` -> `category.status.beneficial`
- `status.harmful` -> `category.status.harmful`
- `status.neutral` -> `category.status.neutral`
- `being-category` -> `category.being`
- `place-category` -> `category.place`
- `faction-category` -> `category.faction`
- `damage-category` -> `category.damage`

## Future Save And Load

Save data should store stable definition IDs, not whole ScriptableObject assets.

Future load flow:

1. Read a saved stable ID.
2. Resolve the static definition through the configured catalog/registry.
3. Reconstruct mutable runtime state separately.
4. Report missing IDs clearly instead of silently substituting unrelated content.

## Future Content Versioning

Catalogs currently expose a content version string only as metadata. Future features may add deterministic ordering, catalog hashes, addressable content, mod packages, server-authoritative catalogs, or multiplayer compatibility checks.

Those systems are not implemented in Step 3.1.

## Known Limitations

- `SpellDefinition` gained an additive `spellId` field; existing prototype spell assets were assigned IDs.
- There is no global project catalog bootstrap yet. Runtime systems should receive or create registries explicitly from configured catalogs.
- Dialogue nodes, objective definitions, rewards, places, species, factions, and full stat-definition assets are outside this feature.
- Rarity, quality, and condition exist as definition foundations only. They do not yet drive loot probability, item stats, repairs, economy, save/load, or inventory-instance behavior.
