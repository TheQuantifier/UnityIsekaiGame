# Observation, Examination, Identification, and Diagnosis

Feature 8.2 adds the production foundation that turns observable game state into Person-owned Knowledge evidence. It does not make Knowledge authoritative for truth. Step 7 body, biology, anatomy, injury, condition, transformation, and compatibility runtimes remain the source of truth.

## Ownership Boundary

The runtime flow is:

1. Authoritative gameplay state exposes a limited projection.
2. `ObservationService` evaluates method rules, quality, privacy, tracking policy, and result type.
3. `PersonKnowledgeRuntime` records or previews evidence and beliefs.

`ObservationService` is a high-level production entry point. It does not mutate Step 7 systems, does not own diagnosis truth, and does not bypass `PersonKnowledgeRuntime` duplicate transaction protection.

## Runtime Content

Runtime code lives under:

`Packages/com.thequantifier.isekai.simulation/Runtime/Knowledge/Observation/`

The main runtime types are:

- `ObservationMethodDefinition`
- `ExaminationMethodDefinition`
- `IdentificationMethodDefinition`
- `DiagnosticMethodDefinition`
- `ObservationContext`
- `ObservableProjection`
- `DiagnosticProjection`
- `ObservationResult`
- `ObservationService`

Authored alpha content lives under:

`Packages/com.thequantifier.isekai.content/Content/Knowledge/Observation/`

## Tracking Policy

Observation supports multiple tracking policies:

- `NpcFullTracking` records relevant NPC knowledge.
- `PlayerMechanicalOnly` records only mechanically relevant player knowledge.
- `RemotePlayerMechanicalOnly` is reserved for future multiplayer-facing player knowledge.
- `DevelopmentObserverNoMutation` lets Test Lab and diagnostics inspect without mutating player knowledge.

Preview never mutates Knowledge, creates no persisted evidence, and does not advance Knowledge revision. Execution reuses the same calculation path but commits through `PersonKnowledgeRuntime`.

These policies are authored/runtime observation policy choices. They do not require a Player MonoBehaviour, camera, input action, or prototype object.

## Repeated Evidence

Feature 8.2 generates stable observation evidence IDs from the observer, method, and proposition identity. `PersonKnowledgeRuntime` remains the owner of evidence and confidence mutation. If a new transaction repeats identical evidence, Knowledge treats it as duplicate evidence, records the transaction as processed, and does not increase confidence, add another evidence record, or advance the Knowledge revision.

Meaningfully different observations can still create distinct evidence by changing the proposition identity, method, source revision, observer, or later authored evidence identity.

## Privacy and Concealment

Private, hidden, diagnostic-only, and confidential facts require explicit access context. Ordinary visible observation cannot silently discover private medical facts.

Concealment, lighting, distance, noise, obstruction, expertise, and tool quality contribute to the final observation quality. If quality is below the projection threshold, no Knowledge mutation occurs.

## Identification and Diagnosis

Identification methods support partial and exact thresholds. They still write ordinary Knowledge evidence when successful.

Diagnostic methods produce differential or exact diagnostic Knowledge from authored diagnostic projections. This is intentionally separate from biological truth. A diagnosis can be incomplete, wrong, stale, or private, while the underlying condition runtime remains authoritative.

Species identification, body identification, persistent Person recognition, transformation detection, possession detection, and prior-body history are intentionally separate fact/method paths. Identifying an apparent species or current body does not automatically prove persistent Person identity after body replacement, disguise, possession, polymorph, or reincarnation.

Diagnosis retains multiple hypotheses and records family-level results until authored thresholds permit an exact candidate. The diagnostic projection carries candidates and supporting projection IDs; the service applies method ceilings and thresholds rather than forcing the correct hidden condition.

## Stale Projections

Observation contexts carry expected source revisions for body and condition-sensitive projections. If a preview or prepared projection was based on an older injury, symptom, species, body-form, or transformation revision, execution rejects it as stale before Knowledge mutation. Callers should rebuild the projection from current authoritative state and retry.

## Active and Foundation-Only Methods

Observation, examination, identification, and diagnostic method definitions include an `active` flag. Runtime service methods reject inactive methods. This keeps foundation-only magical, tool-assisted, and future-system placeholders in the catalog without making them executable before their real source systems exist.

## Compatibility Notes

Feature 8.2 preserves Feature 8.1 save behavior because all committed knowledge still flows through `PersonKnowledgeRuntime` and its existing persistence participant. No new save participant is introduced.

No runtime dependency is added on Player input, cameras, UI, Test Lab, Prototype, Development, or Editor assemblies.

## Prototype Test Lab

The Test Lab includes a new `Observation 8.2` section under `Knowledge Step 8` with controls for:

- validating authored method content;
- previewing ordinary visual observation;
- recording ordinary visual observation;
- proving duplicate transaction idempotency;
- recording medical examination evidence;
- producing a diagnosis foundation result;
- proving player-irrelevant observations are not tracked;
- proving development observers do not mutate player knowledge;
- proving NPC full tracking records and remote-player irrelevant observations do not;
- proving repeated identical observations are bounded;
- proving stale projections and inactive foundation methods are rejected;
- proving concealment lowers quality;
- proving private medical observations are rejected without access.

## Manual Test Steps

1. Open `PrototypeScene`.
2. Enter Play Mode.
3. Press `Tab`.
4. Open `Test Lab`.
5. Select `Knowledge Step 8`.
6. Select `Observation 8.2`.
7. Click `Validate` and confirm the result succeeds.
8. Click `Preview Visual` and confirm Knowledge revision/evidence count does not increase.
9. Click `Record Visual` and confirm evidence/belief count increases.
10. Click `Duplicate` and confirm the duplicate operation succeeds without increasing revision twice.
11. Click `Medical Exam` and confirm stronger examination evidence records.
12. Click `Diagnose` and confirm a differential diagnosis result appears.
13. Click `Player Filter` and `Dev No Mutate` and confirm both succeed without mutating Knowledge.
14. Click `NPC Track`, `Remote Filter`, `Repeat Bound`, `Stale Reject`, and `Inactive Reject` and confirm each succeeds with the expected result.
15. Click `Concealment` and confirm concealed quality is lower than clear quality.
16. Click `Private Reject` and confirm private observation is rejected without mutation.

## Remaining Limitations

Feature 8.2 does not implement camera raycasts, AI perception loops, books, codex UI, rumor networks, field-of-view simulation, full diagnosis gameplay, examination tools, or networking. Those systems should feed projections into this service later instead of bypassing it.
