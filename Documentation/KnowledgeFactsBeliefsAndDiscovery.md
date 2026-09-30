# Knowledge, Facts, Beliefs, and Discovery

Feature 8.1 introduces the production foundation for Person-owned knowledge. It does not duplicate authoritative simulation state. Step 7 body, species, anatomy, injury, hazard, compatibility, transformation, and biological-condition runtimes remain the source of truth.

## Core Model

```text
Authoritative Simulation Truth
-> Observable Projection
-> Observation
-> Evidence
-> Belief Revision
-> Person Knowledge
```

Knowledge belongs to an exact Person:

```text
Person
-> PersonKnowledgeRuntime
-> Beliefs / Evidence / Discoveries

Current Body
-> observable sensations and evidence only
```

A body can produce evidence, but the body does not own general personal knowledge. Replacing or transforming a body may make body-specific beliefs stale, but it must not delete Person-owned beliefs or memories.

## Fact Definitions

`KnowledgeFactDefinition` is the canonical authored description of a possible proposition. Current built-in facts include:

- `fact.species.identity`
- `fact.species.capability`
- `fact.body.injury`
- `fact.body.symptom`
- `fact.body.condition`
- `fact.body.transformation`
- `fact.body.previous-body`
- `fact.body.replacement`
- `fact.compatibility.resistance`
- `fact.person.identity`
- `fact.event.occurred`

Each Fact definition declares a domain, proposition type, subject type, value type, visibility, certainty threshold, contradiction policy, staleness policy, forgetting policy, and shareability.

Canonical Feature 8.1 Fact definitions are authored content assets under:

```text
Packages/com.thequantifier.isekai.content/Content/Knowledge/FactDefinitions/
```

They are registered through `DefinitionCatalog` and resolved by `DefinitionRegistry`. Production runtime code does not synthesize missing canonical Fact definitions; missing facts fail clearly during observation, validation, or restore.

## Propositions

`KnowledgePropositionData` represents a typed claim:

```text
subject + factDefinitionId + object/value + context + negation + revision
```

The deterministic proposition identity is not display text. It is built from stable IDs and typed values so two equivalent claims resolve to the same identity even if runtime ordering changes.

## Evidence And Provenance

Evidence is stored as `KnowledgeEvidenceRecordData`. It has:

- evidence ID;
- observer Person ID;
- proposition;
- support/opposition/correction direction;
- strength;
- credibility;
- provenance;
- acquisition source;
- game-time timestamp;
- body, location, source, and event context;
- visibility.

Supported provenance includes direct observation, self sensation, examination, testimony, document, inference, memory, skill knowledge, species knowledge, cultural knowledge, magical-detection foundation, scripted discovery, development fixture, and authoritative correction.

## Beliefs

Beliefs are Person-owned records derived from evidence. Confidence is an integer in `[0, 1000]`. Belief state is derived deterministically from confidence, evidence, freshness, dispute, forgetting, and truth-comparison state.

Important distinctions:

- high confidence does not imply objective truth;
- a Person can confidently hold a misconception;
- a stale belief remains queryable;
- forgotten knowledge is distinct from never-known information;
- testimony creates listener-owned evidence and does not copy the speaker's belief instance.

## Revision And Dirty State

Queries, snapshots, validation, previews, duplicate transactions, and restore prepare do not mutate Knowledge revision. Successful committed evidence or belief changes increment revision once.

The prototype persistence service marks saves dirty when committed Knowledge changes occur. Preview, duplicate, and silent restore paths do not mark dirty.

## Step 7 Integration

`KnowledgeObservationProjection` is the explicit boundary from Step 7 truth to Knowledge evidence. It creates limited observation requests such as:

- visible Species appearance;
- visible injury;
- self symptom;
- previous-body historical memory.

Ordinary observation does not expose full Step 7 snapshots, internal body IDs as hidden truth, exact disease definitions, compatibility multipliers, private transformation state, hidden traits, hidden capabilities, or exact internal resource values.

Development truth comparison is separate and requires a `KnowledgeTruthAuthorization`. Ordinary callers cannot set truth authorization with a public Boolean. Trusted runtime projection code uses an internal authorization path, and development/test fixtures use an Editor/development-only fixture factory.

## Persistence

`PersonKnowledgePersistenceParticipant` persists Person-owned Knowledge under participant key `person.knowledge`.

The participant serializes a `PersonKnowledgeSaveData` record keyed by `personId`; it is not tied to a Player component, current controlled body, or local-player object. The prototype currently registers the local player's Person Knowledge through the player save infrastructure because the prototype save envelope is still local-player scoped. NPC, companion, enemy, merchant, ruler, witness, remote, and offline Person Knowledge can use the same participant shape later with different owner/wiring.

Feature 8.1 changes the pre-alpha development save participant key from the earlier unmerged `player.knowledge` draft to `person.knowledge`; old development saves using the draft key should be considered invalidated rather than migrated.

Restore flow:

1. Person identity is already restored.
2. Knowledge definitions are resolved.
3. Payload propositions and evidence are validated.
4. Prepare mutates nothing.
5. Commit restores beliefs, evidence, provenance, timestamps, revision, and processed transactions.
6. Restore emits no Knowledge events and does not replay discoveries, testimony, or confidence changes.
7. Failed commit rolls back to the prior coherent Knowledge save data.

Knowledge save data intentionally does not persist full Step 7 truth.

## Validation

Runtime validation detects:

- missing Person ID;
- missing Definition registry;
- missing Fact definition;
- invalid proposition;
- evidence owned by another Person;
- out-of-range confidence or evidence strength;
- duplicate belief/evidence IDs;
- Known beliefs without sufficient evidence;
- forgotten beliefs still acting as active Knowledge;
- impossible timestamp ordering.

Fact definitions validate stable ID format, required `fact.` prefix, concrete domain, proposition type, subject type, value type, visibility, staleness, forgetting, and contradiction policies.

## Test Lab

The Test Lab adds `Knowledge 8.1` under `Knowledge Step 8`.

Manual controls include:

- validate Knowledge runtime;
- preview visible-injury observation;
- record visible-injury observation;
- prove duplicate observation idempotency;
- add weak evidence;
- add strong evidence;
- add opposing evidence;
- create misconception fixture;
- correct misconception;
- mark stale;
- forget;
- share;
- save/load restore validation;
- reset canonical fixture.

## Automation

The registered suite is:

`feature.8.1.knowledge-facts-beliefs`

It covers readiness, baseline reset, preview no mutation, observation, duplicate safety, weak/strong/conflicting evidence, misconception and correction, testimony, source credibility, limited observations, species capability discovery, body replacement/staleness foundations, Person isolation, private fact blocking, forgetting, snapshots, persistence restore, and reset behavior.

## Multiplayer Authority

Future server-authoritative multiplayer must own final Knowledge state, evidence application, belief revision, discovery, sharing results, private access, persistence, and duplicate protection. Clients may display snapshots and submit observation requests, but cannot read unrestricted truth or authoritatively invent Knowledge.

## Feature 8.2 Contracts

Feature 8.2 can call the production request boundaries for:

- observer Person ID;
- target body/species/person/event IDs;
- observation quality;
- sensory/examination method;
- evidence strength and credibility;
- visibility;
- differential hypotheses;
- diagnosis confidence.

## Feature 8.3 Contracts

Feature 8.3 builds on:

- first learned, last updated, and last verified game-time fields;
- discovery categories;
- historical event references;
- previous-body history;
- forgetting policies;
- stale beliefs;
- retained summaries.

Authoritative historical records are owned by `AuthoritativeHistoryRuntime`; Person-owned memories are owned by `PersonMemoryRuntime`. Knowledge remains the owner of evidence and belief revision.

## Deferred

Feature 8.1 does not implement full perception, line of sight, diagnosis gameplay, books, rumor propagation, gossip, reputation-based trust, education progression, map discovery, codex UI, AI reasoning, cognition simulation, or networking.
