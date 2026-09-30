#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Integration;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Emotions;
using UnityIsekaiGame.Social.Family;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(12, "Social", 1200)]
    public static class PrototypeStep12AutomationSuites
    {
        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.1.relationship-identity-records",
                "Relationship Identity and Records",
                "12.1",
                "Persistent person-to-person relationship records with deterministic identity, roles, lifecycle, and persistence.",
                12010,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "RelationshipRuntime", "RelationshipDefinition", "RelationshipPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("symmetric-and-directed-records", "Symmetric and directed relationships create deterministic records", 10,
                        Step("step12-relationship-create", "Create and query relationships", SymmetricAndDirectedRelationships)),
                    Scenario("lifecycle-and-duplicates", "Relationship lifecycle and duplicate active rules are enforced", 20,
                        Step("step12-relationship-lifecycle", "End and reject duplicate active records", LifecycleAndDuplicates)),
                    Scenario("persistence-validation", "Relationship persistence validates before restoring", 30,
                        Step("step12-relationship-persistence", "Save, restore, and reject corrupt payloads", PersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.2.interpersonal-attitudes-relationship-values",
                "Interpersonal Attitudes and Relationship Values",
                "12.2",
                "Directional person-to-person attitude values with definition-backed dimensions, source contributions, thresholds, and persistence.",
                12020,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "InterpersonalAttitudeRuntime", "AttitudeDimensionDefinition", "InterpersonalAttitudePersistenceParticipant" },
                scenarios: new[]
                {
                    AttitudeScenario("definitions-and-neutral-values", "Canonical attitude dimensions resolve with neutral defaults", 10,
                        Step("step12-attitudes-definitions", "Resolve attitude definitions and neutral values", AttitudeDefinitionsAndNeutralValues)),
                    AttitudeScenario("directional-values", "Directional attitudes do not mirror between people", 20,
                        Step("step12-attitudes-directional", "Mutate directed trust and hostility independently", DirectionalAttitudeValues)),
                    AttitudeScenario("contributions-and-idempotence", "Source contributions clamp and duplicate transactions are idempotent", 30,
                        Step("step12-attitudes-contributions", "Preview, execute, duplicate, and clamp source-owned contributions", ContributionsAndIdempotence)),
                    AttitudeScenario("relationship-independence", "Relationship records can inform attitudes without owning them", 40,
                        Step("step12-attitudes-relationship-independent", "End a relationship without deleting attitude values", RelationshipIndependence)),
                    AttitudeScenario("persistence-validation", "Attitudes persist and reject corrupt restores without mutation", 50,
                        Step("step12-attitudes-persistence", "Save, restore, and reject invalid attitude payloads", AttitudePersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.3.reputation-audiences-social-standing",
                "Reputation and Social Standing",
                "12.3",
                "Audience-scoped person reputation records with canonical dimensions, source-owned contributions, hierarchy-aware reads, requirements, and persistence.",
                12030,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "ReputationRuntime", "ReputationAudienceDefinition", "ReputationDimensionDefinition", "ReputationPersistenceParticipant" },
                scenarios: new[]
                {
                    ReputationScenario("runtime-readiness", "Reputation definitions and runtime are ready", 10,
                        Step("step12-reputation-readiness", "Resolve reputation audiences and dimensions", ReputationRuntimeReadiness)),
                    ReputationScenario("record-identity-dimensions", "Records and dimensions remain stable and independent", 20,
                        Step("step12-reputation-records", "Create records and mutate independent dimensions", ReputationRecordIdentityAndDimensions)),
                    ReputationScenario("audience-independence-hierarchy", "Audience independence and hierarchy are deterministic", 30,
                        Step("step12-reputation-audiences", "Verify direct, inherited, and isolated audience values", ReputationAudienceIndependenceAndHierarchy)),
                    ReputationScenario("contributions-disputes-idempotence", "Source contributions preserve dispute metadata and idempotence", 40,
                        Step("step12-reputation-contributions", "Preview, execute, duplicate, replace, remove, and classify sources", ReputationContributionsAndDisputes)),
                    ReputationScenario("requirements-and-separation", "Requirement checks do not mutate relationships or attitudes", 50,
                        Step("step12-reputation-requirements", "Evaluate thresholds and verify feature separation", ReputationRequirementsAndSeparation)),
                    ReputationScenario("persistence-validation", "Reputation persists and rejects corrupt restores", 60,
                        Step("step12-reputation-persistence", "Save, restore, and reject invalid reputation payloads", ReputationPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.4.rumors-gossip-social-knowledge-propagation",
                "Rumors, Gossip, and Social Knowledge Propagation",
                "12.4",
                "Definition-backed rumor records, transmission lineage, bounded propagation, listener knowledge and memory effects, and persistence.",
                12040,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "RumorRuntime", "RumorDefinition", "RumorCommunicationChannelDefinition", "PersonKnowledgeRuntime", "PersonMemoryRuntime" },
                scenarios: new[]
                {
                    RumorScenario("readiness-and-root-identity", "Rumor definitions resolve and root records are stable", 10,
                        Step("step12-rumor-root", "Create root rumor and query identity", RumorReadinessAndRootIdentity)),
                    RumorScenario("transmission-creates-knowledge-memory", "Transmission records listener evidence and memory", 20,
                        Step("step12-rumor-transmission", "Transmit rumor into listener knowledge and memory", RumorTransmissionCreatesKnowledgeAndMemory)),
                    RumorScenario("distortion-lineage", "Distortion creates a derived version with root lineage", 30,
                        Step("step12-rumor-distortion", "Transmit with deterministic distortion", RumorDistortionLineage)),
                    RumorScenario("bounded-propagation", "Propagation is bounded and deterministic", 40,
                        Step("step12-rumor-propagation", "Propagate rumor to ordered listeners", RumorBoundedPropagation)),
                    RumorScenario("social-boundary", "Rumors do not mutate relationships, attitudes, or reputation directly", 50,
                        Step("step12-rumor-social-boundary", "Verify rumor separation from other social runtimes", RumorSocialBoundary)),
                    RumorScenario("persistence-validation", "Rumors persist and reject corrupt restores", 60,
                        Step("step12-rumor-persistence", "Save, restore, and reject invalid rumor payloads", RumorPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.5.social-interactions-relationship-evolution",
                "Social Interactions and Relationship Evolution",
                "12.5",
                "Definition-backed social interaction execution with deterministic consequences, pending responses, promises, persistence, and Step 12 runtime delegation.",
                12050,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "SocialInteractionRuntime", "SocialInteractionDefinition", "SocialInteractionPersistenceParticipant" },
                scenarios: new[]
                {
                    InteractionScenario("readiness-and-preview", "Interaction definitions resolve and previews are non-mutating", 10,
                        Step("step12-interaction-preview", "Preview interaction without mutation", InteractionReadinessAndPreview)),
                    InteractionScenario("attitude-consequences", "Compliments and insults evolve directed attitudes", 20,
                        Step("step12-interaction-attitudes", "Execute attitude-producing interactions", InteractionAttitudeConsequences)),
                    InteractionScenario("pending-response-promise", "Pending responses and accepted promises are explicit", 30,
                        Step("step12-interaction-pending", "Create pending interaction and accept promise", InteractionPendingResponsePromise)),
                    InteractionScenario("public-reputation", "Witnessed and public interactions affect reputation", 40,
                        Step("step12-interaction-reputation", "Execute public reputation consequences", InteractionPublicReputation)),
                    InteractionScenario("rumor-delegation", "Information sharing delegates through Rumor runtime", 50,
                        Step("step12-interaction-rumor", "Share existing rumor through interaction", InteractionRumorDelegation)),
                    InteractionScenario("persistence-validation", "Interactions persist and reject corrupt restores", 60,
                        Step("step12-interaction-persistence", "Save, restore, duplicate, and reject invalid payloads", InteractionPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.6.social-norms-etiquette-contextual-expectations",
                "Social Norms, Etiquette, and Contextual Expectations",
                "12.6",
                "Definition-backed social norm assessment with contextual applicability, actor knowledge, observer interpretation, conflict resolution, consequences, idempotence, and persistence.",
                12060,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "SocialNormRuntime", "SocialNormDefinition", "SocialNormPersistenceParticipant" },
                scenarios: new[]
                {
                    NormScenario("readiness-preview", "Norm definitions resolve and previews do not mutate", 10,
                        Step("step12-norm-preview", "Preview host greeting norm", NormReadinessAndPreview)),
                    NormScenario("visibility-consequences", "Public and private etiquette produce deterministic consequence plans", 20,
                        Step("step12-norm-visibility", "Assess private and public insult norms", NormVisibilityConsequences)),
                    NormScenario("knowledge-exception-observers", "Actor knowledge, exceptions, and observer interpretation remain explicit", 30,
                        Step("step12-norm-knowledge-exception", "Evaluate ignorance and witness context", NormKnowledgeExceptionObservers)),
                    NormScenario("conflict-and-promise", "Norm conflicts and promise expectations resolve deterministically", 40,
                        Step("step12-norm-conflict-promise", "Assess conflict and promise breach", NormConflictAndPromise)),
                    NormScenario("persistence-idempotence", "Norm assessments persist and duplicate transactions are idempotent", 50,
                        Step("step12-norm-persistence", "Save, restore, duplicate, and reject invalid norm payloads", NormPersistenceIdempotence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.7.social-networks-cliques-group-dynamics",
                "Social Networks, Cliques, and Group Dynamics",
                "12.7",
                "Derived social graph projections and persistent informal social groups without owning relationship, attitude, reputation, rumor, interaction, or norm records.",
                12070,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "SocialNetworkRuntime", "SocialGraphProjectionDefinition", "InformalSocialGroupDefinition", "SocialNetworkPersistenceParticipant" },
                scenarios: new[]
                {
                    NetworkScenario("readiness-preview", "Network definitions resolve and previews do not mutate", 10,
                        Step("step12-network-readiness", "Resolve graph and group definitions", NetworkReadinessAndPreview)),
                    NetworkScenario("projection-semantics", "Projected edges preserve source semantics and direction", 20,
                        Step("step12-network-projection", "Build graph from source runtimes", NetworkProjectionSemantics)),
                    NetworkScenario("queries-and-analysis", "Neighbors, paths, metrics, cliques, and communities are deterministic", 30,
                        Step("step12-network-analysis", "Run bounded graph analysis", NetworkQueriesAndAnalysis)),
                    NetworkScenario("group-lifecycle", "Informal group lifecycle and idempotence are explicit", 40,
                        Step("step12-network-group", "Create memberships, roles, and group metrics", NetworkGroupLifecycle)),
                    NetworkScenario("persistence-validation", "Network persistence preserves groups and rejects corrupt restores", 50,
                        Step("step12-network-persistence", "Save, restore, and reject invalid network payloads", NetworkPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.8.social-decision-making-relationship-driven-npc-behavior",
                "Social Decision-Making and Relationship-Driven NPC Behavior",
                "12.8",
                "Deterministic relationship-driven social action selection that delegates execution to the Social Interaction runtime.",
                12080,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "SocialDecisionRuntime", "SocialDecisionProfileDefinition", "SocialIntentionDefinition", "SocialDecisionPersistenceParticipant" },
                scenarios: new[]
                {
                    DecisionScenario("readiness-and-definitions", "Decision definitions resolve and preview without mutation", 10,
                        Step("step12-decision-readiness", "Resolve social decision definitions", DecisionReadiness)),
                    DecisionScenario("deterministic-selection", "Relationship inputs select stable candidates", 20,
                        Step("step12-decision-selection", "Evaluate deterministic social decision candidates", DecisionDeterministicSelection)),
                    DecisionScenario("no-action-boundary", "No targets produces explicit no-action state", 30,
                        Step("step12-decision-no-action", "Evaluate no-action boundary", DecisionNoActionBoundary)),
                    DecisionScenario("submit-through-interactions", "Submitted decisions execute through Social Interaction runtime", 40,
                        Step("step12-decision-submit", "Submit selected action through interaction runtime", DecisionSubmitThroughInteractions)),
                    DecisionScenario("persistence-validation", "Decision state persists and rejects corrupt restores", 50,
                        Step("step12-decision-persistence", "Save, restore, and reject invalid decision payloads", DecisionPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.9.social-influence-persuasion-deception",
                "Social Influence, Persuasion, Deception, and Resistance",
                "12.9",
                "Definition-backed social influence attempts with deterministic persuasion, deception detection, compliance boundaries, decision modifiers, and persistence.",
                12090,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "SocialInfluenceRuntime", "SocialInfluenceMethodDefinition", "SocialInfluencePersistenceParticipant" },
                scenarios: new[]
                {
                    InfluenceScenario("readiness-and-definitions", "Influence definitions resolve and previews are non-mutating", 10,
                        Step("step12-influence-readiness", "Resolve influence definitions and preview", InfluenceReadinessAndPreview)),
                    InfluenceScenario("belief-and-compliance-boundaries", "Belief influence and compliance remain separate", 20,
                        Step("step12-influence-belief-compliance", "Execute belief evidence and accepted promise", InfluenceBeliefAndComplianceBoundaries)),
                    InfluenceScenario("deception-detection", "Detected deception records trust and hostility consequences", 30,
                        Step("step12-influence-deception", "Detect a deliberate lie deterministically", InfluenceDeceptionDetection)),
                    InfluenceScenario("decision-modifiers", "Influence modifiers affect decision scoring without owning decisions", 40,
                        Step("step12-influence-decision-modifier", "Apply influence modifier to decision candidate", InfluenceDecisionModifiers)),
                    InfluenceScenario("persistence-validation", "Influence attempts persist and reject corrupt restores", 50,
                        Step("step12-influence-persistence", "Save, restore, and reject invalid influence payloads", InfluencePersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.10.social-emotions-moods-affective-reactions",
                "Social Emotions, Moods, and Affective Reactions",
                "12.10",
                "Definition-backed transient emotions, mood aggregation, affective projections, deterministic decay, decision modifiers, and persistence.",
                12100,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "SocialEmotionRuntime", "SocialEmotionDefinition", "SocialEmotionPersistenceParticipant" },
                scenarios: new[]
                {
                    EmotionScenario("readiness-and-definitions", "Emotion, mood, and appraisal definitions resolve", 10,
                        Step("step12-emotion-readiness", "Resolve canonical emotion definitions", EmotionReadiness)),
                    EmotionScenario("belief-relative-appraisal", "Belief-relative appraisal creates the expected emotion", 20,
                        Step("step12-emotion-appraisal", "Trigger appraisal from believed social information", EmotionBeliefRelativeAppraisal)),
                    EmotionScenario("decay-and-stacking", "Emotion decay and reinforcement are deterministic", 30,
                        Step("step12-emotion-decay", "Apply deterministic decay and stacking", EmotionDecayAndStacking)),
                    EmotionScenario("decision-modifiers", "Emotion modifiers feed social decisions without owning them", 40,
                        Step("step12-emotion-decision", "Apply emotion decision modifier", EmotionDecisionModifiers)),
                    EmotionScenario("persistence-and-projection", "Persistence and projections preserve affective state", 50,
                        Step("step12-emotion-persistence", "Save, restore, and project emotion state", EmotionPersistenceAndProjection))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.11.family-kinship-romance-households",
                "Family, Kinship, Romance, and Household Relationships",
                "12.11",
                "Authoritative household state with derived kinship, explicit parentage records, consent-gated romantic lifecycle, and persistence.",
                12110,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "FamilyRelationshipRuntime", "RelationshipRuntime", "InterpersonalAttitudeRuntime", "RomanticEligibilityPolicyDefinition", "HouseholdDefinition", "FamilyRelationshipPersistenceParticipant" },
                scenarios: new[]
                {
                    FamilyScenario("definitions-and-parentage", "Parentage definitions resolve and invariants reject unsafe records", 10,
                        Step("step12-family-parentage", "Create parentage and reject self/cycle parentage", FamilyDefinitionsAndParentage)),
                    FamilyScenario("kinship-and-visibility", "Kinship queries are deterministic and visibility aware", 20,
                        Step("step12-family-kinship", "Resolve derived kinship and hidden parentage boundaries", FamilyKinshipAndVisibility)),
                    FamilyScenario("romance-eligibility-and-lifecycle", "Romance requires adult eligibility and explicit consent", 30,
                        Step("step12-family-romance", "Evaluate and execute romantic lifecycle transitions", FamilyRomanceEligibilityLifecycle)),
                    FamilyScenario("households-and-persistence", "Households own membership lifecycle and persist independently", 40,
                        Step("step12-family-household-persistence", "Create, transfer, save, restore, and reject corrupt household state", FamilyHouseholdsAndPersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.12.12.social-simulation-integration-finalization",
                "Social Simulation Integration and Step 12 Finalization",
                "12.12",
                "Final Step 12 integration readiness, ownership, persistence graph, bounded social context snapshots, transaction boundaries, and Step 13 handoff contracts.",
                12120,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "Step12SocialSimulationFacade", "Step12SocialSimulationValidator", "Step12SocialSimulationTransactionCoordinator" },
                scenarios: new[]
                {
                    IntegrationScenario("readiness-authority-persistence", "Step 12 authority and persistence graph validate cleanly", 10,
                        Step("step12-integration-readiness", "Validate authority, runtime readiness, and save graph", Step12IntegrationReadiness)),
                    IntegrationScenario("bounded-context-projections", "Bounded social context projections are deterministic and immutable", 20,
                        Step("step12-integration-context", "Create bounded social context snapshot", Step12IntegrationContext)),
                    IntegrationScenario("transaction-recursion-scheduler", "Cross-runtime transaction and scheduler guardrails are explicit", 30,
                        Step("step12-integration-transaction", "Preview, rollback, duplicate, and reject unsafe scheduling", Step12IntegrationTransactionScheduler)),
                    IntegrationScenario("health-and-step13-handoff", "Health snapshot and Step 13 handoff remain immutable references", 40,
                        Step("step12-integration-health", "Create health snapshot and consequence reference", Step12IntegrationHealthHandoff))
                }), out _);
        }

        private static TestLabAutomationStepResult Step12IntegrationReadiness(TestLabAutomationContext context)
        {
            if (!TryCreateStep12Facade(context, out Step12SocialSimulationFacade facade, out string failure))
            {
                return TestLabAssertions.Fail("step12-integration-readiness", "Validate authority, runtime readiness, and save graph", "FacadeReady", "Succeeded", "MissingRuntime", failure);
            }

            Step12IntegrationValidationReport report = facade.ValidateComplete();
            Step12HealthSnapshot health = facade.CreateHealthSnapshot();
            bool valid = report.Succeeded
                && health.Status == Step12HealthStatus.Ready
                && facade.AuthorityMap.Select(item => item.DomainId).Distinct(StringComparer.Ordinal).Count() == facade.AuthorityMap.Count
                && facade.AuthorityMap.Any(item => item.DomainId == "relationships" && item.AuthoritativeRuntime == nameof(RelationshipRuntime))
                && facade.AuthorityMap.Any(item => item.DomainId == "social-graph" && item.Derived)
                && facade.PersistenceDependencies.Any(item => item.ParticipantKey == SocialInteractionPersistenceParticipant.Key && item.DependsOn.Contains(RumorPersistenceParticipant.Key));

            return TestLabAssertions.True("step12-integration-readiness", "Step 12 authority and persistence graph validate cleanly", valid, $"{report.ToSummary()} Health={health.Status} Runtimes={health.Runtimes.Count} Authorities={facade.AuthorityMap.Count} Dependencies={facade.PersistenceDependencies.Count} Fingerprint={health.Fingerprint}");
        }

        private static TestLabAutomationStepResult Step12IntegrationContext(TestLabAutomationContext context)
        {
            if (!TryCreateStep12Facade(context, out Step12SocialSimulationFacade facade, out string failure))
            {
                return TestLabAssertions.Fail("step12-integration-context", "Create bounded social context snapshot", "FacadeReady", "Succeeded", "MissingRuntime", failure);
            }

            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            string seedFailure = SeedStep12IntegrationContext(context, "context");
            Step12SocialContextOptions options = new Step12SocialContextOptions
            {
                MaxRelationships = 1,
                MaxAttitudes = 4,
                MaxInteractions = 4,
                MaxHouseholds = 4
            };

            Step12SocialContextSnapshot first = facade.CreateContextSnapshot(runtimes.PersonId, runtimes.PersonId, "person.prototype.friend", 75d, options);
            Step12SocialContextSnapshot second = facade.CreateContextSnapshot(runtimes.PersonId, runtimes.PersonId, "person.prototype.friend", 75d, options);
            Step12ContextRecordReference[] returnedRecords = first.Records as Step12ContextRecordReference[];
            if (returnedRecords != null && returnedRecords.Length > 0)
            {
                returnedRecords[0] = new Step12ContextRecordReference("mutated", "mutated", Step12SocialProjectionState.HiddenState);
            }

            bool valid = string.IsNullOrWhiteSpace(seedFailure)
                && first.Fingerprint == second.Fingerprint
                && first.Truncated
                && first.SourceRuntimes.Count == 11
                && first.Records.Any(item => item.RuntimeName == nameof(RelationshipRuntime))
                && first.Records.Any(item => item.RuntimeName == nameof(SocialInteractionRuntime))
                && first.Records.Any(item => item.RuntimeName == nameof(FamilyRelationshipRuntime))
                && !first.Records.Any(item => item.RuntimeName == "mutated")
                && first.Records.Select(item => $"{item.RuntimeName}:{item.RecordId}").SequenceEqual(first.Records.Select(item => $"{item.RuntimeName}:{item.RecordId}").OrderBy(item => item, StringComparer.Ordinal));

            return TestLabAssertions.True("step12-integration-context", "Bounded social context projections are deterministic and immutable", valid, $"Seed='{seedFailure}' Records={first.Records.Count} Runtimes={first.SourceRuntimes.Count} Truncated={first.Truncated} Diagnostics=[{string.Join(";", first.Diagnostics)}] Fingerprint={first.Fingerprint}");
        }

        private static TestLabAutomationStepResult Step12IntegrationTransactionScheduler(TestLabAutomationContext context)
        {
            Step12SocialSimulationTransactionCoordinator coordinator = new Step12SocialSimulationTransactionCoordinator();
            bool previewed = false;
            bool committed = false;
            bool rolledBack = false;

            Step12TransactionParticipantPlan[] failingPlans =
            {
                new Step12TransactionParticipantPlan(nameof(RelationshipRuntime), Step12TransactionFailurePolicy.Required, () => previewed = true, () => true, () => committed = true, () => rolledBack = true),
                new Step12TransactionParticipantPlan(nameof(ReputationRuntime), Step12TransactionFailurePolicy.Required, () => true, () => true, () => false, () => rolledBack = true)
            };
            Step12TransactionResult preview = coordinator.Execute(Tx(context, "step12-integration-tx"), failingPlans, preview: true);
            Step12TransactionResult failed = coordinator.Execute(Tx(context, "step12-integration-tx"), failingPlans);
            Step12TransactionResult success = coordinator.Execute(Tx(context, "step12-integration-success"), new[]
            {
                new Step12TransactionParticipantPlan(nameof(RelationshipRuntime), Step12TransactionFailurePolicy.Required, () => true, () => true, () => true, () => true)
            });
            Step12TransactionResult duplicate = coordinator.Execute(Tx(context, "step12-integration-success"), failingPlans);

            Step12IntegrationValidationReport validation = new Step12IntegrationValidationReport();
            Step12SocialSimulationValidator.ValidateSchedulerBudget(new Step12SchedulerBudget
            {
                MaximumEvaluationsPerTick = 0,
                MaximumQueuedConsequences = 8,
                MaximumRecursionDepth = 99,
                UseSystemTime = true,
                AllowImmediateRecursiveDispatch = true
            }, validation);
            Step12SocialSimulationValidator.ValidatePersistenceDependencies(new[]
            {
                new Step12PersistenceDependencyEntry("a", "b"),
                new Step12PersistenceDependencyEntry("b", "a")
            }, validation);

            bool valid = preview.Succeeded
                && preview.Preview
                && previewed
                && !failed.Succeeded
                && committed
                && rolledBack
                && success.Succeeded
                && duplicate.Succeeded
                && duplicate.Duplicate
                && !validation.Succeeded
                && validation.Diagnostics.Any(item => item.Code == "system-time")
                && validation.Diagnostics.Any(item => item.Code == "immediate-recursion")
                && validation.Diagnostics.Any(item => item.Code == "dependency-cycle");

            return TestLabAssertions.True("step12-integration-transaction", "Cross-runtime transaction and scheduler guardrails are explicit", valid, $"Preview={preview.Succeeded}/{preview.Preview} Failed={failed.Succeeded} Success={success.Succeeded} Duplicate={duplicate.Duplicate} Validation={validation.ToSummary()}");
        }

        private static TestLabAutomationStepResult Step12IntegrationHealthHandoff(TestLabAutomationContext context)
        {
            if (!TryCreateStep12Facade(context, out Step12SocialSimulationFacade facade, out string failure))
            {
                return TestLabAssertions.Fail("step12-integration-health", "Create health snapshot and consequence reference", "FacadeReady", "Succeeded", "MissingRuntime", failure);
            }

            string seedFailure = SeedStep12IntegrationContext(context, "handoff");
            Step12HealthSnapshot first = facade.CreateHealthSnapshot();
            Step12HealthSnapshot second = facade.CreateHealthSnapshot();
            Step12ConsequenceReference handoff = facade.CreateConsequenceReference(
                "12.12",
                "social-context.prototype.step13",
                Tx(context, "step13-handoff"),
                "Step13SocialConsumer",
                "step13.signal.prototype.social-context",
                "ExposeImmutableSocialSignals",
                100d,
                first.Runtimes.Sum(item => item.Revision),
                Step12SocialVisibility.Diagnostic);

            bool valid = string.IsNullOrWhiteSpace(seedFailure)
                && first.Status == Step12HealthStatus.Ready
                && second.Status == Step12HealthStatus.Ready
                && first.Fingerprint == second.Fingerprint
                && handoff.Active
                && handoff.SourceFeature == "12.12"
                && handoff.DestinationRuntime == "Step13SocialConsumer"
                && handoff.Visibility == Step12SocialVisibility.Diagnostic
                && first.Runtimes.Count == 11;

            return TestLabAssertions.True("step12-integration-health", "Health snapshot and Step 13 handoff remain immutable references", valid, $"Seed='{seedFailure}' Health={first.Status}/{second.Status} Runtimes={first.Runtimes.Count} Handoff={handoff.SourceFeature}->{handoff.DestinationRuntime} Fingerprint={first.Fingerprint}");
        }

        private static TestLabAutomationStepResult FamilyDefinitionsAndParentage(TestLabAutomationContext context)
        {
            if (!TryGetFamilyRuntime(context, out FamilyRelationshipRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-family-parentage", "Create parentage and reject self/cycle parentage", "FamilyRelationshipRuntime", "MissingRuntime", failure);
            }

            bool resolved = registry.TryGet(PrototypeRelationshipDefinitionFactory.BiologicalParentChildRelationshipId, out RelationshipDefinition _)
                && registry.TryGet(PrototypeRelationshipDefinitionFactory.AdoptiveParentChildRelationshipId, out RelationshipDefinition _)
                && registry.TryGet(PrototypeRelationshipDefinitionFactory.LegalGuardianDependentRelationshipId, out RelationshipDefinition _)
                && registry.TryGet(PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId, out RomanticEligibilityPolicyDefinition _)
                && registry.TryGet(PrototypeFamilyRelationshipDefinitionFactory.FamilyHouseholdDefinitionId, out HouseholdDefinition _)
                && registry.TryGet(PrototypeAttitudeDefinitionFactory.RomanticAttractionId, out AttitudeDimensionDefinition _);

            FamilyRelationshipMutationResult biological = runtime.RecordParentage(new FamilyParentageRequest
            {
                transactionId = Tx(context, "family-bio-parent"),
                recordId = Scoped(context, "bio-parent"),
                parentPersonId = "person.prototype.parent",
                childPersonId = "person.prototype.child",
                parentageKind = ParentageKind.Biological,
                visibility = FamilyVisibility.Public,
                worldTime = 1d
            });
            FamilyRelationshipMutationResult duplicate = runtime.RecordParentage(new FamilyParentageRequest
            {
                transactionId = Tx(context, "family-bio-parent"),
                recordId = Scoped(context, "bio-parent"),
                parentPersonId = "person.prototype.parent",
                childPersonId = "person.prototype.child",
                parentageKind = ParentageKind.Biological,
                visibility = FamilyVisibility.Public,
                worldTime = 1d
            });
            FamilyRelationshipMutationResult self = runtime.RecordParentage(new FamilyParentageRequest
            {
                transactionId = Tx(context, "family-self-parent"),
                recordId = Scoped(context, "self-parent"),
                parentPersonId = "person.prototype.parent",
                childPersonId = "person.prototype.parent",
                parentageKind = ParentageKind.Biological,
                worldTime = 2d
            });
            FamilyRelationshipMutationResult cycle = runtime.RecordParentage(new FamilyParentageRequest
            {
                transactionId = Tx(context, "family-cycle-parent"),
                recordId = Scoped(context, "cycle-parent"),
                parentPersonId = "person.prototype.child",
                childPersonId = "person.prototype.parent",
                parentageKind = ParentageKind.Biological,
                worldTime = 3d
            });

            bool valid = resolved
                && biological.Succeeded
                && duplicate.Succeeded
                && duplicate.Duplicate
                && !self.Succeeded
                && self.Status == RomanticEligibilityStatus.InvalidRequest
                && !cycle.Succeeded
                && cycle.Status == RomanticEligibilityStatus.ProhibitedKinship
                && runtime.GetParents("person.prototype.child", ParentageKind.Biological, activeOnly: true, privileged: true).Count == 1;
            return TestLabAssertions.True("step12-family-parentage", "Parentage definitions resolve and invariants reject unsafe records", valid, $"Resolved={resolved} Bio={biological.Status} Duplicate={duplicate.Status} Self={self.Status} Cycle={cycle.Status}");
        }

        private static TestLabAutomationStepResult FamilyKinshipAndVisibility(TestLabAutomationContext context)
        {
            if (!TryGetFamilyRuntime(context, out FamilyRelationshipRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-family-kinship", "Resolve derived kinship and hidden parentage boundaries", "FamilyRelationshipRuntime", "MissingRuntime", failure);
            }

            runtime.RecordParentage(new FamilyParentageRequest { transactionId = Tx(context, "family-parent-a"), recordId = Scoped(context, "family-parent-a"), parentPersonId = "person.prototype.parent", childPersonId = "person.prototype.child", parentageKind = ParentageKind.Biological, visibility = FamilyVisibility.Public, worldTime = 1d });
            runtime.RecordParentage(new FamilyParentageRequest { transactionId = Tx(context, "family-parent-b"), recordId = Scoped(context, "family-parent-b"), parentPersonId = "person.prototype.parent", childPersonId = "person.prototype.student", parentageKind = ParentageKind.Biological, visibility = FamilyVisibility.Public, worldTime = 1d });
            runtime.RecordParentage(new FamilyParentageRequest { transactionId = Tx(context, "family-hidden"), recordId = Scoped(context, "family-hidden"), parentPersonId = "person.prototype.mentor", childPersonId = "person.prototype.rival", parentageKind = ParentageKind.Biological, visibility = FamilyVisibility.Hidden, worldTime = 1d });

            KinshipPathResult siblingA = runtime.ResolveKinship("person.prototype.child", "person.prototype.student", privileged: false);
            KinshipPathResult siblingB = runtime.ResolveKinship("person.prototype.child", "person.prototype.student", privileged: false);
            FamilyTreeSnapshot publicTree = runtime.CreateFamilyTreeSnapshot("person.prototype.rival", privileged: false);
            FamilyTreeSnapshot privilegedTree = runtime.CreateFamilyTreeSnapshot("person.prototype.rival", privileged: true);
            FamilyTreeSnapshot truncated = runtime.CreateFamilyTreeSnapshot("person.prototype.child", new KinshipTraversalLimits { maximumVisitedPersons = 1, maximumAncestorDepth = 1, maximumDescendantDepth = 1 }, privileged: true);

            bool valid = siblingA.Classification == KinshipClassification.HalfSibling
                && siblingB.Classification == siblingA.Classification
                && publicTree.Relationships.Count == 0
                && privilegedTree.Relationships.Count == 1
                && truncated.Truncated;
            return TestLabAssertions.True("step12-family-kinship", "Kinship queries are deterministic and visibility aware", valid, $"Sibling={siblingA.Classification}/{siblingB.Classification} Public={publicTree.Relationships.Count} Privileged={privilegedTree.Relationships.Count} Truncated={truncated.Truncated}");
        }

        private static TestLabAutomationStepResult FamilyRomanceEligibilityLifecycle(TestLabAutomationContext context)
        {
            if (!TryGetFamilyRuntime(context, out FamilyRelationshipRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-family-romance", "Evaluate and execute romantic lifecycle transitions", "FamilyRelationshipRuntime", "MissingRuntime", failure);
            }

            string player = context.ScenarioContext.Runtimes.PersonId;
            context.ScenarioContext.Runtimes.Attitudes.Mutate(new AttitudeMutationRequest { transactionId = Tx(context, "family-romance-player-attraction"), observerPersonId = player, subjectPersonId = "person.prototype.partner", dimensionId = PrototypeAttitudeDefinitionFactory.RomanticAttractionId, mutationKind = AttitudeMutationKind.SetBaseline, value = 70, worldTime = 1d });
            context.ScenarioContext.Runtimes.Attitudes.Mutate(new AttitudeMutationRequest { transactionId = Tx(context, "family-romance-partner-attraction"), observerPersonId = "person.prototype.partner", subjectPersonId = player, dimensionId = PrototypeAttitudeDefinitionFactory.RomanticAttractionId, mutationKind = AttitudeMutationKind.SetBaseline, value = 72, worldTime = 1d });

            RomanticEligibilityResult eligible = runtime.EvaluateRomanticEligibility(new RomanticEligibilityRequest { actorPersonId = player, targetPersonId = "person.prototype.partner", policyDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId, transitionKind = RomanticTransitionKind.EstablishPartnership, consentKind = RomanticConsentKind.PlayerChoice });
            RomanticEligibilityResult complianceRejected = runtime.EvaluateRomanticEligibility(new RomanticEligibilityRequest { actorPersonId = player, targetPersonId = "person.prototype.partner", policyDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId, transitionKind = RomanticTransitionKind.EstablishPartnership, consentKind = RomanticConsentKind.Compliance });
            RomanticEligibilityResult childRejected = runtime.EvaluateRomanticEligibility(new RomanticEligibilityRequest { actorPersonId = player, targetPersonId = "person.prototype.child", policyDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId, transitionKind = RomanticTransitionKind.EstablishPartnership, consentKind = RomanticConsentKind.PlayerChoice });
            RomanticTransitionResult partnership = runtime.ExecuteRomanticTransition(new RomanticTransitionRequest { transactionId = Tx(context, "family-romance-partnership"), relationshipRecordId = Scoped(context, "family-romance-partnership"), actorPersonId = player, targetPersonId = "person.prototype.partner", policyDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId, transitionKind = RomanticTransitionKind.EstablishPartnership, consentKind = RomanticConsentKind.PlayerChoice, worldTime = 5d });
            RomanticTransitionResult duplicate = runtime.ExecuteRomanticTransition(new RomanticTransitionRequest { transactionId = Tx(context, "family-romance-partnership"), relationshipRecordId = Scoped(context, "family-romance-partnership"), actorPersonId = player, targetPersonId = "person.prototype.partner", policyDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId, transitionKind = RomanticTransitionKind.EstablishPartnership, consentKind = RomanticConsentKind.PlayerChoice, worldTime = 5d });

            bool valid = eligible.Eligible
                && !complianceRejected.Eligible
                && complianceRejected.Status == RomanticEligibilityStatus.InvalidConsent
                && !childRejected.Eligible
                && childRejected.Status == RomanticEligibilityStatus.NonAdult
                && partnership.Succeeded
                && duplicate.Duplicate
                && context.ScenarioContext.Runtimes.Relationships.QueryByDefinition(PrototypeRelationshipDefinitionFactory.DomesticPartnerRelationshipId, activeOnly: true).Count == 1;
            return TestLabAssertions.True("step12-family-romance", "Romance requires adult eligibility and explicit consent", valid, $"Eligible={eligible.Status} Compliance={complianceRejected.Status} Child={childRejected.Status} Partnership={partnership.Status} Duplicate={duplicate.Status}");
        }

        private static TestLabAutomationStepResult FamilyHouseholdsAndPersistence(TestLabAutomationContext context)
        {
            if (!TryGetFamilyRuntime(context, out FamilyRelationshipRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-family-household-persistence", "Create, transfer, save, restore, and reject corrupt household state", "FamilyRelationshipRuntime", "MissingRuntime", failure);
            }

            string player = context.ScenarioContext.Runtimes.PersonId;
            HouseholdMutationResult create = runtime.CreateHousehold(new HouseholdMutationRequest { transactionId = Tx(context, "family-household-create"), householdId = Scoped(context, "household-a"), householdDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.FamilyHouseholdDefinitionId, personId = player, role = HouseholdRole.Head, residencePlaceId = "place.prototype.home", propertyReferenceId = "property.prototype.home", worldTime = 1d });
            HouseholdMutationResult add = runtime.AddMember(new HouseholdMutationRequest { transactionId = Tx(context, "family-household-add"), householdId = Scoped(context, "household-a"), personId = "person.prototype.partner", role = HouseholdRole.CoHead, worldTime = 2d });
            HouseholdMutationResult role = runtime.ChangeMemberRole(new HouseholdMutationRequest { transactionId = Tx(context, "family-household-role"), householdId = Scoped(context, "household-a"), personId = "person.prototype.partner", role = HouseholdRole.AdultMember, worldTime = 3d });
            HouseholdMutationResult shared = runtime.CreateHousehold(new HouseholdMutationRequest { transactionId = Tx(context, "family-household-shared"), householdId = Scoped(context, "household-b"), householdDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.SharedResidenceHouseholdDefinitionId, personId = "person.prototype.friend", role = HouseholdRole.AdultMember, worldTime = 4d });
            HouseholdMutationResult merge = runtime.MergeHouseholds(new HouseholdTransferRequest { transactionId = Tx(context, "family-household-merge"), sourceHouseholdId = Scoped(context, "household-b"), targetHouseholdId = Scoped(context, "household-a"), worldTime = 5d });
            FamilyRelationshipRuntimeSaveData save = runtime.CreateSaveData();
            FamilyRelationshipRuntime restored = new FamilyRelationshipRuntime();
            restored.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.Relationships, context.ScenarioContext.Runtimes.Attitudes, context.ScenarioContext.Runtimes.SocialInteractions, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds.Where(id => !id.Contains(".child", StringComparison.Ordinal) && !id.Contains(".dependent", StringComparison.Ordinal)));
            RomanticTransitionResult restore = restored.RestoreFromSaveData(save, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            FamilyRelationshipRuntimeSaveData corrupt = save.Clone();
            corrupt.households[0].worldId = "world.other";
            bool corruptRejected = !FamilyRelationshipRuntime.ValidateSaveData(corrupt, registry, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.WorldId, out _);

            bool valid = create.Succeeded
                && add.Succeeded
                && role.Succeeded
                && shared.Succeeded
                && merge.Succeeded
                && restore.Succeeded
                && restored.TryGetHousehold(Scoped(context, "household-a"), out HouseholdSnapshot restoredHousehold)
                && restoredHousehold.ActiveMemberships.Count == 3
                && corruptRejected;
            return TestLabAssertions.True("step12-family-household-persistence", "Households own membership lifecycle and persist independently", valid, $"Create={create.Status} Add={add.Status} Role={role.Status} Shared={shared.Status} Merge={merge.Status} Restore={restore.Status} CorruptRejected={corruptRejected}");
        }

        private static TestLabAutomationStepResult EmotionReadiness(TestLabAutomationContext context)
        {
            if (!TryGetEmotionRuntime(context, out SocialEmotionRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-emotion-readiness", "Resolve canonical emotion definitions", "SocialEmotionRuntime", "MissingRuntime", failure);
            }

            string[] emotionIds =
            {
                PrototypeSocialEmotionDefinitionFactory.JoyId,
                PrototypeSocialEmotionDefinitionFactory.SadnessId,
                PrototypeSocialEmotionDefinitionFactory.AngerId,
                PrototypeSocialEmotionDefinitionFactory.FearId,
                PrototypeSocialEmotionDefinitionFactory.ReliefId,
                PrototypeSocialEmotionDefinitionFactory.GratitudeId,
                PrototypeSocialEmotionDefinitionFactory.GuiltId,
                PrototypeSocialEmotionDefinitionFactory.ShameId,
                PrototypeSocialEmotionDefinitionFactory.PrideId,
                PrototypeSocialEmotionDefinitionFactory.AnxietyId,
                PrototypeSocialEmotionDefinitionFactory.DisgustId,
                PrototypeSocialEmotionDefinitionFactory.EnvyId,
                PrototypeSocialEmotionDefinitionFactory.ResentmentId,
                PrototypeSocialEmotionDefinitionFactory.HopeId,
                PrototypeSocialEmotionDefinitionFactory.DisappointmentId
            };
            bool resolved = emotionIds.All(id => registry.TryGet(id, out SocialEmotionDefinition _))
                && registry.TryGet(PrototypeSocialEmotionDefinitionFactory.MoodValenceId, out SocialMoodDimensionDefinition _)
                && registry.TryGet(PrototypeSocialEmotionDefinitionFactory.DetectedDeceptionRuleId, out SocialEmotionAppraisalRuleDefinition _)
                && runtime.IsReady;
            return TestLabAssertions.True("step12-emotion-readiness", "Emotion, mood, and appraisal definitions resolve", resolved, $"Resolved={resolved} Runtime={runtime.IsReady} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult EmotionBeliefRelativeAppraisal(TestLabAutomationContext context)
        {
            if (!TryGetEmotionRuntime(context, out SocialEmotionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-emotion-appraisal", "Trigger appraisal from believed social information", "SocialEmotionRuntime", "MissingRuntime", failure);
            }

            SocialEmotionResult result = runtime.Execute(new SocialEmotionTriggerRequest
            {
                TransactionId = Tx(context, "emotion-threat"),
                PersonId = context.ScenarioContext.Runtimes.PersonId,
                Cause = new SocialEmotionCauseReferenceData
                {
                    category = SocialEmotionCauseCategory.BeliefAccepted,
                    sourceRuntime = "SocialInfluenceRuntime",
                    sourceRecordId = Scoped(context, "accepted-threat"),
                    subjectId = "claim.prototype.threat",
                    targetPersonId = "person.prototype.rival",
                    responsibility = SocialEmotionResponsibility.Target,
                    believedTruthStatus = SocialInfluenceTruthStatus.True,
                    tags = new[] { "threat" }
                },
                WorldTime = 12d
            });

            bool valid = result.Succeeded
                && result.Episode != null
                && result.Episode.EmotionDefinitionId == PrototypeSocialEmotionDefinitionFactory.FearId
                && result.Episode.CurrentIntensity > 0
                && result.Mood != null
                && result.Mood.MoodDimensionId == PrototypeSocialEmotionDefinitionFactory.MoodAnxietyId;
            return TestLabAssertions.True("step12-emotion-appraisal", "Belief-relative appraisal creates the expected emotion", valid, $"Status={result.Status} Emotion={result.Episode?.EmotionDefinitionId} Mood={result.Mood?.MoodDimensionId} Intensity={result.Episode?.CurrentIntensity}");
        }

        private static TestLabAutomationStepResult EmotionDecayAndStacking(TestLabAutomationContext context)
        {
            if (!TryGetEmotionRuntime(context, out SocialEmotionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-emotion-decay", "Apply deterministic decay and stacking", "SocialEmotionRuntime", "MissingRuntime", failure);
            }

            SocialEmotionTriggerRequest request = new SocialEmotionTriggerRequest
            {
                TransactionId = Tx(context, "emotion-help"),
                PersonId = context.ScenarioContext.Runtimes.PersonId,
                EmotionDefinitionId = PrototypeSocialEmotionDefinitionFactory.GratitudeId,
                TargetPersonId = "person.prototype.friend",
                SubjectId = Scoped(context, "helpful-act"),
                IntensityOverride = 60,
                DurationOverrideSeconds = 100d,
                Cause = new SocialEmotionCauseReferenceData { category = SocialEmotionCauseCategory.Interaction, targetPersonId = "person.prototype.friend", subjectId = Scoped(context, "helpful-act"), responsibility = SocialEmotionResponsibility.Target, tags = new[] { "help" } },
                WorldTime = 20d
            };
            SocialEmotionResult first = runtime.Execute(request);
            SocialEmotionResult duplicate = runtime.Execute(request);
            SocialEmotionResult reinforce = runtime.Execute(new SocialEmotionTriggerRequest
            {
                TransactionId = Tx(context, "emotion-help-reinforce"),
                PersonId = request.PersonId,
                EmotionDefinitionId = request.EmotionDefinitionId,
                TargetPersonId = request.TargetPersonId,
                SubjectId = request.SubjectId,
                IntensityOverride = 45,
                DurationOverrideSeconds = 100d,
                Cause = request.Cause,
                WorldTime = 25d
            });
            int at50a = runtime.QueryActiveEpisodes(request.PersonId, 50d).FirstOrDefault()?.CurrentIntensity ?? 0;
            int at50b = runtime.QueryActiveEpisodes(request.PersonId, 50d).FirstOrDefault()?.CurrentIntensity ?? 0;
            bool valid = first.Succeeded
                && duplicate.Duplicate
                && reinforce.Succeeded
                && runtime.QueryActiveEpisodes(request.PersonId, 25d).Count == 1
                && at50a == at50b
                && at50a > 0
                && at50a < (reinforce.Episode?.CurrentIntensity ?? 0);
            return TestLabAssertions.True("step12-emotion-decay", "Emotion decay and reinforcement are deterministic", valid, $"First={first.Status} Duplicate={duplicate.Status} Reinforce={reinforce.Status} Count={runtime.QueryActiveEpisodes(request.PersonId, 25d).Count} At50={at50a}/{at50b}");
        }

        private static TestLabAutomationStepResult EmotionDecisionModifiers(TestLabAutomationContext context)
        {
            if (!TryGetEmotionRuntime(context, out SocialEmotionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-emotion-decision", "Apply emotion decision modifier", "SocialEmotionRuntime", "MissingRuntime", failure);
            }

            SocialEmotionResult anger = runtime.Execute(new SocialEmotionTriggerRequest
            {
                TransactionId = Tx(context, "emotion-deception"),
                PersonId = context.ScenarioContext.Runtimes.PersonId,
                Cause = new SocialEmotionCauseReferenceData { category = SocialEmotionCauseCategory.DeceptionDetected, targetPersonId = "person.prototype.rival", subjectId = "claim.prototype.lie", responsibility = SocialEmotionResponsibility.Target, believedTruthStatus = SocialInfluenceTruthStatus.False, detectionOutcome = SocialInfluenceDetectionOutcome.Detected, tags = new[] { "deception" } },
                WorldTime = 30d
            });
            int modifier = runtime.ResolveSocialDecisionScoreModifier(context.ScenarioContext.Runtimes.PersonId, "person.prototype.rival", string.Empty, string.Empty, 31d, out string modifierId);
            bool valid = anger.Succeeded && modifier < 0 && !string.IsNullOrWhiteSpace(modifierId) && context.ScenarioContext.Runtimes.SocialDecisions.Count == 0;
            return TestLabAssertions.True("step12-emotion-decision", "Emotion modifiers feed social decisions without owning them", valid, $"Emotion={anger.Status} Modifier={modifier} Source={modifierId} Decisions={context.ScenarioContext.Runtimes.SocialDecisions.Count}");
        }

        private static TestLabAutomationStepResult EmotionPersistenceAndProjection(TestLabAutomationContext context)
        {
            if (!TryGetEmotionRuntime(context, out SocialEmotionRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-emotion-persistence", "Save, restore, and project emotion state", "SocialEmotionRuntime", "MissingRuntime", failure);
            }

            SocialEmotionResult result = runtime.Execute(new SocialEmotionTriggerRequest
            {
                TransactionId = Tx(context, "emotion-concealed"),
                PersonId = context.ScenarioContext.Runtimes.PersonId,
                EmotionDefinitionId = PrototypeSocialEmotionDefinitionFactory.ShameId,
                SubjectId = Scoped(context, "mistake"),
                Concealed = true,
                WorldTime = 40d
            });
            SocialEmotionRuntimeSaveData save = runtime.CreateSaveData();
            SocialEmotionRuntime restored = new SocialEmotionRuntime();
            restored.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds);
            SocialEmotionResult restore = restored.RestoreFromSaveData(save, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            SocialEmotionProjection ownerProjection = restored.GetProjection(context.ScenarioContext.Runtimes.PersonId, result.Episode?.EpisodeId, privileged: false, worldTime: 41d);
            SocialEmotionProjection otherProjection = restored.GetProjection("person.prototype.rival", result.Episode?.EpisodeId, privileged: false, worldTime: 41d);
            save.episodes[0].personId = "person.prototype.unknown";
            bool invalidRejected = !SocialEmotionRuntime.ValidateSaveData(save, registry, context.ScenarioContext.Runtimes.KnownPersonIds, out _);
            bool valid = result.Succeeded
                && restore.Succeeded
                && ownerProjection.Access == SocialEmotionProjectionAccess.Full
                && otherProjection.Access == SocialEmotionProjectionAccess.Concealed
                && invalidRejected
                && restored.QueryActiveEpisodes(context.ScenarioContext.Runtimes.PersonId, 41d).Count == 1;
            return TestLabAssertions.True("step12-emotion-persistence", "Persistence and projections preserve affective state", valid, $"Create={result.Status} Restore={restore.Status} Owner={ownerProjection.Access} Other={otherProjection.Access} InvalidRejected={invalidRejected}");
        }

        private static TestLabAutomationStepResult SymmetricAndDirectedRelationships(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out RelationshipRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-relationship-create", "Create and query relationships", "RelationshipRuntime", "MissingRuntime", failure);
            }

            RelationshipOperationResult friend = runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = Scoped(context, "friend"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = "person.prototype.friend",
                firstRoleId = "friend",
                secondPersonId = context.ScenarioContext.Runtimes.PersonId,
                secondRoleId = "friend",
                sourceEventId = "event.relationship.friendship-start",
                startWorldTime = 10d,
                transactionId = Tx(context, "friend")
            });
            RelationshipOperationResult parentChild = runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = Scoped(context, "parent-child"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.ParentChildRelationshipId,
                firstPersonId = "person.prototype.parent",
                firstRoleId = "parent",
                secondPersonId = "person.prototype.child",
                secondRoleId = "child",
                sourceRecordId = "record.relationship.family-register",
                startWorldTime = 1d,
                transactionId = Tx(context, "parent-child")
            });

            RelationshipSnapshot friendSnapshot = friend.Snapshot;
            bool symmetricCanonical = friendSnapshot != null
                && friendSnapshot.Participants.Count == 2
                && string.CompareOrdinal(friendSnapshot.Participants[0].personId, friendSnapshot.Participants[1].personId) <= 0;
            bool valid = registry.Contains(PrototypeRelationshipDefinitionFactory.FriendRelationshipId)
                && friend.Succeeded
                && parentChild.Succeeded
                && symmetricCanonical
                && runtime.QueryBetween(context.ScenarioContext.Runtimes.PersonId, "person.prototype.friend", activeOnly: true).Count == 1
                && runtime.QueryByRole("parent", activeOnly: true).Count == 1
                && runtime.QueryByCategory(RelationshipCategory.Personal, activeOnly: true).Count == 1
                && runtime.QueryByDefinition(PrototypeRelationshipDefinitionFactory.ParentChildRelationshipId, activeOnly: true).Count == 1;
            return TestLabAssertions.True("step12-relationship-create", "Symmetric and directed relationships create deterministic records", valid, $"Friend={friend.Status} Directed={parentChild.Status} Canonical={symmetricCanonical} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult LifecycleAndDuplicates(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out RelationshipRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-relationship-lifecycle", "End and reject duplicate active records", "RelationshipRuntime", "MissingRuntime", failure);
            }

            RelationshipCreateRequest request = new RelationshipCreateRequest
            {
                recordId = Scoped(context, "rival"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.RivalRelationshipId,
                firstPersonId = context.ScenarioContext.Runtimes.PersonId,
                firstRoleId = "rival",
                secondPersonId = "person.prototype.rival",
                secondRoleId = "rival",
                startWorldTime = 3d,
                transactionId = Tx(context, "rival")
            };
            RelationshipOperationResult create = runtime.CreateRelationship(request);
            RelationshipOperationResult duplicateSameId = runtime.CreateRelationship(request);
            RelationshipOperationResult duplicateActive = runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = Scoped(context, "rival-second"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.RivalRelationshipId,
                firstPersonId = "person.prototype.rival",
                firstRoleId = "rival",
                secondPersonId = context.ScenarioContext.Runtimes.PersonId,
                secondRoleId = "rival",
                startWorldTime = 4d,
                transactionId = Tx(context, "rival-duplicate")
            });
            RelationshipOperationResult ended = runtime.EndRelationship(new RelationshipEndRequest
            {
                recordId = request.recordId,
                endWorldTime = 9d,
                sourceEventId = "event.relationship.rivalry-ended",
                transactionId = Tx(context, "rival-end")
            });
            RelationshipOperationResult recreate = runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = Scoped(context, "rival-after-end"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.RivalRelationshipId,
                firstPersonId = "person.prototype.rival",
                firstRoleId = "rival",
                secondPersonId = context.ScenarioContext.Runtimes.PersonId,
                secondRoleId = "rival",
                startWorldTime = 10d,
                transactionId = Tx(context, "rival-recreate")
            });

            bool valid = create.Succeeded
                && duplicateSameId.Duplicate
                && duplicateActive.Status == RelationshipOperationStatus.DuplicateActiveRelationship
                && ended.Succeeded
                && recreate.Succeeded
                && runtime.QueryByStatus(RelationshipLifecycleStatus.Ended).Count == 1
                && runtime.QueryBetween(context.ScenarioContext.Runtimes.PersonId, "person.prototype.rival", activeOnly: true).Count == 1;
            return TestLabAssertions.True("step12-relationship-lifecycle", "Relationship lifecycle and duplicate active rules are enforced", valid, $"Create={create.Status} SameId={duplicateSameId.Status} DuplicateActive={duplicateActive.Status} End={ended.Status} Recreate={recreate.Status}");
        }

        private static TestLabAutomationStepResult PersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out RelationshipRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-relationship-persistence", "Save, restore, and reject corrupt payloads", "RelationshipRuntime", "MissingRuntime", failure);
            }

            RelationshipOperationResult create = runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = Scoped(context, "mentor"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.MentorStudentRelationshipId,
                firstPersonId = "person.prototype.mentor",
                firstRoleId = "mentor",
                secondPersonId = "person.prototype.student",
                secondRoleId = "student",
                startWorldTime = 12d,
                sourceEventId = "event.relationship.apprenticeship",
                transactionId = Tx(context, "mentor")
            });
            RelationshipRuntimeSaveData save = runtime.CreateSaveData();
            RelationshipRuntime restored = new RelationshipRuntime();
            RelationshipOperationResult restore = restored.RestoreFromSaveData(save, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoring: true);
            RelationshipRuntimeSaveData corrupt = save.Clone();
            corrupt.records[0].relationshipDefinitionId = "relationship.prototype.missing";
            bool rejected = !RelationshipRuntime.ValidateSaveData(corrupt, registry, context.ScenarioContext.Runtimes.KnownPersonIds, out string validationFailure);
            int countAfterRejectedValidation = runtime.Count;

            bool valid = create.Succeeded
                && restore.Succeeded
                && restored.Count == runtime.Count
                && restored.TryGetSnapshot(create.Snapshot.RecordId, out RelationshipSnapshot snapshot)
                && snapshot.SourceEventId == "event.relationship.apprenticeship"
                && rejected
                && countAfterRejectedValidation == runtime.Count;
            return TestLabAssertions.True("step12-relationship-persistence", "Relationship persistence validates before restoring", valid, $"Create={create.Status} Restore={restore.Status} Rejected={rejected} Failure='{validationFailure}' Count={runtime.Count}/{restored.Count}");
        }

        private static bool TryGetRuntime(TestLabAutomationContext context, out RelationshipRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Relationships;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Relationship runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            runtime.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds);
            failure = string.Empty;
            return true;
        }

        private static TestLabAutomationStepResult AttitudeDefinitionsAndNeutralValues(TestLabAutomationContext context)
        {
            if (!TryGetAttitudeRuntime(context, out InterpersonalAttitudeRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-attitudes-definitions", "Resolve attitude definitions and neutral values", "InterpersonalAttitudeRuntime", "MissingRuntime", failure);
            }

            string[] required =
            {
                PrototypeAttitudeDefinitionFactory.TrustId,
                PrototypeAttitudeDefinitionFactory.AffectionId,
                PrototypeAttitudeDefinitionFactory.RespectId,
                PrototypeAttitudeDefinitionFactory.FearId,
                PrototypeAttitudeDefinitionFactory.LoyaltyId,
                PrototypeAttitudeDefinitionFactory.HostilityId
            };
            bool allDefinitions = required.All(id => registry.TryGet(id, out AttitudeDimensionDefinition _));
            AttitudeEffectiveValueSnapshot trust = runtime.ResolveValue(context.ScenarioContext.Runtimes.PersonId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId);
            AttitudeEffectiveValueSnapshot fear = runtime.ResolveValue(context.ScenarioContext.Runtimes.PersonId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.FearId);
            bool valid = allDefinitions
                && runtime.Count == 0
                && trust.EffectiveValue == 0
                && fear.EffectiveValue == 0
                && trust.IsNeutralDefault
                && fear.IsNeutralDefault;
            return TestLabAssertions.True("step12-attitudes-definitions", "Canonical attitude dimensions resolve with neutral defaults", valid, $"Definitions={allDefinitions} Count={runtime.Count} Trust={trust.EffectiveValue} Fear={fear.EffectiveValue}");
        }

        private static TestLabAutomationStepResult DirectionalAttitudeValues(TestLabAutomationContext context)
        {
            if (!TryGetAttitudeRuntime(context, out InterpersonalAttitudeRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-attitudes-directional", "Mutate directed trust and hostility independently", "InterpersonalAttitudeRuntime", "MissingRuntime", failure);
            }

            string player = context.ScenarioContext.Runtimes.PersonId;
            AttitudeMutationResult trust = runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = Tx(context, "player-trusts-friend"),
                observerPersonId = player,
                subjectPersonId = "person.prototype.friend",
                dimensionId = PrototypeAttitudeDefinitionFactory.TrustId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 35,
                worldTime = 15d
            });
            AttitudeMutationResult hostility = runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = Tx(context, "friend-hostile-player"),
                observerPersonId = "person.prototype.friend",
                subjectPersonId = player,
                dimensionId = PrototypeAttitudeDefinitionFactory.HostilityId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 20,
                worldTime = 16d
            });

            int forwardTrust = runtime.ResolveValue(player, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue;
            int reverseTrust = runtime.ResolveValue("person.prototype.friend", player, PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue;
            int reverseHostility = runtime.ResolveValue("person.prototype.friend", player, PrototypeAttitudeDefinitionFactory.HostilityId).EffectiveValue;
            bool valid = trust.Succeeded
                && hostility.Succeeded
                && forwardTrust == 35
                && reverseTrust == 0
                && reverseHostility == 20
                && runtime.QueryByObserver(player).Count == 1
                && runtime.QueryBySubject(player).Count == 1;
            return TestLabAssertions.True("step12-attitudes-directional", "Directional attitudes do not mirror between people", valid, $"Trust={trust.Status}:{forwardTrust}/{reverseTrust} Hostility={hostility.Status}:{reverseHostility} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult ContributionsAndIdempotence(TestLabAutomationContext context)
        {
            if (!TryGetAttitudeRuntime(context, out InterpersonalAttitudeRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-attitudes-contributions", "Preview, execute, duplicate, and clamp source-owned contributions", "InterpersonalAttitudeRuntime", "MissingRuntime", failure);
            }

            string player = context.ScenarioContext.Runtimes.PersonId;
            AttitudeMutationRequest request = new AttitudeMutationRequest
            {
                transactionId = Tx(context, "hostility-source"),
                observerPersonId = player,
                subjectPersonId = "person.prototype.rival",
                dimensionId = PrototypeAttitudeDefinitionFactory.HostilityId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                sourceId = Scoped(context, "ambush-source"),
                sourceCategory = AttitudeContributionSourceCategory.TestLab,
                value = 150,
                worldTime = 20d,
                historicalEventId = "history.relationship.ambush"
            };
            request.preview = true;
            AttitudeMutationResult preview = runtime.Mutate(request);
            int afterPreviewCount = runtime.Count;
            request.preview = false;
            AttitudeMutationResult execute = runtime.Mutate(request);
            AttitudeMutationResult duplicate = runtime.Mutate(request);
            AttitudeEffectiveValueSnapshot effective = runtime.ResolveValue(player, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.HostilityId);

            bool valid = preview.Preview
                && afterPreviewCount == 0
                && execute.Succeeded
                && duplicate.Status == AttitudeOperationStatus.Duplicate
                && runtime.Count == 1
                && effective.EffectiveValue == 100
                && effective.Clamped
                && effective.Contributions.Count == 1
                && runtime.QueryByHistoricalEvent("history.relationship.ambush").Count == 1;
            return TestLabAssertions.True("step12-attitudes-contributions", "Source contributions clamp and duplicate transactions are idempotent", valid, $"Preview={preview.Status} Execute={execute.Status} Duplicate={duplicate.Status} Count={runtime.Count} Effective={effective.EffectiveValue} Clamped={effective.Clamped}");
        }

        private static TestLabAutomationStepResult RelationshipIndependence(TestLabAutomationContext context)
        {
            bool hasRelationships = TryGetRuntime(context, out RelationshipRuntime relationships, out _, out string relationshipFailure);
            bool hasAttitudes = TryGetAttitudeRuntime(context, out InterpersonalAttitudeRuntime attitudes, out _, out string attitudeFailure);
            if (!hasRelationships || !hasAttitudes)
            {
                return TestLabAssertions.Fail("step12-attitudes-relationship-independent", "End a relationship without deleting attitude values", "SocialRuntime", "MissingRuntime", $"{relationshipFailure} {attitudeFailure}".Trim());
            }

            string player = context.ScenarioContext.Runtimes.PersonId;
            RelationshipOperationResult friendship = relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = Scoped(context, "attitude-friendship"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = player,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 1d,
                transactionId = Tx(context, "friendship")
            });
            AttitudeMutationResult loyalty = attitudes.Mutate(new AttitudeMutationRequest
            {
                transactionId = Tx(context, "friendship-loyalty"),
                observerPersonId = player,
                subjectPersonId = "person.prototype.friend",
                dimensionId = PrototypeAttitudeDefinitionFactory.LoyaltyId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                sourceId = Scoped(context, "friendship-loyalty-source"),
                sourceCategory = AttitudeContributionSourceCategory.Relationship,
                relationshipRecordId = friendship.Snapshot?.RecordId,
                value = 40,
                worldTime = 2d
            });
            RelationshipOperationResult ended = relationships.EndRelationship(new RelationshipEndRequest
            {
                recordId = friendship.Snapshot?.RecordId,
                endWorldTime = 3d,
                transactionId = Tx(context, "friendship-ended")
            });

            AttitudeEffectiveValueSnapshot value = attitudes.ResolveValue(player, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.LoyaltyId);
            bool relationshipEnded = relationships.QueryBetween(player, "person.prototype.friend", activeOnly: true).Count == 0;
            bool valid = friendship.Succeeded
                && loyalty.Succeeded
                && ended.Succeeded
                && relationshipEnded
                && value.EffectiveValue == 40
                && attitudes.QueryByThreshold(PrototypeAttitudeDefinitionFactory.LoyaltyId, AttitudeThresholdComparison.GreaterThanOrEqual, 40).Count == 1;
            return TestLabAssertions.True("step12-attitudes-relationship-independent", "Relationship records can inform attitudes without owning them", valid, $"Friendship={friendship.Status} Loyalty={loyalty.Status} Ended={ended.Status} RelationshipEnded={relationshipEnded} LoyaltyValue={value.EffectiveValue}");
        }

        private static TestLabAutomationStepResult AttitudePersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetAttitudeRuntime(context, out InterpersonalAttitudeRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-attitudes-persistence", "Save, restore, and reject invalid attitude payloads", "InterpersonalAttitudeRuntime", "MissingRuntime", failure);
            }

            string player = context.ScenarioContext.Runtimes.PersonId;
            AttitudeMutationResult respect = runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = Tx(context, "respect-baseline"),
                observerPersonId = player,
                subjectPersonId = "person.prototype.mentor",
                dimensionId = PrototypeAttitudeDefinitionFactory.RespectId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 55,
                worldTime = 25d
            });
            InterpersonalAttitudeRuntimeSaveData save = runtime.CreateSaveData();
            InterpersonalAttitudeRuntime restored = new InterpersonalAttitudeRuntime();
            AttitudeMutationResult restore = restored.RestoreFromSaveData(save, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            InterpersonalAttitudeRuntimeSaveData corrupt = save.Clone();
            corrupt.records[0].dimensions[0].baselineValue = 999;
            bool rejected = !InterpersonalAttitudeRuntime.ValidateSaveData(corrupt, registry, context.ScenarioContext.Runtimes.KnownPersonIds, out string validationFailure);
            int liveValue = runtime.ResolveValue(player, "person.prototype.mentor", PrototypeAttitudeDefinitionFactory.RespectId).EffectiveValue;
            int restoredValue = restored.ResolveValue(player, "person.prototype.mentor", PrototypeAttitudeDefinitionFactory.RespectId).EffectiveValue;

            bool valid = respect.Succeeded
                && restore.Succeeded
                && rejected
                && liveValue == 55
                && restoredValue == 55
                && runtime.Count == 1
                && restored.Count == 1;
            return TestLabAssertions.True("step12-attitudes-persistence", "Attitudes persist and reject corrupt restores without mutation", valid, $"Respect={respect.Status} Restore={restore.Status} Rejected={rejected} Failure='{validationFailure}' Values={liveValue}/{restoredValue}");
        }

        private static bool TryGetAttitudeRuntime(TestLabAutomationContext context, out InterpersonalAttitudeRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Attitudes;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Interpersonal attitude runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            runtime.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds);
            failure = string.Empty;
            return true;
        }

        private static TestLabAutomationStepResult ReputationRuntimeReadiness(TestLabAutomationContext context)
        {
            if (!TryGetReputationRuntime(context, out ReputationRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-reputation-readiness", "Resolve reputation audiences and dimensions", "ReputationRuntime", "MissingRuntime", failure);
            }

            bool graphValid = ReputationRuntime.ValidateAudienceGraph(registry, out string graphFailure);
            bool valid = runtime.IsReady
                && runtime.Count == 0
                && registry.TryGet(PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, out ReputationAudienceDefinition _)
                && registry.TryGet(PrototypeReputationDefinitionFactory.PrototypeTownAudienceId, out ReputationAudienceDefinition _)
                && registry.TryGet(PrototypeReputationDefinitionFactory.RenownId, out ReputationDimensionDefinition _)
                && registry.TryGet(PrototypeReputationDefinitionFactory.EsteemId, out ReputationDimensionDefinition _)
                && graphValid;
            return TestLabAssertions.True("step12-reputation-readiness", "Reputation definitions and runtime are ready", valid, $"Ready={runtime.IsReady} Count={runtime.Count} GraphFailure='{graphFailure}'");
        }

        private static TestLabAutomationStepResult ReputationRecordIdentityAndDimensions(TestLabAutomationContext context)
        {
            if (!TryGetReputationRuntime(context, out ReputationRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-reputation-records", "Create records and mutate independent dimensions", "ReputationRuntime", "MissingRuntime", failure);
            }

            string subject = context.ScenarioContext.Runtimes.PersonId;
            ReputationMutationResult renown = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "renown"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.RenownId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 80,
                worldTime = 10d
            });
            ReputationMutationResult esteem = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "esteem"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.EsteemId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = -35,
                worldTime = 11d
            });
            bool resolvedById = runtime.TryGetSnapshot(renown.RecordId, out ReputationSnapshot byId);
            bool resolvedByPair = runtime.TryGetSnapshotBySubjectAudience(subject, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, out ReputationSnapshot byPair);
            ReputationMutationResult duplicatePair = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "duplicate-pair"),
                recordId = RepScoped(context, "duplicate"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.HonorId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 10,
                worldTime = 12d
            });

            bool valid = renown.Succeeded
                && esteem.Succeeded
                && resolvedById
                && resolvedByPair
                && byId.RecordId == byPair.RecordId
                && duplicatePair.Status == ReputationOperationStatus.DuplicateSubjectAudience
                && runtime.ResolveValue(subject, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.RenownId).EffectiveValue == 80
                && runtime.ResolveValue(subject, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.EsteemId).EffectiveValue == -35;
            return TestLabAssertions.True("step12-reputation-records", "Records and dimensions remain stable and independent", valid, $"Renown={renown.Status} Esteem={esteem.Status} Duplicate={duplicatePair.Status} Records={runtime.Count}");
        }

        private static TestLabAutomationStepResult ReputationAudienceIndependenceAndHierarchy(TestLabAutomationContext context)
        {
            if (!TryGetReputationRuntime(context, out ReputationRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-reputation-audiences", "Verify direct, inherited, and isolated audience values", "ReputationRuntime", "MissingRuntime", failure);
            }

            string subject = context.ScenarioContext.Runtimes.PersonId;
            ReputationMutationResult global = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "global-honor"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.HonorId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 20,
                worldTime = 13d
            });
            ReputationMutationResult guild = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "guild-honor"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.AdventurersGuildAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.HonorId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 70,
                worldTime = 14d
            });
            ReputationEffectiveValueSnapshot inherited = runtime.ResolveValue(subject, PrototypeReputationDefinitionFactory.AdventurersGuildVeteransAudienceId, PrototypeReputationDefinitionFactory.HonorId, allowInherited: true);
            ReputationEffectiveValueSnapshot direct = runtime.ResolveValue(subject, PrototypeReputationDefinitionFactory.AdventurersGuildVeteransAudienceId, PrototypeReputationDefinitionFactory.HonorId, allowInherited: false);
            ReputationMutationResult town = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "town-honor"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.PrototypeTownAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.HonorId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = -15,
                worldTime = 15d
            });

            bool valid = global.Succeeded
                && guild.Succeeded
                && town.Succeeded
                && inherited.EffectiveValue == 70
                && inherited.Inherited
                && inherited.SourceAudienceId == PrototypeReputationDefinitionFactory.AdventurersGuildAudienceId
                && direct.EffectiveValue == 0
                && runtime.ResolveValue(subject, PrototypeReputationDefinitionFactory.PrototypeTownAudienceId, PrototypeReputationDefinitionFactory.HonorId).EffectiveValue == -15
                && runtime.ResolveValue(subject, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.HonorId).EffectiveValue == 20;
            return TestLabAssertions.True("step12-reputation-audiences", "Audience independence and hierarchy are deterministic", valid, $"Inherited={inherited.EffectiveValue}/{inherited.Inherited}/{inherited.SourceAudienceId} Direct={direct.EffectiveValue} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult ReputationContributionsAndDisputes(TestLabAutomationContext context)
        {
            if (!TryGetReputationRuntime(context, out ReputationRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-reputation-contributions", "Preview, execute, duplicate, replace, remove, and classify sources", "ReputationRuntime", "MissingRuntime", failure);
            }

            string subject = context.ScenarioContext.Runtimes.PersonId;
            string accusationEventId = RepScoped(context, "accusation-event");
            ReputationMutationRequest disputed = new ReputationMutationRequest
            {
                transactionId = Tx(context, "disputed"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.RoyalJurisdictionAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.NotorietyId,
                mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                sourceId = RepScoped(context, "accusation"),
                sourceCategory = ReputationContributionSourceCategory.Accusation,
                authenticity = ReputationAuthenticity.Disputed,
                historicalEventId = accusationEventId,
                value = 90,
                worldTime = 16d,
                preview = true
            };
            ReputationMutationResult preview = runtime.Mutate(disputed);
            int afterPreviewCount = runtime.Count;
            disputed.preview = false;
            ReputationMutationResult execute = runtime.Mutate(disputed);
            ReputationMutationResult duplicate = runtime.Mutate(disputed);
            ReputationMutationResult verified = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "verified"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.RoyalJurisdictionAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.NotorietyId,
                mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                sourceId = RepScoped(context, "conviction"),
                sourceCategory = ReputationContributionSourceCategory.Conviction,
                authenticity = ReputationAuthenticity.Verified,
                value = 20,
                worldTime = 17d
            });
            ReputationMutationResult remove = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "remove-disputed"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.RoyalJurisdictionAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.NotorietyId,
                mutationKind = ReputationMutationKind.RemoveContribution,
                sourceId = RepScoped(context, "accusation"),
                worldTime = 18d
            });
            ReputationEffectiveValueSnapshot value = runtime.ResolveValue(subject, PrototypeReputationDefinitionFactory.RoyalJurisdictionAudienceId, PrototypeReputationDefinitionFactory.NotorietyId);

            bool valid = preview.Preview
                && afterPreviewCount == 0
                && execute.Succeeded
                && duplicate.Status == ReputationOperationStatus.Duplicate
                && verified.Succeeded
                && remove.Succeeded
                && value.EffectiveValue == 20
                && value.Contributions.Count == 1
                && value.Contributions[0].Authenticity == ReputationAuthenticity.Verified
                && runtime.QueryByHistoricalEvent(accusationEventId).Count == 0;
            return TestLabAssertions.True("step12-reputation-contributions", "Source contributions preserve dispute metadata and idempotence", valid, $"Preview={preview.Status} Execute={execute.Status} Duplicate={duplicate.Status} Verified={verified.Status} Remove={remove.Status} Value={value.EffectiveValue}");
        }

        private static TestLabAutomationStepResult ReputationRequirementsAndSeparation(TestLabAutomationContext context)
        {
            bool hasReputation = TryGetReputationRuntime(context, out ReputationRuntime reputationRuntime, out _, out string reputationFailure);
            bool hasRelationships = TryGetRuntime(context, out RelationshipRuntime relationshipRuntime, out _, out string relationshipFailure);
            bool hasAttitudes = TryGetAttitudeRuntime(context, out InterpersonalAttitudeRuntime attitudeRuntime, out _, out string attitudeFailure);
            if (!hasReputation || !hasRelationships || !hasAttitudes)
            {
                return TestLabAssertions.Fail("step12-reputation-requirements", "Evaluate thresholds and verify feature separation", "SocialRuntimes", "MissingRuntime", $"{reputationFailure} {relationshipFailure} {attitudeFailure}".Trim());
            }

            string subject = context.ScenarioContext.Runtimes.PersonId;
            RelationshipOperationResult relationship = relationshipRuntime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = RepScoped(context, "friendship"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = subject,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 19d,
                transactionId = Tx(context, "friendship")
            });
            AttitudeMutationResult attitude = attitudeRuntime.Mutate(new AttitudeMutationRequest
            {
                transactionId = Tx(context, "trust"),
                observerPersonId = subject,
                subjectPersonId = "person.prototype.friend",
                dimensionId = PrototypeAttitudeDefinitionFactory.TrustId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 30,
                worldTime = 20d
            });
            ReputationMutationResult reputation = reputationRuntime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "credibility"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.PrototypeTownAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.CredibilityId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 45,
                worldTime = 21d
            });
            ReputationThresholdResult passing = reputationRuntime.EvaluateThreshold(new ReputationThresholdRequest
            {
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.PrototypeTownAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.CredibilityId,
                comparison = ReputationThresholdComparison.GreaterThanOrEqual,
                value = 40
            });
            ReputationThresholdResult missing = reputationRuntime.EvaluateThreshold(new ReputationThresholdRequest
            {
                subjectPersonId = "person.prototype.unknown",
                audienceId = PrototypeReputationDefinitionFactory.PrototypeTownAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.CredibilityId,
                comparison = ReputationThresholdComparison.GreaterThanOrEqual,
                value = 40
            });

            bool valid = relationship.Succeeded
                && attitude.Succeeded
                && reputation.Succeeded
                && passing.Passed
                && missing.Status == ReputationOperationStatus.UnknownSubject
                && relationshipRuntime.Count == 1
                && attitudeRuntime.Count == 1
                && reputationRuntime.Count == 1;
            return TestLabAssertions.True("step12-reputation-requirements", "Requirement checks do not mutate relationships or attitudes", valid, $"Relationship={relationship.Status} Attitude={attitude.Status} Reputation={reputation.Status} Passing={passing.Passed} Missing={missing.Status}");
        }

        private static TestLabAutomationStepResult ReputationPersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetReputationRuntime(context, out ReputationRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-reputation-persistence", "Save, restore, and reject invalid reputation payloads", "ReputationRuntime", "MissingRuntime", failure);
            }

            string subject = context.ScenarioContext.Runtimes.PersonId;
            ReputationMutationResult baseline = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "persist-baseline"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.HiddenInvestigatorsAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.PerceivedDangerId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 65,
                worldTime = 22d
            });
            ReputationMutationResult source = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = Tx(context, "persist-source"),
                subjectPersonId = subject,
                audienceId = PrototypeReputationDefinitionFactory.HiddenInvestigatorsAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.PerceivedDangerId,
                mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                sourceId = RepScoped(context, "hidden-report"),
                sourceCategory = ReputationContributionSourceCategory.Propaganda,
                authenticity = ReputationAuthenticity.Propaganda,
                value = 10,
                supportingReferenceId = RepScoped(context, "hidden-supporting-record"),
                worldTime = 23d
            });
            ReputationRuntimeSaveData save = runtime.CreateSaveData();
            ReputationRuntime restored = new ReputationRuntime();
            ReputationMutationResult restore = restored.RestoreFromSaveData(save, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            ReputationRuntimeSaveData corrupt = save.Clone();
            corrupt.records[0].dimensions[0].baselineValue = 999;
            bool rejected = !ReputationRuntime.ValidateSaveData(corrupt, registry, context.ScenarioContext.Runtimes.KnownPersonIds, out string validationFailure);
            ReputationEffectiveValueSnapshot live = runtime.ResolveValue(subject, PrototypeReputationDefinitionFactory.HiddenInvestigatorsAudienceId, PrototypeReputationDefinitionFactory.PerceivedDangerId);
            ReputationEffectiveValueSnapshot restoredValue = restored.ResolveValue(subject, PrototypeReputationDefinitionFactory.HiddenInvestigatorsAudienceId, PrototypeReputationDefinitionFactory.PerceivedDangerId);

            bool valid = baseline.Succeeded
                && source.Succeeded
                && restore.Succeeded
                && rejected
                && live.EffectiveValue == 75
                && restoredValue.EffectiveValue == 75
                && restoredValue.Contributions.Count == 1
                && restoredValue.Contributions[0].Authenticity == ReputationAuthenticity.Propaganda;
            return TestLabAssertions.True("step12-reputation-persistence", "Reputation persists and rejects corrupt restores", valid, $"Baseline={baseline.Status} Source={source.Status} Restore={restore.Status} Rejected={rejected} Failure='{validationFailure}' Values={live.EffectiveValue}/{restoredValue.EffectiveValue}");
        }

        private static bool TryGetReputationRuntime(TestLabAutomationContext context, out ReputationRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Reputation;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Reputation runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            runtime.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds);
            failure = string.Empty;
            return true;
        }

        private static TestLabAutomationStepResult RumorReadinessAndRootIdentity(TestLabAutomationContext context)
        {
            if (!TryGetRumorRuntime(context, out RumorRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-rumor-root", "Create root rumor and query identity", "RumorRuntime", "MissingRuntime", failure);
            }

            RumorOperationResult created = CreateRootRumor(context, runtime, "root", PrototypeRumorDefinitionFactory.PublicNewsRumorId, context.ScenarioContext.Runtimes.PersonId, RumorAuthenticity.Verified);
            bool valid = created.Succeeded
                && registry.TryGet(PrototypeRumorDefinitionFactory.PublicNewsRumorId, out RumorDefinition _)
                && registry.TryGet(PrototypeRumorDefinitionFactory.ConversationChannelId, out RumorCommunicationChannelDefinition _)
                && created.Rumor != null
                && created.Rumor.RumorId == created.Rumor.RootRumorId
                && runtime.QueryByRoot(created.Rumor.RootRumorId).Count == 1
                && runtime.QueryByClaim(created.Rumor.ClaimIdentity).Count == 1
                && runtime.IsAware(context.ScenarioContext.Runtimes.PersonId, created.Rumor.RumorId);
            return TestLabAssertions.True("step12-rumor-root", "Rumor definitions resolve and root records are stable", valid, $"Create={created.Status} Count={runtime.RumorCount} Root={created.Rumor?.RootRumorId}");
        }

        private static TestLabAutomationStepResult RumorTransmissionCreatesKnowledgeAndMemory(TestLabAutomationContext context)
        {
            if (!TryGetRumorRuntime(context, out RumorRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-rumor-transmission", "Transmit rumor into listener knowledge and memory", "RumorRuntime", "MissingRuntime", failure);
            }

            string listener = context.ScenarioContext.Runtimes.PersonId;
            RumorOperationResult created = CreateRootRumor(context, runtime, "testimony-root", PrototypeRumorDefinitionFactory.PersonalConductRumorId, "person.prototype.friend", RumorAuthenticity.Unverified);
            RumorOperationResult transmitted = runtime.Transmit(new RumorTransmissionRequest
            {
                TransactionId = Tx(context, "transmit-to-player"),
                TransmissionId = RumorScoped(context, "transmission-player"),
                RumorVersionId = created.Rumor?.RumorId,
                SpeakerPersonId = "person.prototype.friend",
                ListenerPersonId = listener,
                ChannelId = PrototypeRumorDefinitionFactory.ConversationChannelId,
                RequestedOutcome = RumorTransmissionOutcome.Believed,
                SpeakerConfidence = 760,
                WorldTime = 22d
            });

            KnowledgeSnapshot knowledge = context.ScenarioContext.Runtimes.Knowledge.CreateSnapshot();
            PersonMemorySnapshot memory = context.ScenarioContext.Runtimes.Memory.CreateSnapshot();
            bool valid = created.Succeeded
                && transmitted.Succeeded
                && transmitted.KnowledgeResult?.Succeeded == true
                && transmitted.MemoryResult?.Succeeded == true
                && !string.IsNullOrWhiteSpace(transmitted.Transmission?.EvidenceId)
                && !string.IsNullOrWhiteSpace(transmitted.Transmission?.MemoryId)
                && knowledge.Evidence.Count == 1
                && memory.Memories.Count == 1
                && runtime.IsAware(listener, transmitted.Rumor.RumorId);
            return TestLabAssertions.True("step12-rumor-transmission", "Transmission records listener evidence and memory", valid, $"Create={created.Status} Transmit={transmitted.Status} Evidence={knowledge.Evidence.Count} Memories={memory.Memories.Count} Outcome={transmitted.Transmission?.Outcome}");
        }

        private static TestLabAutomationStepResult RumorDistortionLineage(TestLabAutomationContext context)
        {
            if (!TryGetRumorRuntime(context, out RumorRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-rumor-distortion", "Transmit with deterministic distortion", "RumorRuntime", "MissingRuntime", failure);
            }

            RumorOperationResult created = CreateRootRumor(context, runtime, "distortion-root", PrototypeRumorDefinitionFactory.SecretLeakRumorId, "person.prototype.friend", RumorAuthenticity.PartiallyAccurate, RumorDisclosure.Shareable);
            RumorOperationResult transmitted = runtime.Transmit(new RumorTransmissionRequest
            {
                TransactionId = Tx(context, "distort"),
                TransmissionId = RumorScoped(context, "transmission-distorted"),
                RumorVersionId = created.Rumor?.RumorId,
                SpeakerPersonId = "person.prototype.friend",
                ListenerPersonId = context.ScenarioContext.Runtimes.PersonId,
                ChannelId = PrototypeRumorDefinitionFactory.TavernGossipChannelId,
                RequestedOutcome = RumorTransmissionOutcome.PartiallyBelieved,
                RequestedDistortionPolicy = RumorDistortionPolicy.ForcedConfidenceDecrease,
                DerivedRumorId = RumorScoped(context, "derived-distorted"),
                DeterministicSeed = "seed.12.4.distortion",
                SpeakerConfidence = 720,
                WorldTime = 30d
            });

            bool valid = created.Succeeded
                && transmitted.Succeeded
                && transmitted.Rumor != null
                && transmitted.Rumor.RumorId != created.Rumor.RumorId
                && transmitted.Rumor.RootRumorId == created.Rumor.RootRumorId
                && transmitted.Rumor.ParentRumorId == created.Rumor.RumorId
                && transmitted.Rumor.Confidence == created.Rumor.Confidence - 100
                && transmitted.Rumor.DistortionOperations.Contains(RumorDistortionOperation.ConfidenceDecreased)
                && runtime.QueryByRoot(created.Rumor.RootRumorId).Count == 2;
            return TestLabAssertions.True("step12-rumor-distortion", "Distortion creates a derived version with root lineage", valid, $"Create={created.Status} Transmit={transmitted.Status} Versions={runtime.QueryByRoot(created.Rumor?.RootRumorId).Count} Confidence={created.Rumor?.Confidence}->{transmitted.Rumor?.Confidence}");
        }

        private static TestLabAutomationStepResult RumorBoundedPropagation(TestLabAutomationContext context)
        {
            if (!TryGetRumorRuntime(context, out RumorRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-rumor-propagation", "Propagate rumor to ordered listeners", "RumorRuntime", "MissingRuntime", failure);
            }

            RumorOperationResult created = CreateRootRumor(context, runtime, "propagation-root", PrototypeRumorDefinitionFactory.PublicNewsRumorId, "person.prototype.friend", RumorAuthenticity.Verified);
            string[] listeners = { "person.prototype.rival", context.ScenarioContext.Runtimes.PersonId, "person.prototype.mentor" };
            RumorPropagationResult propagated = runtime.Propagate(new RumorPropagationRequest
            {
                TransactionId = Tx(context, "propagate"),
                RumorVersionId = created.Rumor?.RumorId,
                SpeakerPersonId = "person.prototype.friend",
                ListenerPersonIds = listeners,
                ChannelId = PrototypeRumorDefinitionFactory.PublicSpeechChannelId,
                MaximumTransmissions = 3,
                DeterministicSeed = "seed.12.4.propagation",
                WorldTime = 40d
            });
            RumorPropagationMetrics metrics = runtime.GetMetrics(created.Rumor?.RootRumorId);
            string[] expectedListeners = listeners
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .Take(3)
                .ToArray();

            bool valid = created.Succeeded
                && propagated.Succeeded
                && propagated.Transmissions.Count == 3
                && propagated.Transmissions.All(result => result.Succeeded)
                && metrics.Transmissions == 3
                && metrics.AwarePeople == 4
                && runtime.QueryTransmissionsByRoot(created.Rumor.RootRumorId).Select(item => item.ListenerPersonId).SequenceEqual(expectedListeners);
            return TestLabAssertions.True("step12-rumor-propagation", "Propagation is bounded and deterministic", valid, $"Create={created.Status} Propagate={propagated.Succeeded} Transmissions={metrics.Transmissions} Aware={metrics.AwarePeople}");
        }

        private static TestLabAutomationStepResult RumorSocialBoundary(TestLabAutomationContext context)
        {
            if (!TryGetRumorRuntime(context, out RumorRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-rumor-social-boundary", "Verify rumor separation from other social runtimes", "RumorRuntime", "MissingRuntime", failure);
            }

            int relationshipsBefore = context.ScenarioContext.Runtimes.Relationships.Count;
            int attitudesBefore = context.ScenarioContext.Runtimes.Attitudes.Count;
            int reputationBefore = context.ScenarioContext.Runtimes.Reputation.Count;
            RumorOperationResult created = CreateRootRumor(context, runtime, "boundary-root", PrototypeRumorDefinitionFactory.ReputationRumorId, "person.prototype.friend", RumorAuthenticity.Disputed);
            RumorOperationResult transmitted = runtime.Transmit(new RumorTransmissionRequest
            {
                TransactionId = Tx(context, "boundary-transmit"),
                TransmissionId = RumorScoped(context, "transmission-boundary"),
                RumorVersionId = created.Rumor?.RumorId,
                SpeakerPersonId = "person.prototype.friend",
                ListenerPersonId = context.ScenarioContext.Runtimes.PersonId,
                ChannelId = PrototypeRumorDefinitionFactory.ConversationChannelId,
                RequestedOutcome = RumorTransmissionOutcome.Uncertain,
                WorldTime = 50d
            });

            bool valid = created.Succeeded
                && transmitted.Succeeded
                && context.ScenarioContext.Runtimes.Relationships.Count == relationshipsBefore
                && context.ScenarioContext.Runtimes.Attitudes.Count == attitudesBefore
                && context.ScenarioContext.Runtimes.Reputation.Count == reputationBefore;
            return TestLabAssertions.True("step12-rumor-social-boundary", "Rumors do not mutate relationships, attitudes, or reputation directly", valid, $"Rumors={runtime.RumorCount}/{runtime.TransmissionCount} Relationships={relationshipsBefore}->{context.ScenarioContext.Runtimes.Relationships.Count} Attitudes={attitudesBefore}->{context.ScenarioContext.Runtimes.Attitudes.Count} Reputation={reputationBefore}->{context.ScenarioContext.Runtimes.Reputation.Count}");
        }

        private static TestLabAutomationStepResult RumorPersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetRumorRuntime(context, out RumorRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-rumor-persistence", "Save, restore, and reject invalid rumor payloads", "RumorRuntime", "MissingRuntime", failure);
            }

            RumorOperationResult created = CreateRootRumor(context, runtime, "persist-root", PrototypeRumorDefinitionFactory.FabricatedAccusationRumorId, "person.prototype.friend", RumorAuthenticity.Fabricated);
            RumorRuntimeSaveData save = runtime.CreateSaveData();
            RumorRuntime restored = new RumorRuntime();
            restored.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds);
            RumorOperationResult restore = restored.RestoreFromSaveData(save, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            RumorRuntimeSaveData corrupt = save.Clone();
            corrupt.rumors[0].definitionId = "rumor.missing";
            bool rejected = !RumorRuntime.ValidateSaveData(corrupt, registry, context.ScenarioContext.Runtimes.KnownPersonIds, out string validationFailure);

            bool valid = created.Succeeded
                && restore.Succeeded
                && restored.RumorCount == runtime.RumorCount
                && restored.TryGetRumor(created.Rumor.RumorId, out RumorSnapshot restoredRumor)
                && restoredRumor.Authenticity == RumorAuthenticity.Fabricated
                && rejected
                && runtime.RumorCount == 1;
            return TestLabAssertions.True("step12-rumor-persistence", "Rumors persist and reject corrupt restores", valid, $"Create={created.Status} Restore={restore.Status} Rejected={rejected} Failure='{validationFailure}' Counts={runtime.RumorCount}/{restored.RumorCount}");
        }

        private static bool TryGetRumorRuntime(TestLabAutomationContext context, out RumorRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Rumors;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Rumor runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            runtime.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds, requestedPersonId => string.Equals(requestedPersonId, context.ScenarioContext.Runtimes.PersonId, StringComparison.Ordinal) ? context.ScenarioContext.Runtimes.Knowledge : null, requestedPersonId => string.Equals(requestedPersonId, context.ScenarioContext.Runtimes.PersonId, StringComparison.Ordinal) ? context.ScenarioContext.Runtimes.Memory : null);
            failure = string.Empty;
            return true;
        }

        private static RumorOperationResult CreateRootRumor(TestLabAutomationContext context, RumorRuntime runtime, string suffix, string definitionId, string originatorPersonId, RumorAuthenticity authenticity, RumorDisclosure? disclosure = null)
        {
            return runtime.CreateRumor(new RumorCreateRequest
            {
                TransactionId = Tx(context, suffix),
                RumorId = RumorScoped(context, suffix),
                DefinitionId = definitionId,
                Claim = BuildRumorClaim(context, suffix),
                OriginatorPersonId = originatorPersonId,
                OriginCategory = RumorOriginCategory.FirsthandObservation,
                OriginatingEventId = RumorScoped(context, $"source-{suffix}"),
                SourceAttributionPersonId = originatorPersonId,
                SourceNamed = true,
                Confidence = authenticity == RumorAuthenticity.Fabricated ? 380 : 720,
                Salience = 620,
                Memorability = 610,
                DisclosureOverride = disclosure,
                Authenticity = authenticity,
                WorldTime = 10d,
                Tags = new[] { "feature.12.4", suffix }
            });
        }

        private static KnowledgePropositionData BuildRumorClaim(TestLabAutomationContext context, string suffix)
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                subjectType = KnowledgeSubjectType.Event,
                subjectId = RumorScoped(context, $"claim-{suffix}"),
                valueType = KnowledgeValueType.Boolean,
                booleanValue = true,
                sourceContextId = RumorScoped(context, $"source-context-{suffix}")
            };
        }

        private static TestLabAutomationStepResult InteractionReadinessAndPreview(TestLabAutomationContext context)
        {
            if (!TryGetInteractionRuntime(context, out SocialInteractionRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-interaction-preview", "Preview interaction without mutation", "SocialInteractionRuntime", "MissingRuntime", failure);
            }

            bool definitions = registry.TryGet(PrototypeSocialInteractionDefinitionFactory.GreetId, out SocialInteractionDefinition _)
                && registry.TryGet(PrototypeSocialInteractionDefinitionFactory.ComplimentId, out SocialInteractionDefinition _)
                && registry.TryGet(PrototypeSocialInteractionDefinitionFactory.PromiseId, out SocialInteractionDefinition _);
            long before = runtime.Revision;
            SocialInteractionResult preview = runtime.Preview(InteractionRequest(context, PrototypeSocialInteractionDefinitionFactory.GreetId, "preview", worldTime: 10d));
            bool valid = runtime.IsReady
                && definitions
                && preview.Succeeded
                && preview.Preview
                && preview.Record != null
                && runtime.Revision == before
                && runtime.Count == 0;
            return TestLabAssertions.True("step12-interaction-preview", "Preview interaction without mutation", valid, $"Ready={runtime.IsReady} Definitions={definitions} Preview={preview.Status} Revision={before}->{runtime.Revision} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult InteractionAttitudeConsequences(TestLabAutomationContext context)
        {
            if (!TryGetInteractionRuntime(context, out SocialInteractionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-interaction-attitudes", "Execute attitude-producing interactions", "SocialInteractionRuntime", "MissingRuntime", failure);
            }

            string initiator = context.ScenarioContext.Runtimes.PersonId;
            string target = "person.prototype.friend";
            SocialInteractionResult compliment = runtime.Execute(InteractionRequest(context, PrototypeSocialInteractionDefinitionFactory.ComplimentId, "compliment", target, worldTime: 20d));
            SocialInteractionResult duplicate = runtime.Execute(InteractionRequest(context, PrototypeSocialInteractionDefinitionFactory.ComplimentId, "compliment", target, worldTime: 20d));
            SocialInteractionResult insult = runtime.Execute(InteractionRequest(context, PrototypeSocialInteractionDefinitionFactory.InsultId, "insult", target, worldTime: 30d));
            AttitudeEffectiveValueSnapshot affection = context.ScenarioContext.Runtimes.Attitudes.ResolveValue(target, initiator, PrototypeAttitudeDefinitionFactory.AffectionId);
            AttitudeEffectiveValueSnapshot hostility = context.ScenarioContext.Runtimes.Attitudes.ResolveValue(target, initiator, PrototypeAttitudeDefinitionFactory.HostilityId);
            bool valid = compliment.Succeeded
                && duplicate.Duplicate
                && insult.Succeeded
                && affection.EffectiveValue == -2
                && hostility.EffectiveValue > 0
                && runtime.QueryByPerson(target).Count >= 2;
            return TestLabAssertions.True("step12-interaction-attitudes", "Execute attitude-producing interactions", valid, $"Compliment={compliment.Status} Duplicate={duplicate.Status} Insult={insult.Status} Affection={affection.EffectiveValue} Hostility={hostility.EffectiveValue}");
        }

        private static TestLabAutomationStepResult InteractionPendingResponsePromise(TestLabAutomationContext context)
        {
            if (!TryGetInteractionRuntime(context, out SocialInteractionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-interaction-pending", "Create pending interaction and accept promise", "SocialInteractionRuntime", "MissingRuntime", failure);
            }

            string target = "person.prototype.friend";
            SocialInteractionResult pending = runtime.Execute(InteractionRequest(context, PrototypeSocialInteractionDefinitionFactory.PromiseId, "promise-pending", target, worldTime: 40d));
            SocialInteractionResult preview = runtime.RespondToPending(Tx(context, "promise-accept-preview"), pending.Pending?.PendingInteractionId, SocialInteractionResponse.Accept, 41d, preview: true);
            SocialInteractionResult accepted = runtime.RespondToPending(Tx(context, "promise-accept"), pending.Pending?.PendingInteractionId, SocialInteractionResponse.Accept, 42d);
            bool hasPromise = !string.IsNullOrWhiteSpace(accepted.Promise?.PromiseId) && runtime.TryGetPromise(accepted.Promise.PromiseId, out SocialPromiseSnapshot promise) && promise.Status == SocialPromiseStatus.Active;
            bool valid = pending.Succeeded
                && pending.Status == SocialInteractionStatus.Pending
                && pending.Pending != null
                && preview.Succeeded
                && preview.Preview
                && accepted.Succeeded
                && accepted.Record.Outcome == SocialInteractionOutcome.Accepted
                && hasPromise;
            return TestLabAssertions.True("step12-interaction-pending", "Create pending interaction and accept promise", valid, $"Pending={pending.Status} Preview={preview.Status} Accepted={accepted.Status} Promise={accepted.Promise?.PromiseId}");
        }

        private static TestLabAutomationStepResult InteractionPublicReputation(TestLabAutomationContext context)
        {
            if (!TryGetInteractionRuntime(context, out SocialInteractionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-interaction-reputation", "Execute public reputation consequences", "SocialInteractionRuntime", "MissingRuntime", failure);
            }

            string target = "person.prototype.friend";
            SocialInteractionResult praise = runtime.Execute(InteractionRequest(context, PrototypeSocialInteractionDefinitionFactory.PublicPraiseId, "public-praise", target, worldTime: 50d, visibility: SocialInteractionVisibility.Public));
            SocialInteractionResult threat = runtime.Execute(InteractionRequest(context, PrototypeSocialInteractionDefinitionFactory.ThreatenId, "threaten", target, worldTime: 60d, witnesses: new[] { "person.prototype.rival" }, visibility: SocialInteractionVisibility.Witnessed));
            ReputationEffectiveValueSnapshot targetEsteem = context.ScenarioContext.Runtimes.Reputation.ResolveValue(target, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.EsteemId);
            ReputationEffectiveValueSnapshot initiatorDanger = context.ScenarioContext.Runtimes.Reputation.ResolveValue(context.ScenarioContext.Runtimes.PersonId, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.PerceivedDangerId);
            bool valid = praise.Succeeded
                && threat.Succeeded
                && targetEsteem.EffectiveValue > 0
                && initiatorDanger.EffectiveValue > 0;
            return TestLabAssertions.True("step12-interaction-reputation", "Execute public reputation consequences", valid, $"Praise={praise.Status} Threat={threat.Status} Esteem={targetEsteem.EffectiveValue} Danger={initiatorDanger.EffectiveValue}");
        }

        private static TestLabAutomationStepResult InteractionRumorDelegation(TestLabAutomationContext context)
        {
            if (!TryGetInteractionRuntime(context, out SocialInteractionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-interaction-rumor", "Share existing rumor through interaction", "SocialInteractionRuntime", "MissingRuntime", failure);
            }

            RumorRuntime rumorsRuntime = context.ScenarioContext.Runtimes.Rumors;
            RumorOperationResult created = CreateRootRumor(context, rumorsRuntime, "interaction-share", PrototypeRumorDefinitionFactory.PublicNewsRumorId, context.ScenarioContext.Runtimes.PersonId, RumorAuthenticity.Verified);
            SocialInteractionRequest request = InteractionRequest(context, PrototypeSocialInteractionDefinitionFactory.ShareInformationId, "share-rumor", "person.prototype.friend", worldTime: 70d);
            request.Subject = new SocialInteractionSubjectData { kind = SocialInteractionSubjectKind.Rumor, subjectId = created.Rumor?.RumorId };
            SocialInteractionResult shared = runtime.Execute(request);
            bool valid = created.Succeeded
                && shared.Succeeded
                && !string.IsNullOrWhiteSpace(shared.Record?.Data.rumorTransmissionId)
                && rumorsRuntime.TransmissionCount > 0;
            return TestLabAssertions.True("step12-interaction-rumor", "Share existing rumor through interaction", valid, $"Create={created.Status} Shared={shared.Status} Transmission={shared.Record?.Data.rumorTransmissionId} Count={rumorsRuntime.TransmissionCount}");
        }

        private static TestLabAutomationStepResult InteractionPersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetInteractionRuntime(context, out SocialInteractionRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-interaction-persistence", "Save, restore, duplicate, and reject invalid payloads", "SocialInteractionRuntime", "MissingRuntime", failure);
            }

            SocialInteractionResult execute = runtime.Execute(InteractionRequest(context, PrototypeSocialInteractionDefinitionFactory.ThankId, "persist-thank", "person.prototype.friend", worldTime: 80d));
            SocialInteractionRuntimeSaveData save = runtime.CreateSaveData();
            SocialInteractionRuntime restored = new SocialInteractionRuntime();
            SocialInteractionResult restore = restored.RestoreFromSaveData(save, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            SocialInteractionRuntimeSaveData corrupt = save.Clone();
            if (corrupt.records.Count > 0)
            {
                corrupt.records[0].interactionDefinitionId = "social-interaction.missing";
            }

            bool rejected = !SocialInteractionRuntime.ValidateSaveData(corrupt, registry, context.ScenarioContext.Runtimes.KnownPersonIds, out string validationFailure);
            bool valid = execute.Succeeded
                && restore.Succeeded
                && restored.Count == runtime.Count
                && rejected
                && runtime.Count == save.records.Count;
            return TestLabAssertions.True("step12-interaction-persistence", "Save, restore, duplicate, and reject invalid payloads", valid, $"Execute={execute.Status} Restore={restore.Status} Rejected={rejected} Failure='{validationFailure}' Counts={runtime.Count}/{restored.Count}");
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                    PrototypeRelationshipDefinitionFactory.ParentChildRelationshipId,
                    PrototypeRelationshipDefinitionFactory.MentorStudentRelationshipId,
                    PrototypeRelationshipDefinitionFactory.RivalRelationshipId
                });
        }

        private static ITestLabAutomationScenario AttitudeScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeAttitudeDefinitionFactory.TrustId,
                    PrototypeAttitudeDefinitionFactory.AffectionId,
                    PrototypeAttitudeDefinitionFactory.RespectId,
                    PrototypeAttitudeDefinitionFactory.FearId,
                    PrototypeAttitudeDefinitionFactory.LoyaltyId,
                    PrototypeAttitudeDefinitionFactory.HostilityId,
                    PrototypeRelationshipDefinitionFactory.FriendRelationshipId
                });
        }

        private static ITestLabAutomationScenario ReputationScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                    PrototypeReputationDefinitionFactory.PrototypeTownAudienceId,
                    PrototypeReputationDefinitionFactory.AdventurersGuildAudienceId,
                    PrototypeReputationDefinitionFactory.AdventurersGuildVeteransAudienceId,
                    PrototypeReputationDefinitionFactory.RoyalJurisdictionAudienceId,
                    PrototypeReputationDefinitionFactory.HiddenInvestigatorsAudienceId,
                    PrototypeReputationDefinitionFactory.RenownId,
                    PrototypeReputationDefinitionFactory.EsteemId,
                    PrototypeReputationDefinitionFactory.NotorietyId,
                    PrototypeReputationDefinitionFactory.CredibilityId,
                    PrototypeReputationDefinitionFactory.PerceivedDangerId,
                    PrototypeReputationDefinitionFactory.HonorId
                });
        }

        private static ITestLabAutomationScenario RumorScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeRumorDefinitionFactory.PersonalConductRumorId,
                    PrototypeRumorDefinitionFactory.PublicNewsRumorId,
                    PrototypeRumorDefinitionFactory.FabricatedAccusationRumorId,
                    PrototypeRumorDefinitionFactory.SecretLeakRumorId,
                    PrototypeRumorDefinitionFactory.ReputationRumorId,
                    PrototypeRumorDefinitionFactory.ConversationChannelId,
                    PrototypeRumorDefinitionFactory.TavernGossipChannelId,
                    PrototypeRumorDefinitionFactory.PublicSpeechChannelId
                });
        }

        private static ITestLabAutomationScenario InteractionScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeSocialInteractionDefinitionFactory.GreetId,
                    PrototypeSocialInteractionDefinitionFactory.ComplimentId,
                    PrototypeSocialInteractionDefinitionFactory.InsultId,
                    PrototypeSocialInteractionDefinitionFactory.PromiseId,
                    PrototypeSocialInteractionDefinitionFactory.PublicPraiseId,
                    PrototypeSocialInteractionDefinitionFactory.ThreatenId,
                    PrototypeSocialInteractionDefinitionFactory.ShareInformationId,
                    PrototypeAttitudeDefinitionFactory.AffectionId,
                    PrototypeAttitudeDefinitionFactory.HostilityId,
                    PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                    PrototypeReputationDefinitionFactory.EsteemId,
                    PrototypeReputationDefinitionFactory.PerceivedDangerId,
                    PrototypeRumorDefinitionFactory.PublicNewsRumorId,
                    PrototypeRumorDefinitionFactory.ConversationChannelId
                });
        }

        private static TestLabAutomationStepResult NormReadinessAndPreview(TestLabAutomationContext context)
        {
            if (!TryGetNormRuntime(context, out SocialNormRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-norm-preview", "Preview host greeting norm", "SocialNormRuntime", "MissingRuntime", failure);
            }

            long before = runtime.Revision;
            SocialNormEvaluationResult preview = runtime.Preview(NormRequest(
                context,
                "host-greeting-preview",
                PrototypeSocialInteractionDefinitionFactory.GreetId,
                requestedNormIds: new[] { PrototypeSocialNormDefinitionFactory.HostGreetingNormId },
                tags: new[] { "host-context" },
                placeId: "place.prototype.court"));
            bool definitions = registry.TryGet(PrototypeSocialNormDefinitionFactory.HostGreetingNormId, out SocialNormDefinition _)
                && registry.TryGet(PrototypeSocialNormDefinitionFactory.PublicInsultNormId, out SocialNormDefinition _)
                && registry.TryGet(PrototypeSocialNormDefinitionFactory.PromiseKeepingNormId, out SocialNormDefinition _);
            bool valid = definitions
                && preview.Succeeded
                && preview.Preview
                && preview.Assessments.Count > 0
                && preview.Assessments.Any(item => item.Classification == SocialNormAssessmentClassification.Satisfied)
                && runtime.Revision == before
                && runtime.Count == 0;
            return TestLabAssertions.True("step12-norm-preview", "Norm definitions resolve and previews do not mutate", valid, $"Definitions={definitions} Preview={preview.Status} Revision={before}->{runtime.Revision} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult NormVisibilityConsequences(TestLabAutomationContext context)
        {
            if (!TryGetNormRuntime(context, out SocialNormRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-norm-visibility", "Assess private and public insult norms", "SocialNormRuntime", "MissingRuntime", failure);
            }

            SocialNormEvaluationResult privateInsult = runtime.Execute(NormRequest(
                context,
                "private-insult",
                PrototypeSocialInteractionDefinitionFactory.InsultId,
                requestedNormIds: new[] { PrototypeSocialNormDefinitionFactory.PrivateInsultNormId },
                visibility: SocialInteractionVisibility.Private));
            SocialNormEvaluationResult publicInsult = runtime.Execute(NormRequest(
                context,
                "public-insult",
                PrototypeSocialInteractionDefinitionFactory.InsultId,
                requestedNormIds: new[] { PrototypeSocialNormDefinitionFactory.PublicInsultNormId },
                witnesses: new[] { "person.prototype.rival" },
                visibility: SocialInteractionVisibility.Public,
                classification: SocialNormAssessmentClassification.Violation));
            SocialNormAssessmentSnapshot privateSnapshot = privateInsult.Assessments.FirstOrDefault();
            SocialNormAssessmentSnapshot publicSnapshot = publicInsult.Assessments.FirstOrDefault();
            bool publicHasRequiredConsequence = publicSnapshot != null && publicSnapshot.Consequences.Any(item => item.policy == SocialNormConsequencePolicy.Required && item.committed);
            bool valid = privateInsult.Succeeded
                && publicInsult.Succeeded
                && privateSnapshot != null
                && publicSnapshot != null
                && privateSnapshot.Severity < publicSnapshot.Severity
                && publicHasRequiredConsequence
                && runtime.QueryByObserver("person.prototype.rival").Count > 0;
            return TestLabAssertions.True("step12-norm-visibility", "Public and private etiquette produce deterministic consequence plans", valid, $"Private={privateInsult.Status}/{privateSnapshot?.Severity} Public={publicInsult.Status}/{publicSnapshot?.Severity} PublicRequired={publicHasRequiredConsequence}");
        }

        private static TestLabAutomationStepResult NormKnowledgeExceptionObservers(TestLabAutomationContext context)
        {
            if (!TryGetNormRuntime(context, out SocialNormRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-norm-knowledge-exception", "Evaluate ignorance and witness context", "SocialNormRuntime", "MissingRuntime", failure);
            }

            SocialNormEvaluationResult ignorance = runtime.Execute(NormRequest(
                context,
                "ignorance",
                PrototypeSocialInteractionDefinitionFactory.CustomActionId,
                requestedNormIds: new[] { PrototypeSocialNormDefinitionFactory.IgnoranceMitigatedEtiquetteNormId },
                witnesses: new[] { "person.prototype.friend" },
                tags: new[] { "culture.prototype.formal", "actor-unaware" },
                actorKnowledge: SocialNormActorKnowledgeState.Unknown,
                classification: SocialNormAssessmentClassification.Violation));
            SocialNormEvaluationResult emergency = runtime.Execute(NormRequest(
                context,
                "emergency",
                PrototypeSocialInteractionDefinitionFactory.ShareInformationId,
                requestedNormIds: new[] { PrototypeSocialNormDefinitionFactory.EmergencyDisclosureNormId },
                witnesses: new[] { "person.prototype.friend" },
                tags: new[] { "secret-subject", "emergency" },
                visibility: SocialInteractionVisibility.Public,
                classification: SocialNormAssessmentClassification.SeriousViolation));
            SocialNormAssessmentSnapshot ignoranceSnapshot = ignorance.Assessments.FirstOrDefault();
            SocialNormAssessmentSnapshot emergencySnapshot = emergency.Assessments.FirstOrDefault();
            bool exceptionApplied = ignoranceSnapshot != null && ignoranceSnapshot.Data.exceptions.Any(item => item.applied && item.effect == SocialNormExceptionEffect.ReduceSeverity);
            bool observerRecorded = ignoranceSnapshot != null && ignoranceSnapshot.Observers.Any(item => string.Equals(item.observerPersonId, "person.prototype.friend", StringComparison.Ordinal));
            bool emergencyExcused = emergencySnapshot != null
                && emergencySnapshot.Classification == SocialNormAssessmentClassification.Excused
                && emergencySnapshot.Data.exceptions.Any(item => item.applied && item.effect == SocialNormExceptionEffect.ExcuseViolation);
            bool valid = ignorance.Succeeded
                && emergency.Succeeded
                && exceptionApplied
                && observerRecorded
                && emergencyExcused;
            return TestLabAssertions.True("step12-norm-knowledge-exception", "Actor knowledge, exceptions, and observer interpretation remain explicit", valid, $"Ignorance={ignoranceSnapshot?.Classification} Exception={exceptionApplied} Observer={observerRecorded} Emergency={emergencySnapshot?.Classification}");
        }

        private static TestLabAutomationStepResult NormConflictAndPromise(TestLabAutomationContext context)
        {
            if (!TryGetNormRuntime(context, out SocialNormRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-norm-conflict-promise", "Assess conflict and promise breach", "SocialNormRuntime", "MissingRuntime", failure);
            }

            SocialNormEvaluationResult conflict = runtime.Execute(NormRequest(
                context,
                "conflict",
                PrototypeSocialInteractionDefinitionFactory.PublicPraiseId,
                requestedNormIds: new[] { PrototypeSocialNormDefinitionFactory.PraiseEnemyConflictNormId, PrototypeSocialNormDefinitionFactory.HospitalityOverrideNormId },
                witnesses: new[] { "person.prototype.friend" },
                tags: new[] { "audience.enemy-of-target", "hospitality-duty", "actor-role.host", "target-role.rival" },
                visibility: SocialInteractionVisibility.Public,
                classification: SocialNormAssessmentClassification.Satisfied));
            SocialNormEvaluationResult promise = runtime.Execute(NormRequest(
                context,
                "promise-breach",
                PrototypeSocialInteractionDefinitionFactory.PromiseId,
                requestedNormIds: new[] { PrototypeSocialNormDefinitionFactory.PromiseKeepingNormId },
                tags: new[] { "promise-context" },
                promiseId: NormScoped(context, "promise"),
                promiseState: SocialPromiseStatus.Breached.ToString(),
                classification: SocialNormAssessmentClassification.Violation));
            bool conflictSuppressed = conflict.Assessments.Any(item => item.Applicability == SocialNormApplicabilityStatus.SuppressedByConflict)
                && conflict.Assessments.Any(item => item.Conflicts.Count > 0);
            SocialNormAssessmentSnapshot promiseSnapshot = promise.Assessments.FirstOrDefault();
            bool promiseValid = promiseSnapshot != null
                && promiseSnapshot.PromiseId.Length > 0
                && promiseSnapshot.Classification == SocialNormAssessmentClassification.Violation
                && promiseSnapshot.Consequences.Any(item => item.targetRuntime == SocialNormConsequenceTargetRuntime.InterpersonalAttitude);
            bool valid = conflict.Succeeded && promise.Succeeded && conflictSuppressed && promiseValid;
            return TestLabAssertions.True("step12-norm-conflict-promise", "Norm conflicts and promise expectations resolve deterministically", valid, $"Conflict={conflict.Status} Suppressed={conflictSuppressed} Promise={promise.Status}/{promiseSnapshot?.Classification}");
        }

        private static TestLabAutomationStepResult NormPersistenceIdempotence(TestLabAutomationContext context)
        {
            if (!TryGetNormRuntime(context, out SocialNormRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-norm-persistence", "Save, restore, duplicate, and reject invalid norm payloads", "SocialNormRuntime", "MissingRuntime", failure);
            }

            SocialNormEvaluationRequest request = NormRequest(
                context,
                "persist",
                PrototypeSocialInteractionDefinitionFactory.ThankId,
                requestedNormIds: new[] { PrototypeSocialNormDefinitionFactory.HostGreetingNormId },
                classification: SocialNormAssessmentClassification.Satisfied);
            SocialNormEvaluationResult execute = runtime.Execute(request);
            SocialNormEvaluationResult duplicate = runtime.Execute(request);
            SocialNormRuntimeSaveData save = runtime.CreateSaveData();
            SocialNormRuntime restored = new SocialNormRuntime();
            restored.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.Relationships, context.ScenarioContext.Runtimes.Attitudes, context.ScenarioContext.Runtimes.Reputation, context.ScenarioContext.Runtimes.Rumors, context.ScenarioContext.Runtimes.SocialInteractions);
            SocialNormEvaluationResult restore = restored.RestoreFromSaveData(save, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            SocialNormRuntimeSaveData corrupt = save.Clone();
            corrupt.assessments[0].normDefinitionId = "social-norm.prototype.missing";
            SocialNormPersistenceParticipant participant = new SocialNormPersistenceParticipant(runtime, () => registry, () => context.ScenarioContext.Runtimes.KnownPersonIds.ToArray());
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialNormPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = execute.Succeeded
                && duplicate.Duplicate
                && restore.Succeeded
                && restored.Count == runtime.Count
                && rejected != null
                && !rejected.Succeeded;
            return TestLabAssertions.True("step12-norm-persistence", "Norm assessments persist and duplicate transactions are idempotent", valid, $"Execute={execute.Status} Duplicate={duplicate.Status}/{duplicate.Duplicate} Restore={restore.Status} Reject={rejected?.Succeeded}");
        }

        private static ITestLabAutomationScenario NormScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeSocialNormDefinitionFactory.HostGreetingNormId,
                    PrototypeSocialNormDefinitionFactory.PublicInsultNormId,
                    PrototypeSocialNormDefinitionFactory.PrivateInsultNormId,
                    PrototypeSocialNormDefinitionFactory.IgnoranceMitigatedEtiquetteNormId,
                    PrototypeSocialNormDefinitionFactory.WitnessRespectNormId,
                    PrototypeSocialNormDefinitionFactory.EmergencyDisclosureNormId,
                    PrototypeSocialNormDefinitionFactory.PromiseKeepingNormId,
                    PrototypeSocialNormDefinitionFactory.PraiseEnemyConflictNormId,
                    PrototypeSocialNormDefinitionFactory.HospitalityOverrideNormId,
                    PrototypeSocialInteractionDefinitionFactory.GreetId,
                    PrototypeSocialInteractionDefinitionFactory.InsultId,
                    PrototypeSocialInteractionDefinitionFactory.PromiseId,
                    PrototypeAttitudeDefinitionFactory.RespectId,
                    PrototypeReputationDefinitionFactory.EsteemId
                });
        }

        private static TestLabAutomationStepResult NetworkReadinessAndPreview(TestLabAutomationContext context)
        {
            if (!TryGetNetworkRuntime(context, out SocialNetworkRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-network-readiness", "Resolve graph and group definitions", "SocialNetworkRuntime", "MissingRuntime", failure);
            }

            long before = runtime.Revision;
            SocialNetworkMutationResult preview = runtime.Mutate(NetworkGroupRequest(context, "readiness-preview", NetworkGroupId(context, "preview"), preview: true));
            bool definitions = registry.TryGet(PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, out SocialGraphProjectionDefinition projection)
                && registry.TryGet(PrototypeSocialNetworkDefinitionFactory.AdventuringPartyGroupId, out InformalSocialGroupDefinition group)
                && projection.IncludedEdgeKinds.Contains(SocialGraphEdgeKind.SharedGroupMembership)
                && group.RequiresLeader;
            bool valid = definitions
                && preview.Status == SocialNetworkOperationStatus.Preview
                && runtime.Revision == before
                && runtime.GroupCount == 0;
            return TestLabAssertions.True("step12-network-readiness", "Network definitions resolve and previews do not mutate", valid, $"Definitions={definitions} Preview={preview.Status} Revision={before}->{runtime.Revision} Groups={runtime.GroupCount}");
        }

        private static TestLabAutomationStepResult NetworkProjectionSemantics(TestLabAutomationContext context)
        {
            if (!TryGetNetworkRuntime(context, out SocialNetworkRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-network-projection", "Build graph from source runtimes", "SocialNetworkRuntime", "MissingRuntime", failure);
            }

            string seedFailure = SeedNetworkFixture(context, "projection");
            if (!string.IsNullOrEmpty(seedFailure))
            {
                return TestLabAssertions.Fail("step12-network-projection", "Build graph from source runtimes", "SeedNetworkFixture", "Succeeded", "Failed", seedFailure);
            }

            SocialGraphSnapshot graph = runtime.BuildGraph(NetworkQuery(PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, 100d));
            bool hasRelationship = graph.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.ObjectiveRelationship);
            bool hasDirectedAttitude = graph.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.DirectedAttitude && edge.SourcePersonId == context.ScenarioContext.Runtimes.PersonId);
            bool hasInteraction = graph.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.RecentInteraction);
            bool hasRumor = graph.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.RumorTransmission);
            bool hasGroup = graph.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.SharedGroupMembership);
            bool requestFilter = runtime.BuildGraph(new SocialGraphQueryRequest
            {
                ProjectionDefinitionId = PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId,
                WorldTime = 100d,
                EdgeKinds = new[] { SocialGraphEdgeKind.SharedGroupMembership },
                MinimumWeight = 1
            }).Edges.All(edge => edge.EdgeKind == SocialGraphEdgeKind.SharedGroupMembership);
            bool valid = hasRelationship && hasDirectedAttitude && hasInteraction && hasRumor && hasGroup && requestFilter;
            return TestLabAssertions.True("step12-network-projection", "Projected edges preserve source semantics and direction", valid, $"Edges={graph.Edges.Count} Relationship={hasRelationship} Attitude={hasDirectedAttitude} Interaction={hasInteraction} Rumor={hasRumor} Group={hasGroup} Filter={requestFilter}");
        }

        private static TestLabAutomationStepResult NetworkQueriesAndAnalysis(TestLabAutomationContext context)
        {
            if (!TryGetNetworkRuntime(context, out SocialNetworkRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-network-analysis", "Run bounded graph analysis", "SocialNetworkRuntime", "MissingRuntime", failure);
            }

            string seedFailure = SeedNetworkFixture(context, "analysis");
            if (!string.IsNullOrEmpty(seedFailure))
            {
                return TestLabAssertions.Fail("step12-network-analysis", "Run bounded graph analysis", "SeedNetworkFixture", "Succeeded", "Failed", seedFailure);
            }

            long before = runtime.Revision;
            SocialGraphQueryRequest composite = NetworkQuery(PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, 100d);
            var neighbors = runtime.QueryNeighbors(context.ScenarioContext.Runtimes.PersonId, composite);
            var mutual = runtime.QueryMutualConnections(context.ScenarioContext.Runtimes.PersonId, "person.prototype.student", composite);
            SocialGraphPathResult path = runtime.FindShortestPath(context.ScenarioContext.Runtimes.PersonId, "person.prototype.student", composite);
            SocialGraphMetricsResult metrics = runtime.CalculatePersonMetrics(context.ScenarioContext.Runtimes.PersonId, composite);
            var components = runtime.FindConnectedComponents(composite);
            var cliques = runtime.FindCliqueCandidates(NetworkQuery(PrototypeSocialNetworkDefinitionFactory.MutualTrustProjectionId, 100d));
            var communities = runtime.FindCommunityCandidates(composite);
            bool valid = neighbors.Count > 0
                && mutual.Any(item => item.MutualPersonId == "person.prototype.friend")
                && path.Connected
                && path.PersonPath.Length <= composite.MaxDepth + 1
                && metrics.Degree > 0
                && components.Count > 0
                && cliques.Count > 0
                && communities.Count > 0
                && runtime.Revision == before;
            return TestLabAssertions.True("step12-network-analysis", "Neighbors, paths, metrics, cliques, and communities are deterministic", valid, $"Neighbors={neighbors.Count} Mutual={mutual.Count} Path={path.Connected}/{path.Distance} Metrics={metrics.Degree} Components={components.Count} Cliques={cliques.Count} Communities={communities.Count} Revision={before}->{runtime.Revision}");
        }

        private static TestLabAutomationStepResult NetworkGroupLifecycle(TestLabAutomationContext context)
        {
            if (!TryGetNetworkRuntime(context, out SocialNetworkRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-network-group", "Create memberships, roles, and group metrics", "SocialNetworkRuntime", "MissingRuntime", failure);
            }

            string groupId = NetworkGroupId(context, "lifecycle");
            SocialNetworkMutationResult create = runtime.Mutate(NetworkGroupRequest(context, "group-create", groupId));
            SocialNetworkMutationResult duplicate = runtime.Mutate(NetworkGroupRequest(context, "group-create", groupId));
            SocialNetworkMutationResult leader = runtime.Mutate(NetworkMembershipRequest(context, "group-leader", groupId, context.ScenarioContext.Runtimes.PersonId, PrototypeSocialNetworkDefinitionFactory.LeaderRoleId));
            SocialNetworkMutationResult companion = runtime.Mutate(NetworkMembershipRequest(context, "group-companion", groupId, "person.prototype.friend", PrototypeSocialNetworkDefinitionFactory.CompanionRoleId));
            SocialNetworkMutationResult invalidLeader = runtime.Mutate(NetworkMembershipRequest(context, "group-second-leader", groupId, "person.prototype.student", PrototypeSocialNetworkDefinitionFactory.LeaderRoleId));
            SocialNetworkMutationResult ended = runtime.Mutate(new SocialGroupMutationRequest
            {
                TransactionId = Tx(context, "group-companion-end"),
                MutationKind = SocialGroupMutationKind.EndMembership,
                MembershipId = NetworkMembershipId(groupId, "person.prototype.friend"),
                WorldTime = 7d
            });
            SocialGroupMetricsResult metrics = runtime.CalculateGroupMetrics(groupId, NetworkQuery(PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, 100d));
            bool valid = create.Succeeded
                && duplicate.Duplicate
                && leader.Succeeded
                && companion.Succeeded
                && !invalidLeader.Succeeded
                && invalidLeader.Status == SocialNetworkOperationStatus.InvalidRole
                && ended.Succeeded
                && metrics.ActiveMemberCount == 1
                && metrics.HistoricalMemberCount == 2
                && !metrics.MutatedGroup;
            return TestLabAssertions.True("step12-network-group", "Informal group lifecycle and idempotence are explicit", valid, $"Create={create.Status} Duplicate={duplicate.Status}/{duplicate.Duplicate} Leader={leader.Status} Companion={companion.Status} InvalidLeader={invalidLeader.Status} End={ended.Status} Active={metrics.ActiveMemberCount} Historical={metrics.HistoricalMemberCount}");
        }

        private static TestLabAutomationStepResult NetworkPersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetNetworkRuntime(context, out SocialNetworkRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-network-persistence", "Save, restore, and reject invalid network payloads", "SocialNetworkRuntime", "MissingRuntime", failure);
            }

            string seedFailure = SeedNetworkFixture(context, "persistence");
            if (!string.IsNullOrEmpty(seedFailure))
            {
                return TestLabAssertions.Fail("step12-network-persistence", "Save, restore, and reject invalid network payloads", "SeedNetworkFixture", "Succeeded", "Failed", seedFailure);
            }

            SocialNetworkPersistenceParticipant participant = new SocialNetworkPersistenceParticipant(runtime, () => registry, () => context.ScenarioContext.Runtimes.KnownPersonIds.ToArray());
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            SocialNetworkRuntimeSaveData saveData = JsonUtility.FromJson<SocialNetworkRuntimeSaveData>(save.PayloadJson);
            SocialNetworkRuntime restored = new SocialNetworkRuntime();
            restored.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.Relationships, context.ScenarioContext.Runtimes.Attitudes, context.ScenarioContext.Runtimes.Reputation, context.ScenarioContext.Runtimes.Rumors, context.ScenarioContext.Runtimes.SocialInteractions, context.ScenarioContext.Runtimes.SocialNorms);
            SocialNetworkMutationResult restore = restored.RestoreFromSaveData(saveData, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            SocialNetworkRuntimeSaveData corrupt = saveData.Clone();
            if (corrupt.memberships.Count > 0)
            {
                corrupt.memberships[0].personId = "person.prototype.missing";
            }
            int beforeGroups = runtime.GroupCount;
            int beforeMemberships = runtime.MembershipCount;
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialNetworkPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = save.Succeeded
                && restore.Succeeded
                && restored.GroupCount == runtime.GroupCount
                && restored.MembershipCount == runtime.MembershipCount
                && rejected != null
                && !rejected.Succeeded
                && runtime.GroupCount == beforeGroups
                && runtime.MembershipCount == beforeMemberships;
            return TestLabAssertions.True("step12-network-persistence", "Network persistence preserves groups and rejects corrupt restores", valid, $"Save={save.Succeeded} Restore={restore.Status} Reject={rejected?.Succeeded} Groups={beforeGroups}->{runtime.GroupCount} Memberships={beforeMemberships}->{runtime.MembershipCount}");
        }

        private static ITestLabAutomationScenario NetworkScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId,
                    PrototypeSocialNetworkDefinitionFactory.MutualTrustProjectionId,
                    PrototypeSocialNetworkDefinitionFactory.RelationshipProjectionId,
                    PrototypeSocialNetworkDefinitionFactory.AdventuringPartyGroupId,
                    PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                    PrototypeRelationshipDefinitionFactory.MentorStudentRelationshipId,
                    PrototypeAttitudeDefinitionFactory.TrustId,
                    PrototypeAttitudeDefinitionFactory.HostilityId,
                    PrototypeSocialInteractionDefinitionFactory.GreetId,
                    PrototypeRumorDefinitionFactory.PublicNewsRumorId,
                    PrototypeRumorDefinitionFactory.ConversationChannelId
                });
        }

        private static bool TryGetNetworkRuntime(TestLabAutomationContext context, out SocialNetworkRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.SocialNetworks;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Social Network runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static string SeedNetworkFixture(TestLabAutomationContext context, string suffix)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            if (runtimes == null)
            {
                return "Runtime bundle is missing.";
            }

            string owner = runtimes.PersonId;
            RelationshipOperationResult friend = runtimes.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = NetworkRecordId(context, suffix, "relationship-player-friend"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = owner,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                transactionId = Tx(context, $"{suffix}-relationship-player-friend"),
                startWorldTime = 1d
            });
            if (!friend.Succeeded && !friend.Duplicate) return friend.Message;

            RelationshipOperationResult student = runtimes.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = NetworkRecordId(context, suffix, "relationship-friend-student"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.MentorStudentRelationshipId,
                firstPersonId = "person.prototype.friend",
                firstRoleId = "mentor",
                secondPersonId = "person.prototype.student",
                secondRoleId = "student",
                transactionId = Tx(context, $"{suffix}-relationship-friend-student"),
                startWorldTime = 2d
            });
            if (!student.Succeeded && !student.Duplicate) return student.Message;

            string attitudeFailure = MutateNetworkAttitude(runtimes.Attitudes, Tx(context, $"{suffix}-attitude-owner-friend"), owner, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId, 70)
                ?? MutateNetworkAttitude(runtimes.Attitudes, Tx(context, $"{suffix}-attitude-friend-owner"), "person.prototype.friend", owner, PrototypeAttitudeDefinitionFactory.TrustId, 65)
                ?? MutateNetworkAttitude(runtimes.Attitudes, Tx(context, $"{suffix}-attitude-friend-student"), "person.prototype.friend", "person.prototype.student", PrototypeAttitudeDefinitionFactory.TrustId, 62)
                ?? MutateNetworkAttitude(runtimes.Attitudes, Tx(context, $"{suffix}-attitude-student-friend"), "person.prototype.student", "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId, 58)
                ?? MutateNetworkAttitude(runtimes.Attitudes, Tx(context, $"{suffix}-attitude-owner-student"), owner, "person.prototype.student", PrototypeAttitudeDefinitionFactory.TrustId, 55)
                ?? MutateNetworkAttitude(runtimes.Attitudes, Tx(context, $"{suffix}-attitude-student-owner"), "person.prototype.student", owner, PrototypeAttitudeDefinitionFactory.TrustId, 52)
                ?? MutateNetworkAttitude(runtimes.Attitudes, Tx(context, $"{suffix}-attitude-owner-rival"), owner, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.HostilityId, -40);
            if (!string.IsNullOrEmpty(attitudeFailure)) return attitudeFailure;

            SocialInteractionResult interaction = runtimes.SocialInteractions.Execute(new SocialInteractionRequest
            {
                TransactionId = Tx(context, $"{suffix}-interaction"),
                InteractionRecordId = NetworkRecordId(context, suffix, "interaction"),
                InteractionDefinitionId = PrototypeSocialInteractionDefinitionFactory.GreetId,
                InitiatorPersonId = owner,
                TargetPersonId = "person.prototype.friend",
                PlaceId = "place.prototype.test-lab",
                Subject = new SocialInteractionSubjectData { kind = SocialInteractionSubjectKind.Person, subjectId = "person.prototype.friend", ownerPersonId = "person.prototype.friend" },
                Channel = SocialInteractionCommunicationChannel.Conversation,
                WorldTime = 30d,
                DeterministicSeed = context.RunId
            });
            if (!interaction.Succeeded && !interaction.Duplicate) return interaction.Message;

            string rumorId = NetworkRecordId(context, suffix, "rumor");
            RumorOperationResult rumor = runtimes.Rumors.CreateRumor(new RumorCreateRequest
            {
                TransactionId = Tx(context, $"{suffix}-rumor"),
                RumorId = rumorId,
                DefinitionId = PrototypeRumorDefinitionFactory.PublicNewsRumorId,
                Claim = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                    subjectType = KnowledgeSubjectType.Event,
                    subjectId = NetworkRecordId(context, suffix, "rumor-event"),
                    valueType = KnowledgeValueType.Boolean,
                    booleanValue = true
                },
                OriginatorPersonId = owner,
                OriginCategory = RumorOriginCategory.FirsthandObservation,
                Confidence = 700,
                Salience = 600,
                Memorability = 600,
                WorldTime = 32d
            });
            if (!rumor.Succeeded && !rumor.Duplicate) return rumor.Message;

            RumorOperationResult transmission = runtimes.Rumors.Transmit(new RumorTransmissionRequest
            {
                TransactionId = Tx(context, $"{suffix}-rumor-transmission"),
                TransmissionId = NetworkRecordId(context, suffix, "rumor-transmission"),
                RumorVersionId = rumorId,
                SpeakerPersonId = owner,
                ListenerPersonId = "person.prototype.friend",
                ChannelId = PrototypeRumorDefinitionFactory.ConversationChannelId,
                WorldTime = 34d,
                DeterministicSeed = context.RunId
            });
            if (!transmission.Succeeded && !transmission.Duplicate) return transmission.Message;

            string groupId = NetworkGroupId(context, suffix);
            SocialNetworkMutationResult group = runtimes.SocialNetworks.Mutate(NetworkGroupRequest(context, $"{suffix}-group", groupId));
            if (!group.Succeeded && !group.Duplicate) return group.Message;
            foreach ((string person, string role) in new[]
            {
                (owner, PrototypeSocialNetworkDefinitionFactory.LeaderRoleId),
                ("person.prototype.friend", PrototypeSocialNetworkDefinitionFactory.CompanionRoleId),
                ("person.prototype.student", PrototypeSocialNetworkDefinitionFactory.CompanionRoleId)
            })
            {
                SocialNetworkMutationResult membership = runtimes.SocialNetworks.Mutate(NetworkMembershipRequest(context, $"{suffix}-member-{person}", groupId, person, role));
                if (!membership.Succeeded && !membership.Duplicate) return membership.Message;
            }

            return string.Empty;
        }

        private static string MutateNetworkAttitude(InterpersonalAttitudeRuntime runtime, string transactionId, string observer, string subject, string dimension, int value)
        {
            AttitudeMutationResult result = runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = transactionId,
                observerPersonId = observer,
                subjectPersonId = subject,
                dimensionId = dimension,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = value,
                worldTime = 4d
            });
            return result.Succeeded || result.Duplicate ? null : result.Message;
        }

        private static SocialGroupMutationRequest NetworkGroupRequest(TestLabAutomationContext context, string suffix, string groupId, bool preview = false)
        {
            return new SocialGroupMutationRequest
            {
                TransactionId = Tx(context, suffix),
                MutationKind = SocialGroupMutationKind.CreateGroup,
                GroupId = groupId,
                GroupDefinitionId = PrototypeSocialNetworkDefinitionFactory.AdventuringPartyGroupId,
                DisplayName = "Test Lab Adventuring Party",
                SourceProjectionDefinitionId = PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId,
                WorldTime = 1d,
                Preview = preview,
                Tags = new[] { "test-lab", "social-network" }
            };
        }

        private static SocialGroupMutationRequest NetworkMembershipRequest(TestLabAutomationContext context, string suffix, string groupId, string personId, string roleId)
        {
            return new SocialGroupMutationRequest
            {
                TransactionId = Tx(context, suffix),
                MutationKind = SocialGroupMutationKind.AddMembership,
                GroupId = groupId,
                MembershipId = NetworkMembershipId(groupId, personId),
                PersonId = personId,
                RoleId = roleId,
                WorldTime = 2d,
                Tags = new[] { "test-lab" }
            };
        }

        private static SocialGraphQueryRequest NetworkQuery(string projectionId, double worldTime)
        {
            return new SocialGraphQueryRequest
            {
                ProjectionDefinitionId = projectionId,
                WorldTime = worldTime,
                MaxDepth = 4,
                MaxVisitedNodes = 16,
                MinimumWeight = 1,
                Visibility = SocialGraphVisibility.Authoritative
            };
        }

        private static string NetworkGroupId(TestLabAutomationContext context, string suffix)
        {
            return $"social-group.automation.{context.RunId}.{context.CurrentScenarioId}.{suffix}";
        }

        private static string NetworkMembershipId(string groupId, string personId)
        {
            return $"membership.{groupId}.{personId}";
        }

        private static string NetworkRecordId(TestLabAutomationContext context, string suffix, string kind)
        {
            return $"social-network.automation.{context.RunId}.{context.CurrentScenarioId}.{suffix}.{kind}";
        }

        private static TestLabAutomationStepResult DecisionReadiness(TestLabAutomationContext context)
        {
            if (!TryGetDecisionRuntime(context, out SocialDecisionRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-decision-readiness", "Resolve social decision definitions", "SocialDecisionRuntime", "MissingRuntime", failure);
            }

            long before = runtime.Revision;
            SocialDecisionProfileDefinition profile = null;
            SocialIntentionDefinition intention = null;
            SocialConsiderationDefinition consideration = null;
            bool definitions = registry.TryGet(PrototypeSocialDecisionDefinitionFactory.SociableProfileId, out profile)
                && registry.TryGet(PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId, out intention)
                && registry.TryGet(PrototypeSocialDecisionDefinitionFactory.ConsiderTrustId, out consideration)
                && registry.TryGet(PrototypeSocialInteractionDefinitionFactory.GreetId, out SocialInteractionDefinition _);
            SocialDecisionResult evaluate = runtime.Evaluate(DecisionRequest(context, PrototypeSocialDecisionDefinitionFactory.ScriptControlledProfileId, explicitIntentionId: PrototypeSocialDecisionDefinitionFactory.IntroduceSelfId, target: "person.prototype.friend", commit: false));
            bool valid = definitions
                && profile.MaximumCandidates > 0
                && intention.EligibleInteractionDefinitionIds.Contains(PrototypeSocialInteractionDefinitionFactory.GreetId)
                && consideration.Input == SocialDecisionConsiderationInput.TrustTowardTarget
                && evaluate.Succeeded
                && runtime.Revision == before
                && runtime.Count == 0;
            return TestLabAssertions.True("step12-decision-readiness", "Resolve social decision definitions", valid, $"Definitions={definitions} Eval={evaluate.Status} Rev={before}->{runtime.Revision} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult DecisionDeterministicSelection(TestLabAutomationContext context)
        {
            if (!TryGetDecisionRuntime(context, out SocialDecisionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-decision-selection", "Evaluate deterministic social decision candidates", "SocialDecisionRuntime", "MissingRuntime", failure);
            }

            string seedFailure = SeedDecisionInputs(context, "deterministic");
            if (!string.IsNullOrWhiteSpace(seedFailure))
            {
                return TestLabAssertions.Fail("step12-decision-selection", "Evaluate deterministic social decision candidates", "SeededInputs", "FixtureFailed", seedFailure);
            }

            SocialDecisionRequest request = DecisionRequest(context, PrototypeSocialDecisionDefinitionFactory.SociableProfileId, target: "person.prototype.friend", commit: false);
            SocialDecisionResult first = runtime.Evaluate(request);
            SocialDecisionResult second = runtime.Evaluate(request);
            string firstKey = first.SelectedCandidate?.candidateKey ?? string.Empty;
            string secondKey = second.SelectedCandidate?.candidateKey ?? string.Empty;
            bool valid = first.Succeeded
                && second.Succeeded
                && string.Equals(firstKey, secondKey, StringComparison.Ordinal)
                && string.Equals(first.SelectedCandidate?.interactionDefinitionId, second.SelectedCandidate?.interactionDefinitionId, StringComparison.Ordinal)
                && first.Candidates.Count == second.Candidates.Count
                && runtime.Count == 0;
            return TestLabAssertions.True("step12-decision-selection", "Evaluate deterministic social decision candidates", valid, $"First={first.Status}:{firstKey} Second={second.Status}:{secondKey} Candidates={first.Candidates.Count}/{second.Candidates.Count} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult DecisionNoActionBoundary(TestLabAutomationContext context)
        {
            if (!TryGetDecisionRuntime(context, out SocialDecisionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-decision-no-action", "Evaluate no-action boundary", "SocialDecisionRuntime", "MissingRuntime", failure);
            }

            SocialDecisionResult result = runtime.Evaluate(DecisionRequest(context, PrototypeSocialDecisionDefinitionFactory.SociableProfileId, target: string.Empty, commit: false));
            bool valid = result.Succeeded
                && result.Status == SocialDecisionStatus.NoAction
                && result.Targets.Count == 0
                && result.Candidates.Count == 0
                && runtime.Count == 0;
            return TestLabAssertions.True("step12-decision-no-action", "Evaluate no-action boundary", valid, $"Status={result.Status} Targets={result.Targets.Count} Candidates={result.Candidates.Count} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult DecisionSubmitThroughInteractions(TestLabAutomationContext context)
        {
            if (!TryGetDecisionRuntime(context, out SocialDecisionRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-decision-submit", "Submit selected action through interaction runtime", "SocialDecisionRuntime", "MissingRuntime", failure);
            }

            string seedFailure = SeedDecisionInputs(context, "submit");
            if (!string.IsNullOrWhiteSpace(seedFailure))
            {
                return TestLabAssertions.Fail("step12-decision-submit", "Submit selected action through interaction runtime", "SeededInputs", "FixtureFailed", seedFailure);
            }

            int beforeInteractions = context.ScenarioContext.Runtimes.SocialInteractions.Count;
            SocialDecisionRequest request = DecisionRequest(context, PrototypeSocialDecisionDefinitionFactory.ScriptControlledProfileId, explicitIntentionId: PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId, target: "person.prototype.friend", commit: true);
            request.ExecutionMode = SocialDecisionExecutionMode.SubmitForExecution;
            SocialDecisionResult result = runtime.Evaluate(request);
            SocialDecisionResult duplicate = runtime.Evaluate(request);
            bool valid = result.Succeeded
                && result.Status == SocialDecisionStatus.Submitted
                && result.ExecutionResult != null
                && result.ExecutionResult.Succeeded
                && context.ScenarioContext.Runtimes.SocialInteractions.Count == beforeInteractions + 1
                && (duplicate.Status == SocialDecisionStatus.EvaluationCooldown || duplicate.Status == SocialDecisionStatus.NoAction);
            return TestLabAssertions.True("step12-decision-submit", "Submit selected action through interaction runtime", valid, $"Submit={result.Status} Exec={result.ExecutionResult?.Status} Duplicate={duplicate.Status} Interactions={beforeInteractions}->{context.ScenarioContext.Runtimes.SocialInteractions.Count}");
        }

        private static TestLabAutomationStepResult DecisionPersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetDecisionRuntime(context, out SocialDecisionRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-decision-persistence", "Save, restore, and reject invalid decision payloads", "SocialDecisionRuntime", "MissingRuntime", failure);
            }

            string seedFailure = SeedDecisionInputs(context, "persist");
            if (!string.IsNullOrWhiteSpace(seedFailure))
            {
                return TestLabAssertions.Fail("step12-decision-persistence", "Save, restore, and reject invalid decision payloads", "SeededInputs", "FixtureFailed", seedFailure);
            }

            SocialDecisionResult selected = runtime.Evaluate(DecisionRequest(context, PrototypeSocialDecisionDefinitionFactory.SociableProfileId, target: "person.prototype.friend", commit: true));
            SocialDecisionPersistenceParticipant participant = new SocialDecisionPersistenceParticipant(runtime, () => registry, () => context.ScenarioContext.Runtimes.KnownPersonIds.ToArray());
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            SocialDecisionRuntimeSaveData saveData = JsonUtility.FromJson<SocialDecisionRuntimeSaveData>(save.PayloadJson);
            SocialDecisionRuntime restored = new SocialDecisionRuntime();
            restored.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.SocialInteractions, context.ScenarioContext.Runtimes.Relationships, context.ScenarioContext.Runtimes.Attitudes, context.ScenarioContext.Runtimes.Reputation, context.ScenarioContext.Runtimes.Rumors, context.ScenarioContext.Runtimes.SocialNorms, context.ScenarioContext.Runtimes.SocialNetworks);
            SocialDecisionResult restore = restored.RestoreFromSaveData(saveData, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            SocialDecisionRuntimeSaveData corrupt = saveData.Clone();
            corrupt.personStates[0].activeTargetPersonId = "person.prototype.unknown";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialDecisionPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = selected.Succeeded
                && save.Succeeded
                && restore.Succeeded
                && restored.Count == runtime.Count
                && !rejected.Succeeded
                && runtime.Count == saveData.personStates.Count;
            return TestLabAssertions.True("step12-decision-persistence", "Save, restore, and reject invalid decision payloads", valid, $"Selected={selected.Status} Save={save.Succeeded} Restore={restore.Status} Rejected={!rejected.Succeeded} Count={runtime.Count}/{restored.Count}");
        }

        private static TestLabAutomationStepResult InfluenceReadinessAndPreview(TestLabAutomationContext context)
        {
            if (!TryGetInfluenceRuntime(context, out SocialInfluenceRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-influence-readiness", "Resolve influence definitions and preview", "SocialInfluenceRuntime", "MissingRuntime", failure);
            }

            long beforeRevision = runtime.Revision;
            int beforeKnowledge = context.ScenarioContext.Runtimes.Knowledge.CreateSaveData().beliefs.Count();
            SocialInfluenceMethodDefinition evidence = null;
            SocialInfluenceMethodDefinition lie = null;
            bool definitions = registry.TryGet(PrototypeSocialInfluenceDefinitionFactory.PresentEvidenceId, out evidence)
                && registry.TryGet(PrototypeSocialInfluenceDefinitionFactory.TellDirectLieId, out lie)
                && registry.TryGet(BuiltInKnowledgeFacts.EventOccurred, out KnowledgeFactDefinition _);
            SocialInfluenceResult preview = runtime.Preview(InfluenceRequest(
                context,
                "readiness-preview",
                PrototypeSocialInfluenceDefinitionFactory.PresentEvidenceId,
                SocialInfluenceIntent.ChangeBelief,
                claim: InfluenceClaim(context, "readiness-preview")));
            bool valid = definitions
                && evidence.SupportedIntents.Contains(SocialInfluenceIntent.ChangeBelief)
                && lie.DeceptionAllowed
                && preview.Succeeded
                && preview.Preview
                && runtime.Revision == beforeRevision
                && runtime.Count == 0
                && context.ScenarioContext.Runtimes.Knowledge.CreateSaveData().beliefs.Count() == beforeKnowledge;
            return TestLabAssertions.True("step12-influence-readiness", "Influence definitions resolve and previews are non-mutating", valid, $"Definitions={definitions} Preview={preview.Status} Count={runtime.Count} Rev={beforeRevision}->{runtime.Revision}");
        }

        private static TestLabAutomationStepResult InfluenceBeliefAndComplianceBoundaries(TestLabAutomationContext context)
        {
            if (!TryGetInfluenceRuntime(context, out SocialInfluenceRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-influence-belief-compliance", "Execute belief evidence and accepted promise", "SocialInfluenceRuntime", "MissingRuntime", failure);
            }

            KnowledgePropositionData claim = InfluenceClaim(context, "belief");
            SocialInfluenceResult belief = runtime.Execute(InfluenceRequest(context, "belief", PrototypeSocialInfluenceDefinitionFactory.PresentEvidenceId, SocialInfluenceIntent.ChangeBelief, claim: claim, speaker: "person.prototype.friend", speakerResolve: 900, targetResistance: 80, evidenceStrength: 500));
            bool knowledgeRecorded = context.ScenarioContext.Runtimes.Knowledge.TryGetBelief(claim, out KnowledgeBeliefRecord recordedBelief);
            int beforeInteractions = context.ScenarioContext.Runtimes.SocialInteractions.Count;
            SocialInfluenceResult promise = runtime.Execute(InfluenceRequest(context, "promise", PrototypeSocialInfluenceDefinitionFactory.PersuadeRequestId, SocialInfluenceIntent.GainPromise, claim: null, speaker: "person.prototype.friend", speakerResolve: 920, targetResistance: 60, evidenceStrength: 0, subjectKind: SocialInfluenceSubjectKind.Promise, interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.PromiseId, worldTime: 30d));
            bool valid = belief.Succeeded
                && belief.KnowledgeResult != null
                && belief.KnowledgeResult.Succeeded
                && knowledgeRecorded
                && recordedBelief.Confidence > 0
                && promise.Succeeded
                && promise.Attempt.complianceOutcome == SocialInfluenceComplianceOutcome.PromiseAccepted
                && promise.InteractionResult != null
                && promise.InteractionResult.Succeeded
                && context.ScenarioContext.Runtimes.SocialInteractions.Count == beforeInteractions + 1;
            return TestLabAssertions.True("step12-influence-belief-compliance", "Belief influence and compliance remain separate", valid, $"Belief={belief.Status}/{belief.KnowledgeResult?.Code} Knowledge={knowledgeRecorded} Promise={promise.Attempt?.complianceOutcome} Interactions={beforeInteractions}->{context.ScenarioContext.Runtimes.SocialInteractions.Count}");
        }

        private static TestLabAutomationStepResult InfluenceDeceptionDetection(TestLabAutomationContext context)
        {
            if (!TryGetInfluenceRuntime(context, out SocialInfluenceRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-influence-deception", "Detect a deliberate lie deterministically", "SocialInfluenceRuntime", "MissingRuntime", failure);
            }

            int trustBefore = context.ScenarioContext.Runtimes.Attitudes.ResolveValue(context.ScenarioContext.Runtimes.PersonId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue;
            int hostilityBefore = context.ScenarioContext.Runtimes.Attitudes.ResolveValue(context.ScenarioContext.Runtimes.PersonId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.HostilityId).EffectiveValue;
            SocialInfluenceResult lie = runtime.Execute(InfluenceRequest(
                context,
                "lie",
                PrototypeSocialInfluenceDefinitionFactory.TellDirectLieId,
                SocialInfluenceIntent.ChangeBelief,
                claim: InfluenceClaim(context, "lie"),
                speaker: "person.prototype.rival",
                speakerResolve: 0,
                targetResistance: 900,
                difficulty: 300,
                truthStatus: SocialInfluenceTruthStatus.False,
                speakerBelief: SocialInfluenceSpeakerBeliefState.BelievesFalse,
                deception: SocialInfluenceDeceptionMode.DirectFalseAssertion,
                worldTime: 40d));
            int trustAfter = context.ScenarioContext.Runtimes.Attitudes.ResolveValue(context.ScenarioContext.Runtimes.PersonId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue;
            int hostilityAfter = context.ScenarioContext.Runtimes.Attitudes.ResolveValue(context.ScenarioContext.Runtimes.PersonId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.HostilityId).EffectiveValue;
            bool detected = lie.Attempt != null && (lie.Attempt.detectionOutcome == SocialInfluenceDetectionOutcome.Detected || lie.Attempt.detectionOutcome == SocialInfluenceDetectionOutcome.Proven);
            bool valid = lie.Succeeded
                && lie.Attempt.honesty == SocialInfluenceHonestyClassification.DirectLie
                && detected
                && trustAfter < trustBefore
                && hostilityAfter > hostilityBefore;
            return TestLabAssertions.True("step12-influence-deception", "Detected deception records trust and hostility consequences", valid, $"Lie={lie.Status} Detection={lie.Attempt?.detectionOutcome} Trust={trustBefore}->{trustAfter} Hostility={hostilityBefore}->{hostilityAfter}");
        }

        private static TestLabAutomationStepResult InfluenceDecisionModifiers(TestLabAutomationContext context)
        {
            if (!TryGetInfluenceRuntime(context, out SocialInfluenceRuntime runtime, out _, out string failure))
            {
                return TestLabAssertions.Fail("step12-influence-decision-modifier", "Apply influence modifier to decision candidate", "SocialInfluenceRuntime", "MissingRuntime", failure);
            }

            SocialDecisionRequest beforeRequest = DecisionRequest(context, PrototypeSocialDecisionDefinitionFactory.ScriptControlledProfileId, explicitIntentionId: PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId, target: "person.prototype.friend", commit: false);
            SocialDecisionResult before = context.ScenarioContext.Runtimes.SocialDecisions.Evaluate(beforeRequest);
            SocialInfluenceResult influence = runtime.Execute(InfluenceRequest(
                context,
                "modifier",
                PrototypeSocialInfluenceDefinitionFactory.InspireId,
                SocialInfluenceIntent.EncourageAction,
                claim: null,
                speaker: "person.prototype.friend",
                speakerResolve: 900,
                targetResistance: 50,
                subjectKind: SocialInfluenceSubjectKind.Decision,
                ownerPersonId: "person.prototype.friend",
                intentionDefinitionId: PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId,
                interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.GreetId,
                worldTime: 80d));
            SocialDecisionRequest afterRequest = DecisionRequest(context, PrototypeSocialDecisionDefinitionFactory.ScriptControlledProfileId, explicitIntentionId: PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId, target: "person.prototype.friend", commit: false);
            afterRequest.WorldTime = 80d;
            SocialDecisionResult after = context.ScenarioContext.Runtimes.SocialDecisions.Evaluate(afterRequest);
            int beforeModifier = before.SelectedCandidate?.externalModifier ?? 0;
            int afterModifier = after.SelectedCandidate?.externalModifier ?? 0;
            bool valid = before.Succeeded
                && before.SelectedCandidate != null
                && influence.Succeeded
                && influence.DecisionModifier != null
                && after.Succeeded
                && after.SelectedCandidate != null
                && beforeModifier == 0
                && afterModifier > 0
                && after.SelectedCandidate.finalScore > before.SelectedCandidate.finalScore;
            return TestLabAssertions.True("step12-influence-decision-modifier", "Influence modifiers affect decision scoring without owning decisions", valid, $"Before={before.Status}:{beforeModifier}/{before.SelectedCandidate?.finalScore} Influence={influence.Status}:{influence.DecisionModifier?.scoreDelta} After={after.Status}:{afterModifier}/{after.SelectedCandidate?.finalScore}");
        }

        private static TestLabAutomationStepResult InfluencePersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetInfluenceRuntime(context, out SocialInfluenceRuntime runtime, out DefinitionRegistry registry, out string failure))
            {
                return TestLabAssertions.Fail("step12-influence-persistence", "Save, restore, and reject invalid influence payloads", "SocialInfluenceRuntime", "MissingRuntime", failure);
            }

            SocialInfluenceResult execute = runtime.Execute(InfluenceRequest(context, "persist", PrototypeSocialInfluenceDefinitionFactory.ReassureId, SocialInfluenceIntent.Reassure, claim: null, speaker: "person.prototype.friend", speakerResolve: 800, targetResistance: 120, subjectKind: SocialInfluenceSubjectKind.Person, worldTime: 90d));
            SocialInfluencePersistenceParticipant participant = new SocialInfluencePersistenceParticipant(runtime, () => registry, () => context.ScenarioContext.Runtimes.KnownPersonIds.ToArray());
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            SocialInfluenceRuntimeSaveData saveData = JsonUtility.FromJson<SocialInfluenceRuntimeSaveData>(save.PayloadJson);
            SocialInfluenceRuntime restored = new SocialInfluenceRuntime();
            restored.Configure(registry, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.Attitudes, context.ScenarioContext.Runtimes.Reputation, context.ScenarioContext.Runtimes.SocialInteractions, new[] { context.ScenarioContext.Runtimes.Knowledge });
            SocialInfluenceResult restore = restored.RestoreFromSaveData(saveData, registry, context.ScenarioContext.Runtimes.KnownPersonIds, restoringState: true);
            SocialInfluenceRuntimeSaveData corrupt = saveData.Clone();
            corrupt.attempts[0].targetPersonId = "person.prototype.unknown";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialInfluencePersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = execute.Succeeded
                && save.Succeeded
                && restore.Succeeded
                && restored.Count == runtime.Count
                && !rejected.Succeeded
                && runtime.Count == saveData.attempts.Count;
            return TestLabAssertions.True("step12-influence-persistence", "Influence attempts persist and reject corrupt restores", valid, $"Execute={execute.Status} Save={save.Succeeded} Restore={restore.Status} Rejected={!rejected.Succeeded} Count={runtime.Count}/{restored.Count}");
        }

        private static ITestLabAutomationScenario InfluenceScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeSocialInfluenceDefinitionFactory.PresentEvidenceId,
                    PrototypeSocialInfluenceDefinitionFactory.PersuadeRequestId,
                    PrototypeSocialInfluenceDefinitionFactory.TellDirectLieId,
                    PrototypeSocialInfluenceDefinitionFactory.InspireId,
                    PrototypeSocialInfluenceDefinitionFactory.ReassureId,
                    PrototypeSocialDecisionDefinitionFactory.ScriptControlledProfileId,
                    PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId,
                    PrototypeSocialInteractionDefinitionFactory.GreetId,
                    PrototypeSocialInteractionDefinitionFactory.PromiseId,
                    BuiltInKnowledgeFacts.EventOccurred
                });
        }

        private static ITestLabAutomationScenario EmotionScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeSocialEmotionDefinitionFactory.JoyId,
                    PrototypeSocialEmotionDefinitionFactory.SadnessId,
                    PrototypeSocialEmotionDefinitionFactory.AngerId,
                    PrototypeSocialEmotionDefinitionFactory.FearId,
                    PrototypeSocialEmotionDefinitionFactory.ReliefId,
                    PrototypeSocialEmotionDefinitionFactory.GratitudeId,
                    PrototypeSocialEmotionDefinitionFactory.GuiltId,
                    PrototypeSocialEmotionDefinitionFactory.ShameId,
                    PrototypeSocialEmotionDefinitionFactory.PrideId,
                    PrototypeSocialEmotionDefinitionFactory.AnxietyId,
                    PrototypeSocialEmotionDefinitionFactory.DisgustId,
                    PrototypeSocialEmotionDefinitionFactory.EnvyId,
                    PrototypeSocialEmotionDefinitionFactory.ResentmentId,
                    PrototypeSocialEmotionDefinitionFactory.HopeId,
                    PrototypeSocialEmotionDefinitionFactory.DisappointmentId,
                    PrototypeSocialEmotionDefinitionFactory.MoodValenceId,
                    PrototypeSocialEmotionDefinitionFactory.MoodAnxietyId,
                    PrototypeSocialEmotionDefinitionFactory.DetectedDeceptionRuleId,
                    PrototypeSocialInfluenceDefinitionFactory.TellDirectLieId,
                    PrototypeSocialDecisionDefinitionFactory.ScriptControlledProfileId
                });
        }

        private static ITestLabAutomationScenario FamilyScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social,
                requiredDefinitionIds: new[]
                {
                    PrototypeRelationshipDefinitionFactory.BiologicalParentChildRelationshipId,
                    PrototypeRelationshipDefinitionFactory.AdoptiveParentChildRelationshipId,
                    PrototypeRelationshipDefinitionFactory.LegalGuardianDependentRelationshipId,
                    PrototypeRelationshipDefinitionFactory.FosterGuardianDependentRelationshipId,
                    PrototypeRelationshipDefinitionFactory.SpouseRelationshipId,
                    PrototypeRelationshipDefinitionFactory.DomesticPartnerRelationshipId,
                    PrototypeRelationshipDefinitionFactory.CourtshipPartnerRelationshipId,
                    PrototypeRelationshipDefinitionFactory.EngagedPartnerRelationshipId,
                    PrototypeRelationshipDefinitionFactory.FormerRomanticPartnerRelationshipId,
                    PrototypeAttitudeDefinitionFactory.RomanticAttractionId,
                    PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId,
                    PrototypeFamilyRelationshipDefinitionFactory.FamilyHouseholdDefinitionId,
                    PrototypeFamilyRelationshipDefinitionFactory.SharedResidenceHouseholdDefinitionId
                });
        }

        private static bool TryGetInfluenceRuntime(TestLabAutomationContext context, out SocialInfluenceRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.SocialInfluence;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Social Influence runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetEmotionRuntime(TestLabAutomationContext context, out SocialEmotionRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.SocialEmotions;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Social Emotion runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetFamilyRuntime(TestLabAutomationContext context, out FamilyRelationshipRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.FamilyRelationships;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Family Relationship runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static SocialInfluenceRequest InfluenceRequest(
            TestLabAutomationContext context,
            string suffix,
            string methodId,
            SocialInfluenceIntent intent,
            KnowledgePropositionData claim = null,
            string speaker = "person.prototype.friend",
            int speakerResolve = 750,
            int targetResistance = 150,
            int evidenceStrength = 350,
            int difficulty = 0,
            SocialInfluenceSubjectKind subjectKind = SocialInfluenceSubjectKind.Claim,
            string ownerPersonId = "",
            string intentionDefinitionId = "",
            string interactionDefinitionId = "",
            SocialInfluenceTruthStatus truthStatus = SocialInfluenceTruthStatus.True,
            SocialInfluenceSpeakerBeliefState speakerBelief = SocialInfluenceSpeakerBeliefState.BelievesTrue,
            SocialInfluenceDeceptionMode deception = SocialInfluenceDeceptionMode.NoDeception,
            double worldTime = 20d)
        {
            string target = context.ScenarioContext.Runtimes.PersonId;
            return new SocialInfluenceRequest
            {
                TransactionId = Tx(context, $"influence-{suffix}"),
                AttemptId = InfluenceScoped(context, suffix),
                MethodDefinitionId = methodId,
                SpeakerPersonId = speaker,
                TargetPersonId = target,
                WitnessPersonIds = new[] { "person.prototype.friend" },
                Intent = intent,
                Subject = new SocialInfluenceSubjectData
                {
                    kind = subjectKind,
                    subjectId = claim == null ? $"social-influence.subject.{context.RunId}.{context.CurrentScenarioId}.{suffix}" : KnowledgeProposition.BuildIdentity(claim),
                    ownerPersonId = string.IsNullOrWhiteSpace(ownerPersonId) ? speaker : ownerPersonId,
                    tags = new[] { "test-lab", "social-influence" }
                },
                Claim = claim,
                EvidencePackage = evidenceStrength <= 0 ? Array.Empty<SocialInfluenceEvidenceReferenceData>() : new[]
                {
                    new SocialInfluenceEvidenceReferenceData
                    {
                        evidenceId = $"social-influence.evidence.{context.RunId}.{context.CurrentScenarioId}.{suffix}",
                        sourceId = speaker,
                        strength = evidenceStrength,
                        credibility = speakerResolve,
                        fabricated = deception != SocialInfluenceDeceptionMode.NoDeception
                    }
                },
                Arguments = new[]
                {
                    new SocialInfluenceArgumentData
                    {
                        argumentId = $"social-influence.argument.{context.RunId}.{context.CurrentScenarioId}.{suffix}",
                        premise = "test-lab premise",
                        conclusion = "test-lab conclusion",
                        clarity = 90,
                        emotionalIntensity = intent == SocialInfluenceIntent.Intimidate ? 90 : 20,
                        coercive = intent == SocialInfluenceIntent.Intimidate
                    }
                },
                TruthStatus = truthStatus,
                SpeakerBeliefState = speakerBelief,
                DeceptionMode = deception,
                SpeakerResolve = speakerResolve,
                TargetResistance = targetResistance,
                EvidenceStrength = evidenceStrength,
                RelationshipModifier = 100,
                ReputationModifier = 100,
                Difficulty = difficulty,
                WorldTime = worldTime,
                DeterministicSeed = context.RunId,
                IntentionDefinitionId = intentionDefinitionId,
                InteractionDefinitionId = interactionDefinitionId,
                Visibility = SocialInfluenceVisibility.Witnessed,
                CommitBeliefEvidence = true,
                CommitDecisionModifier = true
            };
        }

        private static KnowledgePropositionData InfluenceClaim(TestLabAutomationContext context, string suffix)
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                subjectType = KnowledgeSubjectType.Event,
                subjectId = $"event.social-influence.{context.RunId}.{context.CurrentScenarioId}.{suffix}",
                valueType = KnowledgeValueType.Boolean,
                booleanValue = true,
                sourceContextId = $"source.social-influence.{context.RunId}.{context.CurrentScenarioId}.{suffix}"
            };
        }

        private static string InfluenceScoped(TestLabAutomationContext context, string suffix)
        {
            return $"social-influence.automation.{context.RunId}.{context.CurrentScenarioId}.{suffix}";
        }

        private static ITestLabAutomationScenario DecisionScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeSocialDecisionDefinitionFactory.SociableProfileId,
                    PrototypeSocialDecisionDefinitionFactory.ScriptControlledProfileId,
                    PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId,
                    PrototypeSocialDecisionDefinitionFactory.IntroduceSelfId,
                    PrototypeSocialDecisionDefinitionFactory.ConsiderTrustId,
                    PrototypeSocialInteractionDefinitionFactory.GreetId,
                    PrototypeSocialInteractionDefinitionFactory.IntroduceId
                });
        }

        private static bool TryGetDecisionRuntime(TestLabAutomationContext context, out SocialDecisionRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.SocialDecisions;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Social Decision runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static SocialDecisionRequest DecisionRequest(TestLabAutomationContext context, string profileId, string explicitIntentionId = "", string target = "person.prototype.friend", bool commit = false)
        {
            return new SocialDecisionRequest
            {
                ActorPersonId = context.ScenarioContext.Runtimes.PersonId,
                DecisionProfileId = profileId,
                ExplicitIntentionDefinitionId = explicitIntentionId,
                ExplicitTargetPersonId = target,
                AvailableTargetPersonIds = string.IsNullOrWhiteSpace(target) ? Array.Empty<string>() : new[] { target },
                ActorControlPolicy = SocialDecisionActorControlPolicy.AutonomousNpc,
                WorldTime = 100d,
                DeterministicSeed = context.RunId,
                CommitDecisionState = commit,
                ForceEvaluate = true,
                MaximumTargetsOverride = 4,
                MaximumCandidatesOverride = 12
            };
        }

        private static string SeedDecisionInputs(TestLabAutomationContext context, string suffix)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            if (runtimes == null)
            {
                return "Runtime bundle is missing.";
            }

            RelationshipOperationResult relationship = runtimes.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = $"relationship.decision.{context.RunId}.{context.CurrentScenarioId}.{suffix}",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = runtimes.PersonId,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 1d,
                transactionId = Tx(context, $"decision-relationship-{suffix}")
            });
            if (!relationship.Succeeded && !relationship.Duplicate)
            {
                return relationship.Message;
            }

            string attitudeFailure = MutateNetworkAttitude(runtimes.Attitudes, Tx(context, $"decision-trust-{suffix}"), runtimes.PersonId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId, 70)
                ?? MutateNetworkAttitude(runtimes.Attitudes, Tx(context, $"decision-affection-{suffix}"), runtimes.PersonId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.AffectionId, 45);
            return attitudeFailure ?? string.Empty;
        }

        private static bool TryGetNormRuntime(TestLabAutomationContext context, out SocialNormRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.SocialNorms;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Social Norm runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static SocialNormEvaluationRequest NormRequest(
            TestLabAutomationContext context,
            string suffix,
            string interactionDefinitionId,
            IReadOnlyList<string> requestedNormIds = null,
            string target = "person.prototype.friend",
            IReadOnlyList<string> witnesses = null,
            IReadOnlyList<string> tags = null,
            SocialInteractionVisibility visibility = SocialInteractionVisibility.Private,
            SocialNormAssessmentClassification classification = SocialNormAssessmentClassification.Unknown,
            SocialNormActorKnowledgeState actorKnowledge = SocialNormActorKnowledgeState.Knew,
            string promiseId = "",
            string promiseState = "",
            string placeId = "place.prototype.test-lab")
        {
            string[] contextTags = (tags ?? Array.Empty<string>())
                .Concat(string.IsNullOrWhiteSpace(promiseState) ? Array.Empty<string>() : new[] { $"promise-state.{promiseState}" })
                .ToArray();
            return new SocialNormEvaluationRequest
            {
                TransactionId = Tx(context, suffix),
                AssessmentRecordId = NormScoped(context, suffix),
                ActorPersonId = context.ScenarioContext.Runtimes.PersonId,
                TargetPersonId = target,
                InteractionRecordId = InteractionScoped(context, $"norm-{suffix}"),
                InteractionDefinitionId = interactionDefinitionId,
                PromiseId = promiseId,
                Subject = new SocialInteractionSubjectData
                {
                    kind = SocialInteractionSubjectKind.Person,
                    subjectId = target,
                    ownerPersonId = target,
                    tags = new[] { "test-lab", "social-norm" }
                },
                PlaceId = placeId,
                AudienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                WitnessPersonIds = (witnesses ?? Array.Empty<string>()).ToArray(),
                ContextTags = contextTags,
                RequestedNormIds = (requestedNormIds ?? Array.Empty<string>()).ToArray(),
                Visibility = visibility,
                Channel = SocialInteractionCommunicationChannel.Conversation,
                ConductClassification = classification,
                ActorKnowledge = actorKnowledge,
                OccurrenceWorldTime = context.CurrentStepIndex + 1d,
                EvaluationWorldTime = context.CurrentStepIndex + 1d,
                DeterministicSeed = context.RunId
            };
        }

        private static bool TryGetInteractionRuntime(TestLabAutomationContext context, out SocialInteractionRuntime runtime, out DefinitionRegistry registry, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.SocialInteractions;
            registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (runtime == null || registry == null)
            {
                failure = runtime == null ? "Social Interaction runtime is missing from the Test Lab runtime bundle." : "Definition registry is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static SocialInteractionRequest InteractionRequest(TestLabAutomationContext context, string definitionId, string suffix, string target = "person.prototype.friend", double worldTime = 0d, IReadOnlyList<string> witnesses = null, SocialInteractionVisibility? visibility = null)
        {
            return new SocialInteractionRequest
            {
                TransactionId = Tx(context, suffix),
                InteractionRecordId = InteractionScoped(context, suffix),
                InteractionDefinitionId = definitionId,
                InitiatorPersonId = context.ScenarioContext.Runtimes.PersonId,
                TargetPersonId = target,
                WitnessPersonIds = witnesses ?? Array.Empty<string>(),
                AudienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                PlaceId = "place.prototype.test-lab",
                Subject = new SocialInteractionSubjectData
                {
                    kind = SocialInteractionSubjectKind.Person,
                    subjectId = target,
                    ownerPersonId = target,
                    tags = new[] { "test-lab" }
                },
                Channel = SocialInteractionCommunicationChannel.Conversation,
                VisibilityOverride = visibility,
                WorldTime = worldTime,
                DeterministicSeed = context.RunId
            };
        }

        private static ITestLabAutomationScenario IntegrationScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Social | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                    PrototypeRelationshipDefinitionFactory.RivalRelationshipId,
                    PrototypeRelationshipDefinitionFactory.BiologicalParentChildRelationshipId,
                    PrototypeAttitudeDefinitionFactory.TrustId,
                    PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                    PrototypeRumorDefinitionFactory.PersonalConductRumorId,
                    PrototypeSocialInteractionDefinitionFactory.GreetId,
                    PrototypeSocialNormDefinitionFactory.HostGreetingNormId,
                    PrototypeSocialNetworkDefinitionFactory.FriendCircleGroupId,
                    PrototypeSocialDecisionDefinitionFactory.SociableProfileId,
                    PrototypeSocialInfluenceDefinitionFactory.PersuadeRequestId,
                    PrototypeSocialEmotionDefinitionFactory.GratitudeId,
                    PrototypeFamilyRelationshipDefinitionFactory.FamilyHouseholdDefinitionId
                });
        }

        private static bool TryCreateStep12Facade(TestLabAutomationContext context, out Step12SocialSimulationFacade facade, out string failure)
        {
            facade = null;
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            if (runtimes == null)
            {
                failure = "Test Lab runtime bundle is missing.";
                return false;
            }

            if (runtimes.DefinitionRegistry == null
                || runtimes.Relationships == null
                || runtimes.Attitudes == null
                || runtimes.Reputation == null
                || runtimes.Rumors == null
                || runtimes.SocialInteractions == null
                || runtimes.SocialNorms == null
                || runtimes.SocialNetworks == null
                || runtimes.SocialDecisions == null
                || runtimes.SocialInfluence == null
                || runtimes.SocialEmotions == null
                || runtimes.FamilyRelationships == null)
            {
                failure = "One or more Step 12 social runtimes are missing from the Test Lab runtime bundle.";
                return false;
            }

            facade = new Step12SocialSimulationFacade(
                runtimes.DefinitionRegistry,
                runtimes.KnownPersonIds,
                runtimes.WorldId,
                runtimes.Relationships,
                runtimes.Attitudes,
                runtimes.Reputation,
                runtimes.Rumors,
                runtimes.SocialInteractions,
                runtimes.SocialNorms,
                runtimes.SocialNetworks,
                runtimes.SocialDecisions,
                runtimes.SocialInfluence,
                runtimes.SocialEmotions,
                runtimes.FamilyRelationships);
            failure = string.Empty;
            return true;
        }

        private static string SeedStep12IntegrationContext(TestLabAutomationContext context, string suffix)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            if (runtimes == null)
            {
                return "Runtime bundle is missing.";
            }

            RelationshipOperationResult friend = runtimes.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                transactionId = Tx(context, $"integration-friend-{suffix}"),
                recordId = Scoped(context, $"integration-friend-{suffix}"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = runtimes.PersonId,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 1d
            });
            if (!friend.Succeeded && !friend.Duplicate)
            {
                return friend.Message;
            }

            RelationshipOperationResult rival = runtimes.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                transactionId = Tx(context, $"integration-rival-{suffix}"),
                recordId = Scoped(context, $"integration-rival-{suffix}"),
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.RivalRelationshipId,
                firstPersonId = runtimes.PersonId,
                firstRoleId = "rival",
                secondPersonId = "person.prototype.rival",
                secondRoleId = "rival",
                startWorldTime = 2d
            });
            if (!rival.Succeeded && !rival.Duplicate)
            {
                return rival.Message;
            }

            AttitudeMutationResult attitude = runtimes.Attitudes.Mutate(new AttitudeMutationRequest
            {
                transactionId = Tx(context, $"integration-trust-{suffix}"),
                recordId = $"attitude.integration.{context.RunId}.{context.CurrentScenarioId}.{suffix}",
                observerPersonId = runtimes.PersonId,
                subjectPersonId = "person.prototype.friend",
                dimensionId = PrototypeAttitudeDefinitionFactory.TrustId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 65,
                sourceCategory = AttitudeContributionSourceCategory.TestLab,
                worldTime = 3d
            });
            if (!attitude.Succeeded && !attitude.Duplicate)
            {
                return attitude.Message;
            }

            SocialInteractionResult interaction = runtimes.SocialInteractions.Execute(new SocialInteractionRequest
            {
                TransactionId = Tx(context, $"integration-greet-{suffix}"),
                InteractionRecordId = InteractionScoped(context, $"integration-greet-{suffix}"),
                InteractionDefinitionId = PrototypeSocialInteractionDefinitionFactory.GreetId,
                InitiatorPersonId = runtimes.PersonId,
                TargetPersonId = "person.prototype.friend",
                WorldTime = 4d,
                DeterministicSeed = context.RunId
            });
            if (!interaction.Succeeded && !interaction.Duplicate)
            {
                return interaction.Message;
            }

            HouseholdMutationResult household = runtimes.FamilyRelationships.CreateHousehold(new HouseholdMutationRequest
            {
                transactionId = Tx(context, $"integration-household-{suffix}"),
                householdId = $"household.integration.{context.RunId}.{context.CurrentScenarioId}.{suffix}",
                householdDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.FamilyHouseholdDefinitionId,
                personId = runtimes.PersonId,
                role = HouseholdRole.Head,
                worldTime = 5d
            });
            if (!household.Succeeded && !household.Duplicate)
            {
                return household.Message;
            }

            return string.Empty;
        }

        private static ITestLabScenarioStep Step(string stepId, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> run)
        {
            return new TestLabScenarioStep(stepId, displayName, run);
        }

        private static string Scoped(TestLabAutomationContext context, string suffix)
        {
            return $"relationship.automation.{context.RunId}.{context.CurrentScenarioId}.{suffix}";
        }

        private static string RepScoped(TestLabAutomationContext context, string suffix)
        {
            return $"reputation.automation.{context.RunId}.{context.CurrentScenarioId}.{suffix}";
        }

        private static string RumorScoped(TestLabAutomationContext context, string suffix)
        {
            return $"rumor.automation.{context.RunId}.{context.CurrentScenarioId}.{suffix}";
        }

        private static string InteractionScoped(TestLabAutomationContext context, string suffix)
        {
            return $"social-interaction.automation.{context.RunId}.{context.CurrentScenarioId}.{suffix}";
        }

        private static string NormScoped(TestLabAutomationContext context, string suffix)
        {
            return $"social-norm.automation.{context.RunId}.{context.CurrentScenarioId}.{suffix}";
        }

        private static string Tx(TestLabAutomationContext context, string suffix)
        {
            return context.TransactionIds.Create(context.CurrentSuiteId, context.CurrentScenarioId, context.RunId, context.CurrentStepIndex, suffix);
        }
    }
}
#endif
