# Feature 6.7 Combat Costs, Cooldowns, and Execution Commitments

Feature 6.7 adds a production runtime execution layer that owns combat-action commitments, resource costs, cooldowns, charges, and transient execution state.

## Ownership

- `CombatExecutionService` is the high-level entry point for beginning, committing, cancelling, interrupting, ticking, saving, and restoring combat execution state.
- `CombatExecutionDefinition` is authored data for wind-up, recovery, commitment category, cooldown scope, charges, and cost timing.
- `CharacterResourceCollection` remains the only owner of Current Resource mutation and duplicate resource-event protection.
- `AttackResolutionService`, `AbilityExecutor.ExecuteEffects`, and `DefensiveActionService` remain the owners of their underlying attack, effect, and defense behavior when reached by a 6.7 handler.
- `ActorLifecycleController` remains the owner of Defeated, Unconscious, and Dead state. Combat executions require actors that can act.

## Runtime Flow

Preview begin and preview commit run the same validation paths as execution but do not call mutating resource APIs, consume cooldown charges, create commitments, emit execution events, or consume transaction IDs.

Begin execution validates actor identity, lifecycle, requirements, commitment conflicts, cooldown or charge availability, begin-time costs, and handler preview. Execution begin then creates a transient commitment and optionally consumes a cooldown charge if the definition starts cooldowns on begin.

Commit execution revalidates actor identity, exact actor body, lifecycle, readiness boundary, execution-time costs, and handler preview. Execution commit spends resource costs through `CharacterResourceCollection.ApplyChange`, calls the chosen handler exactly once, then enters recovery.

`ProcessExecutionTime` advances authoritative runtime time, restores charges when ready, and completes executions whose recovery has ended.

## Costs

Resource costs are currently implemented. They are checked atomically before mutation. If a later resource mutation fails after a previous cost was committed in the same cost phase, the service attempts a correlated refund.

Inventory item, currency, and ammunition costs are represented in `CombatExecutionCostDefinition` but intentionally rejected with `UnsupportedCostType`. They are deferred until production has a non-Player transactional inventory, wallet, and ammunition API that can commit and refund atomically.

Health can be used as a resource cost through the Current Resource API. The authored `minimumRemaining` value prevents accidental lethal/self-sacrifice costs unless a future definition explicitly allows that behavior.

## Cooldowns and Charges

Cooldowns are tracked per actor and keyed by definition, cooldown group, or global group. Charges are consumed at the authored cooldown start point. Exact readiness uses `now >= readyAt`.

The prototype charge recovery policy is intentionally simple: when the next ready time is reached, charges refill to the authored maximum. A more detailed sequential recharge scheduler can replace the runtime record later without changing definition IDs.

## Commitments

Active commitments are transient and tied to actor ID plus runtime actor body ID. Replacement bodies do not inherit commitments. Commitment conflicts use authored `CombatCommitmentCategory` values and optional overlap declarations.

Cancellation before execution can refund begin-time resource costs when the definition uses `RefundIfCancelledBeforeExecution`. Interruptions obey the authored interruption policy.

## Persistence

Combat execution save data is connected through the optional player-scoped `player.combat-execution` persistence participant. The participant captures `CombatExecutionSaveData` from the shared `CombatExecutionService` instance used by the prototype persistence service and Test Lab.

Combat execution save data persists cooldown and charge records only. Active commitments, wind-ups, recovery windows, and handler payloads are not saved.

Participant load uses the normal prepare/commit flow. Prepare parses and validates the payload without mutating runtime state. Commit calls `RestoreFromSaveData`, which clears transient commitments silently before applying cooldown and charge data. Restore emits no begin, commit, cancel, interrupt, completion, cost, cooldown, or committed-combat events.

Feature 6.7 applies no offline progression. Saved cooldown timestamps are restored exactly as written; elapsed offline time does not recover charges or advance readiness during load.

## Committed Event

`CombatExecutionService.CombatExecutionCommitted` publishes an immutable `CombatExecutionCommitted` result after a successful commit. It includes the execution transaction ID, begin transaction ID, execution instance ID, actor ID, actor body ID, target actor ID when known, action definition, action type, state snapshot, committed costs, cooldown and charge snapshot, underlying handler result, and string context metadata.

This event is intended as the production handoff point for Feature 6.8 reaction triggers. Preview and restore paths do not emit it.

## Existing Direct Paths

The older `PlayerMeleeCombat`, `EnemyMeleeAttack`, `PlayerSpellcaster`, `AbilityCooldownTracker`, and `AbilityResourceCostProcessor` paths still exist for prototype input and legacy ability compatibility. They are not removed in this feature because they are still directly wired to player controls and spell loadout behavior.

The 6.7 ability handler validates ability shape and target/effect readiness without invoking legacy `AbilityExecutor.Validate`, so actions that enter through `CombatExecutionService` do not also spend legacy ability resource costs or start legacy ability cooldowns. Legacy callers that still bypass `CombatExecutionService` continue to own their existing costs/cooldowns until they are explicitly migrated.

New production combat actions should enter through `CombatExecutionService`. Existing callers can be migrated once their input/UI contracts can supply a `CombatExecutionDefinition` and payload without breaking current prototype controls.

## Prototype Definitions

Prototype execution definitions are under `Packages/com.thequantifier.isekai.content/Content/Combat/Execution/` and registered in the prototype catalog:

- `combat-execution.basic-attack`
- `combat-execution.arcane-spell`
- `combat-execution.quick-guard`

The Test Lab exposes these through the `Execution 6.7` tab.
