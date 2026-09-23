using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Development.Automation;
using UnityIsekaiGame.Development.Automation.Fixtures.History;
using UnityIsekaiGame.Editor.Tools.TestLabAutomation;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityEditor;

namespace UnityIsekaiGame.Tests
{
    public sealed class TestLabAutomationFrameworkTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [TearDown]
        public void TearDown()
        {
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void SuiteRegistration_IsDeterministic()
        {
            TestLabAutomationRegistry registry = new TestLabAutomationRegistry();
            Assert.That(registry.TryRegister(Suite("suite.b", 20, Scenario("scenario.b", 20, PassStep("b"))), out _), Is.True);
            Assert.That(registry.TryRegister(Suite("suite.a", 10, Scenario("scenario.a", 10, PassStep("a"))), out _), Is.True);

            Assert.That(registry.Suites.Select(suite => suite.SuiteId), Is.EqualTo(new[] { "suite.a", "suite.b" }));
        }

        [Test]
        public void DuplicateSuiteIds_AreRejected()
        {
            TestLabAutomationRegistry registry = new TestLabAutomationRegistry();
            Assert.That(registry.TryRegister(Suite("suite.duplicate", 10, Scenario("scenario.one", 10, PassStep("one"))), out _), Is.True);

            bool registered = registry.TryRegister(Suite("suite.duplicate", 20, Scenario("scenario.two", 10, PassStep("two"))), out string failure);

            Assert.That(registered, Is.False);
            Assert.That(failure, Does.Contain("Duplicate suite ID"));
        }

        [Test]
        public void DuplicateScenarioIds_AreValidationErrors()
        {
            TestLabAutomationRegistry registry = Registry(Suite("suite", 10,
                Scenario("scenario.same", 10, PassStep("one")),
                Scenario("scenario.same", 20, PassStep("two"))));

            TestLabAutomationValidationResult result = TestLabAutomationValidation.Validate(registry);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Any(error => error.Contains("duplicate scenario ID")), Is.True);
        }

        [Test]
        public void SelectedScenario_RunsOnlyOnce()
        {
            int count = 0;
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("scenario.one", 10, CountStep("one", () => count++)),
                Scenario("scenario.two", 20, CountStep("two", () => count++)))));

            TestLabAutomationResult result = runner.RunScenario("suite", "scenario.two", TestLabAutomationOptions.Default);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(result.Scenarios.Single().ScenarioId, Is.EqualTo("scenario.two"));
        }

        [Test]
        public void CurrentSuite_RunsScenariosInOrder()
        {
            List<string> order = new List<string>();
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("scenario.b", 20, CountStep("b", () => order.Add("b"))),
                Scenario("scenario.a", 10, CountStep("a", () => order.Add("a"))))));

            runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(order, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void RunAll_RespectsSuiteOrdering()
        {
            List<string> order = new List<string>();
            TestLabAutomationRunner runner = Runner(Registry(
                Suite("suite.b", 20, Scenario("scenario", 10, CountStep("b", () => order.Add("b")))),
                Suite("suite.a", 10, Scenario("scenario", 10, CountStep("a", () => order.Add("a"))))));

            runner.RunAll(quickOnly: false, TestLabAutomationOptions.Default);

            Assert.That(order, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void ScenarioOrder_CanRunReverse()
        {
            List<string> order = new List<string>();
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("scenario.a", 10, CountStep("a", () => order.Add("a"))),
                Scenario("scenario.b", 20, CountStep("b", () => order.Add("b"))))));

            TestLabAutomationResult result = runner.RunSuite("suite", new TestLabAutomationOptions { ScenarioOrder = TestLabAutomationScenarioOrder.Reverse });

            Assert.That(order, Is.EqualTo(new[] { "b", "a" }));
            Assert.That(result.ScenarioOrder, Is.EqualTo(TestLabAutomationScenarioOrder.Reverse));
        }

        [Test]
        public void ScenarioOrder_CanRunSeededShuffleDeterministically()
        {
            TestLabAutomationRunner first = Runner(Registry(Suite("suite", 10,
                Scenario("scenario.a", 10, PassStep("a")),
                Scenario("scenario.b", 20, PassStep("b")),
                Scenario("scenario.c", 30, PassStep("c")))));
            TestLabAutomationRunner second = Runner(Registry(Suite("suite", 10,
                Scenario("scenario.a", 10, PassStep("a")),
                Scenario("scenario.b", 20, PassStep("b")),
                Scenario("scenario.c", 30, PassStep("c")))));
            TestLabAutomationOptions options = new TestLabAutomationOptions { ScenarioOrder = TestLabAutomationScenarioOrder.Shuffled, ShuffleSeed = 12345 };

            string[] firstOrder = first.RunSuite("suite", options).Scenarios.Select(scenario => scenario.ScenarioId).ToArray();
            string[] secondOrder = second.RunSuite("suite", options).Scenarios.Select(scenario => scenario.ScenarioId).ToArray();

            Assert.That(firstOrder, Is.EqualTo(secondOrder));
            Assert.That(first.RunSuite("suite", options).ShuffleSeed, Is.EqualTo(12345));
        }

        [Test]
        public void RunAllQuick_RunsOnlyQuickScenarios()
        {
            int quick = 0;
            int standard = 0;
            ITestLabAutomationScenario quickScenario = new TestLabAutomationScenario("quick", "quick", "quick", 10, TestLabAutomationCategory.Quick, true, new[] { CountStep("quick", () => quick++) });
            ITestLabAutomationScenario standardScenario = new TestLabAutomationScenario("standard", "standard", "standard", 20, TestLabAutomationCategory.Standard, false, new[] { CountStep("standard", () => standard++) });
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10, quickScenario, standardScenario)));

            TestLabAutomationResult result = runner.RunAll(quickOnly: true, TestLabAutomationOptions.Default);

            Assert.That(result.TotalScenarios, Is.EqualTo(1));
            Assert.That(quick, Is.EqualTo(1));
            Assert.That(standard, Is.Zero);
        }

        [Test]
        public void RerunFailed_RunsOnlyPreviouslyFailedScenarios()
        {
            int passedCount = 0;
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("fail", 10, FailStep("fail")),
                Scenario("pass", 20, CountStep("pass", () => passedCount++)))));

            runner.RunSuite("suite", new TestLabAutomationOptions { StopOnFirstFailure = false });
            TestLabAutomationResult rerun = runner.RerunFailed(TestLabAutomationOptions.Default);

            Assert.That(rerun.TotalScenarios, Is.EqualTo(1));
            Assert.That(rerun.Scenarios.Single().ScenarioId, Is.EqualTo("fail"));
            Assert.That(passedCount, Is.EqualTo(1));
        }

        [Test]
        public void StopOnFirstFailure_StopsSubsequentScenarios()
        {
            int count = 0;
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("fail", 10, FailStep("fail")),
                Scenario("pass", 20, CountStep("pass", () => count++)))));

            TestLabAutomationResult result = runner.RunSuite("suite", new TestLabAutomationOptions { StopOnFirstFailure = true });

            Assert.That(result.Scenarios[0].Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios[1].Status, Is.EqualTo(TestLabAutomationStatus.Cancelled));
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void ContinueOnFailure_RunsRemainingScenarios()
        {
            int count = 0;
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("fail", 10, FailStep("fail")),
                Scenario("pass", 20, CountStep("pass", () => count++)))));

            TestLabAutomationResult result = runner.RunSuite("suite", new TestLabAutomationOptions { StopOnFirstFailure = false });

            Assert.That(result.Scenarios[0].Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios[1].Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void FailedAssertion_ReportsExpectedAndActualValues()
        {
            TestLabAutomationStepResult result = TestLabAssertions.Equal("equal", "Equal", 1, 2, "numbers");

            Assert.That(result.Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Expected, Is.EqualTo("1"));
            Assert.That(result.Actual, Is.EqualTo("2"));
            Assert.That(result.Diagnostics, Is.EqualTo("numbers"));
        }

        [Test]
        public void ValidationAssertions_ReportSucceededAndFailedStates()
        {
            TestLabAutomationValidationResult success = new TestLabAutomationValidationResult(Array.Empty<string>(), Array.Empty<string>());
            TestLabAutomationValidationResult failure = new TestLabAutomationValidationResult(new[] { "bad" }, Array.Empty<string>());

            Assert.That(TestLabAssertions.ValidationSucceeded("valid", "Valid", success).Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(TestLabAssertions.ValidationFailed("invalid", "Invalid", failure).Status, Is.EqualTo(TestLabAutomationStatus.Passed));
        }

        [Test]
        public void CountAndSequenceAssertions_ReportExpectedResults()
        {
            Assert.That(TestLabAssertions.Count("count", "Count", 2, new[] { "a", "b" }).Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(TestLabAssertions.SequenceEqual("sequence", "Sequence", new[] { 1, 2 }, new[] { 1, 2 }).Status, Is.EqualTo(TestLabAutomationStatus.Passed));
        }

        [Test]
        public void UnexpectedException_ProducesErrorStatus()
        {
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("throws", 10, new TestLabScenarioStep("throw", "Throw", _ => throw new InvalidOperationException("boom"))))));

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Error));
            Assert.That(result.Scenarios.Single().Steps.Any(step => step.ExceptionMessage == "boom"), Is.True);
        }

        [Test]
        public void Cleanup_RunsAfterPassFailureAndException()
        {
            int cleanupCount = 0;
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("pass", 10, PassStep("pass"), cleanup: _ => { cleanupCount++; return TestLabAssertions.Pass("cleanup", "Cleanup"); }),
                Scenario("fail", 20, FailStep("fail"), cleanup: _ => { cleanupCount++; return TestLabAssertions.Pass("cleanup", "Cleanup"); }),
                Scenario("error", 30, new TestLabScenarioStep("error", "Error", _ => throw new InvalidOperationException()), cleanup: _ => { cleanupCount++; return TestLabAssertions.Pass("cleanup", "Cleanup"); }))));

            runner.RunSuite("suite", new TestLabAutomationOptions { StopOnFirstFailure = false });

            Assert.That(cleanupCount, Is.EqualTo(3));
        }

        [Test]
        public void Cancellation_MarksRemainingScenarios()
        {
            TestLabAutomationRunner runner = null;
            runner = Runner(Registry(Suite("suite", 10,
                Scenario("cancel", 10, new TestLabScenarioStep("cancel", "Cancel", _ => { runner.Cancel(); return TestLabAssertions.Pass("cancel", "Cancel"); })),
                Scenario("remaining", 20, PassStep("remaining")))));

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios[0].Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(result.Scenarios[1].Status, Is.EqualTo(TestLabAutomationStatus.Cancelled));
        }

        [Test]
        public void TransactionIds_AreDeterministic()
        {
            TestLabAutomationTransactionIds ids = new TestLabAutomationTransactionIds();

            string first = ids.Create("suite.id", "scenario.id", "run-0001", 3, "execute");
            string second = ids.Create("suite.id", "scenario.id", "run-0001", 3, "execute");

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("suite.id"));
            Assert.That(first, Does.Contain("scenario.id"));
            Assert.That(first, Does.Contain("step-003"));
        }

        [Test]
        public void EventCapture_PreservesOrderAndRemovesSubscriptions()
        {
            int unsubscribeCount = 0;
            using (TestLabAutomationEventCapture capture = new TestLabAutomationEventCapture())
            {
                capture.AddSubscription(() => unsubscribeCount++);
                capture.Record("first");
                capture.Record("second");

                Assert.That(capture.OccurredBefore("first", "second"), Is.True);
                Assert.That(capture.HasEvent("first"), Is.True);
                Assert.That(capture.HasNoEvent("missing"), Is.True);
            }

            Assert.That(unsubscribeCount, Is.EqualTo(1));
        }

        [Test]
        public void BaselineReset_IsolatesTrackedRuntimeBuckets()
        {
            FakeResetCoordinator reset = new FakeResetCoordinator();
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10, Scenario("scenario", 10, PassStep("pass")))), reset);

            runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(reset.ResourceResets, Is.EqualTo(2));
            Assert.That(reset.LifecycleResets, Is.EqualTo(2));
            Assert.That(reset.CombatStateResets, Is.EqualTo(2));
            Assert.That(reset.DefenseResets, Is.EqualTo(2));
            Assert.That(reset.ExecutionResets, Is.EqualTo(2));
        }

        [Test]
        public void ScenarioScope_IsCreatedForEveryAutomationScenario()
        {
            TestLabScenarioContext captured = null;
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("scenario", 10, new TestLabScenarioStep("capture", "Capture", context =>
                {
                    captured = context.ScenarioContext;
                    return TestLabAssertions.Pass("capture", "Capture");
                })))));

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.IsolationMode, Is.EqualTo(TestLabScenarioIsolationMode.FreshRuntime));
            Assert.That(captured.Namespace, Does.Contain("suite"));
            Assert.That(captured.Namespace, Does.Contain("scenario"));
        }

        [Test]
        public void ScenarioIsolationMode_CanBeDeclaredExplicitly()
        {
            TestLabSceneIndependentAutomationHost host = new TestLabSceneIndependentAutomationHost(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "host.test.persistent");
            Assert.That(TestLabAutomationHostRegistry.Register(host, out string failure), Is.True, failure);
            TestLabScenarioContext captured = null;
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "scenario",
                "scenario",
                "scenario",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[]
                {
                    new TestLabScenarioStep("capture", "Capture", context =>
                    {
                        captured = context.ScenarioContext;
                        return TestLabAssertions.Pass("capture", "Capture");
                    })
                },
                isolationMode: TestLabScenarioIsolationMode.PersistentFixture,
                requiredHostId: "host.test.persistent");
            TestLabAutomationRunner runner = new TestLabAutomationRunner(Registry(Suite("suite", 10, scenario)), new TestLabAutomationHostResetCoordinator());

            runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.IsolationMode, Is.EqualTo(TestLabScenarioIsolationMode.PersistentFixture));
        }

        [Test]
        public void ScopedFixtureIds_AreRunScopedAndDeterministic()
        {
            TestLabScenarioContext context = new TestLabScenarioContext("run-0001", "suite.id", "scenario.id", TestLabScenarioIsolationMode.FreshRuntime, null);

            string first = context.ScopedId("memory", "hidden witness");
            string second = context.ScopedId("memory", "hidden witness");

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.StartWith("memory.fixture.suite.id.scenario.id.run-0001."));
            Assert.That(first, Does.EndWith("hidden-witness"));
        }

        [Test]
        public void FixtureLedger_ReusesEquivalentRecordsAndRejectsConflicts()
        {
            TestLabFixtureOwnershipLedger ledger = new TestLabFixtureOwnershipLedger();

            TestLabFixtureHandle created = ledger.EnsureEquivalent("fixture.one", "record", "record.same", "owner=a;subject=b", exists: false);
            TestLabFixtureHandle reused = ledger.EnsureEquivalent("fixture.one", "record", "record.same", "owner=a;subject=b", exists: true, actualSignature: "owner=a;subject=b");
            TestLabFixtureHandle conflict = ledger.EnsureEquivalent("fixture.two", "record", "record.same", "owner=c;subject=d", exists: true, actualSignature: "owner=c;subject=d");

            Assert.That(created.Outcome, Is.EqualTo(TestLabFixtureEnsureOutcome.Created));
            Assert.That(reused.Outcome, Is.EqualTo(TestLabFixtureEnsureOutcome.ReusedEquivalent));
            Assert.That(conflict.Outcome, Is.EqualTo(TestLabFixtureEnsureOutcome.Conflict));
            Assert.That(ledger.HasConflicts, Is.True);
        }

        [Test]
        public void FixtureRegistry_PreflightDetectsMissingDependencies()
        {
            TestLabScenarioContext context = new TestLabScenarioContext("run-0001", "suite", "scenario", TestLabScenarioIsolationMode.FreshRuntime, null);
            Assert.That(context.Fixtures.TryRegister(new TestLabFixtureProvider("fixture.owner", new[] { "fixture.missing" }, _ =>
                new TestLabFixtureHandle("fixture.owner", "record", "record.owner", "signature", TestLabFixtureEnsureOutcome.Created)), out _), Is.True);

            TestLabAutomationStepResult result = context.Preflight();

            Assert.That(result.Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Diagnostics, Does.Contain("fixture.missing"));
        }

        [Test]
        public void FixtureRegistry_RejectsDuplicateProvidersAndDetectsCycles()
        {
            TestLabScenarioContext context = new TestLabScenarioContext("run-0001", "suite", "scenario", TestLabScenarioIsolationMode.FreshRuntime, null);
            ITestLabFixtureProvider providerA = new TestLabFixtureProvider("fixture.a", new[] { "fixture.b" }, _ =>
                new TestLabFixtureHandle("fixture.a", "record", "record.a", "a", TestLabFixtureEnsureOutcome.Created));
            ITestLabFixtureProvider providerB = new TestLabFixtureProvider("fixture.b", new[] { "fixture.a" }, _ =>
                new TestLabFixtureHandle("fixture.b", "record", "record.b", "b", TestLabFixtureEnsureOutcome.Created));

            Assert.That(context.Fixtures.TryRegister(providerA, out string firstFailure), Is.True, firstFailure);
            Assert.That(context.Fixtures.TryRegister(providerA, out string duplicateFailure), Is.False);
            Assert.That(duplicateFailure, Does.Contain("Duplicate"));
            Assert.That(context.Fixtures.TryRegister(providerB, out string secondFailure), Is.True, secondFailure);

            TestLabAutomationStepResult result = context.Preflight();

            Assert.That(result.Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Diagnostics, Does.Contain("cycle"));
        }

        [Test]
        public void SnapshotRestore_RestoresBeforeIntegrityCheck()
        {
            TestLabScenarioContext context = new TestLabScenarioContext("run-0001", "suite", "scenario", TestLabScenarioIsolationMode.SnapshotRestore, null);

            TestLabAutomationStepResult audit = context.AuditMutationsBeforeRestore();
            TestLabAutomationStepResult restore = context.RestoreIsolation();
            TestLabAutomationStepResult integrity = context.VerifyRestoredBaseline();

            Assert.That(audit.Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(restore.Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(integrity.Status, Is.EqualTo(TestLabAutomationStatus.Passed));
        }

        [Test]
        public void SnapshotRestore_AuditsUndeclaredMutationBeforeRestore()
        {
            DefinitionRegistry registry = LoadRegistry();
            TestLabRuntimeBundle bundle = TestLabRuntimeBundle.CreateFresh(
                registry,
                "person.prototype.fixture-owner",
                "world.fixture",
                new[] { "person.prototype.fixture-owner" },
                new[] { "body.prototype.fixture-body" },
                "Snapshot mutation audit test");
            using TestLabScenarioContext context = new TestLabScenarioContext("run-0001", "suite", "scenario", TestLabScenarioIsolationMode.SnapshotRestore, bundle);

            HistoryOperationResult mutation = bundle.History.RecordEvent(new RecordHistoricalEventRequest
            {
                TransactionId = "test.snapshot.undeclared-mutation",
                EventId = "event.fixture.snapshot.undeclared",
                EventDefinitionId = "history-event.person-participation",
                OccurredAtWorldTime = 1d,
                RecordedAtWorldTime = 1d,
                PrimaryPersonId = bundle.PersonId,
                ParticipantPersonIds = new[] { bundle.PersonId },
                BodyIds = bundle.KnownBodyIds.Take(1).ToArray(),
                Visibility = KnowledgeVisibility.Private,
                SourceSystem = "Test",
                Provenance = "Undeclared direct mutation",
                Payload = new HistoricalEventPayloadData
                {
                    kind = HistoricalEventPayloadKind.Generic,
                    note = "This mutation is intentionally not fixture-owned."
                }
            });

            TestLabAutomationStepResult audit = context.AuditMutationsBeforeRestore();
            TestLabAutomationStepResult restore = context.RestoreIsolation();
            TestLabAutomationStepResult integrity = context.VerifyRestoredBaseline();

            Assert.That(mutation.Succeeded, Is.True, mutation.Message);
            Assert.That(audit.Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(audit.Diagnostics, Does.Contain("Diffs=1"));
            Assert.That(audit.Diagnostics, Does.Contain("OwnedMutations=0"));
            Assert.That(restore.Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(integrity.Status, Is.EqualTo(TestLabAutomationStatus.Passed));
        }

        [Test]
        public void HistoryFixtureProvider_CreatesScopedTypedWitnessMemoryAndReusesEquivalent()
        {
            DefinitionRegistry registry = LoadRegistry();
            TestLabRuntimeBundle bundle = TestLabRuntimeBundle.CreateFresh(
                registry,
                "person.prototype.fixture-owner",
                "world.fixture",
                new[] { "person.prototype.fixture-owner" },
                new[] { "body.prototype.fixture-body" },
                "History fixture provider test");
            using TestLabScenarioContext context = new TestLabScenarioContext("run-0001", "suite", "scenario", TestLabScenarioIsolationMode.FreshRuntime, bundle, bundle);

            TestLabFixtureHandle first = context.Fixtures.Require(TestLabHistoryFixtureProviders.WitnessMemoryFixtureId, context);
            TestLabFixtureHandle second = context.Fixtures.Require(TestLabHistoryFixtureProviders.WitnessMemoryFixtureId, context);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(context.TryGetFixturePayload(TestLabHistoryFixtureProviders.WitnessMemoryFixtureId, out HiddenHistoryFixtureHandle payload), Is.True);
            Assert.That(payload.EventId, Does.StartWith("event.fixture.suite.scenario.run-0001."));
            Assert.That(payload.MemoryId, Does.StartWith("memory.fixture.suite.scenario.run-0001."));
            Assert.That(bundle.History.TryGetEvent(payload.EventId, out _), Is.True);
            Assert.That(bundle.Memory.TryGetMemory(payload.MemoryId, out _), Is.True);
        }

        [Test]
        public void FixtureMutationAudit_FailsScenarioWhenOwnershipConflictsExist()
        {
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10,
                Scenario("scenario", 10, new TestLabScenarioStep("conflict", "Conflict", context =>
                {
                    context.ScenarioContext.Ledger.EnsureEquivalent("fixture.one", "record", "record.same", "signature", exists: false);
                    context.ScenarioContext.Ledger.EnsureEquivalent("fixture.two", "record", "record.same", "signature", exists: true, actualSignature: "signature");
                    return TestLabAssertions.Pass("conflict", "Conflict");
                })))));

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios.Single().Steps.Any(step => step.StepId == "fixture.audit" && step.Status == TestLabAutomationStatus.Failed), Is.True);
        }

        [Test]
        public void PreviewAssertions_DetectMutation()
        {
            int before = 1;
            int after = 2;

            TestLabAutomationStepResult result = TestLabAssertions.RevisionUnchanged("preview", "Preview", before, after);

            Assert.That(result.Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.AssertionType, Is.EqualTo("RevisionUnchanged"));
        }

        [Test]
        public void ReportExport_ProducesJsonOutsideAssets()
        {
            TestLabAutomationResult result = new TestLabAutomationResult("run-test", TestLabAutomationRunMode.SelectedScenario, DateTime.UtcNow, DateTime.UtcNow, false, new[]
            {
                new TestLabScenarioResult("suite", "scenario", "Scenario", TestLabAutomationStatus.Passed, DateTime.UtcNow, DateTime.UtcNow, new[] { TestLabAssertions.Pass("step", "Step") })
            });
            TestLabAutomationReportExporter exporter = new TestLabAutomationReportExporter();

            string path = exporter.ExportJson(result);

            Assert.That(path.Replace('\\', '/'), Does.StartWith("Temp/TestLabAutomation/"));
            Assert.That(path.Replace('\\', '/'), Does.Not.StartWith("Assets/"));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllText(path), Does.Contain("\"runId\": \"run-test\""));
        }

        [Test]
        public void ReportExport_ProducesMarkdownOutsideAssets()
        {
            TestLabAutomationResult result = new TestLabAutomationResult("run-md", TestLabAutomationRunMode.AllSuites, DateTime.UtcNow, DateTime.UtcNow, false, new[]
            {
                new TestLabScenarioResult("suite", "scenario", "Scenario", TestLabAutomationStatus.Passed, DateTime.UtcNow, DateTime.UtcNow, new[] { TestLabAssertions.Pass("step", "Step") })
            });
            TestLabAutomationReportExporter exporter = new TestLabAutomationReportExporter();

            string path = exporter.ExportMarkdown(result);

            Assert.That(path.Replace('\\', '/'), Does.StartWith("Temp/TestLabAutomation/"));
            Assert.That(path.Replace('\\', '/'), Does.Not.StartWith("Assets/"));
            Assert.That(File.ReadAllText(path), Does.Contain("run-md"));
        }

        [Test]
        public void ReportExport_CanWriteBatchJsonAndMarkdownToExplicitPath()
        {
            TestLabAutomationResult result = new TestLabAutomationResult("run-command", TestLabAutomationRunMode.CurrentSuite, DateTime.UtcNow, DateTime.UtcNow, false, new[]
            {
                new TestLabScenarioResult("suite", "scenario", "Scenario", TestLabAutomationStatus.Passed, DateTime.UtcNow, DateTime.UtcNow, new[] { TestLabAssertions.Pass("step", "Step") })
            });
            string outputPath = Path.Combine("Temp", "TestLabAutomation", "command-report");
            TestLabAutomationReportExporter exporter = new TestLabAutomationReportExporter();

            IReadOnlyList<string> paths = exporter.Export(result, TestLabAutomationReportFormat.Both, outputPath: outputPath);

            Assert.That(paths.Select(path => path.Replace('\\', '/')), Is.EquivalentTo(new[]
            {
                "Temp/TestLabAutomation/command-report.json",
                "Temp/TestLabAutomation/command-report.md"
            }));
            Assert.That(paths.All(File.Exists), Is.True);
            Assert.That(File.ReadAllText(paths.Single(path => path.EndsWith(".json", StringComparison.Ordinal))), Does.Contain("\"runId\": \"run-command\""));
            Assert.That(File.ReadAllText(paths.Single(path => path.EndsWith(".md", StringComparison.Ordinal))), Does.Contain("run-command"));
        }

        [Test]
        public void ReportExport_ProducesJUnitXmlForCommandRuns()
        {
            TestLabAutomationResult result = new TestLabAutomationResult("run-junit", TestLabAutomationRunMode.AllSuites, DateTime.UtcNow, DateTime.UtcNow, false, new[]
            {
                new TestLabScenarioResult("suite", "pass", "Pass", TestLabAutomationStatus.Passed, DateTime.UtcNow, DateTime.UtcNow, new[] { TestLabAssertions.Pass("step", "Step") }),
                new TestLabScenarioResult("suite", "fail", "Fail", TestLabAutomationStatus.Failed, DateTime.UtcNow, DateTime.UtcNow, new[] { TestLabAssertions.Fail("step", "Step", "Succeeded", "Succeeded", "Failed", "Failure details.") })
            });
            TestLabAutomationReportExporter exporter = new TestLabAutomationReportExporter();

            string xml = exporter.BuildJUnitXml(result);

            Assert.That(xml, Does.Contain("<testsuite"));
            Assert.That(xml, Does.Contain("tests=\"2\""));
            Assert.That(xml, Does.Contain("<failure"));
            Assert.That(xml, Does.Contain("Failure details."));
        }

        [Test]
        public void ReportExport_ProducesCatalogAndCompatibilityReports()
        {
            TestLabAutomationRegistry registry = PrototypeTestLabAutomationCatalog.CreateDefaultRegistry(9);
            TestLabSuiteCompatibilityReport compatibility = new TestLabSuiteCompatibilityReport(new[]
            {
                new TestLabScenarioCompatibilityResult("suite", "scenario", "Scenario", true, true, "host", string.Empty, "Ready")
            });
            TestLabAutomationReportExporter exporter = new TestLabAutomationReportExporter();

            string catalog = exporter.BuildCatalogJson(registry);
            string compatibilityJson = exporter.BuildCompatibilityJson(compatibility);

            Assert.That(catalog, Does.Contain("\"providers\""));
            Assert.That(catalog, Does.Contain("feature.9.1.item-identity-instance-state"));
            Assert.That(catalog, Does.Not.Contain("feature.8.1.knowledge-facts-beliefs"));
            Assert.That(compatibilityJson, Does.Contain("\"compatible\": true"));
            Assert.That(compatibilityJson, Does.Contain("\"scenarioId\": \"scenario\""));
        }

        [Test]
        public void CommandLineOptions_ParseSuiteModeReportAndDeterminismOptions()
        {
            TestLabAutomationCommandLineOptions options = TestLabAutomationCommandLineOptions.Parse(new[]
            {
                "-testLabMode", "suite",
                "-testLabStep", "9",
                "-testLabSuite", "feature.9.4.item-durability-wear-repair-salvage",
                "-testLabFormat", "junit",
                "-testLabOutput", "Logs/TestLabAutomation/feature-9-4",
                "-testLabOrder", "shuffled",
                "-testLabSeed", "123",
                "-testLabStopOnFail", "true",
                "-testLabExit", "false"
            });

            Assert.That(options.Valid, Is.True);
            Assert.That(options.RunMode, Is.EqualTo(TestLabAutomationRunMode.CurrentSuite));
            Assert.That(options.StepFilter, Is.EqualTo(9));
            Assert.That(options.SuiteId, Is.EqualTo("feature.9.4.item-durability-wear-repair-salvage"));
            Assert.That(options.ReportFormat, Is.EqualTo(TestLabAutomationReportFormat.JUnit));
            Assert.That(options.OutputPath.Replace('\\', '/'), Is.EqualTo("Logs/TestLabAutomation/feature-9-4"));
            Assert.That(options.ScenarioOrder, Is.EqualTo(TestLabAutomationScenarioOrder.Shuffled));
            Assert.That(options.ShuffleSeed, Is.EqualTo(123));
            Assert.That(options.StopOnFirstFailure, Is.True);
            Assert.That(options.ExitUnity, Is.False);
        }

        [Test]
        public void CommandLineOptions_ParseListAndCompatibilityModes()
        {
            TestLabAutomationCommandLineOptions list = TestLabAutomationCommandLineOptions.Parse(new[] { "-testLabMode", "list", "-testLabStep", "8" });
            TestLabAutomationCommandLineOptions compatibility = TestLabAutomationCommandLineOptions.Parse(new[] { "-testLabMode", "compatibility", "-testLabSuite", "feature.9.1.item-identity-instance-state" });

            Assert.That(list.Valid, Is.True);
            Assert.That(list.Action, Is.EqualTo(TestLabAutomationCommandAction.List));
            Assert.That(list.StepFilter, Is.EqualTo(8));
            Assert.That(compatibility.Valid, Is.True);
            Assert.That(compatibility.Action, Is.EqualTo(TestLabAutomationCommandAction.Compatibility));
            Assert.That(compatibility.SuiteId, Is.EqualTo("feature.9.1.item-identity-instance-state"));
        }

        [Test]
        public void CommandLineOptions_RejectScenarioModeWithoutScenario()
        {
            TestLabAutomationCommandLineOptions options = TestLabAutomationCommandLineOptions.Parse(new[]
            {
                "-testLabMode", "scenario",
                "-testLabSuite", "feature.9.4.item-durability-wear-repair-salvage"
            });

            Assert.That(options.Valid, Is.False);
            Assert.That(options.Error, Does.Contain("-testLabScenario"));
        }

        [Test]
        public void BatchCommand_HelpDoesNotRunAutomation()
        {
            TestLabAutomationCommandLineOptions options = TestLabAutomationCommandLineOptions.Parse(new[] { "-testLabHelp" });

            TestLabAutomationBatchCommandResult result = TestLabAutomationBatchCommand.Run(options);

            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.AutomationResult, Is.Null);
            Assert.That(result.Message, Does.Contain("-executeMethod"));
        }

        [Test]
        public void BatchCommand_ListModeExportsCatalogWithoutRunningAutomation()
        {
            string outputPath = Path.Combine("Temp", "TestLabAutomation", "catalog-step9");
            TestLabAutomationCommandLineOptions options = TestLabAutomationCommandLineOptions.Parse(new[]
            {
                "-testLabMode", "list",
                "-testLabStep", "9",
                "-testLabFormat", "json",
                "-testLabOutput", outputPath
            });

            TestLabAutomationBatchCommandResult result = TestLabAutomationBatchCommand.Run(options);

            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.AutomationResult, Is.Null);
            Assert.That(result.ReportPaths.Single().Replace('\\', '/'), Does.EndWith("catalog-step9.json"));
            Assert.That(File.ReadAllText(result.ReportPaths.Single()), Does.Contain("feature.9.4.durability-wear-repair-salvage"));
        }

        [Test]
        public void ImmutableResults_ExposeNoMutableCollections()
        {
            List<TestLabAutomationStepResult> steps = new List<TestLabAutomationStepResult> { TestLabAssertions.Pass("one", "One") };
            TestLabScenarioResult scenario = new TestLabScenarioResult("suite", "scenario", "Scenario", TestLabAutomationStatus.Passed, DateTime.UtcNow, DateTime.UtcNow, steps);
            steps.Add(TestLabAssertions.Pass("two", "Two"));

            Assert.That(scenario.Steps.Count, Is.EqualTo(1));
            Assert.That(scenario.Steps, Is.Not.InstanceOf<List<TestLabAutomationStepResult>>());
        }

        [Test]
        public void ResultTotals_AreAccurate()
        {
            DateTime now = DateTime.UtcNow;
            TestLabAutomationResult result = new TestLabAutomationResult("run", TestLabAutomationRunMode.AllSuites, now, now, false, new[]
            {
                new TestLabScenarioResult("suite", "pass", "Pass", TestLabAutomationStatus.Passed, now, now, new[] { TestLabAssertions.Pass("step", "Step") }),
                new TestLabScenarioResult("suite", "fail", "Fail", TestLabAutomationStatus.Failed, now, now, new[] { TestLabAssertions.Fail("step", "Step", "Equal", 1, 2) }),
                new TestLabScenarioResult("suite", "skip", "Skip", TestLabAutomationStatus.Skipped, now, now, Array.Empty<TestLabAutomationStepResult>())
            });

            Assert.That(result.TotalScenarios, Is.EqualTo(3));
            Assert.That(result.PassedScenarios, Is.EqualTo(1));
            Assert.That(result.FailedScenarios, Is.EqualTo(1));
            Assert.That(result.SkippedScenarios, Is.EqualTo(1));
            Assert.That(result.TotalSteps, Is.EqualTo(2));
        }

        [Test]
        public void MissingOptionalSuite_IsHandledSafely()
        {
            TestLabAutomationRunner runner = Runner(new TestLabAutomationRegistry());

            TestLabAutomationResult result = runner.RunSuite("missing", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios.Single().Steps.Single().Diagnostics, Does.Contain("not registered").IgnoreCase);
        }

        [Test]
        public void MissingScenario_IsHandledSafely()
        {
            TestLabAutomationRunner runner = Runner(Registry(Suite("suite", 10, Scenario("scenario", 10, PassStep("pass")))));

            TestLabAutomationResult result = runner.RunScenario("suite", "missing", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios.Single().Steps.Single().Diagnostics, Does.Contain("not registered").IgnoreCase);
        }

        [Test]
        public void Validation_CatchesMissingDisplayNameNoScenariosAndMissingServices()
        {
            TestLabAutomationRegistry registry = Registry(new TestLabAutomationSuite("suite", "", "test", "test", 10, TestLabAutomationCategory.Quick, true, Array.Empty<string>(), Array.Empty<ITestLabAutomationScenario>()));

            TestLabAutomationValidationResult result = TestLabAutomationValidation.Validate(registry);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Any(error => error.Contains("display name")), Is.True);
            Assert.That(result.Errors.Any(error => error.Contains("no scenarios")), Is.True);
            Assert.That(result.Warnings.Any(warning => warning.Contains("no required service")), Is.True);
        }

        [Test]
        public void Validation_CatchesInvalidScenarioOrderingAndNoSteps()
        {
            TestLabAutomationRegistry registry = Registry(Suite("suite", 10,
                Scenario("late", 20, PassStep("late")),
                new TestLabAutomationScenario("early", "early", "early", 10, TestLabAutomationCategory.Quick, true, Array.Empty<ITestLabScenarioStep>())));

            TestLabAutomationValidationResult result = TestLabAutomationValidation.Validate(registry);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Any(error => error.Contains("deterministic order")), Is.True);
            Assert.That(result.Errors.Any(error => error.Contains("no steps")), Is.True);
        }

        [Test]
        public void Validation_CatchesUnsupportedIsolationMode()
        {
            TestLabAutomationRegistry registry = Registry(Suite("suite", 10,
                new TestLabAutomationScenario("scenario", "scenario", "scenario", 10, TestLabAutomationCategory.Quick, true, new[] { PassStep("pass") }, isolationMode: (TestLabScenarioIsolationMode)999)));

            TestLabAutomationValidationResult result = TestLabAutomationValidation.Validate(registry);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Any(error => error.Contains("unsupported isolation mode")), Is.True);
        }

        [Test]
        public void Validation_CatchesUnsupportedCommandLineSupportMode()
        {
            TestLabAutomationRegistry registry = Registry(Suite("suite", 10,
                new TestLabAutomationScenario(
                    "scenario",
                    "scenario",
                    "scenario",
                    10,
                    TestLabAutomationCategory.Quick,
                    true,
                    new[] { PassStep("pass") },
                    commandLineSupport: (TestLabCommandLineSupport)999)));

            TestLabAutomationValidationResult result = TestLabAutomationValidation.Validate(registry);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Any(error => error.Contains("unsupported command-line support")), Is.True);
        }

        [Test]
        public void Validation_CatchesMissingFixtureRequirements()
        {
            TestLabAutomationRegistry registry = Registry(Suite("suite", 10,
                new TestLabAutomationScenario("scenario", "scenario", "scenario", 10, TestLabAutomationCategory.Quick, true, new[] { PassStep("pass") }, requiredFixtureIds: Array.Empty<string>())));

            TestLabAutomationValidationResult result = TestLabAutomationValidation.Validate(registry);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Any(error => error.Contains("no fixture requirements")), Is.True);
        }

        [Test]
        public void Validation_CatchesUnsupportedIsolatedRuntimeAreas()
        {
            TestLabAutomationRegistry registry = Registry(Suite("suite", 10,
                new TestLabAutomationScenario(
                    "combat-runtime",
                    "combat-runtime",
                    "combat-runtime",
                    10,
                    TestLabAutomationCategory.Quick,
                    true,
                    new[] { PassStep("pass") },
                    isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                    requiredRuntimeAreas: TestLabRuntimeArea.Combat,
                    requiresSceneHost: false)));

            TestLabAutomationValidationResult result = TestLabAutomationValidation.Validate(registry);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Any(error => error.Contains("cannot be isolated automatically")
                || error.Contains("runtime area", StringComparison.OrdinalIgnoreCase)
                || error.Contains("scene host", StringComparison.OrdinalIgnoreCase)), Is.True, string.Join(Environment.NewLine, result.Errors));
        }

        [Test]
        public void Validation_CatchesUnapprovedSharedRuntimeScenarios()
        {
            TestLabAutomationRegistry registry = Registry(Suite("suite", 10,
                new TestLabAutomationScenario(
                    "shared",
                    "shared",
                    "shared",
                    10,
                    TestLabAutomationCategory.Quick,
                    true,
                    new[] { PassStep("pass") },
                    isolationMode: TestLabScenarioIsolationMode.SharedRuntime,
                    requiredRuntimeAreas: TestLabRuntimeArea.Character)));

            TestLabAutomationValidationResult result = TestLabAutomationValidation.Validate(registry);
            TestLabAutomationMigrationInventory inventory = TestLabAutomationValidation.BuildMigrationInventory(registry);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Any(error => error.Contains("temporary shared-runtime migration allowlist")), Is.True);
            Assert.That(inventory.LegacySharedFeatureScenarios, Is.EqualTo(1));
            Assert.That(inventory.LegacySharedScenarioIds.Single(), Is.EqualTo("suite/shared"));
        }

        [Test]
        public void HostRegistry_RegistersResolvesRejectsDuplicateAndAmbiguousHosts()
        {
            TestLabAutomationHostRegistry.ClearForTests();
            DefinitionRegistry definitions = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            TestLabSceneIndependentAutomationHost first = new TestLabSceneIndependentAutomationHost(definitions, "host.test.one");
            TestLabSceneIndependentAutomationHost duplicate = new TestLabSceneIndependentAutomationHost(definitions, "host.test.one");
            TestLabSceneIndependentAutomationHost second = new TestLabSceneIndependentAutomationHost(definitions, "host.test.two");

            Assert.That(TestLabAutomationHostRegistry.Register(first, out string firstFailure), Is.True, firstFailure);
            TestLabAutomationHostResolution active = TestLabAutomationHostRegistry.ResolveActive();
            Assert.That(active.Succeeded, Is.True, active.Message);
            Assert.That(active.Host, Is.SameAs(first));

            Assert.That(TestLabAutomationHostRegistry.Register(duplicate, out string duplicateFailure), Is.False);
            Assert.That(duplicateFailure, Does.Contain("Duplicate"));

            Assert.That(TestLabAutomationHostRegistry.Register(second, out string secondFailure), Is.True, secondFailure);
            TestLabAutomationHostResolution ambiguous = TestLabAutomationHostRegistry.ResolveActive();
            Assert.That(ambiguous.Succeeded, Is.False);
            Assert.That(ambiguous.FailureCode, Is.EqualTo("AmbiguousHost"));

            TestLabAutomationHostResolution exact = TestLabAutomationHostRegistry.ResolveActive("host.test.two");
            Assert.That(exact.Succeeded, Is.True, exact.Message);
            Assert.That(exact.Host, Is.SameAs(second));

            TestLabAutomationHostRegistry.Unregister(first);
            TestLabAutomationHostRegistry.Unregister(second);
            Assert.That(TestLabAutomationHostRegistry.ResolveActive().FailureCode, Is.EqualTo("NoHost"));
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void HostRegistry_DoesNotReturnDestroyedSceneHosts()
        {
            TestLabAutomationHostRegistry.ClearForTests();
            GameObject hostObject = new GameObject("Test Lab Host Registry Lifecycle");
            TestLabAutomationHostBehaviour host = hostObject.AddComponent<TestLabAutomationHostBehaviour>();
            Assert.That(TestLabAutomationHostRegistry.Register(host, out string failure), Is.True, failure);

            Assert.That(TestLabAutomationHostRegistry.ResolveActive().Succeeded, Is.True);

            UnityEngine.Object.DestroyImmediate(hostObject);

            TestLabAutomationHostResolution resolution = TestLabAutomationHostRegistry.ResolveActive();
            Assert.That(resolution.Succeeded, Is.False);
            Assert.That(resolution.FailureCode, Is.EqualTo("NoHost"));
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void FreshRuntime_CanRunWithoutSceneHost()
        {
            TestLabAutomationHostRegistry.ClearForTests();
            TestLabRuntimeBundle captured = null;
            TestLabAutomationRunner runner = new TestLabAutomationRunner(
                Registry(Suite("suite", 10,
                    Scenario("fresh", 10, new TestLabScenarioStep("capture", "Capture", context =>
                    {
                        captured = context.ScenarioContext.Runtimes;
                        Assert.That(context.Host, Is.Null);
                        return TestLabAssertions.Pass("capture", "Capture");
                    })))),
                new FakeResetCoordinator(),
                requiredHostId => TestLabAutomationHostRegistry.ResolveActive(requiredHostId),
                new TestLabDefinitionContext(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "definitions.test", "Test definitions", catalogAuthored: false, fallbackDefinitionsAvailable: false, revision: 0));

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.HasFailures, Is.False, string.Join(Environment.NewLine, result.Scenarios.SelectMany(scenario => scenario.Steps).Select(step => step.Diagnostics)));
            Assert.That(captured, Is.Not.Null);
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void NonPrototypeHost_CanRunCompatiblePersistentFreshSuite()
        {
            TestLabAutomationHostRegistry.ClearForTests();
            DefinitionRegistry definitions = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            TestLabSceneIndependentAutomationHost host = new TestLabSceneIndependentAutomationHost(definitions, "host.test.generic");
            Assert.That(TestLabAutomationHostRegistry.Register(host, out string failure), Is.True, failure);
            TestLabRuntimeBundle first = null;
            TestLabRuntimeBundle second = null;
            ITestLabAutomationScenario firstScenario = new TestLabAutomationScenario(
                "first",
                "first",
                "first",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[] { new TestLabScenarioStep("capture-first", "Capture first", context => { first = context.ScenarioContext.Runtimes; return TestLabAssertions.Pass("capture-first", "Capture first"); }) },
                isolationMode: TestLabScenarioIsolationMode.PersistentFixture,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiredHostId: "host.test.generic");
            ITestLabAutomationScenario secondScenario = new TestLabAutomationScenario(
                "second",
                "second",
                "second",
                20,
                TestLabAutomationCategory.Quick,
                true,
                new[] { new TestLabScenarioStep("capture-second", "Capture second", context => { second = context.ScenarioContext.Runtimes; return TestLabAssertions.Pass("capture-second", "Capture second"); }) },
                isolationMode: TestLabScenarioIsolationMode.PersistentFixture,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiredHostId: "host.test.generic");
            TestLabAutomationRunner runner = new TestLabAutomationRunner(
                Registry(Suite("suite", 10, firstScenario, secondScenario)),
                new TestLabAutomationHostResetCoordinator());

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.HasFailures, Is.False, string.Join(Environment.NewLine, result.Scenarios.SelectMany(scenario => scenario.Steps).Select(step => step.Diagnostics)));
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
            TestLabAutomationHostRegistry.Unregister(host);
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void UnsupportedHost_FailsBeforeScenarioStepRuns()
        {
            TestLabAutomationHostRegistry.ClearForTests();
            DefinitionRegistry definitions = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            TestLabSceneIndependentAutomationHost host = new TestLabSceneIndependentAutomationHost(definitions, "host.test.knowledge-only");
            TestLabAutomationHostRegistry.Register(host, out _);
            int stepRuns = 0;
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "shared-combat",
                "shared-combat",
                "shared-combat",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[] { CountStep("should-not-run", () => stepRuns++) },
                isolationMode: TestLabScenarioIsolationMode.SharedRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Combat,
                requiredHostId: "host.test.knowledge-only");
            TestLabAutomationRunner runner = new TestLabAutomationRunner(Registry(Suite("suite", 10, scenario)), new TestLabAutomationHostResetCoordinator());

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios.Single().Steps.Any(step => step.StepId == "host.compatibility" && step.Actual == "IncompatibleHost"), Is.True);
            Assert.That(stepRuns, Is.Zero);
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void SuiteCompatibilityPreview_BlocksUnsupportedBatchBeforeAnyStepRuns()
        {
            TestLabAutomationHostRegistry.ClearForTests();
            TestLabSceneIndependentAutomationHost host = new TestLabSceneIndependentAutomationHost(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "host.test.knowledge-only");
            TestLabAutomationHostRegistry.Register(host, out _);
            int compatibleStepRuns = 0;
            int incompatibleStepRuns = 0;
            ITestLabAutomationSuite suite = Suite("suite", 10,
                new TestLabAutomationScenario(
                    "compatible",
                    "compatible",
                    "compatible",
                    10,
                    TestLabAutomationCategory.Quick,
                    true,
                    new[] { CountStep("compatible-step", () => compatibleStepRuns++) },
                    isolationMode: TestLabScenarioIsolationMode.PersistentFixture,
                    requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                    requiredHostId: "host.test.knowledge-only"),
                new TestLabAutomationScenario(
                    "unsupported",
                    "unsupported",
                    "unsupported",
                    20,
                    TestLabAutomationCategory.Quick,
                    true,
                    new[] { CountStep("unsupported-step", () => incompatibleStepRuns++) },
                    isolationMode: TestLabScenarioIsolationMode.SharedRuntime,
                    requiredRuntimeAreas: TestLabRuntimeArea.Combat,
                    requiredHostId: "host.test.knowledge-only"));
            TestLabAutomationRunner runner = new TestLabAutomationRunner(Registry(suite), new TestLabAutomationHostResetCoordinator());

            TestLabSuiteCompatibilityReport preview = runner.PreviewCompatibility("suite");
            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(preview.Compatible, Is.False);
            Assert.That(preview.UnsupportedCount, Is.EqualTo(1));
            Assert.That(result.HasFailures, Is.True);
            Assert.That(result.Scenarios.Single().ScenarioId, Is.EqualTo("unsupported"));
            Assert.That(result.Scenarios.Single().Steps.Single().StepId, Is.EqualTo("host.compatibility"));
            Assert.That(compatibleStepRuns, Is.Zero);
            Assert.That(incompatibleStepRuns, Is.Zero);
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void CommandLineRun_SkipsSceneRequiredScenariosWhenNoSceneWasOpened()
        {
            int stepRuns = 0;
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "scene-required",
                "scene-required",
                "scene-required",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[] { CountStep("should-not-run", () => stepRuns++) },
                isolationMode: TestLabScenarioIsolationMode.PersistentFixture,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiredHostId: "host.test.scene");
            TestLabAutomationRunner runner = new TestLabAutomationRunner(Registry(Suite("suite", 10, scenario)), new TestLabAutomationHostResetCoordinator());

            TestLabAutomationResult result = runner.RunSuite("suite", new TestLabAutomationOptions
            {
                RunSurface = TestLabAutomationRunSurface.CommandLine,
                CommandLineSceneAvailable = false
            });

            Assert.That(result.HasFailures, Is.False);
            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Skipped));
            Assert.That(result.Scenarios.Single().Steps.Single().StepId, Is.EqualTo("command-line.support"));
            Assert.That(stepRuns, Is.Zero);
        }

        [Test]
        public void CommandLineRun_RunsSceneRequiredScenariosWhenSceneHostIsAvailable()
        {
            TestLabAutomationHostRegistry.ClearForTests();
            TestLabSceneIndependentAutomationHost host = new TestLabSceneIndependentAutomationHost(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "host.test.scene");
            TestLabAutomationHostRegistry.Register(host, out _);
            int stepRuns = 0;
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "scene-required",
                "scene-required",
                "scene-required",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[] { CountStep("runs", () => stepRuns++) },
                isolationMode: TestLabScenarioIsolationMode.PersistentFixture,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiredHostId: "host.test.scene");
            TestLabAutomationRunner runner = new TestLabAutomationRunner(Registry(Suite("suite", 10, scenario)), new TestLabAutomationHostResetCoordinator());

            TestLabAutomationResult result = runner.RunSuite("suite", new TestLabAutomationOptions
            {
                RunSurface = TestLabAutomationRunSurface.CommandLine,
                CommandLineSceneAvailable = true
            });

            Assert.That(result.HasFailures, Is.False, string.Join(Environment.NewLine, result.Scenarios.SelectMany(item => item.Steps).Select(step => step.Diagnostics)));
            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(stepRuns, Is.EqualTo(1));
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void InGameRun_DoesNotSkipSceneRequiredScenariosWithoutHost()
        {
            int stepRuns = 0;
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "scene-required",
                "scene-required",
                "scene-required",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[] { CountStep("should-not-run", () => stepRuns++) },
                isolationMode: TestLabScenarioIsolationMode.PersistentFixture,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiredHostId: "host.test.scene");
            TestLabAutomationRunner runner = new TestLabAutomationRunner(Registry(Suite("suite", 10, scenario)), new TestLabAutomationHostResetCoordinator());

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios.Single().Steps.Single().StepId, Is.EqualTo("host.compatibility"));
            Assert.That(result.Scenarios.Single().Steps.Single().Actual, Is.EqualTo("HostNotFound"));
            Assert.That(stepRuns, Is.Zero);
        }

        [Test]
        public void HostRemovalDuringExecutionFailsClearlyWithoutSelectingReplacement()
        {
            TestLabAutomationHostRegistry.ClearForTests();
            TestLabSceneIndependentAutomationHost host = new TestLabSceneIndependentAutomationHost(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "host.test.removal");
            TestLabAutomationHostRegistry.Register(host, out _);
            int afterRemovalRuns = 0;
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "host-removal",
                "host-removal",
                "host-removal",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[]
                {
                    new TestLabScenarioStep("remove-host", "Remove host", _ =>
                    {
                        TestLabAutomationHostRegistry.Unregister(host);
                        return TestLabAssertions.Pass("remove-host", "Remove host");
                    }),
                    CountStep("after-removal", () => afterRemovalRuns++)
                },
                isolationMode: TestLabScenarioIsolationMode.PersistentFixture,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiredHostId: "host.test.removal");
            TestLabAutomationRunner runner = new TestLabAutomationRunner(Registry(Suite("suite", 10, scenario)), new TestLabAutomationHostResetCoordinator());

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios.Single().Steps.Any(step => step.StepId == "host.continuity" && step.Actual == "HostRemoved"), Is.True);
            Assert.That(afterRemovalRuns, Is.Zero);
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void HostlessScenarioRequiresExplicitDefinitions()
        {
            int stepRuns = 0;
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "requires-definition",
                "requires-definition",
                "requires-definition",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[] { CountStep("should-not-run", () => stepRuns++) },
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiresSceneHost: false,
                requiredDefinitionIds: new[] { "testlab.required-definition" });
            TestLabAutomationRunner runner = new TestLabAutomationRunner(
                Registry(Suite("suite", 10, scenario)),
                new FakeResetCoordinator(),
                defaultDefinitionContext: DefinitionContext());

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios.Single().Steps.Single().Actual, Is.EqualTo("MissingDefinitions"));
            Assert.That(stepRuns, Is.Zero);
        }

        [Test]
        public void HostlessScenarioRejectsConflictingDefinitionContext()
        {
            int stepRuns = 0;
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "conflicting-definitions",
                "conflicting-definitions",
                "conflicting-definitions",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[] { CountStep("should-not-run", () => stepRuns++) },
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiresSceneHost: false);
            TestLabAutomationRunner runner = new TestLabAutomationRunner(
                Registry(Suite("suite", 10, scenario)),
                new FakeResetCoordinator(),
                defaultDefinitionContext: DefinitionContext(validationErrors: new[] { "Duplicate definition ID 'testlab.conflict'." }));

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Failed));
            Assert.That(result.Scenarios.Single().Steps.Single().Actual, Is.EqualTo("DefinitionConflict"));
            Assert.That(stepRuns, Is.Zero);
        }

        [Test]
        public void CatalogDefinitionContextSatisfiesRequiredDefinition()
        {
            int stepRuns = 0;
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "requires-catalog-definition",
                "requires-catalog-definition",
                "requires-catalog-definition",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[] { CountStep("definition-step", () => stepRuns++) },
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiresSceneHost: false,
                requiredDefinitionIds: new[] { "testlab.catalog-definition" });
            TestLabAutomationRunner runner = new TestLabAutomationRunner(
                Registry(Suite("suite", 10, scenario)),
                new FakeResetCoordinator(),
                defaultDefinitionContext: DefinitionContext(new FakeDefinition("testlab.catalog-definition", "Catalog Definition")));

            TestLabAutomationResult result = runner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(result.HasFailures, Is.False, string.Join(Environment.NewLine, result.Scenarios.SelectMany(item => item.Steps).Select(step => step.Diagnostics)));
            Assert.That(stepRuns, Is.EqualTo(1));
        }

        [Test]
        public void HostlessScenarioResolutionIsIndependentOfRegisteredSceneHosts()
        {
            ITestLabAutomationScenario scenario = new TestLabAutomationScenario(
                "hostless",
                "hostless",
                "hostless",
                10,
                TestLabAutomationCategory.Quick,
                true,
                new[] { PassStep("hostless-step") },
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.KnowledgeHistory,
                requiresSceneHost: false);
            TestLabDefinitionContext definitions = DefinitionContext();
            TestLabAutomationRunner beforeHostRunner = new TestLabAutomationRunner(
                Registry(Suite("suite", 10, scenario)),
                new FakeResetCoordinator(),
                requiredHostId => TestLabAutomationHostRegistry.ResolveActive(requiredHostId),
                definitions);

            TestLabAutomationResult beforeHost = beforeHostRunner.RunSuite("suite", TestLabAutomationOptions.Default);

            TestLabSceneIndependentAutomationHost host = new TestLabSceneIndependentAutomationHost(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "host.test.unused");
            TestLabAutomationHostRegistry.Register(host, out _);
            TestLabAutomationRunner afterHostRunner = new TestLabAutomationRunner(
                Registry(Suite("suite", 10, scenario)),
                new FakeResetCoordinator(),
                requiredHostId => TestLabAutomationHostRegistry.ResolveActive(requiredHostId),
                definitions);

            TestLabAutomationResult afterHost = afterHostRunner.RunSuite("suite", TestLabAutomationOptions.Default);

            Assert.That(beforeHost.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(afterHost.Scenarios.Single().Status, Is.EqualTo(TestLabAutomationStatus.Passed));
            Assert.That(beforeHost.Scenarios.Single().Steps.Any(step => step.StepId == "hostless-step"), Is.True);
            Assert.That(afterHost.Scenarios.Single().Steps.Any(step => step.StepId == "hostless-step"), Is.True);
            TestLabAutomationHostRegistry.ClearForTests();
        }

        [Test]
        public void AutomationCore_DoesNotReferencePrototypeSceneServiceOrView()
        {
            string[] coreFiles =
            {
                "Assets/_Project/Development/TestLab/Automation/TestLabAutomationContracts.cs",
                "Assets/_Project/Development/TestLab/Automation/TestLabAutomationHost.cs",
                "Assets/_Project/Development/TestLab/Automation/TestLabAutomationRunner.cs",
                "Assets/_Project/Development/TestLab/Automation/TestLabAutomationTypes.cs",
                "Assets/_Project/Development/TestLab/Automation/TestLabAutomationValidation.cs",
                "Assets/_Project/Development/TestLab/Automation/TestLabFixtureSystem.cs"
            };
            string joined = string.Join(Environment.NewLine, coreFiles.Select(File.ReadAllText));

            Assert.That(joined, Does.Not.Contain("PrototypeTestLabService"));
            Assert.That(joined, Does.Not.Contain("PrototypeTestLabView"));
            Assert.That(joined, Does.Not.Contain("PrototypeScene"));
        }

        [Test]
        public void GenericDevelopmentHostScene_IsNonPrototypeAndCatalogBacked()
        {
            string scenePath = "Assets/_Project/Scenes/Development/TestLabGenericHostScene.unity";
            Assert.That(File.Exists(scenePath), Is.True);

            string scene = File.ReadAllText(scenePath);
            Assert.That(scene, Does.Contain("host.generic-test-lab"));
            Assert.That(scene, Does.Contain("guid: 3f21ecdbb7904425b456ed3f7fbf5c22"));
            Assert.That(scene, Does.Contain("guid: 357d3d18865946889262f9bf55802d62"));
            Assert.That(scene, Does.Contain("freshRuntimeAreas: 1"));
            Assert.That(scene, Does.Contain("persistentFixtureAreas: 1"));
            Assert.That(scene, Does.Not.Contain("PrototypeTestLabService"));
            Assert.That(scene, Does.Not.Contain("PrototypeTestLabView"));
        }

        [Test]
        public void DefaultPrototypeSuites_RegisterStep3ThroughStep15()
        {
            TestLabAutomationRegistry registry = PrototypeTestLabAutomationCatalog.CreateDefaultRegistry();

            TestLabAutomationValidationResult validation = TestLabAutomationValidation.Validate(registry);
            TestLabAutomationMigrationInventory inventory = TestLabAutomationValidation.BuildMigrationInventory(registry);

            Assert.That(validation.Succeeded, Is.True, string.Join(Environment.NewLine, validation.Errors));
            Assert.That(inventory.TotalScenarios, Is.GreaterThan(0), inventory.ToSummary());
            Assert.That(inventory.LegacySharedFeatureScenarios, Is.Zero, inventory.ToSummary());
            string[] actualSuiteIds = registry.Suites.Select(suite => suite.SuiteId).ToArray();
            Assert.That(actualSuiteIds, Is.EqualTo(PrototypeTestLabAutomationCatalog.SuiteIds()));
            Assert.That(actualSuiteIds.First(), Is.EqualTo("feature.3.runtime-taxonomy"));
            Assert.That(actualSuiteIds.Last(), Is.EqualTo("maintenance.phase-2.prototype-integration-legacy-cleanup"));
            Assert.That(registry.Suites.SelectMany(suite => suite.Scenarios).All(scenario => scenario.IsolationMode == TestLabScenarioIsolationMode.FreshRuntime
                || scenario.RequiredFixtureIds.Contains(TestLabScenarioContext.MutableStateScopeFixtureId)), Is.True);
            Assert.That(registry.Suites.SelectMany(suite => suite.Scenarios).All(scenario => scenario.RequiredFixtureIds.Contains(TestLabScenarioContext.RuntimeBaselineFixtureId)), Is.True);
            Assert.That(registry.Suites.SelectMany(suite => suite.Scenarios).Where(scenario => scenario.IsolationMode == TestLabScenarioIsolationMode.FreshRuntime || scenario.IsolationMode == TestLabScenarioIsolationMode.SnapshotRestore)
                .All(scenario => (scenario.RequiredRuntimeAreas & ~(TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice | TestLabRuntimeArea.WorldLocations | TestLabRuntimeArea.Quests)) == TestLabRuntimeArea.None), Is.True);
        }

        [Test]
        public void PrototypeAutomationCatalog_DiscoversStepProvidersInOrder()
        {
            PrototypeTestLabAutomationProviderDescriptor[] providers = PrototypeTestLabAutomationCatalog.Providers.ToArray();

            Assert.That(providers.Select(provider => provider.Step), Is.EqualTo(new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 99 }));
            Assert.That(providers.Select(provider => provider.Label), Is.EqualTo(new[] { "Runtime Taxonomy", "World Data", "Character", "Combat", "Body", "Knowledge", "Items", "Professions", "Economy", "Social", "Organizations", "World Locations", "Quests", "Maintenance" }));
            Assert.That(providers.Select(provider => provider.Name), Is.EqualTo(new[]
            {
                nameof(PrototypeStep3AutomationSuites),
                nameof(PrototypeStep4AutomationSuites),
                nameof(PrototypeStep5AutomationSuites),
                nameof(PrototypeStep6AutomationSuites),
                nameof(PrototypeStep7AutomationSuites),
                nameof(PrototypeStep8AutomationSuites),
                nameof(PrototypeStep9AutomationSuites),
                nameof(PrototypeStep10AutomationSuites),
                nameof(PrototypeStep11AutomationSuites),
                nameof(PrototypeStep12AutomationSuites),
                nameof(PrototypeStep13AutomationSuites),
                nameof(PrototypeStep14AutomationSuites),
                nameof(PrototypeStep15AutomationSuites),
                nameof(PrototypeStep99MaintenanceAutomationSuites)
            }));
        }

        [Test]
        public void PrototypeAutomationCatalog_ValidatesProvidersAndSupportsStepFiltering()
        {
            PrototypeTestLabAutomationCatalogValidationResult validation = PrototypeTestLabAutomationCatalog.Validate();
            TestLabAutomationRegistry step9 = PrototypeTestLabAutomationCatalog.CreateDefaultRegistry(9);

            Assert.That(validation.Succeeded, Is.True, string.Join(Environment.NewLine, validation.Errors));
            Assert.That(step9.Suites.All(suite => suite.SuiteId.StartsWith("feature.9.", StringComparison.Ordinal)), Is.True);
            Assert.That(step9.Suites.Count, Is.GreaterThan(0));
            Assert.That(PrototypeTestLabAutomationCatalog.DescribeSuites(9).All(suite => suite.Step == 9 && suite.Label == "Items"), Is.True);
        }

        [Test]
        public void AutomationProviderRegistration_OnlyFlowsThroughCatalogOutsideProviders()
        {
            string forbiddenRegistrationCall = ".RegisterDefaults" + "(registry)";
            string[] files = Directory.GetFiles("Assets/_Project", "*.cs", SearchOption.AllDirectories);
            string[] offenders = files
                .Where(path => !path.Replace('\\', '/').EndsWith("PrototypeTestLabAutomationCatalog.cs", StringComparison.Ordinal)
                    && !Path.GetFileName(path).StartsWith("PrototypeStep", StringComparison.Ordinal))
                .Where(path => File.ReadAllText(path).Contains(forbiddenRegistrationCall))
                .ToArray();

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void AutomationSuites_DoNotUseLegacyMutableFixturePatterns()
        {
            string[] suiteFiles = Directory.GetFiles("Assets/_Project/Development/TestLab/Automation", "PrototypeStep*AutomationSuites.cs", SearchOption.AllDirectories);
            string joined = string.Join(Environment.NewLine, suiteFiles.Select(File.ReadAllText));

            Assert.That(joined, Does.Not.Contain("Guid.NewGuid"));
            Assert.That(joined, Does.Not.Contain("FormWitnessHistoryMemory"));
            Assert.That(joined, Does.Not.Contain("memory.prototype."));
            Assert.That(joined, Does.Not.Contain("event.prototype."));
            Assert.That(joined, Does.Not.Contain("record.prototype."));
        }

        [Test]
        public void RuntimeGameplayAssembly_DoesNotReferenceAutomationTypes()
        {
            string gameplayAsmdef = File.ReadAllText("Assets/_Project/Runtime/UnityIsekaiGame.Gameplay.asmdef");

            Assert.That(gameplayAsmdef, Does.Not.Contain("Development"));
            Assert.That(gameplayAsmdef, Does.Not.Contain("Automation"));
        }

        [Test]
        public void DevelopmentAutomation_IsExcludedFromRuntimeAssembly()
        {
            string developmentAsmdef = File.ReadAllText("Assets/_Project/Development/UnityIsekaiGame.Development.asmdef");

            Assert.That(developmentAsmdef, Does.Contain("UnityIsekaiGame.Gameplay"));
            Assert.That(File.Exists("Assets/_Project/Development/TestLab/Automation/TestLabAutomationRunner.cs"), Is.True);
        }

        private static TestLabAutomationRegistry Registry(params ITestLabAutomationSuite[] suites)
        {
            TestLabAutomationRegistry registry = new TestLabAutomationRegistry();
            foreach (ITestLabAutomationSuite suite in suites)
            {
                registry.TryRegister(suite, out _);
            }

            return registry;
        }

        private static DefinitionRegistry LoadRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing prototype catalog at {CatalogPath}.");
            return catalog.CreateRegistry();
        }

        private static TestLabAutomationRunner Runner(TestLabAutomationRegistry registry, ITestLabAutomationResetCoordinator reset = null)
        {
            return new TestLabAutomationRunner(registry, reset ?? new FakeResetCoordinator());
        }

        private static TestLabDefinitionContext DefinitionContext(params IGameDefinition[] definitions)
        {
            return DefinitionContext(definitions, Array.Empty<string>());
        }

        private static TestLabDefinitionContext DefinitionContext(IEnumerable<string> validationErrors)
        {
            return DefinitionContext(Array.Empty<IGameDefinition>(), validationErrors);
        }

        private static TestLabDefinitionContext DefinitionContext(IEnumerable<IGameDefinition> definitions, IEnumerable<string> validationErrors)
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            DefinitionRegistry registry = new DefinitionRegistry(definitions ?? Array.Empty<IGameDefinition>(), report);
            string[] errors = (validationErrors ?? Array.Empty<string>())
                .Concat(report.Messages.Where(message => message.Severity == DefinitionIdValidationSeverity.Error).Select(message => message.Message))
                .ToArray();
            return new TestLabDefinitionContext(registry, "definitions.test", "Test definitions", catalogAuthored: true, fallbackDefinitionsAvailable: false, revision: 1, validationErrors: errors);
        }

        private static ITestLabAutomationSuite Suite(string suiteId, int order, params ITestLabAutomationScenario[] scenarios)
        {
            return new TestLabAutomationSuite(suiteId, suiteId, "test", "test", order, TestLabAutomationCategory.Quick, true, new[] { "fake" }, scenarios);
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, int order, ITestLabScenarioStep step, Func<TestLabAutomationContext, TestLabAutomationStepResult> cleanup = null)
        {
            return new TestLabAutomationScenario(scenarioId, scenarioId, scenarioId, order, TestLabAutomationCategory.Quick, true, new[] { step }, cleanup: cleanup);
        }

        private static ITestLabScenarioStep PassStep(string stepId)
        {
            return new TestLabScenarioStep(stepId, stepId, _ => TestLabAssertions.Pass(stepId, stepId));
        }

        private static ITestLabScenarioStep FailStep(string stepId)
        {
            return new TestLabScenarioStep(stepId, stepId, _ => TestLabAssertions.Fail(stepId, stepId, "Equal", 1, 2));
        }

        private static ITestLabScenarioStep CountStep(string stepId, Action action)
        {
            return new TestLabScenarioStep(stepId, stepId, _ =>
            {
                action();
                return TestLabAssertions.Pass(stepId, stepId);
            });
        }

        private sealed class FakeDefinition : IGameDefinition
        {
            public FakeDefinition(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public string Id { get; }
            public string DisplayName { get; }
        }

        private sealed class FakeResetCoordinator : ITestLabAutomationResetCoordinator
        {
            public int ResourceResets { get; private set; }
            public int LifecycleResets { get; private set; }
            public int CombatStateResets { get; private set; }
            public int DefenseResets { get; private set; }
            public int ExecutionResets { get; private set; }

            public TestLabAutomationStepResult Reset(TestLabAutomationContext context, string reason)
            {
                ResourceResets++;
                LifecycleResets++;
                CombatStateResets++;
                DefenseResets++;
                ExecutionResets++;
                return TestLabAssertions.Pass("reset", "Reset", reason);
            }
        }
    }
}
