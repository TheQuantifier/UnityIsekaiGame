#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Development.Automation
{
    public sealed class TestLabDefinitionContext
    {
        public TestLabDefinitionContext(
            DefinitionRegistry registry,
            string sourceId,
            string sourceLabel,
            bool catalogAuthored,
            bool fallbackDefinitionsAvailable,
            long revision,
            IEnumerable<string> diagnostics = null,
            IEnumerable<string> validationErrors = null)
        {
            Registry = registry;
            SourceId = string.IsNullOrWhiteSpace(sourceId) ? "definitions.unknown" : sourceId.Trim();
            SourceLabel = string.IsNullOrWhiteSpace(sourceLabel) ? SourceId : sourceLabel.Trim();
            CatalogAuthored = catalogAuthored;
            FallbackDefinitionsAvailable = fallbackDefinitionsAvailable;
            Revision = revision;
            Diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            ValidationErrors = (validationErrors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public DefinitionRegistry Registry { get; }
        public string SourceId { get; }
        public string SourceLabel { get; }
        public bool CatalogAuthored { get; }
        public bool FallbackDefinitionsAvailable { get; }
        public long Revision { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> ValidationErrors { get; }
        public bool HasDefinitions => Registry != null;
        public bool HasValidationErrors => ValidationErrors.Count > 0;

        public string ToDiagnostic()
        {
            return $"Definitions={SourceId} Label='{SourceLabel}' Catalog={CatalogAuthored} Fallbacks={FallbackDefinitionsAvailable} Revision={Revision} Errors={ValidationErrors.Count} Diagnostics={string.Join("; ", Diagnostics)}";
        }
    }

    public sealed class TestLabAutomationHostCapabilities
    {
        public TestLabAutomationHostCapabilities(
            string hostId,
            string displayName,
            string sceneName,
            TestLabRuntimeArea supportedRuntimeAreas,
            TestLabRuntimeArea freshRuntimeAreas,
            TestLabRuntimeArea snapshotRestoreAreas,
            TestLabRuntimeArea sharedRuntimeAreas,
            TestLabRuntimeArea persistentFixtureAreas,
            IEnumerable<TestLabScenarioIsolationMode> supportedIsolationModes,
            TestLabHostFeature features,
            IEnumerable<string> diagnostics = null)
        {
            HostId = string.IsNullOrWhiteSpace(hostId) ? string.Empty : hostId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? HostId : displayName.Trim();
            SceneName = string.IsNullOrWhiteSpace(sceneName) ? "scene.none" : sceneName.Trim();
            SupportedRuntimeAreas = supportedRuntimeAreas;
            FreshRuntimeAreas = freshRuntimeAreas;
            SnapshotRestoreAreas = snapshotRestoreAreas;
            SharedRuntimeAreas = sharedRuntimeAreas;
            PersistentFixtureAreas = persistentFixtureAreas;
            SupportedIsolationModes = (supportedIsolationModes ?? Array.Empty<TestLabScenarioIsolationMode>()).Distinct().ToArray();
            Features = features;
            Diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public string HostId { get; }
        public string DisplayName { get; }
        public string SceneName { get; }
        public TestLabRuntimeArea SupportedRuntimeAreas { get; }
        public TestLabRuntimeArea FreshRuntimeAreas { get; }
        public TestLabRuntimeArea SnapshotRestoreAreas { get; }
        public TestLabRuntimeArea SharedRuntimeAreas { get; }
        public TestLabRuntimeArea PersistentFixtureAreas { get; }
        public IReadOnlyList<TestLabScenarioIsolationMode> SupportedIsolationModes { get; }
        public TestLabHostFeature Features { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool SupportsAutomatedExecution => (Features & TestLabHostFeature.AutomatedExecution) != 0;

        public bool SupportsIsolation(TestLabScenarioIsolationMode mode)
        {
            return SupportedIsolationModes.Contains(mode);
        }

        public bool SupportsRuntimeAreas(TestLabScenarioIsolationMode mode, TestLabRuntimeArea requiredAreas)
        {
            TestLabRuntimeArea supported = mode switch
            {
                TestLabScenarioIsolationMode.FreshRuntime => FreshRuntimeAreas,
                TestLabScenarioIsolationMode.SnapshotRestore => SnapshotRestoreAreas,
                TestLabScenarioIsolationMode.SharedRuntime => SharedRuntimeAreas,
                TestLabScenarioIsolationMode.PersistentFixture => PersistentFixtureAreas,
                _ => TestLabRuntimeArea.None
            };

            return (requiredAreas & ~supported) == TestLabRuntimeArea.None;
        }

        public bool HasFeatures(TestLabHostFeature required)
        {
            return (Features & required) == required;
        }

        public string ToDiagnostic()
        {
            return $"Host={HostId} Name='{DisplayName}' Scene='{SceneName}' Areas={SupportedRuntimeAreas} Fresh={FreshRuntimeAreas} Snapshot={SnapshotRestoreAreas} Shared={SharedRuntimeAreas} Persistent={PersistentFixtureAreas} Modes={string.Join(",", SupportedIsolationModes)} Features={Features}";
        }
    }

    public sealed class TestLabRuntimeBundleRequest
    {
        public TestLabRuntimeBundleRequest(
            string runId,
            string suiteId,
            string scenarioId,
            TestLabScenarioIsolationMode isolationMode,
            TestLabRuntimeArea requiredRuntimeAreas,
            IReadOnlyList<string> requiredFixtureIds,
            int deterministicSeed = 0,
            bool allowSceneObjects = false,
            bool visibleUiRequired = false,
            bool persistenceRequired = false,
            bool snapshotRequired = false,
            string sharedScopeId = "",
            string persistentScopeId = "")
        {
            RunId = runId ?? string.Empty;
            SuiteId = suiteId ?? string.Empty;
            ScenarioId = scenarioId ?? string.Empty;
            IsolationMode = isolationMode;
            RequiredRuntimeAreas = requiredRuntimeAreas;
            RequiredFixtureIds = (requiredFixtureIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            DeterministicSeed = deterministicSeed;
            AllowSceneObjects = allowSceneObjects;
            VisibleUiRequired = visibleUiRequired;
            PersistenceRequired = persistenceRequired;
            SnapshotRequired = snapshotRequired;
            SharedScopeId = sharedScopeId ?? string.Empty;
            PersistentScopeId = persistentScopeId ?? string.Empty;
        }

        public string RunId { get; }
        public string SuiteId { get; }
        public string ScenarioId { get; }
        public TestLabScenarioIsolationMode IsolationMode { get; }
        public TestLabRuntimeArea RequiredRuntimeAreas { get; }
        public IReadOnlyList<string> RequiredFixtureIds { get; }
        public int DeterministicSeed { get; }
        public bool AllowSceneObjects { get; }
        public bool VisibleUiRequired { get; }
        public bool PersistenceRequired { get; }
        public bool SnapshotRequired { get; }
        public string SharedScopeId { get; }
        public string PersistentScopeId { get; }
    }

    public sealed class TestLabRuntimeBundleResult
    {
        private TestLabRuntimeBundleResult(bool succeeded, TestLabRuntimeBundle bundle, TestLabRuntimeBundle ownedBundle, TestLabAutomationHostCapabilities capabilities, string failureCode, string message)
        {
            Succeeded = succeeded;
            Bundle = bundle;
            OwnedBundle = ownedBundle;
            Capabilities = capabilities;
            FailureCode = failureCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public TestLabRuntimeBundle Bundle { get; }
        public TestLabRuntimeBundle OwnedBundle { get; }
        public TestLabAutomationHostCapabilities Capabilities { get; }
        public string FailureCode { get; }
        public string Message { get; }

        public static TestLabRuntimeBundleResult Success(TestLabRuntimeBundle bundle, TestLabRuntimeBundle ownedBundle, TestLabAutomationHostCapabilities capabilities)
        {
            return new TestLabRuntimeBundleResult(true, bundle, ownedBundle, capabilities, string.Empty, string.Empty);
        }

        public static TestLabRuntimeBundleResult Fail(string failureCode, string message, TestLabAutomationHostCapabilities capabilities = null)
        {
            return new TestLabRuntimeBundleResult(false, null, null, capabilities, failureCode, message);
        }
    }

    public sealed class TestLabEnvironmentSnapshotRequest
    {
        public TestLabEnvironmentSnapshotRequest(string runId, string suiteId, string scenarioId, TestLabRuntimeArea requiredRuntimeAreas)
        {
            RunId = runId ?? string.Empty;
            SuiteId = suiteId ?? string.Empty;
            ScenarioId = scenarioId ?? string.Empty;
            RequiredRuntimeAreas = requiredRuntimeAreas;
        }

        public string RunId { get; }
        public string SuiteId { get; }
        public string ScenarioId { get; }
        public TestLabRuntimeArea RequiredRuntimeAreas { get; }
    }

    public sealed class TestLabEnvironmentSnapshot
    {
        public TestLabEnvironmentSnapshot(string hostId, TestLabRuntimeArea capturedAreas, TestLabRuntimeBundleFingerprint fingerprint, object payload = null)
        {
            HostId = hostId ?? string.Empty;
            CapturedAreas = capturedAreas;
            Fingerprint = fingerprint;
            Payload = payload;
        }

        public string HostId { get; }
        public TestLabRuntimeArea CapturedAreas { get; }
        public TestLabRuntimeBundleFingerprint Fingerprint { get; }
        public object Payload { get; }
    }

    public sealed class TestLabEnvironmentResetRequest
    {
        public TestLabEnvironmentResetRequest(string runId, string suiteId, string scenarioId, string reason, TestLabRuntimeArea requiredRuntimeAreas)
        {
            RunId = runId ?? string.Empty;
            SuiteId = suiteId ?? string.Empty;
            ScenarioId = scenarioId ?? string.Empty;
            Reason = reason ?? string.Empty;
            RequiredRuntimeAreas = requiredRuntimeAreas;
        }

        public string RunId { get; }
        public string SuiteId { get; }
        public string ScenarioId { get; }
        public string Reason { get; }
        public TestLabRuntimeArea RequiredRuntimeAreas { get; }
    }

    public sealed class TestLabOperationResult
    {
        public TestLabOperationResult(bool succeeded, string code, string message)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? (succeeded ? "Success" : "Failed") : code.Trim();
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }
    }

    public interface ITestLabAutomationHost
    {
        string HostId { get; }
        TestLabAutomationHostCapabilities GetCapabilities();
        TestLabDefinitionContext GetDefinitionContext();
        TestLabRuntimeBundleResult CreateRuntimeBundle(TestLabRuntimeBundleRequest request);
        TestLabEnvironmentSnapshot CaptureEnvironment(TestLabEnvironmentSnapshotRequest request);
        TestLabOperationResult RestoreEnvironment(TestLabEnvironmentSnapshot snapshot);
        TestLabOperationResult ResetEnvironment(TestLabEnvironmentResetRequest request);
        IEnumerable<TestLabRuntimeFingerprintSection> CaptureFingerprint(TestLabRuntimeArea requiredAreas);
    }

    public interface ITestLabAutomationScenarioScopeHost
    {
        void SetActiveScenarioContext(TestLabScenarioContext scenarioContext);
        void ClearActiveScenarioContext(TestLabScenarioContext scenarioContext);
    }

    public sealed class TestLabAutomationHostResolution
    {
        private TestLabAutomationHostResolution(ITestLabAutomationHost host, TestLabAutomationHostCapabilities capabilities, long registryRevision, string failureCode, string message)
        {
            Host = host;
            Capabilities = capabilities;
            RegistryRevision = registryRevision;
            FailureCode = failureCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ITestLabAutomationHost Host { get; }
        public TestLabAutomationHostCapabilities Capabilities { get; }
        public long RegistryRevision { get; }
        public string FailureCode { get; }
        public string Message { get; }
        public bool Succeeded => Host != null && string.IsNullOrWhiteSpace(FailureCode);

        public static TestLabAutomationHostResolution Success(ITestLabAutomationHost host, TestLabAutomationHostCapabilities capabilities, long registryRevision)
        {
            return new TestLabAutomationHostResolution(host, capabilities, registryRevision, string.Empty, string.Empty);
        }

        public static TestLabAutomationHostResolution Fail(string failureCode, string message)
        {
            return new TestLabAutomationHostResolution(null, null, TestLabAutomationHostRegistry.Revision, failureCode, message);
        }
    }

    public sealed class TestLabScenarioCompatibilityResult
    {
        public TestLabScenarioCompatibilityResult(
            string suiteId,
            string scenarioId,
            string displayName,
            bool compatible,
            bool sceneIndependent,
            string hostId,
            string failureCode,
            string diagnostics)
        {
            SuiteId = suiteId ?? string.Empty;
            ScenarioId = scenarioId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ScenarioId : displayName;
            Compatible = compatible;
            SceneIndependent = sceneIndependent;
            HostId = hostId ?? string.Empty;
            FailureCode = failureCode ?? string.Empty;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public string SuiteId { get; }
        public string ScenarioId { get; }
        public string DisplayName { get; }
        public bool Compatible { get; }
        public bool SceneIndependent { get; }
        public string HostId { get; }
        public string FailureCode { get; }
        public string Diagnostics { get; }
    }

    public sealed class TestLabSuiteCompatibilityReport
    {
        public TestLabSuiteCompatibilityReport(IEnumerable<TestLabScenarioCompatibilityResult> scenarios)
        {
            Scenarios = (scenarios ?? Array.Empty<TestLabScenarioCompatibilityResult>()).ToArray();
        }

        public IReadOnlyList<TestLabScenarioCompatibilityResult> Scenarios { get; }
        public int CompatibleCount => Scenarios.Count(scenario => scenario.Compatible);
        public int SceneIndependentCount => Scenarios.Count(scenario => scenario.SceneIndependent);
        public int UnsupportedCount => Scenarios.Count(scenario => !scenario.Compatible);
        public bool Compatible => UnsupportedCount == 0;

        public string ToDiagnostic()
        {
            return $"Compatibility: {CompatibleCount} compatible, {SceneIndependentCount} scene-independent, {UnsupportedCount} unsupported.";
        }
    }

    public static class TestLabAutomationCompatibility
    {
        private const TestLabRuntimeArea HostlessFreshRuntimeAreas = TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice | TestLabRuntimeArea.WorldLocations | TestLabRuntimeArea.Quests;

        public static TestLabSuiteCompatibilityReport Preview(
            IEnumerable<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)> selections,
            Func<string, TestLabAutomationHostResolution> hostResolver,
            TestLabDefinitionContext defaultDefinitionContext)
        {
            return new TestLabSuiteCompatibilityReport((selections ?? Array.Empty<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)>())
                .Select(selection => PreviewScenario(selection.Suite, selection.Scenario, hostResolver, defaultDefinitionContext)));
        }

        public static TestLabScenarioCompatibilityResult PreviewScenario(
            ITestLabAutomationSuite suite,
            ITestLabAutomationScenario scenario,
            Func<string, TestLabAutomationHostResolution> hostResolver,
            TestLabDefinitionContext defaultDefinitionContext)
        {
            if (scenario == null)
            {
                return new TestLabScenarioCompatibilityResult(suite?.SuiteId, string.Empty, string.Empty, false, false, string.Empty, "MissingScenario", "Cannot preview a null automation scenario.");
            }

            if (!scenario.RequiresSceneHost)
            {
                if (scenario.IsolationMode != TestLabScenarioIsolationMode.FreshRuntime)
                {
                    return Incompatible(suite, scenario, "MissingHostRequirement", $"Hostless scenario '{suite?.SuiteId}/{scenario.ScenarioId}' uses isolation mode '{scenario.IsolationMode}'. Only FreshRuntime can run without a scene host.");
                }

                if ((scenario.RequiredRuntimeAreas & ~HostlessFreshRuntimeAreas) != TestLabRuntimeArea.None)
                {
                    return Incompatible(suite, scenario, "UnsupportedHostlessArea", $"Hostless FreshRuntime supports '{HostlessFreshRuntimeAreas}', not '{scenario.RequiredRuntimeAreas}'.");
                }

                if (defaultDefinitionContext == null || !defaultDefinitionContext.HasDefinitions)
                {
                    return Incompatible(suite, scenario, "MissingDefinitions", $"Hostless scenario '{suite?.SuiteId}/{scenario.ScenarioId}' has no explicit default definition context.");
                }

                if (defaultDefinitionContext.HasValidationErrors)
                {
                    return Incompatible(suite, scenario, "DefinitionConflict", $"Hostless definition context has validation errors: {string.Join(" | ", defaultDefinitionContext.ValidationErrors)}.");
                }

                string missingHostlessDefinitions = MissingDefinitions(scenario.RequiredDefinitionIds, defaultDefinitionContext);
                if (!string.IsNullOrWhiteSpace(missingHostlessDefinitions))
                {
                    return Incompatible(suite, scenario, "MissingDefinitions", $"Hostless scenario '{suite?.SuiteId}/{scenario.ScenarioId}' is missing required definitions: {missingHostlessDefinitions}. {defaultDefinitionContext.ToDiagnostic()}");
                }

                return new TestLabScenarioCompatibilityResult(suite?.SuiteId, scenario.ScenarioId, scenario.DisplayName, true, true, string.Empty, string.Empty, $"Scene-independent FreshRuntime using {defaultDefinitionContext.ToDiagnostic()}.");
            }

            TestLabAutomationHostResolution resolution = (hostResolver ?? TestLabAutomationHostRegistry.ResolveActive)(scenario.RequiredHostId);
            if (resolution == null || !resolution.Succeeded)
            {
                return Incompatible(suite, scenario, resolution?.FailureCode ?? "NoHost", resolution?.Message ?? "No Test Lab automation host is registered for the active scene.");
            }

            TestLabAutomationStepResult validation = TestLabAutomationHostValidation.ValidateHostForScenario(
                resolution.Capabilities,
                scenario.IsolationMode,
                scenario.RequiredRuntimeAreas,
                scenario.RequiredHostFeatures);
            if (!validation.Succeeded)
            {
                return Incompatible(suite, scenario, validation.Actual, validation.Diagnostics, resolution.Capabilities.HostId);
            }

            TestLabDefinitionContext definitions = resolution.Host.GetDefinitionContext();
            if (definitions == null || !definitions.HasDefinitions)
            {
                return Incompatible(suite, scenario, "MissingDefinitions", $"Host '{resolution.Capabilities.HostId}' does not provide a definition context.", resolution.Capabilities.HostId);
            }

            if (definitions.HasValidationErrors)
            {
                return Incompatible(suite, scenario, "DefinitionConflict", $"Host '{resolution.Capabilities.HostId}' definition context has validation errors: {string.Join(" | ", definitions.ValidationErrors)}.", resolution.Capabilities.HostId);
            }

            string missingDefinitions = MissingDefinitions(scenario.RequiredDefinitionIds, definitions);
            if (!string.IsNullOrWhiteSpace(missingDefinitions))
            {
                return Incompatible(suite, scenario, "MissingDefinitions", $"Host '{resolution.Capabilities.HostId}' is missing required definitions: {missingDefinitions}. {definitions.ToDiagnostic()}", resolution.Capabilities.HostId);
            }

            return new TestLabScenarioCompatibilityResult(suite?.SuiteId, scenario.ScenarioId, scenario.DisplayName, true, false, resolution.Capabilities.HostId, string.Empty, $"{resolution.Capabilities.ToDiagnostic()} {definitions.ToDiagnostic()}.");
        }

        private static string MissingDefinitions(IReadOnlyList<string> requiredDefinitionIds, TestLabDefinitionContext definitions)
        {
            string[] missing = (requiredDefinitionIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Where(id => definitions?.Registry == null || !definitions.Registry.Contains(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            return string.Join(", ", missing);
        }

        private static TestLabScenarioCompatibilityResult Incompatible(ITestLabAutomationSuite suite, ITestLabAutomationScenario scenario, string code, string diagnostics, string hostId = "")
        {
            return new TestLabScenarioCompatibilityResult(suite?.SuiteId, scenario?.ScenarioId, scenario?.DisplayName, false, false, hostId, code, diagnostics);
        }
    }

    public static class TestLabAutomationHostRegistry
    {
        private static readonly List<ITestLabAutomationHost> hosts = new List<ITestLabAutomationHost>();
        private static long revision;

        public static long Revision => revision;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration()
        {
            hosts.Clear();
            revision++;
        }

        public static IReadOnlyList<TestLabAutomationHostCapabilities> RegisteredCapabilities
        {
            get
            {
                PruneDestroyedHosts();
                return hosts.Select(host => host.GetCapabilities()).Where(capabilities => capabilities != null).OrderBy(capabilities => capabilities.HostId, StringComparer.Ordinal).ToArray();
            }
        }

        public static bool Register(ITestLabAutomationHost host, out string failure)
        {
            PruneDestroyedHosts();
            failure = string.Empty;
            if (host == null)
            {
                failure = "Cannot register a null Test Lab automation host.";
                return false;
            }

            TestLabAutomationHostCapabilities capabilities = host.GetCapabilities();
            string hostId = string.IsNullOrWhiteSpace(host.HostId) ? capabilities?.HostId : host.HostId;
            if (string.IsNullOrWhiteSpace(hostId))
            {
                failure = "Test Lab automation host has no stable host ID.";
                return false;
            }

            if (hosts.Any(existing => ReferenceEquals(existing, host)))
            {
                return true;
            }

            ITestLabAutomationHost duplicate = hosts.FirstOrDefault(existing => string.Equals(existing.HostId, hostId, StringComparison.Ordinal));
            if (duplicate != null)
            {
                failure = $"Duplicate Test Lab automation host ID '{hostId}'. Existing={DescribeHost(duplicate)} New={DescribeHost(host)}.";
                return false;
            }

            hosts.Add(host);
            revision++;
            return true;
        }

        public static void Unregister(ITestLabAutomationHost host)
        {
            if (host == null)
            {
                return;
            }

            hosts.RemoveAll(existing => ReferenceEquals(existing, host));
            PruneDestroyedHosts();
            revision++;
        }

        public static TestLabAutomationHostResolution ResolveActive(string requiredHostId = "")
        {
            PruneDestroyedHosts();
            ITestLabAutomationHost[] active = hosts.OrderBy(host => host.HostId, StringComparer.Ordinal).ToArray();
            if (!string.IsNullOrWhiteSpace(requiredHostId))
            {
                ITestLabAutomationHost exact = active.FirstOrDefault(host => string.Equals(host.HostId, requiredHostId, StringComparison.Ordinal));
                return exact == null
                    ? TestLabAutomationHostResolution.Fail("HostNotFound", $"No Test Lab automation host with ID '{requiredHostId}' is registered.")
                    : TestLabAutomationHostResolution.Success(exact, exact.GetCapabilities(), revision);
            }

            if (active.Length == 0)
            {
                return TestLabAutomationHostResolution.Fail("NoHost", "No Test Lab automation host is registered for the active scene.");
            }

            if (active.Length > 1)
            {
                return TestLabAutomationHostResolution.Fail("AmbiguousHost", $"Multiple Test Lab automation hosts are registered. Select one explicitly. Hosts={string.Join(", ", active.Select(host => host.HostId))}.");
            }

            return TestLabAutomationHostResolution.Success(active[0], active[0].GetCapabilities(), revision);
        }

        public static void ClearForTests()
        {
            hosts.Clear();
            revision++;
        }

        public static bool IsRegistered(ITestLabAutomationHost host, long expectedRevision)
        {
            PruneDestroyedHosts();
            return host != null
                && revision == expectedRevision
                && hosts.Any(existing => ReferenceEquals(existing, host));
        }

        private static void PruneDestroyedHosts()
        {
            int removed = hosts.RemoveAll(host => host == null || (host is UnityEngine.Object unityObject && unityObject == null));
            if (removed > 0)
            {
                revision++;
            }
        }

        private static string DescribeHost(ITestLabAutomationHost host)
        {
            TestLabAutomationHostCapabilities capabilities = host?.GetCapabilities();
            return capabilities == null ? "unknown" : capabilities.ToDiagnostic();
        }
    }

    public sealed class TestLabSceneIndependentAutomationHost : ITestLabAutomationHost
    {
        private readonly TestLabDefinitionContext definitionContext;
        private readonly Dictionary<string, TestLabRuntimeBundle> persistentBundles = new Dictionary<string, TestLabRuntimeBundle>(StringComparer.Ordinal);

        public TestLabSceneIndependentAutomationHost(DefinitionRegistry registry, string hostId = "host.scene-independent.fresh-runtime")
        {
            definitionContext = new TestLabDefinitionContext(registry, "definitions.scene-independent", "Scene-independent automation definitions", catalogAuthored: registry != null, fallbackDefinitionsAvailable: true, revision: 1);
            HostId = string.IsNullOrWhiteSpace(hostId) ? "host.scene-independent.fresh-runtime" : hostId;
        }

        public string HostId { get; }

        public TestLabAutomationHostCapabilities GetCapabilities()
        {
            return new TestLabAutomationHostCapabilities(
                HostId,
                "Scene-Independent Fresh Runtime Host",
                "scene.none",
                TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice | TestLabRuntimeArea.Quests,
                TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice | TestLabRuntimeArea.Quests,
                TestLabRuntimeArea.None,
                TestLabRuntimeArea.None,
                TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice | TestLabRuntimeArea.Quests,
                new[] { TestLabScenarioIsolationMode.FreshRuntime, TestLabScenarioIsolationMode.PersistentFixture },
                TestLabHostFeature.DefinitionContext
                    | TestLabHostFeature.DeterministicTime
                    | TestLabHostFeature.FixtureFingerprinting
                    | TestLabHostFeature.AutomatedExecution
                    | TestLabHostFeature.DevelopmentOnly,
                new[] { "Constructs isolated knowledge/history runtimes without scene objects." });
        }

        public TestLabDefinitionContext GetDefinitionContext()
        {
            return definitionContext;
        }

        public TestLabRuntimeBundleResult CreateRuntimeBundle(TestLabRuntimeBundleRequest request)
        {
            TestLabAutomationHostCapabilities capabilities = GetCapabilities();
            TestLabAutomationStepResult validation = TestLabAutomationHostValidation.ValidateHostForScenario(capabilities, request?.IsolationMode ?? TestLabScenarioIsolationMode.FreshRuntime, request?.RequiredRuntimeAreas ?? TestLabRuntimeArea.None, TestLabHostFeature.None);
            if (!validation.Succeeded)
            {
                return TestLabRuntimeBundleResult.Fail(validation.Actual, validation.Diagnostics, capabilities);
            }

            if (definitionContext.Registry == null)
            {
                return TestLabRuntimeBundleResult.Fail("MissingDefinitions", "Scene-independent automation host has no definition registry.", capabilities);
            }

            string personId = string.IsNullOrWhiteSpace(request?.RunId) ? "person.testlab.fresh" : $"person.testlab.{Sanitize(request.RunId)}";
            if (request != null && request.IsolationMode == TestLabScenarioIsolationMode.PersistentFixture)
            {
                string key = $"{request.RunId}:{request.SuiteId}:persistent";
                if (!persistentBundles.TryGetValue(key, out TestLabRuntimeBundle persistent))
                {
                    persistent = TestLabRuntimeBundle.CreateFresh(definitionContext.Registry, personId, PersistenceService.LocalWorldId, new[] { personId }, Array.Empty<string>(), $"Test Lab Persistent {request.SuiteId}");
                    persistentBundles.Add(key, persistent);
                }

                return TestLabRuntimeBundleResult.Success(persistent, null, capabilities);
            }

            TestLabRuntimeBundle bundle = TestLabRuntimeBundle.CreateFresh(definitionContext.Registry, personId, PersistenceService.LocalWorldId, new[] { personId }, Array.Empty<string>());
            return TestLabRuntimeBundleResult.Success(bundle, bundle, capabilities);
        }

        public TestLabEnvironmentSnapshot CaptureEnvironment(TestLabEnvironmentSnapshotRequest request)
        {
            return new TestLabEnvironmentSnapshot(HostId, request?.RequiredRuntimeAreas ?? TestLabRuntimeArea.None, new TestLabRuntimeBundleFingerprint(Array.Empty<TestLabRuntimeFingerprintSection>()));
        }

        public TestLabOperationResult RestoreEnvironment(TestLabEnvironmentSnapshot snapshot)
        {
            return new TestLabOperationResult(true, "Success", "Scene-independent host has no scene environment to restore.");
        }

        public TestLabOperationResult ResetEnvironment(TestLabEnvironmentResetRequest request)
        {
            if (request != null && string.Equals(request.Reason, "Clearing automation run scopes.", StringComparison.Ordinal))
            {
                string prefix = $"{request.RunId}:";
                string[] keys = persistentBundles.Keys.Where(key => string.IsNullOrWhiteSpace(request.RunId) || key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                foreach (string key in keys)
                {
                    persistentBundles[key]?.Dispose();
                    persistentBundles.Remove(key);
                }
            }

            return new TestLabOperationResult(true, "Success", request?.Reason ?? "Scene-independent host reset.");
        }

        public IEnumerable<TestLabRuntimeFingerprintSection> CaptureFingerprint(TestLabRuntimeArea requiredAreas)
        {
            return Array.Empty<TestLabRuntimeFingerprintSection>();
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "run";
            }

            char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '.' && chars[i] != '-')
                {
                    chars[i] = '-';
                }
            }

            return new string(chars).Trim('.', '-');
        }
    }

    public sealed class TestLabAutomationHostBehaviour : MonoBehaviour, ITestLabAutomationHost
    {
        private readonly Dictionary<string, TestLabRuntimeBundle> persistentBundles = new Dictionary<string, TestLabRuntimeBundle>(StringComparer.Ordinal);

        [SerializeField] private string hostId = "host.scene.generic";
        [SerializeField] private string displayName = "Generic Test Lab Host";
        [SerializeField] private DefinitionCatalog definitionCatalog;
        [SerializeField] private TestLabRuntimeArea freshRuntimeAreas = TestLabRuntimeArea.KnowledgeHistory;
        [SerializeField] private TestLabRuntimeArea sharedRuntimeAreas = TestLabRuntimeArea.None;
        [SerializeField] private TestLabRuntimeArea snapshotRestoreAreas = TestLabRuntimeArea.None;
        [SerializeField] private TestLabRuntimeArea persistentFixtureAreas = TestLabRuntimeArea.KnowledgeHistory;
        [SerializeField] private bool supportsSceneReset;
        [SerializeField] private bool supportsPersistence;
        [SerializeField] private bool hasVisibleUi;

        public string HostId => string.IsNullOrWhiteSpace(hostId) ? "host.scene.generic" : hostId;

        private void OnEnable()
        {
            TestLabAutomationHostRegistry.Register(this, out _);
        }

        private void OnDisable()
        {
            TestLabAutomationHostRegistry.Unregister(this);
            foreach (TestLabRuntimeBundle bundle in persistentBundles.Values.ToArray())
            {
                bundle?.Dispose();
            }

            persistentBundles.Clear();
        }

        public TestLabAutomationHostCapabilities GetCapabilities()
        {
            TestLabRuntimeArea effectiveSnapshotRestoreAreas = TestLabRuntimeArea.None;
            TestLabRuntimeArea effectiveSharedRuntimeAreas = TestLabRuntimeArea.None;
            TestLabRuntimeArea supported = freshRuntimeAreas | effectiveSharedRuntimeAreas | effectiveSnapshotRestoreAreas | persistentFixtureAreas;
            List<TestLabScenarioIsolationMode> modes = new List<TestLabScenarioIsolationMode>();
            if (freshRuntimeAreas != TestLabRuntimeArea.None)
            {
                modes.Add(TestLabScenarioIsolationMode.FreshRuntime);
            }

            if (effectiveSnapshotRestoreAreas != TestLabRuntimeArea.None)
            {
                modes.Add(TestLabScenarioIsolationMode.SnapshotRestore);
            }

            if (effectiveSharedRuntimeAreas != TestLabRuntimeArea.None)
            {
                modes.Add(TestLabScenarioIsolationMode.SharedRuntime);
            }

            if (persistentFixtureAreas != TestLabRuntimeArea.None)
            {
                modes.Add(TestLabScenarioIsolationMode.PersistentFixture);
            }

            TestLabHostFeature features = TestLabHostFeature.DefinitionContext
                | TestLabHostFeature.DeterministicTime
                | TestLabHostFeature.FixtureFingerprinting
                | TestLabHostFeature.AutomatedExecution
                | TestLabHostFeature.DevelopmentOnly;
            if (supportsSceneReset)
            {
                features |= TestLabHostFeature.SceneReset;
            }

            if (effectiveSnapshotRestoreAreas != TestLabRuntimeArea.None)
            {
                features |= TestLabHostFeature.SnapshotRestore;
            }

            if (effectiveSharedRuntimeAreas != TestLabRuntimeArea.None)
            {
                features |= TestLabHostFeature.SharedRuntime;
            }

            if (persistentFixtureAreas != TestLabRuntimeArea.None)
            {
                features |= TestLabHostFeature.PersistentFixture;
            }

            if (supportsPersistence)
            {
                features |= TestLabHostFeature.Persistence;
            }

            if (hasVisibleUi)
            {
                features |= TestLabHostFeature.VisibleUi;
            }

            List<string> diagnostics = new List<string>();
            if (sharedRuntimeAreas != TestLabRuntimeArea.None)
            {
                diagnostics.Add($"Configured shared runtime areas '{sharedRuntimeAreas}' are ignored because this generic host has no scene runtime providers yet.");
            }

            if (snapshotRestoreAreas != TestLabRuntimeArea.None)
            {
                diagnostics.Add($"Configured snapshot areas '{snapshotRestoreAreas}' are ignored because this generic host has no complete snapshot provider yet.");
            }

            return new TestLabAutomationHostCapabilities(HostId, displayName, SceneManager.GetActiveScene().name, supported, freshRuntimeAreas, effectiveSnapshotRestoreAreas, effectiveSharedRuntimeAreas, persistentFixtureAreas, modes, features, diagnostics);
        }

        public TestLabDefinitionContext GetDefinitionContext()
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            DefinitionRegistry registry = definitionCatalog == null ? null : definitionCatalog.CreateRegistry(report);
            string[] validationErrors = report.Messages
                .Where(message => message.Severity == DefinitionIdValidationSeverity.Error)
                .Select(message => message.Message)
                .ToArray();
            string[] diagnostics = definitionCatalog == null
                ? new[] { "No definition catalog is assigned." }
                : new[] { $"Catalog='{definitionCatalog.CatalogId}' Version='{definitionCatalog.ContentVersion}' Definitions={registry.Count} Warnings={report.WarningCount}." };
            return new TestLabDefinitionContext(
                registry,
                HostId + ".definitions",
                displayName,
                definitionCatalog != null,
                fallbackDefinitionsAvailable: false,
                revision: registry?.Count ?? 0,
                diagnostics,
                validationErrors);
        }

        public TestLabRuntimeBundleResult CreateRuntimeBundle(TestLabRuntimeBundleRequest request)
        {
            TestLabAutomationHostCapabilities capabilities = GetCapabilities();
            TestLabAutomationStepResult validation = TestLabAutomationHostValidation.ValidateHostForScenario(capabilities, request?.IsolationMode ?? TestLabScenarioIsolationMode.FreshRuntime, request?.RequiredRuntimeAreas ?? TestLabRuntimeArea.None, TestLabHostFeature.None);
            if (!validation.Succeeded)
            {
                return TestLabRuntimeBundleResult.Fail(validation.Actual, validation.Diagnostics, capabilities);
            }

            TestLabDefinitionContext definitions = GetDefinitionContext();
            if (definitions.Registry == null)
            {
                return TestLabRuntimeBundleResult.Fail("MissingDefinitions", $"Host '{HostId}' has no definition catalog.", capabilities);
            }

            if (request != null && request.IsolationMode == TestLabScenarioIsolationMode.FreshRuntime)
            {
                TestLabRuntimeBundle fresh = TestLabRuntimeBundle.CreateFresh(definitions.Registry, $"person.{HostId}.fresh", PersistenceService.LocalWorldId, Array.Empty<string>(), Array.Empty<string>());
                return TestLabRuntimeBundleResult.Success(fresh, fresh, capabilities);
            }

            if (request != null && request.IsolationMode == TestLabScenarioIsolationMode.PersistentFixture)
            {
                string key = $"{request.RunId}:{request.SuiteId}:persistent";
                if (!persistentBundles.TryGetValue(key, out TestLabRuntimeBundle persistent))
                {
                    persistent = TestLabRuntimeBundle.CreateFresh(definitions.Registry, $"person.{HostId}.persistent", PersistenceService.LocalWorldId, Array.Empty<string>(), Array.Empty<string>(), $"Test Lab Generic Persistent {request.SuiteId}");
                    persistentBundles.Add(key, persistent);
                }

                return TestLabRuntimeBundleResult.Success(persistent, null, capabilities);
            }

            return TestLabRuntimeBundleResult.Fail("UnsupportedRuntimeConstruction", $"Host '{HostId}' does not expose concrete shared scene runtime providers yet.", capabilities);
        }

        public TestLabEnvironmentSnapshot CaptureEnvironment(TestLabEnvironmentSnapshotRequest request)
        {
            return new TestLabEnvironmentSnapshot(HostId, request?.RequiredRuntimeAreas ?? TestLabRuntimeArea.None, new TestLabRuntimeBundleFingerprint(CaptureFingerprint(request?.RequiredRuntimeAreas ?? TestLabRuntimeArea.None)));
        }

        public TestLabOperationResult RestoreEnvironment(TestLabEnvironmentSnapshot snapshot)
        {
            bool matchingHost = snapshot == null || string.Equals(snapshot.HostId, HostId, StringComparison.Ordinal);
            return matchingHost
                ? new TestLabOperationResult(true, "Success", "Generic host has no mutable scene restore payload.")
                : new TestLabOperationResult(false, "HostMismatch", $"Snapshot belongs to host '{snapshot.HostId}', not '{HostId}'.");
        }

        public TestLabOperationResult ResetEnvironment(TestLabEnvironmentResetRequest request)
        {
            if (request != null && string.Equals(request.Reason, "Clearing automation run scopes.", StringComparison.Ordinal))
            {
                string prefix = $"{request.RunId}:";
                string[] keys = persistentBundles.Keys.Where(key => string.IsNullOrWhiteSpace(request.RunId) || key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                foreach (string key in keys)
                {
                    persistentBundles[key]?.Dispose();
                    persistentBundles.Remove(key);
                }
            }

            return supportsSceneReset
                ? new TestLabOperationResult(true, "Success", request?.Reason ?? "Generic scene host reset.")
                : new TestLabOperationResult(false, "UnsupportedSceneReset", $"Host '{HostId}' does not declare scene reset support.");
        }

        public IEnumerable<TestLabRuntimeFingerprintSection> CaptureFingerprint(TestLabRuntimeArea requiredAreas)
        {
            yield return TestLabRuntimeFingerprintSection.FromText($"Host.{HostId}", 0L, GetCapabilities().ToDiagnostic());
        }
    }

    public static class TestLabAutomationHostValidation
    {
        public static TestLabAutomationStepResult ValidateHostForScenario(
            TestLabAutomationHostCapabilities capabilities,
            TestLabScenarioIsolationMode isolationMode,
            TestLabRuntimeArea requiredAreas,
            TestLabHostFeature requiredFeatures)
        {
            List<string> errors = new List<string>();
            if (capabilities == null)
            {
                errors.Add("No Test Lab automation host capability snapshot was provided.");
                return Failure("MissingCapabilities", isolationMode, requiredAreas, requiredFeatures, errors, null);
            }

            if (string.IsNullOrWhiteSpace(capabilities.HostId))
            {
                errors.Add("Host has no stable host ID.");
            }

            if (!capabilities.SupportsAutomatedExecution)
            {
                errors.Add($"Host '{capabilities.HostId}' does not declare automated execution support.");
            }

            if (!capabilities.SupportsIsolation(isolationMode))
            {
                errors.Add($"Host '{capabilities.HostId}' does not support isolation mode '{isolationMode}'. Supported: {string.Join(",", capabilities.SupportedIsolationModes)}.");
            }

            if (!capabilities.SupportsRuntimeAreas(isolationMode, requiredAreas))
            {
                errors.Add($"Host '{capabilities.HostId}' does not support runtime areas '{requiredAreas}' for isolation mode '{isolationMode}'. {capabilities.ToDiagnostic()}.");
            }

            if (!capabilities.HasFeatures(requiredFeatures))
            {
                errors.Add($"Host '{capabilities.HostId}' is missing required features '{requiredFeatures}'. Supported: {capabilities.Features}.");
            }

            if (errors.Count > 0)
            {
                return Failure("IncompatibleHost", isolationMode, requiredAreas, requiredFeatures, errors, capabilities);
            }

            return TestLabAssertions.Pass("host.validation", "Automation host validation", $"Host '{capabilities.HostId}' supports {isolationMode} for {requiredAreas}. Features={capabilities.Features}.");
        }

        public static TestLabAutomationStepResult ResolutionFailure(TestLabAutomationHostResolution resolution, string suiteId, string scenarioId)
        {
            return TestLabAssertions.Fail(
                "host.validation",
                "Automation host validation",
                "CompatibleHost",
                "Available",
                resolution?.FailureCode ?? "NoHost",
                $"Suite={suiteId} Scenario={scenarioId}. {resolution?.Message ?? "No Test Lab automation host is registered for the active scene."}");
        }

        private static TestLabAutomationStepResult Failure(
            string code,
            TestLabScenarioIsolationMode isolationMode,
            TestLabRuntimeArea requiredAreas,
            TestLabHostFeature requiredFeatures,
            IEnumerable<string> errors,
            TestLabAutomationHostCapabilities capabilities)
        {
            string diagnostics = $"Code={code} Host={capabilities?.HostId ?? "None"} Scene={capabilities?.SceneName ?? "None"} Isolation={isolationMode} RequiredAreas={requiredAreas} RequiredFeatures={requiredFeatures}. {string.Join(" | ", errors ?? Array.Empty<string>())}";
            return TestLabAssertions.Fail("host.validation", "Automation host validation", "CompatibleHost", "Succeeded", code, diagnostics);
        }
    }

    public sealed class TestLabAutomationHostResetCoordinator : ITestLabAutomationResetCoordinator
    {
        public TestLabAutomationStepResult Reset(TestLabAutomationContext context, string reason)
        {
            if (context?.Host == null)
            {
                context?.EventCapture?.Clear();
                return TestLabAssertions.Pass("reset", "Reset runtime state", reason ?? "No scene host reset required.");
            }

            TestLabOperationResult result = context.Host.ResetEnvironment(new TestLabEnvironmentResetRequest(
                context.RunId,
                context.CurrentSuiteId,
                context.CurrentScenarioId,
                reason,
                context.ScenarioContext?.RequiredRuntimeAreas ?? TestLabRuntimeArea.None));
            context.EventCapture?.Clear();
            return result.Succeeded
                ? TestLabAssertions.Pass("reset", "Reset runtime state", result.Message)
                : TestLabAssertions.Fail("reset", "Reset runtime state", "ResetSucceeded", "Succeeded", result.Code, result.Message);
        }
    }
}
#endif
