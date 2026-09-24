# Professional Activity and Experience

Feature 10.4 adds the professional activity and experience layer for Step 10.

Professional activities are records that adapt completed work from owning systems into professional experience evidence. Crafting, production, repair, item recovery, experimentation, and training remain the owners of their source records. The professional activity runtime records the professional meaning of those source records, validates whether they can count as experience, and derives summaries for entry and training requirements.

## Ownership

- `ProfessionalActivityDefinition` defines what source records can count for a profession or specialization.
- `ProfessionalActivityRuntime` owns professional activity records and validated experience evidence.
- Source runtimes own their own records. They are referenced through `ProfessionalActivitySourceReferenceData`.
- Experience summaries are derived projections. They do not mutate professions, skills, knowledge, training, credentials, titles, ranks, employment, or reputation.

## Source Adapters

`ProfessionalActivitySourceAdapters` converts existing records into exact professional source snapshots:

- crafting operations
- production jobs and work orders
- repair records
- item-recovery records, distinguished by disassembly or salvage-pickup tags
- experimentation trials and discovery claims
- training practical assignments
- supervised work records
- teaching sessions
- custom authoritative prototype records

Adapters preserve source identity, parent source identity, revision, actor, world time, completion state, outcome, difficulty, quality, tags, and related subjects.

## Validation

Validation checks:

- known Person identity
- profession and specialization definitions
- professional activity definition
- accepted source type
- actor/source ownership
- completed and accessible source state
- quality and difficulty thresholds
- required activity tags
- supervision and independent-work policies
- failed-work credit policy
- exclusive duplicate source policy

Exclusive activity definitions reject duplicate validated evidence from the same source signature. Shared definitions allow role-specific credit for teams, teaching, supervision, and collaborative work.

## Experience Summaries

Experience summaries derive:

- total validated activity count
- first and most recent activity time
- category counts
- specialization counts
- supervised, independent, teaching, leadership, research, success, and failure counts
- quality and difficulty distributions
- representative source references
- breadth, depth, recency, and consistency foundations

These summaries are deterministic and immutable snapshots.

## Access and History

Professional activities expose Step 8 information subjects so callers can request full, partial, redacted, or denied projections through `InformationAccessRuntime`.

The runtime produces lightweight history hooks for important professional milestones such as first activity, major independent work, important failure, leadership, innovation, correction, dispute, and revocation. Restore paths do not replay hooks.

## Persistence

`ProfessionalActivityPersistenceParticipant` persists professional activities as a strict player-scoped participant. Save restore validates the graph before commit and rolls back if commit fails.

Missing definitions, unknown people, missing professions, missing activity definitions, duplicate IDs, broken evidence links, and invalid quantities reject restore before live state is mutated.

## Test Lab

Feature 10.4 registers the `feature.10.4.professional-activity-experience` automation suite under Step 10. It runs in FreshRuntime isolation and uses the shared Test Lab fixture snapshot/audit system.
