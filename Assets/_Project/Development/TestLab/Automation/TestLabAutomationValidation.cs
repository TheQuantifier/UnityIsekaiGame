#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Development.Automation
{
    public sealed class TestLabAutomationValidationResult
    {
        private readonly IReadOnlyList<string> errors;
        private readonly IReadOnlyList<string> warnings;

        public TestLabAutomationValidationResult(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            this.errors = new List<string>(errors ?? Array.Empty<string>()).AsReadOnly();
            this.warnings = new List<string>(warnings ?? Array.Empty<string>()).AsReadOnly();
        }

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool Succeeded => errors.Count == 0;

        public string ToSummary()
        {
            return $"Automation validation: {errors.Count} error(s), {warnings.Count} warning(s).";
        }
    }

    public sealed class TestLabAutomationMigrationInventory
    {
        public TestLabAutomationMigrationInventory(
            int totalScenarios,
            int freshRuntimeScenarios,
            int snapshotRestoreScenarios,
            int sharedRuntimeScenarios,
            int persistentFixtureScenarios,
            int legacySharedFeatureScenarios,
            IReadOnlyList<string> legacySharedScenarioIds)
        {
            TotalScenarios = totalScenarios;
            FreshRuntimeScenarios = freshRuntimeScenarios;
            SnapshotRestoreScenarios = snapshotRestoreScenarios;
            SharedRuntimeScenarios = sharedRuntimeScenarios;
            PersistentFixtureScenarios = persistentFixtureScenarios;
            LegacySharedFeatureScenarios = legacySharedFeatureScenarios;
            LegacySharedScenarioIds = (legacySharedScenarioIds ?? Array.Empty<string>()).ToArray();
        }

        public int TotalScenarios { get; }
        public int FreshRuntimeScenarios { get; }
        public int SnapshotRestoreScenarios { get; }
        public int SharedRuntimeScenarios { get; }
        public int PersistentFixtureScenarios { get; }
        public int LegacySharedFeatureScenarios { get; }
        public IReadOnlyList<string> LegacySharedScenarioIds { get; }

        public string ToSummary()
        {
            return $"Registered scenarios: {TotalScenarios}. FreshRuntime={FreshRuntimeScenarios}, SnapshotRestore={SnapshotRestoreScenarios}, SharedRuntime={SharedRuntimeScenarios}, PersistentFixture={PersistentFixtureScenarios}, Legacy shared feature scenarios={LegacySharedFeatureScenarios}.";
        }
    }

    public static class TestLabAutomationValidation
    {
        private const TestLabRuntimeArea IsolatedRuntimeAreas = TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice | TestLabRuntimeArea.WorldLocations | TestLabRuntimeArea.Quests;
        private const TestLabRuntimeArea AllRuntimeAreas = TestLabRuntimeArea.KnowledgeHistory
            | TestLabRuntimeArea.Character
            | TestLabRuntimeArea.Combat
            | TestLabRuntimeArea.Biology
            | TestLabRuntimeArea.Persistence
            | TestLabRuntimeArea.Items
            | TestLabRuntimeArea.Professions
            | TestLabRuntimeArea.Economy
            | TestLabRuntimeArea.Social
            | TestLabRuntimeArea.Organizations
            | TestLabRuntimeArea.OrganizationMemberships
            | TestLabRuntimeArea.OrganizationAuthority
            | TestLabRuntimeArea.OrganizationResources
            | TestLabRuntimeArea.OrganizationDecisions
            | TestLabRuntimeArea.Factions
            | TestLabRuntimeArea.Diplomacy
            | TestLabRuntimeArea.Governments
            | TestLabRuntimeArea.Laws
            | TestLabRuntimeArea.Crimes
            | TestLabRuntimeArea.Justice
            | TestLabRuntimeArea.WorldLocations
            | TestLabRuntimeArea.Quests;
        private const TestLabHostFeature AllHostFeatures = TestLabHostFeature.DefinitionContext
            | TestLabHostFeature.SceneReset
            | TestLabHostFeature.SnapshotRestore
            | TestLabHostFeature.SharedRuntime
            | TestLabHostFeature.PersistentFixture
            | TestLabHostFeature.DeterministicTime
            | TestLabHostFeature.FixtureFingerprinting
            | TestLabHostFeature.DirtyStateInspection
            | TestLabHostFeature.DomainEventInspection
            | TestLabHostFeature.VisibleUi
            | TestLabHostFeature.AutomatedExecution
            | TestLabHostFeature.DevelopmentOnly
            | TestLabHostFeature.Persistence;
        private static readonly HashSet<string> SharedRuntimeMigrationSuiteIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "feature.3.runtime-taxonomy",
            "feature.4.1.save-file-foundation",
            "feature.4.2.inventory-equipment-persistence",
            "feature.4.3.vitals-status-persistence",
            "feature.4.4.quest-persistence",
            "feature.4.5.location-persistence",
            "feature.4.6.world-entity-identity",
            "feature.4.7.save-slots-autosave-load-ui",
            "feature.4.8.persistence-recovery-hardening",
            "feature.5.1.identity-origin-progression",
            "feature.5.2-5.4a.attributes-calculated-stats",
            "feature.5.3.skills-progression",
            "feature.5.4b.current-resources",
            "feature.5.5.traits-requirements",
            "feature.5.6.character-integration",
            "feature.6.1.damage-healing",
            "feature.6.2.attack-resolution",
            "feature.6.3.lifecycle",
            "feature.6.4.ongoing-effects",
            "feature.6.5.combat-state",
            "feature.6.6.defensive-actions",
            "feature.6.7.combat-execution",
            "feature.6.8.combat-reactions",
            "feature.6.9.combat-contribution",
            "feature.6.10.combat-integration",
            "feature.7.1.body-species",
            "feature.7.2.body-anatomy",
            "feature.7.3.body-condition",
            "feature.7.4.vital-processes",
            "feature.7.5.biological-hazards",
            "feature.7.6.biological-compatibility",
            "feature.7.7.natural-recovery-repair",
            "feature.7.8.transformation-body-replacement",
            "feature.7.9.diseases-biological-conditions",
            "feature.7.10.biological-integration"
        };
        private static readonly HashSet<string> PersistentRuntimeSuiteIds = new HashSet<string>(StringComparer.Ordinal)
        {
        };

        public static TestLabAutomationValidationResult Validate(TestLabAutomationRegistry registry)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            if (registry == null)
            {
                errors.Add("Automation registry is missing.");
                return new TestLabAutomationValidationResult(errors, warnings);
            }

            HashSet<string> suiteIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ITestLabAutomationSuite suite in registry.Suites)
            {
                ValidateSuite(suite, suiteIds, errors, warnings);
            }

            return new TestLabAutomationValidationResult(errors, warnings);
        }

        public static TestLabAutomationMigrationInventory BuildMigrationInventory(TestLabAutomationRegistry registry)
        {
            IReadOnlyList<(string SuiteId, ITestLabAutomationScenario Scenario)> scenarios = (registry?.Suites ?? Array.Empty<ITestLabAutomationSuite>())
                .SelectMany(suite => (suite.Scenarios ?? Array.Empty<ITestLabAutomationScenario>()).Select(scenario => (suite.SuiteId, Scenario: scenario)))
                .Where(entry => entry.Scenario != null)
                .ToArray();
            string[] legacyShared = scenarios
                .Where(entry => entry.Scenario.IsolationMode == TestLabScenarioIsolationMode.SharedRuntime && !SharedRuntimeMigrationSuiteIds.Contains(entry.SuiteId))
                .Select(entry => $"{entry.SuiteId}/{entry.Scenario.ScenarioId}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            return new TestLabAutomationMigrationInventory(
                scenarios.Count,
                scenarios.Count(entry => entry.Scenario.IsolationMode == TestLabScenarioIsolationMode.FreshRuntime),
                scenarios.Count(entry => entry.Scenario.IsolationMode == TestLabScenarioIsolationMode.SnapshotRestore),
                scenarios.Count(entry => entry.Scenario.IsolationMode == TestLabScenarioIsolationMode.SharedRuntime),
                scenarios.Count(entry => entry.Scenario.IsolationMode == TestLabScenarioIsolationMode.PersistentFixture),
                legacyShared.Length,
                legacyShared);
        }

        private static void ValidateSuite(ITestLabAutomationSuite suite, HashSet<string> suiteIds, List<string> errors, List<string> warnings)
        {
            if (suite == null)
            {
                errors.Add("Null suite registered.");
                return;
            }

            if (string.IsNullOrWhiteSpace(suite.SuiteId))
            {
                errors.Add("Suite has no stable suite ID.");
            }
            else if (!suiteIds.Add(suite.SuiteId))
            {
                errors.Add($"Duplicate suite ID '{suite.SuiteId}'.");
            }

            if (string.IsNullOrWhiteSpace(suite.DisplayName))
            {
                errors.Add($"Suite '{suite.SuiteId}' has no display name.");
            }

            if (suite.Scenarios == null || suite.Scenarios.Count == 0)
            {
                errors.Add($"Suite '{suite.SuiteId}' has no scenarios.");
            }

            if (suite.RequiredServices == null || suite.RequiredServices.Count == 0)
            {
                warnings.Add($"Suite '{suite.SuiteId}' has no required service declarations.");
            }

            HashSet<string> scenarioIds = new HashSet<string>(StringComparer.Ordinal);
            int previousOrder = int.MinValue;
            foreach (ITestLabAutomationScenario scenario in suite.Scenarios ?? Array.Empty<ITestLabAutomationScenario>())
            {
                if (scenario == null)
                {
                    errors.Add($"Suite '{suite.SuiteId}' contains a null scenario.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(scenario.ScenarioId))
                {
                    errors.Add($"Suite '{suite.SuiteId}' contains a scenario with no stable scenario ID.");
                }
                else if (!scenarioIds.Add(scenario.ScenarioId))
                {
                    errors.Add($"Suite '{suite.SuiteId}' has duplicate scenario ID '{scenario.ScenarioId}'.");
                }

                if (string.IsNullOrWhiteSpace(scenario.DisplayName))
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' has no display name.");
                }

                if (!Enum.IsDefined(typeof(TestLabScenarioIsolationMode), scenario.IsolationMode))
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' declares unsupported isolation mode '{scenario.IsolationMode}'.");
                }

                if (!Enum.IsDefined(typeof(TestLabCommandLineSupport), scenario.CommandLineSupport))
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' declares unsupported command-line support mode '{scenario.CommandLineSupport}'.");
                }

                if (scenario.RequiredRuntimeAreas == TestLabRuntimeArea.None)
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' declares no required runtime areas.");
                }

                if ((scenario.RequiredRuntimeAreas & ~AllRuntimeAreas) != TestLabRuntimeArea.None)
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' declares unknown runtime area flags '{scenario.RequiredRuntimeAreas}'.");
                }

                if (!CanIsolate(scenario.RequiredRuntimeAreas)
                    && (scenario.IsolationMode == TestLabScenarioIsolationMode.FreshRuntime
                        || scenario.IsolationMode == TestLabScenarioIsolationMode.SnapshotRestore)
                    && !scenario.RequiresSceneHost)
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' uses {scenario.IsolationMode} for runtime areas '{scenario.RequiredRuntimeAreas}', but only '{IsolatedRuntimeAreas}' can be isolated automatically.");
                }

                if ((scenario.RequiredHostFeatures & ~AllHostFeatures) != TestLabHostFeature.None)
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' declares unknown host feature flags '{scenario.RequiredHostFeatures}'.");
                }

                if (scenario.RequiredFixtureIds == null || scenario.RequiredFixtureIds.Count == 0)
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' declares no fixture requirements.");
                }

                if (scenario.IsolationMode != TestLabScenarioIsolationMode.FreshRuntime
                    && (scenario.RequiredFixtureIds == null || !scenario.RequiredFixtureIds.Any(id => string.Equals(id, TestLabScenarioContext.MutableStateScopeFixtureId, StringComparison.Ordinal))))
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' uses {scenario.IsolationMode} without declaring an owning mutable-state scope fixture.");
                }

                if (scenario.IsolationMode == TestLabScenarioIsolationMode.SharedRuntime && !SharedRuntimeMigrationSuiteIds.Contains(suite.SuiteId))
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' uses SharedRuntime but the suite is not in the temporary shared-runtime migration allowlist.");
                }

                if (scenario.IsolationMode == TestLabScenarioIsolationMode.PersistentFixture && !PersistentRuntimeSuiteIds.Contains(suite.SuiteId))
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' uses PersistentFixture but the suite is not an approved persistent integration suite.");
                }

                if (scenario.Order < previousOrder)
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' is not in deterministic order.");
                }

                previousOrder = scenario.Order;

                if (scenario.Steps == null || scenario.Steps.Count == 0)
                {
                    errors.Add($"Scenario '{suite.SuiteId}/{scenario.ScenarioId}' has no steps.");
                }
            }
        }

        private static bool CanIsolate(TestLabRuntimeArea requiredRuntimeAreas)
        {
            return (requiredRuntimeAreas & ~IsolatedRuntimeAreas) == TestLabRuntimeArea.None;
        }
    }
}
#endif
