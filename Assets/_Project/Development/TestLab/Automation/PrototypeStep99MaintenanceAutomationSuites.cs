#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.PrototypeIntegration;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(99, "Maintenance", 9900)]
    public static class PrototypeStep99MaintenanceAutomationSuites
    {
        private static readonly string[] RequiredDefinitionIds = PrototypeSceneProductionIntegrationProbe
            .BuildRegistry(new DefinitionRegistry(Array.Empty<IGameDefinition>()))
            .DefinitionsById
            .Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            registry?.TryRegister(new TestLabAutomationSuite(
                "maintenance.phase-2.prototype-integration-legacy-cleanup",
                "Phase 2 Prototype Integration & Legacy Cleanup",
                "Maintenance",
                "Validates the prototype scene integration contract, production-backed logical bindings, quest source scene bindings, idempotent seeding, and legacy-bypass diagnostics.",
                99000,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "PrototypeSceneIntegrationValidator", "WorldSceneBindingRuntime", "QuestSourceRuntime" },
                scenarios: new[]
                {
                    Scenario("phase2-contract-readiness", "Prototype integration contract is explicit and complete", 10, Step("maintenance-phase2-contract", "Validate integration contract shape", ContractReadiness)),
                    Scenario("phase2-authoritative-runtime-bindings", "Scene bindings resolve through authoritative runtimes", 20, Step("maintenance-phase2-runtimes", "Validate world/location runtime bindings", RuntimeBindings)),
                    Scenario("phase2-quest-source-bindings", "Quest source scene bindings seed and validate idempotently", 30, Step("maintenance-phase2-quest-sources", "Seed and validate quest source bindings", QuestSourceBindings)),
                    Scenario("phase2-diagnostics", "Missing and duplicate bindings report actionable diagnostics", 40, Step("maintenance-phase2-diagnostics", "Validate failure diagnostics", FailureDiagnostics)),
                    Scenario("phase2-placeholder-coverage", "Required prototype surfaces have generated binding placeholders", 50, Step("maintenance-phase2-placeholders", "Validate required placeholder coverage", PlaceholderCoverage)),
                    Scenario("phase2-adventurer-guild-production-flow", "Adventurer Guild bindings execute through production quest, membership, dialogue, and narrative runtimes", 60, Step("maintenance-phase2-guild-production", "Run Adventurer Guild production flow", AdventurerGuildProductionFlow)),
                    Scenario("phase2-merchant-civic-production-flow", "Merchant and civic bindings execute through production source, conversation, and narrative runtimes", 70, Step("maintenance-phase2-civic-production", "Run Merchant Guild and civic production flow", MerchantCivicProductionFlow))
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
                requiredRuntimeAreas: TestLabRuntimeArea.WorldLocations | TestLabRuntimeArea.Quests | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                commandLineSupport: TestLabCommandLineSupport.Supported,
                requiredDefinitionIds: RequiredDefinitionIds);
        }

        private static ITestLabScenarioStep Step(string id, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(id, displayName, action);
        }

        private static TestLabAutomationStepResult ContractReadiness(TestLabAutomationContext context)
        {
            bool requiredWorldIds = PrototypeSceneIntegrationContract.WorldBindings.All(binding =>
                !string.IsNullOrWhiteSpace(binding.LogicalId)
                && !string.IsNullOrWhiteSpace(binding.BindingKey)
                && binding.BindingKey.StartsWith("prototype.", StringComparison.Ordinal));
            bool requiredQuestIds = PrototypeSceneIntegrationContract.QuestSourceBindings.All(binding =>
                !string.IsNullOrWhiteSpace(binding.QuestSourceId)
                && !string.IsNullOrWhiteSpace(binding.DefinitionId)
                && !string.IsNullOrWhiteSpace(binding.BindingKey)
                && !string.IsNullOrWhiteSpace(binding.HostLocationId)
                && !string.IsNullOrWhiteSpace(binding.InteractionPointId));
            bool uniqueWorldKeys = PrototypeSceneIntegrationContract.WorldBindings
                .GroupBy(binding => $"{binding.WorldId}:{binding.SceneKey}:{binding.Category}:{binding.LogicalId}:{binding.BindingKey}", StringComparer.Ordinal)
                .All(group => group.Count() == 1);
            bool uniqueQuestKeys = PrototypeSceneIntegrationContract.QuestSourceBindings
                .GroupBy(binding => $"{binding.WorldId}:{binding.SceneKey}:{binding.QuestSourceId}:{binding.BindingKey}", StringComparer.Ordinal)
                .All(group => group.Count() == 1);
            bool coverage = PrototypeSceneIntegrationContract.WorldBindings.Count >= 30
                && PrototypeSceneIntegrationContract.QuestSourceBindings.Count >= 5
                && PrototypeSceneIntegrationContract.WorldBindings.Any(binding => binding.Category == WorldSceneBindingCategory.Location && binding.LogicalId == "location.prototype.adventurers-guild")
                && PrototypeSceneIntegrationContract.WorldBindings.Any(binding => binding.Category == WorldSceneBindingCategory.InteractionPoint && binding.LogicalId == PrototypeInteractionPointDefinitionFactory.QuestBoardPointId)
                && PrototypeSceneIntegrationContract.QuestSourceBindings.Any(binding => binding.QuestSourceId == PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId);

            bool valid = requiredWorldIds && requiredQuestIds && uniqueWorldKeys && uniqueQuestKeys && coverage;
            return TestLabAssertions.True(
                "maintenance-phase2-contract",
                "Prototype integration contract is explicit and complete",
                valid,
                $"WorldBindings={PrototypeSceneIntegrationContract.WorldBindings.Count} QuestSources={PrototypeSceneIntegrationContract.QuestSourceBindings.Count} RequiredWorldIds={requiredWorldIds} RequiredQuestIds={requiredQuestIds} UniqueWorld={uniqueWorldKeys} UniqueQuest={uniqueQuestKeys} Coverage={coverage}.");
        }

        private static TestLabAutomationStepResult RuntimeBindings(TestLabAutomationContext context)
        {
            PrototypeSceneIntegrationRuntimeContext runtimeContext = SeedWorldRuntimeContext(context);
            QuestSourceRuntime questSources = SeedQuestSources(context);
            runtimeContext.QuestSources = questSources;
            PrototypeSceneIntegrationValidationReport report = PrototypeSceneIntegrationValidator.Validate(
                PrototypeSceneIntegrationContract.WorldBindings.Select(ToSnapshot),
                PrototypeSceneIntegrationContract.QuestSourceBindings.Select(ToSnapshot),
                runtimeContext);
            string diagnostics = report.Succeeded
                ? report.Summary
                : $"{report.Summary} {string.Join(" | ", report.Failures.Select(issue => issue.ToString()))}";

            return TestLabAssertions.True(
                "maintenance-phase2-runtimes",
                "Scene bindings resolve through authoritative runtimes",
                report.Succeeded,
                diagnostics);
        }

        private static TestLabAutomationStepResult QuestSourceBindings(TestLabAutomationContext context)
        {
            QuestSourceRuntime questSources = SeedQuestSources(context);
            long revisionAfterFirst = questSources.Revision;
            PrototypeQuestSourceSceneFactory.SeedPrototypeSceneQuestSources(questSources, Registry(context), PersistenceService.LocalWorldId);

            bool sourceCount = questSources.SourceCount == PrototypeSceneIntegrationContract.QuestSourceBindings.Count;
            bool idempotent = questSources.Revision == revisionAfterFirst;
            bool definitions = PrototypeSceneIntegrationContract.QuestSourceBindings.All(expected =>
                questSources.TryGetSource(expected.QuestSourceId, out QuestSourceSnapshot source)
                && source.QuestSourceDefinitionId == expected.DefinitionId
                && source.SceneBindingKey == expected.BindingKey);

            bool valid = sourceCount && idempotent && definitions;
            return TestLabAssertions.True(
                "maintenance-phase2-quest-sources",
                "Quest source scene bindings seed and validate idempotently",
                valid,
                $"SourceCount={questSources.SourceCount}/{PrototypeSceneIntegrationContract.QuestSourceBindings.Count} Idempotent={idempotent} Definitions={definitions} Revision={questSources.Revision}.");
        }

        private static TestLabAutomationStepResult FailureDiagnostics(TestLabAutomationContext context)
        {
            WorldSceneBindingSnapshot[] missingQuestBoard = PrototypeSceneIntegrationContract.WorldBindings
                .Where(binding => binding.LogicalId != PrototypeInteractionPointDefinitionFactory.QuestBoardPointId)
                .Select(ToSnapshot)
                .ToArray();
            WorldSceneBindingSnapshot duplicate = ToSnapshot(PrototypeSceneIntegrationContract.WorldBindings.First(binding => binding.Category == WorldSceneBindingCategory.Location));
            PrototypeSceneIntegrationValidationReport report = PrototypeSceneIntegrationValidator.Validate(
                missingQuestBoard.Concat(new[] { duplicate }),
                PrototypeSceneIntegrationContract.QuestSourceBindings.Select(ToSnapshot));

            bool missing = report.Failures.Any(issue => issue.Domain == PrototypeSceneIntegrationIssueDomain.SceneBinding && issue.SubjectId == PrototypeInteractionPointDefinitionFactory.QuestBoardPointId);
            bool duplicateDetected = report.Failures.Any(issue => issue.Domain == PrototypeSceneIntegrationIssueDomain.DuplicateBinding);
            bool valid = !report.Succeeded && missing && duplicateDetected;
            return TestLabAssertions.True(
                "maintenance-phase2-diagnostics",
                "Missing and duplicate bindings report actionable diagnostics",
                valid,
                $"Succeeded={report.Succeeded} MissingQuestBoard={missing} Duplicate={duplicateDetected} Failures={report.Failures.Count}.");
        }

        private static TestLabAutomationStepResult PlaceholderCoverage(TestLabAutomationContext context)
        {
            bool allRequired = PrototypeSceneIntegrationContract.WorldBindings.All(binding => binding.Required)
                && PrototypeSceneIntegrationContract.QuestSourceBindings.All(binding => binding.Required);
            bool physicalSurfaceCoverage = PrototypeSceneIntegrationContract.WorldBindings.Any(binding => binding.DisplayName.Contains("Quest Board", StringComparison.Ordinal))
                && PrototypeSceneIntegrationContract.WorldBindings.Any(binding => binding.DisplayName.Contains("Counter", StringComparison.Ordinal))
                && PrototypeSceneIntegrationContract.WorldBindings.Any(binding => binding.DisplayName.Contains("Desk", StringComparison.Ordinal))
                && PrototypeSceneIntegrationContract.WorldBindings.Any(binding => binding.DisplayName.Contains("Door", StringComparison.Ordinal) || binding.DisplayName.Contains("Entrance", StringComparison.Ordinal))
                && PrototypeSceneIntegrationContract.WorldBindings.Any(binding => binding.DisplayName.Contains("Chest", StringComparison.Ordinal))
                && PrototypeSceneIntegrationContract.WorldBindings.Any(binding => binding.DisplayName.Contains("Dungeon", StringComparison.Ordinal));
            bool nonAuthoritative = PrototypeSceneIntegrationContract.WorldBindings.All(binding => binding.Role == WorldSceneBindingRole.Primary && !string.IsNullOrWhiteSpace(binding.BindingKey))
                && PrototypeSceneIntegrationContract.QuestSourceBindings.All(binding => !string.IsNullOrWhiteSpace(binding.BindingKey));

            bool valid = allRequired && physicalSurfaceCoverage && nonAuthoritative;
            return TestLabAssertions.True(
                "maintenance-phase2-placeholders",
                "Required prototype surfaces have generated binding placeholders",
                valid,
                $"AllRequired={allRequired} PhysicalSurfaceCoverage={physicalSurfaceCoverage} NonAuthoritativeBindings={nonAuthoritative}.");
        }

        private static TestLabAutomationStepResult AdventurerGuildProductionFlow(TestLabAutomationContext context)
        {
            PrototypeSceneProductionIntegrationProbe probe = PrototypeSceneProductionIntegrationProbe.Create($"automation.{context?.RunId ?? "run"}");
            PrototypeSceneProductionProbeResult result = probe.RunGuildFlow();
            PrototypeSceneProductionIntegrationProbe restored = probe.Restore();

            int assignments = restored.Participation.QueryAssignments(new QuestAssignmentQuery { questId = result.GuildQuestId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count;
            int discoveries = restored.Sources.QueryDiscoveries(personId: PrototypeEntityLocationFactory.PlayerPersonId).Count;
            int conversations = restored.Conversations.Query(new ConversationQuery { questSourceId = PrototypeSceneIntegrationIds.AdventurerGuildCounterSourceId, access = ConversationAccessLevel.PrivilegedDiagnostic }).Count;
            int dialogue = restored.Dialogue.Query(conversationId: result.GuildConversationId).Count;
            int memberships = restored.Memberships.QueryMemberships(PrototypeEntityLocationFactory.PlayerPersonId, "organization.prototype.guild", activeOnly: true).Count;
            int arcs = restored.Arcs.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId, actorPersonId = PrototypeEntityLocationFactory.PlayerPersonId }).Count;
            bool valid = result.Succeeded && assignments == 1 && discoveries >= 1 && conversations == 1 && dialogue == 1 && memberships == 1 && arcs == 1;

            return TestLabAssertions.True(
                "maintenance-phase2-guild-production",
                "Adventurer Guild bindings execute through production quest, membership, dialogue, and narrative runtimes",
                valid,
                $"{result.Diagnostics} Assignments={assignments} Discoveries={discoveries} Conversations={conversations} Dialogue={dialogue} Memberships={memberships} Arcs={arcs}.");
        }

        private static TestLabAutomationStepResult MerchantCivicProductionFlow(TestLabAutomationContext context)
        {
            PrototypeSceneProductionIntegrationProbe probe = PrototypeSceneProductionIntegrationProbe.Create($"automation.civic.{context?.RunId ?? "run"}");
            PrototypeSceneProductionProbeResult result = probe.RunMerchantAndCivicFlow();
            PrototypeSceneProductionIntegrationProbe restored = probe.Restore();

            int assignments = restored.Participation.QueryAssignments(new QuestAssignmentQuery { questId = result.MerchantQuestId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count;
            int merchantConversations = restored.Conversations.Query(new ConversationQuery { questSourceId = PrototypeSceneIntegrationIds.MerchantGuildCounterSourceId, access = ConversationAccessLevel.PrivilegedDiagnostic }).Count;
            int mayorConversations = restored.Conversations.Query(new ConversationQuery { questSourceId = PrototypeSceneIntegrationIds.MayorOfficeDeskSourceId, access = ConversationAccessLevel.PrivilegedDiagnostic }).Count;
            int recordsConversations = restored.Conversations.Query(new ConversationQuery { definitionId = PrototypeConversationDefinitionFactory.RecordsDeskDefinitionId, access = ConversationAccessLevel.PrivilegedDiagnostic }).Count;
            int states = restored.States.Query(new NarrativeStateQuery { stateDefinitionId = PrototypeNarrativeStateDefinitionFactory.MayorInvestigationDefinitionId, scope = NarrativeStateScope.World, scopeKey = PersistenceService.LocalWorldId }).Count;
            int arcs = restored.Arcs.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, scopeKey = PersistenceService.LocalWorldId }).Count;
            bool valid = result.Succeeded && assignments == 1 && merchantConversations == 1 && mayorConversations == 1 && recordsConversations == 1 && states >= 1 && arcs == 1;

            return TestLabAssertions.True(
                "maintenance-phase2-civic-production",
                "Merchant and civic bindings execute through production source, conversation, and narrative runtimes",
                valid,
                $"{result.Diagnostics} Assignments={assignments} MerchantConversations={merchantConversations} MayorConversations={mayorConversations} RecordsConversations={recordsConversations} States={states} Arcs={arcs}.");
        }

        private static DefinitionRegistry Registry(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            return PrototypeSceneProductionIntegrationProbe.BuildRegistry(registry);
        }

        private static PrototypeSceneIntegrationRuntimeContext SeedWorldRuntimeContext(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(registry);
            registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
            registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);

            LocationRuntime locations = new LocationRuntime();
            EntityLocationRuntime entityLocations = new EntityLocationRuntime();
            InteractionPointRuntime interactionPoints = new InteractionPointRuntime();
            LocationConnectionRuntime connections = new LocationConnectionRuntime();

            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, PersistenceService.LocalWorldId);
            locations.Configure(registry, PersistenceService.LocalWorldId);
            PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, PersistenceService.LocalWorldId);
            PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactionPoints, registry, locations, entityLocations, PersistenceService.LocalWorldId);
            PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(connections, registry, locations, entityLocations, interactionPoints, PersistenceService.LocalWorldId);

            return new PrototypeSceneIntegrationRuntimeContext
            {
                Locations = locations,
                EntityLocations = entityLocations,
                InteractionPoints = interactionPoints,
                Connections = connections
            };
        }

        private static QuestSourceRuntime SeedQuestSources(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = Registry(context);
            QuestSourceRuntime questSources = new QuestSourceRuntime(null, null, registry, PersistenceService.LocalWorldId);
            PrototypeQuestSourceSceneFactory.SeedPrototypeSceneQuestSources(questSources, registry, PersistenceService.LocalWorldId);
            return questSources;
        }

        private static WorldSceneBindingSnapshot ToSnapshot(PrototypeSceneWorldBindingExpectation expected)
        {
            return new WorldSceneBindingSnapshot(
                $"automation.{expected.BindingKey}",
                PersistenceService.LocalWorldId,
                PrototypeSceneIntegrationIds.SceneKey,
                "PrototypeScene",
                expected.Category,
                expected.Role,
                expected.LogicalId,
                expected.BindingKey,
                expected.DisplayName,
                WorldSceneBindingStatus.Bound,
                expected.Required,
                string.Empty);
        }

        private static PrototypeQuestSourceSceneBindingSnapshot ToSnapshot(PrototypeQuestSourceBindingExpectation expected)
        {
            return new PrototypeQuestSourceSceneBindingSnapshot(
                expected.QuestSourceId,
                expected.DefinitionId,
                expected.BindingKey,
                PrototypeSceneIntegrationIds.SceneKey,
                PersistenceService.LocalWorldId,
                expected.DisplayName,
                expected.HostLocationId,
                expected.InteractionPointId,
                expected.Required);
        }
    }
}
#endif
