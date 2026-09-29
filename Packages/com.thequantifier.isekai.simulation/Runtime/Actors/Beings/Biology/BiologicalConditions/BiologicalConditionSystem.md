# Biological Condition System

Feature 7.9 adds the body-owned runtime for persistent internal biological conditions: diseases, infections, parasites, poisons, venoms, toxins, intoxication, fever-like responses, chronic conditions, dormancy, treatment response, immunity memory, and transmission planning.

## Ownership

Biological condition state belongs to the exact Actor/body. Ordinary disease, infection, poison, venom, toxin, intoxication, fever, and parasite state is body-bound by default. Person, controller, and occupancy changes do not transfer condition state unless a future authored policy explicitly does so.

Body replacement clears ordinary body-bound conditions by default. Same-body Species or body-form changes reconcile active instances through authored transformation policies and Feature 7.6 compatibility.

## Compatibility

All exposure, progression, treatment, transmission planning, and transformation reconciliation use the Feature 7.6 biological compatibility runtime. The condition runtime consumes compatibility state, immunity, resistance, vulnerability, suppression, rate and severity multipliers, consequence multipliers, maximum severity, and dependency revisions. Missing or stale compatibility context fails closed.

Definition IDs and runtime concepts remain distinct:

- `BiologicalConditionDefinition.Id` is the canonical catalog identity.
- Biological interaction IDs and categories describe how Feature 7.6 evaluates the condition.
- Condition instance IDs identify exact body-owned runtime state.

## Exposure And Progression

Exposure requests include body ID, condition definition, route, dose, optional strain, source, anatomy target, transaction ID, preview flag, and optional expected dependency revisions. Preview uses the same calculation path as execution but does not mutate state or remember transactions.

Execution establishes or updates deterministic condition instances. Duplicate transaction IDs are idempotent and do not apply a second mutation. Instance IDs are stable and derived from the owning body, condition definition, strain, and source policy.

Ticks progress active conditions deterministically from elapsed game time. Conditions can move through exposed, incubating, active, recovering, dormant, chronic, carrier, cleared, or resolved states according to authored stage rules.

## Consequences

Feature 7.9 owns condition state and condition progression. When condition ticks need consequences, `BiologicalConditionRuntime` coordinates those consequences through the systems that own the affected state:

- vital pressure commits through `VitalProcessRuntime`;
- hazard source commits through `BiologicalHazardRuntime`;
- Step 6 damage commits through `DamageHealingService`;
- recovery-rate modifiers commit through `BiologicalRecoveryRuntime`;
- symptom snapshots;
- transmission eligibility.

Preview uses the same consequence calculation path but calls only preview/non-mutating owner APIs. It must not mutate vital values, hazard sources, recovery modifiers, Health, revisions, dirty state, or transaction memory.

Execution first previews condition progression and all owner consequences. If any owner rejects the operation, the condition tick is rejected. If owner commits start and a later operation fails, the runtime restores the pre-tick condition, vital, hazard, and recovery state where those owners provide restore data. Duplicate tick transaction IDs are idempotent through the owning systems.

This keeps condition state authoritative without bypassing the systems that own health, hazards, damage, or repair. Conditions decide that a fever applies heat pressure or a poison applies damage; the target systems decide whether those mutations are valid and how they affect their own state.

## Treatment And Immunity

Treatment definitions declare compatible condition IDs and/or condition families. Applying treatment uses Feature 7.6 compatibility and reduces condition load according to authored effectiveness. Treatments may clear conditions and grant acquired immunity memory. Immunity memory reduces future effective dose for matching condition definitions and strains.

Medication inventory, crafting, diagnosis, hospitals, doctors, and production UI are deferred.

## Transmission

Transmission profiles produce exposure plans only. They do not apply disease to another body directly. Future server-owned simulation can choose whether and when to submit the resulting exposure request to the target body runtime.

## Persistence

Biological conditions are serialized inside `BodySaveData` schema version 8. Save data stores active condition instances, immunity memory, processed transaction IDs, and the biological-condition revision. Restore uses validate/prepare/commit behavior through the body runtime and does not replay exposure, tick, treatment, or transmission events.

Development saves before schema 8 are pre-alpha and may be invalidated rather than migrated permanently.

## Multiplayer

Future multiplayer persistence should be server-owned. A client may request actions that cause exposure or treatment, but clients must not be authoritative over shared disease spread, body condition state, or cross-body transmission. If one player disconnects, only their player/body state should be persisted; the world and other bodies continue under server authority.

## Deferred To Later Features

Deferred work includes diagnosis UI, medicine inventory costs, addiction, food/drink systems, population epidemiology, visual symptoms, animation, quests, and multiplayer replication. Transmission remains plan-only in 7.9; future server-owned simulation can submit the planned exposure to another body.
