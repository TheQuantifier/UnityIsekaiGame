# Item Quality and Affixes

Feature 9.3 adds item quality, workmanship, defects, rarity scoring, and affix state as a layer above item identity and item composition.

## Ownership

`ItemInstanceIdentityRuntime` owns instance identity, ownership, location, and lifecycle state.

`ItemCompositionRuntime` owns material makeup, component structure, hidden components, purity, and component relationships.

`ItemQualityAffixRuntime` owns per-instance workmanship, quality dimensions, defects, affix instances, generated roll values, derived rarity classification, and quality/affix projections.

The quality runtime references item identity and composition for validation, but it does not create raw item identities or rewrite material composition directly. Use `ItemQualityAffixCoordinator` when a caller needs atomic creation across identity, composition, quality, and affixes.

`IItemQualityAffixRuntimeProvider` is the shared integration contract for gameplay hosts that expose item identity, composition, quality/affix runtime state, and the definition registry together. The prototype persistence service implements this provider so pickups, equipment, save/load, and Test Lab flows resolve the same runtime set instead of silently using different registries or isolated state.

## Quality Model

Quality is recorded per item instance as an `ItemQualityRecordData`.

Important fields:

- `qualityRecordId`: stable contribution identity for the quality record.
- `itemInstanceId`: the item instance being described.
- `itemDefinitionId`: definition identity captured for validation and projection.
- `qualityTierId`: authored tier such as common, fine, or masterwork.
- `workmanship`: known, unknown, or not-applicable workmanship dimensions.
- `dimensions`: structural, functional, magical, and other quality dimensions.
- `defects`: visible or hidden defect records.
- `rarityDefinitionId`: derived rarity classification, kept separate from quality and value.
- `source`: authored, crafted, generated, repaired, degraded, appraised, migrated, or debug source.

Unknown quality and not-applicable quality are distinct states. Unknown means the runtime has not established the value. Not-applicable means the dimension deliberately does not apply to the item.

## Affix Model

Affixes are authored as `ItemAffixDefinition` assets and applied as per-instance `ItemAffixInstanceData` records.

Affix definitions include:

- eligible item definitions, categories, tags, and material tags;
- required and forbidden quality tiers;
- allowed quality score range;
- affix classification such as prefix, suffix, innate, defect, or hidden;
- conflict groups and max occurrence rules;
- deterministic generation weight;
- stat modifier templates.

Affix instances include:

- stable affix instance ID;
- item instance ID;
- affix definition ID;
- tier;
- source;
- roll seed;
- rolled values;
- active/hidden/discovered state;
- source references.

Generation is deterministic by seed and sorted candidate order. Preview generation uses a cloned runtime and does not mutate live state. Executing the same item, policy, seed, and source combination more than once is treated as a duplicate generation request and does not continue rolling additional affixes.

Creation through `ItemQualityAffixCoordinator` requires either an explicit item instance ID or a deterministic item seed. The coordinator derives the same instance ID for preview and execution, so previewed quality, composition, and affixes describe the item that execution will actually create.

## Stat Modifiers

Affix stat effects are projected into `RuntimeStatCollection` through `ApplyActiveAffixModifiers`.

Modifier source IDs are stable per affix instance:

`item-affix.{affixInstanceId}`

This allows the stat runtime to reject duplicate contribution attempts and keeps affix effects reversible by source. Feature 9.3 does not implement durability, crafting progression, loot generation, economy pricing, or final UI.

`PlayerStats` now applies active affix stat modifiers through the real equipment recalculation path. Equipment modifiers and affix modifiers are tracked by source and removed together before recalculation, which keeps equip, unequip, load, and affix enable/disable flows idempotent. Runtime affix state changes raise `ItemAffixStateChanged`; equipped items recalculate automatically when one of their affixes changes.

The stat receiver overload prepares all affix modifiers before committing. If any definition, tier, modifier template, or receiver add fails, previously applied sources from that operation are rolled back and the operation reports `AtomicCommitFailed`.

## Access Projections

Quality and affix records expose Step 8 information subjects through `ItemQualityAffixInformationSubject`.

Access-aware projection rules:

- denied decisions return no raw snapshot;
- redacted decisions hide protected fields such as hidden defects, hidden affixes, rolled values, source/provenance details, modifier source, and revision history;
- when redacted projections contain hidden affixes, hidden-affix-derived rarity diagnostics are suppressed so public projections do not leak hidden modifiers through rarity score, policy, or derived rarity ID;
- full access returns immutable snapshots;
- projection does not mutate appraisal, discovery, knowledge, or item state.

## Persistence

`ItemQualityAffixPersistenceParticipant` saves and restores only quality/affix state. Prepare validates:

- schema version;
- duplicate quality and affix IDs;
- every quality/affix item instance exists;
- referenced item definitions, tiers, affix definitions, and rarity definitions exist when a registry is supplied;
- active affixes remain eligible for the restored item/composition state.

Commit restores from prepared data only after validation. If validation fails, live runtime state remains unchanged.

When a current item has no quality record, `EnsureDefaultQuality` performs idempotent default initialization. It does not invent affixes or change item identity.

Participant ordering is explicit:

1. item instance identity;
2. item composition;
3. item quality and affixes;
4. player inventory/equipment projection.

The player inventory/equipment participant declares quality and composition as optional dependencies so old saves can still load, while new saves restore item state before inventory and equipment projections consume it.

Removed affixes are preserved as inactive tombstones rather than deleted from runtime state. They no longer apply stat modifiers, no longer count as active affixes, and do not reappear after save/restore, but their revision history remains available to privileged/internal projections.

Authored rarity overrides are separate from derived rarity. Derived rarity can be recalculated from quality and active affixes without erasing an explicit override.

## Scene-Authored Pickups

`WorldItemQualityAffixPreset` can be placed on a scene pickup to initialize a stable scene-authored item instance with quality and affix data before it enters inventory. It:

- resolves a stable scene item instance ID from the serialized ID or a deterministic scene path;
- creates the item identity as a `WorldFixture` if it does not already exist;
- applies an authored quality record or ensures default quality;
- applies authored affixes idempotently;
- can generate deterministic affixes from a seed;
- hands the exact prepared item instance to inventory when the pickup is collected.

Preparation captures identity and quality runtime rollback snapshots before mutating state. If any definition, quality, affix, or generation step fails, the preset restores both runtimes so a failed pickup cannot leave a half-created identity or partial quality record behind.

Use this for prototype items that should keep their specific quality, defects, affixes, rarity override, or generated rolls instead of becoming a fresh generic instance at pickup time.

## Test Lab

The default Step 9 automation order is:

1. `feature.9.1.item-identity-instance-state`
2. `feature.9.2.materials-item-composition`
3. `feature.9.3.item-quality-affixes`

The 9.3 suite covers:

- default and authored quality;
- unknown and not-applicable dimensions;
- hidden defect redaction;
- deterministic affix generation and duplicate prevention;
- conflict groups and stackable affixes;
- stat modifier projection;
- save/restore and default-quality initialization.
