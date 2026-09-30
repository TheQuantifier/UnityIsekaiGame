# Tools and Production Requirements

Feature 9.5 establishes the qualification layer used by later crafting, repair,
maintenance, and salvage execution. It does not execute recipes or production
queues. It answers whether the current actor, tools, station, resources, access,
knowledge, and environment can support a requested production operation.

## Ownership Boundaries

Definitions describe eligibility:

- `ProductionToolDefinition` defines tool category, roles, capabilities,
  substitutions, minimum quality, minimum durability, and wear-per-use.
- `ProductionStationDefinition` defines station category, capabilities,
  supported tool roles, and reservation capacity.
- `ProductionRequirementDefinition` defines a single requirement and optional
  alternatives.

Runtime state remains owned by the systems that created it:

- Item identity owns item instances, ownership, custody, and location.
- Composition owns item materials and components.
- Quality owns workmanship and affixes.
- Durability owns damage, repair, maintenance, and wear.
- Item recovery owns disassembly and salvage-pickup operation history.
- Production requirements own only plans, station instances, and reservations.

## Evaluation Flow

`ProductionRequirementRuntime.EvaluateRequirements` accepts authored
requirements and a `ProductionContextData` snapshot of currently available
tools, quantities, access, body capabilities, known facts, and environment keys.
The runtime selects deterministic candidates and returns a
`ProductionRequirementPlanData`.

Evaluation can be run as a preview. Preview produces a plan but does not commit
it to the runtime.

## Plans and Reservations

A plan records exact selected tool item instances, station instances, allocated
item/material/resource quantities, alternatives used, dependency revisions, and
a deterministic signature.

Reservations are explicit and separate from planning. Active reservations block
other plans from selecting the same tool and enforce station capacity. Released
and expired reservations no longer block selection.

## Invalidation

Plans store dependency revisions for selected item identities, durability
records, and station instances. `ValidatePlanCurrent` invalidates a plan when a
dependency revision changes before execution.

## Persistence

`ProductionRequirementRuntimeSaveData` persists station instances, plans, and
reservations. `ProductionRequirementPersistenceParticipant` validates payloads
before commit and rolls back if restore fails.

## Deferred

The following are intentionally deferred:

- Recipe execution and output creation.
- Production queues and multi-tick jobs.
- Full NPC labor scheduling.
- Legal ownership and workplace authority rules.
- UI flows for choosing alternatives.
- Multiplayer/account-level access checks.
