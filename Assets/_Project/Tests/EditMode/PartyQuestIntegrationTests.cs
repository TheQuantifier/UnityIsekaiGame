using System;
using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Parties;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.Social.Networks;

namespace UnityIsekaiGame.Tests
{
    public sealed class PartyQuestIntegrationTests
    {
        private static readonly string[] KnownPeople =
        {
            "person.prototype.player",
            "person.prototype.companion-a",
            "person.prototype.companion-b"
        };

        [Test]
        public void PartyLifecycleUsesPersistentSocialMemberships()
        {
            DefinitionRegistry registry = PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
            SocialNetworkRuntime networks = new SocialNetworkRuntime();
            networks.Configure(registry, KnownPeople, null, null, null, null, null, null);
            AdventuringPartyService parties = new AdventuringPartyService(networks, registry);

            PartyOperationResult created = parties.CreateParty("party.prototype.player", "Player Party", KnownPeople[0], 1d, "tx.party.create");
            PartyOperationResult joined = parties.AddMember(created.Party.PartyId, KnownPeople[0], KnownPeople[1], 2d, "tx.party.join.a");
            PartyOperationResult transferred = parties.TransferLeadership(created.Party.PartyId, KnownPeople[0], KnownPeople[1], 3d, "tx.party.transfer");
            PartyOperationResult left = parties.RemoveMember(created.Party.PartyId, KnownPeople[0], KnownPeople[0], 4d, "tx.party.leave");
            PartyOperationResult dissolved = parties.DissolveParty(created.Party.PartyId, KnownPeople[1], 5d, "tx.party.dissolve");

            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(joined.Party.MemberCount, Is.EqualTo(2));
            Assert.That(joined.Party.IsOperational, Is.True);
            Assert.That(transferred.Party.LeaderPersonId, Is.EqualTo(KnownPeople[1]));
            Assert.That(left.Party.MemberPersonIds, Is.EquivalentTo(new[] { KnownPeople[1] }));
            Assert.That(dissolved.Succeeded, Is.True, dissolved.Message);
            Assert.That(parties.GetPartyForPerson(KnownPeople[1]), Is.Null);
            Assert.That(networks.QueryMembers(created.Party.PartyId, activeOnly: true), Is.Empty);
        }

        [Test]
        public void PersonCannotJoinTwoActiveAdventuringParties()
        {
            DefinitionRegistry registry = PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
            SocialNetworkRuntime networks = new SocialNetworkRuntime();
            networks.Configure(registry, KnownPeople, null, null, null, null, null, null);
            AdventuringPartyService parties = new AdventuringPartyService(networks, registry);
            PartyOperationResult first = parties.CreateParty("party.prototype.first", "First", KnownPeople[0], 1d, "tx.party.first");
            PartyOperationResult second = parties.CreateParty("party.prototype.second", "Second", KnownPeople[2], 1d, "tx.party.second");

            PartyOperationResult initialJoin = parties.AddMember(first.Party.PartyId, KnownPeople[0], KnownPeople[1], 2d, "tx.party.first.join");
            PartyOperationResult duplicateJoin = parties.AddMember(second.Party.PartyId, KnownPeople[2], KnownPeople[1], 3d, "tx.party.second.join");

            Assert.That(initialJoin.Succeeded, Is.True, initialJoin.Message);
            Assert.That(duplicateJoin.Status, Is.EqualTo(PartyOperationStatus.AlreadyInParty));
            Assert.That(parties.GetPartyForPerson(KnownPeople[1]).PartyId, Is.EqualTo(first.Party.PartyId));
        }

        [Test]
        public void RequiredPartySizeBlocksSoloAcceptanceAndSnapshotsAcceptedRoster()
        {
            DefinitionRegistry registry = PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
            Assert.That(registry.TryGet(PrototypeQuestDefinitionFactory.DynamicBountyDefinitionId, out QuestDefinition definition), Is.True);
            Assert.That(definition.RequiredPartySize, Is.EqualTo(2));
            Assert.That(definition.RequiresParty, Is.True);

            QuestRuntime quests = new QuestRuntime(registry, PersistenceService.LocalWorldId);
            QuestRuntimeOperationResult created = quests.CreateQuest(new QuestCreateRequest
            {
                transactionId = "tx.quest.party.create",
                questId = "quest.runtime.party-bounty",
                questDefinitionId = PrototypeQuestDefinitionFactory.DynamicBountyDefinitionId,
                repeatInstanceKey = "party-test",
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, interactionPointId = "interaction-point.prototype.bounty-board" }
            });
            QuestParticipationRuntime participation = new QuestParticipationRuntime(quests, registry, PersistenceService.LocalWorldId);
            QuestEligibilityContext solo = BountyContext(KnownPeople[0], null, KnownPeople[0]);
            QuestEligibilityContext party = BountyContext(KnownPeople[0], "party.prototype.player", KnownPeople[0], KnownPeople[1]);

            QuestEligibilityResult soloEligibility = participation.EvaluateEligibility(created.Snapshot.QuestId, solo);
            QuestParticipationOperationResult accepted = participation.DirectAssign(new QuestDirectAssignmentRequest
            {
                transactionId = "tx.quest.party.assign",
                questId = created.Snapshot.QuestId,
                assigneePersonId = KnownPeople[0],
                assignedBy = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                authorityBasisId = "authority.prototype.bounty-board.post",
                explicitConsent = true,
                eligibilityContext = party,
                worldTime = 2d
            });

            Assert.That(soloEligibility.Eligible, Is.False);
            Assert.That(soloEligibility.VisibleFailureReasons.Single(reason => reason.StartsWith("Requires at least", StringComparison.Ordinal)), Does.Contain("2"));
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(accepted.Assignment.UndertakingPartyId, Is.EqualTo("party.prototype.player"));
            Assert.That(accepted.Assignment.ParticipantPersonIds, Is.EquivalentTo(new[] { KnownPeople[0], KnownPeople[1] }));
            Assert.That(accepted.Assignment.RequiredPartySizeAtAcceptance, Is.EqualTo(2));
            Assert.That(participation.QueryAssignments(new QuestAssignmentQuery { participantPersonId = KnownPeople[1], access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count, Is.EqualTo(1));
        }

        [Test]
        public void InvitationCommandsReadinessAndSaveRoundTripRemainAuthoritative()
        {
            DefinitionRegistry registry = PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
            SocialNetworkRuntime networks = new SocialNetworkRuntime();
            networks.Configure(registry, KnownPeople, null, null, null, null, null, null);
            AdventuringPartyService parties = new AdventuringPartyService(networks, registry);
            PartyOperationalRuntime operations = new PartyOperationalRuntime();
            operations.Configure(parties);
            PartySnapshot party = parties.CreateParty("party.prototype.runtime", "Runtime Party", KnownPeople[0], 1d, "tx.party.runtime").Party;

            Assert.That(operations.Invite(party.PartyId, KnownPeople[0], KnownPeople[1], 2d, "invite.runtime.a", out string invitationMessage), Is.True, invitationMessage);
            PartyInvitationData invitation = operations.QueryInvitations(KnownPeople[1], PartyInvitationStatus.Pending).Single();
            PartyOperationResult accepted = operations.AcceptInvitation(invitation.invitationId, KnownPeople[1], 3d, "tx.party.runtime.accept");
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);

            Assert.That(operations.SetSettings(party.PartyId, KnownPeople[0], PartyFormation.Line, PartyLootPolicy.RoundRobin, PartyFriendlyFirePolicy.Prevent, PartyCommand.Defend, out string settingsMessage), Is.True, settingsMessage);
            operations.ReportMemberState(party.PartyId, KnownPeople[0], PartyMemberReadiness.Ready, "place.town", 0f, true, true, true);
            operations.ReportMemberState(party.PartyId, KnownPeople[1], PartyMemberReadiness.Ready, "place.town", 3f, true, true, true);
            operations.RecordContribution(party.PartyId, KnownPeople[1], 4);

            PartyOperationalSaveData saved = operations.CreateSaveData();
            PartyOperationalRuntime restored = new PartyOperationalRuntime();
            restored.Configure(parties);
            Assert.That(restored.Restore(saved, out string restoreMessage), Is.True, restoreMessage);
            Assert.That(restored.GetSettings(party.PartyId).formation, Is.EqualTo(PartyFormation.Line));
            Assert.That(restored.GetReadyMemberIds(party.PartyId, "place.town"), Is.EquivalentTo(new[] { KnownPeople[0], KnownPeople[1] }));
            Assert.That(restored.GetMember(party.PartyId, KnownPeople[1]).questContribution, Is.EqualTo(4));
            Assert.That(PartyCombatRules.CanDamage(parties, restored, KnownPeople[0], KnownPeople[1]), Is.False);
        }

        [Test]
        public void PartyOperationalMigrationRepairsVersionOneAndRejectsDuplicateMembers()
        {
            PartyOperationalSaveData versionOne = new PartyOperationalSaveData { schemaVersion = 1, invitations = null };
            Assert.That(PartyOperationalRuntime.TryMigrate(versionOne, out PartyOperationalSaveData migrated, out string migrationMessage), Is.True, migrationMessage);
            Assert.That(migrated.schemaVersion, Is.EqualTo(PartyOperationalSaveData.CurrentSchemaVersion));
            Assert.That(migrated.invitations, Is.Not.Null);

            migrated.members.Add(new PartyMemberOperationalData { partyId = "party.a", personId = "person.a" });
            migrated.members.Add(new PartyMemberOperationalData { partyId = "party.a", personId = "person.a" });
            Assert.That(PartyOperationalRuntime.Validate(migrated, out string validationMessage), Is.False);
            Assert.That(validationMessage, Does.Contain("duplicate"));
        }

        [Test]
        public void PartyQuestRequiresReadyMembersAndAllowsCompanionObjectiveSignals()
        {
            QuestEligibilityContext context = BountyContext(KnownPeople[0], "party.prototype.player", KnownPeople[0], KnownPeople[1]);
            context.readyPartyMemberPersonIds = new[] { KnownPeople[0] };
            DefinitionRegistry registry = PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
            QuestRuntime quests = new QuestRuntime(registry, PersistenceService.LocalWorldId);
            QuestRuntimeOperationResult created = quests.CreateQuest(new QuestCreateRequest
            {
                transactionId = "tx.quest.ready.create", questId = "quest.runtime.ready", questDefinitionId = PrototypeQuestDefinitionFactory.DynamicBountyDefinitionId,
                repeatInstanceKey = "ready-test", issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open }, origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, interactionPointId = "interaction-point.prototype.bounty-board" }
            });
            Assert.That(created.Succeeded, Is.True, created.Message);
            QuestParticipationRuntime participation = new QuestParticipationRuntime(quests, registry, PersistenceService.LocalWorldId);
            QuestEligibilityResult eligibility = participation.EvaluateEligibility(created.Snapshot.QuestId, context);
            Assert.That(eligibility.Eligible, Is.False);
            Assert.That(eligibility.VisibleFailureReasons.Any(reason => reason.Contains("current: 1")), Is.True);
        }

        [Test]
        public void FormationSlotsAreDeterministicAndDistinct()
        {
            Assert.That(PartyFormationLayout.Offset(PartyFormation.Wedge, 0), Is.EqualTo(UnityEngine.Vector3.zero));
            Assert.That(PartyFormationLayout.Offset(PartyFormation.Wedge, 1), Is.Not.EqualTo(PartyFormationLayout.Offset(PartyFormation.Wedge, 2)));
            Assert.That(PartyFormationLayout.Offset(PartyFormation.Line, 3), Is.EqualTo(PartyFormationLayout.Offset(PartyFormation.Line, 3)));
        }

        private static QuestEligibilityContext BountyContext(string personId, string partyId, params string[] members)
        {
            return new QuestEligibilityContext
            {
                personId = personId,
                partyId = partyId ?? string.Empty,
                partyMemberPersonIds = members,
                readyPartyMemberPersonIds = members,
                interactionPointId = "interaction-point.prototype.bounty-board",
                privilegedDiagnostics = true,
                facts = new QuestEligibilityFactSet(authorityGrants: new[] { "authority.prototype.bounty-board.post" })
            };
        }
    }
}
