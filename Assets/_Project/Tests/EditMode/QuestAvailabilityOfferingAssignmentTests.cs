using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Tests
{
    public sealed class QuestAvailabilityOfferingAssignmentTests
    {
        [Test]
        public void PrototypeParticipationPoliciesRegisterAndValidate()
        {
            DefinitionRegistry registry = Registry();
            DefinitionValidationReport report = new DefinitionValidationReport();

            foreach (QuestDefinition definition in PrototypeQuestDefinitionFactory.CreateMissingQuestDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
            Assert.That(registry.TryGet(PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, out QuestDefinition guild), Is.True);
            Assert.That(guild.AssignmentPolicy, Is.EqualTo(QuestAssignmentPolicy.Exclusive));
            Assert.That(guild.ConsentPolicy, Is.EqualTo(QuestConsentPolicy.ExplicitRecipientConsentRequired));
            Assert.That(guild.EligibilityRequirementGroups.Count, Is.EqualTo(1));
        }

        [Test]
        public void AvailabilityEligibilityOfferAcceptanceRemainDistinct()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestRuntimeOperationResult create = CreateGuildQuest(fixture.Quests, "tx.quest.guild.distinct", "quest.runtime.guild.distinct");

            QuestEligibilityResult ineligible = fixture.Participation.EvaluateEligibility(create.Snapshot.QuestId, Context("person.prototype.player"));
            QuestParticipationOperationResult preview = fixture.Participation.CreateOffer(OfferRequest(create.Snapshot.QuestId, "person.prototype.player", Context("person.prototype.player", eligible: true), preview: true));
            int countAfterPreview = fixture.Participation.OfferCount;
            QuestParticipationOperationResult offer = fixture.Participation.CreateOffer(OfferRequest(create.Snapshot.QuestId, "person.prototype.player", Context("person.prototype.player", eligible: true)));
            QuestParticipationOperationResult missingConsent = fixture.Participation.AcceptOffer(new QuestAcceptOfferRequest { offerId = offer.Offer.OfferId, personId = "person.prototype.player", eligibilityContext = Context("person.prototype.player", eligible: true), worldTime = 1d });
            QuestParticipationOperationResult accept = fixture.Participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.offer.accept", offerId = offer.Offer.OfferId, personId = "person.prototype.player", explicitConsent = true, consentRecordId = "consent.prototype.player.guild", eligibilityContext = Context("person.prototype.player", eligible: true), worldTime = 1d });

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(ineligible.Eligible, Is.False);
            Assert.That(ineligible.VisibleFailureReasons, Does.Contain("requirement.OrganizationMembership.organization.prototype.adventurers-guild.OrganizationMembership.missing"));
            Assert.That(preview.Status, Is.EqualTo(QuestParticipationOperationStatus.Preview));
            Assert.That(countAfterPreview, Is.EqualTo(0));
            Assert.That(offer.Succeeded, Is.True, offer.Message);
            Assert.That(missingConsent.Status, Is.EqualTo(QuestParticipationOperationStatus.ConsentRequired));
            Assert.That(accept.Succeeded, Is.True, accept.Message);
            Assert.That(accept.Assignment.AssigneePersonId, Is.EqualTo("person.prototype.player"));
            Assert.That(fixture.Participation.OfferCount, Is.EqualTo(1));
            Assert.That(fixture.Participation.AssignmentCount, Is.EqualTo(1));
        }

        [Test]
        public void AcceptanceRevalidatesAuthorityEligibilityAndCapacityAtomically()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestRuntimeOperationResult create = CreateGuildQuest(fixture.Quests, "tx.quest.guild.atomic", "quest.runtime.guild.atomic");
            QuestParticipationOperationResult firstOffer = fixture.Participation.CreateOffer(OfferRequest(create.Snapshot.QuestId, "person.prototype.first", Context("person.prototype.first", eligible: true), "tx.quest.offer.first"));
            QuestParticipationOperationResult secondOffer = fixture.Participation.CreateOffer(OfferRequest(create.Snapshot.QuestId, "person.prototype.second", Context("person.prototype.second", eligible: true), "tx.quest.offer.second"));

            QuestParticipationOperationResult firstAccept = fixture.Participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.accept.first", offerId = firstOffer.Offer.OfferId, personId = "person.prototype.first", explicitConsent = true, eligibilityContext = Context("person.prototype.first", eligible: true), worldTime = 2d });
            QuestParticipationOperationResult secondAccept = fixture.Participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.accept.second", offerId = secondOffer.Offer.OfferId, personId = "person.prototype.second", explicitConsent = true, eligibilityContext = Context("person.prototype.second", eligible: true), worldTime = 3d });

            Assert.That(firstOffer.Succeeded, Is.True, firstOffer.Message);
            Assert.That(secondOffer.Succeeded, Is.True, secondOffer.Message);
            Assert.That(firstAccept.Succeeded, Is.True, firstAccept.Message);
            Assert.That(secondAccept.Status, Is.EqualTo(QuestParticipationOperationStatus.Unavailable));
            Assert.That(fixture.Participation.AssignmentCount, Is.EqualTo(1));
            Assert.That(fixture.Participation.QueryAssignments(new QuestAssignmentQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single().AssigneePersonId, Is.EqualTo("person.prototype.first"));
        }

        [Test]
        public void AbandonmentReleasesExclusiveCapacityWhenPolicyAllowsIt()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestRuntimeOperationResult create = CreateGuildQuest(fixture.Quests, "tx.quest.guild.abandon", "quest.runtime.guild.abandon");
            QuestParticipationOperationResult offer = fixture.Participation.CreateOffer(OfferRequest(create.Snapshot.QuestId, "person.prototype.player", Context("person.prototype.player", eligible: true)));
            QuestParticipationOperationResult accept = fixture.Participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.accept.abandon", offerId = offer.Offer.OfferId, personId = "person.prototype.player", explicitConsent = true, eligibilityContext = Context("person.prototype.player", eligible: true), worldTime = 1d });
            QuestAvailabilityResult claimed = fixture.Participation.EvaluateAvailability(create.Snapshot.QuestId, Context("person.prototype.other", eligible: true));

            QuestParticipationOperationResult abandon = fixture.Participation.AbandonAssignment(new QuestAssignmentLifecycleRequest { transactionId = "tx.quest.assignment.abandon", assignmentId = accept.Assignment.AssignmentId, actingPersonId = "person.prototype.player", explicitConsent = true, worldTime = 5d });
            QuestAvailabilityResult after = fixture.Participation.EvaluateAvailability(create.Snapshot.QuestId, Context("person.prototype.other", eligible: true));

            Assert.That(claimed.Available, Is.False);
            Assert.That(claimed.State, Is.EqualTo(QuestAvailabilityState.ExclusivelyAssigned));
            Assert.That(abandon.Succeeded, Is.True, abandon.Message);
            Assert.That(after.Available, Is.True);
        }

        [Test]
        public void HiddenOffersAndAssignmentsDoNotLeakToOrdinaryQueries()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestRuntimeOperationResult create = fixture.Quests.CreateQuest(new QuestCreateRequest
            {
                transactionId = "tx.quest.hidden.participation",
                questId = "quest.runtime.hidden.participation",
                questDefinitionId = PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Anonymous },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Discovery },
                subjectLinks = new[] { Subject("location.prototype.secret-dungeon-entry", QuestSubjectRole.Location, InformationSubjectType.Location) }
            });
            QuestParticipationOperationResult offer = fixture.Participation.CreateOffer(new QuestOfferRequest
            {
                transactionId = "tx.quest.hidden.offer",
                questId = create.Snapshot.QuestId,
                recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.scout" },
                offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                channel = QuestOfferChannel.NarrativeEventPlaceholder,
                eligibilityContext = Context("person.prototype.scout", eligible: false),
                worldTime = 1d
            });

            Assert.That(offer.Succeeded, Is.True, offer.Message);
            Assert.That(fixture.Participation.QueryOffers(new QuestOfferQuery { access = QuestVisibilityAccess.PublicOnly }).Count, Is.EqualTo(0));
            Assert.That(fixture.Participation.QueryOffers(new QuestOfferQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count, Is.EqualTo(1));
            Assert.That(fixture.Participation.SummarizeQuestParticipation(create.Snapshot.QuestId, QuestVisibilityAccess.PublicOnly).CountsRedacted, Is.True);
        }

        [Test]
        public void ParticipationPersistenceRoundTripsAndFailedPrepareLeavesRuntimeUnchanged()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestRuntimeOperationResult create = CreateGuildQuest(fixture.Quests, "tx.quest.guild.persist", "quest.runtime.guild.persist");
            QuestParticipationOperationResult offer = fixture.Participation.CreateOffer(OfferRequest(create.Snapshot.QuestId, "person.prototype.player", Context("person.prototype.player", eligible: true)));
            QuestParticipationOperationResult accept = fixture.Participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = "tx.quest.accept.persist", offerId = offer.Offer.OfferId, personId = "person.prototype.player", explicitConsent = true, eligibilityContext = Context("person.prototype.player", eligible: true), worldTime = 2d });
            QuestParticipationRuntimePersistenceParticipant participant = new QuestParticipationRuntimePersistenceParticipant(fixture.Participation, () => fixture.Quests, () => fixture.Registry);
            PersistenceParticipantSaveResult save = participant.CapturePayload();

            QuestParticipationRuntime restored = new QuestParticipationRuntime(fixture.Quests, fixture.Registry);
            QuestParticipationRuntimePersistenceParticipant restoredParticipant = new QuestParticipationRuntimePersistenceParticipant(restored, () => fixture.Quests, () => fixture.Registry);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestParticipationRuntimePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            int restoredAssignments = restored.AssignmentCount;
            int restoredEvents = restored.Events.Count;

            QuestParticipationRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.worldId = "world.other";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestParticipationRuntimePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(accept.Succeeded, Is.True, accept.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(restoredAssignments, Is.EqualTo(1));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.AssignmentCount, Is.EqualTo(restoredAssignments));
            Assert.That(restored.Events.Count, Is.EqualTo(restoredEvents));
        }

        [Test]
        public void SnapshotsAreImmutableAfterParticipationMutation()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestRuntimeOperationResult create = CreateGuildQuest(fixture.Quests, "tx.quest.guild.snapshot", "quest.runtime.guild.snapshot");
            QuestParticipationOperationResult offer = fixture.Participation.CreateOffer(OfferRequest(create.Snapshot.QuestId, "person.prototype.player", Context("person.prototype.player", eligible: true)));
            QuestOfferSnapshot snapshot = offer.Offer;
            QuestRecipientReferenceData recipient = snapshot.Recipient;
            recipient.recipientId = "mutated";

            fixture.Participation.RefuseOffer(new QuestOfferLifecycleRequest { transactionId = "tx.quest.offer.refuse", offerId = snapshot.OfferId, worldTime = 3d });

            Assert.That(snapshot.LifecycleState, Is.EqualTo(QuestOfferLifecycleState.Active));
            Assert.That(snapshot.Recipient.recipientId, Is.EqualTo("person.prototype.player"));
            Assert.That(fixture.Participation.TryGetOffer(snapshot.OfferId, out QuestOfferSnapshot after), Is.True);
            Assert.That(after.LifecycleState, Is.EqualTo(QuestOfferLifecycleState.Refused));
        }

        private static DefinitionRegistry Registry()
        {
            return PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
        }

        private static QuestRuntimeOperationResult CreateGuildQuest(QuestRuntime runtime, string transactionId, string questId)
        {
            return runtime.CreateQuest(new QuestCreateRequest
            {
                transactionId = transactionId,
                questId = questId,
                questDefinitionId = PrototypeQuestDefinitionFactory.GuildPostingDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, locationId = "location.prototype.adventurers-guild", interactionPointId = "interaction-point.prototype.adventurer-guild-counter" },
                subjectLinks = new[] { Subject("location.prototype.dungeon-entry", QuestSubjectRole.Location, InformationSubjectType.Location) }
            });
        }

        private static QuestOfferRequest OfferRequest(string questId, string recipientPersonId, QuestEligibilityContext context, string transactionId = "tx.quest.offer.create", bool preview = false)
        {
            return new QuestOfferRequest
            {
                transactionId = transactionId,
                questId = questId,
                recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = recipientPersonId },
                institutionalIssuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild", actingPersonId = "person.prototype.guild-clerk" },
                channel = QuestOfferChannel.GuildCounter,
                sourceInteractionPointId = "interaction-point.prototype.adventurer-guild-counter",
                sourceLocationId = "location.prototype.adventurers-guild",
                authorityBasisId = "authority.prototype.guild.quest-offer",
                eligibilityContext = context,
                worldTime = 1d,
                preview = preview
            };
        }

        private static QuestEligibilityContext Context(string personId, bool eligible = false)
        {
            return new QuestEligibilityContext
            {
                personId = personId,
                interactionPointId = eligible ? "interaction-point.prototype.adventurer-guild-counter" : string.Empty,
                locationId = eligible ? "location.prototype.adventurers-guild" : string.Empty,
                worldTime = 1d,
                privilegedDiagnostics = true,
                facts = eligible
                    ? new QuestEligibilityFactSet(
                        organizationMemberships: new[] { "organization.prototype.adventurers-guild" },
                        authorityGrants: new[] { "authority.prototype.guild.quest-offer" })
                    : QuestEligibilityFactSet.Empty
            };
        }

        private static QuestSubjectLinkData Subject(string id, QuestSubjectRole role, InformationSubjectType type)
        {
            return new QuestSubjectLinkData
            {
                role = role,
                subject = new InformationSubjectReferenceData { subjectType = type, subjectId = id, tags = new[] { role.ToString().ToLowerInvariant() } },
                provenanceId = "test.quest.participation"
            };
        }

        private sealed class RuntimeFixture
        {
            private RuntimeFixture(DefinitionRegistry registry, QuestRuntime quests, QuestParticipationRuntime participation)
            {
                Registry = registry;
                Quests = quests;
                Participation = participation;
            }

            public DefinitionRegistry Registry { get; }
            public QuestRuntime Quests { get; }
            public QuestParticipationRuntime Participation { get; }

            public static RuntimeFixture Create()
            {
                DefinitionRegistry registry = Registry();
                QuestRuntime quests = new QuestRuntime(registry, PersistenceService.LocalWorldId);
                QuestParticipationRuntime participation = new QuestParticipationRuntime(quests, registry, PersistenceService.LocalWorldId);
                return new RuntimeFixture(registry, quests, participation);
            }
        }
    }
}
