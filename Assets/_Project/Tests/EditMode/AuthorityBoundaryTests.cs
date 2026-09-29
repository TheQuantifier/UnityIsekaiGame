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
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Client/UI/Inventory/InventoryScreenController.cs");
            string source = File.ReadAllText(sourcePath);

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
                "_Project/Runtime/Dialogue/NpcDialogueInteractable.cs",
                "_Project/Runtime/PrototypeIntegration/ConversationSceneBinding.cs",
                "_Project/Runtime/PrototypeIntegration/QuestSourceSceneBinding.cs"
            };

            foreach (string relativePath in relativePaths)
            {
                string source = File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
                StringAssert.DoesNotContain("GameUiModalState.IsModalActive", source, relativePath);
                StringAssert.Contains("OwnsPlayerInteractor", source, relativePath);
            }
        }

        [Test]
        public void QuestSignalRoutingDoesNotInventOrMutateCharacterAttribution()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Gameplay/PrototypeNarrativeCoordinator.cs"));

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
                "_Project/Runtime/Combat/PlayerMeleeCombat.cs",
                "_Project/Runtime/Magic/PlayerSpellcaster.cs",
                "_Project/Runtime/Combat/EnemyMeleeAttack.cs"
            };

            foreach (string relativePath in relativePaths)
            {
                string source = File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
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
                "_Project/Runtime/Client/UI/Inventory/InventoryScreenController.cs",
                "_Project/Runtime/Client/UI/Inventory/InventoryScreenView.cs",
                "_Project/Runtime/Client/UI/Parties/PartyMenuExtension.cs",
                "_Project/Runtime/Client/UI/QuestTrackerHudView.cs",
                "_Project/Runtime/Gameplay/PrototypeProfessionPanel.cs",
                "_Project/Runtime/Parties/PartyRuntimeComponents.cs"
            };

            foreach (string relativePath in relativePaths)
            {
                string source = File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
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
    }
}
