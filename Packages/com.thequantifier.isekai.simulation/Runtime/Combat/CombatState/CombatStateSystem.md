# Feature 6.5 Combat State

Combat state is transient production runtime coordination for exact Actor/body identities.

Flow:

`Committed hostile activity -> CombatStateService -> engagement refresh -> encounter membership -> post-commit combat-state events`

`CombatStateService` does not replace `DamageHealingService`, `AttackResolutionService`, actor lifecycle, factions, social status, ongoing effects, resources, AI threat, or target selection. It consumes committed result objects or explicit authoritative requests and exposes immutable snapshots.

## Entry Rules

- Valid executed hostile attack attempts enter or refresh combat.
- Misses start combat when the active `CombatStatePolicyDefinition` allows it.
- Invalid or blocked attacks do not enter combat.
- Fully prevented hostile damage starts or refreshes combat when the policy allows it.
- Healing, regeneration, previews, duplicate transactions, self-targeting activity, and source-less damage do not create enemy engagements.
- Hostile ongoing damage ticks refresh combat when the policy allows it. Harmless ongoing effects do not.

## Engagements and Encounters

Engagements are one active pair relationship per exact Actor/body pair. The engagement keeps the latest directional activity classification but uses a bilateral pair key for participant combat state.

Encounters group connected participants. If a hostile interaction connects two active encounters, the older encounter survives. If creation times match, the lexicographically lower encounter ID survives. Participant references and engagement encounter IDs are updated before merge events are emitted.

After an active engagement or participant is removed, the service reconciles the encounter as an active engagement graph. Participants are nodes and active bilateral engagements are edges. Each active encounter must contain exactly one connected component.

When a graph disconnects, the component containing the participant with the earliest encounter join time retains the original encounter ID. If join times tie, the component containing the lexicographically lowest Actor/body ID retains it. Additional multi-participant components receive generated `encounter.runtime.*` IDs. Single participants with no active hostile engagement leave combat instead of remaining in one-participant encounters, and empty encounters end.

`GetRecentOpponents(actorId)` is intentionally historical for the runtime session. It can include opponents from ended engagements after a split. Use `GetActiveEngagements(actorId)` for current connected combat only.

## Timeout and Lifecycle

The alpha timeout is authored on `combat-state-policy.prototype-alpha` and defaults to 10 seconds. Timeout processing uses elapsed simulation time and can process large jumps deterministically up to the policy safety cap.

Defeated and unconscious actors remain participants until timeout unless removed by explicit authoritative exit. Dead actors are removed when the policy enables dead-participant removal. Revival alone does not create combat state and replacement bodies do not inherit combat state.

## Persistence

Combat state is intentionally not persisted in alpha. Active encounters, engagements, processed combat transactions, and combat-state events are runtime-session state only. Save/load restores resources, lifecycle, ongoing effects, and other durable systems independently; loading starts actors out of combat and does not replay combat entry/exit events.

Future multiplayer persistence will be server-owned. The authoritative server will own hostility validation, combat entry, encounter grouping, merging, timeout evaluation, participant removal, and committed combat events.

## Legacy Audit

Legacy prototype fields and wrappers such as melee `AttackResolved` events, `PlayerHealth`, `EnemyHealth`, and simple enemy targeting do not own combat flags, engagement state, or encounter membership. They are not a competing authoritative combat-state system. New systems should query `CombatStateService` snapshots instead of adding new `inCombat` flags.
