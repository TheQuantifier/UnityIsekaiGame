# Body Species Biology System

Feature 7.1 introduced the production foundation for embodied biological identity. It intentionally stopped at identity, classification, body form, Species assignment, snapshots, and persistence. Feature 7.2 now layers body-owned Anatomy definitions and runtime structure on top of Species. Injuries, hunger, thirst, transformation, replacement bodies, equipment-slot derivation, and playable content rules remain deferred.

## Identity Boundaries

- `Person` is persistent character identity.
- `Actor/body` is the exact embodied runtime identity that owns one current body state.
- `SpeciesDefinition` is the authored biological kind, such as `species.human`.
- `BiologicalClassificationDefinition` is the broad biological family, such as living, undead, construct, or spirit.
- `BodyFormDefinition` is the structural form, such as humanoid, construct, or incorporeal.
- `AnatomyDefinition` is the exact structural blueprint for regions, parts, organs, and internal structures.

Body state belongs to the exact Actor/body runtime. It is not owned by Person identity alone and must not be silently copied to a replacement body.

## Capability Keys

`CapabilityDefinition` assets use canonical definition IDs such as `capability.biology.is-living` and `capability.can.die`.

Runtime capability lookups may intentionally use narrower gameplay keys when an existing subsystem owns that contract. Actor lifecycle defeat policies use `can.*` and `immunity.*` keys, such as `can.die`, because `DefeatPolicyDefinition` validates those as lifecycle runtime keys. Biological capability grants therefore keep three separate values:

- the referenced canonical `CapabilityDefinition`;
- an optional runtime capability key, such as `can.die`;
- a contribution `entryId` used for deterministic source identity.

Do not collapse those fields unless the lifecycle runtime contract is migrated at the same time.

## Runtime Ownership

`ActorBodyRuntime` is the body-state component. It resolves Species through `DefinitionRegistry`, applies deterministic Species and classification sources to `CharacterTraitCollection` and `CalculatedStatCollection`, and exposes immutable `BodySnapshot` objects for UI, combat, persistence, and development tooling.

Species assignment also resolves the Species AnatomyDefinition and rebuilds the body-owned AnatomyRuntime. Anatomy node identity is exact-body-owned and deterministic from the Actor/body ID, Anatomy definition ID, and authored structural node ID.

Preview assignment resolves the requested Species without mutating traits, stats, capabilities, body revision, persistence state, or events. Execution revalidates and commits the Species assignment once.

Subsystem rebuilds may recreate trait/stat collections. When a body already has a Species, `ActorBodyRuntime.Configure` silently reapplies deterministic biological sources without changing body identity or incrementing body revision.

## Persistence

`PlayerBodyPersistenceParticipant` stores:

- schema version;
- exact Actor/body ID;
- optional Person ID;
- Species definition ID;
- body revision;
- anatomy definition ID, anatomy revision, and explicit anatomy presence overrides.

Restore validates the saved Actor/body ID, optional Person ID, and Species definition ID before committing. Trait, capability, and stat grants from Species/classification are deterministic and are not saved as separate permanent trait state.

## Canonical Alpha Content

Canonical 7.1 content lives outside Prototype-only folders under `Packages/com.thequantifier.isekai.content/Content/Actors/Beings/` and is registered by the prototype catalog for current testing.

Required alpha definitions:

- classifications: `biology.classification.living`, `biology.classification.undead`, `biology.classification.construct`, `biology.classification.spirit`;
- body forms: `body-form.humanoid`, `body-form.construct`, `body-form.incorporeal`;
- species: `species.human`, `species.undead-human`, `species.basic-construct`, `species.basic-spirit`.

## Test Lab

The unified Tab menu exposes `Body Species 7.1` under Test Lab. It can preview Species assignment, assign Human, Undead Human, Basic Construct, and Basic Spirit, validate current body integrity, run missing-species and stale-actor checks, and save/load to verify persistence.

Automation suite `feature.7.1.body-species` covers player body snapshot resolution, Human capability grants, preview no-mutation behavior, alternate Species assignment, missing Species rejection, and save/load restoration.
