using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Parties;
using UnityIsekaiGame.Social.Networks;
using Object = UnityEngine.Object;

namespace UnityIsekaiGame.Tests
{
    public sealed class AuthorityBoundaryTests
    {
        [TestCase("person.player.jhand", true)]
        [TestCase("person.player.audit-smoke", true)]
        [TestCase("person.player.jhand|social-interaction.prototype.compliment", false)]
        [TestCase("person.player.jhand.person.player.other", false)]
        [TestCase("person.player.jhand.binding.merchant-delivery.quest", false)]
        [TestCase("person.prototype.guard", false)]
        public void RuntimePlayerIdentityRecoveryRejectsCompositeWorldRecordKeys(string candidate, bool expected)
        {
            MethodInfo validator = typeof(PrototypePersistenceServiceBehaviour).GetMethod(
                "IsAuthoritativeRuntimePersonId",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(validator, Is.Not.Null);
            Assert.That((bool)validator.Invoke(null, new object[] { candidate }), Is.EqualTo(expected));
        }

        [Test]
        public void PersistenceServiceRecognizesOnlyItsConfiguredPlayerHierarchy()
        {
            GameObject serviceObject = new GameObject("Authority Service");
            GameObject player = new GameObject("Authoritative Player");
            GameObject playerChild = new GameObject("Player Child");
            GameObject unrelated = new GameObject("Unrelated Character");
            serviceObject.SetActive(false);
            playerChild.transform.SetParent(player.transform);
            try
            {
                PrototypePersistenceServiceBehaviour service = serviceObject.AddComponent<PrototypePersistenceServiceBehaviour>();
                typeof(PrototypePersistenceServiceBehaviour)
                    .GetField("playerRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(service, player.transform);

                Assert.That(service.OwnsPlayerInteractor(player), Is.True);
                Assert.That(service.OwnsPlayerInteractor(playerChild), Is.True);
                Assert.That(service.OwnsPlayerInteractor(unrelated), Is.False);
                Assert.That(service.OwnsPlayerInteractor(null), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(unrelated);
            }
        }

        [Test]
        public void PersistenceLookupSelectsTheServiceThatOwnsTheInteractor()
        {
            GameObject firstServiceObject = new GameObject("First Authority Service");
            GameObject secondServiceObject = new GameObject("Second Authority Service");
            GameObject firstPlayer = new GameObject("First Player");
            GameObject secondPlayer = new GameObject("Second Player");
            GameObject secondPlayerChild = new GameObject("Second Player Child");
            firstServiceObject.SetActive(false);
            secondServiceObject.SetActive(false);
            secondPlayerChild.transform.SetParent(secondPlayer.transform);
            try
            {
                PrototypePersistenceServiceBehaviour first = firstServiceObject.AddComponent<PrototypePersistenceServiceBehaviour>();
                PrototypePersistenceServiceBehaviour second = secondServiceObject.AddComponent<PrototypePersistenceServiceBehaviour>();
                FieldInfo playerRoot = typeof(PrototypePersistenceServiceBehaviour)
                    .GetField("playerRoot", BindingFlags.Instance | BindingFlags.NonPublic);
                playerRoot?.SetValue(first, firstPlayer.transform);
                playerRoot?.SetValue(second, secondPlayer.transform);

                Assert.That(PrototypePersistenceServiceBehaviour.FindForInteractor(secondPlayerChild), Is.SameAs(second));
                Assert.That(PrototypePersistenceServiceBehaviour.FindForInteractor(firstPlayer), Is.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(firstServiceObject);
                Object.DestroyImmediate(secondServiceObject);
                Object.DestroyImmediate(firstPlayer);
                Object.DestroyImmediate(secondPlayer);
            }
        }

        [Test]
        public void ClosingOneNarrativeOwnerDoesNotClearAnotherOwnersModalState()
        {
            GameObject first = new GameObject("First Narrative Owner");
            GameObject second = new GameObject("Second Narrative Owner");
            try
            {
                GameUiModalState.SetNarrativeActive(first, true);
                GameUiModalState.SetNarrativeActive(second, true);
                GameUiModalState.SetNarrativeActive(first, false);
                Assert.That(GameUiModalState.NarrativeActive, Is.True);

                GameUiModalState.SetNarrativeActive(second, false);
                Assert.That(GameUiModalState.NarrativeActive, Is.False);
            }
            finally
            {
                GameUiModalState.SetNarrativeActive(false);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void PlayerMenuSourceCannotBootstrapOrRetargetAuthoritativeServices()
        {
            string source = ReadRepositoryFile(
                "Packages/com.thequantifier.isekai.client/Runtime/UI/Inventory/InventoryScreenController.cs");

            StringAssert.DoesNotContain(".InitializeFromRegistry(", source);
            StringAssert.DoesNotContain(".ConfigurePlayerPersistence(", source);
            StringAssert.DoesNotContain(".EnsureInitialized(", source);
            StringAssert.DoesNotContain("new GameObject(\"Prototype Persistence Service\")", source);
        }

        [Test]
        public void CharacterInteractionsDoNotUseProcessWideModalStateAsWorldEligibility()
        {
            string[] relativePaths =
            {
                "Packages/com.thequantifier.isekai.simulation/Runtime/Dialogue/NpcDialogueInteractable.cs",
                "Packages/com.thequantifier.isekai.simulation/Runtime/PrototypeIntegration/ConversationSceneBinding.cs",
                "Packages/com.thequantifier.isekai.simulation/Runtime/PrototypeIntegration/QuestSourceSceneBinding.cs"
            };

            foreach (string relativePath in relativePaths)
            {
                string source = ReadRepositoryFile(relativePath);
                StringAssert.DoesNotContain("GameUiModalState.IsModalActive", source, relativePath);
                StringAssert.Contains("OwnsPlayerInteractor", source, relativePath);
            }
        }

        [Test]
        public void QuestSignalRoutingDoesNotInventOrMutateCharacterAttribution()
        {
            string source = ReadRepositoryFile(
                "Packages/com.thequantifier.isekai.simulation/Runtime/Gameplay/PrototypeNarrativeCoordinator.cs");

            StringAssert.Contains("QuestObjectiveSignal routedSignal = signal.Clone()", source);
            StringAssert.Contains("IsObjectiveContributorRelevant(contributor)", source);
            StringAssert.DoesNotContain("signal.actorPersonId = PlayerPersonId", source);
            StringAssert.DoesNotContain("signal.participantPersonId = PlayerPersonId", source);
            StringAssert.DoesNotContain("ReportMemberState(party.PartyId, person", source);
        }

        [Test]
        public void CharacterCombatControllersDoNotSelectAnArbitraryPlayersWorldService()
        {
            string[] relativePaths =
            {
                "Packages/com.thequantifier.isekai.simulation/Runtime/Combat/PlayerMeleeCombat.cs",
                "Packages/com.thequantifier.isekai.simulation/Runtime/Magic/PlayerSpellcaster.cs",
                "Packages/com.thequantifier.isekai.simulation/Runtime/Combat/EnemyMeleeAttack.cs"
            };

            foreach (string relativePath in relativePaths)
            {
                string source = ReadRepositoryFile(relativePath);
                StringAssert.DoesNotContain("FindAnyObjectByType<PrototypePersistenceServiceBehaviour>", source, relativePath);
                StringAssert.Contains("FindForInteractor", source, relativePath);
            }
        }

        [Test]
        public void PartyCombatPoliciesRemainScopedToThePartyThatContainsBothCharacters()
        {
            DefinitionRegistry registry = PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(
                new DefinitionRegistry(Array.Empty<IGameDefinition>()));
            (AdventuringPartyService parties, PartyOperationalRuntime operations) first = CreatePartyRuntime(
                registry, "party.first", "person.first.a", "person.first.b", PartyFriendlyFirePolicy.Prevent);
            (AdventuringPartyService parties, PartyOperationalRuntime operations) second = CreatePartyRuntime(
                registry, "party.second", "person.second.a", "person.second.b", PartyFriendlyFirePolicy.Allow);
            try
            {
                PartyCombatContext.Configure(first.parties, first.operations);
                PartyCombatContext.Configure(second.parties, second.operations);

                Assert.That(PartyCombatContext.CanDamage(null, "person.first.a", null, "person.first.b"), Is.False);
                Assert.That(PartyCombatContext.CanDamage(null, "person.second.a", null, "person.second.b"), Is.True);
                Assert.That(PartyCombatContext.CanDamage(null, "person.first.a", null, "person.second.b"), Is.True);
            }
            finally
            {
                PartyCombatContext.Clear(first.operations);
                PartyCombatContext.Clear(second.operations);
            }
        }

        [Test]
        public void PartySettingsQueryDoesNotCreateWorldState()
        {
            PartyOperationalRuntime operations = new PartyOperationalRuntime();
            long revision = operations.Revision;

            PartySettingsData settings = operations.GetSettings("party.query-only");

            Assert.That(settings.partyId, Is.EqualTo("party.query-only").IgnoreCase);
            Assert.That(operations.Revision, Is.EqualTo(revision));
            Assert.That(operations.CreateSaveData().settings, Is.Empty);
        }

        [Test]
        public void PlayerFacingRuntimeCodeDoesNotSelectArbitraryPersistenceServices()
        {
            string[] relativePaths =
            {
                "Packages/com.thequantifier.isekai.client/Runtime/UI/Inventory/InventoryScreenController.cs",
                "Packages/com.thequantifier.isekai.client/Runtime/UI/Inventory/InventoryScreenView.cs",
                "Packages/com.thequantifier.isekai.client/Runtime/UI/Parties/PartyMenuExtension.cs",
                "Packages/com.thequantifier.isekai.client/Runtime/UI/QuestTrackerHudView.cs",
                "Packages/com.thequantifier.isekai.simulation/Runtime/Gameplay/PrototypeProfessionPanel.cs",
                "Packages/com.thequantifier.isekai.simulation/Runtime/Parties/PartyRuntimeComponents.cs"
            };

            foreach (string relativePath in relativePaths)
            {
                string source = ReadRepositoryFile(relativePath);
                StringAssert.DoesNotContain("FindAnyObjectByType<PrototypePersistenceServiceBehaviour>", source, relativePath);
            }
        }

        private static (AdventuringPartyService parties, PartyOperationalRuntime operations) CreatePartyRuntime(
            DefinitionRegistry registry,
            string partyId,
            string leaderId,
            string memberId,
            PartyFriendlyFirePolicy friendlyFire)
        {
            SocialNetworkRuntime networks = new SocialNetworkRuntime();
            networks.Configure(registry, new[] { leaderId, memberId }, null, null, null, null, null, null);
            AdventuringPartyService parties = new AdventuringPartyService(networks, registry);
            PartySnapshot party = parties.CreateParty(partyId, partyId, leaderId, 1d, $"{partyId}.create").Party;
            parties.AddMember(party.PartyId, leaderId, memberId, 2d, $"{partyId}.join");
            PartyOperationalRuntime operations = new PartyOperationalRuntime();
            operations.Configure(parties);
            operations.SetSettings(party.PartyId, leaderId, PartyFormation.Wedge, PartyLootPolicy.Individual, friendlyFire, PartyCommand.Follow, out _);
            return (parties, operations);
        }

        private static string ReadRepositoryFile(string repositoryRelativePath)
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            return File.ReadAllText(Path.Combine(repositoryRoot, repositoryRelativePath));
        }
    }
}
