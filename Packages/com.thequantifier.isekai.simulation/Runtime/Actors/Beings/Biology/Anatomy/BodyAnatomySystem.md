# Body Structure, Anatomy, and Regions

Feature 7.2 adds the production structural anatomy model for exact Actor/body runtimes. It defines what structures exist, how they relate, and which structures are present, vital, targetable, corporeal, internal, mirrored, repeated, or future-compatible with equipment and localized damage.

## Ownership

```text
Person
-> Actor/body
-> ActorBodyRuntime
-> SpeciesDefinition
-> AnatomyDefinition
-> AnatomyRuntime
-> Regions / Parts / Organs / Internal Structures
```

The exact Actor/body owns the AnatomyRuntime. Person identity is recorded for ownership context, but the Person does not own body parts, regions, organs, structural presence, or anatomy revision. A replacement Actor/body gets a separate runtime because runtime node IDs include the exact body ID.

## Definitions

AnatomyDefinition is a canonical authored ScriptableObject registered in the definition catalog. It contains one structural node list rather than a large inheritance tree. Each AnatomyNodeDefinition has a stable node ID, category, parent node, region association, body side, presence state, optionality, vitality, targetability, corporeal/internal metadata, repeat/mirror groups, equipment tags, future damage tags, and ordering.

Canonical alpha anatomies:

- `anatomy.human`: humanoid root, head, torso, bilateral arms and legs, hands, feet, brain, heart, paired lungs, and an optional tail fixture.
- `anatomy.basic-construct`: chassis, sensor housing, left/right manipulators, locomotion base, and vital power core.
- `anatomy.basic-spirit`: incorporeal root, essence region, aura, and spiritual core.

Human and Undead Human Species use `anatomy.human`. Basic Construct and Basic Spirit use their own anatomy definitions.

## Runtime

AnatomyRuntime is body-owned and non-MonoBehaviour. It builds deterministic AnatomyRuntimeNode identities from:

```text
anatomy-node.{actorBodyId}.{anatomyDefinitionId}.{authoredNodeId}
```

Runtime build flow:

```text
Anatomy Definition
-> resolve stable nodes
-> build parent/child hierarchy
-> validate root and relationships
-> assign runtime IDs
-> create immutable Anatomy Snapshot
```

Read-only queries and snapshot creation do not mutate revision. Rebuilding a valid anatomy increments anatomy revision once and preserves deterministic runtime node IDs. Failed construction changes nothing in normal body assignment rollback.

## Readiness

Anatomy readiness uses:

- `Uninitialized`
- `ResolvingDefinition`
- `BuildingHierarchy`
- `ValidatingStructure`
- `Ready`
- `Restoring`
- `Invalid`
- `Disposed`

ActorBodyRuntime is Ready only after Species, body form, biological classification, and anatomy definition all resolve and the AnatomyRuntime validates its hierarchy.

## Presence

Structural presence supports Present, Absent, Optional, Suppressed, Inactive, and Unknown. Feature 7.2 only uses authored structural differences and Development/Test Lab presence overrides. Injury-driven absence, severing, and body-part destruction are deferred to Feature 7.3+.

Absent stable nodes remain queryable when authored into the definition. That keeps persistence and future historical references stable.

## Persistence

BodySaveData schema version 2 stores the body Species plus AnatomySaveData:

- exact Actor/body ID;
- Anatomy definition ID;
- anatomy revision;
- explicit presence overrides only.

The deterministic hierarchy itself is rebuilt from the AnatomyDefinition. Schema version 1 body saves are accepted by rebuilding default Species anatomy when no anatomy payload exists.

Restore uses the existing player body persistence participant:

1. prepare validates the body save payload;
2. prepare resolves the exact Actor/body and Species;
3. prepare confirms the Species has a compatible AnatomyDefinition;
4. commit restores Species silently;
5. commit rebuilds AnatomyRuntime from definition plus presence overrides;
6. anatomy revision is restored;
7. no gameplay anatomy events are replayed.

## Validation

Definition validation rejects missing IDs, duplicate node IDs, missing root, multiple roots, self-parenting, missing parents, invalid region references, cycles, vital nodes defaulting absent, internal structures without containers, negative target weights, and mirrored groups with duplicate side metadata.

Runtime validation rejects missing body identity, missing Species, missing AnatomyDefinition, incompatible body form, invalid root, missing parent nodes, invalid region references, and orphan nodes.

## Step 6 and Equipment Boundaries

Feature 7.2 does not localize damage, alter attack resolution, alter defeat, or attach health to parts. Step 6 systems may read targetable regions and anatomy readiness through query APIs later, but no attack damages a region or organ yet.

Equipment tags such as `equipment.head-compatible`, `equipment.hand-compatible`, `equipment.foot-compatible`, `equipment.manipulator-compatible`, and `equipment.incorporeal-incompatible` are metadata only. Equipment-slot enforcement remains deferred.

## Feature 7.3 Contract

Feature 7.3 should layer body condition, injuries, wounds, severing, impairment, and localized damage over stable AnatomyRuntime node IDs. It should not replace the anatomy identity model or add health fields to AnatomyDefinition.

## Multiplayer Authority

Future multiplayer persistence should be server-owned. The authoritative server owns anatomy construction, definition assignment, presence changes, runtime node IDs, hierarchy, revisions, and persistence. Clients may query and present anatomy but should not author final structural truth.

Networking, servers, authentication, and databases remain deferred.
