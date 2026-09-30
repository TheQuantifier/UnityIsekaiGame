# Biological Compatibility System

Feature 7.6 adds a shared biological interaction layer owned by the body runtime. It answers whether a body can be affected by a biological interaction and how strongly that interaction should apply. It does not replace species, anatomy, condition, vital process, hazard, or combat systems; it gives them a shared compatibility query before they mutate state.

## Core Model

- `BiologicalInteractionDefinition` is the catalog definition for an interaction such as bleeding, fracture, biological healing, construct repair, poison, disease, polymorph, holy energy, or necrotic energy.
- `BiologicalCompatibilityProfileDefinition` is an authored rule set for a species, body form, or biological classification.
- `BiologicalCompatibilityRuntime` is the body-owned runtime cache and dynamic contribution store.
- `BiologicalInteractionEvaluator` is the stateless read-only evaluator used for preview and execution decisions.
- `BiologicalCompatibilitySnapshot` exposes immutable state for UI, tests, and future persistence diagnostics.

## Rule Types

- Compatibility overrides mark an interaction as compatible or incompatible for a body.
- Immunity is semantic immunity. It blocks the interaction as an immunity result, not just as a zero multiplier.
- Resistance multiplies rate, severity, and consequence downward.
- Vulnerability multiplies rate, severity, and consequence upward.
- Affinity marks a beneficial or aligned interaction and may increase positive effects.
- Suppression pauses or scales an interaction without claiming intrinsic immunity.
- Conversion maps one interaction to another future interaction contract.
- Absorption marks an interaction as a special beneficial handling path.

## Identity and Ownership

Compatibility rules keep identity separated by purpose:

- `BiologicalInteractionDefinition.Id` is the canonical catalog identity, such as `interaction.biology.bleeding`.
- Runtime capability keys remain the keys consumed by existing runtime systems, such as `can.die` or `immunity.defeat`.
- Rule `entryId` identifies the contribution itself and is used for source-safe replacement and removal.
- Dynamic rule ownership is source-safe: removing one source contribution does not remove another source that targets the same interaction.

## Evaluation Order

Evaluation is deterministic:

1. Body-derived implicit rules run first, such as no blood blocking bleeding.
2. Matching profile rules and dynamic runtime contributions are merged into one ordered stream.
3. Rules are ordered by priority, broad category rules, specific interaction rules, source kind, source ID, and entry ID.

At the same priority, category-level rules act as broad defaults and specific interaction rules apply after them. A specific rule can therefore restore or override an interaction after a broad category rule has set a default. If two rules still tie, source kind, source ID, and entry ID provide a deterministic stable order.

Multipliers are combined by multiplication. Authored runtime multipliers are finite, non-negative values capped at `10`. Invalid values are sanitized at the runtime boundary. Maximum severity limits use a separate finite ceiling capped at `999`, so safety clamping does not accidentally turn severity ceilings into small multipliers. Minimum floors and maximum severity limits are applied after matching rules are processed. Compatibility, immunity, suppression, affinity, conversion, and absorption remain separately visible in the result instead of being collapsed into one number.

Conversion and absorption are explicit contracts. Conversion rules must point to a registered converted interaction and may not convert an interaction to itself. Authored profile validation rejects unsafe conversion chains. Absorption rules must describe their alpha outcome in the rule explanation until later systems add richer outcome metadata.

## Existing System Integration

Hazard source application, hazard synchronization, hazard preview, and hazard execution require a current compatibility runtime and body snapshot. Missing compatibility context fails closed. Stale body or anatomy context is rejected instead of falling back to lower-level hazard behavior. Inactive biological resources still report their own inactive-resource result first so designers can tell the difference between "this body has no active resource to affect" and "this interaction is biologically incompatible."

Localized body-condition damage requires a current compatibility runtime and body snapshot. Preview and execution both evaluate compatibility before structural damage is accepted. Missing context, stale context, missing interaction mapping, incompatible bodies, immunity, and suppression fail before mutation.

Actor bodies build compatibility after species, anatomy, condition, vital processes, and hazards are configured or restored. Compatibility snapshots include body, anatomy, condition, vital, hazard, and compatibility revisions. Evaluation rejects body or anatomy snapshots that no longer match the runtime body, which forces replacement bodies and rebuilt anatomy to be evaluated through the current compatibility runtime.

## Persistence

Feature 7.6 keeps compatibility runtime state rebuildable from authored definitions and current body state. Dynamic compatibility contributions are source-owned runtime state. The owning system is responsible for reapplying or removing its source contribution; the compatibility runtime does not persist development/Test Lab rules. Restore rebuilds compatibility silently as part of body restore and does not emit gameplay events from compatibility itself.

Dynamic rule changes increment compatibility revision and invalidate old evaluation assumptions. Preview callers should use a fresh body snapshot for each evaluation. Execution callers must re-evaluate with the current runtime and body snapshot instead of reusing preview results.

## Future Contracts

Feature 7.7 recovery and repair must query compatibility before applying biological healing, regeneration, construct repair, spirit restoration, holy healing, or necrotic restoration.

Feature 7.8 transformation must query compatibility before polymorph, species change, possession, body replacement, or reincarnation.

Feature 7.9 disease and toxicology must query compatibility before disease, infection, parasite, poison, venom, toxin, alcohol, and intoxication processing.

These future features should use the same evaluator and should add source-owned runtime contributions rather than adding parallel compatibility systems.

## Prototype Test Lab

The Test Lab has a `Biological Compatibility 7.6` section under Step 7. It can reset to Human, validate readiness, evaluate canonical interactions, add and remove development rules, prove deterministic ordering, prove category-versus-specific precedence, prove missing/stale context rejection, prove dynamic reset behavior, and prove snapshots are read-only.
