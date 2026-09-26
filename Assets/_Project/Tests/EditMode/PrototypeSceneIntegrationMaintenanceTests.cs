using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.PrototypeIntegration;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Tests
{
    public sealed class PrototypeSceneIntegrationMaintenanceTests
    {
        private const string AdventurerGuildPrefabPath = "Assets/_Project/Prototype/Prefabs/Buildings/PrototypeAdventurerGuild/AdventurerGuild.prefab";
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [Test]
        public void Phase2Contract_CoversPrototypeGuildCivicQuestDungeonAndEntitySurfaces()
        {
            string[] logicalIds = PrototypeSceneIntegrationContract.RequiredLogicalIds.ToArray();

            Assert.That(logicalIds, Contains.Item("location.prototype.adventurers-guild"));
            Assert.That(logicalIds, Contains.Item("location.prototype.civic-office"));
            Assert.That(logicalIds, Contains.Item("location.prototype.basement-prison"));
            Assert.That(logicalIds, Contains.Item("location.prototype.dungeon-entry"));
            Assert.That(logicalIds, Contains.Item(PrototypeInteractionPointDefinitionFactory.QuestBoardPointId));
            Assert.That(logicalIds, Contains.Item(PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId));
            Assert.That(logicalIds, Contains.Item(EntityLocationReferenceKey.Build(LocationOccupantEntityType.Person, PrototypeEntityLocationFactory.PlayerPersonId, PersistenceService.LocalWorldId)));
            Assert.That(PrototypeSceneIntegrationContract.WorldBindings.Count, Is.GreaterThanOrEqualTo(30));
            Assert.That(PrototypeSceneIntegrationContract.QuestSourceBindings.Count, Is.EqualTo(5));
        }

        [Test]
        public void Phase2Validator_DetectsMissingRequiredPhysicalBindings()
        {
            PrototypeSceneIntegrationValidationReport report = PrototypeSceneIntegrationValidator.Validate(Array.Empty<WorldSceneBindingSnapshot>(), Array.Empty<PrototypeQuestSourceSceneBindingSnapshot>());

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Failures.Count, Is.GreaterThan(10));
            Assert.That(report.Failures.Any(issue => issue.SubjectId == PrototypeInteractionPointDefinitionFactory.QuestBoardPointId), Is.True);
            Assert.That(report.Failures.Any(issue => issue.SubjectId == PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId), Is.True);
        }

        [Test]
        public void Phase2Validator_AcceptsContractBindingsAgainstAuthoritativeRuntimes()
        {
            DefinitionRegistry registry = BuildRegistry();
            LocationRuntime locations = new LocationRuntime();
            EntityLocationRuntime entityLocations = new EntityLocationRuntime();
            InteractionPointRuntime interactionPoints = new InteractionPointRuntime();
            LocationConnectionRuntime connections = new LocationConnectionRuntime();
            QuestSourceRuntime questSources = new QuestSourceRuntime(null, null, registry, PersistenceService.LocalWorldId);

            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, PersistenceService.LocalWorldId);
            locations.Configure(registry, PersistenceService.LocalWorldId);
            PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, PersistenceService.LocalWorldId);
            PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactionPoints, registry, locations, entityLocations, PersistenceService.LocalWorldId);
            PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(connections, registry, locations, entityLocations, interactionPoints, PersistenceService.LocalWorldId);
            PrototypeQuestSourceSceneFactory.SeedPrototypeSceneQuestSources(questSources, registry, PersistenceService.LocalWorldId);

            PrototypeSceneIntegrationValidationReport report = PrototypeSceneIntegrationValidator.Validate(
                PrototypeSceneIntegrationContract.WorldBindings.Select(ToSnapshot),
                PrototypeSceneIntegrationContract.QuestSourceBindings.Select(ToSnapshot),
                new PrototypeSceneIntegrationRuntimeContext
                {
                    Locations = locations,
                    EntityLocations = entityLocations,
                    InteractionPoints = interactionPoints,
                    Connections = connections,
                    QuestSources = questSources
                });

            Assert.That(report.Succeeded, Is.True, string.Join(Environment.NewLine, report.Failures.Select(issue => issue.ToString())));
        }

        [Test]
        public void Phase2QuestSourceSeeder_IsIdempotentAndUsesSceneBindingContract()
        {
            DefinitionRegistry registry = BuildRegistry();
            QuestSourceRuntime questSources = new QuestSourceRuntime(null, null, registry, PersistenceService.LocalWorldId);

            PrototypeQuestSourceSceneFactory.SeedPrototypeSceneQuestSources(questSources, registry, PersistenceService.LocalWorldId);
            long revisionAfterFirst = questSources.Revision;
            PrototypeQuestSourceSceneFactory.SeedPrototypeSceneQuestSources(questSources, registry, PersistenceService.LocalWorldId);

            Assert.That(questSources.Revision, Is.EqualTo(revisionAfterFirst));
            Assert.That(questSources.SourceCount, Is.EqualTo(PrototypeSceneIntegrationContract.QuestSourceBindings.Count));
            foreach (PrototypeQuestSourceBindingExpectation expected in PrototypeSceneIntegrationContract.QuestSourceBindings)
            {
                Assert.That(questSources.TryGetSource(expected.QuestSourceId, out QuestSourceSnapshot snapshot), Is.True);
                Assert.That(snapshot.QuestSourceDefinitionId, Is.EqualTo(expected.DefinitionId));
                Assert.That(snapshot.SceneBindingKey, Is.EqualTo(expected.BindingKey));
            }
        }

        [Test]
        public void Phase2Contract_IsBindingOnlyAndEnumeratesTemporaryPhysicalSurfaces()
        {
            Assert.That(PrototypeSceneIntegrationContract.PhysicalSurfaces.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(PrototypeSceneIntegrationContract.PhysicalSurfaces.All(surface =>
                !string.IsNullOrWhiteSpace(surface.SurfaceId)
                && !string.IsNullOrWhiteSpace(surface.HierarchyPath)
                && !string.IsNullOrWhiteSpace(surface.LogicalBindingId)
                && !string.IsNullOrWhiteSpace(surface.ReplacementExpectation)), Is.True);
            Assert.That(PrototypeSceneIntegrationContract.PhysicalSurfaces.Any(surface => surface.LogicalBindingId == PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId), Is.True);
            Assert.That(PrototypeSceneIntegrationContract.PhysicalSurfaces.Any(surface => surface.LogicalBindingId == PrototypeInteractionPointDefinitionFactory.MayorDeskPointId), Is.True);
            Assert.That(PrototypeSceneIntegrationContract.PhysicalSurfaces.Any(surface => surface.ReplacementExpectation.Contains("runtime", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public void Phase2AdventurerGuildPhysicalInteractionObjectsCarryProductionBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdventurerGuildPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            AssertChildMissing(prefab, "Interact - Adventurer Guild Counter");
            AssertChildMissing(prefab, "Interact - Merchant Guild Counter");
            AssertInteraction(prefab, "AdventurerGuildCounter", PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, requiresCollider: true);
            AssertInteractionCollidersDoNotBlockMovement(prefab, "AdventurerGuildCounter");
            AssertQuestSource(prefab, "AdventurerGuildCounter", PrototypeSceneIntegrationIds.AdventurerGuildCounterSourceId, PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId);
            AssertInteraction(prefab, "MerchantGuildCounter", PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId, requiresCollider: true);
            AssertInteractionCollidersDoNotBlockMovement(prefab, "MerchantGuildCounter");
            AssertQuestSource(prefab, "MerchantGuildCounter", PrototypeSceneIntegrationIds.MerchantGuildCounterSourceId, PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId);
            AssertInteraction(prefab, "Quest Board", PrototypeInteractionPointDefinitionFactory.QuestBoardPointId, requiresCollider: true);
            AssertInteractionCollidersDoNotBlockMovement(prefab, "Quest Board");
            AssertQuestSource(prefab, "Quest Board", PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId, PrototypeInteractionPointDefinitionFactory.QuestBoardPointId);
            AssertInteraction(prefab, "Interact - Mayor Desk", PrototypeInteractionPointDefinitionFactory.MayorDeskPointId, requiresCollider: true);
            AssertInteractionCollidersDoNotBlockMovement(prefab, "Interact - Mayor Desk");
            AssertQuestSource(prefab, "Interact - Mayor Desk", PrototypeSceneIntegrationIds.MayorOfficeDeskSourceId, PrototypeInteractionPointDefinitionFactory.MayorDeskPointId);
            AssertInteraction(prefab, "Interact - City Office Records Desk", PrototypeInteractionPointDefinitionFactory.RecordsDeskPointId, requiresCollider: true);
            AssertInteractionCollidersDoNotBlockMovement(prefab, "Interact - City Office Records Desk");
            AssertQuestSource(prefab, "Interact - City Office Records Desk", PrototypeSceneIntegrationIds.CityRecordsArchiveSourceId, PrototypeInteractionPointDefinitionFactory.RecordsDeskPointId);
            AssertInteraction(prefab, "Interact - Guild Head Desk", PrototypeInteractionPointDefinitionFactory.GuildHeadDeskPointId, requiresCollider: true);
            AssertInteractionCollidersDoNotBlockMovement(prefab, "Interact - Guild Head Desk");
            AssertInteraction(prefab, "Interact - Prison Cell A Door", PrototypeInteractionPointDefinitionFactory.PrisonCellPointId, requiresCollider: true);
            AssertInteractionCollidersDoNotBlockMovement(prefab, "Interact - Prison Cell A Door");

            GameObject merchantCounter = FindChild(prefab, "MerchantGuildCounter");
            Assert.That(merchantCounter.GetComponent<LocationSceneBinding>(), Is.Not.Null, "Merchant counter must retain its co-located location binding.");
        }

        [Test]
        public void Phase2PrototypeSceneDoesNotKeepLegacyCounterOrQuestBoardBindingObjects()
        {
            EditorSceneManager.OpenScene(PrototypeScenePath);
            string[] obsoleteNames =
            {
                "Interact - Adventurer Guild Counter",
                "Interact - Merchant Guild Counter",
                "Adventurer Guild Counter Source",
                "Merchant Guild Counter Source",
                "Adventurer Guild Counter",
                "Merchant Guild Counter",
                "Adventurer Guild Quest Board"
            };

            GameObject[] sceneObjects = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Select(item => item.gameObject)
                .ToArray();
            Assert.That(sceneObjects.Where(item => obsoleteNames.Contains(item.name)).Select(item => item.name).ToArray(), Is.Empty);
            AssertScenePhysicalInteractionBinding(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, "AdventurerGuildCounter");
            AssertScenePhysicalInteractionBinding(PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId, "MerchantGuildCounter");
            AssertScenePhysicalInteractionBinding(PrototypeInteractionPointDefinitionFactory.QuestBoardPointId, "Quest Board");

            MonoBehaviour[] interactables = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(item => item is IInteractable)
                .ToArray();
            Assert.That(interactables, Is.Not.Empty);
            foreach (MonoBehaviour interactable in interactables)
            {
                Collider[] ownedColliders = interactable.GetComponentsInChildren<Collider>(true)
                    .Where(collider => ReferenceEquals(collider.GetComponentInParent<IInteractable>(), interactable))
                    .ToArray();
                Assert.That(ownedColliders, Is.Not.Empty, interactable.name);
                Assert.That(ownedColliders.Any(collider => collider.enabled && collider.isTrigger), Is.True, interactable.name);
                Assert.That(((IInteractable)interactable).InteractionPrompt, Is.Not.Empty, interactable.name);
            }
        }

        private static void AssertInteraction(GameObject prefab, string objectName, string interactionPointId, bool requiresCollider = false)
        {
            GameObject marker = FindChild(prefab, objectName);
            Assert.That(marker, Is.Not.Null, objectName);
            if (requiresCollider)
            {
                Assert.That(marker.GetComponentInChildren<Collider>(true), Is.Not.Null, objectName);
            }

            InteractionPointSceneBinding binding = marker.GetComponent<InteractionPointSceneBinding>();
            Assert.That(binding, Is.Not.Null, objectName);
            Assert.That(binding.LogicalId, Is.EqualTo(interactionPointId), objectName);
            Assert.That(binding.BindingKey, Is.EqualTo(PrototypeSceneIntegrationContract.WorldBindings.Single(item => item.LogicalId == interactionPointId).BindingKey), objectName);
            Assert.That(binding.Required, Is.True, objectName);
        }

        private static void AssertQuestSource(GameObject prefab, string objectName, string questSourceId, string interactionPointId)
        {
            GameObject marker = FindChild(prefab, objectName);
            QuestSourceSceneBinding binding = marker.GetComponent<QuestSourceSceneBinding>();
            Assert.That(binding, Is.Not.Null, objectName);
            Assert.That(binding.QuestSourceId, Is.EqualTo(questSourceId), objectName);
            Assert.That(binding.InteractionPointId, Is.EqualTo(interactionPointId), objectName);
            Assert.That(binding.SceneBindingKey, Is.EqualTo(PrototypeSceneIntegrationContract.QuestSourceBindings.Single(item => item.QuestSourceId == questSourceId).BindingKey), objectName);
            Assert.That(binding.Required, Is.True, objectName);
        }

        private static void AssertInteractionCollidersDoNotBlockMovement(GameObject prefab, string objectName)
        {
            GameObject marker = FindChild(prefab, objectName);
            Collider[] colliders = marker.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders.Length, Is.GreaterThan(0), objectName);
            Assert.That(colliders.All(item => item.isTrigger), Is.True, objectName);
            Assert.That(colliders.All(item => !item.providesContacts), Is.True, objectName);
        }

        private static void AssertScenePhysicalInteractionBinding(string interactionPointId, string objectName)
        {
            InteractionPointSceneBinding[] bindings = UnityEngine.Object.FindObjectsByType<InteractionPointSceneBinding>(FindObjectsInactive.Include)
                .Where(item => string.Equals(item.LogicalId, interactionPointId, StringComparison.Ordinal))
                .ToArray();
            Assert.That(bindings.Length, Is.EqualTo(1), interactionPointId);
            Assert.That(bindings[0].gameObject.name, Is.EqualTo(objectName), interactionPointId);
            Collider[] colliders = bindings[0].GetComponentsInChildren<Collider>(true);
            Assert.That(colliders.Length, Is.GreaterThan(0), interactionPointId);
            Assert.That(colliders.All(item => item.isTrigger), Is.True, interactionPointId);
            Assert.That(colliders.All(item => !item.providesContacts), Is.True, interactionPointId);
        }

        private static void AssertChildMissing(GameObject prefab, string objectName)
        {
            Assert.That(FindChild(prefab, objectName), Is.Null, objectName);
        }

        private static GameObject FindChild(GameObject root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Select(item => item.gameObject)
                .FirstOrDefault(item => string.Equals(item.name, objectName, StringComparison.Ordinal));
        }

        [Test]
        public void Phase2LegacyCleanup_DoesNotUseNameBasedRuntimeLookupInPrototypeIntegration()
        {
            string root = Path.Combine("Assets", "_Project", "Runtime", "PrototypeIntegration");
            string[] files = Directory.Exists(root) ? Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories) : Array.Empty<string>();
            string[] banned =
            {
                "GameObject.Find",
                "FindObjectOfType",
                "FindObjectsOfType",
                "QuestManager",
                "ConversationManager",
                "NarrativeManager",
                "isGuildMember",
                "currentQuestProgress"
            };

            string[] hits = files
                .SelectMany(file => banned.Where(pattern => File.ReadAllText(file).Contains(pattern, StringComparison.Ordinal)).Select(pattern => $"{file}:{pattern}"))
                .ToArray();

            Assert.That(hits, Is.Empty);
        }

        [Test]
        public void Phase2AdventurerGuildFlow_UsesProductionRuntimesAndRestoresWithoutReplay()
        {
            PrototypeSceneProductionIntegrationProbe harness = PrototypeSceneProductionIntegrationProbe.Create("test");
            PrototypeSceneProductionProbeResult result = harness.RunGuildFlow();
            PrototypeSceneProductionIntegrationProbe restored = harness.Restore();

            Assert.That(result.Succeeded, Is.True, result.Diagnostics);
            Assert.That(restored.Participation.QueryAssignments(new QuestAssignmentQuery { questId = result.GuildQuestId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count, Is.EqualTo(1));
            Assert.That(restored.Sources.QueryDiscoveries(personId: PrototypeEntityLocationFactory.PlayerPersonId).Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(restored.Conversations.Query(new ConversationQuery { questSourceId = PrototypeSceneIntegrationIds.AdventurerGuildCounterSourceId, access = ConversationAccessLevel.PrivilegedDiagnostic }).Count, Is.EqualTo(1));
            Assert.That(restored.Dialogue.Query(conversationId: result.GuildConversationId).Count, Is.EqualTo(1));
            Assert.That(restored.Memberships.QueryMemberships(PrototypeEntityLocationFactory.PlayerPersonId, "organization.prototype.adventurers-guild", activeOnly: true).Count, Is.EqualTo(1));
            Assert.That(restored.Arcs.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId, actorPersonId = PrototypeEntityLocationFactory.PlayerPersonId }).Count, Is.EqualTo(1));
        }

        [Test]
        public void Phase2MerchantAndCivicFlows_UseProductionSourceConversationAndNarrativeRecords()
        {
            PrototypeSceneProductionIntegrationProbe harness = PrototypeSceneProductionIntegrationProbe.Create("test.civic");
            PrototypeSceneProductionProbeResult result = harness.RunMerchantAndCivicFlow();
            PrototypeSceneProductionIntegrationProbe restored = harness.Restore();

            Assert.That(result.Succeeded, Is.True, result.Diagnostics);
            Assert.That(restored.Participation.QueryAssignments(new QuestAssignmentQuery { questId = result.MerchantQuestId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count, Is.EqualTo(1));
            Assert.That(restored.Conversations.Query(new ConversationQuery { questSourceId = PrototypeSceneIntegrationIds.MerchantGuildCounterSourceId, access = ConversationAccessLevel.PrivilegedDiagnostic, includeInactive = true }).Count, Is.EqualTo(1));
            Assert.That(restored.Conversations.Query(new ConversationQuery { questSourceId = PrototypeSceneIntegrationIds.MayorOfficeDeskSourceId, access = ConversationAccessLevel.PrivilegedDiagnostic, includeInactive = true }).Count, Is.EqualTo(1));
            Assert.That(restored.Conversations.Query(new ConversationQuery { definitionId = PrototypeConversationDefinitionFactory.RecordsDeskDefinitionId, access = ConversationAccessLevel.PrivilegedDiagnostic }).Count, Is.EqualTo(1));
            Assert.That(restored.States.Query(new NarrativeStateQuery { stateDefinitionId = PrototypeNarrativeStateDefinitionFactory.MayorInvestigationDefinitionId, scope = NarrativeStateScope.World, scopeKey = PersistenceService.LocalWorldId }).Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(restored.Arcs.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, scopeKey = PersistenceService.LocalWorldId }).Count, Is.EqualTo(1));
        }

        private static DefinitionRegistry BuildRegistry()
        {
            DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            registry = PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(registry);
            registry = PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(registry);
            registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(registry);
            registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
            registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);
            registry = PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(registry);
            registry = PrototypeQuestSourceDefinitionFactory.AddMissingPrototypeQuestSourceDefinitions(registry);
            registry = PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(registry);
            registry = PrototypeDialogueGraphDefinitionFactory.AddMissingPrototypeDialogueGraphDefinitions(registry);
            registry = PrototypeNarrativeEventDefinitionFactory.AddMissingPrototypeNarrativeEventDefinitions(registry);
            registry = PrototypeNarrativeStateDefinitionFactory.AddMissingPrototypeNarrativeStateDefinitions(registry);
            registry = PrototypeNarrativeArcDefinitionFactory.AddMissingPrototypeNarrativeArcDefinitions(registry);
            return registry;
        }

        private static WorldSceneBindingSnapshot ToSnapshot(PrototypeSceneWorldBindingExpectation expected)
        {
            return new WorldSceneBindingSnapshot(
                $"test.{expected.BindingKey}",
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
