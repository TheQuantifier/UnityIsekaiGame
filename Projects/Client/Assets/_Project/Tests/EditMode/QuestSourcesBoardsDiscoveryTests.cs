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
    public sealed class QuestSourcesBoardsDiscoveryTests
    {
        [Test]
        public void PrototypeQuestSourcesRegisterAndValidate()
        {
            DefinitionRegistry registry = Registry();
            DefinitionValidationReport report = new DefinitionValidationReport();

            foreach (QuestSourceDefinition definition in PrototypeQuestSourceDefinitionFactory.CreateMissingQuestSourceDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
            Assert.That(registry.TryGet(PrototypeQuestSourceDefinitionFactory.AdventurerGuildCounterDefinitionId, out QuestSourceDefinition counter), Is.True);
            Assert.That(counter.Category, Is.EqualTo(QuestSourceCategory.GuildCounter));
            Assert.That(counter.PublicationPolicy.maxActiveListings, Is.EqualTo(6));
            Assert.That(counter.SourceRoleIds, Does.Contain("quest-source-role.acceptance"));
        }

        [Test]
        public void SourceCanExistWithoutListingsAndPreservesSceneBinding()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestSourceOperationResult create = fixture.Sources.CreateSource(new QuestSourceCreateRequest
            {
                transactionId = "tx.15.5.source.empty",
                questSourceId = "quest-source.prototype.empty-archive",
                questSourceDefinitionId = PrototypeQuestSourceDefinitionFactory.EmptyArchiveDefinitionId,
                hostLocationId = "location.prototype.guild-archive",
                interactionPointId = "interaction-point.prototype.archive",
                sceneBindingKey = "scene.prototype.guild.archive",
                visibility = QuestSourceVisibility.Restricted,
                worldTime = 1d
            });

            QuestSourceBrowseResult ordinary = fixture.Sources.BrowseSource(new QuestSourceBrowseRequest { questSourceId = create.Source.QuestSourceId, access = QuestVisibilityAccess.PublicOnly });
            QuestSourceBrowseResult privileged = fixture.Sources.BrowseSource(new QuestSourceBrowseRequest { questSourceId = create.Source.QuestSourceId, access = QuestVisibilityAccess.PrivilegedDiagnostic });

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(fixture.Sources.SourceCount, Is.EqualTo(1));
            Assert.That(fixture.Sources.ListingCount, Is.EqualTo(0));
            Assert.That(create.Source.SceneBindingKey, Is.EqualTo("scene.prototype.guild.archive"));
            Assert.That(ordinary.Status, Is.EqualTo(QuestSourceOperationStatus.VisibilityDenied));
            Assert.That(privileged.Succeeded, Is.True, privileged.Message);
            Assert.That(privileged.VisibleCount, Is.EqualTo(0));
        }

        [Test]
        public void PublicationAppliesAuthorityFiltersCapacityAndDuplicateRules()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestSnapshot guildQuest = fixture.CreateGuildQuest("publish-guild");
            QuestSnapshot deliveryQuest = fixture.CreateDeliveryQuest("publish-delivery");
            QuestSourceSnapshot source = fixture.CreateGuildCounter("quest-source.prototype.guild-counter.publish");

            QuestSourceOperationResult unauthorized = fixture.Sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.15.5.publish.unauthorized", questSourceId = source.QuestSourceId, questId = guildQuest.QuestId, worldTime = 2d });
            QuestSourceOperationResult preview = fixture.Sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.15.5.publish.preview", questSourceId = source.QuestSourceId, questId = guildQuest.QuestId, publisherAuthorityId = "authority.prototype.guild.quest-offer", worldTime = 2d, preview = true });
            int previewListingCount = fixture.Sources.ListingCount;
            QuestSourceOperationResult published = fixture.Sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.15.5.publish.guild", questSourceId = source.QuestSourceId, questId = guildQuest.QuestId, publisherAuthorityId = "authority.prototype.guild.quest-offer", publisherPersonId = "person.prototype.guild-clerk", worldTime = 2d });
            QuestSourceOperationResult duplicate = fixture.Sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.15.5.publish.duplicate", questSourceId = source.QuestSourceId, questId = guildQuest.QuestId, publisherAuthorityId = "authority.prototype.guild.quest-offer", worldTime = 3d });
            QuestSourceOperationResult filtered = fixture.Sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.15.5.publish.filtered", questSourceId = source.QuestSourceId, questId = deliveryQuest.QuestId, publisherAuthorityId = "authority.prototype.guild.quest-offer", worldTime = 3d });

            Assert.That(unauthorized.Status, Is.EqualTo(QuestSourceOperationStatus.UnauthorizedPublisher));
            Assert.That(preview.Status, Is.EqualTo(QuestSourceOperationStatus.Preview));
            Assert.That(previewListingCount, Is.EqualTo(0));
            Assert.That(published.Succeeded, Is.True, published.Message);
            Assert.That(duplicate.Status, Is.EqualTo(QuestSourceOperationStatus.Duplicate));
            Assert.That(filtered.Status, Is.EqualTo(QuestSourceOperationStatus.SourceFilterRejected));
            Assert.That(fixture.Sources.ListingCount, Is.EqualTo(1));
        }

        [Test]
        public void BrowseAndInspectRecordDiscoveryWithoutCreatingOffers()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestSnapshot quest = fixture.CreateGuildQuest("discover");
            QuestSourceSnapshot source = fixture.CreateGuildCounter("quest-source.prototype.guild-counter.discovery");
            QuestSourceOperationResult publish = fixture.Sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.15.5.discovery.publish", questSourceId = source.QuestSourceId, questId = quest.QuestId, publisherAuthorityId = "authority.prototype.guild.quest-offer", worldTime = 2d });

            QuestSourceBrowseResult browse = fixture.Sources.BrowseSource(new QuestSourceBrowseRequest { transactionId = "tx.15.5.discovery.browse", questSourceId = source.QuestSourceId, requesterPersonId = "person.prototype.player", access = QuestVisibilityAccess.LocalKnowledge, eligibilityContext = EligibleContext("person.prototype.player"), recordDiscovery = true, worldTime = 3d });
            QuestListingInspectionResult inspect = fixture.Sources.InspectListing(new QuestListingInspectRequest { transactionId = "tx.15.5.discovery.inspect", questListingId = publish.Listing.QuestListingId, questSourceId = source.QuestSourceId, requesterPersonId = "person.prototype.player", access = QuestVisibilityAccess.LocalKnowledge, eligibilityContext = EligibleContext("person.prototype.player"), recordDiscovery = true, worldTime = 4d });

            Assert.That(browse.Succeeded, Is.True, browse.Message);
            Assert.That(browse.VisibleCount, Is.EqualTo(1));
            Assert.That(inspect.Succeeded, Is.True, inspect.Message);
            Assert.That(fixture.Sources.DiscoveryCount, Is.EqualTo(2));
            Assert.That(fixture.Participation.OfferCount, Is.EqualTo(0));
            Assert.That(fixture.Participation.AssignmentCount, Is.EqualTo(0));
            Assert.That(fixture.Sources.QueryDiscoveries("person.prototype.player").Select(value => value.discoveryKind), Does.Contain(QuestSourceDiscoveryKind.ListingDetailsKnown));
        }

        [Test]
        public void AcceptingExclusiveListingDelegatesToParticipationAndClaimsListing()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestSnapshot quest = fixture.CreateGuildQuest("accept");
            QuestSourceSnapshot source = fixture.CreateGuildCounter("quest-source.prototype.guild-counter.accept");
            QuestSourceOperationResult publish = fixture.Sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.15.5.accept.publish", questSourceId = source.QuestSourceId, questId = quest.QuestId, publisherAuthorityId = "authority.prototype.guild.quest-offer", publisherPersonId = "person.prototype.guild-clerk", worldTime = 2d });

            QuestSourceOperationResult preview = fixture.Sources.AcceptFromSource(new QuestSourceAcceptRequest { transactionId = "tx.15.5.accept.preview", questListingId = publish.Listing.QuestListingId, personId = "person.prototype.player", authorityBasisId = "authority.prototype.guild.quest-offer", eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 3d, preview = true });
            int previewAssignmentCount = fixture.Participation.AssignmentCount;
            QuestSourceOperationResult accept = fixture.Sources.AcceptFromSource(new QuestSourceAcceptRequest { transactionId = "tx.15.5.accept", questListingId = publish.Listing.QuestListingId, personId = "person.prototype.player", authorityBasisId = "authority.prototype.guild.quest-offer", eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 3d });
            QuestSourceBrowseResult after = fixture.Sources.BrowseSource(new QuestSourceBrowseRequest { questSourceId = source.QuestSourceId, access = QuestVisibilityAccess.LocalKnowledge, eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 4d });

            Assert.That(preview.Status, Is.EqualTo(QuestSourceOperationStatus.Preview));
            Assert.That(previewAssignmentCount, Is.EqualTo(0));
            Assert.That(accept.Succeeded, Is.True, accept.Message);
            Assert.That(accept.Assignment, Is.Not.Null);
            Assert.That(fixture.Participation.OfferCount, Is.EqualTo(1));
            Assert.That(fixture.Participation.AssignmentCount, Is.EqualTo(1));
            Assert.That(accept.Listing.LifecycleState, Is.EqualTo(QuestListingLifecycleState.Claimed));
            Assert.That(after.VisibleCount, Is.EqualTo(1));
            Assert.That(after.Listings.Single().Taken, Is.True);
        }

        [Test]
        public void ExpirationAndPersistenceAreDeterministicAndRejectCorruptPayload()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestSnapshot quest = fixture.CreateDeliveryQuest("persist");
            QuestSourceSnapshot source = fixture.CreateMerchantCounter("quest-source.prototype.merchant-counter.persist");
            QuestSourceOperationResult publish = fixture.Sources.PublishListing(new QuestListingPublishRequest { transactionId = "tx.15.5.persist.publish", questSourceId = source.QuestSourceId, questId = quest.QuestId, publisherAuthorityId = "authority.prototype.merchant.quest-offer", expirationWorldTime = 5d, worldTime = 2d });
            QuestSourceOperationResult firstExpire = fixture.Sources.EvaluateExpirations(5d, "tx.15.5.persist.expire").Single();
            QuestSourceOperationResult secondExpire = fixture.Sources.EvaluateExpirations(5d, "tx.15.5.persist.expire").SingleOrDefault();

            QuestSourcePersistenceParticipant participant = new QuestSourcePersistenceParticipant(fixture.Sources, () => fixture.Quests, () => fixture.Participation, () => fixture.Registry);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            QuestSourceRuntime restored = new QuestSourceRuntime(fixture.Quests, fixture.Participation, fixture.Registry, PersistenceService.LocalWorldId);
            QuestSourcePersistenceParticipant restoredParticipant = new QuestSourcePersistenceParticipant(restored, () => fixture.Quests, () => fixture.Participation, () => fixture.Registry);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestSourcePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            int restoredListings = restored.ListingCount;
            int restoredEvents = restored.Events.Count;

            QuestSourceRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.listings[0].questId = "quest.prototype.missing";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestSourcePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(publish.Succeeded, Is.True, publish.Message);
            Assert.That(firstExpire.Succeeded, Is.True, firstExpire.Message);
            Assert.That(firstExpire.Listing.LifecycleState, Is.EqualTo(QuestListingLifecycleState.Expired));
            Assert.That(secondExpire, Is.Null);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(restoredListings, Is.EqualTo(1));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.ListingCount, Is.EqualTo(restoredListings));
            Assert.That(restored.Events.Count, Is.EqualTo(restoredEvents));
        }

        private static DefinitionRegistry Registry()
        {
            return PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
        }

        private sealed class RuntimeFixture
        {
            private RuntimeFixture(DefinitionRegistry registry, QuestRuntime quests, QuestParticipationRuntime participation, QuestSourceRuntime sources)
            {
                Registry = registry;
                Quests = quests;
                Participation = participation;
                Sources = sources;
            }

            public DefinitionRegistry Registry { get; }
            public QuestRuntime Quests { get; }
            public QuestParticipationRuntime Participation { get; }
            public QuestSourceRuntime Sources { get; }

            public static RuntimeFixture Create()
            {
                DefinitionRegistry registry = Registry();
                QuestRuntime quests = new QuestRuntime(registry, PersistenceService.LocalWorldId);
                QuestParticipationRuntime participation = new QuestParticipationRuntime(quests, registry, PersistenceService.LocalWorldId);
                QuestSourceRuntime sources = new QuestSourceRuntime(quests, participation, registry, PersistenceService.LocalWorldId);
                return new RuntimeFixture(registry, quests, participation, sources);
            }

            public QuestSourceSnapshot CreateGuildCounter(string sourceId)
            {
                QuestSourceOperationResult create = Sources.CreateSource(new QuestSourceCreateRequest
                {
                    transactionId = $"tx.{sourceId}.create",
                    questSourceId = sourceId,
                    questSourceDefinitionId = PrototypeQuestSourceDefinitionFactory.AdventurerGuildCounterDefinitionId,
                    hostLocationId = "location.prototype.adventurers-guild",
                    interactionPointId = "interaction-point.prototype.adventurer-guild-counter",
                    operatingOrganizationId = "organization.prototype.adventurers-guild",
                    sceneBindingKey = "scene.prototype.guild.counter",
                    worldTime = 1d
                });
                Assert.That(create.Succeeded, Is.True, create.Message);
                return create.Source;
            }

            public QuestSourceSnapshot CreateMerchantCounter(string sourceId)
            {
                QuestSourceOperationResult create = Sources.CreateSource(new QuestSourceCreateRequest
                {
                    transactionId = $"tx.{sourceId}.create",
                    questSourceId = sourceId,
                    questSourceDefinitionId = PrototypeQuestSourceDefinitionFactory.MerchantGuildCounterDefinitionId,
                    hostLocationId = "location.prototype.market-stall",
                    interactionPointId = "interaction-point.prototype.merchant-guild-counter",
                    operatingOrganizationId = "organization.prototype.merchant-guild",
                    sceneBindingKey = "scene.prototype.guild.merchant-counter",
                    worldTime = 1d
                });
                Assert.That(create.Succeeded, Is.True, create.Message);
                return create.Source;
            }

            public QuestSnapshot CreateGuildQuest(string suffix)
            {
                QuestRuntimeOperationResult create = Quests.CreateQuest(new QuestCreateRequest
                {
                    transactionId = $"tx.15.5.guild.create.{suffix}",
                    questId = $"quest.runtime.15.5.guild.{suffix}",
                    questDefinitionId = PrototypeQuestDefinitionFactory.GuildPostingDefinitionId,
                    issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                    intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                    origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, locationId = "location.prototype.adventurers-guild", interactionPointId = "interaction-point.prototype.adventurer-guild-counter" },
                    subjectLinks = new[] { Subject("location.prototype.dungeon-entry", QuestSubjectRole.Location, InformationSubjectType.Location) },
                    createdWorldTime = 1d
                });
                Assert.That(create.Succeeded, Is.True, create.Message);
                return create.Snapshot;
            }

            public QuestSnapshot CreateDeliveryQuest(string suffix)
            {
                QuestRuntimeOperationResult create = Quests.CreateQuest(new QuestCreateRequest
                {
                    transactionId = $"tx.15.5.delivery.create.{suffix}",
                    questId = $"quest.runtime.15.5.delivery.{suffix}",
                    questDefinitionId = PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId,
                    issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.merchant-guild" },
                    intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.player" },
                    origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Contract, locationId = "location.prototype.market-stall", interactionPointId = "interaction-point.prototype.merchant-guild-counter" },
                    subjectLinks = new[] { Subject("item.prototype.merchant-parcel", QuestSubjectRole.Item, InformationSubjectType.Custom) },
                    createdWorldTime = 1d
                });
                Assert.That(create.Succeeded, Is.True, create.Message);
                return create.Snapshot;
            }
        }

        private static QuestEligibilityContext EligibleContext(string personId)
        {
            return new QuestEligibilityContext
            {
                personId = personId,
                locationId = "location.prototype.adventurers-guild",
                interactionPointId = "interaction-point.prototype.adventurer-guild-counter",
                privilegedDiagnostics = true,
                worldTime = 1d,
                facts = new QuestEligibilityFactSet(
                    organizationMemberships: new[] { "organization.prototype.adventurers-guild" },
                    authorityGrants: new[] { "authority.prototype.guild.quest-offer" })
            };
        }

        private static QuestSubjectLinkData Subject(string id, QuestSubjectRole role, InformationSubjectType type)
        {
            return new QuestSubjectLinkData
            {
                role = role,
                subject = new InformationSubjectReferenceData { subjectType = type, subjectId = id, tags = new[] { role.ToString().ToLowerInvariant() } },
                provenanceId = "test.quest.sources"
            };
        }
    }
}
