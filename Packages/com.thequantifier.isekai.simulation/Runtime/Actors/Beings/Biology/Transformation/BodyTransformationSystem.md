# Body Transformation System

Feature 7.8 introduces the production foundation for transformation, body replacement, and Species change. It defines the request, compatibility, planning, execution, rollback, reversion, persistence, and Test Lab contracts without implementing final spell content, VFX, possession AI, soul combat, resurrection gameplay, networking, or visual body swapping.

## Identity Boundaries

- `Person` is persistent character identity and is not destroyed by a transformation.
- `Actor/body` is the exact embodied runtime identity that owns current body state.
- `SpeciesDefinition` and `BodyFormDefinition` describe the biological target.
- Anatomy, body condition, vital processes, hazards, and recovery are body-owned state.
- Controller, mind, soul, and consciousness are modeled as separate transfer decisions, but concrete handoff remains deferred to future owning systems.

Body-owned state is never silently transferred to a replacement body. Plans explicitly describe what would be preserved, rebuilt, cleared, remapped, or reassigned.

## Definitions

`TransformationMethodDefinition` is the authored high-level method, such as temporary polymorph, permanent Species change, body replacement, body swap, possession, reincarnation, spirit embodiment, or structural replacement. Each method references a `BiologicalInteractionDefinition` so Feature 7.6 compatibility remains the gate for transformation eligibility.

`TransformationProfileDefinition` enables methods for Species and body forms. Profiles are deterministic and catalog-owned. The alpha catalog provides profiles for the default body set, humans, constructs, and spirits.

## Runtime Flow

The authoritative high-level entry point is `BodyTransformationRuntime`.

1. A caller builds an immutable `BodyTransformationRequest`.
2. Preview builds a `BodyTransformationPlan` using the same eligibility logic as execution.
3. Preview does not mutate body state, transformation state, revisions, events, or persistence flags.
4. Execution revalidates the request and compatibility state.
5. Execution captures rollback state.
6. Execution commits once through the existing `ActorBodyRuntime` body operations where mutation is supported.
7. If execution fails, body and transformation state roll back atomically.
8. Duplicate transaction IDs return the prior committed result without a second mutation.

## Temporary Reversion

Temporary transformations capture a compact reversion body state. Revert restores the captured Species/body-owned state silently through existing body restore behavior. Duplicate revert transactions are idempotent.

## Persistence

`BodySaveData` schema version 7 includes `BodyTransformationSaveData`. Restore reconstructs transformation state without replaying transformation events or execution side effects. Active temporary transformations preserve their reversion state.

## Deferred Contracts

Feature 7.8 records body replacement, body swap, possession, reincarnation, resurrection body, and embodiment as eligible plans where supported. Concrete Person/body reassociation, controller transfer, soul/mind rules, equipment reattachment, multiplayer authority, and authored gameplay presentation remain future work.

Feature 7.9 should build on:

- `BodyTransformationRequest`
- `BodyTransformationPlan`
- `BodyTransformationResult`
- `TransformationMethodDefinition`
- `TransformationProfileDefinition`
- `BodyTransformationRuntime`

Future systems should consume the plan decisions instead of inferring transfer behavior from method IDs.
