#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(5, "Character", 500)]
    public static class PrototypeStep5AutomationSuites
    {
        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            TryRegister(registry, BuildIdentityProgressionSuite());
            TryRegister(registry, BuildAttributesCalculatedStatsSuite());
            TryRegister(registry, BuildSkillsProgressionSuite());
            TryRegister(registry, BuildCurrentResourcesSuite());
            TryRegister(registry, BuildTraitsRequirementsSuite());
            TryRegister(registry, BuildCharacterIntegrationSuite());
        }

        private static ITestLabAutomationSuite BuildIdentityProgressionSuite()
        {
            return Suite("feature.5.1.identity-origin-progression", "Feature 5.1 Identity and Origin Progression", "5.1", 510,
                Required("PlayerIdentityProgression", "OriginDefinition", "BirthGiftDefinition"),
                Scenario("identity-origin-and-birth-gift", "Identity, origin, and birth gift flow", 10,
                    Step("reset", "Reset identity progression", context => Operation(context.Prototype().ResetIdentityProgression(true), context, "step5-reset-identity")),
                    Step("validate", "Validate identity", context => Operation(context.Prototype().ValidateIdentityProgression(), context, "step5-validate-identity")),
                    Step("origin", "Generate origin", context => Operation(context.Prototype().GenerateOrigin(5101), context, "step5-origin")),
                    Step("origin-once", "Reject duplicate origin", context => Operation(context.Prototype().ProveOriginAssignmentIsOnceOnly(), context, "step5-origin-once")),
                    Step("birth-progress", "Advance birth gift progress", context => Operation(context.Prototype().AdvanceBirthGiftProgress(30f), context, "step5-birth-progress")),
                    Step("birth-awaken", "Force birth gift awakening", context => Operation(context.Prototype().ForceBirthGiftAwakening(), context, "step5-birth-awaken"))),
                Scenario("roles-status-and-currency", "Roles, social status, and currency flow", 20,
                    Step("reset", "Reset identity progression", context => Operation(context.Prototype().ResetIdentityProgression(true), context, "step5-role-reset")),
                    Step("role", "Add role", context => Operation(context.Prototype().AddRole(First<RoleDefinition>(context), acceptConflicts: true), context, "step5-add-role")),
                    Step("suspend-role", "Suspend first active role", context => Operation(context.Prototype().SuspendFirstActiveRole(), context, "step5-suspend-role")),
                    Step("global-status", "Add global social status", context => Operation(context.Prototype().AddGlobalSocialStatus(First<SocialStatusDefinition>(context)), context, "step5-global-status")),
                    Step("place-status", "Add place social status", context => Operation(context.Prototype().AddPlaceSocialStatus(First<SocialStatusDefinition>(context), First<PlaceDefinition>(context)), context, "step5-place-status")),
                    Step("resolve-status", "Resolve active social status", context => Operation(context.Prototype().ResolveFirstActiveSocialStatus(), context, "step5-resolve-status")),
                    Step("currency-add", "Add currency", context => Operation(context.Prototype().AddCurrency(First<CurrencyDefinition>(context), 25), context, "step5-currency-add")),
                    Step("currency-spend", "Spend currency", context => Operation(context.Prototype().SpendCurrency(First<CurrencyDefinition>(context), 5), context, "step5-currency-spend"))),
                Scenario("overall-activity-progression", "Overall activity progression flow", 30,
                    Step("success", "Record successful activity", context => Operation(context.Prototype().RecordSuccessfulActivity(2f), context, "step5-activity-success")),
                    Step("failed", "Record failed activity", context => Operation(context.Prototype().RecordFailedActivity(1f), context, "step5-activity-failed")),
                    Step("participation", "Record participation", context => Operation(context.Prototype().RecordParticipation(), context, "step5-participation"))));
        }

        private static ITestLabAutomationSuite BuildAttributesCalculatedStatsSuite()
        {
            return Suite("feature.5.2-5.4a.attributes-calculated-stats", "Feature 5.2/5.4a Attributes and Calculated Stats", "5.2/5.4a", 520,
                Required("CharacterAttributes", "CalculatedStatCollection"),
                Scenario("attributes-and-calculated-stats", "Attributes and calculated stats flow", 10,
                    Step("clear", "Clear Feature 5.2 contributions", context => Operation(context.Prototype().ClearFeature52Contributions(), context, "step5-clear-attributes")),
                    Step("strength", "Add strength training", context => Operation(context.Prototype().AddStrengthTraining(), context, "step5-strength")),
                    Step("balanced", "Add balanced training", context => Operation(context.Prototype().AddBalancedAttributeTraining(), context, "step5-balanced")),
                    Step("flat", "Add physical power flat modifier", context => Operation(context.Prototype().AddPhysicalPowerFlat(), context, "step5-power-flat")),
                    Step("penalty", "Add physical defense penalty", context => Operation(context.Prototype().AddPhysicalDefensePenalty(), context, "step5-defense-penalty")),
                    Step("invalid", "Reject invalid attribute growth", context => Operation(context.Prototype().AttemptInvalidAttributeGrowth(), context, "step5-invalid-growth")),
                    Step("recalculate", "Recalculate stats", context => Operation(context.Prototype().RecalculateFeature52Stats(), context, "step5-recalculate"))));
        }

        private static ITestLabAutomationSuite BuildSkillsProgressionSuite()
        {
            return Suite("feature.5.3.skills-progression", "Feature 5.3 Skills and Progression", "5.3", 530,
                Required("CharacterSkillCollection", "SkillDefinition"),
                Scenario("skills-learning-and-effects", "Skills, learning, and effects flow", 10,
                    Step("clear", "Clear skill development state", context => Operation(context.Prototype().ClearSkillDevelopmentState(true), context, "step5-clear-skills")),
                    Step("grant", "Grant skill", context => Operation(context.Prototype().GrantSkill(First<SkillDefinition>(context), SkillGrade.E), context, "step5-grant-skill")),
                    Step("action", "Simulate skill action", context => Operation(context.Prototype().SimulateSkillAction(First<SkillDefinition>(context), executed: true, succeeded: true), context, "step5-skill-action")),
                    Step("many", "Simulate many skill actions", context => Operation(context.Prototype().SimulateManySkillActions(First<SkillDefinition>(context), 3), context, "step5-skill-many")),
                    Step("duplicate", "Duplicate skill action is idempotent", context => Operation(context.Prototype().TestDuplicateSkillAction(First<SkillDefinition>(context)), context, "step5-skill-duplicate")),
                    Step("xp", "Award skill XP", context => Operation(context.Prototype().AwardSkillXp(First<SkillDefinition>(context), 25), context, "step5-skill-xp")),
                    Step("rebuild", "Rebuild skill effects", context => Operation(context.Prototype().RebuildSkillEffects(), context, "step5-skill-rebuild"))));
        }

        private static ITestLabAutomationSuite BuildCurrentResourcesSuite()
        {
            return Suite("feature.5.4b.current-resources", "Feature 5.4b Current Resources", "5.4b", 540,
                Required("CharacterResourceCollection", "ResourceDefinition"),
                Scenario("resources-runtime", "Current resources runtime flow", 10,
                    Step("reconcile", "Reconcile resources", context => Operation(context.Prototype().ReconcileResources(), context, "step5-reconcile-resources")),
                    Step("mana", "Drain mana", context => Operation(context.Prototype().DrainMana(10f), context, "step5-drain-mana")),
                    Step("stamina", "Drain stamina", context => Operation(context.Prototype().DrainStamina(10f), context, "step5-drain-stamina")),
                    Step("duplicate", "Prove duplicate resource event", context => Operation(context.Prototype().ProveResourceDuplicateEvent(), context, "step5-resource-duplicate")),
                    Step("regen", "Tick regeneration", context => Operation(context.Prototype().TickResourceRegeneration(), context, "step5-resource-regen")),
                    Step("snapshot", "Snapshot resources", context => Operation(context.Prototype().SnapshotResourcesForPersistence(), context, "step5-resource-snapshot"))));
        }

        private static ITestLabAutomationSuite BuildTraitsRequirementsSuite()
        {
            return Suite("feature.5.5.traits-requirements", "Feature 5.5 Traits and Requirements", "5.5", 550,
                Required("CharacterTraitCollection", "RequirementSetDefinition", "CapabilityDefinition"),
                Scenario("traits-requirements-and-capabilities", "Traits, requirements, and capabilities flow", 10,
                    Step("grant", "Grant trait", context => Operation(context.Prototype().GrantTrait(First<TraitDefinition>(context), TraitLifecycleState.Active, TraitDiscoveryState.Discovered), context, "step5-grant-trait")),
                    Step("second-source", "Grant trait second source", context => Operation(context.Prototype().GrantTraitSecondSource(First<TraitDefinition>(context)), context, "step5-trait-second-source")),
                    Step("suppress", "Suppress trait", context => Operation(context.Prototype().SuppressTrait(First<TraitDefinition>(context)), context, "step5-suppress-trait")),
                    Step("unsuppress", "Unsuppress trait", context => Operation(context.Prototype().UnsuppressTrait(First<TraitDefinition>(context)), context, "step5-unsuppress-trait")),
                    Step("suspect", "Set trait suspected", context => Operation(context.Prototype().SetTraitSuspected(First<TraitDefinition>(context)), context, "step5-trait-suspected")),
                    Step("discover", "Set trait discovered", context => Operation(context.Prototype().SetTraitDiscovered(First<TraitDefinition>(context)), context, "step5-trait-discovered")),
                    Step("rebuild", "Rebuild trait effects", context => Operation(context.Prototype().RebuildTraitEffects(), context, "step5-trait-rebuild")),
                    Step("snapshot", "Snapshot traits", context => Operation(context.Prototype().SnapshotTraitsForPersistence(), context, "step5-trait-snapshot")),
                    Step("requirement", "Evaluate requirement set", context => ObserveRequirement(context.Prototype().EvaluateRequirement(First<RequirementSetDefinition>(context)), context, "step5-requirement"))));
        }

        private static ITestLabAutomationSuite BuildCharacterIntegrationSuite()
        {
            return Suite("feature.5.6.character-integration", "Feature 5.6 Character System Integration", "5.6", 560,
                Required("CharacterSystemCoordinator", "CharacterQueryService"),
                Scenario("character-system-integrity", "Character system initialization and integrity", 10,
                    Step("initialize", "Initialize character system", context => Operation(context.Prototype().InitializeCharacterSystem(), context, "step5-initialize")),
                    Step("rebuild", "Rebuild character system", context => Operation(context.Prototype().RebuildCharacterSystem(), context, "step5-rebuild")),
                    Step("validate", "Validate character system", context => Operation(context.Prototype().ValidateCharacterSystemIntegrity(), context, "step5-validate")),
                    Step("snapshot", "Snapshot character system", context => Operation(context.Prototype().SnapshotCharacterSystem(), context, "step5-snapshot"))));
        }

        private static ITestLabAutomationSuite Suite(string suiteId, string displayName, string feature, int order, IReadOnlyList<string> required, params ITestLabAutomationScenario[] scenarios)
        {
            return new TestLabAutomationSuite(
                suiteId,
                displayName,
                feature,
                $"{displayName} runtime integration scenarios.",
                order,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: required,
                scenarios: scenarios);
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 30 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 30,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.SharedRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Character,
                requiredHostId: PrototypeTestLabAutomationHost.DefaultHostId,
                requiredHostFeatures: TestLabHostFeature.SharedRuntime | TestLabHostFeature.SceneReset | TestLabHostFeature.FixtureFingerprinting | TestLabHostFeature.AutomatedExecution);
        }

        private static ITestLabScenarioStep Step(string stepId, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(stepId, displayName, action);
        }

        private static IReadOnlyList<string> Required(params string[] services)
        {
            return services.ToArray();
        }

        private static T First<T>(TestLabAutomationContext context)
            where T : class, IGameDefinition
        {
            return context.Prototype().GetDefinitions<T>().FirstOrDefault();
        }

        private static TestLabAutomationStepResult ObserveRequirement(PrototypeTestLabOperation operation, TestLabAutomationContext context, string operationId)
        {
            string transactionId = context.TransactionIds.Create(context.CurrentSuiteId, context.CurrentScenarioId, context.RunId, context.CurrentStepIndex, operationId);
            bool missingInfrastructure = string.Equals(operation.Code, "MissingRequirement", StringComparison.Ordinal)
                || string.Equals(operation.Code, "MissingReference", StringComparison.Ordinal)
                || string.Equals(operation.Code, "MissingCharacterSystem", StringComparison.Ordinal);
            return missingInfrastructure
                ? new TestLabAutomationStepResult(operationId, operation.OperationName, TestLabAutomationStatus.Failed, "RequirementEvaluationObserved", "Evaluated", operation.Code, string.Empty, transactionId, operation.Message)
                : new TestLabAutomationStepResult(operationId, operation.OperationName, TestLabAutomationStatus.Passed, "RequirementEvaluationObserved", "Evaluated", operation.Code, string.Empty, transactionId, operation.Message);
        }

        private static TestLabAutomationStepResult Operation(PrototypeTestLabOperation operation, TestLabAutomationContext context, string operationId, bool acceptFailure = false)
        {
            string transactionId = context.TransactionIds.Create(context.CurrentSuiteId, context.CurrentScenarioId, context.RunId, context.CurrentStepIndex, operationId);
            if (acceptFailure)
            {
                return operation.Succeeded
                    ? TestLabAssertions.Fail(operationId, operation.OperationName, "OperationFailed", "Failure", operation.Code, operation.Message, string.Empty, transactionId)
                    : TestLabAssertions.Pass(operationId, operation.OperationName, $"Expected rejection observed: {operation.Code} {operation.Message}");
            }

            return operation.Succeeded
                ? new TestLabAutomationStepResult(operationId, operation.OperationName, TestLabAutomationStatus.Passed, "OperationSucceeded", "Succeeded", operation.Code, string.Empty, transactionId, operation.Message)
                : new TestLabAutomationStepResult(operationId, operation.OperationName, TestLabAutomationStatus.Failed, "OperationSucceeded", "Succeeded", operation.Code, string.Empty, transactionId, operation.Message);
        }

        private static void TryRegister(TestLabAutomationRegistry registry, ITestLabAutomationSuite suite)
        {
            registry.TryRegister(suite, out _);
        }
    }
}
#endif
