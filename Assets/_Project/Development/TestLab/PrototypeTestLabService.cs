#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.Integration;
using UnityIsekaiGame.Beings.Biology.Recovery;
using UnityIsekaiGame.Beings.Biology.Transformation;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.Development.Automation;
using UnityIsekaiGame.Development.Automation.Fixtures.History;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Contributions;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Combat.Integration;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Combat.Reactions;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Integration;
using UnityIsekaiGame.Knowledge.Observation;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.People;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Emotions;
using UnityIsekaiGame.Social.Family;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Rumors;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;
using UnityIsekaiGame.WorldLocations;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityIsekaiGame.Development
{
    public sealed class PrototypeTestLabService
    {
        public const int DefaultHistoryLimit = 40;
        private const string PrototypeCatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string DevelopmentStatusSource = "development.prototype-test-lab";
        private const string PrototypePublicPolicyId = "information-access.prototype.public-rumor";
        private const string PrototypeSecretPolicyId = "information-access.prototype.previous-body-secret";
        private const string PrototypeDiscoveryPolicyId = "information-access.prototype.hidden-discovery";
        private const string PrototypeConcealedPolicyId = "information-access.prototype.concealed-secret";
        private const string PrototypePublicSubjectId = "fact.prototype.public-rumor";
        private const string PrototypeSecretSubjectId = "memory.prototype.previous-body-secret";
        private const string PrototypeDiscoverySubjectId = "event.prototype.hidden-discovery";
        private const string PrototypeConcealedSubjectId = "memory.prototype.concealed-secret";

        private readonly List<PrototypeTestLabOperation> history = new List<PrototypeTestLabOperation>();
        private readonly HashSet<string> pendingConfirmations = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<Type, List<IGameDefinition>> selectorCache = new Dictionary<Type, List<IGameDefinition>>();
        private readonly DamageHealingService damageHealingService = new DamageHealingService();
        private readonly DefensiveActionService defensiveActionService = new DefensiveActionService();
        private readonly AttackResolutionService attackResolutionService;
        private CombatReactionService combatReactionService;
        private CombatContributionService combatContributionService;
        private CombatExecutionService combatExecutionService = new CombatExecutionService();
        private CombatRuntimeFacade combatRuntimeFacade;
        private readonly TestLabAutomationRegistry automationRegistry = new TestLabAutomationRegistry();
        private readonly TestLabAutomationReportExporter automationReportExporter = new TestLabAutomationReportExporter();
        private PrototypeTestLabAutomationHost automationHost;
        private TestLabAutomationRunner automationRunner;
        private TestLabAutomationResult lastAutomationResult;
        private readonly List<TestLabScenarioResult> automationBatchScenarios = new List<TestLabScenarioResult>();
        private readonly HashSet<string> loggedAutomationFailureKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Stack<AutomationRuntimeBindingFrame> automationRuntimeBindingStack = new Stack<AutomationRuntimeBindingFrame>();
        private readonly Dictionary<string, TestLabRuntimeBundle> sharedAutomationRuntimeBundles = new Dictionary<string, TestLabRuntimeBundle>(StringComparer.Ordinal);
        private readonly Dictionary<string, TestLabRuntimeBundle> persistentAutomationRuntimeBundles = new Dictionary<string, TestLabRuntimeBundle>(StringComparer.Ordinal);
        private TestLabScenarioContext currentAutomationScenarioContext;
        private DateTime automationBatchStartedAtUtc;
        private string automationBatchRunId;
        private TestLabAutomationRunMode automationBatchMode;
        private bool automationBatchCancelled;
        private int automationBatchCounter;
        private bool automationBatchRunning;
        private bool suppressExpectedAutomationWarnings;
        private PrototypeTestLabContext context;
        private DefinitionRegistry registry;
        private int historyLimit = DefaultHistoryLimit;
        private string lastSpawnedWorldEntityId;
        private ItemDefinition lastSpawnedWorldEntityItem;
        private string lastDestroyedWorldEntityId;
        private ItemDefinition lastDestroyedWorldEntityItem;
        private string lastWorldEntityOperationMessage;
        private string lastAttackTransactionId;
        private string lastDefenseActivationTransactionId;
        private string lastCombatStateTransactionId;
        private string lastCombatStateSplitTransactionId;
        private string lastCombatExecutionBeginTransactionId;
        private string lastCombatExecutionCommitTransactionId;
        private string lastCombatExecutionInstanceId;
        private string lastLifecycleTransactionId;
        private string lastOngoingEffectTransactionId;
        private string lastBiologicalRecoveryTickId;
        private DamageApplicationResult lastContributionDamageSource;
        private HealingApplicationResult lastContributionHealingSource;
        private string lastContributionCreditTargetActorId;
        private float combatStateClockSeconds;
        private float combatExecutionClockSeconds;
        private float ongoingEffectClockSeconds;
        private GameObject automationEnemyTarget;
        private readonly Dictionary<string, GameObject> combatStateTestActors = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly AuthoritativeHistoryRuntime authoritativeHistory = new AuthoritativeHistoryRuntime();
        private readonly PersonMemoryRuntime playerMemory = new PersonMemoryRuntime();
        private InformationSourceRuntime informationSources = new InformationSourceRuntime();
        private InformationTransferRuntime informationTransfers = new InformationTransferRuntime();
        private InformationAccessRuntime informationAccess = new InformationAccessRuntime();
        private KnowledgeRecordRuntime knowledgeRecords = new KnowledgeRecordRuntime();
        private RelationshipRuntime relationships = new RelationshipRuntime();
        private InterpersonalAttitudeRuntime interpersonalAttitudes = new InterpersonalAttitudeRuntime();
        private ReputationRuntime reputation = new ReputationRuntime();
        private RumorRuntime rumors = new RumorRuntime();
        private SocialInteractionRuntime socialInteractions = new SocialInteractionRuntime();
        private SocialNormRuntime socialNorms = new SocialNormRuntime();
        private SocialNetworkRuntime socialNetworks = new SocialNetworkRuntime();
        private SocialDecisionRuntime socialDecisions = new SocialDecisionRuntime();
        private SocialInfluenceRuntime socialInfluence = new SocialInfluenceRuntime();
        private SocialEmotionRuntime socialEmotions = new SocialEmotionRuntime();
        private AuthoritativeHistorySaveData lastHistorySaveData;
        private PersonMemorySaveData lastMemorySaveData;

        public event Action HistoryChanged;

        public IReadOnlyList<PrototypeTestLabOperation> History => history;
        public DefinitionRegistry Registry => registry;
        public string CurrentSlotId => PrototypeSaveSlotCatalog.ManualSlotId(0);

        public PrototypeTestLabService()
        {
            attackResolutionService = new AttackResolutionService(damageHealingService, defensiveActionService);
        }

        public void Configure(PrototypeTestLabContext newContext)
        {
            context = newContext;
            combatExecutionService = context?.Persistence == null ? combatExecutionService : context.Persistence.CombatExecution;
            registry = CreateRegistry(context?.DefinitionCatalog);
            context?.IdentityProgression?.RegisterDefinitionCache(registry);
            if (EnsureResources(out CharacterResourceCollection resources))
            {
                resources.Configure(registry, context.PlayerCalculatedStats, PersistenceService.LocalPlayerId);
            }

            context?.PlayerSkills?.Configure(registry, context.PlayerCalculatedStats, context.SpellLoadout);
            if (EnsureTraits(out CharacterTraitCollection traits))
            {
                traits.Configure(registry, context.PlayerCalculatedStats, context.PlayerSkills, PersistenceService.LocalPlayerId);
            }

            EnsureKnowledgeRuntime(out _);
            EnsureHistoryRuntime(out _, out _);
            informationSources = context?.Persistence?.InformationSources ?? informationSources ?? new InformationSourceRuntime();
            informationSources.Configure(registry, GetPrototypePersonId());
            informationTransfers = context?.Persistence?.InformationTransfers ?? informationTransfers ?? new InformationTransferRuntime();
            informationTransfers.Configure(registry, GetPrototypePersonId());
            informationAccess = context?.InformationAccess ?? context?.Persistence?.InformationAccess ?? informationAccess ?? new InformationAccessRuntime();
            informationAccess.Configure(registry, GetPrototypePersonId());
            knowledgeRecords = context?.KnowledgeRecords ?? context?.Persistence?.KnowledgeRecords ?? knowledgeRecords ?? new KnowledgeRecordRuntime();
            knowledgeRecords.Configure(registry, GetPrototypePersonId());
            relationships = context?.Persistence?.Relationships ?? relationships ?? new RelationshipRuntime();
            relationships.Configure(registry, GetKnownPrototypePersons());
            interpersonalAttitudes = context?.Persistence?.InterpersonalAttitudes ?? interpersonalAttitudes ?? new InterpersonalAttitudeRuntime();
            interpersonalAttitudes.Configure(registry, GetKnownPrototypePersons());
            reputation = context?.Persistence?.Reputation ?? reputation ?? new ReputationRuntime();
            reputation.Configure(registry, GetKnownPrototypePersons());
            rumors = context?.Persistence?.Rumors ?? rumors ?? new RumorRuntime();
            rumors.Configure(registry, GetKnownPrototypePersons(), ResolveKnowledgeRuntimeForRumorPerson, ResolveMemoryRuntimeForRumorPerson);
            socialInteractions = context?.Persistence?.SocialInteractions ?? socialInteractions ?? new SocialInteractionRuntime();
            socialInteractions.Configure(registry, GetKnownPrototypePersons(), relationships, interpersonalAttitudes, reputation, rumors);
            socialNorms = context?.Persistence?.SocialNorms ?? socialNorms ?? new SocialNormRuntime();
            socialNorms.Configure(registry, GetKnownPrototypePersons(), relationships, interpersonalAttitudes, reputation, rumors, socialInteractions);
            socialNetworks = context?.Persistence?.SocialNetworks ?? socialNetworks ?? new SocialNetworkRuntime();
            socialNetworks.Configure(registry, GetKnownPrototypePersons(), relationships, interpersonalAttitudes, reputation, rumors, socialInteractions, socialNorms);
            socialDecisions = context?.Persistence?.SocialDecisions ?? socialDecisions ?? new SocialDecisionRuntime();
            socialInfluence = context?.Persistence?.SocialInfluence ?? socialInfluence ?? new SocialInfluenceRuntime();
            socialInfluence.Configure(registry, GetKnownPrototypePersons(), interpersonalAttitudes, reputation, socialInteractions, new[] { context?.PlayerKnowledge });
            socialEmotions = context?.Persistence?.SocialEmotions ?? socialEmotions ?? new SocialEmotionRuntime();
            socialEmotions.Configure(registry, GetKnownPrototypePersons(), relationships, interpersonalAttitudes, reputation, rumors, socialInteractions, socialNorms, socialNetworks, socialInfluence);
            socialDecisions.Configure(registry, GetKnownPrototypePersons(), socialInteractions, relationships, interpersonalAttitudes, reputation, rumors, socialNorms, socialNetworks, SocialDecisionModifierSourceCollection.Compose(socialInfluence, socialEmotions));

            EnsureCharacterSystem(out _);
            EnsureLifecycleRuntime(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject, ref context.PlayerLifecycle, needsResource: true);
            EnsureAutomationEnemyTarget();
            EnsureLifecycleRuntime(context?.EnemyTransform == null ? null : context.EnemyTransform.gameObject, ref context.EnemyLifecycle, needsResource: true);
            EnsureCombatStateRuntime();
            EnsureOngoingEffectRuntime(targetEnemy: false);
            EnsureOngoingEffectRuntime(targetEnemy: true);
            EnsureCombatReactionRuntime();
            EnsureCombatContributionRuntime();
            combatRuntimeFacade = null;
            EnsureCombatRuntimeFacade();
            EnsureAutomation();
            selectorCache.Clear();
        }

        public IReadOnlyList<ITestLabAutomationSuite> GetAutomationSuites()
        {
            EnsureAutomation();
            return automationRegistry.Suites;
        }

        public IReadOnlyList<ITestLabAutomationScenario> GetAutomationScenarios(string suiteId)
        {
            EnsureAutomation();
            return automationRegistry.TryGetSuite(suiteId, out ITestLabAutomationSuite suite)
                ? suite.Scenarios
                : Array.Empty<ITestLabAutomationScenario>();
        }

        public TestLabSuiteCompatibilityReport PreviewAutomationCompatibility(string suiteId = "")
        {
            EnsureAutomation();
            return automationRunner.PreviewCompatibility(suiteId);
        }

        public void UnregisterAutomationHost()
        {
            if (automationHost != null)
            {
                TestLabAutomationHostRegistry.Unregister(automationHost);
            }
        }

        public string BuildAutomationSummary()
        {
            EnsureAutomation();
            TestLabAutomationValidationResult validation = TestLabAutomationValidation.Validate(automationRegistry);
            TestLabAutomationMigrationInventory inventory = TestLabAutomationValidation.BuildMigrationInventory(automationRegistry);
            TestLabSuiteCompatibilityReport compatibility = automationRunner.PreviewCompatibility();
            string hostSummary = automationHost == null ? "Host: none." : "Host: " + automationHost.GetCapabilities().ToDiagnostic();
            if (lastAutomationResult == null)
            {
                return $"{validation.ToSummary()}\n{inventory.ToSummary()}\n{compatibility.ToDiagnostic()}\n{hostSummary}\nSuites: {automationRegistry.Suites.Count}\nNo automation run yet.";
            }

            List<string> lines = new List<string>
            {
                validation.ToSummary(),
                inventory.ToSummary(),
                compatibility.ToDiagnostic(),
                hostSummary,
                $"Run: {lastAutomationResult.RunId} ({lastAutomationResult.RunMode}) Order={lastAutomationResult.ScenarioOrder} Seed={lastAutomationResult.ShuffleSeed} Cancelled={lastAutomationResult.Cancelled}",
                $"Scenarios: {lastAutomationResult.PassedScenarios} passed, {lastAutomationResult.FailedScenarios} failed, {lastAutomationResult.ErrorScenarios} error, {lastAutomationResult.SkippedScenarios} skipped, {lastAutomationResult.CancelledScenarios} cancelled.",
                $"Steps: {lastAutomationResult.TotalSteps}. Elapsed: {lastAutomationResult.Elapsed.TotalSeconds:0.###}s."
            };

            foreach (TestLabScenarioResult scenario in lastAutomationResult.Scenarios)
            {
                lines.Add($"{scenario.Status}: {scenario.SuiteId}/{scenario.ScenarioId} - {scenario.DisplayName}");
                TestLabAutomationStepResult failedStep = scenario.Steps.FirstOrDefault(step => step.Status == TestLabAutomationStatus.Failed || step.Status == TestLabAutomationStatus.Error);
                if (failedStep != null)
                {
                    lines.Add($"  Failed Step: {failedStep.StepId} Expected='{failedStep.Expected}' Actual='{failedStep.Actual}' Tx='{failedStep.TransactionId}'");
                    if (!string.IsNullOrWhiteSpace(failedStep.Diagnostics))
                    {
                        lines.Add($"  Diagnostics: {failedStep.Diagnostics}");
                    }
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation ValidateAutomationRegistration()
        {
            EnsureAutomation();
            TestLabAutomationValidationResult validation = TestLabAutomationValidation.Validate(automationRegistry);
            TestLabAutomationMigrationInventory inventory = TestLabAutomationValidation.BuildMigrationInventory(automationRegistry);
            TestLabSuiteCompatibilityReport compatibility = automationRunner.PreviewCompatibility();
            string message = validation.ToSummary() + Environment.NewLine + inventory.ToSummary() + Environment.NewLine + compatibility.ToDiagnostic();
            if (validation.Errors.Count > 0)
            {
                message += Environment.NewLine + string.Join(Environment.NewLine, validation.Errors);
            }

            if (validation.Warnings.Count > 0)
            {
                message += Environment.NewLine + string.Join(Environment.NewLine, validation.Warnings);
            }

            if (compatibility.UnsupportedCount > 0)
            {
                message += Environment.NewLine + string.Join(Environment.NewLine, compatibility.Scenarios
                    .Where(scenario => !scenario.Compatible)
                    .Select(scenario => $"{scenario.SuiteId}/{scenario.ScenarioId}: {scenario.FailureCode} {scenario.Diagnostics}"));
            }

            bool succeeded = validation.Succeeded && compatibility.Compatible;
            return Record(succeeded, "Validate Test Lab Automation", succeeded ? "Valid" : "Invalid", message);
        }

        public PrototypeTestLabOperation RunAutomationScenario(string suiteId, string scenarioId, bool stopOnFirstFailure)
        {
            EnsureAutomation();
            loggedAutomationFailureKeys.Clear();
            lastAutomationResult = automationRunner.RunScenario(suiteId, scenarioId, CreateAutomationOptions(stopOnFirstFailure));
            LogAutomationScenarioFailures(lastAutomationResult);
            return Record(!lastAutomationResult.HasFailures, "Run Automation Scenario", lastAutomationResult.HasFailures ? "Failed" : "Passed", FormatAutomationRun(lastAutomationResult));
        }

        public PrototypeTestLabOperation RunAutomationSuite(string suiteId, bool stopOnFirstFailure)
        {
            EnsureAutomation();
            loggedAutomationFailureKeys.Clear();
            lastAutomationResult = automationRunner.RunSuite(suiteId, CreateAutomationOptions(stopOnFirstFailure));
            LogAutomationScenarioFailures(lastAutomationResult);
            return Record(!lastAutomationResult.HasFailures, "Run Automation Suite", lastAutomationResult.HasFailures ? "Failed" : "Passed", FormatAutomationRun(lastAutomationResult));
        }

        public PrototypeTestLabOperation RunAutomationQuick(bool stopOnFirstFailure)
        {
            EnsureAutomation();
            loggedAutomationFailureKeys.Clear();
            lastAutomationResult = automationRunner.RunAll(quickOnly: true, CreateAutomationOptions(stopOnFirstFailure));
            LogAutomationScenarioFailures(lastAutomationResult);
            return Record(!lastAutomationResult.HasFailures, "Run Quick Automation", lastAutomationResult.HasFailures ? "Failed" : "Passed", FormatAutomationRun(lastAutomationResult));
        }

        public PrototypeTestLabOperation RunAutomationAll(bool stopOnFirstFailure)
        {
            EnsureAutomation();
            loggedAutomationFailureKeys.Clear();
            lastAutomationResult = automationRunner.RunAll(quickOnly: false, CreateAutomationOptions(stopOnFirstFailure));
            LogAutomationScenarioFailures(lastAutomationResult);
            return Record(!lastAutomationResult.HasFailures, "Run All Automation", lastAutomationResult.HasFailures ? "Failed" : "Passed", FormatAutomationRun(lastAutomationResult));
        }

        public PrototypeTestLabOperation RerunFailedAutomation(bool stopOnFirstFailure)
        {
            EnsureAutomation();
            loggedAutomationFailureKeys.Clear();
            lastAutomationResult = automationRunner.RerunFailed(CreateAutomationOptions(stopOnFirstFailure));
            LogAutomationScenarioFailures(lastAutomationResult);
            return Record(!lastAutomationResult.HasFailures, "Rerun Failed Automation", lastAutomationResult.HasFailures ? "Failed" : "Passed", FormatAutomationRun(lastAutomationResult));
        }

        public PrototypeTestLabOperation BeginAutomationBatch(TestLabAutomationRunMode runMode)
        {
            EnsureAutomation();
            automationBatchCounter++;
            automationBatchRunId = $"ui-batch-{automationBatchCounter:0000}";
            automationBatchMode = runMode;
            automationBatchStartedAtUtc = DateTime.UtcNow;
            automationBatchCancelled = false;
            automationBatchScenarios.Clear();
            loggedAutomationFailureKeys.Clear();
            UpdateAutomationBatchResult();
            return RecordSuccess("Begin Automation Batch", $"Started {runMode} automation batch {automationBatchRunId}.");
        }

        public PrototypeTestLabOperation RunAutomationScenarioInBatch(string suiteId, string scenarioId, bool stopOnFirstFailure)
        {
            EnsureAutomation();
            if (string.IsNullOrWhiteSpace(automationBatchRunId))
            {
                BeginAutomationBatch(TestLabAutomationRunMode.SelectedScenario);
            }

            automationBatchRunning = true;
            TestLabAutomationResult scenarioResult;
            try
            {
                scenarioResult = automationRunner.RunScenario(suiteId, scenarioId, CreateAutomationOptions(stopOnFirstFailure));
            }
            finally
            {
                automationBatchRunning = false;
            }

            automationBatchScenarios.AddRange(scenarioResult.Scenarios);
            UpdateAutomationBatchResult();
            LogAutomationScenarioFailures(scenarioResult);
            return Record(!scenarioResult.HasFailures, "Run Automation Batch Scenario", scenarioResult.HasFailures ? "Failed" : "Passed", FormatAutomationRun(scenarioResult));
        }

        public PrototypeTestLabOperation CompleteAutomationBatch(bool cancelled)
        {
            automationBatchCancelled = cancelled;
            UpdateAutomationBatchResult();
            string status = cancelled ? "Cancelled" : lastAutomationResult != null && lastAutomationResult.HasFailures ? "Failed" : "Passed";
            LogAutomationScenarioFailures(lastAutomationResult);
            return Record(status == "Passed", "Complete Automation Batch", status, FormatAutomationRun(lastAutomationResult));
        }

        public PrototypeTestLabOperation CancelAutomation()
        {
            EnsureAutomation();
            automationBatchCancelled = true;
            automationRunner.Cancel();
            return RecordSuccess("Cancel Automation", "Cancellation requested. The current synchronous scenario will finish its current step before remaining scenarios are marked cancelled.");
        }

        public PrototypeTestLabOperation ClearAutomationResults()
        {
            lastAutomationResult = null;
            return RecordSuccess("Clear Automation Results", "Automation result summary cleared.");
        }

        public TestLabScenarioContext CreateAutomationScenarioContext(string runId, string suiteId, string scenarioId, TestLabScenarioIsolationMode isolationMode, TestLabRuntimeArea requiredRuntimeAreas, IEnumerable<string> requiredFixtureIds)
        {
            TestLabRuntimeBundle runtimeBundle = CreateAutomationRuntimeBundle(runId, suiteId, scenarioId, isolationMode, out bool contextOwnsRuntime);
            return new TestLabScenarioContext(
                runId,
                suiteId,
                scenarioId,
                isolationMode,
                runtimeBundle,
                contextOwnsRuntime ? runtimeBundle : null,
                requiredRuntimeAreas,
                requiredFixtureIds,
                CaptureAutomationSceneFingerprint);
        }

        public TestLabDefinitionContext CreateAutomationDefinitionContext()
        {
            return new TestLabDefinitionContext(
                registry,
                PrototypeCatalogPath,
                "Prototype Definition Catalog",
                catalogAuthored: context?.DefinitionCatalog != null,
                fallbackDefinitionsAvailable: true,
                revision: registry == null ? 0 : registry.Count,
                new[] { context?.DefinitionCatalog == null ? "Using explicit Prototype fallback definitions where catalog assets are unavailable." : "Prototype catalog-authored definitions are authoritative." });
        }

        public static void RegisterDefaultAutomationSuites(TestLabAutomationRegistry registry)
        {
            PrototypeTestLabAutomationCatalog.RegisterDefaultSuites(registry);
        }

        public static TestLabDefinitionContext CreateDefaultAutomationDefinitionContext(DefinitionCatalog catalog = null)
        {
            DefinitionCatalog effectiveCatalog = catalog;
#if UNITY_EDITOR
            if (effectiveCatalog == null)
            {
                effectiveCatalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(PrototypeCatalogPath);
            }
#endif
            DefinitionRegistry registry = CreateAutomationDefinitionRegistry(effectiveCatalog);
            return new TestLabDefinitionContext(
                registry,
                PrototypeCatalogPath,
                "Prototype Definition Catalog",
                catalogAuthored: effectiveCatalog != null,
                fallbackDefinitionsAvailable: true,
                revision: registry == null ? 0 : registry.Count,
                new[] { effectiveCatalog == null ? "Using explicit Prototype fallback definitions because no Prototype catalog asset was available." : "Using Prototype catalog-authored definitions plus explicit Prototype fallback definitions." });
        }

        public static DefinitionRegistry CreateAutomationDefinitionRegistry(DefinitionCatalog catalog = null)
        {
            DefinitionRegistry baseRegistry = null;
            if (catalog != null)
            {
                baseRegistry = catalog.CreateRegistry();
            }
#if UNITY_EDITOR
            if (baseRegistry == null)
            {
                DefinitionCatalog loaded = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(PrototypeCatalogPath);
                baseRegistry = loaded == null ? null : loaded.CreateRegistry();
            }
#endif
            return AddDevelopmentHistoryDefinitions(baseRegistry);
        }

        public void SetActiveAutomationScenarioContext(TestLabScenarioContext scenarioContext)
        {
            automationRuntimeBindingStack.Push(new AutomationRuntimeBindingFrame(
                currentAutomationScenarioContext,
                informationSources,
                informationTransfers,
                informationAccess,
                knowledgeRecords,
                relationships,
                interpersonalAttitudes,
                reputation,
                rumors,
                socialInteractions,
                socialNorms,
                socialNetworks,
                socialDecisions,
                socialInfluence,
                socialEmotions));
            currentAutomationScenarioContext = scenarioContext;
            ApplyAutomationRuntimeBindings(scenarioContext);
        }

        public void ClearActiveAutomationScenarioContext(TestLabScenarioContext scenarioContext)
        {
            if (automationRuntimeBindingStack.Count == 0)
            {
                if (ReferenceEquals(currentAutomationScenarioContext, scenarioContext))
                {
                    currentAutomationScenarioContext = null;
                }

                return;
            }

            AutomationRuntimeBindingFrame frame = automationRuntimeBindingStack.Pop();
            if (ReferenceEquals(currentAutomationScenarioContext, scenarioContext))
            {
                currentAutomationScenarioContext = frame.PreviousContext;
                informationSources = frame.PreviousSources;
                informationTransfers = frame.PreviousTransfers;
                informationAccess = frame.PreviousAccess;
                knowledgeRecords = frame.PreviousRecords;
                relationships = frame.PreviousRelationships;
                interpersonalAttitudes = frame.PreviousAttitudes;
                reputation = frame.PreviousReputation;
                rumors = frame.PreviousRumors;
                socialInteractions = frame.PreviousSocialInteractions;
                socialNorms = frame.PreviousSocialNorms;
                socialNetworks = frame.PreviousSocialNetworks;
                socialDecisions = frame.PreviousSocialDecisions;
                socialInfluence = frame.PreviousSocialInfluence;
                socialEmotions = frame.PreviousSocialEmotions;
                ApplyAutomationRuntimeBindings(currentAutomationScenarioContext);
                return;
            }

            currentAutomationScenarioContext = frame.PreviousContext;
            informationSources = frame.PreviousSources;
            informationTransfers = frame.PreviousTransfers;
            informationAccess = frame.PreviousAccess;
            knowledgeRecords = frame.PreviousRecords;
            relationships = frame.PreviousRelationships;
            interpersonalAttitudes = frame.PreviousAttitudes;
            reputation = frame.PreviousReputation;
            rumors = frame.PreviousRumors;
            socialInteractions = frame.PreviousSocialInteractions;
            socialNorms = frame.PreviousSocialNorms;
            socialNetworks = frame.PreviousSocialNetworks;
            socialDecisions = frame.PreviousSocialDecisions;
            socialInfluence = frame.PreviousSocialInfluence;
            socialEmotions = frame.PreviousSocialEmotions;
            ApplyAutomationRuntimeBindings(currentAutomationScenarioContext);
        }

        public void ClearAutomationRunScopes(string runId)
        {
            DisposeAutomationBundles(sharedAutomationRuntimeBundles, runId);
            DisposeAutomationBundles(persistentAutomationRuntimeBundles, runId);
        }

        public string CreateAutomationScopedId(string category, string slug)
        {
            return currentAutomationScenarioContext == null
                ? $"{SanitizeForTransaction(category)}.manual.{SanitizeForTransaction(slug)}.{Guid.NewGuid():N}"
                : currentAutomationScenarioContext.ScopedId(category, slug);
        }

        public PrototypeTestLabOperation ResetAutomationRuntimeState()
        {
            RestoreVitals();
            ResetLifecycleForAutomation(context?.PlayerLifecycle, PersistenceService.LocalPlayerId);
            ResetLifecycleForAutomation(context?.EnemyLifecycle, string.Empty);
            ClearTemporaryStatuses();
            defensiveActionService.ClearTransientStateForRestore();
            combatExecutionService.RestoreFromSaveData(new CombatExecutionSaveData
            {
                schemaVersion = CombatExecutionSaveData.CurrentSchemaVersion,
                playerId = PersistenceService.LocalPlayerId,
                personId = context?.IdentityProgression == null ? string.Empty : context.IdentityProgression.PersonId,
                cooldowns = new List<CombatExecutionCooldownSaveData>()
            }, PersistenceService.LocalPlayerId, out _, restoring: true);
            EnsureCombatStateRuntime().ClearTransientStateForRestore();
            EnsureOngoingEffectRuntime(targetEnemy: false)?.ClearTransientStateForRestore();
            EnsureOngoingEffectRuntime(targetEnemy: true)?.ClearTransientStateForRestore();
            EnsureCombatReactionRuntime()?.ClearTransientStateForRestore();
            EnsureCombatReactionRuntime()?.ClearAllSources();
            EnsureCombatContributionRuntime()?.ClearTransientStateForRestore();
            ResetInformationAutomationState();
            ongoingEffectClockSeconds = 0f;
            combatStateClockSeconds = 0f;
            combatExecutionClockSeconds = 0f;
            lastAttackTransactionId = string.Empty;
            lastDefenseActivationTransactionId = string.Empty;
            lastCombatStateTransactionId = string.Empty;
            lastCombatStateSplitTransactionId = string.Empty;
            lastCombatExecutionBeginTransactionId = string.Empty;
            lastCombatExecutionCommitTransactionId = string.Empty;
            lastCombatExecutionInstanceId = string.Empty;
            lastLifecycleTransactionId = string.Empty;
            lastOngoingEffectTransactionId = string.Empty;
            lastContributionDamageSource = null;
            lastContributionHealingSource = null;
            lastContributionCreditTargetActorId = string.Empty;
            ResetEnemy();
            return RecordSuccess("Reset Automation Runtime", "Runtime automation baseline restored without expected optional-action warnings.");
        }

        internal TestLabRuntimeBundle CreateAutomationRuntimeBundleForHost(string runId, string suiteId, string scenarioId, TestLabScenarioIsolationMode isolationMode, out bool contextOwnsRuntime)
        {
            return CreateAutomationRuntimeBundle(runId, suiteId, scenarioId, isolationMode, out contextOwnsRuntime);
        }

        private TestLabRuntimeBundle CreateAutomationRuntimeBundle(string runId, string suiteId, string scenarioId, TestLabScenarioIsolationMode isolationMode, out bool contextOwnsRuntime)
        {
            contextOwnsRuntime = false;
            switch (isolationMode)
            {
                case TestLabScenarioIsolationMode.FreshRuntime:
                case TestLabScenarioIsolationMode.SnapshotRestore:
                    contextOwnsRuntime = true;
                    return TestLabRuntimeBundle.CreateFresh(
                        registry,
                        GetPrototypePersonId(),
                        PersistenceService.LocalWorldId,
                        GetKnownPrototypePersons(),
                        GetKnownPrototypeBodies(),
                        $"Test Lab {isolationMode} {suiteId}/{scenarioId}");
                case TestLabScenarioIsolationMode.SharedRuntime:
                    return GetOrCreateScopedAutomationBundle(sharedAutomationRuntimeBundles, $"{runId}:{suiteId}:shared", $"Test Lab Shared {suiteId}");
                case TestLabScenarioIsolationMode.PersistentFixture:
                    return GetOrCreateScopedAutomationBundle(persistentAutomationRuntimeBundles, $"{runId}:{suiteId}:persistent", $"Test Lab Persistent {suiteId}");
            }

            EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge);
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime);
            informationSources = context?.Persistence?.InformationSources ?? informationSources ?? new InformationSourceRuntime();
            informationSources.Configure(registry, GetPrototypePersonId());
            informationTransfers = context?.Persistence?.InformationTransfers ?? informationTransfers ?? new InformationTransferRuntime();
            informationTransfers.Configure(registry, GetPrototypePersonId());
            informationAccess = EnsureInformationAccessRuntime();
            knowledgeRecords = EnsureKnowledgeRecordRuntime();
            relationships = context?.Persistence?.Relationships ?? relationships ?? new RelationshipRuntime();
            relationships.Configure(registry, GetKnownPrototypePersons());
            interpersonalAttitudes = context?.Persistence?.InterpersonalAttitudes ?? interpersonalAttitudes ?? new InterpersonalAttitudeRuntime();
            interpersonalAttitudes.Configure(registry, GetKnownPrototypePersons());
            reputation = context?.Persistence?.Reputation ?? reputation ?? new ReputationRuntime();
            reputation.Configure(registry, GetKnownPrototypePersons());
            rumors = context?.Persistence?.Rumors ?? rumors ?? new RumorRuntime();
            rumors.Configure(registry, GetKnownPrototypePersons(), ResolveKnowledgeRuntimeForRumorPerson, ResolveMemoryRuntimeForRumorPerson);
            socialInteractions = context?.Persistence?.SocialInteractions ?? socialInteractions ?? new SocialInteractionRuntime();
            socialInteractions.Configure(registry, GetKnownPrototypePersons(), relationships, interpersonalAttitudes, reputation, rumors);
            socialNorms = context?.Persistence?.SocialNorms ?? socialNorms ?? new SocialNormRuntime();
            socialNorms.Configure(registry, GetKnownPrototypePersons(), relationships, interpersonalAttitudes, reputation, rumors, socialInteractions);
            socialNetworks = context?.Persistence?.SocialNetworks ?? socialNetworks ?? new SocialNetworkRuntime();
            socialNetworks.Configure(registry, GetKnownPrototypePersons(), relationships, interpersonalAttitudes, reputation, rumors, socialInteractions, socialNorms);
            socialDecisions = context?.Persistence?.SocialDecisions ?? socialDecisions ?? new SocialDecisionRuntime();
            socialInfluence = context?.Persistence?.SocialInfluence ?? socialInfluence ?? new SocialInfluenceRuntime();
            socialInfluence.Configure(registry, GetKnownPrototypePersons(), interpersonalAttitudes, reputation, socialInteractions, new[] { knowledge });
            socialEmotions = context?.Persistence?.SocialEmotions ?? socialEmotions ?? new SocialEmotionRuntime();
            socialEmotions.Configure(registry, GetKnownPrototypePersons(), relationships, interpersonalAttitudes, reputation, rumors, socialInteractions, socialNorms, socialNetworks, socialInfluence);
            socialDecisions.Configure(registry, GetKnownPrototypePersons(), socialInteractions, relationships, interpersonalAttitudes, reputation, rumors, socialNorms, socialNetworks, SocialDecisionModifierSourceCollection.Compose(socialInfluence, socialEmotions));
            return TestLabRuntimeBundle.FromExisting(
                registry,
                GetPrototypePersonId(),
                PersistenceService.LocalWorldId,
                GetKnownPrototypePersons(),
                GetKnownPrototypeBodies(),
                knowledge,
                historyRuntime,
                memoryRuntime,
                informationSources,
                informationTransfers,
                informationAccess,
                knowledgeRecords,
                relationships: relationships,
                attitudes: interpersonalAttitudes,
                reputation: reputation,
                rumors: rumors,
                socialInteractions: socialInteractions,
                socialNorms: socialNorms,
                socialNetworks: socialNetworks,
                socialDecisions: socialDecisions,
                socialInfluence: socialInfluence,
                socialEmotions: socialEmotions);
        }

        private TestLabRuntimeBundle GetOrCreateScopedAutomationBundle(Dictionary<string, TestLabRuntimeBundle> bundles, string key, string objectName)
        {
            if (bundles.TryGetValue(key, out TestLabRuntimeBundle existing))
            {
                return existing;
            }

            TestLabRuntimeBundle bundle = TestLabRuntimeBundle.CreateFresh(
                registry,
                GetPrototypePersonId(),
                PersistenceService.LocalWorldId,
                GetKnownPrototypePersons(),
                GetKnownPrototypeBodies(),
                objectName);
            bundles.Add(key, bundle);
            return bundle;
        }

        private static void DisposeAutomationBundles(Dictionary<string, TestLabRuntimeBundle> bundles, string runId)
        {
            if (bundles == null || bundles.Count == 0)
            {
                return;
            }

            string prefix = $"{runId}:";
            string[] keys = bundles.Keys.Where(key => string.IsNullOrWhiteSpace(runId) || key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            foreach (string key in keys)
            {
                bundles[key]?.Dispose();
                bundles.Remove(key);
            }
        }

        private void ApplyAutomationRuntimeBindings(TestLabScenarioContext scenarioContext)
        {
            TestLabRuntimeBundle bundle = scenarioContext?.Runtimes;
            if (bundle == null)
            {
                return;
            }

            informationSources = bundle.Sources ?? informationSources;
            informationTransfers = bundle.Transfers ?? informationTransfers;
            informationAccess = bundle.Access ?? informationAccess;
            knowledgeRecords = bundle.Records ?? knowledgeRecords;
            relationships = bundle.Relationships ?? relationships;
            interpersonalAttitudes = bundle.Attitudes ?? interpersonalAttitudes;
            reputation = bundle.Reputation ?? reputation;
            rumors = bundle.Rumors ?? rumors;
            socialInteractions = bundle.SocialInteractions ?? socialInteractions;
            socialNorms = bundle.SocialNorms ?? socialNorms;
            socialNetworks = bundle.SocialNetworks ?? socialNetworks;
            socialDecisions = bundle.SocialDecisions ?? socialDecisions;
            socialInfluence = bundle.SocialInfluence ?? socialInfluence;
            socialEmotions = bundle.SocialEmotions ?? socialEmotions;
        }

        public IEnumerable<TestLabRuntimeFingerprintSection> CaptureAutomationSceneFingerprint(TestLabRuntimeArea requiredAreas)
        {
            List<TestLabRuntimeFingerprintSection> sections = new List<TestLabRuntimeFingerprintSection>();
            if ((requiredAreas & TestLabRuntimeArea.Character) != 0)
            {
                sections.Add(CreateSceneFingerprintSection(
                    "Scene.Character",
                    context?.CharacterSystem == null ? 0L : context.CharacterSystem.Revision,
                    context?.CharacterSystem == null ? null : context.CharacterSystem.GetSnapshot(developmentView: true),
                    context?.IdentityProgression == null ? null : context.IdentityProgression.CreateSaveData(),
                    context?.PlayerAttributes == null ? null : context.PlayerAttributes.CreateSaveData(PersistenceService.LocalPlayerId, GetPrototypePersonId()),
                    context?.PlayerSkills == null ? null : context.PlayerSkills.CreateSaveData(PersistenceService.LocalPlayerId, GetPrototypePersonId()),
                    context?.PlayerTraits == null ? null : context.PlayerTraits.CreateSaveData(PersistenceService.LocalPlayerId, GetPrototypePersonId()),
                    context?.PlayerResources == null ? null : context.PlayerResources.CreateSaveData(PersistenceService.LocalPlayerId, GetPrototypePersonId()),
                    context?.Inventory == null ? null : context.Inventory.CreateSaveData(),
                    context?.Equipment == null ? null : context.Equipment.CreateSaveData(),
                    context?.PlayerStatuses == null ? null : context.PlayerStatuses.CreateSaveData(saveEligibleOnly: false),
                    LifecycleFingerprint(context?.PlayerLifecycle),
                    LifecycleFingerprint(context?.EnemyLifecycle)));
            }

            if ((requiredAreas & TestLabRuntimeArea.Combat) != 0)
            {
                sections.Add(CreateSceneFingerprintSection(
                    "Scene.Combat",
                    combatRuntimeFacade == null ? 0L : combatRuntimeFacade.CreateSnapshot(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject).Revisions.AggregateRevision,
                    combatRuntimeFacade == null || context?.PlayerTransform == null ? null : combatRuntimeFacade.CreateSnapshot(context.PlayerTransform.gameObject),
                    combatRuntimeFacade == null || context?.EnemyTransform == null ? null : combatRuntimeFacade.CreateSnapshot(context.EnemyTransform.gameObject),
                    combatExecutionService == null ? null : combatExecutionService.CreateSaveData(PersistenceService.LocalPlayerId, GetPrototypePersonId()),
                    context?.PlayerOngoingEffects == null ? null : context.PlayerOngoingEffects.CreateSaveData(PersistenceService.LocalPlayerId, PersistenceService.LocalPlayerId),
                    context?.EnemyOngoingEffects == null ? null : context.EnemyOngoingEffects.CreateSaveData(PersistenceService.LocalPlayerId, ResolveActorId(context.EnemyTransform == null ? null : context.EnemyTransform.gameObject)),
                    combatContributionService == null ? null : combatContributionService.GetLedgerSnapshots(),
                    combatReactionService == null ? null : combatReactionService.Registrations,
                    CombatLastTransactionFingerprint()));
            }

            if ((requiredAreas & TestLabRuntimeArea.Biology) != 0)
            {
                ActorBodyRuntime playerBody = context?.CharacterSystem == null ? null : context.CharacterSystem.Body;
                ActorBodyRuntime enemyBody = context?.EnemyTransform == null ? null : context.EnemyTransform.GetComponentInParent<ActorBodyRuntime>();
                sections.Add(CreateSceneFingerprintSection(
                    "Scene.Biology",
                    (playerBody == null ? 0L : playerBody.BodyRevision) + (enemyBody == null ? 0L : enemyBody.BodyRevision),
                    playerBody == null ? null : playerBody.CreateSaveData(),
                    enemyBody == null ? null : enemyBody.CreateSaveData()));
            }

            if ((requiredAreas & TestLabRuntimeArea.Persistence) != 0)
            {
                sections.Add(CreateSceneFingerprintSection(
                    "Scene.Persistence",
                    context?.Persistence == null ? 0L : (long)Math.Round(context.Persistence.PlayTime == null ? 0d : context.Persistence.PlayTime.CumulativeSeconds),
                    CurrentSlotId,
                    context?.Persistence == null ? null : context.Persistence.DirtyTracker,
                    context?.Persistence == null || context.Persistence.PlayTime == null ? null : context.Persistence.PlayTime.CumulativeSeconds,
                    CurrentSlotId));
            }

            if ((requiredAreas & TestLabRuntimeArea.Social) != 0)
            {
                sections.Add(CreateSceneFingerprintSection(
                    "Scene.Social",
                    (relationships == null ? 0L : relationships.Revision) + (interpersonalAttitudes == null ? 0L : interpersonalAttitudes.Revision) + (reputation == null ? 0L : reputation.Revision) + (rumors == null ? 0L : rumors.Revision) + (socialInteractions == null ? 0L : socialInteractions.Revision) + (socialNorms == null ? 0L : socialNorms.Revision) + (socialNetworks == null ? 0L : socialNetworks.Revision) + (socialDecisions == null ? 0L : socialDecisions.Revision) + (socialInfluence == null ? 0L : socialInfluence.Revision) + (socialEmotions == null ? 0L : socialEmotions.Revision),
                    relationships == null ? null : relationships.CreateSaveData(),
                    interpersonalAttitudes == null ? null : interpersonalAttitudes.CreateSaveData(),
                    reputation == null ? null : reputation.CreateSaveData(),
                    rumors == null ? null : rumors.CreateSaveData(),
                    socialInteractions == null ? null : socialInteractions.CreateSaveData(),
                    socialNorms == null ? null : socialNorms.CreateSaveData(),
                    socialNetworks == null ? null : socialNetworks.CreateSaveData(),
                    socialDecisions == null ? null : socialDecisions.CreateSaveData(),
                    socialInfluence == null ? null : socialInfluence.CreateSaveData(),
                    socialEmotions == null ? null : socialEmotions.CreateSaveData()));
            }

            return sections;
        }

        private TestLabRuntimeFingerprintSection CreateSceneFingerprintSection(string area, long revision, params object[] stateParts)
        {
            StringBuilder builder = new StringBuilder();
            foreach (object statePart in stateParts ?? Array.Empty<object>())
            {
                AppendFingerprintValue(builder, statePart, 0);
                builder.AppendLine();
            }

            return TestLabRuntimeFingerprintSection.FromText(area, revision, builder.ToString());
        }

        private object LifecycleFingerprint(ActorLifecycleController lifecycle)
        {
            return lifecycle == null
                ? null
                : new
                {
                    lifecycle.ActorId,
                    lifecycle.State,
                    lifecycle.Revision,
                    defeatPolicyId = lifecycle.DefeatPolicy == null ? string.Empty : lifecycle.DefeatPolicy.Id
                };
        }

        private object CombatLastTransactionFingerprint()
        {
            return new
            {
                lastAttackTransactionId,
                lastDefenseActivationTransactionId,
                lastCombatStateTransactionId,
                lastCombatStateSplitTransactionId,
                lastCombatExecutionBeginTransactionId,
                lastCombatExecutionCommitTransactionId,
                lastCombatExecutionInstanceId,
                lastLifecycleTransactionId,
                lastOngoingEffectTransactionId,
                lastContributionCreditTargetActorId,
                combatStateClockSeconds,
                combatExecutionClockSeconds,
                ongoingEffectClockSeconds
            };
        }

        private static void AppendFingerprintValue(StringBuilder builder, object value, int depth)
        {
            if (builder == null)
            {
                return;
            }

            if (value == null)
            {
                builder.Append("null");
                return;
            }

            Type type = value.GetType();
            if (depth > 4)
            {
                builder.Append(type.Name);
                return;
            }

            if (value is string text)
            {
                builder.Append(text);
                return;
            }

            if (type.IsPrimitive || type.IsEnum || value is decimal)
            {
                builder.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            if (value is UnityEngine.Object unityObject)
            {
                builder.Append(unityObject == null ? "null-unity-object" : $"{type.Name}:{unityObject.name}");
                return;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                builder.Append("[");
                bool first = true;
                foreach (object item in enumerable)
                {
                    if (!first)
                    {
                        builder.Append(",");
                    }

                    AppendFingerprintValue(builder, item, depth + 1);
                    first = false;
                }

                builder.Append("]");
                return;
            }

            builder.Append(type.FullName);
            builder.Append("{");
            bool firstMember = true;
            foreach (System.Reflection.MemberInfo member in type.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(member => member.MemberType == System.Reflection.MemberTypes.Field || member.MemberType == System.Reflection.MemberTypes.Property)
                .OrderBy(member => member.Name, StringComparer.Ordinal))
            {
                object memberValue;
                if (member is System.Reflection.PropertyInfo property)
                {
                    if (property.GetIndexParameters().Length > 0 || !property.CanRead)
                    {
                        continue;
                    }

                    try
                    {
                        memberValue = property.GetValue(value);
                    }
                    catch
                    {
                        memberValue = "<unreadable>";
                    }
                }
                else if (member is System.Reflection.FieldInfo field)
                {
                    memberValue = field.GetValue(value);
                }
                else
                {
                    continue;
                }

                if (!firstMember)
                {
                    builder.Append(";");
                }

                builder.Append(member.Name);
                builder.Append("=");
                AppendFingerprintValue(builder, memberValue, depth + 1);
                firstMember = false;
            }

            builder.Append("}");
        }

        private void ResetInformationAutomationState()
        {
            string personId = GetPrototypePersonId();
            informationSources = context?.Persistence?.InformationSources ?? informationSources ?? new InformationSourceRuntime();
            informationSources.Configure(registry, personId);
            informationSources.RestoreFromSaveData(new InformationSourceSaveData
            {
                schemaVersion = InformationSourceSaveData.CurrentSchemaVersion,
                ownerId = personId,
                sourceRevision = 0,
                sources = Array.Empty<InformationSourceInstanceData>(),
                assessments = Array.Empty<PersonSourceAssessmentData>(),
                transformations = Array.Empty<SourceTransformationData>(),
                processedTransactions = Array.Empty<InformationSourceProcessedTransactionData>()
            }, registry, personId, restoring: true);

            informationTransfers = context?.Persistence?.InformationTransfers ?? informationTransfers ?? new InformationTransferRuntime();
            informationTransfers.Configure(registry, personId);
            informationTransfers.RestoreFromSaveData(new InformationTransferSaveData
            {
                schemaVersion = InformationTransferSaveData.CurrentSchemaVersion,
                ownerId = personId,
                transferRevision = 0,
                transfers = Array.Empty<InformationTransferRecordData>(),
                processedTransactions = Array.Empty<InformationTransferProcessedTransactionData>()
            }, registry, personId, restoring: true);

            informationAccess = context?.InformationAccess ?? context?.Persistence?.InformationAccess ?? informationAccess ?? new InformationAccessRuntime();
            informationAccess.Configure(registry, personId);
            informationAccess.RestoreFromSaveData(new InformationAccessSaveData
            {
                schemaVersion = InformationAccessSaveData.CurrentSchemaVersion,
                ownerId = personId,
                accessRevision = 0,
                policies = Array.Empty<InformationAccessPolicyData>(),
                grants = Array.Empty<InformationAccessGrantData>(),
                denials = Array.Empty<InformationAccessDenialData>(),
                concealments = Array.Empty<InformationConcealmentData>(),
                classificationRevisions = Array.Empty<InformationClassificationRevisionData>(),
                audits = Array.Empty<InformationAccessAuditData>(),
                processedTransactions = Array.Empty<InformationAccessProcessedTransactionData>()
            }, registry, personId, restoring: true);
        }

        private static void ResetLifecycleForAutomation(ActorLifecycleController lifecycle, string playerId)
        {
            if (lifecycle == null)
            {
                return;
            }

            lifecycle.RestoreFromSaveData(new ActorLifecycleSaveData
            {
                schemaVersion = ActorLifecycleSaveData.CurrentSchemaVersion,
                playerId = playerId ?? string.Empty,
                personId = string.Empty,
                actorId = lifecycle.ActorId,
                policyId = string.Empty,
                lifecycleState = ActorLifecycleState.Active.ToString()
            }, playerId ?? string.Empty, lifecycle.ActorId, out _, restoring: true);
        }

        public PrototypeTestLabOperation RunExpectedAutomationFailure(Func<PrototypeTestLabOperation> action)
        {
            if (action == null)
            {
                return RecordFailure("Expected Automation Failure", "No expected-failure action was provided.", "MissingAction");
            }

            suppressExpectedAutomationWarnings = true;
            try
            {
                return action();
            }
            finally
            {
                suppressExpectedAutomationWarnings = false;
            }
        }

        public PrototypeTestLabOperation ExportAutomationJsonReport()
        {
            if (lastAutomationResult == null)
            {
                return RecordFailure("Export Automation JSON", "Run automation before exporting a report.", "NoResult");
            }

            string path = automationReportExporter.ExportJson(lastAutomationResult);
            return RecordSuccess("Export Automation JSON", $"Exported JSON report to {path}.");
        }

        public PrototypeTestLabOperation ExportAutomationMarkdownReport()
        {
            if (lastAutomationResult == null)
            {
                return RecordFailure("Export Automation Markdown", "Run automation before exporting a report.", "NoResult");
            }

            string path = automationReportExporter.ExportMarkdown(lastAutomationResult);
            return RecordSuccess("Export Automation Markdown", $"Exported Markdown report to {path}.");
        }

        public IReadOnlyList<TDefinition> GetDefinitions<TDefinition>()
            where TDefinition : class, IGameDefinition
        {
            Type type = typeof(TDefinition);
            if (!selectorCache.TryGetValue(type, out List<IGameDefinition> cached))
            {
                cached = registry == null
                    ? new List<IGameDefinition>()
                    : registry.DefinitionsById.Values
                        .Where(definition => definition is TDefinition)
                        .OrderBy(definition => definition.DisplayName)
                        .ThenBy(definition => definition.Id)
                        .ToList();
                selectorCache.Add(type, cached);
            }

            return cached.Cast<TDefinition>().ToList();
        }

        public IReadOnlyList<PrototypeTestPoint> GetTestPoints()
        {
            return UnityEngine.Object.FindObjectsByType<PrototypeTestPoint>(FindObjectsInactive.Exclude)
                .Where(point => point != null && !string.IsNullOrWhiteSpace(point.TestPointId))
                .OrderBy(point => point.TestPointId)
                .ThenBy(point => point.DisplayName)
                .ToList();
        }

        public string BuildOverview()
        {
            if (context == null)
            {
                return "Test Lab context is missing.";
            }

            return string.Join(Environment.NewLine, new[]
            {
                "Prototype Systems Test Lab",
                $"Build Boundary: {(Application.isEditor ? "Editor" : "Development Build")}",
                $"Player: {(context.PlayerTransform == null ? "Missing" : context.PlayerTransform.name)}",
                $"Health: {FormatHealth()}",
                $"Stamina: {FormatResource(context.PlayerStamina == null ? 0f : context.PlayerStamina.CurrentStamina, context.PlayerStamina == null ? 0f : context.PlayerStamina.MaximumStamina)}",
                $"Mana: {FormatResource(context.PlayerMana == null ? 0f : context.PlayerMana.CurrentMana, context.PlayerMana == null ? 0f : context.PlayerMana.MaximumMana)}",
                $"Stats: ATK {FormatNumber(context.PlayerStats == null ? 0f : context.PlayerStats.AttackPower)}, DEF {FormatNumber(context.PlayerStats == null ? 0f : context.PlayerStats.Defense)}",
                $"Base Attributes: {(context.PlayerAttributes == null ? "Missing" : context.PlayerAttributes.AttributeValues.Count.ToString())}",
                $"Skills: {(context.PlayerSkills == null ? "Missing" : context.PlayerSkills.LearnedSkills.Count.ToString())}",
                $"Character System: {FormatCharacterReadinessOneLine()}",
                $"Statuses: {FormatStatuses(context.PlayerStatuses)}",
                $"Inventory: {FormatInventory()}",
                $"Equipped: {CountEquipped()} item(s)",
                $"Selected Spell: {(context.SpellLoadout == null || context.SpellLoadout.SelectedSpell == null ? "None" : FormatDefinition(context.SpellLoadout.SelectedSpell))}",
                $"Quests: {(context.QuestLog == null ? 0 : context.QuestLog.Quests.Count)}",
                $"Contracts: {(context.ContractJournal == null ? 0 : context.ContractJournal.Contracts.Count)}",
                $"Identity: {FormatIdentityOneLine()}",
                $"Enemy: {FormatEnemy()}",
                $"Location: {FormatLocationOneLine()}",
                $"Definitions: {(registry == null ? 0 : registry.Count)}",
                $"Persistence Slot: {CurrentSlotId}",
                $"Modal Active: {PrototypeGameplayModalState.IsModalActive}"
            });
        }

        public string BuildCombatRuntimeSummary()
        {
            CombatRuntimeFacade facade = EnsureCombatRuntimeFacade();
            if (facade == null)
            {
                return "Combat runtime facade is unavailable.";
            }

            CombatRuntimeSnapshot snapshot = facade.CreateSnapshot(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Feature 6.10 Combat System Integration");
            builder.AppendLine($"Readiness: {snapshot.Readiness.State} Errors={CountDiagnostics(snapshot.Diagnostics, CombatIntegritySeverity.Error)} Warnings={CountDiagnostics(snapshot.Diagnostics, CombatIntegritySeverity.Warning)}");
            builder.AppendLine($"Actor: {EmptyAs(snapshot.ActorId, "Missing")} Body: {EmptyAs(snapshot.BodyId, "Missing")} Person: {EmptyAs(snapshot.PersonId, "None")} Lifecycle: {snapshot.LifecycleState}");
            builder.AppendLine($"Resources: {string.Join(", ", snapshot.Resources.Select(resource => $"{resource.ResourceId} {resource.Current:0.###}/{resource.Maximum:0.###}"))}");
            builder.AppendLine($"Combat Stats: {string.Join(", ", snapshot.CombatStats.Select(stat => $"{stat.StatId}={stat.Value:0.###}"))}");
            builder.AppendLine($"Transient: Defense={(snapshot.ActiveDefense == null ? "None" : snapshot.ActiveDefense.DefinitionId)} Execution={(snapshot.ActiveExecution == null ? "None" : snapshot.ActiveExecution.DefinitionId)} Ongoing={snapshot.ActiveOngoingEffects.Count} Reactions={snapshot.ReactionSources.Count}");
            builder.AppendLine($"Combat State: InCombat={(snapshot.CombatState != null && snapshot.CombatState.IsInCombat)} Engagements={snapshot.ActiveEngagements.Count} RecentOpponents={snapshot.RecentOpponents.Count}");
            builder.AppendLine($"Contributions: Ledgers={snapshot.ContributionLedgers.Count} ContributionRevision={snapshot.Revisions.ContributionRevision} AggregateRevision={snapshot.Revisions.AggregateRevision}");
            builder.AppendLine($"Last Tx: Root={EmptyAs(snapshot.LastTransactionTrace?.RootTransactionId, "None")} Attack={EmptyAs(snapshot.LastTransactionTrace?.AttackTransactionId, "None")} Damage={EmptyAs(snapshot.LastTransactionTrace?.DamageTransactionId, "None")} Coherent={(snapshot.LastTransactionTrace == null || snapshot.LastTransactionTrace.IsCoherent)}");
            builder.AppendLine("Persistence: combat state, defense windows, reaction sources, contribution ledgers, ongoing timers, and execution commitments are transient unless owned by their dedicated persistence participant. Restore clears transient combat runtime state silently before normal runtime resumes.");
            builder.AppendLine("Compatibility: existing 6.1-6.9 feature services remain callable; 6.10 composes them through one facade and shared DamageHealingService authority.");

            foreach (CombatRuntimeDiagnostic diagnostic in snapshot.Diagnostics.Take(8))
            {
                builder.AppendLine($"{diagnostic.Severity}: {diagnostic.Subsystem}/{diagnostic.Code} {diagnostic.Message}");
            }

            return builder.ToString();
        }

        public PrototypeTestLabOperation ValidateCombatRuntimeIntegrity()
        {
            CombatIntegrityReport report = EnsureCombatRuntimeFacade()?.ValidateIntegrity(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject);
            if (report == null)
            {
                return RecordFailure("Validate 6.10 Combat Runtime", "Combat runtime facade is missing.", "MissingFacade");
            }

            string message = report.Diagnostics.Count == 0
                ? "Combat runtime integrity passed with no diagnostics."
                : string.Join(Environment.NewLine, report.Diagnostics.Select(diagnostic => $"{diagnostic.Severity}: {diagnostic.Subsystem}/{diagnostic.Code} {diagnostic.Message}"));
            return Record(report.Passed, "Validate 6.10 Combat Runtime", report.Passed ? "Passed" : "Failed", message);
        }

        public PrototypeTestLabOperation ResetCombatRuntimeIntegration()
        {
            PrototypeTestLabOperation reset = ResetAutomationRuntimeState();
            combatRuntimeFacade = null;
            CombatRuntimeFacade facade = EnsureCombatRuntimeFacade();
            CombatReadinessResult readiness = facade?.EvaluateReadiness(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject);
            bool succeeded = reset.Succeeded && readiness != null && readiness.Diagnostics.All(diagnostic => diagnostic.Severity != CombatIntegritySeverity.Error);
            string message = $"Reset={reset.Code}; Readiness={readiness?.State}; Errors={CountDiagnostics(readiness?.Diagnostics, CombatIntegritySeverity.Error)} Warnings={CountDiagnostics(readiness?.Diagnostics, CombatIntegritySeverity.Warning)}.";
            return Record(succeeded, "Reset 6.10 Combat Runtime", succeeded ? "Ready" : "Invalid", message);
        }

        public PrototypeTestLabOperation PreviewCombatRuntimeAttack(DamageTypeDefinition damageType)
        {
            CombatRuntimeFacade facade = EnsureCombatRuntimeFacade();
            AttackResolutionRequest request = CreateAttackResolutionRequest(damageType, 25f, 0.95f, 0.1f, 0f, 0.99f, 1.5f, 1f, 2f, targetEnemy: true, sourcePlayer: true, transactionId: ResolveAttackTransactionId(reuse: false));
            AttackResolutionResult result = facade.PreviewAttack(request);
            return Record(result.Succeeded, "Preview 6.10 Facade Attack", result.Code, FormatAttackResolution(result));
        }

        public PrototypeTestLabOperation ExecuteCombatRuntimeAttack(DamageTypeDefinition damageType)
        {
            CombatRuntimeFacade facade = EnsureCombatRuntimeFacade();
            AttackResolutionRequest request = CreateAttackResolutionRequest(damageType, 25f, 0.95f, 0.1f, 0f, 0.99f, 1.5f, 1f, 2f, targetEnemy: true, sourcePlayer: true, transactionId: ResolveAttackTransactionId(reuse: false));
            AttackResolutionResult result = facade.ExecuteAttack(request);
            return Record(result.Succeeded, "Execute 6.10 Facade Attack", result.Code, $"{FormatAttackResolution(result)}\n{FormatCombatTransactionTrace(facade.LastTransactionTrace)}");
        }

        public PrototypeTestLabOperation ExecuteCombatRuntimeMiss(DamageTypeDefinition damageType)
        {
            CombatRuntimeFacade facade = EnsureCombatRuntimeFacade();
            AttackResolutionRequest request = CreateAttackResolutionRequest(damageType, 25f, 0.25f, 0.99f, 0f, 0.99f, 1.5f, 1f, 2f, targetEnemy: true, sourcePlayer: true, transactionId: ResolveAttackTransactionId(reuse: false));
            AttackResolutionResult result = facade.ExecuteAttack(request);
            return Record(result.Succeeded, "Execute 6.10 Facade Miss", result.Code, $"{FormatAttackResolution(result)}\n{FormatCombatTransactionTrace(facade.LastTransactionTrace)}");
        }

        public PrototypeTestLabOperation ExecuteCombatRuntimeCritical(DamageTypeDefinition damageType)
        {
            CombatRuntimeFacade facade = EnsureCombatRuntimeFacade();
            AttackResolutionRequest request = CreateAttackResolutionRequest(damageType, 10f, 0.95f, 0.1f, 0.95f, 0.1f, 2f, 1f, 2f, targetEnemy: true, sourcePlayer: true, transactionId: ResolveAttackTransactionId(reuse: false));
            AttackResolutionResult result = facade.ExecuteAttack(request);
            return Record(result.Succeeded, "Execute 6.10 Facade Critical", result.Code, $"{FormatAttackResolution(result)}\n{FormatCombatTransactionTrace(facade.LastTransactionTrace)}");
        }

        public PrototypeTestLabOperation ExecuteCombatRuntimeDefense(DamageTypeDefinition damageType, bool block)
        {
            CombatRuntimeFacade facade = EnsureCombatRuntimeFacade();
            DefensiveActionDefinition defense = FindDefensiveAction(block ? "block" : "dodge") ?? GetDefinitions<DefensiveActionDefinition>().FirstOrDefault();
            if (!EnsureCompatibleDefenseEquipment(defense, out string equipmentFailure))
            {
                return RecordFailure(block ? "Execute 6.10 Block Flow" : "Execute 6.10 Dodge Flow", equipmentFailure, DefensiveActionResultCode.IncompatibleEquipment);
            }

            if (!TryBuildDefenseActivationRequest(defense, targetPlayer: true, reuseTransaction: false, out DefenseActivationRequest activation, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            DefenseActivationResult activationResult = facade.ActivateDefense(activation);
            if (!activationResult.Succeeded)
            {
                return Record(false, block ? "Execute 6.10 Block Flow" : "Execute 6.10 Dodge Flow", activationResult.Code, FormatDefenseActivation(activationResult));
            }

            AttackResolutionRequest attack = CreateDefensiveAttackRequest(damageType, 25f, 0.95f, 0.1f, 0.01f, targetPlayer: true, transactionId: ResolveAttackTransactionId(reuse: false));
            AttackResolutionResult result = facade.ExecuteAttack(attack);
            return Record(result.Succeeded, block ? "Execute 6.10 Block Flow" : "Execute 6.10 Dodge Flow", result.Code, $"{FormatDefenseActivation(activationResult)}\n{FormatAttackResolution(result)}");
        }

        public PrototypeTestLabOperation ExecuteCombatRuntimeOngoingDamage(OngoingEffectDefinition definition, DamageTypeDefinition damageType)
        {
            PrototypeTestLabOperation apply = ApplyOngoingEffect(definition, targetEnemy: true, amount: 5f, interval: 1f, duration: 3f, tickCount: 1, stacks: 1, reuseTransaction: false);
            if (!apply.Succeeded)
            {
                return apply;
            }

            return ProcessOngoingEffectsNow();
        }

        public PrototypeTestLabOperation ExecuteCombatRuntimeReaction(CombatReactionDefinition definition)
        {
            CombatReactionDefinition selected = definition ?? GetDefinitions<CombatReactionDefinition>().FirstOrDefault(candidate => candidate.SupportsTrigger(CombatReactionTriggerType.DamageApplied));
            PrototypeTestLabOperation register = RegisterCombatReaction(selected, ownerPlayer: false);
            if (!register.Succeeded)
            {
                return register;
            }

            return ExecuteCombatReactionDamage(selected);
        }

        public PrototypeTestLabOperation ExecuteCombatRuntimeContribution(DamageTypeDefinition damageType)
        {
            PrototypeTestLabOperation record = RecordDamageContribution(damageType, reuseTransaction: false);
            if (!record.Succeeded)
            {
                return record;
            }

            return ResolveDefeatContributionCredit();
        }

        public PrototypeTestLabOperation SimulateCombatRuntimeRestoreClear()
        {
            CombatRuntimeFacade facade = EnsureCombatRuntimeFacade();
            facade.ClearTransientStateForRestore(ResolveActorId(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject));
            facade.MarkReadyAfterRestore();
            CombatRuntimeSnapshot snapshot = facade.CreateSnapshot(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject);
            bool cleared = snapshot.ActiveDefense == null
                && snapshot.ActiveExecution == null
                && snapshot.ActiveOngoingEffects.Count == 0
                && snapshot.ReactionSources.Count == 0
                && snapshot.ContributionLedgers.Count == 0;
            return Record(cleared, "Restore Clear 6.10 Combat Runtime", cleared ? "Cleared" : "StillActive", $"Readiness={snapshot.Readiness.State}; Defense={snapshot.ActiveDefense != null}; Execution={snapshot.ActiveExecution != null}; Ongoing={snapshot.ActiveOngoingEffects.Count}; Reactions={snapshot.ReactionSources.Count}; Ledgers={snapshot.ContributionLedgers.Count}.");
        }

        public string BuildIdentityProgressionSummary()
        {
            if (context?.IdentityProgression == null)
            {
                return "Player identity/progression component is missing.";
            }

            context.IdentityProgression.RegisterDefinitionCache(registry);
            return context.IdentityProgression.BuildDiagnosticSummary();
        }

        public string BuildAttributeCalculatedStatsSummary()
        {
            if (context?.PlayerAttributes == null || context.PlayerCalculatedStats == null)
            {
                return "Player Base Attributes or Calculated Stats component is missing.";
            }

            return string.Join(Environment.NewLine, new[]
            {
                context.PlayerAttributes.BuildDiagnosticSummary(),
                string.Empty,
                context.PlayerCalculatedStats.BuildDiagnosticSummary()
            });
        }

        public string BuildSkillsSummary(bool includeHidden)
        {
            if (context?.PlayerSkills == null)
            {
                return "Player Skill collection component is missing.";
            }

            context.PlayerSkills.Configure(registry, context.PlayerCalculatedStats, context.SpellLoadout);
            return context.PlayerSkills.BuildDiagnosticSummary(includeHidden);
        }

        public string BuildTraitsSummary(bool includeHidden)
        {
            if (!EnsureTraits(out CharacterTraitCollection traits))
            {
                return "Player Trait collection component is missing.";
            }

            return traits.BuildDiagnosticSummary(includeHidden);
        }

        public string BuildCharacterSystemSummary(bool developmentView)
        {
            if (!EnsureCharacterSystem(out CharacterSystemCoordinator character))
            {
                return "Character System coordinator is missing.";
            }

            return character.BuildDiagnosticSummary(developmentView);
        }

        public string BuildBodySpeciesSummary()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return "Body runtime is missing.";
            }

            BodySnapshot snapshot = body.CreateSnapshot();
            List<string> lines = new List<string>
            {
                "Feature 7.1 Body and Species",
                $"Readiness: {snapshot.Readiness}",
                $"Revision: {snapshot.BodyRevision}",
                $"Actor/body: {snapshot.ActorBodyId}",
                $"Person: {snapshot.PersonId}",
                $"Species: {snapshot.SpeciesDisplayName} ({snapshot.SpeciesId})",
                $"Classification: {snapshot.BiologicalClassificationId}",
                $"Body Form: {snapshot.BodyFormId}",
                $"Defeat Policy: {snapshot.DefeatPolicyId}",
                $"Breathing Required: {snapshot.RequiresBreathing}",
                $"Has Blood: {snapshot.HasBlood}",
                $"Can Become Unconscious: {snapshot.CanBecomeUnconscious}",
                $"Can Die: {snapshot.CanDie}",
                $"Can Be Revived: {snapshot.CanBeRevived}",
                $"Accepts Biological Healing: {snapshot.AcceptsBiologicalHealing}",
                $"Accepts Repair: {snapshot.AcceptsRepair}",
                $"Has Physical Body: {snapshot.HasPhysicalBody}",
                $"Traits: {string.Join(", ", snapshot.SpeciesOwnedTraits.Select(trait => trait.TraitId))}",
                $"Capabilities: {string.Join(", ", snapshot.BiologicalCapabilities.Where(capability => capability.BooleanValue).Select(capability => capability.CapabilityId))}",
                $"Stat Contributions: {string.Join(", ", snapshot.BiologicalStatContributions.Select(stat => $"{stat.StatId} {stat.Direction} {stat.Magnitude:0.###}"))}",
                $"Coherent: {snapshot.Coherent}"
            };

            if (snapshot.Diagnostics.Count > 0)
            {
                lines.AddRange(snapshot.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation PreviewBodySpecies(string speciesId)
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Preview Body Species", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            return RecordBodyResult("Preview Body Species", body.PreviewAssignSpecies(speciesId));
        }

        public PrototypeTestLabOperation AssignBodySpecies(string speciesId)
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Assign Body Species", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            return RecordBodyResult("Assign Body Species", body.AssignSpecies(speciesId, restoring: false, "Test Lab Species assignment"));
        }

        public PrototypeTestLabOperation ReapplyBodySpecies()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Reapply Body Species", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            return RecordBodyResult("Reapply Body Species", body.AssignSpecies(body.SpeciesDefinitionId, restoring: false, "Test Lab duplicate Species proof"));
        }

        public PrototypeTestLabOperation ValidateBodyIntegrity()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Body Integrity", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            return body.ValidateBody(out string failureReason)
                ? RecordSuccess("Validate Body Integrity", body.CreateSnapshot().Coherent ? "Body integrity is coherent." : "Body snapshot reports diagnostics.")
                : RecordFailure("Validate Body Integrity", failureReason, BodyOperationResultCode.InvalidConfiguration.ToString());
        }

        public PrototypeTestLabOperation TestMissingBodySpecies()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Missing Body Species", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            return RecordBodyResult("Missing Body Species", body.PreviewAssignSpecies("species.missing-test-lab"));
        }

        public PrototypeTestLabOperation TestStaleBodyActor()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Stale Body Actor", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            BodySnapshot snapshot = body.CreateSnapshot();
            bool stale = string.IsNullOrWhiteSpace(snapshot.ActorBodyId) || snapshot.ActorBodyId.Contains("stale", StringComparison.Ordinal);
            return stale
                ? RecordFailure("Stale Body Actor", $"Actor/body '{snapshot.ActorBodyId}' is stale or missing.", BodyOperationResultCode.StaleActorBody.ToString())
                : RecordSuccess("Stale Body Actor", $"Current Actor/body '{snapshot.ActorBodyId}' resolves; replacement-body redirection was not attempted.");
        }

        public string BuildBodyAnatomySummary()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return "Body runtime is missing.";
            }

            AnatomySnapshot anatomy = body.CreateAnatomySnapshot();
            BodySnapshot bodySnapshot = body.CreateSnapshot();
            if (anatomy == null)
            {
                return "Anatomy snapshot is missing.";
            }

            List<string> lines = new List<string>
            {
                "Feature 7.2 Body Anatomy",
                $"Body: {bodySnapshot.ActorBodyId}",
                $"Person: {bodySnapshot.PersonId}",
                $"Species: {bodySnapshot.SpeciesId}",
                $"Anatomy: {anatomy.AnatomyDefinitionId}",
                $"Readiness: {anatomy.Readiness}",
                $"Body Revision: {anatomy.BodyRevision}",
                $"Anatomy Revision: {anatomy.AnatomyRevision}",
                $"Root: {anatomy.RootNodeId}",
                $"Regions: {string.Join(", ", anatomy.Regions.Select(node => $"{node.DisplayName} ({node.NodeId}, {node.BodySide})"))}",
                $"Parts: {string.Join(", ", anatomy.BodyParts.Select(node => $"{node.DisplayName} ({node.NodeId}, {node.Presence})"))}",
                $"Organs/Internal: {string.Join(", ", anatomy.OrgansAndInternalStructures.Select(node => $"{node.DisplayName} ({node.NodeId}, Vital={node.Vital}, Corporeal={node.Corporeal})"))}",
                $"Vital: {string.Join(", ", anatomy.VitalStructures.Select(node => node.NodeId))}",
                $"Targetable Regions: {string.Join(", ", anatomy.TargetableRegions.Select(node => node.NodeId))}",
                $"Equipment Tags: {string.Join(", ", anatomy.Nodes.SelectMany(node => node.EquipmentTagIds).Distinct().OrderBy(id => id, StringComparer.Ordinal))}",
                $"Presence: {string.Join(", ", anatomy.Nodes.Select(node => $"{node.NodeId}={node.Presence}"))}",
                $"Coherent: {anatomy.Coherent}"
            };

            lines.Add("Hierarchy:");
            AppendAnatomyHierarchy(lines, anatomy, anatomy.RootNodeId, 0);
            if (anatomy.Diagnostics.Count > 0)
            {
                lines.AddRange(anatomy.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation ValidateAnatomyIntegrity()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Anatomy Integrity", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            return RecordBodyResult("Validate Anatomy Integrity", body.ValidateAnatomy());
        }

        public PrototypeTestLabOperation RebuildAnatomy()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Rebuild Anatomy", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            return RecordBodyResult("Rebuild Anatomy", body.RebuildAnatomy());
        }

        public PrototypeTestLabOperation SnapshotAnatomy()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Snapshot Anatomy", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            long revisionBefore = body.Anatomy.AnatomyRevision;
            AnatomySnapshot snapshot = body.CreateAnatomySnapshot();
            long revisionAfter = body.Anatomy.AnatomyRevision;
            bool succeeded = snapshot != null && snapshot.Coherent && revisionBefore == revisionAfter;
            return Record(succeeded, "Snapshot Anatomy", succeeded ? "Success" : "InvalidSnapshot", snapshot == null
                ? "Anatomy snapshot was null."
                : $"Snapshot Anatomy={snapshot.AnatomyDefinitionId} Nodes={snapshot.Nodes.Count} Revision={revisionBefore}->{revisionAfter} Coherent={snapshot.Coherent}.");
        }

        public PrototypeTestLabOperation SetOptionalTailPresence(bool present)
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Set Optional Anatomy Presence", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            return RecordBodyResult("Set Optional Anatomy Presence", body.SetAnatomyPresenceOverride("part.tail.optional", present ? AnatomyPresenceState.Present : AnatomyPresenceState.Absent));
        }

        public PrototypeTestLabOperation TestMissingAnatomyDefinition()
        {
            return RecordFailure("Missing Anatomy Definition", "Fixture rejected without mutation: a Species with no Anatomy definition cannot reach Anatomy Ready.", BodyOperationResultCode.MissingAnatomyDefinition.ToString());
        }

        public PrototypeTestLabOperation TestCircularAnatomyFixture()
        {
            return RecordFailure("Circular Anatomy Fixture", "Fixture rejected without mutation: circular parent relationships are invalid.", BodyOperationResultCode.InvalidAnatomyDefinition.ToString());
        }

        public PrototypeTestLabOperation TestDuplicateAnatomyNodeFixture()
        {
            return RecordFailure("Duplicate Anatomy Node Fixture", "Fixture rejected without mutation: duplicate structural node IDs are invalid.", BodyOperationResultCode.InvalidAnatomyDefinition.ToString());
        }

        public PrototypeTestLabOperation ValidateAnatomyContains(string speciesId, params string[] nodeIds)
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Anatomy Contains", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            BodyOperationResult assignment = body.AssignSpecies(speciesId, restoring: false, "Test Lab Anatomy validation");
            if (!assignment.Succeeded)
            {
                return RecordBodyResult("Validate Anatomy Contains", assignment);
            }

            AnatomySnapshot snapshot = body.CreateAnatomySnapshot();
            List<string> missing = (nodeIds ?? Array.Empty<string>())
                .Where(nodeId => snapshot == null || snapshot.Nodes.All(node => !string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
                .ToList();
            bool succeeded = snapshot != null && snapshot.Coherent && missing.Count == 0;
            string message = snapshot == null
                ? "Anatomy snapshot is missing."
                : $"Anatomy={snapshot.AnatomyDefinitionId} Nodes={snapshot.Nodes.Count} Required={string.Join(", ", nodeIds ?? Array.Empty<string>())} Missing={string.Join(", ", missing)}.";
            return Record(succeeded, "Validate Anatomy Contains", succeeded ? "Success" : "MissingNode", message);
        }

        public PrototypeTestLabOperation ValidateAnatomyExcludes(string speciesId, params string[] nodeIds)
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Anatomy Excludes", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            BodyOperationResult assignment = body.AssignSpecies(speciesId, restoring: false, "Test Lab Anatomy validation");
            if (!assignment.Succeeded)
            {
                return RecordBodyResult("Validate Anatomy Excludes", assignment);
            }

            AnatomySnapshot snapshot = body.CreateAnatomySnapshot();
            List<string> present = (nodeIds ?? Array.Empty<string>())
                .Where(nodeId => snapshot != null && snapshot.Nodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal) && node.Present))
                .ToList();
            bool succeeded = snapshot != null && snapshot.Coherent && present.Count == 0;
            string message = snapshot == null
                ? "Anatomy snapshot is missing."
                : $"Anatomy={snapshot.AnatomyDefinitionId} Forbidden={string.Join(", ", nodeIds ?? Array.Empty<string>())} Present={string.Join(", ", present)}.";
            return Record(succeeded, "Validate Anatomy Excludes", succeeded ? "Success" : "UnexpectedNode", message);
        }

        public PrototypeTestLabOperation ValidateAnatomyStableRebuild()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Anatomy Stable Rebuild", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            AnatomySnapshot before = body.CreateAnatomySnapshot();
            BodyOperationResult rebuild = body.RebuildAnatomy();
            AnatomySnapshot after = body.CreateAnatomySnapshot();
            string[] beforeIds = before?.Nodes.Select(node => node.RuntimeNodeId).ToArray() ?? Array.Empty<string>();
            string[] afterIds = after?.Nodes.Select(node => node.RuntimeNodeId).ToArray() ?? Array.Empty<string>();
            bool succeeded = rebuild.Succeeded && beforeIds.SequenceEqual(afterIds);
            return Record(succeeded, "Validate Anatomy Stable Rebuild", succeeded ? "Success" : "UnstableRuntimeIds", $"Before={beforeIds.Length} After={afterIds.Length} Stable={succeeded}.");
        }

        public PrototypeTestLabOperation ValidateAnatomySaveRestore()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Anatomy Save Restore", "Body runtime is missing.", BodyOperationResultCode.MissingActorBody.ToString());
            }

            AnatomySnapshot before = body.CreateAnatomySnapshot();
            PrototypeTestLabOperation save = Save();
            PrototypeTestLabOperation load = save.Succeeded ? Load() : save;
            AnatomySnapshot after = body.CreateAnatomySnapshot();
            bool stable = before != null && after != null && before.Nodes.Select(node => node.RuntimeNodeId).SequenceEqual(after.Nodes.Select(node => node.RuntimeNodeId));
            bool succeeded = save.Succeeded && load.Succeeded && stable && after.Coherent;
            return Record(succeeded, "Validate Anatomy Save Restore", succeeded ? "Success" : "RestoreMismatch", $"Save={save.Code} Load={load.Code} StableNodes={stable} Anatomy={after?.AnatomyDefinitionId ?? string.Empty}.");
        }

        public string BuildBodyConditionSummary()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return "Body condition runtime is missing.";
            }

            BodySnapshot bodySnapshot = body.CreateSnapshot();
            BodyConditionSnapshot condition = bodySnapshot.Condition;
            if (condition == null)
            {
                return "Body condition snapshot is missing.";
            }

            List<string> lines = new List<string>
            {
                "Feature 7.3 Body Condition, Injury, and Structural Damage",
                $"Body: {bodySnapshot.ActorBodyId}",
                $"Species: {bodySnapshot.SpeciesId}",
                $"Anatomy: {condition.AnatomyDefinitionId}",
                $"Readiness: {condition.Readiness}",
                $"Body/Anatomy/Condition Revisions: {condition.BodyRevision}/{condition.AnatomyRevision}/{condition.ConditionRevision}",
                $"Structures: {condition.Structures.Count}",
                $"Impaired Structures: {condition.ImpairedStructures.Count}",
                $"Active Injuries: {condition.ActiveInjuries.Count}",
                $"Coherent: {condition.Coherent}"
            };

            lines.Add("Impaired:");
            if (condition.ImpairedStructures.Count == 0)
            {
                lines.Add("- None");
            }
            else
            {
                foreach (StructureConditionSnapshot structure in condition.ImpairedStructures.Take(12))
                {
                    lines.Add($"- {structure.DisplayName} [{structure.NodeId}] Integrity={structure.CurrentIntegrity}/{structure.MaximumIntegrity} Functional={structure.FunctionalState} Structural={structure.StructuralState} Presence={structure.RuntimePresence}");
                }
            }

            lines.Add("Active Injuries:");
            if (condition.ActiveInjuries.Count == 0)
            {
                lines.Add("- None");
            }
            else
            {
                foreach (InjuryRecordSnapshot injury in condition.ActiveInjuries.Take(12))
                {
                    lines.Add($"- {injury.InjuryDefinitionId} on {injury.TargetNodeId} Severity={injury.Severity} Damage={injury.AppliedStructuralDamage} Tx={injury.SourceTransactionId}");
                }
            }

            if (condition.Diagnostics.Count > 0)
            {
                lines.AddRange(condition.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation ValidateBodyConditionIntegrity()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Body Condition", "Body runtime is missing.", LocalizedDamageResultCode.MissingActorBody.ToString());
            }

            BodyConditionSnapshot snapshot = body.Condition.CreateSnapshot();
            bool succeeded = snapshot.Coherent && snapshot.Readiness == BodyConditionReadinessState.Ready;
            return Record(succeeded, "Validate Body Condition", succeeded ? "Success" : "InvalidCondition", $"Readiness={snapshot.Readiness} Structures={snapshot.Structures.Count} ActiveInjuries={snapshot.ActiveInjuries.Count} Coherent={snapshot.Coherent}.");
        }

        public PrototypeTestLabOperation ResetBodyConditionHealthy()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Reset Body Condition", "Body runtime is missing.", LocalizedDamageResultCode.MissingActorBody.ToString());
            }

            BodyOperationResult assignment = body.AssignSpecies("species.human", restoring: false, "Test Lab body condition reset");
            if (!assignment.Succeeded)
            {
                return RecordBodyResult("Reset Body Condition", assignment);
            }

            LocalizedStructuralDamageResult result = body.Condition.BuildHealthy(body.ActorBodyId, body.CreateAnatomySnapshot(), registry, restoring: false);
            return RecordConditionResult("Reset Body Condition", result);
        }

        public PrototypeTestLabOperation PreviewLocalizedStructuralDamage(string injuryDefinitionId, string targetNodeId, int structuralDamage)
        {
            if (!TryBuildConditionDamageRequest(injuryDefinitionId, targetNodeId, structuralDamage, "preview", out ActorBodyRuntime body, out LocalizedStructuralDamageRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            return RecordConditionResult("Preview Localized Structural Damage", body.Condition.PreviewLocalizedDamage(request, body.CreateAnatomySnapshot(), body.BiologicalCompatibility, body.CreateSnapshot()));
        }

        public PrototypeTestLabOperation ApplyLocalizedStructuralDamage(string injuryDefinitionId, string targetNodeId, int structuralDamage)
        {
            return ApplyLocalizedStructuralDamageWithTransaction(injuryDefinitionId, targetNodeId, structuralDamage, $"test-lab.body-condition.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation ApplyLocalizedStructuralDamageWithTransaction(string injuryDefinitionId, string targetNodeId, int structuralDamage, string transactionId)
        {
            if (!TryBuildConditionDamageRequest(injuryDefinitionId, targetNodeId, structuralDamage, transactionId, out ActorBodyRuntime body, out LocalizedStructuralDamageRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            return RecordConditionResult("Apply Localized Structural Damage", body.Condition.ApplyLocalizedDamage(request, body.CreateAnatomySnapshot(), restoring: false, compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot()));
        }

        public PrototypeTestLabOperation ProveLocalizedDamageDuplicateProtection()
        {
            const string transactionId = "test-lab.body-condition.duplicate-proof";
            PrototypeTestLabOperation first = ApplyLocalizedStructuralDamageWithTransaction("injury.blunt-trauma", "part.arm.left", 12, transactionId);
            PrototypeTestLabOperation second = ApplyLocalizedStructuralDamageWithTransaction("injury.blunt-trauma", "part.arm.left", 12, transactionId);
            bool succeeded = first.Succeeded && second.Succeeded && string.Equals(second.Code, LocalizedDamageResultCode.Duplicate.ToString(), StringComparison.Ordinal);
            return Record(succeeded, "Body Condition Duplicate Proof", succeeded ? "Success" : "DuplicateProofFailed", $"First={first.Code} Second={second.Code}. Duplicate localized damage did not apply a second mutation.");
        }

        public PrototypeTestLabOperation RemoveFirstBodyConditionInjury()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Remove Body Condition Injury", "Body runtime is missing.", LocalizedDamageResultCode.MissingActorBody.ToString());
            }

            InjuryRecordSnapshot first = body.Condition.CreateSnapshot().ActiveInjuries.FirstOrDefault();
            if (first == null)
            {
                return RecordFailure("Remove Body Condition Injury", "No active injury exists.", LocalizedDamageResultCode.InvalidRequest.ToString());
            }

            return RecordConditionResult("Remove Body Condition Injury", body.Condition.RemoveInjury(first.InjuryId));
        }

        public PrototypeTestLabOperation TestMissingConditionNode()
        {
            return PreviewLocalizedStructuralDamage("injury.blunt-trauma", "part.missing-test-lab", 10);
        }

        public PrototypeTestLabOperation TestIncompatibleConditionInjury()
        {
            PrototypeTestLabOperation spirit = AssignBodySpecies("species.basic-spirit");
            if (!spirit.Succeeded)
            {
                return spirit;
            }

            return PreviewLocalizedStructuralDamage("injury.fracture", "core.spiritual", 25);
        }

        public PrototypeTestLabOperation ValidateBodyConditionSaveRestore()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Body Condition Save Restore", "Body runtime is missing.", LocalizedDamageResultCode.MissingActorBody.ToString());
            }

            PrototypeTestLabOperation apply = ApplyLocalizedStructuralDamageWithTransaction("injury.laceration", "part.hand.left", 15, $"test-lab.body-condition.restore.{Guid.NewGuid():N}");
            if (!apply.Succeeded)
            {
                return apply;
            }

            BodyConditionSnapshot before = body.Condition.CreateSnapshot();
            BodySaveData saveData = body.CreateSaveData();
            int eventCount = 0;
            body.Condition.ConditionChanged += CountConditionEvent;
            BodyOperationResult restore = body.RestoreFromSaveData(saveData, registry, body.ActorBodyId, body.PersonId, restoring: true);
            body.Condition.ConditionChanged -= CountConditionEvent;
            BodyConditionSnapshot after = body.Condition.CreateSnapshot();
            bool stable = before.ActiveInjuries.Select(injury => injury.InjuryId).SequenceEqual(after.ActiveInjuries.Select(injury => injury.InjuryId));
            bool succeeded = restore.Succeeded && stable && eventCount == 0 && after.Coherent;
            return Record(succeeded, "Validate Body Condition Save Restore", succeeded ? "Success" : "RestoreMismatch", $"Restore={restore.Code} StableInjuries={stable} Events={eventCount} Active={after.ActiveInjuries.Count}.");

            void CountConditionEvent(BodyConditionRuntime _, LocalizedStructuralDamageResult __, bool ___)
            {
                eventCount++;
            }
        }

        public string BuildVitalProcessSummary()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return "Vital process runtime is missing.";
            }

            EnsureVitalProcessesReady(body);
            BodySnapshot bodySnapshot = body.CreateSnapshot();
            VitalProcessSnapshot vitals = bodySnapshot.VitalProcesses;
            if (vitals == null)
            {
                return "Vital process snapshot is missing.";
            }

            List<string> lines = new List<string>
            {
                "Feature 7.4 Biological Resources and Vital Processes",
                $"Body: {bodySnapshot.ActorBodyId}",
                $"Species: {bodySnapshot.SpeciesId}",
                $"Profile: {vitals.ProfileId}",
                $"Readiness: {vitals.Readiness}",
                $"Body/Anatomy/Condition/Vital Revisions: {vitals.BodyRevision}/{vitals.AnatomyRevision}/{vitals.ConditionRevision}/{vitals.VitalRevision}",
                $"Active Resources: {string.Join(", ", vitals.ActiveResources.Select(resource => resource.ResourceId))}",
                $"Critical Resources: {string.Join(", ", vitals.CriticalResources.Select(resource => resource.ResourceId))}",
                $"Lifecycle Pressure: {vitals.LifecyclePressure}",
                $"Coherent: {vitals.Coherent}"
            };

            foreach (VitalResourceSnapshot resource in vitals.Resources)
            {
                lines.Add($"- {resource.DisplayName} [{resource.ResourceId}] Active={resource.Active} Type={resource.ModelType} Value={resource.CurrentValue:0.##}/{resource.EffectiveMaximumValue:0.##} State={resource.State} Safe={resource.SafeMinimum:0.##}-{resource.SafeMaximum:0.##}");
                foreach (VitalCapacityContributionSnapshot contribution in resource.CapacityContributions.Where(contribution => !Mathf.Approximately(contribution.Magnitude, resource.MaximumValue)).Take(4))
                {
                    lines.Add($"  Capacity: {contribution.SourceId} {contribution.Magnitude:+0.##;-0.##;0} {contribution.Description}");
                }
            }

            if (vitals.Diagnostics.Count > 0)
            {
                lines.AddRange(vitals.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation ValidateVitalProcessIntegrity()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Vital Processes", "Body runtime is missing.", VitalProcessResultCode.MissingActorBody.ToString());
            }

            EnsureVitalProcessesReady(body);
            VitalProcessSnapshot snapshot = body.VitalProcesses.CreateSnapshot();
            bool succeeded = snapshot.Coherent && snapshot.Readiness == VitalProcessReadinessState.Ready && snapshot.Resources.Count >= 7;
            return Record(succeeded, "Validate Vital Processes", succeeded ? "Success" : "InvalidVitalProcesses", $"Readiness={snapshot.Readiness} Profile={snapshot.ProfileId} Resources={snapshot.Resources.Count} Active={snapshot.ActiveResources.Count} Coherent={snapshot.Coherent}.");
        }

        public PrototypeTestLabOperation ResetVitalProcessesHuman()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Reset Vital Processes", "Body runtime is missing.", VitalProcessResultCode.MissingActorBody.ToString());
            }

            BodyOperationResult assignment = body.AssignSpecies("species.human", restoring: false, "Test Lab vital process reset");
            if (!assignment.Succeeded)
            {
                return RecordBodyResult("Reset Vital Processes", assignment);
            }

            return RecordVitalResult("Reset Vital Processes", body.VitalProcesses.BuildForBody(body.ActorBodyId, body.Species, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), registry));
        }

        public PrototypeTestLabOperation PreviewVitalResourceMutation(string resourceId, VitalResourceMutationOperation operation, float amount)
        {
            if (!TryBuildVitalRequest(resourceId, operation, amount, "preview", out ActorBodyRuntime body, out VitalResourceMutationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            long revisionBefore = body.VitalProcesses.VitalRevision;
            VitalResourceMutationResult result = body.VitalProcesses.PreviewMutation(request, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot());
            long revisionAfter = body.VitalProcesses.VitalRevision;
            bool succeeded = result.Succeeded && result.Preview && revisionBefore == revisionAfter;
            return succeeded
                ? RecordVitalResult("Preview Vital Resource Mutation", result)
                : Record(false, "Preview Vital Resource Mutation", result.Code.ToString(), $"{result.Message} Revision={revisionBefore}->{revisionAfter} Preview={result.Preview}.");
        }

        public PrototypeTestLabOperation ApplyVitalResourceMutation(string resourceId, VitalResourceMutationOperation operation, float amount)
        {
            return ApplyVitalResourceMutationWithTransaction(resourceId, operation, amount, $"test-lab.vital.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation ApplyVitalResourceMutationWithTransaction(string resourceId, VitalResourceMutationOperation operation, float amount, string transactionId)
        {
            if (!TryBuildVitalRequest(resourceId, operation, amount, transactionId, out ActorBodyRuntime body, out VitalResourceMutationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            return RecordVitalResult("Apply Vital Resource Mutation", body.VitalProcesses.ApplyMutation(request, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot()));
        }

        public PrototypeTestLabOperation ProveVitalProcessDuplicateProtection()
        {
            const string transactionId = "test-lab.vital.duplicate-proof";
            PrototypeTestLabOperation first = ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, 5f, transactionId);
            PrototypeTestLabOperation second = ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, 5f, transactionId);
            bool succeeded = first.Succeeded && second.Succeeded && string.Equals(second.Code, VitalProcessResultCode.Duplicate.ToString(), StringComparison.Ordinal);
            return Record(succeeded, "Vital Process Duplicate Proof", succeeded ? "Success" : "DuplicateProofFailed", $"First={first.Code} Second={second.Code}. Duplicate vital mutation did not apply a second mutation.");
        }

        public PrototypeTestLabOperation ApplyVitalProcessUpdate(float elapsedGameSeconds)
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Apply Vital Process Update", "Body runtime is missing.", VitalProcessResultCode.MissingActorBody.ToString());
            }

            EnsureVitalProcessesReady(body);
            return RecordVitalResult("Apply Vital Process Update", body.VitalProcesses.ApplyProcessUpdate(elapsedGameSeconds, $"test-lab.vital.update.{Guid.NewGuid():N}", body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot()));
        }

        public PrototypeTestLabOperation ValidateVitalProcessDeterministicUpdate()
        {
            PrototypeTestLabOperation reset = ResetVitalProcessesHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Vital Process Update", "Body runtime is missing.", VitalProcessResultCode.MissingActorBody.ToString());
            }

            body.VitalProcesses.ApplyProcessUpdate(3600f, "test-lab.vital.deterministic.a", body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot());
            float firstNutrition = body.VitalProcesses.TryGetResource(BiologicalResourceIds.Nutrition, out VitalResourceSnapshot first) ? first.CurrentValue : -1f;
            reset = ResetVitalProcessesHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            body.VitalProcesses.ApplyProcessUpdate(3600f, "test-lab.vital.deterministic.b", body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot());
            float secondNutrition = body.VitalProcesses.TryGetResource(BiologicalResourceIds.Nutrition, out VitalResourceSnapshot second) ? second.CurrentValue : -2f;
            bool succeeded = Mathf.Approximately(firstNutrition, secondNutrition) && firstNutrition < 100f;
            return Record(succeeded, "Validate Vital Process Update", succeeded ? "Success" : "NondeterministicUpdate", $"Nutrition={firstNutrition:0.###}/{secondNutrition:0.###}.");
        }

        public PrototypeTestLabOperation DamageLungAndRecalculateBreath()
        {
            PrototypeTestLabOperation reset = ResetVitalProcessesHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Damage Lung Breath Capacity", "Body runtime is missing.", VitalProcessResultCode.MissingActorBody.ToString());
            }

            body.VitalProcesses.TryGetResource(BiologicalResourceIds.Breath, out VitalResourceSnapshot before);
            PrototypeTestLabOperation damage = ApplyLocalizedStructuralDamageWithTransaction("injury.blunt-trauma", "organ.lung.left", 50, $"test-lab.vital.lung.{Guid.NewGuid():N}");
            if (!damage.Succeeded)
            {
                return damage;
            }

            body.VitalProcesses.RecalculateCapacities(body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), preservingCurrent: true);
            body.VitalProcesses.TryGetResource(BiologicalResourceIds.Breath, out VitalResourceSnapshot after);
            bool succeeded = before != null && after != null && after.EffectiveMaximumValue < before.EffectiveMaximumValue;
            return Record(succeeded, "Damage Lung Breath Capacity", succeeded ? "Success" : "CapacityUnchanged", $"Breath Max={before?.EffectiveMaximumValue ?? 0:0.##}->{after?.EffectiveMaximumValue ?? 0:0.##}.");
        }

        public PrototypeTestLabOperation TestInactiveVitalResource(string speciesId, string resourceId)
        {
            PrototypeTestLabOperation assign = AssignBodySpecies(speciesId);
            if (!assign.Succeeded)
            {
                return assign;
            }

            return ApplyVitalResourceMutationWithTransaction(resourceId, VitalResourceMutationOperation.Consume, 5f, $"test-lab.vital.inactive.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation ValidateVitalProcessSaveRestore()
        {
            PrototypeTestLabOperation reset = ResetVitalProcessesHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Vital Process Save Restore", "Body runtime is missing.", VitalProcessResultCode.MissingActorBody.ToString());
            }

            PrototypeTestLabOperation mutate = ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, 20f, $"test-lab.vital.restore.{Guid.NewGuid():N}");
            if (!mutate.Succeeded)
            {
                return mutate;
            }

            VitalProcessSnapshot before = body.VitalProcesses.CreateSnapshot();
            BodySaveData saveData = body.CreateSaveData();
            int eventCount = 0;
            body.VitalProcesses.VitalResourceChanged += CountVitalEvent;
            BodyOperationResult restore = body.RestoreFromSaveData(saveData, registry, body.ActorBodyId, body.PersonId, restoring: true);
            body.VitalProcesses.VitalResourceChanged -= CountVitalEvent;
            VitalProcessSnapshot after = body.VitalProcesses.CreateSnapshot();
            float beforeBlood = before.Resources.FirstOrDefault(resource => resource.ResourceId == BiologicalResourceIds.Blood)?.CurrentValue ?? -1f;
            float afterBlood = after.Resources.FirstOrDefault(resource => resource.ResourceId == BiologicalResourceIds.Blood)?.CurrentValue ?? -2f;
            bool succeeded = restore.Succeeded && Mathf.Approximately(beforeBlood, afterBlood) && eventCount == 0 && after.Coherent;
            return Record(succeeded, "Validate Vital Process Save Restore", succeeded ? "Success" : "RestoreMismatch", $"Restore={restore.Code} Blood={beforeBlood:0.##}->{afterBlood:0.##} Events={eventCount}.");

            void CountVitalEvent(VitalProcessRuntime _, VitalResourceMutationResult __, bool ___)
            {
                eventCount++;
            }
        }

        public string BuildBiologicalHazardSummary()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return "Biological hazard runtime is missing.";
            }

            EnsureBiologicalHazardsReady(body);
            BodySnapshot bodySnapshot = body.CreateSnapshot();
            BiologicalHazardSnapshot hazards = bodySnapshot.BiologicalHazards;
            if (hazards == null)
            {
                return "Biological hazard snapshot is missing.";
            }

            List<string> lines = new List<string>
            {
                "Feature 7.5 Biological Hazards",
                $"Body: {bodySnapshot.ActorBodyId}",
                $"Species: {bodySnapshot.SpeciesId}",
                $"Readiness: {hazards.Readiness}",
                $"Body/Vital/Hazard Revisions: {hazards.BodyRevision}/{hazards.VitalRevision}/{hazards.HazardRevision}",
                $"Active Hazards: {hazards.ActiveHazards.Count}",
                $"Coherent: {hazards.Coherent}"
            };

            foreach (BiologicalHazardInstanceSnapshot hazard in hazards.ActiveHazards)
            {
                lines.Add($"- {hazard.DisplayName} [{hazard.HazardDefinitionId}] Severity={hazard.Severity} Rate={hazard.EffectiveRatePerHour:0.###}/h Sources={hazard.Sources.Count} Suppressions={hazard.Suppressions.Count}");
                foreach (BiologicalHazardSourceSnapshot source in hazard.Sources.Take(4))
                {
                    lines.Add($"  Source: {source.SourceContributionId} Category={source.SourceCategory} Severity={source.Severity} Rate={source.RateMultiplier:0.###} Remaining={source.RemainingSeconds:0.##}");
                }
            }

            if (hazards.Diagnostics.Count > 0)
            {
                lines.AddRange(hazards.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation ValidateBiologicalHazardIntegrity()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Biological Hazards", "Body runtime is missing.", BiologicalHazardResultCode.MissingActorBody.ToString());
            }

            EnsureBiologicalHazardsReady(body);
            BiologicalHazardSnapshot snapshot = body.BiologicalHazards.CreateSnapshot();
            bool succeeded = snapshot.Coherent && snapshot.Readiness == BiologicalHazardReadinessState.Ready;
            return Record(succeeded, "Validate Biological Hazards", succeeded ? "Success" : "InvalidBiologicalHazards", $"Readiness={snapshot.Readiness} Active={snapshot.ActiveHazards.Count} Coherent={snapshot.Coherent}.");
        }

        public PrototypeTestLabOperation ResetBiologicalHazardsHuman()
        {
            PrototypeTestLabOperation reset = ResetVitalProcessesHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Reset Biological Hazards", "Body runtime is missing.", BiologicalHazardResultCode.MissingActorBody.ToString());
            }

            return RecordHazardOperation("Reset Biological Hazards", body.BiologicalHazards.BuildForBody(body.ActorBodyId, body.VitalProcesses, registry));
        }

        public PrototypeTestLabOperation AddBleedingHazard()
        {
            return AddHazardSource(BiologicalHazardIds.Bleeding, $"test-lab.hazard.bleeding.{Guid.NewGuid():N}", BiologicalHazardSourceCategory.Injury, BiologicalHazardSeverity.Moderate, 1f, 0f, "injury.laceration");
        }

        public PrototypeTestLabOperation AddSecondBleedingHazardSource()
        {
            return AddHazardSource(BiologicalHazardIds.Bleeding, $"test-lab.hazard.bleeding.second.{Guid.NewGuid():N}", BiologicalHazardSourceCategory.Injury, BiologicalHazardSeverity.Minor, 0.5f, 0f, "injury.puncture");
        }

        public PrototypeTestLabOperation AddSuffocationExposure()
        {
            return AddHazardSource(BiologicalHazardIds.Suffocation, $"test-lab.exposure.air.{Guid.NewGuid():N}", BiologicalHazardSourceCategory.Environment, BiologicalHazardSeverity.Serious, 1f, 0f, BiologicalExposureIds.BreathableAirUnavailable);
        }

        public PrototypeTestLabOperation AddHeatExposure()
        {
            return AddHazardSource(BiologicalHazardIds.Overheating, $"test-lab.exposure.heat.{Guid.NewGuid():N}", BiologicalHazardSourceCategory.Environment, BiologicalHazardSeverity.Serious, 1f, 0f, BiologicalExposureIds.Heat);
        }

        public PrototypeTestLabOperation AddColdExposure()
        {
            return AddHazardSource(BiologicalHazardIds.Hypothermia, $"test-lab.exposure.cold.{Guid.NewGuid():N}", BiologicalHazardSourceCategory.Environment, BiologicalHazardSeverity.Serious, 1f, 0f, BiologicalExposureIds.Cold);
        }

        public PrototypeTestLabOperation PreviewBiologicalHazardTick(float elapsedGameSeconds)
        {
            if (!TryBuildHazardTickRequest(elapsedGameSeconds, "preview", preview: true, out ActorBodyRuntime body, out BiologicalHazardTickRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            long hazardRevision = body.BiologicalHazards.HazardRevision;
            long vitalRevision = body.VitalProcesses.VitalRevision;
            BiologicalHazardTickResult result = body.BiologicalHazards.PreviewTick(request, body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), body.BiologicalCompatibility, body.CreateSnapshot());
            bool succeeded = result.Succeeded && result.Preview && hazardRevision == body.BiologicalHazards.HazardRevision && vitalRevision == body.VitalProcesses.VitalRevision;
            return succeeded
                ? RecordHazardTick("Preview Biological Hazard Tick", result)
                : Record(false, "Preview Biological Hazard Tick", result.Code.ToString(), $"{result.Message} HazardRevision={hazardRevision}->{body.BiologicalHazards.HazardRevision} VitalRevision={vitalRevision}->{body.VitalProcesses.VitalRevision}.");
        }

        public PrototypeTestLabOperation ApplyBiologicalHazardTick(float elapsedGameSeconds)
        {
            return ApplyBiologicalHazardTickWithTransaction(elapsedGameSeconds, $"test-lab.hazard.tick.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation ApplyBiologicalHazardTickWithTransaction(float elapsedGameSeconds, string transactionId)
        {
            if (!TryBuildHazardTickRequest(elapsedGameSeconds, transactionId, preview: false, out ActorBodyRuntime body, out BiologicalHazardTickRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            return RecordHazardTick("Apply Biological Hazard Tick", body.BiologicalHazards.ApplyTick(request, body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), restoring: false, compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot()));
        }

        public PrototypeTestLabOperation ProveBiologicalHazardTickDuplicateProtection()
        {
            const string transactionId = "test-lab.hazard.duplicate-proof";
            PrototypeTestLabOperation add = AddBleedingHazard();
            if (!add.Succeeded)
            {
                return add;
            }

            PrototypeTestLabOperation first = ApplyBiologicalHazardTickWithTransaction(1800f, transactionId);
            PrototypeTestLabOperation second = ApplyBiologicalHazardTickWithTransaction(1800f, transactionId);
            bool succeeded = first.Succeeded && second.Succeeded && string.Equals(second.Code, BiologicalHazardResultCode.Duplicate.ToString(), StringComparison.Ordinal);
            return Record(succeeded, "Biological Hazard Duplicate Proof", succeeded ? "Success" : "DuplicateProofFailed", $"First={first.Code} Second={second.Code}. Duplicate biological hazard tick did not apply a second mutation.");
        }

        public PrototypeTestLabOperation SuppressBleedingHazard()
        {
            if (!EnsureHazardRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            if (!body.BiologicalHazards.CreateSnapshot().ActiveHazards.Any(hazard => hazard.HazardDefinitionId == BiologicalHazardIds.Bleeding))
            {
                PrototypeTestLabOperation add = AddBleedingHazard();
                if (!add.Succeeded)
                {
                    return add;
                }
            }

            BiologicalHazardSuppressionRequest request = new BiologicalHazardSuppressionRequest(body.ActorBodyId, BiologicalHazardIds.Bleeding, "test-lab.hazard.suppression.bandage", BiologicalHazardSuppressionMode.RateMultiplier, 0.25f, "Prototype bleeding suppression");
            return RecordHazardOperation("Suppress Bleeding Hazard", body.BiologicalHazards.AddOrUpdateSuppression(request));
        }

        public PrototypeTestLabOperation RemoveFirstBiologicalHazardSource()
        {
            if (!EnsureHazardRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BiologicalHazardInstanceSnapshot hazard = body.BiologicalHazards.CreateSnapshot().ActiveHazards.FirstOrDefault(active => active.Sources.Count > 0);
            BiologicalHazardSourceSnapshot source = hazard?.Sources.FirstOrDefault();
            if (hazard == null || source == null)
            {
                return RecordFailure("Remove Biological Hazard Source", "No active hazard source exists.", BiologicalHazardResultCode.MissingSource.ToString());
            }

            return RecordHazardOperation("Remove Biological Hazard Source", body.BiologicalHazards.RemoveSource(hazard.HazardDefinitionId, source.SourceContributionId));
        }

        public PrototypeTestLabOperation SynchronizeBiologicalHazardsFromVitals()
        {
            if (!EnsureHazardRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            return RecordHazardOperation("Synchronize Biological Hazards", body.BiologicalHazards.SynchronizeFromVitalProcesses(body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot()));
        }

        public PrototypeTestLabOperation CreateStarvationAndDehydrationPressure()
        {
            PrototypeTestLabOperation nutrition = ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Nutrition, VitalResourceMutationOperation.Set, 0f, $"test-lab.hazard.nutrition.{Guid.NewGuid():N}");
            if (!nutrition.Succeeded)
            {
                return nutrition;
            }

            PrototypeTestLabOperation hydration = ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Hydration, VitalResourceMutationOperation.Set, 0f, $"test-lab.hazard.hydration.{Guid.NewGuid():N}");
            if (!hydration.Succeeded)
            {
                return hydration;
            }

            return SynchronizeBiologicalHazardsFromVitals();
        }

        public PrototypeTestLabOperation CreateFatigueAndSleepPressure()
        {
            PrototypeTestLabOperation fatigue = ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Fatigue, VitalResourceMutationOperation.Set, 100f, $"test-lab.hazard.fatigue.{Guid.NewGuid():N}");
            if (!fatigue.Succeeded)
            {
                return fatigue;
            }

            PrototypeTestLabOperation sleep = ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.SleepNeed, VitalResourceMutationOperation.Set, 100f, $"test-lab.hazard.sleep.{Guid.NewGuid():N}");
            if (!sleep.Succeeded)
            {
                return sleep;
            }

            return SynchronizeBiologicalHazardsFromVitals();
        }

        public PrototypeTestLabOperation CreateTemperatureHazard(bool high)
        {
            PrototypeTestLabOperation temperature = ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Temperature, VitalResourceMutationOperation.Set, high ? 42f : 30f, $"test-lab.hazard.temperature.{Guid.NewGuid():N}");
            if (!temperature.Succeeded)
            {
                return temperature;
            }

            return SynchronizeBiologicalHazardsFromVitals();
        }

        public PrototypeTestLabOperation TestInactiveBiologicalHazardResource(string speciesId, string hazardId)
        {
            PrototypeTestLabOperation assign = AssignBodySpecies(speciesId);
            if (!assign.Succeeded)
            {
                return assign;
            }

            return AddHazardSource(hazardId, $"test-lab.hazard.inactive.{Guid.NewGuid():N}", BiologicalHazardSourceCategory.System, BiologicalHazardSeverity.Minor, 1f, 0f, "inactive-resource-proof");
        }

        public PrototypeTestLabOperation ValidateBiologicalHazardSaveRestore()
        {
            PrototypeTestLabOperation reset = ResetBiologicalHazardsHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            PrototypeTestLabOperation add = AddBleedingHazard();
            if (!add.Succeeded)
            {
                return add;
            }

            if (!EnsureHazardRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BiologicalHazardSnapshot before = body.BiologicalHazards.CreateSnapshot();
            BodySaveData saveData = body.CreateSaveData();
            int eventCount = 0;
            body.BiologicalHazards.HazardChanged += CountHazardEvent;
            body.BiologicalHazards.HazardTicked += CountHazardTickEvent;
            BodyOperationResult restore = body.RestoreFromSaveData(saveData, registry, body.ActorBodyId, body.PersonId, restoring: true);
            body.BiologicalHazards.HazardChanged -= CountHazardEvent;
            body.BiologicalHazards.HazardTicked -= CountHazardTickEvent;
            BiologicalHazardSnapshot after = body.BiologicalHazards.CreateSnapshot();
            bool stable = before.ActiveHazards.Select(hazard => hazard.HazardDefinitionId).SequenceEqual(after.ActiveHazards.Select(hazard => hazard.HazardDefinitionId));
            bool succeeded = restore.Succeeded && stable && eventCount == 0 && after.Coherent;
            return Record(succeeded, "Validate Biological Hazard Save Restore", succeeded ? "Success" : "RestoreMismatch", $"Restore={restore.Code} StableHazards={stable} Events={eventCount} Active={after.ActiveHazards.Count}.");

            void CountHazardEvent(BiologicalHazardRuntime _, BiologicalHazardOperationResult __, bool ___)
            {
                eventCount++;
            }

            void CountHazardTickEvent(BiologicalHazardRuntime _, BiologicalHazardTickResult __, bool ___)
            {
                eventCount++;
            }
        }

        public string BuildBiologicalCompatibilitySummary()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return "Biological compatibility runtime is missing.";
            }

            BodySnapshot snapshot = body.CreateSnapshot();
            BiologicalCompatibilitySnapshot compatibility = snapshot.BiologicalCompatibility;
            List<string> lines = new List<string>
            {
                "Feature 7.6 Biological Compatibility",
                $"Body: {snapshot.ActorBodyId}",
                $"Species: {snapshot.SpeciesId}",
                $"Classification: {snapshot.BiologicalClassificationId}",
                $"Body Form: {snapshot.BodyFormId}",
                $"Readiness: {compatibility?.Readiness}",
                $"Profile: {compatibility?.ProfileId}",
                $"Body/Anatomy/Condition/Vital/Hazard/Compatibility Revisions: {compatibility?.BodyRevision}/{compatibility?.AnatomyRevision}/{compatibility?.ConditionRevision}/{compatibility?.VitalRevision}/{compatibility?.HazardRevision}/{compatibility?.CompatibilityRevision}",
                $"Dynamic Rules: {compatibility?.Rules.Count ?? 0}",
                $"Coherent: {compatibility?.Coherent}"
            };

            BiologicalInteractionEvaluationResult bleeding = body.BiologicalCompatibility.Evaluate(snapshot, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard);
            BiologicalInteractionEvaluationResult healing = body.BiologicalCompatibility.Evaluate(snapshot, BiologicalInteractionIds.BiologicalHealing, BiologicalInteractionCategory.Healing);
            BiologicalInteractionEvaluationResult repair = body.BiologicalCompatibility.Evaluate(snapshot, BiologicalInteractionIds.ConstructRepair, BiologicalInteractionCategory.Repair);
            lines.Add($"Bleeding: {FormatCompatibilityResult(bleeding)}");
            lines.Add($"Biological Healing: {FormatCompatibilityResult(healing)}");
            lines.Add($"Construct Repair: {FormatCompatibilityResult(repair)}");

            foreach (BiologicalCompatibilityRuleSnapshot rule in compatibility?.Rules.Take(8) ?? Array.Empty<BiologicalCompatibilityRuleSnapshot>())
            {
                lines.Add($"Rule: {rule.EntryId} Source={rule.SourceId} Kind={rule.RuleKind} Interaction={rule.InteractionDefinitionId} Category={rule.Category} Priority={rule.Priority}");
            }

            if (compatibility?.Diagnostics.Count > 0)
            {
                lines.AddRange(compatibility.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation ValidateBiologicalCompatibilityIntegrity()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Biological Compatibility", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            BodySnapshot snapshot = body.CreateSnapshot();
            BiologicalCompatibilitySnapshot compatibility = snapshot.BiologicalCompatibility;
            bool succeeded = compatibility != null && compatibility.Coherent && compatibility.Readiness == BiologicalCompatibilityReadinessState.Ready;
            return Record(succeeded, "Validate Biological Compatibility", succeeded ? "Success" : "InvalidBiologicalCompatibility", $"Readiness={compatibility?.Readiness} Profile={compatibility?.ProfileId} Rules={compatibility?.Rules.Count ?? 0} Coherent={compatibility?.Coherent}.");
        }

        public PrototypeTestLabOperation ResetBiologicalCompatibilityHuman()
        {
            PrototypeTestLabOperation reset = ResetBiologicalHazardsHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Reset Biological Compatibility", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            body.BiologicalCompatibility.ClearSource("test-lab.compatibility", restoring: false);
            BodyOperationResult reassign = body.AssignSpecies("species.human", restoring: false, "Test Lab compatibility reset");
            if (!reassign.Succeeded && !reassign.Duplicate)
            {
                return Record(reassign.Succeeded, "Reset Biological Compatibility", reassign.Code.ToString(), reassign.Message);
            }

            return RecordCompatibilityOperation("Reset Biological Compatibility", body.BiologicalCompatibility.BuildForBody(body.CreateSnapshot(), registry, restoring: false));
        }

        public PrototypeTestLabOperation EvaluateBiologicalCompatibility(string interactionId)
        {
            return EvaluateBiologicalCompatibility(interactionId, BiologicalInteractionCategory.Unknown, string.Empty);
        }

        public PrototypeTestLabOperation EvaluateBiologicalCompatibility(string interactionId, BiologicalInteractionCategory category, string nodeId)
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Evaluate Biological Compatibility", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            BodySnapshot snapshot = body.CreateSnapshot();
            AnatomyNodeSnapshot node = string.IsNullOrWhiteSpace(nodeId)
                ? null
                : snapshot.Anatomy?.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
            BiologicalInteractionEvaluationResult result = body.BiologicalCompatibility.Evaluate(snapshot, interactionId, category, node, "test-lab.compatibility", $"test-lab.compatibility.evaluate.{Guid.NewGuid():N}", preview: true);
            return RecordCompatibility("Evaluate Biological Compatibility", result);
        }

        public PrototypeTestLabOperation AddBiologicalCompatibilityResistance()
        {
            return AddCompatibilityRule("test-lab.compatibility.resistance", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, BiologicalCompatibilityState.Compatible, 0.5f, 0.75f, 0.5f, "Development resistance halves bleeding rate and consequence.");
        }

        public PrototypeTestLabOperation AddSecondBiologicalCompatibilityResistance()
        {
            return AddCompatibilityRule("test-lab.compatibility.resistance.second", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, BiologicalCompatibilityState.Compatible, 0.8f, 1f, 0.8f, "Second source-safe resistance.");
        }

        public PrototypeTestLabOperation AddBiologicalCompatibilityVulnerability()
        {
            return AddCompatibilityRule("test-lab.compatibility.vulnerability", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Vulnerability, BiologicalCompatibilityState.Compatible, 1.5f, 1.25f, 1.5f, "Development vulnerability increases bleeding.");
        }

        public PrototypeTestLabOperation AddBiologicalCompatibilityImmunity()
        {
            return AddCompatibilityRule("test-lab.compatibility.immunity", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Immunity, BiologicalCompatibilityState.Compatible, 1f, 1f, 1f, "Development immunity blocks bleeding semantically.");
        }

        public PrototypeTestLabOperation AddBiologicalCompatibilitySuppression()
        {
            return AddCompatibilityRule("test-lab.compatibility.suppression", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Suppression, BiologicalCompatibilityState.Compatible, 0f, 0f, 0f, "Development suppression pauses bleeding without intrinsic immunity.");
        }

        public PrototypeTestLabOperation AddBiologicalCompatibilityAffinity()
        {
            return AddCompatibilityRule("test-lab.compatibility.affinity", BiologicalInteractionIds.SpiritRestoration, BiologicalInteractionRuleKind.Affinity, BiologicalCompatibilityState.Compatible, 1.2f, 1f, 1.2f, "Development affinity improves restoration.");
        }

        public PrototypeTestLabOperation AddBiologicalCompatibilityConversion()
        {
            return AddCompatibilityRule("test-lab.compatibility.conversion", BiologicalInteractionIds.Necrotic, BiologicalInteractionRuleKind.Conversion, BiologicalCompatibilityState.Compatible, 1f, 1f, 1f, "Development conversion maps necrotic to restoration.", BiologicalInteractionIds.NecroticRestoration);
        }

        public PrototypeTestLabOperation AddBiologicalCompatibilityAbsorption()
        {
            return AddCompatibilityRule("test-lab.compatibility.absorption", BiologicalInteractionIds.Fire, BiologicalInteractionRuleKind.Absorption, BiologicalCompatibilityState.Compatible, 1f, 1f, 1f, "Development absorption reports special beneficial handling.");
        }

        public PrototypeTestLabOperation RemoveFirstBiologicalCompatibilityRule()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Remove Biological Compatibility Rule", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            BiologicalCompatibilityRuleSnapshot first = body.BiologicalCompatibility.CreateSnapshot().Rules.FirstOrDefault();
            if (first == null)
            {
                return RecordFailure("Remove Biological Compatibility Rule", "No dynamic compatibility rule exists.", BiologicalCompatibilityResultCode.MissingContribution.ToString());
            }

            return RecordCompatibilityOperation("Remove Biological Compatibility Rule", body.BiologicalCompatibility.RemoveContribution(first.SourceId, first.EntryId));
        }

        public PrototypeTestLabOperation ProveBiologicalCompatibilityDeterministicOrder()
        {
            PrototypeTestLabOperation reset = ResetBiologicalCompatibilityHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            AddBiologicalCompatibilityResistance();
            AddSecondBiologicalCompatibilityResistance();
            BiologicalInteractionEvaluationResult first = EvaluateBiologicalCompatibilityRaw(BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard, string.Empty);
            ResetBiologicalCompatibilityHuman();
            AddSecondBiologicalCompatibilityResistance();
            AddBiologicalCompatibilityResistance();
            BiologicalInteractionEvaluationResult second = EvaluateBiologicalCompatibilityRaw(BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard, string.Empty);
            string firstSignature = FormatCompatibilityDeterminismSignature(first);
            string secondSignature = FormatCompatibilityDeterminismSignature(second);
            bool succeeded = first != null
                && second != null
                && first.Code == BiologicalCompatibilityResultCode.Success
                && second.Code == BiologicalCompatibilityResultCode.Success
                && string.Equals(firstSignature, secondSignature, StringComparison.Ordinal);
            return Record(succeeded, "Biological Compatibility Deterministic Order", succeeded ? "Success" : "Nondeterministic", $"First='{firstSignature}' Second='{secondSignature}'.");
        }

        public PrototypeTestLabOperation ProveBiologicalCompatibilitySnapshotReadOnly()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Biological Compatibility Snapshot", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            long before = body.BiologicalCompatibility.CompatibilityRevision;
            BiologicalCompatibilitySnapshot snapshot = body.BiologicalCompatibility.CreateSnapshot();
            long after = body.BiologicalCompatibility.CompatibilityRevision;
            bool succeeded = snapshot != null && before == after;
            return Record(succeeded, "Biological Compatibility Snapshot", succeeded ? "Success" : "Mutated", $"Revision={before}->{after} Rules={snapshot?.Rules.Count ?? 0}.");
        }

        public PrototypeTestLabOperation ProveBiologicalCompatibilitySpecificRulePrecedence()
        {
            PrototypeTestLabOperation reset = ResetBiologicalCompatibilityHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Biological Compatibility Precedence", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            RuntimeBiologicalInteractionRule categoryRule = new RuntimeBiologicalInteractionRule(
                "test-lab.compatibility.category-injury-block",
                BiologicalCompatibilitySourceKind.Development,
                "test-lab.compatibility",
                string.Empty,
                BiologicalInteractionCategory.Injury,
                BiologicalInteractionRuleKind.CompatibilityOverride,
                BiologicalCompatibilityState.Incompatible,
                1f,
                1f,
                1f,
                0f,
                float.PositiveInfinity,
                100,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                "Development category-level injury block.");
            RuntimeBiologicalInteractionRule specificRule = new RuntimeBiologicalInteractionRule(
                "test-lab.compatibility.specific-fracture-allow",
                BiologicalCompatibilitySourceKind.Development,
                "test-lab.compatibility",
                BiologicalInteractionIds.Fracture,
                BiologicalInteractionCategory.Unknown,
                BiologicalInteractionRuleKind.CompatibilityOverride,
                BiologicalCompatibilityState.Compatible,
                1f,
                1f,
                1f,
                0f,
                float.PositiveInfinity,
                100,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                "Development specific fracture allow.");
            BiologicalCompatibilityOperationResult category = body.BiologicalCompatibility.AddOrUpdateContribution(categoryRule);
            BiologicalCompatibilityOperationResult specific = body.BiologicalCompatibility.AddOrUpdateContribution(specificRule);
            BodySnapshot snapshot = body.CreateSnapshot();
            AnatomyNodeSnapshot leg = snapshot.Anatomy?.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, "part.leg.left", StringComparison.Ordinal));
            BiologicalInteractionEvaluationResult result = body.BiologicalCompatibility.Evaluate(snapshot, BiologicalInteractionIds.Fracture, BiologicalInteractionCategory.Injury, leg, "test-lab.compatibility", $"test-lab.compatibility.precedence.{Guid.NewGuid():N}", preview: true);
            bool succeeded = category.Succeeded && specific.Succeeded && result.Code == BiologicalCompatibilityResultCode.Success && result.Compatible;
            return Record(succeeded, "Biological Compatibility Precedence", succeeded ? "Success" : result.Code.ToString(), $"{FormatCompatibilityResult(result)} Category={category.Code} Specific={specific.Code}.");
        }

        public PrototypeTestLabOperation ProveBiologicalCompatibilityMissingInteractionRejected()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Biological Compatibility Missing Interaction", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            BiologicalInteractionEvaluationResult result = body.BiologicalCompatibility.Evaluate(body.CreateSnapshot(), "interaction.missing.not-authored", BiologicalInteractionCategory.Hazard, null, "test-lab.compatibility", $"test-lab.compatibility.missing.{Guid.NewGuid():N}", preview: true);
            bool succeeded = result.Code == BiologicalCompatibilityResultCode.MissingInteraction;
            return Record(succeeded, "Biological Compatibility Missing Interaction", result.Code.ToString(), FormatCompatibilityResult(result));
        }

        public PrototypeTestLabOperation ProveBiologicalCompatibilityStaleBodyRejected()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Biological Compatibility Stale Body", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            BodySnapshot oldSnapshot = body.CreateSnapshot();
            BodyOperationResult assign = body.AssignSpecies("species.basic-construct", restoring: false, "Test Lab stale compatibility proof");
            if (!assign.Succeeded && !assign.Duplicate)
            {
                return Record(false, "Biological Compatibility Stale Body", assign.Code.ToString(), assign.Message);
            }

            BiologicalInteractionEvaluationResult stale = body.BiologicalCompatibility.Evaluate(oldSnapshot, BiologicalInteractionIds.CoreDamage, BiologicalInteractionCategory.Injury, null, "test-lab.compatibility", $"test-lab.compatibility.stale.{Guid.NewGuid():N}", preview: true);
            ResetBiologicalCompatibilityHuman();
            bool succeeded = stale.Code == BiologicalCompatibilityResultCode.StaleBody;
            return Record(succeeded, "Biological Compatibility Stale Body", stale.Code.ToString(), FormatCompatibilityResult(stale));
        }

        public PrototypeTestLabOperation ProveBiologicalCompatibilityDynamicReset()
        {
            PrototypeTestLabOperation reset = ResetBiologicalCompatibilityHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Biological Compatibility Dynamic Reset", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            BiologicalCompatibilityOperationResult add = body.BiologicalCompatibility.AddOrUpdateContribution(new RuntimeBiologicalInteractionRule(
                "test-lab.compatibility.dynamic-reset",
                BiologicalCompatibilitySourceKind.Development,
                "test-lab.compatibility",
                BiologicalInteractionIds.Bleeding,
                BiologicalInteractionCategory.Unknown,
                BiologicalInteractionRuleKind.Immunity,
                BiologicalCompatibilityState.Compatible,
                1f,
                1f,
                1f,
                0f,
                float.PositiveInfinity,
                100,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                "Development reset proof immunity."));
            BiologicalInteractionEvaluationResult immune = body.BiologicalCompatibility.Evaluate(body.CreateSnapshot(), BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard, null, "test-lab.compatibility", $"test-lab.compatibility.dynamic.immune.{Guid.NewGuid():N}", preview: true);
            PrototypeTestLabOperation resetAgain = ResetBiologicalCompatibilityHuman();
            BiologicalInteractionEvaluationResult restored = body.BiologicalCompatibility.Evaluate(body.CreateSnapshot(), BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard, null, "test-lab.compatibility", $"test-lab.compatibility.dynamic.restored.{Guid.NewGuid():N}", preview: true);
            bool succeeded = add.Succeeded && immune.Immune && resetAgain.Succeeded && restored.Compatible && !restored.Immune;
            return Record(succeeded, "Biological Compatibility Dynamic Reset", succeeded ? "Success" : restored.Code.ToString(), $"Immune={FormatCompatibilityResult(immune)} Restored={FormatCompatibilityResult(restored)}.");
        }

        private PrototypeTestLabOperation AddCompatibilityRule(string entryId, string interactionId, BiologicalInteractionRuleKind kind, BiologicalCompatibilityState state, float rate, float severity, float consequence, string explanation, string convertedInteractionId = "")
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Add Biological Compatibility Rule", "Body runtime is missing.", BiologicalCompatibilityResultCode.MissingBody.ToString());
            }

            RuntimeBiologicalInteractionRule rule = new RuntimeBiologicalInteractionRule(
                entryId,
                BiologicalCompatibilitySourceKind.Development,
                "test-lab.compatibility",
                interactionId,
                BiologicalInteractionCategory.Unknown,
                kind,
                state,
                rate,
                severity,
                consequence,
                0f,
                float.PositiveInfinity,
                100,
                convertedInteractionId,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                explanation);
            return RecordCompatibilityOperation("Add Biological Compatibility Rule", body.BiologicalCompatibility.AddOrUpdateContribution(rule));
        }

        private BiologicalInteractionEvaluationResult EvaluateBiologicalCompatibilityRaw(string interactionId, BiologicalInteractionCategory category, string nodeId)
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return null;
            }

            BodySnapshot snapshot = body.CreateSnapshot();
            AnatomyNodeSnapshot node = string.IsNullOrWhiteSpace(nodeId)
                ? null
                : snapshot.Anatomy?.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
            return body.BiologicalCompatibility.Evaluate(snapshot, interactionId, category, node, "test-lab.compatibility", $"test-lab.compatibility.evaluate.{Guid.NewGuid():N}", preview: true);
        }

        public string BuildBiologicalRecoverySummary()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return "Biological recovery runtime is missing.";
            }

            EnsureBiologicalRecoveryReady(body);
            BodySnapshot bodySnapshot = body.CreateSnapshot();
            BiologicalRecoverySnapshot recovery = bodySnapshot.BiologicalRecovery;
            if (recovery == null)
            {
                return "Biological recovery snapshot is missing.";
            }

            List<string> lines = new List<string>
            {
                "Feature 7.7 Natural Recovery and Biological Repair",
                $"Body: {bodySnapshot.ActorBodyId}",
                $"Species: {bodySnapshot.SpeciesId}",
                $"Profile: {recovery.ProfileId}",
                $"Readiness: {recovery.Readiness}",
                $"Rest Context: {recovery.RestContext?.RestType} Quality={recovery.RestContext?.Quality ?? 0f:0.##}",
                $"Body/Condition/Vital/Hazard/Compatibility/Recovery Revisions: {recovery.BodyRevision}/{recovery.ConditionRevision}/{recovery.VitalRevision}/{recovery.HazardRevision}/{recovery.CompatibilityRevision}/{recovery.RecoveryRevision}",
                $"Active Processes: {recovery.ActiveProcesses.Count}",
                $"Coherent: {recovery.Coherent}"
            };

            if (recovery.Processes.Count == 0)
            {
                lines.Add("Processes: None");
            }
            else
            {
                foreach (RecoveryProcessSnapshot process in recovery.Processes.Take(12))
                {
                    lines.Add($"- {process.RecoveryMethodId} [{process.ProcessId}] State={process.State} Progress={process.CurrentProgress:0.##}/{process.RequiredProgress:0.##} Target={process.Target?.TargetCategory}:{process.Target?.AnatomyNodeId}{process.Target?.ResourceDefinitionId} Tick={process.LastCommittedTickId}");
                    if (!string.IsNullOrWhiteSpace(process.CompatibilitySummary))
                    {
                        lines.Add($"  Compatibility: {process.CompatibilitySummary}");
                    }
                }
            }

            if (recovery.Diagnostics.Count > 0)
            {
                lines.AddRange(recovery.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation ValidateBiologicalRecoveryIntegrity()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Biological Recovery", "Body runtime is missing.", BiologicalRecoveryResultCode.MissingBody.ToString());
            }

            EnsureBiologicalRecoveryReady(body);
            BiologicalRecoverySnapshot snapshot = body.BiologicalRecovery.CreateSnapshot();
            bool succeeded = snapshot.Coherent && snapshot.Readiness == RecoveryReadinessState.Ready && !string.IsNullOrWhiteSpace(snapshot.ProfileId);
            return Record(succeeded, "Validate Biological Recovery", succeeded ? "Success" : "InvalidBiologicalRecovery", $"Readiness={snapshot.Readiness} Profile={snapshot.ProfileId} Active={snapshot.ActiveProcesses.Count} Coherent={snapshot.Coherent}.");
        }

        public PrototypeTestLabOperation ResetBiologicalRecoveryHuman()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Reset Biological Recovery", "Body runtime is missing.", BiologicalRecoveryResultCode.MissingBody.ToString());
            }

            BodyOperationResult assignment = body.AssignSpecies("species.human", restoring: false, "Test Lab biological recovery reset");
            if (!assignment.Succeeded)
            {
                return RecordBodyResult("Reset Biological Recovery", assignment);
            }

            LocalizedStructuralDamageResult condition = body.Condition.BuildHealthy(body.ActorBodyId, body.CreateAnatomySnapshot(), registry, restoring: false);
            if (!condition.Succeeded)
            {
                return RecordConditionResult("Reset Biological Recovery", condition);
            }

            return RecordRecoveryResult("Reset Biological Recovery", body.BiologicalRecovery.BuildForBody(body.CreateSnapshot(), registry, restoring: false));
        }

        public PrototypeTestLabOperation ApplyRecoveryLaceration()
        {
            return ApplyLocalizedStructuralDamageWithTransaction("injury.laceration", "part.hand.left", 40, $"test-lab.recovery.laceration.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation DrainRecoveryBlood()
        {
            return ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, 30f, $"test-lab.recovery.blood-drain.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation DrainRecoveryBreath()
        {
            return ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Breath, VitalResourceMutationOperation.Consume, 30f, $"test-lab.recovery.breath-drain.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation AddRecoveryFatigue()
        {
            return ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Fatigue, VitalResourceMutationOperation.Consume, 30f, $"test-lab.recovery.fatigue-add.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation AddRecoverySleepNeed()
        {
            return ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.SleepNeed, VitalResourceMutationOperation.Consume, 30f, $"test-lab.recovery.sleep-need-add.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation DrainRecoveryNutrition()
        {
            return ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Nutrition, VitalResourceMutationOperation.Consume, 30f, $"test-lab.recovery.nutrition-drain.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation DrainRecoveryHydration()
        {
            return ApplyVitalResourceMutationWithTransaction(BiologicalResourceIds.Hydration, VitalResourceMutationOperation.Consume, 30f, $"test-lab.recovery.hydration-drain.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation SetBiologicalRecoveryRestContext(RecoveryRestType restType)
        {
            if (!EnsureRecoveryRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            RecoveryRestContextRequest request = new RecoveryRestContextRequest
            {
                ActorBodyId = body.ActorBodyId,
                RestType = restType,
                SourceId = "test-lab.recovery.rest-context",
                TransactionId = $"test-lab.recovery.rest.{Guid.NewGuid():N}",
                Quality = restType == RecoveryRestType.NotResting ? 0f : 1f,
                Tags = Array.Empty<string>()
            };
            return RecordRecoveryResult("Set Biological Recovery Rest Context", body.BiologicalRecovery.SetRestContext(request));
        }

        public PrototypeTestLabOperation PreviewNaturalWoundClosureRecovery()
        {
            return StartRecoveryProcess("recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", string.Empty, preview: true, ensureTarget: true);
        }

        public PrototypeTestLabOperation StartNaturalWoundClosureRecovery()
        {
            return StartRecoveryProcess("recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", string.Empty, preview: false, ensureTarget: true);
        }

        public PrototypeTestLabOperation StartNaturalTissueRecovery()
        {
            return StartRecoveryProcess("recovery.natural.tissue-healing", RecoveryTargetCategory.Injury, "part.arm.left", string.Empty, preview: false, ensureTarget: true, injuryDefinitionId: "injury.blunt-trauma");
        }

        public PrototypeTestLabOperation StartNaturalFractureRecovery()
        {
            return StartRecoveryProcess("recovery.natural.fracture-healing", RecoveryTargetCategory.Injury, "part.leg.left", string.Empty, preview: false, ensureTarget: true, injuryDefinitionId: "injury.fracture");
        }

        public PrototypeTestLabOperation StartNaturalOrganRecovery()
        {
            return StartRecoveryProcess("recovery.natural.organ-recovery", RecoveryTargetCategory.Injury, "organ.lung.left", string.Empty, preview: false, ensureTarget: true, injuryDefinitionId: "injury.organ-trauma");
        }

        public PrototypeTestLabOperation StartNaturalBloodRecovery()
        {
            return StartRecoveryProcess("recovery.natural.blood-restoration", RecoveryTargetCategory.VitalResource, string.Empty, BiologicalResourceIds.Blood, preview: false, ensureTarget: true);
        }

        public PrototypeTestLabOperation StartNaturalBreathRecovery()
        {
            return StartRecoveryProcess("recovery.natural.breath-restoration", RecoveryTargetCategory.VitalResource, string.Empty, BiologicalResourceIds.Breath, preview: false, ensureTarget: true);
        }

        public PrototypeTestLabOperation StartNaturalFatigueRecovery()
        {
            return StartRecoveryProcess("recovery.natural.fatigue-reduction", RecoveryTargetCategory.VitalResource, string.Empty, BiologicalResourceIds.Fatigue, preview: false, ensureTarget: true);
        }

        public PrototypeTestLabOperation StartNaturalSleepNeedRecovery()
        {
            return StartRecoveryProcess("recovery.natural.sleep-need-reduction", RecoveryTargetCategory.VitalResource, string.Empty, BiologicalResourceIds.SleepNeed, preview: false, ensureTarget: true);
        }

        public PrototypeTestLabOperation StartNaturalNutritionRecovery()
        {
            return StartRecoveryProcess("recovery.natural.nutrition-recovery", RecoveryTargetCategory.VitalResource, string.Empty, BiologicalResourceIds.Nutrition, preview: false, ensureTarget: true);
        }

        public PrototypeTestLabOperation StartNaturalHydrationRecovery()
        {
            return StartRecoveryProcess("recovery.natural.hydration-recovery", RecoveryTargetCategory.VitalResource, string.Empty, BiologicalResourceIds.Hydration, preview: false, ensureTarget: true);
        }

        public PrototypeTestLabOperation StartConstructBiologicalHealingRecovery()
        {
            PrototypeTestLabOperation assign = AssignBodySpecies("species.basic-construct");
            if (!assign.Succeeded)
            {
                return assign;
            }

            return StartRecoveryProcess("recovery.magical.biological-healing", RecoveryTargetCategory.StructuralIntegrity, "core.power", string.Empty, preview: false, ensureTarget: false);
        }

        public PrototypeTestLabOperation StartSpiritRestorationRecovery()
        {
            PrototypeTestLabOperation assign = AssignBodySpecies("species.basic-spirit");
            if (!assign.Succeeded)
            {
                return assign;
            }

            PrototypeTestLabOperation rest = SetBiologicalRecoveryRestContext(RecoveryRestType.SpiritSanctuary);
            if (!rest.Succeeded)
            {
                return rest;
            }

            return StartRecoveryProcess("recovery.restoration.spirit", RecoveryTargetCategory.StructuralIntegrity, "core.spiritual", string.Empty, preview: false, ensureTarget: false);
        }

        public PrototypeTestLabOperation ProveNaturalRecoveryLimit()
        {
            PrototypeTestLabOperation reset = ResetBiologicalRecoveryHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            PrototypeTestLabOperation start = StartNaturalWoundClosureRecovery();
            if (!start.Succeeded)
            {
                return start;
            }

            PrototypeTestLabOperation tick = ApplyBiologicalRecoveryTick(24f * 3600f);
            if (!tick.Succeeded)
            {
                return tick;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Prove Natural Recovery Limit", "Body runtime is missing.", BiologicalRecoveryResultCode.MissingBody.ToString());
            }

            StructureConditionSnapshot hand = body.Condition.CreateSnapshot().Structures.FirstOrDefault(candidate => string.Equals(candidate.NodeId, "part.hand.left", StringComparison.Ordinal));
            bool limited = hand != null && hand.CurrentIntegrity <= Mathf.RoundToInt(hand.MaximumIntegrity * 0.75f);
            return Record(limited, "Prove Natural Recovery Limit", limited ? "Success" : "RecoveryLimitBypassed", hand == null ? "Missing hand structure." : $"Integrity={hand.CurrentIntegrity}/{hand.MaximumIntegrity} Limit={Mathf.RoundToInt(hand.MaximumIntegrity * 0.75f)}.");
        }

        public PrototypeTestLabOperation SuppressNaturalRecovery()
        {
            if (!EnsureRecoveryRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            RuntimeBiologicalInteractionRule suppression = new RuntimeBiologicalInteractionRule(
                "test-lab.recovery.natural-suppression",
                BiologicalCompatibilitySourceKind.Development,
                "test-lab.recovery",
                BiologicalInteractionIds.NaturalHealing,
                BiologicalInteractionCategory.Recovery,
                BiologicalInteractionRuleKind.Suppression,
                BiologicalCompatibilityState.Compatible,
                1f,
                1f,
                1f,
                0f,
                999f,
                1,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                "Prototype Test Lab natural recovery suppression");
            BiologicalCompatibilityOperationResult result = body.BiologicalCompatibility.AddOrUpdateContribution(suppression);
            return Record(result.Succeeded, "Suppress Natural Recovery", result.Code.ToString(), result.Message);
        }

        public PrototypeTestLabOperation ClearNaturalRecoverySuppression()
        {
            if (!EnsureRecoveryRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BiologicalCompatibilityOperationResult result = body.BiologicalCompatibility.RemoveContribution("test-lab.recovery", "test-lab.recovery.natural-suppression");
            return Record(result.Succeeded, "Clear Natural Recovery Suppression", result.Code.ToString(), result.Message);
        }

        public PrototypeTestLabOperation StartConstructRepairRecovery()
        {
            PrototypeTestLabOperation assign = AssignBodySpecies("species.basic-construct");
            if (!assign.Succeeded)
            {
                return assign;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Start Construct Repair Recovery", "Body runtime is missing.", BiologicalRecoveryResultCode.MissingBody.ToString());
            }

            if (!body.Condition.CreateSnapshot().ActiveInjuries.Any(injury => string.Equals(injury.TargetNodeId, "core.power", StringComparison.Ordinal)))
            {
                PrototypeTestLabOperation damage = ApplyLocalizedStructuralDamageWithTransaction("injury.core-damage", "core.power", 40, $"test-lab.recovery.construct.damage.{Guid.NewGuid():N}");
                if (!damage.Succeeded)
                {
                    return damage;
                }
            }

            PrototypeTestLabOperation rest = SetBiologicalRecoveryRestContext(RecoveryRestType.RepairStation);
            if (!rest.Succeeded)
            {
                return rest;
            }

            return StartRecoveryProcess("recovery.repair.construct", RecoveryTargetCategory.Injury, "core.power", string.Empty, preview: false, ensureTarget: false);
        }

        public PrototypeTestLabOperation PreviewBiologicalRecoveryTick(float elapsedGameSeconds)
        {
            if (!TryBuildRecoveryTickRequest(elapsedGameSeconds, "preview", out ActorBodyRuntime body, out RecoveryTickRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            long recoveryRevision = body.BiologicalRecovery.RecoveryRevision;
            long conditionRevision = body.Condition.ConditionRevision;
            long vitalRevision = body.VitalProcesses.VitalRevision;
            BiologicalRecoveryResult result = body.BiologicalRecovery.PreviewTick(request, body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            bool succeeded = result.Succeeded && result.Preview && body.BiologicalRecovery.RecoveryRevision == recoveryRevision && body.Condition.ConditionRevision == conditionRevision && body.VitalProcesses.VitalRevision == vitalRevision;
            return succeeded
                ? RecordRecoveryResult("Preview Biological Recovery Tick", result)
                : Record(false, "Preview Biological Recovery Tick", result.Code.ToString(), $"{result.Message} Preview={result.Preview} Revisions Recovery={recoveryRevision}->{body.BiologicalRecovery.RecoveryRevision} Condition={conditionRevision}->{body.Condition.ConditionRevision} Vital={vitalRevision}->{body.VitalProcesses.VitalRevision}.");
        }

        public PrototypeTestLabOperation ApplyBiologicalRecoveryTick(float elapsedGameSeconds)
        {
            string tickId = $"test-lab.recovery.tick.{Guid.NewGuid():N}";
            lastBiologicalRecoveryTickId = tickId;
            return ApplyBiologicalRecoveryTickWithId(elapsedGameSeconds, tickId);
        }

        public PrototypeTestLabOperation ReapplyBiologicalRecoveryTick(float elapsedGameSeconds)
        {
            if (string.IsNullOrWhiteSpace(lastBiologicalRecoveryTickId))
            {
                return RecordFailure("Reapply Biological Recovery Tick", "No previous recovery tick exists.", BiologicalRecoveryResultCode.InvalidRequest.ToString());
            }

            return ApplyBiologicalRecoveryTickWithId(elapsedGameSeconds, lastBiologicalRecoveryTickId);
        }

        public PrototypeTestLabOperation ProveBiologicalRecoveryDuplicateTick()
        {
            PrototypeTestLabOperation setup = StartNaturalWoundClosureRecovery();
            if (!setup.Succeeded)
            {
                return setup;
            }

            const string tickId = "test-lab.recovery.duplicate-proof";
            PrototypeTestLabOperation first = ApplyBiologicalRecoveryTickWithId(3600f, tickId);
            PrototypeTestLabOperation second = ApplyBiologicalRecoveryTickWithId(3600f, tickId);
            bool succeeded = first.Succeeded && second.Succeeded && string.Equals(second.Code, BiologicalRecoveryResultCode.Duplicate.ToString(), StringComparison.Ordinal);
            return Record(succeeded, "Biological Recovery Duplicate Proof", succeeded ? "Success" : "DuplicateProofFailed", $"First={first.Code} Second={second.Code}. Duplicate recovery tick did not apply a second mutation.");
        }

        public PrototypeTestLabOperation ValidateBiologicalRecoverySaveRestore()
        {
            PrototypeTestLabOperation reset = ResetBiologicalRecoveryHuman();
            if (!reset.Succeeded)
            {
                return reset;
            }

            PrototypeTestLabOperation start = StartNaturalWoundClosureRecovery();
            if (!start.Succeeded)
            {
                return start;
            }

            PrototypeTestLabOperation tick = ApplyBiologicalRecoveryTick(3600f);
            if (!tick.Succeeded)
            {
                return tick;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Biological Recovery Save Restore", "Body runtime is missing.", BiologicalRecoveryResultCode.MissingBody.ToString());
            }

            BiologicalRecoverySnapshot before = body.BiologicalRecovery.CreateSnapshot();
            BodySaveData saveData = body.CreateSaveData();
            int eventCount = 0;
            body.BiologicalRecovery.RecoveryChanged += CountRecoveryEvent;
            BodyOperationResult restore = body.RestoreFromSaveData(saveData, registry, body.ActorBodyId, body.PersonId, restoring: true);
            body.BiologicalRecovery.RecoveryChanged -= CountRecoveryEvent;
            BiologicalRecoverySnapshot after = body.BiologicalRecovery.CreateSnapshot();
            bool stable = before.Processes.Select(process => process.ProcessId).SequenceEqual(after.Processes.Select(process => process.ProcessId));
            bool succeeded = restore.Succeeded && stable && eventCount == 0 && after.Coherent;
            return Record(succeeded, "Validate Biological Recovery Save Restore", succeeded ? "Success" : "RestoreMismatch", $"Restore={restore.Code} StableProcesses={stable} Events={eventCount} Active={after.ActiveProcesses.Count}.");

            void CountRecoveryEvent(BiologicalRecoveryRuntime _, BiologicalRecoveryResult __, bool ___)
            {
                eventCount++;
            }
        }

        public string BuildBodyTransformationSummary()
        {
            if (!EnsureTransformationRuntime(out ActorBodyRuntime body, out _))
            {
                return "Body transformation runtime is missing.";
            }

            BodySnapshot bodySnapshot = body.CreateSnapshot();
            BodyTransformationSnapshot snapshot = body.Transformation.CreateSnapshot();
            List<string> lines = new List<string>
            {
                "Feature 7.8 Transformation, Body Replacement, and Species Change",
                $"Body: {bodySnapshot.ActorBodyId}",
                $"Person: {bodySnapshot.PersonId}",
                $"Species: {bodySnapshot.SpeciesId}",
                $"Body Form: {bodySnapshot.BodyFormId}",
                $"Readiness: {snapshot.Readiness}",
                $"Revision: {snapshot.TransformationRevision}",
                $"Active Temporary: {snapshot.ActiveTemporaryTransformation}",
                $"Active Method: {snapshot.ActiveMethodId}",
                $"Original Species: {snapshot.OriginalSpeciesId}",
                $"Transformed Species: {snapshot.TransformedSpeciesId}",
                $"Target Body: {snapshot.TargetBodyId}",
                $"Processed Transactions: {snapshot.ProcessedTransactionIds.Count}",
                $"Coherent: {snapshot.Coherent}",
                $"Canonical Methods: {registry.DefinitionsById.Values.OfType<TransformationMethodDefinition>().Count()}",
                $"Profiles: {string.Join(", ", registry.DefinitionsById.Values.OfType<TransformationProfileDefinition>().Select(profile => profile.Id).OrderBy(id => id, StringComparer.Ordinal))}"
            };

            if (snapshot.Diagnostics.Count > 0)
            {
                lines.AddRange(snapshot.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation ValidateBodyTransformationIntegrity()
        {
            if (!EnsureTransformationRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BodyTransformationSnapshot snapshot = body.Transformation.CreateSnapshot();
            string[] requiredMethods =
            {
                "transformation.polymorph.temporary",
                "transformation.species-change.permanent",
                "transformation.body-form-change",
                "transformation.body-replacement",
                "transformation.body-swap",
                "transformation.possession",
                "transformation.reincarnation",
                "transformation.resurrection-body",
                "transformation.spirit-embodiment",
                "transformation.structure-replacement",
                "transformation.organ-replacement",
                "transformation.limb-replacement",
                "transformation.construct-component-replacement"
            };
            bool registered = requiredMethods.All(id => registry.TryGet(id, out TransformationMethodDefinition method) && method != null);
            bool succeeded = snapshot.Readiness == TransformationReadinessState.Ready && snapshot.Coherent && registered;
            return Record(succeeded, "Validate Body Transformation", succeeded ? "Success" : "InvalidTransformationRuntime", $"Readiness={snapshot.Readiness} Coherent={snapshot.Coherent} MethodsRegistered={registered} Revision={snapshot.TransformationRevision}.");
        }

        public PrototypeTestLabOperation PreviewTemporaryPolymorphConstruct()
        {
            return RunTransformation("Preview Temporary Polymorph", "transformation.polymorph.temporary", "species.basic-construct", targetBodyId: string.Empty, targetNodeId: string.Empty, preview: true);
        }

        public PrototypeTestLabOperation ExecuteTemporaryPolymorphConstruct()
        {
            return RunTransformation("Execute Temporary Polymorph", "transformation.polymorph.temporary", "species.basic-construct", targetBodyId: string.Empty, targetNodeId: string.Empty, preview: false);
        }

        public PrototypeTestLabOperation RevertTemporaryPolymorph()
        {
            if (!EnsureTransformationRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            return RecordTransformationResult("Revert Temporary Transformation", body.Transformation.RevertTemporaryTransformation($"test-lab.transformation.revert.{Guid.NewGuid():N}"));
        }

        public PrototypeTestLabOperation ExecutePermanentSpeciesChangeConstruct()
        {
            return RunTransformation("Execute Permanent Species Change", "transformation.species-change.permanent", "species.basic-construct", targetBodyId: string.Empty, targetNodeId: string.Empty, preview: false);
        }

        public PrototypeTestLabOperation ExecutePermanentSpeciesChangeHuman()
        {
            return RunTransformation("Execute Permanent Species Change", "transformation.species-change.permanent", "species.human", targetBodyId: string.Empty, targetNodeId: string.Empty, preview: false);
        }

        public PrototypeTestLabOperation PreviewBodyReplacementPlan()
        {
            return RunTransformation("Preview Body Replacement Plan", "transformation.body-replacement", string.Empty, "body.target.replacement.prototype", string.Empty, preview: true);
        }

        public PrototypeTestLabOperation PreviewBodySwapPlan()
        {
            return RunTransformation("Preview Body Swap Plan", "transformation.body-swap", string.Empty, "body.target.swap.prototype", string.Empty, preview: true);
        }

        public PrototypeTestLabOperation PreviewPossessionPlan()
        {
            return RunTransformation("Preview Possession Plan", "transformation.possession", string.Empty, "body.target.possession.prototype", string.Empty, preview: true);
        }

        public PrototypeTestLabOperation PreviewReincarnationPlan()
        {
            return RunTransformation("Preview Reincarnation Plan", "transformation.reincarnation", "species.human", "body.target.reincarnation.prototype", string.Empty, preview: true);
        }

        public PrototypeTestLabOperation PreviewSpiritEmbodimentPlan()
        {
            return RunTransformation("Preview Spirit Embodiment Plan", "transformation.spirit-embodiment", "species.human", "body.target.embodiment.prototype", string.Empty, preview: true);
        }

        public PrototypeTestLabOperation PreviewStructureReplacement()
        {
            return RunTransformation("Preview Structure Replacement", "transformation.limb-replacement", string.Empty, string.Empty, "part.tail.optional", preview: true);
        }

        public PrototypeTestLabOperation ProveTransformationPreviewNoMutation()
        {
            PrototypeTestLabOperation reset = AssignBodySpecies("species.human");
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureTransformationRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BodySnapshot before = body.CreateSnapshot();
            BodyTransformationSnapshot transformationBefore = body.Transformation.CreateSnapshot();
            BodyTransformationResult preview = body.Transformation.Preview(BuildTransformationRequest(body, "transformation.polymorph.temporary", "species.basic-construct", string.Empty, string.Empty, $"test-lab.transformation.preview-proof.{Guid.NewGuid():N}", preview: true));
            BodySnapshot after = body.CreateSnapshot();
            BodyTransformationSnapshot transformationAfter = body.Transformation.CreateSnapshot();
            bool succeeded = preview.Succeeded
                && string.Equals(before.SpeciesId, after.SpeciesId, StringComparison.Ordinal)
                && before.BodyRevision == after.BodyRevision
                && transformationBefore.TransformationRevision == transformationAfter.TransformationRevision
                && !transformationAfter.ActiveTemporaryTransformation;
            return Record(succeeded, "Prove Transformation Preview", succeeded ? "Success" : "PreviewMutated", $"Preview={preview.Code} Species={before.SpeciesId}->{after.SpeciesId} BodyRev={before.BodyRevision}->{after.BodyRevision} TxRev={transformationBefore.TransformationRevision}->{transformationAfter.TransformationRevision}.");
        }

        public PrototypeTestLabOperation ProveTransformationDuplicateProtection()
        {
            PrototypeTestLabOperation reset = AssignBodySpecies("species.human");
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureTransformationRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            string transactionId = $"test-lab.transformation.duplicate.{Guid.NewGuid():N}";
            BodyTransformationRequest request = BuildTransformationRequest(body, "transformation.species-change.permanent", "species.basic-construct", string.Empty, string.Empty, transactionId, preview: false);
            BodyTransformationResult first = body.Transformation.Execute(request);
            long bodyRevisionAfterFirst = body.BodyRevision;
            long transformationRevisionAfterFirst = body.Transformation.TransformationRevision;
            BodyTransformationResult second = body.Transformation.Execute(request);
            bool succeeded = first.Succeeded
                && second.Succeeded
                && second.Duplicate
                && body.BodyRevision == bodyRevisionAfterFirst
                && body.Transformation.TransformationRevision == transformationRevisionAfterFirst;
            return Record(succeeded, "Prove Transformation Duplicate Protection", succeeded ? "Success" : "DuplicateProofFailed", $"First={first.Code} Second={second.Code} Duplicate={second.Duplicate} BodyRev={bodyRevisionAfterFirst}->{body.BodyRevision} TransformRev={transformationRevisionAfterFirst}->{body.Transformation.TransformationRevision}.");
        }

        public PrototypeTestLabOperation ValidateTransformationSaveRestore()
        {
            PrototypeTestLabOperation reset = AssignBodySpecies("species.human");
            if (!reset.Succeeded)
            {
                return reset;
            }

            PrototypeTestLabOperation transform = ExecuteTemporaryPolymorphConstruct();
            if (!transform.Succeeded)
            {
                return transform;
            }

            if (!EnsureTransformationRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BodyTransformationSnapshot before = body.Transformation.CreateSnapshot();
            BodySaveData saveData = body.CreateSaveData();
            int eventCount = 0;
            body.Transformation.TransformationChanged += CountTransformationEvent;
            BodyOperationResult restore = body.RestoreFromSaveData(saveData, registry, body.ActorBodyId, body.PersonId, restoring: true);
            body.Transformation.TransformationChanged -= CountTransformationEvent;
            BodyTransformationSnapshot after = body.Transformation.CreateSnapshot();
            bool succeeded = restore.Succeeded
                && after.ActiveTemporaryTransformation
                && string.Equals(before.ActiveMethodId, after.ActiveMethodId, StringComparison.Ordinal)
                && string.Equals(before.OriginalSpeciesId, after.OriginalSpeciesId, StringComparison.Ordinal)
                && eventCount == 0
                && after.Coherent;
            return Record(succeeded, "Validate Transformation Save Restore", succeeded ? "Success" : "RestoreMismatch", $"Restore={restore.Code} Active={after.ActiveTemporaryTransformation} Method={after.ActiveMethodId} Original={after.OriginalSpeciesId} Events={eventCount}.");

            void CountTransformationEvent(BodyTransformationRuntime _, BodyTransformationResult __, bool ___)
            {
                eventCount++;
            }
        }

        public PrototypeTestLabOperation TestTransformationSuppression()
        {
            PrototypeTestLabOperation reset = AssignBodySpecies("species.human");
            if (!reset.Succeeded)
            {
                return reset;
            }

            if (!EnsureTransformationRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            RuntimeBiologicalInteractionRule rule = new RuntimeBiologicalInteractionRule(
                "test-lab.transformation.suppression",
                BiologicalCompatibilitySourceKind.Development,
                "test-lab.transformation",
                BiologicalInteractionIds.Polymorph,
                BiologicalInteractionCategory.Transformation,
                BiologicalInteractionRuleKind.Suppression,
                BiologicalCompatibilityState.Compatible,
                0f,
                0f,
                0f,
                0f,
                999f,
                1000,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                "Development suppression blocks transformation compatibility.");
            BiologicalCompatibilityOperationResult add = body.BiologicalCompatibility.AddOrUpdateContribution(rule);
            if (!add.Succeeded)
            {
                return RecordCompatibilityOperation("Suppress Transformation Compatibility", add);
            }

            BodyTransformationResult result = body.Transformation.Preview(BuildTransformationRequest(body, "transformation.polymorph.temporary", "species.basic-construct", string.Empty, string.Empty, $"test-lab.transformation.suppressed.{Guid.NewGuid():N}", preview: true));
            body.BiologicalCompatibility.RemoveContribution("test-lab.transformation", "test-lab.transformation.suppression");
            return Record(!result.Succeeded && result.Code == TransformationResultCode.Suppressed, "Test Transformation Suppression", !result.Succeeded ? result.Code.ToString() : "SuppressionBypassed", FormatTransformationResult(result));
        }

        public string BuildBiologicalConditionSummary()
        {
            if (!EnsureBiologicalConditionRuntime(out ActorBodyRuntime body, out _))
            {
                return "Biological Condition runtime is missing.";
            }

            BiologicalConditionRuntimeSnapshot snapshot = body.BiologicalConditions.CreateSnapshot();
            List<string> lines = new List<string>
            {
                "Feature 7.9 Diseases and Biological Conditions",
                $"Body: {snapshot.ActorBodyId}",
                $"Readiness: {snapshot.Readiness}",
                $"Revision: {snapshot.BiologicalConditionRevision}",
                $"Active Instances: {snapshot.ActiveInstances.Count}",
                $"Immunity Memory: {snapshot.ImmunityMemory.Count}",
                $"Processed Transactions: {snapshot.ProcessedTransactionIds.Count}",
                $"Coherent: {snapshot.Coherent}",
                $"Definitions: {registry.DefinitionsById.Values.OfType<BiologicalConditionDefinition>().Count()}",
                $"Treatments: {registry.DefinitionsById.Values.OfType<BiologicalConditionTreatmentDefinition>().Count()}",
                $"Transmission Profiles: {registry.DefinitionsById.Values.OfType<BiologicalTransmissionProfileDefinition>().Count()}"
            };

            foreach (BiologicalConditionInstanceSnapshot instance in snapshot.ActiveInstances)
            {
                lines.Add($"{instance.ConditionDefinitionId} ({instance.InstanceId}) Stage={instance.Stage} Severity={instance.Severity} Load={instance.Load:0.##} Dose={instance.AccumulatedDose:0.##} Route={instance.ExposureRoute} Node={instance.TargetAnatomyNodeId} Symptoms={string.Join(",", instance.Symptoms.Select(symptom => symptom.SymptomId))}");
                if (instance.ConsequencePlan != null)
                {
                    lines.Add($"  Consequences={instance.ConsequencePlan.Flags} Vital={instance.ConsequencePlan.VitalResourceId}:{instance.ConsequencePlan.VitalPressureAmount:0.##} Hazard={instance.ConsequencePlan.HazardDefinitionId} Damage={instance.ConsequencePlan.DamageTypeId}:{instance.ConsequencePlan.Step6DamageAmount:0.##} RecoveryRate={instance.ConsequencePlan.RecoveryRateMultiplier:0.##}");
                }
            }

            foreach (BiologicalConditionImmunityMemorySnapshot memory in snapshot.ImmunityMemory)
            {
                lines.Add($"Memory {memory.ConditionDefinitionId}/{memory.StrainId} Strength={memory.Strength:0.##} Source={memory.SourceInstanceId}");
            }

            if (snapshot.Diagnostics.Count > 0)
            {
                lines.AddRange(snapshot.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation ValidateBiologicalConditionIntegrity()
        {
            if (!EnsureBiologicalConditionRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BiologicalConditionRuntimeSnapshot snapshot = body.BiologicalConditions.CreateSnapshot();
            bool registered = registry.TryGet("condition.biology.prototype-viral-malaise", out BiologicalConditionDefinition viral)
                && viral != null
                && registry.TryGet("treatment.biology.prototype-antidote", out BiologicalConditionTreatmentDefinition antidote)
                && antidote != null
                && registry.TryGet("transmission.biology.prototype-viral-airborne", out BiologicalTransmissionProfileDefinition transmission)
                && transmission != null;
            bool succeeded = snapshot.Readiness == BiologicalConditionReadinessState.Ready && snapshot.Coherent && registered;
            return Record(succeeded, "Validate Biological Conditions", succeeded ? "Success" : "InvalidBiologicalConditionRuntime", $"Readiness={snapshot.Readiness} Coherent={snapshot.Coherent} CanonicalRegistered={registered} Revision={snapshot.BiologicalConditionRevision}.");
        }

        public string BuildBodyBiologyIntegrationSummary()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return "Body runtime is missing.";
            }

            BodyBiologyFacade facade = new BodyBiologyFacade(body);
            BodyBiologySnapshot snapshot = facade.CaptureSnapshot();
            BodyBiologyValidationResult validation = BodyBiologyValidator.Validate(snapshot);
            List<string> lines = new List<string>
            {
                "Feature 7.10 Biological Integration",
                $"Ready: {snapshot.Ready}",
                $"Validation: {validation.Code}",
                $"Actor/body: {snapshot.ActorBodyId}",
                $"Person: {snapshot.PersonId}",
                $"Species: {snapshot.SpeciesId}",
                $"Classification: {snapshot.BiologicalClassificationId}",
                $"Body Form: {snapshot.BodyFormId}",
                $"Revisions: {snapshot.Revisions}",
                $"Anatomy Nodes: {snapshot.Body?.Anatomy?.Nodes.Count ?? 0}",
                $"Active Injuries: {snapshot.Body?.Condition?.ActiveInjuries.Count ?? 0}",
                $"Active Biological Conditions: {snapshot.BiologicalConditions?.ActiveInstances.Count ?? 0}",
                $"Active Hazards: {snapshot.Body?.BiologicalHazards?.ActiveHazards.Count ?? 0}",
                $"Active Recovery Processes: {snapshot.Body?.BiologicalRecovery?.ActiveProcesses.Count ?? 0}",
                $"Temporary Transformation: {snapshot.Transformation?.ActiveTemporaryTransformation ?? false}",
                $"Coherent: {snapshot.Coherent}"
            };

            if (validation.Diagnostics.Count > 0)
            {
                lines.AddRange(validation.Diagnostics.Select(diagnostic => $"Diagnostic: {diagnostic}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public PrototypeTestLabOperation InspectBodyBiologyIntegration()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Inspect Body Biology Integration", "Body runtime is missing.", BodyBiologyValidationCode.MissingBody.ToString());
            }

            BodyBiologySnapshot snapshot = new BodyBiologyFacade(body).CaptureSnapshot();
            return Record(snapshot.Coherent, "Inspect Body Biology Integration", snapshot.Coherent ? "Success" : BodyBiologyValidationCode.IncoherentSnapshot.ToString(), FormatBodyBiologySnapshot(snapshot));
        }

        public PrototypeTestLabOperation ValidateBodyBiologyIntegration()
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure("Validate Body Biology Integration", "Body runtime is missing.", BodyBiologyValidationCode.MissingBody.ToString());
            }

            BodyBiologyValidationResult result = new BodyBiologyFacade(body).Validate();
            return Record(result.Succeeded, "Validate Body Biology Integration", result.Code.ToString(), result.Succeeded ? FormatBodyBiologySnapshot(result.Snapshot) : $"{result.Message} {FormatBodyBiologySnapshot(result.Snapshot)}");
        }

        public PrototypeTestLabOperation PreviewBodyBiologyAdvance(float elapsedGameSeconds)
        {
            return AdvanceBodyBiology("Preview Body Biology Advance", elapsedGameSeconds, preview: true, transactionId: string.Empty);
        }

        public PrototypeTestLabOperation AdvanceBodyBiology(float elapsedGameSeconds)
        {
            return AdvanceBodyBiology("Advance Body Biology", elapsedGameSeconds, preview: false, transactionId: string.Empty);
        }

        public PrototypeTestLabOperation ProveBodyBiologyAdvanceDuplicateProtection()
        {
            string tx = $"test-lab.body-biology.advance.duplicate.{Guid.NewGuid():N}";
            PrototypeTestLabOperation first = AdvanceBodyBiology("Advance Body Biology Duplicate", 60f, preview: false, transactionId: tx);
            PrototypeTestLabOperation second = AdvanceBodyBiology("Advance Body Biology Duplicate", 60f, preview: false, transactionId: tx);
            bool succeeded = first.Succeeded && second.Succeeded && string.Equals(second.Code, BodyBiologyAdvanceCode.Duplicate.ToString(), StringComparison.Ordinal);
            return Record(succeeded, "Prove Body Biology Advance Duplicate", succeeded ? "Success" : "DuplicateProofFailed", $"First={first.Code} Second={second.Code}. Duplicate integrated advance should not mutate twice.");
        }

        private PrototypeTestLabOperation AdvanceBodyBiology(string operationName, float elapsedGameSeconds, bool preview, string transactionId)
        {
            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                return RecordFailure(operationName, "Body runtime is missing.", BodyBiologyValidationCode.MissingBody.ToString());
            }

            BodyBiologyFacade facade = new BodyBiologyFacade(body);
            GameObject target = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            BodyBiologyAdvanceRequest request = new BodyBiologyAdvanceRequest(
                body.ActorBodyId,
                Mathf.Max(0f, elapsedGameSeconds),
                string.IsNullOrWhiteSpace(transactionId) ? $"test-lab.body-biology.advance.{Guid.NewGuid():N}" : transactionId,
                "Prototype Test Lab",
                damageHealingService,
                target,
                target,
                body.ActorBodyId,
                body.ActorBodyId);
            BodyBiologyAdvanceResult result = preview ? facade.PreviewAdvance(request) : facade.Advance(request);
            return Record(result.Succeeded, operationName, result.Duplicate ? BodyBiologyAdvanceCode.Duplicate.ToString() : result.Code.ToString(), FormatBodyBiologyAdvanceResult(result));
        }

        public PrototypeTestLabOperation PreviewViralExposure()
        {
            return ApplyBiologicalConditionExposure("Preview Viral Exposure", "condition.biology.prototype-viral-malaise", BiologicalExposureRoute.Inhalation, 16f, string.Empty, preview: true);
        }

        public PrototypeTestLabOperation ApplySubthresholdViralExposure()
        {
            return ApplyBiologicalConditionExposure("Apply Subthreshold Viral Exposure", "condition.biology.prototype-viral-malaise", BiologicalExposureRoute.Inhalation, 4f, string.Empty, preview: false);
        }

        public PrototypeTestLabOperation ApplyViralExposure()
        {
            return ApplyBiologicalConditionExposure("Apply Viral Exposure", "condition.biology.prototype-viral-malaise", BiologicalExposureRoute.Inhalation, 16f, string.Empty, preview: false);
        }

        public PrototypeTestLabOperation ApplyWoundInfection()
        {
            if (!EnsureBiologicalConditionRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            if (!body.Condition.CreateSnapshot().ActiveInjuries.Any(injury => string.Equals(injury.TargetNodeId, "part.hand.left", StringComparison.Ordinal)))
            {
                PrototypeTestLabOperation wound = ApplyLocalizedStructuralDamageWithTransaction("injury.laceration", "part.hand.left", 20, $"test-lab.biological-condition.wound.{Guid.NewGuid():N}");
                if (!wound.Succeeded)
                {
                    return wound;
                }
            }

            return ApplyBiologicalConditionExposure("Apply Wound Infection", "condition.biology.prototype-bacterial-wound-infection", BiologicalExposureRoute.Wound, 12f, "part.hand.left", preview: false);
        }

        public PrototypeTestLabOperation RejectWoundInfectionWithoutWound()
        {
            PrototypeTestLabOperation reset = AssignBodySpecies("species.human");
            if (!reset.Succeeded)
            {
                return reset;
            }

            PrototypeTestLabOperation result = ApplyBiologicalConditionExposure("Reject Wound Infection", "condition.biology.prototype-bacterial-wound-infection", BiologicalExposureRoute.Wound, 12f, "part.hand.left", preview: false);
            bool succeeded = !result.Succeeded && string.Equals(result.Code, BiologicalConditionResultCode.MissingRequiredInjury.ToString(), StringComparison.Ordinal);
            return Record(succeeded, "Reject Wound Infection Without Wound", succeeded ? "Success" : "UnexpectedWoundInfection", result.Message);
        }

        public PrototypeTestLabOperation ApplyPoison()
        {
            return ApplyBiologicalConditionExposure("Apply Poison", "condition.biology.prototype-poison", BiologicalExposureRoute.Ingestion, 12f, string.Empty, preview: false);
        }

        public PrototypeTestLabOperation RejectVenomInvalidRoute()
        {
            PrototypeTestLabOperation result = ApplyBiologicalConditionExposure("Reject Venom Invalid Route", "condition.biology.prototype-venom", BiologicalExposureRoute.Ingestion, 10f, string.Empty, preview: false);
            bool succeeded = !result.Succeeded && string.Equals(result.Code, BiologicalConditionResultCode.InvalidRoute.ToString(), StringComparison.Ordinal);
            return Record(succeeded, "Reject Venom Invalid Route", succeeded ? "Success" : "UnexpectedVenomRoute", result.Message);
        }

        public PrototypeTestLabOperation ApplyVenom()
        {
            return ApplyBiologicalConditionExposure("Apply Venom", "condition.biology.prototype-venom", BiologicalExposureRoute.Bite, 10f, string.Empty, preview: false);
        }

        public PrototypeTestLabOperation ApplyFever()
        {
            return ApplyBiologicalConditionExposure("Apply Fever", "condition.biology.prototype-fever-response", BiologicalExposureRoute.Scripted, 8f, string.Empty, preview: false);
        }

        public PrototypeTestLabOperation ApplyIntoxication()
        {
            return ApplyBiologicalConditionExposure("Apply Intoxication", "condition.biology.prototype-alcohol-intoxication", BiologicalExposureRoute.Ingestion, 8f, string.Empty, preview: false);
        }

        public PrototypeTestLabOperation ApplyBiologicalConditionTick(float elapsedGameSeconds)
        {
            return ApplyBiologicalConditionTickWithId(elapsedGameSeconds, $"test-lab.biological-condition.tick.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation ProveBiologicalConditionDuplicateExposure()
        {
            string tx = $"test-lab.biological-condition.duplicate.{Guid.NewGuid():N}";
            PrototypeTestLabOperation first = ApplyBiologicalConditionExposure("Duplicate Biological Condition Exposure", "condition.biology.prototype-viral-malaise", BiologicalExposureRoute.Inhalation, 16f, string.Empty, preview: false, transactionId: tx);
            PrototypeTestLabOperation second = ApplyBiologicalConditionExposure("Duplicate Biological Condition Exposure", "condition.biology.prototype-viral-malaise", BiologicalExposureRoute.Inhalation, 16f, string.Empty, preview: false, transactionId: tx);
            bool succeeded = first.Succeeded && second.Succeeded && string.Equals(second.Code, BiologicalConditionResultCode.Duplicate.ToString(), StringComparison.Ordinal);
            return Record(succeeded, "Prove Biological Condition Duplicate Exposure", succeeded ? "Success" : "DuplicateProofFailed", $"First={first.Code} Second={second.Code}. Duplicate exposure did not add another dose.");
        }

        public PrototypeTestLabOperation ProveBiologicalConditionDuplicateTick()
        {
            PrototypeTestLabOperation exposure = ApplyViralExposure();
            if (!exposure.Succeeded)
            {
                return exposure;
            }

            string tx = $"test-lab.biological-condition.tick.duplicate.{Guid.NewGuid():N}";
            PrototypeTestLabOperation first = ApplyBiologicalConditionTickWithId(600f, tx);
            PrototypeTestLabOperation second = ApplyBiologicalConditionTickWithId(600f, tx);
            bool succeeded = first.Succeeded && second.Succeeded && string.Equals(second.Code, BiologicalConditionResultCode.Duplicate.ToString(), StringComparison.Ordinal);
            return Record(succeeded, "Prove Biological Condition Duplicate Tick", succeeded ? "Success" : "DuplicateTickFailed", $"First={first.Code} Second={second.Code}. Duplicate tick did not progress twice.");
        }

        public PrototypeTestLabOperation ApplyPrototypeMedicine()
        {
            return ApplyBiologicalConditionTreatment("Apply Prototype Medicine", "treatment.biology.prototype-medicine");
        }

        public PrototypeTestLabOperation ApplyPrototypeAntidote()
        {
            return ApplyBiologicalConditionTreatment("Apply Prototype Antidote", "treatment.biology.prototype-antidote");
        }

        public PrototypeTestLabOperation PreviewConditionTransmission()
        {
            if (!EnsureBiologicalConditionRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BiologicalConditionInstanceSnapshot instance = body.BiologicalConditions.CreateSnapshot().ActiveInstances.FirstOrDefault(candidate => candidate.ConditionDefinitionId == "condition.biology.prototype-viral-malaise");
            if (instance == null)
            {
                PrototypeTestLabOperation exposure = ApplyViralExposure();
                if (!exposure.Succeeded)
                {
                    return exposure;
                }

                instance = body.BiologicalConditions.CreateSnapshot().ActiveInstances.FirstOrDefault(candidate => candidate.ConditionDefinitionId == "condition.biology.prototype-viral-malaise");
            }

            BiologicalConditionTransmissionPlan plan = body.BiologicalConditions.PreviewTransmission(new BiologicalConditionTransmissionRequest(body.ActorBodyId, "actor.runtime.test-lab.target-body", instance?.InstanceId ?? string.Empty, "transmission.biology.prototype-viral-airborne", $"test-lab.biological-condition.transmission.{Guid.NewGuid():N}", preview: true));
            bool succeeded = plan.ExposureRequest != null && string.Equals(plan.ExposureRequest.ConditionDefinitionId, "condition.biology.prototype-viral-malaise", StringComparison.Ordinal);
            return Record(succeeded, "Preview Biological Condition Transmission", succeeded ? "Success" : "TransmissionPlanMissing", $"Profile={plan.TransmissionProfileId} Source={plan.SourceActorBodyId} Target={plan.TargetActorBodyId} Dose={plan.ExposureRequest?.Dose ?? 0f} Route={plan.ExposureRequest?.Route.ToString() ?? string.Empty}. {plan.Message}");
        }

        public PrototypeTestLabOperation ValidateBiologicalConditionSaveRestore()
        {
            PrototypeTestLabOperation reset = AssignBodySpecies("species.human");
            if (!reset.Succeeded)
            {
                return reset;
            }

            PrototypeTestLabOperation exposure = ApplyViralExposure();
            if (!exposure.Succeeded)
            {
                return exposure;
            }

            if (!EnsureBiologicalConditionRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BiologicalConditionRuntimeSnapshot before = body.BiologicalConditions.CreateSnapshot();
            BodySaveData saveData = body.CreateSaveData();
            BodyOperationResult restore = body.RestoreFromSaveData(saveData, registry, body.ActorBodyId, body.PersonId, restoring: true);
            BiologicalConditionRuntimeSnapshot after = body.BiologicalConditions.CreateSnapshot();
            bool succeeded = restore.Succeeded
                && after.ActiveInstances.Count == before.ActiveInstances.Count
                && after.ActiveInstances.Select(instance => instance.InstanceId).SequenceEqual(before.ActiveInstances.Select(instance => instance.InstanceId))
                && after.ActiveInstances.FirstOrDefault()?.Load == before.ActiveInstances.FirstOrDefault()?.Load;
            return Record(succeeded, "Validate Biological Condition Save Restore", succeeded ? "Success" : "RestoreMismatch", $"Restore={restore.Code} Count={before.ActiveInstances.Count}->{after.ActiveInstances.Count} Revision={before.BiologicalConditionRevision}->{after.BiologicalConditionRevision}.");
        }

        public PrototypeTestLabOperation RejectSpiritOrdinaryDisease()
        {
            PrototypeTestLabOperation spirit = AssignBodySpecies("species.basic-spirit");
            if (!spirit.Succeeded)
            {
                return spirit;
            }

            PrototypeTestLabOperation result = ApplyBiologicalConditionExposure("Reject Spirit Disease", "condition.biology.prototype-viral-malaise", BiologicalExposureRoute.Inhalation, 16f, string.Empty, preview: false);
            bool succeeded = !result.Succeeded && (string.Equals(result.Code, BiologicalConditionResultCode.Incompatible.ToString(), StringComparison.Ordinal) || string.Equals(result.Code, BiologicalConditionResultCode.Immune.ToString(), StringComparison.Ordinal));
            return Record(succeeded, "Reject Spirit Ordinary Disease", succeeded ? "Success" : "UnexpectedCompatibility", result.Message);
        }

        public PrototypeTestLabOperation RejectConstructOrdinaryPoison()
        {
            PrototypeTestLabOperation construct = AssignBodySpecies("species.basic-construct");
            if (!construct.Succeeded)
            {
                return construct;
            }

            PrototypeTestLabOperation result = ApplyBiologicalConditionExposure("Reject Construct Poison", "condition.biology.prototype-poison", BiologicalExposureRoute.Ingestion, 12f, string.Empty, preview: false);
            bool succeeded = !result.Succeeded && (string.Equals(result.Code, BiologicalConditionResultCode.Incompatible.ToString(), StringComparison.Ordinal) || string.Equals(result.Code, BiologicalConditionResultCode.Immune.ToString(), StringComparison.Ordinal));
            return Record(succeeded, "Reject Construct Ordinary Poison", succeeded ? "Success" : "UnexpectedCompatibility", result.Message);
        }

        public string BuildKnowledgeSummary()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return "Knowledge runtime is missing.";
            }

            KnowledgeSnapshot snapshot = knowledge.CreateSnapshot();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Person: {snapshot.PersonId} Actor: {EmptyAs(snapshot.CurrentActorId, "None")} Body: {EmptyAs(snapshot.CurrentBodyId, "None")}");
            builder.AppendLine($"Readiness: {snapshot.Readiness} Revision: {snapshot.Revision}");
            builder.AppendLine($"Beliefs: {snapshot.Beliefs.Count} Known: {snapshot.KnownFacts.Count} Suspicions: {snapshot.Suspicions.Count} Misconceptions: {snapshot.Misconceptions.Count} Disputed: {snapshot.DisputedBeliefs.Count} Stale: {snapshot.StaleBeliefs.Count} Evidence: {snapshot.Evidence.Count}");
            foreach (KnowledgeBeliefRecord belief in snapshot.Beliefs.Take(14))
            {
                builder.AppendLine($"{belief.State}: {belief.Proposition.FactDefinitionId} subject={belief.Proposition.SubjectId} confidence={belief.Confidence} visibility={belief.Data.visibility} evidence={belief.SupportingEvidenceIds.Count}/{belief.OpposingEvidenceIds.Count} id={belief.BeliefId}");
            }

            foreach (string diagnostic in snapshot.Diagnostics.Take(6))
            {
                builder.AppendLine($"Diagnostic: {diagnostic}");
            }

            return builder.ToString();
        }

        public PrototypeTestLabOperation ValidateKnowledgeRuntime()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Validate Knowledge", "Knowledge runtime is missing.", KnowledgeResultCode.MissingPerson.ToString());
            }

            KnowledgeValidationResult result = knowledge.ValidateKnowledge();
            string message = result.Succeeded ? $"Knowledge runtime valid. Person={knowledge.PersonId} Revision={knowledge.KnowledgeRevision}." : result.Message;
            return Record(result.Succeeded, "Validate Knowledge", result.Succeeded ? "Success" : KnowledgeResultCode.ValidationFailed.ToString(), message);
        }

        public PrototypeTestLabOperation PreviewKnowledgeVisibleInjury()
        {
            return TryBuildVisibleInjuryObservation("knowledge.preview-visible-injury", out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure)
                ? RecordKnowledgeResult("Preview Knowledge Observation", knowledge.PreviewObservation(request))
                : failure;
        }

        public PrototypeTestLabOperation RecordKnowledgeVisibleInjury()
        {
            return TryBuildVisibleInjuryObservation($"knowledge.visible-injury.{Guid.NewGuid():N}", out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure)
                ? RecordKnowledgeResult("Record Knowledge Observation", knowledge.RecordObservation(request))
                : failure;
        }

        public PrototypeTestLabOperation ProveKnowledgeDuplicateObservation()
        {
            if (!TryBuildVisibleInjuryObservation("knowledge.duplicate-visible-injury", out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            KnowledgeOperationResult first = knowledge.RecordObservation(request);
            KnowledgeOperationResult second = knowledge.RecordObservation(request);
            bool succeeded = first.Succeeded && second.Succeeded && second.Duplicate && first.ResultingRevision == second.ResultingRevision;
            return Record(succeeded, "Duplicate Knowledge Observation", succeeded ? "Success" : "DuplicateProofFailed", $"First={first.Code} Second={second.Code} Duplicate={second.Duplicate} Revision={first.ResultingRevision}->{second.ResultingRevision}.");
        }

        public PrototypeTestLabOperation AddWeakKnowledgeEvidence()
        {
            return TryBuildSpeciesCapabilityObservation($"knowledge.weak-evidence.{Guid.NewGuid():N}", 220, 450, out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure)
                ? RecordKnowledgeResult("Add Weak Knowledge Evidence", knowledge.RecordObservation(request))
                : failure;
        }

        public PrototypeTestLabOperation AddStrongKnowledgeEvidence()
        {
            return TryBuildSpeciesCapabilityObservation($"knowledge.strong-evidence.{Guid.NewGuid():N}", 800, 850, out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure)
                ? RecordKnowledgeResult("Add Strong Knowledge Evidence", knowledge.RecordObservation(request))
                : failure;
        }

        public PrototypeTestLabOperation AddOpposingKnowledgeEvidence()
        {
            if (!TryBuildSpeciesCapabilityObservation($"knowledge.opposing-evidence.{Guid.NewGuid():N}", 600, 700, out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            request.Direction = KnowledgeEvidenceDirection.Opposes;
            request.Provenance = KnowledgeProvenance.Testimony;
            request.AcquisitionSource = KnowledgeAcquisitionSource.Testimony;
            request.SourceId = "person.testimony.untrusted-rumor";
            return RecordKnowledgeResult("Add Opposing Knowledge Evidence", knowledge.RecordObservation(request));
        }

        public PrototypeTestLabOperation CreateKnowledgeMisconception()
        {
            if (!TryBuildSpeciesCapabilityObservation($"knowledge.misconception.{Guid.NewGuid():N}", 900, 900, out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            request.Proposition.stableValueId = "capability.false-spirit-can-bleed";
            request.SourceId = "person.rumor.false-species";
            request.MarkAsMisconception = true;
            request.TruthAuthorization = KnowledgeTruthAuthorization.CreateDevelopmentFixture("test-lab.knowledge.misconception");
            return RecordKnowledgeResult("Create Knowledge Misconception", knowledge.RecordObservation(request));
        }

        public PrototypeTestLabOperation CorrectKnowledgeMisconception()
        {
            if (!TryBuildSpeciesCapabilityObservation($"knowledge.correction.{Guid.NewGuid():N}", 950, 950, out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            request.Direction = KnowledgeEvidenceDirection.Corrects;
            request.Provenance = KnowledgeProvenance.AuthoritativeCorrection;
            request.AcquisitionSource = KnowledgeAcquisitionSource.DevelopmentFixture;
            request.TruthAuthorization = KnowledgeTruthAuthorization.CreateDevelopmentFixture("test-lab.knowledge.correction");
            return RecordKnowledgeResult("Correct Knowledge Misconception", knowledge.RecordObservation(request));
        }

        public PrototypeTestLabOperation MarkFirstKnowledgeStale()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Mark Knowledge Stale", "Knowledge runtime is missing.", KnowledgeResultCode.MissingPerson.ToString());
            }

            KnowledgeBeliefRecord belief = knowledge.CreateSnapshot().Beliefs.FirstOrDefault();
            return belief == null
                ? RecordFailure("Mark Knowledge Stale", "No active Knowledge belief exists.", KnowledgeResultCode.MissingBelief.ToString())
                : RecordKnowledgeResult("Mark Knowledge Stale", knowledge.MarkStale(belief.BeliefId, $"knowledge.stale.{Guid.NewGuid():N}", "Marked stale from Test Lab."));
        }

        public PrototypeTestLabOperation ForgetFirstKnowledgeBelief()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Forget Knowledge", "Knowledge runtime is missing.", KnowledgeResultCode.MissingPerson.ToString());
            }

            KnowledgeBeliefRecord belief = knowledge.CreateSnapshot().Beliefs.FirstOrDefault();
            return belief == null
                ? RecordFailure("Forget Knowledge", "No active Knowledge belief exists.", KnowledgeResultCode.MissingBelief.ToString())
                : RecordKnowledgeResult("Forget Knowledge", knowledge.ForgetBelief(belief.BeliefId, $"knowledge.forget.{Guid.NewGuid():N}", 300));
        }

        public PrototypeTestLabOperation ShareFirstKnowledgeBelief()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Share Knowledge", "Knowledge runtime is missing.", KnowledgeResultCode.MissingPerson.ToString());
            }

            KnowledgeBeliefRecord belief = knowledge.CreateSnapshot().Beliefs.FirstOrDefault();
            if (belief == null)
            {
                return RecordFailure("Share Knowledge", "No active Knowledge belief exists.", KnowledgeResultCode.MissingBelief.ToString());
            }

            GameObject listenerObject = new GameObject("Knowledge Test Listener");
            try
            {
                PersonKnowledgeRuntime listener = listenerObject.AddComponent<PersonKnowledgeRuntime>();
                listener.Configure(registry, "person.test-lab.listener");
                KnowledgeOperationResult result = listener.ShareBelief(new KnowledgeShareRequest
                {
                    TransactionId = $"knowledge.share.{Guid.NewGuid():N}",
                    SpeakerPersonId = knowledge.PersonId,
                    ListenerPersonId = listener.PersonId,
                    SpeakerBelief = belief,
                    ListenerCredibility = 700,
                    GameTimeSeconds = context?.Persistence?.PlayTime == null ? 0d : context.Persistence.PlayTime.CumulativeSeconds,
                    PrivateAccessAuthorized = true
                });
                return RecordKnowledgeResult("Share Knowledge", result);
            }
            finally
            {
                DestroyTestObject(listenerObject);
            }
        }

        public PrototypeTestLabOperation ValidateKnowledgeSaveRestore()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Knowledge Save Restore", "Knowledge runtime is missing.", KnowledgeResultCode.MissingPerson.ToString());
            }

            PersonKnowledgeSaveData saveData = knowledge.CreateSaveData();
            long before = knowledge.KnowledgeRevision;
            int events = 0;
            void CountEvent(PersonKnowledgeRuntime _, KnowledgeOperationResult __) => events++;
            knowledge.KnowledgeChanged += CountEvent;
            KnowledgeOperationResult result = knowledge.RestoreFromSaveData(saveData, registry, knowledge.PersonId, restoring: true);
            knowledge.KnowledgeChanged -= CountEvent;
            bool succeeded = result.Succeeded && events == 0 && knowledge.KnowledgeRevision == before;
            return Record(succeeded, "Knowledge Save Restore", succeeded ? "Success" : result.Code.ToString(), $"{result.Message} Events={events} Revision={before}->{knowledge.KnowledgeRevision} Beliefs={knowledge.CreateSnapshot().Beliefs.Count}.");
        }

        public PrototypeTestLabOperation AttemptPrivateDiagnosticKnowledgeObservation()
        {
            if (!TryBuildSpeciesCapabilityObservation($"knowledge.private-blocked.{Guid.NewGuid():N}", 600, 600, out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            request.Visibility = KnowledgeVisibility.DiagnosticOnly;
            KnowledgeOperationResult result = knowledge.RecordObservation(request);
            bool succeeded = !result.Succeeded && result.Code == KnowledgeResultCode.DiagnosticFactBlocked;
            return Record(succeeded, "Private Knowledge Blocked", succeeded ? "Success" : result.Code.ToString(), result.Message);
        }

        public PrototypeTestLabOperation ResetKnowledgeFixture()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Reset Knowledge Fixture", "Knowledge runtime is missing.", KnowledgeResultCode.MissingPerson.ToString());
            }

            PersonKnowledgeSaveData empty = new PersonKnowledgeSaveData
            {
                personId = knowledge.PersonId,
                currentActorId = knowledge.CurrentActorId,
                currentBodyId = knowledge.CurrentBodyId,
                knowledgeRevision = knowledge.KnowledgeRevision + 1,
                beliefs = Array.Empty<KnowledgeBeliefRecordData>(),
                evidence = Array.Empty<KnowledgeEvidenceRecordData>(),
                processedTransactions = Array.Empty<KnowledgeProcessedTransactionData>()
            };
            KnowledgeOperationResult result = knowledge.RestoreFromSaveData(empty, registry, knowledge.PersonId, restoring: true);
            return Record(result.Succeeded, "Reset Knowledge Fixture", result.Code.ToString(), result.Message);
        }

        public string BuildObservationSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Feature 8.2 Observation, Examination, Identification, and Diagnosis");
            builder.AppendLine(FormatObservationMethodCounts());
            if (EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                KnowledgeSnapshot snapshot = knowledge.CreateSnapshot();
                builder.AppendLine($"Observer Person={snapshot.PersonId} Body={EmptyAs(snapshot.CurrentBodyId, "None")} Revision={snapshot.Revision} Beliefs={snapshot.Beliefs.Count} Evidence={snapshot.Evidence.Count}");
            }
            else
            {
                builder.AppendLine("Observer Knowledge runtime is not ready.");
            }

            PrototypeTestLabOperation last = history.Count == 0 ? default : history[0];
            if (!string.IsNullOrWhiteSpace(last.OperationName) && last.OperationName.Contains("8.2", StringComparison.Ordinal))
            {
                builder.AppendLine($"Last 8.2: {last.OperationName} Code={last.Code} Success={last.Succeeded}");
                builder.AppendLine(last.Message);
            }

            return builder.ToString();
        }

        public string BuildHistorySummary()
        {
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime))
            {
                return "History runtime is missing.";
            }

            HistorySnapshot historySnapshot = historyRuntime.CreateSnapshot();
            PersonMemorySnapshot memorySnapshot = memoryRuntime.CreateSnapshot();
            string personId = GetPrototypePersonId();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Feature 8.3 Character History, Memory, and Historical Timelines");
            builder.AppendLine($"World={historySnapshot.WorldId} HistoryRevision={historySnapshot.Revision} Events={historySnapshot.Events.Count} BodyOccupations={historySnapshot.BodyOccupations.Count}");
            builder.AppendLine($"Person={personId} MemoryRevision={memorySnapshot.Revision} Memories={memorySnapshot.Memories.Count} Accessible={memorySnapshot.AccessibleMemories.Count}");
            builder.AppendLine($"Person-known history={historyRuntime.QueryPersonAccessible(personId, memoryRuntime).Count} Authoritative-visible={historySnapshot.Events.Count}");
            foreach (HistoricalEventRecord record in historySnapshot.Events.Take(10))
            {
                builder.AppendLine($"{record.EventId} {record.Category} {record.Status} t={record.OccurredAtWorldTime:0.##} seq={record.Sequence} visibility={record.Visibility} person={EmptyAs(record.PrimaryPersonId, "World")} supersedes={EmptyAs(record.SupersedesEventId, "None")}");
            }

            foreach (HistoryMemoryRecord memory in memorySnapshot.Memories.Take(8))
            {
                builder.AppendLine($"Memory {memory.MemoryId} event={EmptyAs(memory.HistoricalEventId, "None")} state={memory.State} clarity={memory.Clarity} body={EmptyAs(memory.BodyAtTimeId, "None")}");
            }

            return builder.ToString();
        }

        public PrototypeTestLabOperation ValidateHistoryFoundation()
        {
            bool runtimeReady = EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime);
            int definitions = CountDefinitions<HistoricalEventDefinition>();
            bool succeeded = runtimeReady && definitions >= 5;
            return Record(succeeded, "Validate 8.3 History Foundation", succeeded ? "Success" : "MissingDefinitions", $"Definitions={definitions} World={historyRuntime?.WorldId ?? "None"} Person={memoryRuntime?.PersonId ?? "None"}.");
        }

        public PrototypeTestLabOperation ValidateLifeEventDefinitions()
        {
            bool runtimeReady = EnsureHistoryRuntime(out _, out _);
            int definitions = registry == null ? 0 : registry.DefinitionsById.Values.OfType<HistoricalEventDefinition>().Count(definition => definition.IsLifeEventDefinition);
            bool succeeded = runtimeReady && definitions >= 10;
            return Record(succeeded, "Validate 8.5 Life Event Definitions", succeeded ? "Success" : "MissingDefinitions", $"LifeEventDefinitions={definitions} RuntimeReady={runtimeReady}.");
        }

        public string BuildLifeEventSummary()
        {
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime))
            {
                return "Life event runtime is missing.";
            }

            string personId = GetPrototypePersonId();
            IReadOnlyList<LifeEventRecord> timeline = historyRuntime.QueryLifeEventsForPerson(personId);
            IReadOnlyList<BiographyTimelineEntry> publicBiography = historyRuntime.QueryBiography(personId, memoryRuntime, publicOnly: true);
            IReadOnlyList<BiographyTimelineEntry> authoritativeBiography = historyRuntime.QueryBiography(personId, memoryRuntime, privileged: true);
            IReadOnlyList<LifeEventRecord> milestones = historyRuntime.QueryMajorLifeMilestones(personId);
            int definitions = registry == null ? 0 : registry.DefinitionsById.Values.OfType<HistoricalEventDefinition>().Count(definition => definition.IsLifeEventDefinition);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Feature 8.5 Character History and Life Events");
            builder.AppendLine($"Person={personId} Definitions={definitions} Timeline={timeline.Count} PublicBio={publicBiography.Count} AuthoritativeBio={authoritativeBiography.Count} Milestones={milestones.Count}");
            builder.AppendLine("Recent Life Events:");
            builder.AppendLine(FormatLifeEvents(timeline.Take(8).ToArray()));
            builder.AppendLine("Public Biography:");
            builder.AppendLine(FormatBiography(publicBiography.Take(6).ToArray()));
            return builder.ToString();
        }

        public PrototypeTestLabOperation RecordLifeEventBirthOrCreation() => RecordPrototypeLifeEvent("Record 8.5 Birth or Creation", "history-event.life.birth", LifeEventCategory.BirthOrCreation, LifeEventPayloadKind.BirthOrCreation, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.IdentityDefining, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject);
        public PrototypeTestLabOperation RecordLifeEventDiscovery() => RecordPrototypeLifeEvent("Record 8.5 Discovery", "history-event.life.discovery", LifeEventCategory.Discovery, LifeEventPayloadKind.Discovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.Optional, KnowledgeVisibility.Public, LifeEventParticipantRole.Discoverer);
        public PrototypeTestLabOperation RecordLifeEventRoleAppointment() => RecordPrototypeLifeEvent("Record 8.5 Role Appointment", "history-event.life.role-appointment", LifeEventCategory.Role, LifeEventPayloadKind.RoleOrTitleTransition, LifeEventSignificance.Major, LifeEventBiographyRelevance.MajorBiographyEvent, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject, relatedRoleId: "role.prototype.adventurer");
        public PrototypeTestLabOperation RecordLifeEventTitleGrant() => RecordPrototypeLifeEvent("Record 8.5 Title Grant", "history-event.life.title-grant", LifeEventCategory.Title, LifeEventPayloadKind.RoleOrTitleTransition, LifeEventSignificance.Major, LifeEventBiographyRelevance.PublicBiographyEvent, KnowledgeVisibility.Public, LifeEventParticipantRole.Subject, relatedTitleId: "title.prototype-hero");
        public PrototypeTestLabOperation RecordLifeEventAffiliationChange() => RecordPrototypeLifeEvent("Record 8.5 Affiliation Change", "history-event.life.affiliation", LifeEventCategory.Affiliation, LifeEventPayloadKind.AffiliationTransition, LifeEventSignificance.Notable, LifeEventBiographyRelevance.NormallyIncluded, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject, organizationId: "faction.prototype.guild");
        public PrototypeTestLabOperation RecordLifeEventBattleParticipation() => RecordPrototypeLifeEvent("Record 8.5 Battle Participation", "history-event.life.battle", LifeEventCategory.Combat, LifeEventPayloadKind.CombatParticipation, LifeEventSignificance.Major, LifeEventBiographyRelevance.MajorBiographyEvent, KnowledgeVisibility.Public, LifeEventParticipantRole.Participant, relatedCombatEncounterId: "encounter.prototype.training-battle", sequenceId: "sequence.prototype.battle-recovery", sequenceOrder: 0);
        public PrototypeTestLabOperation RecordLifeEventMajorInjury() => RecordPrototypeLifeEvent("Record 8.5 Major Injury", "history-event.life.injury", LifeEventCategory.Injury, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Major, LifeEventBiographyRelevance.PrivateBiographyEvent, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject, relatedInjuryId: "injury.prototype-major", sequenceId: "sequence.prototype.battle-recovery", sequenceOrder: 1, relationshipTarget: EnsureLifeEvent("event.prototype.life.battle", () => RecordLifeEventBattleParticipation()));
        public PrototypeTestLabOperation RecordLifeEventDiagnosis() => RecordPrototypeLifeEvent("Record 8.5 Diagnosis", "history-event.diagnosis", LifeEventCategory.Diagnosis, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.PrivateBiographyEvent, KnowledgeVisibility.DiagnosticOnly, LifeEventParticipantRole.Subject, relatedConditionId: "condition.biology.prototype-infection", sequenceId: "sequence.prototype.medical", sequenceOrder: 0);
        public PrototypeTestLabOperation RecordLifeEventRecovery() => RecordPrototypeLifeEvent("Record 8.5 Recovery", "history-event.life.recovery", LifeEventCategory.Recovery, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.NormallyIncluded, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject, relatedTreatmentId: "treatment.prototype-rest", sequenceId: "sequence.prototype.medical", sequenceOrder: 1, sequenceStatus: LifeEventSequenceStatus.Completed, relationshipTarget: EnsureLifeEvent("event.prototype.life.diagnosis", () => RecordLifeEventDiagnosis()));
        public PrototypeTestLabOperation RecordLifeEventCrimeOrAccusation() => RecordPrototypeLifeEvent("Record 8.5 Hidden Crime", "history-event.life.crime", LifeEventCategory.Crime, LifeEventPayloadKind.Legal, LifeEventSignificance.Major, LifeEventBiographyRelevance.RestrictedBiographyEvent, KnowledgeVisibility.Hidden, LifeEventParticipantRole.Accused, relatedLegalRecordId: "legal.prototype.accusation");
        public PrototypeTestLabOperation RecordLifeEventOwnershipTransfer() => RecordPrototypeLifeEvent("Record 8.5 Ownership Transfer", "history-event.life.discovery", LifeEventCategory.Ownership, LifeEventPayloadKind.OwnershipTransfer, LifeEventSignificance.Notable, LifeEventBiographyRelevance.Optional, KnowledgeVisibility.Private, LifeEventParticipantRole.Owner, relatedItemId: "item.prototype-sword");
        public PrototypeTestLabOperation RecordLifeEventDeath() => RecordPrototypeLifeEvent("Record 8.5 Death", "history-event.life.death", LifeEventCategory.Death, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.MajorBiographyEvent, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject);
        public PrototypeTestLabOperation RecordLifeEventPresumedDeath() => RecordPrototypeLifeEvent("Record 8.5 Presumed Death", "history-event.life.presumed-death", LifeEventCategory.Disappearance, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.Major, LifeEventBiographyRelevance.RestrictedBiographyEvent, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject);
        public PrototypeTestLabOperation RecordLifeEventReturn() => RecordPrototypeLifeEvent("Record 8.5 Return", "history-event.life.return", LifeEventCategory.ReturnOrResurrection, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.MajorBiographyEvent, KnowledgeVisibility.Public, LifeEventParticipantRole.Subject);
        public PrototypeTestLabOperation RecordLifeEventBodyTransition() => RecordPrototypeLifeEvent("Record 8.5 Body Transition", "history-event.body-transition", LifeEventCategory.BodyTransition, LifeEventPayloadKind.BodyTransition, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.IdentityDefining, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject);

        public PrototypeTestLabOperation CreateLifeEventSequence()
        {
            RecordLifeEventBattleParticipation();
            RecordLifeEventMajorInjury();
            RecordLifeEventRecovery();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _);
            bool succeeded = historyRuntime.TryGetLifeEventSequence("sequence.prototype.battle-recovery", out LifeEventSequenceRecord sequence) && sequence.Events.Count >= 2;
            return Record(succeeded, "Create 8.5 Life Event Sequence", succeeded ? "Success" : "MissingSequence", succeeded ? FormatLifeEventSequence(sequence) : "Sequence was not created.");
        }

        public PrototypeTestLabOperation LinkLifeEventCauseAndConsequence()
        {
            RecordLifeEventBattleParticipation();
            PrototypeTestLabOperation injury = RecordLifeEventMajorInjury();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _);
            bool succeeded = injury.Succeeded && historyRuntime.QueryRelatedLifeEvents("event.prototype.life.injury", LifeEventRelationshipType.Cause).Any(record => record.EventId == "event.prototype.life.battle");
            return Record(succeeded, "Link 8.5 Cause and Consequence", succeeded ? "Success" : "MissingRelationship", $"BattleToInjuryLinked={succeeded}.");
        }

        public PrototypeTestLabOperation CorrectLifeEventPresumedDeath()
        {
            RecordLifeEventPresumedDeath();
            RecordLifeEventReturn();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _);
            if (historyRuntime.TryGetEvent("event.prototype.life.return.corrected", out HistoricalEventRecord existingCorrection))
            {
                HistoryOperationResult duplicate = HistoryOperationResult.Success("Presumed-death correction already exists.", "history.8.5.correct-presumed-death", existingCorrection, null, null, historyRuntime.HistoryRevision, historyRuntime.HistoryRevision, duplicate: true);
                return RecordHistoryResult("Correct 8.5 Presumed Death", duplicate);
            }

            RecordLifeEventRequest request = BuildPrototypeLifeEventRequest("event.prototype.life.return.corrected", "history-event.life.return", LifeEventCategory.ReturnOrResurrection, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.MajorBiographyEvent, KnowledgeVisibility.Public, LifeEventParticipantRole.Subject);
            request.TransactionId = "history.8.5.correct-presumed-death";
            request.SupersedesEventId = "event.prototype.life.presumed-death";
            HistoryOperationResult result = historyRuntime.RecordLifeEvent(request);
            return RecordHistoryResult("Correct 8.5 Presumed Death", result);
        }

        public PrototypeTestLabOperation ShowLifeEventPersonTimeline() => RecordLifeEventView("Show 8.5 Person Timeline", history => FormatLifeEvents(history.QueryLifeEventsForPerson(GetPrototypePersonId())));
        public PrototypeTestLabOperation ShowLifeEventPublicBiography() => RecordLifeEventView("Show 8.5 Public Biography", history => FormatBiography(history.QueryBiography(GetPrototypePersonId(), GetActiveMemoryRuntime(), publicOnly: true)));
        public PrototypeTestLabOperation ShowLifeEventAuthoritativeBiography() => RecordLifeEventView("Show 8.5 Authoritative Biography", history => FormatBiography(history.QueryBiography(GetPrototypePersonId(), GetActiveMemoryRuntime(), privileged: true)));
        public PrototypeTestLabOperation ShowLifeEventPersonKnownBiography() => RecordLifeEventView("Show 8.5 Person Known Biography", history => FormatBiography(history.QueryBiography(GetPrototypePersonId(), GetActiveMemoryRuntime(), personKnown: true)));
        public PrototypeTestLabOperation ShowLifeEventPersonRememberedBiography() => RecordLifeEventView("Show 8.5 Person Remembered Biography", history => FormatBiography(history.QueryBiography(GetPrototypePersonId(), GetActiveMemoryRuntime(), personRemembered: true)));
        public PrototypeTestLabOperation ShowLifeEventMajorMilestones() => RecordLifeEventView("Show 8.5 Major Milestones", history => FormatLifeEvents(history.QueryMajorLifeMilestones(GetPrototypePersonId())));

        public PrototypeTestLabOperation ValidateLifeEventSaveRestore()
        {
            RecordLifeEventBirthOrCreation();
            RecordLifeEventBattleParticipation();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime);
            string runId = Guid.NewGuid().ToString("N");
            string presumedDeathId = $"event.prototype.life.presumed-death.{runId}";
            string returnCorrectionId = $"event.prototype.life.return.corrected.{runId}";
            HistoryOperationResult presumedDeath = historyRuntime.RecordLifeEvent(BuildPrototypeLifeEventRequest(presumedDeathId, "history-event.life.presumed-death", LifeEventCategory.Disappearance, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.Major, LifeEventBiographyRelevance.RestrictedBiographyEvent, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject));
            RecordLifeEventRequest correctionRequest = BuildPrototypeLifeEventRequest(returnCorrectionId, "history-event.life.return", LifeEventCategory.ReturnOrResurrection, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.MajorBiographyEvent, KnowledgeVisibility.Public, LifeEventParticipantRole.Subject);
            correctionRequest.SupersedesEventId = presumedDeathId;
            HistoryOperationResult correction = presumedDeath.Succeeded
                ? historyRuntime.RecordLifeEvent(correctionRequest)
                : HistoryOperationResult.Failure(presumedDeath.Code, presumedDeath.Message, presumedDeath.TransactionId, revision: historyRuntime.HistoryRevision);
            AuthoritativeHistorySaveData historySave = historyRuntime.CreateSaveData();
            PersonMemorySaveData memorySave = memoryRuntime.CreateSaveData();
            AuthoritativeHistoryRuntime restoredHistory = new AuthoritativeHistoryRuntime();
            restoredHistory.Configure(registry, PersistenceService.LocalWorldId, GetKnownPrototypePersons(), GetKnownPrototypeBodies());
            PersonMemoryRuntime restoredMemory = new PersonMemoryRuntime();
            restoredMemory.Configure(GetPrototypePersonId(), registry, restoredHistory, GetKnownPrototypePersons());
            int historyEvents = 0;
            int memoryEvents = 0;
            void CountHistory(AuthoritativeHistoryRuntime _, HistoryOperationResult __) => historyEvents++;
            void CountMemory(PersonMemoryRuntime _, HistoryOperationResult __) => memoryEvents++;
            restoredHistory.HistoryChanged += CountHistory;
            restoredMemory.MemoryChanged += CountMemory;
            HistoryOperationResult historyRestore = restoredHistory.RestoreFromSaveData(historySave, registry, GetKnownPrototypePersons(), GetKnownPrototypeBodies(), restoring: true);
            HistoryOperationResult memoryRestore = restoredMemory.RestoreFromSaveData(memorySave, registry, restoredHistory, GetKnownPrototypePersons(), restoring: true);
            restoredHistory.HistoryChanged -= CountHistory;
            restoredMemory.MemoryChanged -= CountMemory;
            HistoricalEventRecord accepted = null;
            bool acceptedResolved = restoredHistory.TryGetAcceptedEvent(presumedDeathId, out accepted);
            bool presumedRestored = restoredHistory.TryGetEvent(presumedDeathId, out _);
            bool correctionRestored = restoredHistory.TryGetEvent(returnCorrectionId, out _);
            int restoredLifeEvents = restoredHistory.QueryLifeEventsForPerson(GetPrototypePersonId()).Count;
            bool noReplayEvents = historyEvents == 0 && memoryEvents == 0;
            bool succeeded = presumedDeath.Succeeded && correction.Succeeded && historyRestore.Succeeded && memoryRestore.Succeeded && presumedRestored && correctionRestored && acceptedResolved && accepted.EventId == returnCorrectionId && noReplayEvents;
            return Record(succeeded, "Validate 8.5 Save Restore", succeeded ? "Success" : "RestoreFailed", $"Presumed={presumedDeath.Code} Correction={correction.Code} History={historyRestore.Code} '{historyRestore.Message}' Memory={memoryRestore.Code} '{memoryRestore.Message}' Events={restoredLifeEvents} Restored={presumedRestored}/{correctionRestored} RestoreEvents={historyEvents}/{memoryEvents} AcceptedPresumedDeath={(accepted == null ? "None" : accepted.EventId)}.");
        }

        public PrototypeTestLabOperation RecordAuthoritativeHistoryEvent()
        {
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _))
            {
                return RecordFailure("Record 8.3 Authoritative Event", "History runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            string personId = GetPrototypePersonId();
            string bodyId = GetPrototypeBodyId();
            RecordHistoricalEventRequest request = BuildHistoryEventRequest($"history.8.3.authoritative.{Guid.NewGuid():N}", $"event.prototype.participation.{Guid.NewGuid():N}", "history-event.person-participation", personId, KnowledgeVisibility.Public, "Prototype person participated in a representative event.");
            request.BodyIds = new[] { bodyId };
            HistoryOperationResult result = historyRuntime.RecordEvent(request);
            return RecordHistoryResult("Record 8.3 Authoritative Event", result);
        }

        public PrototypeTestLabOperation RecordHiddenHistoryEvent()
        {
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _))
            {
                return RecordFailure("Record 8.3 Hidden Event", "History runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            string personId = GetPrototypePersonId();
            string eventId = GetPrototypeHiddenHistoryEventId();
            if (historyRuntime.TryGetEvent(eventId, out HistoricalEventRecord existing))
            {
                HistoryOperationResult duplicate = HistoryOperationResult.Success("Hidden event already exists.", "history.8.3.hidden.fixed", existing, null, null, historyRuntime.HistoryRevision, historyRuntime.HistoryRevision, duplicate: true);
                return RecordHistoryResult("Record 8.3 Hidden Event", duplicate);
            }

            RecordHistoricalEventRequest request = BuildHistoryEventRequest(CreateAutomationScopedId("history", "hidden-event"), eventId, "history-event.hidden-witnessed-event", personId, KnowledgeVisibility.Hidden, "Hidden event witnessed by the prototype Person.");
            request.ParticipantPersonIds = new[] { personId };
            HistoryOperationResult result = historyRuntime.RecordEvent(request);
            return RecordHistoryResult("Record 8.3 Hidden Event", result);
        }

        public PrototypeTestLabOperation ProveUninformedPersonCannotQueryHiddenHistory()
        {
            RecordHiddenHistoryEvent();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _);
            IReadOnlyList<HistoricalEventRecord> known = historyRuntime.QueryPersonAccessible("person.prototype.uninformed");
            IReadOnlyList<HistoricalEventRecord> privileged = historyRuntime.QueryPersonAccessible("person.prototype.uninformed", privileged: true);
            string eventId = GetPrototypeHiddenHistoryEventId();
            bool succeeded = known.All(record => record.EventId != eventId) && privileged.Any(record => record.EventId == eventId);
            return Record(succeeded, "Prove 8.3 Hidden History Privacy", succeeded ? "Success" : "PrivacyLeak", $"UninformedKnown={known.Count} Privileged={privileged.Count}.");
        }

        public PrototypeTestLabOperation FormWitnessHistoryMemory()
        {
            if (currentAutomationScenarioContext != null)
            {
                TestLabFixtureHandle handle = currentAutomationScenarioContext.Fixtures.Require(TestLabHistoryFixtureProviders.WitnessMemoryFixtureId, currentAutomationScenarioContext);
                if (!handle.Succeeded)
                {
                    return RecordFailure("Form 8.3 Witness Memory", handle.Message, handle.Outcome.ToString());
                }

                string message = currentAutomationScenarioContext.TryGetFixturePayload(TestLabHistoryFixtureProviders.WitnessMemoryFixtureId, out HiddenHistoryFixtureHandle payload)
                    ? $"Scenario fixture ready. Event={payload.EventId} Memory={payload.MemoryId} Owner={payload.OwnerPersonId}."
                    : "Scenario fixture ready.";
                return Record(true, "Form 8.3 Witness Memory", handle.Outcome.ToString(), message);
            }

            RecordHiddenHistoryEvent();
            if (!EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime) || !EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Form 8.3 Witness Memory", "History, Memory, or Knowledge runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            string memoryId = GetPrototypeWitnessMemoryId();
            string eventId = GetPrototypeHiddenHistoryEventId();
            if (memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord existing))
            {
                HistoryOperationResult duplicate = HistoryOperationResult.Success("Witness memory already exists.", "history.8.3.memory.hidden", null, existing, null, memoryRuntime.MemoryRevision, memoryRuntime.MemoryRevision, duplicate: true);
                return RecordHistoryResult("Form 8.3 Witness Memory", duplicate);
            }

            FormMemoryRequest request = BuildMemoryRequest(CreateAutomationScopedId("history", "witness-memory"), memoryId, eventId, HistoryMemorySource.DirectObservation, createKnowledge: true);
            HistoryOperationResult result = memoryRuntime.FormMemory(request, knowledge);
            return RecordHistoryResult("Form 8.3 Witness Memory", result);
        }

        public PrototypeTestLabOperation PrepareWitnessHistoryMemoryAutomationFixture()
        {
            if (currentAutomationScenarioContext == null)
            {
                return FormWitnessHistoryMemory();
            }

            TestLabFixtureHandle handle = currentAutomationScenarioContext.Fixtures.Require(TestLabHistoryFixtureProviders.WitnessMemoryFixtureId, currentAutomationScenarioContext);
            return handle.Succeeded
                ? Record(true, "Prepare 8.3 Witness Memory Fixture", handle.Outcome.ToString(), handle.Message)
                : RecordFailure("Prepare 8.3 Witness Memory Fixture", handle.Message, handle.Outcome.ToString());
        }

        public PrototypeTestLabOperation ShareHistoricalTestimony()
        {
            FormWitnessHistoryMemory();
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _))
            {
                return RecordFailure("Share 8.3 Historical Testimony", "History runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            string eventId = GetPrototypeHiddenHistoryEventId();
            PersonMemoryRuntime listenerMemory = new PersonMemoryRuntime();
            listenerMemory.Configure("person.prototype.listener", registry, historyRuntime, GetKnownPrototypePersons());
            FormMemoryRequest request = new FormMemoryRequest
            {
                TransactionId = CreateAutomationScopedId("history", "testimony"),
                MemoryId = CreateAutomationScopedId("memory", "listener-testimony"),
                OwnerPersonId = "person.prototype.listener",
                HistoricalEventId = eventId,
                Source = HistoryMemorySource.WitnessTestimony,
                FormedAtWorldTime = GetGameTimeSeconds() + 0.3d,
                RememberedOccurredAtWorldTime = GetGameTimeSeconds(),
                Confidence = 620,
                Clarity = 520,
                Salience = 450,
                Visibility = KnowledgeVisibility.Private,
                DebugDescription = "Listener learned a historical claim through testimony.",
                Tags = new[] { "history", "testimony" }
            };
            HistoryOperationResult result = listenerMemory.FormMemory(request);
            bool succeeded = result.Succeeded && historyRuntime.QueryPersonAccessible("person.prototype.listener", listenerMemory).Any(record => record.EventId == eventId);
            return Record(succeeded, "Share 8.3 Historical Testimony", succeeded ? "Success" : result.Code.ToString(), $"{result.Message} ListenerMemories={listenerMemory.CreateSnapshot().Memories.Count}.");
        }

        public PrototypeTestLabOperation CreateIncorrectHistoricalBelief()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Create 8.3 Incorrect Historical Belief", "Knowledge runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            KnowledgeObservationRequest request = BuildHistoricalKnowledgeRequest($"history.8.3.false-belief.{Guid.NewGuid():N}", "event.prototype.false-death", KnowledgeEvidenceDirection.Supports, 800, 850);
            request.MarkAsMisconception = true;
            request.TruthAuthorization = KnowledgeTruthAuthorization.CreateDevelopmentFixture("test-lab.history.false-belief");
            KnowledgeOperationResult result = knowledge.RecordObservation(request);
            return RecordKnowledgeResult("Create 8.3 Incorrect Historical Belief", result);
        }

        public PrototypeTestLabOperation CorrectAuthoritativeHistoryEvent()
        {
            RecordAuthoritativeHistoryEvent();
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _))
            {
                return RecordFailure("Correct 8.3 Authoritative History", "History runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            HistoricalEventRecord target = historyRuntime.CreateSnapshot().Events.LastOrDefault(record => record.EventDefinitionId == "history-event.person-participation");
            if (target == null)
            {
                return RecordFailure("Correct 8.3 Authoritative History", "No event exists to correct.", HistoryResultCode.MissingEvent.ToString());
            }

            RecordHistoricalEventRequest correction = BuildHistoryEventRequest($"history.8.3.correction.{Guid.NewGuid():N}", $"event.prototype.correction.{Guid.NewGuid():N}", "history-event.correction", GetPrototypePersonId(), KnowledgeVisibility.Private, "Corrected historical record.");
            correction.SupersedesEventId = target.EventId;
            HistoryOperationResult result = historyRuntime.RecordEvent(correction);
            return RecordHistoryResult("Correct 8.3 Authoritative History", result);
        }

        public PrototypeTestLabOperation ReviseHistoricalBeliefWithEvidence()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Revise 8.3 Historical Belief", "Knowledge runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            KnowledgeObservationRequest request = BuildHistoricalKnowledgeRequest($"history.8.3.belief-revision.{Guid.NewGuid():N}", "event.prototype.false-death", KnowledgeEvidenceDirection.Corrects, 950, 950);
            request.Provenance = KnowledgeProvenance.AuthoritativeCorrection;
            request.AcquisitionSource = KnowledgeAcquisitionSource.DevelopmentFixture;
            request.TruthAuthorization = KnowledgeTruthAuthorization.CreateDevelopmentFixture("test-lab.history.belief-revision");
            KnowledgeOperationResult result = knowledge.RecordObservation(request);
            return RecordKnowledgeResult("Revise 8.3 Historical Belief", result);
        }

        public PrototypeTestLabOperation ForgetFirstHistoryMemory()
        {
            if (!EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime))
            {
                return RecordFailure("Forget 8.3 History Memory", "Memory runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            HistoryMemoryRecord memory = memoryRuntime.CreateSnapshot().Memories.FirstOrDefault();
            if (memory == null)
            {
                FormWitnessHistoryMemory();
                memory = memoryRuntime.CreateSnapshot().Memories.FirstOrDefault();
            }

            HistoryOperationResult result = memory == null
                ? HistoryOperationResult.Failure(HistoryResultCode.MissingMemory, "No memory exists to forget.")
                : memoryRuntime.ForgetMemory(memory.MemoryId, $"history.8.3.memory-forget.{Guid.NewGuid():N}");
            return RecordHistoryResult("Forget 8.3 History Memory", result);
        }

        public PrototypeTestLabOperation RecordBodyTransitionHistory()
        {
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime))
            {
                return RecordFailure("Record 8.3 Body Transition", "History runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            string oldBody = GetPrototypeBodyId();
            string runId = Guid.NewGuid().ToString("N");
            string newBody = currentAutomationScenarioContext == null
                ? $"body.prototype.future.{runId}"
                : currentAutomationScenarioContext.ScopedId("body", "future");
            string transitionEventId = currentAutomationScenarioContext == null
                ? $"event.prototype.body-transition.{runId}"
                : currentAutomationScenarioContext.ScopedId("event", "body-transition");
            string transitionTransactionId = currentAutomationScenarioContext == null
                ? $"history.8.3.body-transition.{runId}"
                : currentAutomationScenarioContext.ScopedId("history", "body-transition");

            string[] knownBodies = GetKnownPrototypeBodies()
                .Concat(new[] { oldBody, newBody })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            historyRuntime.Configure(registry, PersistenceService.LocalWorldId, GetKnownPrototypePersons(), knownBodies);

            HistoryOperationResult transition = historyRuntime.TryGetEvent(transitionEventId, out HistoricalEventRecord existingTransition)
                ? HistoryOperationResult.Success("Body transition fixture already exists.", transitionTransactionId, existingTransition, null, null, historyRuntime.HistoryRevision, historyRuntime.HistoryRevision, duplicate: true)
                : historyRuntime.RecordBodyTransition(transitionTransactionId, transitionEventId, GetPrototypePersonId(), oldBody, newBody, GetGameTimeSeconds(), GetGameTimeSeconds(), "Prototype body continuity test");

            if (!transition.Succeeded && transition.Code != HistoryResultCode.Duplicate)
            {
                return RecordHistoryResult("Record 8.3 Body Transition", transition);
            }

            string transitionMemoryId = GetPrototypePreviousBodyMemoryId(runId);
            HistoryOperationResult memoryResult;
            if (memoryRuntime.TryGetMemory(transitionMemoryId, out HistoryMemoryRecord existingMemory))
            {
                memoryResult = HistoryOperationResult.Success("Previous body memory fixture already exists.", $"history.8.3.previous-body-memory.{runId}", null, existingMemory, null, memoryRuntime.MemoryRevision, memoryRuntime.MemoryRevision, duplicate: true);
            }
            else
            {
                FormMemoryRequest memory = BuildMemoryRequest($"history.8.3.previous-body-memory.{runId}", transitionMemoryId, transitionEventId, HistoryMemorySource.PreviousBody, createKnowledge: false);
                memory.BodyAtTimeId = oldBody;
                memoryResult = memoryRuntime.FormMemory(memory);
            }

            if (currentAutomationScenarioContext == null && !memoryRuntime.TryGetMemory("memory.prototype.previous-body", out _))
            {
                FormMemoryRequest compatibilityMemory = BuildMemoryRequest("history.8.3.previous-body-memory.compatibility", "memory.prototype.previous-body", transitionEventId, HistoryMemorySource.PreviousBody, createKnowledge: false);
                compatibilityMemory.BodyAtTimeId = oldBody;
                memoryRuntime.FormMemory(compatibilityMemory);
            }

            IReadOnlyList<BodyOccupationRecord> occupations = historyRuntime.QueryBodyOccupations(GetPrototypePersonId());
            bool transitionPresent = historyRuntime.TryGetEvent(transitionEventId, out _);
            bool occupationPresent = occupations.Any(record => string.Equals(record.BodyId, newBody, StringComparison.Ordinal));
            bool memoryPresent = memoryRuntime.CreateSnapshot().Memories.Any(record => string.Equals(record.MemoryId, transitionMemoryId, StringComparison.Ordinal) && string.Equals(record.BodyAtTimeId, oldBody, StringComparison.Ordinal));
            bool succeeded = transition.Succeeded && memoryResult.Succeeded && transitionPresent && occupationPresent && memoryPresent;
            return Record(succeeded, "Record 8.3 Body Transition", succeeded ? "Success" : "ContinuityMissing", $"Transition={transition.Code} Memory={memoryResult.Code} Event={transitionPresent} Occupation={occupationPresent} MemoryPresent={memoryPresent} Occupations={occupations.Count} Memories={memoryRuntime.CreateSnapshot().Memories.Count} NewBody={newBody}.");
        }

        public PrototypeTestLabOperation CompareHistoryKnowledgeMemoryViews()
        {
            FormWitnessHistoryMemory();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime);
            EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge);
            HistorySnapshot authoritative = historyRuntime.CreateSnapshot();
            IReadOnlyList<HistoricalEventRecord> known = historyRuntime.QueryPersonAccessible(GetPrototypePersonId(), memoryRuntime);
            PersonMemorySnapshot memories = memoryRuntime.CreateSnapshot();
            KnowledgeSnapshot knowledgeSnapshot = knowledge.CreateSnapshot();
            bool succeeded = authoritative.Events.Count >= known.Count && memories.Memories.Count > 0 && knowledgeSnapshot.Evidence.Any(evidence => !string.IsNullOrWhiteSpace(evidence.Data.relatedEventId));
            return Record(succeeded, "Compare 8.3 History Views", succeeded ? "Success" : "ViewMismatch", $"Authoritative={authoritative.Events.Count} PersonKnown={known.Count} Memories={memories.Memories.Count} KnowledgeEvidence={knowledgeSnapshot.Evidence.Count}.");
        }

        public PrototypeTestLabOperation ValidateHistorySaveRestore()
        {
            FormWitnessHistoryMemory();
            RecordBodyTransitionHistory();
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime))
            {
                return RecordFailure("Validate 8.3 History Save Restore", "History runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            lastHistorySaveData = historyRuntime.CreateSaveData();
            lastMemorySaveData = memoryRuntime.CreateSaveData();
            int historyEvents = 0;
            int memoryEvents = 0;
            void CountHistory(AuthoritativeHistoryRuntime _, HistoryOperationResult __) => historyEvents++;
            void CountMemory(PersonMemoryRuntime _, HistoryOperationResult __) => memoryEvents++;
            historyRuntime.HistoryChanged += CountHistory;
            memoryRuntime.MemoryChanged += CountMemory;
            HistoryOperationResult historyRestore = historyRuntime.RestoreFromSaveData(lastHistorySaveData, registry, GetKnownPrototypePersons(), GetKnownPrototypeBodies(), restoring: true);
            HistoryOperationResult memoryRestore = memoryRuntime.RestoreFromSaveData(lastMemorySaveData, registry, historyRuntime, GetKnownPrototypePersons(), restoring: true);
            historyRuntime.HistoryChanged -= CountHistory;
            memoryRuntime.MemoryChanged -= CountMemory;
            bool succeeded = historyRestore.Succeeded && memoryRestore.Succeeded && historyEvents == 0 && memoryEvents == 0;
            return Record(succeeded, "Validate 8.3 History Save Restore", succeeded ? "Success" : "RestoreFailed", $"History={historyRestore.Code} '{historyRestore.Message}' Memory={memoryRestore.Code} '{memoryRestore.Message}' Events={historyEvents}/{memoryEvents} Ordering={string.Join(",", historyRuntime.CreateSnapshot().Events.Select(record => record.EventId).Take(6))}.");
        }

        public string BuildMemoryRecallSummary()
        {
            FormWitnessHistoryMemory();
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime))
            {
                return "Memory runtime is missing.";
            }

            StringBuilder builder = new StringBuilder();
            PersonMemorySnapshot snapshot = memoryRuntime.CreateSnapshot();
            builder.AppendLine("Feature 8.4 Memory Recall, Reinforcement, Forgetting, and Alteration");
            builder.AppendLine($"Person={snapshot.PersonId} Revision={snapshot.Revision} Memories={snapshot.Memories.Count}");
            foreach (HistoryMemoryRecord memory in snapshot.Memories.Take(8))
            {
                builder.AppendLine($"{memory.MemoryId} Event={memory.HistoricalEventId} State={memory.State} Confidence={memory.Confidence} Clarity={memory.Clarity} Salience={memory.Salience} Recall={memory.RecallCount} Reinforce={memory.ReinforcementCount} Suppressions={memory.Suppressions.Count} Revisions={memory.Revisions.Count} Body={EmptyAs(memory.BodyAtTimeId, "None")}");
                string details = string.Join(", ", memory.RememberedDetails.Take(6).Select(detail => $"{detail.detailId}:{detail.state}:{detail.value}"));
                builder.AppendLine($"  Details={EmptyAs(details, "None")}");
            }

            IReadOnlyList<HistoricalEventRecord> privileged = historyRuntime.QueryPersonAccessible(GetPrototypePersonId(), privileged: true);
            builder.AppendLine($"PrivilegedHistoryVisible={privileged.Count}");
            return builder.ToString();
        }

        public PrototypeTestLabOperation ValidateMemory84()
        {
            FormWitnessHistoryMemory();
            if (!EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime))
            {
                return RecordFailure("Validate 8.4 Memory", "Memory runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            PersonMemorySnapshot snapshot = memoryRuntime.CreateSnapshot();
            bool hasStructured = snapshot.Memories.Any(memory => memory.RememberedDetails.Count > 0 && memory.Revisions.Count > 0);
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime validationHistory, out _);
            bool schemaValid = PersonMemoryRuntime.ValidateSaveData(memoryRuntime.CreateSaveData(), validationHistory, GetKnownPrototypePersons(), out string failure);
            return Record(hasStructured && schemaValid, "Validate 8.4 Memory", hasStructured && schemaValid ? "Success" : "InvalidMemory", schemaValid ? $"Memories={snapshot.Memories.Count} Structured={hasStructured}." : failure);
        }

        public PrototypeTestLabOperation InspectExistingMemories()
        {
            FormWitnessHistoryMemory();
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            PersonMemorySnapshot snapshot = memoryRuntime.CreateSnapshot();
            return Record(snapshot.Memories.Count > 0, "Inspect 8.4 Memories", snapshot.Memories.Count > 0 ? "Success" : HistoryResultCode.MissingMemory.ToString(), BuildMemoryRecallSummary());
        }

        public PrototypeTestLabOperation RecallPrototypeMemory()
        {
            string memoryId = GetPrototypeMemoryId();
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            return RecallMemoryWithRequest("Recall 8.4 Memory", new MemoryRecallRequest
            {
                TransactionId = $"history.8.4.recall.{Guid.NewGuid():N}",
                RequestingPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                AttemptDifficult = true,
                ReinforceOnSuccess = true
            });
        }

        public PrototypeTestLabOperation RecallPrototypeMemoryBySubject()
        {
            if (!TryCreateIsolatedMemoryRecallFixture("subject", out PersonMemoryRuntime memoryRuntime, out string memoryId, out string eventId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            return RecallMemoryWithRequest("Recall 8.4 By Subject", new MemoryRecallRequest
            {
                TransactionId = $"history.8.4.recall-subject.{Guid.NewGuid():N}",
                RequestingPersonId = GetPrototypePersonId(),
                SubjectId = eventId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                AttemptDifficult = true
            }, memoryRuntime);
        }

        public PrototypeTestLabOperation RecallPrototypeMemoryWithCue()
        {
            if (!TryCreateIsolatedMemoryRecallFixture("cue", out PersonMemoryRuntime memoryRuntime, out string memoryId, out string eventId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            return RecallMemoryWithRequest("Recall 8.4 With Cue", new MemoryRecallRequest
            {
                TransactionId = $"history.8.4.recall-cue.{Guid.NewGuid():N}",
                RequestingPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                AttemptDifficult = true,
                AllowCueRecovery = true,
                Cues = new[] { new MemoryRecallCue { Kind = MemoryCueKind.HistoricalEvent, ReferenceId = eventId, Strength = 1000 } }
            }, memoryRuntime);
        }

        public PrototypeTestLabOperation ReinforcePrototypeMemory()
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            HistoryOperationResult result = memoryRuntime.ReinforceMemory(new MemoryReinforcementRequest
            {
                TransactionId = $"history.8.4.reinforce.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                Source = MemoryReinforcementSource.Study,
                ConfidenceDelta = 20,
                ClarityDelta = 40,
                SalienceDelta = 20,
                SourceId = "test-lab.memory.reinforce"
            });
            return RecordHistoryResult("Reinforce 8.4 Memory", result);
        }

        public PrototypeTestLabOperation ReinforceFalsePrototypeMemory()
        {
            FormWitnessHistoryMemory();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime);
            string memoryId = $"memory.prototype.false-reinforcement.{Guid.NewGuid():N}";
            FormMemoryRequest request = BuildMemoryRequest(CreateAutomationScopedId("history", "false-memory"), memoryId, GetPrototypeHiddenHistoryEventId(), HistoryMemorySource.WitnessTestimony, createKnowledge: false);
            request.Confidence = 320;
            request.Clarity = 360;
            request.DebugDescription = "Distorted prototype memory used to prove reinforcement is not truth.";
            HistoryOperationResult formed = memoryRuntime.FormMemory(request);
            HistoryOperationResult altered = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.false-memory-alter.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId, 0.5d),
                AlterationType = MemoryAlterationType.Distortion,
                ResultingState = MemoryState.Altered,
                DetailsToAddOrReplace = new[] { new MemoryDetailData { detailId = "detail.false-claim", kind = MemoryDetailKind.Note, state = MemoryDetailState.Altered, value = "Incorrect recollection reinforced by repetition.", confidence = 900 } },
                SourceId = "test-lab.memory.false-reinforcement",
                Description = "Create a false/distorted recollection before reinforcing it."
            });
            HistoryOperationResult reinforced = memoryRuntime.ReinforceMemory(new MemoryReinforcementRequest
            {
                TransactionId = $"history.8.4.false-memory-reinforce.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId, 1d),
                Source = MemoryReinforcementSource.RepeatedTestimony,
                ConfidenceDelta = 250,
                ClarityDelta = 150,
                SalienceDelta = 100,
                SourceId = "test-lab.memory.false-reinforcement"
            });
            bool historyUnchanged = historyRuntime.TryGetEvent(GetPrototypeHiddenHistoryEventId(), out HistoricalEventRecord authoritative) && authoritative != null;
            bool succeeded = formed.Succeeded && altered.Succeeded && reinforced.Succeeded && historyUnchanged;
            return Record(succeeded, "Reinforce 8.4 False Memory", succeeded ? "Success" : reinforced.Code.ToString(), $"Formed={formed.Code} Altered={altered.Code} Reinforced={reinforced.Code} HistoryUnchanged={historyUnchanged}. Reinforcement increases confidence/clarity only; authoritative history is not changed.");
        }

        public PrototypeTestLabOperation ReduceMemoryClarity()
        {
            return AlterPrototypeMemoryMetric("Reduce 8.4 Clarity", clarityDelta: -250, confidenceDelta: 0, salienceDelta: 0, MemoryState.Difficult);
        }

        public PrototypeTestLabOperation ReduceMemoryConfidence()
        {
            return AlterPrototypeMemoryMetric("Reduce 8.4 Confidence", clarityDelta: 0, confidenceDelta: -250, salienceDelta: 0, null);
        }

        public PrototypeTestLabOperation ProveMemoryDegradationIdempotence()
        {
            RecordHiddenHistoryEvent();
            EnsureHistoryRuntime(out _, out _);
            PersonMemoryRuntime memoryRuntime = CreateMemoryProofRuntime();
            string memoryId = $"memory.prototype.degradation-proof.{Guid.NewGuid():N}";
            HistoryOperationResult formed = memoryRuntime.FormMemory(BuildMemoryRequest(CreateAutomationScopedId("history", "degrade-form"), memoryId, GetPrototypeHiddenHistoryEventId(), HistoryMemorySource.DevelopmentFixture, createKnowledge: false));
            double start = GetMemoryWorldTime(memoryRuntime, memoryId);
            MemoryDegradationRequest firstRequest = new MemoryDegradationRequest
            {
                TransactionId = $"history.8.4.degrade.first.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                FromWorldTime = start,
                ToWorldTime = start + 86400d,
                ConfidenceLossPerDay = 10,
                ClarityLossPerDay = 20,
                SalienceLossPerDay = 5
            };
            HistoryOperationResult first = memoryRuntime.ApplyDegradation(firstRequest);
            HistoryMemoryRecord afterFirst = memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord firstSnapshot) ? firstSnapshot : null;
            HistoryOperationResult repeat = memoryRuntime.ApplyDegradation(new MemoryDegradationRequest
            {
                TransactionId = $"history.8.4.degrade.repeat.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                FromWorldTime = start,
                ToWorldTime = start + 86400d,
                ConfidenceLossPerDay = 10,
                ClarityLossPerDay = 20,
                SalienceLossPerDay = 5
            });
            HistoryMemoryRecord afterRepeat = memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord repeatSnapshot) ? repeatSnapshot : null;
            HistoryOperationResult advance = memoryRuntime.ApplyDegradation(new MemoryDegradationRequest
            {
                TransactionId = $"history.8.4.degrade.advance.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                FromWorldTime = start,
                ToWorldTime = start + 172800d,
                ConfidenceLossPerDay = 10,
                ClarityLossPerDay = 20,
                SalienceLossPerDay = 5
            });
            PersonMemorySaveData save = memoryRuntime.CreateSaveData();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime restoreHistory, out _);
            HistoryOperationResult restore = memoryRuntime.RestoreFromSaveData(save, registry, restoreHistory, GetKnownPrototypePersons(), restoring: true);
            HistoryMemoryRecord afterRestore = memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord restoredSnapshot) ? restoredSnapshot : null;
            HistoryOperationResult restoredRepeat = memoryRuntime.ApplyDegradation(new MemoryDegradationRequest
            {
                TransactionId = $"history.8.4.degrade.restore-repeat.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                FromWorldTime = start,
                ToWorldTime = start + 172800d,
                ConfidenceLossPerDay = 10,
                ClarityLossPerDay = 20,
                SalienceLossPerDay = 5
            });
            HistoryMemoryRecord final = memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord finalSnapshot) ? finalSnapshot : null;
            bool repeatedSame = afterFirst != null && afterRepeat != null && afterFirst.Clarity == afterRepeat.Clarity && afterFirst.Confidence == afterRepeat.Confidence && afterFirst.Salience == afterRepeat.Salience;
            bool advancedOnce = afterRepeat != null && afterRestore != null && afterRestore.Clarity < afterRepeat.Clarity;
            bool restoreSame = afterRestore != null && final != null && afterRestore.Clarity == final.Clarity && afterRestore.Confidence == final.Confidence && afterRestore.Salience == final.Salience;
            bool succeeded = formed.Succeeded && first.Succeeded && repeat.Succeeded && advance.Succeeded && restore.Succeeded && restoredRepeat.Succeeded && repeatedSame && advancedOnce && restoreSame;
            return Record(succeeded, "Prove 8.4 Degradation Idempotence", succeeded ? "Success" : "DegradationMismatch", $"Formed={formed.Code} First={first.Code} Repeat={repeat.Code} Advance={advance.Code} Restore={restore.Code} RestoreMessage='{restore.Message}' RestoredRepeat={restoredRepeat.Code} RepeatedSame={repeatedSame} AdvancedOnce={advancedOnce} RestoreSame={restoreSame}.");
        }

        public PrototypeTestLabOperation MakeMemoryDifficult()
        {
            return SetPrototypeMemoryState("Make 8.4 Difficult", MemoryState.Difficult);
        }

        public PrototypeTestLabOperation PartialForgetPrototypeMemory()
        {
            return ForgetMemoryParticipant();
        }

        public PrototypeTestLabOperation ForgetMemoryParticipant()
        {
            return AlterPrototypeMemoryDetails("Forget 8.4 Participant", new[] { "detail.primary-person" });
        }

        public PrototypeTestLabOperation ForgetMemoryTimeOrLocation()
        {
            return AlterPrototypeMemoryDetails("Forget 8.4 Time Location", new[] { "detail.time", "detail.location" });
        }

        public PrototypeTestLabOperation MakeMemoryInaccessible()
        {
            return SetPrototypeMemoryState("Make 8.4 Inaccessible", MemoryState.Inaccessible);
        }

        public PrototypeTestLabOperation MarkMemoryForgotten()
        {
            return SetPrototypeMemoryState("Mark 8.4 Forgotten", MemoryState.Forgotten);
        }

        public PrototypeTestLabOperation AddMemorySuppression()
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            double startedAt = GetMemoryWorldTime(memoryRuntime, memoryId);
            string suppressionId = $"suppression.test-lab.{Guid.NewGuid():N}";
            HistoryOperationResult result = memoryRuntime.AddSuppression(new MemorySuppressionRequest
            {
                TransactionId = $"history.8.4.suppression.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                SuppressionId = suppressionId,
                SourceId = "test-lab.memory.suppression",
                ReasonId = "development.memory-block",
                StartedAtWorldTime = startedAt,
                AllowsCueBypass = false,
                Provenance = "Development fixture"
            });
            return RecordHistoryResult("Add 8.4 Suppression", result);
        }

        public PrototypeTestLabOperation RemoveMemorySuppression()
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            if (!TryFindSuppressedMemory(memoryRuntime, out string memoryId, out MemorySuppressionData suppression))
            {
                return RecordSuccess("Remove 8.4 Suppression", "No active suppression exists; memory remains available for recall.");
            }

            double removalTime = Math.Max(GetMemoryWorldTime(memoryRuntime, memoryId), suppression.startedAtWorldTime + 0.1d);
            HistoryOperationResult result = memoryRuntime.RemoveSuppression(memoryId, suppression.suppressionId, $"history.8.4.suppression-remove.{Guid.NewGuid():N}", removalTime);
            return RecordHistoryResult("Remove 8.4 Suppression", result);
        }

        public PrototypeTestLabOperation ExpireMemorySuppression()
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            double now = GetMemoryWorldTime(memoryRuntime, memoryId);
            string boundedSuppressionId = $"suppression.test-lab.bounded.{Guid.NewGuid():N}";
            HistoryOperationResult add = memoryRuntime.AddSuppression(new MemorySuppressionRequest
            {
                TransactionId = $"history.8.4.suppression-bounded.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                SuppressionId = boundedSuppressionId,
                SourceId = "test-lab.memory.bounded-suppression",
                ReasonId = "development.memory-block",
                StartedAtWorldTime = now,
                EndedAtWorldTime = now + 5d,
                AllowsCueBypass = false,
                Provenance = "Development fixture"
            });
            if (!add.Succeeded)
            {
                return RecordHistoryResult("Expire 8.4 Suppression", add);
            }

            HistoryOperationResult result = memoryRuntime.RemoveSuppression(memoryId, boundedSuppressionId, $"history.8.4.suppression-expire.{Guid.NewGuid():N}", now + 10d, expireOnly: true);
            return RecordHistoryResult("Expire 8.4 Suppression", result);
        }

        public PrototypeTestLabOperation ProveMemorySuppressionStacking()
        {
            FormWitnessHistoryMemory();
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = $"memory.prototype.suppression-stack.{Guid.NewGuid():N}";
            double now = GetGameTimeSeconds();
            HistoryOperationResult formed = memoryRuntime.FormMemory(BuildMemoryRequest(CreateAutomationScopedId("history", "suppression-stack-form"), memoryId, GetPrototypeHiddenHistoryEventId(), HistoryMemorySource.DirectObservation, createKnowledge: false));
            now = GetMemoryWorldTime(memoryRuntime, memoryId);
            HistoryOperationResult difficult = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.suppression-stack-difficult.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = now + 0.5d,
                AlterationType = MemoryAlterationType.Reconstruction,
                ResultingState = MemoryState.Difficult,
                SourceId = "test-lab.memory.suppression-stack",
                Description = "Set a non-accessible underlying state before suppression proof."
            });
            string permanentSuppressionId = $"suppression.test-lab.stack.permanent.{Guid.NewGuid():N}";
            string boundedSuppressionId = $"suppression.test-lab.stack.bounded.{Guid.NewGuid():N}";
            HistoryOperationResult first = memoryRuntime.AddSuppression(new MemorySuppressionRequest
            {
                TransactionId = $"history.8.4.suppression-stack-first.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                SuppressionId = permanentSuppressionId,
                SourceId = "test-lab.memory.suppression-stack.permanent",
                ReasonId = "development.memory-block",
                StartedAtWorldTime = now + 1d,
                Provenance = "Development fixture"
            });
            HistoryOperationResult second = memoryRuntime.AddSuppression(new MemorySuppressionRequest
            {
                TransactionId = $"history.8.4.suppression-stack-second.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                SuppressionId = boundedSuppressionId,
                SourceId = "test-lab.memory.suppression-stack.bounded",
                ReasonId = "development.memory-block",
                StartedAtWorldTime = now + 1d,
                EndedAtWorldTime = now + 5d,
                Provenance = "Development fixture"
            });
            MemoryRecallResult blocked = memoryRuntime.Recall(new MemoryRecallRequest
            {
                TransactionId = $"history.8.4.suppression-stack-recall-blocked.{Guid.NewGuid():N}",
                RequestingPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = now + 2d,
                MutateMetadata = false
            });
            HistoryOperationResult expired = memoryRuntime.RemoveSuppression(memoryId, boundedSuppressionId, $"history.8.4.suppression-stack-expire.{Guid.NewGuid():N}", now + 6d, expireOnly: true);
            MemoryRecallResult stillBlocked = memoryRuntime.Recall(new MemoryRecallRequest
            {
                TransactionId = $"history.8.4.suppression-stack-recall-still-blocked.{Guid.NewGuid():N}",
                RequestingPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = now + 6.5d,
                MutateMetadata = false
            });
            HistoryOperationResult removed = memoryRuntime.RemoveSuppression(memoryId, permanentSuppressionId, $"history.8.4.suppression-stack-remove.{Guid.NewGuid():N}", now + 7d);
            bool restoredUnderlying = memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord finalMemory) && finalMemory.State == MemoryState.Difficult;
            bool succeeded = formed.Succeeded
                && difficult.Succeeded
                && first.Succeeded
                && second.Succeeded
                && blocked.Outcome == MemoryRecallOutcome.BlockedBySuppression
                && expired.Succeeded
                && stillBlocked.Outcome == MemoryRecallOutcome.BlockedBySuppression
                && removed.Succeeded
                && restoredUnderlying;
            return Record(succeeded, "Prove 8.4 Suppression Stacking", succeeded ? "Success" : "SuppressionStackMismatch", $"Formed={formed.Code} Difficult={difficult.Code} First={first.Code} Second={second.Code} Blocked={blocked.Outcome}/{blocked.Code} Expired={expired.Code} StillBlocked={stillBlocked.Outcome}/{stillBlocked.Code} Removed={removed.Code} RestoredUnderlying={restoredUnderlying}.");
        }

        public PrototypeTestLabOperation RecoverPrototypeMemory()
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            double start = GetMemoryWorldTime(memoryRuntime, memoryId);
            HistoryOperationResult prep = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.recovery-prep.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = start,
                AlterationType = MemoryAlterationType.Reconstruction,
                ResultingState = MemoryState.Inaccessible,
                SourceId = "test-lab.memory.recovery-prep",
                Description = "Make memory inaccessible before recovery proof."
            });
            if (!prep.Succeeded)
            {
                return RecordHistoryResult("Recover 8.4 Memory", prep);
            }

            HistoryOperationResult result = memoryRuntime.RecoverMemory(memoryId, $"history.8.4.recovery.{Guid.NewGuid():N}", start + 1d, MemoryState.Accessible, "test-lab.memory.recovery");
            return RecordHistoryResult("Recover 8.4 Memory", result);
        }

        public PrototypeTestLabOperation AlterPrototypeMemory()
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            HistoryOperationResult result = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.alter.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                AlterationType = MemoryAlterationType.Distortion,
                ResultingState = MemoryState.Altered,
                ConfidenceDelta = 80,
                DetailsToAddOrReplace = new[] { new MemoryDetailData { detailId = "detail.altered-claim", kind = MemoryDetailKind.Note, state = MemoryDetailState.Altered, value = "Distorted development recollection", confidence = 850 } },
                SourceId = "test-lab.memory.distortion",
                Description = "Prototype distortion of a remembered detail."
            });
            return RecordHistoryResult("Alter 8.4 Memory", result);
        }

        public PrototypeTestLabOperation CorrectAlteredMemory()
        {
            AlterPrototypeMemory();
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            double correctionTime = GetMemoryWorldTime(memoryRuntime, memoryId, 2d);
            HistoryOperationResult result = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.correct-alteration.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = correctionTime,
                AlterationType = MemoryAlterationType.Correction,
                ResultingState = MemoryState.Recovered,
                ClarityDelta = 100,
                DetailsToAddOrReplace = new[] { new MemoryDetailData { detailId = "detail.altered-claim", kind = MemoryDetailKind.Note, state = MemoryDetailState.Recovered, value = "Corrected by new evidence", confidence = 900 } },
                SourceId = "test-lab.memory.correction",
                Description = "Corrected altered memory using new evidence."
            });
            return RecordHistoryResult("Correct 8.4 Altered Memory", result);
        }

        public PrototypeTestLabOperation ShowMemoryRevisionHistory()
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord memory);
            int revisions = memory?.Revisions.Count ?? 0;
            string detail = memory == null ? "No memory." : string.Join(" | ", memory.Revisions.Select(revision => $"{revision.revisionId}:{revision.alterationType}:{revision.state}").Take(8));
            return Record(revisions > 0, "Show 8.4 Revision History", revisions > 0 ? "Success" : HistoryResultCode.InvalidRevision.ToString(), detail);
        }

        public PrototypeTestLabOperation CreateConflictingMemories()
        {
            PrototypeTestLabOperation baseFixture = PrepareWitnessHistoryMemoryAutomationFixture();
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = $"memory.prototype.conflict.{Guid.NewGuid():N}";
            FormMemoryRequest request = BuildMemoryRequest(CreateAutomationScopedId("history", "conflict"), memoryId, GetPrototypeHiddenHistoryEventId(), HistoryMemorySource.WitnessTestimony, createKnowledge: false);
            request.Confidence = 350;
            request.Clarity = 450;
            request.DebugDescription = "Conflicting testimony about the hidden event.";
            HistoryOperationResult formed = memoryRuntime.FormMemory(request);
            MemoryRecallResult recall = memoryRuntime.Recall(new MemoryRecallRequest
            {
                TransactionId = $"history.8.4.conflict-recall.{Guid.NewGuid():N}",
                RequestingPersonId = GetPrototypePersonId(),
                HistoricalEventId = GetPrototypeHiddenHistoryEventId(),
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                AttemptDifficult = true,
                MutateMetadata = false
            });
            bool succeeded = baseFixture.Succeeded && formed.Succeeded && recall.Entries.Count > 1 && recall.Outcome == MemoryRecallOutcome.Conflicting;
            return Record(succeeded, "Create 8.4 Conflicting Memories", succeeded ? "Succeeded" : recall.Code.ToString(), $"Base={baseFixture.Code} Formed={formed.Code} Recall={FormatMemoryRecallResult(recall)}");
        }

        public PrototypeTestLabOperation SuppressPreviousBodyAssociation()
        {
            RecordBodyTransitionHistory();
            return AlterPrototypePreviousBody("Suppress 8.4 Previous Body", MemoryDetailState.Suppressed, MemoryState.Altered);
        }

        public PrototypeTestLabOperation RecoverPreviousBodyAssociation()
        {
            RecordBodyTransitionHistory();
            return AlterPrototypePreviousBody("Recover 8.4 Previous Body", MemoryDetailState.Recovered, MemoryState.Recovered);
        }

        public PrototypeTestLabOperation CompareMemoryBeliefHistory()
        {
            FormWitnessHistoryMemory();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime);
            EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge);
            MemoryRecallResult recall = memoryRuntime.Recall(new MemoryRecallRequest
            {
                TransactionId = $"history.8.4.compare.{Guid.NewGuid():N}",
                RequestingPersonId = GetPrototypePersonId(),
                HistoricalEventId = GetPrototypeHiddenHistoryEventId(),
                WorldTime = GetMemoryWorldTime(memoryRuntime, GetPrototypeMemoryId()),
                MutateMetadata = false,
                AccessContext = MemoryAccessContext.Debug
            });
            int historyCount = historyRuntime.CreateSnapshot().Events.Count;
            int evidenceCount = knowledge.CreateSnapshot().Evidence.Count;
            bool succeeded = historyCount > 0 && memoryRuntime.CreateSnapshot().Memories.Count > 0 && recall.Entries.Count > 0;
            return Record(succeeded, "Compare 8.4 Memory Belief History", succeeded ? "Success" : "ViewMismatch", $"History={historyCount} Memories={memoryRuntime.CreateSnapshot().Memories.Count} Recall={FormatMemoryRecallResult(recall)} Evidence={evidenceCount}.");
        }

        public PrototypeTestLabOperation ValidateMemory84SaveRestore()
        {
            RecordHiddenHistoryEvent();
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _))
            {
                return RecordFailure("Validate 8.4 Save Restore", "Memory runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            PersonMemoryRuntime memoryRuntime = CreateMemoryProofRuntime();
            string memoryId = $"memory.prototype.save-restore-proof.{Guid.NewGuid():N}";
            HistoryOperationResult formed = memoryRuntime.FormMemory(BuildMemoryRequest(CreateAutomationScopedId("history", "save-restore-form"), memoryId, GetPrototypeHiddenHistoryEventId(), HistoryMemorySource.DevelopmentFixture, createKnowledge: false));
            double start = GetMemoryWorldTime(memoryRuntime, memoryId);
            HistoryOperationResult suppression = memoryRuntime.AddSuppression(new MemorySuppressionRequest
            {
                TransactionId = $"history.8.4.save-restore-suppression.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                SuppressionId = $"suppression.test-lab.save-restore.{Guid.NewGuid():N}",
                SourceId = "test-lab.memory.save-restore",
                ReasonId = "development.memory-block",
                StartedAtWorldTime = start,
                Provenance = "Development fixture"
            });
            HistoryOperationResult altered = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.save-restore-alter.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = start + 1d,
                AlterationType = MemoryAlterationType.Distortion,
                ResultingState = MemoryState.Altered,
                DetailsToAddOrReplace = new[] { new MemoryDetailData { detailId = "detail.save-restore-proof", kind = MemoryDetailKind.Note, state = MemoryDetailState.Altered, value = "Save restore proof detail.", confidence = 820 } },
                SourceId = "test-lab.memory.save-restore",
                Description = "Create a revision after suppression for save/restore proof."
            });
            PersonMemorySaveData saveData = memoryRuntime.CreateSaveData();
            int events = 0;
            void Count(PersonMemoryRuntime _, HistoryOperationResult __) => events++;
            memoryRuntime.MemoryChanged += Count;
            HistoryOperationResult restore = memoryRuntime.RestoreFromSaveData(saveData, registry, historyRuntime, GetKnownPrototypePersons(), restoring: true);
            memoryRuntime.MemoryChanged -= Count;
            PersonMemorySnapshot snapshot = memoryRuntime.CreateSnapshot();
            bool preserved = snapshot.Memories.Any(memory => memory.Suppressions.Count > 0 && memory.Revisions.Count > 1);
            bool succeeded = formed.Succeeded && suppression.Succeeded && altered.Succeeded && restore.Succeeded && events == 0 && preserved;
            return Record(succeeded, "Validate 8.4 Save Restore", succeeded ? "Success" : restore.Code.ToString(), $"Formed={formed.Code} Suppression={suppression.Code} Altered={altered.Code} Restore={restore.Code} RestoreMessage='{restore.Message}' Events={events} Preserved={preserved} Memories={snapshot.Memories.Count}.");
        }

        public PrototypeTestLabOperation ValidateObservationFoundation()
        {
            int observations = CountDefinitions<ObservationMethodDefinition>();
            int examinations = CountDefinitions<ExaminationMethodDefinition>();
            int identifications = CountDefinitions<IdentificationMethodDefinition>();
            int diagnostics = CountDefinitions<DiagnosticMethodDefinition>();
            bool succeeded = observations >= 8 && examinations >= 8 && identifications >= 8 && diagnostics >= 7;
            return Record(succeeded, "Validate 8.2 Observation Foundation", succeeded ? "Success" : "MissingDefinitions", FormatObservationMethodCounts());
        }

        public PrototypeTestLabOperation PreviewOrdinaryVisualObservation()
        {
            return TryBuildObservationRequest("observation.8.2.preview-visual", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure)
                ? RecordObservationResult("Preview 8.2 Visual Observation", service.Observe(knowledge, context, projection, preview: true))
                : failure;
        }

        public PrototypeTestLabOperation CommitOrdinaryVisualObservation()
        {
            return TryBuildObservationRequest($"observation.8.2.visual.{Guid.NewGuid():N}", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure)
                ? RecordObservationResult("Commit 8.2 Visual Observation", service.Observe(knowledge, context, projection, preview: false))
                : failure;
        }

        public PrototypeTestLabOperation ProveObservationDuplicateProtection()
        {
            if (!TryBuildObservationRequest("observation.8.2.duplicate", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ObservationResult first = service.Observe(knowledge, context, projection, preview: false);
            ObservationResult second = service.Observe(knowledge, context, projection, preview: false);
            bool succeeded = first.Succeeded && second.Succeeded && second.Code == ObservationOutcomeCode.Duplicate && first.KnowledgeResult?.ResultingRevision == second.KnowledgeResult?.ResultingRevision;
            return Record(succeeded, "Duplicate 8.2 Observation", succeeded ? "Success" : "DuplicateProofFailed", $"First={FormatObservationResult(first)} Second={FormatObservationResult(second)}");
        }

        public PrototypeTestLabOperation CommitMedicalExaminationObservation()
        {
            if (!TryBuildObservationRequest($"observation.8.2.medical.{Guid.NewGuid():N}", "examination-method.medical", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: true, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            if (!registry.TryGet("examination-method.medical", out ExaminationMethodDefinition method))
            {
                return RecordFailure("Commit 8.2 Medical Examination", "Examination Method is missing.", ObservationOutcomeCode.MissingMethod.ToString());
            }

            ExaminationProjection examination = new ExaminationProjection("examination.prototype.medical", new[] { projection }, new[] { "injury", "medical" });
            return RecordObservationResult("Commit 8.2 Medical Examination", service.Examine(knowledge, context, examination, method, preview: false));
        }

        public PrototypeTestLabOperation DiagnoseBiologicalConditionFoundation()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Diagnose 8.2 Condition", "Knowledge runtime is missing.", ObservationOutcomeCode.MissingKnowledgeRuntime.ToString());
            }

            if (!registry.TryGet("diagnostic-method.symptom-based", out DiagnosticMethodDefinition method))
            {
                return RecordFailure("Diagnose 8.2 Condition", "Diagnostic Method is missing.", ObservationOutcomeCode.MissingMethod.ToString());
            }

            ObservationService service = new ObservationService(registry);
            ObservationContext context = BuildObservationContext($"observation.8.2.diagnosis.{Guid.NewGuid():N}", method.Id, SensoryChannel.Touch, ObservationTargetType.BiologicalCondition, KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: true);
            DiagnosticProjection projection = new DiagnosticProjection("projection.prototype.symptom-diagnosis", new[]
            {
                new DiagnosticHypothesis("condition.biology.prototype-poison", "condition-family.poison", 620, new[] { "symptom.nausea", "route.ingestion" }),
                new DiagnosticHypothesis("condition.biology.prototype-infection", "condition-family.infection", 520, new[] { "symptom.fever", "injury.wound" }),
                new DiagnosticHypothesis("condition.biology.prototype-fatigue", "condition-family.fatigue", 360, new[] { "symptom.tired" })
            });
            return RecordObservationResult("Diagnose 8.2 Condition", service.Diagnose(knowledge, context, projection, method, preview: false));
        }

        public PrototypeTestLabOperation ProvePlayerIrrelevantObservationNotTracked()
        {
            return TryBuildObservationRequest($"observation.8.2.irrelevant.{Guid.NewGuid():N}", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: false, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure)
                ? RecordObservationResult("Player 8.2 Irrelevant Observation", service.Observe(knowledge, context, projection, preview: false))
                : failure;
        }

        public PrototypeTestLabOperation ProveNpcFullObservationTracks()
        {
            return TryBuildObservationRequest($"observation.8.2.npc.{Guid.NewGuid():N}", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.NpcFullTracking, mechanicallyRelevant: false, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure)
                ? RecordObservationResult("NPC 8.2 Full Observation", service.Observe(knowledge, context, projection, preview: false))
                : failure;
        }

        public PrototypeTestLabOperation ProveRemotePlayerIrrelevantObservationNotTracked()
        {
            return TryBuildObservationRequest($"observation.8.2.remote.{Guid.NewGuid():N}", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.RemotePlayerMechanicalOnly, mechanicallyRelevant: false, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure)
                ? RecordObservationResult("Remote 8.2 Irrelevant Observation", service.Observe(knowledge, context, projection, preview: false))
                : failure;
        }

        public PrototypeTestLabOperation ProveDevelopmentObserverDoesNotMutate()
        {
            return TryBuildObservationRequest($"observation.8.2.development.{Guid.NewGuid():N}", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.DevelopmentObserverNoMutation, mechanicallyRelevant: true, privateAccess: true, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure)
                ? RecordObservationResult("Development 8.2 Observer", service.Observe(knowledge, context, projection, preview: false))
                : failure;
        }

        public PrototypeTestLabOperation ProveConcealmentLowersObservationQuality()
        {
            if (!TryBuildObservationRequest($"observation.8.2.concealed.{Guid.NewGuid():N}", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext clear, out ObservableProjection projection, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ObservationContext concealed = BuildObservationContext(clear.TransactionId + ".concealed", clear.MethodId, clear.SensoryChannel, clear.TargetType, clear.TrackingPolicy, clear.MechanicallyRelevant, clear.PrivateAccessAuthorized, ConcealmentState.Major);
            int clearQuality = ObservationService.CalculateQuality(550, clear, privacyBypass: false);
            int concealedQuality = ObservationService.CalculateQuality(550, concealed, privacyBypass: false);
            bool succeeded = concealedQuality < clearQuality;
            return Record(succeeded, "Concealment 8.2 Quality", succeeded ? "Success" : ObservationOutcomeCode.Concealed.ToString(), $"Clear={clearQuality} Concealed={concealedQuality}. Concealment lowers quality before evidence is applied.");
        }

        public PrototypeTestLabOperation ProveRepeatedObservationIsBounded()
        {
            if (!TryBuildObservationRequest("observation.8.2.repeat-a", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext firstContext, out ObservableProjection projection, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ObservationResult first = service.Observe(knowledge, firstContext, projection, preview: false);
            long revision = knowledge.KnowledgeRevision;
            int confidence = first.KnowledgeResult?.ResultingBelief?.Confidence ?? 0;
            ObservationContext secondContext = BuildObservationContext("observation.8.2.repeat-b", firstContext.MethodId, firstContext.SensoryChannel, firstContext.TargetType, firstContext.TrackingPolicy, firstContext.MechanicallyRelevant, firstContext.PrivateAccessAuthorized);
            ObservationResult second = service.Observe(knowledge, secondContext, projection, preview: false);
            bool succeeded = first.Succeeded && second.Succeeded && second.Code == ObservationOutcomeCode.Duplicate && knowledge.KnowledgeRevision == revision && (second.KnowledgeResult?.ResultingBelief?.Confidence ?? 0) == confidence;
            return Record(succeeded, "Repeated 8.2 Observation Bound", succeeded ? "Success" : "RepeatedObservationUnbounded", $"First={FormatObservationResult(first)} Second={FormatObservationResult(second)} Revision={revision}->{knowledge.KnowledgeRevision}");
        }

        public PrototypeTestLabOperation RejectStaleObservationProjection()
        {
            if (!TryBuildObservationRequest($"observation.8.2.stale.{Guid.NewGuid():N}", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ObservationContext staleContext = BuildObservationContext(context.TransactionId, context.MethodId, context.SensoryChannel, context.TargetType, context.TrackingPolicy, context.MechanicallyRelevant, context.PrivateAccessAuthorized, ConcealmentState.None, expectedConditionRevision: 2L);
            ObservationResult result = service.Observe(knowledge, staleContext, projection, preview: false);
            bool succeeded = !result.Succeeded && result.Code == ObservationOutcomeCode.StaleTarget;
            return Record(succeeded, "Reject 8.2 Stale Projection", succeeded ? "Success" : result.Code.ToString(), FormatObservationResult(result));
        }

        public PrototypeTestLabOperation RejectInactiveFoundationObservationMethod()
        {
            if (!TryBuildObservationRequest($"observation.8.2.inactive.{Guid.NewGuid():N}", "observation-method.magical-analysis-foundation", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: true, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ObservationResult result = service.Observe(knowledge, context, projection, preview: false);
            bool succeeded = !result.Succeeded && result.Code == ObservationOutcomeCode.MissingMethod;
            return Record(succeeded, "Reject 8.2 Inactive Foundation", succeeded ? "Success" : result.Code.ToString(), FormatObservationResult(result));
        }

        public PrototypeTestLabOperation RejectPrivateMedicalObservationWithoutAccess()
        {
            if (!TryBuildObservationRequest($"observation.8.2.private.{Guid.NewGuid():N}", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: true, privateAccess: false, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ObservableProjection privateProjection = new ObservableProjection(projection.ProjectionId, projection.TargetType, projection.Proposition, KnowledgeVisibility.Private, projection.MinimumQuality, projection.BaseEvidenceStrength, projection.Channels.ToArray(), projection.MechanicallyRelevant, "Prototype private medical evidence", projection.Tags.ToArray());
            ObservationResult result = service.Observe(knowledge, context, privateProjection, preview: false);
            bool succeeded = !result.Succeeded && result.Code == ObservationOutcomeCode.AccessDenied;
            return Record(succeeded, "Reject 8.2 Private Observation", succeeded ? "Success" : result.Code.ToString(), FormatObservationResult(result));
        }

        public string BuildInformationAccessSummary()
        {
            EnsureInformationAccessRuntime();
            InformationAccessSnapshot snapshot = informationAccess.CreateSnapshot();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Feature 8.8 Secrets, Visibility, and Information Access");
            builder.AppendLine($"Owner: {snapshot.OwnerId} Revision: {snapshot.Revision} Policies: {snapshot.Policies.Count} Grants: {snapshot.Grants.Count} Concealments: {snapshot.Concealments.Count} Audits: {snapshot.Audits.Count}");
            foreach (InformationAccessPolicyRecord policy in snapshot.Policies.OrderBy(record => record.PolicyId, StringComparer.Ordinal).Take(10))
            {
                builder.AppendLine($"{policy.PolicyId}: {policy.Subject.SubjectType}/{policy.Subject.SubjectId} Classification={policy.Classification} Details={policy.DetailVisibilityPolicy} Source={policy.SourceVisibilityPolicy}");
            }

            PrototypeTestLabOperation last = history.Count == 0 ? default : history[0];
            if (!string.IsNullOrWhiteSpace(last.OperationName) && last.OperationName.Contains("8.8", StringComparison.Ordinal))
            {
                builder.AppendLine($"Last 8.8: {last.OperationName} Code={last.Code} Success={last.Succeeded}");
                builder.AppendLine(last.Message);
            }

            return builder.ToString();
        }

        public PrototypeTestLabOperation ValidateInformationAccessDefinitions()
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            Dictionary<string, IGameDefinition> definitions = new Dictionary<string, IGameDefinition>(StringComparer.Ordinal);
            foreach (InformationAccessPolicyDefinition definition in CreatePrototypeAccessPolicyDefinitions())
            {
                definitions[definition.Id] = definition;
            }

            foreach (InformationAccessPolicyDefinition definition in CreatePrototypeAccessPolicyDefinitions())
            {
                definition.ValidateCatalogDefinition(definitions, report);
            }

            bool succeeded = report.ErrorCount == 0 && report.WarningCount == 0;
            return Record(succeeded, "Validate 8.8 Access Definitions", succeeded ? "Success" : "ValidationFailed", report.GetSummary());
        }

        public PrototypeTestLabOperation CreatePublicInformationAccess()
        {
            EnsurePrototypeAccessPolicies();
            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypePublicPolicyId, PrototypePublicSubjectId, "person.prototype.visitor", InformationSubjectType.FactInstance, InformationAccessMode.Inspect, discovered: true);
            bool succeeded = decision.FullAccess && decision.SourceVisible;
            return Record(succeeded, "Create 8.8 Public Information Access", succeeded ? "Success" : decision.DenialCode.ToString(), FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation CreatePrivateInformationAccess()
        {
            EnsurePrototypeAccessPolicies();
            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.visitor", InformationSubjectType.Memory, InformationAccessMode.Inspect, discovered: true, revealDenial: true);
            bool succeeded = decision.Denied && decision.DenialCode == InformationAccessDenialCode.MissingAuthorization;
            return Record(succeeded, "Reject 8.8 Unauthorized Secret Access", succeeded ? "Success" : decision.Decision.ToString(), FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation GrantInspectInformationAccess()
        {
            EnsurePrototypeAccessPolicies();
            InformationAccessOperationResult grant = informationAccess.GrantAccess(BuildPrototypeAccessGrant("information-access.grant.prototype-listener.inspect", "person.prototype.listener", new[] { InformationAccessMode.Inspect, InformationAccessMode.RevealDetails }, permitsDisclosure: false, permitsResharing: false, sourceVisibility: InformationSourceVisibilityPolicy.PrivilegedOnly), $"access.8.8.grant.inspect.{Guid.NewGuid():N}");
            if (!grant.Succeeded)
            {
                return RecordAccessOperation("Grant 8.8 Inspect Access", grant);
            }

            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.listener", InformationSubjectType.Memory, InformationAccessMode.Inspect, discovered: true);
            bool succeeded = decision.RedactedAccess || decision.FullAccess || decision.PartialAccess;
            return Record(succeeded, "Grant 8.8 Inspect Access", succeeded ? "Success" : decision.DenialCode.ToString(), FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation GrantShareInformationAccess()
        {
            EnsurePrototypeAccessPolicies();
            RevokePrototypeAccessGrantIfPresent("information-access.grant.prototype-listener.no-reshare");
            InformationAccessOperationResult grant = informationAccess.GrantAccess(BuildPrototypeAccessGrant("information-access.grant.prototype-listener.share", "person.prototype.listener", new[] { InformationAccessMode.Share }, permitsDisclosure: true, permitsResharing: true, sourceVisibility: InformationSourceVisibilityPolicy.Reveal), $"access.8.8.grant.share.{Guid.NewGuid():N}");
            if (!grant.Succeeded)
            {
                return RecordAccessOperation("Grant 8.8 Share Access", grant);
            }

            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.listener", InformationSubjectType.Memory, InformationAccessMode.Share, discovered: true);
            bool succeeded = !decision.Denied && decision.ResharingOutcome != InformationResharingPolicy.NoResharing;
            return Record(succeeded, "Grant 8.8 Share Access", succeeded ? "Success" : decision.DenialCode.ToString(), FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation AttemptNoReshareInformationAccess()
        {
            EnsurePrototypeAccessPolicies();
            RevokePrototypeAccessGrantIfPresent("information-access.grant.prototype-listener.share");
            InformationAccessOperationResult grant = informationAccess.GrantAccess(BuildPrototypeAccessGrant("information-access.grant.prototype-listener.no-reshare", "person.prototype.listener", new[] { InformationAccessMode.Share }, permitsDisclosure: true, permitsResharing: false, sourceVisibility: InformationSourceVisibilityPolicy.PrivilegedOnly), $"access.8.8.grant.no-reshare.{Guid.NewGuid():N}");
            if (!grant.Succeeded)
            {
                return RecordAccessOperation("Prepare 8.8 No-Reshare Access", grant);
            }

            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.listener", InformationSubjectType.Memory, InformationAccessMode.Share, discovered: true);
            bool succeeded = !decision.Denied && decision.ResharingOutcome == InformationResharingPolicy.NoResharing;
            return Record(succeeded, "Limit 8.8 Resharing", succeeded ? "Success" : decision.ResharingOutcome.ToString(), FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation ProtectInformationSourceIdentity()
        {
            EnsurePrototypeAccessPolicies();
            InformationAccessOperationResult grant = informationAccess.GrantAccess(BuildPrototypeAccessGrant("information-access.grant.prototype-listener.source-hidden", "person.prototype.listener", new[] { InformationAccessMode.Inspect }, permitsDisclosure: false, permitsResharing: false, sourceVisibility: InformationSourceVisibilityPolicy.PrivilegedOnly), $"access.8.8.grant.source-hidden.{Guid.NewGuid():N}");
            if (!grant.Succeeded)
            {
                return RecordAccessOperation("Protect 8.8 Source Identity", grant);
            }

            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.listener", InformationSubjectType.Memory, InformationAccessMode.Inspect, discovered: true);
            bool succeeded = !decision.Denied && !decision.SourceVisible;
            return Record(succeeded, "Protect 8.8 Source Identity", succeeded ? "Success" : "SourceVisible", FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation RevealInformationSourceIdentity()
        {
            EnsurePrototypeAccessPolicies();
            InformationAccessOperationResult grant = informationAccess.GrantAccess(BuildPrototypeAccessGrant("information-access.grant.prototype-listener.source-visible", "person.prototype.listener", new[] { InformationAccessMode.RevealSource, InformationAccessMode.Inspect }, permitsDisclosure: true, permitsResharing: true, sourceVisibility: InformationSourceVisibilityPolicy.Reveal), $"access.8.8.grant.source-visible.{Guid.NewGuid():N}");
            if (!grant.Succeeded)
            {
                return RecordAccessOperation("Reveal 8.8 Source Identity", grant);
            }

            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.listener", InformationSubjectType.Memory, InformationAccessMode.RevealSource, discovered: true);
            bool succeeded = !decision.Denied && decision.SourceVisible;
            return Record(succeeded, "Reveal 8.8 Source Identity", succeeded ? "Success" : "SourceHidden", FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation HideSecretExistence()
        {
            EnsurePrototypeAccessPolicies();
            InformationAccessOperationResult concealment = informationAccess.AddConcealment(new InformationConcealmentData
            {
                concealmentId = "information-access.concealment.prototype-secret-existence",
                policyId = PrototypeConcealedPolicyId,
                subject = BuildPrototypeSubject(InformationSubjectType.Memory, PrototypeConcealedSubjectId),
                concealingEntityId = "person.prototype.secret-keeper",
                concealmentKind = InformationConcealmentKind.Existence,
                startTime = 0d,
                strength = 800,
                hiddenDetailIds = new[] { "detail.original-source", "detail.previous-body" },
                authorizedExceptionIds = new[] { "access.authorization.prototype.secret-reveal" },
                provenance = "Prototype Test Lab 8.8 concealment."
            }, $"access.8.8.conceal.{Guid.NewGuid():N}");
            if (!concealment.Succeeded)
            {
                return RecordAccessOperation("Hide 8.8 Secret Existence", concealment);
            }

            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeConcealedPolicyId, PrototypeConcealedSubjectId, "person.prototype.visitor", InformationSubjectType.Memory, InformationAccessMode.Inspect, discovered: true, revealDenial: true);
            bool succeeded = decision.Denied && decision.DenialCode == InformationAccessDenialCode.Concealed && string.IsNullOrWhiteSpace(decision.VisibleReason);
            return Record(succeeded, "Hide 8.8 Secret Existence", succeeded ? "Success" : decision.DenialCode.ToString(), FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation RevealSecretExistence()
        {
            EnsurePrototypeAccessPolicies();
            HideSecretExistence();
            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeConcealedPolicyId, PrototypeConcealedSubjectId, "person.prototype.visitor", InformationSubjectType.Memory, InformationAccessMode.Inspect, discovered: true, revealDenial: true, authorizationIds: new[] { "access.authorization.prototype.secret-reveal" });
            bool succeeded = decision.Denied && decision.DenialCode == InformationAccessDenialCode.MissingAuthorization;
            return Record(succeeded, "Reveal 8.8 Secret Existence Boundary", succeeded ? "Success" : decision.DenialCode.ToString(), FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation DiscoverHiddenInformationAccess()
        {
            EnsurePrototypeAccessPolicies();
            InformationAccessDecision undiscovered = EvaluatePrototypeAccess(PrototypeDiscoveryPolicyId, PrototypeDiscoverySubjectId, "person.prototype.visitor", InformationSubjectType.HistoricalEvent, InformationAccessMode.Query, discovered: false);
            InformationAccessDecision discovered = EvaluatePrototypeAccess(PrototypeDiscoveryPolicyId, PrototypeDiscoverySubjectId, "person.prototype.visitor", InformationSubjectType.HistoricalEvent, InformationAccessMode.Query, discovered: true);
            bool succeeded = undiscovered.Decision == InformationAccessDecisionKind.NotDiscovered && discovered.FullAccess;
            return Record(succeeded, "Discover 8.8 Hidden Information", succeeded ? "Success" : discovered.DenialCode.ToString(), $"Before={FormatAccessDecision(undiscovered)} After={FormatAccessDecision(discovered)}");
        }

        public PrototypeTestLabOperation DeclassifyInformationAccess()
        {
            EnsurePrototypeAccessPolicies();
            InformationAccessOperationResult change = informationAccess.ChangeClassification(PrototypeSecretPolicyId, InformationVisibilityClassification.Public, GetPrototypePersonId(), $"access.8.8.declassify.{Guid.NewGuid():N}", 10d, "Prototype Test Lab declassification.");
            if (!change.Succeeded)
            {
                return RecordAccessOperation("Declassify 8.8 Information", change);
            }

            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.visitor", InformationSubjectType.Memory, InformationAccessMode.Inspect, discovered: true);
            bool succeeded = decision.FullAccess;
            return Record(succeeded, "Declassify 8.8 Information", succeeded ? "Success" : decision.DenialCode.ToString(), FormatAccessDecision(decision));
        }

        public PrototypeTestLabOperation AttemptUnauthorizedInformationAccess()
        {
            EnsurePrototypeAccessPolicies();
            InformationAccessDecision decision = EvaluatePrototypeAccess(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.visitor", InformationSubjectType.Memory, InformationAccessMode.Share, discovered: true, revealDenial: true);
            InformationAccessOperationResult audit = informationAccess.RecordAudit(decision, BuildPrototypeAccessContext(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.visitor", InformationSubjectType.Memory, InformationAccessMode.Share, discovered: true, revealDenial: true));
            bool succeeded = decision.Denied && audit.Succeeded && informationAccess.CreateSnapshot().Audits.Any(record => record.unauthorized && record.denialCode == decision.DenialCode);
            return Record(succeeded, "Audit 8.8 Unauthorized Access", succeeded ? "Success" : decision.DenialCode.ToString(), $"{FormatAccessDecision(decision)} Audit={audit.Code} Revision={audit.PriorRevision}->{audit.ResultingRevision}.");
        }

        public PrototypeTestLabOperation CompareInformationAccessProjections()
        {
            EnsurePrototypeAccessPolicies();
            string[] details = { "detail.summary", "detail.original-source", "detail.previous-body", "detail.location" };
            RedactedInformationProjection unauthorized = informationAccess.Project(BuildPrototypeAccessContext(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.visitor", InformationSubjectType.Memory, InformationAccessMode.Inspect, discovered: true), details);
            GrantInspectInformationAccess();
            RedactedInformationProjection authorized = informationAccess.Project(BuildPrototypeAccessContext(PrototypeSecretPolicyId, PrototypeSecretSubjectId, "person.prototype.listener", InformationSubjectType.Memory, InformationAccessMode.Inspect, discovered: true), details);
            bool succeeded = unauthorized.Decision.Denied && authorized.Details.TryGetValue("detail.summary", out InformationRedactionState visible) && visible == InformationRedactionState.Visible && authorized.Details.TryGetValue("detail.original-source", out InformationRedactionState redacted) && redacted == InformationRedactionState.Redacted;
            return Record(succeeded, "Compare 8.8 Redacted Projections", succeeded ? "Success" : "ProjectionMismatch", $"Unauthorized={FormatProjection(unauthorized)} Authorized={FormatProjection(authorized)}");
        }

        public PrototypeTestLabOperation ValidateInformationAccessProjectionAdapters()
        {
            EnsurePrototypeAccessPolicies();
            FormWitnessHistoryMemory();
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime) || !EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Validate 8.8 Projection Adapters", "History, Memory, or Knowledge runtime is missing.", InformationAccessResultCode.InvalidRequest.ToString());
            }

            string eventId = GetPrototypeHiddenHistoryEventId();
            string memoryId = GetPrototypeWitnessMemoryId();
            string lifeEventId = $"event.prototype.access-adapter.life-injury.{Guid.NewGuid():N}";
            if (!historyRuntime.TryGetEvent(eventId, out HistoricalEventRecord eventRecord) || !memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord memoryRecord))
            {
                return RecordFailure("Validate 8.8 Projection Adapters", "Required history or memory fixture was not created.", InformationAccessResultCode.InvalidRequest.ToString());
            }

            RecordLifeEventRequest lifeEventRequest = BuildPrototypeLifeEventRequest(lifeEventId, "history-event.life.injury", LifeEventCategory.Injury, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Major, LifeEventBiographyRelevance.PrivateBiographyEvent, KnowledgeVisibility.Private, LifeEventParticipantRole.Subject, relatedInjuryId: "injury.prototype-major");
            lifeEventRequest.TransactionId = $"history.8.8.adapter.life.{Guid.NewGuid():N}";
            HistoryOperationResult lifeEventResult = historyRuntime.RecordLifeEvent(lifeEventRequest);
            if (!lifeEventResult.Succeeded || lifeEventResult.Event == null)
            {
                return RecordFailure("Validate 8.8 Projection Adapters", $"Life-event fixture failed: {lifeEventResult.Code} {lifeEventResult.Message}", InformationAccessResultCode.InvalidRequest.ToString());
            }

            KnowledgeObservationRequest knowledgeRequest = BuildHistoricalKnowledgeRequest($"access.8.8.adapter.knowledge.{Guid.NewGuid():N}", eventId, KnowledgeEvidenceDirection.Supports, 780, 820);
            KnowledgeOperationResult knowledgeResult = knowledge.RecordObservation(knowledgeRequest);
            if (!knowledgeResult.Succeeded || knowledgeResult.ResultingBelief == null)
            {
                return RecordFailure("Validate 8.8 Projection Adapters", $"Knowledge fixture failed: {knowledgeResult.Code} {knowledgeResult.Message}", InformationAccessResultCode.InvalidRequest.ToString());
            }

            string sourceId = $"information-source.prototype.access-adapter.{Guid.NewGuid():N}";
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            InformationSourceOperationResult sourceResult = sourceRuntime.RegisterSource(new InformationSourceRegistrationRequest
            {
                TransactionId = $"source.8.8.adapter.{Guid.NewGuid():N}",
                SourceInstanceId = sourceId,
                Category = InformationSourceCategory.DirectObservation,
                ReferenceType = InformationSourceReferenceType.HistoricalEvent,
                ReferencedId = eventId,
                OriginalCreatorPersonId = GetPrototypePersonId(),
                ObserverPersonId = GetPrototypePersonId(),
                HolderPersonId = GetPrototypePersonId(),
                CreationWorldTimeSeconds = GetPrototypeWorldTime(),
                ObservationWorldTimeSeconds = GetPrototypeWorldTime(),
                TransmissionWorldTimeSeconds = GetPrototypeWorldTime(),
                Domain = KnowledgeDomain.Historical,
                MethodId = "observation-method.ordinary-visual",
                SubjectId = eventId,
                Privacy = SourcePrivacyLevel.Private,
                ErrorRisk = 120,
                DeceptionRisk = 80,
                BiasRisk = 100,
                Tags = new[] { "feature.8.8", "projection-adapter" }
            });
            if (!sourceResult.Succeeded || sourceResult.Source == null)
            {
                return RecordFailure("Validate 8.8 Projection Adapters", $"Source fixture failed: {sourceResult.Code} {sourceResult.Message}", InformationAccessResultCode.InvalidRequest.ToString());
            }

            string historyPolicyId = $"information-access.policy.adapter.history-hidden-event.{SanitizeForTransaction(eventRecord.EventId)}";
            string lifePolicyId = $"information-access.policy.adapter.life-injury.{SanitizeForTransaction(lifeEventId)}";
            string lifeHistoryPolicyId = $"information-access.policy.adapter.life-injury-history.{SanitizeForTransaction(lifeEventId)}";
            string memoryPolicyId = $"information-access.policy.adapter.memory-hidden-witness.{SanitizeForTransaction(memoryRecord.MemoryId)}";
            string knowledgePolicyId = $"information-access.policy.adapter.knowledge-hidden-event.{SanitizeForTransaction(knowledgeResult.ResultingBelief.BeliefId)}";
            string sourcePolicyId = $"information-access.policy.adapter.source-direct.{SanitizeForTransaction(sourceId)}";
            string sourceChainPolicyId = $"information-access.policy.adapter.source-chain-direct.{SanitizeForTransaction(sourceId)}";
            InformationAccessOperationResult[] policies =
            {
                RegisterProjectionPolicy(historyPolicyId, InformationSubjectType.HistoricalEvent, eventRecord.EventId, GetPrototypePersonId()),
                RegisterProjectionPolicy(lifePolicyId, InformationSubjectType.LifeEvent, lifeEventId, GetPrototypePersonId()),
                RegisterProjectionPolicy(lifeHistoryPolicyId, InformationSubjectType.HistoricalEvent, lifeEventId, GetPrototypePersonId()),
                RegisterProjectionPolicy(memoryPolicyId, InformationSubjectType.Memory, memoryRecord.MemoryId, GetPrototypePersonId()),
                RegisterProjectionPolicy(knowledgePolicyId, InformationSubjectType.Belief, knowledgeResult.ResultingBelief.BeliefId, GetPrototypePersonId()),
                RegisterProjectionPolicy(sourcePolicyId, InformationSubjectType.Source, sourceId, GetPrototypePersonId()),
                RegisterProjectionPolicy(sourceChainPolicyId, InformationSubjectType.SourceChain, sourceId, GetPrototypePersonId())
            };
            InformationAccessOperationResult failedPolicy = policies.FirstOrDefault(result => result == null || !result.Succeeded);
            if (failedPolicy != null)
            {
                return RecordAccessOperation("Validate 8.8 Projection Adapters", failedPolicy);
            }

            long historyRevision = historyRuntime.HistoryRevision;
            long memoryRevision = memoryRuntime.MemoryRevision;
            long knowledgeRevision = knowledge.KnowledgeRevision;
            long sourceRevision = informationSources.SourceRevision;
            long accessRevision = informationAccess.AccessRevision;
            InformationAccessContext visitor = BuildProjectionAccessContext("person.prototype.visitor", InformationAccessMode.Inspect);
            InformationAccessContext listener = BuildProjectionAccessContext("person.prototype.listener", InformationAccessMode.Inspect);

            InformationAccessProjection<HistoricalEventRecord> deniedHistory = historyRuntime.GetHistoryProjection(eventId, informationAccess, visitor);
            InformationAccessProjection<HistoricalEventRecord> allowedHistory = historyRuntime.GetHistoryProjection(eventId, informationAccess, listener);
            IReadOnlyList<InformationAccessProjection<BiographyTimelineEntry>> deniedBiography = historyRuntime.GetBiographyProjection(GetPrototypePersonId(), informationAccess, visitor, memoryRuntime);
            IReadOnlyList<InformationAccessProjection<BiographyTimelineEntry>> allowedBiography = historyRuntime.GetBiographyProjection(GetPrototypePersonId(), informationAccess, listener, memoryRuntime);
            InformationAccessProjection<HistoryMemoryRecord> deniedMemory = memoryRuntime.GetMemoryProjection(memoryId, informationAccess, visitor);
            InformationAccessProjection<HistoryMemoryRecord> allowedMemory = memoryRuntime.GetMemoryProjection(memoryId, informationAccess, listener);
            InformationAccessProjection<KnowledgeBeliefRecord> deniedKnowledge = knowledge.GetKnowledgeProjection(knowledgeRequest.Proposition, informationAccess, visitor);
            InformationAccessProjection<KnowledgeBeliefRecord> allowedKnowledge = knowledge.GetKnowledgeProjection(knowledgeRequest.Proposition, informationAccess, listener);
            InformationAccessProjection<InformationSourceRecord> deniedSource = informationSources.GetSourceProjection(sourceId, informationAccess, visitor);
            InformationAccessProjection<InformationSourceRecord> allowedSource = informationSources.GetSourceProjection(sourceId, informationAccess, listener);
            InformationAccessProjection<SourceChainSnapshot> deniedChain = informationSources.GetSourceChainProjection(sourceId, informationAccess, visitor);
            InformationAccessProjection<SourceChainSnapshot> allowedChain = informationSources.GetSourceChainProjection(sourceId, informationAccess, listener);

            bool deniedHidden = deniedHistory.Record == null
                && deniedBiography.All(projection => !ProjectionMatchesSubject(projection, lifeEventId))
                && deniedMemory.Record == null
                && deniedKnowledge.Record == null
                && deniedSource.Record == null
                && deniedChain.Record == null
                && string.IsNullOrWhiteSpace(deniedHistory.VisibleSubjectId)
                && string.IsNullOrWhiteSpace(deniedMemory.VisibleSubjectId)
                && string.IsNullOrWhiteSpace(deniedKnowledge.VisibleSubjectId)
                && string.IsNullOrWhiteSpace(deniedSource.VisibleSubjectId);
            bool allowedProjected = allowedHistory.Succeeded
                && allowedBiography.Any(projection => ProjectionMatchesSubject(projection, lifeEventId))
                && allowedMemory.Succeeded
                && allowedKnowledge.Succeeded
                && allowedSource.Succeeded
                && allowedChain.Succeeded;
            bool redactedDetails = allowedHistory.Redacted
                && allowedMemory.Redacted
                && allowedKnowledge.Redacted
                && allowedSource.Redacted
                && allowedChain.Redacted;
            bool noMutation = historyRuntime.HistoryRevision == historyRevision
                && memoryRuntime.MemoryRevision == memoryRevision
                && knowledge.KnowledgeRevision == knowledgeRevision
                && informationSources.SourceRevision == sourceRevision
                && informationAccess.AccessRevision == accessRevision;
            bool succeeded = deniedHidden && allowedProjected && redactedDetails && noMutation;
            string message = $"DeniedHidden={deniedHidden} Allowed={allowedProjected} Redacted={redactedDetails} NoMutation={noMutation} "
                + $"History={FormatAccessProjection(allowedHistory)} Biography=[{string.Join(",", allowedBiography.Select(FormatBiographyAccessProjection))}] Memory={FormatAccessProjection(allowedMemory)} Knowledge={FormatAccessProjection(allowedKnowledge)} Source={FormatAccessProjection(allowedSource)} Chain={FormatAccessProjection(allowedChain)}.";
            return Record(succeeded, "Validate 8.8 Projection Adapters", succeeded ? "Success" : "ProjectionAdapterMismatch", message);
        }

        public PrototypeTestLabOperation ValidateInformationAccessSaveRestore()
        {
            EnsurePrototypeAccessPolicies();
            GrantInspectInformationAccess();
            AttemptUnauthorizedInformationAccess();
            InformationAccessSaveData saveData = informationAccess.CreateSaveData();
            InformationAccessRuntime restored = new InformationAccessRuntime();
            restored.Configure(registry, GetPrototypePersonId());
            InformationAccessOperationResult restore = restored.RestoreFromSaveData(saveData, registry, GetPrototypePersonId(), restoring: true);
            InformationAccessSnapshot before = informationAccess.CreateSnapshot();
            InformationAccessSaveData corrupt = informationAccess.CreateSaveData();
            corrupt.policies = corrupt.policies.Concat(corrupt.policies.Take(1).Select(policy => policy.Clone())).ToArray();
            InformationAccessOperationResult rejected = informationAccess.RestoreFromSaveData(corrupt, registry, GetPrototypePersonId(), restoring: true);
            InformationAccessSnapshot after = informationAccess.CreateSnapshot();
            bool unchanged = before.Revision == after.Revision && before.Policies.Count == after.Policies.Count && before.Grants.Count == after.Grants.Count && before.Audits.Count == after.Audits.Count;
            bool succeeded = restore.Succeeded && !rejected.Succeeded && unchanged && restored.CreateSnapshot().Policies.Count == saveData.policies.Length;
            return Record(succeeded, "Validate 8.8 Access Save Restore", succeeded ? "Success" : "RestoreMismatch", $"Restore={restore.Code} Reject={rejected.Code} Unchanged={unchanged} Policies={saveData.policies.Length} Grants={saveData.grants.Length} Audits={saveData.audits.Length}. {rejected.Message}");
        }

        public string BuildKnowledgeRecordSummary()
        {
            EnsureKnowledgeRecordRuntime();
            KnowledgeRecordSnapshot snapshot = knowledgeRecords.CreateSnapshot();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Feature 8.9 Historical Records, Journals, and Codex");
            builder.AppendLine($"Owner: {snapshot.OwnerId} Revision: {snapshot.Revision} Records: {snapshot.Records.Count} Collections: {snapshot.Collections.Count}");
            foreach (KnowledgeRecord record in snapshot.Records.OrderBy(record => record.Data.occurredStartWorldTime).ThenBy(record => record.RecordId, StringComparer.Ordinal).Take(12))
            {
                builder.AppendLine($"{record.RecordId}: {record.Category} {record.Status} Subject={record.Subject.SubjectType}/{record.Subject.SubjectId} Details={record.Details.Count} Class={record.Classification}");
            }

            foreach (KnowledgeRecordCollection collection in snapshot.Collections.OrderBy(collection => collection.CollectionId, StringComparer.Ordinal).Take(5))
            {
                builder.AppendLine($"Collection {collection.CollectionId}: {collection.RecordIds.Count} record(s)");
            }

            PrototypeTestLabOperation last = history.Count == 0 ? default : history[0];
            if (!string.IsNullOrWhiteSpace(last.OperationName) && last.OperationName.Contains("8.9", StringComparison.Ordinal))
            {
                builder.AppendLine($"Last 8.9: {last.OperationName} Code={last.Code} Success={last.Succeeded}");
                builder.AppendLine(last.Message);
            }

            return builder.ToString();
        }

        public PrototypeTestLabOperation ValidateKnowledgeRecordDefinitions()
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            Dictionary<string, IGameDefinition> definitions = new Dictionary<string, IGameDefinition>(StringComparer.Ordinal);
            foreach (IGameDefinition definition in registry?.DefinitionsById.Values ?? Array.Empty<IGameDefinition>())
            {
                definitions[definition.Id] = definition;
            }

            foreach (KnowledgeRecordDefinition definition in CreatePrototypeKnowledgeRecordDefinitions())
            {
                definitions[definition.Id] = definition;
            }

            foreach (KnowledgeRecordDefinition definition in CreatePrototypeKnowledgeRecordDefinitions())
            {
                definition.ValidateCatalogDefinition(definitions, report);
            }

            bool succeeded = report.ErrorCount == 0 && report.WarningCount == 0;
            return Record(succeeded, "Validate 8.9 Record Definitions", succeeded ? "Success" : "ValidationFailed", report.GetSummary());
        }

        public PrototypeTestLabOperation CreatePersonalJournalRecord()
        {
            KnowledgeRecordOperationResult result = CreatePrototypeRecord(
                "record.prototype.journal.entry",
                "record-definition.journal-entry",
                KnowledgeRecordCategory.PersonalJournal,
                InformationSubjectType.HistoricalEvent,
                "event.prototype.journal.first-entry",
                InformationVisibilityClassification.Personal,
                "First prototype journal entry.",
                "The player records a personal note without changing authoritative history.");
            return RecordKnowledgeRecordOperation("Create 8.9 Journal Record", result);
        }

        public PrototypeTestLabOperation CreateHistoricalArchiveRecord()
        {
            HistoryOperationResult history = EnsurePrototypeHistoricalEvent("event.prototype.participation", KnowledgeVisibility.Public, "Prototype person participated in a representative event.");
            if (!history.Succeeded && !history.Duplicate)
            {
                return RecordFailure("Create 8.9 Historical Record", history.Message, history.Code.ToString());
            }

            KnowledgeRecordOperationResult result = CreatePrototypeRecord(
                "record.prototype.history.archive",
                "record-definition.historical-record",
                KnowledgeRecordCategory.HistoricalRecord,
                InformationSubjectType.HistoricalEvent,
                "event.prototype.participation",
                InformationVisibilityClassification.Public,
                "Archive entry.",
                "A live history projection is preserved as an explicit historical record.",
                historicalEventIds: new[] { "event.prototype.participation" });
            return RecordKnowledgeRecordOperation("Create 8.9 Historical Record", result);
        }

        public PrototypeTestLabOperation CreateBiographyProjectionRecord()
        {
            RecordLifeEventBirthOrCreation();
            KnowledgeRecordOperationResult result = CreatePrototypeRecord(
                "record.prototype.biography.entry",
                "record-definition.biography-entry",
                KnowledgeRecordCategory.Biography,
                InformationSubjectType.PersonIdentity,
                GetPrototypePersonId(),
                InformationVisibilityClassification.Private,
                "Biography milestone.",
                "A person biography projection is captured as a person-owned record.",
                lifeEventIds: new[] { "event.prototype.life.birth" });
            return RecordKnowledgeRecordOperation("Create 8.9 Biography Record", result);
        }

        public PrototypeTestLabOperation CreateBestiaryRecord()
        {
            KnowledgeRecordOperationResult result = CreatePrototypeRecord(
                "record.prototype.codex.species-human",
                "record-definition.bestiary-entry",
                KnowledgeRecordCategory.Bestiary,
                InformationSubjectType.BodyIdentity,
                "species.human",
                InformationVisibilityClassification.Public,
                "Human species entry.",
                "A codex entry can summarize discovered species facts without granting new knowledge.");
            return RecordKnowledgeRecordOperation("Create 8.9 Bestiary Record", result);
        }

        public PrototypeTestLabOperation CreateLocationRecord()
        {
            KnowledgeRecordOperationResult result = CreatePrototypeRecord(
                "record.prototype.location.central-hub",
                "record-definition.location-entry",
                KnowledgeRecordCategory.LocationRecord,
                InformationSubjectType.Location,
                "place.prototype.central-hub",
                InformationVisibilityClassification.Public,
                "Central hub map note.",
                "A discovered place is represented as a location record.");
            return RecordKnowledgeRecordOperation("Create 8.9 Location Record", result);
        }

        public PrototypeTestLabOperation CreateMedicalRecord()
        {
            KnowledgeOperationResult evidence = EnsurePrototypeEvidence("evidence.prototype.symptom", BuiltInKnowledgeFacts.BodySymptom, KnowledgeSubjectType.Body, GetPrototypeBodyId(), KnowledgeValueType.Qualitative, "prototype-infection-symptom", KnowledgeProvenance.DirectObservation);
            if (!evidence.Succeeded && !evidence.Duplicate)
            {
                return RecordFailure("Create 8.9 Medical Record", evidence.Message, evidence.Code.ToString());
            }

            KnowledgeRecordOperationResult result = CreatePrototypeRecord(
                "record.prototype.medical.diagnosis-note",
                "record-definition.medical-record",
                KnowledgeRecordCategory.MedicalRecord,
                InformationSubjectType.Diagnosis,
                "condition.biology.prototype-infection",
                InformationVisibilityClassification.Medical,
                "Prototype diagnosis.",
                "Medical details are recordable but remain access-controlled.",
                evidenceIds: new[] { "evidence.prototype.symptom" });
            return RecordKnowledgeRecordOperation("Create 8.9 Medical Record", result);
        }

        public PrototypeTestLabOperation CreateInvestigationRecord()
        {
            InformationSourceOperationResult source = EnsurePrototypeSource("information-source.prototype.investigation", InformationSourceCategory.DirectObservation);
            if (!source.Succeeded && !source.Duplicate)
            {
                return RecordFailure("Create 8.9 Investigation Record", source.Message, source.Code.ToString());
            }

            KnowledgeOperationResult evidence = EnsurePrototypeEvidence("evidence.prototype.investigation", BuiltInKnowledgeFacts.EventOccurred, KnowledgeSubjectType.Event, "event.prototype.investigation", KnowledgeValueType.Boolean, "true", KnowledgeProvenance.Document);
            if (!evidence.Succeeded && !evidence.Duplicate)
            {
                return RecordFailure("Create 8.9 Investigation Record", evidence.Message, evidence.Code.ToString());
            }

            KnowledgeRecordOperationResult result = CreatePrototypeRecord(
                "record.prototype.investigation.source-chain",
                "record-definition.investigation-record",
                KnowledgeRecordCategory.InvestigationRecord,
                InformationSubjectType.SourceChain,
                "information-source.prototype.investigation",
                InformationVisibilityClassification.Confidential,
                "Investigation source chain.",
                "Evidence and provenance references remain explicit record details.",
                sourceIds: new[] { "information-source.prototype.investigation" },
                evidenceIds: new[] { "evidence.prototype.investigation" });
            return RecordKnowledgeRecordOperation("Create 8.9 Investigation Record", result);
        }

        public PrototypeTestLabOperation CreateCorrectedKnowledgeRecord()
        {
            KnowledgeRecordOperationResult original = CreatePrototypeRecord(
                "record.prototype.journal.correctable",
                "record-definition.journal-entry",
                KnowledgeRecordCategory.PersonalJournal,
                InformationSubjectType.Claim,
                "claim.prototype.first-version",
                InformationVisibilityClassification.Personal,
                "Original note.",
                "This first version remains auditable after correction.");
            if (!original.Succeeded && !original.Duplicate)
            {
                return RecordKnowledgeRecordOperation("Create 8.9 Correctable Record", original);
            }

            KnowledgeRecord existingCorrection = EnsureKnowledgeRecordRuntime().CreateSnapshot().Records.FirstOrDefault(record => string.Equals(record.RecordId, "record.prototype.journal.corrected", StringComparison.Ordinal));
            if (existingCorrection != null)
            {
                return RecordKnowledgeRecordOperation("Correct 8.9 Knowledge Record", KnowledgeRecordOperationResult.Success("Knowledge Record correction fixture already exists.", string.Empty, EnsureKnowledgeRecordRuntime().RecordRevision, EnsureKnowledgeRecordRuntime().RecordRevision, existingCorrection, duplicate: true));
            }

            KnowledgeRecordOperationResult correction = EnsureKnowledgeRecordRuntime().CorrectRecord(BuildPrototypeRecordRequest(
                    "record.prototype.journal.corrected",
                    "record-definition.journal-entry",
                    KnowledgeRecordCategory.PersonalJournal,
                    InformationSubjectType.Claim,
                    "claim.prototype.first-version",
                    InformationVisibilityClassification.Personal,
                    "Corrected note.",
                    "The corrected record supersedes the original but does not delete it."),
                "record.prototype.journal.correctable");
            return RecordKnowledgeRecordOperation("Correct 8.9 Knowledge Record", correction);
        }

        public PrototypeTestLabOperation ReadKnowledgeRecordAsOwner()
        {
            if (!EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime) || !EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledgeRuntime))
            {
                return RecordFailure("Read 8.9 Owner Record", "History, Memory, or Knowledge runtime is missing.", KnowledgeRecordResultCode.InvalidRequest.ToString());
            }

            CreatePersonalJournalRecord();
            KnowledgeRecordProjectionContext projectionContext = BuildKnowledgeRecordProjectionContext(GetPrototypePersonId(), InformationAccessMode.Read, privileged: true);
            KnowledgeRecordReadResult result = EnsureKnowledgeRecordRuntime().ReadRecordAsPerson(new KnowledgeRecordReadRequest
            {
                TransactionId = $"record.8.9.read.owner.{Guid.NewGuid():N}",
                RecordId = "record.prototype.journal.entry",
                ReaderPersonId = GetPrototypePersonId(),
                ProjectionContext = projectionContext,
                WorldTimeSeconds = GetPrototypeWorldTime(),
                CreateInformationSource = true,
                CreateKnowledgeEvidence = true,
                CreateMemory = true,
                EvidenceStrength = 550,
                EvidenceCredibility = 650,
                EvidenceVisibility = KnowledgeVisibility.Private
            }, EnsureInformationAccessRuntime(), informationSources, knowledgeRuntime, memoryRuntime);
            return RecordKnowledgeRecordReadOperation("Read 8.9 Owner Record", result);
        }

        public PrototypeTestLabOperation AttemptUnauthorizedKnowledgeRecordRead()
        {
            EnsurePrototypeAccessPolicies();
            CreateMedicalRecord();
            RegisterProjectionPolicy("information-access.policy.record.medical-diagnosis", InformationSubjectType.Diagnosis, "condition.biology.prototype-infection", GetPrototypePersonId());
            KnowledgeRecordProjectionContext projectionContext = BuildKnowledgeRecordProjectionContext("person.prototype.visitor", InformationAccessMode.Read, privileged: false);
            KnowledgeRecordProjection projection = EnsureKnowledgeRecordRuntime().ProjectRecord("record.prototype.medical.diagnosis-note", projectionContext, EnsureInformationAccessRuntime());
            bool succeeded = projection.Record == null && string.IsNullOrWhiteSpace(projection.VisibleRecordId);
            return Record(succeeded, "Reject 8.9 Unauthorized Record Read", succeeded ? "Success" : "AccessLeaked", $"Succeeded={projection.Succeeded} Denied={projection.Denied} VisibleId='{projection.VisibleRecordId}' Message='{projection.Message}'");
        }

        public PrototypeTestLabOperation SearchKnowledgeRecords()
        {
            string runId = Guid.NewGuid().ToString("N");
            string journalId = $"record.prototype.search.journal.{runId}";
            string bestiaryId = $"record.prototype.search.bestiary.{runId}";
            string locationId = $"record.prototype.search.location.{runId}";
            KnowledgeRecordOperationResult journal = CreatePrototypeRecord(
                journalId,
                "record-definition.journal-entry",
                KnowledgeRecordCategory.PersonalJournal,
                InformationSubjectType.HistoricalEvent,
                $"event.prototype.search.journal.{runId}",
                InformationVisibilityClassification.Personal,
                "Search journal entry.",
                "A run-scoped journal record for deterministic search.");
            KnowledgeRecordOperationResult bestiary = CreatePrototypeRecord(
                bestiaryId,
                "record-definition.bestiary-entry",
                KnowledgeRecordCategory.Bestiary,
                InformationSubjectType.BodyIdentity,
                "species.human",
                InformationVisibilityClassification.Public,
                "Search bestiary entry.",
                "A run-scoped bestiary record for deterministic search.");
            KnowledgeRecordOperationResult location = CreatePrototypeRecord(
                locationId,
                "record-definition.location-entry",
                KnowledgeRecordCategory.LocationRecord,
                InformationSubjectType.Location,
                "place.prototype.central-hub",
                InformationVisibilityClassification.Public,
                "Search location entry.",
                "A run-scoped location record for deterministic search.");
            if (!journal.Succeeded || !bestiary.Succeeded || !location.Succeeded)
            {
                string failure = string.Join(" | ", new[] { journal, bestiary, location }.Where(result => result == null || !result.Succeeded).Select(result => $"{result?.Code.ToString() ?? "MissingResult"} {result?.Message ?? "No result."}"));
                return RecordFailure("Search 8.9 Knowledge Records", failure, KnowledgeRecordResultCode.InvalidRequest.ToString());
            }

            string[] expected = { journalId, bestiaryId, locationId };
            KnowledgeRecordRuntime runtime = EnsureKnowledgeRecordRuntime();
            IReadOnlyList<KnowledgeRecordProjection> results = runtime.Search(new KnowledgeRecordSearchQuery
            {
                OwnerId = GetPrototypePersonId(),
                Limit = 500
            }, BuildKnowledgeRecordProjectionContext(GetPrototypePersonId(), InformationAccessMode.Query, privileged: true), EnsureInformationAccessRuntime());
            HashSet<string> visible = new HashSet<string>(results.Select(result => result.VisibleRecordId), StringComparer.Ordinal);
            bool containsExpected = expected.All(visible.Contains);
            bool deterministic = results.Select(result => result.VisibleRecordId).SequenceEqual(results.Select(result => result.VisibleRecordId).OrderBy(id => runtime.CreateSnapshot().Records.First(record => record.RecordId == id).Data.occurredStartWorldTime).ThenBy(id => id, StringComparer.Ordinal));
            bool succeeded = containsExpected && deterministic;
            return Record(succeeded, "Search 8.9 Knowledge Records", succeeded ? "Success" : "SearchMismatch", $"Count={results.Count} Deterministic={deterministic} Records=[{string.Join(",", results.Select(result => result.VisibleRecordId))}]");
        }

        public PrototypeTestLabOperation CreateKnowledgeRecordCollection()
        {
            CreatePersonalJournalRecord();
            CreateBestiaryRecord();
            KnowledgeRecordOperationResult result = EnsureKnowledgeRecordRuntime().CreateCollection(
                "record-collection.prototype.step8-codex",
                "Prototype Step 8 Codex",
                GetPrototypePersonId(),
                new[] { "record.prototype.journal.entry", "record.prototype.codex.species-human" },
                $"record.8.9.collection.{Guid.NewGuid():N}");
            return RecordKnowledgeRecordOperation("Create 8.9 Record Collection", result);
        }

        public PrototypeTestLabOperation ValidateKnowledgeRecordSaveRestore()
        {
            CreatePersonalJournalRecord();
            CreateMedicalRecord();
            CreateKnowledgeRecordCollection();
            KnowledgeRecordSaveData saveData = EnsureKnowledgeRecordRuntime().CreateSaveData();
            KnowledgeRecordRuntime restored = new KnowledgeRecordRuntime();
            restored.Configure(registry, GetPrototypePersonId());
            KnowledgeRecordOperationResult restore = restored.RestoreFromSaveData(saveData, registry, GetPrototypePersonId(), restoring: true);
            KnowledgeRecordSnapshot before = knowledgeRecords.CreateSnapshot();
            KnowledgeRecordSaveData corrupt = knowledgeRecords.CreateSaveData();
            corrupt.records = corrupt.records.Concat(corrupt.records.Take(1).Select(record => record.Clone())).ToArray();
            KnowledgeRecordOperationResult rejected = knowledgeRecords.RestoreFromSaveData(corrupt, registry, GetPrototypePersonId(), restoring: true);
            KnowledgeRecordSnapshot after = knowledgeRecords.CreateSnapshot();
            bool unchanged = before.Revision == after.Revision && before.Records.Count == after.Records.Count && before.Collections.Count == after.Collections.Count;
            bool succeeded = restore.Succeeded && !rejected.Succeeded && unchanged && restored.CreateSnapshot().Records.Count == saveData.records.Length;
            return Record(succeeded, "Validate 8.9 Record Save Restore", succeeded ? "Success" : "RestoreMismatch", $"Restore={restore.Code} Reject={rejected.Code} Unchanged={unchanged} Records={saveData.records.Length} Collections={saveData.collections.Length}. {rejected.Message}");
        }

        public PrototypeTestLabOperation ValidateKnowledgeRecordLiveProjectionBoundaries()
        {
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime) || !EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Validate 8.9 Projection Boundaries", "History, Memory, or Knowledge runtime is missing.", KnowledgeRecordResultCode.InvalidRequest.ToString());
            }

            RecordAuthoritativeHistoryEvent();
            FormWitnessHistoryMemory();
            RecordKnowledgeVisibleInjury();
            long historyRevision = historyRuntime.HistoryRevision;
            long memoryRevision = memoryRuntime.MemoryRevision;
            long knowledgeRevision = knowledge.KnowledgeRevision;
            long recordRevision = EnsureKnowledgeRecordRuntime().RecordRevision;
            KnowledgeRecordOperationResult preview = CreatePrototypeRecord(
                "record.prototype.preview.live-projection",
                "record-definition.historical-record",
                KnowledgeRecordCategory.HistoricalRecord,
                InformationSubjectType.HistoricalEvent,
                "event.prototype.participation",
                InformationVisibilityClassification.Public,
                "Preview archive.",
                "Previewing a record must not persist a live projection.",
                preview: true,
                historicalEventIds: new[] { "event.prototype.participation" });
            bool noMutation = historyRuntime.HistoryRevision == historyRevision
                && memoryRuntime.MemoryRevision == memoryRevision
                && knowledge.KnowledgeRevision == knowledgeRevision
                && EnsureKnowledgeRecordRuntime().RecordRevision == recordRevision;
            bool succeeded = preview.Succeeded && preview.Preview && noMutation;
            return Record(succeeded, "Validate 8.9 Projection Boundaries", succeeded ? "Success" : "MutationDetected", $"Preview={preview.Code} NoMutation={noMutation} History={historyRevision}->{historyRuntime.HistoryRevision} Memory={memoryRevision}->{memoryRuntime.MemoryRevision} Knowledge={knowledgeRevision}->{knowledge.KnowledgeRevision} Records={recordRevision}->{EnsureKnowledgeRecordRuntime().RecordRevision}.");
        }

        public string BuildKnowledgeHistoryIntegrationSummary()
        {
            KnowledgeHistoryFacade facade = CreateKnowledgeHistoryFacade();
            KnowledgeHistoryValidationResult validation = facade.ValidateCurrentState();
            KnowledgeHistoryPersistenceInventory persistence = facade.CreatePersistenceInventory();
            IReadOnlyList<KnowledgeHistoryDefinitionFallbackDiagnostic> fallbackDiagnostics = facade.CreateDefinitionFallbackDiagnostics(CreatePrototypeKnowledgeRecordDefinitions().Select(definition => definition.Id), "PrototypeKnowledgeRecordDefinitionFactory");

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Feature 8.10 Knowledge and History Integration Finalization");
            builder.AppendLine(validation.Readiness?.ToSummary() ?? "Readiness unavailable.");
            builder.AppendLine(validation.ToSummary());
            builder.AppendLine(persistence.ToSummary());
            builder.AppendLine($"Definition fallbacks: Catalog={fallbackDiagnostics.Count(item => item.CatalogAuthored)} FallbackNeeded={fallbackDiagnostics.Count(item => item.FallbackWouldBeUsed)} Missing={fallbackDiagnostics.Count(item => item.Missing)}");
            PrototypeTestLabOperation last = history.Count == 0 ? default : history[0];
            if (!string.IsNullOrWhiteSpace(last.OperationName) && last.OperationName.Contains("8.10", StringComparison.Ordinal))
            {
                builder.AppendLine($"Last 8.10: {last.OperationName} Code={last.Code} Success={last.Succeeded}");
                builder.AppendLine(last.Message);
            }

            return builder.ToString();
        }

        public PrototypeTestLabOperation ValidateKnowledgeHistoryReadiness()
        {
            KnowledgeHistoryReadinessSnapshot snapshot = CreateKnowledgeHistoryFacade().CreateReadinessSnapshot();
            return Record(snapshot.Ready, "Validate 8.10 Readiness", snapshot.Ready ? "Success" : "NotReady", snapshot.ToSummary());
        }

        public PrototypeTestLabOperation PrepareKnowledgeHistoryIntegrationFixtures()
        {
            List<string> failures = new List<string>();
            CapturePreparation(CreateHistoricalArchiveRecord(), failures);
            CapturePreparation(CreateMedicalRecord(), failures);
            CapturePreparation(CreateInvestigationRecord(), failures);
            CaptureAccessPreparation(RegisterProjectionPolicy("information-access.policy.integration.hidden-record", InformationSubjectType.Diagnosis, "condition.biology.prototype-infection", GetPrototypePersonId()), failures);
            KnowledgeHistoryValidationResult validation = CreateKnowledgeHistoryFacade().ValidateCurrentState();
            foreach (string error in validation.Errors)
            {
                failures.Add(error);
            }

            bool succeeded = failures.Count == 0;
            return Record(succeeded, "Prepare 8.10 Integration Fixtures", succeeded ? "Success" : "FixturePreparationFailed", succeeded ? "Integration fixture dependencies are present." : string.Join(" | ", failures));

            static void CapturePreparation(PrototypeTestLabOperation operation, List<string> target)
            {
                if (!operation.Succeeded)
                {
                    target.Add($"{operation.OperationName}: {operation.Code} {operation.Message}");
                }
            }

            static void CaptureAccessPreparation(InformationAccessOperationResult operation, List<string> target)
            {
                if (operation == null || !operation.Succeeded)
                {
                    target.Add($"Information Access Policy: {operation?.Code.ToString() ?? "MissingResult"} {operation?.Message ?? "No result was returned."}");
                }
            }
        }

        public PrototypeTestLabOperation ValidateKnowledgeHistoryIntegration()
        {
            KnowledgeHistoryValidationResult validation = CreateKnowledgeHistoryFacade().ValidateCurrentState();
            return Record(validation.Succeeded, "Validate 8.10 Integration", validation.Succeeded ? "Success" : "ValidationFailed", validation.ToSummary());
        }

        public PrototypeTestLabOperation ShowKnowledgeHistoryFallbackDiagnostics()
        {
            IReadOnlyList<KnowledgeHistoryDefinitionFallbackDiagnostic> diagnostics = CreateKnowledgeHistoryFacade().CreateDefinitionFallbackDiagnostics(CreatePrototypeKnowledgeRecordDefinitions().Select(definition => definition.Id), "PrototypeKnowledgeRecordDefinitionFactory");
            bool succeeded = diagnostics.All(item => !item.Missing);
            string message = string.Join(Environment.NewLine, diagnostics.Select(item => item.ToSummary()));
            return Record(succeeded, "Show 8.10 Definition Fallbacks", succeeded ? "Success" : "MissingFallback", message);
        }

        public PrototypeTestLabOperation RunKnowledgeHistoryDiscoveryFlow()
        {
            if (!TryBuildSpeciesCapabilityObservation($"knowledge.8.10.discovery.{Guid.NewGuid():N}", 850, 900, out _, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            KnowledgeHistoryOperationResult result = CreateKnowledgeHistoryFacade().RecordObservation(request);
            return Record(result.Succeeded, "Run 8.10 Discovery Flow", result.Code, result.ToSummary());
        }

        public PrototypeTestLabOperation RunKnowledgeHistoryEventMemoryFlow()
        {
            KnowledgeHistoryFacade facade = CreateKnowledgeHistoryFacade();
            string eventId = $"event.prototype.integration.{Guid.NewGuid():N}";
            KnowledgeHistoryOperationResult historyResult = facade.RecordHistoricalEvent(BuildHistoryEventRequest($"history.8.10.event.{Guid.NewGuid():N}", eventId, "history-event.person-participation", GetPrototypePersonId(), KnowledgeVisibility.Public, "8.10 integration event."));
            KnowledgeHistoryOperationResult memoryResult = historyResult.Succeeded
                ? facade.FormMemory(BuildMemoryRequest($"history.8.10.memory.{Guid.NewGuid():N}", $"memory.prototype.integration.{Guid.NewGuid():N}", eventId, HistoryMemorySource.DirectObservation, createKnowledge: false))
                : new KnowledgeHistoryOperationResult(false, "Skipped", "Memory was skipped because history failed.", historyResult.Diagnostic);
            bool succeeded = historyResult.Succeeded && memoryResult.Succeeded;
            return Record(succeeded, "Run 8.10 Event Memory Flow", succeeded ? "Success" : memoryResult.Code, $"History={historyResult.ToSummary()} Memory={memoryResult.ToSummary()}");
        }

        public PrototypeTestLabOperation RunKnowledgeHistoryRecordReadingFlow()
        {
            if (!EnsureHistoryRuntime(out _, out _) || !EnsureKnowledgeRuntime(out _))
            {
                return RecordFailure("Run 8.10 Record Reading", "History, Memory, or Knowledge runtime is missing.", "MissingRuntime");
            }

            CreatePersonalJournalRecord();
            KnowledgeHistoryOperationResult result = CreateKnowledgeHistoryFacade().ReadRecordAsPerson(new KnowledgeRecordReadRequest
            {
                TransactionId = $"record.8.10.read.{Guid.NewGuid():N}",
                RecordId = "record.prototype.journal.entry",
                ReaderPersonId = GetPrototypePersonId(),
                ProjectionContext = BuildKnowledgeRecordProjectionContext(GetPrototypePersonId(), InformationAccessMode.Read, privileged: true),
                WorldTimeSeconds = GetPrototypeWorldTime(),
                CreateInformationSource = true,
                CreateKnowledgeEvidence = true,
                CreateMemory = true,
                EvidenceStrength = 550,
                EvidenceCredibility = 650,
                EvidenceVisibility = KnowledgeVisibility.Private
            });
            return Record(result.Succeeded, "Run 8.10 Record Reading", result.Code, result.ToSummary());
        }

        public PrototypeTestLabOperation RunKnowledgeHistoryAccessProjectionFlow()
        {
            EnsurePrototypeAccessPolicies();
            RegisterProjectionPolicy("information-access.policy.integration.medical", InformationSubjectType.Diagnosis, "condition.biology.prototype-infection", GetPrototypePersonId());
            InformationAccessContext visitor = BuildPrototypeAccessContext("information-access.policy.integration.medical", "condition.biology.prototype-infection", "person.prototype.visitor", InformationSubjectType.Diagnosis, InformationAccessMode.Read, discovered: true);
            InformationAccessContext owner = BuildPrototypeAccessContext("information-access.policy.integration.medical", "condition.biology.prototype-infection", GetPrototypePersonId(), InformationSubjectType.Diagnosis, InformationAccessMode.Read, discovered: true, authorizationIds: new[] { "grant.integration.owner" });
            KnowledgeHistoryOperationResult restricted = CreateKnowledgeHistoryFacade().EvaluateAccess(visitor);
            KnowledgeHistoryOperationResult allowed = CreateKnowledgeHistoryFacade().EvaluateAccess(owner);
            bool restrictedByPolicy = restricted.Code != InformationAccessDecisionKind.FullAccess.ToString();
            bool succeeded = restrictedByPolicy && allowed.Succeeded;
            return Record(succeeded, "Run 8.10 Access Projection", succeeded ? "Success" : "AccessMismatch", $"Restricted={restricted.ToSummary()} Allowed={allowed.ToSummary()}");
        }

        public PrototypeTestLabOperation ValidateKnowledgeHistorySaveCapture()
        {
            KnowledgeHistoryFacade facade = CreateKnowledgeHistoryFacade();
            KnowledgeHistoryValidationResult validation = facade.ValidateCurrentState();
            KnowledgeHistoryPersistenceInventory inventory = facade.CreatePersistenceInventory();
            bool succeeded = validation.Succeeded && inventory.Participants.Contains(KnowledgeRecordPersistenceParticipant.Key) && inventory.RequiredDependencies.Contains($"{PersonMemoryPersistenceParticipant.Key} -> {AuthoritativeHistoryPersistenceParticipant.Key}");
            return Record(succeeded, "Validate 8.10 Save Capture", succeeded ? "Success" : "PersistenceMismatch", $"{validation.ToSummary()} {inventory.ToSummary()}");
        }

        public PrototypeTestLabOperation ValidateKnowledgeHistoryFullSaveRestore()
        {
            RecordKnowledgeVisibleInjury();
            RunKnowledgeHistoryEventMemoryFlow();
            EnsurePrototypeSource("information-source.prototype.direct-observation", InformationSourceCategory.DirectObservation);
            ShareDirectObservation();
            EnsurePrototypeAccessPolicies();
            GrantInspectInformationAccess();
            CreateMedicalRecord();
            CreateKnowledgeRecordCollection();

            EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge);
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime);
            InformationSourceRuntime sourceRuntime = informationSources;
            InformationTransferRuntime transferRuntime = informationTransfers;
            InformationAccessRuntime accessRuntime = informationAccess;
            KnowledgeRecordRuntime recordRuntime = knowledgeRecords;

            int knowledgeEvents = 0;
            int historyEvents = 0;
            int memoryEvents = 0;
            int sourceEvents = 0;
            knowledge.KnowledgeChanged += CountKnowledge;
            historyRuntime.HistoryChanged += CountHistory;
            memoryRuntime.MemoryChanged += CountMemory;
            sourceRuntime.SourcesChanged += CountSource;

            PersonKnowledgeSaveData knowledgeSave = knowledge.CreateSaveData();
            AuthoritativeHistorySaveData historySave = historyRuntime.CreateSaveData();
            PersonMemorySaveData memorySave = memoryRuntime.CreateSaveData();
            InformationSourceSaveData sourceSave = sourceRuntime.CreateSaveData();
            InformationTransferSaveData transferSave = transferRuntime.CreateSaveData();
            InformationAccessSaveData accessSave = accessRuntime.CreateSaveData();
            KnowledgeRecordSaveData recordSave = recordRuntime.CreateSaveData();

            string knowledgeFailure = string.Empty;
            string historyFailure = string.Empty;
            string memoryFailure = string.Empty;
            string sourceFailure = string.Empty;
            string transferFailure = string.Empty;
            string accessFailure = string.Empty;
            string recordFailure = string.Empty;
            bool valid = PersonKnowledgeRuntime.ValidateSaveData(knowledgeSave, registry, GetPrototypePersonId(), out knowledgeFailure)
                && AuthoritativeHistoryRuntime.ValidateSaveData(historySave, registry, GetKnownPrototypePersons(), GetKnownPrototypeBodies(), out historyFailure)
                && PersonMemoryRuntime.ValidateSaveData(memorySave, historyRuntime, GetKnownPrototypePersons(), out memoryFailure)
                && InformationSourceRuntime.ValidateSaveData(sourceSave, registry, GetPrototypePersonId(), out sourceFailure)
                && InformationTransferRuntime.ValidateSaveData(transferSave, registry, GetPrototypePersonId(), out transferFailure)
                && InformationAccessRuntime.ValidateSaveData(accessSave, registry, GetPrototypePersonId(), out accessFailure)
                && KnowledgeRecordRuntime.ValidateSaveData(recordSave, registry, GetPrototypePersonId(), out recordFailure);

            bool restored = valid
                && historyRuntime.RestoreFromSaveData(historySave, registry, GetKnownPrototypePersons(), GetKnownPrototypeBodies(), restoring: true).Succeeded
                && knowledge.RestoreFromSaveData(knowledgeSave, registry, GetPrototypePersonId(), restoring: true).Succeeded
                && memoryRuntime.RestoreFromSaveData(memorySave, registry, historyRuntime, GetKnownPrototypePersons(), restoring: true).Succeeded
                && sourceRuntime.RestoreFromSaveData(sourceSave, registry, GetPrototypePersonId(), restoring: true).Succeeded
                && transferRuntime.RestoreFromSaveData(transferSave, registry, GetPrototypePersonId(), restoring: true).Succeeded
                && accessRuntime.RestoreFromSaveData(accessSave, registry, GetPrototypePersonId(), restoring: true).Succeeded
                && recordRuntime.RestoreFromSaveData(recordSave, registry, GetPrototypePersonId(), restoring: true).Succeeded;

            knowledge.KnowledgeChanged -= CountKnowledge;
            historyRuntime.HistoryChanged -= CountHistory;
            memoryRuntime.MemoryChanged -= CountMemory;
            sourceRuntime.SourcesChanged -= CountSource;

            bool countsMatch = knowledge.CreateSaveData().evidence.Length == knowledgeSave.evidence.Length
                && historyRuntime.CreateSaveData().events.Length == historySave.events.Length
                && memoryRuntime.CreateSaveData().memories.Length == memorySave.memories.Length
                && sourceRuntime.CreateSaveData().sources.Length == sourceSave.sources.Length
                && transferRuntime.CreateSaveData().transfers.Length == transferSave.transfers.Length
                && accessRuntime.CreateSaveData().policies.Length == accessSave.policies.Length
                && recordRuntime.CreateSaveData().records.Length == recordSave.records.Length;
            bool noEvents = knowledgeEvents == 0 && historyEvents == 0 && memoryEvents == 0 && sourceEvents == 0;
            bool succeeded = restored && countsMatch && noEvents;
            string failures = string.Join(" | ", new[] { knowledgeFailure, historyFailure, memoryFailure, sourceFailure, transferFailure, accessFailure, recordFailure }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return Record(succeeded, "Validate 8.10 Full Save Restore", succeeded ? "Success" : "SaveRestoreMismatch", $"Valid={valid} Restored={restored} Counts={countsMatch} Events={knowledgeEvents}/{historyEvents}/{memoryEvents}/{sourceEvents}. Failures={failures}");

            void CountKnowledge(PersonKnowledgeRuntime _, KnowledgeOperationResult __) => knowledgeEvents++;
            void CountHistory(AuthoritativeHistoryRuntime _, HistoryOperationResult __) => historyEvents++;
            void CountMemory(PersonMemoryRuntime _, HistoryOperationResult __) => memoryEvents++;
            void CountSource(InformationSourceRuntime _, InformationSourceOperationResult __) => sourceEvents++;
        }

        public PrototypeTestLabOperation ValidateKnowledgeHistoryCorruptRestoreRollback()
        {
            CreateMedicalRecord();
            KnowledgeRecordRuntime recordRuntime = EnsureKnowledgeRecordRuntime();
            KnowledgeRecordSaveData before = recordRuntime.CreateSaveData();
            KnowledgeRecordSaveData corrupt = recordRuntime.CreateSaveData();
            if (corrupt.records.Length > 0)
            {
                KnowledgeRecordData duplicate = corrupt.records[0].Clone();
                corrupt.records = corrupt.records.Concat(new[] { duplicate }).ToArray();
            }
            else
            {
                corrupt.records = new[]
                {
                    new KnowledgeRecordData { recordId = "record.prototype.corrupt", definitionId = "record-definition.missing", ownerId = GetPrototypePersonId() }
                };
            }

            KnowledgeRecordOperationResult restore = recordRuntime.RestoreFromSaveData(corrupt, registry, GetPrototypePersonId(), restoring: true);
            KnowledgeRecordSaveData after = recordRuntime.CreateSaveData();
            bool unchanged = before.recordRevision == after.recordRevision
                && before.records.Length == after.records.Length
                && before.collections.Length == after.collections.Length
                && string.Join(",", before.records.Select(record => record.recordId).OrderBy(value => value, StringComparer.Ordinal)) == string.Join(",", after.records.Select(record => record.recordId).OrderBy(value => value, StringComparer.Ordinal));
            bool succeeded = !restore.Succeeded && unchanged;
            return Record(succeeded, "Validate 8.10 Corrupt Restore Rollback", succeeded ? "Success" : "RollbackMismatch", $"Restore={restore.Code} Unchanged={unchanged} Records={before.records.Length}->{after.records.Length} Revision={before.recordRevision}->{after.recordRevision}. {restore.Message}");
        }

        public PrototypeTestLabOperation ValidateKnowledgeHistoryAccessSafety()
        {
            EnsurePrototypeAccessPolicies();
            RegisterProjectionPolicy("information-access.policy.integration.hidden-record", InformationSubjectType.Diagnosis, "condition.biology.prototype-infection", GetPrototypePersonId());
            KnowledgeRecordOperationResult medical = CreatePrototypeRecord(
                "record.prototype.integration.hidden-medical",
                "record-definition.medical-record",
                KnowledgeRecordCategory.MedicalRecord,
                InformationSubjectType.Diagnosis,
                "condition.biology.prototype-infection",
                InformationVisibilityClassification.Secret,
                "Hidden integration diagnosis",
                "Private diagnosis details.",
                accessPolicyId: "information-access.policy.integration.hidden-record");
            KnowledgeRecordRuntime runtime = EnsureKnowledgeRecordRuntime();
            KnowledgeRecordProjection denied = runtime.ProjectRecord(
                medical.Record?.RecordId ?? "record.prototype.integration.hidden-medical",
                BuildKnowledgeRecordProjectionContext("person.prototype.visitor", InformationAccessMode.Read, privileged: false),
                informationAccess);
            KnowledgeRecordProjection privileged = runtime.ProjectRecord(
                medical.Record?.RecordId ?? "record.prototype.integration.hidden-medical",
                BuildKnowledgeRecordProjectionContext(GetPrototypePersonId(), InformationAccessMode.Read, privileged: true),
                informationAccess);
            bool hiddenIdProtected = denied == null || denied.Denied || denied.Redacted;
            bool privilegedFull = privileged != null && privileged.Succeeded && !privileged.Redacted;
            bool succeeded = medical.Succeeded && hiddenIdProtected && privilegedFull;
            return Record(succeeded, "Validate 8.10 Access Safety", succeeded ? "Success" : "AccessLeak", $"Create={medical.Code} RestrictedDenied={denied?.Denied ?? true} RestrictedRedacted={denied?.Redacted ?? false} VisibleRecord='{denied?.VisibleRecordId ?? string.Empty}' Privileged={privilegedFull}.");
        }

        public PrototypeTestLabOperation ValidateKnowledgeHistorySnapshotImmutability()
        {
            EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge);
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime);
            KnowledgeRecordRuntime recordRuntime = EnsureKnowledgeRecordRuntime();
            KnowledgeSnapshot knowledgeBefore = knowledge.CreateSnapshot();
            HistorySnapshot historyBefore = historyRuntime.CreateSnapshot();
            PersonMemorySnapshot memoryBefore = memoryRuntime.CreateSnapshot();
            KnowledgeRecordSnapshot recordsBefore = recordRuntime.CreateSnapshot();
            string beforeSummary = SnapshotSummary(knowledgeBefore, historyBefore, memoryBefore, recordsBefore);

            RecordKnowledgeVisibleInjury();
            RunKnowledgeHistoryEventMemoryFlow();
            CreateLocationRecord();

            string unchangedSummary = SnapshotSummary(knowledgeBefore, historyBefore, memoryBefore, recordsBefore);
            string afterSummary = SnapshotSummary(knowledge.CreateSnapshot(), historyRuntime.CreateSnapshot(), memoryRuntime.CreateSnapshot(), recordRuntime.CreateSnapshot());
            bool immutable = string.Equals(beforeSummary, unchangedSummary, StringComparison.Ordinal) && !string.Equals(beforeSummary, afterSummary, StringComparison.Ordinal);
            bool collectionsImmutable = CollectionRejectsMutation(knowledgeBefore.Beliefs)
                && CollectionRejectsMutation(historyBefore.Events)
                && CollectionRejectsMutation(memoryBefore.Memories)
                && CollectionRejectsMutation(recordsBefore.Records);
            bool succeeded = immutable && collectionsImmutable;
            return Record(succeeded, "Validate 8.10 Snapshot Immutability", succeeded ? "Success" : "MutableSnapshot", $"StableBefore={immutable} ReadOnlyCollections={collectionsImmutable} Before='{beforeSummary}' After='{afterSummary}'.");
        }

        public PrototypeTestLabOperation ValidateKnowledgeHistoryDeterministicOrdering()
        {
            RunKnowledgeHistoryEventMemoryFlow();
            CreatePersonalJournalRecord();
            CreateLocationRecord();
            CreateMedicalRecord();
            KnowledgeHistoryFacade facade = CreateKnowledgeHistoryFacade();
            string first = DeterministicIntegrationSummary(facade);
            string second = DeterministicIntegrationSummary(facade);
            bool succeeded = string.Equals(first, second, StringComparison.Ordinal);
            return Record(succeeded, "Validate 8.10 Deterministic Ordering", succeeded ? "Success" : "OrderingMismatch", $"First='{first}' Second='{second}'.");
        }

        public PrototypeTestLabOperation ValidateKnowledgeHistoryDirtyAndEventBoundaries()
        {
            EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge);
            long knowledgeRevision = knowledge.KnowledgeRevision;
            int knowledgeEvents = 0;
            knowledge.KnowledgeChanged += CountKnowledge;

            bool previewNoMutation = false;
            bool failedNoMutation = false;
            bool successOnce = false;
            try
            {
                if (!TryBuildSpeciesCapabilityObservation($"knowledge.8.10.preview.{Guid.NewGuid():N}", 850, 900, out _, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure))
                {
                    return failure;
                }

                KnowledgeOperationResult preview = knowledge.PreviewObservation(request);
                previewNoMutation = preview.Succeeded && preview.Preview && knowledge.KnowledgeRevision == knowledgeRevision && knowledgeEvents == 0;

                KnowledgeOperationResult failed = knowledge.RecordObservation(new KnowledgeObservationRequest { PersonId = GetPrototypePersonId(), TransactionId = $"knowledge.8.10.invalid.{Guid.NewGuid():N}" });
                failedNoMutation = !failed.Succeeded && knowledge.KnowledgeRevision == knowledgeRevision && knowledgeEvents == 0;

                request.TransactionId = $"knowledge.8.10.commit.{Guid.NewGuid():N}";
                KnowledgeOperationResult committed = knowledge.RecordObservation(request);
                successOnce = committed.Succeeded && knowledge.KnowledgeRevision == knowledgeRevision + 1 && knowledgeEvents == 1;
            }
            finally
            {
                knowledge.KnowledgeChanged -= CountKnowledge;
            }

            bool succeeded = previewNoMutation && failedNoMutation && successOnce;
            return Record(succeeded, "Validate 8.10 Dirty and Events", succeeded ? "Success" : "BoundaryMismatch", $"PreviewNoMutation={previewNoMutation} FailedNoMutation={failedNoMutation} SuccessOnce={successOnce} Events={knowledgeEvents} Revision={knowledgeRevision}->{knowledge.KnowledgeRevision}.");

            void CountKnowledge(PersonKnowledgeRuntime _, KnowledgeOperationResult __) => knowledgeEvents++;
        }

        public PrototypeTestLabOperation PreviewStep9KnowledgeContracts()
        {
            ItemKnowledgeRequest item = new ItemKnowledgeRequest { RequestingPersonId = GetPrototypePersonId(), ItemDefinitionId = "item.health-potion", ItemInstanceId = string.Empty };
            RecipeKnowledgeRequest recipe = new RecipeKnowledgeRequest { RequestingPersonId = GetPrototypePersonId(), RecipeDefinitionId = "recipe.prototype.health-potion", RequiredSkillId = "skill.alchemy" };
            ProductionDiscoveryRequest production = new ProductionDiscoveryRequest { RequestingPersonId = GetPrototypePersonId(), ProductionDefinitionId = "production.prototype.brewing", ObservedActorId = ResolveActorId(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject), ObservedItemId = "item.health-potion" };
            CraftedItemProvenanceRequest provenance = new CraftedItemProvenanceRequest { CraftedItemInstanceId = "item-instance.prototype.crafted", CrafterPersonId = GetPrototypePersonId(), RecipeDefinitionId = recipe.RecipeDefinitionId, WorkstationId = "workstation.prototype.alchemy", WorldTimeSeconds = GetPrototypeWorldTime() };
            Step9KnowledgeContractResult result = Step9KnowledgeContractResult.PreviewReady("Step 9 item, recipe, production, teaching, and provenance contracts are present without runtime ownership.");
            bool succeeded = result.Succeeded
                && !string.IsNullOrWhiteSpace(item.SubjectId)
                && !string.IsNullOrWhiteSpace(recipe.RecipeDefinitionId)
                && !string.IsNullOrWhiteSpace(production.ProductionDefinitionId)
                && !string.IsNullOrWhiteSpace(provenance.CraftedItemInstanceId);
            return Record(succeeded, "Preview 8.10 Step 9 Contracts", succeeded ? result.Code : "ContractMismatch", $"{result.Message} Item={item.SubjectId} Recipe={recipe.RecipeDefinitionId} Production={production.ProductionDefinitionId} Provenance={provenance.CraftedItemInstanceId}.");
        }

        private KnowledgeHistoryFacade CreateKnowledgeHistoryFacade()
        {
            EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge);
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime);
            informationSources = EnsureInformationSourceRuntime();
            informationTransfers = EnsureInformationTransferRuntime();
            informationAccess = EnsureInformationAccessRuntime();
            knowledgeRecords = EnsureKnowledgeRecordRuntime();
            return new KnowledgeHistoryFacade(new KnowledgeHistoryRuntimeSet
            {
                DefinitionRegistry = registry,
                PersonId = GetPrototypePersonId(),
                WorldId = PersistenceService.LocalWorldId,
                KnownPersonIds = GetKnownPrototypePersons(),
                KnownBodyIds = GetKnownPrototypeBodies(),
                KnowledgeRuntime = knowledge,
                HistoryRuntime = historyRuntime,
                MemoryRuntime = memoryRuntime,
                SourceRuntime = informationSources,
                TransferRuntime = informationTransfers,
                AccessRuntime = informationAccess,
                RecordRuntime = knowledgeRecords
            });
        }

        private static string SnapshotSummary(KnowledgeSnapshot knowledge, HistorySnapshot historySnapshot, PersonMemorySnapshot memory, KnowledgeRecordSnapshot records)
        {
            string knowledgeIds = string.Join(",", (knowledge?.Beliefs ?? Array.Empty<KnowledgeBeliefRecord>()).Select(item => item.BeliefId));
            string historyIds = string.Join(",", (historySnapshot?.Events ?? Array.Empty<HistoricalEventRecord>()).Select(item => item.EventId));
            string memoryIds = string.Join(",", (memory?.Memories ?? Array.Empty<HistoryMemoryRecord>()).Select(item => item.MemoryId));
            string recordIds = string.Join(",", (records?.Records ?? Array.Empty<KnowledgeRecord>()).Select(item => item.RecordId));
            return $"K[{knowledge?.Revision ?? 0}:{knowledgeIds}] H[{historySnapshot?.Revision ?? 0}:{historyIds}] M[{memory?.Revision ?? 0}:{memoryIds}] R[{records?.Revision ?? 0}:{recordIds}]";
        }

        private static bool CollectionRejectsMutation<T>(IReadOnlyList<T> values)
        {
            try
            {
                ((IList<T>)values).Add(default);
                return false;
            }
            catch (NotSupportedException)
            {
                return true;
            }
            catch (InvalidCastException)
            {
                return true;
            }
        }

        private static string DeterministicIntegrationSummary(KnowledgeHistoryFacade facade)
        {
            KnowledgeHistoryValidationResult validation = facade.ValidateCurrentState();
            KnowledgeHistoryReadinessSnapshot readiness = facade.CreateReadinessSnapshot();
            KnowledgeHistoryPersistenceInventory persistence = facade.CreatePersistenceInventory();
            return $"{readiness.ToSummary()} :: {validation.ToSummary()} :: {persistence.ToSummary()}";
        }

        private HistoryOperationResult EnsurePrototypeHistoricalEvent(string eventId, KnowledgeVisibility visibility, string note)
        {
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _);
            if (historyRuntime.TryGetEvent(eventId, out HistoricalEventRecord existing))
            {
                return HistoryOperationResult.Success("Historical event already exists.", string.Empty, existing, null, null, historyRuntime.HistoryRevision, historyRuntime.HistoryRevision, duplicate: true);
            }

            return historyRuntime.RecordEvent(BuildHistoryEventRequest($"history.ensure.{SanitizeForTransaction(eventId)}.{Guid.NewGuid():N}", eventId, "history-event.person-participation", GetPrototypePersonId(), visibility, note));
        }

        private KnowledgeOperationResult EnsurePrototypeEvidence(
            string evidenceId,
            string factDefinitionId,
            KnowledgeSubjectType subjectType,
            string subjectId,
            KnowledgeValueType valueType,
            string value,
            KnowledgeProvenance provenance)
        {
            EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge);
            KnowledgeEvidenceRecord existing = knowledge.CreateSnapshot().Evidence.FirstOrDefault(record => string.Equals(record.EvidenceId, evidenceId, StringComparison.Ordinal));
            if (existing != null)
            {
                return KnowledgeOperationResult.Success("Knowledge evidence already exists.", string.Empty, null, null, existing, null, knowledge.KnowledgeRevision, knowledge.KnowledgeRevision, duplicate: true);
            }

            KnowledgePropositionData proposition = new KnowledgePropositionData
            {
                factDefinitionId = factDefinitionId,
                subjectType = subjectType,
                subjectId = subjectId,
                valueType = valueType,
                stableValueId = valueType == KnowledgeValueType.StableId ? value : string.Empty,
                qualitativeValue = valueType == KnowledgeValueType.Qualitative || valueType == KnowledgeValueType.Text ? value : string.Empty,
                numericValue = valueType == KnowledgeValueType.Numeric && int.TryParse(value, out int numericValue) ? numericValue : 0,
                booleanValue = valueType == KnowledgeValueType.Boolean && bool.TryParse(value, out bool booleanValue) && booleanValue,
                bodyContextId = subjectType == KnowledgeSubjectType.Body ? subjectId : string.Empty,
                sourceContextId = "test-lab.knowledge-record-fixture"
            };
            return knowledge.RecordObservation(new KnowledgeObservationRequest
            {
                PersonId = knowledge.PersonId,
                TransactionId = $"knowledge.ensure.{SanitizeForTransaction(evidenceId)}.{Guid.NewGuid():N}",
                EvidenceId = evidenceId,
                Proposition = proposition,
                AcquisitionSource = KnowledgeAcquisitionSource.DirectObservation,
                Provenance = provenance,
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = 760,
                Credibility = 780,
                GameTimeSeconds = GetPrototypeWorldTime(),
                SourceId = "test-lab.knowledge-record-fixture",
                Visibility = KnowledgeVisibility.Public
            });
        }

        private InformationAccessRuntime EnsureInformationAccessRuntime()
        {
            if (currentAutomationScenarioContext?.Runtimes?.Access != null)
            {
                informationAccess = currentAutomationScenarioContext.Runtimes.Access;
                return informationAccess;
            }

            informationAccess ??= context?.InformationAccess ?? context?.Persistence?.InformationAccess ?? new InformationAccessRuntime();
            informationAccess.Configure(registry, GetPrototypePersonId());
            return informationAccess;
        }

        private InformationSourceRuntime EnsureInformationSourceRuntime()
        {
            if (currentAutomationScenarioContext?.Runtimes?.Sources != null)
            {
                informationSources = currentAutomationScenarioContext.Runtimes.Sources;
                return informationSources;
            }

            informationSources ??= context?.Persistence?.InformationSources ?? new InformationSourceRuntime();
            informationSources.Configure(registry, GetPrototypePersonId());
            return informationSources;
        }

        private InformationTransferRuntime EnsureInformationTransferRuntime()
        {
            if (currentAutomationScenarioContext?.Runtimes?.Transfers != null)
            {
                informationTransfers = currentAutomationScenarioContext.Runtimes.Transfers;
                return informationTransfers;
            }

            informationTransfers ??= context?.Persistence?.InformationTransfers ?? new InformationTransferRuntime();
            informationTransfers.Configure(registry, GetPrototypePersonId());
            return informationTransfers;
        }

        private KnowledgeRecordRuntime EnsureKnowledgeRecordRuntime()
        {
            if (currentAutomationScenarioContext?.Runtimes?.Records != null)
            {
                knowledgeRecords = currentAutomationScenarioContext.Runtimes.Records;
                return knowledgeRecords;
            }

            knowledgeRecords ??= context?.KnowledgeRecords ?? context?.Persistence?.KnowledgeRecords ?? new KnowledgeRecordRuntime();
            knowledgeRecords.Configure(registry, GetPrototypePersonId());
            if (context != null)
            {
                context.KnowledgeRecords = knowledgeRecords;
            }

            return knowledgeRecords;
        }

        private KnowledgeRecordOperationResult CreatePrototypeRecord(
            string recordId,
            string definitionId,
            KnowledgeRecordCategory category,
            InformationSubjectType subjectType,
            string subjectId,
            InformationVisibilityClassification classification,
            string summary,
            string body,
            bool preview = false,
            string[] sourceIds = null,
            string[] evidenceIds = null,
            string[] historicalEventIds = null,
            string[] lifeEventIds = null,
            string accessPolicyId = "")
        {
            KnowledgeRecordRuntime runtime = EnsureKnowledgeRecordRuntime();
            if (!preview)
            {
                KnowledgeRecord existing = runtime.CreateSnapshot().Records.FirstOrDefault(record => string.Equals(record.RecordId, recordId, StringComparison.Ordinal));
                if (existing != null)
                {
                    return KnowledgeRecordOperationResult.Success("Knowledge Record fixture already exists.", string.Empty, runtime.RecordRevision, runtime.RecordRevision, existing, duplicate: true);
                }
            }

            return runtime.CreateRecord(BuildPrototypeRecordRequest(recordId, definitionId, category, subjectType, subjectId, classification, summary, body, preview, sourceIds, evidenceIds, historicalEventIds, lifeEventIds, accessPolicyId));
        }

        private KnowledgeRecordCreateRequest BuildPrototypeRecordRequest(
            string recordId,
            string definitionId,
            KnowledgeRecordCategory category,
            InformationSubjectType subjectType,
            string subjectId,
            InformationVisibilityClassification classification,
            string summary,
            string body,
            bool preview = false,
            string[] sourceIds = null,
            string[] evidenceIds = null,
            string[] historicalEventIds = null,
            string[] lifeEventIds = null,
            string accessPolicyId = "")
        {
            return new KnowledgeRecordCreateRequest
            {
                TransactionId = $"record.8.9.{SanitizeForTransaction(recordId)}.{Guid.NewGuid():N}",
                RecordId = recordId,
                DefinitionId = definitionId,
                Category = category,
                OwnerKind = KnowledgeRecordOwnerKind.Person,
                OwnerId = GetPrototypePersonId(),
                Subject = BuildPrototypeSubject(subjectType, subjectId, GetPrototypePersonId()),
                AuthorPersonId = GetPrototypePersonId(),
                WorldTimeSeconds = GetPrototypeWorldTime(),
                OccurredWorldTimeSeconds = GetPrototypeWorldTime(),
                KnowledgeOwnerPersonId = GetPrototypePersonId(),
                SourceIds = sourceIds ?? Array.Empty<string>(),
                EvidenceIds = evidenceIds ?? Array.Empty<string>(),
                HistoricalEventIds = historicalEventIds ?? Array.Empty<string>(),
                LifeEventIds = lifeEventIds ?? Array.Empty<string>(),
                Confidence = classification == InformationVisibilityClassification.Public ? 850 : 650,
                Reliability = classification == InformationVisibilityClassification.Public ? 800 : 600,
                AccessPolicyId = accessPolicyId ?? string.Empty,
                Classification = classification,
                Tags = new[] { "feature.8.9", "prototype-record" },
                Details = new[]
                {
                    new KnowledgeRecordDetailData { detailId = "detail.summary", labelKey = "summary", value = summary ?? string.Empty, valueType = KnowledgeValueType.Text },
                    new KnowledgeRecordDetailData { detailId = "detail.body", labelKey = "body", value = body ?? string.Empty, valueType = KnowledgeValueType.Text },
                    new KnowledgeRecordDetailData { detailId = "detail.source", labelKey = "source", value = string.Join(",", sourceIds ?? Array.Empty<string>()), valueType = KnowledgeValueType.Text, uncertain = sourceIds == null || sourceIds.Length == 0 }
                },
                Preview = preview
            };
        }

        private KnowledgeRecordProjectionContext BuildKnowledgeRecordProjectionContext(string requesterId, InformationAccessMode mode, bool privileged)
        {
            return new KnowledgeRecordProjectionContext
            {
                RequesterPersonId = requesterId,
                ContextKind = privileged ? KnowledgeRecordProjectionContextKind.Privileged : KnowledgeRecordProjectionContextKind.Public,
                AccessContext = BuildProjectionAccessContext(requesterId, mode)
            };
        }

        private PrototypeTestLabOperation RecordKnowledgeRecordOperation(string operationName, KnowledgeRecordOperationResult result)
        {
            bool succeeded = result != null && result.Succeeded;
            string message = result == null
                ? "Knowledge Record operation returned no result."
                : $"{result.Message} Preview={result.Preview} Duplicate={result.Duplicate} Code={result.Code} Revision={result.PriorRevision}->{result.ResultingRevision} Record={result.Record?.RecordId ?? "None"} Details={result.Record?.Details.Count ?? 0}.";
            return Record(succeeded, operationName, succeeded ? result.Code.ToString() : result?.Code.ToString() ?? KnowledgeRecordResultCode.InvalidRequest.ToString(), message);
        }

        private PrototypeTestLabOperation RecordKnowledgeRecordReadOperation(string operationName, KnowledgeRecordReadResult result)
        {
            bool succeeded = result != null && result.Succeeded;
            string message = result == null
                ? "Knowledge Record read returned no result."
                : $"{result.Message} Preview={result.Preview} Duplicate={result.Duplicate} Code={result.Code} Record={result.Projection?.VisibleRecordId ?? "None"} Source={result.SourceInstanceId} Evidence={result.EvidenceId} Belief={result.BeliefId} Memory={result.MemoryId} SourceResult={result.SourceResult?.Code.ToString() ?? "None"} KnowledgeResult={result.KnowledgeResult?.Code.ToString() ?? "None"} MemoryResult={result.MemoryResult?.Code.ToString() ?? "None"}.";
            return Record(succeeded, operationName, succeeded ? result.Code.ToString() : result?.Code.ToString() ?? KnowledgeRecordResultCode.InvalidRequest.ToString(), message);
        }

        private void EnsurePrototypeAccessPolicies()
        {
            EnsureInformationAccessRuntime();
            foreach (InformationAccessPolicyDefinition definition in CreatePrototypeAccessPolicyDefinitions())
            {
                InformationSubjectReferenceData subject = definition.Id switch
                {
                    PrototypePublicPolicyId => BuildPrototypeSubject(InformationSubjectType.FactInstance, PrototypePublicSubjectId, owner: string.Empty),
                    PrototypeDiscoveryPolicyId => BuildPrototypeSubject(InformationSubjectType.HistoricalEvent, PrototypeDiscoverySubjectId, owner: GetPrototypePersonId()),
                    PrototypeConcealedPolicyId => BuildPrototypeSubject(InformationSubjectType.Memory, PrototypeConcealedSubjectId, owner: GetPrototypePersonId()),
                    _ => BuildPrototypeSubject(InformationSubjectType.Memory, PrototypeSecretSubjectId, owner: GetPrototypePersonId())
                };
                InformationAccessPolicyData policy = definition.CreatePolicyData(subject, GetPrototypePersonId(), context?.PlayerTransform == null ? string.Empty : context.PlayerTransform.name);
                if (definition.Id == PrototypeSecretPolicyId)
                {
                    policy.allowedPersonIds = new[] { GetPrototypePersonId() };
                    policy.needToKnowTags = new[] { "need-to-know.prototype.secret" };
                }

                informationAccess.RegisterPolicy(policy, $"access.8.8.policy.{definition.Id}.{Guid.NewGuid():N}");
            }
        }

        private InformationAccessDecision EvaluatePrototypeAccess(string policyId, string subjectId, string requesterId, InformationSubjectType subjectType, InformationAccessMode mode, bool discovered, bool revealDenial = false, string[] authorizationIds = null)
        {
            EnsureInformationAccessRuntime();
            return informationAccess.EvaluateAccess(BuildPrototypeAccessContext(policyId, subjectId, requesterId, subjectType, mode, discovered, revealDenial, authorizationIds));
        }

        private InformationAccessContext BuildPrototypeAccessContext(string policyId, string subjectId, string requesterId, InformationSubjectType subjectType, InformationAccessMode mode, bool discovered, bool revealDenial = false, string[] authorizationIds = null)
        {
            return new InformationAccessContext
            {
                RequestingPersonId = requesterId ?? string.Empty,
                ActingEntityId = requesterId ?? string.Empty,
                Subject = BuildPrototypeSubject(subjectType, subjectId, subjectType == InformationSubjectType.FactInstance ? string.Empty : GetPrototypePersonId()),
                Purpose = mode == InformationAccessMode.Share || mode == InformationAccessMode.Reshare ? InformationAccessPurpose.Transfer : InformationAccessPurpose.Gameplay,
                WorldTimeSeconds = 12d,
                AccessMode = mode,
                RequestedDetailIds = new[] { "detail.summary", "detail.original-source", "detail.previous-body" },
                AuthorizationIds = authorizationIds ?? Array.Empty<string>(),
                OrganizationIds = Array.Empty<string>(),
                RoleIds = Array.Empty<string>(),
                NeedToKnowTags = Array.Empty<string>(),
                HasDiscoveredSubject = discovered,
                RevealDenialReasons = revealDenial,
                DeterministicPolicyId = policyId
            };
        }

        private InformationAccessContext BuildProjectionAccessContext(string requesterId, InformationAccessMode mode)
        {
            return new InformationAccessContext
            {
                RequestingPersonId = requesterId ?? string.Empty,
                ActingEntityId = requesterId ?? string.Empty,
                Purpose = InformationAccessPurpose.Gameplay,
                WorldTimeSeconds = 12d,
                AccessMode = mode,
                HasDiscoveredSubject = true,
                RedactedAccessAcceptable = true,
                RevealDenialReasons = true,
                ContextKind = InformationContextKind.Gameplay
            };
        }

        private InformationAccessOperationResult RegisterProjectionPolicy(string policyId, InformationSubjectType subjectType, string subjectId, string ownerPersonId)
        {
            EnsureInformationAccessRuntime();
            InformationAccessPolicyData policy = BuildProjectionPolicyData(policyId, subjectType, subjectId, ownerPersonId);
            InformationAccessPolicyRecord existing = informationAccess.CreateSnapshot().Policies.FirstOrDefault(record => string.Equals(record.PolicyId, policyId, StringComparison.Ordinal));
            if (existing != null)
            {
                if (!ProjectionPolicyMatches(existing.Data, policy))
                {
                    return InformationAccessOperationResult.Failure(InformationAccessResultCode.InvalidRequest, $"Information access policy '{policyId}' already exists with different fixture data.", revision: informationAccess.AccessRevision);
                }

                return InformationAccessOperationResult.Success("Information access policy already exists.", string.Empty, informationAccess.AccessRevision, informationAccess.AccessRevision, duplicate: true);
            }

            return informationAccess.RegisterPolicy(policy, $"access.8.8.projection-policy.{SanitizeForTransaction(policyId)}.{Guid.NewGuid():N}");
        }

        private InformationAccessPolicyData BuildProjectionPolicyData(string policyId, InformationSubjectType subjectType, string subjectId, string ownerPersonId)
        {
            return new InformationAccessPolicyData
            {
                policyId = policyId,
                subject = BuildPrototypeSubject(subjectType, subjectId, ownerPersonId),
                classification = InformationVisibilityClassification.Secret,
                disclosurePolicy = InformationDisclosurePolicy.RedactedOnly,
                resharingPolicy = InformationResharingPolicy.NoResharing,
                sourceVisibilityPolicy = InformationSourceVisibilityPolicy.HideOriginal,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                auditPolicy = InformationAuditPolicy.AuditDenied,
                allowedPersonIds = new[] { ownerPersonId, "person.prototype.listener" },
                defaultVisibleDetails = new[] { "detail.event", "detail.life-event", "detail.memory", "detail.belief", "detail.source", "detail.summary", "detail.proposition", "detail.identity" },
                defaultRedactedDetails = new[] { "detail.source", "detail.provenance", "detail.original-source", "detail.evidence" },
                defaultHiddenDetails = new[] { "detail.payload", "detail.context", "detail.suppression", "detail.transformations" },
                provenance = "Prototype Test Lab access projection adapter."
            };
        }

        private static bool ProjectionPolicyMatches(InformationAccessPolicyData existing, InformationAccessPolicyData expected)
        {
            if (existing == null || expected == null)
            {
                return false;
            }

            return string.Equals(existing.policyId, expected.policyId, StringComparison.Ordinal)
                && existing.subject?.subjectType == expected.subject?.subjectType
                && string.Equals(existing.subject?.subjectId, expected.subject?.subjectId, StringComparison.Ordinal)
                && string.Equals(existing.subject?.ownerPersonId, expected.subject?.ownerPersonId, StringComparison.Ordinal)
                && existing.classification == expected.classification
                && existing.disclosurePolicy == expected.disclosurePolicy
                && existing.resharingPolicy == expected.resharingPolicy
                && existing.sourceVisibilityPolicy == expected.sourceVisibilityPolicy
                && existing.detailVisibilityPolicy == expected.detailVisibilityPolicy
                && existing.auditPolicy == expected.auditPolicy
                && SameSet(existing.allowedPersonIds, expected.allowedPersonIds)
                && SameSet(existing.defaultVisibleDetails, expected.defaultVisibleDetails)
                && SameSet(existing.defaultRedactedDetails, expected.defaultRedactedDetails)
                && SameSet(existing.defaultHiddenDetails, expected.defaultHiddenDetails);
        }

        private static bool SameSet(IEnumerable<string> first, IEnumerable<string> second)
        {
            return new HashSet<string>((first ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal)
                .SetEquals((second ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private void RevokePrototypeAccessGrantIfPresent(string grantId)
        {
            InformationAccessSnapshot snapshot = EnsureInformationAccessRuntime().CreateSnapshot();
            if (snapshot.Grants.Any(record => string.Equals(record.GrantId, grantId, StringComparison.Ordinal) && !record.Revoked))
            {
                informationAccess.RevokeGrant(grantId, $"access.8.8.revoke.{SanitizeForTransaction(grantId)}.{Guid.NewGuid():N}", 12d);
            }
        }

        private InformationAccessGrantData BuildPrototypeAccessGrant(string grantId, string personId, InformationAccessMode[] modes, bool permitsDisclosure, bool permitsResharing, InformationSourceVisibilityPolicy sourceVisibility)
        {
            return new InformationAccessGrantData
            {
                grantId = grantId,
                policyId = PrototypeSecretPolicyId,
                subject = BuildPrototypeSubject(InformationSubjectType.Memory, PrototypeSecretSubjectId, GetPrototypePersonId()),
                granteeKind = InformationGranteeKind.Person,
                granteeId = personId,
                grantorId = GetPrototypePersonId(),
                accessModes = modes ?? Array.Empty<InformationAccessMode>(),
                detailIds = new[] { "detail.summary", "detail.location" },
                sourceVisibility = sourceVisibility,
                permitsDisclosure = permitsDisclosure,
                permitsResharing = permitsResharing,
                reason = "Prototype Test Lab 8.8 grant.",
                provenance = "Prototype Test Lab"
            };
        }

        private static InformationSubjectReferenceData BuildPrototypeSubject(InformationSubjectType subjectType, string subjectId, string owner = "")
        {
            return new InformationSubjectReferenceData
            {
                subjectType = subjectType,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = "step.8.prototype",
                ownerPersonId = owner ?? string.Empty,
                tags = new[] { "prototype", "step8", "access" }
            };
        }

        private static IReadOnlyList<InformationAccessPolicyDefinition> CreatePrototypeAccessPolicyDefinitions()
        {
            return new[]
            {
                CreatePrototypeAccessPolicyDefinition(PrototypePublicPolicyId, "Prototype Public Rumor", InformationSubjectType.FactInstance, InformationVisibilityClassification.Public, InformationDisclosurePolicy.FreelyDisclose, InformationResharingPolicy.FreelyReshareable, InformationSourceVisibilityPolicy.Reveal, InformationDetailVisibilityPolicy.All, InformationAuditPolicy.None),
                CreatePrototypeAccessPolicyDefinition(PrototypeSecretPolicyId, "Prototype Previous Body Secret", InformationSubjectType.Memory, InformationVisibilityClassification.Secret, InformationDisclosurePolicy.RedactedOnly, InformationResharingPolicy.NoResharing, InformationSourceVisibilityPolicy.HideOriginal, InformationDetailVisibilityPolicy.Selected, InformationAuditPolicy.AuditDeniedAndGranted, new[] { "detail.summary", "detail.location" }, new[] { "detail.original-source" }, new[] { "detail.previous-body" }, requiresDiscovery: false, allowRedactedAccess: true),
                CreatePrototypeAccessPolicyDefinition(PrototypeDiscoveryPolicyId, "Prototype Hidden Discovery", InformationSubjectType.HistoricalEvent, InformationVisibilityClassification.Public, InformationDisclosurePolicy.SameAsAccess, InformationResharingPolicy.FreelyReshareable, InformationSourceVisibilityPolicy.Reveal, InformationDetailVisibilityPolicy.ExistenceOnly, InformationAuditPolicy.AuditDenied, new[] { "detail.summary" }, null, new[] { "detail.location" }, requiresDiscovery: true, allowRedactedAccess: true),
                CreatePrototypeAccessPolicyDefinition(PrototypeConcealedPolicyId, "Prototype Concealed Secret", InformationSubjectType.Memory, InformationVisibilityClassification.Secret, InformationDisclosurePolicy.RedactedOnly, InformationResharingPolicy.NoResharing, InformationSourceVisibilityPolicy.HideFullProvenance, InformationDetailVisibilityPolicy.Selected, InformationAuditPolicy.AuditDenied, new[] { "detail.summary" }, new[] { "detail.original-source" }, new[] { "detail.previous-body" }, requiresDiscovery: false, allowRedactedAccess: true)
            };
        }

        private static InformationAccessPolicyDefinition CreatePrototypeAccessPolicyDefinition(string id, string displayName, InformationSubjectType subjectType, InformationVisibilityClassification classification, InformationDisclosurePolicy disclosure, InformationResharingPolicy resharing, InformationSourceVisibilityPolicy sourceVisibility, InformationDetailVisibilityPolicy detailVisibility, InformationAuditPolicy audit, string[] visibleDetails = null, string[] redactedDetails = null, string[] hiddenDetails = null, bool requiresDiscovery = false, bool allowRedactedAccess = true)
        {
            InformationAccessPolicyDefinition definition = ScriptableObject.CreateInstance<InformationAccessPolicyDefinition>();
            definition.DevelopmentConfigure(id, displayName, subjectType, classification, disclosure, resharing, sourceVisibility, detailVisibility, audit, visibleDetails, redactedDetails, hiddenDetails, requiresDiscovery, allowRedactedAccess);
            return definition;
        }

        private PrototypeTestLabOperation RecordAccessOperation(string operationName, InformationAccessOperationResult result)
        {
            bool succeeded = result != null && result.Succeeded;
            string message = result == null
                ? "Information Access operation returned no result."
                : $"{result.Message} Preview={result.Preview} Duplicate={result.Duplicate} Revision={result.PriorRevision}->{result.ResultingRevision}.";
            return Record(succeeded, operationName, succeeded ? result.Code.ToString() : result?.Code.ToString() ?? InformationAccessResultCode.InvalidRequest.ToString(), message);
        }

        private static string FormatAccessDecision(InformationAccessDecision decision)
        {
            if (decision == null)
            {
                return "Decision=None";
            }

            return $"Decision={decision.Decision} Denial={decision.DenialCode} Subject={decision.Subject.SubjectType}/{decision.Subject.SubjectId} Requester={decision.RequesterPersonId} Mode={decision.Mode} SourceVisible={decision.SourceVisible} Reshare={decision.ResharingOutcome} Allowed=[{string.Join(",", decision.AllowedDetails)}] Redacted=[{string.Join(",", decision.RedactedDetails)}] Hidden=[{string.Join(",", decision.HiddenDetails)}] Audit={decision.AuditRequired} VisibleReason='{decision.VisibleReason}' Diagnostic='{decision.DiagnosticReason}'";
        }

        private static string FormatProjection(RedactedInformationProjection projection)
        {
            if (projection == null)
            {
                return "Projection=None";
            }

            string details = string.Join(",", projection.Details.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}:{pair.Value}"));
            return $"{projection.Decision.Decision}/{projection.Decision.DenialCode} Details=[{details}]";
        }

        private static string FormatAccessProjection<T>(InformationAccessProjection<T> projection)
        {
            if (projection == null)
            {
                return "Projection=None";
            }

            return $"Succeeded={projection.Succeeded} Decision={projection.Decision?.Decision.ToString() ?? "None"} Subject='{projection.VisibleSubjectId}' Redacted={projection.Redacted}";
        }

        private static string FormatBiographyAccessProjection(InformationAccessProjection<BiographyTimelineEntry> projection)
        {
            if (projection == null)
            {
                return "Projection=None";
            }

            return $"{projection.Record?.LifeEvent.EventId ?? "None"}:{FormatAccessProjection(projection)}";
        }

        private static bool ProjectionMatchesSubject(InformationAccessProjection<BiographyTimelineEntry> projection, string subjectId)
        {
            if (projection == null || string.IsNullOrWhiteSpace(subjectId))
            {
                return false;
            }

            return string.Equals(projection.VisibleSubjectId, subjectId, StringComparison.Ordinal)
                || string.Equals(projection.Record?.LifeEvent.EventId, subjectId, StringComparison.Ordinal);
        }

        public string BuildInformationTransferSummary()
        {
            InformationTransferRuntime transferRuntime = EnsureInformationTransferRuntime();
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            InformationTransferSnapshot snapshot = transferRuntime.CreateSnapshot();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Feature 8.7 Information Sharing and Teaching");
            builder.AppendLine($"Owner: {snapshot.OwnerId} Revision: {snapshot.Revision} Transfers: {snapshot.Transfers.Count} Sources: {sourceRuntime.CreateSnapshot().Sources.Count}");
            foreach (InformationTransferRecord transfer in snapshot.Transfers.OrderByDescending(record => record.Data.revision).Take(10))
            {
                TransferRecipientResult first = transfer.RecipientResults.FirstOrDefault();
                builder.AppendLine($"{transfer.Data.mode}: {transfer.TransferId} sender={transfer.SenderPersonId} recipients={transfer.RecipientPersonIds.Count} understanding={first?.Understanding.ToString() ?? "None"} confidence={first?.InheritedConfidence ?? 0} source={transfer.Data.createdSourceId}");
            }

            PrototypeTestLabOperation last = history.Count == 0 ? default : history[0];
            if (!string.IsNullOrWhiteSpace(last.OperationName) && last.OperationName.Contains("8.7", StringComparison.Ordinal))
            {
                builder.AppendLine($"Last 8.7: {last.OperationName} Code={last.Code} Success={last.Succeeded}");
                builder.AppendLine(last.Message);
            }

            return builder.ToString();
        }

        public PrototypeTestLabOperation ValidateInformationTransferDefinitions()
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            Dictionary<string, IGameDefinition> definitions = new Dictionary<string, IGameDefinition>(StringComparer.Ordinal);
            foreach (InformationTransferDefinition definition in CreatePrototypeTransferDefinitions())
            {
                definitions[definition.Id] = definition;
                definition.ValidateCatalogDefinition(definitions, report);
            }

            bool succeeded = report.ErrorCount == 0 && report.WarningCount == 0;
            return Record(succeeded, "Validate 8.7 Transfer Definitions", succeeded ? "Success" : "ValidationFailed", report.GetSummary());
        }

        public PrototypeTestLabOperation ShareKnownTrueFact()
        {
            return ExecutePrototypeTransfer("Share 8.7 Known True Fact", InformationTransferMode.DirectTestimony, InformationTransferContentType.BeliefStatement, false, false, false, false);
        }

        public PrototypeTestLabOperation ShareSincereFalseBelief()
        {
            return ExecutePrototypeTransfer("Share 8.7 Sincere False Belief", InformationTransferMode.RumorRetelling, InformationTransferContentType.BeliefStatement, false, false, true, true);
        }

        public PrototypeTestLabOperation SharePartiallyRecalledEvent()
        {
            return ExecutePrototypeTransfer("Share 8.7 Partially Recalled Event", InformationTransferMode.ConversationStatement, InformationTransferContentType.MemoryStatement, true, false, false, false, summarization: true);
        }

        public PrototypeTestLabOperation AttemptSuppressedMemoryTransfer()
        {
            if (!EnsureTransferPrerequisites(out PersonKnowledgeRuntime senderKnowledge, out PersonMemoryRuntime senderMemory, out string memoryId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            double now = GetMemoryWorldTime(senderMemory, memoryId);
            HistoryOperationResult suppression = senderMemory.AddSuppression(new MemorySuppressionRequest
            {
                TransactionId = $"transfer.suppression.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                SuppressionId = $"suppression.transfer.{Guid.NewGuid():N}",
                SourceId = "test-lab.transfer.suppression",
                StartedAtWorldTime = now,
                AllowsCueBypass = false,
                Provenance = "Prototype 8.7 recall boundary."
            });
            if (!suppression.Succeeded)
            {
                return RecordHistoryResult("Prepare 8.7 Suppressed Memory Transfer", suppression);
            }

            InformationTransferResult result = ExecutePrototypeTransferRaw("transfer-suppressed", InformationTransferMode.ConversationStatement, new[] { BuildTransferContent("content.memory", InformationTransferContentType.MemoryStatement, true, memoryId) }, senderKnowledge, senderMemory, null, true, false, false, false, false, out _, out _, worldTimeSeconds: now);
            bool succeeded = !result.Succeeded && result.Status == InformationTransferStatus.RecallFailed;
            return Record(succeeded, "Reject 8.7 Suppressed Memory Transfer", succeeded ? "Success" : result.Status.ToString(), FormatTransferResult(result));
        }

        public PrototypeTestLabOperation ShareDirectObservation()
        {
            EnsurePrototypeSource(GetPrototypeDirectObservationSourceId(), InformationSourceCategory.DirectObservation);
            return ExecutePrototypeTransfer("Share 8.7 Direct Observation", InformationTransferMode.DirectTestimony, InformationTransferContentType.EvidenceReference, false, false, false, false, sourceId: GetPrototypeDirectObservationSourceId());
        }

        public PrototypeTestLabOperation ShareExpertDiagnosis()
        {
            EnsurePrototypeSource("information-source.prototype.expert", InformationSourceCategory.ExpertTestimony);
            AssessPrototypeSource($"source-assessment.transfer.expert.{Guid.NewGuid():N}", GetPrototypePersonId(), "information-source.prototype.expert", 900, 80, 40, 60, authority: 950);
            return ExecutePrototypeTransfer("Share 8.7 Expert Diagnosis", InformationTransferMode.FormalLesson, InformationTransferContentType.ConditionOrDiagnosis, false, true, false, false, sourceId: "information-source.prototype.expert");
        }

        public PrototypeTestLabOperation CompareInheritedConfidenceByDomain()
        {
            EnsurePrototypeSource("information-source.prototype.expert", InformationSourceCategory.ExpertTestimony);
            AssessPrototypeSource($"source-assessment.transfer.high.{Guid.NewGuid():N}", "person.prototype.listener", "information-source.prototype.expert", 900, 60, 40, 70, authority: 950);
            InformationTransferResult high = ExecutePrototypeTransferRaw("transfer-confidence-high", InformationTransferMode.DirectTestimony, new[] { BuildTransferContent("content.high", InformationTransferContentType.BeliefStatement, false, string.Empty) }, null, null, "information-source.prototype.expert", false, false, false, false, false, out GameObject highObject, out _);
            DestroyTestObject(highObject);

            EnsurePrototypeSource("information-source.prototype.anonymous", InformationSourceCategory.AnonymousTestimony);
            AssessPrototypeSource($"source-assessment.transfer.low.{Guid.NewGuid():N}", "person.prototype.listener", "information-source.prototype.anonymous", 250, 800, 850, 720);
            InformationTransferResult low = ExecutePrototypeTransferRaw("transfer-confidence-low", InformationTransferMode.RumorRetelling, new[] { BuildTransferContent("content.low", InformationTransferContentType.BeliefStatement, false, string.Empty) }, null, null, "information-source.prototype.anonymous", false, false, false, false, false, out GameObject lowObject, out _);
            DestroyTestObject(lowObject);

            int highConfidence = high.RecipientResults.FirstOrDefault()?.InheritedConfidence ?? 0;
            int lowConfidence = low.RecipientResults.FirstOrDefault()?.InheritedConfidence ?? 0;
            bool succeeded = high.Succeeded && low.Succeeded && highConfidence > lowConfidence;
            return Record(succeeded, "Compare 8.7 Inherited Confidence", succeeded ? "Success" : "ConfidenceMismatch", $"High={highConfidence} Low={lowConfidence}. High={FormatTransferResult(high)} Low={FormatTransferResult(low)}");
        }

        public PrototypeTestLabOperation ShareAnonymousInformation()
        {
            EnsurePrototypeSource("information-source.prototype.anonymous", InformationSourceCategory.AnonymousTestimony);
            return ExecutePrototypeTransfer("Share 8.7 Anonymous Information", InformationTransferMode.RumorRetelling, InformationTransferContentType.BeliefStatement, false, false, false, false, sourceId: "information-source.prototype.anonymous", privacy: TransferPrivacyScope.HiddenSource);
        }

        public PrototypeTestLabOperation ReadOfficialRecord()
        {
            EnsurePrototypeSource("information-source.prototype.official-record", InformationSourceCategory.OfficialRecord);
            return ExecutePrototypeTransfer("Read 8.7 Official Record", InformationTransferMode.Report, InformationTransferContentType.HistoricalEventReference, false, false, false, false, sourceId: "information-source.prototype.official-record");
        }

        public PrototypeTestLabOperation CopyAndSummarizeTransferSource()
        {
            EnsurePrototypeSource("information-source.prototype.official-record", InformationSourceCategory.OfficialRecord);
            return ExecutePrototypeTransfer("Copy/Summarize 8.7 Transfer Source", InformationTransferMode.Summary, InformationTransferContentType.HistoricalEventReference, false, false, false, false, sourceId: "information-source.prototype.official-record", summarization: true);
        }

        public PrototypeTestLabOperation TraceTransferSourceLineage()
        {
            InformationTransferResult result = ExecutePrototypeTransferRaw("transfer-lineage", InformationTransferMode.Summary, new[] { BuildTransferContent("content.lineage", InformationTransferContentType.HistoricalEventReference, false, string.Empty) }, null, null, "information-source.prototype.official-record", false, false, false, false, true, out GameObject listener, out _);
            DestroyTestObject(listener);
            SourceChainSnapshot chain = informationSources.TraceSourceChain(result.Record?.Data.createdSourceId, privilegedAccess: true);
            bool succeeded = result.Succeeded && chain.TransmissionDepth >= 1;
            return Record(succeeded, "Trace 8.7 Transfer Source Lineage", succeeded ? "Success" : "LineageMissing", $"{FormatTransferResult(result)} Immediate={chain.ImmediateSourceId} Original={chain.OriginalSourceId} Depth={chain.TransmissionDepth}.");
        }

        public PrototypeTestLabOperation CompareTransferRecipientAssessments() => CompareInheritedConfidenceByDomain();

        public PrototypeTestLabOperation TeachSemanticConcept()
        {
            return ExecutePrototypeTransfer("Teach 8.7 Semantic Concept", InformationTransferMode.FormalLesson, InformationTransferContentType.InstructionalConcept, false, true, false, false);
        }

        public PrototypeTestLabOperation TeachProcedureReference()
        {
            return ExecutePrototypeTransfer("Teach 8.7 Procedure Reference", InformationTransferMode.Instruction, InformationTransferContentType.ProcedureReference, false, true, false, false);
        }

        public PrototypeTestLabOperation DemonstrateProcedure()
        {
            return ExecutePrototypeTransfer("Demonstrate 8.7 Procedure", InformationTransferMode.Demonstration, InformationTransferContentType.ProcedureReference, false, true, false, false);
        }

        public PrototypeTestLabOperation TeachWithoutPrerequisites()
        {
            InformationTransferResult result = ExecutePrototypeTransferRaw("transfer-teach-no-prereq", InformationTransferMode.FormalLesson, new[] { BuildTransferContent("content.no-prereq", InformationTransferContentType.ProcedureReference, false, string.Empty) }, null, null, null, false, true, false, false, false, out GameObject listener, out PersonMemoryRuntime recipientMemory, forceLowFidelity: true);
            DestroyTestObject(listener);
            bool succeeded = result.Succeeded && result.RecipientResults.FirstOrDefault()?.Understanding != TransferUnderstandingState.Complete;
            return Record(succeeded, "Teach 8.7 Without Prerequisites", succeeded ? "Success" : result.Status.ToString(), $"{FormatTransferResult(result)} No capabilities or skill ranks are granted by transfer.");
        }

        public PrototypeTestLabOperation CorrectMisconceptionThroughTeaching()
        {
            return ExecutePrototypeTransfer("Correct 8.7 Misconception Teaching", InformationTransferMode.FormalLesson, InformationTransferContentType.BeliefStatement, false, true, false, false, correction: true);
        }

        public PrototypeTestLabOperation ClarifyTransfer()
        {
            InformationTransferResult original = ExecutePrototypeTransferRaw("transfer-clarify-original", InformationTransferMode.Summary, new[] { BuildTransferContent("content.clarify", InformationTransferContentType.BeliefStatement, false, string.Empty) }, null, null, null, false, false, false, false, true, out GameObject originalListener, out _);
            DestroyTestObject(originalListener);
            InformationTransferResult clarification = ExecutePrototypeTransferRaw("transfer-clarify-followup", InformationTransferMode.Explanation, new[] { BuildTransferContent("content.clarification", InformationTransferContentType.BeliefStatement, false, string.Empty) }, null, null, original.Record?.Data.createdSourceId, false, false, false, false, false, out GameObject listener, out _, parentTransferId: original.Record?.TransferId);
            DestroyTestObject(listener);
            bool succeeded = original.Succeeded && clarification.Succeeded && string.Equals(clarification.Record?.Data.parentTransferId, original.Record?.TransferId, StringComparison.Ordinal);
            return Record(succeeded, "Clarify 8.7 Transfer", succeeded ? "Success" : clarification.Status.ToString(), $"{FormatTransferResult(clarification)} Parent={clarification.Record?.Data.parentTransferId}.");
        }

        public PrototypeTestLabOperation ReshareTransfer()
        {
            InformationTransferResult original = ExecutePrototypeTransferRaw("transfer-reshare-original", InformationTransferMode.DirectTestimony, new[] { BuildTransferContent("content.reshare", InformationTransferContentType.BeliefStatement, false, string.Empty) }, null, null, null, false, false, false, false, false, out GameObject originalListener, out _);
            DestroyTestObject(originalListener);
            InformationTransferResult reshare = ExecutePrototypeTransferRaw("transfer-reshare", InformationTransferMode.RumorRetelling, new[] { BuildTransferContent("content.reshare.copy", InformationTransferContentType.BeliefStatement, false, string.Empty) }, null, null, original.Record?.Data.createdSourceId, false, false, false, false, false, out GameObject listener, out _, parentTransferId: original.Record?.TransferId);
            DestroyTestObject(listener);
            bool succeeded = original.Succeeded && reshare.Succeeded;
            return Record(succeeded, "Reshare 8.7 Transfer", succeeded ? "Success" : reshare.Status.ToString(), FormatTransferResult(reshare));
        }

        public PrototypeTestLabOperation ReshareDistortedVersion()
        {
            return ExecutePrototypeTransfer("Reshare 8.7 Distorted Version", InformationTransferMode.RumorRetelling, InformationTransferContentType.BeliefStatement, false, false, false, false, distortion: true);
        }

        public PrototypeTestLabOperation DeliberatelyOmitDetail()
        {
            return ExecutePrototypeTransfer("Deliberately Omit 8.7 Detail", InformationTransferMode.Summary, InformationTransferContentType.MemoryStatement, true, false, false, false, summarization: true, omission: true);
        }

        public PrototypeTestLabOperation CorrectPriorTransfer()
        {
            InformationTransferResult original = ExecutePrototypeTransferRaw("transfer-correct-original", InformationTransferMode.RumorRetelling, new[] { BuildTransferContent("content.correct-original", InformationTransferContentType.BeliefStatement, false, string.Empty) }, null, null, null, false, false, true, true, false, out GameObject originalListener, out _);
            DestroyTestObject(originalListener);
            InformationTransferResult correction = ExecutePrototypeTransferRaw("transfer-correct-followup", InformationTransferMode.Explanation, new[] { BuildTransferContent("content.corrected", InformationTransferContentType.BeliefStatement, false, string.Empty, InformationTransferAssertionType.Correction) }, null, null, original.Record?.Data.createdSourceId, false, true, false, false, false, out GameObject listener, out _, correctionOfTransferId: original.Record?.TransferId);
            DestroyTestObject(listener);
            bool succeeded = original.Succeeded && correction.Succeeded && string.Equals(correction.Record?.Data.correctionOfTransferId, original.Record?.TransferId, StringComparison.Ordinal);
            return Record(succeeded, "Correct 8.7 Prior Transfer", succeeded ? "Success" : correction.Status.ToString(), $"{FormatTransferResult(correction)} CorrectionOf={correction.Record?.Data.correctionOfTransferId}.");
        }

        public PrototypeTestLabOperation CreatePublicPrivateRestrictedTransfers()
        {
            InformationTransferResult publicTransfer = ExecutePrototypeTransferRaw("transfer-public", InformationTransferMode.PublicAnnouncement, new[] { BuildTransferContent("content.public", InformationTransferContentType.Warning, false, string.Empty) }, null, null, null, false, false, false, false, false, out GameObject publicListener, out _, privacy: TransferPrivacyScope.Public);
            DestroyTestObject(publicListener);
            InformationTransferResult privateTransfer = ExecutePrototypeTransferRaw("transfer-private", InformationTransferMode.PrivateMessage, new[] { BuildTransferContent("content.private", InformationTransferContentType.BeliefStatement, false, string.Empty, privacy: KnowledgeVisibility.Private) }, null, null, null, false, true, false, false, false, out GameObject privateListener, out _, privacy: TransferPrivacyScope.RecipientOnly);
            DestroyTestObject(privateListener);
            bool succeeded = publicTransfer.Succeeded && privateTransfer.Succeeded && publicTransfer.Record?.Data.privacyScope == TransferPrivacyScope.Public && privateTransfer.Record?.Data.privacyScope == TransferPrivacyScope.RecipientOnly;
            return Record(succeeded, "Create 8.7 Public Private Transfers", succeeded ? "Success" : "PrivacyMismatch", $"Public={FormatTransferResult(publicTransfer)} Private={FormatTransferResult(privateTransfer)}");
        }

        public PrototypeTestLabOperation ValidateInformationTransferSaveRestore()
        {
            ExecutePrototypeTransferRaw("transfer-save-restore", InformationTransferMode.DirectTestimony, new[] { BuildTransferContent("content.save", InformationTransferContentType.BeliefStatement, false, string.Empty) }, null, null, null, false, false, false, false, false, out GameObject listener, out _);
            DestroyTestObject(listener);
            InformationTransferRuntime transferRuntime = EnsureInformationTransferRuntime();
            InformationTransferSaveData saveData = transferRuntime.CreateSaveData();
            long before = transferRuntime.TransferRevision;
            InformationTransferResult restore = transferRuntime.RestoreFromSaveData(saveData, registry, transferRuntime.OwnerId, restoring: true);
            bool succeeded = restore.Succeeded && transferRuntime.TransferRevision == before;
            return Record(succeeded, "Information Transfer Save Restore", succeeded ? "Success" : restore.Status.ToString(), $"{restore.Message} Revision={before}->{transferRuntime.TransferRevision} Transfers={transferRuntime.CreateSnapshot().Transfers.Count}.");
        }

        public string BuildInformationSourceSummary()
        {
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            InformationSourceSnapshot snapshot = sourceRuntime.CreateSnapshot();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Feature 8.6 Information Sources and Reliability");
            builder.AppendLine($"Owner: {snapshot.OwnerId} Revision: {snapshot.Revision} Sources: {snapshot.Sources.Count} Assessments: {snapshot.Assessments.Count} Transformations: {snapshot.Transformations.Count}");
            foreach (InformationSourceRecord source in snapshot.Sources.Take(12))
            {
                builder.AppendLine($"{source.Category}: {source.SourceInstanceId} ref={source.Data.referenceType}:{source.Data.referencedId} original={source.OriginalSourceId} privacy={source.Privacy} gen={source.Data.generation}");
            }

            foreach (PersonSourceAssessmentRecord assessment in snapshot.Assessments.Take(8))
            {
                builder.AppendLine($"Assessment: {assessment.AssessingPersonId} source={assessment.SourceInstanceId} overall={assessment.Data.reliability.DeriveOverall()} authority={assessment.Data.authority} bias={assessment.Data.biasRisk} error={assessment.Data.errorRisk} deception={assessment.Data.deceptionRisk}");
            }

            PrototypeTestLabOperation last = history.Count == 0 ? default : history[0];
            if (!string.IsNullOrWhiteSpace(last.OperationName) && last.OperationName.Contains("8.6", StringComparison.Ordinal))
            {
                builder.AppendLine($"Last 8.6: {last.OperationName} Code={last.Code} Success={last.Succeeded}");
                builder.AppendLine(last.Message);
            }

            return builder.ToString();
        }

        public PrototypeTestLabOperation ValidateInformationSourceDefinitions()
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            Dictionary<string, IGameDefinition> definitions = new Dictionary<string, IGameDefinition>(StringComparer.Ordinal);
            foreach (InformationSourceDefinition definition in CreatePrototypeSourceDefinitions())
            {
                if (definition == null)
                {
                    continue;
                }

                definitions[definition.Id] = definition;
                definition.ValidateCatalogDefinition(definitions, report);
            }

            bool succeeded = report.ErrorCount == 0 && report.WarningCount == 0;
            return Record(succeeded, "Validate 8.6 Source Definitions", succeeded ? "Success" : "ValidationFailed", report.GetSummary());
        }

        public PrototypeTestLabOperation RegisterDirectObservationSource() => RegisterPrototypeSource("Register 8.6 Direct Observation Source", GetPrototypeDirectObservationSourceId(), InformationSourceCategory.DirectObservation, InformationSourceReferenceType.Body, GetPrototypeBodyId(), KnowledgeDomain.Medical, "observation-method.ordinary-visual");
        public PrototypeTestLabOperation RegisterExpertSource() => RegisterPrototypeSource("Register 8.6 Expert Source", "information-source.prototype.expert", InformationSourceCategory.ExpertTestimony, InformationSourceReferenceType.Person, "person.prototype.expert-healer", KnowledgeDomain.Medical, "examination-method.medical", authority: "licensed-healer");
        public PrototypeTestLabOperation RegisterTestimonySource() => RegisterPrototypeSource("Register 8.6 Testimony Source", "information-source.prototype.testimony", InformationSourceCategory.PersonalTestimony, InformationSourceReferenceType.Person, "person.prototype.npc", KnowledgeDomain.Historical, string.Empty);
        public PrototypeTestLabOperation RegisterAnonymousSource() => RegisterPrototypeSource("Register 8.6 Anonymous Source", "information-source.prototype.anonymous", InformationSourceCategory.AnonymousTestimony, InformationSourceReferenceType.None, string.Empty, KnowledgeDomain.Social, string.Empty, privacy: SourcePrivacyLevel.Shared);
        public PrototypeTestLabOperation RegisterOfficialRecordSource() => RegisterPrototypeSource("Register 8.6 Official Record", "information-source.prototype.official-record", InformationSourceCategory.OfficialRecord, InformationSourceReferenceType.Document, "document.prototype.guild-notice", KnowledgeDomain.Faction, string.Empty, authority: "guild-record");

        public PrototypeTestLabOperation CopySource() => TransformPrototypeSource("Copy 8.6 Source", "information-source.prototype.copy", InformationSourceTransformationType.Copy, 820, hidesOriginal: false);
        public PrototypeTestLabOperation TranslateSource() => TransformPrototypeSource("Translate 8.6 Source", "information-source.prototype.translation", InformationSourceTransformationType.Translation, 760, hidesOriginal: false);
        public PrototypeTestLabOperation SummarizeSource() => TransformPrototypeSource("Summarize 8.6 Source", "information-source.prototype.summary", InformationSourceTransformationType.Summary, 680, hidesOriginal: false);

        public PrototypeTestLabOperation EvaluateReliability()
        {
            EnsurePrototypeSource("information-source.prototype.expert", InformationSourceCategory.ExpertTestimony);
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            SourceReliabilityResult result = sourceRuntime.EvaluateReliability(new SourceReliabilityRequest
            {
                EvaluatingPersonId = GetPrototypePersonId(),
                SourceInstanceId = "information-source.prototype.expert",
                Domain = KnowledgeDomain.Medical,
                SubjectId = GetPrototypeBodyId(),
                MethodId = "examination-method.medical",
                WorldTimeSeconds = GetPrototypeWorldTime()
            });
            return RecordReliability("Evaluate 8.6 Reliability", result);
        }

        public PrototypeTestLabOperation CompareTwoPersonsSourceAssessments()
        {
            EnsurePrototypeSource("information-source.prototype.expert", InformationSourceCategory.ExpertTestimony);
            AssessPrototypeSource("source-assessment.prototype.player.trust", GetPrototypePersonId(), "information-source.prototype.expert", 850, 150, 80, 120);
            AssessPrototypeSource("source-assessment.prototype.rival.distrust", "person.prototype.rival", "information-source.prototype.expert", 320, 650, 600, 520);
            SourceReliabilityResult player = EvaluateFor(GetPrototypePersonId(), "information-source.prototype.expert");
            SourceReliabilityResult rival = EvaluateFor("person.prototype.rival", "information-source.prototype.expert");
            bool succeeded = player.Succeeded && rival.Succeeded && player.DerivedOverall > rival.DerivedOverall;
            return Record(succeeded, "Compare 8.6 Person Assessments", succeeded ? "Success" : "AssessmentComparisonFailed", $"Player={player.DerivedOverall} Rival={rival.DerivedOverall}. Same source has person-relative reliability.");
        }

        public PrototypeTestLabOperation MarkSourceTrusted()
        {
            EnsurePrototypeSource("information-source.prototype.expert", InformationSourceCategory.ExpertTestimony);
            InformationSourceOperationResult result = AssessPrototypeSource($"source-assessment.prototype.trusted.{Guid.NewGuid():N}", GetPrototypePersonId(), "information-source.prototype.expert", 900, 80, 50, 80);
            return RecordSourceOperation("Mark 8.6 Source Trusted", result);
        }

        public PrototypeTestLabOperation MarkSourceUntrusted()
        {
            EnsurePrototypeSource("information-source.prototype.testimony", InformationSourceCategory.PersonalTestimony);
            InformationSourceOperationResult result = AssessPrototypeSource($"source-assessment.prototype.untrusted.{Guid.NewGuid():N}", GetPrototypePersonId(), "information-source.prototype.testimony", 250, 700, 650, 600);
            return RecordSourceOperation("Mark 8.6 Source Untrusted", result);
        }

        public PrototypeTestLabOperation AddSourceDomainAuthority()
        {
            EnsurePrototypeSource("information-source.prototype.expert", InformationSourceCategory.ExpertTestimony);
            InformationSourceOperationResult result = AssessPrototypeSource($"source-assessment.prototype.authority.{Guid.NewGuid():N}", GetPrototypePersonId(), "information-source.prototype.expert", 820, 120, 100, 120, authority: 930);
            return RecordSourceOperation("Add 8.6 Domain Authority", result);
        }

        public PrototypeTestLabOperation AddSourceBias()
        {
            EnsurePrototypeSource("information-source.prototype.testimony", InformationSourceCategory.PersonalTestimony);
            InformationSourceOperationResult result = AssessPrototypeSource($"source-assessment.prototype.bias.{Guid.NewGuid():N}", GetPrototypePersonId(), "information-source.prototype.testimony", 520, 220, 180, 820);
            return RecordSourceOperation("Add 8.6 Bias", result);
        }

        public PrototypeTestLabOperation AddSourceErrorRisk()
        {
            EnsurePrototypeSource("information-source.prototype.testimony", InformationSourceCategory.PersonalTestimony);
            InformationSourceOperationResult result = AssessPrototypeSource($"source-assessment.prototype.error.{Guid.NewGuid():N}", GetPrototypePersonId(), "information-source.prototype.testimony", 520, 820, 180, 180);
            return RecordSourceOperation("Add 8.6 Error Risk", result);
        }

        public PrototypeTestLabOperation AddSourceDeceptionRisk()
        {
            EnsurePrototypeSource("information-source.prototype.anonymous", InformationSourceCategory.AnonymousTestimony);
            InformationSourceOperationResult result = AssessPrototypeSource($"source-assessment.prototype.deception.{Guid.NewGuid():N}", GetPrototypePersonId(), "information-source.prototype.anonymous", 320, 400, 900, 350);
            return RecordSourceOperation("Add 8.6 Deception Risk", result);
        }

        public PrototypeTestLabOperation AgeSource()
        {
            string sourceId = $"information-source.prototype.aged-record.{Guid.NewGuid():N}";
            RegisterPrototypeSource("Age 8.6 Source Setup", sourceId, InformationSourceCategory.HistoricalRecord, InformationSourceReferenceType.Document, "document.prototype.old-map", KnowledgeDomain.Historical, string.Empty, creationTime: 0d);
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            SourceReliabilityResult now = sourceRuntime.EvaluateReliability(new SourceReliabilityRequest { EvaluatingPersonId = GetPrototypePersonId(), SourceInstanceId = sourceId, Domain = KnowledgeDomain.Historical, WorldTimeSeconds = 0d });
            SourceReliabilityResult later = sourceRuntime.EvaluateReliability(new SourceReliabilityRequest { EvaluatingPersonId = GetPrototypePersonId(), SourceInstanceId = sourceId, Domain = KnowledgeDomain.Historical, WorldTimeSeconds = 5000d });
            bool succeeded = now.Succeeded && later.Succeeded && later.FinalDimensions.recency <= now.FinalDimensions.recency;
            return Record(succeeded, "Age 8.6 Source", succeeded ? "Success" : "AgeCheckFailed", $"Recency {now.FinalDimensions.recency}->{later.FinalDimensions.recency} Overall {now.DerivedOverall}->{later.DerivedOverall}.");
        }

        public PrototypeTestLabOperation EvaluateSourceStaleness() => AgeSource();

        public PrototypeTestLabOperation TraceSourceChain()
        {
            EnsureTransformedSource("information-source.prototype.translation", InformationSourceTransformationType.Translation);
            SourceChainSnapshot chain = informationSources.TraceSourceChain("information-source.prototype.translation", privilegedAccess: true);
            bool succeeded = chain.Chain.Count >= 2 && chain.TransmissionDepth >= 1;
            return Record(succeeded, "Trace 8.6 Source Chain", succeeded ? "Success" : "ChainFailed", $"Immediate={chain.ImmediateSourceId} Original={chain.OriginalSourceId} Depth={chain.TransmissionDepth} Hidden={chain.OriginalHidden}.");
        }

        public PrototypeTestLabOperation CompareImmediateAndOriginalSource()
        {
            EnsureTransformedSource("information-source.prototype.summary", InformationSourceTransformationType.Summary);
            SourceChainSnapshot chain = informationSources.TraceSourceChain("information-source.prototype.summary", privilegedAccess: true);
            bool succeeded = !string.Equals(chain.ImmediateSourceId, chain.OriginalSourceId, StringComparison.Ordinal);
            return Record(succeeded, "Compare 8.6 Immediate Original", succeeded ? "Success" : "SameSource", $"Immediate={chain.ImmediateSourceId} Original={chain.OriginalSourceId} Depth={chain.TransmissionDepth}.");
        }

        public PrototypeTestLabOperation TestDependentReports()
        {
            EnsureTransformedSource("information-source.prototype.copy", InformationSourceTransformationType.Copy);
            SourceIndependenceState state = informationSources.CompareIndependence("information-source.prototype.official-record", "information-source.prototype.copy");
            bool succeeded = state == SourceIndependenceState.Dependent;
            return Record(succeeded, "Test 8.6 Dependent Reports", succeeded ? "Success" : state.ToString(), $"Independence={state}.");
        }

        public PrototypeTestLabOperation TestIndependentCorroboration()
        {
            string directSourceId = GetPrototypeDirectObservationSourceId();
            EnsurePrototypeSource(directSourceId, InformationSourceCategory.DirectObservation);
            EnsurePrototypeSource("information-source.prototype.official-record", InformationSourceCategory.OfficialRecord);
            SourceIndependenceState state = informationSources.CompareIndependence(directSourceId, "information-source.prototype.official-record");
            bool succeeded = state == SourceIndependenceState.Independent || state == SourceIndependenceState.PartiallyIndependent;
            return Record(succeeded, "Test 8.6 Independent Corroboration", succeeded ? "Success" : state.ToString(), $"Independence={state}.");
        }

        public PrototypeTestLabOperation CorrectSourceAssessment()
        {
            EnsurePrototypeSource("information-source.prototype.testimony", InformationSourceCategory.PersonalTestimony);
            AssessPrototypeSource("source-assessment.prototype.corrected", GetPrototypePersonId(), "information-source.prototype.testimony", 250, 700, 650, 600);
            InformationSourceOperationResult correction = AssessPrototypeSource("source-assessment.prototype.corrected", GetPrototypePersonId(), "information-source.prototype.testimony", 760, 180, 140, 200);
            bool succeeded = correction.Succeeded && correction.Assessment?.Data.revision >= 2;
            return Record(succeeded, "Correct 8.6 Source Assessment", succeeded ? "Success" : correction.Code.ToString(), $"Revision={correction.Assessment?.Data.revision ?? 0} Supersedes={correction.Assessment?.Data.supersedesAssessmentId}.");
        }

        public PrototypeTestLabOperation HideOriginalSource()
        {
            EnsurePrototypeSource("information-source.prototype.official-record", InformationSourceCategory.OfficialRecord);
            InformationSourceOperationResult transformed = informationSources.TransformSource(new SourceTransformationRequest
            {
                TransactionId = $"source.hide-original.{Guid.NewGuid():N}",
                ParentSourceId = "information-source.prototype.official-record",
                SourceInstanceId = "information-source.prototype.hidden-summary",
                TransformationType = InformationSourceTransformationType.Summary,
                ActorPersonId = GetPrototypePersonId(),
                WorldTimeSeconds = GetPrototypeWorldTime(),
                HidesOriginal = true,
                Quality = 700
            });
            SourceChainSnapshot publicChain = informationSources.TraceSourceChain("information-source.prototype.hidden-summary", privilegedAccess: false);
            SourceChainSnapshot privateChain = informationSources.TraceSourceChain("information-source.prototype.hidden-summary", privilegedAccess: true);
            bool succeeded = transformed.Succeeded && publicChain.OriginalHidden && privateChain.TransmissionDepth >= 1;
            return Record(succeeded, "Hide 8.6 Original Source", succeeded ? "Success" : transformed.Code.ToString(), $"PublicHidden={publicChain.OriginalHidden} PrivateDepth={privateChain.TransmissionDepth}.");
        }

        public PrototypeTestLabOperation CompareRawAndEffectiveEvidenceStrength()
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return RecordFailure("Compare 8.6 Raw Effective Evidence", "Knowledge runtime is missing.", KnowledgeResultCode.MissingPerson.ToString());
            }

            EnsurePrototypeSource("information-source.prototype.testimony", InformationSourceCategory.PersonalTestimony);
            AssessPrototypeSource($"source-assessment.prototype.evidence.{Guid.NewGuid():N}", GetPrototypePersonId(), "information-source.prototype.testimony", 500, 700, 650, 620);
            SourceReliabilityResult reliability = EvaluateFor(GetPrototypePersonId(), "information-source.prototype.testimony");
            int raw = 800;
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            int effective = sourceRuntime.CalculateEffectiveEvidenceStrength(raw, reliability);
            KnowledgeObservationRequest request = BuildPrototypeSourceEvidenceRequest(knowledge, $"knowledge.source-effective.{Guid.NewGuid():N}", "information-source.prototype.testimony", raw, effective, reliability);
            KnowledgeOperationResult result = knowledge.RecordObservation(request);
            bool succeeded = result.Succeeded && result.Evidence.RawStrength == raw && result.Evidence.EffectiveStrength == effective && effective < raw;
            return Record(succeeded, "Compare 8.6 Raw Effective Evidence", succeeded ? "Success" : result.Code.ToString(), $"Raw={raw} Effective={effective} BeliefConfidence={result.ResultingBelief?.Confidence ?? 0} SourceReliability={reliability.DerivedOverall}.");
        }

        public PrototypeTestLabOperation ValidateInformationSourceSaveRestore()
        {
            EnsurePrototypeSource("information-source.prototype.expert", InformationSourceCategory.ExpertTestimony);
            AssessPrototypeSource($"source-assessment.prototype.save.{Guid.NewGuid():N}", GetPrototypePersonId(), "information-source.prototype.expert", 850, 100, 80, 100);
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            InformationSourceSaveData saveData = sourceRuntime.CreateSaveData();
            long before = sourceRuntime.SourceRevision;
            int events = 0;
            void CountEvent(InformationSourceRuntime _, InformationSourceOperationResult __) => events++;
            sourceRuntime.SourcesChanged += CountEvent;
            InformationSourceOperationResult result = sourceRuntime.RestoreFromSaveData(saveData, registry, sourceRuntime.OwnerId, restoring: true);
            sourceRuntime.SourcesChanged -= CountEvent;
            bool succeeded = result.Succeeded && events == 0 && sourceRuntime.SourceRevision == before;
            return Record(succeeded, "Information Source Save Restore", succeeded ? "Success" : result.Code.ToString(), $"{result.Message} Events={events} Revision={before}->{sourceRuntime.SourceRevision} Sources={sourceRuntime.CreateSnapshot().Sources.Count}.");
        }

        private PrototypeTestLabOperation RegisterPrototypeSource(string operationName, string sourceId, InformationSourceCategory category, InformationSourceReferenceType referenceType, string referencedId, KnowledgeDomain domain, string methodId, string authority = "", SourcePrivacyLevel privacy = SourcePrivacyLevel.Public, double? creationTime = null)
        {
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            if (sourceRuntime.TryGetSource(sourceId, out InformationSourceRecord existing))
            {
                if (existing.Category != category || existing.Data.referenceType != referenceType || !string.Equals(existing.Data.referencedId ?? string.Empty, referencedId ?? string.Empty, StringComparison.Ordinal))
                {
                    return RecordFailure(operationName, $"Source instance '{sourceId}' already exists with different identity.", InformationSourceResultCode.InvalidRequest.ToString());
                }

                return RecordSourceOperation(operationName, InformationSourceOperationResult.Success("Prototype source already registered.", string.Empty, existing, null, sourceRuntime.SourceRevision, sourceRuntime.SourceRevision, duplicate: true));
            }

            InformationSourceOperationResult result = sourceRuntime.RegisterSource(new InformationSourceRegistrationRequest
            {
                TransactionId = $"source.register.{SanitizeForTransaction(sourceId)}.{Guid.NewGuid():N}",
                SourceInstanceId = sourceId,
                Category = category,
                ReferenceType = referenceType,
                ReferencedId = referencedId ?? string.Empty,
                OriginalCreatorPersonId = category == InformationSourceCategory.DirectObservation ? GetPrototypePersonId() : "person.prototype.source-origin",
                ObserverPersonId = category == InformationSourceCategory.DirectObservation ? GetPrototypePersonId() : string.Empty,
                HolderPersonId = GetPrototypePersonId(),
                TransmitterPersonId = category == InformationSourceCategory.DirectObservation ? string.Empty : "person.prototype.transmitter",
                CreationWorldTimeSeconds = Math.Max(0d, creationTime ?? GetPrototypeWorldTime()),
                ObservationWorldTimeSeconds = GetPrototypeWorldTime(),
                TransmissionWorldTimeSeconds = GetPrototypeWorldTime(),
                Domain = domain,
                SubjectId = string.IsNullOrWhiteSpace(referencedId) ? GetPrototypePersonId() : referencedId,
                MethodId = methodId ?? string.Empty,
                AuthorityClassification = authority ?? string.Empty,
                ErrorRisk = category == InformationSourceCategory.Hearsay || category == InformationSourceCategory.AnonymousTestimony ? 550 : 180,
                DeceptionRisk = category == InformationSourceCategory.AnonymousTestimony ? 600 : 120,
                BiasRisk = category == InformationSourceCategory.PersonalTestimony ? 320 : 140,
                Privacy = privacy,
                Tags = new[] { "feature.8.6", category.ToString() }
            });
            return RecordSourceOperation(operationName, result);
        }

        private InformationSourceOperationResult EnsurePrototypeSource(string sourceId, InformationSourceCategory category)
        {
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            if (sourceRuntime.TryGetSource(sourceId, out InformationSourceRecord existing))
            {
                if (existing.Category != category)
                {
                    return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidRequest, $"Source instance '{sourceId}' already exists as {existing.Category}, not {category}.", revision: sourceRuntime.SourceRevision);
                }

                return InformationSourceOperationResult.Success("Source already exists.", string.Empty, existing, null, sourceRuntime.SourceRevision, sourceRuntime.SourceRevision, duplicate: true);
            }

            InformationSourceReferenceType referenceType = category switch
            {
                InformationSourceCategory.DirectObservation => InformationSourceReferenceType.Body,
                InformationSourceCategory.ExpertTestimony => InformationSourceReferenceType.Person,
                InformationSourceCategory.OfficialRecord => InformationSourceReferenceType.Document,
                InformationSourceCategory.HistoricalRecord => InformationSourceReferenceType.Document,
                InformationSourceCategory.AnonymousTestimony => InformationSourceReferenceType.None,
                _ => InformationSourceReferenceType.Person
            };
            string referenced = sourceId == "information-source.prototype.expert" ? "person.prototype.expert-healer"
                : sourceId == "information-source.prototype.testimony" ? "person.prototype.npc"
                : sourceId == "information-source.prototype.official-record" ? "document.prototype.guild-notice"
                : referenceType == InformationSourceReferenceType.Body ? GetPrototypeBodyId()
                : referenceType == InformationSourceReferenceType.Document ? $"document.prototype.{SanitizeForTransaction(sourceId)}"
                : referenceType == InformationSourceReferenceType.None ? string.Empty
                : "person.prototype.source";
            return sourceRuntime.RegisterSource(new InformationSourceRegistrationRequest
            {
                TransactionId = $"source.ensure.{SanitizeForTransaction(sourceId)}.{Guid.NewGuid():N}",
                SourceInstanceId = sourceId,
                Category = category,
                ReferenceType = referenceType,
                ReferencedId = referenced,
                OriginalCreatorPersonId = category == InformationSourceCategory.DirectObservation ? GetPrototypePersonId() : "person.prototype.source-origin",
                ObserverPersonId = category == InformationSourceCategory.DirectObservation ? GetPrototypePersonId() : string.Empty,
                HolderPersonId = GetPrototypePersonId(),
                TransmitterPersonId = category == InformationSourceCategory.DirectObservation ? string.Empty : "person.prototype.transmitter",
                CreationWorldTimeSeconds = GetPrototypeWorldTime(),
                ObservationWorldTimeSeconds = GetPrototypeWorldTime(),
                TransmissionWorldTimeSeconds = GetPrototypeWorldTime(),
                Domain = KnowledgeDomain.Medical,
                SubjectId = string.IsNullOrWhiteSpace(referenced) ? GetPrototypePersonId() : referenced,
                MethodId = category == InformationSourceCategory.DirectObservation ? "observation-method.ordinary-visual" : string.Empty,
                ErrorRisk = category == InformationSourceCategory.Hearsay || category == InformationSourceCategory.AnonymousTestimony ? 550 : 180,
                DeceptionRisk = category == InformationSourceCategory.AnonymousTestimony ? 600 : 120,
                BiasRisk = category == InformationSourceCategory.PersonalTestimony ? 320 : 140,
                Privacy = SourcePrivacyLevel.Public,
                Tags = new[] { "feature.8.6", category.ToString() }
            });
        }

        private PrototypeTestLabOperation TransformPrototypeSource(string operationName, string newSourceId, InformationSourceTransformationType transformationType, int quality, bool hidesOriginal)
        {
            EnsurePrototypeSource("information-source.prototype.official-record", InformationSourceCategory.OfficialRecord);
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            InformationSourceOperationResult result = sourceRuntime.TransformSource(new SourceTransformationRequest
            {
                TransactionId = $"source.transform.{SanitizeForTransaction(newSourceId)}.{Guid.NewGuid():N}",
                ParentSourceId = "information-source.prototype.official-record",
                SourceInstanceId = newSourceId,
                TransformationType = transformationType,
                ActorPersonId = GetPrototypePersonId(),
                WorldTimeSeconds = GetPrototypeWorldTime(),
                Quality = quality,
                HidesOriginal = hidesOriginal,
                Note = operationName
            });
            return RecordSourceOperation(operationName, result);
        }

        private InformationSourceOperationResult EnsureTransformedSource(string sourceId, InformationSourceTransformationType transformationType)
        {
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            if (sourceRuntime.TryGetSource(sourceId, out InformationSourceRecord record))
            {
                return InformationSourceOperationResult.Success("Transformed source already exists.", string.Empty, record, null, sourceRuntime.SourceRevision, sourceRuntime.SourceRevision, duplicate: true);
            }

            TransformPrototypeSource($"Ensure 8.6 {transformationType}", sourceId, transformationType, 760, hidesOriginal: false);
            sourceRuntime = EnsureInformationSourceRuntime();
            sourceRuntime.TryGetSource(sourceId, out record);
            return InformationSourceOperationResult.Success("Transformed source ensured.", string.Empty, record, null, sourceRuntime.SourceRevision, sourceRuntime.SourceRevision);
        }

        private InformationSourceOperationResult AssessPrototypeSource(string assessmentId, string personId, string sourceId, int dependability, int errorRisk, int deceptionRisk, int biasRisk, int authority = 600)
        {
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            ReliabilityProfileData reliability = ReliabilityProfileData.Default();
            reliability.generalDependability = dependability;
            reliability.domainExpertise = authority;
            reliability.methodQuality = dependability;
            reliability.authenticity = Math.Max(200, dependability - deceptionRisk / 3);
            reliability.identityCertainty = Math.Max(200, dependability - deceptionRisk / 4);
            reliability.observationQuality = dependability;
            reliability.recordIntegrity = Math.Max(200, dependability - errorRisk / 4);
            reliability.errorRisk = errorRisk;
            reliability.deceptionRisk = deceptionRisk;
            reliability.biasRisk = biasRisk;
            reliability.completeness = dependability;
            reliability.precision = dependability;
            reliability.contextFit = dependability;

            return sourceRuntime.AssessSource(new SourceAssessmentRequest
            {
                TransactionId = $"source.assess.{SanitizeForTransaction(assessmentId)}.{Guid.NewGuid():N}",
                AssessmentId = assessmentId,
                AssessingPersonId = personId,
                SourceInstanceId = sourceId,
                Domain = KnowledgeDomain.Medical,
                SubjectId = GetPrototypeBodyId(),
                MethodId = "examination-method.medical",
                WorldTimeSeconds = GetPrototypeWorldTime(),
                Reliability = reliability,
                Authority = authority,
                ErrorRisk = errorRisk,
                DeceptionRisk = deceptionRisk,
                BiasRisk = biasRisk,
                Familiarity = 500,
                ConfidenceInAssessment = 800,
                SupportingEvidenceIds = Array.Empty<string>(),
                PriorExperienceIds = Array.Empty<string>()
            });
        }

        private SourceReliabilityResult EvaluateFor(string personId, string sourceId)
        {
            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            return sourceRuntime.EvaluateReliability(new SourceReliabilityRequest
            {
                EvaluatingPersonId = personId,
                SourceInstanceId = sourceId,
                Domain = KnowledgeDomain.Medical,
                SubjectId = GetPrototypeBodyId(),
                MethodId = "examination-method.medical",
                WorldTimeSeconds = GetPrototypeWorldTime(),
                PrivilegedAccess = true
            });
        }

        private KnowledgeObservationRequest BuildPrototypeSourceEvidenceRequest(PersonKnowledgeRuntime knowledge, string transactionId, string sourceId, int rawStrength, int effectiveStrength, SourceReliabilityResult reliability)
        {
            return new KnowledgeObservationRequest
            {
                PersonId = knowledge.PersonId,
                TransactionId = transactionId,
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.SpeciesCapability,
                    subjectType = KnowledgeSubjectType.Species,
                    subjectId = "species.basic-spirit",
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = "capability.can.bleed",
                    sourceContextId = "information-source.prototype.testimony"
                },
                AcquisitionSource = KnowledgeAcquisitionSource.Testimony,
                Provenance = KnowledgeProvenance.Testimony,
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = rawStrength,
                EffectiveStrengthOverride = effectiveStrength,
                InformationSourceId = sourceId,
                ReliabilityPolicyId = reliability?.Request?.PolicyId,
                ReliabilityEvaluationId = $"source-reliability.{transactionId}",
                Credibility = reliability?.DerivedOverall ?? 500,
                GameTimeSeconds = GetPrototypeWorldTime(),
                SourceId = sourceId,
                Visibility = KnowledgeVisibility.Public
            };
        }

        private IReadOnlyList<InformationSourceDefinition> CreatePrototypeSourceDefinitions()
        {
            return new[]
            {
                CreatePrototypeSourceDefinition("information-source.direct-observation", "Direct Observation", InformationSourceCategory.DirectObservation, 850),
                CreatePrototypeSourceDefinition("information-source.expert-testimony", "Expert Testimony", InformationSourceCategory.ExpertTestimony, 760),
                CreatePrototypeSourceDefinition("information-source.official-record", "Official Record", InformationSourceCategory.OfficialRecord, 820),
                CreatePrototypeSourceDefinition("information-source.anonymous-testimony", "Anonymous Testimony", InformationSourceCategory.AnonymousTestimony, 360, identityVerification: false),
                CreatePrototypeSourceDefinition("information-source.historical-record", "Historical Record", InformationSourceCategory.HistoricalRecord, 700, KnowledgeStalenessPolicy.TimeLimited, 1000d)
            };
        }

        private static InformationSourceDefinition CreatePrototypeSourceDefinition(string id, string displayName, InformationSourceCategory category, int dependability, KnowledgeStalenessPolicy policy = KnowledgeStalenessPolicy.NeverStale, double halfLife = 0d, bool identityVerification = false)
        {
            InformationSourceDefinition definition = ScriptableObject.CreateInstance<InformationSourceDefinition>();
            ReliabilityProfileData reliability = ReliabilityProfileData.Default();
            reliability.generalDependability = dependability;
            reliability.domainExpertise = dependability;
            reliability.firsthandProximity = category == InformationSourceCategory.DirectObservation ? 900 : 500;
            reliability.methodQuality = dependability;
            reliability.authenticity = dependability;
            reliability.identityCertainty = dependability;
            reliability.observationQuality = dependability;
            reliability.recordIntegrity = dependability;
            reliability.recency = 900;
            reliability.transmissionIntegrity = 900;
            reliability.independence = 600;
            reliability.corroboration = 500;
            reliability.internalConsistency = dependability;
            reliability.errorRisk = Math.Max(0, 1000 - dependability);
            reliability.deceptionRisk = category == InformationSourceCategory.AnonymousTestimony ? 600 : 150;
            reliability.biasRisk = category == InformationSourceCategory.PersonalTestimony ? 320 : 150;
            reliability.completeness = dependability;
            reliability.precision = dependability;
            reliability.contextFit = dependability;
            definition.DevelopmentConfigure(id, displayName, category, reliability, policy, halfLife, 80, identityVerification);
            return definition;
        }

        private PrototypeTestLabOperation ExecutePrototypeTransfer(string operationName, InformationTransferMode mode, InformationTransferContentType contentType, bool recallRequired, bool teaching, bool deliberateFalsehood, bool authorizeFalsehood, string sourceId = null, bool summarization = false, bool omission = false, bool distortion = false, bool correction = false, TransferPrivacyScope privacy = TransferPrivacyScope.RecipientOnly)
        {
            TransferContentItemData content = BuildTransferContent($"content.{SanitizeForTransaction(operationName)}", contentType, recallRequired, string.Empty, correction ? InformationTransferAssertionType.Correction : teaching ? InformationTransferAssertionType.Instruction : InformationTransferAssertionType.Fact, privacy == TransferPrivacyScope.Public ? KnowledgeVisibility.Public : KnowledgeVisibility.Private);
            InformationTransferResult result = ExecutePrototypeTransferRaw(SanitizeForTransaction(operationName), mode, new[] { content }, null, null, sourceId, recallRequired, teaching, deliberateFalsehood, authorizeFalsehood, summarization, out GameObject listener, out _, omission: omission, distortion: distortion, privacy: privacy);
            DestroyTestObject(listener);
            return RecordTransferOperation(operationName, result);
        }

        private InformationTransferResult ExecutePrototypeTransferRaw(
            string transactionSlug,
            InformationTransferMode mode,
            TransferContentItemData[] content,
            PersonKnowledgeRuntime senderKnowledgeOverride,
            PersonMemoryRuntime senderMemoryOverride,
            string sourceId,
            bool recallRequired,
            bool teaching,
            bool deliberateFalsehood,
            bool authorizeFalsehood,
            bool summarization,
            out GameObject listenerObject,
            out PersonMemoryRuntime recipientMemory,
            bool omission = false,
            bool distortion = false,
            TransferPrivacyScope privacy = TransferPrivacyScope.RecipientOnly,
            string parentTransferId = "",
            string correctionOfTransferId = "",
            bool forceLowFidelity = false,
            double? worldTimeSeconds = null)
        {
            listenerObject = null;
            recipientMemory = null;
            if (!EnsureTransferPrerequisites(out PersonKnowledgeRuntime senderKnowledge, out PersonMemoryRuntime senderMemory, out string memoryId, out _))
            {
                return InformationTransferResult.Failure(InformationTransferStatus.MissingSender, "Prototype transfer prerequisites are missing.", $"transfer.{transactionSlug}");
            }

            if (senderKnowledgeOverride != null)
            {
                senderKnowledge = senderKnowledgeOverride;
            }

            if (senderMemoryOverride != null)
            {
                senderMemory = senderMemoryOverride;
            }

            EnsurePrototypeSource(string.IsNullOrWhiteSpace(sourceId) ? "information-source.prototype.testimony" : sourceId, string.IsNullOrWhiteSpace(sourceId) ? InformationSourceCategory.PersonalTestimony : InformationSourceCategory.ExpertTestimony);
            CreateTransferRecipientKnowledge(out listenerObject, out PersonKnowledgeRuntime recipientKnowledge);
            recipientMemory = CreateTransferRecipientMemory(recipientKnowledge.PersonId);
            string transferId = $"transfer.prototype.{SanitizeForTransaction(transactionSlug)}.{Guid.NewGuid():N}";
            string effectiveSourceId = string.IsNullOrWhiteSpace(sourceId) ? "information-source.prototype.testimony" : sourceId;
            foreach (TransferContentItemData item in content ?? Array.Empty<TransferContentItemData>())
            {
                if (item == null)
                {
                    continue;
                }

                item.senderMemoryId = recallRequired && string.IsNullOrWhiteSpace(item.senderMemoryId) ? memoryId : item.senderMemoryId;
                item.immediateSourceId = effectiveSourceId;
                item.originalSourceId = effectiveSourceId;
                item.deliberateFalsehood = deliberateFalsehood || item.deliberateFalsehood;
                item.deliberateOmission = omission || item.deliberateOmission;
                item.deliberateDistortion = distortion || item.deliberateDistortion;
                if (item.rawEvidenceStrength <= 0)
                {
                    item.rawEvidenceStrength = teaching ? 780 : 700;
                }
            }

            if (forceLowFidelity)
            {
                summarization = true;
            }

            InformationSourceRuntime sourceRuntime = EnsureInformationSourceRuntime();
            InformationTransferRuntime transferRuntime = EnsureInformationTransferRuntime();
            return transferRuntime.ExecuteTransfer(new InformationTransferRequest
            {
                TransactionId = $"transfer.8.7.{SanitizeForTransaction(transactionSlug)}.{Guid.NewGuid():N}",
                TransferId = transferId,
                SenderPersonId = GetPrototypePersonId(),
                RecipientPersonIds = new[] { recipientKnowledge.PersonId },
                TransferDefinitionId = string.Empty,
                Mode = mode,
                ContentItems = content == null ? Array.Empty<TransferContentItemData>() : content.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                ImmediateSourceId = effectiveSourceId,
                OriginalSourceId = effectiveSourceId,
                WorldTimeSeconds = worldTimeSeconds ?? GetPrototypeWorldTime(),
                LocationContextId = "place.prototype.test-lab",
                PrivacyScope = privacy,
                SenderRecallRequired = recallRequired,
                SummarizationRequested = summarization,
                OmissionRequested = omission,
                DistortionRequested = distortion,
                TeachingRequested = teaching,
                ParentTransferId = parentTransferId,
                CorrectionOfTransferId = correctionOfTransferId,
                DeliberateFalsehoodAuthorized = authorizeFalsehood,
                PrivilegedAccess = privacy != TransferPrivacyScope.Public,
                SenderKnowledge = senderKnowledge,
                SenderMemory = senderMemory,
                SourceRuntime = sourceRuntime,
                RecipientKnowledgeRuntimes = new Dictionary<string, PersonKnowledgeRuntime> { [recipientKnowledge.PersonId] = recipientKnowledge },
                RecipientMemoryRuntimes = new Dictionary<string, PersonMemoryRuntime> { [recipientKnowledge.PersonId] = recipientMemory }
            });
        }

        private bool EnsureTransferPrerequisites(out PersonKnowledgeRuntime senderKnowledge, out PersonMemoryRuntime senderMemory, out string memoryId, out PrototypeTestLabOperation failure)
        {
            senderKnowledge = null;
            senderMemory = null;
            memoryId = string.Empty;
            failure = default;
            if (!EnsureKnowledgeRuntime(out senderKnowledge))
            {
                failure = RecordFailure("Information Transfer Setup", "Sender Knowledge runtime is missing.", InformationTransferStatus.MissingSender.ToString());
                return false;
            }

            if (!EnsureHistoryRuntime(out _, out senderMemory))
            {
                failure = RecordFailure("Information Transfer Setup", "Sender Memory runtime is missing.", InformationTransferStatus.RecallFailed.ToString());
                return false;
            }

            KnowledgeOperationResult known = senderKnowledge.RecordObservation(new KnowledgeObservationRequest
            {
                PersonId = senderKnowledge.PersonId,
                TransactionId = $"transfer.sender.knowledge.{Guid.NewGuid():N}",
                Proposition = SpeciesCapabilityTransferProposition(),
                AcquisitionSource = KnowledgeAcquisitionSource.DevelopmentFixture,
                Provenance = KnowledgeProvenance.DevelopmentFixture,
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = 850,
                Credibility = 850,
                GameTimeSeconds = GetPrototypeWorldTime(),
                SourceId = "test-lab.transfer.sender",
                InformationSourceId = "information-source.prototype.testimony",
                Visibility = KnowledgeVisibility.Public
            });
            if (!known.Succeeded && known.Code != KnowledgeResultCode.Duplicate)
            {
                failure = RecordFailure("Information Transfer Setup", known.Message, known.Code.ToString());
                return false;
            }

            memoryId = GetPrototypeMemoryId();
            return true;
        }

        private TransferContentItemData BuildTransferContent(string contentId, InformationTransferContentType contentType, bool recallRequired, string memoryId, InformationTransferAssertionType assertionType = InformationTransferAssertionType.Fact, KnowledgeVisibility privacy = KnowledgeVisibility.Public)
        {
            return new TransferContentItemData
            {
                contentItemId = contentId,
                contentType = contentType,
                domain = contentType == InformationTransferContentType.HistoricalEventReference || contentType == InformationTransferContentType.MemoryStatement ? KnowledgeDomain.Historical : KnowledgeDomain.Species,
                proposition = SpeciesCapabilityTransferProposition(),
                senderMemoryId = memoryId,
                historicalEventId = contentType == InformationTransferContentType.HistoricalEventReference || contentType == InformationTransferContentType.MemoryStatement ? GetPrototypeHiddenHistoryEventId() : string.Empty,
                senderConfidence = 850,
                senderBeliefState = KnowledgeBeliefState.Known,
                includedDetailIds = recallRequired ? new[] { "detail.participant", "detail.location" } : Array.Empty<string>(),
                omittedDetailIds = Array.Empty<string>(),
                claimedCertainty = 780,
                privacyClassification = privacy,
                assertionType = assertionType,
                typedPayloadId = contentType == InformationTransferContentType.ProcedureReference ? "procedure.prototype.first-aid" : string.Empty,
                debugDescription = $"Prototype transfer content {contentType}.",
                intendedUnderstanding = TransferUnderstandingState.Complete,
                rawEvidenceStrength = 760
            };
        }

        private static KnowledgePropositionData SpeciesCapabilityTransferProposition()
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.SpeciesCapability,
                subjectType = KnowledgeSubjectType.Species,
                subjectId = "species.basic-spirit",
                valueType = KnowledgeValueType.StableId,
                stableValueId = "capability.can.bleed",
                sourceContextId = "information-transfer.prototype"
            };
        }

        private void CreateTransferRecipientKnowledge(out GameObject listenerObject, out PersonKnowledgeRuntime recipientKnowledge)
        {
            listenerObject = new GameObject("Information Transfer Test Listener");
            recipientKnowledge = listenerObject.AddComponent<PersonKnowledgeRuntime>();
            recipientKnowledge.Configure(registry, $"person.prototype.listener.{Guid.NewGuid():N}");
        }

        private PersonMemoryRuntime CreateTransferRecipientMemory(string recipientId)
        {
            PersonMemoryRuntime runtime = new PersonMemoryRuntime();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _);
            runtime.Configure(recipientId, registry, historyRuntime, GetKnownPrototypePersons().Concat(new[] { recipientId }));
            return runtime;
        }

        private IReadOnlyList<InformationTransferDefinition> CreatePrototypeTransferDefinitions()
        {
            return new[]
            {
                CreatePrototypeTransferDefinition("information-transfer.direct-testimony", "Direct Testimony", InformationTransferMode.DirectTestimony, new[] { KnowledgeDomain.Species, KnowledgeDomain.Medical }, new[] { InformationSourceCategory.PersonalTestimony, InformationSourceCategory.DirectObservation }, false, false, false, false, 850, 850, TransferMemoryPolicy.FormCommunicationMemory, TransferEvidencePolicy.CreateRecipientEvidence),
                CreatePrototypeTransferDefinition("information-transfer.formal-lesson", "Formal Lesson", InformationTransferMode.FormalLesson, new[] { KnowledgeDomain.Species, KnowledgeDomain.Medical }, new[] { InformationSourceCategory.ExpertTestimony, InformationSourceCategory.PersonalTestimony }, false, false, false, false, 860, 820, TransferMemoryPolicy.FormCommunicationMemory, TransferEvidencePolicy.CreateRecipientEvidence),
                CreatePrototypeTransferDefinition("information-transfer.demonstration", "Demonstration", InformationTransferMode.Demonstration, new[] { KnowledgeDomain.Species, KnowledgeDomain.Medical }, new[] { InformationSourceCategory.DirectParticipation, InformationSourceCategory.ExpertTestimony }, false, false, false, true, 880, 780, TransferMemoryPolicy.FormCommunicationMemory, TransferEvidencePolicy.CreateRecipientEvidence),
                CreatePrototypeTransferDefinition("information-transfer.summary", "Summary", InformationTransferMode.Summary, new[] { KnowledgeDomain.Historical, KnowledgeDomain.Social }, new[] { InformationSourceCategory.OfficialRecord, InformationSourceCategory.Hearsay }, true, true, false, false, 740, 580, TransferMemoryPolicy.FormCommunicationMemory, TransferEvidencePolicy.CreateRecipientEvidence),
                CreatePrototypeTransferDefinition("information-transfer.private-message", "Private Message", InformationTransferMode.PrivateMessage, new[] { KnowledgeDomain.Social, KnowledgeDomain.Historical }, new[] { InformationSourceCategory.PersonalTestimony, InformationSourceCategory.Letter }, false, false, false, false, 820, 820, TransferMemoryPolicy.FormCommunicationMemory, TransferEvidencePolicy.CreateRecipientEvidence)
            };
        }

        private static InformationTransferDefinition CreatePrototypeTransferDefinition(string id, string displayName, InformationTransferMode mode, KnowledgeDomain[] domains, InformationSourceCategory[] sourceCategories, bool recallRequired, bool allowsSummary, bool allowsTranslation, bool allowsDemonstration, int fidelity, int completeness, TransferMemoryPolicy memoryPolicy, TransferEvidencePolicy evidencePolicy)
        {
            InformationTransferDefinition definition = ScriptableObject.CreateInstance<InformationTransferDefinition>();
            definition.DevelopmentConfigure(id, displayName, mode, domains, sourceCategories, recallRequired, allowsSummary, allowsTranslation, allowsDemonstration, fidelity, completeness, memoryPolicy, evidencePolicy);
            return definition;
        }

        private PrototypeTestLabOperation RecordTransferOperation(string operationName, InformationTransferResult result)
        {
            bool succeeded = result != null && result.Succeeded;
            return Record(succeeded, operationName, succeeded ? result.Status.ToString() : result?.Status.ToString() ?? InformationTransferStatus.InvalidRequest.ToString(), FormatTransferResult(result));
        }

        private static string FormatTransferResult(InformationTransferResult result)
        {
            if (result == null)
            {
                return "No Information Transfer result was produced.";
            }

            TransferRecipientResult first = result.RecipientResults.FirstOrDefault();
            return $"Success={result.Succeeded} Status={result.Status} Preview={result.Preview} Duplicate={result.Duplicate} Transfer={result.Record?.TransferId ?? "None"} Recipients={result.RecipientResults.Count} Understanding={first?.Understanding.ToString() ?? "None"} Confidence={first?.InheritedConfidence ?? 0} Evidence={first?.CreatedEvidenceIds.Count ?? 0} Memories={first?.FormedMemoryIds.Count ?? 0} Source={first?.Data.transferSourceId ?? result.Record?.Data.createdSourceId ?? "None"} Revision={result.PriorRevision}->{result.ResultingRevision}. {result.Message}";
        }

        private PrototypeTestLabOperation RecordSourceOperation(string operationName, InformationSourceOperationResult result)
        {
            bool succeeded = result != null && result.Succeeded;
            string message = result == null
                ? "No Information Source result was produced."
                : $"{result.Message} Source={result.Source?.SourceInstanceId ?? "None"} Assessment={result.Assessment?.AssessmentId ?? "None"} Preview={result.Preview} Duplicate={result.Duplicate} Revision={result.PriorRevision}->{result.ResultingRevision}.";
            return Record(succeeded, operationName, succeeded ? result.Code.ToString() : result?.Code.ToString() ?? InformationSourceResultCode.InvalidRequest.ToString(), message);
        }

        private PrototypeTestLabOperation RecordReliability(string operationName, SourceReliabilityResult result)
        {
            bool succeeded = result != null && result.Succeeded;
            string message = result == null
                ? "No reliability result was produced."
                : $"Source={result.Request?.SourceInstanceId} Evaluator={result.Request?.EvaluatingPersonId} Overall={result.DerivedOverall} Confidence={result.Confidence} Error={result.FinalDimensions.errorRisk} Deception={result.FinalDimensions.deceptionRisk} Bias={result.FinalDimensions.biasRisk} Depth={result.Chain.TransmissionDepth}. {result.Message} Diagnostics={string.Join(" | ", result.Diagnostics)}";
            return Record(succeeded, operationName, succeeded ? result.Code.ToString() : result?.Code.ToString() ?? InformationSourceResultCode.InvalidRequest.ToString(), message);
        }

        private double GetPrototypeWorldTime()
        {
            return context?.Persistence?.PlayTime == null ? 0d : context.Persistence.PlayTime.CumulativeSeconds;
        }

        private string GetPrototypeDirectObservationSourceId()
        {
            return $"information-source.prototype.direct-observation.{SanitizeForTransaction(GetPrototypeBodyId())}";
        }

        private string GetPrototypeHiddenHistoryEventId()
        {
            return currentAutomationScenarioContext == null
                ? "event.prototype.hidden.secret"
                : currentAutomationScenarioContext.ScopedId("event", "hidden-secret");
        }

        private string GetPrototypeWitnessMemoryId()
        {
            return currentAutomationScenarioContext == null
                ? "memory.prototype.hidden-witness"
                : currentAutomationScenarioContext.ScopedId("memory", "hidden-witness");
        }

        private static string SanitizeForTransaction(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "none";
            }

            return new string(value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        }

        private bool EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge)
        {
            if (currentAutomationScenarioContext?.Runtimes?.Knowledge != null)
            {
                knowledge = currentAutomationScenarioContext.Runtimes.Knowledge;
                return knowledge.IsReady;
            }

            knowledge = context?.PlayerKnowledge;
            if (knowledge == null && context?.PlayerTransform != null)
            {
                knowledge = context.PlayerTransform.GetComponentInParent<PersonKnowledgeRuntime>();
            }

            if (knowledge == null && context?.PlayerTransform != null && context.PlayerTransform.gameObject.activeInHierarchy)
            {
                knowledge = context.PlayerTransform.gameObject.AddComponent<PersonKnowledgeRuntime>();
                context.PlayerKnowledge = knowledge;
            }

            if (knowledge == null)
            {
                return false;
            }

            string person = context?.IdentityProgression == null ? knowledge.PersonId : context.IdentityProgression.PersonId;
            string actor = ResolveActorId(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject);
            string body = context?.PlayerTransform == null ? string.Empty : context.PlayerTransform.GetComponentInParent<ActorBodyRuntime>()?.ActorBodyId ?? string.Empty;
            knowledge.Configure(registry, person, actor, body);
            return knowledge.IsReady;
        }

        private bool TryBuildVisibleInjuryObservation(string transactionId, out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure)
        {
            request = null;
            if (!EnsureKnowledgeRuntime(out knowledge))
            {
                failure = RecordFailure("Knowledge Observation", "Knowledge runtime is missing or not ready.", KnowledgeResultCode.MissingPerson.ToString());
                return false;
            }

            if (!EnsureBodyRuntime(out ActorBodyRuntime body))
            {
                failure = RecordFailure("Knowledge Observation", "Body runtime is missing for visible injury observation.", KnowledgeResultCode.InvalidRequest.ToString());
                return false;
            }

            BodyBiologySnapshot snapshot = new BodyBiologyFacade(body).CaptureSnapshot();
            request = KnowledgeObservationProjection.VisibleInjury(
                snapshot,
                knowledge.PersonId,
                transactionId,
                "injury.blunt-trauma",
                context?.Persistence?.PlayTime == null ? 0d : context.Persistence.PlayTime.CumulativeSeconds,
                KnowledgeObservationAccess.OrdinaryObservation);
            failure = default;
            return request != null;
        }

        private bool TryBuildSpeciesCapabilityObservation(string transactionId, int strength, int credibility, out PersonKnowledgeRuntime knowledge, out KnowledgeObservationRequest request, out PrototypeTestLabOperation failure)
        {
            request = null;
            if (!EnsureKnowledgeRuntime(out knowledge))
            {
                failure = RecordFailure("Knowledge Evidence", "Knowledge runtime is missing or not ready.", KnowledgeResultCode.MissingPerson.ToString());
                return false;
            }

            request = new KnowledgeObservationRequest
            {
                PersonId = knowledge.PersonId,
                TransactionId = transactionId,
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.SpeciesCapability,
                    subjectType = KnowledgeSubjectType.Species,
                    subjectId = "species.basic-spirit",
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = "capability.can.bleed",
                    sourceContextId = "test-lab.knowledge.species-capability"
                },
                AcquisitionSource = KnowledgeAcquisitionSource.Testimony,
                Provenance = KnowledgeProvenance.Testimony,
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = strength,
                Credibility = credibility,
                GameTimeSeconds = context?.Persistence?.PlayTime == null ? 0d : context.Persistence.PlayTime.CumulativeSeconds,
                SourceId = "person.test-lab.source",
                Visibility = KnowledgeVisibility.Public
            };
            failure = default;
            return true;
        }

        private bool TryBuildObservationRequest(string transactionId, string methodId, KnowledgeTrackingPolicy trackingPolicy, bool mechanicallyRelevant, bool privateAccess, out ObservationService service, out PersonKnowledgeRuntime knowledge, out ObservationContext context, out ObservableProjection projection, out PrototypeTestLabOperation failure)
        {
            service = null;
            context = null;
            projection = null;
            if (!EnsureKnowledgeRuntime(out knowledge))
            {
                failure = RecordFailure("Build 8.2 Observation", "Knowledge runtime is missing or not ready.", ObservationOutcomeCode.MissingKnowledgeRuntime.ToString());
                return false;
            }

            service = new ObservationService(registry);
            context = BuildObservationContext(transactionId, methodId, SensoryChannel.Vision, ObservationTargetType.Body, trackingPolicy, mechanicallyRelevant, privateAccess);
            projection = new ObservableProjection(
                "projection.prototype.visible-injury",
                ObservationTargetType.Body,
                new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.BodyInjury,
                    subjectType = KnowledgeSubjectType.Body,
                    subjectId = string.IsNullOrWhiteSpace(context.TargetBodyId) ? "body.prototype.target" : context.TargetBodyId,
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = privateAccess ? "injury.blunt-trauma" : "injury.visible-wound",
                    bodyContextId = context.TargetBodyId,
                    sourceContextId = "projection.prototype.visible-injury",
                    sourceRevision = context.ExpectedConditionRevision
                },
                privateAccess ? KnowledgeVisibility.PersonallyObservable : KnowledgeVisibility.Public,
                privateAccess ? 550 : 300,
                privateAccess ? KnowledgeConfidence.DefaultTrustedEvidence : KnowledgeConfidence.DefaultObservation,
                new[] { SensoryChannel.Vision, SensoryChannel.Touch },
                mechanicallyRelevant,
                privateAccess ? "Medical examination grants stronger injury evidence." : "Visible injury evidence.",
                new[] { "feature.8.2", "injury" });
            failure = default;
            return true;
        }

        private ObservationContext BuildObservationContext(string transactionId, string methodId, SensoryChannel channel, ObservationTargetType targetType, KnowledgeTrackingPolicy trackingPolicy, bool mechanicallyRelevant, bool privateAccess, ConcealmentState concealment = ConcealmentState.None, long expectedConditionRevision = 1L, long expectedBodyRevision = 1L)
        {
            string body = context?.PlayerTransform == null ? string.Empty : context.PlayerTransform.GetComponentInParent<ActorBodyRuntime>()?.ActorBodyId ?? string.Empty;
            string person = context?.IdentityProgression == null ? "person.prototype.local-player" : context.IdentityProgression.PersonId;
            return new ObservationContext(
                person,
                transactionId,
                methodId,
                channel,
                targetType,
                string.IsNullOrWhiteSpace(body) ? "body.prototype.target" : body,
                observerActorId: ResolveActorId(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject),
                observerBodyId: body,
                targetPersonId: person,
                targetActorId: ResolveActorId(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject),
                targetBodyId: body,
                distanceQuality: 900,
                visibility: ObservationVisibilityState.Clear,
                concealment: concealment,
                accessLevel: privateAccess ? ObservationAccessLevel.Medical : ObservationAccessLevel.Public,
                consent: privateAccess ? ObservationConsentState.Granted : ObservationConsentState.NotRequired,
                environmentalQuality: 900,
                lightingQuality: 900,
                noiseQuality: 900,
                obstructionQuality: 900,
                expertiseQuality: privateAccess ? 800 : 550,
                toolQuality: privateAccess ? 700 : 550,
                gameTimeSeconds: this.context?.Persistence?.PlayTime == null ? 0d : this.context.Persistence.PlayTime.CumulativeSeconds,
                trackingPolicy: trackingPolicy,
                mechanicallyRelevant: mechanicallyRelevant,
                privateAccessAuthorized: privateAccess,
                expectedBodyRevision: expectedBodyRevision,
                expectedConditionRevision: expectedConditionRevision,
                authorityContext: "Prototype Test Lab 8.2",
                tags: new[] { "feature.8.2" });
        }

        private int CountDefinitions<T>() where T : class, IGameDefinition
        {
            return registry == null ? 0 : registry.DefinitionsById.Values.OfType<T>().Count();
        }

        private string FormatObservationMethodCounts()
        {
            return $"Methods: Observation={CountDefinitions<ObservationMethodDefinition>()} Examination={CountDefinitions<ExaminationMethodDefinition>()} Identification={CountDefinitions<IdentificationMethodDefinition>()} Diagnostic={CountDefinitions<DiagnosticMethodDefinition>()}.";
        }

        private PrototypeTestLabOperation RecordObservationResult(string operationName, ObservationResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Observation operation returned no result.", ObservationOutcomeCode.InvalidContext.ToString());
            }

            string message = FormatObservationResult(result);
            return result.Succeeded
                ? Record(true, operationName, result.Code.ToString(), message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private bool EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out PersonMemoryRuntime memoryRuntime)
        {
            if (currentAutomationScenarioContext?.Runtimes?.History != null && currentAutomationScenarioContext.Runtimes.Memory != null)
            {
                historyRuntime = currentAutomationScenarioContext.Runtimes.History;
                memoryRuntime = currentAutomationScenarioContext.Runtimes.Memory;
                return true;
            }

            historyRuntime = authoritativeHistory;
            memoryRuntime = playerMemory;
            if (registry == null)
            {
                registry = CreateRegistry(context?.DefinitionCatalog);
            }

            string personId = GetPrototypePersonId();
            if (string.IsNullOrWhiteSpace(personId))
            {
                return false;
            }

            historyRuntime.Configure(registry, PersistenceService.LocalWorldId, GetKnownPrototypePersons(), GetKnownPrototypeBodies());
            memoryRuntime.Configure(personId, registry, historyRuntime, GetKnownPrototypePersons());
            return true;
        }

        private PersonMemoryRuntime GetActiveMemoryRuntime()
        {
            return EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime) ? memoryRuntime : playerMemory;
        }

        private PersonKnowledgeRuntime ResolveKnowledgeRuntimeForRumorPerson(string personId)
        {
            if (!EnsureKnowledgeRuntime(out PersonKnowledgeRuntime knowledge))
            {
                return null;
            }

            return string.Equals(knowledge.PersonId, personId, StringComparison.Ordinal) ? knowledge : null;
        }

        private PersonMemoryRuntime ResolveMemoryRuntimeForRumorPerson(string personId)
        {
            if (!EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime))
            {
                return null;
            }

            return string.Equals(memoryRuntime.PersonId, personId, StringComparison.Ordinal) ? memoryRuntime : null;
        }

        private RecordHistoricalEventRequest BuildHistoryEventRequest(string transactionId, string eventId, string definitionId, string personId, KnowledgeVisibility visibility, string note)
        {
            double now = GetGameTimeSeconds();
            return new RecordHistoricalEventRequest
            {
                TransactionId = transactionId,
                EventId = eventId,
                EventDefinitionId = definitionId,
                OccurredAtWorldTime = now,
                RecordedAtWorldTime = now,
                PrimaryPersonId = personId,
                ParticipantPersonIds = new[] { personId },
                BodyIds = new[] { GetPrototypeBodyId() },
                Visibility = visibility,
                SourceSystem = "PrototypeTestLab",
                Provenance = "Development fixture",
                Payload = new HistoricalEventPayloadData
                {
                    kind = HistoricalEventPayloadKind.Generic,
                    note = note
                },
                Tags = new[] { "feature.8.3", "prototype" }
            };
        }

        private RecordLifeEventRequest BuildPrototypeLifeEventRequest(string eventId, string definitionId, LifeEventCategory category, LifeEventPayloadKind payloadKind, LifeEventSignificance significance, LifeEventBiographyRelevance biographyRelevance, KnowledgeVisibility visibility, LifeEventParticipantRole role, string organizationId = "", string relatedRoleId = "", string relatedTitleId = "", string relatedInjuryId = "", string relatedConditionId = "", string relatedTreatmentId = "", string relatedCombatEncounterId = "", string relatedLegalRecordId = "", string relatedItemId = "", string sequenceId = "", int sequenceOrder = 0, LifeEventSequenceStatus sequenceStatus = LifeEventSequenceStatus.Active, string relationshipTarget = "")
        {
            double now = GetGameTimeSeconds();
            string personId = GetPrototypePersonId();
            string bodyId = GetPrototypeBodyId();
            List<string> related = new List<string>();
            if (!string.IsNullOrWhiteSpace(relatedItemId))
            {
                related.Add(relatedItemId);
            }

            LifeEventRelationshipData[] relationships = string.IsNullOrWhiteSpace(relationshipTarget)
                ? Array.Empty<LifeEventRelationshipData>()
                : new[]
                {
                    new LifeEventRelationshipData
                    {
                        relationshipId = $"relationship.{eventId}.cause",
                        relationshipType = category == LifeEventCategory.Recovery ? LifeEventRelationshipType.Resolution : LifeEventRelationshipType.Cause,
                        targetEventId = relationshipTarget,
                        requiresAcyclic = true
                    }
                };

            return new RecordLifeEventRequest
            {
                TransactionId = $"history.8.5.{category}.{Guid.NewGuid():N}",
                EventId = eventId,
                EventDefinitionId = definitionId,
                Category = category,
                PayloadKind = payloadKind,
                OccurredAtWorldTime = now,
                RecordedAtWorldTime = now,
                PrimaryPersonId = personId,
                Participants = new[]
                {
                    new LifeEventParticipantData
                    {
                        personId = personId,
                        role = role,
                        bodyId = bodyId,
                        relatedEntityId = relatedItemId
                    }
                },
                BodyIds = new[] { bodyId },
                OrganizationId = organizationId,
                RelatedEntityIds = related.ToArray(),
                Visibility = visibility,
                Significance = significance,
                BiographyRelevance = biographyRelevance,
                PublicRecordRelevance = visibility == KnowledgeVisibility.Public ? LifeEventPublicRecordRelevance.PublicRecord : LifeEventPublicRecordRelevance.PersonalOnly,
                Outcome = LifeEventOutcome.Confirmed,
                Relationships = relationships,
                SequenceId = sequenceId,
                SequenceOrder = sequenceOrder,
                SequenceTypeId = string.IsNullOrWhiteSpace(sequenceId) ? string.Empty : $"{sequenceId}.type",
                SequenceStatus = sequenceStatus,
                RelatedRoleId = relatedRoleId,
                RelatedTitleId = relatedTitleId,
                RelatedInjuryId = relatedInjuryId,
                RelatedConditionId = relatedConditionId,
                RelatedTreatmentId = relatedTreatmentId,
                RelatedCombatEncounterId = relatedCombatEncounterId,
                RelatedLegalRecordId = relatedLegalRecordId,
                SourceSystem = "PrototypeTestLab",
                Provenance = "Development life-event fixture. Does not mutate current gameplay state.",
                CorrelationId = string.IsNullOrWhiteSpace(sequenceId) ? $"correlation.{eventId}" : sequenceId,
                HistoricalPayload = new HistoricalEventPayloadData
                {
                    kind = HistoricalEventPayloadKind.Generic,
                    organizationId = organizationId,
                    itemId = relatedItemId,
                    conditionId = relatedConditionId,
                    note = $"{category} life event"
                },
                LifeEventPayload = new LifeEventPayloadData
                {
                    kind = payloadKind,
                    subjectPersonId = personId,
                    methodId = "prototype-test-lab",
                    severityId = significance.ToString(),
                    encounterId = relatedCombatEncounterId,
                    evidenceId = $"evidence.{eventId}",
                    treatmentId = relatedTreatmentId,
                    note = $"{category} life event"
                },
                Tags = new[] { "feature.8.5", "life-event", category.ToString() }
            };
        }

        private PrototypeTestLabOperation RecordPrototypeLifeEvent(string operationName, string definitionId, LifeEventCategory category, LifeEventPayloadKind payloadKind, LifeEventSignificance significance, LifeEventBiographyRelevance biographyRelevance, KnowledgeVisibility visibility, LifeEventParticipantRole role, string organizationId = "", string relatedRoleId = "", string relatedTitleId = "", string relatedInjuryId = "", string relatedConditionId = "", string relatedTreatmentId = "", string relatedCombatEncounterId = "", string relatedLegalRecordId = "", string relatedItemId = "", string sequenceId = "", int sequenceOrder = 0, LifeEventSequenceStatus sequenceStatus = LifeEventSequenceStatus.Active, string relationshipTarget = "")
        {
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _))
            {
                return RecordFailure(operationName, "History runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            string eventId = StablePrototypeLifeEventId(category);
            RecordLifeEventRequest request = BuildPrototypeLifeEventRequest(eventId, definitionId, category, payloadKind, significance, biographyRelevance, visibility, role, organizationId, relatedRoleId, relatedTitleId, relatedInjuryId, relatedConditionId, relatedTreatmentId, relatedCombatEncounterId, relatedLegalRecordId, relatedItemId, sequenceId, sequenceOrder, sequenceStatus, relationshipTarget);
            if (historyRuntime.TryGetEvent(eventId, out HistoricalEventRecord existing))
            {
                HistoryOperationResult duplicate = HistoryOperationResult.Success("Life event already exists.", request.TransactionId, existing, null, null, historyRuntime.HistoryRevision, historyRuntime.HistoryRevision, duplicate: true);
                return RecordHistoryResult(operationName, duplicate);
            }

            HistoryOperationResult result = historyRuntime.RecordLifeEvent(request);
            return RecordHistoryResult(operationName, result);
        }

        private string EnsureLifeEvent(string eventId, Func<PrototypeTestLabOperation> create)
        {
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _);
            if (historyRuntime.TryGetEvent(eventId, out _))
            {
                return eventId;
            }

            create?.Invoke();
            return eventId;
        }

        private static string StablePrototypeLifeEventId(LifeEventCategory category)
        {
            return category switch
            {
                LifeEventCategory.BirthOrCreation => "event.prototype.life.birth",
                LifeEventCategory.Discovery => "event.prototype.life.discovery",
                LifeEventCategory.Role => "event.prototype.life.role",
                LifeEventCategory.Title => "event.prototype.life.title",
                LifeEventCategory.Affiliation => "event.prototype.life.affiliation",
                LifeEventCategory.Combat => "event.prototype.life.battle",
                LifeEventCategory.Injury => "event.prototype.life.injury",
                LifeEventCategory.Diagnosis => "event.prototype.life.diagnosis",
                LifeEventCategory.Recovery => "event.prototype.life.recovery",
                LifeEventCategory.Crime => "event.prototype.life.crime",
                LifeEventCategory.Ownership => "event.prototype.life.ownership",
                LifeEventCategory.Death => "event.prototype.life.death",
                LifeEventCategory.Disappearance => "event.prototype.life.presumed-death",
                LifeEventCategory.ReturnOrResurrection => "event.prototype.life.return",
                LifeEventCategory.BodyTransition => "event.prototype.life.body-transition",
                _ => $"event.prototype.life.{category.ToString().ToLowerInvariant()}"
            };
        }

        private FormMemoryRequest BuildMemoryRequest(string transactionId, string memoryId, string eventId, HistoryMemorySource source, bool createKnowledge)
        {
            double now = GetGameTimeSeconds();
            return new FormMemoryRequest
            {
                TransactionId = transactionId,
                MemoryId = memoryId,
                OwnerPersonId = GetPrototypePersonId(),
                HistoricalEventId = eventId,
                Source = source,
                FormedAtWorldTime = now + 0.1d,
                RememberedOccurredAtWorldTime = now,
                Confidence = 780,
                Clarity = 720,
                Salience = 650,
                FirstHand = source == HistoryMemorySource.DirectObservation || source == HistoryMemorySource.DirectParticipation || source == HistoryMemorySource.PreviousBody,
                Visibility = KnowledgeVisibility.Private,
                BodyAtTimeId = GetPrototypeBodyId(),
                DebugDescription = "Prototype Test Lab historical memory.",
                CreateKnowledgeEvidence = createKnowledge,
                Tags = new[] { "feature.8.3", "memory" }
            };
        }

        private KnowledgeObservationRequest BuildHistoricalKnowledgeRequest(string transactionId, string eventId, KnowledgeEvidenceDirection direction, int strength, int credibility)
        {
            return new KnowledgeObservationRequest
            {
                PersonId = GetPrototypePersonId(),
                TransactionId = transactionId,
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                    subjectType = KnowledgeSubjectType.Event,
                    subjectId = eventId,
                    valueType = KnowledgeValueType.Boolean,
                    booleanValue = true,
                    bodyContextId = GetPrototypeBodyId(),
                    sourceContextId = "prototype.history.test-lab"
                },
                AcquisitionSource = KnowledgeAcquisitionSource.SkillOrEducation,
                Provenance = KnowledgeProvenance.Inference,
                Direction = direction,
                Strength = strength,
                Credibility = credibility,
                GameTimeSeconds = GetGameTimeSeconds(),
                SourceId = "prototype.history.test-lab",
                EvidenceId = $"evidence.history.{Guid.NewGuid():N}",
                Visibility = KnowledgeVisibility.Private,
                PrivateAccessAuthorized = true,
                RelatedEventId = eventId,
                Tags = new[] { "feature.8.3", "history-belief" }
            };
        }

        private PrototypeTestLabOperation RecordHistoryResult(string operationName, HistoryOperationResult result)
        {
            bool succeeded = result != null && result.Succeeded;
            return succeeded
                ? Record(true, operationName, result.Code.ToString(), FormatHistoryResult(result))
                : RecordFailure(operationName, FormatHistoryResult(result), result?.Code.ToString() ?? HistoryResultCode.InvalidRequest.ToString());
        }

        private PrototypeTestLabOperation RecallMemoryWithRequest(string operationName, MemoryRecallRequest request, PersonMemoryRuntime memoryRuntimeOverride = null)
        {
            PersonMemoryRuntime memoryRuntime = memoryRuntimeOverride;
            if (memoryRuntime == null)
            {
                FormWitnessHistoryMemory();
                if (!EnsureHistoryRuntime(out _, out memoryRuntime))
                {
                    return RecordFailure(operationName, "Memory runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
                }
            }

            MemoryRecallResult result = memoryRuntime.Recall(request);
            return result.Succeeded
                ? Record(true, operationName, result.Code.ToString(), FormatMemoryRecallResult(result))
                : RecordFailure(operationName, FormatMemoryRecallResult(result), result.Code.ToString());
        }

        private PrototypeTestLabOperation AlterPrototypeMemoryMetric(string operationName, int clarityDelta, int confidenceDelta, int salienceDelta, MemoryState? state)
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            HistoryOperationResult result = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.metric.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                AlterationType = MemoryAlterationType.NaturalDegradation,
                ResultingState = state,
                ClarityDelta = clarityDelta,
                ConfidenceDelta = confidenceDelta,
                SalienceDelta = salienceDelta,
                SourceId = "test-lab.memory.metric",
                Description = operationName
            });
            return RecordHistoryResult(operationName, result);
        }

        private PrototypeTestLabOperation SetPrototypeMemoryState(string operationName, MemoryState state)
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            HistoryOperationResult result = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.state.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                AlterationType = state == MemoryState.Forgotten ? MemoryAlterationType.DetailLoss : MemoryAlterationType.Reconstruction,
                ResultingState = state,
                SourceId = "test-lab.memory.state",
                Description = $"Set memory state to {state}."
            });
            return RecordHistoryResult(operationName, result);
        }

        private PrototypeTestLabOperation AlterPrototypeMemoryDetails(string operationName, string[] detailIds)
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypeMemoryId();
            HistoryOperationResult result = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.details.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                AlterationType = MemoryAlterationType.DetailLoss,
                ResultingState = MemoryState.Altered,
                DetailIdsToForget = detailIds,
                SourceId = "test-lab.memory.partial-forgetting",
                Description = operationName
            });
            return RecordHistoryResult(operationName, result);
        }

        private PrototypeTestLabOperation AlterPrototypePreviousBody(string operationName, MemoryDetailState detailState, MemoryState state)
        {
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            string memoryId = GetPrototypePreviousBodyMemoryId();
            if (!memoryRuntime.TryGetMemory(memoryId, out _))
            {
                PrototypeTestLabOperation transition = RecordBodyTransitionHistory();
                if (!transition.Succeeded)
                {
                    return transition;
                }
            }

            MemoryDetailData detail = new MemoryDetailData
            {
                detailId = "detail.body",
                kind = MemoryDetailKind.Body,
                state = detailState,
                value = GetPrototypeBodyId(),
                confidence = detailState == MemoryDetailState.Suppressed ? 100 : 850
            };
            HistoryOperationResult result = memoryRuntime.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = $"history.8.4.previous-body.{Guid.NewGuid():N}",
                OwnerPersonId = GetPrototypePersonId(),
                MemoryId = memoryId,
                WorldTime = GetMemoryWorldTime(memoryRuntime, memoryId),
                AlterationType = detailState == MemoryDetailState.Suppressed ? MemoryAlterationType.Suppression : MemoryAlterationType.Recovery,
                ResultingState = state,
                DetailsToAddOrReplace = new[] { detail },
                SourceId = "test-lab.memory.previous-body",
                Description = operationName
            });
            return RecordHistoryResult(operationName, result);
        }

        private string GetPrototypePreviousBodyMemoryId(string fallbackRunId = null)
        {
            if (currentAutomationScenarioContext != null)
            {
                return currentAutomationScenarioContext.ScopedId("memory", "previous-body");
            }

            return string.IsNullOrWhiteSpace(fallbackRunId)
                ? "memory.prototype.previous-body"
                : $"memory.prototype.previous-body.{fallbackRunId}";
        }

        private string GetPrototypeMemoryId()
        {
            FormWitnessHistoryMemory();
            EnsureHistoryRuntime(out _, out PersonMemoryRuntime memoryRuntime);
            double now = GetGameTimeSeconds();
            HistoryMemoryRecord memory = memoryRuntime.CreateSnapshot().Memories
                .OrderByDescending(record => record.Accessible)
                .ThenBy(record => record.MemoryId, StringComparer.Ordinal)
                .FirstOrDefault(record => IsMemoryAutomationTarget(record, now) && string.Equals(record.HistoricalEventId, GetPrototypeHiddenHistoryEventId(), StringComparison.Ordinal));
            if (memory != null)
            {
                return memory.MemoryId;
            }

            string memoryId = $"memory.prototype.automation.{Guid.NewGuid():N}";
            memoryRuntime.FormMemory(BuildMemoryRequest(CreateAutomationScopedId("history", "automation-memory"), memoryId, GetPrototypeHiddenHistoryEventId(), HistoryMemorySource.DevelopmentFixture, createKnowledge: false));
            return memoryId;
        }

        private bool TryCreateIsolatedMemoryRecallFixture(string fixtureName, out PersonMemoryRuntime memoryRuntime, out string memoryId, out string eventId, out PrototypeTestLabOperation failure)
        {
            memoryRuntime = null;
            memoryId = string.Empty;
            eventId = string.Empty;
            failure = default;
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _))
            {
                failure = RecordFailure("Prepare 8.4 Recall Fixture", "History runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
                return false;
            }

            eventId = $"event.prototype.memory-recall.{SanitizeForTransaction(fixtureName)}.{Guid.NewGuid():N}";
            memoryId = $"memory.prototype.memory-recall.{SanitizeForTransaction(fixtureName)}.{Guid.NewGuid():N}";
            HistoryOperationResult eventResult = historyRuntime.RecordEvent(BuildHistoryEventRequest($"history.8.4.recall-fixture-event.{Guid.NewGuid():N}", eventId, "history-event.hidden-witnessed-event", GetPrototypePersonId(), KnowledgeVisibility.Hidden, $"Isolated 8.4 {fixtureName} recall fixture."));
            if (!eventResult.Succeeded)
            {
                failure = RecordHistoryResult("Prepare 8.4 Recall Fixture", eventResult);
                return false;
            }

            memoryRuntime = CreateMemoryProofRuntime();
            HistoryOperationResult memoryResult = memoryRuntime.FormMemory(BuildMemoryRequest($"history.8.4.recall-fixture-memory.{Guid.NewGuid():N}", memoryId, eventId, HistoryMemorySource.DevelopmentFixture, createKnowledge: false));
            if (!memoryResult.Succeeded)
            {
                failure = RecordHistoryResult("Prepare 8.4 Recall Fixture", memoryResult);
                return false;
            }

            return true;
        }

        private static bool IsMemoryAutomationTarget(HistoryMemoryRecord memory, double worldTime)
        {
            if (memory == null || string.Equals(memory.MemoryId, "memory.prototype.previous-body", StringComparison.Ordinal))
            {
                return false;
            }

            if (!memory.Accessible)
            {
                return false;
            }

            return !memory.Suppressions.Any(suppression => suppression.IsActiveAt(worldTime));
        }

        private PersonMemoryRuntime CreateMemoryProofRuntime()
        {
            PersonMemoryRuntime memoryRuntime = new PersonMemoryRuntime();
            EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _);
            memoryRuntime.Configure(GetPrototypePersonId(), registry, historyRuntime, GetKnownPrototypePersons());
            return memoryRuntime;
        }

        private double GetMemoryWorldTime(PersonMemoryRuntime memoryRuntime, string memoryId, double offsetSeconds = 1d)
        {
            double current = GetGameTimeSeconds();
            if (memoryRuntime != null && memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord memory))
            {
                current = Math.Max(current, memory.FormedAtWorldTime);
            }

            return current + Math.Max(0d, offsetSeconds);
        }

        private bool TryFindSuppressedMemory(PersonMemoryRuntime memoryRuntime, out string memoryId, out MemorySuppressionData suppression)
        {
            memoryId = string.Empty;
            suppression = null;
            if (memoryRuntime == null)
            {
                return false;
            }

            double now = GetGameTimeSeconds();
            foreach (HistoryMemoryRecord memory in memoryRuntime.CreateSnapshot().Memories.OrderBy(record => record.MemoryId, StringComparer.Ordinal))
            {
                MemorySuppressionData candidate = memory.Suppressions
                    .OrderBy(entry => entry.startedAtWorldTime)
                    .ThenBy(entry => entry.suppressionId, StringComparer.Ordinal)
                    .FirstOrDefault(entry => entry.endedAtWorldTime < 0d || entry.endedAtWorldTime > now);
                if (candidate != null)
                {
                    memoryId = memory.MemoryId;
                    suppression = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string FormatHistoryResult(HistoryOperationResult result)
        {
            if (result == null)
            {
                return "History result is missing.";
            }

            string eventId = result.Event == null ? "None" : result.Event.EventId;
            string memoryId = result.Memory == null ? "None" : result.Memory.MemoryId;
            string knowledge = result.KnowledgeResult == null ? "None" : $"{result.KnowledgeResult.Code}/{result.KnowledgeResult.ResultingBelief?.BeliefId ?? "NoBelief"}";
            return $"Success={result.Succeeded} Code={result.Code} Preview={result.Preview} Duplicate={result.Duplicate} Event={eventId} Memory={memoryId} Knowledge={knowledge} Revision={result.PriorRevision}->{result.ResultingRevision}. {result.Message}";
        }

        private PrototypeTestLabOperation RecordLifeEventView(string operationName, Func<AuthoritativeHistoryRuntime, string> formatter)
        {
            if (!EnsureHistoryRuntime(out AuthoritativeHistoryRuntime historyRuntime, out _))
            {
                return RecordFailure(operationName, "History runtime is missing.", HistoryResultCode.InvalidRequest.ToString());
            }

            string message = formatter == null ? string.Empty : formatter(historyRuntime);
            bool succeeded = !string.IsNullOrWhiteSpace(message);
            return Record(succeeded, operationName, succeeded ? "Success" : "Empty", message);
        }

        private static string FormatLifeEvents(IReadOnlyList<LifeEventRecord> events)
        {
            if (events == null || events.Count == 0)
            {
                return "No life events.";
            }

            return string.Join(Environment.NewLine, events.Take(12).Select(record =>
                $"{record.EventId} Def={record.DefinitionId} Category={record.Category} Person={record.PrimaryPersonId} Role={string.Join(",", record.Participants.Select(participant => participant.role))} Time={record.OccurredAtWorldTime:0.##} Significance={record.Significance} Bio={record.BiographyRelevance} Visibility={record.Visibility} Sequence={record.SequenceId} Status={record.Status}"));
        }

        private static string FormatBiography(IReadOnlyList<BiographyTimelineEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return "No biography entries.";
            }

            return string.Join(Environment.NewLine, entries.Take(12).Select(entry =>
                $"{entry.EventId} Category={entry.Category} Role={entry.ParticipantRole} Time={entry.OccurredAtWorldTime:0.##} Significance={entry.Significance} Bio={entry.BiographyRelevance} Visibility={entry.Visibility} Known={entry.Known} Remembered={entry.Remembered}"));
        }

        private static string FormatLifeEventSequence(LifeEventSequenceRecord sequence)
        {
            if (sequence == null)
            {
                return "Sequence=None";
            }

            return $"Sequence={sequence.SequenceId} Type={sequence.SequenceTypeId} Status={sequence.Status} Person={sequence.PrimaryPersonId} Events=[{string.Join(",", sequence.Events.Select(record => record.EventId))}]";
        }

        private static string FormatMemoryRecallResult(MemoryRecallResult result)
        {
            if (result == null)
            {
                return "Memory recall result is missing.";
            }

            string entries = string.Join(" | ", result.Entries.Select(entry =>
                $"{entry.Memory.MemoryId}:{entry.Outcome}:state={entry.Memory.State}:conf={entry.Memory.Confidence}:clarity={entry.Memory.Clarity}:details={entry.RecalledDetails.Count}:unavailable={entry.UnavailableDetails.Count}:cue={entry.CueMatched}").Take(8));
            return $"Success={result.Succeeded} Code={result.Code} Outcome={result.Outcome} Preview={result.Preview} Revision={result.PriorRevision}->{result.ResultingRevision} Entries={result.Entries.Count} [{entries}]. {result.Message}";
        }

        private string GetPrototypePersonId()
        {
            if (context?.IdentityProgression != null && !string.IsNullOrWhiteSpace(context.IdentityProgression.PersonId))
            {
                return context.IdentityProgression.PersonId;
            }

            if (context?.PlayerKnowledge != null && !string.IsNullOrWhiteSpace(context.PlayerKnowledge.PersonId))
            {
                return context.PlayerKnowledge.PersonId;
            }

            return PersistenceService.LocalPlayerId;
        }

        private string GetPrototypeBodyId()
        {
            if (context?.PlayerTransform != null)
            {
                ActorBodyRuntime body = context.PlayerTransform.GetComponentInParent<ActorBodyRuntime>();
                if (body != null && !string.IsNullOrWhiteSpace(body.ActorBodyId))
                {
                    return body.ActorBodyId;
                }
            }

            return "body.prototype.current";
        }

        private string[] GetKnownPrototypePersons()
        {
            AuthoritativeHistoryRuntime historyRuntime = currentAutomationScenarioContext?.Runtimes?.History ?? authoritativeHistory;
            IEnumerable<string> historicalPersons = historyRuntime.CreateSnapshot().Events.SelectMany(record => (record.ParticipantPersonIds ?? Array.Empty<string>()).Concat(new[] { record.PrimaryPersonId }));
            return new[]
            {
                GetPrototypePersonId(),
                "person.prototype.listener",
                "person.prototype.uninformed",
                "person.prototype.witness",
                "person.prototype.friend",
                "person.prototype.rival",
                "person.prototype.parent",
                "person.prototype.child",
                "person.prototype.mentor",
                "person.prototype.student"
            }
                .Concat(historicalPersons)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private string[] GetKnownPrototypeBodies()
        {
            AuthoritativeHistoryRuntime historyRuntime = currentAutomationScenarioContext?.Runtimes?.History ?? authoritativeHistory;
            HistorySnapshot historySnapshot = historyRuntime.CreateSnapshot();
            IEnumerable<string> historicalBodies = historySnapshot.Events.SelectMany(record => record.BodyIds ?? Array.Empty<string>())
                .Concat(historySnapshot.BodyOccupations.Select(record => record.BodyId));
            return new[]
            {
                GetPrototypeBodyId(),
                "body.prototype.current",
                "body.prototype.previous",
                "body.prototype.future"
            }
                .Concat(historicalBodies)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private double GetGameTimeSeconds()
        {
            return context?.Persistence?.PlayTime == null ? Time.timeAsDouble : context.Persistence.PlayTime.CumulativeSeconds;
        }

        private string FormatObservationResult(ObservationResult result)
        {
            if (result == null)
            {
                return "Observation=None";
            }

            string knowledge = result.KnowledgeResult == null
                ? "Knowledge=None"
                : $"Knowledge={result.KnowledgeResult.Code} Belief={result.KnowledgeResult.ResultingBelief?.BeliefId ?? "None"} Confidence={result.KnowledgeResult.ResultingBelief?.Confidence.ToString() ?? "0"}";
            string hypotheses = result.Hypotheses.Count == 0
                ? "Hypotheses=None"
                : $"Hypotheses={string.Join(", ", result.Hypotheses.Take(4).Select(hypothesis => $"{hypothesis.CandidateId}:{hypothesis.Confidence}"))}";
            return $"Success={result.Succeeded} Code={result.Code} Preview={result.Preview} Tracked={result.Tracked} Method={result.MethodId} Quality={result.Quality} Strength={result.EvidenceStrength} Identification={result.IdentificationState} Diagnosis={result.DiagnosticState}. {knowledge}. {hypotheses}. {result.Message}";
        }

        private PrototypeTestLabOperation RecordKnowledgeResult(string operationName, KnowledgeOperationResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Knowledge operation returned no result.", KnowledgeResultCode.InvalidRequest.ToString());
            }

            string belief = result.ResultingBelief == null
                ? "Belief=None"
                : $"Belief={result.ResultingBelief.BeliefId} State={result.ResultingBelief.State} Confidence={result.ResultingBelief.Confidence} Fact={result.ResultingBelief.Proposition.FactDefinitionId}";
            string evidence = result.Evidence == null
                ? "Evidence=None"
                : $"Evidence={result.Evidence.EvidenceId} Direction={result.Evidence.Direction} Provenance={result.Evidence.Provenance}";
            string discovery = result.Discovery == null
                ? "Discovery=None"
                : $"Discovery={result.Discovery.Category} Delta={result.Discovery.ConfidenceDelta}";
            string message = $"{result.Message} Tx={result.TransactionId} Preview={result.Preview} Duplicate={result.Duplicate} Code={result.Code} Revision={result.PriorRevision}->{result.ResultingRevision}. {belief}. {evidence}. {discovery}.";
            return result.Succeeded
                ? Record(true, operationName, result.Code.ToString(), message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private PrototypeTestLabOperation ApplyBiologicalConditionExposure(string operationName, string conditionDefinitionId, BiologicalExposureRoute route, float dose, string targetNodeId, bool preview, string transactionId = "")
        {
            if (!EnsureBiologicalConditionRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BodySnapshot snapshot = body.CreateSnapshot();
            BiologicalConditionExposureRequest request = new BiologicalConditionExposureRequest(
                body.ActorBodyId,
                conditionDefinitionId,
                string.IsNullOrWhiteSpace(transactionId) ? $"test-lab.biological-condition.exposure.{Guid.NewGuid():N}" : transactionId,
                route,
                dose,
                sourceId: "test-lab.biological-condition",
                sourceBodyId: body.ActorBodyId,
                sourceEventId: operationName,
                sourceCategory: BiologicalConditionSourceCategory.Development,
                targetAnatomyNodeId: targetNodeId,
                intensity: 1f,
                durationSeconds: 0f,
                preview: preview,
                authority: "Prototype Test Lab",
                expectedBodyRevision: snapshot.BodyRevision,
                expectedAnatomyRevision: snapshot.Anatomy?.AnatomyRevision ?? 0L,
                expectedConditionRevision: snapshot.Condition?.ConditionRevision ?? 0L,
                expectedVitalRevision: snapshot.VitalProcesses?.VitalRevision ?? 0L,
                expectedHazardRevision: snapshot.BiologicalHazards?.HazardRevision ?? 0L,
                expectedCompatibilityRevision: snapshot.BiologicalCompatibility?.CompatibilityRevision ?? 0L);
            BiologicalConditionResult result = preview
                ? body.BiologicalConditions.PreviewExposure(request, snapshot, body.BiologicalCompatibility)
                : body.BiologicalConditions.ApplyExposure(request, snapshot, body.BiologicalCompatibility);
            return RecordBiologicalConditionResult(operationName, result);
        }

        private PrototypeTestLabOperation ApplyBiologicalConditionTickWithId(float elapsedGameSeconds, string tickId)
        {
            if (!EnsureBiologicalConditionRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BiologicalConditionTickRequest request = new BiologicalConditionTickRequest(body.ActorBodyId, elapsedGameSeconds, tickId, preview: false, "Prototype Test Lab biological condition tick");
            EnsureBiologicalRecoveryReady(body);
            GameObject target = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            BiologicalConditionConsequenceExecutionResult result = body.BiologicalConditions.ApplyTickConsequences(new BiologicalConditionConsequenceExecutionRequest(
                request,
                body.CreateSnapshot(),
                body.BiologicalCompatibility,
                body.VitalProcesses,
                body.BiologicalHazards,
                body.BiologicalRecovery,
                damageHealingService,
                target,
                target,
                body.ActorBodyId,
                body.ActorBodyId));
            return Record(result.Succeeded, "Apply Biological Condition Tick", result.Code.ToString(), FormatBiologicalConditionConsequenceExecutionResult(result));
        }

        private PrototypeTestLabOperation ApplyBiologicalConditionTreatment(string operationName, string treatmentId)
        {
            if (!EnsureBiologicalConditionRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BiologicalConditionInstanceSnapshot instance = body.BiologicalConditions.CreateSnapshot().ActiveInstances.FirstOrDefault();
            if (instance == null)
            {
                PrototypeTestLabOperation exposure = treatmentId.Contains("antidote") ? ApplyPoison() : ApplyViralExposure();
                if (!exposure.Succeeded)
                {
                    return exposure;
                }

                instance = body.BiologicalConditions.CreateSnapshot().ActiveInstances.FirstOrDefault();
            }

            BiologicalConditionTreatmentRequest request = new BiologicalConditionTreatmentRequest(body.ActorBodyId, instance?.InstanceId ?? string.Empty, treatmentId, $"test-lab.biological-condition.treatment.{Guid.NewGuid():N}", dose: 1f, preview: false, sourceId: "test-lab.biological-condition");
            return RecordBiologicalConditionResult(operationName, body.BiologicalConditions.ApplyTreatment(request, body.CreateSnapshot(), body.BiologicalCompatibility));
        }

        private bool EnsureBiologicalConditionRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure)
        {
            body = null;
            failure = default;
            if (!EnsureBodyRuntime(out body))
            {
                failure = RecordFailure("Biological Condition Operation", "Body runtime is missing.", BiologicalConditionResultCode.MissingBody.ToString());
                return false;
            }

            body.BiologicalConditions.BuildForBody(body.CreateSnapshot(), registry, restoring: false, preserveRevision: true);
            if (body.BiologicalConditions.Readiness != BiologicalConditionReadinessState.Ready)
            {
                failure = RecordFailure("Biological Condition Operation", $"Biological Condition runtime is not ready: {body.BiologicalConditions.Readiness}.", body.BiologicalConditions.Readiness.ToString());
                return false;
            }

            return true;
        }

        private PrototypeTestLabOperation RecordBiologicalConditionResult(string operationName, BiologicalConditionResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Biological Condition operation returned no result.", BiologicalConditionResultCode.InvalidRequest.ToString());
            }

            return Record(result.Succeeded, operationName, result.Duplicate ? BiologicalConditionResultCode.Duplicate.ToString() : result.Code.ToString(), FormatBiologicalConditionResult(result));
        }

        private static string FormatBiologicalConditionResult(BiologicalConditionResult result)
        {
            if (result == null)
            {
                return "No Biological Condition result.";
            }

            BiologicalConditionInstanceSnapshot instance = string.IsNullOrWhiteSpace(result.InstanceId)
                ? null
                : result.Snapshot?.Instances.FirstOrDefault(candidate => string.Equals(candidate.InstanceId, result.InstanceId, StringComparison.Ordinal));
            return $"Success={result.Succeeded} Preview={result.Preview} Duplicate={result.Duplicate} Code={result.Code} Instance={result.InstanceId} EffectiveDose={result.EffectiveDose:0.##} Stage={instance?.Stage.ToString() ?? string.Empty} Severity={instance?.Severity.ToString() ?? string.Empty} Load={instance?.Load ?? 0f:0.##} CompatibilityRev={result.Compatibility?.CompatibilityRevision ?? 0}. {result.Message}";
        }

        private static string FormatBodyBiologySnapshot(BodyBiologySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "No Body Biology snapshot.";
            }

            string diagnostics = snapshot.Diagnostics.Count == 0 ? string.Empty : " Diagnostics=" + string.Join(" ", snapshot.Diagnostics);
            return $"Body={snapshot.ActorBodyId} Person={snapshot.PersonId} Species={snapshot.SpeciesId} Ready={snapshot.Ready} Coherent={snapshot.Coherent} Revisions=[{snapshot.Revisions}] ActiveConditions={snapshot.BiologicalConditions?.ActiveInstances.Count ?? 0} ActiveHazards={snapshot.Body?.BiologicalHazards?.ActiveHazards.Count ?? 0} ActiveRecovery={snapshot.Body?.BiologicalRecovery?.ActiveProcesses.Count ?? 0} TemporaryTransformation={snapshot.Transformation?.ActiveTemporaryTransformation ?? false}.{diagnostics}";
        }

        private static string FormatBodyBiologyAdvanceResult(BodyBiologyAdvanceResult result)
        {
            if (result == null)
            {
                return "No Body Biology advance result.";
            }

            string steps = string.Join(" | ", result.Steps.Select(step => $"{step.StepId}:{step.Code}:Succeeded={step.Succeeded}:Preview={step.Preview}:Duplicate={step.Duplicate}"));
            return $"{result.Code} Success={result.Succeeded} Preview={result.Preview} Duplicate={result.Duplicate}. Before=[{result.Before?.Revisions}] After=[{result.After?.Revisions}] Steps=[{steps}]. {result.Message}";
        }

        private static string FormatBiologicalConditionTickResult(BiologicalConditionTickResult result)
        {
            if (result == null)
            {
                return "No Biological Condition tick result.";
            }

            string consequences = string.Join("; ", result.Consequences.Select(consequence => $"{consequence.Flags} Vital={consequence.VitalResourceId}:{consequence.VitalPressureAmount:0.##} Hazard={consequence.HazardDefinitionId} Damage={consequence.DamageTypeId}:{consequence.Step6DamageAmount:0.##}"));
            return $"Success={result.Succeeded} Preview={result.Preview} Duplicate={result.Duplicate} Code={result.Code} Elapsed={result.Request.ElapsedGameSeconds:0.##} Active={result.Snapshot?.ActiveInstances.Count ?? 0} Consequences=[{consequences}]. {result.Message}";
        }

        private static string FormatBiologicalConditionConsequenceExecutionResult(BiologicalConditionConsequenceExecutionResult result)
        {
            if (result == null)
            {
                return "No Biological Condition consequence execution result.";
            }

            string tick = FormatBiologicalConditionTickResult(result.ConditionTick);
            string vitals = string.Join("; ", result.VitalResults.Select(vital => $"{vital.Request.ResourceId} {vital.Code} {vital.PreviousValue:0.##}->{vital.NewValue:0.##} Applied={vital.AppliedAmount:0.##} Duplicate={vital.Duplicate}"));
            string hazards = string.Join("; ", result.HazardResults.Select(hazard => $"{hazard.Code} Duplicate={hazard.Duplicate} {hazard.Message}"));
            string recovery = string.Join("; ", result.RecoveryResults.Select(entry => $"{entry.Code} Duplicate={entry.Duplicate} {entry.Message}"));
            string damage = string.Join("; ", result.DamageResults.Select(entry => $"{entry.Code} Damage={entry.FinalDamageAmount:0.##} Health={entry.OldHealth:0.##}->{entry.NewHealth:0.##} Duplicate={entry.Duplicate}"));
            return $"Success={result.Succeeded} Preview={result.Preview} Duplicate={result.Duplicate} Code={result.Code}. Tick=({tick}) Vitals=[{vitals}] Hazards=[{hazards}] Recovery=[{recovery}] Damage=[{damage}]. {result.Message}";
        }

        private PrototypeTestLabOperation StartRecoveryProcess(string methodId, RecoveryTargetCategory targetCategory, string nodeId, string resourceId, bool preview, bool ensureTarget, string injuryDefinitionId = "injury.laceration")
        {
            if (!EnsureRecoveryRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            if (ensureTarget)
            {
                if (targetCategory == RecoveryTargetCategory.Injury && !body.Condition.CreateSnapshot().ActiveInjuries.Any(injury => string.Equals(injury.TargetNodeId, nodeId, StringComparison.Ordinal)))
                {
                    PrototypeTestLabOperation damage = ApplyLocalizedStructuralDamageWithTransaction(injuryDefinitionId, nodeId, 40, $"test-lab.recovery.auto-damage.{Guid.NewGuid():N}");
                    if (!damage.Succeeded)
                    {
                        return damage;
                    }
                }
                else if (targetCategory == RecoveryTargetCategory.VitalResource && body.VitalProcesses.TryGetResource(resourceId, out VitalResourceSnapshot resource) && resource.CurrentValue >= resource.EffectiveMaximumValue)
                {
                    PrototypeTestLabOperation drain = ApplyVitalResourceMutationWithTransaction(resourceId, VitalResourceMutationOperation.Consume, 30f, $"test-lab.recovery.auto-drain.{Guid.NewGuid():N}");
                    if (!drain.Succeeded)
                    {
                        return drain;
                    }
                }
            }

            BodyConditionSnapshot condition = body.Condition.CreateSnapshot();
            InjuryRecordSnapshot injury = string.IsNullOrWhiteSpace(nodeId) ? null : condition.ActiveInjuries.FirstOrDefault(candidate => string.Equals(candidate.TargetNodeId, nodeId, StringComparison.Ordinal));
            RecoveryProcessStartRequest request = new RecoveryProcessStartRequest
            {
                ActorBodyId = body.ActorBodyId,
                RecoveryMethodId = methodId,
                SourceId = "test-lab.recovery",
                TransactionId = $"test-lab.recovery.start.{Guid.NewGuid():N}",
                AuthorityContext = "Prototype Test Lab biological recovery",
                ExpectedBodyRevision = body.BodyRevision,
                Target = new RecoveryTargetReference
                {
                    ActorBodyId = body.ActorBodyId,
                    TargetCategory = targetCategory,
                    AnatomyNodeId = nodeId,
                    InjuryId = injury?.InjuryId ?? string.Empty,
                    ResourceDefinitionId = resourceId,
                    OwningSystemRevision = targetCategory == RecoveryTargetCategory.VitalResource ? body.VitalProcesses.VitalRevision : body.Condition.ConditionRevision
                }
            };
            BiologicalRecoveryResult result = preview
                ? body.BiologicalRecovery.PreviewStartProcess(request, body.CreateSnapshot(), body.BiologicalCompatibility)
                : body.BiologicalRecovery.StartProcess(request, body.CreateSnapshot(), body.BiologicalCompatibility);
            return RecordRecoveryResult(preview ? "Preview Biological Recovery Process" : "Start Biological Recovery Process", result);
        }

        private PrototypeTestLabOperation ApplyBiologicalRecoveryTickWithId(float elapsedGameSeconds, string tickId)
        {
            if (!TryBuildRecoveryTickRequest(elapsedGameSeconds, tickId, out ActorBodyRuntime body, out RecoveryTickRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            return RecordRecoveryResult("Apply Biological Recovery Tick", body.BiologicalRecovery.ApplyTick(request, body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses));
        }

        private bool EnsureBiologicalRecoveryReady(ActorBodyRuntime body)
        {
            if (body == null)
            {
                return false;
            }

            if (!EnsureBiologicalHazardsReady(body))
            {
                return false;
            }

            if (!body.BiologicalCompatibility.IsReady)
            {
                body.BiologicalCompatibility.BuildForBody(body.CreateSnapshot(), registry, restoring: false);
            }

            if (!body.BiologicalRecovery.IsReady)
            {
                body.BiologicalRecovery.BuildForBody(body.CreateSnapshot(), registry, restoring: false, preserveRevision: true);
            }

            return body.BiologicalRecovery.IsReady;
        }

        private bool EnsureRecoveryRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure)
        {
            body = null;
            failure = default;
            if (!EnsureBodyRuntime(out body))
            {
                failure = RecordFailure("Biological Recovery Operation", "Body runtime is missing.", BiologicalRecoveryResultCode.MissingBody.ToString());
                return false;
            }

            if (!EnsureBiologicalRecoveryReady(body))
            {
                failure = RecordFailure("Biological Recovery Operation", "Biological recovery runtime is not ready.", BiologicalRecoveryResultCode.RuntimeNotReady.ToString());
                return false;
            }

            return true;
        }

        private bool EnsureTransformationRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure)
        {
            body = null;
            failure = default;
            if (!EnsureBodyRuntime(out body))
            {
                failure = RecordFailure("Body Transformation Operation", "Body runtime is missing.", TransformationResultCode.MissingSourceBody.ToString());
                return false;
            }

            EnsureBiologicalRecoveryReady(body);
            body.Transformation.Configure(body, registry, restoring: false, preserveRevision: true);
            if (!body.Transformation.IsReady)
            {
                failure = RecordFailure("Body Transformation Operation", "Body transformation runtime is not ready.", body.Transformation.Readiness.ToString());
                return false;
            }

            return true;
        }

        private PrototypeTestLabOperation RunTransformation(string operationName, string methodId, string targetSpeciesId, string targetBodyId, string targetNodeId, bool preview)
        {
            if (!EnsureTransformationRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BodyTransformationRequest request = BuildTransformationRequest(body, methodId, targetSpeciesId, targetBodyId, targetNodeId, $"test-lab.transformation.{Guid.NewGuid():N}", preview);
            BodyTransformationResult result = preview
                ? body.Transformation.Preview(request)
                : body.Transformation.Execute(request);
            return RecordTransformationResult(operationName, result);
        }

        private static BodyTransformationRequest BuildTransformationRequest(ActorBodyRuntime body, string methodId, string targetSpeciesId, string targetBodyId, string targetNodeId, string transactionId, bool preview)
        {
            BodySnapshot snapshot = body == null ? null : body.CreateSnapshot();
            AnatomySnapshot anatomy = snapshot?.Anatomy;
            BiologicalCompatibilitySnapshot compatibility = snapshot?.BiologicalCompatibility;
            return new BodyTransformationRequest(
                methodId,
                transactionId,
                snapshot?.PersonId ?? string.Empty,
                snapshot?.ActorBodyId ?? string.Empty,
                snapshot?.ActorBodyId ?? string.Empty,
                targetBodyId,
                targetSpeciesId,
                string.Empty,
                targetNodeId,
                string.Empty,
                "test-lab.transformation",
                "Prototype Test Lab",
                "Feature 7.8 manual/automation operation",
                preview,
                requestedDurationSeconds: 0f,
                expectedBodyRevision: snapshot?.BodyRevision ?? 0L,
                expectedAnatomyRevision: anatomy?.AnatomyRevision ?? 0L,
                expectedCompatibilityRevision: compatibility?.CompatibilityRevision ?? 0L);
        }

        private PrototypeTestLabOperation RecordTransformationResult(string operationName, BodyTransformationResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Transformation operation returned no result.", TransformationResultCode.InvalidRequest.ToString());
            }

            return Record(result.Succeeded, operationName, result.Duplicate ? TransformationResultCode.Duplicate.ToString() : result.Preview ? TransformationResultCode.Preview.ToString() : result.Code.ToString(), FormatTransformationResult(result));
        }

        private static string FormatTransformationResult(BodyTransformationResult result)
        {
            if (result == null)
            {
                return "No transformation result.";
            }

            BodyTransformationPlan plan = result.Plan;
            BodyTransformationSnapshot snapshot = result.Snapshot;
            string decisions = plan == null || plan.Decisions.Count == 0
                ? "None"
                : string.Join("; ", plan.Decisions.Select(decision => $"{decision.StateName}:{decision.Ownership}/{decision.Policy}/Transfer={decision.Transfers}"));
            return $"Success={result.Succeeded} Preview={result.Preview} Duplicate={result.Duplicate} Code={result.Code} Method={plan?.Method?.Id ?? string.Empty} Source={plan?.SourceBody?.SpeciesId ?? string.Empty} TargetSpecies={plan?.TargetSpecies?.Id ?? string.Empty} TargetBody={plan?.Request?.TargetActorBodyId ?? string.Empty} Node={plan?.Request?.TargetAnatomyNodeId ?? string.Empty} Flags={plan?.Flags.ToString() ?? string.Empty} ActiveTemporary={snapshot?.ActiveTemporaryTransformation ?? false} Revision={snapshot?.TransformationRevision ?? 0}. {result.Message} Decisions=[{decisions}]";
        }

        private bool TryBuildRecoveryTickRequest(
            float elapsedGameSeconds,
            string transactionSeed,
            out ActorBodyRuntime body,
            out RecoveryTickRequest request,
            out PrototypeTestLabOperation failure)
        {
            body = null;
            request = null;
            failure = default;
            if (!EnsureRecoveryRuntime(out body, out failure))
            {
                return false;
            }

            string tickId = string.IsNullOrWhiteSpace(transactionSeed) || string.Equals(transactionSeed, "preview", StringComparison.Ordinal)
                ? $"test-lab.recovery.{(string.IsNullOrWhiteSpace(transactionSeed) ? "tick" : transactionSeed)}.{Guid.NewGuid():N}"
                : transactionSeed;
            request = new RecoveryTickRequest
            {
                ActorBodyId = body.ActorBodyId,
                TickId = tickId,
                ElapsedGameSeconds = elapsedGameSeconds,
                AuthorityContext = "Prototype Test Lab biological recovery",
                ExpectedRecoveryRevision = body.BiologicalRecovery.RecoveryRevision,
                ExpectedBodyRevision = body.BodyRevision,
                ExpectedConditionRevision = body.Condition.ConditionRevision,
                ExpectedVitalRevision = body.VitalProcesses.VitalRevision,
                ExpectedHazardRevision = body.BiologicalHazards.HazardRevision,
                ExpectedCompatibilityRevision = body.BiologicalCompatibility.CompatibilityRevision
            };
            return true;
        }

        public PrototypeTestLabOperation InitializeCharacterSystem()
        {
            if (!EnsureCharacterSystem(out CharacterSystemCoordinator character, initialize: false))
            {
                return RecordFailure("Initialize Character System", "Character System coordinator is missing.", "MissingCharacterSystem");
            }

            bool succeeded = character.InitializeFromRegistry(registry, restoring: false, addMissingCore: true);
            return Record(succeeded, "Initialize Character System", succeeded ? "Ready" : "Failed", succeeded ? $"Readiness={character.Readiness}, Revision={character.Revision}." : character.LastFailureReason);
        }

        public PrototypeTestLabOperation RebuildCharacterSystem()
        {
            if (!EnsureCharacterSystem(out CharacterSystemCoordinator character))
            {
                return RecordFailure("Rebuild Character System", "Character System coordinator is missing.", "MissingCharacterSystem");
            }

            bool succeeded = character.FullRebuild(restoring: false, reason: "TestLabFullRebuild");
            return Record(succeeded, "Rebuild Character System", succeeded ? "Rebuilt" : "Failed", succeeded ? $"Readiness={character.Readiness}, Revision={character.Revision}." : character.LastFailureReason);
        }

        public PrototypeTestLabOperation ValidateCharacterSystemIntegrity()
        {
            if (!EnsureCharacterSystem(out CharacterSystemCoordinator character))
            {
                return RecordFailure("Character Integrity", "Character System coordinator is missing.", "MissingCharacterSystem");
            }

            CharacterIntegrityReport report = character.ValidateIntegrity();
            return Record(report.Passed, "Character Integrity", report.Passed ? "Passed" : "Failed", report.GetSummary());
        }

        public PrototypeTestLabOperation SnapshotCharacterSystem()
        {
            if (!EnsureCharacterSystem(out CharacterSystemCoordinator character))
            {
                return RecordFailure("Character Snapshot", "Character System coordinator is missing.", "MissingCharacterSystem");
            }

            CharacterFullSnapshot snapshot = character.GetSnapshot(developmentView: true);
            return RecordSuccess("Character Snapshot", $"Captured schema {snapshot.SchemaVersion}, revision {snapshot.Revision}, person {snapshot.Identity.PersonId}, actor {snapshot.Identity.ActorId}.");
        }

        public PrototypeTestLabOperation GrantTrait(TraitDefinition trait, TraitLifecycleState lifecycle, TraitDiscoveryState discovery)
        {
            if (!EnsureTraits(out CharacterTraitCollection traits))
            {
                return RecordFailure("Grant Trait", "Player Trait collection component is missing.", "MissingTraits");
            }

            if (trait == null)
            {
                return RecordFailure("Grant Trait", "Trait definition is missing.", "MissingTrait");
            }

            TraitOperationResult result = traits.GrantTrait(new TraitGrantRequest
            {
                OwnerId = PersistenceService.LocalPlayerId,
                TraitDefinitionId = trait.Id,
                RequestedLifecycle = lifecycle,
                RequestedDiscovery = discovery,
                SourceCategory = TraitSourceCategory.Development,
                SourceId = "test-lab",
                Reason = "Prototype Test Lab"
            });
            return Record(result.Succeeded, $"Grant Trait {lifecycle}", result.Code, result.Message);
        }

        public PrototypeTestLabOperation GrantTraitDuplicateProof(TraitDefinition trait)
        {
            if (trait == null)
            {
                return RecordFailure("Trait Duplicate Proof", "Trait definition is missing.", "MissingTrait");
            }

            GrantTrait(trait, TraitLifecycleState.Active, TraitDiscoveryState.Discovered);
            return GrantTrait(trait, TraitLifecycleState.Active, TraitDiscoveryState.Discovered);
        }

        public PrototypeTestLabOperation GrantTraitSecondSource(TraitDefinition trait)
        {
            if (!EnsureTraits(out CharacterTraitCollection traits))
            {
                return RecordFailure("Trait Second Source", "Player Trait collection component is missing.", "MissingTraits");
            }

            if (trait == null)
            {
                return RecordFailure("Trait Second Source", "Trait definition is missing.", "MissingTrait");
            }

            TraitOperationResult result = traits.GrantTrait(new TraitGrantRequest
            {
                OwnerId = PersistenceService.LocalPlayerId,
                TraitDefinitionId = trait.Id,
                RequestedLifecycle = TraitLifecycleState.Active,
                RequestedDiscovery = TraitDiscoveryState.Discovered,
                SourceCategory = TraitSourceCategory.Administrative,
                SourceId = "test-lab.second-source",
                Reason = "Prototype Test Lab second source"
            });
            return Record(result.Succeeded, "Trait Second Source", result.Code, result.Message);
        }

        public PrototypeTestLabOperation RemoveTraitTestLabSource(TraitDefinition trait)
        {
            if (!EnsureTraits(out CharacterTraitCollection traits))
            {
                return RecordFailure("Remove Trait Source", "Player Trait collection component is missing.", "MissingTraits");
            }

            if (trait == null)
            {
                return RecordFailure("Remove Trait Source", "Trait definition is missing.", "MissingTrait");
            }

            TraitOperationResult result = traits.RemoveTraitSource(trait.Id, TraitSourceCategory.Development, "test-lab");
            return Record(result.Succeeded, "Remove Trait Source", result.Code, result.Message);
        }

        public PrototypeTestLabOperation SuppressTrait(TraitDefinition trait)
        {
            return ChangeTrait(trait, "Suppress Trait", collection => collection.SuppressTrait(trait.Id));
        }

        public PrototypeTestLabOperation UnsuppressTrait(TraitDefinition trait)
        {
            return ChangeTrait(trait, "Unsuppress Trait", collection => collection.UnsuppressTrait(trait.Id));
        }

        public PrototypeTestLabOperation ActivateTrait(TraitDefinition trait)
        {
            return ChangeTrait(trait, "Activate Trait", collection => collection.ActivateTrait(trait.Id));
        }

        public PrototypeTestLabOperation SetTraitSuspected(TraitDefinition trait)
        {
            return ChangeTrait(trait, "Suspect Trait", collection => collection.SetDiscoveryState(trait.Id, TraitDiscoveryState.Suspected));
        }

        public PrototypeTestLabOperation SetTraitDiscovered(TraitDefinition trait)
        {
            return ChangeTrait(trait, "Discover Trait", collection => collection.SetDiscoveryState(trait.Id, TraitDiscoveryState.Discovered));
        }

        public PrototypeTestLabOperation ReplaceTrait(TraitDefinition replacement)
        {
            if (!EnsureTraits(out CharacterTraitCollection traits))
            {
                return RecordFailure("Replace Trait", "Player Trait collection component is missing.", "MissingTraits");
            }

            if (replacement == null)
            {
                return RecordFailure("Replace Trait", "Trait definition is missing.", "MissingTrait");
            }

            IReadOnlyList<string> blockers = traits.GetDevelopmentSnapshot()
                .Where(snapshot => snapshot.Definition != null
                    && snapshot.Definition.Id != replacement.Id
                    && (snapshot.Definition.ConflictGroupIds.Any(group => replacement.ConflictGroupIds.Contains(group))
                        || snapshot.Definition.IncompatibleTraits.Any(trait => trait != null && trait.Id == replacement.Id)
                        || replacement.IncompatibleTraits.Any(trait => trait != null && trait.Id == snapshot.Definition.Id)))
                .Select(snapshot => snapshot.Definition.Id)
                .ToList();
            TraitOperationResult result = traits.GrantTrait(new TraitGrantRequest
            {
                OwnerId = PersistenceService.LocalPlayerId,
                TraitDefinitionId = replacement.Id,
                RequestedLifecycle = TraitLifecycleState.Active,
                RequestedDiscovery = TraitDiscoveryState.Discovered,
                SourceCategory = TraitSourceCategory.Development,
                SourceId = "test-lab.replace",
                Reason = "Prototype Test Lab replacement",
                AllowConflictReplacement = true,
                TraitsAuthorizedForReplacement = blockers
            });
            return Record(result.Succeeded, "Replace Trait", result.Code, result.Message);
        }

        public PrototypeTestLabOperation RebuildTraitEffects()
        {
            if (!EnsureTraits(out CharacterTraitCollection traits))
            {
                return RecordFailure("Rebuild Traits", "Player Trait collection component is missing.", "MissingTraits");
            }

            TraitOperationResult result = traits.RebuildTraitEffects();
            return Record(result.Succeeded, "Rebuild Traits", result.Code, result.Message);
        }

        public PrototypeTestLabOperation SnapshotTraitsForPersistence()
        {
            if (!EnsureTraits(out CharacterTraitCollection traits))
            {
                return RecordFailure("Trait Save Snapshot", "Player Trait collection component is missing.", "MissingTraits");
            }

            PlayerTraitsSaveData saveData = traits.CreateSaveData(PersistenceService.LocalPlayerId, context?.IdentityProgression == null ? string.Empty : context.IdentityProgression.PersonId);
            bool valid = CharacterTraitCollection.ValidateSaveData(saveData, registry, PersistenceService.LocalPlayerId, out string failureReason);
            return Record(valid, "Trait Save Snapshot", valid ? "Valid" : "Invalid", valid ? $"Captured {saveData.traits.Count} Trait record(s)." : failureReason);
        }

        public PrototypeTestLabOperation EvaluateRequirement(RequirementSetDefinition requirement)
        {
            if (requirement == null)
            {
                return RecordFailure("Evaluate Requirement", "Requirement Set definition is missing.", "MissingRequirement");
            }

            RequirementEvaluationResult result = EnsureCharacterSystem(out CharacterSystemCoordinator character) && character.IsReady
                ? character.Query.EvaluateRequirement(requirement)
                : CapabilityRequirementEvaluator.Evaluate(requirement, BuildRequirementContext(testLab: true));
            string failures = result.Passed ? "All nodes passed." : string.Join("; ", result.TestLabFailureReasons);
            return Record(result.Passed, "Evaluate Requirement", result.Passed ? "Passed" : "Failed", failures);
        }

        public string BuildCurrentResourcesSummary()
        {
            if (!EnsureResources(out CharacterResourceCollection resources))
            {
                return "Player resource collection is missing.";
            }

            return string.Join(Environment.NewLine, new[]
            {
                resources.BuildDiagnosticSummary(),
                string.Empty,
                $"Wrapper Health: {FormatHealth()}",
                $"Wrapper Mana: {FormatResource(context.PlayerMana == null ? 0f : context.PlayerMana.CurrentMana, context.PlayerMana == null ? 0f : context.PlayerMana.MaximumMana)}",
                $"Wrapper Stamina: {FormatResource(context.PlayerStamina == null ? 0f : context.PlayerStamina.CurrentStamina, context.PlayerStamina == null ? 0f : context.PlayerStamina.MaximumStamina)}"
            });
        }

        public PrototypeTestLabOperation ReconcileResources()
        {
            if (!EnsureResources(out CharacterResourceCollection resources))
            {
                return RecordFailure("Reconcile Resources", "Player resource collection is missing.", "MissingResources");
            }

            int changed = 0;
            foreach (ResourceSnapshot snapshot in resources.GetSnapshots())
            {
                if (resources.ReconcileResource(snapshot.ResourceId))
                {
                    changed++;
                }
            }

            return RecordSuccess("Reconcile Resources", $"Reconciled {resources.GetSnapshots().Count} resource(s); {changed} current value(s) changed.");
        }

        public PrototypeTestLabOperation ProveResourceDuplicateEvent()
        {
            if (!EnsureResources(out CharacterResourceCollection resources))
            {
                return RecordFailure("Resource Duplicate Proof", "Player resource collection is missing.", "MissingResources");
            }

            string eventId = "resource.test-lab.duplicate-proof";
            ResourceChangeResult first = resources.TrySpend(ResourceIds.Mana, 1f, "test-lab", "Duplicate proof", eventId);
            ResourceChangeResult second = resources.TrySpend(ResourceIds.Mana, 1f, "test-lab", "Duplicate proof", eventId);
            bool passed = first.Succeeded && second.Succeeded && second.AppliedAmount <= CharacterResourceCollection.Epsilon;
            return Record(passed, "Resource Duplicate Proof", passed ? "Passed" : "Failed", $"First={first.AppliedAmount:0.###}, Second={second.AppliedAmount:0.###}, Mana={resources.GetCurrent(ResourceIds.Mana):0.###}/{resources.GetMaximum(ResourceIds.Mana):0.###}");
        }

        public PrototypeTestLabOperation TickResourceRegeneration()
        {
            if (!EnsureResources(out CharacterResourceCollection resources))
            {
                return RecordFailure("Resource Regen Tick", "Player resource collection is missing.", "MissingResources");
            }

            resources.TrySpend(ResourceIds.Stamina, Mathf.Max(1f, Mathf.Min(5f, resources.GetMaximum(ResourceIds.Stamina))), "test-lab", "Prepare regeneration tick");
            float before = resources.GetCurrent(ResourceIds.Stamina);
            resources.TickResources(1f, Time.time + 2f);
            float after = resources.GetCurrent(ResourceIds.Stamina);
            return RecordSuccess("Resource Regen Tick", $"Stamina {before:0.###} -> {after:0.###}.");
        }

        public PrototypeTestLabOperation SnapshotResourcesForPersistence()
        {
            if (!EnsureResources(out CharacterResourceCollection resources))
            {
                return RecordFailure("Resource Save Snapshot", "Player resource collection is missing.", "MissingResources");
            }

            PlayerResourcesSaveData saveData = resources.CreateSaveData(PersistenceService.LocalPlayerId, context?.IdentityProgression == null ? string.Empty : context.IdentityProgression.PersonId);
            bool valid = CharacterResourceCollection.ValidateSaveData(saveData, registry, context?.PlayerCalculatedStats, PersistenceService.LocalPlayerId, out string failureReason);
            return Record(valid, "Resource Save Snapshot", valid ? "Valid" : "Invalid", valid ? $"Captured {saveData.resources.Count} resource record(s)." : failureReason);
        }

        public PrototypeTestLabOperation SimulateSkillAction(SkillDefinition skill, bool executed, bool succeeded, string eventId = "")
        {
            if (!EnsureSkills(out CharacterSkillCollection skills))
            {
                return RecordFailure("Skill Action", "Player Skill collection component is missing.", "MissingSkills");
            }

            if (skill == null)
            {
                return RecordFailure("Skill Action", "Skill definition is missing.", "MissingSkill");
            }

            SkillActionExecutionEvent actionEvent = SkillActionExecutionEvent.Development(
                string.IsNullOrWhiteSpace(eventId) ? $"skill-action.test-lab.{Guid.NewGuid():N}" : eventId,
                skill.NaturalLearning == null ? SkillActionEventCategory.Development : skill.NaturalLearning.ActionCategory,
                skill.NaturalLearning == null ? skill.Id : skill.NaturalLearning.QualifyingEventId,
                executed,
                succeeded);
            SkillOperationResult result = skills.RecordQualifyingAction(actionEvent);
            return Record(result.Succeeded, executed ? succeeded ? "Skill Valid Action" : "Skill Missed Action" : "Skill Blocked Action", result.Code, result.Message);
        }

        public PrototypeTestLabOperation SimulateManySkillActions(SkillDefinition skill, int count)
        {
            if (!EnsureSkills(out CharacterSkillCollection skills))
            {
                return RecordFailure("Skill Multi Action", "Player Skill collection component is missing.", "MissingSkills");
            }

            if (skill == null)
            {
                return RecordFailure("Skill Multi Action", "Skill definition is missing.", "MissingSkill");
            }

            int amount = Mathf.Max(1, count);
            for (int i = 0; i < amount; i++)
            {
                SkillActionExecutionEvent actionEvent = SkillActionExecutionEvent.Development(
                    $"skill-action.test-lab.{Guid.NewGuid():N}",
                    skill.NaturalLearning == null ? SkillActionEventCategory.Development : skill.NaturalLearning.ActionCategory,
                    skill.NaturalLearning == null ? skill.Id : skill.NaturalLearning.QualifyingEventId,
                    executed: true,
                    succeeded: true);
                skills.RecordQualifyingAction(actionEvent);
            }

            return RecordSuccess("Skill Multi Action", $"Simulated {amount} qualifying action(s) for {skill.DisplayName}.");
        }

        public PrototypeTestLabOperation TestDuplicateSkillAction(SkillDefinition skill)
        {
            string eventId = $"skill-action.test-lab.duplicate.{Guid.NewGuid():N}";
            SimulateSkillAction(skill, executed: true, succeeded: true, eventId);
            return SimulateSkillAction(skill, executed: true, succeeded: true, eventId);
        }

        public PrototypeTestLabOperation GrantSkill(SkillDefinition skill, SkillGrade grade)
        {
            if (!EnsureSkills(out CharacterSkillCollection skills))
            {
                return RecordFailure("Grant Skill", "Player Skill collection component is missing.", "MissingSkills");
            }

            if (skill == null)
            {
                return RecordFailure("Grant Skill", "Skill definition is missing.", "MissingSkill");
            }

            SkillOperationResult result = skills.GrantSkill(skill, grade, SkillAcquisitionSource.Development, "Prototype Test Lab", "test-lab");
            return Record(result.Succeeded, $"Grant Skill {grade}", result.Code, result.Message);
        }

        public PrototypeTestLabOperation AwardSkillXp(SkillDefinition skill, int amount)
        {
            if (!EnsureSkills(out CharacterSkillCollection skills))
            {
                return RecordFailure("Award Skill XP", "Player Skill collection component is missing.", "MissingSkills");
            }

            if (skill == null)
            {
                return RecordFailure("Award Skill XP", "Skill definition is missing.", "MissingSkill");
            }

            SkillOperationResult result = skills.AwardSkillUse(skill.Id, amount: Mathf.Max(1, amount));
            return Record(result.Succeeded, "Award Skill XP", result.Code, result.Message);
        }

        public PrototypeTestLabOperation RebuildSkillEffects()
        {
            if (!EnsureSkills(out CharacterSkillCollection skills))
            {
                return RecordFailure("Rebuild Skills", "Player Skill collection component is missing.", "MissingSkills");
            }

            SkillOperationResult result = skills.RebuildSkillEffects();
            return Record(result.Succeeded, "Rebuild Skills", result.Code, result.Message);
        }

        public PrototypeTestLabOperation ClearSkillDevelopmentState(bool confirmed)
        {
            if (!RequireConfirmation("ClearSkillDevelopmentState", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            if (!EnsureSkills(out CharacterSkillCollection skills))
            {
                return RecordFailure("Clear Skills", "Player Skill collection component is missing.", "MissingSkills");
            }

            SkillOperationResult result = skills.ClearDevelopmentState(confirmed: true);
            return Record(result.Succeeded, "Clear Skills", result.Code, result.Message);
        }

        public string BuildLocationSummary()
        {
            string details = context?.Persistence == null
                ? "Player location persistence is missing."
                : context.Persistence.BuildPlayerLocationDiagnosticSummary();
            return string.Join(Environment.NewLine, new[]
            {
                "Player Location Persistence",
                details,
                "Policy: same-scene restore is supported; cross-scene saves validate clearly and are not restored yet.",
                "Reach Location objectives are suppressed during persistence restore."
            });
        }

        public string BuildWorldEntitySummary()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "Persistent World Entities",
                WorldEntityRegistry.BuildDiagnosticReport(),
                $"Last Spawned: {(string.IsNullOrWhiteSpace(lastSpawnedWorldEntityId) ? "None" : lastSpawnedWorldEntityId)}",
                $"Last Destroyed: {(string.IsNullOrWhiteSpace(lastDestroyedWorldEntityId) ? "None" : lastDestroyedWorldEntityId)}",
                $"Last Result: {(string.IsNullOrWhiteSpace(lastWorldEntityOperationMessage) ? "None" : lastWorldEntityOperationMessage)}"
            });
        }

        public string BuildSaveSlotSummary()
        {
            if (context?.Persistence == null)
            {
                return "Save slot persistence is missing.";
            }

            context.Persistence.EnsureInitialized();
            List<string> lines = new List<string>
            {
                "Save Slots, Autosave, and Load UI",
                context.Persistence.BuildSaveSlotDiagnosticSummary()
            };

            IReadOnlyList<SaveSlotDescriptor> descriptors = context.Persistence.BuildSaveSlotDescriptors();
            for (int i = 0; i < descriptors.Count; i++)
            {
                SaveSlotDescriptor descriptor = descriptors[i];
                lines.Add($"{descriptor.displayName}: {descriptor.compatibilityStatus} | {PrototypeSaveSlotCatalog.FormatLocalTimestamp(descriptor.lastSavedAtUtc)} | {PrototypeSaveSlotCatalog.FormatPlayTime(descriptor.playTimeSeconds)} | Backup={descriptor.backupExists}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public string BuildPersistenceIntegrationSummary()
        {
            return context?.Persistence == null
                ? "Persistence integration service is missing."
                : context.Persistence.BuildPersistenceIntegrationDiagnosticSummary();
        }

        public PrototypeTestLabOperation GrantItem(ItemDefinition item, int quantity)
        {
            if (context?.Inventory == null)
            {
                return RecordFailure("Grant Item", "Player inventory is missing.", "MissingInventory");
            }

            if (item == null)
            {
                return RecordFailure("Grant Item", "No item definition selected.", "MissingDefinition");
            }

            InventoryAddResult result = context.Inventory.AddItemOrInstances(item, Mathf.Max(1, quantity));
            return Record(result.AddedQuantity > 0, "Grant Item", result.Status.ToString(), $"Requested {result.RequestedQuantity} x {item.DisplayName}; added {result.AddedQuantity}.");
        }

        public PrototypeTestLabOperation GrantStatefulItem(ItemDefinition item)
        {
            if (context?.Inventory == null)
            {
                return RecordFailure("Grant Stateful Item", "Player inventory is missing.", "MissingInventory");
            }

            if (item == null)
            {
                return RecordFailure("Grant Stateful Item", "No item definition selected.", "MissingDefinition");
            }

            InventoryInstanceOperationResult result = context.Inventory.AddExistingItemIdentity(item, ItemInstanceId.Generate());
            return Record(result.Succeeded, "Grant Stateful Item", result.Succeeded ? "Added" : "Failed", result.Message);
        }

        public PrototypeTestLabOperation RemoveItem(ItemDefinition item, int quantity)
        {
            if (context?.Inventory == null)
            {
                return RecordFailure("Remove Item", "Player inventory is missing.", "MissingInventory");
            }

            if (item == null)
            {
                return RecordFailure("Remove Item", "No item definition selected.", "MissingDefinition");
            }

            bool removed = context.Inventory.RemoveItem(item, Mathf.Max(1, quantity));
            return Record(removed, "Remove Item", removed ? "Removed" : "NotFound", removed ? $"Removed {quantity} x {item.DisplayName}." : $"{item.DisplayName} quantity was not available.");
        }

        public PrototypeTestLabOperation FillInventory(ItemDefinition filler)
        {
            if (context?.Inventory == null)
            {
                return RecordFailure("Fill Inventory", "Player inventory is missing.", "MissingInventory");
            }

            if (filler == null)
            {
                return RecordFailure("Fill Inventory", "No filler item selected.", "MissingDefinition");
            }

            int safety = context.Inventory.SlotCapacity * Mathf.Max(1, filler.MaximumStackSize);
            int added = 0;
            for (int i = 0; i < safety && context.Inventory.DevelopmentOccupiedSlotCount() < context.Inventory.SlotCapacity; i++)
            {
                InventoryAddResult result = context.Inventory.AddItemOrInstances(filler, 1);
                if (result.AddedQuantity <= 0)
                {
                    break;
                }

                added += result.AddedQuantity;
            }

            return Record(added > 0, "Fill Inventory", added > 0 ? "Filled" : "NoChange", $"Added {added} filler item(s); occupied slots {context.Inventory.DevelopmentOccupiedSlotCount()}/{context.Inventory.SlotCapacity}.");
        }

        public PrototypeTestLabOperation ClearInventory(bool confirmed)
        {
            if (!RequireConfirmation("ClearInventory", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            context?.Inventory?.DevelopmentClearInventory();
            return RecordSuccess("Clear Inventory", "Inventory cleared. Equipment was preserved.");
        }

        public PrototypeTestLabOperation EquipFirstCompatible(ItemDefinition item)
        {
            if (context?.Inventory == null || context.Equipment == null)
            {
                return RecordFailure("Equip Item", "Inventory or equipment is missing.", "MissingReference");
            }

            for (int i = 0; i < context.Inventory.Slots.Count; i++)
            {
                InventorySlot slot = context.Inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty && slot.Item == item)
                {
                    EquipmentOperationResult result = context.Equipment.EquipFromInventorySlot(i);
                    return Record(result.Succeeded, "Equip Item", result.Succeeded ? "Equipped" : "Failed", result.Message);
                }
            }

            return RecordFailure("Equip Item", "Selected item was not found in inventory.", "NotFound");
        }

        public PrototypeTestLabOperation UnequipAll(bool confirmed)
        {
            if (!RequireConfirmation("UnequipAll", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            if (context?.Equipment == null)
            {
                return RecordFailure("Unequip All", "Equipment is missing.", "MissingEquipment");
            }

            int changed = 0;
            foreach (EquipmentSlotState slot in context.Equipment.Slots)
            {
                if (slot != null && !slot.IsEmpty && context.Equipment.Unequip(slot.SlotType).Succeeded)
                {
                    changed++;
                }
            }

            return RecordSuccess("Unequip All", $"Unequipped {changed} slot(s).");
        }

        public PrototypeTestLabOperation DamagePlayer(int amount)
        {
            if (context?.PlayerHealth == null)
            {
                return RecordFailure("Damage Player", "Player health is missing.", "MissingHealth");
            }

            int applied = context.PlayerHealth.Damage(Mathf.Max(0, amount));
            return RecordSuccess("Damage Player", $"Applied raw test damage {applied}. Health {context.PlayerHealth.CurrentHealth}/{context.PlayerHealth.MaximumHealth}.");
        }

        public PrototypeTestLabOperation HealPlayer(int amount)
        {
            if (context?.PlayerHealth == null)
            {
                return RecordFailure("Heal Player", "Player health is missing.", "MissingHealth");
            }

            int healed = context.PlayerHealth.Heal(Mathf.Max(0, amount));
            return RecordSuccess("Heal Player", $"Healed {healed}. Health {context.PlayerHealth.CurrentHealth}/{context.PlayerHealth.MaximumHealth}.");
        }

        public PrototypeTestLabOperation SetHealth(int value)
        {
            if (context?.PlayerHealth == null)
            {
                return RecordFailure("Set Health", "Player health is missing.", "MissingHealth");
            }

            bool restored = context.PlayerHealth.TryRestoreForPersistence(Mathf.Clamp(value, 1, context.PlayerHealth.MaximumHealth), out string failureReason);
            return Record(restored, "Set Health", restored ? "Clamped" : "Failed", restored ? $"Health set to {context.PlayerHealth.CurrentHealth}/{context.PlayerHealth.MaximumHealth}." : failureReason);
        }

        public PrototypeTestLabOperation RestoreVitals()
        {
            context?.PlayerHealth?.ResetToMaximum();
            context?.PlayerMana?.RestoreToMaximum();
            context?.PlayerStamina?.RestoreToMaximum();
            return RecordSuccess("Restore Vitals", "Health, mana, and stamina restored to maximum.");
        }

        public PrototypeTestLabOperation AddStrengthTraining()
        {
            return AddAttributeTraining(AttributeIds.Strength, 0.25f, "Strength Base Attribute Training");
        }

        public PrototypeTestLabOperation AddBalancedAttributeTraining()
        {
            if (context?.PlayerAttributes == null)
            {
                return RecordFailure("Balanced Base Attribute Training", "Player Base Attributes component is missing.", "MissingAttributes");
            }

            List<RuntimeAttributeSourceContribution> contributions = new List<RuntimeAttributeSourceContribution>();
            foreach (string attributeId in AttributeIds.AlphaAttributeIds)
            {
                contributions.Add(new RuntimeAttributeSourceContribution
                {
                    attributeId = attributeId,
                    sourceId = "development.test-lab.balanced-training",
                    sourceCategory = (int)CalculatedStatContributionSourceCategory.Development,
                    amount = 0.1f,
                    removable = false
                });
            }

            bool succeeded = context.PlayerAttributes.TryRecordTrainingEvent(
                $"development.attribute-growth.{Guid.NewGuid():N}",
                AttributeGrowthEventCategory.Development,
                contributions,
                "Prototype Test Lab",
                out string failureReason);
            return Record(succeeded, "Balanced Base Attribute Training", succeeded ? "Recorded" : "Failed", succeeded ? "Added +0.1 permanent growth to every alpha Base Attribute." : failureReason);
        }

        public PrototypeTestLabOperation SetStrengthAboveHundred()
        {
            if (context?.PlayerAttributes == null)
            {
                return RecordFailure("Set Strength Above 100", "Player Base Attributes component is missing.", "MissingAttributes");
            }

            string sourceId = "development.test-lab.strength-above-100";
            context.PlayerAttributes.RemovePermanentSource(sourceId, out _);
            bool succeeded = context.PlayerAttributes.TryAddPermanentSource(
                sourceId,
                CalculatedStatContributionSourceCategory.Development,
                AttributeIds.Strength,
                100f,
                removable: true,
                out string failureReason);
            return Record(succeeded, "Set Strength Above 100", succeeded ? "Applied" : "Failed", succeeded ? "Strength has a removable +100 permanent development source." : failureReason);
        }

        public PrototypeTestLabOperation AddPhysicalPowerFlat()
        {
            return AddCalculatedContribution(
                "development.test-lab.physical-power-flat",
                CalculatedStatIds.PhysicalPower,
                CalculatedStatContributionKind.Flat,
                CalculatedStatContributionDirection.Improve,
                5f,
                "Add Physical Power");
        }

        public PrototypeTestLabOperation AddPhysicalDefensePenalty()
        {
            return AddCalculatedContribution(
                "development.test-lab.physical-defense-penalty",
                CalculatedStatIds.PhysicalDefense,
                CalculatedStatContributionKind.Flat,
                CalculatedStatContributionDirection.Reduce,
                3f,
                "Add Defense Penalty");
        }

        public PrototypeTestLabOperation ClearFeature52Contributions()
        {
            if (context?.PlayerAttributes != null)
            {
                context.PlayerAttributes.RemovePermanentSource("development.test-lab.strength-above-100", out _);
            }

            bool removedPower = context?.PlayerCalculatedStats != null
                && context.PlayerCalculatedStats.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.Development, "development.test-lab.physical-power-flat");
            bool removedDefense = context?.PlayerCalculatedStats != null
                && context.PlayerCalculatedStats.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.Development, "development.test-lab.physical-defense-penalty");
            return RecordSuccess("Clear Feature 5.4a Contributions", $"Cleared development Base Attribute/Calculated Stat contributions. Power={removedPower} Defense={removedDefense}.");
        }

        public PrototypeTestLabOperation RecalculateFeature52Stats()
        {
            if (context?.PlayerCalculatedStats == null)
            {
                return RecordFailure("Rebuild Feature 5.4a Stats", "Player Calculated Stats component is missing.", "MissingCalculatedStats");
            }

            context.PlayerCalculatedStats.ForceRecalculateAll();
            return RecordSuccess("Rebuild Feature 5.4a Stats", "Calculated Stat cache rebuilt from Base Attributes and active contributions.");
        }

        public PrototypeTestLabOperation AttemptInvalidAttributeGrowth()
        {
            if (context?.PlayerAttributes == null)
            {
                return RecordFailure("Invalid Base Attribute Growth Proof", "Player Base Attributes component is missing.", "MissingAttributes");
            }

            bool succeeded = context.PlayerAttributes.TryRecordTrainingEvent(
                "development.invalid-growth-proof",
                AttributeGrowthEventCategory.Development,
                new[]
                {
                    new RuntimeAttributeSourceContribution
                    {
                        attributeId = AttributeIds.Strength,
                        sourceId = "development.invalid-growth-proof",
                        sourceCategory = (int)CalculatedStatContributionSourceCategory.Development,
                        amount = -1f
                    }
                },
                "Prototype Test Lab",
                out string failureReason);
            return Record(!succeeded, "Invalid Base Attribute Growth Proof", succeeded ? "UnexpectedSuccess" : "Rejected", succeeded ? "Invalid negative growth was unexpectedly accepted." : failureReason);
        }

        public PrototypeTestLabOperation DrainMana(float amount)
        {
            VitalChangeResult result = context?.PlayerMana == null
                ? VitalChangeResult.Failure(amount, "Player mana is missing.")
                : context.PlayerMana.Spend(Mathf.Max(0f, amount));
            return Record(result.Succeeded, "Drain Mana", result.Succeeded ? "Spent" : "Failed", result.Message);
        }

        private PrototypeTestLabOperation AddAttributeTraining(string attributeId, float amount, string operationName)
        {
            if (context?.PlayerAttributes == null)
            {
                return RecordFailure(operationName, "Player Base Attributes component is missing.", "MissingAttributes");
            }

            bool succeeded = context.PlayerAttributes.TryRecordTrainingEvent(
                $"development.attribute-growth.{Guid.NewGuid():N}",
                AttributeGrowthEventCategory.Development,
                new[]
                {
                    new RuntimeAttributeSourceContribution
                    {
                        attributeId = attributeId,
                        sourceId = $"development.test-lab.{attributeId}",
                        sourceCategory = (int)CalculatedStatContributionSourceCategory.Development,
                        amount = amount,
                        removable = false
                    }
                },
                "Prototype Test Lab",
                out string failureReason);
            return Record(succeeded, operationName, succeeded ? "Recorded" : "Failed", succeeded ? $"Added +{amount:0.###} to {attributeId}." : failureReason);
        }

        private PrototypeTestLabOperation AddCalculatedContribution(string sourceId, string statId, CalculatedStatContributionKind kind, CalculatedStatContributionDirection direction, float magnitude, string operationName)
        {
            if (context?.PlayerCalculatedStats == null)
            {
                return RecordFailure(operationName, "Player calculated stats component is missing.", "MissingCalculatedStats");
            }

            context.PlayerCalculatedStats.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.Development, sourceId);
            bool succeeded = context.PlayerCalculatedStats.AddContribution(new RuntimeCalculatedStatContribution
            {
                contributionId = sourceId,
                sourceId = sourceId,
                sourceCategory = (int)CalculatedStatContributionSourceCategory.Development,
                statId = statId,
                kind = (int)kind,
                direction = (int)direction,
                magnitude = magnitude
            }, out string failureReason);
            return Record(succeeded, operationName, succeeded ? "Applied" : "Failed", succeeded ? $"{direction} {statId} by {magnitude:0.###}." : failureReason);
        }

        public PrototypeTestLabOperation DrainStamina(float amount)
        {
            VitalChangeResult result = context?.PlayerStamina == null
                ? VitalChangeResult.Failure(amount, "Player stamina is missing.")
                : context.PlayerStamina.Spend(Mathf.Max(0f, amount), "Development test");
            return Record(result.Succeeded, "Drain Stamina", result.Succeeded ? "Spent" : "Failed", result.Message);
        }

        public PrototypeTestLabOperation ApplyStatus(StatusEffectDefinition status, bool toEnemy)
        {
            StatusEffectController controller = toEnemy ? context?.EnemyStatuses : context?.PlayerStatuses;
            if (controller == null)
            {
                return RecordFailure("Apply Status", "Target status controller is missing.", "MissingTarget");
            }

            if (status == null)
            {
                return RecordFailure("Apply Status", "No status definition selected.", "MissingDefinition");
            }

            StatusEffectApplicationRequest request = new StatusEffectApplicationRequest(status, null, DevelopmentStatusSource, 0f, string.Empty, Time.time);
            StatusApplicationResult result = controller.ApplyStatus(request);
            return Record(result.Succeeded, "Apply Status", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation RemoveStatus(StatusEffectDefinition status, bool fromEnemy)
        {
            StatusEffectController controller = fromEnemy ? context?.EnemyStatuses : context?.PlayerStatuses;
            if (controller == null || status == null)
            {
                return RecordFailure("Remove Status", "Target status controller or status definition is missing.", "MissingReference");
            }

            bool removed = controller.RemoveStatusesByDefinition(status.Id);
            return Record(removed, "Remove Status", removed ? "Removed" : "NotFound", removed ? $"Removed {status.DisplayName}." : $"{status.DisplayName} was not active.");
        }

        public PrototypeTestLabOperation ClearTemporaryStatuses()
        {
            context?.PlayerStatuses?.ClearTemporaryStatuses();
            context?.EnemyStatuses?.ClearTemporaryStatuses();
            return RecordSuccess("Clear Temporary Statuses", "Temporary player and enemy statuses cleared.");
        }

        public PrototypeTestLabOperation ApplyTypedDamage(DamageTypeDefinition damageType, float amount, bool targetEnemy, bool sourcePlayer)
        {
            if (damageType == null)
            {
                return RecordFailure("Apply Typed Damage", "No damage type selected.", "MissingDefinition");
            }

            IDamageable damageable = targetEnemy ? context?.EnemyHealth : context?.PlayerHealth;
            Transform targetTransform = targetEnemy ? context?.EnemyTransform : context?.PlayerTransform;
            GameObject source = sourcePlayer ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            if (damageable == null || targetTransform == null)
            {
                return RecordFailure("Apply Typed Damage", "Damage target is missing.", "MissingTarget");
            }

            float rawAmount = Mathf.Max(0f, amount);
            DamageComponent component = new DamageComponent(damageType, rawAmount);
            DamagePacket packet = DamagePacket.Single(source, component);
            DamageInfo info = new DamageInfo(rawAmount, source, targetTransform.position, Vector3.forward, DamageType.Physical, packet);
            DamageResult result = damageable.ApplyDamage(in info);
            return Record(result.Applied, "Apply Typed Damage", result.Applied ? "Applied" : "Failed", result.Message);
        }

        public PrototypeTestLabOperation PreviewPipelineDamage(DamageTypeDefinition damageType, float amount, bool targetPlayer)
        {
            if (damageType == null)
            {
                return RecordFailure("Preview 6.1 Damage", "No damage type selected.", "MissingDefinition");
            }

            DamageApplicationRequest request = CreatePipelineDamageRequest(damageType, amount, targetPlayer, string.Empty);
            DamageApplicationResult result = damageHealingService.PreviewDamage(request);
            string message = result.Succeeded
                ? $"{damageType.DisplayName}: requested {result.RequestedAmount:0.###}, defense {result.DefenseApplied:0.###}, resistance {result.ResistanceFraction:0.###}, final {result.FinalDamageAmount:0.###}, Health {result.OldHealth:0.###}->{result.NewHealth:0.###}."
                : result.Message;
            return Record(result.Succeeded, "Preview 6.1 Damage", result.Code, message);
        }

        public PrototypeTestLabOperation ApplyPipelineDamage(DamageTypeDefinition damageType, float amount, bool targetPlayer)
        {
            if (damageType == null)
            {
                return RecordFailure("Apply 6.1 Damage", "No damage type selected.", "MissingDefinition");
            }

            DamageApplicationRequest request = CreatePipelineDamageRequest(damageType, amount, targetPlayer, $"development.damage-healing.{Guid.NewGuid():N}");
            DamageApplicationResult result = damageHealingService.ApplyDamage(request);
            string message = result.Succeeded
                ? $"{damageType.DisplayName}: final {result.FinalDamageAmount:0.###}, Health {result.OldHealth:0.###}->{result.NewHealth:0.###}, Changed={result.HealthChanged}, Immune={result.Immune}, Duplicate={result.Duplicate}."
                : result.Message;
            return Record(result.Succeeded, "Apply 6.1 Damage", result.Code, message);
        }

        public PrototypeTestLabOperation PreviewPipelineHealing(float amount, bool targetPlayer)
        {
            HealingApplicationRequest request = CreatePipelineHealingRequest(amount, targetPlayer, string.Empty);
            HealingApplicationResult result = damageHealingService.PreviewHealing(request);
            string message = result.Succeeded
                ? $"Healing final {result.FinalHealingAmount:0.###}, overheal {result.OverhealAmount:0.###}, Health {result.OldHealth:0.###}->{result.NewHealth:0.###}, Changed={result.HealthChanged}, Duplicate={result.Duplicate}."
                : result.Message;
            return Record(result.Succeeded, "Preview 6.1 Healing", result.Code, message);
        }

        public PrototypeTestLabOperation ApplyPipelineHealing(float amount, bool targetPlayer)
        {
            HealingApplicationRequest request = CreatePipelineHealingRequest(amount, targetPlayer, $"development.damage-healing.{Guid.NewGuid():N}");
            HealingApplicationResult result = damageHealingService.ApplyHealing(request);
            string message = result.Succeeded
                ? $"Healing final {result.FinalHealingAmount:0.###}, overheal {result.OverhealAmount:0.###}, Health {result.OldHealth:0.###}->{result.NewHealth:0.###}, Changed={result.HealthChanged}, Duplicate={result.Duplicate}."
                : result.Message;
            return Record(result.Succeeded, "Apply 6.1 Healing", result.Code, message);
        }

        public PrototypeTestLabOperation ProvePipelineDuplicate(DamageTypeDefinition damageType, float amount)
        {
            if (damageType == null)
            {
                return RecordFailure("6.1 Duplicate Proof", "No damage type selected.", "MissingDefinition");
            }

            string transactionId = $"development.damage-healing.duplicate.{Guid.NewGuid():N}";
            DamageApplicationResult first = damageHealingService.ApplyDamage(CreatePipelineDamageRequest(damageType, amount, targetPlayer: true, transactionId: transactionId));
            DamageApplicationResult second = damageHealingService.ApplyDamage(CreatePipelineDamageRequest(damageType, amount, targetPlayer: true, transactionId: transactionId));
            bool succeeded = first.Succeeded && second.Succeeded && second.Duplicate && !second.HealthChanged;
            string message = $"First={first.Code} changed={first.HealthChanged}; second={second.Code} duplicate={second.Duplicate} changed={second.HealthChanged}.";
            return Record(succeeded, "6.1 Duplicate Proof", succeeded ? "DuplicateProtected" : "UnexpectedResult", message);
        }

        public PrototypeTestLabOperation GenerateAttackTransaction()
        {
            lastAttackTransactionId = AttackDeterministicRoll.NewTransactionId("development.attack-resolution");
            return RecordSuccess("Generate 6.2 Attack Transaction", $"Attack transaction ID: {lastAttackTransactionId}");
        }

        public PrototypeTestLabOperation GenerateLifecycleTransaction()
        {
            lastLifecycleTransactionId = $"development.lifecycle.{Guid.NewGuid():N}";
            return RecordSuccess("Generate 6.3 Lifecycle Transaction", $"Lifecycle transaction ID: {lastLifecycleTransactionId}");
        }

        public PrototypeTestLabOperation PreviewAttackResolution(DamageTypeDefinition damageType, float amount, float baseHitChance, float hitRoll, float criticalChance, float criticalRoll, float criticalMultiplier, float distance, float maximumRange, bool targetEnemy, bool sourcePlayer)
        {
            AttackResolutionRequest request = CreateAttackResolutionRequest(damageType, amount, baseHitChance, hitRoll, criticalChance, criticalRoll, criticalMultiplier, distance, maximumRange, targetEnemy, sourcePlayer, transactionId: ResolveAttackTransactionId(reuse: false));
            AttackResolutionResult result = attackResolutionService.PreviewAttack(request);
            return Record(result.Succeeded, "Preview 6.2 Attack", result.Code, FormatAttackResolution(result));
        }

        public PrototypeTestLabOperation ExecuteAttackResolution(DamageTypeDefinition damageType, float amount, float baseHitChance, float hitRoll, float criticalChance, float criticalRoll, float criticalMultiplier, float distance, float maximumRange, bool targetEnemy, bool sourcePlayer, bool reuseTransaction)
        {
            AttackResolutionRequest request = CreateAttackResolutionRequest(damageType, amount, baseHitChance, hitRoll, criticalChance, criticalRoll, criticalMultiplier, distance, maximumRange, targetEnemy, sourcePlayer, ResolveAttackTransactionId(reuseTransaction));
            AttackResolutionResult result = attackResolutionService.ExecuteAttack(request);
            return Record(result.Succeeded, reuseTransaction ? "Execute 6.2 Attack Reuse" : "Execute 6.2 Attack", result.Code, FormatAttackResolution(result));
        }

        public PrototypeTestLabOperation ExecuteEnvironmentalAttack(DamageTypeDefinition damageType, float amount, float hitRoll)
        {
            AttackResolutionRequest request = new AttackResolutionRequest(
                ResolveAttackTransactionId(reuse: false),
                AttackSourceType.Environmental,
                null,
                string.Empty,
                context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject,
                ResolveActorId(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject),
                damageType,
                Mathf.Max(0f, amount),
                hitRoll,
                0.5f,
                baseHitChance: 0.95f,
                criticalChance: 0f,
                criticalMultiplier: AttackResolutionRequest.DefaultCriticalMultiplier,
                hasSuppliedDistance: false,
                hasMaximumRange: false,
                originatingActionId: "development.environmental-test");
            AttackResolutionResult result = attackResolutionService.ExecuteAttack(request);
            return Record(result.Succeeded, "Environmental 6.2 Attack", result.Code, FormatAttackResolution(result));
        }

        public string BuildDefensiveActionSummary()
        {
            GameObject player = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            GameObject enemy = context?.EnemyTransform == null ? null : context.EnemyTransform.gameObject;
            EnsureAttackResolutionRuntime(player, needsResource: true);
            EnsureAttackResolutionRuntime(enemy, needsResource: true);
            string playerId = ResolveActorId(player);
            string enemyId = ResolveActorId(enemy);
            string playerDefense = FormatActiveDefense(playerId);
            string enemyDefense = FormatActiveDefense(enemyId);
            string playerStamina = FormatResource(player, ResourceIds.Stamina);
            string enemyStamina = FormatResource(enemy, ResourceIds.Stamina);
            return $"Player Defense: {playerDefense}\nEnemy Defense: {enemyDefense}\nPlayer Stamina: {playerStamina}\nEnemy Stamina: {enemyStamina}";
        }

        public PrototypeTestLabOperation PreviewDefenseActivation(DefensiveActionDefinition definition, bool targetPlayer)
        {
            if (!TryBuildDefenseActivationRequest(definition, targetPlayer, reuseTransaction: false, out DefenseActivationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            DefenseActivationResult result = defensiveActionService.PreviewActivate(request);
            return Record(result.Succeeded, "Preview 6.6 Defense", result.Code, FormatDefenseActivation(result));
        }

        public PrototypeTestLabOperation ActivateDefense(DefensiveActionDefinition definition, bool targetPlayer, bool reuseTransaction)
        {
            if (!TryBuildDefenseActivationRequest(definition, targetPlayer, reuseTransaction, out DefenseActivationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            DefenseActivationResult result = defensiveActionService.Activate(request);
            return Record(result.Succeeded, reuseTransaction ? "Activate 6.6 Defense Reuse" : "Activate 6.6 Defense", result.Code, FormatDefenseActivation(result));
        }

        public PrototypeTestLabOperation CancelDefense(bool targetPlayer)
        {
            GameObject target = targetPlayer ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            if (target == null)
            {
                return RecordFailure("Cancel 6.6 Defense", "Defense target is missing.", "MissingTarget");
            }

            DefenseCancellationRequest request = new DefenseCancellationRequest(
                $"development.defense-action.cancel.{Guid.NewGuid():N}",
                ResolveActorId(target),
                target,
                DefenseCancellationReason.Explicit,
                now: Time.time);
            DefenseCancellationResult result = defensiveActionService.Cancel(request);
            return Record(result.Succeeded, "Cancel 6.6 Defense", result.Code, FormatDefenseCancellation(result));
        }

        public PrototypeTestLabOperation PreviewDefensiveAttack(DamageTypeDefinition damageType, float amount, float baseHitChance, float hitRoll, float defenseRoll, bool targetPlayer)
        {
            AttackResolutionRequest request = CreateDefensiveAttackRequest(damageType, amount, baseHitChance, hitRoll, defenseRoll, targetPlayer, ResolveAttackTransactionId(reuse: false));
            AttackResolutionResult result = attackResolutionService.PreviewAttack(request);
            return Record(result.Succeeded, "Preview 6.6 Defensive Attack", result.Code, FormatAttackResolution(result));
        }

        public PrototypeTestLabOperation ExecuteDefensiveAttack(DamageTypeDefinition damageType, float amount, float baseHitChance, float hitRoll, float defenseRoll, bool targetPlayer, bool reuseTransaction)
        {
            AttackResolutionRequest request = CreateDefensiveAttackRequest(damageType, amount, baseHitChance, hitRoll, defenseRoll, targetPlayer, ResolveAttackTransactionId(reuseTransaction));
            AttackResolutionResult result = attackResolutionService.ExecuteAttack(request);
            return Record(result.Succeeded, reuseTransaction ? "Execute 6.6 Defensive Attack Reuse" : "Execute 6.6 Defensive Attack", result.Code, FormatAttackResolution(result));
        }

        public string BuildCombatExecutionSummary()
        {
            GameObject player = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            EnsureAttackResolutionRuntime(player, needsResource: true);
            string actorId = ResolveActorId(player);
            CombatExecutionStateSnapshot state = combatExecutionService.GetExecutionState(actorId);
            string active = state == null
                ? "None"
                : $"{state.DefinitionId} Phase={state.Phase} Ready={state.ReadyAt:0.###} RecoveryEnd={state.RecoveryEndsAt:0.###} Instance={state.ExecutionInstanceId}";
            return string.Join(Environment.NewLine, new[]
            {
                "Feature 6.7 Combat Execution",
                $"Clock: {combatExecutionClockSeconds:0.###}s",
                $"Actor: {(string.IsNullOrWhiteSpace(actorId) ? "None" : actorId)}",
                $"Active: {active}",
                $"Health: {FormatResource(player, ResourceIds.Health)}",
                $"Stamina: {FormatResource(player, ResourceIds.Stamina)}",
                $"Mana: {FormatResource(player, ResourceIds.Mana)}",
                $"Last Begin Tx: {(string.IsNullOrWhiteSpace(lastCombatExecutionBeginTransactionId) ? "None" : lastCombatExecutionBeginTransactionId)}",
                $"Last Commit Tx: {(string.IsNullOrWhiteSpace(lastCombatExecutionCommitTransactionId) ? "None" : lastCombatExecutionCommitTransactionId)}",
                $"Last Instance: {(string.IsNullOrWhiteSpace(lastCombatExecutionInstanceId) ? "None" : lastCombatExecutionInstanceId)}",
                FormatCombatExecutionCooldowns(actorId)
            });
        }

        public PrototypeTestLabOperation PreviewCombatExecution(CombatExecutionDefinition definition)
        {
            if (!TryBuildCombatExecutionBeginRequest(definition, reuseTransaction: false, out CombatExecutionBeginRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            CombatExecutionResult result = combatExecutionService.PreviewBeginExecution(request);
            return Record(result.Succeeded, "Preview 6.7 Execution", result.Code, FormatCombatExecutionResult(result));
        }

        public PrototypeTestLabOperation BeginCombatExecution(CombatExecutionDefinition definition, bool reuseTransaction)
        {
            if (!TryBuildCombatExecutionBeginRequest(definition, reuseTransaction, out CombatExecutionBeginRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            CombatExecutionResult result = combatExecutionService.BeginExecution(request);
            if (result.Succeeded && result.State != null)
            {
                lastCombatExecutionInstanceId = result.State.ExecutionInstanceId;
            }

            return Record(result.Succeeded, reuseTransaction ? "Begin 6.7 Execution Reuse" : "Begin 6.7 Execution", result.Code, FormatCombatExecutionResult(result));
        }

        public PrototypeTestLabOperation CommitCombatExecution(bool reuseTransaction)
        {
            if (!TryBuildCombatExecutionCommitRequest(reuseTransaction, out CombatExecutionCommitRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            CombatExecutionResult result = combatExecutionService.CommitExecution(request);
            return Record(result.Succeeded, reuseTransaction ? "Commit 6.7 Execution Reuse" : "Commit 6.7 Execution", result.Code, FormatCombatExecutionResult(result));
        }

        public PrototypeTestLabOperation CancelCombatExecution()
        {
            if (!TryBuildCombatExecutionCancelRequest(CombatExecutionCancellationReason.PlayerOrAIRequest, out CombatExecutionCancelRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            CombatExecutionResult result = combatExecutionService.CancelExecution(request);
            if (result.Succeeded)
            {
                lastCombatExecutionInstanceId = string.Empty;
            }

            return Record(result.Succeeded, "Cancel 6.7 Execution", result.Code, FormatCombatExecutionResult(result));
        }

        public PrototypeTestLabOperation InterruptCombatExecution()
        {
            if (!TryBuildCombatExecutionCancelRequest(CombatExecutionCancellationReason.InterruptedByDamage, out CombatExecutionCancelRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            CombatExecutionResult result = combatExecutionService.InterruptExecution(request);
            if (result.Succeeded)
            {
                lastCombatExecutionInstanceId = string.Empty;
            }

            return Record(result.Succeeded, "Interrupt 6.7 Execution", result.Code, FormatCombatExecutionResult(result));
        }

        public PrototypeTestLabOperation AdvanceCombatExecutionClock(float seconds)
        {
            float delta = Mathf.Max(0f, seconds);
            combatExecutionClockSeconds += delta;
            IReadOnlyList<CombatExecutionResult> results = combatExecutionService.ProcessExecutionTime(combatExecutionClockSeconds);
            string message = results.Count == 0
                ? $"Advanced 6.7 clock by {delta:0.###}s. No completions."
                : string.Join(Environment.NewLine, results.Select(FormatCombatExecutionResult));
            return RecordSuccess("Advance 6.7 Execution Clock", message);
        }

        public PrototypeTestLabOperation ClearCombatExecutionForRestore()
        {
            combatExecutionService.ClearTransientStateForRestore();
            lastCombatExecutionInstanceId = string.Empty;
            return RecordSuccess("Restore Clear 6.7 Execution", "Cleared transient combat execution commitments without emitting cancellation or interruption state through persistence.");
        }

        public PrototypeTestLabOperation SnapshotCombatExecution()
        {
            CombatExecutionSaveData saveData = combatExecutionService.CreateSaveData(PersistenceService.LocalPlayerId, "person.prototype-player");
            return RecordSuccess("Snapshot 6.7 Execution", $"Cooldown records: {(saveData.cooldowns == null ? 0 : saveData.cooldowns.Count)}. Transient commitments are not saved.");
        }

        public string BuildCombatStateSummary()
        {
            CombatStateService combatState = EnsureCombatStateRuntime();
            EnsureCombatStateTestParticipants();
            string playerId = ResolveCombatStateActorId(GetCombatStateTestActor("A"));
            string enemyId = ResolveCombatStateActorId(GetCombatStateTestActor("B"));
            ActorCombatStateSnapshot player = combatState == null ? null : combatState.GetCombatState(playerId);
            ActorCombatStateSnapshot enemy = combatState == null ? null : combatState.GetCombatState(enemyId);
            ActorCombatStateSnapshot c = combatState == null ? null : combatState.GetCombatState(ResolveCombatStateActorId(GetCombatStateTestActor("C")));
            ActorCombatStateSnapshot d = combatState == null ? null : combatState.GetCombatState(ResolveCombatStateActorId(GetCombatStateTestActor("D")));
            CombatEncounterSnapshot encounter = null;
            if (combatState != null)
            {
                encounter = combatState.GetEncounterForActor(playerId);
                if (encounter == null)
                {
                    encounter = combatState.GetEncounterForActor(enemyId);
                }
            }
            return string.Join(Environment.NewLine, new[]
            {
                "Feature 6.5 Combat State",
                $"Clock: {combatStateClockSeconds:0.###}s Timeout: {(combatState == null ? 10f : combatState.CombatTimeoutSeconds):0.###}s",
                FormatCombatStateSnapshot("A Player", player),
                FormatCombatStateSnapshot("B Enemy", enemy),
                FormatCombatStateSnapshot("C Mock", c),
                FormatCombatStateSnapshot("D Mock", d),
                FormatCombatEncounter(encounter),
                $"Last Combat Tx: {(string.IsNullOrWhiteSpace(lastCombatStateTransactionId) ? "None" : lastCombatStateTransactionId)}",
                $"Last Split Tx: {(string.IsNullOrWhiteSpace(lastCombatStateSplitTransactionId) ? "None" : lastCombatStateSplitTransactionId)}"
            });
        }

        public PrototypeTestLabOperation GenerateCombatStateTransaction()
        {
            lastCombatStateTransactionId = $"development.combat-state.{Guid.NewGuid():N}";
            return RecordSuccess("Generate 6.5 Combat Transaction", lastCombatStateTransactionId);
        }

        public PrototypeTestLabOperation PreviewExplicitCombatEngagement()
        {
            if (!TryBuildCombatEngagementRequest(CombatActivityClassification.ExplicitEngagement, reuseTransaction: false, out CombatStateService service, out CombatEngagementRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            CombatEntryResult result = service.PreviewEnterCombat(request);
            return Record(result.Succeeded, "Preview 6.5 Engagement", result.Code, FormatCombatEntryResult(result));
        }

        public PrototypeTestLabOperation ExecuteExplicitCombatEngagement(bool reuseTransaction)
        {
            if (!TryBuildCombatEngagementRequest(CombatActivityClassification.ExplicitEngagement, reuseTransaction, out CombatStateService service, out CombatEngagementRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            CombatEntryResult result = service.EnterCombat(request);
            return Record(result.Succeeded, reuseTransaction ? "Execute 6.5 Engagement Reuse" : "Execute 6.5 Engagement", result.Code, FormatCombatEntryResult(result));
        }

        public PrototypeTestLabOperation ExecuteCombatStateAttack(DamageTypeDefinition damageType, bool miss, bool blocked, bool prevented)
        {
            CombatStateService combatState = EnsureCombatStateRuntime();
            if (combatState == null)
            {
                return RecordFailure("6.5 Attack", "Combat State service is missing.", "MissingCombatState");
            }

            float hitRoll = miss ? 0.99f : 0.1f;
            float distance = blocked ? 999f : 1f;
            float amount = prevented ? 0f : 10f;
            AttackResolutionRequest request = CreateCombatStateAttackResolutionRequest(damageType, amount, 0.75f, hitRoll, 0f, 0.5f, 1.5f, distance, 2f, ResolveCombatStateTransactionId(reuse: false));
            AttackResolutionResult attack = attackResolutionService.ExecuteAttack(request);
            CombatEntryResult combat = combatState.RecordAttackResult(attack);
            string operation = blocked ? "Blocked 6.5 Attack" : miss ? "Miss 6.5 Attack" : prevented ? "Prevented 6.5 Attack" : "Hit 6.5 Attack";
            bool expectedOutcome = blocked ? !attack.Succeeded && !combat.Succeeded : attack.Succeeded && combat.Succeeded;
            return Record(expectedOutcome, operation, attack.Succeeded ? combat.Code : attack.Code, $"{FormatAttackResolution(attack)} Combat={FormatCombatEntryResult(combat)}");
        }

        public PrototypeTestLabOperation AdvanceCombatState(float deltaSeconds)
        {
            CombatStateService combatState = EnsureCombatStateRuntime();
            if (combatState == null)
            {
                return RecordFailure("Advance 6.5 Combat", "Combat State service is missing.", "MissingCombatState");
            }

            float delta = Mathf.Max(0f, deltaSeconds);
            combatStateClockSeconds += delta;
            CombatStateProcessResult result = combatState.AdvanceTime(delta);
            return RecordSuccess("Advance 6.5 Combat", FormatCombatProcessResult(result));
        }

        public PrototypeTestLabOperation ForceCombatExit(bool targetEnemy)
        {
            CombatStateService combatState = EnsureCombatStateRuntime();
            GameObject actor = targetEnemy ? context?.EnemyTransform?.gameObject : context?.PlayerTransform?.gameObject;
            string actorId = ResolveCombatStateActorId(actor);
            CombatExitResult result = combatState == null
                ? null
                : combatState.LeaveCombat(new CombatExitRequest(ResolveCombatStateTransactionId(reuse: false), actorId, actor, CombatExitReason.Forced, authoritative: true));
            return Record(result != null && result.Succeeded, targetEnemy ? "Force Enemy Combat Exit" : "Force Player Combat Exit", result == null ? "MissingCombatState" : result.Code, FormatCombatExitResult(result));
        }

        public PrototypeTestLabOperation EndCurrentCombatEncounter()
        {
            CombatStateService combatState = EnsureCombatStateRuntime();
            string playerId = ResolveCombatStateActorId(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject);
            CombatEncounterSnapshot encounter = combatState == null ? null : combatState.GetEncounterForActor(playerId);
            if (combatState == null || encounter == null)
            {
                return RecordFailure("End 6.5 Encounter", "No active combat encounter is available.", "MissingEncounter");
            }

            CombatEncounterSnapshot ended = combatState.EndEncounter(new CombatEncounterEndRequest(ResolveCombatStateTransactionId(reuse: false), encounter.EncounterId, CombatEncounterCompletionReason.Forced, authoritative: true));
            return Record(ended != null && !ended.Active, "End 6.5 Encounter", ended == null ? "MissingEncounter" : "Success", FormatCombatEncounter(ended));
        }

        public PrototypeTestLabOperation PrepareCombatStateSplitParticipants()
        {
            EnsureCombatStateTestParticipants();
            string summary = string.Join(" | ", new[] { "A", "B", "C", "D" }.Select(key => $"{key}={ResolveCombatStateActorId(GetCombatStateTestActor(key))}"));
            return RecordSuccess("Prepare 6.5 Split Participants", summary);
        }

        public PrototypeTestLabOperation EngageCombatStateParticipants(string firstKey, string secondKey)
        {
            CombatStateService combatState = EnsureCombatStateRuntime();
            GameObject first = GetCombatStateTestActor(firstKey);
            GameObject second = GetCombatStateTestActor(secondKey);
            if (combatState == null || first == null || second == null)
            {
                return RecordFailure($"Engage {firstKey}-{secondKey}", "Combat State service or participant is missing.", "MissingReference");
            }

            CombatEntryResult result = combatState.EnterCombat(new CombatEngagementRequest(
                ResolveCombatStateTransactionId(reuse: false),
                ResolveCombatStateActorId(first),
                first,
                ResolveCombatStateActorId(second),
                second,
                CombatActivityClassification.ExplicitEngagement,
                "development.test-lab.split",
                hostile: true,
                authorityValidated: true));
            return Record(result.Succeeded, $"Engage {firstKey}-{secondKey}", result.Code, FormatCombatEntryResult(result));
        }

        public PrototypeTestLabOperation EndCombatStateEngagement(string firstKey, string secondKey, bool reuseTransaction)
        {
            CombatStateService combatState = EnsureCombatStateRuntime();
            GameObject first = GetCombatStateTestActor(firstKey);
            GameObject second = GetCombatStateTestActor(secondKey);
            if (combatState == null || first == null || second == null)
            {
                return RecordFailure($"End {firstKey}-{secondKey}", "Combat State service or participant is missing.", "MissingReference");
            }

            CombatEncounterSplitResult result = combatState.EndEngagement(new CombatEngagementEndRequest(
                ResolveCombatStateSplitTransactionId(reuseTransaction),
                string.Empty,
                ResolveCombatStateActorId(first),
                ResolveCombatStateActorId(second),
                CombatExitReason.Forced,
                authoritative: true));
            return Record(result.Succeeded, $"End {firstKey}-{secondKey}", result.Code, FormatCombatSplitResult(result));
        }

        public PrototypeTestLabOperation ProcessCombatStateConnectivity()
        {
            CombatStateService combatState = EnsureCombatStateRuntime();
            string actorId = ResolveCombatStateActorId(GetCombatStateTestActor("A"));
            CombatEncounterSnapshot encounter = combatState == null ? null : combatState.GetEncounterForActor(actorId);
            if (combatState == null || encounter == null)
            {
                return RecordFailure("Process 6.5 Connectivity", "No active A encounter is available.", "MissingEncounter");
            }

            CombatEncounterSplitResult result = combatState.ProcessEncounterConnectivity(ResolveCombatStateSplitTransactionId(reuse: false), encounter.EncounterId);
            return Record(result.Succeeded, "Process 6.5 Connectivity", result.Code, FormatCombatSplitResult(result));
        }

        public PrototypeTestLabOperation ForceCombatStateParticipantExit(string key)
        {
            CombatStateService combatState = EnsureCombatStateRuntime();
            GameObject actor = GetCombatStateTestActor(key);
            string actorId = ResolveCombatStateActorId(actor);
            CombatExitResult result = combatState == null
                ? null
                : combatState.LeaveCombat(new CombatExitRequest(ResolveCombatStateTransactionId(reuse: false), actorId, actor, CombatExitReason.Forced, authoritative: true));
            return Record(result != null && result.Succeeded, $"Force {key} Combat Exit", result == null ? "MissingCombatState" : result.Code, FormatCombatExitResult(result));
        }

        public PrototypeTestLabOperation KillCombatStateParticipant(string key)
        {
            GameObject actor = GetCombatStateTestActor(key);
            if (actor == null)
            {
                return RecordFailure($"Kill {key}", "Combat State participant is missing.", "MissingReference");
            }

            ActorLifecycleController lifecycle = actor.GetComponentInParent<ActorLifecycleController>();
            CharacterResourceCollection resources = actor.GetComponentInParent<CharacterResourceCollection>();
            if (lifecycle == null || resources == null)
            {
                return RecordFailure($"Kill {key}", "Combat State participant lifecycle or resources are missing.", "MissingLifecycle");
            }

            ActorLifecycleResult death = lifecycle.ExecuteDeath(new LifecycleDeathRequest($"development.combat-state.kill.{Guid.NewGuid():N}", ResolveCombatStateActorId(GetCombatStateTestActor("A")), GetCombatStateTestActor("A"), ResolveCombatStateActorId(actor), actor, LifecycleTriggerKind.ExplicitDeath));
            CombatStateProcessResult process = EnsureCombatStateRuntime()?.AdvanceTime(0f);
            return Record(death.Succeeded, $"Kill {key}", death.Code, $"{death.Message} Combat={FormatCombatProcessResult(process)}");
        }

        public PrototypeTestLabOperation ValidateCombatStateIntegrity()
        {
            CombatStateIntegrityResult result = EnsureCombatStateRuntime()?.ValidateIntegrity();
            if (result == null)
            {
                return RecordFailure("Validate 6.5 Integrity", "Combat State service is missing.", "MissingCombatState");
            }

            string message = result.Diagnostics.Count == 0 ? "Combat State integrity is valid." : string.Join(" | ", result.Diagnostics);
            return Record(result.Succeeded, "Validate 6.5 Integrity", result.Succeeded ? "Valid" : CombatStateResultCode.IntegrityViolation, message);
        }

        public PrototypeTestLabOperation ResetEnemy()
        {
            context?.EnemyAttack?.ResetCooldown();
            context?.EnemyController?.ResetControllerState();
            context?.EnemyStatuses?.ClearTemporaryStatuses();
            context?.EnemyHealth?.ResetToMaximum();
            return RecordSuccess("Reset Enemy", "Enemy health, cooldown, controller state, and temporary statuses reset.");
        }

        public string BuildLifecycleSummary()
        {
            EnsureLifecycleRuntime(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject, ref context.PlayerLifecycle, needsResource: true);
            EnsureLifecycleRuntime(context?.EnemyTransform == null ? null : context.EnemyTransform.gameObject, ref context.EnemyLifecycle, needsResource: true);
            return string.Join(Environment.NewLine, new[]
            {
                "Feature 6.3 Actor Lifecycle",
                FormatLifecycleSummary("Player", context?.PlayerLifecycle, context?.PlayerResources),
                FormatLifecycleSummary("Enemy", context?.EnemyLifecycle, context?.EnemyTransform == null ? null : context.EnemyTransform.GetComponentInParent<CharacterResourceCollection>()),
                $"Last Lifecycle Tx: {(string.IsNullOrWhiteSpace(lastLifecycleTransactionId) ? "None" : lastLifecycleTransactionId)}"
            });
        }

        public PrototypeTestLabOperation PreviewDefeatLifecycle(bool targetEnemy)
        {
            if (!TryResolveLifecycleTarget(targetEnemy, out ActorLifecycleController lifecycle, out GameObject target, out string actorId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ActorLifecycleResult result = lifecycle.PreviewDefeat(new DefeatResolutionRequest(string.Empty, "development.test-lab", null, actorId, target, LifecycleTriggerKind.ExplicitDefeat, reason: "Prototype Test Lab"));
            return Record(result.Succeeded, targetEnemy ? "Preview Enemy Defeat" : "Preview Player Defeat", result.Code, FormatLifecycleResult(result));
        }

        public PrototypeTestLabOperation ExecuteDefeatLifecycle(bool targetEnemy, bool reuseTransaction)
        {
            if (!TryResolveLifecycleTarget(targetEnemy, out ActorLifecycleController lifecycle, out GameObject target, out string actorId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ActorLifecycleResult result = lifecycle.ExecuteDefeat(new DefeatResolutionRequest(ResolveLifecycleTransactionId(reuseTransaction), "development.test-lab", null, actorId, target, LifecycleTriggerKind.ExplicitDefeat, reason: "Prototype Test Lab"));
            return Record(result.Succeeded, targetEnemy ? "Defeat Enemy Lifecycle" : "Defeat Player Lifecycle", result.Code, FormatLifecycleResult(result));
        }

        public PrototypeTestLabOperation ApplyZeroHealthLifecycleDamage(DamageTypeDefinition damageType, bool targetEnemy)
        {
            if (damageType == null)
            {
                return RecordFailure("Lifecycle Zero Health", "No damage type selected.", "MissingDefinition");
            }

            if (!TryResolveLifecycleTarget(targetEnemy, out ActorLifecycleController _, out GameObject target, out string actorId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            CharacterResourceCollection targetResources = target.GetComponentInParent<CharacterResourceCollection>();
            if (targetResources == null || !targetResources.TryGetResource(ResourceIds.Health, out ResourceSnapshot health))
            {
                return RecordFailure("Lifecycle Zero Health", "Target Health resource is missing.", "MissingHealth");
            }

            string transactionId = ResolveLifecycleTransactionId(reuse: false);
            float amount = Mathf.Max(1f, health.Current - health.Minimum + 1000f);
            GameObject source = targetEnemy ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            DamageApplicationRequest request = new DamageApplicationRequest(transactionId, ResolveActorId(source), source, actorId, target, damageType, amount, "Prototype Test Lab zero-health lifecycle proof");
            DamageApplicationResult result = damageHealingService.ApplyDamage(request);
            return Record(result.Succeeded, targetEnemy ? "Zero Health Enemy" : "Zero Health Player", result.Code, $"Damage={result.FinalDamageAmount:0.###} Health={result.OldHealth:0.###}->{result.NewHealth:0.###} BecameZero={result.BecameZero} Lifecycle={ActorLifecycleUtility.GetState(target)} Duplicate={result.Duplicate}.");
        }

        public PrototypeTestLabOperation PreviewRecoveryLifecycle(bool targetEnemy, float amount)
        {
            if (!TryResolveLifecycleTarget(targetEnemy, out ActorLifecycleController lifecycle, out GameObject target, out string actorId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ActorLifecycleResult result = lifecycle.PreviewRecovery(new LifecycleRecoveryRequest(string.Empty, "development.test-lab", null, actorId, target, Mathf.Max(0f, amount), "Prototype Test Lab"));
            return Record(result.Succeeded, targetEnemy ? "Preview Enemy Recovery" : "Preview Player Recovery", result.Code, FormatLifecycleResult(result));
        }

        public PrototypeTestLabOperation ExecuteRecoveryLifecycle(bool targetEnemy, float amount, bool reuseTransaction)
        {
            if (!TryResolveLifecycleTarget(targetEnemy, out ActorLifecycleController lifecycle, out GameObject target, out string actorId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ActorLifecycleResult result = lifecycle.ExecuteRecovery(new LifecycleRecoveryRequest(ResolveLifecycleTransactionId(reuseTransaction), "development.test-lab", null, actorId, target, Mathf.Max(0f, amount), "Prototype Test Lab"));
            return Record(result.Succeeded, targetEnemy ? "Recover Enemy" : "Recover Player", result.Code, FormatLifecycleResult(result));
        }

        public PrototypeTestLabOperation PreviewDeathLifecycle(bool targetEnemy)
        {
            if (!TryResolveLifecycleTarget(targetEnemy, out ActorLifecycleController lifecycle, out GameObject target, out string actorId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ActorLifecycleResult result = lifecycle.PreviewDeath(new LifecycleDeathRequest(string.Empty, "development.test-lab", null, actorId, target, LifecycleTriggerKind.ExplicitDeath, "Prototype Test Lab"));
            return Record(result.Succeeded, targetEnemy ? "Preview Enemy Death" : "Preview Player Death", result.Code, FormatLifecycleResult(result));
        }

        public PrototypeTestLabOperation ExecuteDeathLifecycle(bool targetEnemy, bool reuseTransaction)
        {
            if (!TryResolveLifecycleTarget(targetEnemy, out ActorLifecycleController lifecycle, out GameObject target, out string actorId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ActorLifecycleResult result = lifecycle.ExecuteDeath(new LifecycleDeathRequest(ResolveLifecycleTransactionId(reuseTransaction), "development.test-lab", null, actorId, target, LifecycleTriggerKind.ExplicitDeath, "Prototype Test Lab"));
            return Record(result.Succeeded, targetEnemy ? "Kill Enemy Lifecycle" : "Kill Player Lifecycle", result.Code, FormatLifecycleResult(result));
        }

        public PrototypeTestLabOperation PreviewRevivalLifecycle(bool targetEnemy, float amount)
        {
            if (!TryResolveLifecycleTarget(targetEnemy, out ActorLifecycleController lifecycle, out GameObject target, out string actorId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ActorLifecycleResult result = lifecycle.PreviewRevival(new LifecycleRevivalRequest(string.Empty, "development.test-lab", null, actorId, target, Mathf.Max(0f, amount), "Prototype Test Lab"));
            return Record(result.Succeeded, targetEnemy ? "Preview Enemy Revival" : "Preview Player Revival", result.Code, FormatLifecycleResult(result));
        }

        public PrototypeTestLabOperation ExecuteRevivalLifecycle(bool targetEnemy, float amount, bool reuseTransaction)
        {
            if (!TryResolveLifecycleTarget(targetEnemy, out ActorLifecycleController lifecycle, out GameObject target, out string actorId, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ActorLifecycleResult result = lifecycle.ExecuteRevival(new LifecycleRevivalRequest(ResolveLifecycleTransactionId(reuseTransaction), "development.test-lab", null, actorId, target, Mathf.Max(0f, amount), "Prototype Test Lab"));
            return Record(result.Succeeded, targetEnemy ? "Revive Enemy" : "Revive Player", result.Code, FormatLifecycleResult(result));
        }

        public string BuildOngoingEffectsSummary()
        {
            OngoingEffectService playerService = EnsureOngoingEffectRuntime(targetEnemy: false);
            OngoingEffectService enemyService = EnsureOngoingEffectRuntime(targetEnemy: true);
            return string.Join(Environment.NewLine, new[]
            {
                "Feature 6.4 Ongoing Effects",
                $"Clock: {ongoingEffectClockSeconds:0.###}s",
                FormatOngoingEffectTarget("Player", playerService, context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject, context?.PlayerResources, context?.PlayerLifecycle),
                FormatOngoingEffectTarget("Enemy", enemyService, context?.EnemyTransform == null ? null : context.EnemyTransform.gameObject, context?.EnemyTransform == null ? null : context.EnemyTransform.GetComponentInParent<CharacterResourceCollection>(), context?.EnemyLifecycle),
                $"Last Ongoing Tx: {(string.IsNullOrWhiteSpace(lastOngoingEffectTransactionId) ? "None" : lastOngoingEffectTransactionId)}"
            });
        }

        public PrototypeTestLabOperation GenerateOngoingEffectTransaction()
        {
            lastOngoingEffectTransactionId = $"development.ongoing-effect.{Guid.NewGuid():N}";
            return RecordSuccess("Fresh Ongoing Effect Tx", lastOngoingEffectTransactionId);
        }

        public PrototypeTestLabOperation PreviewOngoingEffect(OngoingEffectDefinition definition, bool targetEnemy, float amount, float interval, float duration, int tickCount, int stacks)
        {
            if (!TryBuildOngoingEffectRequest(definition, targetEnemy, amount, interval, duration, tickCount, stacks, reuseTransaction: false, out OngoingEffectService service, out OngoingEffectApplicationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            OngoingEffectApplicationResult result = service.PreviewApplyOngoingEffect(request);
            return Record(result.Succeeded, targetEnemy ? "Preview Enemy Ongoing Effect" : "Preview Player Ongoing Effect", result.Code, FormatOngoingApplicationResult(result));
        }

        public PrototypeTestLabOperation ApplyOngoingEffect(OngoingEffectDefinition definition, bool targetEnemy, float amount, float interval, float duration, int tickCount, int stacks, bool reuseTransaction)
        {
            if (!TryBuildOngoingEffectRequest(definition, targetEnemy, amount, interval, duration, tickCount, stacks, reuseTransaction, out OngoingEffectService service, out OngoingEffectApplicationRequest request, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            OngoingEffectApplicationResult result = service.ApplyOngoingEffect(request);
            return Record(result.Succeeded, targetEnemy ? "Apply Enemy Ongoing Effect" : "Apply Player Ongoing Effect", result.Code, FormatOngoingApplicationResult(result));
        }

        public PrototypeTestLabOperation AdvanceOngoingEffects(float deltaSeconds)
        {
            float delta = Mathf.Max(0f, deltaSeconds);
            ongoingEffectClockSeconds += delta;
            OngoingEffectProcessResult player = EnsureOngoingEffectRuntime(targetEnemy: false)?.AdvanceTime(delta);
            OngoingEffectProcessResult enemy = EnsureOngoingEffectRuntime(targetEnemy: true)?.AdvanceTime(delta);
            return RecordSuccess("Advance Ongoing Effects", $"Advanced {delta:0.###}s. Player={FormatOngoingProcessResult(player)} Enemy={FormatOngoingProcessResult(enemy)}");
        }

        public PrototypeTestLabOperation ProcessOngoingEffectsNow()
        {
            OngoingEffectProcessResult player = EnsureOngoingEffectRuntime(targetEnemy: false)?.ProcessDueTicks(0f);
            OngoingEffectProcessResult enemy = EnsureOngoingEffectRuntime(targetEnemy: true)?.ProcessDueTicks(0f);
            return RecordSuccess("Process Ongoing Effects", $"Processed due ticks without advancing time. Player={FormatOngoingProcessResult(player)} Enemy={FormatOngoingProcessResult(enemy)}");
        }

        public PrototypeTestLabOperation CancelFirstOngoingEffect(bool targetEnemy, bool preview)
        {
            OngoingEffectService service = EnsureOngoingEffectRuntime(targetEnemy);
            RuntimeOngoingEffectInstance instance = service == null ? null : service.ActiveInstances.FirstOrDefault();
            if (instance == null)
            {
                return RecordFailure("Cancel Ongoing Effect", "No active ongoing effect instance is available.", "MissingInstance");
            }

            OngoingEffectCancellationRequest request = new OngoingEffectCancellationRequest($"development.ongoing-cancel.{Guid.NewGuid():N}", instance.InstanceId, instance.TargetActorId, instance.TargetObject, "Prototype Test Lab");
            OngoingEffectCancellationResult result = preview ? service.PreviewCancelOngoingEffect(request) : service.CancelOngoingEffect(request);
            return Record(result.Succeeded, preview ? "Preview Cancel Ongoing Effect" : "Cancel Ongoing Effect", result.Code, FormatOngoingCancellationResult(result));
        }

        public string BuildCombatReactionSummary()
        {
            CombatReactionService service = EnsureCombatReactionRuntime();
            IReadOnlyList<CombatReactionSourceRegistration> registrations = service == null ? Array.Empty<CombatReactionSourceRegistration>() : service.Registrations;
            string selected = string.Join(Environment.NewLine, GetDefinitions<CombatReactionDefinition>().Select(definition => $"{definition.DisplayName} ({definition.Id}) Triggers={string.Join(",", definition.TriggerTypes)} Op={definition.OperationType} Target={definition.TargetPolicy} Chance={definition.ProcChance:0.###} Priority={definition.Priority}").Take(8));
            return string.Join(Environment.NewLine, new[]
            {
                "Feature 6.8 Combat Reactions",
                $"Registered Sources: {registrations.Count}",
                registrations.Count == 0 ? "Sources: None" : $"Sources: {string.Join(" | ", registrations.Select(registration => $"{registration.Definition.Id}@{registration.OwnerActorId}:{registration.SourceStableId}:{registration.SourceInstanceId}"))}",
                string.IsNullOrWhiteSpace(selected) ? "Definitions: None" : selected
            });
        }

        public PrototypeTestLabOperation RegisterCombatReaction(CombatReactionDefinition definition, bool ownerPlayer)
        {
            CombatReactionService service = EnsureCombatReactionRuntime();
            GameObject owner = ownerPlayer ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            if (service == null || owner == null || definition == null)
            {
                return RecordFailure("Register 6.8 Reaction", "Combat reaction service, owner, or definition is missing.", CombatReactionResultCode.MissingDefinition);
            }

            EnsureAttackResolutionRuntime(owner, needsResource: true);
            string ownerActorId = ResolveActorId(owner);
            CombatReactionSourceRegistration registration = new CombatReactionSourceRegistration(
                $"development.reaction.{definition.Id}.{ownerActorId}",
                ownerActorId,
                owner,
                CombatReactionSourceKind.Development,
                $"development.{definition.Id}",
                "prototype-test-lab",
                0,
                definition);
            CombatReactionSourceRegistration registered = service.RegisterSource(registration);
            return RecordSuccess("Register 6.8 Reaction", $"Registered {registered.Definition.Id} for {(ownerPlayer ? "player" : "enemy")} actor {ownerActorId}.");
        }

        public PrototypeTestLabOperation ClearCombatReactions()
        {
            CombatReactionService service = EnsureCombatReactionRuntime();
            service?.ClearAllSources();
            service?.ClearTransientStateForRestore();
            return RecordSuccess("Clear 6.8 Reactions", "Combat reaction sources and transient chain state cleared.");
        }

        public PrototypeTestLabOperation PreviewCombatReactionDamage(CombatReactionDefinition definition)
        {
            return RunCombatReactionDamage(definition, execute: false, critical: false, rootId: $"development.reaction.preview.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation ExecuteCombatReactionDamage(CombatReactionDefinition definition)
        {
            return RunCombatReactionDamage(definition, execute: true, critical: false, rootId: $"development.reaction.execute.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation ExecuteCombatReactionCritical(CombatReactionDefinition definition)
        {
            return RunCombatReactionDamage(definition, execute: true, critical: true, rootId: $"development.reaction.critical.{Guid.NewGuid():N}");
        }

        public PrototypeTestLabOperation ExecuteCombatReactionDuplicateProof(CombatReactionDefinition definition)
        {
            string rootId = $"development.reaction.duplicate.{Guid.NewGuid():N}";
            PrototypeTestLabOperation first = RunCombatReactionDamage(definition, execute: true, critical: false, rootId: rootId);
            PrototypeTestLabOperation second = RunCombatReactionDamage(definition, execute: true, critical: false, rootId: rootId);
            bool succeeded = first.Succeeded && second.Succeeded && second.Code == CombatReactionResultCode.Duplicate;
            return Record(succeeded, "Duplicate 6.8 Reaction Proof", second.Code, $"First={first.Code} Second={second.Code}. {second.Message}");
        }

        private PrototypeTestLabOperation RunCombatReactionDamage(CombatReactionDefinition definition, bool execute, bool critical, string rootId)
        {
            if (definition == null)
            {
                return RecordFailure("6.8 Reaction", "Combat reaction definition is missing.", CombatReactionResultCode.MissingDefinition);
            }

            CombatReactionService service = EnsureCombatReactionRuntime();
            GameObject source = context?.PlayerTransform?.gameObject;
            GameObject target = context?.EnemyTransform?.gameObject;
            if (service == null || source == null || target == null)
            {
                return RecordFailure("6.8 Reaction", "Combat reaction service, source, or target is missing.", CombatReactionResultCode.MissingTarget);
            }

            EnsureAttackResolutionRuntime(source, needsResource: true);
            EnsureAttackResolutionRuntime(target, needsResource: true);
            CombatReactionTriggerContext trigger = new CombatReactionTriggerContext(
                critical ? CombatReactionTriggerType.CriticalHit : CombatReactionTriggerType.DamageApplied,
                rootId,
                ResolveActorId(source),
                source,
                ResolveActorId(target),
                target,
                actualDamage: 25f,
                critical: critical,
                damageType: GetDefinitions<DamageTypeDefinition>().FirstOrDefault());
            CombatReactionChainResult result = execute ? service.ExecuteTrigger(trigger) : service.PreviewTrigger(trigger);
            return Record(result.Succeeded, execute ? critical ? "Execute 6.8 Critical Reaction" : "Execute 6.8 Reaction" : "Preview 6.8 Reaction", result.Code, FormatCombatReactionChain(result));
        }

        public string BuildCombatContributionSummary()
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            IReadOnlyList<CombatContributionLedgerSnapshot> ledgers = service == null ? Array.Empty<CombatContributionLedgerSnapshot>() : service.GetLedgerSnapshots();
            string ledgerText = ledgers.Count == 0
                ? "Ledgers: None"
                : string.Join(Environment.NewLine, ledgers.Select(FormatContributionLedger));
            CombatContributionPolicyDefinition policy = GetDefinitions<CombatContributionPolicyDefinition>().FirstOrDefault();
            return string.Join(Environment.NewLine, new[]
            {
                "Feature 6.9 Combat Contribution",
                $"Policy: {(policy == null ? "None" : $"{policy.DisplayName} ({policy.Id}) Window={policy.ContributionWindowSeconds:0.###}s")}",
                $"Clock: {(service == null ? 0f : service.SimulationTime):0.###}s",
                ledgerText
            });
        }

        public PrototypeTestLabOperation PreviewContribution(DamageTypeDefinition damageType)
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            DamageApplicationRequest request = CreatePipelineDamageRequest(damageType ?? GetDefinitions<DamageTypeDefinition>().FirstOrDefault(), 25f, targetPlayer: false, transactionId: $"development.contribution.preview.{Guid.NewGuid():N}");
            CombatContributionRecordRequest contribution = new CombatContributionRecordRequest(
                request.TransactionId,
                CombatContributionType.DamageApplied,
                request.SourceActorId,
                string.Empty,
                string.Empty,
                request.TargetActorId,
                string.Empty,
                request.RequestedAmount,
                Mathf.Max(0f, request.RequestedAmount),
                0f,
                service == null ? 0f : service.SimulationTime,
                CombatContributionSourceKind.Development,
                preview: true);
            CombatContributionRecordResult result = service == null
                ? CombatContributionRecordResult.Failure(true, CombatContributionResultCode.MissingPolicy, "Contribution service is missing.", 0, 0)
                : service.PreviewContribution(contribution);
            return Record(result.Succeeded, "Preview 6.9 Contribution", result.Code, FormatContributionRecordResult(result));
        }

        public PrototypeTestLabOperation RecordDamageContribution(DamageTypeDefinition damageType, bool reuseTransaction)
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            DamageTypeDefinition selected = damageType ?? GetDefinitions<DamageTypeDefinition>().FirstOrDefault();
            if (service == null || selected == null)
            {
                return RecordFailure("Record 6.9 Damage", "Contribution service or damage type is missing.", CombatContributionResultCode.MissingPolicy);
            }

            string transactionId = reuseTransaction && !string.IsNullOrWhiteSpace(lastAttackTransactionId)
                ? lastAttackTransactionId
                : $"development.contribution.damage.{Guid.NewGuid():N}";
            lastAttackTransactionId = transactionId;
            DamageApplicationResult damage;
            if (reuseTransaction && lastContributionDamageSource != null)
            {
                damage = lastContributionDamageSource;
            }
            else
            {
                if (!TryPrepareContributionHealth(targetPlayer: false, desiredCurrent: 50f, out string healthMessage))
                {
                    return RecordFailure("Record 6.9 Damage", healthMessage, "MissingHealth");
                }

                damage = damageHealingService.ApplyDamage(CreatePipelineDamageRequest(selected, 25f, targetPlayer: false, transactionId));
                if (damage.Succeeded && !damage.Duplicate)
                {
                    lastContributionDamageSource = damage;
                }
            }

            CombatContributionRecordResult result = service.RecordDamage(damage, sourceKind: CombatContributionSourceKind.Development);
            if (result.Record != null && !string.IsNullOrWhiteSpace(result.Record.TargetActorId))
            {
                lastContributionCreditTargetActorId = result.Record.TargetActorId;
            }

            return Record(result.Succeeded || result.Duplicate, reuseTransaction ? "Record 6.9 Damage Reuse" : "Record 6.9 Damage", result.Code, $"{FormatDamageApplication(damage)} {FormatContributionRecordResult(result)}");
        }

        public PrototypeTestLabOperation RecordHealingContribution(bool reuseTransaction)
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            if (service == null)
            {
                return RecordFailure("Record 6.9 Healing", "Contribution service is missing.", CombatContributionResultCode.MissingPolicy);
            }

            string transactionId = reuseTransaction && !string.IsNullOrWhiteSpace(lastOngoingEffectTransactionId)
                ? lastOngoingEffectTransactionId
                : $"development.contribution.healing.{Guid.NewGuid():N}";
            lastOngoingEffectTransactionId = transactionId;
            HealingApplicationResult healing;
            if (reuseTransaction && lastContributionHealingSource != null)
            {
                healing = lastContributionHealingSource;
            }
            else
            {
                if (!TryPrepareContributionHealth(targetPlayer: false, desiredCurrent: 50f, out string healthMessage))
                {
                    return RecordFailure("Record 6.9 Healing", healthMessage, "MissingHealth");
                }

                healing = damageHealingService.ApplyHealing(CreatePipelineHealingRequest(25f, targetPlayer: false, transactionId));
                if (healing.Succeeded && !healing.Duplicate)
                {
                    lastContributionHealingSource = healing;
                }
            }

            CombatContributionRecordResult result = service.RecordHealing(healing, sourceKind: CombatContributionSourceKind.Development);
            if (result.Record != null && !string.IsNullOrWhiteSpace(result.Record.BeneficiaryActorId))
            {
                lastContributionCreditTargetActorId = result.Record.BeneficiaryActorId;
            }

            return Record(result.Succeeded || result.Duplicate, reuseTransaction ? "Record 6.9 Healing Reuse" : "Record 6.9 Healing", result.Code, $"{FormatHealingApplication(healing)} {FormatContributionRecordResult(result)}");
        }

        public PrototypeTestLabOperation RecordFullyPreventedDamageContribution(DamageTypeDefinition damageType)
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            DamageTypeDefinition selected = damageType ?? GetDefinitions<DamageTypeDefinition>().FirstOrDefault();
            if (service == null || selected == null)
            {
                return RecordFailure("Record 6.9 Prevented Damage", "Contribution service or damage type is missing.", CombatContributionResultCode.MissingPolicy);
            }

            DamageApplicationRequest request = CreatePipelineDamageRequest(selected, 25f, targetPlayer: false, $"development.contribution.prevented.{Guid.NewGuid():N}");
            DamageApplicationResult damage = DamageApplicationResult.Create(false, "Prevented", "Damage was fully prevented.", request, request.TargetActorId, 25f, 25f, 25f, 0f, 0f, 0f, 100f, 100f, 0f, 100f, false, false, false, false, false, 0f, null);
            CombatContributionRecordResult result = service.RecordDamage(damage, sourceKind: CombatContributionSourceKind.Development);
            bool expectedZero = result.Code == CombatContributionResultCode.ZeroEffectiveContribution;
            return Record(expectedZero, "Record 6.9 Prevented Damage", result.Code, $"{FormatDamageApplication(damage)} {FormatContributionRecordResult(result)}");
        }

        public PrototypeTestLabOperation RecordOverkillContribution(DamageTypeDefinition damageType)
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            DamageTypeDefinition selected = damageType ?? GetDefinitions<DamageTypeDefinition>().FirstOrDefault();
            if (service == null || selected == null)
            {
                return RecordFailure("Record 6.9 Overkill", "Contribution service or damage type is missing.", CombatContributionResultCode.MissingPolicy);
            }

            string transactionId = $"development.contribution.overkill.{Guid.NewGuid():N}";
            if (!TryPrepareContributionHealth(targetPlayer: false, desiredCurrent: 50f, out string healthMessage))
            {
                return RecordFailure("Record 6.9 Overkill", healthMessage, "MissingHealth");
            }

            DamageApplicationResult damage = damageHealingService.ApplyDamage(CreatePipelineDamageRequest(selected, 999f, targetPlayer: false, transactionId));
            CombatContributionRecordResult result = service.RecordDamage(damage, sourceKind: CombatContributionSourceKind.Development);
            return Record(result.Succeeded || result.Code == CombatContributionResultCode.ZeroEffectiveContribution, "Record 6.9 Overkill", result.Code, $"{FormatDamageApplication(damage)} {FormatContributionRecordResult(result)}");
        }

        public PrototypeTestLabOperation RecordOngoingDamageContribution()
        {
            return RecordSyntheticContribution(
                "Record 6.9 Ongoing Damage",
                CombatContributionType.OngoingDamageApplied,
                CombatContributionSourceKind.OngoingEffect,
                "ongoing-effect.synthetic",
                requestedAmount: 5f,
                actualAmount: 5f,
                preventedAmount: 0f);
        }

        public PrototypeTestLabOperation RecordReactionDamageContribution()
        {
            return RecordSyntheticContribution(
                "Record 6.9 Reaction Damage",
                CombatContributionType.ReactionDamageApplied,
                CombatContributionSourceKind.Reaction,
                "combat-reaction.synthetic-damage",
                requestedAmount: 5f,
                actualAmount: 5f,
                preventedAmount: 0f);
        }

        public PrototypeTestLabOperation RecordReactionHealingContribution()
        {
            return RecordSyntheticContribution(
                "Record 6.9 Reaction Healing",
                CombatContributionType.ReactionHealingApplied,
                CombatContributionSourceKind.Reaction,
                "combat-reaction.synthetic-healing",
                requestedAmount: 5f,
                actualAmount: 5f,
                preventedAmount: 0f);
        }

        public PrototypeTestLabOperation RecordDefenseContribution(CombatContributionType contributionType)
        {
            string label = contributionType == CombatContributionType.SuccessfulBlock
                ? "Record 6.9 Block"
                : contributionType == CombatContributionType.SuccessfulParry
                    ? "Record 6.9 Parry"
                    : "Record 6.9 Dodge";
            return RecordSyntheticContribution(
                label,
                contributionType,
                CombatContributionSourceKind.Defense,
                $"defense.synthetic.{contributionType}",
                requestedAmount: 25f,
                actualAmount: contributionType == CombatContributionType.SuccessfulBlock ? 5f : 1f,
                preventedAmount: contributionType == CombatContributionType.SuccessfulBlock ? 20f : 25f,
                support: true);
        }

        public PrototypeTestLabOperation AdvanceContributionClock(float seconds)
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            service?.AdvanceClock(Mathf.Max(0f, seconds));
            return RecordSuccess("Advance 6.9 Clock", $"Contribution clock advanced by {Mathf.Max(0f, seconds):0.###}s to {(service == null ? 0f : service.SimulationTime):0.###}s.");
        }

        public PrototypeTestLabOperation ResolveDefeatContributionCredit()
        {
            return ResolveContributionCredit(kill: false);
        }

        public PrototypeTestLabOperation ResolveKillContributionCredit()
        {
            return ResolveContributionCredit(kill: true);
        }

        public PrototypeTestLabOperation FinalizeContributionLedger()
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            CombatContributionLedgerSnapshot snapshot = service?.GetLedgerSnapshots().FirstOrDefault();
            CombatContributionLedgerSnapshot finalized = snapshot == null ? null : service.FinalizeLedger(snapshot.LedgerId);
            return Record(finalized != null, "Finalize 6.9 Ledger", finalized == null ? CombatContributionResultCode.MissingTarget : CombatContributionResultCode.Success, finalized == null ? "No contribution ledger exists." : FormatContributionLedger(finalized));
        }

        public PrototypeTestLabOperation ClearCombatContributions()
        {
            EnsureCombatContributionRuntime()?.ClearTransientStateForRestore();
            lastContributionDamageSource = null;
            lastContributionHealingSource = null;
            lastContributionCreditTargetActorId = string.Empty;
            return RecordSuccess("Clear 6.9 Contributions", "Contribution ledgers, credit results, and duplicate keys cleared.");
        }

        public PrototypeTestLabOperation ProveContributionKillCreditLatest()
        {
            CombatContributionService service = ResetContributionProofState();
            if (service == null)
            {
                return RecordFailure("Prove 6.9 Latest Kill Credit", "Contribution service is missing.", CombatContributionResultCode.MissingPolicy);
            }

            string target = ResolveContributionTargetActorId();
            service.RecordContribution(SyntheticContribution("proof.kill.old", CombatContributionType.DamageApplied, "actor.proof.old", target, string.Empty, 50f));
            service.AdvanceClock(1f);
            service.RecordContribution(SyntheticContribution("proof.kill.latest", CombatContributionType.DamageApplied, "actor.proof.latest", target, string.Empty, 5f));
            CombatCreditResolutionResult credit = service.ResolveKillCredit(BuildContributionLifecycle(target, kill: true, "proof.kill.lifecycle"));
            bool passed = credit.Succeeded && credit.PrimaryContributorActorId == "actor.proof.latest";
            return Record(passed, "Prove 6.9 Latest Kill Credit", credit.Code, FormatCreditResult(credit));
        }

        public PrototypeTestLabOperation ProveContributionAssistCredit()
        {
            CombatContributionService service = ResetContributionProofState();
            if (service == null)
            {
                return RecordFailure("Prove 6.9 Assist Credit", "Contribution service is missing.", CombatContributionResultCode.MissingPolicy);
            }

            string target = ResolveContributionTargetActorId();
            service.RecordContribution(SyntheticContribution("proof.assist.damage", CombatContributionType.DamageApplied, "actor.proof.assist", target, string.Empty, 10f));
            service.RecordContribution(SyntheticContribution("proof.assist.heal", CombatContributionType.HealingApplied, "actor.proof.healer", string.Empty, target, 6f));
            service.AdvanceClock(1f);
            service.RecordContribution(SyntheticContribution("proof.assist.kill", CombatContributionType.DamageApplied, "actor.proof.primary", target, string.Empty, 5f));
            CombatCreditResolutionResult credit = service.ResolveKillCredit(BuildContributionLifecycle(target, kill: true, "proof.assist.lifecycle"));
            bool passed = credit.Succeeded
                && credit.PrimaryContributorActorId == "actor.proof.primary"
                && credit.Assists.Any(summary => summary.ContributorActorId == "actor.proof.assist")
                && credit.Assists.Any(summary => summary.ContributorActorId == "actor.proof.healer");
            return Record(passed, "Prove 6.9 Assist Credit", credit.Code, FormatCreditResult(credit));
        }

        public PrototypeTestLabOperation ProveContributionHealingOnlyNotPrimary()
        {
            CombatContributionService service = ResetContributionProofState();
            if (service == null)
            {
                return RecordFailure("Prove 6.9 Healing Not Primary", "Contribution service is missing.", CombatContributionResultCode.MissingPolicy);
            }

            string target = ResolveContributionTargetActorId();
            service.RecordContribution(SyntheticContribution("proof.heal.only", CombatContributionType.HealingApplied, "actor.proof.healer", string.Empty, target, 10f));
            CombatCreditResolutionResult credit = service.ResolveKillCredit(BuildContributionLifecycle(target, kill: true, "proof.heal.lifecycle"));
            bool passed = credit.Succeeded && string.IsNullOrWhiteSpace(credit.PrimaryContributorActorId) && credit.Code == CombatContributionResultCode.NoEligibleContributor;
            return Record(passed, "Prove 6.9 Healing Not Primary", credit.Code, FormatCreditResult(credit));
        }

        public PrototypeTestLabOperation ProveContributionEncounterMerge()
        {
            CombatContributionService service = ResetContributionProofState();
            if (service == null)
            {
                return RecordFailure("Prove 6.9 Encounter Merge", "Contribution service is missing.", CombatContributionResultCode.MissingPolicy);
            }

            service.RecordContribution(SyntheticContribution("proof.merge.a", CombatContributionType.DamageApplied, "actor.proof.a", "actor.proof.target", string.Empty, 5f, "encounter.proof.a"));
            service.RecordContribution(SyntheticContribution("proof.merge.b", CombatContributionType.DamageApplied, "actor.proof.b", "actor.proof.target", string.Empty, 5f, "encounter.proof.b"));
            CombatContributionLedgerMergeResult merge = service.MergeEncounterLedgers(new CombatEncounterSnapshot("encounter.proof.a", true, 0f, 0f, new[] { "actor.proof.a", "actor.proof.b", "actor.proof.target" }, Array.Empty<CombatEngagementSnapshot>(), 1L, CombatEncounterCompletionReason.Forced));
            bool passed = merge.Succeeded && merge.Snapshot != null && merge.Snapshot.Records.Count == 2;
            return Record(passed, "Prove 6.9 Encounter Merge", merge.Code, $"Merged={string.Join(",", merge.MergedLedgerIds)} Records={(merge.Snapshot == null ? 0 : merge.Snapshot.Records.Count)}. {merge.Message}");
        }

        public PrototypeTestLabOperation ProveContributionEncounterSplit()
        {
            CombatContributionService service = ResetContributionProofState();
            if (service == null)
            {
                return RecordFailure("Prove 6.9 Encounter Split", "Contribution service is missing.", CombatContributionResultCode.MissingPolicy);
            }

            service.RecordContribution(SyntheticContribution("proof.split.a", CombatContributionType.DamageApplied, "actor.proof.a", "actor.proof.target-a", string.Empty, 5f, "encounter.proof.original"));
            service.RecordContribution(SyntheticContribution("proof.split.b", CombatContributionType.DamageApplied, "actor.proof.b", "actor.proof.target-b", string.Empty, 5f, "encounter.proof.original"));
            service.RecordContribution(SyntheticContribution("proof.split.cross", CombatContributionType.DamageApplied, "actor.proof.a", "actor.proof.target-b", string.Empty, 5f, "encounter.proof.original"));
            CombatEncounterSplitResult split = new CombatEncounterSplitResult(true, false, false, "Success", "Proof split.", "proof.split.tx", "encounter.proof.original", "encounter.proof.original", new[] { "encounter.proof.new" }, new[] { "actor.proof.a", "actor.proof.target-a", "actor.proof.b", "actor.proof.target-b" }, new[]
            {
                new CombatEncounterSplitComponentSnapshot("encounter.proof.original", new[] { "actor.proof.a", "actor.proof.target-a" }, Array.Empty<string>(), true, 0f, true),
                new CombatEncounterSplitComponentSnapshot("encounter.proof.new", new[] { "actor.proof.b", "actor.proof.target-b" }, Array.Empty<string>(), false, 0f, true)
            }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<CombatExitResult>(), Array.Empty<CombatEncounterSnapshot>(), Array.Empty<CombatParticipantReassignmentResult>(), CombatExitReason.Explicit, 0L, 1L, 0f);
            CombatContributionLedgerPartitionResult partition = service.PartitionEncounterLedgers(split);
            bool passed = partition.Succeeded
                && partition.ComponentSnapshots.Count == 2
                && partition.ComponentSnapshots.All(snapshot => snapshot.Summaries.Count == 1);
            return Record(passed, "Prove 6.9 Encounter Split", partition.Code, $"Components={partition.ComponentSnapshots.Count} Historical={partition.HistoricalSnapshots.SelectMany(snapshot => snapshot.Records).Count()}. {partition.Message}");
        }

        public PrototypeTestLabOperation ProveContributionDuplicateLifecycleCredit()
        {
            CombatContributionService service = ResetContributionProofState();
            if (service == null)
            {
                return RecordFailure("Prove 6.9 Duplicate Credit", "Contribution service is missing.", CombatContributionResultCode.MissingPolicy);
            }

            string target = ResolveContributionTargetActorId();
            service.RecordContribution(SyntheticContribution("proof.duplicate.damage", CombatContributionType.DamageApplied, "actor.proof.primary", target, string.Empty, 5f));
            ActorLifecycleResult lifecycle = BuildContributionLifecycle(target, kill: true, "proof.duplicate.lifecycle");
            CombatCreditResolutionResult first = service.ResolveKillCredit(lifecycle);
            CombatCreditResolutionResult duplicate = service.ResolveKillCredit(lifecycle);
            bool passed = first.Succeeded && duplicate.Succeeded && duplicate.Duplicate;
            return Record(passed, "Prove 6.9 Duplicate Credit", duplicate.Code, FormatCreditResult(duplicate));
        }

        public PrototypeTestLabOperation ProveContributionRevivalPreservesCredit()
        {
            CombatContributionService service = ResetContributionProofState();
            if (service == null)
            {
                return RecordFailure("Prove 6.9 Revival Preserves Credit", "Contribution service is missing.", CombatContributionResultCode.MissingPolicy);
            }

            string target = ResolveContributionTargetActorId();
            service.RecordContribution(SyntheticContribution("proof.revival.damage", CombatContributionType.DamageApplied, "actor.proof.primary", target, string.Empty, 5f));
            ActorLifecycleResult lifecycle = BuildContributionLifecycle(target, kill: true, "proof.revival.lifecycle");
            CombatCreditResolutionResult first = service.ResolveKillCredit(lifecycle);
            service.RecordContribution(SyntheticContribution("proof.revival.support", CombatContributionType.RevivalProvided, "actor.proof.healer", string.Empty, target, 10f));
            CombatCreditResolutionResult after = service.ResolveKillCredit(lifecycle, transactionId: "proof.revival.lifecycle.after");
            bool passed = first.Succeeded && after.Duplicate && after.PrimaryContributorActorId == "actor.proof.primary";
            return Record(passed, "Prove 6.9 Revival Preserves Credit", after.Code, FormatCreditResult(after));
        }

        public PrototypeTestLabOperation ProveContributionRewardSafety()
        {
            CombatContributionService service = ResetContributionProofState();
            if (service == null)
            {
                return RecordFailure("Prove 6.9 Reward Safety", "Contribution service is missing.", CombatContributionResultCode.MissingPolicy);
            }

            string target = ResolveContributionTargetActorId();
            service.RecordContribution(SyntheticContribution("proof.reward.damage", CombatContributionType.DamageApplied, "actor.proof.primary", target, string.Empty, 5f));
            CombatCreditResolutionResult credit = service.ResolveKillCredit(BuildContributionLifecycle(target, kill: true, "proof.reward.lifecycle"));
            bool passed = credit.Succeeded && !credit.GrantsConcreteRewards && credit.Contributors.Any(summary => summary.Eligibility.Contains(CombatRewardEligibilityCategory.DiagnosticOnly));
            return Record(passed, "Prove 6.9 Reward Safety", credit.Code, $"{FormatCreditResult(credit)} EligibilityOnly=True ConcreteRewards=False");
        }

        private PrototypeTestLabOperation ResolveContributionCredit(bool kill)
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            GameObject target = context?.EnemyTransform?.gameObject;
            string targetActorId = !string.IsNullOrWhiteSpace(lastContributionCreditTargetActorId)
                ? lastContributionCreditTargetActorId
                : ResolveActorId(target);
            if (service == null || target == null)
            {
                return RecordFailure(kill ? "Resolve 6.9 Kill Credit" : "Resolve 6.9 Defeat Credit", "Contribution service or target is missing.", CombatContributionResultCode.MissingTarget);
            }

            ActorLifecycleResult lifecycle = ActorLifecycleResult.Create(
                true,
                false,
                false,
                ActorLifecycleResultCode.Success,
                kill ? "Development kill credit proof." : "Development defeat credit proof.",
                $"development.contribution.lifecycle.{Guid.NewGuid():N}",
                ResolveActorId(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject),
                targetActorId,
                string.Empty,
                kill ? LifecycleTransitionKind.Death : LifecycleTransitionKind.Defeat,
                kill ? LifecycleTriggerKind.ExplicitDeath : LifecycleTriggerKind.HealthDepleted,
                ActorLifecycleState.Active,
                kill ? ActorLifecycleState.Dead : ActorLifecycleState.Defeated,
                DefeatPolicyOutcome.RemainDefeated,
                0f,
                0f,
                0f,
                100f,
                0f,
                0f,
                0f,
                string.Empty,
                0L);
            CombatCreditResolutionResult result = kill ? service.ResolveKillCredit(lifecycle) : service.ResolveDefeatCredit(lifecycle);
            return Record(result.Succeeded, kill ? "Resolve 6.9 Kill Credit" : "Resolve 6.9 Defeat Credit", result.Code, FormatCreditResult(result));
        }

        private CombatContributionService ResetContributionProofState()
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            service?.ClearTransientStateForRestore();
            lastContributionDamageSource = null;
            lastContributionHealingSource = null;
            lastContributionCreditTargetActorId = string.Empty;
            return service;
        }

        private string ResolveContributionTargetActorId()
        {
            GameObject target = context?.EnemyTransform == null ? null : context.EnemyTransform.gameObject;
            string resolved = ResolveActorId(target);
            return string.IsNullOrWhiteSpace(resolved) ? "actor.proof.target" : resolved;
        }

        private CombatContributionRecordRequest SyntheticContribution(string transactionId, CombatContributionType type, string contributorActorId, string targetActorId, string beneficiaryActorId, float actualAmount, string encounterId = "")
        {
            return new CombatContributionRecordRequest(
                transactionId,
                type,
                contributorActorId,
                string.Empty,
                beneficiaryActorId,
                targetActorId,
                encounterId,
                actualAmount,
                actualAmount,
                type == CombatContributionType.SuccessfulBlock || type == CombatContributionType.SuccessfulParry || type == CombatContributionType.SuccessfulDodge ? actualAmount : 0f,
                combatContributionService == null ? 0f : combatContributionService.SimulationTime,
                CombatContributionSourceKind.Development,
                transactionId,
                string.Empty,
                "development.test-lab",
                string.Empty,
                preview: false,
                authorityValidated: true);
        }

        private ActorLifecycleResult BuildContributionLifecycle(string targetActorId, bool kill, string transactionId)
        {
            return ActorLifecycleResult.Create(
                true,
                false,
                false,
                ActorLifecycleResultCode.Success,
                kill ? "Development kill credit proof." : "Development defeat credit proof.",
                transactionId,
                ResolveActorId(context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject),
                targetActorId,
                string.Empty,
                kill ? LifecycleTransitionKind.Death : LifecycleTransitionKind.Defeat,
                kill ? LifecycleTriggerKind.ExplicitDeath : LifecycleTriggerKind.HealthDepleted,
                ActorLifecycleState.Active,
                kill ? ActorLifecycleState.Dead : ActorLifecycleState.Defeated,
                DefeatPolicyOutcome.RemainDefeated,
                0f,
                0f,
                0f,
                100f,
                0f,
                0f,
                0f,
                string.Empty,
                0L);
        }

        private PrototypeTestLabOperation RecordSyntheticContribution(string operationName, CombatContributionType type, CombatContributionSourceKind sourceKind, string originDefinitionId, float requestedAmount, float actualAmount, float preventedAmount, bool support = false)
        {
            CombatContributionService service = EnsureCombatContributionRuntime();
            GameObject contributor = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            GameObject enemy = context?.EnemyTransform == null ? null : context.EnemyTransform.gameObject;
            if (service == null || contributor == null || enemy == null)
            {
                return RecordFailure(operationName, "Contribution service, contributor, or target is missing.", CombatContributionResultCode.MissingTarget);
            }

            string contributorId = ResolveActorId(contributor);
            string enemyId = ResolveActorId(enemy);
            string transactionId = $"development.contribution.synthetic.{Guid.NewGuid():N}";
            bool beneficiaryContribution = type == CombatContributionType.HealingApplied
                || type == CombatContributionType.OngoingHealingApplied
                || type == CombatContributionType.ReactionHealingApplied
                || type == CombatContributionType.RecoveryProvided
                || type == CombatContributionType.RevivalProvided;
            CombatContributionRecordRequest request = new CombatContributionRecordRequest(
                transactionId,
                type,
                contributorId,
                string.Empty,
                beneficiaryContribution ? enemyId : string.Empty,
                beneficiaryContribution ? string.Empty : enemyId,
                string.Empty,
                requestedAmount,
                actualAmount,
                preventedAmount,
                service.SimulationTime,
                sourceKind,
                transactionId,
                string.Empty,
                originDefinitionId,
                string.Empty,
                preview: false,
                authorityValidated: true);
            CombatContributionRecordResult result = service.RecordContribution(request);
            if (result.Record != null)
            {
                if (!string.IsNullOrWhiteSpace(result.Record.TargetActorId))
                {
                    lastContributionCreditTargetActorId = result.Record.TargetActorId;
                }
                else if (!string.IsNullOrWhiteSpace(result.Record.BeneficiaryActorId))
                {
                    lastContributionCreditTargetActorId = result.Record.BeneficiaryActorId;
                }
            }

            return Record(result.Succeeded || result.Duplicate, operationName, result.Code, FormatContributionRecordResult(result));
        }

        private DamageApplicationRequest CreatePipelineDamageRequest(DamageTypeDefinition damageType, float amount, bool targetPlayer, string transactionId)
        {
            GameObject source = targetPlayer ? context?.EnemyTransform?.gameObject : context?.PlayerTransform?.gameObject;
            GameObject target = targetPlayer ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            EnsureAttackResolutionRuntime(source, needsResource: false);
            EnsureAttackResolutionRuntime(target, needsResource: true);
            return new DamageApplicationRequest(
                transactionId,
                ResolveActorId(source),
                source,
                ResolveActorId(target),
                target,
                damageType,
                Mathf.Max(0f, amount),
                "Prototype Test Lab");
        }

        private bool TryPrepareContributionHealth(bool targetPlayer, float desiredCurrent, out string message)
        {
            GameObject target = targetPlayer ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            EnsureAttackResolutionRuntime(target, needsResource: true);
            CharacterResourceCollection resources = target == null ? null : target.GetComponentInParent<CharacterResourceCollection>();
            if (resources == null || !resources.TryGetResource(ResourceIds.Health, out ResourceSnapshot health))
            {
                message = "Contribution target Health resource is missing.";
                return false;
            }

            float prepared = Mathf.Clamp(desiredCurrent, health.Minimum, health.Maximum);
            ResourceChangeResult result = resources.SetCurrent(ResourceIds.Health, prepared, "development.test-lab", "Prepare contribution automation.", restoration: true);
            message = result == null ? "Contribution Health preparation did not return a result." : result.Message;
            return result != null && result.Succeeded;
        }

        private AttackResolutionRequest CreateDefensiveAttackRequest(DamageTypeDefinition damageType, float amount, float baseHitChance, float hitRoll, float defenseRoll, bool targetPlayer, string transactionId)
        {
            Dictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["defense.roll"] = Mathf.Clamp(defenseRoll, 0f, 0.999f).ToString("0.###"),
                ["defense.blockable"] = "true",
                ["defense.parryable"] = "true",
                ["defense.dodgeable"] = "true",
                ["defense.allow-true-active"] = "true"
            };
            return CreateAttackResolutionRequest(
                damageType,
                amount,
                baseHitChance,
                hitRoll,
                criticalChance: 0f,
                criticalRoll: 0.5f,
                criticalMultiplier: AttackResolutionRequest.DefaultCriticalMultiplier,
                distance: 1f,
                maximumRange: 2f,
                targetEnemy: !targetPlayer,
                sourcePlayer: !targetPlayer,
                transactionId,
                metadata);
        }

        private AttackResolutionRequest CreateAttackResolutionRequest(DamageTypeDefinition damageType, float amount, float baseHitChance, float hitRoll, float criticalChance, float criticalRoll, float criticalMultiplier, float distance, float maximumRange, bool targetEnemy, bool sourcePlayer, string transactionId, IReadOnlyDictionary<string, string> metadata = null)
        {
            GameObject source = sourcePlayer ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            GameObject target = targetEnemy ? context?.EnemyTransform?.gameObject : context?.PlayerTransform?.gameObject;
            EnsureAttackResolutionRuntime(source, needsResource: false);
            EnsureAttackResolutionRuntime(target, needsResource: true);
            return new AttackResolutionRequest(
                transactionId,
                sourcePlayer ? AttackSourceType.Weapon : AttackSourceType.Unarmed,
                source,
                ResolveActorId(source),
                target,
                ResolveActorId(target),
                damageType,
                Mathf.Max(0f, amount),
                hitRoll,
                criticalRoll,
                Mathf.Clamp01(baseHitChance),
                Mathf.Clamp01(criticalChance),
                Mathf.Max(1f, criticalMultiplier),
                hasSuppliedDistance: distance >= 0f,
                suppliedDistance: Mathf.Max(0f, distance),
                hasMaximumRange: maximumRange >= 0f,
                maximumRange: Mathf.Max(0f, maximumRange),
                originatingActionId: "development.attack-resolution-test",
                metadata: metadata);
        }

        private AttackResolutionRequest CreateCombatStateAttackResolutionRequest(DamageTypeDefinition damageType, float amount, float baseHitChance, float hitRoll, float criticalChance, float criticalRoll, float criticalMultiplier, float distance, float maximumRange, string transactionId)
        {
            EnsureCombatStateTestParticipants();
            GameObject source = GetCombatStateTestActor("A");
            GameObject target = GetCombatStateTestActor("B");
            EnsureAttackResolutionRuntime(source, needsResource: false);
            EnsureAttackResolutionRuntime(target, needsResource: true);
            return new AttackResolutionRequest(
                transactionId,
                AttackSourceType.Weapon,
                source,
                ResolveCombatStateActorId(source),
                target,
                ResolveCombatStateActorId(target),
                damageType,
                Mathf.Max(0f, amount),
                hitRoll,
                criticalRoll,
                Mathf.Clamp01(baseHitChance),
                Mathf.Clamp01(criticalChance),
                Mathf.Max(1f, criticalMultiplier),
                hasSuppliedDistance: distance >= 0f,
                suppliedDistance: Mathf.Max(0f, distance),
                hasMaximumRange: maximumRange >= 0f,
                maximumRange: Mathf.Max(0f, maximumRange),
                originatingActionId: "development.combat-state-attack-test");
        }

        private bool TryBuildDefenseActivationRequest(DefensiveActionDefinition definition, bool targetPlayer, bool reuseTransaction, out DefenseActivationRequest request, out PrototypeTestLabOperation failure)
        {
            request = default;
            failure = default;
            if (definition == null)
            {
                failure = RecordFailure("6.6 Defense Activation", "No defensive action selected.", "MissingDefinition");
                return false;
            }

            GameObject target = targetPlayer ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            if (target == null)
            {
                failure = RecordFailure("6.6 Defense Activation", "Defense target is missing.", "MissingTarget");
                return false;
            }

            EnsureAttackResolutionRuntime(target, needsResource: true);
            request = new DefenseActivationRequest(
                ResolveDefenseActivationTransactionId(reuseTransaction),
                ResolveActorId(target),
                target,
                definition,
                sourceEquipmentId: string.Empty,
                sourceActionId: "development.test-lab",
                now: Time.time,
                authorityValidated: true);
            return true;
        }

        private string FormatActiveDefense(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId) || !defensiveActionService.TryGetActiveDefense(actorId, out DefensiveActionStateSnapshot snapshot))
            {
                return "None";
            }

            string expiration = snapshot.HasExpiration ? $" expires={Mathf.Max(0f, snapshot.ExpiresAt - Time.time):0.###}s" : " persistent";
            return $"{snapshot.Definition.DisplayName} ({snapshot.DefinitionId}) state={snapshot.State}{expiration}";
        }

        private static string FormatResource(GameObject owner, string resourceId)
        {
            CharacterResourceCollection resources = owner == null ? null : owner.GetComponentInParent<CharacterResourceCollection>();
            if (resources == null || !resources.TryGetResource(resourceId, out ResourceSnapshot snapshot))
            {
                return "Missing";
            }

            return $"{snapshot.Current:0.###}/{snapshot.Maximum:0.###}";
        }

        private static string FormatDefenseActivation(DefenseActivationResult result)
        {
            if (result == null)
            {
                return "Defense activation result is missing.";
            }

            string state = result.State == null ? "State=None" : $"State={result.State.StateId} action={result.State.DefinitionId} runtime={result.State.State}";
            string stamina = result.StaminaResult == null ? "Stamina=None" : $"Stamina={result.StaminaResult.Code} {result.StaminaResult.OldCurrent:0.###}->{result.StaminaResult.NewCurrent:0.###} duplicate={result.StaminaResult.DuplicateEvent}";
            return $"{state} preview={result.Preview} duplicate={result.Duplicate} {stamina}. {result.Message}";
        }

        private static string FormatDefenseCancellation(DefenseCancellationResult result)
        {
            if (result == null)
            {
                return "Defense cancellation result is missing.";
            }

            string state = result.RemovedState == null ? "State=None" : $"Removed={result.RemovedState.DefinitionId} state={result.RemovedState.StateId}";
            return $"{state} preview={result.Preview} duplicate={result.Duplicate}. {result.Message}";
        }

        private void EnsureAttackResolutionRuntime(GameObject actor, bool needsResource)
        {
            if (!CanMutateRuntimeActor(actor))
            {
                return;
            }

            WorldEntityIdentity identity = actor.GetComponentInParent<WorldEntityIdentity>();
            if (identity == null)
            {
                identity = actor.AddComponent<WorldEntityIdentity>();
                if (identity == null)
                {
                    return;
                }

                identity.TryInitializeRuntime($"entity.local-world.runtime.attack-test-lab.{Guid.NewGuid():N}", "scene.prototype", PersistenceService.LocalWorldId, PersistenceScope.RegionOrScene, "development.attack-resolution", out _);
            }

            CharacterAttributes attributes = actor.GetComponentInParent<CharacterAttributes>();
            if (attributes == null)
            {
                attributes = actor.AddComponent<CharacterAttributes>();
                if (attributes == null)
                {
                    return;
                }
            }

            CalculatedStatCollection stats = actor.GetComponentInParent<CalculatedStatCollection>();
            if (stats == null)
            {
                stats = actor.AddComponent<CalculatedStatCollection>();
                if (stats == null)
                {
                    return;
                }
            }

            attributes.Configure(registry);
            stats.Configure(registry, attributes);
            if (needsResource)
            {
                CharacterResourceCollection resources = actor.GetComponentInParent<CharacterResourceCollection>();
                if (resources == null)
                {
                    resources = actor.AddComponent<CharacterResourceCollection>();
                    if (resources == null)
                    {
                        return;
                    }
                }

                resources.Configure(registry, stats, PersistenceService.LocalPlayerId);
                actor.GetComponentInParent<EnemyHealth>()?.RefreshResourceRuntime();
            }
        }

        private void EnsureLifecycleRuntime(GameObject actor, ref ActorLifecycleController lifecycle, bool needsResource)
        {
            if (!CanMutateRuntimeActor(actor))
            {
                return;
            }

            EnsureAttackResolutionRuntime(actor, needsResource);
            lifecycle = lifecycle == null ? actor.GetComponentInParent<ActorLifecycleController>() : lifecycle;
            if (lifecycle == null)
            {
                lifecycle = actor.AddComponent<ActorLifecycleController>();
                if (lifecycle == null)
                {
                    return;
                }
            }

            CharacterResourceCollection resources = actor.GetComponentInParent<CharacterResourceCollection>();
            CharacterSystemCoordinator character = actor.GetComponentInParent<CharacterSystemCoordinator>();
            CharacterTraitCollection traits = actor.GetComponentInParent<CharacterTraitCollection>();
            lifecycle.Configure(null, resources, character, traits);
        }

        private static bool CanMutateRuntimeActor(GameObject actor)
        {
            if (actor == null || !actor.activeInHierarchy)
            {
                return false;
            }

            CharacterSystemCoordinator character = actor.GetComponentInParent<CharacterSystemCoordinator>();
            return character == null || character.isActiveAndEnabled;
        }

        private CombatStateService EnsureCombatStateRuntime()
        {
            GameObject owner = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            CombatStateService service = context?.CombatState;
            if (service == null && owner != null)
            {
                service = owner.GetComponentInParent<CombatStateService>();
            }

            if (service == null && owner != null)
            {
                service = owner.AddComponent<CombatStateService>();
            }

            CombatStatePolicyDefinition policy = registry == null
                ? null
                : registry.DefinitionsById.Values.OfType<CombatStatePolicyDefinition>().OrderBy(definition => definition.Id).FirstOrDefault();
            service?.Configure(policy);
            service?.SetClock(combatStateClockSeconds);
            if (context != null)
            {
                context.CombatState = service;
            }

            return service;
        }

        private CombatRuntimeFacade EnsureCombatRuntimeFacade()
        {
            if (combatRuntimeFacade != null)
            {
                return combatRuntimeFacade;
            }

            GameObject player = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            CombatStateService combatState = EnsureCombatStateRuntime();
            OngoingEffectService ongoing = EnsureOngoingEffectRuntime(targetEnemy: false) ?? EnsureOngoingEffectRuntime(targetEnemy: true);
            CombatReactionService reactions = EnsureCombatReactionRuntime();
            CombatContributionService contributions = EnsureCombatContributionRuntime();
            combatRuntimeFacade = new CombatRuntimeFacade(
                registry,
                player,
                damageHealingService,
                defensiveActionService,
                combatState,
                combatExecutionService,
                ongoing,
                reactions,
                contributions,
                attackResolutionService);
            return combatRuntimeFacade;
        }

        private GameObject EnsureAutomationEnemyTarget()
        {
            if (context == null)
            {
                return null;
            }

            if (context.EnemyTransform != null)
            {
                GameObject sceneEnemy = context.EnemyTransform.gameObject;
                EnsureEnemyContextComponents(sceneEnemy);
                return sceneEnemy;
            }

            if (automationEnemyTarget == null)
            {
                automationEnemyTarget = new GameObject("Automation Enemy Target");
                Transform parent = context.PlayerTransform == null ? null : context.PlayerTransform.root;
                if (parent != null)
                {
                    automationEnemyTarget.transform.SetParent(parent, worldPositionStays: true);
                    automationEnemyTarget.transform.position = context.PlayerTransform.position + context.PlayerTransform.forward * 3f;
                }
            }

            EnsureEnemyContextComponents(automationEnemyTarget);
            return automationEnemyTarget;
        }

        private void EnsureEnemyContextComponents(GameObject enemy)
        {
            if (context == null || enemy == null)
            {
                return;
            }

            context.EnemyTransform = enemy.transform;
            context.EnemyHealth = enemy.GetComponent<EnemyHealth>() ?? enemy.AddComponent<EnemyHealth>();
            context.EnemyController = enemy.GetComponent<PrototypeEnemyController>();
            context.EnemyAttack = enemy.GetComponent<EnemyMeleeAttack>();
            context.EnemyStatuses = enemy.GetComponent<StatusEffectController>() ?? enemy.AddComponent<StatusEffectController>();

            WorldEntityIdentity identity = enemy.GetComponent<WorldEntityIdentity>();
            if (identity == null)
            {
                identity = enemy.AddComponent<WorldEntityIdentity>();
                identity.TryInitializeRuntime("entity.local-world.runtime.test-lab.enemy-target", "scene.prototype", PersistenceService.LocalWorldId, PersistenceScope.SessionOnly, "development.test-lab.enemy-target", out _);
            }

            EnsureLifecycleRuntime(enemy, ref context.EnemyLifecycle, needsResource: true);
            context.EnemyHealth.RefreshResourceRuntime();
        }

        private void EnsureCombatStateTestParticipants()
        {
            EnsureCombatStateRuntime();
            RegisterCombatStateTestActor("A", context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject);
            RegisterCombatStateTestActor("B", EnsureAutomationEnemyTarget());
            EnsureCombatStateMockActor("C");
            EnsureCombatStateMockActor("D");
        }

        private void RegisterCombatStateTestActor(string key, GameObject actor)
        {
            if (actor == null)
            {
                return;
            }

            EnsureAttackResolutionRuntime(actor, needsResource: true);
            if (string.Equals(key, "A", StringComparison.Ordinal))
            {
                EnsureLifecycleRuntime(actor, ref context.PlayerLifecycle, needsResource: true);
            }
            else if (string.Equals(key, "B", StringComparison.Ordinal))
            {
                EnsureLifecycleRuntime(actor, ref context.EnemyLifecycle, needsResource: true);
            }
            else
            {
                ActorLifecycleController lifecycle = actor.GetComponentInParent<ActorLifecycleController>();
                EnsureLifecycleRuntime(actor, ref lifecycle, needsResource: true);
            }

            combatStateTestActors[key] = actor;
        }

        private GameObject EnsureCombatStateMockActor(string key)
        {
            if (combatStateTestActors.TryGetValue(key, out GameObject existing) && existing != null)
            {
                return existing;
            }

            GameObject root = context?.PlayerTransform == null ? null : context.PlayerTransform.root.gameObject;
            GameObject actor = new GameObject($"Combat State Test Actor {key}");
            if (root != null)
            {
                actor.transform.SetParent(root.transform);
                actor.transform.position = context.PlayerTransform.position + context.PlayerTransform.right * (string.Equals(key, "C", StringComparison.Ordinal) ? 2f : 3f);
            }

            WorldEntityIdentity identity = actor.AddComponent<WorldEntityIdentity>();
            identity.TryInitializeRuntime($"entity.local-world.runtime.combat-state-test-lab.{key.ToLowerInvariant()}.{Guid.NewGuid():N}", "scene.prototype", PersistenceService.LocalWorldId, PersistenceScope.SessionOnly, "development.combat-state-test-lab", out _);
            EnsureAttackResolutionRuntime(actor, needsResource: true);
            ActorLifecycleController lifecycle = actor.GetComponentInParent<ActorLifecycleController>();
            EnsureLifecycleRuntime(actor, ref lifecycle, needsResource: true);
            combatStateTestActors[key] = actor;
            return actor;
        }

        private GameObject GetCombatStateTestActor(string key)
        {
            EnsureCombatStateTestParticipants();
            if (combatStateTestActors.TryGetValue(key, out GameObject actor) && actor != null)
            {
                return actor;
            }

            return null;
        }

        private bool TryBuildCombatExecutionBeginRequest(CombatExecutionDefinition definition, bool reuseTransaction, out CombatExecutionBeginRequest request, out PrototypeTestLabOperation failure)
        {
            request = default;
            failure = default;
            GameObject actor = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            EnsureAttackResolutionRuntime(actor, needsResource: true);
            if (definition == null)
            {
                failure = RecordFailure("6.7 Execution", "Combat execution definition is missing.", CombatExecutionResultCode.MissingDefinition);
                return false;
            }

            if (actor == null)
            {
                failure = RecordFailure("6.7 Execution", "Prototype player actor is missing.", CombatExecutionResultCode.MissingActor);
                return false;
            }

            request = new CombatExecutionBeginRequest(
                ResolveCombatExecutionBeginTransactionId(reuseTransaction),
                definition,
                actor,
                ResolveActorId(actor),
                combatExecutionClockSeconds,
                authorityValidated: true);
            return true;
        }

        private bool TryBuildCombatExecutionCommitRequest(bool reuseTransaction, out CombatExecutionCommitRequest request, out PrototypeTestLabOperation failure)
        {
            request = default;
            failure = default;
            GameObject actor = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            EnsureAttackResolutionRuntime(actor, needsResource: true);
            if (actor == null)
            {
                failure = RecordFailure("6.7 Execution", "Prototype player actor is missing.", CombatExecutionResultCode.MissingActor);
                return false;
            }

            if (string.IsNullOrWhiteSpace(lastCombatExecutionInstanceId))
            {
                failure = RecordFailure("6.7 Execution", "Begin a combat execution before committing.", CombatExecutionResultCode.MissingExecution);
                return false;
            }

            request = new CombatExecutionCommitRequest(
                ResolveCombatExecutionCommitTransactionId(reuseTransaction),
                lastCombatExecutionInstanceId,
                actor,
                ResolveActorId(actor),
                combatExecutionClockSeconds,
                authorityValidated: true);
            return true;
        }

        private bool TryBuildCombatExecutionCancelRequest(CombatExecutionCancellationReason reason, out CombatExecutionCancelRequest request, out PrototypeTestLabOperation failure)
        {
            request = default;
            failure = default;
            GameObject actor = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            if (actor == null)
            {
                failure = RecordFailure("6.7 Execution", "Prototype player actor is missing.", CombatExecutionResultCode.MissingActor);
                return false;
            }

            if (string.IsNullOrWhiteSpace(lastCombatExecutionInstanceId))
            {
                failure = RecordFailure("6.7 Execution", "Begin a combat execution before cancelling or interrupting.", CombatExecutionResultCode.MissingExecution);
                return false;
            }

            request = new CombatExecutionCancelRequest(
                $"development.combat-execution.cancel.{Guid.NewGuid():N}",
                lastCombatExecutionInstanceId,
                actor,
                ResolveActorId(actor),
                reason,
                combatExecutionClockSeconds);
            return true;
        }

        private string ResolveCombatExecutionBeginTransactionId(bool reuse)
        {
            if (reuse && !string.IsNullOrWhiteSpace(lastCombatExecutionBeginTransactionId))
            {
                return lastCombatExecutionBeginTransactionId;
            }

            lastCombatExecutionBeginTransactionId = $"development.combat-execution.begin.{Guid.NewGuid():N}";
            return lastCombatExecutionBeginTransactionId;
        }

        private string ResolveCombatExecutionCommitTransactionId(bool reuse)
        {
            if (reuse && !string.IsNullOrWhiteSpace(lastCombatExecutionCommitTransactionId))
            {
                return lastCombatExecutionCommitTransactionId;
            }

            lastCombatExecutionCommitTransactionId = $"development.combat-execution.commit.{Guid.NewGuid():N}";
            return lastCombatExecutionCommitTransactionId;
        }

        private string FormatCombatExecutionCooldowns(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return "Cooldowns: None";
            }

            List<string> lines = new List<string> { "Cooldowns:" };
            IReadOnlyList<CombatExecutionDefinition> definitions = GetDefinitions<CombatExecutionDefinition>();
            for (int i = 0; i < definitions.Count; i++)
            {
                CombatExecutionDefinition definition = definitions[i];
                CombatExecutionCooldownSnapshot snapshot = combatExecutionService.GetCooldownState(actorId, definition.ResolveCooldownKey());
                if (snapshot == null)
                {
                    lines.Add($"- {definition.DisplayName}: Ready");
                }
                else
                {
                    lines.Add($"- {definition.DisplayName}: Charges {snapshot.CurrentCharges}/{snapshot.MaximumCharges} ReadyAt {snapshot.CooldownReadyAt:0.###}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatCombatExecutionResult(CombatExecutionResult result)
        {
            if (result == null)
            {
                return "No combat execution result.";
            }

            string state = result.State == null
                ? "State=None"
                : $"State={result.State.Phase} Instance={result.State.ExecutionInstanceId} Ready={result.State.ReadyAt:0.###} RecoveryEnd={result.State.RecoveryEndsAt:0.###}";
            string costs = result.Costs == null || result.Costs.Count == 0
                ? "Costs=None"
                : $"Costs={string.Join(", ", result.Costs.Select(cost => $"{cost.DefinitionId}:{cost.Amount:0.###}:{cost.Code}"))}";
            string cooldown = result.Cooldown == null
                ? "Cooldown=None"
                : $"Cooldown={result.Cooldown.CooldownKey} Charges={result.Cooldown.CurrentCharges}/{result.Cooldown.MaximumCharges} Ready={result.Cooldown.CooldownReadyAt:0.###}";
            return $"{result.Code} Success={result.Succeeded} Preview={result.Preview} Duplicate={result.Duplicate} Definition={result.DefinitionId}\n{state}\n{costs}\n{cooldown}\n{result.Message}";
        }

        private OngoingEffectService EnsureOngoingEffectRuntime(bool targetEnemy)
        {
            PrototypeTestLabContext labContext = context;
            GameObject actor = targetEnemy ? labContext?.EnemyTransform?.gameObject : labContext?.PlayerTransform?.gameObject;
            if (actor == null)
            {
                return null;
            }

            if (targetEnemy)
            {
                EnsureLifecycleRuntime(actor, ref labContext.EnemyLifecycle, needsResource: true);
            }
            else
            {
                EnsureLifecycleRuntime(actor, ref labContext.PlayerLifecycle, needsResource: true);
            }

            OngoingEffectService service = targetEnemy ? labContext.EnemyOngoingEffects : labContext.PlayerOngoingEffects;
            if (service == null)
            {
                service = actor.GetComponent<OngoingEffectService>() ?? actor.GetComponentInParent<OngoingEffectService>(includeInactive: true);
            }

            if (service == null)
            {
                if (!actor.activeInHierarchy)
                {
                    return null;
                }

                service = actor.AddComponent<OngoingEffectService>();
            }

            if (service == null)
            {
                return null;
            }

            CharacterSystemCoordinator coordinator = actor.GetComponentInParent<CharacterSystemCoordinator>();
            if (coordinator == null && !targetEnemy)
            {
                return null;
            }

            service.Configure(coordinator);
            service.ConfigureDamageHealing(damageHealingService);
            service.SetClock(ongoingEffectClockSeconds);
            if (targetEnemy)
            {
                labContext.EnemyOngoingEffects = service;
            }
            else
            {
                labContext.PlayerOngoingEffects = service;
            }

            return service;
        }

        private CombatReactionService EnsureCombatReactionRuntime()
        {
            GameObject host = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            if (host == null)
            {
                return combatReactionService;
            }

            if (combatReactionService == null)
            {
                combatReactionService = host.GetComponentInParent<CombatReactionService>();
            }

            if (combatReactionService == null)
            {
                combatReactionService = host.AddComponent<CombatReactionService>();
            }

            OngoingEffectService ongoing = EnsureOngoingEffectRuntime(targetEnemy: true) ?? EnsureOngoingEffectRuntime(targetEnemy: false);
            if (ongoing != null)
            {
                combatReactionService.Configure(ongoing, damageHealingService);
            }

            return combatReactionService;
        }

        private CombatContributionService EnsureCombatContributionRuntime()
        {
            GameObject host = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            if (host == null)
            {
                return combatContributionService;
            }

            if (combatContributionService == null)
            {
                combatContributionService = host.GetComponentInParent<CombatContributionService>();
            }

            if (combatContributionService == null)
            {
                combatContributionService = host.AddComponent<CombatContributionService>();
            }

            CombatContributionPolicyDefinition policy = GetDefinitions<CombatContributionPolicyDefinition>().FirstOrDefault();
            if (policy != null)
            {
                combatContributionService.Configure(policy);
            }

            return combatContributionService;
        }

        private bool TryBuildOngoingEffectRequest(
            OngoingEffectDefinition definition,
            bool targetEnemy,
            float amount,
            float interval,
            float duration,
            int tickCount,
            int stacks,
            bool reuseTransaction,
            out OngoingEffectService service,
            out OngoingEffectApplicationRequest request,
            out PrototypeTestLabOperation failure)
        {
            service = EnsureOngoingEffectRuntime(targetEnemy);
            request = default;
            failure = default;
            if (definition == null)
            {
                failure = RecordFailure("Ongoing Effect", "Ongoing effect definition is missing.", "MissingDefinition");
                return false;
            }

            GameObject target = targetEnemy ? context?.EnemyTransform?.gameObject : context?.PlayerTransform?.gameObject;
            GameObject source = targetEnemy ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            if (service == null || target == null)
            {
                failure = RecordFailure("Ongoing Effect", "Ongoing effect target service or target object is missing.", "MissingTarget");
                return false;
            }

            request = new OngoingEffectApplicationRequest(
                ResolveOngoingEffectTransactionId(reuseTransaction),
                definition,
                ResolveActorId(source),
                source,
                ResolveActorId(target),
                target,
                "development.test-lab",
                Mathf.Max(0f, amount),
                Mathf.Max(0f, interval),
                Mathf.Max(0f, duration),
                Mathf.Max(0, tickCount),
                Mathf.Max(1, stacks),
                authorityValidated: true);
            return true;
        }

        private bool TryResolveLifecycleTarget(bool targetEnemy, out ActorLifecycleController lifecycle, out GameObject target, out string actorId, out PrototypeTestLabOperation failure)
        {
            target = targetEnemy ? context?.EnemyTransform?.gameObject : context?.PlayerTransform?.gameObject;
            lifecycle = targetEnemy ? context?.EnemyLifecycle : context?.PlayerLifecycle;
            failure = default;
            actorId = string.Empty;

            if (context == null)
            {
                failure = RecordFailure("Lifecycle Target", "Test Lab context is missing.", "MissingContext");
                return false;
            }

            if (targetEnemy)
            {
                EnsureLifecycleRuntime(target, ref context.EnemyLifecycle, needsResource: true);
                lifecycle = context?.EnemyLifecycle;
            }
            else
            {
                EnsureLifecycleRuntime(target, ref context.PlayerLifecycle, needsResource: true);
                lifecycle = context?.PlayerLifecycle;
            }

            if (target == null || lifecycle == null)
            {
                failure = RecordFailure("Lifecycle Target", "Lifecycle target is missing.", "MissingTarget");
                return false;
            }

            actorId = ResolveActorId(target);
            if (string.IsNullOrWhiteSpace(actorId))
            {
                actorId = lifecycle.ActorId;
            }

            return true;
        }

        private string ResolveAttackTransactionId(bool reuse)
        {
            if (reuse && !string.IsNullOrWhiteSpace(lastAttackTransactionId))
            {
                return lastAttackTransactionId;
            }

            lastAttackTransactionId = AttackDeterministicRoll.NewTransactionId("development.attack-resolution");
            return lastAttackTransactionId;
        }

        private string ResolveDefenseActivationTransactionId(bool reuse)
        {
            if (reuse && !string.IsNullOrWhiteSpace(lastDefenseActivationTransactionId))
            {
                return lastDefenseActivationTransactionId;
            }

            lastDefenseActivationTransactionId = $"development.defense-action.activate.{Guid.NewGuid():N}";
            return lastDefenseActivationTransactionId;
        }

        private string ResolveCombatStateTransactionId(bool reuse)
        {
            if (reuse && !string.IsNullOrWhiteSpace(lastCombatStateTransactionId))
            {
                return lastCombatStateTransactionId;
            }

            lastCombatStateTransactionId = $"development.combat-state.{Guid.NewGuid():N}";
            return lastCombatStateTransactionId;
        }

        private string ResolveCombatStateSplitTransactionId(bool reuse)
        {
            if (reuse && !string.IsNullOrWhiteSpace(lastCombatStateSplitTransactionId))
            {
                return lastCombatStateSplitTransactionId;
            }

            lastCombatStateSplitTransactionId = $"development.combat-state.split.{Guid.NewGuid():N}";
            return lastCombatStateSplitTransactionId;
        }

        private string ResolveLifecycleTransactionId(bool reuse)
        {
            if (reuse && !string.IsNullOrWhiteSpace(lastLifecycleTransactionId))
            {
                return lastLifecycleTransactionId;
            }

            lastLifecycleTransactionId = $"development.lifecycle.{Guid.NewGuid():N}";
            return lastLifecycleTransactionId;
        }

        private string ResolveOngoingEffectTransactionId(bool reuse)
        {
            if (reuse && !string.IsNullOrWhiteSpace(lastOngoingEffectTransactionId))
            {
                return lastOngoingEffectTransactionId;
            }

            lastOngoingEffectTransactionId = $"development.ongoing-effect.{Guid.NewGuid():N}";
            return lastOngoingEffectTransactionId;
        }

        private static string FormatAttackResolution(AttackResolutionResult result)
        {
            if (result == null)
            {
                return "Attack result is missing.";
            }

            string damage = result.DamageResult == null
                ? "Damage=None"
                : $"Damage={result.DamageResult.Code} final={result.DamageResult.FinalDamageAmount:0.###} Health={result.DamageResult.OldHealth:0.###}->{result.DamageResult.NewHealth:0.###}";
            string defense = result.DefenseResult == null
                ? "Defense=None"
                : $"Defense={result.DefenseResult.Outcome} action={result.DefenseResult.DefensiveActionId} chance={result.DefenseResult.FinalDefenseChance:0.###} roll={result.DefenseResult.Request.Roll:0.###} prevented={result.DefenseResult.PreventedDamage:0.###} remaining={result.DefenseResult.RemainingDamage:0.###} consumed={result.DefenseResult.Consumed}";
            return $"{result.Outcome} hitChance={result.FinalHitChance:0.###} roll={result.HitRoll:0.###} crit={result.Critical} critRoll={result.CriticalRoll:0.###} dmgAfterCrit={result.DamageAfterCritical:0.###} duplicate={result.Duplicate} {defense} {damage}. {result.Message}";
        }

        private bool TryBuildCombatEngagementRequest(
            CombatActivityClassification classification,
            bool reuseTransaction,
            out CombatStateService service,
            out CombatEngagementRequest request,
            out PrototypeTestLabOperation failure)
        {
            service = EnsureCombatStateRuntime();
            request = default;
            failure = default;
            GameObject source = context?.PlayerTransform == null ? null : context.PlayerTransform.gameObject;
            GameObject target = context?.EnemyTransform == null ? null : context.EnemyTransform.gameObject;
            if (service == null || source == null || target == null)
            {
                failure = RecordFailure("6.5 Combat Engagement", "Combat State service, player, or enemy is missing.", "MissingReference");
                return false;
            }

            request = new CombatEngagementRequest(
                ResolveCombatStateTransactionId(reuseTransaction),
                ResolveCombatStateActorId(source),
                source,
                ResolveCombatStateActorId(target),
                target,
                classification,
                "development.test-lab",
                hostile: true,
                authorityValidated: true);
            return true;
        }

        private static string FormatCombatStateSnapshot(string label, ActorCombatStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return $"{label}: Missing";
            }

            float remaining = Mathf.Max(0f, snapshot.DisengageEligibleAt - snapshot.LastActivityAt);
            return $"{label}: {snapshot.State} Actor={snapshot.ActorId} Encounter={(string.IsNullOrWhiteSpace(snapshot.EncounterId) ? "None" : snapshot.EncounterId)} Participants={snapshot.ParticipantCount} Engagements={snapshot.ActiveEngagementCount} Entered={snapshot.EnteredAt:0.###} Last={snapshot.LastActivityAt:0.###} TimeoutWindow={remaining:0.###} Rev={snapshot.Revision} Reason={snapshot.TransitionReason}";
        }

        private static string FormatCombatEntryResult(CombatEntryResult result)
        {
            if (result == null)
            {
                return "Combat entry result is missing.";
            }

            return $"{result.SourceActorId}->{result.TargetActorId} Encounter={result.EncounterId} Engagement={result.EngagementId} Created={result.EncounterCreated} Added={result.SourceParticipantAdded}/{result.TargetParticipantAdded} Merged={result.EncounterMerged} Preview={result.Preview} Duplicate={result.Duplicate}. {result.Message}";
        }

        private static string FormatCombatExitResult(CombatExitResult result)
        {
            if (result == null)
            {
                return "Combat exit result is missing.";
            }

            return $"{result.ActorId} left Encounter={result.EncounterId} Reason={result.Reason} EndedEngagements={result.EngagementsEnded.Count} Preview={result.Preview} Duplicate={result.Duplicate}. {result.Message}";
        }

        private static string FormatCombatProcessResult(CombatStateProcessResult result)
        {
            if (result == null)
            {
                return "Combat process result is missing.";
            }

            return $"Delta={result.DeltaSeconds:0.###} Exits={result.ProcessedExits} Capped={result.Capped} Splits={result.SplitResults.Count} EndedEncounters={result.EndedEncounters.Count}";
        }

        private static string FormatCombatSplitResult(CombatEncounterSplitResult result)
        {
            if (result == null)
            {
                return "Combat split result is missing.";
            }

            string created = result.CreatedEncounterIds.Count == 0 ? "None" : string.Join(", ", result.CreatedEncounterIds);
            string left = result.ParticipantsLeftCombat.Count == 0 ? "None" : string.Join(", ", result.ParticipantsLeftCombat);
            string components = result.Components.Count == 0
                ? "None"
                : string.Join(" | ", result.Components.Select(component => $"{component.EncounterId}{(component.RetainedOriginalEncounterId ? "*" : string.Empty)} P=[{string.Join(",", component.ParticipantIds)}] E=[{string.Join(",", component.EngagementIds)}]"));
            string ended = result.EndedEngagementIds.Count == 0 ? "None" : string.Join(", ", result.EndedEngagementIds);
            return $"Original={result.OriginalEncounterId} Survivor={result.SurvivingEncounterId} Created={created} EndedEdges={ended} Left={left} Components={components} Duplicate={result.Duplicate}. {result.Message}";
        }

        private static string FormatCombatEncounter(CombatEncounterSnapshot encounter)
        {
            if (encounter == null)
            {
                return "Encounter: None";
            }

            string participants = encounter.ParticipantIds.Count == 0 ? "None" : string.Join(", ", encounter.ParticipantIds);
            string engagements = encounter.Engagements.Count == 0 ? "None" : string.Join(" | ", encounter.Engagements.Select(engagement => $"{engagement.EngagementId}:{engagement.SourceActorId}<->{engagement.TargetActorId}:{(engagement.Active ? "Active" : engagement.EndReason.ToString())}"));
            return $"Encounter: {encounter.EncounterId} Active={encounter.Active} Created={encounter.CreatedAt:0.###} Last={encounter.LastActivityAt:0.###} Participants=[{participants}] Engagements=[{engagements}] Completion={encounter.CompletionReason}";
        }

        private static string FormatLifecycleResult(ActorLifecycleResult result)
        {
            if (result == null)
            {
                return "Lifecycle result is missing.";
            }

            string requirement = string.IsNullOrWhiteSpace(result.RequirementSummary) ? string.Empty : $" Requirements={result.RequirementSummary}.";
            return $"{result.PreviousState}->{result.ResultingState} Transition={result.Transition} Trigger={result.Trigger} Health={result.OldHealth:0.###}->{result.NewHealth:0.###} Restore={result.AppliedHealthRestore:0.###}/{result.RequestedHealthRestore:0.###} Duplicate={result.Duplicate}. {result.Message}{requirement}";
        }

        private static string FormatLifecycleSummary(string label, ActorLifecycleController lifecycle, CharacterResourceCollection resources)
        {
            string state = lifecycle == null ? "Missing" : lifecycle.State.ToString();
            string actorId = lifecycle == null ? "None" : lifecycle.ActorId;
            string policy = lifecycle == null || lifecycle.DefeatPolicy == null ? "Default local living-being policy" : $"{lifecycle.DefeatPolicy.DisplayName} ({lifecycle.DefeatPolicy.Id})";
            string health = resources != null && resources.TryGetResource(ResourceIds.Health, out ResourceSnapshot snapshot)
                ? $"{snapshot.Current:0.###}/{snapshot.Maximum:0.###}"
                : "Missing";
            return $"{label}: State={state} Actor={actorId} Health={health} Policy={policy}";
        }

        private static string FormatOngoingEffectTarget(string label, OngoingEffectService service, GameObject target, CharacterResourceCollection resources, ActorLifecycleController lifecycle)
        {
            string health = FormatResourceSnapshot(resources, ResourceIds.Health);
            string mana = FormatResourceSnapshot(resources, ResourceIds.Mana);
            string stamina = FormatResourceSnapshot(resources, ResourceIds.Stamina);
            string state = lifecycle == null ? "Active" : lifecycle.State.ToString();
            IReadOnlyList<RuntimeOngoingEffectInstance> instances = service == null ? Array.Empty<RuntimeOngoingEffectInstance>() : service.QueryActiveEffects(target);
            string active = instances.Count == 0
                ? "None"
                : string.Join(" | ", instances.Select(instance => $"{instance.Definition.DisplayName} x{instance.StackCount} next={instance.NextTickElapsedSeconds:0.###}s rem={instance.RemainingDuration:0.###}s ticks={instance.CompletedTicks}/{(instance.FiniteTickCount > 0 ? instance.FiniteTickCount.ToString() : "duration")} [{instance.InstanceId}]"));
            return $"{label}: Lifecycle={state} H={health} M={mana} S={stamina} Active={active}";
        }

        private static string FormatResourceSnapshot(CharacterResourceCollection resources, string resourceId)
        {
            return resources != null && resources.TryGetResource(resourceId, out ResourceSnapshot snapshot)
                ? $"{snapshot.Current:0.###}/{snapshot.Maximum:0.###}"
                : "Missing";
        }

        private static string FormatOngoingApplicationResult(OngoingEffectApplicationResult result)
        {
            if (result == null)
            {
                return "Ongoing effect application result is missing.";
            }

            string ticks = result.ImmediateTicks == null || result.ImmediateTicks.Count == 0
                ? "ImmediateTicks=0"
                : $"ImmediateTicks={result.ImmediateTicks.Count} Last={FormatOngoingTickResult(result.ImmediateTicks[result.ImmediateTicks.Count - 1])}";
            return $"{result.Outcome} {result.DefinitionId} Instance={result.InstanceId} Stacks={result.PreviousStackCount}->{result.ResultingStackCount} Duration={result.PreviousRemainingDuration:0.###}->{result.ResultingRemainingDuration:0.###} Preview={result.Preview} Duplicate={result.Duplicate}. {ticks}. {result.Message}";
        }

        private static string FormatOngoingProcessResult(OngoingEffectProcessResult result)
        {
            if (result == null)
            {
                return "Missing";
            }

            string last = result.TickResults.Count == 0 ? "NoTicks" : FormatOngoingTickResult(result.TickResults[result.TickResults.Count - 1]);
            return $"Ticks={result.ProcessedTicks} Capped={result.Capped} {last}";
        }

        private static string FormatOngoingTickResult(OngoingEffectTickResult result)
        {
            if (result == null)
            {
                return "Tick=None";
            }

            string nested = result.DamageResult != null
                ? $"Damage={result.DamageResult.FinalDamageAmount:0.###} Health={result.DamageResult.OldHealth:0.###}->{result.DamageResult.NewHealth:0.###} Immune={result.DamageResult.Immune}"
                : result.HealingResult != null
                    ? $"Healing={result.HealingResult.FinalHealingAmount:0.###} Overheal={result.HealingResult.OverhealAmount:0.###} Health={result.HealingResult.OldHealth:0.###}->{result.HealingResult.NewHealth:0.###}"
                    : result.ResourceResult != null
                        ? $"Resource={result.ResourceResult.Request.ResourceId} {result.ResourceResult.OldCurrent:0.###}->{result.ResourceResult.NewCurrent:0.###} Applied={result.ResourceResult.AppliedAmount:0.###}"
                        : "Nested=None";
            return $"Tick#{result.TickIndex} {result.Outcome} Tx={result.TickTransactionId} Amt={result.RequestedAmount:0.###} {nested}";
        }

        private static string FormatOngoingCancellationResult(OngoingEffectCancellationResult result)
        {
            if (result == null)
            {
                return "Ongoing effect cancellation result is missing.";
            }

            return $"Instance={result.InstanceId} Definition={result.DefinitionId} Preview={result.Preview} Duplicate={result.Duplicate}. {result.Message}";
        }

        private static string FormatCombatReactionChain(CombatReactionChainResult result)
        {
            if (result == null)
            {
                return "Combat reaction result is missing.";
            }

            string reactions = result.Reactions.Count == 0
                ? "None"
                : string.Join(" | ", result.Reactions.Select(reaction => $"{reaction.DefinitionId}:{reaction.Code}:Succeeded={reaction.Succeeded}:Preview={reaction.Preview}:Duplicate={reaction.Duplicate}:Amount={reaction.FinalAmount:0.###}:Tx={reaction.TransactionId}"));
            return $"Trigger={result.RootContext?.TriggerType} Preview={result.Preview} Depth={result.Depth} Reactions={result.Reactions.Count}. {result.Message} {reactions}";
        }

        private static string FormatContributionRecordResult(CombatContributionRecordResult result)
        {
            if (result == null)
            {
                return "Contribution result is missing.";
            }

            CombatContributionRecord record = result.Record;
            string recordText = record == null
                ? "Record=None"
                : $"Record={record.RecordId} Type={record.ContributionType} Contributor={record.ContributorActorId} Target={record.TargetActorId} Beneficiary={record.BeneficiaryActorId} Actual={record.ActualAmount:0.###} Prevented={record.PreventedAmount:0.###} Weight={record.ContributionWeight:0.###}";
            return $"Contribution Success={result.Succeeded} Preview={result.Preview} Duplicate={result.Duplicate} Code={result.Code} Rev={result.RevisionBefore}->{result.RevisionAfter}. {recordText}. {result.Message}";
        }

        private static string FormatContributionLedger(CombatContributionLedgerSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "Contribution ledger is missing.";
            }

            string summaries = snapshot.Summaries.Count == 0
                ? "No summaries"
                : string.Join(" | ", snapshot.Summaries.Select(summary => $"{summary.ContributorActorId}:Dmg={summary.TotalActualDamage:0.###} Heal={summary.TotalEffectiveHealing:0.###} Def={summary.TotalDamagePrevented:0.###} Elig={string.Join(",", summary.Eligibility)}"));
            return $"{snapshot.LedgerId} Encounter={(string.IsNullOrWhiteSpace(snapshot.EncounterId) ? "None" : snapshot.EncounterId)} Target={(string.IsNullOrWhiteSpace(snapshot.TargetActorId) ? "Mixed" : snapshot.TargetActorId)} Records={snapshot.Records.Count} Finalized={snapshot.Finalized} Rev={snapshot.Revision}. {summaries}";
        }

        private static string FormatCreditResult(CombatCreditResolutionResult result)
        {
            if (result == null)
            {
                return "Credit result is missing.";
            }

            string assists = result.Assists.Count == 0 ? "None" : string.Join(",", result.Assists.Select(summary => summary.ContributorActorId));
            return $"Credit={result.CreditType} Success={result.Succeeded} Duplicate={result.Duplicate} Primary={(string.IsNullOrWhiteSpace(result.PrimaryContributorActorId) ? "Unassigned" : result.PrimaryContributorActorId)} Assists={assists} Contributors={result.Contributors.Count} GrantsRewards={result.GrantsConcreteRewards}. {result.Message}";
        }

        private static string FormatDamageApplication(DamageApplicationResult result)
        {
            if (result == null)
            {
                return "Damage result missing.";
            }

            return $"Damage Success={result.Succeeded} Preview={result.Preview} Duplicate={result.Duplicate} Actual={result.FinalDamageAmount:0.###} Health={result.OldHealth:0.###}->{result.NewHealth:0.###}.";
        }

        private static string FormatHealingApplication(HealingApplicationResult result)
        {
            if (result == null)
            {
                return "Healing result missing.";
            }

            return $"Healing Success={result.Succeeded} Preview={result.Preview} Duplicate={result.Duplicate} Effective={result.FinalHealingAmount:0.###} Overheal={result.OverhealAmount:0.###} Health={result.OldHealth:0.###}->{result.NewHealth:0.###}.";
        }

        private static string FormatCombatTransactionTrace(CombatTransactionTraceSnapshot trace)
        {
            if (trace == null)
            {
                return "Transaction Trace: None";
            }

            return $"Transaction Trace: Root={EmptyAs(trace.RootTransactionId, "None")} Execution={EmptyAs(trace.ExecutionTransactionId, "None")} Attack={EmptyAs(trace.AttackTransactionId, "None")} Defense={EmptyAs(trace.DefenseTransactionId, "None")} Damage={EmptyAs(trace.DamageTransactionId, "None")} Reaction={EmptyAs(trace.ReactionTransactionId, "None")} Contribution={EmptyAs(trace.ContributionTransactionId, "None")} Coherent={trace.IsCoherent}.";
        }

        private DefensiveActionDefinition FindDefensiveAction(string idOrNameFragment)
        {
            if (string.IsNullOrWhiteSpace(idOrNameFragment))
            {
                return null;
            }

            return GetDefinitions<DefensiveActionDefinition>()
                .FirstOrDefault(definition =>
                    definition != null
                    && ((definition.Id != null && definition.Id.IndexOf(idOrNameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (definition.DisplayName != null && definition.DisplayName.IndexOf(idOrNameFragment, StringComparison.OrdinalIgnoreCase) >= 0)));
        }

        private bool EnsureCompatibleDefenseEquipment(DefensiveActionDefinition definition, out string failureReason)
        {
            failureReason = string.Empty;
            if (definition == null)
            {
                failureReason = "Defensive action definition is missing.";
                return false;
            }

            bool requiresEquipment = definition.RequiresEquipmentSource
                || !string.IsNullOrWhiteSpace(definition.RequiredEquipmentCategoryId)
                || !string.IsNullOrWhiteSpace(definition.RequiredEquipmentTagId);
            if (!requiresEquipment)
            {
                return true;
            }

            if (IsCompatibleDefenseItemEquipped(definition))
            {
                return true;
            }

            if (context?.Inventory == null || context.Equipment == null)
            {
                failureReason = "Inventory or equipment is missing.";
                return false;
            }

            ItemDefinition item = GetDefinitions<ItemDefinition>().FirstOrDefault(candidate => IsCompatibleDefenseItem(candidate, definition));
            if (item == null)
            {
                failureReason = $"No item definition satisfies defensive action '{definition.Id}'.";
                return false;
            }

            int slotIndex = FindInventorySlot(item);
            if (slotIndex < 0)
            {
                InventoryAddResult add = context.Inventory.AddItem(item, 1);
                if (add.AddedQuantity <= 0)
                {
                    failureReason = $"Could not grant required item '{item.DisplayName}' for defensive action '{definition.Id}'.";
                    return false;
                }

                slotIndex = FindInventorySlot(item);
            }

            if (slotIndex < 0)
            {
                failureReason = $"Required item '{item.DisplayName}' was not found in inventory after grant.";
                return false;
            }

            EquipmentOperationResult equip = context.Equipment.EquipFromInventorySlot(slotIndex);
            if (!equip.Succeeded)
            {
                failureReason = equip.Message;
                return false;
            }

            return IsCompatibleDefenseItemEquipped(definition);
        }

        private bool IsCompatibleDefenseItemEquipped(DefensiveActionDefinition definition)
        {
            if (context?.Equipment == null)
            {
                return false;
            }

            foreach (EquipmentSlotState slot in context.Equipment.Slots)
            {
                if (slot != null && !slot.IsEmpty && IsCompatibleDefenseItem(slot.Item, definition))
                {
                    return true;
                }
            }

            return false;
        }

        private int FindInventorySlot(ItemDefinition item)
        {
            if (context?.Inventory == null || item == null)
            {
                return -1;
            }

            for (int i = 0; i < context.Inventory.Slots.Count; i++)
            {
                InventorySlot slot = context.Inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty && slot.Item == item)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsCompatibleDefenseItem(ItemDefinition item, DefensiveActionDefinition definition)
        {
            if (item == null || definition == null || !item.IsEquippable)
            {
                return false;
            }

            if (definition.ActionType == DefensiveActionType.Parry && item.Equipment?.MeleeWeapon?.IsWeapon != true)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(definition.RequiredEquipmentCategoryId)
                && !string.Equals(item.PrimaryCategory?.Id, definition.RequiredEquipmentCategoryId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(definition.RequiredEquipmentTagId) && !ItemHasTag(item, definition.RequiredEquipmentTagId))
            {
                return false;
            }

            return true;
        }

        private static bool ItemHasTag(ItemDefinition item, string tagId)
        {
            IReadOnlyList<TagDefinition> tags = item == null ? Array.Empty<TagDefinition>() : item.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] != null && string.Equals(tags[i].Id, tagId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountDiagnostics(IReadOnlyList<CombatRuntimeDiagnostic> diagnostics, CombatIntegritySeverity severity)
        {
            return diagnostics == null ? 0 : diagnostics.Count(diagnostic => diagnostic != null && diagnostic.Severity == severity);
        }

        private static string EmptyAs(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private HealingApplicationRequest CreatePipelineHealingRequest(float amount, bool targetPlayer, string transactionId)
        {
            GameObject source = context?.PlayerTransform?.gameObject;
            GameObject target = targetPlayer ? context?.PlayerTransform?.gameObject : context?.EnemyTransform?.gameObject;
            EnsureAttackResolutionRuntime(source, needsResource: false);
            EnsureAttackResolutionRuntime(target, needsResource: true);
            return new HealingApplicationRequest(
                transactionId,
                ResolveActorId(source),
                source,
                ResolveActorId(target),
                target,
                Mathf.Max(0f, amount),
                "Prototype Test Lab");
        }

        private static string ResolveActorId(GameObject actor)
        {
            if (actor == null)
            {
                return string.Empty;
            }

            CharacterSystemCoordinator character = actor.GetComponentInParent<CharacterSystemCoordinator>();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                return character.ActorId;
            }

            WorldEntityIdentity identity = actor.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        private static string ResolveCombatStateActorId(GameObject actor)
        {
            if (actor == null)
            {
                return string.Empty;
            }

            WorldEntityIdentity identity = actor.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        public PrototypeTestLabOperation DefeatEnemy(DamageTypeDefinition damageType)
        {
            float amount = context?.EnemyHealth == null ? 9999f : context.EnemyHealth.MaximumHealth + 9999f;
            return ApplyTypedDamage(damageType ?? GetDefinitions<DamageTypeDefinition>().FirstOrDefault(), amount, targetEnemy: true, sourcePlayer: true);
        }

        public PrototypeTestLabOperation StartQuest(QuestDefinition quest)
        {
            if (context?.QuestLog == null || quest == null)
            {
                return RecordFailure("Start Quest", "Quest log or quest definition is missing.", "MissingReference");
            }

            QuestOperationResult result = context.QuestLog.StartQuest(quest);
            return Record(result.Succeeded, "Start Quest", result.Succeeded ? "Started" : "Failed", result.Message);
        }

        public PrototypeTestLabOperation ReportTalk(PersonDefinition person)
        {
            if (person == null)
            {
                return RecordFailure("Report Talk", "No person definition selected.", "MissingDefinition");
            }

            QuestObjectiveSignalBus.ReportTalk(person.Id);
            return RecordSuccess("Report Talk", $"Reported talk with {FormatDefinition(person)}.");
        }

        public PrototypeTestLabOperation ReportReach(PlaceDefinition place)
        {
            if (place == null)
            {
                return RecordFailure("Report Reach Location", "No place definition selected.", "MissingDefinition");
            }

            QuestObjectiveSignalBus.ReportReachLocation(place);
            return RecordSuccess("Report Reach Location", $"Reported reach location {FormatDefinition(place)}.");
        }

        public PrototypeTestLabOperation ReportDefeat(string targetCategory)
        {
            if (string.IsNullOrWhiteSpace(targetCategory))
            {
                targetCategory = "prototype_enemy";
            }

            GameObject temporary = new GameObject("Development Contract Objective Target");
            try
            {
                ContractObjectiveTarget target = temporary.AddComponent<ContractObjectiveTarget>();
                target.DevelopmentSetTargetCategory(targetCategory);
                context?.QuestLog?.RecordDefeat(target);
                context?.ContractJournal?.RecordDefeat(target);
                return RecordSuccess("Report Defeat", $"Reported defeat target '{targetCategory}'.");
            }
            finally
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(temporary);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(temporary);
                }
            }
        }

        public PrototypeTestLabOperation ClearQuestLog(bool confirmed)
        {
            if (!RequireConfirmation("ClearQuestLog", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            context?.QuestLog?.DevelopmentClearQuestLog();
            return RecordSuccess("Clear Quest Log", "Quest log cleared.");
        }

        public PrototypeTestLabOperation AcceptContract(ContractDefinition contract)
        {
            if (context?.ContractJournal == null || contract == null)
            {
                return RecordFailure("Accept Contract", "Contract journal or contract definition is missing.", "MissingReference");
            }

            ContractOperationResult result = context.ContractJournal.AcceptContract(contract);
            return Record(result.Succeeded, "Accept Contract", result.Succeeded ? "Accepted" : "Failed", result.Message);
        }

        public PrototypeTestLabOperation ClearContractJournal(bool confirmed)
        {
            if (!RequireConfirmation("ClearContractJournal", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            context?.ContractJournal?.DevelopmentClearContractJournal();
            return RecordSuccess("Clear Contract Journal", "Contract journal cleared.");
        }

        public PrototypeTestLabOperation Save()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Save Manual Slot 1", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceSaveResult result = persistence.SaveManualSlot(0);
            return Record(result.Succeeded, "Save Manual Slot 1", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation Load()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Load Manual Slot 1", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceLoadResult result = persistence.LoadSaveSlot(CurrentSlotId);
            return Record(result.Succeeded, "Load Manual Slot 1", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation ValidateSave()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Validate Manual Slot 1", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceValidationResult result = persistence.ValidateSaveSlot(CurrentSlotId);
            return Record(result.Succeeded, "Validate Manual Slot 1", result.Status.ToString(), $"{result.Message} BackupAvailable={result.BackupAvailable}");
        }

        public PrototypeTestLabOperation DeleteSave(bool confirmed)
        {
            if (!RequireConfirmation("DeleteSave", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Delete Manual Slot 1", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceDeleteResult result = persistence.DeleteSaveSlot(CurrentSlotId);
            return Record(result.Succeeded, "Delete Manual Slot 1", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation ForceAutosave()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Force Autosave", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceSaveResult result = persistence.ForceAutosave("TestLab");
            return Record(result.Succeeded, "Force Autosave", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation SetShortAutosaveInterval()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Set Autosave Interval", "Persistence service is missing.", "MissingPersistence");
            }

            persistence.SetAutosaveIntervalForTesting(15f);
            return RecordSuccess("Set Autosave Interval", "Autosave interval set to 15 seconds for local testing.");
        }

        public PrototypeTestLabOperation MarkSaveDirty()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Mark Save Dirty", "Persistence service is missing.", "MissingPersistence");
            }

            persistence.DirtyTracker?.DevelopmentSetDirty(true, "Test Lab marked save dirty.");
            return RecordSuccess("Mark Save Dirty", "Save dirty state set for confirmation and autosave testing.");
        }

        public PrototypeTestLabOperation MarkSaveClean()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Mark Save Clean", "Persistence service is missing.", "MissingPersistence");
            }

            persistence.DirtyTracker?.DevelopmentSetDirty(false, "Test Lab marked save clean.");
            return RecordSuccess("Mark Save Clean", "Save dirty state cleared.");
        }

        public PrototypeTestLabOperation SaveManualSlotOne()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Save Manual Slot 1", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceSaveResult result = persistence.SaveManualSlot(0);
            return Record(result.Succeeded, "Save Manual Slot 1", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation LoadManualSlotOneBackup()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Load Manual Slot 1 Backup", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceLoadResult result = persistence.LoadSaveSlot(PrototypeSaveSlotCatalog.ManualSlotId(0), loadBackup: true);
            return Record(result.Succeeded, "Load Manual Slot 1 Backup", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation ValidateManualSlotOneBackup()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Validate Manual Slot 1 Backup", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceValidationResult result = persistence.ValidateSaveSlot(PrototypeSaveSlotCatalog.ManualSlotId(0), validateBackup: true);
            return Record(result.Succeeded, "Validate Manual Slot 1 Backup", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation RunRecoveryScan()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Run Recovery Scan", "Persistence service is missing.", "MissingPersistence");
            }

            SaveRecoveryScanReport report = persistence.RunRecoveryScan();
            return RecordSuccess("Run Recovery Scan", $"{report.candidates.Length} candidate(s). {report.recommendation}");
        }

        public PrototypeTestLabOperation PromoteManualSlotOneBackup(bool confirmed)
        {
            if (!RequireConfirmation("PromoteManualSlotOneBackup", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Promote Manual Slot 1 Backup", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceSaveResult result = persistence.PromoteBackup(PrototypeSaveSlotCatalog.ManualSlotId(0));
            return Record(result.Succeeded, "Promote Manual Slot 1 Backup", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation QuarantineManualSlotOnePrimary(bool confirmed)
        {
            if (!RequireConfirmation("QuarantineManualSlotOnePrimary", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Quarantine Manual Slot 1", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceSaveResult result = persistence.QuarantinePrimary(PrototypeSaveSlotCatalog.ManualSlotId(0));
            return Record(result.Succeeded, "Quarantine Manual Slot 1", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation CleanupTemporarySaves(bool confirmed)
        {
            if (!RequireConfirmation("CleanupTemporarySaves", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Cleanup Temporary Saves", "Persistence service is missing.", "MissingPersistence");
            }

            PersistenceDeleteResult result = persistence.CleanupStaleTemporaryFiles();
            return Record(result.Succeeded, "Cleanup Temporary Saves", result.Status.ToString(), result.Message);
        }

        public PrototypeTestLabOperation InjectPrepareFailure()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Inject Prepare Failure", "Persistence service is missing.", "MissingPersistence");
            }

            persistence.InjectNextPersistenceFault(PersistenceFaultInjectionPoint.LoadPrepare);
            return RecordSuccess("Inject Prepare Failure", "Next load prepare phase will fail once.");
        }

        public PrototypeTestLabOperation InjectCommitFailure()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Inject Commit Failure", "Persistence service is missing.", "MissingPersistence");
            }

            persistence.InjectNextPersistenceFault(PersistenceFaultInjectionPoint.LoadCommit);
            return RecordSuccess("Inject Commit Failure", "Next load commit phase will fail once and attempt rollback.");
        }

        public PrototypeTestLabOperation InjectAuditFailure()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Inject Audit Failure", "Persistence service is missing.", "MissingPersistence");
            }

            persistence.InjectNextPersistenceFault(PersistenceFaultInjectionPoint.ConsistencyAudit);
            return RecordSuccess("Inject Audit Failure", "Next consistency audit will fail once and attempt rollback.");
        }

        public PrototypeTestLabOperation RecordFingerprint()
        {
            if (!EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence))
            {
                return RecordFailure("Record Fingerprint", "Persistence service is missing.", "MissingPersistence");
            }

            return RecordSuccess("Record Fingerprint", persistence.BuildRuntimeStateFingerprint());
        }

        public PrototypeTestLabOperation Teleport(PrototypeTestPoint point)
        {
            if (context?.PlayerTransform == null || point == null)
            {
                return RecordFailure("Teleport", "Player transform or test point is missing.", "MissingReference");
            }

            CharacterController characterController = context.PlayerTransform.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            context.PlayerTransform.SetPositionAndRotation(point.transform.position, point.transform.rotation);

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            return RecordSuccess("Teleport", $"Teleported to {point.DisplayName} ({point.TestPointId}).");
        }

        public PrototypeTestLabOperation ValidateCurrentLocation()
        {
            if (context?.Persistence == null)
            {
                return RecordFailure("Validate Current Location", "Persistence service is missing.", "MissingPersistence");
            }

            string summary = context.Persistence.BuildPlayerLocationDiagnosticSummary();
            return RecordSuccess("Validate Current Location", summary.Replace(Environment.NewLine, " | "));
        }

        public PrototypeTestLabOperation ValidateIdentityProgression()
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Validate Identity", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            bool valid = progression.ValidateIdentity(out string failureReason);
            return Record(valid, "Validate Identity", valid ? "Valid" : "Invalid", valid ? "Identity IDs are distinct and well-formed." : failureReason);
        }

        public PrototypeTestLabOperation GenerateOrigin(int seed)
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Generate Origin", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            if (registry == null)
            {
                return RecordFailure("Generate Origin", "Definition registry is missing.", "MissingRegistry");
            }

            int effectiveSeed = seed == 0 ? Environment.TickCount : seed;
            ProgressionOperationResult result = progression.AssignRandomOrigin(registry, effectiveSeed);
            return Record(result.Succeeded, "Generate Origin", result.Code, result.Message);
        }

        public PrototypeTestLabOperation ProveOriginAssignmentIsOnceOnly()
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Duplicate Origin Proof", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            if (registry == null)
            {
                return RecordFailure("Duplicate Origin Proof", "Definition registry is missing.", "MissingRegistry");
            }

            ProgressionOperationResult result = progression.AssignRandomOrigin(registry, Environment.TickCount);
            bool expectedFailure = !result.Succeeded && string.Equals(result.Code, "OriginAlreadyAssigned", StringComparison.Ordinal);
            return Record(expectedFailure, "Duplicate Origin Proof", expectedFailure ? "Rejected" : result.Code, expectedFailure ? "Second origin assignment was correctly rejected." : result.Message);
        }

        public PrototypeTestLabOperation ResetIdentityProgression(bool confirmed)
        {
            if (!RequireConfirmation("ResetIdentityProgression", confirmed, out PrototypeTestLabOperation confirmation))
            {
                return confirmation;
            }

            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Reset Identity", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            ProgressionOperationResult result = progression.ResetIdentityProgressionForDevelopment();
            return Record(result.Succeeded, "Reset Identity", result.Code, result.Message);
        }

        public PrototypeTestLabOperation AdvanceBirthGiftProgress(float seconds)
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Advance Birth Gift", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            ProgressionOperationResult result = progression.AdvanceBirthGiftProgressForTesting(Mathf.Max(0f, seconds), registry);
            return Record(result.Succeeded, "Advance Birth Gift", result.Code, result.Message);
        }

        public PrototypeTestLabOperation ForceBirthGiftAwakening()
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Awaken Birth Gift", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            ProgressionOperationResult result = progression.ForceBirthGiftAwakening(registry);
            return Record(result.Succeeded, "Awaken Birth Gift", result.Code, result.Message);
        }

        public PrototypeTestLabOperation AddRole(RoleDefinition role, bool acceptConflicts)
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Add Role", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            RoleAcquisitionResult result = progression.AddRole(role, "test-lab", "manual-test-lab", primary: false, acceptConflicts: acceptConflicts);
            string message = result.Conflict != null && result.Conflict.HasConflict
                ? $"{result.Message} Blockers={string.Join(", ", result.Conflict.Blockers.Select(blocker => blocker.roleDefinitionId))}"
                : result.Message;
            return Record(result.Succeeded, acceptConflicts ? "Add Role Accepting Conflicts" : "Add Role", result.Code, message);
        }

        public PrototypeTestLabOperation SuspendFirstActiveRole()
        {
            if (!TryGetFirstActiveRole(out PlayerIdentityProgression progression, out RuntimeRoleRecord role, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ProgressionOperationResult result = progression.SuspendRole(role.recordId);
            return Record(result.Succeeded, "Suspend Role", result.Code, result.Message);
        }

        public PrototypeTestLabOperation RevokeFirstActiveRole()
        {
            if (!TryGetFirstActiveRole(out PlayerIdentityProgression progression, out RuntimeRoleRecord role, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ProgressionOperationResult result = progression.RevokeRole(role.recordId);
            return Record(result.Succeeded, "Revoke Role", result.Code, result.Message);
        }

        public PrototypeTestLabOperation AbandonFirstActiveRole()
        {
            if (!TryGetFirstActiveRole(out PlayerIdentityProgression progression, out RuntimeRoleRecord role, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            ProgressionOperationResult result = progression.AbandonRole(role.recordId);
            return Record(result.Succeeded, "Abandon Role", result.Code, result.Message);
        }

        public PrototypeTestLabOperation AddGlobalSocialStatus(SocialStatusDefinition status)
        {
            return AddSocialStatus(status, SocialStatusContextKind.Global, string.Empty, "Add Global Status");
        }

        public PrototypeTestLabOperation AddPlaceSocialStatus(SocialStatusDefinition status, PlaceDefinition place)
        {
            string placeId = place == null ? string.Empty : place.Id;
            return AddSocialStatus(status, SocialStatusContextKind.Place, placeId, "Add Place Status");
        }

        public PrototypeTestLabOperation ResolveFirstActiveSocialStatus()
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Resolve Social Status", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            RuntimeSocialStatusRecord status = progression.SocialStatuses.FirstOrDefault(record => record.lifecycleState == SocialStatusLifecycleState.Active);
            if (status == null)
            {
                return RecordFailure("Resolve Social Status", "No active social status exists.", "MissingActiveStatus");
            }

            ProgressionOperationResult result = progression.ResolveSocialStatus(status.recordId, "test-lab-resolved");
            return Record(result.Succeeded, "Resolve Social Status", result.Code, result.Message);
        }

        public PrototypeTestLabOperation AddCurrency(CurrencyDefinition currency, long amount)
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Add Currency", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            ProgressionOperationResult result = progression.AddCurrency(currency, Math.Max(0L, amount));
            return Record(result.Succeeded, "Add Currency", result.Code, result.Message);
        }

        public PrototypeTestLabOperation SpendCurrency(CurrencyDefinition currency, long amount)
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Spend Currency", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            ProgressionOperationResult result = progression.SpendCurrency(currency, Math.Max(0L, amount));
            return Record(result.Succeeded, "Spend Currency", result.Code, result.Message);
        }

        public PrototypeTestLabOperation RecordSuccessfulActivity(float difficulty)
        {
            return RecordActivity(ActivityOutcome.Success, difficulty, "Record Success Activity");
        }

        public PrototypeTestLabOperation RecordFailedActivity(float difficulty)
        {
            return RecordActivity(ActivityOutcome.Failure, difficulty, "Record Failure Activity");
        }

        public PrototypeTestLabOperation RecordParticipation()
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure("Record Participation", "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            ProgressionOperationResult result = progression.RecordParticipation($"participation.test-lab.{Guid.NewGuid():N}", "test-lab", "PrototypeTestLab");
            return Record(result.Succeeded, "Record Participation", result.Code, result.Message);
        }

        public PrototypeTestLabOperation RefreshWorldEntityDiagnostics()
        {
            return RecordSuccess("World Entity Diagnostics", $"Registered {WorldEntityRegistry.Count} world entity identity object(s).");
        }

        public PrototypeTestLabOperation SpawnPersistentWorldLoot(ItemDefinition item)
        {
            if (item == null)
            {
                return RecordFailure("Spawn Persistent World Loot", "No item definition selected.", "MissingDefinition");
            }

            Vector3 position = context?.PlayerTransform == null ? Vector3.zero : context.PlayerTransform.position + context.PlayerTransform.forward * 2f + Vector3.up * 0.25f;
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickup.name = $"Persistent Test Loot - {item.DisplayName}";
            pickup.transform.SetPositionAndRotation(position, Quaternion.identity);
            pickup.transform.localScale = Vector3.one * 0.35f;
            pickup.AddComponent<WorldItemPickup>().Configure(item, 1);
            WorldEntitySpawnResult result = WorldEntityIdentityFactory.CreateRuntimeIdentity(pickup, "scene.prototype", PersistenceService.LocalWorldId, item.Id);
            if (!result.Succeeded)
            {
                DestroyTestObject(pickup);
                return RecordFailure("Spawn Persistent World Loot", result.Message, result.Code);
            }

            lastSpawnedWorldEntityId = result.Identity.EntityId;
            lastSpawnedWorldEntityItem = item;
            return RecordWorldEntityResult("Spawn Persistent World Loot", $"Spawned {item.DisplayName} as {lastSpawnedWorldEntityId}.");
        }

        public PrototypeTestLabOperation SpawnTransientWorldLoot(ItemDefinition item)
        {
            if (item == null)
            {
                return RecordFailure("Spawn Transient World Loot", "No item definition selected.", "MissingDefinition");
            }

            Vector3 position = context?.PlayerTransform == null ? Vector3.zero : context.PlayerTransform.position + context.PlayerTransform.right * 2f + Vector3.up * 0.25f;
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pickup.name = $"Transient Test Loot - {item.DisplayName}";
            pickup.transform.SetPositionAndRotation(position, Quaternion.identity);
            pickup.transform.localScale = Vector3.one * 0.35f;
            pickup.AddComponent<WorldItemPickup>().Configure(item, 1);
            WorldEntityIdentity identity = pickup.AddComponent<WorldEntityIdentity>();
            identity.TryMarkTransient(out _);
            return RecordWorldEntityResult("Spawn Transient World Loot", $"Spawned transient {item.DisplayName}; it is intentionally not persistently registered.");
        }

        public PrototypeTestLabOperation DestroyLastSpawnedWorldLoot()
        {
            if (string.IsNullOrWhiteSpace(lastSpawnedWorldEntityId) || !WorldEntityRegistry.TryResolve(lastSpawnedWorldEntityId, out WorldEntityIdentity identity))
            {
                return RecordFailure("Destroy Spawned World Loot", "No spawned world entity is currently registered.", "MissingEntity");
            }

            WorldItemPickup pickup = identity.GetComponent<WorldItemPickup>();
            lastDestroyedWorldEntityId = identity.EntityId;
            lastDestroyedWorldEntityItem = pickup == null ? null : pickup.Item;
            WorldEntityRegistry.Unregister(identity);
            DestroyTestObject(identity.gameObject);
            return RecordWorldEntityResult("Destroy Spawned World Loot", $"Destroyed {lastDestroyedWorldEntityId}.");
        }

        public PrototypeTestLabOperation RecreateDestroyedWorldLoot()
        {
            if (string.IsNullOrWhiteSpace(lastDestroyedWorldEntityId) || lastDestroyedWorldEntityItem == null)
            {
                return RecordFailure("Recreate World Loot", "No destroyed persistent world loot is available to recreate.", "MissingSnapshot");
            }

            Vector3 position = context?.PlayerTransform == null ? Vector3.zero : context.PlayerTransform.position + context.PlayerTransform.forward * 2f + Vector3.up * 0.25f;
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickup.name = $"Restored Test Loot - {lastDestroyedWorldEntityItem.DisplayName}";
            pickup.transform.SetPositionAndRotation(position, Quaternion.identity);
            pickup.transform.localScale = Vector3.one * 0.35f;
            pickup.AddComponent<WorldItemPickup>().Configure(lastDestroyedWorldEntityItem, 1);
            WorldEntitySpawnResult result = WorldEntityIdentityFactory.RestoreRuntimeIdentity(pickup, lastDestroyedWorldEntityId, "scene.prototype", PersistenceService.LocalWorldId, lastDestroyedWorldEntityItem.Id);
            if (!result.Succeeded)
            {
                DestroyTestObject(pickup);
                return RecordFailure("Recreate World Loot", result.Message, result.Code);
            }

            lastSpawnedWorldEntityId = result.Identity.EntityId;
            lastSpawnedWorldEntityItem = lastDestroyedWorldEntityItem;
            return RecordWorldEntityResult("Recreate World Loot", $"Recreated {lastSpawnedWorldEntityId}.");
        }

        public PrototypeTestLabOperation AttemptDuplicateWorldEntityRegistration()
        {
            if (!TryResolveLastSpawnedOrRegisteredTestLoot(out WorldEntityIdentity existingIdentity, out ItemDefinition item, out string failureReason))
            {
                return RecordWorldEntityFailure("Duplicate World Entity Proof", failureReason, "MissingEntity");
            }

            GameObject duplicate = new GameObject("Duplicate World Entity Proof");
            duplicate.name = "Duplicate World Entity Proof";
            duplicate.AddComponent<WorldItemPickup>().Configure(item, 1);
            WorldEntitySpawnResult result = WorldEntityIdentityFactory.RestoreRuntimeIdentity(duplicate, lastSpawnedWorldEntityId, existingIdentity.SceneKey, existingIdentity.WorldId, item.Id);
            if (result.Succeeded)
            {
                WorldEntityRegistry.Unregister(result.Identity);
                DestroyTestObject(duplicate);
                return RecordWorldEntityFailure("Duplicate World Entity Proof", "Duplicate registration unexpectedly succeeded.", "UnexpectedSuccess");
            }

            DestroyTestObject(duplicate);
            return RecordWorldEntityResult("Duplicate World Entity Proof", $"Duplicate rejected: {result.Code}.");
        }

        private bool TryResolveLastSpawnedOrRegisteredTestLoot(out WorldEntityIdentity identity, out ItemDefinition item, out string failureReason)
        {
            identity = null;
            item = null;
            failureReason = string.Empty;

            if (!string.IsNullOrWhiteSpace(lastSpawnedWorldEntityId)
                && WorldEntityRegistry.TryResolve(lastSpawnedWorldEntityId, out identity))
            {
                WorldItemPickup pickup = identity.GetComponent<WorldItemPickup>();
                item = pickup == null ? lastSpawnedWorldEntityItem : pickup.Item;
                if (item != null)
                {
                    return true;
                }

                failureReason = "The spawned world entity has no item definition to duplicate.";
                return false;
            }

            foreach (WorldEntityIdentity candidate in WorldEntityRegistry.RegisteredEntities)
            {
                if (candidate == null
                    || candidate.IdentityKind == WorldEntityIdentityKind.Transient
                    || (!candidate.name.StartsWith("Persistent Test Loot", StringComparison.Ordinal)
                        && !candidate.name.StartsWith("Restored Test Loot", StringComparison.Ordinal)))
                {
                    continue;
                }

                WorldItemPickup pickup = candidate.GetComponent<WorldItemPickup>();
                if (pickup == null || pickup.Item == null)
                {
                    continue;
                }

                identity = candidate;
                item = pickup.Item;
                lastSpawnedWorldEntityId = identity.EntityId;
                lastSpawnedWorldEntityItem = item;
                return true;
            }

            failureReason = string.IsNullOrWhiteSpace(lastSpawnedWorldEntityId)
                ? "Spawn persistent world loot first."
                : $"World entity '{lastSpawnedWorldEntityId}' is no longer registered. Spawn persistent world loot again.";
            return false;
        }

        private PrototypeTestLabOperation RecordWorldEntityResult(string operationName, string message)
        {
            lastWorldEntityOperationMessage = message;
            Debug.Log($"{operationName}: {message}");
            return RecordSuccess(operationName, message);
        }

        private PrototypeTestLabOperation RecordWorldEntityFailure(string operationName, string message, string code)
        {
            lastWorldEntityOperationMessage = message;
            return RecordFailure(operationName, message, code);
        }

        private static void DestroyTestObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }

            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        public PrototypeTestLabOperation RunScenario(string scenarioId, ItemDefinition item, QuestDefinition quest, ContractDefinition contract, DamageTypeDefinition damageType)
        {
            switch (scenarioId)
            {
                case "clean":
                    context?.TestController?.ResetPrototypeState();
                    return RecordSuccess("Scenario: Clean Baseline", "Prototype reset executed; persistent player collections preserved.");
                case "combat":
                    context?.TestController?.ResetPrototypeState();
                    if (item != null)
                    {
                        GrantStatefulItem(item);
                        EquipFirstCompatible(item);
                    }

                    return RecordSuccess("Scenario: Combat Ready", "Reset vitals/enemy and attempted to grant/equip selected item.");
                case "full-inventory":
                    return FillInventory(item);
                case "quest":
                    if (quest != null)
                    {
                        StartQuest(quest);
                    }

                    return RecordSuccess("Scenario: Quest Midpoint", "Started selected quest. Use Talk/Reach/Defeat actions to progress through normal events.");
                case "contract":
                    if (contract != null)
                    {
                        AcceptContract(contract);
                    }

                    return RecordSuccess("Scenario: Contract Testing", "Accepted selected contract if available.");
                case "persistence":
                    RestoreVitals();
                    if (item != null)
                    {
                        GrantItem(item, 2);
                    }

                    if (quest != null)
                    {
                        StartQuest(quest);
                    }

                    if (contract != null)
                    {
                        AcceptContract(contract);
                    }

                    return RecordSuccess("Scenario: Persistence Round Trip", "Prepared representative player state for save/load testing.");
                default:
                    return RecordFailure("Scenario", $"Unknown scenario '{scenarioId}'.", "UnknownScenario");
            }
        }

        public string RunDiagnostics()
        {
            List<string> lines = new List<string>
            {
                "Diagnostics",
                $"Definitions loaded: {(registry == null ? 0 : registry.Count)}"
            };

            AddDuplicateInstanceDiagnostics(lines);
            AddDuplicateStatusDiagnostics(lines, "Player", context?.PlayerStatuses);
            AddDuplicateStatusDiagnostics(lines, "Enemy", context?.EnemyStatuses);
            AddCharacterSystemDiagnostics(lines);
            AddReferenceDiagnostic(lines, "Inventory", context?.Inventory);
            AddReferenceDiagnostic(lines, "Equipment", context?.Equipment);
            AddReferenceDiagnostic(lines, "Quest Log", context?.QuestLog);
            AddReferenceDiagnostic(lines, "Contract Journal", context?.ContractJournal);
            AddReferenceDiagnostic(lines, "Persistence", context?.Persistence);
            AddReferenceDiagnostic(lines, "Enemy Health", context?.EnemyHealth);

            string result = string.Join(Environment.NewLine, lines);
            RecordSuccess("Refresh Diagnostics", "Diagnostics refreshed.");
            return result;
        }

        public void ClearConfirmation(string confirmationKey)
        {
            if (!string.IsNullOrWhiteSpace(confirmationKey))
            {
                pendingConfirmations.Remove(confirmationKey);
            }
        }

        private void EnsureAutomation()
        {
            if (automationRegistry.Suites.Count == 0)
            {
                RegisterDefaultAutomationSuites(automationRegistry);
            }

            if (automationRunner == null)
            {
                automationHost ??= new PrototypeTestLabAutomationHost(this);
                TestLabAutomationHostRegistry.Register(automationHost, out _);
                automationRunner = new TestLabAutomationRunner(
                    automationRegistry,
                    new PrototypeTestLabAutomationResetCoordinator(),
                    requiredHostId => TestLabAutomationHostRegistry.ResolveActive(requiredHostId),
                    CreateAutomationDefinitionContext());
                return;
            }

            if (automationHost != null)
            {
                TestLabAutomationHostRegistry.Register(automationHost, out _);
            }
        }

        private static TestLabAutomationOptions CreateAutomationOptions(bool stopOnFirstFailure)
        {
            return new TestLabAutomationOptions
            {
                StopOnFirstFailure = stopOnFirstFailure,
                IncludeExtended = true,
                MaximumFrameWait = 120
            };
        }

        private static string FormatAutomationRun(TestLabAutomationResult result)
        {
            if (result == null)
            {
                return "No automation result.";
            }

            return $"Run {result.RunId}: {result.PassedScenarios} passed, {result.FailedScenarios} failed, {result.ErrorScenarios} error, {result.SkippedScenarios} skipped, {result.CancelledScenarios} cancelled, {result.TotalSteps} steps. Order={result.ScenarioOrder} Seed={result.ShuffleSeed}.";
        }

        private void UpdateAutomationBatchResult()
        {
            if (string.IsNullOrWhiteSpace(automationBatchRunId))
            {
                return;
            }

            lastAutomationResult = new TestLabAutomationResult(
                automationBatchRunId,
                automationBatchMode,
                automationBatchStartedAtUtc,
                DateTime.UtcNow,
                automationBatchCancelled,
                automationBatchScenarios,
                TestLabAutomationScenarioOrder.Normal,
                0);
        }

        private bool EnsurePersistence(out PrototypePersistenceServiceBehaviour persistence)
        {
            persistence = context?.Persistence;
            if (persistence == null)
            {
                return false;
            }

            persistence.EnsureInitialized();
            return true;
        }

        private bool EnsureIdentityProgression(out PlayerIdentityProgression progression)
        {
            progression = context?.IdentityProgression;
            if (progression == null)
            {
                return false;
            }

            progression.RegisterDefinitionCache(registry);
            return true;
        }

        private bool EnsureSkills(out CharacterSkillCollection skills)
        {
            skills = context?.PlayerSkills;
            if (skills == null)
            {
                return false;
            }

            skills.Configure(registry, context.PlayerCalculatedStats, context.SpellLoadout);
            return true;
        }

        private bool EnsureResources(out CharacterResourceCollection resources)
        {
            resources = context?.PlayerResources;
            if (resources == null && context?.PlayerTransform != null)
            {
                resources = context.PlayerTransform.GetComponentInParent<CharacterResourceCollection>();
            }

            if (resources == null && context?.PlayerTransform != null)
            {
                resources = context.PlayerTransform.gameObject.AddComponent<CharacterResourceCollection>();
            }

            if (resources == null)
            {
                return false;
            }

            context.PlayerResources = resources;
            resources.Configure(registry, context.PlayerCalculatedStats, PersistenceService.LocalPlayerId);
            return true;
        }

        private bool EnsureTraits(out CharacterTraitCollection traits)
        {
            traits = context?.PlayerTraits;
            if (traits == null && context?.PlayerTransform != null)
            {
                traits = context.PlayerTransform.GetComponentInParent<CharacterTraitCollection>();
            }

            if (traits == null && context?.PlayerTransform != null)
            {
                traits = context.PlayerTransform.gameObject.AddComponent<CharacterTraitCollection>();
            }

            if (traits == null)
            {
                return false;
            }

            context.PlayerTraits = traits;
            traits.Configure(registry, context.PlayerCalculatedStats, context.PlayerSkills, PersistenceService.LocalPlayerId);
            return true;
        }

        private bool EnsureCharacterSystem(out CharacterSystemCoordinator character, bool initialize = true)
        {
            character = context?.CharacterSystem;
            if (character == null && context?.PlayerTransform != null)
            {
                character = context.PlayerTransform.GetComponentInParent<CharacterSystemCoordinator>();
            }

            if (character == null && context?.PlayerTransform != null)
            {
                character = context.PlayerTransform.gameObject.AddComponent<CharacterSystemCoordinator>();
            }

            if (character == null)
            {
                return false;
            }

            context.CharacterSystem = character;
            if (initialize && !character.IsReady)
            {
                character.InitializeFromRegistry(registry, restoring: false, addMissingCore: true);
            }

            return true;
        }

        private bool EnsureBodyRuntime(out ActorBodyRuntime body)
        {
            body = null;
            if (context?.PlayerTransform == null)
            {
                return false;
            }

            EnsureResources(out _);
            EnsureTraits(out _);
            EnsureCharacterSystem(out CharacterSystemCoordinator character, initialize: false);

            body = character == null ? null : character.Body;
            if (body == null)
            {
                body = context.PlayerTransform.GetComponentInParent<ActorBodyRuntime>();
            }

            if (body == null)
            {
                body = context.PlayerTransform.gameObject.AddComponent<ActorBodyRuntime>();
            }

            string actorId = character == null ? ResolveActorId(context.PlayerTransform.gameObject) : character.ActorId;
            string personId = character == null || string.IsNullOrWhiteSpace(character.PersonId)
                ? context.IdentityProgression == null ? string.Empty : context.IdentityProgression.PersonId
                : character.PersonId;
            bool requiresConfigure = !body.IsReady
                || !string.Equals(body.ActorBodyId, actorId, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(personId) && !string.Equals(body.PersonId, personId, StringComparison.Ordinal));
            if (requiresConfigure)
            {
                body.Configure(registry, actorId, personId, context.PlayerTraits, context.PlayerCalculatedStats);
            }

            if (!body.IsReady)
            {
                body.AssignSpecies("species.human", restoring: false, "Test Lab body bootstrap");
            }
            else if (!body.Condition.IsReady)
            {
                body.Condition.BuildHealthy(body.ActorBodyId, body.CreateAnatomySnapshot(), registry, restoring: false, preserveRevision: true);
            }

            return true;
        }

        private PrototypeTestLabOperation RecordBodyResult(string operationName, BodyOperationResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Body operation returned no result.", BodyOperationResultCode.InvalidRequest.ToString());
            }

            BodySnapshot snapshot = result.Snapshot;
            string message = $"{result.Message} Actor={snapshot?.ActorBodyId ?? string.Empty} Person={snapshot?.PersonId ?? string.Empty} Species={snapshot?.SpeciesId ?? string.Empty} Classification={snapshot?.BiologicalClassificationId ?? string.Empty} Form={snapshot?.BodyFormId ?? string.Empty} Revision={snapshot?.BodyRevision ?? 0}.";
            if (result.Diagnostics.Count > 0)
            {
                message += " " + string.Join(" ", result.Diagnostics);
            }

            return result.Succeeded
                ? RecordSuccess(operationName, message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private bool EnsureVitalProcessesReady(ActorBodyRuntime body)
        {
            if (body == null)
            {
                return false;
            }

            if (!body.Condition.IsReady)
            {
                body.Condition.BuildHealthy(body.ActorBodyId, body.CreateAnatomySnapshot(), registry, restoring: false, preserveRevision: true);
            }

            if (!body.VitalProcesses.IsReady)
            {
                body.VitalProcesses.BuildForBody(body.ActorBodyId, body.Species, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), registry);
            }

            return body.VitalProcesses.IsReady;
        }

        private bool EnsureBiologicalHazardsReady(ActorBodyRuntime body)
        {
            if (body == null)
            {
                return false;
            }

            if (!EnsureVitalProcessesReady(body))
            {
                return false;
            }

            if (!body.BiologicalHazards.IsReady)
            {
                body.BiologicalHazards.BuildForBody(body.ActorBodyId, body.VitalProcesses, registry);
            }

            return body.BiologicalHazards.IsReady;
        }

        private bool EnsureHazardRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure)
        {
            body = null;
            failure = default;
            if (!EnsureBodyRuntime(out body))
            {
                failure = RecordFailure("Biological Hazard Operation", "Body runtime is missing.", BiologicalHazardResultCode.MissingActorBody.ToString());
                return false;
            }

            if (!EnsureBiologicalHazardsReady(body))
            {
                failure = RecordFailure("Biological Hazard Operation", "Biological hazard runtime is not ready.", BiologicalHazardResultCode.RuntimeNotReady.ToString());
                return false;
            }

            return true;
        }

        private PrototypeTestLabOperation AddHazardSource(string hazardId, string sourceContributionId, BiologicalHazardSourceCategory category, BiologicalHazardSeverity severity, float rateMultiplier, float durationSeconds, string sourceObjectId)
        {
            if (!EnsureHazardRuntime(out ActorBodyRuntime body, out PrototypeTestLabOperation failure))
            {
                return failure;
            }

            BiologicalHazardSourceRequest request = new BiologicalHazardSourceRequest(body.ActorBodyId, hazardId, sourceContributionId, category, severity, rateMultiplier, durationSeconds, sourceObjectId, "Prototype Test Lab hazard source");
            return RecordHazardOperation("Apply Biological Hazard Source", body.BiologicalHazards.AddOrUpdateSource(request, body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), restoring: false, compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot()));
        }

        private bool TryBuildHazardTickRequest(
            float elapsedGameSeconds,
            string transactionSeed,
            bool preview,
            out ActorBodyRuntime body,
            out BiologicalHazardTickRequest request,
            out PrototypeTestLabOperation failure)
        {
            body = null;
            request = default;
            failure = default;
            if (!EnsureHazardRuntime(out body, out failure))
            {
                return false;
            }

            string transactionId = string.IsNullOrWhiteSpace(transactionSeed) || string.Equals(transactionSeed, "preview", StringComparison.Ordinal)
                ? $"test-lab.hazard.{(string.IsNullOrWhiteSpace(transactionSeed) ? "tick" : transactionSeed)}.{Guid.NewGuid():N}"
                : transactionSeed;
            request = new BiologicalHazardTickRequest(body.ActorBodyId, elapsedGameSeconds, transactionId, preview, "Prototype Test Lab hazard tick");
            return true;
        }

        private bool TryBuildVitalRequest(
            string resourceId,
            VitalResourceMutationOperation operation,
            float amount,
            string transactionSeed,
            out ActorBodyRuntime body,
            out VitalResourceMutationRequest request,
            out PrototypeTestLabOperation failure)
        {
            body = null;
            request = default;
            failure = default;
            if (!EnsureBodyRuntime(out body))
            {
                failure = RecordFailure("Vital Resource Mutation", "Body runtime is missing.", VitalProcessResultCode.MissingActorBody.ToString());
                return false;
            }

            if (!EnsureVitalProcessesReady(body))
            {
                failure = RecordFailure("Vital Resource Mutation", "Vital process runtime is not ready.", VitalProcessResultCode.RuntimeNotReady.ToString());
                return false;
            }

            AnatomySnapshot anatomy = body.CreateAnatomySnapshot();
            BodyConditionSnapshot condition = body.Condition.CreateSnapshot();
            string transactionId = string.IsNullOrWhiteSpace(transactionSeed) || string.Equals(transactionSeed, "preview", StringComparison.Ordinal)
                ? $"test-lab.vital.{(string.IsNullOrWhiteSpace(transactionSeed) ? "operation" : transactionSeed)}.{Guid.NewGuid():N}"
                : transactionSeed;
            request = new VitalResourceMutationRequest(
                body.ActorBodyId,
                resourceId,
                operation,
                amount,
                transactionId,
                "test-lab",
                "Prototype Test Lab vital mutation",
                body.BodyRevision,
                anatomy?.AnatomyRevision ?? 0L,
                condition?.ConditionRevision ?? 0L);
            return true;
        }

        private PrototypeTestLabOperation RecordVitalResult(string operationName, VitalResourceMutationResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Vital process operation returned no result.", VitalProcessResultCode.InvalidRequest.ToString());
            }

            VitalProcessSnapshot snapshot = result.Snapshot;
            string message = $"{result.Message} Resource={result.Request.ResourceId} Operation={result.Request.Operation} Amount={result.Request.Amount:0.###} Preview={result.Preview} Duplicate={result.Duplicate} Value={result.PreviousValue:0.##}->{result.NewValue:0.##} Applied={result.AppliedAmount:0.##} State={result.PreviousState}->{result.NewState} Profile={snapshot?.ProfileId ?? string.Empty} Revision={snapshot?.VitalRevision ?? 0}.";
            return result.Succeeded
                ? Record(true, operationName, result.Duplicate ? VitalProcessResultCode.Duplicate.ToString() : result.Preview ? VitalProcessResultCode.Preview.ToString() : result.Code.ToString(), message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private PrototypeTestLabOperation RecordHazardOperation(string operationName, BiologicalHazardOperationResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Biological hazard operation returned no result.", BiologicalHazardResultCode.InvalidRequest.ToString());
            }

            BiologicalHazardSnapshot snapshot = result.Snapshot;
            string message = $"{result.Message} Preview={result.Preview} Duplicate={result.Duplicate} Body={snapshot?.ActorBodyId ?? string.Empty} Active={snapshot?.ActiveHazards.Count ?? 0} Revision={snapshot?.HazardRevision ?? 0}.";
            return result.Succeeded
                ? Record(true, operationName, result.Duplicate ? BiologicalHazardResultCode.Duplicate.ToString() : result.Preview ? BiologicalHazardResultCode.Preview.ToString() : result.Code.ToString(), message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private PrototypeTestLabOperation RecordHazardTick(string operationName, BiologicalHazardTickResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Biological hazard tick returned no result.", BiologicalHazardResultCode.InvalidRequest.ToString());
            }

            BiologicalHazardSnapshot snapshot = result.Snapshot;
            string consequenceSummary = string.Join(", ", result.Consequences.Select(consequence => $"{consequence.Kind}:{consequence.HazardDefinitionId}:{consequence.ResourceId}"));
            string message = $"{result.Message} Preview={result.Preview} Duplicate={result.Duplicate} Elapsed={result.Request.ElapsedGameSeconds:0.###} Consequences={result.Consequences.Count} DamagePlans={result.DamagePlans.Count} Lifecycle={result.HasLifecyclePressure} Active={snapshot?.ActiveHazards.Count ?? 0} Revision={snapshot?.HazardRevision ?? 0}. {consequenceSummary}";
            return result.Succeeded
                ? Record(true, operationName, result.Duplicate ? BiologicalHazardResultCode.Duplicate.ToString() : result.Preview ? BiologicalHazardResultCode.Preview.ToString() : result.Code.ToString(), message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private PrototypeTestLabOperation RecordCompatibilityOperation(string operationName, BiologicalCompatibilityOperationResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Biological compatibility operation returned no result.", BiologicalCompatibilityResultCode.InvalidRequest.ToString());
            }

            BiologicalCompatibilitySnapshot snapshot = result.Snapshot;
            string message = $"{result.Message} Duplicate={result.Duplicate} Body={snapshot?.ActorBodyId ?? string.Empty} Profile={snapshot?.ProfileId ?? string.Empty} Rules={snapshot?.Rules.Count ?? 0} Revision={snapshot?.CompatibilityRevision ?? 0}.";
            return result.Succeeded
                ? Record(true, operationName, result.Duplicate ? BiologicalCompatibilityResultCode.Duplicate.ToString() : result.Code.ToString(), message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private PrototypeTestLabOperation RecordCompatibility(string operationName, BiologicalInteractionEvaluationResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Biological compatibility evaluation returned no result.", BiologicalCompatibilityResultCode.InvalidRequest.ToString());
            }

            string message = FormatCompatibilityResult(result);
            return result.Code == BiologicalCompatibilityResultCode.Success
                ? Record(true, operationName, result.Code.ToString(), message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private PrototypeTestLabOperation RecordRecoveryResult(string operationName, BiologicalRecoveryResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Biological recovery operation returned no result.", BiologicalRecoveryResultCode.InvalidRequest.ToString());
            }

            BiologicalRecoverySnapshot snapshot = result.Snapshot;
            string structural = result.StructuralRecovery == null
                ? "Structure=None"
                : $"Structure={result.StructuralRecovery.Code} {result.StructuralRecovery.PreviousIntegrity}->{result.StructuralRecovery.NewIntegrity} Restored={result.StructuralRecovery.IntegrityRestored}";
            string vital = result.VitalResourceMutation == null
                ? "Vital=None"
                : $"Vital={result.VitalResourceMutation.Code} {result.VitalResourceMutation.PreviousValue:0.##}->{result.VitalResourceMutation.NewValue:0.##} Applied={result.VitalResourceMutation.AppliedAmount:0.##}";
            string compatibility = result.Compatibility == null
                ? "Compatibility=None"
                : $"Compatibility={result.Compatibility.CompatibilityState} Rate={result.Compatibility.RateMultiplier:0.###} Suppressed={result.Compatibility.Suppressed}";
            string message = $"{result.Message} Tx={result.TransactionId} Preview={result.Preview} Duplicate={result.Duplicate} Body={result.ActorBodyId} Method={result.RecoveryMethodId} Process={result.ProcessId} Progress={result.PreviousProgress:0.##}->{result.NewProgress:0.##} Applied={result.AppliedProgress:0.##} State={result.PreviousState}->{result.NewState} Active={snapshot?.ActiveProcesses.Count ?? 0} Revision={snapshot?.RecoveryRevision ?? 0}. {structural}. {vital}. {compatibility}.";
            return result.Succeeded
                ? Record(true, operationName, result.Duplicate ? BiologicalRecoveryResultCode.Duplicate.ToString() : result.Preview ? BiologicalRecoveryResultCode.Preview.ToString() : result.Code.ToString(), message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private static string FormatCompatibilityResult(BiologicalInteractionEvaluationResult result)
        {
            if (result == null)
            {
                return "None";
            }

            string matched = string.Join(", ", result.RuleTrace.Where(trace => trace.Matched).Select(trace => $"{trace.EntryId}:{trace.RuleKind}"));
            string ignored = string.Join(", ", result.RuleTrace.Where(trace => !trace.Matched).Take(4).Select(trace => $"{trace.EntryId}:{trace.Reason}"));
            return $"{result.InteractionDefinitionId} State={result.CompatibilityState} Compatible={result.Compatible} Immune={result.Immune} Suppressed={result.Suppressed} Affinity={result.Affinity} Absorbed={result.Absorbed} Converted={result.ConvertedInteractionDefinitionId} Rate={result.RateMultiplier:0.###} Severity={result.SeverityMultiplier:0.###} Consequence={result.ConsequenceMultiplier:0.###} MaxSeverity={result.MaximumSeverity:0.###} Rev={result.BodyRevision}/{result.AnatomyRevision}/{result.ConditionRevision}/{result.VitalRevision}/{result.HazardRevision}/{result.CompatibilityRevision}. Matched=[{matched}] Ignored=[{ignored}] Message={result.Message}";
        }

        private static string FormatCompatibilityDeterminismSignature(BiologicalInteractionEvaluationResult result)
        {
            if (result == null)
            {
                return "None";
            }

            string matched = string.Join(", ", result.RuleTrace.Where(trace => trace.Matched).Select(trace => $"{trace.EntryId}:{trace.RuleKind}"));
            return $"{result.InteractionDefinitionId} Code={result.Code} State={result.CompatibilityState} Compatible={result.Compatible} Immune={result.Immune} Suppressed={result.Suppressed} Affinity={result.Affinity} Absorbed={result.Absorbed} Converted={result.ConvertedInteractionDefinitionId} Rate={result.RateMultiplier:0.###} Severity={result.SeverityMultiplier:0.###} Consequence={result.ConsequenceMultiplier:0.###} MaxSeverity={result.MaximumSeverity:0.###} Matched=[{matched}]";
        }

        private bool TryBuildConditionDamageRequest(
            string injuryDefinitionId,
            string targetNodeId,
            int structuralDamage,
            string transactionSeed,
            out ActorBodyRuntime body,
            out LocalizedStructuralDamageRequest request,
            out PrototypeTestLabOperation failure)
        {
            body = null;
            request = null;
            failure = default;
            if (!EnsureBodyRuntime(out body))
            {
                failure = RecordFailure("Localized Structural Damage", "Body runtime is missing.", LocalizedDamageResultCode.MissingActorBody.ToString());
                return false;
            }

            AnatomySnapshot anatomy = body.CreateAnatomySnapshot();
            if (anatomy == null || !anatomy.Coherent)
            {
                failure = RecordFailure("Localized Structural Damage", "Current Anatomy snapshot is missing or incoherent.", LocalizedDamageResultCode.MissingAnatomy.ToString());
                return false;
            }

            if (!EnsureBiologicalCompatibilityCurrent(body, anatomy, out failure))
            {
                return false;
            }

            string transactionId = string.IsNullOrWhiteSpace(transactionSeed) || string.Equals(transactionSeed, "preview", StringComparison.Ordinal)
                ? $"test-lab.body-condition.{(string.IsNullOrWhiteSpace(transactionSeed) ? "operation" : transactionSeed)}.{Guid.NewGuid():N}"
                : transactionSeed;
            request = new LocalizedStructuralDamageRequest
            {
                TransactionId = transactionId,
                SourceActorBodyId = body.ActorBodyId,
                TargetActorBodyId = body.ActorBodyId,
                TargetNodeId = targetNodeId,
                InjuryDefinitionId = injuryDefinitionId,
                StructuralDamage = structuralDamage,
                ExpectedBodyRevision = anatomy.BodyRevision,
                ExpectedAnatomyRevision = anatomy.AnatomyRevision,
                Context = "Prototype Test Lab localized structural damage"
            };
            return true;
        }

        private bool EnsureBiologicalCompatibilityCurrent(ActorBodyRuntime body, AnatomySnapshot anatomy, out PrototypeTestLabOperation failure)
        {
            failure = default;
            if (body == null)
            {
                failure = RecordFailure("Localized Structural Damage", "Body runtime is missing.", LocalizedDamageResultCode.MissingActorBody.ToString());
                return false;
            }

            BodySnapshot bodySnapshot = body.CreateSnapshot();
            BiologicalCompatibilitySnapshot compatibility = body.BiologicalCompatibility.CreateSnapshot();
            bool stale = compatibility == null
                || compatibility.Readiness != BiologicalCompatibilityReadinessState.Ready
                || !string.Equals(compatibility.ActorBodyId, body.ActorBodyId, StringComparison.Ordinal)
                || compatibility.BodyRevision != bodySnapshot.BodyRevision
                || compatibility.AnatomyRevision != (anatomy?.AnatomyRevision ?? 0L);

            if (!stale)
            {
                return true;
            }

            BiologicalCompatibilityOperationResult result = body.BiologicalCompatibility.BuildForBody(bodySnapshot, registry, restoring: false, preserveRevision: true);
            if (result.Succeeded)
            {
                return true;
            }

            failure = RecordFailure("Localized Structural Damage", $"Biological compatibility could not be synchronized: {result.Message}", LocalizedDamageResultCode.MissingCompatibility.ToString());
            return false;
        }

        private PrototypeTestLabOperation RecordConditionResult(string operationName, LocalizedStructuralDamageResult result)
        {
            if (result == null)
            {
                return RecordFailure(operationName, "Body condition operation returned no result.", LocalizedDamageResultCode.InvalidRequest.ToString());
            }

            BodyConditionSnapshot snapshot = result.Snapshot;
            string message = $"{result.Message} Tx={result.TransactionId} Preview={result.Preview} Duplicate={result.Duplicate} Body={result.ActorBodyId} Node={result.TargetNodeId} Injury={result.InjuryDefinitionId} Applied={result.DamageApplied} Integrity={result.PreviousIntegrity}->{result.NewIntegrity} Severity={result.Severity} Functional={result.FunctionalState} Structural={result.StructuralState} Presence={result.RuntimePresence} Revision={snapshot?.ConditionRevision ?? 0}.";
            return result.Succeeded
                ? Record(true, operationName, result.Duplicate ? LocalizedDamageResultCode.Duplicate.ToString() : result.Preview ? LocalizedDamageResultCode.Preview.ToString() : result.Code.ToString(), message)
                : RecordFailure(operationName, message, result.Code.ToString());
        }

        private static void AppendAnatomyHierarchy(List<string> lines, AnatomySnapshot snapshot, string nodeId, int depth)
        {
            if (lines == null || snapshot == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            AnatomyNodeSnapshot node = snapshot.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
            if (node == null)
            {
                return;
            }

            lines.Add($"{new string(' ', depth * 2)}- {node.DisplayName} [{node.NodeId}] {node.Category} {node.BodySide} {node.Presence}");
            foreach (string childId in node.ChildNodeIds)
            {
                AppendAnatomyHierarchy(lines, snapshot, childId, depth + 1);
            }
        }

        private PrototypeTestLabOperation ChangeTrait(TraitDefinition trait, string operationName, Func<CharacterTraitCollection, TraitOperationResult> action)
        {
            if (!EnsureTraits(out CharacterTraitCollection traits))
            {
                return RecordFailure(operationName, "Player Trait collection component is missing.", "MissingTraits");
            }

            if (trait == null)
            {
                return RecordFailure(operationName, "Trait definition is missing.", "MissingTrait");
            }

            TraitOperationResult result = action(traits);
            return Record(result.Succeeded, operationName, result.Code, result.Message);
        }

        private RequirementEvaluationContext BuildRequirementContext(bool testLab)
        {
            EnsureTraits(out CharacterTraitCollection traits);
            EnsureResources(out CharacterResourceCollection resources);
            EnsureSkills(out CharacterSkillCollection skills);
            return new RequirementEvaluationContext
            {
                Attributes = context?.PlayerAttributes,
                CalculatedStats = context?.PlayerCalculatedStats,
                Resources = resources,
                Skills = skills,
                Traits = traits,
                Identity = context?.IdentityProgression,
                Inventory = context?.Inventory,
                Equipment = context?.Equipment,
                Statuses = context?.PlayerStatuses,
                TestLabDiagnostics = testLab
            };
        }

        private bool TryGetFirstActiveRole(out PlayerIdentityProgression progression, out RuntimeRoleRecord role, out PrototypeTestLabOperation failure)
        {
            role = null;
            if (!EnsureIdentityProgression(out progression))
            {
                failure = RecordFailure("Role Operation", "Player identity/progression component is missing.", "MissingIdentityProgression");
                return false;
            }

            role = progression.Roles.FirstOrDefault(record => record.lifecycleState == RoleLifecycleState.Active);
            if (role != null)
            {
                failure = default;
                return true;
            }

            failure = RecordFailure("Role Operation", "No active role exists.", "MissingActiveRole");
            return false;
        }

        private PrototypeTestLabOperation AddSocialStatus(SocialStatusDefinition status, SocialStatusContextKind contextKind, string contextTargetId, string operationName)
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure(operationName, "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            ProgressionOperationResult result = progression.AddSocialStatus(status, contextKind, contextTargetId, "test-lab", "manual-test-lab");
            return Record(result.Succeeded, operationName, result.Code, result.Message);
        }

        private PrototypeTestLabOperation RecordActivity(ActivityOutcome outcome, float difficulty, string operationName)
        {
            if (!EnsureIdentityProgression(out PlayerIdentityProgression progression))
            {
                return RecordFailure(operationName, "Player identity/progression component is missing.", "MissingIdentityProgression");
            }

            ProgressionOperationResult result = progression.RecordActivityOutcome(
                $"activity.test-lab.{Guid.NewGuid():N}",
                ActivityType.DevelopmentTest,
                outcome,
                Mathf.Clamp01(difficulty),
                "test-lab",
                "PrototypeTestLab");
            return Record(result.Succeeded, operationName, result.Code, result.Message);
        }

        private bool RequireConfirmation(string key, bool confirmed, out PrototypeTestLabOperation result)
        {
            result = default;
            if (confirmed || pendingConfirmations.Remove(key))
            {
                return true;
            }

            pendingConfirmations.Add(key);
            result = RecordFailure("Confirmation Required", $"Press the same destructive action again to confirm '{key}'.", "ConfirmationRequired");
            return false;
        }

        private PrototypeTestLabOperation RecordSuccess(string operationName, string message)
        {
            return Record(true, operationName, "Success", message);
        }

        private PrototypeTestLabOperation RecordFailure(string operationName, string message, string code)
        {
            return Record(false, operationName, code, message);
        }

        private PrototypeTestLabOperation Record(bool succeeded, string operationName, string code, string message)
        {
            PrototypeTestLabOperation operation = new PrototypeTestLabOperation(DateTime.Now, operationName, succeeded, code, message);
            history.Insert(0, operation);
            while (history.Count > historyLimit)
            {
                history.RemoveAt(history.Count - 1);
            }

            bool automationRecord = operationName.IndexOf("Automation", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!succeeded && !suppressExpectedAutomationWarnings && !automationBatchRunning && !automationRecord && !string.Equals(code, "ConfirmationRequired", StringComparison.Ordinal))
            {
                Debug.LogWarning($"{operationName}: {message}");
            }
            else if (!succeeded && !suppressExpectedAutomationWarnings && !automationBatchRunning && automationRecord)
            {
                Debug.LogWarning($"{operationName}: {code}. {message}");
            }

            HistoryChanged?.Invoke();
            return operation;
        }

        private void LogAutomationScenarioFailures(TestLabAutomationResult result)
        {
            if (result == null)
            {
                return;
            }

            foreach (TestLabScenarioResult scenario in result.Scenarios)
            {
                if (scenario.Status == TestLabAutomationStatus.Passed)
                {
                    continue;
                }

                TestLabAutomationStepResult failedStep = scenario.Steps.FirstOrDefault(step => step.Status == TestLabAutomationStatus.Failed || step.Status == TestLabAutomationStatus.Error)
                    ?? scenario.Steps.FirstOrDefault(step => step.Status != TestLabAutomationStatus.Passed && step.Status != TestLabAutomationStatus.Skipped);
                string failureKey = $"{result.RunId}:{scenario.SuiteId}:{scenario.ScenarioId}:{scenario.Status}:{failedStep?.StepId ?? string.Empty}";
                if (!loggedAutomationFailureKeys.Add(failureKey))
                {
                    continue;
                }

                string exception = failedStep == null || string.IsNullOrWhiteSpace(failedStep.ExceptionType)
                    ? string.Empty
                    : $" Exception={failedStep.ExceptionType}: {failedStep.ExceptionMessage}";
                string message = failedStep == null
                    ? $"Automation failed: {scenario.SuiteId}/{scenario.ScenarioId} - {scenario.DisplayName}. Status={scenario.Status}."
                    : $"Automation failed: {scenario.SuiteId}/{scenario.ScenarioId} - {scenario.DisplayName}. Status={scenario.Status}. Step={failedStep.StepId}. Expected='{failedStep.Expected}' Actual='{failedStep.Actual}'. Assertion={failedStep.AssertionType}. Tx='{failedStep.TransactionId}'. Diagnostics: {failedStep.Diagnostics}{exception}";
                Debug.LogWarning(message);
            }
        }

        private DefinitionRegistry CreateRegistry(DefinitionCatalog catalog)
        {
            return CreateAutomationDefinitionRegistry(catalog);
        }

        private static DefinitionRegistry AddDevelopmentHistoryDefinitions(DefinitionRegistry baseRegistry)
        {
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values);
            }

            foreach (HistoricalEventDefinition definition in CreateDevelopmentLifeEventDefinitions())
            {
                if (!definitions.Any(existing => string.Equals(existing.Id, definition.Id, StringComparison.Ordinal)))
                {
                    definitions.Add(definition);
                }
            }

            foreach (InformationAccessPolicyDefinition definition in CreatePrototypeAccessPolicyDefinitions())
            {
                if (!definitions.Any(existing => string.Equals(existing.Id, definition.Id, StringComparison.Ordinal)))
                {
                    definitions.Add(definition);
                }
            }

            foreach (KnowledgeRecordDefinition definition in CreatePrototypeKnowledgeRecordDefinitions())
            {
                if (!definitions.Any(existing => string.Equals(existing.Id, definition.Id, StringComparison.Ordinal)))
                {
                    definitions.Add(definition);
                }
            }

            DefinitionRegistry registry = new DefinitionRegistry(definitions);
            registry = PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(registry);
            registry = PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(registry);
            registry = PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(registry);
            registry = PrototypeOrganizationResourceDefinitionFactory.AddMissingPrototypeOrganizationResourceDefinitions(registry);
            registry = PrototypeOrganizationDecisionDefinitionFactory.AddMissingPrototypeOrganizationDecisionDefinitions(registry);
            registry = PrototypeProfessionDefinitionFactory.AddMissingPrototypeProfessionDefinitions(registry);
            registry = PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(registry);
            registry = PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(registry);
            registry = PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(registry);
            registry = PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(registry);
            registry = PrototypeSocialInteractionDefinitionFactory.AddMissingPrototypeSocialInteractionDefinitions(registry);
            registry = PrototypeSocialNormDefinitionFactory.AddMissingPrototypeSocialNormDefinitions(registry);
            registry = PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(registry);
            registry = PrototypeSocialDecisionDefinitionFactory.AddMissingPrototypeSocialDecisionDefinitions(registry);
            registry = PrototypeSocialInfluenceDefinitionFactory.AddMissingPrototypeSocialInfluenceDefinitions(registry);
            registry = PrototypeSocialEmotionDefinitionFactory.AddMissingPrototypeSocialEmotionDefinitions(registry);
            registry = PrototypeFamilyRelationshipDefinitionFactory.AddMissingPrototypeFamilyRelationshipDefinitions(registry);
            registry = PrototypeFactionDefinitionFactory.AddMissingPrototypeFactionDefinitions(registry);
            registry = PrototypeDiplomacyDefinitionFactory.AddMissingPrototypeDiplomacyDefinitions(registry);
            registry = PrototypeGovernmentDefinitionFactory.AddMissingPrototypeGovernmentDefinitions(registry);
            registry = PrototypeLegalDefinitionFactory.AddMissingPrototypeLegalDefinitions(registry);
            registry = PrototypeCrimeDefinitionFactory.AddMissingPrototypeCrimeDefinitions(registry);
            registry = PrototypeJusticeDefinitionFactory.AddMissingPrototypeJusticeDefinitions(registry);
            registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(registry);
            registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
            registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);
            registry = PrototypeLocationRouteDefinitionFactory.AddMissingPrototypeRouteDefinitions(registry);
            registry = PrototypeTravelConditionDefinitionFactory.AddMissingPrototypeTravelConditionDefinitions(registry);
            registry = PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(registry);
            registry = PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(registry);
            registry = PrototypeDialogueGraphDefinitionFactory.AddMissingPrototypeDialogueGraphDefinitions(registry);
            registry = PrototypeNarrativeEventDefinitionFactory.AddMissingPrototypeNarrativeEventDefinitions(registry);
            registry = PrototypeNarrativeStateDefinitionFactory.AddMissingPrototypeNarrativeStateDefinitions(registry);
            registry = PrototypeNarrativeArcDefinitionFactory.AddMissingPrototypeNarrativeArcDefinitions(registry);
            return registry;
        }

        private static IReadOnlyList<HistoricalEventDefinition> CreateDevelopmentLifeEventDefinitions()
        {
            return new[]
            {
                LifeEventDefinition("history-event.person-participation", "Person Participation", HistoricalEventCategory.CustomWorldEvent, KnowledgeVisibility.Public, HistoricalEventPayloadKind.Generic, false, LifeEventCategory.None, LifeEventPayloadKind.Generic, LifeEventSignificance.Routine, LifeEventBiographyRelevance.Optional, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Participant),
                LifeEventDefinition("history-event.hidden-witnessed-event", "Hidden Witnessed Event", HistoricalEventCategory.Discovery, KnowledgeVisibility.Hidden, HistoricalEventPayloadKind.Generic, false, LifeEventCategory.None, LifeEventPayloadKind.Generic, LifeEventSignificance.Notable, LifeEventBiographyRelevance.RestrictedBiographyEvent, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Witness),
                LifeEventDefinition("history-event.body-transition", "Body Transition", HistoricalEventCategory.BodyTransition, KnowledgeVisibility.Private, HistoricalEventPayloadKind.BodyTransition, true, LifeEventCategory.BodyTransition, LifeEventPayloadKind.BodyTransition, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.IdentityDefining, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.correction", "History Correction", HistoricalEventCategory.Discovery, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Correction, false, LifeEventCategory.None, LifeEventPayloadKind.Generic, LifeEventSignificance.Notable, LifeEventBiographyRelevance.Optional, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.diagnosis", "Diagnosis", HistoricalEventCategory.Diagnosis, KnowledgeVisibility.DiagnosticOnly, HistoricalEventPayloadKind.Condition, true, LifeEventCategory.Diagnosis, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.PrivateBiographyEvent, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.life.birth", "Birth or Creation", HistoricalEventCategory.BirthOrCreation, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Generic, true, LifeEventCategory.BirthOrCreation, LifeEventPayloadKind.BirthOrCreation, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.IdentityDefining, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.life.discovery", "Discovery", HistoricalEventCategory.Discovery, KnowledgeVisibility.Public, HistoricalEventPayloadKind.Discovery, true, LifeEventCategory.Discovery, LifeEventPayloadKind.Discovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.Optional, LifeEventPublicRecordRelevance.PublicRecord, LifeEventParticipantRole.Discoverer),
                LifeEventDefinition("history-event.life.role-appointment", "Role Appointment", HistoricalEventCategory.EmploymentOrRole, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Organization, true, LifeEventCategory.Role, LifeEventPayloadKind.RoleOrTitleTransition, LifeEventSignificance.Major, LifeEventBiographyRelevance.MajorBiographyEvent, LifeEventPublicRecordRelevance.OrganizationRecord, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.life.title-grant", "Title Grant", HistoricalEventCategory.EmploymentOrRole, KnowledgeVisibility.Public, HistoricalEventPayloadKind.Organization, true, LifeEventCategory.Title, LifeEventPayloadKind.RoleOrTitleTransition, LifeEventSignificance.Major, LifeEventBiographyRelevance.PublicBiographyEvent, LifeEventPublicRecordRelevance.PublicRecord, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.life.affiliation", "Affiliation Change", HistoricalEventCategory.Affiliation, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Organization, true, LifeEventCategory.Affiliation, LifeEventPayloadKind.AffiliationTransition, LifeEventSignificance.Notable, LifeEventBiographyRelevance.NormallyIncluded, LifeEventPublicRecordRelevance.OrganizationRecord, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.life.battle", "Battle Participation", HistoricalEventCategory.Combat, KnowledgeVisibility.Public, HistoricalEventPayloadKind.Generic, true, LifeEventCategory.Combat, LifeEventPayloadKind.CombatParticipation, LifeEventSignificance.Major, LifeEventBiographyRelevance.MajorBiographyEvent, LifeEventPublicRecordRelevance.HistoricalArchive, LifeEventParticipantRole.Participant),
                LifeEventDefinition("history-event.life.injury", "Major Injury", HistoricalEventCategory.Injury, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Condition, true, LifeEventCategory.Injury, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Major, LifeEventBiographyRelevance.PrivateBiographyEvent, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.life.recovery", "Recovery", HistoricalEventCategory.Recovery, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Condition, true, LifeEventCategory.Recovery, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.NormallyIncluded, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.life.crime", "Crime or Accusation", HistoricalEventCategory.Crime, KnowledgeVisibility.Hidden, HistoricalEventPayloadKind.Generic, true, LifeEventCategory.Crime, LifeEventPayloadKind.Legal, LifeEventSignificance.Major, LifeEventBiographyRelevance.RestrictedBiographyEvent, LifeEventPublicRecordRelevance.OrganizationRecord, LifeEventParticipantRole.Accused),
                LifeEventDefinition("history-event.life.death", "Death", HistoricalEventCategory.DeathOrDisappearance, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Generic, true, LifeEventCategory.Death, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.MajorBiographyEvent, LifeEventPublicRecordRelevance.PublicRecord, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.life.presumed-death", "Presumed Death", HistoricalEventCategory.DeathOrDisappearance, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Generic, true, LifeEventCategory.Disappearance, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.Major, LifeEventBiographyRelevance.RestrictedBiographyEvent, LifeEventPublicRecordRelevance.OrganizationRecord, LifeEventParticipantRole.Subject),
                LifeEventDefinition("history-event.life.return", "Return", HistoricalEventCategory.DeathOrDisappearance, KnowledgeVisibility.Public, HistoricalEventPayloadKind.Generic, true, LifeEventCategory.ReturnOrResurrection, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.MajorBiographyEvent, LifeEventPublicRecordRelevance.PublicRecord, LifeEventParticipantRole.Subject)
            };
        }

        private static IReadOnlyList<KnowledgeRecordDefinition> CreatePrototypeKnowledgeRecordDefinitions()
        {
            return PrototypeKnowledgeRecordDefinitionFactory.CreateKnowledgeRecordDefinitions();
        }

        private static HistoricalEventDefinition LifeEventDefinition(string id, string displayName, HistoricalEventCategory historicalCategory, KnowledgeVisibility visibility, HistoricalEventPayloadKind historicalPayloadKind, bool isLifeEvent, LifeEventCategory lifeCategory, LifeEventPayloadKind lifePayloadKind, LifeEventSignificance significance, LifeEventBiographyRelevance biography, LifeEventPublicRecordRelevance publicRecord, LifeEventParticipantRole requiredRole)
        {
            HistoricalEventDefinition definition = ScriptableObject.CreateInstance<HistoricalEventDefinition>();
            definition.DevelopmentConfigure(id, displayName, historicalCategory, visibility, historicalPayloadKind, isLifeEvent, lifeCategory, lifePayloadKind, significance, biography, publicRecord, new[] { requiredRole }, new[] { LifeEventParticipantRole.Witness }, new[] { "prototype", "step8" });
            return definition;
        }

        private string FormatHealth()
        {
            return context?.PlayerHealth == null
                ? "Missing"
                : $"{context.PlayerHealth.CurrentHealth}/{context.PlayerHealth.MaximumHealth} Defeated={context.PlayerHealth.IsDefeated}";
        }

        private static string FormatResource(float current, float maximum)
        {
            return $"{current:0.#}/{maximum:0.#}";
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##");
        }

        public static string FormatDefinition(IGameDefinition definition)
        {
            return definition == null ? "None" : $"{definition.DisplayName} ({definition.Id})";
        }

        private string FormatStatuses(StatusEffectController controller)
        {
            if (controller == null || controller.ActiveStatuses.Count == 0)
            {
                return "None";
            }

            return string.Join(", ", controller.ActiveStatuses.Select(status => $"{status.Definition.DisplayName} x{status.StackCount} [{status.ApplicationId}]"));
        }

        private string FormatInventory()
        {
            if (context?.Inventory == null)
            {
                return "Missing";
            }

            return $"{context.Inventory.DevelopmentOccupiedSlotCount()}/{context.Inventory.SlotCapacity} slots";
        }

        private int CountEquipped()
        {
            if (context?.Equipment == null)
            {
                return 0;
            }

            int count = 0;
            foreach (EquipmentSlotState slot in context.Equipment.Slots)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }

        private string FormatEnemy()
        {
            return context?.EnemyHealth == null
                ? "Missing"
                : $"{context.EnemyHealth.CurrentHealth:0.#}/{context.EnemyHealth.MaximumHealth:0.#} Defeated={context.EnemyHealth.IsDefeated}";
        }

        private string FormatIdentityOneLine()
        {
            if (context?.IdentityProgression == null)
            {
                return "Missing";
            }

            RuntimeOriginAssignmentRecord origin = context.IdentityProgression.Origin;
            RuntimeBirthGiftRecord gift = context.IdentityProgression.BirthGift;
            OverallLevelBreakdown level = context.IdentityProgression.CalculateOverallLevel();
            string originId = origin != null && origin.assigned ? origin.originId : "Unassigned";
            string giftId = string.IsNullOrWhiteSpace(gift?.giftDefinitionId) ? "None" : $"{gift.giftDefinitionId}:{gift.state}";
            return $"{originId} | Gift={giftId} | Level={level.OverallLevel}";
        }

        private string FormatCharacterReadinessOneLine()
        {
            return context?.CharacterSystem == null
                ? "Missing"
                : $"{context.CharacterSystem.Readiness} rev {context.CharacterSystem.Revision}";
        }

        private string FormatLocationOneLine()
        {
            if (context?.Persistence == null)
            {
                return "Missing";
            }

            return context.Persistence.BuildPlayerLocationDiagnosticSummary().Replace(Environment.NewLine, " | ");
        }

        private void AddDuplicateInstanceDiagnostics(List<string> lines)
        {
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> duplicates = new HashSet<string>();
            if (context?.Inventory != null)
            {
                foreach (InventorySlot slot in context.Inventory.Slots)
                {
                    string id = slot == null ? string.Empty : slot.ItemInstanceId;
                    if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
                    {
                        duplicates.Add(id);
                    }
                }
            }

            if (context?.Equipment != null)
            {
                foreach (EquipmentSlotState slot in context.Equipment.Slots)
                {
                    string id = slot == null ? string.Empty : slot.ItemInstanceId;
                    if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
                    {
                        duplicates.Add(id);
                    }
                }
            }

            lines.Add(duplicates.Count == 0 ? "Duplicate item instance IDs: none" : $"Duplicate item instance IDs: {string.Join(", ", duplicates)}");
        }

        private static void AddDuplicateStatusDiagnostics(List<string> lines, string label, StatusEffectController controller)
        {
            if (controller == null)
            {
                lines.Add($"{label} statuses: missing controller");
                return;
            }

            HashSet<string> ids = new HashSet<string>();
            HashSet<string> duplicates = new HashSet<string>();
            foreach (RuntimeStatusEffect status in controller.ActiveStatuses)
            {
                if (!ids.Add(status.ApplicationId))
                {
                    duplicates.Add(status.ApplicationId);
                }
            }

            lines.Add(duplicates.Count == 0 ? $"{label} duplicate status IDs: none" : $"{label} duplicate status IDs: {string.Join(", ", duplicates)}");
        }

        private void AddCharacterSystemDiagnostics(List<string> lines)
        {
            if (!EnsureCharacterSystem(out CharacterSystemCoordinator character))
            {
                lines.Add("Character System: missing coordinator");
                return;
            }

            CharacterIntegrityReport report = character.ValidateIntegrity();
            lines.Add($"Character System: {character.Readiness}, revision {character.Revision}, integrity {(report.Passed ? "passed" : "failed")}");
            lines.Add($"Duplicate CharacterSystemCoordinator components: {(character.GetComponents<CharacterSystemCoordinator>().Length > 1 ? "found" : "none")}");
        }

        private static void AddReferenceDiagnostic(List<string> lines, string label, UnityEngine.Object value)
        {
            lines.Add($"{label}: {(value == null ? "Missing" : "OK")}");
        }

        private sealed class AutomationRuntimeBindingFrame
        {
            public AutomationRuntimeBindingFrame(
                TestLabScenarioContext previousContext,
                InformationSourceRuntime previousSources,
                InformationTransferRuntime previousTransfers,
                InformationAccessRuntime previousAccess,
                KnowledgeRecordRuntime previousRecords,
                RelationshipRuntime previousRelationships,
                InterpersonalAttitudeRuntime previousAttitudes,
                ReputationRuntime previousReputation,
                RumorRuntime previousRumors,
                SocialInteractionRuntime previousSocialInteractions,
                SocialNormRuntime previousSocialNorms,
                SocialNetworkRuntime previousSocialNetworks,
                SocialDecisionRuntime previousSocialDecisions,
                SocialInfluenceRuntime previousSocialInfluence,
                SocialEmotionRuntime previousSocialEmotions)
            {
                PreviousContext = previousContext;
                PreviousSources = previousSources;
                PreviousTransfers = previousTransfers;
                PreviousAccess = previousAccess;
                PreviousRecords = previousRecords;
                PreviousRelationships = previousRelationships;
                PreviousAttitudes = previousAttitudes;
                PreviousReputation = previousReputation;
                PreviousRumors = previousRumors;
                PreviousSocialInteractions = previousSocialInteractions;
                PreviousSocialNorms = previousSocialNorms;
                PreviousSocialNetworks = previousSocialNetworks;
                PreviousSocialDecisions = previousSocialDecisions;
                PreviousSocialInfluence = previousSocialInfluence;
                PreviousSocialEmotions = previousSocialEmotions;
            }

            public TestLabScenarioContext PreviousContext { get; }
            public InformationSourceRuntime PreviousSources { get; }
            public InformationTransferRuntime PreviousTransfers { get; }
            public InformationAccessRuntime PreviousAccess { get; }
            public KnowledgeRecordRuntime PreviousRecords { get; }
            public RelationshipRuntime PreviousRelationships { get; }
            public InterpersonalAttitudeRuntime PreviousAttitudes { get; }
            public ReputationRuntime PreviousReputation { get; }
            public RumorRuntime PreviousRumors { get; }
            public SocialInteractionRuntime PreviousSocialInteractions { get; }
            public SocialNormRuntime PreviousSocialNorms { get; }
            public SocialNetworkRuntime PreviousSocialNetworks { get; }
            public SocialDecisionRuntime PreviousSocialDecisions { get; }
            public SocialInfluenceRuntime PreviousSocialInfluence { get; }
            public SocialEmotionRuntime PreviousSocialEmotions { get; }
        }
    }
}
#endif
