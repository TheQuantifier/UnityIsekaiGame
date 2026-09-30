#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Development.Automation;

namespace UnityIsekaiGame.Development
{
    public sealed class PrototypeTestLabAutomationHost : ITestLabAutomationHost, ITestLabAutomationScenarioScopeHost
    {
        public const string DefaultHostId = "host.prototype-test-lab";

        private readonly PrototypeTestLabService service;

        public PrototypeTestLabAutomationHost(PrototypeTestLabService service, string hostId = DefaultHostId)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            HostId = string.IsNullOrWhiteSpace(hostId) ? DefaultHostId : hostId.Trim();
        }

        public string HostId { get; }
        public PrototypeTestLabService Service => service;

        public TestLabAutomationHostCapabilities GetCapabilities()
        {
            TestLabRuntimeArea allAreas = TestLabRuntimeArea.KnowledgeHistory
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
                | TestLabRuntimeArea.Justice;
            return new TestLabAutomationHostCapabilities(
                HostId,
                "Prototype Test Lab Host",
                SceneManager.GetActiveScene().name,
                allAreas,
                TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice,
                allAreas,
                allAreas,
                TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice,
                new[]
                {
                    TestLabScenarioIsolationMode.FreshRuntime,
                    TestLabScenarioIsolationMode.SnapshotRestore,
                    TestLabScenarioIsolationMode.SharedRuntime,
                    TestLabScenarioIsolationMode.PersistentFixture
                },
                TestLabHostFeature.DefinitionContext
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
                    | TestLabHostFeature.Persistence,
                new[] { "Uses PrototypeTestLabService as the scene-specific runtime adapter." });
        }

        public TestLabDefinitionContext GetDefinitionContext()
        {
            return service.CreateAutomationDefinitionContext();
        }

        public TestLabRuntimeBundleResult CreateRuntimeBundle(TestLabRuntimeBundleRequest request)
        {
            TestLabAutomationHostCapabilities capabilities = GetCapabilities();
            TestLabAutomationStepResult validation = TestLabAutomationHostValidation.ValidateHostForScenario(capabilities, request?.IsolationMode ?? TestLabScenarioIsolationMode.FreshRuntime, request?.RequiredRuntimeAreas ?? TestLabRuntimeArea.None, TestLabHostFeature.None);
            if (!validation.Succeeded)
            {
                return TestLabRuntimeBundleResult.Fail(validation.Actual, validation.Diagnostics, capabilities);
            }

            TestLabRuntimeBundle bundle = service.CreateAutomationRuntimeBundleForHost(
                request?.RunId,
                request?.SuiteId,
                request?.ScenarioId,
                request?.IsolationMode ?? TestLabScenarioIsolationMode.FreshRuntime,
                out bool contextOwnsRuntime);
            return TestLabRuntimeBundleResult.Success(bundle, contextOwnsRuntime ? bundle : null, capabilities);
        }

        public TestLabEnvironmentSnapshot CaptureEnvironment(TestLabEnvironmentSnapshotRequest request)
        {
            TestLabRuntimeArea areas = request?.RequiredRuntimeAreas ?? TestLabRuntimeArea.None;
            return new TestLabEnvironmentSnapshot(HostId, areas, new TestLabRuntimeBundleFingerprint(CaptureFingerprint(areas)));
        }

        public TestLabOperationResult RestoreEnvironment(TestLabEnvironmentSnapshot snapshot)
        {
            if (snapshot != null && !string.Equals(snapshot.HostId, HostId, StringComparison.Ordinal))
            {
                return new TestLabOperationResult(false, "HostMismatch", $"Snapshot belongs to host '{snapshot.HostId}', not '{HostId}'.");
            }

            return new TestLabOperationResult(true, "Success", "Prototype host restore is handled by scenario fixture snapshots.");
        }

        public TestLabOperationResult ResetEnvironment(TestLabEnvironmentResetRequest request)
        {
            if (request != null && string.Equals(request.Reason, "Clearing automation run scopes.", StringComparison.Ordinal))
            {
                service.ClearAutomationRunScopes(request.RunId);
                return new TestLabOperationResult(true, "Success", "Prototype automation run scopes cleared.");
            }

            PrototypeTestLabOperation result = service.ResetAutomationRuntimeState();
            return new TestLabOperationResult(result.Succeeded, result.Code, result.Message);
        }

        public IEnumerable<TestLabRuntimeFingerprintSection> CaptureFingerprint(TestLabRuntimeArea requiredAreas)
        {
            return service.CaptureAutomationSceneFingerprint(requiredAreas);
        }

        public void SetActiveScenarioContext(TestLabScenarioContext scenarioContext)
        {
            service.SetActiveAutomationScenarioContext(scenarioContext);
        }

        public void ClearActiveScenarioContext(TestLabScenarioContext scenarioContext)
        {
            service.ClearActiveAutomationScenarioContext(scenarioContext);
        }
    }

}

namespace UnityIsekaiGame.Development.Automation
{
    public static class PrototypeTestLabAutomationContextExtensions
    {
        public static PrototypeTestLabService Prototype(this TestLabAutomationContext context)
        {
            PrototypeTestLabAutomationHost host = context?.GetHost<PrototypeTestLabAutomationHost>();
            if (host == null)
            {
                throw new InvalidOperationException("This automation scenario requires the Prototype Test Lab host, but the selected host is not PrototypeTestLabAutomationHost.");
            }

            return host.Service;
        }
    }
}
#endif
