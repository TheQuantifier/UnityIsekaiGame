#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(15, "Quests", 1500)]
    public static class PrototypeStep15AutomationSuites
    {
        private static readonly string[] RequiredQuestDefinitionIds = PrototypeQuestDefinitionFactory.PrototypeDefinitionIds
            .Concat(PrototypeQuestSourceDefinitionFactory.PrototypeDefinitionIds)
            .Concat(PrototypeConversationDefinitionFactory.PrototypeDefinitionIds)
            .Concat(PrototypeDialogueGraphDefinitionFactory.PrototypeDefinitionIds)
            .Concat(PrototypeNarrativeEventDefinitionFactory.PrototypeDefinitionIds)
            .Concat(PrototypeNarrativeStateDefinitionFactory.PrototypeDefinitionIds)
            .Concat(PrototypeNarrativeArcDefinitionFactory.PrototypeDefinitionIds)
            .ToArray();

        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.1.quest-identity-definitions-runtime-records",
                "Quest Identity, Definitions, and Runtime Records",
                "15.1",
                "Definition-authored quest identity and runtime-owned quest records with issuers, recipients, origins, subject links, visibility-safe queries, persistence, and automation coverage.",
                15010,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "QuestRuntime", "QuestDefinition", "QuestRuntimePersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("90.1-readiness-and-definitions", "Quest definitions register and validate", 10, Step("step15-quest-readiness", "Resolve prototype quest definitions", ReadinessAndDefinitions)),
                    Scenario("90.2-unique-and-repeatable", "Unique quests and reusable dynamic quests have distinct identities", 20, Step("step15-quest-identity", "Create unique and dynamic quest records", UniqueAndRepeatable)),
                    Scenario("90.3-issuer-recipient-origin-subjects", "Issuer, recipient, origin, and subject links stay reference-only", 30, Step("step15-quest-references", "Create linked quest references", ReferenceBoundaries)),
                    Scenario("90.4-visibility-safe-query", "Hidden quests do not leak through ordinary queries", 40, Step("step15-quest-visibility", "Query hidden and public quests", VisibilitySafeQuery)),
                    Scenario("90.5-lifecycle-revision-idempotence", "Lifecycle, revisions, and duplicate transactions are deterministic", 50, Step("step15-quest-lifecycle", "Transition lifecycle with revision checks", LifecycleRevisionIdempotence)),
                    Scenario("90.6-persistence-world-isolation", "Quest save/restore validates world scope without replaying events", 60, Step("step15-quest-persistence", "Save, restore, and reject corrupt quest payload", PersistenceWorldIsolation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.2.quest-availability-eligibility-offering-acceptance-assignment",
                "Quest Availability, Eligibility, Offering, Acceptance, Assignment, and Abandonment",
                "15.2",
                "Participant-owned quest availability, eligibility, offer, acceptance, assignment, visibility, persistence, and abandonment boundaries.",
                15020,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "QuestRuntime", "QuestParticipationRuntime", "QuestParticipationRuntimePersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("91.1-participation-policy-readiness", "Participation policies register and validate", 10, Step("step15-participation-readiness", "Resolve quest participation policies", ParticipationPolicyReadiness)),
                    Scenario("91.2-availability-eligibility", "Availability and eligibility remain distinct", 20, Step("step15-participation-eligibility", "Evaluate availability and eligibility", AvailabilityEligibility)),
                    Scenario("91.3-offer-and-acceptance", "Offer preview, creation, consent, and acceptance are atomic", 30, Step("step15-participation-offer", "Create and accept an offer", OfferAndAcceptance)),
                    Scenario("91.4-exclusive-capacity", "Exclusive assignment prevents stale acceptance", 40, Step("step15-participation-exclusive", "Revalidate capacity on acceptance", ExclusiveCapacity)),
                    Scenario("91.5-abandonment-release", "Abandonment releases capacity when configured", 50, Step("step15-participation-abandonment", "Abandon an active assignment", AbandonmentRelease)),
                    Scenario("91.6-visibility-and-persistence", "Hidden participation and persistence preserve boundaries", 60, Step("step15-participation-persistence", "Query and restore participation state", VisibilityAndPersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.3.quest-objectives-conditions-progress-tracking",
                "Quest Objectives, Conditions, and Progress Tracking",
                "15.3",
                "Assignment-owned objective progress with stable objective identities, state reconciliation, event-driven progress, visibility, idempotence, and persistence boundaries.",
                15030,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "QuestRuntime", "QuestParticipationRuntime", "QuestObjectiveProgressRuntime", "QuestObjectiveProgressPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("92.1-objective-readiness", "Objective definitions register and validate", 10, Step("step15-objective-readiness", "Resolve objective definitions", ObjectiveReadiness)),
                    Scenario("92.2-assignment-instantiation", "Accepted assignments instantiate objective records", 20, Step("step15-objective-instantiate", "Create assignment objectives", ObjectiveInstantiation)),
                    Scenario("92.3-event-sequence-idempotence", "Event progress unlocks sequence and deduplicates source events", 30, Step("step15-objective-events", "Apply committed objective signals", ObjectiveEventSequence)),
                    Scenario("92.4-current-state-reconciliation", "Current quantity objectives reconcile without fake events", 40, Step("step15-objective-state", "Reconcile current item state", ObjectiveCurrentState)),
                    Scenario("92.5-hidden-progress-visibility", "Hidden objectives progress without public leakage", 50, Step("step15-objective-hidden", "Apply hidden objective progress", ObjectiveHiddenVisibility)),
                    Scenario("92.6-persistence-and-rejection", "Objective progress persists and rejects invalid payloads safely", 60, Step("step15-objective-persistence", "Save, restore, and reject objective progress", ObjectivePersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.4.quest-completion-failure-deadlines-rewards-consequences",
                "Quest Completion, Failure, Deadlines, Rewards, and Consequences",
                "15.4",
                "Assignment-owned terminal outcomes with deadline evaluation, completion policy enforcement, reward entitlements, owner-runtime reward delegation, visibility boundaries, and persistence-safe restore.",
                15040,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "QuestRuntime", "QuestParticipationRuntime", "QuestObjectiveProgressRuntime", "QuestOutcomeRuntime", "QuestOutcomePersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("93.1-outcome-readiness", "Outcome policies, deadlines, rewards, and consequences register", 10, Step("step15-outcome-readiness", "Resolve terminal outcome definitions", OutcomeReadiness)),
                    Scenario("93.2-turn-in-completion", "Turn-in completion creates one terminal outcome and claimable rewards", 20, Step("step15-outcome-completion", "Complete an assignment through turn-in", OutcomeTurnInCompletion)),
                    Scenario("93.3-deadline-failure", "Deadline expiration fails exactly once", 30, Step("step15-outcome-deadline", "Evaluate deadline failure", OutcomeDeadlineFailure)),
                    Scenario("93.4-reward-claim", "Reward claims delegate to owner runtimes and remain idempotent", 40, Step("step15-outcome-reward", "Claim a reward entitlement", OutcomeRewardClaim)),
                    Scenario("93.5-persistence-and-redaction", "Outcomes and hidden rewards persist with redacted ordinary projections", 50, Step("step15-outcome-persistence", "Save, restore, and query redacted outcome state", OutcomePersistenceAndRedaction))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.5.quest-sources-boards-discovery-availability-presentation",
                "Quest Sources, Quest Boards, Discovery, and Availability Presentation",
                "15.5",
                "Runtime-owned quest sources and listings with publication authority, source filtering, discovery-safe browse and inspect projections, delegated acceptance, expiration, and persistence boundaries.",
                15050,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "QuestRuntime", "QuestParticipationRuntime", "QuestSourceRuntime", "QuestSourcePersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("94.1-source-readiness", "Quest source definitions register and validate", 10, Step("step15-source-readiness", "Resolve quest source definitions", QuestSourceReadiness)),
                    Scenario("94.2-empty-source", "Quest source can exist without listings", 20, Step("step15-source-empty", "Create an empty source with scene binding", QuestSourceEmpty)),
                    Scenario("94.3-publish-browse-discovery", "Publication and browse produce visibility-safe discovery", 30, Step("step15-source-publish-browse", "Publish and browse a source listing", QuestSourcePublishBrowseDiscovery)),
                    Scenario("94.4-acceptance-claims-listing", "Source acceptance delegates to participation and marks listing taken", 40, Step("step15-source-acceptance", "Accept a quest through its listing", QuestSourceAcceptanceClaimsListing)),
                    Scenario("94.5-expiration-persistence", "Expiration and persistence preserve source graph deterministically", 50, Step("step15-source-persistence", "Expire, save, restore, and reject corrupt source payload", QuestSourceExpirationPersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.6.dialogue-conversation-identity-foundation",
                "Dialogue and Conversation Identity Foundation",
                "15.6",
                "Runtime-owned conversation identity and records with participant roles, quest/source/location context, provider boundaries, visibility-safe projections, idempotence, and persistence.",
                15060,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "ConversationRuntime", "ConversationDefinition", "ConversationPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("95.1-conversation-readiness", "Conversation definitions register and validate", 10, Step("step15-conversation-readiness", "Resolve conversation definitions", ConversationReadiness)),
                    Scenario("95.2-guild-counter-context", "Guild counter conversation records quest source, listing, location, and provider context", 20, Step("step15-conversation-context", "Start guild counter conversation", ConversationGuildCounterContext)),
                    Scenario("95.3-private-projection", "Private conversations redact hidden participant and subject details", 30, Step("step15-conversation-private", "Query private conversation projections", ConversationPrivateProjection)),
                    Scenario("95.4-provider-and-location", "Provider and co-location requirements reject invalid starts without mutation", 40, Step("step15-conversation-provider-location", "Reject missing provider and wrong location", ConversationProviderLocationValidation)),
                    Scenario("95.5-idempotence-lifecycle", "Conversation transactions and lifecycle transitions are deterministic", 50, Step("step15-conversation-lifecycle", "Deduplicate start and transition lifecycle", ConversationIdempotenceLifecycle)),
                    Scenario("95.6-persistence", "Conversation save and restore preserve records without replaying events", 60, Step("step15-conversation-persistence", "Save, restore, and reject corrupt conversation payload", ConversationPersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.7.dialogue-nodes-conditions-choices-conversation-flow",
                "Dialogue Nodes, Conditions, Choices, and Conversation Flow",
                "15.7",
                "Authored dialogue graphs and runtime-owned conversation flow with stable node and choice identities, condition-gated choices, delegated effects, deterministic transitions, and persistence.",
                15070,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "ConversationRuntime", "DialogueGraphDefinition", "DialogueFlowRuntime", "DialogueFlowPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("96.1-graph-readiness", "Dialogue graphs register and validate", 10, Step("step15-dialogue-flow-readiness", "Resolve prototype dialogue graphs", DialogueFlowReadiness)),
                    Scenario("96.2-start-and-visible-choices", "Starting a graph enters the canonical node with deterministic choices", 20, Step("step15-dialogue-flow-start", "Start guild counter dialogue flow", DialogueFlowStartAndChoices)),
                    Scenario("96.3-conditions-and-hidden-choices", "Hidden and unavailable choices respect condition context", 30, Step("step15-dialogue-flow-conditions", "Evaluate visible and hidden choices", DialogueFlowConditions)),
                    Scenario("96.4-choice-history-and-idempotence", "Choice selection records history and idempotence without owner mutation", 40, Step("step15-dialogue-flow-choice", "Select a dialogue choice", DialogueFlowChoiceHistory)),
                    Scenario("96.5-required-effect-failure", "Required owner-runtime effects fail atomically when no executor is available", 50, Step("step15-dialogue-flow-effect-failure", "Reject required dialogue effect without executor", DialogueFlowRequiredEffectFailure)),
                    Scenario("96.6-persistence", "Dialogue flow save and restore preserve current node without replay", 60, Step("step15-dialogue-flow-persistence", "Save, restore, and reject corrupt flow payload", DialogueFlowPersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.8.narrative-world-events-triggers-conditions-actions",
                "Narrative and World Events, Triggers, Conditions, and Actions",
                "15.8",
                "Runtime-owned narrative event orchestration with stable authored triggers, condition gates, typed owner-runtime actions, cascade limits, redacted projections, and persistence-safe restore.",
                15080,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "NarrativeEventRuntime", "NarrativeEventDefinition", "NarrativeEventPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("97.1-readiness-and-validation", "Narrative event definitions register and validate", 10, Step("step15-narrative-readiness", "Resolve prototype narrative event definitions", NarrativeReadiness)),
                    Scenario("97.2-location-trigger-quest-action", "Location triggers create delegated quest actions once per scoped actor", 20, Step("step15-narrative-location", "Trigger dungeon entry narrative event", NarrativeLocationQuestAction)),
                    Scenario("97.3-cross-runtime-signals", "Dialogue and knowledge signals route through explicit narrative signals", 30, Step("step15-narrative-signals", "Route dialogue and knowledge narrative signals", NarrativeCrossRuntimeSignals)),
                    Scenario("97.4-hidden-projection-boundaries", "Hidden narrative events redact ordinary projections", 40, Step("step15-narrative-hidden", "Query hidden narrative projections", NarrativeHiddenProjectionBoundaries)),
                    Scenario("97.5-required-action-failure", "Required owner action failures stop execution without fake owner mutation", 50, Step("step15-narrative-required-action", "Reject missing required owner runtime action", NarrativeRequiredActionFailure)),
                    Scenario("97.6-cascade-and-persistence", "Cascades and persistence remain deterministic and restore-safe", 60, Step("step15-narrative-persistence", "Cascade, save, restore, and reject corrupt narrative payload", NarrativeCascadePersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.9.branching-narrative-state-persistent-variables-consequences",
                "Branching Narrative State, Persistent Variables, and Consequences",
                "15.9",
                "Runtime-owned branching narrative state with typed persistent variables, exclusive branches, historical queries, dialogue/event/quest adapters, visibility-safe projections, and persistence-safe restore.",
                15090,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "NarrativeStateRuntime", "NarrativeStateDefinition", "NarrativeStatePersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("98.1-readiness-and-validation", "Narrative state definitions register and validate", 10, Step("step15-narrative-state-readiness", "Resolve prototype narrative state definitions", NarrativeStateReadiness)),
                    Scenario("98.2-exclusive-branch-transitions", "Person-scoped exclusive branches are deterministic", 20, Step("step15-narrative-state-exclusive", "Preview, commit, duplicate, and reject stale branches", NarrativeStateExclusiveBranches)),
                    Scenario("98.3-merge-terminal-history", "Merged and terminal branches preserve historical values", 30, Step("step15-narrative-state-history", "Merge, terminate, and query historical values", NarrativeStateMergeTerminalHistory)),
                    Scenario("98.4-access-dialogue-quest-adapters", "Hidden projections and adapter conditions do not leak state", 40, Step("step15-narrative-state-adapters", "Evaluate hidden state, dialogue, and quest adapters", NarrativeStateAccessAndAdapters)),
                    Scenario("98.5-narrative-event-transition-cascade", "Narrative events request state transitions through the owner runtime", 50, Step("step15-narrative-state-event", "Execute event-driven state transition", NarrativeStateEventTransitionCascade)),
                    Scenario("98.6-persistence-no-replay", "Narrative state persists without replaying consequences", 60, Step("step15-narrative-state-persistence", "Save, restore, and reject corrupt state payload", NarrativeStatePersistenceNoReplay))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.10.quest-chains-narrative-arcs-dependencies-reactive-orchestration",
                "Quest Chains, Narrative Arcs, Dependencies, and Reactive Orchestration",
                "15.10",
                "Runtime-owned narrative arcs coordinate quest chains, branching dependencies, narrative state, narrative events, and persistence-safe orchestration without owning quest, event, or state records.",
                15100,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "NarrativeArcRuntime", "NarrativeArcDefinition", "NarrativeArcPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("99.1-readiness-and-validation", "Narrative arc definitions register and validate", 10, Step("step15-narrative-arc-readiness", "Resolve prototype narrative arc definitions and graph", NarrativeArcReadiness)),
                    Scenario("99.2-state-driven-quest-binding", "State progression activates a chained quest through QuestRuntime", 20, Step("step15-narrative-arc-state-quest", "Complete state-gated stage and bind quest", NarrativeArcStateDrivenQuestBinding)),
                    Scenario("99.3-quest-outcome-branching", "Quest outcomes branch arc stages without owning quest state", 30, Step("step15-narrative-arc-quest-outcome", "Apply completed and failed quest outcome signals", NarrativeArcQuestOutcomeBranching)),
                    Scenario("99.4-parallel-convergence", "Parallel stages converge deterministically", 40, Step("step15-narrative-arc-parallel", "Resolve two of three parallel branches", NarrativeArcParallelConvergence)),
                    Scenario("99.5-event-state-hooks", "Narrative events can request arc progression through explicit hooks", 50, Step("step15-narrative-arc-event-hooks", "Route event action into arc runtime", NarrativeArcEventStateHooks)),
                    Scenario("99.6-persistence-no-replay", "Narrative arcs persist without replaying delegated side effects", 60, Step("step15-narrative-arc-persistence", "Save, restore, redact, and reject corrupt arc payload", NarrativeArcPersistenceNoReplay))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.11.narrative-persistence-history-recovery-scene-integration",
                "Narrative Persistence, Historical Reconstruction, Recovery, and Scene Integration",
                "15.11",
                "Step 15-wide persistence ownership, restore phases, historical reconstruction, recovery diagnostics, visibility-safe timeline queries, and scene-binding readiness contracts.",
                15110,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "Step15NarrativeHistoricalService", "QuestRuntimePersistenceParticipant", "ConversationPersistenceParticipant", "NarrativeArcPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("100.1-manifest-ownership", "Step 15 persistence manifest declares ownership and restore phases", 10, Step("step15-narrative-persistence-manifest", "Build Step 15 persistence ownership manifest", Step15NarrativePersistenceManifestScenario)),
                    Scenario("100.2-historical-reconstruction", "Historical quest, conversation, state, and arc reconstruction is deterministic", 20, Step("step15-narrative-history-query", "Query historical Step 15 state", Step15NarrativeHistoricalReconstruction)),
                    Scenario("100.3-timeline-visibility-pagination", "Unified narrative timeline redacts hidden entries and pages deterministically", 30, Step("step15-narrative-timeline", "Query Step 15 historical timeline", Step15NarrativeTimelineVisibility)),
                    Scenario("100.4-recovery-diagnostics", "Validation separates recoverable derived gaps from authoritative corruption", 40, Step("step15-narrative-recovery", "Validate Step 15 recovery diagnostics", Step15NarrativeRecoveryDiagnostics))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.15.12.quest-dialogue-narrative-event-integration-finalization",
                "Step 15 Quest, Dialogue, Narrative, and Event Integration Finalization",
                "15.12",
                "Final Step 15 ownership, readiness, immutable context projection, validation hardening, scene-binding authority, and Step 16 authoring contract checks.",
                15120,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "Step15NarrativeIntegrationService", "Step15NarrativeHistoricalService", "QuestRuntime", "DialogueFlowRuntime", "NarrativeArcRuntime" },
                scenarios: new[]
                {
                    Scenario("101.1-readiness-ownership", "Aggregate Step 15 readiness and ownership are coherent", 10, Step("step15-integration-readiness", "Build final Step 15 readiness snapshot", Step15IntegrationReadinessOwnership)),
                    Scenario("101.2-context-projection", "Unified narrative context is immutable and bounded", 20, Step("step15-integration-context", "Build player narrative context projection", Step15IntegrationContextProjection)),
                    Scenario("101.3-validation-boundaries", "Final validator catches cross-runtime and scene-binding violations", 30, Step("step15-integration-validation", "Validate deliberate integration drift", Step15IntegrationValidationBoundaries)),
                    Scenario("101.4-step16-contract", "Step 16 authoring contract is explicit", 40, Step("step15-integration-step16-contract", "Build Step 16 authoring contract", Step15IntegrationStep16Contract))
                }), out _);
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Quests | TestLabRuntimeArea.WorldLocations | TestLabRuntimeArea.KnowledgeHistory,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: RequiredQuestDefinitionIds);
        }

        private static ITestLabScenarioStep Step(string id, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(id, displayName, action);
        }

        private static TestLabAutomationStepResult Step15NarrativePersistenceManifestScenario(TestLabAutomationContext context)
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot snapshot = Step15NarrativeAutomationSnapshot();
            Step15NarrativePersistenceManifest manifest = service.BuildManifest(snapshot, Step15NarrativeReadinessState.Ready);
            Step15NarrativePersistenceManifest repeat = service.BuildManifest(snapshot.Clone(), Step15NarrativeReadinessState.Ready);
            Step15NarrativeValidationReport validation = service.Validate(snapshot);

            bool ownership = manifest.Ownership.Where(entry => !entry.Derived).All(entry => !string.IsNullOrWhiteSpace(entry.ParticipantKey))
                && manifest.Ownership.Any(entry => entry.Category == "Scene bindings" && entry.Derived);
            bool phases = manifest.RestorePhases.SequenceEqual(Enum.GetValues(typeof(Step15NarrativeRestorePhase)).Cast<Step15NarrativeRestorePhase>());
            bool counts = manifest.RecordCounts.TryGetValue("quests", out int quests) && quests == 1
                && manifest.RecordCounts.TryGetValue("narrativeArcs", out int arcs) && arcs == 1;
            bool valid = ownership && phases && counts && validation.Succeeded && manifest.DeterministicFingerprint == repeat.DeterministicFingerprint;

            return TestLabAssertions.True("step15-narrative-persistence-manifest", "Step 15 manifest owns authoritative categories and restore phases", valid, $"Ownership={ownership} Phases={phases} Counts={counts} Validation={validation.Succeeded} Fingerprint={manifest.DeterministicFingerprint}");
        }

        private static TestLabAutomationStepResult Step15NarrativeHistoricalReconstruction(TestLabAutomationContext context)
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot snapshot = Step15NarrativeAutomationSnapshot();
            HistoricalQuestSnapshot quest = service.GetQuestAt(snapshot, "quest.prototype.automation", 12d);
            HistoricalConversationSnapshot conversation = service.GetConversationAt(snapshot, "conversation.prototype.automation", 8d);
            HistoricalNarrativeStateSnapshot state = service.GetNarrativeStateAt(snapshot, snapshot.NarrativeStates.states[0].narrativeStateId, 9d);
            HistoricalNarrativeArcSnapshot arc = service.GetNarrativeArcAt(snapshot, "narrative-arc.prototype.automation", 12d);
            state.VariableValues.TryGetValue(PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyVariableId, out string stage);

            bool valid = quest.Existed
                && quest.Outcome == QuestTerminalOutcomeKind.Completed
                && quest.Objectives.Any(objective => objective.Satisfied)
                && conversation.ActiveDialogueNodeId == "node.report"
                && conversation.LatestChoiceId == "choice.accept"
                && stage == PrototypeNarrativeStateDefinitionFactory.GuildLoyalValueId
                && arc.Lifecycle == NarrativeArcLifecycle.Completed
                && arc.BoundQuestIds.SequenceEqual(new[] { snapshot.Quests.quests[0].questId });

            return TestLabAssertions.True("step15-narrative-history-query", "Step 15 historical reconstruction reads owner histories without replay", valid, $"Quest={quest.Existed}/{quest.Outcome} Objective={quest.Objectives.Count} Conversation={conversation.ActiveDialogueNodeId}/{conversation.LatestChoiceId} State={stage} Arc={arc.Lifecycle}/{string.Join(",", arc.BoundQuestIds)}");
        }

        private static TestLabAutomationStepResult Step15NarrativeTimelineVisibility(TestLabAutomationContext context)
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot snapshot = Step15NarrativeAutomationSnapshot();
            NarrativeTimelinePage publicPage = service.QueryTimeline(snapshot, new NarrativeTimelineQuery
            {
                AccessMode = NarrativeHistoricalAccessMode.PersonSafe,
                RequesterPersonId = "person.prototype.hero",
                Limit = 500
            });
            NarrativeTimelinePage first = service.QueryTimeline(snapshot, new NarrativeTimelineQuery { AccessMode = NarrativeHistoricalAccessMode.Development, Limit = 2 });
            NarrativeTimelinePage second = service.QueryTimeline(snapshot, new NarrativeTimelineQuery { AccessMode = NarrativeHistoricalAccessMode.Development, Limit = 2, AfterCursor = first.NextCursor });
            string hiddenNarrativeEventId = snapshot.NarrativeEvents?.events?.FirstOrDefault(value => value.visibility == NarrativeEventVisibility.Hidden)?.narrativeEventId ?? string.Empty;

            bool hiddenRedacted = publicPage.Entries.All(entry => !entry.Hidden && !string.Equals(entry.NarrativeEventId, hiddenNarrativeEventId, StringComparison.Ordinal));
            bool paged = first.HasMore && first.Entries.Count == 2 && second.Entries.Count > 0 && string.CompareOrdinal(second.Entries[0].Cursor, first.NextCursor) > 0;
            bool deterministic = service.QueryTimeline(snapshot.Clone(), new NarrativeTimelineQuery { AccessMode = NarrativeHistoricalAccessMode.Development, Limit = 500 }).Entries.Select(entry => entry.Cursor)
                .SequenceEqual(service.QueryTimeline(snapshot, new NarrativeTimelineQuery { AccessMode = NarrativeHistoricalAccessMode.Development, Limit = 500 }).Entries.Select(entry => entry.Cursor));
            bool valid = hiddenRedacted && paged && deterministic;

            return TestLabAssertions.True("step15-narrative-timeline", "Step 15 unified timeline is visibility-safe and deterministic", valid, $"Public={publicPage.Entries.Count} HiddenRedacted={hiddenRedacted} Paged={paged} Deterministic={deterministic}");
        }

        private static TestLabAutomationStepResult Step15NarrativeRecoveryDiagnostics(TestLabAutomationContext context)
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot snapshot = Step15NarrativeAutomationSnapshot();
            snapshot.DialogueFlows.flows[0].currentNodeId = "node.missing-visit";
            snapshot.NarrativeArcs.arcs.Add(snapshot.NarrativeArcs.arcs[0].Clone());

            Step15NarrativeValidationReport report = service.Validate(snapshot);
            bool recoverable = report.RecoveryIssues.Any(issue => issue.Kind == NarrativeRecoveryIssueKind.StaleDerivedIndex && issue.Recoverable);
            bool hardFailure = report.RecoveryIssues.Any(issue => issue.Kind == NarrativeRecoveryIssueKind.AuthoritativeCorruption && !issue.Recoverable);
            bool valid = !report.Succeeded && recoverable && hardFailure;

            return TestLabAssertions.True("step15-narrative-recovery", "Step 15 validation distinguishes repairable projections from authoritative corruption", valid, $"Succeeded={report.Succeeded} Errors={report.Errors.Count} Recoverable={recoverable} Hard={hardFailure} Issues={report.RecoveryIssues.Count}");
        }

        private static TestLabAutomationStepResult Step15IntegrationReadinessOwnership(TestLabAutomationContext context)
        {
            Step15NarrativeIntegrationService service = new Step15NarrativeIntegrationService();
            Step15NarrativePersistenceSnapshot snapshot = Step15NarrativeCoherentAutomationSnapshot();
            Step15NarrativeReadinessSnapshot readiness = service.BuildReadiness(snapshot, new Step15NarrativeSceneBindingSummary
            {
                SceneId = "scene.prototype",
                LocationBindingCount = 4,
                InteractionPointBindingCount = 8,
                QuestSourceBindingCount = 2,
                PresentationOnlyBindingCount = 14
            });

            bool owners = readiness.Manifest.Ownership.Where(entry => !entry.Derived).Select(entry => entry.Category).Distinct(StringComparer.Ordinal).Count()
                == readiness.Manifest.Ownership.Count(entry => !entry.Derived);
            bool requiredReady = readiness.Runtimes.Where(item => item.Requirement == Step15NarrativeRuntimeRequirement.Required).All(item => item.State == Step15NarrativeReadinessState.Ready);
            bool sceneOptional = readiness.Runtimes.Any(item => item.ComponentId == "SceneBindingState" && item.Requirement == Step15NarrativeRuntimeRequirement.Optional);
            bool valid = readiness.Ready && readiness.Validation.Succeeded && owners && requiredReady && sceneOptional;

            return TestLabAssertions.True("step15-integration-readiness", "Step 15 aggregate readiness uses owner runtimes and keeps scene binding optional", valid, $"Ready={readiness.State} Errors={readiness.Validation.ErrorCount} Warnings={readiness.Validation.WarningCount} Owners={owners} Required={requiredReady} SceneOptional={sceneOptional}");
        }

        private static TestLabAutomationStepResult Step15IntegrationContextProjection(TestLabAutomationContext context)
        {
            Step15NarrativeIntegrationService service = new Step15NarrativeIntegrationService();
            Step15NarrativePersistenceSnapshot snapshot = Step15NarrativeActiveContextAutomationSnapshot();
            Step15NarrativeContextSnapshot projection = service.BuildNarrativeContext(snapshot, new Step15NarrativeContextQuery
            {
                RequesterPersonId = "person.prototype.hero",
                PersonId = "person.prototype.hero",
                LocationId = "location.prototype.guild",
                OrganizationId = "organization.prototype.adventurers-guild",
                WorldTime = 9d,
                Limit = 25
            });

            snapshot.Participation.assignments[0].assignmentId = "assignment.prototype.mutated-after-projection";
            snapshot.DialogueFlows.flows[0].currentNodeId = "node.mutated-after-projection";

            bool active = projection.ActiveAssignments.Any(entry => entry.PrimaryId == "assignment.prototype.automation");
            bool objective = projection.ActiveObjectives.Any(entry => entry.PrimaryId == "objective.prototype.automation");
            bool turnIn = projection.TurnInReadyQuests.Any(entry => entry.RelatedId == "quest.prototype.automation");
            bool source = projection.AvailableQuestSources.Any(entry => entry.PrimaryId == "quest-source.prototype.automation");
            bool dialogue = projection.CurrentDialogueNodes.Any(entry => entry.PrimaryId == "node.report");
            bool immutable = projection.ActiveAssignments.All(entry => entry.PrimaryId != "assignment.prototype.mutated-after-projection")
                && projection.CurrentDialogueNodes.All(entry => entry.PrimaryId != "node.mutated-after-projection");
            bool valid = active && objective && turnIn && source && dialogue && immutable;

            return TestLabAssertions.True("step15-integration-context", "Step 15 narrative context projection is bounded, immutable, and owner-derived", valid, $"Active={active} Objective={objective} TurnIn={turnIn} Source={source} Dialogue={dialogue} Immutable={immutable} Entries={projection.TotalEntries}");
        }

        private static TestLabAutomationStepResult Step15IntegrationValidationBoundaries(TestLabAutomationContext context)
        {
            Step15NarrativeIntegrationService service = new Step15NarrativeIntegrationService();
            Step15NarrativePersistenceSnapshot corrupt = Step15NarrativeAutomationSnapshot();
            corrupt.Outcomes.rewardEntitlements.Add(corrupt.Outcomes.rewardEntitlements[0].Clone());
            corrupt.NarrativeEvents.events[0].cascadeDepth = Step15NarrativeIntegrationService.DefaultCascadeBudget + 1;
            corrupt.NarrativeEvents.events[0].actionExecutions[0].targetOwnerRuntime = "reflection:UnsafeInvoke";

            Step15NarrativeIntegrationValidationReport report = service.ValidateFinalization(corrupt, new Step15NarrativeSceneBindingSummary
            {
                SceneId = "scene.prototype",
                AuthoritativeMutationBindingCount = 1
            });

            bool participation = report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.QuestParticipation);
            bool rewards = report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.Rewards);
            bool cascade = report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.Performance);
            bool security = report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.NarrativeEvents);
            bool scene = report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.SceneBinding);
            bool valid = !report.Succeeded && participation && rewards && cascade && security && scene;

            return TestLabAssertions.True("step15-integration-validation", "Step 15 final validator catches coherence, duplicate reward, cascade, security, and scene authority violations", valid, $"Succeeded={report.Succeeded} Errors={report.ErrorCount} Participation={participation} Rewards={rewards} Cascade={cascade} Security={security} Scene={scene}");
        }

        private static TestLabAutomationStepResult Step15IntegrationStep16Contract(TestLabAutomationContext context)
        {
            Step15NarrativeIntegrationService service = new Step15NarrativeIntegrationService();
            Step15NarrativeAuthoringContract contract = service.BuildStep16AuthoringContract();

            string[] required = { "quests", "quest-sources", "objectives", "outcomes-rewards", "conversation-dialogue", "narrative-events", "narrative-state", "narrative-arcs", "history-persistence" };
            bool sections = required.All(id => contract.Sections.Any(section => section.SectionId == id));
            bool populated = contract.Sections.All(section => section.AuthorableConcepts.Count > 0 && section.ValidationGuarantees.Count > 0);
            bool valid = sections && populated;

            return TestLabAssertions.True("step15-integration-step16-contract", "Step 16 can rely on explicit Step 15 authoring contracts", valid, $"Sections={contract.Sections.Count} Required={sections} Populated={populated}");
        }

        private static TestLabAutomationStepResult ReadinessAndDefinitions(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool hasAll = PrototypeQuestDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out QuestDefinition _));
            bool metadata = registry.TryGet(PrototypeQuestDefinitionFactory.DynamicBountyDefinitionId, out QuestDefinition bounty)
                && bounty.AllowDynamicInstances
                && bounty.Category == QuestCategory.BountyPlaceholder
                && bounty.DefaultTagIds.Contains("dynamic");
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (QuestDefinition definition in PrototypeQuestDefinitionFactory.CreateMissingQuestDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            bool valid = hasAll && metadata && report.ErrorCount == 0;
            return TestLabAssertions.True("step15-quest-readiness", "Quest definitions register and validate", valid, $"Definitions={hasAll} Metadata={metadata} Errors={report.ErrorCount} Warnings={report.WarningCount}");
        }

        private static TestLabAutomationStepResult UniqueAndRepeatable(TestLabAutomationContext context)
        {
            QuestRuntime runtime = Runtime(context);
            QuestRuntimeOperationResult first = CreateGuildPosting(runtime, "unique-a", "quest.prototype.guild.unique");
            QuestRuntimeOperationResult duplicateUnique = CreateGuildPosting(runtime, "unique-b", "quest.prototype.guild.unique-b");
            QuestRuntimeOperationResult dynamicA = CreateDynamicBounty(runtime, "dynamic-a", "wolf");
            QuestRuntimeOperationResult dynamicB = CreateDynamicBounty(runtime, "dynamic-b", "slime");
            bool valid = first.Succeeded
                && duplicateUnique.Status == QuestRuntimeOperationStatus.UniqueQuestAlreadyExists
                && dynamicA.Succeeded
                && dynamicB.Succeeded
                && dynamicA.Snapshot.QuestId != dynamicB.Snapshot.QuestId
                && runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count == 3;
            return TestLabAssertions.True("step15-quest-identity", "Unique and dynamic quest identity rules are enforced", valid, $"First={first.Status} UniqueDuplicate={duplicateUnique.Status} Dynamic={dynamicA.Status}/{dynamicB.Status} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult ReferenceBoundaries(TestLabAutomationContext context)
        {
            QuestRuntime runtime = Runtime(context);
            QuestRuntimeOperationResult create = runtime.CreateQuest(new QuestCreateRequest
            {
                transactionId = "tx.quest.references",
                questId = "quest.prototype.civic-investigation.001",
                questDefinitionId = PrototypeQuestDefinitionFactory.CivicInvestigationDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Government, issuerId = "government.prototype.civic", actingPersonId = "person.prototype.mayor" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.player" },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Government, locationId = "location.prototype.civic-office", interactionPointId = "interaction-point.prototype.civic-desk" },
                subjectLinks = new[]
                {
                    Subject("person.prototype.witness", QuestSubjectRole.Person, InformationSubjectType.PersonIdentity),
                    Subject("location.prototype.basement-prison", QuestSubjectRole.Location, InformationSubjectType.Location),
                    Subject("knowledge.prototype.quest-incident", QuestSubjectRole.Incident, InformationSubjectType.KnowledgeRecord)
                },
                tagIds = new[] { "civic", "investigation" },
                createdWorldTime = 15d
            });
            QuestSnapshot snapshot = create.Snapshot;
            bool subject = snapshot != null
                && snapshot.SubjectLinks.Count == 3
                && snapshot.CreateInformationSubject().tags.Contains(QuestInformationSubject.QuestTag)
                && snapshot.Issuer.issuerId == "government.prototype.civic"
                && snapshot.IntendedRecipient.recipientId == "person.prototype.player"
                && snapshot.Origin.interactionPointId == "interaction-point.prototype.civic-desk";
            bool valid = create.Succeeded && subject && runtime.Events.Count == 1;
            return TestLabAssertions.True("step15-quest-references", "Quest issuer, recipient, origin, and subjects are explicit references", valid, $"Create={create.Status} Links={snapshot?.SubjectLinks.Count} Events={runtime.Events.Count}");
        }

        private static TestLabAutomationStepResult VisibilitySafeQuery(TestLabAutomationContext context)
        {
            QuestRuntime runtime = Runtime(context);
            QuestRuntimeOperationResult publicQuest = CreateGuildPosting(runtime, "visible", "quest.prototype.guild.visible");
            QuestRuntimeOperationResult hiddenQuest = runtime.CreateQuest(new QuestCreateRequest
            {
                transactionId = "tx.quest.hidden",
                questId = "quest.prototype.hidden.dungeon",
                questDefinitionId = PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Anonymous },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Discovery },
                subjectLinks = new[] { Subject("location.prototype.dungeon-entry", QuestSubjectRole.Location, InformationSubjectType.Location) },
                createdWorldTime = 20d
            });
            int publicCount = runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PublicOnly }).Count;
            int privilegedCount = runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count;
            bool valid = publicQuest.Succeeded && hiddenQuest.Succeeded && publicCount == 1 && privilegedCount == 2;
            return TestLabAssertions.True("step15-quest-visibility", "Hidden quests do not leak into ordinary query results", valid, $"Public={publicCount} Privileged={privilegedCount} HiddenStatus={hiddenQuest.Status}");
        }

        private static TestLabAutomationStepResult LifecycleRevisionIdempotence(TestLabAutomationContext context)
        {
            QuestRuntime runtime = Runtime(context);
            QuestRuntimeOperationResult create = CreateDynamicBounty(runtime, "lifecycle-create", "boar");
            long createdRevision = runtime.Revision;
            QuestRuntimeOperationResult stale = runtime.TransitionLifecycle(new QuestLifecycleTransitionRequest { transactionId = "tx.quest.lifecycle.stale", questId = create.Snapshot?.QuestId, targetState = QuestRuntimeLifecycleState.Suspended, expectedRevision = createdRevision - 1L });
            QuestRuntimeOperationResult retire = runtime.TransitionLifecycle(new QuestLifecycleTransitionRequest { transactionId = "tx.quest.lifecycle.retire", questId = create.Snapshot?.QuestId, targetState = QuestRuntimeLifecycleState.Retired, expectedRevision = createdRevision, worldTime = 30d });
            QuestRuntimeOperationResult duplicate = runtime.TransitionLifecycle(new QuestLifecycleTransitionRequest { transactionId = "tx.quest.lifecycle.retire", questId = create.Snapshot?.QuestId, targetState = QuestRuntimeLifecycleState.Retired, worldTime = 30d });
            bool hiddenFromActive = runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count == 0;
            bool visibleWithRetired = runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic, includeRetired = true }).Count == 1;
            bool valid = create.Succeeded
                && stale.Status == QuestRuntimeOperationStatus.RevisionConflict
                && retire.Succeeded
                && duplicate.Duplicate
                && hiddenFromActive
                && visibleWithRetired
                && runtime.Events.Count == 2;
            return TestLabAssertions.True("step15-quest-lifecycle", "Lifecycle transitions are revision-safe and idempotent", valid, $"Create={create.Status} Stale={stale.Status} Retire={retire.Status} Duplicate={duplicate.Status} Events={runtime.Events.Count}");
        }

        private static TestLabAutomationStepResult PersistenceWorldIsolation(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime runtime = Runtime(context);
            QuestRuntimeOperationResult create = CreateDynamicBounty(runtime, "persist-create", "goblin");
            QuestRuntimePersistenceParticipant participant = new QuestRuntimePersistenceParticipant(runtime, () => registry, PersistenceService.LocalWorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            QuestRuntime restored = Runtime(context);
            QuestRuntimePersistenceParticipant restoredParticipant = new QuestRuntimePersistenceParticipant(restored, () => registry, PersistenceService.LocalWorldId);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestRuntimePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            int restoredEvents = restored.Events.Count;
            QuestRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.worldId = "world.other";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestRuntimePersistenceParticipant.CurrentParticipantSchemaVersion);
            bool unchanged = restored.Count == 1 && restored.Events.Count == restoredEvents;
            bool valid = create.Succeeded && save.Succeeded && prepare.Succeeded && commit.Succeeded && rejected.Succeeded == false && unchanged;
            return TestLabAssertions.True("step15-quest-persistence", "Quest persistence round-trips and rejects wrong-world payloads without mutation", valid, $"Create={create.Status} Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Reject={rejected.Succeeded} Count={restored.Count} Events={restored.Events.Count}");
        }

        private static TestLabAutomationStepResult ParticipationPolicyReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool guild = registry.TryGet(PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, out QuestDefinition guildDefinition)
                && guildDefinition.AssignmentPolicy == QuestAssignmentPolicy.Exclusive
                && guildDefinition.ConsentPolicy == QuestConsentPolicy.ExplicitRecipientConsentRequired
                && guildDefinition.EligibilityRequirementGroups.Count == 1;
            bool bounty = registry.TryGet(PrototypeQuestDefinitionFactory.DynamicBountyDefinitionId, out QuestDefinition bountyDefinition)
                && bountyDefinition.AssignmentPolicy == QuestAssignmentPolicy.CapacityLimited
                && bountyDefinition.AssignmentCapacity == 4;
            return TestLabAssertions.True("step15-participation-readiness", "Quest participation policies register and validate", guild && bounty, $"Guild={guild} Bounty={bounty}");
        }

        private static TestLabAutomationStepResult AvailabilityEligibility(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestRuntimeOperationResult create = CreateGuildPosting(quests, "participation-eligibility", "quest.prototype.guild.participation-eligibility");

            QuestAvailabilityResult availability = participation.EvaluateAvailability(create.Snapshot?.QuestId, EligibleContext("person.prototype.player"));
            QuestEligibilityResult missing = participation.EvaluateEligibility(create.Snapshot?.QuestId, new QuestEligibilityContext { personId = "person.prototype.player", privilegedDiagnostics = true, worldTime = 1d });
            QuestEligibilityResult eligible = participation.EvaluateEligibility(create.Snapshot?.QuestId, EligibleContext("person.prototype.player"));

            bool valid = create.Succeeded
                && availability.Available
                && missing.Eligible == false
                && missing.VisibleFailureReasons.Count > 0
                && eligible.Eligible;
            return TestLabAssertions.True("step15-participation-eligibility", "Availability and eligibility remain separate and deterministic", valid, $"Create={create.Status} Available={availability.State} Missing={missing.Eligible} Eligible={eligible.Eligible}");
        }

        private static TestLabAutomationStepResult OfferAndAcceptance(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestRuntimeOperationResult create = CreateGuildPosting(quests, "participation-offer", "quest.prototype.guild.participation-offer");

            QuestParticipationOperationResult preview = participation.CreateOffer(OfferRequest(create.Snapshot?.QuestId, "person.prototype.player", EligibleContext("person.prototype.player"), "tx.quest.participation.preview", preview: true));
            QuestParticipationOperationResult offer = participation.CreateOffer(OfferRequest(create.Snapshot?.QuestId, "person.prototype.player", EligibleContext("person.prototype.player"), "tx.quest.participation.offer"));
            QuestParticipationOperationResult missingConsent = participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.participation.no-consent", offerId = offer.Offer?.OfferId, personId = "person.prototype.player", eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 2d });
            QuestParticipationOperationResult accept = participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.participation.accept", offerId = offer.Offer?.OfferId, personId = "person.prototype.player", explicitConsent = true, consentRecordId = "consent.prototype.player.guild", eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 2d });
            QuestParticipationOperationResult duplicate = participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.participation.accept", offerId = offer.Offer?.OfferId, personId = "person.prototype.player", explicitConsent = true, eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 2d });

            bool valid = create.Succeeded
                && preview.Status == QuestParticipationOperationStatus.Preview
                && offer.Succeeded
                && missingConsent.Status == QuestParticipationOperationStatus.ConsentRequired
                && accept.Succeeded
                && duplicate.Duplicate
                && participation.OfferCount == 1
                && participation.AssignmentCount == 1;
            return TestLabAssertions.True("step15-participation-offer", "Offer creation and acceptance are consent-gated and idempotent", valid, $"Preview={preview.Status} Offer={offer.Status} Consent={missingConsent.Status} Accept={accept.Status} Duplicate={duplicate.Status} Offers={participation.OfferCount} Assignments={participation.AssignmentCount}");
        }

        private static TestLabAutomationStepResult ExclusiveCapacity(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestRuntimeOperationResult create = CreateGuildPosting(quests, "participation-exclusive", "quest.prototype.guild.participation-exclusive");
            QuestParticipationOperationResult firstOffer = participation.CreateOffer(OfferRequest(create.Snapshot?.QuestId, "person.prototype.first", EligibleContext("person.prototype.first"), "tx.quest.participation.exclusive.first-offer"));
            QuestParticipationOperationResult secondOffer = participation.CreateOffer(OfferRequest(create.Snapshot?.QuestId, "person.prototype.second", EligibleContext("person.prototype.second"), "tx.quest.participation.exclusive.second-offer"));
            QuestParticipationOperationResult first = participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.participation.exclusive.first", offerId = firstOffer.Offer?.OfferId, personId = "person.prototype.first", explicitConsent = true, eligibilityContext = EligibleContext("person.prototype.first"), worldTime = 3d });
            QuestParticipationOperationResult second = participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.participation.exclusive.second", offerId = secondOffer.Offer?.OfferId, personId = "person.prototype.second", explicitConsent = true, eligibilityContext = EligibleContext("person.prototype.second"), worldTime = 3d });

            bool valid = firstOffer.Succeeded
                && secondOffer.Succeeded
                && first.Succeeded
                && second.Status == QuestParticipationOperationStatus.Unavailable
                && participation.AssignmentCount == 1;
            return TestLabAssertions.True("step15-participation-exclusive", "Exclusive assignment capacity is revalidated at acceptance", valid, $"First={first.Status} Second={second.Status} Assignments={participation.AssignmentCount}");
        }

        private static TestLabAutomationStepResult AbandonmentRelease(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestRuntimeOperationResult create = CreateGuildPosting(quests, "participation-abandon", "quest.prototype.guild.participation-abandon");
            QuestParticipationOperationResult offer = participation.CreateOffer(OfferRequest(create.Snapshot?.QuestId, "person.prototype.player", EligibleContext("person.prototype.player"), "tx.quest.participation.abandon.offer"));
            QuestParticipationOperationResult accept = participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.participation.abandon.accept", offerId = offer.Offer?.OfferId, personId = "person.prototype.player", explicitConsent = true, eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 4d });
            QuestAvailabilityResult claimed = participation.EvaluateAvailability(create.Snapshot?.QuestId, EligibleContext("person.prototype.other"));
            QuestParticipationOperationResult abandon = participation.AbandonAssignment(new QuestAssignmentLifecycleRequest { transactionId = "tx.quest.participation.abandon", assignmentId = accept.Assignment?.AssignmentId, actingPersonId = "person.prototype.player", explicitConsent = true, worldTime = 5d });
            QuestAvailabilityResult after = participation.EvaluateAvailability(create.Snapshot?.QuestId, EligibleContext("person.prototype.other"));

            bool valid = accept.Succeeded
                && claimed.State == QuestAvailabilityState.ExclusivelyAssigned
                && abandon.Succeeded
                && after.Available;
            return TestLabAssertions.True("step15-participation-abandonment", "Abandonment releases configured exclusive capacity", valid, $"Accept={accept.Status} Claimed={claimed.State} Abandon={abandon.Status} After={after.State}");
        }

        private static TestLabAutomationStepResult VisibilityAndPersistence(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestRuntimeOperationResult create = quests.CreateQuest(new QuestCreateRequest
            {
                transactionId = "tx.quest.participation.hidden.create",
                questId = "quest.prototype.hidden.participation",
                questDefinitionId = PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Anonymous },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Discovery },
                subjectLinks = new[] { Subject("location.prototype.secret-dungeon-entry", QuestSubjectRole.Location, InformationSubjectType.Location) },
                createdWorldTime = 5d
            });
            QuestParticipationOperationResult offer = participation.CreateOffer(new QuestOfferRequest
            {
                transactionId = "tx.quest.participation.hidden.offer",
                questId = create.Snapshot?.QuestId,
                recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.scout" },
                offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                eligibilityContext = new QuestEligibilityContext { personId = "person.prototype.scout", privilegedDiagnostics = true, worldTime = 5d },
                channel = QuestOfferChannel.NarrativeEventPlaceholder,
                worldTime = 5d
            });
            int publicOffers = participation.QueryOffers(new QuestOfferQuery { access = QuestVisibilityAccess.PublicOnly }).Count;
            int privilegedOffers = participation.QueryOffers(new QuestOfferQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count;
            QuestParticipationRuntimePersistenceParticipant participant = new QuestParticipationRuntimePersistenceParticipant(participation, () => quests, () => registry, PersistenceService.LocalWorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            QuestParticipationRuntime restored = Participation(quests, registry);
            QuestParticipationRuntimePersistenceParticipant restoredParticipant = new QuestParticipationRuntimePersistenceParticipant(restored, () => quests, () => registry, PersistenceService.LocalWorldId);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestParticipationRuntimePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            QuestParticipationRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.worldId = "world.other";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestParticipationRuntimePersistenceParticipant.CurrentParticipantSchemaVersion);

            bool valid = create.Succeeded
                && offer.Succeeded
                && publicOffers == 0
                && privilegedOffers == 1
                && save.Succeeded
                && prepare.Succeeded
                && commit.Succeeded
                && rejected.Succeeded == false
                && restored.OfferCount == 1;
            return TestLabAssertions.True("step15-participation-persistence", "Hidden offers do not leak and persistence validates before restore", valid, $"Offer={offer.Status} Public={publicOffers} Privileged={privilegedOffers} Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Reject={rejected.Succeeded} Restored={restored.OfferCount}");
        }

        private static TestLabAutomationStepResult ObjectiveReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool guild = registry.TryGet(PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, out QuestDefinition guildDefinition)
                && guildDefinition.ObjectiveDefinitions.Count == 4
                && guildDefinition.ObjectiveDefinitions.Any(objective => objective.progressModel == QuestObjectiveProgressModel.Counter);
            bool delivery = registry.TryGet(PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId, out QuestDefinition deliveryDefinition)
                && deliveryDefinition.ObjectiveDefinitions.Any(objective => objective.progressModel == QuestObjectiveProgressModel.QuantityCurrent)
                && deliveryDefinition.ObjectiveGroups.Count == 1;
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (QuestDefinition definition in PrototypeQuestDefinitionFactory.CreateMissingQuestDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            bool valid = guild && delivery && report.ErrorCount == 0;
            return TestLabAssertions.True("step15-objective-readiness", "Quest objective definitions register and validate", valid, $"Guild={guild} Delivery={delivery} Errors={report.ErrorCount} Warnings={report.WarningCount}");
        }

        private static TestLabAutomationStepResult ObjectiveInstantiation(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestObjectiveProgressRuntime objectives = Objectives(quests, participation, registry);
            QuestAssignmentSnapshot assignment = AcceptedGuildAssignment(quests, participation, "objective-instantiate");

            QuestObjectiveOperationResult preview = objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objective.preview", preview: true);
            QuestObjectiveOperationResult instantiate = objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objective.instantiate");
            QuestObjectiveOperationResult duplicate = objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objective.instantiate-again");
            QuestAssignmentObjectiveSummary summary = objectives.SummarizeAssignment(assignment.AssignmentId, QuestVisibilityAccess.PrivilegedDiagnostic);

            bool valid = preview.Status == QuestObjectiveOperationStatus.Preview
                && instantiate.Succeeded
                && duplicate.Duplicate
                && instantiate.Objectives.Count == 4
                && summary.CompletionCandidate == false
                && objectives.ObjectiveCount == 4;
            return TestLabAssertions.True("step15-objective-instantiate", "Accepted assignments instantiate stable objective runtime records without completing quests", valid, $"Preview={preview.Status} Instantiate={instantiate.Status} Duplicate={duplicate.Status} Count={objectives.ObjectiveCount} Candidate={summary.CompletionCandidate}");
        }

        private static TestLabAutomationStepResult ObjectiveEventSequence(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestObjectiveProgressRuntime objectives = Objectives(quests, participation, registry);
            QuestAssignmentSnapshot assignment = AcceptedGuildAssignment(quests, participation, "objective-events");
            objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objective.events.instantiate");

            QuestObjectiveOperationResult early = objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "source.quest.objective.early"));
            QuestObjectiveOperationResult counter = objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", "source.quest.objective.counter"));
            QuestObjectiveOperationResult duplicate = objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", "source.quest.objective.counter"));
            QuestObjectiveOperationResult dungeon = objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.VisitLocation, "location.prototype.dungeon-entry", "source.quest.objective.dungeon", InformationSubjectType.Location));
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "source.quest.objective.defeat1"));
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "source.quest.objective.defeat2"));
            QuestObjectiveOperationResult defeat3 = objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "source.quest.objective.defeat3"));
            QuestObjectiveSnapshot defeat = objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, category = QuestObjectiveCategory.DefeatCount, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();

            bool valid = early.Succeeded == false
                && counter.Succeeded
                && duplicate.Status == QuestObjectiveOperationStatus.AlreadyCounted
                && dungeon.Succeeded
                && defeat3.Succeeded
                && defeat.CurrentValue == 3
                && defeat.Satisfied;
            return TestLabAssertions.True("step15-objective-events", "Committed objective signals unlock prerequisites and deduplicate source events", valid, $"Early={early.Status} Counter={counter.Status} Duplicate={duplicate.Status} Dungeon={dungeon.Status} Defeat={defeat.CurrentValue}/{defeat.TargetValue} Satisfied={defeat.Satisfied}");
        }

        private static TestLabAutomationStepResult ObjectiveCurrentState(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestObjectiveProgressRuntime objectives = Objectives(quests, participation, registry);
            QuestAssignmentSnapshot assignment = AcceptedDeliveryAssignment(quests, participation, "objective-state");
            objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objective.state.instantiate");

            QuestObjectiveOperationResult state = objectives.ReconcileState(new QuestObjectiveStateContext
            {
                assignmentId = assignment.AssignmentId,
                personId = assignment.AssigneePersonId,
                worldTime = 3d,
                facts = new QuestObjectiveStateFactSet(new[] { ObjectiveFact(QuestObjectiveCategory.PossessItem, "item.prototype.merchant-parcel", 1) })
            });
            QuestObjectiveOperationResult collect = objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.ObtainItem, "item.prototype.merchant-parcel", "source.quest.objective.collect"));
            QuestObjectiveSnapshot possess = objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, objectiveDefinitionId = "quest-objective-definition.prototype.delivery.possess-parcel", access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();
            QuestObjectiveSnapshot collected = objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, objectiveDefinitionId = "quest-objective-definition.prototype.delivery.collect-parcel", access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();

            bool valid = state.Succeeded
                && collect.Succeeded
                && possess.Satisfied
                && collected.Satisfied
                && collected.CountedSourceEventIds.Contains("source.quest.objective.collect");
            return TestLabAssertions.True("step15-objective-state", "Current-state and cumulative event objectives remain distinct", valid, $"State={state.Status} Collect={collect.Status} Possess={possess.CurrentValue}/{possess.TargetValue} Collected={collected.Satisfied}");
        }

        private static TestLabAutomationStepResult ObjectiveHiddenVisibility(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestObjectiveProgressRuntime objectives = Objectives(quests, participation, registry);
            QuestAssignmentSnapshot assignment = AcceptedHiddenAssignment(quests, participation, "objective-hidden");
            objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objective.hidden.instantiate");

            QuestObjectiveOperationResult discover = objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.DiscoverLocation, "location.prototype.secret-dungeon-entry", "source.quest.objective.hidden", InformationSubjectType.Location));
            int publicCount = objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PublicOnly }).Count;
            int privilegedCount = objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count;
            QuestAssignmentObjectiveSummary summary = objectives.SummarizeAssignment(assignment.AssignmentId, QuestVisibilityAccess.PublicOnly);

            bool valid = discover.Succeeded
                && publicCount == 0
                && privilegedCount == 2
                && summary.HiddenCountsRedacted
                && summary.RequiredRemaining == -1;
            return TestLabAssertions.True("step15-objective-hidden", "Hidden objective progress does not leak through ordinary views", valid, $"Discover={discover.Status} Public={publicCount} Privileged={privilegedCount} Redacted={summary.HiddenCountsRedacted}");
        }

        private static TestLabAutomationStepResult ObjectivePersistence(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestObjectiveProgressRuntime objectives = Objectives(quests, participation, registry);
            QuestAssignmentSnapshot assignment = AcceptedGuildAssignment(quests, participation, "objective-persist");
            objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objective.persist.instantiate");
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", "source.quest.objective.persist"));
            QuestObjectiveProgressPersistenceParticipant participant = new QuestObjectiveProgressPersistenceParticipant(objectives, () => quests, () => participation, () => registry, PersistenceService.LocalWorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            QuestObjectiveProgressRuntime restored = Objectives(quests, participation, registry);
            QuestObjectiveProgressPersistenceParticipant restoredParticipant = new QuestObjectiveProgressPersistenceParticipant(restored, () => quests, () => participation, () => registry, PersistenceService.LocalWorldId);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestObjectiveProgressPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            QuestObjectiveProgressRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.objectives[0].assignmentId = "quest-assignment.missing";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestObjectiveProgressPersistenceParticipant.CurrentParticipantSchemaVersion);

            bool valid = save.Succeeded
                && prepare.Succeeded
                && commit.Succeeded
                && rejected.Succeeded == false
                && restored.ObjectiveCount == 4;
            return TestLabAssertions.True("step15-objective-persistence", "Quest objective progress persists and rejects invalid payloads before mutation", valid, $"Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Reject={rejected.Succeeded} Count={restored.ObjectiveCount}");
        }

        private static TestLabAutomationStepResult OutcomeReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool guild = registry.TryGet(PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, out QuestDefinition guildDefinition)
                && guildDefinition.CompletionPolicy.policy == QuestCompletionPolicy.RequireTurnIn
                && guildDefinition.DeadlineDefinitions.Count == 1
                && guildDefinition.RewardPackages.SelectMany(package => package.rewards).Count() == 2;
            bool hidden = registry.TryGet(PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId, out QuestDefinition hiddenDefinition)
                && hiddenDefinition.CompletionPolicy.policy == QuestCompletionPolicy.AutoCompleteWhenRequiredObjectivesSatisfied
                && hiddenDefinition.RewardPackages.Any(package => package.deliveryPolicy == QuestRewardDeliveryPolicy.GrantOnCompletion);
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (QuestDefinition definition in PrototypeQuestDefinitionFactory.CreateMissingQuestDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            bool valid = guild && hidden && report.ErrorCount == 0;
            return TestLabAssertions.True("step15-outcome-readiness", "Quest outcome definitions register and validate", valid, $"Guild={guild} Hidden={hidden} Errors={report.ErrorCount} Warnings={report.WarningCount}");
        }

        private static TestLabAutomationStepResult OutcomeTurnInCompletion(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestObjectiveProgressRuntime objectives = Objectives(quests, participation, registry);
            QuestOutcomeRuntime outcomes = Outcomes(quests, participation, objectives, registry, new AutomationRewardExecutor());
            QuestAssignmentSnapshot assignment = AcceptedGuildAssignment(quests, participation, "outcome-complete");
            objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.outcome.complete.objectives");
            outcomes.TrackAssignment(assignment, "tx.quest.outcome.complete.track");
            CompleteGuildObjectives(objectives, assignment, "outcome.complete");

            QuestCompletionEvaluationResult wrongCounter = outcomes.EvaluateCompletion(new QuestCompletionEvaluationRequest { assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.other", worldTime = 4d });
            QuestOutcomeOperationResult complete = outcomes.Complete(new QuestCompletionRequest { transactionId = "tx.quest.outcome.complete", assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.adventurer-guild-counter", locationId = "location.prototype.adventurers-guild", issuerId = "organization.prototype.adventurers-guild", worldTime = assignment.AssignedWorldTime + 2d });
            QuestOutcomeOperationResult duplicate = outcomes.Complete(new QuestCompletionRequest { transactionId = "tx.quest.outcome.complete", assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.adventurer-guild-counter", worldTime = assignment.AssignedWorldTime + 2.1d });

            bool valid = wrongCounter.Status == QuestOutcomeOperationStatus.TurnInRequired
                && complete.Succeeded
                && complete.Outcome?.OutcomeKind == QuestTerminalOutcomeKind.Completed
                && complete.Rewards.Count == 2
                && complete.Rewards.All(reward => reward.State == QuestRewardEntitlementState.Claimable)
                && duplicate.Status == QuestOutcomeOperationStatus.Duplicate
                && outcomes.TerminalOutcomeCount == 1;
            return TestLabAssertions.True("step15-outcome-completion", "Turn-in completion records one terminal outcome and claimable rewards", valid, $"Wrong={wrongCounter.Status} Complete={complete.Status} Duplicate={duplicate.Status} Outcomes={outcomes.TerminalOutcomeCount} Rewards={complete.Rewards.Count}");
        }

        private static TestLabAutomationStepResult OutcomeDeadlineFailure(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestObjectiveProgressRuntime objectives = Objectives(quests, participation, registry);
            QuestOutcomeRuntime outcomes = Outcomes(quests, participation, objectives, registry, new AutomationRewardExecutor());
            QuestAssignmentSnapshot assignment = AcceptedGuildAssignment(quests, participation, "outcome-deadline");
            objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.outcome.deadline.objectives");
            QuestOutcomeOperationResult track = outcomes.TrackAssignment(assignment, "tx.quest.outcome.deadline.track");
            QuestOutcomeOperationResult expired = outcomes.EvaluateDeadlines(assignment.AssignedWorldTime + 3d, "tx.quest.outcome.deadline");
            QuestOutcomeOperationResult duplicate = outcomes.EvaluateDeadlines(assignment.AssignedWorldTime + 3d, "tx.quest.outcome.deadline");
            QuestCompletionEvaluationResult completeAfter = outcomes.EvaluateCompletion(new QuestCompletionEvaluationRequest { assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.adventurer-guild-counter", worldTime = assignment.AssignedWorldTime + 3.1d });

            bool valid = track.Succeeded
                && expired.Succeeded
                && expired.Outcome?.OutcomeKind == QuestTerminalOutcomeKind.Expired
                && duplicate.Status == QuestOutcomeOperationStatus.Duplicate
                && completeAfter.Status == QuestOutcomeOperationStatus.AlreadyTerminal
                && outcomes.QueryDeadlines(new QuestOutcomeQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single().Expired;
            return TestLabAssertions.True("step15-outcome-deadline", "Deadline expiration fails the assignment exactly once", valid, $"Track={track.Status} Expired={expired.Status} Duplicate={duplicate.Status} CompleteAfter={completeAfter.Status}");
        }

        private static TestLabAutomationStepResult OutcomeRewardClaim(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestObjectiveProgressRuntime objectives = Objectives(quests, participation, registry);
            AutomationRewardExecutor executor = new AutomationRewardExecutor();
            QuestOutcomeRuntime outcomes = Outcomes(quests, participation, objectives, registry, executor);
            QuestAssignmentSnapshot assignment = AcceptedGuildAssignment(quests, participation, "outcome-reward");
            objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.outcome.reward.objectives");
            CompleteGuildObjectives(objectives, assignment, "outcome.reward");
            QuestOutcomeOperationResult complete = outcomes.Complete(new QuestCompletionRequest { transactionId = "tx.quest.outcome.reward.complete", assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.adventurer-guild-counter", worldTime = 4d });
            QuestRewardEntitlementSnapshot reward = complete.Rewards.FirstOrDefault(value => value.Category == QuestRewardCategory.Currency);
            QuestOutcomeOperationResult claim = outcomes.ClaimReward(new QuestRewardClaimRequest { transactionId = "tx.quest.outcome.reward.claim", entitlementId = reward?.EntitlementId, claimantPersonId = assignment.AssigneePersonId, worldTime = 5d });
            QuestOutcomeOperationResult duplicate = outcomes.ClaimReward(new QuestRewardClaimRequest { transactionId = "tx.quest.outcome.reward.claim", entitlementId = reward?.EntitlementId, claimantPersonId = assignment.AssigneePersonId, worldTime = 6d });

            bool valid = complete.Succeeded
                && reward != null
                && claim.Succeeded
                && claim.Reward?.State == QuestRewardEntitlementState.Granted
                && duplicate.Status == QuestOutcomeOperationStatus.Duplicate
                && executor.Requests.Count == 1
                && executor.Requests[0].category == QuestRewardCategory.Currency;
            return TestLabAssertions.True("step15-outcome-reward", "Reward claims delegate once to owner runtimes", valid, $"Complete={complete.Status} Claim={claim.Status} Duplicate={duplicate.Status} Calls={executor.Requests.Count}");
        }

        private static TestLabAutomationStepResult OutcomePersistenceAndRedaction(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestObjectiveProgressRuntime objectives = Objectives(quests, participation, registry);
            AutomationRewardExecutor executor = new AutomationRewardExecutor();
            QuestOutcomeRuntime outcomes = Outcomes(quests, participation, objectives, registry, executor);
            QuestAssignmentSnapshot assignment = AcceptedHiddenAssignment(quests, participation, "outcome-hidden");
            objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.outcome.hidden.objectives");
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.DiscoverLocation, "location.prototype.secret-dungeon-entry", "source.quest.outcome.hidden.discover", InformationSubjectType.Location));
            QuestOutcomeOperationResult complete = outcomes.Complete(new QuestCompletionRequest { transactionId = "tx.quest.outcome.hidden.complete", assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, worldTime = 4d });
            QuestRewardEntitlementSnapshot publicReward = outcomes.QueryRewards(new QuestRewardQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PublicOnly, includeHidden = true, includeTerminal = true }).SingleOrDefault();
            QuestRewardEntitlementSnapshot privilegedReward = outcomes.QueryRewards(new QuestRewardQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PrivilegedDiagnostic, includeTerminal = true }).SingleOrDefault();
            QuestOutcomePersistenceParticipant participant = new QuestOutcomePersistenceParticipant(outcomes, () => quests, () => participation, () => objectives, () => registry, () => executor);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            QuestOutcomeRuntime restored = Outcomes(quests, participation, objectives, registry, executor);
            QuestOutcomePersistenceParticipant restoredParticipant = new QuestOutcomePersistenceParticipant(restored, () => quests, () => participation, () => objectives, () => registry, () => executor);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestOutcomePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            QuestOutcomeRuntimeSaveData corrupt = restored.CreateSaveData();
            if (corrupt.terminalOutcomes.Count > 0)
            {
                corrupt.terminalOutcomes[0].assignmentId = "quest-assignment.prototype.missing";
            }
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestOutcomePersistenceParticipant.CurrentParticipantSchemaVersion);

            bool valid = complete.Succeeded
                && publicReward != null
                && publicReward.Redacted
                && string.IsNullOrEmpty(publicReward.TargetDefinitionId)
                && privilegedReward != null
                && privilegedReward.Redacted == false
                && save.Succeeded
                && prepare.Succeeded
                && commit.Succeeded
                && rejected.Succeeded == false
                && restored.TerminalOutcomeCount == 1
                && restored.RewardEntitlementCount == 1;
            return TestLabAssertions.True("step15-outcome-persistence", "Hidden outcome rewards redact and persistence rejects invalid graphs before mutation", valid, $"Complete={complete.Status} PublicRedacted={publicReward?.Redacted} PrivilegedRedacted={privilegedReward?.Redacted} Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Reject={rejected.Succeeded} Restored={restored.TerminalOutcomeCount}/{restored.RewardEntitlementCount}");
        }

        private static TestLabAutomationStepResult QuestSourceReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool hasAll = PrototypeQuestSourceDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out QuestSourceDefinition _));
            bool metadata = registry.TryGet(PrototypeQuestSourceDefinitionFactory.AdventurerGuildCounterDefinitionId, out QuestSourceDefinition counter)
                && counter.Category == QuestSourceCategory.GuildCounter
                && counter.PublicationPolicy.maxActiveListings == 6
                && counter.SourceRoleIds.Contains("quest-source-role.acceptance");
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (QuestSourceDefinition definition in PrototypeQuestSourceDefinitionFactory.CreateMissingQuestSourceDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            bool valid = hasAll && metadata && report.ErrorCount == 0;
            return TestLabAssertions.True("step15-source-readiness", "Quest source definitions register and validate", valid, $"Definitions={hasAll} Metadata={metadata} Errors={report.ErrorCount} Warnings={report.WarningCount}");
        }

        private static TestLabAutomationStepResult QuestSourceEmpty(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestSourceRuntime sources = Sources(quests, participation, registry);
            QuestSourceOperationResult create = sources.CreateSource(new QuestSourceCreateRequest
            {
                transactionId = "tx.quest-source.empty.create",
                questSourceId = "quest-source.prototype.empty-archive.automation",
                questSourceDefinitionId = PrototypeQuestSourceDefinitionFactory.EmptyArchiveDefinitionId,
                hostLocationId = "location.prototype.guild-archive",
                interactionPointId = "interaction-point.prototype.archive",
                sceneBindingKey = "scene.prototype.guild.archive",
                visibility = QuestSourceVisibility.Restricted,
                worldTime = 1d
            });
            QuestSourceBrowseResult publicBrowse = sources.BrowseSource(new QuestSourceBrowseRequest { questSourceId = create.Source?.QuestSourceId, access = QuestVisibilityAccess.PublicOnly });
            QuestSourceBrowseResult privilegedBrowse = sources.BrowseSource(new QuestSourceBrowseRequest { questSourceId = create.Source?.QuestSourceId, access = QuestVisibilityAccess.PrivilegedDiagnostic });
            bool valid = create.Succeeded
                && sources.SourceCount == 1
                && sources.ListingCount == 0
                && create.Source.SceneBindingKey == "scene.prototype.guild.archive"
                && publicBrowse.Status == QuestSourceOperationStatus.VisibilityDenied
                && privilegedBrowse.Succeeded
                && privilegedBrowse.VisibleCount == 0;
            return TestLabAssertions.True("step15-source-empty", "Quest sources can exist without listings and preserve scene binding", valid, $"Create={create.Status} Public={publicBrowse.Status} Privileged={privilegedBrowse.Status} Sources={sources.SourceCount} Listings={sources.ListingCount}");
        }

        private static TestLabAutomationStepResult QuestSourcePublishBrowseDiscovery(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestSourceRuntime sources = Sources(quests, participation, registry);
            QuestRuntimeOperationResult quest = CreateGuildPosting(quests, "source-publish", "quest.prototype.guild.source-publish");
            QuestSourceOperationResult source = CreateGuildCounter(sources, "quest-source.prototype.guild-counter.source-publish");
            QuestSourceOperationResult unauthorized = sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.quest-source.publish.unauthorized", questSourceId = source.Source?.QuestSourceId, questId = quest.Snapshot?.QuestId, worldTime = 2d });
            QuestSourceOperationResult publish = sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.quest-source.publish", questSourceId = source.Source?.QuestSourceId, questId = quest.Snapshot?.QuestId, publisherAuthorityId = "authority.prototype.guild.quest-offer", publisherPersonId = "person.prototype.guild-clerk", worldTime = 2d });
            QuestSourceBrowseResult browse = sources.BrowseSource(new QuestSourceBrowseRequest { transactionId = "tx.quest-source.browse", questSourceId = source.Source?.QuestSourceId, requesterPersonId = "person.prototype.player", access = QuestVisibilityAccess.LocalKnowledge, eligibilityContext = EligibleContext("person.prototype.player"), recordDiscovery = true, worldTime = 3d });
            QuestListingInspectionResult inspect = sources.InspectListing(new QuestListingInspectRequest { transactionId = "tx.quest-source.inspect", questSourceId = source.Source?.QuestSourceId, questListingId = publish.Listing?.QuestListingId, requesterPersonId = "person.prototype.player", access = QuestVisibilityAccess.LocalKnowledge, eligibilityContext = EligibleContext("person.prototype.player"), recordDiscovery = true, worldTime = 4d });
            bool valid = quest.Succeeded
                && source.Succeeded
                && unauthorized.Status == QuestSourceOperationStatus.UnauthorizedPublisher
                && publish.Succeeded
                && browse.Succeeded
                && browse.VisibleCount == 1
                && inspect.Succeeded
                && sources.DiscoveryCount == 2
                && participation.OfferCount == 0
                && participation.AssignmentCount == 0;
            return TestLabAssertions.True("step15-source-publish-browse", "Publication, browse, inspect, and discovery stay separate from offer creation", valid, $"Quest={quest.Status} Source={source.Status} Unauthorized={unauthorized.Status} Publish={publish.Status} Browse={browse.Status}/{browse.VisibleCount} Inspect={inspect.Status} Discoveries={sources.DiscoveryCount} Offers={participation.OfferCount} Assignments={participation.AssignmentCount}");
        }

        private static TestLabAutomationStepResult QuestSourceAcceptanceClaimsListing(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestSourceRuntime sources = Sources(quests, participation, registry);
            QuestRuntimeOperationResult quest = CreateGuildPosting(quests, "source-accept", "quest.prototype.guild.source-accept");
            QuestSourceOperationResult source = CreateGuildCounter(sources, "quest-source.prototype.guild-counter.source-accept");
            QuestSourceOperationResult publish = sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.quest-source.accept.publish", questSourceId = source.Source?.QuestSourceId, questId = quest.Snapshot?.QuestId, publisherAuthorityId = "authority.prototype.guild.quest-offer", publisherPersonId = "person.prototype.guild-clerk", worldTime = 2d });
            QuestSourceOperationResult preview = sources.AcceptFromSource(new QuestSourceAcceptRequest { transactionId = "tx.quest-source.accept.preview", questListingId = publish.Listing?.QuestListingId, personId = "person.prototype.player", authorityBasisId = "authority.prototype.guild.quest-offer", eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 3d, preview = true });
            QuestSourceOperationResult accept = sources.AcceptFromSource(new QuestSourceAcceptRequest { transactionId = "tx.quest-source.accept", questListingId = publish.Listing?.QuestListingId, personId = "person.prototype.player", authorityBasisId = "authority.prototype.guild.quest-offer", eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 3d });
            QuestSourceBrowseResult after = sources.BrowseSource(new QuestSourceBrowseRequest { questSourceId = source.Source?.QuestSourceId, access = QuestVisibilityAccess.LocalKnowledge, eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 4d });
            QuestVisibleListingSnapshot visible = after.Listings.FirstOrDefault();
            bool valid = quest.Succeeded
                && source.Succeeded
                && publish.Succeeded
                && preview.Status == QuestSourceOperationStatus.Preview
                && accept.Succeeded
                && accept.Assignment != null
                && accept.Listing.LifecycleState == QuestListingLifecycleState.Claimed
                && participation.OfferCount == 1
                && participation.AssignmentCount == 1
                && after.VisibleCount == 1
                && visible != null
                && visible.Taken;
            return TestLabAssertions.True("step15-source-acceptance", "Quest source acceptance delegates to participation and marks listings taken", valid, $"Quest={quest.Status} Source={source.Status} Publish={publish.Status} Preview={preview.Status} Accept={accept.Status} Listing={accept.Listing?.LifecycleState} Browse={after.VisibleCount} Taken={visible?.Taken} Offers={participation.OfferCount} Assignments={participation.AssignmentCount}");
        }

        private static TestLabAutomationStepResult QuestSourceExpirationPersistence(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestSourceRuntime sources = Sources(quests, participation, registry);
            QuestRuntimeOperationResult quest = quests.CreateQuest(new QuestCreateRequest
            {
                transactionId = "tx.quest-source.persistence.quest",
                questId = "quest.prototype.delivery.source-persist",
                questDefinitionId = PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.merchant-guild" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.player" },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Contract, locationId = "location.prototype.market-stall", interactionPointId = "interaction-point.prototype.merchant-guild-counter" },
                subjectLinks = new[] { Subject("item.prototype.merchant-parcel", QuestSubjectRole.Item, InformationSubjectType.Custom) },
                createdWorldTime = 1d
            });
            QuestSourceOperationResult source = CreateMerchantCounter(sources, "quest-source.prototype.merchant-counter.source-persist");
            QuestSourceOperationResult publish = sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.quest-source.persistence.publish", questSourceId = source.Source?.QuestSourceId, questId = quest.Snapshot?.QuestId, publisherAuthorityId = "authority.prototype.merchant.quest-offer", expirationWorldTime = 5d, worldTime = 2d });
            QuestSourceOperationResult firstExpire = sources.EvaluateExpirations(5d, "tx.quest-source.persistence.expire").FirstOrDefault();
            QuestSourceOperationResult secondExpire = sources.EvaluateExpirations(5d, "tx.quest-source.persistence.expire").FirstOrDefault();
            QuestSourcePersistenceParticipant participant = new QuestSourcePersistenceParticipant(sources, () => quests, () => participation, () => registry);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            QuestSourceRuntime restored = Sources(quests, participation, registry);
            QuestSourcePersistenceParticipant restoredParticipant = new QuestSourcePersistenceParticipant(restored, () => quests, () => participation, () => registry);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestSourcePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            int restoredListings = restored.ListingCount;
            QuestSourceRuntimeSaveData corrupt = restored.CreateSaveData();
            if (corrupt.listings.Count > 0)
            {
                corrupt.listings[0].questId = "quest.prototype.missing";
            }
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestSourcePersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = quest.Succeeded
                && source.Succeeded
                && publish.Succeeded
                && firstExpire != null
                && firstExpire.Succeeded
                && firstExpire.Listing.LifecycleState == QuestListingLifecycleState.Expired
                && secondExpire == null
                && save.Succeeded
                && prepare.Succeeded
                && commit.Succeeded
                && rejected.Succeeded == false
                && restored.ListingCount == restoredListings
                && restoredListings == 1;
            return TestLabAssertions.True("step15-source-persistence", "Quest source expiration and persistence are deterministic and reject invalid graphs", valid, $"Quest={quest.Status} Source={source.Status} Publish={publish.Status} Expire={firstExpire?.Status}/{firstExpire?.Listing?.LifecycleState} DuplicateExpire={secondExpire?.Status.ToString() ?? "None"} Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Reject={rejected.Succeeded} Restored={restored.ListingCount}");
        }

        private static TestLabAutomationStepResult ConversationReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool hasAll = PrototypeConversationDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out ConversationDefinition _));
            bool metadata = registry.TryGet(PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId, out ConversationDefinition counter)
                && counter.Category == ConversationCategory.QuestOffer
                && counter.CoLocationPolicy == ConversationCoLocationPolicy.SameInteractionPoint
                && counter.RequiredRoles.Contains(ConversationParticipantRole.Provider);
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (ConversationDefinition definition in PrototypeConversationDefinitionFactory.CreateMissingConversationDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            bool valid = hasAll && metadata && report.ErrorCount == 0;
            return TestLabAssertions.True("step15-conversation-readiness", "Conversation definitions register and validate", valid, $"Definitions={hasAll} Metadata={metadata} Errors={report.ErrorCount} Warnings={report.WarningCount}");
        }

        private static TestLabAutomationStepResult ConversationGuildCounterContext(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestSourceRuntime sources = Sources(quests, participation, registry);
            QuestRuntimeOperationResult quest = CreateGuildPosting(quests, "conversation-context", "quest.prototype.guild.conversation-context");
            QuestSourceOperationResult source = CreateGuildCounter(sources, "quest-source.prototype.guild-counter.conversation-context");
            QuestSourceOperationResult publish = sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.conversation.context.publish", questSourceId = source.Source?.QuestSourceId, questId = quest.Snapshot?.QuestId, publisherAuthorityId = "authority.prototype.guild.quest-offer", worldTime = 2d });
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult start = conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = "tx.conversation.context.start",
                conversationId = "conversation.prototype.guild-counter.context",
                conversationDefinitionId = PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId,
                participants = GuildCounterParticipants("interaction-point.prototype.adventurer-guild-counter"),
                hostLocationId = "location.prototype.adventurers-guild",
                hostInteractionPointId = "interaction-point.prototype.adventurer-guild-counter",
                questSourceId = source.Source?.QuestSourceId,
                questListingId = publish.Listing?.QuestListingId,
                questId = quest.Snapshot?.QuestId,
                operatingOrganizationId = "organization.prototype.adventurers-guild",
                sceneBindingKey = "scene.prototype.guild.counter",
                worldTime = 3d
            });

            ConversationSnapshot snapshot = start.Snapshot;
            bool valid = quest.Succeeded
                && source.Succeeded
                && publish.Succeeded
                && start.Succeeded
                && snapshot != null
                && snapshot.Participants.Count == 3
                && snapshot.SubjectLinks.Any(link => link.role == ConversationSubjectRole.Quest)
                && snapshot.SubjectLinks.Any(link => link.role == ConversationSubjectRole.QuestSource)
                && snapshot.SubjectLinks.Any(link => link.role == ConversationSubjectRole.QuestListing)
                && snapshot.CreateInformationSubject().tags.Contains(ConversationInformationSubject.ConversationTag)
                && conversations.Query(new ConversationQuery { questId = quest.Snapshot?.QuestId, access = ConversationAccessLevel.PrivilegedDiagnostic }).Count == 1;
            return TestLabAssertions.True("step15-conversation-context", "Conversation stores quest, source, location, and provider references without owning them", valid, $"Quest={quest.Status} Source={source.Status} Publish={publish.Status} Conversation={start.Status} Participants={snapshot?.Participants.Count} Subjects={snapshot?.SubjectLinks.Count}");
        }

        private static TestLabAutomationStepResult ConversationPrivateProjection(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult start = conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = "tx.conversation.private.start",
                conversationId = "conversation.prototype.private-audience",
                conversationDefinitionId = PrototypeConversationDefinitionFactory.PrivateAudienceDefinitionId,
                participants = new[]
                {
                    Participant("person.prototype.player", ConversationParticipantRole.Initiator, "location.prototype.guild-head-office", "interaction-point.prototype.guild-head-desk"),
                    Participant("person.prototype.guild-head", ConversationParticipantRole.Addressee, "location.prototype.guild-head-office", "interaction-point.prototype.guild-head-desk", hidden: true)
                },
                subjectLinks = new[] { HiddenSubject("knowledge.prototype.private-recommendation") },
                hostLocationId = "location.prototype.guild-head-office",
                hostInteractionPointId = "interaction-point.prototype.guild-head-desk",
                worldTime = 4d
            });
            ConversationProjection publicProjection = conversations.Query(new ConversationQuery { conversationId = start.Snapshot?.ConversationId, access = ConversationAccessLevel.Public, requesterPersonId = "person.prototype.visitor" }).SingleOrDefault();
            ConversationProjection participantProjection = conversations.Query(new ConversationQuery { conversationId = start.Snapshot?.ConversationId, access = ConversationAccessLevel.Participant, requesterPersonId = "person.prototype.player" }).SingleOrDefault();
            ConversationProjection privilegedProjection = conversations.Query(new ConversationQuery { conversationId = start.Snapshot?.ConversationId, access = ConversationAccessLevel.PrivilegedDiagnostic }).SingleOrDefault();
            bool valid = start.Succeeded
                && publicProjection == null
                && participantProjection != null
                && participantProjection.Redacted
                && participantProjection.Snapshot.Participants.All(value => !value.hidden)
                && participantProjection.Snapshot.SubjectLinks.All(value => !value.hidden)
                && privilegedProjection != null
                && privilegedProjection.Redacted == false
                && privilegedProjection.Snapshot.Participants.Any(value => value.hidden);
            return TestLabAssertions.True("step15-conversation-private", "Private conversation projection redacts hidden participants and subjects", valid, $"Start={start.Status} Public={(publicProjection == null ? "Denied" : "Visible")} ParticipantRedacted={participantProjection?.Redacted} Privileged={privilegedProjection != null}");
        }

        private static TestLabAutomationStepResult ConversationProviderLocationValidation(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult missingProvider = conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = "tx.conversation.provider.missing",
                conversationDefinitionId = PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId,
                participants = new[] { Participant("person.prototype.player", ConversationParticipantRole.Initiator, "location.prototype.adventurers-guild", "interaction-point.prototype.adventurer-guild-counter") },
                hostLocationId = "location.prototype.adventurers-guild",
                hostInteractionPointId = "interaction-point.prototype.adventurer-guild-counter"
            });
            ConversationOperationResult wrongLocation = conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = "tx.conversation.location.wrong",
                conversationDefinitionId = PrototypeConversationDefinitionFactory.PrisonerInterviewDefinitionId,
                participants = new[]
                {
                    Participant("person.prototype.player", ConversationParticipantRole.Initiator, "location.prototype.guild-hall", string.Empty),
                    Participant("person.prototype.prisoner", ConversationParticipantRole.Prisoner, "location.prototype.basement-prison", string.Empty),
                    Participant("person.prototype.guard", ConversationParticipantRole.Guard, "location.prototype.basement-prison", string.Empty, provenanceId: "authority.prototype.prison.interview")
                },
                hostLocationId = "location.prototype.basement-prison",
                tagIds = new[] { "authority.prototype.prison.interview" }
            });
            bool valid = missingProvider.Status == ConversationOperationStatus.MissingParticipant
                && wrongLocation.Status == ConversationOperationStatus.CoLocationRejected
                && conversations.Count == 0
                && conversations.Revision == 0;
            return TestLabAssertions.True("step15-conversation-provider-location", "Invalid provider and location requests do not mutate conversation runtime", valid, $"Provider={missingProvider.Status} Location={wrongLocation.Status} Count={conversations.Count} Revision={conversations.Revision}");
        }

        private static TestLabAutomationStepResult ConversationIdempotenceLifecycle(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult start = conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = "tx.conversation.lifecycle.start",
                conversationId = "conversation.prototype.group-briefing.lifecycle",
                conversationDefinitionId = PrototypeConversationDefinitionFactory.GroupBriefingDefinitionId,
                participants = new[]
                {
                    Participant("person.prototype.player", ConversationParticipantRole.Initiator, "location.prototype.guild-hall", string.Empty),
                    Participant("person.prototype.guild-head", ConversationParticipantRole.Speaker, "location.prototype.guild-hall", string.Empty),
                    Participant("person.prototype.adventurer", ConversationParticipantRole.Listener, "location.prototype.guild-hall", string.Empty),
                    Participant("person.prototype.scribe", ConversationParticipantRole.Witness, "location.prototype.guild-hall", string.Empty)
                },
                hostLocationId = "location.prototype.guild-hall",
                worldTime = 1d
            });
            ConversationOperationResult duplicate = conversations.StartConversation(new ConversationStartRequest { transactionId = "tx.conversation.lifecycle.start" });
            long createdRevision = conversations.Revision;
            ConversationOperationResult stale = conversations.TransitionLifecycle(new ConversationLifecycleRequest { transactionId = "tx.conversation.lifecycle.stale", conversationId = start.Snapshot?.ConversationId, targetState = ConversationLifecycleState.Completed, expectedRevision = createdRevision - 1L });
            ConversationOperationResult complete = conversations.TransitionLifecycle(new ConversationLifecycleRequest { transactionId = "tx.conversation.lifecycle.complete", conversationId = start.Snapshot?.ConversationId, targetState = ConversationLifecycleState.Completed, expectedRevision = createdRevision, worldTime = 6d });
            ConversationOperationResult duplicateComplete = conversations.TransitionLifecycle(new ConversationLifecycleRequest { transactionId = "tx.conversation.lifecycle.complete", conversationId = start.Snapshot?.ConversationId, targetState = ConversationLifecycleState.Completed, worldTime = 6d });
            bool valid = start.Succeeded
                && duplicate.Duplicate
                && stale.Status == ConversationOperationStatus.RevisionConflict
                && complete.Succeeded
                && complete.Snapshot.LifecycleState == ConversationLifecycleState.Completed
                && duplicateComplete.Duplicate
                && conversations.Events.Count == 2;
            return TestLabAssertions.True("step15-conversation-lifecycle", "Conversation start and lifecycle transitions are idempotent and revision guarded", valid, $"Start={start.Status} Duplicate={duplicate.Status} Stale={stale.Status} Complete={complete.Status}/{complete.Snapshot?.LifecycleState} Events={conversations.Events.Count}");
        }

        private static TestLabAutomationStepResult ConversationPersistence(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult start = conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = "tx.conversation.persistence.start",
                conversationId = "conversation.prototype.records.persist",
                conversationDefinitionId = PrototypeConversationDefinitionFactory.RecordsDeskDefinitionId,
                participants = new[]
                {
                    Participant("person.prototype.player", ConversationParticipantRole.Initiator, "location.prototype.records-room", "interaction-point.prototype.records-desk"),
                    Participant("person.prototype.records-clerk", ConversationParticipantRole.Provider, "location.prototype.records-room", "interaction-point.prototype.records-desk", provenanceId: "authority.prototype.records.read"),
                    Participant("person.prototype.player", ConversationParticipantRole.Listener, "location.prototype.records-room", "interaction-point.prototype.records-desk")
                },
                hostLocationId = "location.prototype.records-room",
                hostInteractionPointId = "interaction-point.prototype.records-desk",
                operatingOfficeId = "office.prototype.records",
                tagIds = new[] { "authority.prototype.records.read" },
                worldTime = 2d
            });
            ConversationPersistenceParticipant participant = new ConversationPersistenceParticipant(conversations, () => registry);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            ConversationRuntime restored = Conversations(registry);
            ConversationPersistenceParticipant restoredParticipant = new ConversationPersistenceParticipant(restored, () => registry);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, ConversationPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            ConversationRuntimeSaveData corrupt = restored.CreateSaveData();
            if (corrupt.conversations.Count > 0)
            {
                corrupt.conversations[0].conversationDefinitionId = "conversation-definition.prototype.missing";
            }
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), ConversationPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = start.Succeeded
                && save.Succeeded
                && prepare.Succeeded
                && commit.Succeeded
                && restored.Count == 1
                && restored.Events.Count == 1
                && rejected.Succeeded == false
                && restored.Count == 1;
            return TestLabAssertions.True("step15-conversation-persistence", "Conversation persistence restores records and rejects corrupt payloads safely", valid, $"Start={start.Status} Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Reject={rejected.Succeeded} Count={restored.Count} Events={restored.Events.Count}");
        }

        private static TestLabAutomationStepResult DialogueFlowReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool hasAll = PrototypeDialogueGraphDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out DialogueGraphDefinition _));
            bool metadata = registry.TryGet(PrototypeDialogueGraphDefinitionFactory.AdventurerGuildCounterGraphId, out DialogueGraphDefinition graph)
                && graph.ConversationDefinitionId == PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId
                && graph.Nodes.Any(node => node.choices.Any(choice => choice.choiceId == "guild.choice.accept-posting"));
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (DialogueGraphDefinition definition in PrototypeDialogueGraphDefinitionFactory.CreateMissingDialogueGraphDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            bool valid = hasAll && metadata && report.ErrorCount == 0;
            return TestLabAssertions.True("step15-dialogue-flow-readiness", "Dialogue graph definitions register and validate", valid, $"Definitions={hasAll} Metadata={metadata} Errors={report.ErrorCount} Warnings={report.WarningCount}");
        }

        private static TestLabAutomationStepResult DialogueFlowStartAndChoices(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult conversation = StartGuildConversation(conversations, "flow-start");
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest
            {
                transactionId = "tx.dialogue.flow.start",
                conversationId = conversation.Snapshot?.ConversationId,
                conditionContext = GuildDialogueContext(),
                worldTime = 10d
            });

            bool valid = conversation.Succeeded
                && start.Succeeded
                && start.Snapshot.CurrentNodeId == "guild.entry"
                && start.Snapshot.State == DialogueFlowState.AwaitingChoice
                && start.Snapshot.VisibleChoices.Select(choice => choice.ChoiceId).SequenceEqual(new[] { "guild.choice.accept-posting", "guild.choice.ask-work", "guild.choice.leave" });
            return TestLabAssertions.True("step15-dialogue-flow-start", "Dialogue flow starts at the canonical node with deterministic visible choices", valid, $"Conversation={conversation.Status} Flow={start.Status} Node={start.Snapshot?.CurrentNodeId} Choices={string.Join(",", start.Snapshot?.VisibleChoices.Select(choice => choice.ChoiceId) ?? Array.Empty<string>())}");
        }

        private static TestLabAutomationStepResult DialogueFlowConditions(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult conversation = StartGuildConversation(conversations, "flow-conditions");
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest { transactionId = "tx.dialogue.conditions.start", conversationId = conversation.Snapshot?.ConversationId, conditionContext = GuildDialogueContext(), worldTime = 1d });
            DialogueFlowSnapshot ordinary = start.Snapshot;
            DialogueFlowSnapshot ranked;
            bool rankedSnapshot = flows.TryGetSnapshot(start.Snapshot?.FlowId, GuildDialogueContext(rank: true), out ranked);

            bool valid = start.Succeeded
                && ordinary.VisibleChoices.All(choice => choice.ChoiceId != "guild.choice.silver-rank")
                && rankedSnapshot
                && ranked.VisibleChoices.Any(choice => choice.ChoiceId == "guild.choice.silver-rank")
                && ranked.VisibleChoices.First(choice => choice.ChoiceId == "guild.choice.silver-rank").Evaluation.Selectable;
            return TestLabAssertions.True("step15-dialogue-flow-conditions", "Dialogue flow conditions hide restricted choices until context satisfies them", valid, $"Start={start.Status} Ordinary={string.Join(",", ordinary?.VisibleChoices.Select(choice => choice.ChoiceId) ?? Array.Empty<string>())} Ranked={rankedSnapshot}:{string.Join(",", ranked?.VisibleChoices.Select(choice => choice.ChoiceId) ?? Array.Empty<string>())}");
        }

        private static TestLabAutomationStepResult DialogueFlowChoiceHistory(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult conversation = StartGuildConversation(conversations, "flow-choice");
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest { transactionId = "tx.dialogue.choice.start", conversationId = conversation.Snapshot?.ConversationId, conditionContext = GuildDialogueContext(), worldTime = 1d });
            long beforeConversationRevision = conversations.Revision;
            DialogueFlowOperationResult select = flows.SelectChoice(new DialogueChoiceSelectionRequest { transactionId = "tx.dialogue.choice.select", flowId = start.Snapshot?.FlowId, choiceId = "guild.choice.ask-work", actorPersonId = "person.prototype.player", conditionContext = GuildDialogueContext(), worldTime = 2d });
            DialogueFlowOperationResult duplicate = flows.SelectChoice(new DialogueChoiceSelectionRequest { transactionId = "tx.dialogue.choice.select", flowId = start.Snapshot?.FlowId, choiceId = "guild.choice.ask-work", actorPersonId = "person.prototype.player", conditionContext = GuildDialogueContext(), worldTime = 2d });

            bool valid = start.Succeeded
                && select.Succeeded
                && duplicate.Duplicate
                && select.Snapshot.CurrentNodeId == "guild.entry"
                && select.Snapshot.Visits.Count == 3
                && select.Snapshot.Selections.Count == 1
                && select.Snapshot.LocalVariables.Any(value => value.variableId == "flag.guild.asked-work" && value.boolValue)
                && conversations.Revision == beforeConversationRevision;
            return TestLabAssertions.True("step15-dialogue-flow-choice", "Dialogue choice selection records deterministic history without mutating conversation ownership", valid, $"Start={start.Status} Select={select.Status} Duplicate={duplicate.Status} Node={select.Snapshot?.CurrentNodeId} Visits={select.Snapshot?.Visits.Count} Selections={select.Snapshot?.Selections.Count} ConversationRevision={conversations.Revision}->{beforeConversationRevision}");
        }

        private static TestLabAutomationStepResult DialogueFlowRequiredEffectFailure(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = RegistryWithRequiredEffectGraph(context);
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult conversation = StartGuildConversation(conversations, "flow-effect-failure");
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest { transactionId = "tx.dialogue.effect.start", graphId = "dialogue-graph.prototype.required-effect-test", conversationId = conversation.Snapshot?.ConversationId, conditionContext = GuildDialogueContext(), worldTime = 1d });
            DialogueFlowOperationResult select = flows.SelectChoice(new DialogueChoiceSelectionRequest { transactionId = "tx.dialogue.effect.select", flowId = start.Snapshot?.FlowId, choiceId = "required.choice", actorPersonId = "person.prototype.player", conditionContext = GuildDialogueContext(), worldTime = 2d });
            DialogueFlowSnapshot after;
            bool snapshot = flows.TryGetSnapshot(start.Snapshot?.FlowId, GuildDialogueContext(), out after);

            bool valid = start.Succeeded
                && select.Status == DialogueFlowOperationStatus.EffectFailed
                && snapshot
                && after.CurrentNodeId == "required.entry"
                && after.Selections.Count == 0
                && flows.Revision == 1;
            return TestLabAssertions.True("step15-dialogue-flow-effect-failure", "Required delegated dialogue effects fail atomically without an executor", valid, $"Start={start.Status} Select={select.Status} Snapshot={snapshot} Node={after?.CurrentNodeId} Selections={after?.Selections.Count} Revision={flows.Revision}");
        }

        private static TestLabAutomationStepResult DialogueFlowPersistence(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            ConversationRuntime conversations = Conversations(registry);
            ConversationOperationResult conversation = StartGuildConversation(conversations, "flow-persistence");
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest { transactionId = "tx.dialogue.persistence.start", conversationId = conversation.Snapshot?.ConversationId, conditionContext = GuildDialogueContext(), worldTime = 1d });
            DialogueFlowOperationResult select = flows.SelectChoice(new DialogueChoiceSelectionRequest { transactionId = "tx.dialogue.persistence.select", flowId = start.Snapshot?.FlowId, choiceId = "guild.choice.ask-work", actorPersonId = "person.prototype.player", conditionContext = GuildDialogueContext(), worldTime = 2d });
            DialogueFlowPersistenceParticipant participant = new DialogueFlowPersistenceParticipant(flows, () => registry, () => conversations);
            PersistenceParticipantSaveResult save = participant.CapturePayload();

            DialogueFlowRuntime restored = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowPersistenceParticipant restoredParticipant = new DialogueFlowPersistenceParticipant(restored, () => registry, () => conversations);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, DialogueFlowPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            DialogueFlowRuntimeSaveData corrupt = restored.CreateSaveData();
            if (corrupt.flows.Count > 0)
            {
                corrupt.flows[0].graphId = "dialogue-graph.prototype.missing";
            }
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), DialogueFlowPersistenceParticipant.CurrentParticipantSchemaVersion);

            bool valid = conversation.Succeeded
                && start.Succeeded
                && select.Succeeded
                && save.Succeeded
                && prepare.Succeeded
                && commit.Succeeded
                && restored.Count == 1
                && restored.Events.Count == flows.Events.Count
                && rejected.Succeeded == false
                && restored.Count == 1;
            return TestLabAssertions.True("step15-dialogue-flow-persistence", "Dialogue flow persistence restores current state and rejects corrupt graph references safely", valid, $"Conversation={conversation.Status} Start={start.Status} Select={select.Status} Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Reject={rejected.Succeeded} Restored={restored.Count} Events={restored.Events.Count}");
        }

        private static TestLabAutomationStepResult NarrativeReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool hasAll = PrototypeNarrativeEventDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out NarrativeEventDefinition _));
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (NarrativeEventDefinition definition in PrototypeNarrativeEventDefinitionFactory.CreateMissingNarrativeEventDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            NarrativeEventRuntime runtime = NarrativeRuntime(registry);
            bool valid = hasAll && report.HasErrors == false && report.WarningCount == 0 && runtime.Count == 0;
            return TestLabAssertions.True("step15-narrative-readiness", "Narrative event definitions register and validate without runtime mutation", valid, $"Definitions={hasAll} Errors={report.ErrorCount} Warnings={report.WarningCount} Runtime={runtime.Count}");
        }

        private static TestLabAutomationStepResult NarrativeLocationQuestAction(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestRuntime quests = Runtime(context);
            QuestParticipationRuntime participation = Participation(quests, registry);
            QuestSourceRuntime sources = Sources(quests, participation, registry);
            CreateGuildBoard(sources, "quest-source.prototype.guild-board");
            NarrativeEventRuntime runtime = NarrativeRuntime(registry, quests, sources, Conversations(registry));

            NarrativeTriggerRequest request = new NarrativeTriggerRequest
            {
                transactionId = Tx(context, "narrative-dungeon-entry"),
                source = Source(NarrativeTriggerCategory.LocationEntered, PrototypeNarrativeEventDefinitionFactory.DungeonEntrySignalId, "location.prototype.dungeon-entry", "person.prototype.player", 10d),
                conditionContext = NarrativeContext("person.prototype.player", locationId: "location.prototype.dungeon-entry", organizationIds: new[] { "organization.prototype.adventurers-guild" })
            };

            NarrativeEventOperationResult preview = runtime.RouteTrigger(new NarrativeTriggerRequest { transactionId = request.transactionId + ".preview", source = request.source.Clone(), conditionContext = request.conditionContext.Clone(), preview = true });
            NarrativeEventOperationResult execute = runtime.RouteTrigger(request);
            NarrativeEventOperationResult duplicate = runtime.RouteTrigger(new NarrativeTriggerRequest { transactionId = request.transactionId + ".duplicate", source = request.source.Clone(), conditionContext = request.conditionContext.Clone() });
            NarrativeEventSnapshot snapshot = execute.Snapshots.FirstOrDefault();

            bool valid = preview.Preview
                && execute.Succeeded
                && duplicate.Succeeded
                && runtime.Count == 1
                && quests.Count == 1
                && snapshot != null
                && snapshot.Lifecycle == NarrativeEventLifecycle.Resolved
                && snapshot.ActionExecutions.Any(action => action.category == NarrativeActionCategory.InstantiateQuest && action.lifecycle == NarrativeActionLifecycle.Committed)
                && snapshot.ActionExecutions.Any(action => action.category == NarrativeActionCategory.PublishQuestListing);
            return TestLabAssertions.True("step15-narrative-location", "Committed location trigger creates one scoped narrative event and delegates quest creation once", valid, $"Preview={preview.Status} Execute={execute.Status} Duplicate={duplicate.Status} Events={runtime.Count} Quests={quests.Count} Actions={snapshot?.ActionExecutions.Count ?? 0}");
        }

        private static TestLabAutomationStepResult NarrativeCrossRuntimeSignals(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            ConversationRuntime conversations = Conversations(registry);
            NarrativeEventRuntime runtime = NarrativeRuntime(registry, Runtime(context), null, conversations);

            NarrativeEventOperationResult dialogue = runtime.RouteTrigger(new NarrativeTriggerRequest
            {
                transactionId = Tx(context, "narrative-dialogue-choice"),
                source = Source(NarrativeTriggerCategory.DialogueChoice, PrototypeNarrativeEventDefinitionFactory.DialogueChoiceSignalId, "guild.choice.ask-work", "person.prototype.player", 11d),
                conditionContext = NarrativeContext("person.prototype.player", conversationId: "conversation.prototype.guild.counter", dialogueIds: new[] { "dialogue-choice.guild.choice.ask-work" })
            });
            NarrativeEventOperationResult knowledge = runtime.EmitSignal(new NarrativeSignalRequest
            {
                transactionId = Tx(context, "narrative-knowledge"),
                signalDefinitionId = PrototypeNarrativeEventDefinitionFactory.KnowledgeLearnedSignalId,
                actorPersonId = "person.prototype.player",
                subjectIds = new[] { "subject.prototype.hidden-dungeon" },
                conditionContext = NarrativeContext("person.prototype.player", locationId: "location.prototype.dungeon-entry", knownIds: new[] { "subject.prototype.hidden-dungeon" }),
                worldTime = 12d
            });

            bool dialogueSignal = runtime.Signals.Any(signal => signal.signalDefinitionId == PrototypeNarrativeEventDefinitionFactory.CascadeStartSignalId);
            bool valid = dialogue.Succeeded
                && knowledge.Succeeded
                && dialogueSignal
                && conversations.Count == 1
                && runtime.Query(new NarrativeEventQuery { definitionId = PrototypeNarrativeEventDefinitionFactory.DialogueChoiceWorldEventDefinitionId }).Count == 1
                && runtime.Query(new NarrativeEventQuery { definitionId = PrototypeNarrativeEventDefinitionFactory.KnowledgeUnlockConversationDefinitionId }).Count == 1;
            return TestLabAssertions.True("step15-narrative-signals", "Dialogue choices and knowledge signals route through typed narrative definitions", valid, $"Dialogue={dialogue.Status} Knowledge={knowledge.Status} Events={runtime.Count} Signals={runtime.Signals.Count} Conversations={conversations.Count}");
        }

        private static TestLabAutomationStepResult NarrativeHiddenProjectionBoundaries(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeEventRuntime runtime = NarrativeRuntime(registry, Runtime(context));
            NarrativeEventOperationResult result = runtime.RouteTrigger(new NarrativeTriggerRequest
            {
                transactionId = Tx(context, "narrative-hidden"),
                source = Source(NarrativeTriggerCategory.SocialState, PrototypeNarrativeEventDefinitionFactory.HiddenFactionSignalId, "faction.prototype.hidden", "person.prototype.player", 13d),
                conditionContext = NarrativeContext("person.prototype.player", socialIds: new[] { "faction.prototype.hidden" })
            });

            NarrativeEventSnapshot development = runtime.Query(new NarrativeEventQuery { definitionId = PrototypeNarrativeEventDefinitionFactory.HiddenFactionOfferDefinitionId, developmentView = true }).FirstOrDefault();
            NarrativeEventSnapshot redacted = runtime.Query(new NarrativeEventQuery { definitionId = PrototypeNarrativeEventDefinitionFactory.HiddenFactionOfferDefinitionId, developmentView = false }).FirstOrDefault();
            bool valid = result.Succeeded
                && development != null
                && redacted != null
                && development.IsHidden
                && development.ActionExecutions.Count > 0
                && redacted.ActionExecutions.Count == 0
                && redacted.MatchedConditions.Count == 0;
            return TestLabAssertions.True("step15-narrative-hidden", "Hidden narrative event projections redact action and condition details outside development views", valid, $"Result={result.Status} Hidden={development?.IsHidden ?? false} DevActions={development?.ActionExecutions.Count ?? 0} PublicActions={redacted?.ActionExecutions.Count ?? 0}");
        }

        private static TestLabAutomationStepResult NarrativeRequiredActionFailure(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeEventRuntime runtime = NarrativeRuntime(registry);
            NarrativeEventOperationResult result = runtime.RouteTrigger(new NarrativeTriggerRequest
            {
                transactionId = Tx(context, "narrative-required-failure"),
                source = Source(NarrativeTriggerCategory.LocationEntered, PrototypeNarrativeEventDefinitionFactory.DungeonEntrySignalId, "location.prototype.dungeon-entry", "person.prototype.player", 14d),
                conditionContext = NarrativeContext("person.prototype.player", locationId: "location.prototype.dungeon-entry", organizationIds: new[] { "organization.prototype.adventurers-guild" })
            });

            NarrativeEventSnapshot failed = runtime.Query(new NarrativeEventQuery { definitionId = PrototypeNarrativeEventDefinitionFactory.DungeonEntryQuestDefinitionId }).FirstOrDefault();
            bool valid = result.Status == NarrativeOperationStatus.ActionFailed
                && failed != null
                && failed.Lifecycle == NarrativeEventLifecycle.Failed
                && failed.ActionExecutions.Any(action => action.category == NarrativeActionCategory.InstantiateQuest && action.lifecycle == NarrativeActionLifecycle.Failed);
            return TestLabAssertions.True("step15-narrative-required-action", "Missing required owner runtime integration fails the narrative event without faking owner mutation", valid, $"Result={result.Status} Lifecycle={failed?.Lifecycle.ToString() ?? "None"} Actions={failed?.ActionExecutions.Count ?? 0}");
        }

        private static TestLabAutomationStepResult NarrativeCascadePersistence(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeEventRuntime runtime = NarrativeRuntime(registry);
            NarrativeEventOperationResult cascade = runtime.EmitSignal(new NarrativeSignalRequest
            {
                transactionId = Tx(context, "narrative-cascade"),
                signalDefinitionId = PrototypeNarrativeEventDefinitionFactory.CascadeStartSignalId,
                actorPersonId = "person.prototype.player",
                subjectIds = new[] { "subject.prototype.cascade" },
                conditionContext = NarrativeContext("person.prototype.player", subjectId: "subject.prototype.cascade"),
                worldTime = 20d
            });

            NarrativeEventPersistenceParticipant participant = new NarrativeEventPersistenceParticipant(runtime, () => registry, () => NarrativeIntegrations());
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            NarrativeEventRuntime restored = NarrativeRuntime(registry);
            NarrativeEventPersistenceParticipant restoredParticipant = new NarrativeEventPersistenceParticipant(restored, () => registry, () => NarrativeIntegrations());
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, NarrativeEventPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            NarrativeEventRuntimeSaveData corrupt = restored.CreateSaveData();
            if (corrupt.events.Count > 0) corrupt.events[0].eventDefinitionId = "narrative-event-definition.prototype.missing";
            int beforeReject = restored.Count;
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), NarrativeEventPersistenceParticipant.CurrentParticipantSchemaVersion);

            bool valid = cascade.Succeeded
                && runtime.Signals.Count >= 2
                && save.Succeeded
                && prepare.Succeeded
                && commit.Succeeded
                && restored.Count == runtime.Count
                && rejected.Succeeded == false
                && restored.Count == beforeReject;
            return TestLabAssertions.True("step15-narrative-persistence", "Narrative cascades persist and corrupt restore payloads are rejected before live mutation", valid, $"Cascade={cascade.Status} Signals={runtime.Signals.Count} Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Reject={rejected.Succeeded} Restored={restored.Count}");
        }

        private static TestLabAutomationStepResult NarrativeStateReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool hasAll = PrototypeNarrativeStateDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out NarrativeStateDefinition _));
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (NarrativeStateDefinition definition in PrototypeNarrativeStateDefinitionFactory.CreateMissingNarrativeStateDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            NarrativeStateRuntime runtime = StateRuntime(registry);
            bool valid = hasAll && report.ErrorCount == 0 && report.WarningCount == 0 && runtime.MaterializedStateCount == 0 && runtime.TransitionCount == 0;
            return TestLabAssertions.True("step15-narrative-state-readiness", "Narrative state definitions register and validate without materializing defaults", valid, $"Definitions={hasAll} Errors={report.ErrorCount} Warnings={report.WarningCount} States={runtime.MaterializedStateCount} Transitions={runtime.TransitionCount}");
        }

        private static TestLabAutomationStepResult NarrativeStateExclusiveBranches(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeStateRuntime runtime = StateRuntime(registry);

            NarrativeStateTransitionResult preview = runtime.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, Tx(context, "narrative-state-guild-preview"), "person.prototype.player", preview: true));
            NarrativeStateTransitionRequest request = StateRequest(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, Tx(context, "narrative-state-guild"), "person.prototype.player");
            NarrativeStateTransitionResult execute = runtime.RequestTransition(request);
            NarrativeStateTransitionResult duplicate = runtime.RequestTransition(request);
            NarrativeStateTransitionResult blocked = runtime.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.ChooseMerchantTransitionId, Tx(context, "narrative-state-merchant-blocked"), "person.prototype.player"));
            NarrativeStateTransitionResult otherPerson = runtime.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.ChooseMerchantTransitionId, Tx(context, "narrative-state-merchant-other"), "person.prototype.merchant"));
            bool snapshot = runtime.TryGetSnapshot(PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyDefinitionId, NarrativeStateScope.Person, "person.prototype.player", out NarrativeStateSnapshot player)
                && player.TryGetValue(PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyVariableId, out NarrativeVariableValueData value)
                && value.tokenValue == PrototypeNarrativeStateDefinitionFactory.GuildLoyalValueId;

            bool valid = preview.Preview
                && execute.Succeeded
                && duplicate.Duplicate
                && blocked.Status == NarrativeStateTransitionStatus.SourceValueMismatch
                && otherPerson.Succeeded
                && snapshot
                && runtime.MaterializedStateCount == 2
                && runtime.TransitionCount == 2;
            return TestLabAssertions.True("step15-narrative-state-exclusive", "Exclusive person branches preview, commit, deduplicate, and reject stale source values deterministically", valid, $"Preview={preview.Status} Execute={execute.Status} Duplicate={duplicate.Status}/{duplicate.Duplicate} Blocked={blocked.Status} Other={otherPerson.Status} States={runtime.MaterializedStateCount} Transitions={runtime.TransitionCount}");
        }

        private static TestLabAutomationStepResult NarrativeStateMergeTerminalHistory(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeStateRuntime runtime = StateRuntime(registry);
            NarrativeStateTransitionResult heir = runtime.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.SupportHeirTransitionId, Tx(context, "narrative-state-heir"), string.Empty, NarrativeStateScope.World, worldTime: 1d));
            NarrativeStateTransitionResult merge = runtime.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.ReconcileSuccessionTransitionId, Tx(context, "narrative-state-merge"), string.Empty, NarrativeStateScope.World, worldTime: 2d));
            NarrativeStateTransitionResult terminal = runtime.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.CrownHeirTransitionId, Tx(context, "narrative-state-crown"), string.Empty, NarrativeStateScope.World, worldTime: 3d));
            NarrativeStateTransitionResult afterTerminal = runtime.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.SupportRivalTransitionId, Tx(context, "narrative-state-rival-after-terminal"), string.Empty, NarrativeStateScope.World, worldTime: 4d));

            string scopeKey = PersistenceService.LocalWorldId;
            string atHeir = runtime.ValueAt(PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId, PrototypeNarrativeStateDefinitionFactory.RoyalBranchVariableId, NarrativeStateScope.World, scopeKey, 1d)?.tokenValue;
            string atMerge = runtime.ValueAt(PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId, PrototypeNarrativeStateDefinitionFactory.RoyalBranchVariableId, NarrativeStateScope.World, scopeKey, 2d)?.tokenValue;
            string atTerminal = runtime.ValueAt(PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId, PrototypeNarrativeStateDefinitionFactory.RoyalBranchVariableId, NarrativeStateScope.World, scopeKey, 3d)?.tokenValue;

            bool valid = heir.Succeeded
                && merge.Succeeded
                && terminal.Succeeded
                && (afterTerminal.Status == NarrativeStateTransitionStatus.TerminalState || afterTerminal.Status == NarrativeStateTransitionStatus.SourceValueMismatch)
                && atHeir == PrototypeNarrativeStateDefinitionFactory.RoyalSupportHeirValueId
                && atMerge == PrototypeNarrativeStateDefinitionFactory.RoyalReconciledValueId
                && atTerminal == PrototypeNarrativeStateDefinitionFactory.RoyalTerminalValueId
                && runtime.QueryTransitions(PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId).Count == 3;
            return TestLabAssertions.True("step15-narrative-state-history", "Merged and terminal world branches preserve deterministic historical values", valid, $"Heir={heir.Status} Merge={merge.Status} Terminal={terminal.Status} After={afterTerminal.Status} Values={atHeir}/{atMerge}/{atTerminal} Transitions={runtime.TransitionCount}");
        }

        private static TestLabAutomationStepResult NarrativeStateAccessAndAdapters(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeStateRuntime runtime = StateRuntime(registry);
            NarrativeStateTransitionResult opened = runtime.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.OpenInvestigationTransitionId, Tx(context, "narrative-state-open-investigation"), string.Empty, NarrativeStateScope.World, worldTime: 5d));
            bool development = runtime.TryGetSnapshot(PrototypeNarrativeStateDefinitionFactory.MayorInvestigationDefinitionId, NarrativeStateScope.World, PersistenceService.LocalWorldId, out NarrativeStateSnapshot dev, developmentView: true);
            bool publicView = runtime.TryGetSnapshot(PrototypeNarrativeStateDefinitionFactory.MayorInvestigationDefinitionId, NarrativeStateScope.World, PersistenceService.LocalWorldId, out NarrativeStateSnapshot redacted, developmentView: false);
            bool condition = runtime.EvaluateCondition(new NarrativeStateConditionQuery
            {
                stateDefinitionId = PrototypeNarrativeStateDefinitionFactory.MayorInvestigationDefinitionId,
                variableDefinitionId = PrototypeNarrativeStateDefinitionFactory.MayorStageVariableId,
                scope = NarrativeStateScope.World,
                scopeKey = PersistenceService.LocalWorldId,
                expectedValue = NarrativeVariableValueData.Token(PrototypeNarrativeStateDefinitionFactory.InvestigationOpenedValueId)
            });
            bool narrativeCondition = runtime.EvaluateCondition(new NarrativeStateConditionQuery
            {
                stateDefinitionId = PrototypeNarrativeStateDefinitionFactory.MayorInvestigationDefinitionId,
                variableDefinitionId = PrototypeNarrativeStateDefinitionFactory.MayorStageVariableId,
                scope = NarrativeStateScope.World,
                scopeKey = PersistenceService.LocalWorldId,
                expectedValue = NarrativeVariableValueData.Token(PrototypeNarrativeStateDefinitionFactory.InvestigationOpenedValueId)
            });
            bool questAdapter = new QuestEligibilityFactSet(narrativeStates: new[] { "narrative-state.prototype.mayor-investigation.opened" })
                .Contains(QuestEligibilityRequirementKind.NarrativeState, "narrative-state.prototype.mayor-investigation.opened");
            bool dialogueAdapter = new QuestEligibilityFactSet(narrativeStates: new[] { "narrative-state.prototype.dialogue.guild-loyal" })
                .Contains(QuestEligibilityRequirementKind.NarrativeState, "narrative-state.prototype.dialogue.guild-loyal");

            bool valid = opened.Succeeded
                && development
                && publicView
                && dev.IsHidden
                && dev.Variables.Count > 0
                && redacted.IsHidden
                && redacted.Variables.Count == 0
                && condition
                && narrativeCondition
                && questAdapter
                && dialogueAdapter;
            return TestLabAssertions.True("step15-narrative-state-adapters", "Hidden state projections redact values while dialogue, quest, and narrative conditions use access-safe state signals", valid, $"Open={opened.Status} Dev={development}/{dev?.Variables.Count ?? 0} Public={publicView}/{redacted?.Variables.Count ?? 0} Condition={condition} Narrative={narrativeCondition} Quest={questAdapter} Dialogue={dialogueAdapter}");
        }

        private static TestLabAutomationStepResult NarrativeStateEventTransitionCascade(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = RegistryWithNarrativeStateActionProbe(context);
            NarrativeStateRuntime states = StateRuntime(registry);
            NarrativeEventRuntime events = NarrativeRuntime(registry, integrations: new NarrativeEventRuntimeIntegrations
            {
                NarrativeStateTransitionExecutor = states.RequestTransition,
                NarrativeStateConditionEvaluator = (condition, conditionContext) => EvaluateNarrativeStateCondition(states, condition, conditionContext)
            });
            states.Configure(registry, StateIntegrations(events));

            NarrativeEventOperationResult result = events.EmitSignal(new NarrativeSignalRequest
            {
                transactionId = Tx(context, "narrative-state-event-signal"),
                signalDefinitionId = "narrative-signal-definition.prototype.15-9.state-action",
                actorPersonId = "person.prototype.player",
                subjectIds = new[] { "person.prototype.player" },
                conditionContext = NarrativeContext("person.prototype.player", narrativeStateIds: new[] { "narrative-state.prototype.guild.uncommitted" }),
                worldTime = 6d
            });
            bool state = states.TryGetSnapshot(PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyDefinitionId, NarrativeStateScope.Person, "person.prototype.player", out NarrativeStateSnapshot snapshot)
                && snapshot.TryGetValue(PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyVariableId, out NarrativeVariableValueData value)
                && value.tokenValue == PrototypeNarrativeStateDefinitionFactory.GuildLoyalValueId;
            bool action = events.Query(new NarrativeEventQuery { definitionId = "narrative-event-definition.prototype.15-9.state-action", developmentView = true })
                .SelectMany(item => item.ActionExecutions)
                .Any(item => item.category == NarrativeActionCategory.RequestNarrativeStateTransition && item.lifecycle == NarrativeActionLifecycle.Committed);

            bool valid = result.Succeeded && state && action && states.TransitionCount == 1 && events.Count == 1;
            return TestLabAssertions.True("step15-narrative-state-event", "Narrative events request state transitions through NarrativeStateRuntime instead of mutating state directly", valid, $"Event={result.Status} Events={events.Count} State={state} Actions={action} Transitions={states.TransitionCount}");
        }

        private static TestLabAutomationStepResult NarrativeStatePersistenceNoReplay(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeEventRuntime sourceEvents = NarrativeRuntime(registry);
            NarrativeStateRuntime runtime = StateRuntime(registry, sourceEvents);
            NarrativeStateTransitionResult transition = runtime.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, Tx(context, "narrative-state-persist-guild"), "person.prototype.player", worldTime: 7d));
            NarrativeStatePersistenceParticipant participant = new NarrativeStatePersistenceParticipant(runtime, () => registry);
            PersistenceParticipantSaveResult save = participant.CapturePayload();

            NarrativeEventRuntime restoredEvents = NarrativeRuntime(registry);
            NarrativeStateRuntime restored = StateRuntime(registry, restoredEvents);
            NarrativeStatePersistenceParticipant restoredParticipant = new NarrativeStatePersistenceParticipant(restored, () => registry);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, NarrativeStatePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            bool restoredState = restored.TryGetSnapshot(PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyDefinitionId, NarrativeStateScope.Person, "person.prototype.player", out NarrativeStateSnapshot snapshot)
                && snapshot.TryGetValue(PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyVariableId, out NarrativeVariableValueData value)
                && value.tokenValue == PrototypeNarrativeStateDefinitionFactory.GuildLoyalValueId;

            NarrativeStateRuntimeSaveData corrupt = runtime.CreateSaveData();
            if (corrupt.states.Length > 0) corrupt.states[0].stateDefinitionId = "narrative-state-definition.prototype.missing";
            int beforeReject = restored.MaterializedStateCount;
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), NarrativeStatePersistenceParticipant.CurrentParticipantSchemaVersion);

            bool valid = transition.Succeeded
                && save.Succeeded
                && prepare.Succeeded
                && commit.Succeeded
                && restoredState
                && restored.TransitionCount == runtime.TransitionCount
                && restoredEvents.Count == 0
                && rejected.Succeeded == false
                && restored.MaterializedStateCount == beforeReject;
            return TestLabAssertions.True("step15-narrative-state-persistence", "Narrative state restore preserves state and history without replaying consequences, and corrupt payloads fail before mutation", valid, $"Transition={transition.Status} Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Restored={restoredState} ReplayEvents={restoredEvents.Count} Reject={rejected.Succeeded} States={restored.MaterializedStateCount}");
        }

        private static TestLabAutomationStepResult NarrativeArcReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            bool hasAll = PrototypeNarrativeArcDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out NarrativeArcDefinition _));
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (NarrativeArcDefinition definition in PrototypeNarrativeArcDefinitionFactory.CreateMissingNarrativeArcDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            NarrativeArcValidationReport graph = NarrativeArcDefinitionValidator.ValidateGraph(registry.DefinitionsById.Values.OfType<NarrativeArcDefinition>().Select(definition => definition.ToRecordData()));
            NarrativeArcRuntime runtime = ArcRuntime(registry, out _, out _);
            bool valid = hasAll && report.ErrorCount == 0 && report.WarningCount == 0 && graph.Succeeded && runtime.Count == 0 && runtime.TransactionCount == 0;
            return TestLabAssertions.True("step15-narrative-arc-readiness", "Narrative arc definitions register, validate, and remain inactive until explicitly started", valid, $"Definitions={hasAll} Errors={report.ErrorCount} Warnings={report.WarningCount} Graph={graph.Succeeded} Arcs={runtime.Count} Transactions={runtime.TransactionCount}");
        }

        private static TestLabAutomationStepResult NarrativeArcStateDrivenQuestBinding(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeArcRuntime arcs = ArcRuntime(registry, out NarrativeStateRuntime states, out QuestRuntime quests);
            NarrativeArcOperationResult start = arcs.StartArc(ArcStart(context, PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId, "person.prototype.player", "arc-guild-start"));
            NarrativeStateTransitionResult state = states.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, Tx(context, "arc-guild-state"), "person.prototype.player"));
            NarrativeArcOperationResult signal = arcs.ApplySignal(ArcSignal(context, "arc-guild-state-signal", PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId, NarrativeArcSignalCategory.NarrativeState, actor: "person.prototype.player", sourceId: PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyDefinitionId));

            NarrativeArcSnapshot snapshot = arcs.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId }).SingleOrDefault();
            NarrativeArcStageLifecycle join = snapshot?.Stages.Single(stage => stage.StageDefinitionId == PrototypeNarrativeArcDefinitionFactory.GuildIntroJoinStageId).Lifecycle ?? NarrativeArcStageLifecycle.Unknown;
            NarrativeArcStageLifecycle posting = snapshot?.Stages.Single(stage => stage.StageDefinitionId == PrototypeNarrativeArcDefinitionFactory.GuildIntroPostingStageId).Lifecycle ?? NarrativeArcStageLifecycle.Unknown;
            int questsCreated = quests.Query(new QuestQuery { definitionId = PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count;

            bool valid = start.Succeeded && state.Succeeded && signal.Succeeded && join == NarrativeArcStageLifecycle.Completed && posting == NarrativeArcStageLifecycle.Active && questsCreated == 1;
            return TestLabAssertions.True("step15-narrative-arc-state-quest", "Narrative state progression completes a chain stage and delegates quest binding to QuestRuntime", valid, $"Start={start.Status} State={state.Status} Signal={signal.Status} Join={join} Posting={posting} Quests={questsCreated}");
        }

        private static TestLabAutomationStepResult NarrativeArcQuestOutcomeBranching(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeArcRuntime completedArcs = ArcRuntime(registry, out _, out QuestRuntime completedQuests);
            NarrativeArcOperationResult completedStart = completedArcs.StartArc(ArcStart(context, PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId, "person.prototype.player", "arc-merchant-complete-start"));
            QuestSnapshot completedQuest = completedQuests.Query(new QuestQuery { definitionId = PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).SingleOrDefault();
            NarrativeArcOperationResult completed = completedArcs.ApplySignal(ArcSignal(context, "arc-merchant-completed", PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId, NarrativeArcSignalCategory.QuestOutcome, actor: "person.prototype.player", questId: completedQuest?.QuestId, questDefinitionId: completedQuest?.QuestDefinitionId, outcome: QuestTerminalOutcomeKind.Completed));

            NarrativeArcRuntime failedArcs = ArcRuntime(registry, out _, out QuestRuntime failedQuests);
            NarrativeArcOperationResult failedStart = failedArcs.StartArc(ArcStart(context, PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId, "person.prototype.player", "arc-merchant-fail-start"));
            QuestSnapshot failedQuest = failedQuests.Query(new QuestQuery { definitionId = PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).SingleOrDefault();
            NarrativeArcOperationResult failed = failedArcs.ApplySignal(ArcSignal(context, "arc-merchant-failed", PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId, NarrativeArcSignalCategory.QuestOutcome, actor: "person.prototype.player", questId: failedQuest?.QuestId, questDefinitionId: failedQuest?.QuestDefinitionId, outcome: QuestTerminalOutcomeKind.Failed));

            NarrativeArcStageLifecycle completedStage = completedArcs.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId }).SingleOrDefault()?.Stages.Single().Lifecycle ?? NarrativeArcStageLifecycle.Unknown;
            NarrativeArcStageLifecycle failedStage = failedArcs.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId }).SingleOrDefault()?.Stages.Single().Lifecycle ?? NarrativeArcStageLifecycle.Unknown;
            bool questOwned = completedQuest?.LifecycleState == QuestRuntimeLifecycleState.Available && failedQuest?.LifecycleState == QuestRuntimeLifecycleState.Available;

            bool valid = completedStart.Succeeded && failedStart.Succeeded && completed.Succeeded && failed.Succeeded && completedStage == NarrativeArcStageLifecycle.Completed && failedStage == NarrativeArcStageLifecycle.Skipped && questOwned;
            return TestLabAssertions.True("step15-narrative-arc-quest-outcome", "Quest outcome signals complete or skip arc stages without mutating the owning quest records", valid, $"Complete={completed.Status}/{completedStage} Failed={failed.Status}/{failedStage} QuestOwned={questOwned}");
        }

        private static TestLabAutomationStepResult NarrativeArcParallelConvergence(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            NarrativeArcRuntime arcs = ArcRuntime(registry, out _, out _);
            NarrativeArcOperationResult start = arcs.StartArc(ArcStart(context, PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId, "person.prototype.player", "arc-parallel-start"));
            NarrativeArcOperationResult first = arcs.ApplySignal(ArcSignal(context, "arc-parallel-a", PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId, NarrativeArcSignalCategory.Custom, actor: "person.prototype.player", value: "signal.parallel.a"));
            NarrativeArcSnapshot afterOne = arcs.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId }).SingleOrDefault();
            NarrativeArcOperationResult second = arcs.ApplySignal(ArcSignal(context, "arc-parallel-b", PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId, NarrativeArcSignalCategory.Custom, actor: "person.prototype.player", value: "signal.parallel.b"));
            NarrativeArcSnapshot afterTwo = arcs.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId }).SingleOrDefault();
            NarrativeArcOperationResult duplicate = arcs.ApplySignal(ArcSignal(context, "arc-parallel-b", PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId, NarrativeArcSignalCategory.Custom, actor: "person.prototype.player", value: "signal.parallel.b"));

            NarrativeArcStageLifecycle joinAfterOne = afterOne?.Stages.Single(stage => stage.StageDefinitionId == PrototypeNarrativeArcDefinitionFactory.ParallelJoinStageId).Lifecycle ?? NarrativeArcStageLifecycle.Unknown;
            NarrativeArcStageLifecycle joinAfterTwo = afterTwo?.Stages.Single(stage => stage.StageDefinitionId == PrototypeNarrativeArcDefinitionFactory.ParallelJoinStageId).Lifecycle ?? NarrativeArcStageLifecycle.Unknown;
            bool valid = start.Succeeded && first.Succeeded && second.Succeeded && duplicate.Duplicate && joinAfterOne == NarrativeArcStageLifecycle.Locked && joinAfterTwo == NarrativeArcStageLifecycle.Active;
            return TestLabAssertions.True("step15-narrative-arc-parallel", "Parallel arc branches converge only after the authored two-of-three dependency is satisfied", valid, $"Start={start.Status} First={first.Status} Second={second.Status} Duplicate={duplicate.Status}/{duplicate.Duplicate} Join={joinAfterOne}->{joinAfterTwo}");
        }

        private static TestLabAutomationStepResult NarrativeArcEventStateHooks(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = RegistryWithNarrativeArcActionProbe(context);
            NarrativeArcRuntime arcs = ArcRuntime(registry, out _, out QuestRuntime quests);
            NarrativeEventRuntime events = NarrativeRuntime(registry, quests: quests, integrations: NarrativeIntegrations(quests));
            events.Configure(registry, NarrativeIntegrations(quests));
            events.Configure(registry, new NarrativeEventRuntimeIntegrations
            {
                QuestRuntime = quests,
                NarrativeArcSignalExecutor = arcs.ApplySignal,
                NarrativeArcConditionEvaluator = arcs.EvaluateCondition
            });

            const string probeArcId = "narrative-arc-definition.prototype.15-10.event-hook";
            const string probeStageId = "narrative-arc-stage-definition.prototype.15-10.event-hook.stage";
            NarrativeArcOperationResult start = arcs.StartArc(ArcStart(context, probeArcId, "person.prototype.player", "arc-event-hook-start"));
            NarrativeEventOperationResult emitted = events.EmitSignal(new NarrativeSignalRequest
            {
                transactionId = Tx(context, "arc-event-hook-signal"),
                signalDefinitionId = "narrative-signal-definition.prototype.15-10.arc-progress",
                actorPersonId = "person.prototype.player",
                subjectIds = new[] { "person.prototype.player" },
                conditionContext = NarrativeContext("person.prototype.player", subjectId: "person.prototype.player"),
                worldTime = 4d
            });

            NarrativeArcSnapshot snapshot = arcs.Query(new NarrativeArcQuery { arcDefinitionId = probeArcId }).SingleOrDefault();
            NarrativeArcStageLifecycle stage = snapshot?.Stages.Single(value => value.StageDefinitionId == probeStageId).Lifecycle ?? NarrativeArcStageLifecycle.Unknown;
            bool actionCommitted = events.Query(new NarrativeEventQuery { definitionId = "narrative-event-definition.prototype.15-10.arc-progress", developmentView = true })
                .SelectMany(item => item.ActionExecutions)
                .Any(action => action.category == NarrativeActionCategory.RequestNarrativeArcProgression && action.lifecycle == NarrativeActionLifecycle.Committed);
            bool valid = start.Succeeded && emitted.Succeeded && stage == NarrativeArcStageLifecycle.Completed && actionCommitted && arcs.Count == 1 && events.Count == 1;
            return TestLabAssertions.True("step15-narrative-arc-event-hooks", "Narrative events request arc progression through the arc runtime integration hook", valid, $"Start={start.Status} Event={emitted.Status} Stage={stage} Action={actionCommitted} Arcs={arcs.Count} Events={events.Count}");
        }

        private static TestLabAutomationStepResult NarrativeArcPersistenceNoReplay(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            int actions = 0;
            NarrativeArcRuntime runtime = new NarrativeArcRuntime(registry, new NarrativeArcRuntimeIntegrations
            {
                QuestRuntime = new QuestRuntime(registry, PersistenceService.LocalWorldId),
                ActionExecutor = (_, _) => ++actions >= 0
            }, PersistenceService.LocalWorldId);
            NarrativeArcOperationResult start = runtime.StartArc(ArcStart(context, PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, string.Empty, "arc-persist-start", NarrativeArcScope.World));
            NarrativeArcSnapshot publicView = runtime.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, developmentView = false }).SingleOrDefault();
            NarrativeArcPersistenceParticipant participant = new NarrativeArcPersistenceParticipant(runtime, () => registry, () => new NarrativeArcRuntimeIntegrations { QuestRuntime = new QuestRuntime(registry, PersistenceService.LocalWorldId), ActionExecutor = (_, _) => ++actions >= 0 });
            PersistenceParticipantSaveResult save = participant.CapturePayload();

            NarrativeArcRuntime restored = new NarrativeArcRuntime(registry, new NarrativeArcRuntimeIntegrations { QuestRuntime = new QuestRuntime(registry, PersistenceService.LocalWorldId), ActionExecutor = (_, _) => ++actions >= 0 }, PersistenceService.LocalWorldId);
            NarrativeArcPersistenceParticipant restoredParticipant = new NarrativeArcPersistenceParticipant(restored, () => registry, () => new NarrativeArcRuntimeIntegrations { QuestRuntime = new QuestRuntime(registry, PersistenceService.LocalWorldId), ActionExecutor = (_, _) => ++actions >= 0 });
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, NarrativeArcPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            NarrativeArcRuntimeSaveData corrupt = restored.CreateSaveData();
            if (corrupt.arcs.Count > 0 && corrupt.arcs[0].stages.Length > 0) corrupt.arcs[0].stages[0].stageRuntimeId = "narrative-arc-stage-runtime.corrupt";
            int beforeReject = restored.Count;
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), NarrativeArcPersistenceParticipant.CurrentParticipantSchemaVersion);

            bool restoredArc = restored.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, developmentView = true }).Count == 1;
            bool redacted = publicView != null && publicView.IsHidden && publicView.Stages.Count == 0;
            bool valid = start.Succeeded && redacted && save.Succeeded && prepare.Succeeded && commit.Succeeded && restoredArc && actions == 0 && rejected.Succeeded == false && restored.Count == beforeReject;
            return TestLabAssertions.True("step15-narrative-arc-persistence", "Narrative arc restore preserves state without replaying delegated actions, redacts hidden stages, and rejects corrupt payloads before mutation", valid, $"Start={start.Status} Redacted={redacted} Save={save.Succeeded} Prepare={prepare.Succeeded} Commit={commit.Succeeded} Restored={restoredArc} Actions={actions} Reject={rejected.Succeeded} Arcs={restored.Count}");
        }

        private static DefinitionRegistry Registry(TestLabAutomationContext context)
        {
            DefinitionRegistry baseRegistry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            return PrototypeNarrativeArcDefinitionFactory.AddMissingPrototypeNarrativeArcDefinitions(PrototypeNarrativeStateDefinitionFactory.AddMissingPrototypeNarrativeStateDefinitions(PrototypeNarrativeEventDefinitionFactory.AddMissingPrototypeNarrativeEventDefinitions(PrototypeDialogueGraphDefinitionFactory.AddMissingPrototypeDialogueGraphDefinitions(PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(PrototypeQuestSourceDefinitionFactory.AddMissingPrototypeQuestSourceDefinitions(PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(baseRegistry)))))));
        }

        private static QuestRuntime Runtime(TestLabAutomationContext context)
        {
            return new QuestRuntime(Registry(context), PersistenceService.LocalWorldId);
        }

        private static QuestParticipationRuntime Participation(QuestRuntime quests, DefinitionRegistry registry)
        {
            return new QuestParticipationRuntime(quests, registry, PersistenceService.LocalWorldId);
        }

        private static QuestObjectiveProgressRuntime Objectives(QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry registry)
        {
            return new QuestObjectiveProgressRuntime(quests, participation, registry, PersistenceService.LocalWorldId);
        }

        private static QuestOutcomeRuntime Outcomes(QuestRuntime quests, QuestParticipationRuntime participation, QuestObjectiveProgressRuntime objectives, DefinitionRegistry registry, IQuestRewardEffectExecutor executor)
        {
            return new QuestOutcomeRuntime(quests, participation, objectives, registry, executor, PersistenceService.LocalWorldId);
        }

        private static QuestSourceRuntime Sources(QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry registry)
        {
            return new QuestSourceRuntime(quests, participation, registry, PersistenceService.LocalWorldId);
        }

        private static ConversationRuntime Conversations(DefinitionRegistry registry)
        {
            return new ConversationRuntime(registry, PersistenceService.LocalWorldId);
        }

        private static NarrativeEventRuntime NarrativeRuntime(DefinitionRegistry registry, QuestRuntime quests = null, QuestSourceRuntime sources = null, ConversationRuntime conversations = null, NarrativeEventRuntimeIntegrations integrations = null)
        {
            return new NarrativeEventRuntime(registry, integrations ?? NarrativeIntegrations(quests, sources, conversations), PersistenceService.LocalWorldId);
        }

        private static NarrativeEventRuntimeIntegrations NarrativeIntegrations(QuestRuntime quests = null, QuestSourceRuntime sources = null, ConversationRuntime conversations = null)
        {
            return new NarrativeEventRuntimeIntegrations
            {
                QuestRuntime = quests,
                QuestSourceRuntime = sources,
                ConversationRuntime = conversations,
                InformationGrantExecutor = target => !string.IsNullOrWhiteSpace(target),
                TravelConditionExecutor = target => !string.IsNullOrWhiteSpace(target),
                ConnectionChangeExecutor = target => !string.IsNullOrWhiteSpace(target),
                SocialActionExecutor = target => !string.IsNullOrWhiteSpace(target),
                OrganizationActionExecutor = target => !string.IsNullOrWhiteSpace(target),
                LegalActionExecutor = target => !string.IsNullOrWhiteSpace(target)
            };
        }

        private static NarrativeStateRuntime StateRuntime(DefinitionRegistry registry, NarrativeEventRuntime narrativeEvents = null)
        {
            return new NarrativeStateRuntime(registry, StateIntegrations(narrativeEvents), PersistenceService.LocalWorldId);
        }

        private static NarrativeStateRuntimeIntegrations StateIntegrations(NarrativeEventRuntime narrativeEvents = null)
        {
            return new NarrativeStateRuntimeIntegrations
            {
                NarrativeEventRuntime = narrativeEvents,
                ConsequenceValidator = (action, request) => action.category == NarrativeActionCategory.None || !string.IsNullOrWhiteSpace(action.targetId),
                ConsequenceExecutor = (action, request) => !string.IsNullOrWhiteSpace(action.targetId) ? action.targetId : string.Empty
            };
        }

        private static NarrativeStateTransitionRequest StateRequest(string transitionId, string transactionId, string actorPersonId, NarrativeStateScope scope = NarrativeStateScope.Person, bool preview = false, double worldTime = 1d)
        {
            return new NarrativeStateTransitionRequest
            {
                transactionId = transactionId,
                transitionDefinitionId = transitionId,
                scope = scope,
                scopeKey = scope == NarrativeStateScope.World ? PersistenceService.LocalWorldId : actorPersonId,
                actorPersonId = actorPersonId,
                sourceKind = NarrativeTransitionSourceKind.Development,
                sourceId = "testlab.feature.15.9",
                conditionContext = NarrativeContext(actorPersonId),
                preview = preview,
                worldTime = worldTime
            };
        }

        private static NarrativeArcRuntime ArcRuntime(DefinitionRegistry registry, out NarrativeStateRuntime states, out QuestRuntime quests, NarrativeEventRuntime events = null, QuestOutcomeRuntime outcomes = null)
        {
            states = StateRuntime(registry, events);
            quests = new QuestRuntime(registry, PersistenceService.LocalWorldId);
            return new NarrativeArcRuntime(registry, ArcIntegrations(quests, states, events, outcomes), PersistenceService.LocalWorldId);
        }

        private static NarrativeArcRuntimeIntegrations ArcIntegrations(QuestRuntime quests, NarrativeStateRuntime states, NarrativeEventRuntime events = null, QuestOutcomeRuntime outcomes = null)
        {
            return new NarrativeArcRuntimeIntegrations
            {
                QuestRuntime = quests,
                QuestOutcomeRuntime = outcomes,
                NarrativeEventRuntime = events,
                NarrativeStateRuntime = states
            };
        }

        private static NarrativeArcStartRequest ArcStart(TestLabAutomationContext context, string arcDefinitionId, string actorPersonId, string operation, NarrativeArcScope scope = NarrativeArcScope.Person, bool preview = false, double worldTime = 1d)
        {
            return new NarrativeArcStartRequest
            {
                transactionId = Tx(context, operation),
                arcDefinitionId = arcDefinitionId,
                actorPersonId = actorPersonId,
                scopeKey = scope == NarrativeArcScope.World ? PersistenceService.LocalWorldId : actorPersonId,
                subjectId = actorPersonId,
                conditionContext = NarrativeContext(actorPersonId, subjectId: actorPersonId),
                worldTime = worldTime,
                preview = preview
            };
        }

        private static NarrativeArcSignalRequest ArcSignal(
            TestLabAutomationContext context,
            string operation,
            string arcDefinitionId,
            NarrativeArcSignalCategory category,
            string actor = "person.prototype.player",
            string sourceId = "",
            string value = "",
            string questId = "",
            string questDefinitionId = "",
            QuestTerminalOutcomeKind outcome = QuestTerminalOutcomeKind.Unknown,
            double worldTime = 2d)
        {
            string actualSource = string.IsNullOrWhiteSpace(sourceId) ? value : sourceId;
            return new NarrativeArcSignalRequest
            {
                transactionId = Tx(context, operation),
                arcDefinitionId = arcDefinitionId,
                category = category,
                signalId = Tx(context, $"{operation}-signal"),
                sourceId = actualSource,
                value = value,
                questId = questId,
                questDefinitionId = questDefinitionId,
                questOutcomeKind = outcome,
                actorPersonId = actor,
                subjectId = actor,
                scopeKey = string.IsNullOrWhiteSpace(actor) ? PersistenceService.LocalWorldId : actor,
                conditionContext = NarrativeContext(actor, subjectId: actor, narrativeStateIds: string.IsNullOrWhiteSpace(sourceId) ? Array.Empty<string>() : new[] { sourceId }, customStateIds: string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value }),
                worldTime = worldTime
            };
        }

        private static bool EvaluateNarrativeStateCondition(NarrativeStateRuntime runtime, NarrativeConditionDefinitionData condition, NarrativeConditionContextData context)
        {
            if (runtime == null || condition == null) return false;
            string[] parts = (condition.requiredId ?? string.Empty).Split('|');
            NarrativeStateScope scope = NarrativeStateScope.World;
            if (parts.Length > 3 && Enum.TryParse(parts[3], ignoreCase: true, out NarrativeStateScope parsedScope)) scope = parsedScope;
            string scopeKey = parts.Length > 4 ? parts[4] : string.Empty;
            if (string.IsNullOrWhiteSpace(scopeKey)) scopeKey = scope == NarrativeStateScope.Person ? context?.actorPersonId : PersistenceService.LocalWorldId;
            bool matched = runtime.EvaluateCondition(new NarrativeStateConditionQuery
            {
                stateDefinitionId = parts.Length > 0 ? parts[0] : string.Empty,
                variableDefinitionId = parts.Length > 1 ? parts[1] : string.Empty,
                scope = scope,
                scopeKey = scopeKey,
                expectedValue = parts.Length > 2 ? NarrativeVariableValueData.Token(parts[2]) : null,
                minimumValue = condition.minimumValue
            });
            return condition.negate ? !matched : matched;
        }

        private static NarrativeTriggerSourceData Source(NarrativeTriggerCategory category, string sourceId, string subjectId, string actorId, double worldTime)
        {
            return new NarrativeTriggerSourceData
            {
                category = category,
                sourceId = sourceId,
                sourceTransactionId = $"source.{NarrativeModelUtility.SanitizeForId(sourceId)}.{worldTime:0}",
                actorPersonId = actorId,
                targetId = subjectId,
                subjectId = subjectId,
                ownerRuntime = "TestLabAutomation",
                worldTime = worldTime,
                committed = true
            };
        }

        private static NarrativeConditionContextData NarrativeContext(
            string actorId,
            string locationId = "",
            string conversationId = "",
            string subjectId = "",
            string[] knownIds = null,
            string[] dialogueIds = null,
            string[] narrativeStateIds = null,
            string[] organizationIds = null,
            string[] socialIds = null,
            string[] customStateIds = null)
        {
            return new NarrativeConditionContextData
            {
                actorPersonId = actorId,
                locationId = locationId,
                conversationId = conversationId,
                subjectId = subjectId,
                worldTime = 10d,
                knownSubjectIds = knownIds ?? Array.Empty<string>(),
                dialogueStateIds = dialogueIds ?? Array.Empty<string>(),
                narrativeStateIds = narrativeStateIds ?? Array.Empty<string>(),
                organizationStateIds = organizationIds ?? Array.Empty<string>(),
                socialStateIds = socialIds ?? Array.Empty<string>(),
                customStateIds = customStateIds ?? Array.Empty<string>()
            };
        }

        private static DefinitionRegistry RegistryWithNarrativeArcActionProbe(TestLabAutomationContext context)
        {
            const string eventDefinitionId = "narrative-event-definition.prototype.15-10.arc-progress";
            const string signalDefinitionId = "narrative-signal-definition.prototype.15-10.arc-progress";
            const string arcDefinitionId = "narrative-arc-definition.prototype.15-10.event-hook";
            const string stageDefinitionId = "narrative-arc-stage-definition.prototype.15-10.event-hook.stage";

            DefinitionRegistry registry = Registry(context);
            List<IGameDefinition> definitions = registry.DefinitionsById.Values.Where(definition => definition != null).ToList();

            NarrativeEventDefinition eventDefinition = ScriptableObject.CreateInstance<NarrativeEventDefinition>();
            eventDefinition.name = "Narrative Arc Progression Probe";
            eventDefinition.DevelopmentConfigure(new NarrativeEventDefinitionData
            {
                eventDefinitionId = eventDefinitionId,
                displayName = "Narrative Arc Progression Probe",
                category = NarrativeEventCategory.Scripted,
                scope = NarrativeEventScope.OncePerPerson,
                repeatPolicy = NarrativeRepeatPolicy.OncePerScope,
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                triggerMode = NarrativeTriggerMode.TriggerImmediatelyWhenMatched,
                scopeSelectorId = "actor",
                triggers = new[]
                {
                    new NarrativeTriggerDefinitionData
                    {
                        triggerDefinitionId = "narrative-trigger-definition.prototype.15-10.arc-progress",
                        category = NarrativeTriggerCategory.ExplicitSignal,
                        requiredSourceId = signalDefinitionId
                    }
                },
                actions = new[]
                {
                    new NarrativeActionDefinitionData
                    {
                        actionDefinitionId = "narrative-action-definition.prototype.15-10.request-arc-progress",
                        category = NarrativeActionCategory.RequestNarrativeArcProgression,
                        requirement = NarrativeActionRequirement.Required,
                        targetId = stageDefinitionId,
                        secondaryTargetId = arcDefinitionId
                    }
                },
                tagIds = new[] { "prototype", "testlab", "narrative-arc" }
            });
            definitions.Add(eventDefinition);

            NarrativeArcDefinition arcDefinition = ScriptableObject.CreateInstance<NarrativeArcDefinition>();
            arcDefinition.name = "Narrative Arc Event Hook Probe";
            arcDefinition.DevelopmentConfigure(new NarrativeArcDefinitionData
            {
                arcDefinitionId = arcDefinitionId,
                displayName = "Narrative Arc Event Hook Probe",
                scope = NarrativeArcScope.Person,
                visibility = NarrativeEventVisibility.ParticipantKnown,
                stages = new[]
                {
                    new NarrativeArcStageDefinitionData
                    {
                        stageDefinitionId = stageDefinitionId,
                        displayName = "Event hook stage",
                        initial = true,
                        terminalOnCompletion = true,
                        completionDependencies = new[]
                        {
                            new NarrativeArcDependencyDefinitionData
                            {
                                dependencyDefinitionId = "dependency.15-10.event-hook",
                                kind = NarrativeArcDependencyKind.NarrativeEvent,
                                requiredId = eventDefinitionId
                            }
                        }
                    }
                },
                tagIds = new[] { "prototype", "testlab", "narrative-arc" }
            });
            definitions.Add(arcDefinition);

            return new DefinitionRegistry(definitions);
        }

        private static DefinitionRegistry RegistryWithNarrativeStateActionProbe(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            List<IGameDefinition> definitions = registry.DefinitionsById.Values.Where(definition => definition != null).ToList();
            NarrativeEventDefinition definition = ScriptableObject.CreateInstance<NarrativeEventDefinition>();
            definition.name = "Narrative State Transition Probe";
            definition.DevelopmentConfigure(new NarrativeEventDefinitionData
            {
                eventDefinitionId = "narrative-event-definition.prototype.15-9.state-action",
                displayName = "Narrative State Transition Probe",
                category = NarrativeEventCategory.Scripted,
                scope = NarrativeEventScope.OncePerPerson,
                repeatPolicy = NarrativeRepeatPolicy.OncePerScope,
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                triggerMode = NarrativeTriggerMode.TriggerImmediatelyWhenMatched,
                scopeSelectorId = "actor",
                triggers = new[]
                {
                    new NarrativeTriggerDefinitionData
                    {
                        triggerDefinitionId = "narrative-trigger-definition.prototype.15-9.state-action",
                        category = NarrativeTriggerCategory.ExplicitSignal,
                        requiredSourceId = "narrative-signal-definition.prototype.15-9.state-action"
                    }
                },
                actions = new[]
                {
                    new NarrativeActionDefinitionData
                    {
                        actionDefinitionId = "narrative-action-definition.prototype.15-9.choose-guild",
                        category = NarrativeActionCategory.RequestNarrativeStateTransition,
                        requirement = NarrativeActionRequirement.Required,
                        targetId = PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId
                    }
                },
                tagIds = new[] { "prototype", "testlab", "narrative-state" }
            });
            definitions.Add(definition);
            return new DefinitionRegistry(definitions);
        }

        private static string Tx(TestLabAutomationContext context, string operation)
        {
            return context?.TransactionIds?.Create(context.CurrentSuiteId, context.CurrentScenarioId, context.RunId, context.CurrentStepIndex, operation)
                ?? $"testlab.step15.{operation}";
        }

        private static ConversationOperationResult StartGuildConversation(ConversationRuntime conversations, string key)
        {
            return conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = $"tx.dialogue.{key}.conversation",
                conversationId = $"conversation.prototype.dialogue.{key}",
                conversationDefinitionId = PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId,
                participants = GuildCounterParticipants("interaction-point.prototype.adventurer-guild-counter"),
                hostLocationId = "location.prototype.adventurers-guild",
                hostInteractionPointId = "interaction-point.prototype.adventurer-guild-counter",
                questId = "quest.prototype.guild.counter",
                questSourceId = "quest-source.prototype.guild-counter",
                questListingId = "quest-listing.prototype.guild-counter",
                operatingOrganizationId = "organization.prototype.adventurers-guild",
                sceneBindingKey = "scene.prototype.guild.counter",
                worldTime = 1d
            });
        }

        private static DialogueConditionContext GuildDialogueContext(bool rank = false)
        {
            return new DialogueConditionContext
            {
                actorPersonId = "person.prototype.player",
                listenerPersonId = "person.prototype.player",
                locationId = "location.prototype.adventurers-guild",
                interactionPointId = "interaction-point.prototype.adventurer-guild-counter",
                worldTime = 1d,
                facts = new QuestEligibilityFactSet(
                    organizationMemberships: new[] { "organization.prototype.adventurers-guild" },
                    organizationRanks: rank ? new[] { "rank.prototype.adventurers.silver" } : Array.Empty<string>(),
                    authorityGrants: new[] { "authority.prototype.guild.quest-offer", "authority.prototype.records.read", "authority.prototype.city.quest-assign" },
                    knownSubjects: new[] { "subject.prototype.hidden-dungeon" }),
                activeQuestIds = new[] { "quest.prototype.guild.counter" },
                activeOfferIds = new[] { "offer.prototype.guild.counter" },
                activeAssignmentQuestIds = Array.Empty<string>(),
                completedQuestIds = Array.Empty<string>(),
                claimableRewardIds = Array.Empty<string>()
            };
        }

        private static DefinitionRegistry RegistryWithRequiredEffectGraph(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            List<IGameDefinition> definitions = registry.DefinitionsById.Values.Where(definition => definition != null).ToList();
            DialogueGraphDefinition graph = ScriptableObject.CreateInstance<DialogueGraphDefinition>();
            graph.name = "Required Effect Test Dialogue";
            graph.DevelopmentConfigure(
                "dialogue-graph.prototype.required-effect-test",
                "Required Effect Test Dialogue",
                PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId,
                "required.entry",
                "required.end",
                new[]
                {
                    new DialogueNodeDefinitionData
                    {
                        nodeId = "required.entry",
                        category = DialogueNodeCategory.ChoicePrompt,
                        authoredText = "Required effect test.",
                        speaker = new DialogueSpeakerSelectorData { kind = DialogueSpeakerSelectorKind.Provider },
                        listener = new DialogueListenerSelectorData { kind = DialogueListenerSelectorKind.AllParticipants },
                        choices = new[]
                        {
                            new DialogueChoiceDefinitionData
                            {
                                choiceId = "required.choice",
                                displayText = "Run required owner effect",
                                category = DialogueChoiceCategory.ServiceRequest,
                                effects = new[]
                                {
                                    new DialogueEffectData
                                    {
                                        effectId = "effect.required.owner",
                                        kind = DialogueEffectKind.CreateQuestOffer,
                                        requirement = DialogueEffectRequirement.Required,
                                        targetId = "quest.prototype.guild.counter"
                                    }
                                }
                            }
                        }
                    },
                    new DialogueNodeDefinitionData { nodeId = "required.end", category = DialogueNodeCategory.End, authoredText = "End.", speaker = new DialogueSpeakerSelectorData { kind = DialogueSpeakerSelectorKind.None } }
                },
                tags: new[] { "prototype", "test", "required-effect" });
            definitions.Add(graph);
            return new DefinitionRegistry(definitions);
        }

        private static ConversationParticipantRecordData[] GuildCounterParticipants(string interactionPointId)
        {
            return new[]
            {
                Participant("person.prototype.player", ConversationParticipantRole.Initiator, "location.prototype.adventurers-guild", interactionPointId),
                Participant("person.prototype.guild-clerk", ConversationParticipantRole.Provider, "location.prototype.adventurers-guild", interactionPointId, organizationId: "organization.prototype.adventurers-guild"),
                Participant("person.prototype.player", ConversationParticipantRole.QuestRecipient, "location.prototype.adventurers-guild", interactionPointId)
            };
        }

        private static ConversationParticipantRecordData Participant(string personId, ConversationParticipantRole role, string locationId, string interactionPointId, string organizationId = "", string officeId = "", bool hidden = false, string provenanceId = "")
        {
            return new ConversationParticipantRecordData
            {
                personId = personId,
                role = role,
                currentLocationId = locationId,
                currentInteractionPointId = interactionPointId,
                representedOrganizationId = organizationId,
                representedOfficeId = officeId,
                hidden = hidden,
                provenanceId = provenanceId
            };
        }

        private static ConversationSubjectLinkData HiddenSubject(string subjectId)
        {
            return new ConversationSubjectLinkData
            {
                role = ConversationSubjectRole.Information,
                subject = new InformationSubjectReferenceData { subjectType = InformationSubjectType.KnowledgeRecord, subjectId = subjectId, tags = new[] { "private" } },
                hidden = true
            };
        }

        private static QuestSourceOperationResult CreateGuildCounter(QuestSourceRuntime sources, string sourceId)
        {
            return sources.CreateSource(new QuestSourceCreateRequest
            {
                transactionId = $"tx.{sourceId}.create",
                questSourceId = sourceId,
                questSourceDefinitionId = PrototypeQuestSourceDefinitionFactory.AdventurerGuildCounterDefinitionId,
                hostLocationId = "location.prototype.adventurers-guild",
                interactionPointId = "interaction-point.prototype.adventurer-guild-counter",
                operatingOrganizationId = "organization.prototype.adventurers-guild",
                sceneBindingKey = "scene.prototype.guild.counter",
                worldTime = 1d
            });
        }

        private static QuestSourceOperationResult CreateGuildBoard(QuestSourceRuntime sources, string sourceId)
        {
            return sources.CreateSource(new QuestSourceCreateRequest
            {
                transactionId = $"tx.{sourceId}.create",
                questSourceId = sourceId,
                questSourceDefinitionId = PrototypeQuestSourceDefinitionFactory.AdventurerGuildBoardDefinitionId,
                hostLocationId = "location.prototype.adventurers-guild",
                interactionPointId = "interaction-point.prototype.guild-board",
                operatingOrganizationId = "organization.prototype.adventurers-guild",
                sceneBindingKey = "scene.prototype.guild.board",
                worldTime = 1d
            });
        }

        private static QuestSourceOperationResult CreateMerchantCounter(QuestSourceRuntime sources, string sourceId)
        {
            return sources.CreateSource(new QuestSourceCreateRequest
            {
                transactionId = $"tx.{sourceId}.create",
                questSourceId = sourceId,
                questSourceDefinitionId = PrototypeQuestSourceDefinitionFactory.MerchantGuildCounterDefinitionId,
                hostLocationId = "location.prototype.market-stall",
                interactionPointId = "interaction-point.prototype.merchant-guild-counter",
                operatingOrganizationId = "organization.prototype.merchant-guild",
                sceneBindingKey = "scene.prototype.guild.merchant-counter",
                worldTime = 1d
            });
        }

        private static QuestAssignmentSnapshot AcceptedGuildAssignment(QuestRuntime quests, QuestParticipationRuntime participation, string key)
        {
            string questId = $"quest.prototype.guild.{key}";
            string personId = $"person.prototype.{key}";
            CreateGuildPosting(quests, key, questId);
            QuestParticipationOperationResult offer = participation.CreateOffer(OfferRequest(questId, personId, EligibleContext(personId), $"tx.quest.offer.{key}"));
            QuestParticipationOperationResult accept = participation.AcceptOffer(new QuestAcceptOfferRequest
            {
                transactionId = $"tx.quest.accept.{key}",
                offerId = offer.Offer?.OfferId,
                personId = personId,
                explicitConsent = true,
                consentRecordId = $"consent.prototype.{key}",
                eligibilityContext = EligibleContext(personId),
                worldTime = 2d
            });
            return accept.Assignment;
        }

        private static QuestAssignmentSnapshot AcceptedDeliveryAssignment(QuestRuntime quests, QuestParticipationRuntime participation, string key)
        {
            string questId = $"quest.prototype.delivery.{key}";
            string personId = $"person.prototype.{key}";
            quests.CreateQuest(new QuestCreateRequest
            {
                transactionId = $"tx.quest.delivery.create.{key}",
                questId = questId,
                questDefinitionId = PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Business, issuerId = "business.prototype.merchant" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = personId },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Contract, locationId = "location.prototype.market", interactionPointId = "interaction-point.prototype.merchant-guild-counter" },
                subjectLinks = new[] { Subject("item.prototype.merchant-parcel", QuestSubjectRole.Item, InformationSubjectType.Custom) },
                createdWorldTime = 1d
            });
            QuestEligibilityContext context = new QuestEligibilityContext
            {
                personId = personId,
                interactionPointId = "interaction-point.prototype.merchant-guild-counter",
                privilegedDiagnostics = true,
                worldTime = 1d,
                facts = new QuestEligibilityFactSet(authorityGrants: new[] { "authority.prototype.merchant.quest-offer" })
            };
            QuestParticipationOperationResult offer = participation.CreateOffer(new QuestOfferRequest
            {
                transactionId = $"tx.quest.delivery.offer.{key}",
                questId = questId,
                recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = personId },
                institutionalIssuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Business, issuerId = "business.prototype.merchant" },
                offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Business, issuerId = "business.prototype.merchant", actingPersonId = "person.prototype.merchant" },
                channel = QuestOfferChannel.InteractionPoint,
                sourceInteractionPointId = "interaction-point.prototype.merchant-guild-counter",
                sourceLocationId = "location.prototype.market",
                authorityBasisId = "authority.prototype.merchant.quest-offer",
                eligibilityContext = context,
                worldTime = 1d
            });
            QuestParticipationOperationResult accept = participation.AcceptOffer(new QuestAcceptOfferRequest
            {
                transactionId = $"tx.quest.delivery.accept.{key}",
                offerId = offer.Offer?.OfferId,
                personId = personId,
                explicitConsent = true,
                consentRecordId = $"consent.prototype.{key}",
                eligibilityContext = context,
                worldTime = 2d
            });
            return accept.Assignment;
        }

        private static QuestAssignmentSnapshot AcceptedHiddenAssignment(QuestRuntime quests, QuestParticipationRuntime participation, string key)
        {
            string questId = $"quest.prototype.hidden.{key}";
            string personId = $"person.prototype.{key}";
            quests.CreateQuest(new QuestCreateRequest
            {
                transactionId = $"tx.quest.hidden.create.{key}",
                questId = questId,
                questDefinitionId = PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.WorldEvent, locationId = "location.prototype.tavern" },
                subjectLinks = new[] { Subject("location.prototype.secret-dungeon-entry", QuestSubjectRole.Location, InformationSubjectType.Location) },
                visibility = QuestVisibility.Hidden,
                createdWorldTime = 1d
            });
            QuestParticipationOperationResult assignment = participation.DirectAssign(new QuestDirectAssignmentRequest
            {
                transactionId = $"tx.quest.hidden.assign.{key}",
                questId = questId,
                assigneePersonId = personId,
                assignedBy = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                explicitConsent = true,
                consentRecordId = $"consent.prototype.{key}",
                eligibilityContext = new QuestEligibilityContext { personId = personId, privilegedDiagnostics = true, worldTime = 1d, facts = new QuestEligibilityFactSet(knownSubjects: new[] { "subject.prototype.hidden-dungeon" }) },
                worldTime = 2d,
                visibility = QuestVisibility.Hidden
            });
            return assignment.Assignment;
        }

        private static QuestObjectiveSignal ObjectiveSignal(QuestAssignmentSnapshot assignment, QuestObjectiveCategory category, string targetId, string sourceEventId, InformationSubjectType targetType = InformationSubjectType.Custom)
        {
            return new QuestObjectiveSignal
            {
                transactionId = $"tx.{sourceEventId}",
                assignmentId = assignment.AssignmentId,
                questId = assignment.QuestId,
                participantPersonId = assignment.AssigneePersonId,
                actorPersonId = assignment.AssigneePersonId,
                category = category,
                target = new InformationSubjectReferenceData { subjectType = targetType, subjectId = targetId },
                amount = 1,
                sourceEventId = sourceEventId,
                sourceRuntimeId = "testlab.objective-signal",
                worldTime = 5d,
                committed = true
            };
        }

        private static void CompleteGuildObjectives(QuestObjectiveProgressRuntime objectives, QuestAssignmentSnapshot assignment, string key)
        {
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", $"source.quest.{key}.counter"));
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.VisitLocation, "location.prototype.dungeon-entry", $"source.quest.{key}.dungeon", InformationSubjectType.Location));
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", $"source.quest.{key}.defeat1"));
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", $"source.quest.{key}.defeat2"));
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", $"source.quest.{key}.defeat3"));
            objectives.ApplySignal(ObjectiveSignal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", $"source.quest.{key}.report"));
        }

        private static QuestObjectiveStateFactData ObjectiveFact(QuestObjectiveCategory category, string targetId, int value)
        {
            return new QuestObjectiveStateFactData
            {
                category = category,
                target = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Custom, subjectId = targetId },
                value = value
            };
        }

        private static QuestOfferRequest OfferRequest(string questId, string personId, QuestEligibilityContext context, string transactionId, bool preview = false)
        {
            return new QuestOfferRequest
            {
                transactionId = transactionId,
                questId = questId,
                recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = personId },
                institutionalIssuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild", actingPersonId = "person.prototype.guild-clerk" },
                channel = QuestOfferChannel.GuildCounter,
                sourceInteractionPointId = "interaction-point.prototype.adventurer-guild-counter",
                sourceLocationId = "location.prototype.adventurers-guild",
                authorityBasisId = "authority.prototype.guild.quest-offer",
                eligibilityContext = context,
                worldTime = 1d,
                preview = preview
            };
        }

        private static QuestEligibilityContext EligibleContext(string personId)
        {
            return new QuestEligibilityContext
            {
                personId = personId,
                locationId = "location.prototype.adventurers-guild",
                interactionPointId = "interaction-point.prototype.adventurer-guild-counter",
                privilegedDiagnostics = true,
                worldTime = 1d,
                facts = new QuestEligibilityFactSet(
                    organizationMemberships: new[] { "organization.prototype.adventurers-guild" },
                    authorityGrants: new[] { "authority.prototype.guild.quest-offer" })
            };
        }

        private static QuestRuntimeOperationResult CreateGuildPosting(QuestRuntime runtime, string transaction, string questId)
        {
            return runtime.CreateQuest(new QuestCreateRequest
            {
                transactionId = $"tx.quest.{transaction}",
                questId = questId,
                questDefinitionId = PrototypeQuestDefinitionFactory.GuildPostingDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, locationId = "location.prototype.adventurers-guild", interactionPointId = "interaction-point.prototype.adventurer-guild-counter" },
                subjectLinks = new[] { Subject("location.prototype.dungeon-entry", QuestSubjectRole.Location, InformationSubjectType.Location) },
                createdWorldTime = 10d
            });
        }

        private static QuestRuntimeOperationResult CreateDynamicBounty(QuestRuntime runtime, string transaction, string key)
        {
            return runtime.CreateQuest(new QuestCreateRequest
            {
                transactionId = $"tx.quest.{transaction}",
                questDefinitionId = PrototypeQuestDefinitionFactory.DynamicBountyDefinitionId,
                repeatInstanceKey = key,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, locationId = "location.prototype.adventurers-guild", interactionPointId = "interaction-point.prototype.guild-board" },
                subjectLinks = new[] { Subject($"encounter.prototype.{key}", QuestSubjectRole.Encounter, InformationSubjectType.Custom) },
                createdWorldTime = 12d,
                tagIds = new[] { "bounty", key }
            });
        }

        private static QuestSubjectLinkData Subject(string id, QuestSubjectRole role, InformationSubjectType type)
        {
            return new QuestSubjectLinkData
            {
                role = role,
                subject = new InformationSubjectReferenceData { subjectType = type, subjectId = id, tags = new[] { role.ToString().ToLowerInvariant() } },
                provenanceId = "prototype.quest.automation"
            };
        }

        private static Step15NarrativePersistenceSnapshot Step15NarrativeAutomationSnapshot()
        {
            const string World = PersistenceService.LocalWorldId;
            const string QuestId = "quest.prototype.automation";
            const string OfferId = "offer.prototype.automation";
            const string AssignmentId = "assignment.prototype.automation";
            const string ObjectiveId = "objective.prototype.automation";
            const string PersonId = "person.prototype.hero";
            const string SourceId = "quest-source.prototype.automation";
            const string ListingId = "quest-listing.prototype.automation";
            const string ConversationId = "conversation.prototype.automation";
            const string ArcId = "narrative-arc.prototype.automation";
            const string NarrativeEventId = "narrative-event.testlab.automation";
            const string HiddenNarrativeEventId = "narrative-event.testlab.hidden";
            const string NarrativeStateId = "narrative-state.testlab.automation";

            return new Step15NarrativePersistenceSnapshot
            {
                WorldId = World,
                SaveSlotId = "slot.prototype.automation",
                SaveWorldTime = 20d,
                Quests = new QuestRuntimeSaveData
                {
                    schemaVersion = QuestRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    quests = new List<QuestRecordData>
                    {
                        new QuestRecordData { questId = QuestId, questDefinitionId = PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, worldId = World, lifecycleState = QuestRuntimeLifecycleState.Available, issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" }, intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = PersonId }, origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, locationId = "location.prototype.guild", interactionPointId = "interaction-point.prototype.guild-board" }, visibility = QuestVisibility.Public, createdWorldTime = 1d }
                    },
                    events = new List<QuestRuntimeEventData>
                    {
                        new QuestRuntimeEventData { eventId = "quest-event.automation.001", questId = QuestId, eventKind = QuestRuntimeEventKind.Instantiated, afterState = QuestRuntimeLifecycleState.Available, worldTime = 1d, runtimeRevision = 1 },
                        new QuestRuntimeEventData { eventId = "quest-event.automation.002", questId = QuestId, eventKind = QuestRuntimeEventKind.LifecycleChanged, beforeState = QuestRuntimeLifecycleState.Available, afterState = QuestRuntimeLifecycleState.Retired, worldTime = 10d, runtimeRevision = 2 }
                    }
                },
                Participation = new QuestParticipationRuntimeSaveData
                {
                    schemaVersion = QuestParticipationRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    offers = new List<QuestOfferRecordData> { new QuestOfferRecordData { offerId = OfferId, questId = QuestId, worldId = World, recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = PersonId }, lifecycleState = QuestOfferLifecycleState.Active, createdWorldTime = 2d, visibility = QuestVisibility.Public } },
                    assignments = new List<QuestAssignmentRecordData> { new QuestAssignmentRecordData { assignmentId = AssignmentId, offerId = OfferId, questId = QuestId, worldId = World, assigneePersonId = PersonId, lifecycleState = QuestAssignmentLifecycleState.Active, assignedWorldTime = 3d, visibility = QuestVisibility.Public } },
                    events = new List<QuestParticipationEventData>
                    {
                        new QuestParticipationEventData { eventId = "participation-event.automation.001", eventKind = QuestParticipationEventKind.OfferCreated, offerId = OfferId, questId = QuestId, personId = PersonId, worldTime = 2d, runtimeRevision = 1 },
                        new QuestParticipationEventData { eventId = "participation-event.automation.002", eventKind = QuestParticipationEventKind.OfferAccepted, offerId = OfferId, assignmentId = AssignmentId, questId = QuestId, personId = PersonId, worldTime = 3d, runtimeRevision = 2 },
                        new QuestParticipationEventData { eventId = "participation-event.automation.003", eventKind = QuestParticipationEventKind.AssignmentCreated, assignmentId = AssignmentId, questId = QuestId, personId = PersonId, worldTime = 3d, runtimeRevision = 2 }
                    }
                },
                Objectives = new QuestObjectiveProgressRuntimeSaveData
                {
                    schemaVersion = QuestObjectiveProgressRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    objectives = new List<QuestObjectiveRecordData> { new QuestObjectiveRecordData { objectiveId = ObjectiveId, objectiveDefinitionId = "objective.prototype.report", questId = QuestId, assignmentId = AssignmentId, assigneePersonId = PersonId, worldId = World, lifecycleState = QuestObjectiveLifecycleState.Active, visibility = QuestObjectiveVisibility.Public, currentValue = 1, targetValue = 1, satisfied = false, activatedWorldTime = 4d, satisfiedWorldTime = -1d } },
                    events = new List<QuestObjectiveRuntimeEventData>
                    {
                        new QuestObjectiveRuntimeEventData { eventId = "objective-event.automation.001", objectiveId = ObjectiveId, questId = QuestId, assignmentId = AssignmentId, eventKind = QuestObjectiveEventKind.ObjectiveActivated, beforeState = QuestObjectiveLifecycleState.Locked, afterState = QuestObjectiveLifecycleState.Active, worldTime = 4d, runtimeRevision = 1 },
                        new QuestObjectiveRuntimeEventData { eventId = "objective-event.automation.002", objectiveId = ObjectiveId, questId = QuestId, assignmentId = AssignmentId, eventKind = QuestObjectiveEventKind.ObjectiveSatisfied, beforeValue = 0, afterValue = 1, beforeState = QuestObjectiveLifecycleState.Active, afterState = QuestObjectiveLifecycleState.Satisfied, worldTime = 9d, runtimeRevision = 2 }
                    }
                },
                Outcomes = new QuestOutcomeRuntimeSaveData
                {
                    schemaVersion = QuestOutcomeRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    terminalOutcomes = new List<QuestTerminalOutcomeRecordData> { new QuestTerminalOutcomeRecordData { outcomeId = "outcome.prototype.automation", terminalOutcomeId = "terminal-outcome.prototype.automation", questId = QuestId, assignmentId = AssignmentId, worldId = World, outcomeKind = QuestTerminalOutcomeKind.Completed, actorPersonId = PersonId, worldTime = 10d } },
                    rewardEntitlements = new List<QuestRewardEntitlementRecordData> { new QuestRewardEntitlementRecordData { entitlementId = "reward.prototype.automation", terminalOutcomeId = "terminal-outcome.prototype.automation", questId = QuestId, assignmentId = AssignmentId, recipientPersonId = PersonId, worldId = World, category = QuestRewardCategory.Currency, targetDefinitionId = "currency.prototype.coin", quantity = 25, state = QuestRewardEntitlementState.Claimable, createdWorldTime = 10d } },
                    events = new List<QuestOutcomeEventData>
                    {
                        new QuestOutcomeEventData { eventId = "outcome-event.automation.001", eventKind = QuestOutcomeEventKind.TerminalOutcomeRecorded, questId = QuestId, assignmentId = AssignmentId, terminalOutcomeId = "terminal-outcome.prototype.automation", worldTime = 10d, runtimeRevision = 1 },
                        new QuestOutcomeEventData { eventId = "outcome-event.automation.002", eventKind = QuestOutcomeEventKind.RewardEntitlementCreated, questId = QuestId, assignmentId = AssignmentId, rewardEntitlementId = "reward.prototype.automation", worldTime = 10d, runtimeRevision = 2 }
                    }
                },
                Sources = new QuestSourceRuntimeSaveData
                {
                    schemaVersion = QuestSourceRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 1,
                    sources = new List<QuestSourceRecordData> { new QuestSourceRecordData { questSourceId = SourceId, questSourceDefinitionId = PrototypeQuestSourceDefinitionFactory.AdventurerGuildBoardDefinitionId, worldId = World, visibility = QuestSourceVisibility.Public, createdWorldTime = 1d } },
                    listings = new List<QuestListingRecordData> { new QuestListingRecordData { questListingId = ListingId, questId = QuestId, questSourceId = SourceId, worldId = World, visibility = QuestSourceVisibility.Public, lifecycleState = QuestListingLifecycleState.Claimed, publishedWorldTime = 2d, endedWorldTime = 3d } },
                    events = new List<QuestSourceEventData> { new QuestSourceEventData { eventId = "source-event.automation.001", questSourceId = SourceId, questListingId = ListingId, questId = QuestId, eventKind = QuestSourceEventKind.ListingPublished, worldTime = 2d, runtimeRevision = 1 } }
                },
                Conversations = new ConversationRuntimeSaveData
                {
                    schemaVersion = ConversationRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 1,
                    conversations = new List<ConversationRecordData>
                    {
                        new ConversationRecordData { conversationId = ConversationId, conversationDefinitionId = PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId, worldId = World, lifecycleState = ConversationLifecycleState.Active, visibility = ConversationVisibility.Public, participants = new[] { new ConversationParticipantRecordData { participantId = "participant.hero", personId = PersonId, role = ConversationParticipantRole.Initiator }, new ConversationParticipantRecordData { participantId = "participant.clerk", personId = "person.prototype.guild-clerk", role = ConversationParticipantRole.Provider } }, questId = QuestId, questSourceId = SourceId, questListingId = ListingId, startedWorldTime = 5d }
                    },
                    events = new List<ConversationEventData> { new ConversationEventData { eventId = "conversation-event.automation.001", conversationId = ConversationId, personId = PersonId, eventKind = ConversationEventKind.ConversationStarted, afterState = ConversationLifecycleState.Active, worldTime = 5d, runtimeRevision = 1 } }
                },
                DialogueFlows = new DialogueFlowRuntimeSaveData
                {
                    schemaVersion = DialogueFlowRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    flows = new List<DialogueFlowRecordData>
                    {
                        new DialogueFlowRecordData { flowId = "dialogue-flow.prototype.automation", conversationId = ConversationId, graphId = PrototypeDialogueGraphDefinitionFactory.AdventurerGuildCounterGraphId, worldId = World, state = DialogueFlowState.AwaitingChoice, currentNodeId = "node.report", currentVisitId = "visit.automation.002", visits = new[] { new DialogueNodeVisitRecordData { visitId = "visit.automation.001", conversationId = ConversationId, graphId = PrototypeDialogueGraphDefinitionFactory.AdventurerGuildCounterGraphId, nodeId = "node.start", speakerPersonId = "person.prototype.guild-clerk", enteredWorldTime = 5d, exitedWorldTime = 7d, selectedChoiceId = "choice.accept", sequence = 1 }, new DialogueNodeVisitRecordData { visitId = "visit.automation.002", conversationId = ConversationId, graphId = PrototypeDialogueGraphDefinitionFactory.AdventurerGuildCounterGraphId, nodeId = "node.report", speakerPersonId = PersonId, enteredWorldTime = 7d, exitedWorldTime = -1d, sequence = 2 } }, selections = new[] { new DialogueChoiceSelectionRecordData { selectionId = "selection.automation.001", conversationId = ConversationId, graphId = PrototypeDialogueGraphDefinitionFactory.AdventurerGuildCounterGraphId, nodeId = "node.start", choiceId = "choice.accept", actorPersonId = PersonId, targetNodeId = "node.report", worldTime = 7d, runtimeRevision = 2 } } }
                    }
                },
                NarrativeEvents = new NarrativeEventRuntimeSaveData
                {
                    schemaVersion = NarrativeEventRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    events = new List<NarrativeEventRecordData>
                    {
                        new NarrativeEventRecordData { narrativeEventId = NarrativeEventId, eventDefinitionId = PrototypeNarrativeEventDefinitionFactory.DungeonEntryQuestDefinitionId, worldId = World, lifecycle = NarrativeEventLifecycle.Resolved, actorPersonId = PersonId, questId = QuestId, conversationId = ConversationId, triggerTime = 8d, visibility = NarrativeEventVisibility.Public, actionExecutions = new[] { new NarrativeActionExecutionRecordData { actionExecutionId = "action.automation.001", narrativeEventId = NarrativeEventId, actionDefinitionId = "action-definition.prototype.state", category = NarrativeActionCategory.RequestNarrativeStateTransition, lifecycle = NarrativeActionLifecycle.Committed, worldTime = 8d } } },
                        new NarrativeEventRecordData { narrativeEventId = HiddenNarrativeEventId, eventDefinitionId = PrototypeNarrativeEventDefinitionFactory.HiddenFactionOfferDefinitionId, worldId = World, lifecycle = NarrativeEventLifecycle.Resolved, actorPersonId = "person.prototype.hidden", triggerTime = 8.5d, visibility = NarrativeEventVisibility.Hidden }
                    },
                    signals = new List<NarrativeSignalRecordData> { new NarrativeSignalRecordData { narrativeSignalId = "signal.prototype.automation", signalDefinitionId = "signal-definition.prototype.automation", actorPersonId = PersonId, sourceId = QuestId, worldTime = 8d, runtimeRevision = 1 } }
                },
                NarrativeStates = new NarrativeStateRuntimeSaveData
                {
                    schemaVersion = 1,
                    worldId = World,
                    revision = 2,
                    states = new[] { new NarrativeStateRecordData { narrativeStateId = NarrativeStateId, stateDefinitionId = PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyDefinitionId, worldId = World, variables = new[] { new NarrativeStateVariableRecordData { variableDefinitionId = PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyVariableId, value = NarrativeVariableValueData.Token(PrototypeNarrativeStateDefinitionFactory.GuildLoyalValueId), changedWorldTime = 8d } }, createdWorldTime = 1d, updatedWorldTime = 8d, revision = 2 } },
                    transitions = new[] { new NarrativeStateTransitionRecordData { transitionId = "state-transition.automation.001", narrativeStateId = NarrativeStateId, stateDefinitionId = PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyDefinitionId, variableDefinitionId = PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyVariableId, worldId = World, actorPersonId = PersonId, questId = QuestId, conversationId = ConversationId, narrativeEventId = NarrativeEventId, oldValue = NarrativeVariableValueData.Token(PrototypeNarrativeStateDefinitionFactory.GuildUncommittedValueId), newValue = NarrativeVariableValueData.Token(PrototypeNarrativeStateDefinitionFactory.GuildLoyalValueId), worldTime = 8d, revisionBefore = 1, revisionAfter = 2, sequence = 1, visibility = NarrativeStateVisibility.Public } }
                },
                NarrativeArcs = new NarrativeArcRuntimeSaveData
                {
                    schemaVersion = NarrativeArcRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    arcs = new List<NarrativeArcRecordData>
                    {
                        new NarrativeArcRecordData { narrativeArcId = ArcId, arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId, worldId = World, lifecycle = NarrativeArcLifecycle.Completed, actorPersonId = PersonId, startedWorldTime = 1d, resolvedWorldTime = 12d, stages = new[] { new NarrativeArcStageRecordData { stageRuntimeId = "arc-stage.automation.001", stageDefinitionId = PrototypeNarrativeArcDefinitionFactory.GuildIntroJoinStageId, lifecycle = NarrativeArcStageLifecycle.Completed, activatedWorldTime = 2d, resolvedWorldTime = 3d, boundQuests = new[] { new NarrativeArcBoundQuestRecordData { bindingDefinitionId = "binding.automation", questId = QuestId, questDefinitionId = PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, mode = NarrativeArcQuestBindingMode.ReferenceExistingQuest, worldTime = 2d } } }, new NarrativeArcStageRecordData { stageRuntimeId = "arc-stage.automation.002", stageDefinitionId = PrototypeNarrativeArcDefinitionFactory.GuildIntroPostingStageId, lifecycle = NarrativeArcStageLifecycle.Completed, activatedWorldTime = 7d, resolvedWorldTime = 12d } } }
                    }
                }
            }.Clone();
        }

        private static Step15NarrativePersistenceSnapshot Step15NarrativeCoherentAutomationSnapshot()
        {
            Step15NarrativePersistenceSnapshot snapshot = Step15NarrativeAutomationSnapshot();
            snapshot.Participation.offers[0].lifecycleState = QuestOfferLifecycleState.Accepted;
            snapshot.Participation.assignments[0].lifecycleState = QuestAssignmentLifecycleState.Historical;
            snapshot.Participation.assignments[0].endedWorldTime = 10d;
            snapshot.Objectives.objectives[0].lifecycleState = QuestObjectiveLifecycleState.Satisfied;
            snapshot.Objectives.objectives[0].satisfied = true;
            snapshot.Objectives.objectives[0].satisfiedWorldTime = 9d;
            snapshot.Sources.sources[0].hostLocationId = "location.prototype.guild";
            snapshot.Sources.sources[0].interactionPointId = "interaction-point.prototype.guild-board";
            snapshot.Sources.sources[0].operatingOrganizationId = "organization.prototype.adventurers-guild";
            snapshot.Sources.listings[0].claimedAssignmentId = "assignment.prototype.automation";
            snapshot.Conversations.conversations[0].hostLocationId = "location.prototype.guild";
            snapshot.Conversations.conversations[0].hostInteractionPointId = "interaction-point.prototype.adventurer-guild-counter";
            snapshot.Conversations.conversations[0].operatingOrganizationId = "organization.prototype.adventurers-guild";
            return snapshot.Clone();
        }

        private static Step15NarrativePersistenceSnapshot Step15NarrativeActiveContextAutomationSnapshot()
        {
            Step15NarrativePersistenceSnapshot snapshot = Step15NarrativeCoherentAutomationSnapshot();
            snapshot.Participation.offers[0].lifecycleState = QuestOfferLifecycleState.Active;
            snapshot.Participation.assignments[0].lifecycleState = QuestAssignmentLifecycleState.Active;
            snapshot.Participation.assignments[0].endedWorldTime = -1d;
            snapshot.Outcomes.terminalOutcomes.Clear();
            snapshot.Outcomes.rewardEntitlements.Clear();
            snapshot.Outcomes.rewardGrants.Clear();
            snapshot.Outcomes.events.Clear();
            snapshot.Sources.listings[0].lifecycleState = QuestListingLifecycleState.Published;
            snapshot.Sources.listings[0].endedWorldTime = -1d;
            snapshot.Sources.listings[0].claimedAssignmentId = string.Empty;
            return snapshot.Clone();
        }

        private sealed class AutomationRewardExecutor : IQuestRewardEffectExecutor
        {
            public readonly List<QuestRewardEffectRequest> Requests = new List<QuestRewardEffectRequest>();

            public QuestRewardEffectResult Execute(QuestRewardEffectRequest request)
            {
                Requests.Add(request);
                return QuestRewardEffectResult.Success($"owner.{request.category.ToString().ToLowerInvariant()}", $"{request.category}.{request.targetDefinitionId}.{request.quantity}");
            }
        }
    }
}
#endif
