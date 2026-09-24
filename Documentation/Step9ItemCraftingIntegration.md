# Step 9 Item and Crafting Integration

Step 9 finalizes the item, crafting, and production stack as one integrated graph. The integration layer does not own item records. It validates that each owning runtime keeps a single authority boundary, preserves deterministic save data, and exposes enough diagnostics for Test Lab, persistence, command automation, and future UI projections.

## Authority Map

Authoritative ownership remains split by responsibility:

- `ItemInstanceIdentityRuntime` owns stable item instance IDs, lifecycle, location, ownership, custody, world representation, and Step 8 item subject identity.
- `ItemCompositionRuntime` owns material and component structure for item instances.
- `ItemQualityAffixRuntime` owns workmanship, quality records, affixes, defects, rarity derivation, and affix stat contribution source IDs.
- `ItemDurabilityRuntime` owns wear, damage, breakage, repair, and durability-derived equipment availability.
- `DisassemblyRuntime` owns item-recovery operation history and persisted schedules for workstation disassembly, natural decomposition, and field salvage pickups.
- `ProductionRequirementRuntime` owns tool, station, resource, material, skill, knowledge, access, and reservation plans.
- `RecipeKnowledgeRuntime` owns person-facing recipe knowledge projections.
- `CraftingExecutionRuntime` owns crafting operation history, input consumption, tool use, output creation records, and rollback boundaries.
- `ProductionWorkflowRuntime` owns work orders, jobs, queues, batches, lots, intermediates, occupancy, assignments, and production events.
- `ExperimentationRuntime` owns hypotheses, experiment plans, trials, measurements, inferences, claims, reviews, and registration proposals.

Consumers may read projections from these runtimes, but they should not mutate another runtime's authority directly. Cross-runtime mutations must happen through explicit operation APIs with rollback or preview semantics.

## Integration Validator

`Step9IntegrationValidator` validates cloned save snapshots. It is intentionally read-only and can run from:

- Edit Mode tests.
- Test Lab in-game automation.
- Command-line automation.
- Persistence prepare/failure diagnostics.
- Future development tools.

It currently checks:

- unique authority domains;
- acyclic persistence dependencies;
- save schema versions;
- item identity stable IDs and definition references;
- exclusive location conflicts;
- lifecycle/location compatibility;
- tracked item stack violations;
- composition ownership and tracked component references;
- quality, affix, and durability item references;
- active affixes on terminal item instances;
- item-recovery-closed durability state against active item identity;
- production reservation conflicts;
- crafting, production workflow, recipe knowledge, and experimentation references;
- deterministic canonical fingerprints.

The validator reports structured diagnostics instead of string-only failures so callers can route errors by domain, code, subject, and severity.

## Persistence Order

Step 9 persistence dependencies are ordered by owning runtime:

1. Item identity.
2. Item composition.
3. Quality and affixes.
4. Durability.
5. Item recovery.
6. Production requirements.
7. Recipe knowledge.
8. Crafting execution.
9. Production workflow.
10. Experimentation and discovery.

This order ensures downstream systems can validate references against upstream ownership without replaying gameplay operations during restore.

## Determinism

The canonical fingerprint sorts stable IDs and serializes only authoritative graph fields. It is order-independent for equivalent save snapshots and intentionally excludes transient object references, Unity instance IDs, frame time, and scene-only object identity.

Use the fingerprint to compare:

- preview versus execution boundaries;
- save and restore results;
- snapshot restore cleanliness;
- command automation versus in-game automation;
- fixture baseline drift.

## Future Extension Rules

When adding a Step 9 subsystem:

1. Add an authority entry if it owns new records.
2. Add a persistence dependency entry if it saves cross-runtime references.
3. Include its save data in `Step9IntegrationRuntimeSnapshot`.
4. Add stable IDs to the canonical fingerprint.
5. Validate foreign references against the owning runtime, not against local copies.
6. Keep validation read-only.
7. Add both Edit Mode and Test Lab coverage.

Do not bypass validation to make a save load. Missing definitions, unsupported schema versions, stale ownership, duplicate locations, and partial graph references should fail before commit.
