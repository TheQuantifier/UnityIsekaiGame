# Combat Contributions, Credit, and Reward Hooks

Feature 6.9 introduces `CombatContributionService` as the production runtime ledger for committed combat contribution facts. It does not grant XP, gold, loot, quests, reputation, or skill progress directly.

The alpha policy is `combat-contribution-policy.alpha`. It keeps a 30 second contribution window, requires positive actual damage for primary hostile credit, treats effective healing and prevented damage as assist signals, and resolves ties by most recent contribution, then greater damage, then lexicographically lower actor ID.

Only committed results may create records. Preview, failed, duplicate, restore, reset, and passive query paths are ignored. Damage uses actual Health damage after mitigation and clamping, healing uses effective restored Health, and defensive support uses prevented damage or a successful defense marker. Overkill, overheal, requested-but-not-applied amounts, and preview calculations do not create reward credit.

Credit resolution returns immutable diagnostic results with primary contributor, assists, disqualified contributors, policy ID, transaction IDs, actor IDs, optional person IDs, and future reward eligibility categories. `GrantsConcreteRewards` is always false.

## Production Event Sources

`CombatContributionEventBridge` is the production adapter for automatic recording. It subscribes to committed events from `DamageHealingService`, `AttackResolutionService`, `DefensiveActionService`, `OngoingEffectService`, `CombatReactionService`, `ActorLifecycleController`, and `CombatStateService`.

Authoritative event sources:

- Direct damage and healing: `DamageHealingService.DamageResolved` and `HealingResolved`.
- Attack damage: `AttackResolutionService.AttackDamageApplied`; child damage uses the existing damage transaction and dedupes against direct damage if both are attached.
- Active defense: `DefensiveActionService.AttackDodged`, `AttackParried`, and `AttackBlockedByDefense`.
- Ongoing damage and healing: `OngoingEffectService.OngoingEffectTickProcessed`.
- Reaction damage and healing: `CombatReactionService.ReactionProcessed`.
- Defeat and kill credit: `ActorLifecycleController.DefeatProcessed` and `ActorDied`.
- Recovery and revival support: `ActorRecovered` and `ActorRevived`.
- Encounter merge and split: `CombatStateService.EncountersMerged` and `EncounterSplit`.

Preview, failed, duplicate, restore, and Test Lab reset events are ignored or deduped by the contribution service.

## Encounter Merge and Split

Encounter merge uses the surviving `CombatStateService` encounter snapshot. Active ledgers touching the surviving participant set are merged into the survivor ledger in stable ledger/record order. Contribution records are unique by immutable record ID, absorbed encounter ledgers are removed, and one merge result event is emitted.

Encounter split keeps historical records but partitions active eligibility by resulting component participant IDs. Component ledgers contain records whose contributor, target, and beneficiary still belong to that component. Cross-component historical records remain visible in partition history but do not refresh current active eligibility for disconnected targets. Isolated participants are removed from active participant sets.

Restore behavior is intentionally transient. `ClearTransientStateForRestore` clears ledgers, credit results, dedupe keys, revision, and simulation time without emitting contribution, credit, or finalization events.

Manual Test Lab steps:

1. Open `Tab > Test Lab > Contribution 6.9`.
2. Click `Preview` and confirm the summary still shows no ledgers.
3. Click `Record Damage` and confirm one target ledger appears with a damage contributor.
4. Click `Reuse Damage` and confirm it reports a duplicate/idempotent path without adding a second record.
5. Click `Defeat Credit` and confirm a primary contributor is resolved with diagnostic-only reward hooks.
6. Click `Advance` with Amount set above `30`, then record new damage or resolve credit to test contribution expiry.
7. Click `Finalize` and confirm the current ledger is marked finalized.
8. Click `Clear` and confirm no ledgers remain.
