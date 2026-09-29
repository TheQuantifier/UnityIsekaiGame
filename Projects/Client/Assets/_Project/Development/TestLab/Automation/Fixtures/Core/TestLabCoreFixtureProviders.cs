#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UnityIsekaiGame.Development.Automation.Fixtures.Core
{
    public static class TestLabCoreFixtureProviders
    {
        public static void RegisterDefaults(TestLabScenarioContext context)
        {
            if (context == null)
            {
                return;
            }

            context.Fixtures.TryRegister(new TestLabFixtureProvider(TestLabScenarioContext.RuntimeBaselineFixtureId, Array.Empty<string>(), PrepareRuntimeBaselineFixture), out _);
            context.Fixtures.TryRegister(new TestLabFixtureProvider(TestLabScenarioContext.MutableStateScopeFixtureId, new[] { TestLabScenarioContext.RuntimeBaselineFixtureId }, PrepareMutableStateScopeFixture), out _);
        }

        private static TestLabFixtureHandle PrepareRuntimeBaselineFixture(TestLabScenarioContext context)
        {
            string signature = context.Runtimes == null
                ? $"suite={context.SuiteId};scenario={context.ScenarioId};mode={context.IsolationMode};runtime=none"
                : $"suite={context.SuiteId};scenario={context.ScenarioId};mode={context.IsolationMode};person={context.Runtimes.PersonId};world={context.Runtimes.WorldId};knowledge={context.Runtimes.Knowledge?.KnowledgeRevision ?? 0};history={context.Runtimes.History?.HistoryRevision ?? 0};memory={context.Runtimes.Memory?.MemoryRevision ?? 0};sources={context.Runtimes.Sources?.SourceRevision ?? 0};transfers={context.Runtimes.Transfers?.TransferRevision ?? 0};access={context.Runtimes.Access?.AccessRevision ?? 0};records={context.Runtimes.Records?.RecordRevision ?? 0}";
            return context.Ledger.EnsureEquivalent(TestLabScenarioContext.RuntimeBaselineFixtureId, "runtime-bundle", context.ScopedId("runtime", "baseline"), signature, exists: false);
        }

        private static TestLabFixtureHandle PrepareMutableStateScopeFixture(TestLabScenarioContext context)
        {
            string signature = $"suite={context.SuiteId};scenario={context.ScenarioId};run={context.RunId};mode={context.IsolationMode};namespace={context.Namespace}";
            return context.Ledger.EnsureEquivalent(TestLabScenarioContext.MutableStateScopeFixtureId, "mutable-scope", context.ScopedId("scope", "mutable-state"), signature, exists: false);
        }
    }
}
#endif
