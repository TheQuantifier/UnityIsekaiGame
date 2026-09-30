# Feature 6.4 Ongoing Effects

Ongoing effects are production runtime instances that schedule repeated damage, healing, or Resource changes against the exact Actor/body selected at application time.

Flow:

`OngoingEffectService -> deterministic tick validation -> DamageHealingService or Current Resources -> post-commit ongoing-effect event`

Health damage and Health healing always use `DamageHealingService`. Mana and Stamina regeneration use `CharacterResourceCollection.ApplyChange` with deterministic tick event IDs. Ongoing effects do not write resource fields directly and do not trigger actor lifecycle directly; Health reaching zero flows through the existing Current Resource event into `ActorLifecycleController`.

## Tick Rules

- `tickInterval` must be greater than zero.
- `initialDelay` defaults to the interval when authored as zero.
- `tickImmediately` schedules exactly one legal tick at elapsed time zero.
- A tick due exactly at the duration boundary is legal.
- Completion occurs after the finite tick count is reached, or after duration has elapsed and no legal due tick remains.
- Large elapsed-time updates process all due ticks up to `maximumTicksPerProcess`; capped processing is reported.
- Preview application does not create instances, mutate Resources, emit execution events, mark persistence dirty, advance time, or consume transaction IDs.

Each committed tick uses:

`{ongoingEffectInstanceId}.tick.{tickIndex}.{operationType}`

The Resource/DamageHealing transaction layer remains the authoritative duplicate protection for mutations. The ongoing-effect service tracks application, cancellation, and tick transaction IDs so scheduler state cannot replay the same committed tick.

## Lifecycle

Definitions author unconscious and death behavior independently. Alpha defaults allow damage, healing, and ordinary regeneration to continue while unconscious, and cancel when the exact Actor/body becomes dead. Dead-target continuation requires explicit authored configuration. Effects never transfer to replacement bodies and revival does not restore cancelled effects.

## Persistence

The player ongoing-effect participant saves active instance state only. It stores definition ID, source/target actor IDs, origin ID, elapsed state, next tick position, completed tick count, stack count, and revision. It does not save Health, Mana, or Stamina values; those remain owned by player resources.

Restore resolves definitions and the exact target actor before commit. Restore does not replay application events, tick events, or already completed ticks.

Offline-time policy for alpha: paused game/save time does not generate offline ticks. Loading resumes from saved remaining duration and next-tick state.

## Legacy Audit

`StatusEffectDefinition.periodicEffects` exists as an older authored placeholder, but `StatusEffectController` does not execute those effects. Feature 6.4 intentionally does not activate that path to avoid a second periodic scheduler. Future status/condition integration should adapt status meaning to `OngoingEffectService` instances rather than adding another tick loop.

Legacy prototype wrappers such as `PlayerHealth`, `PlayerMana`, and `PlayerStamina` remain for UI and backwards compatibility. New 6.4 periodic behavior uses `DamageHealingService` and Current Resources directly.

`CharacterResourceCollection` still owns native Resource-definition regeneration for passive Mana and Stamina recovery. Ongoing-effect regeneration is an explicit gameplay effect layered through the same Resource mutation API, not a replacement scheduler for passive Resource definitions. Test Lab keeps the legacy "Regen Tick" control so passive regeneration can still be tested separately from 6.4 instances.
