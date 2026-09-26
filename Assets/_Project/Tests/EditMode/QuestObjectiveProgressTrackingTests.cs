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
    public sealed class QuestObjectiveProgressTrackingTests
    {
        [Test]
        public void PrototypeQuestObjectivesRegisterAndValidate()
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
            Assert.That(guild.ObjectiveDefinitions.Count, Is.EqualTo(4));
            Assert.That(guild.ObjectiveDefinitions.SelectMany(objective => objective.prerequisiteObjectiveDefinitionIds), Does.Contain("quest-objective-definition.prototype.guild.use-counter"));
        }

        [Test]
        public void AssignmentCreatesObjectiveRuntimeRecordsWithoutCompletingQuest()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestAssignmentSnapshot assignment = fixture.CreateAcceptedGuildAssignment("instantiate");

            QuestObjectiveOperationResult instantiate = fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objectives.instantiate");
            QuestAssignmentObjectiveSummary summary = fixture.Objectives.SummarizeAssignment(assignment.AssignmentId, QuestVisibilityAccess.PrivilegedDiagnostic);

            Assert.That(instantiate.Succeeded, Is.True, instantiate.Message);
            Assert.That(instantiate.Objectives.Count, Is.EqualTo(4));
            Assert.That(instantiate.Objectives.Count(objective => objective.LifecycleState == QuestObjectiveLifecycleState.Active), Is.EqualTo(1));
            Assert.That(summary.CompletionCandidate, Is.False);
            Assert.That(fixture.Quests.TryGetSnapshot(assignment.QuestId, out QuestSnapshot quest), Is.True);
            Assert.That(quest.LifecycleState, Is.EqualTo(QuestRuntimeLifecycleState.Available));
        }

        [Test]
        public void EventProgressUnlocksSequenceAndRejectsDuplicateSourceEvents()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestAssignmentSnapshot assignment = fixture.CreateAcceptedGuildAssignment("sequence");
            fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objectives.sequence.instantiate");

            QuestObjectiveOperationResult counterEarly = fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "event.defeat.too-early"));
            QuestObjectiveOperationResult counter = fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", "event.use-counter"));
            QuestObjectiveOperationResult duplicate = fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", "event.use-counter"));
            QuestObjectiveOperationResult dungeon = fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.VisitLocation, "location.prototype.dungeon-entry", "event.enter-dungeon", InformationSubjectType.Location));
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "event.defeat.1"));
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "event.defeat.2"));
            QuestObjectiveOperationResult thirdDefeat = fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "event.defeat.3"));

            QuestObjectiveSnapshot defeat = fixture.Objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, objectiveDefinitionId = "quest-objective-definition.prototype.guild.defeat-monsters", access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();
            QuestObjectiveSnapshot report = fixture.Objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, objectiveDefinitionId = "quest-objective-definition.prototype.guild.report-return", access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();

            Assert.That(counterEarly.Succeeded, Is.False);
            Assert.That(counter.Succeeded, Is.True, counter.Message);
            Assert.That(duplicate.Status, Is.EqualTo(QuestObjectiveOperationStatus.AlreadyCounted));
            Assert.That(dungeon.Succeeded, Is.True, dungeon.Message);
            Assert.That(thirdDefeat.Succeeded, Is.True, thirdDefeat.Message);
            Assert.That(defeat.CurrentValue, Is.EqualTo(3));
            Assert.That(defeat.Satisfied, Is.True);
            Assert.That(report.LifecycleState, Is.EqualTo(QuestObjectiveLifecycleState.Active));
        }

        [Test]
        public void CurrentQuantityAndCumulativeQuantityRemainDistinct()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestAssignmentSnapshot assignment = fixture.CreateAcceptedDeliveryAssignment("items");
            fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objectives.items.instantiate");

            QuestObjectiveOperationResult current = fixture.Objectives.ReconcileState(new QuestObjectiveStateContext
            {
                assignmentId = assignment.AssignmentId,
                personId = assignment.AssigneePersonId,
                worldTime = 3d,
                facts = new QuestObjectiveStateFactSet(new[] { Fact(QuestObjectiveCategory.PossessItem, "item.prototype.merchant-parcel", 1) })
            });
            QuestObjectiveOperationResult collect = fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.ObtainItem, "item.prototype.merchant-parcel", "event.collect.parcel", InformationSubjectType.Custom, amount: 1, worldTime: 4d));
            QuestObjectiveOperationResult lost = fixture.Objectives.ReconcileState(new QuestObjectiveStateContext
            {
                assignmentId = assignment.AssignmentId,
                personId = assignment.AssigneePersonId,
                worldTime = 5d,
                facts = QuestObjectiveStateFactSet.Empty
            });

            QuestObjectiveSnapshot possess = fixture.Objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, objectiveDefinitionId = "quest-objective-definition.prototype.delivery.possess-parcel", access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();
            QuestObjectiveSnapshot collected = fixture.Objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, objectiveDefinitionId = "quest-objective-definition.prototype.delivery.collect-parcel", access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();

            Assert.That(current.Succeeded, Is.True, current.Message);
            Assert.That(collect.Succeeded, Is.True, collect.Message);
            Assert.That(lost.Succeeded, Is.True, lost.Message);
            Assert.That(possess.Satisfied, Is.True, "Prototype possession objective is sticky after first satisfaction.");
            Assert.That(collected.Satisfied, Is.True);
            Assert.That(collected.CountedSourceEventIds, Does.Contain("event.collect.parcel"));
        }

        [Test]
        public void HiddenObjectivesDoNotLeakThroughOrdinaryQueries()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestAssignmentSnapshot assignment = fixture.CreateAcceptedHiddenAssignment("hidden");
            fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objectives.hidden.instantiate");

            QuestObjectiveOperationResult discover = fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.DiscoverLocation, "location.prototype.secret-dungeon-entry", "event.secret-dungeon", InformationSubjectType.Location));
            int publicCount = fixture.Objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PublicOnly }).Count;
            int privilegedCount = fixture.Objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count;
            QuestAssignmentObjectiveSummary publicSummary = fixture.Objectives.SummarizeAssignment(assignment.AssignmentId, QuestVisibilityAccess.PublicOnly);

            Assert.That(discover.Succeeded, Is.True, discover.Message);
            Assert.That(publicCount, Is.EqualTo(0));
            Assert.That(privilegedCount, Is.EqualTo(2));
            Assert.That(publicSummary.HiddenCountsRedacted, Is.True);
            Assert.That(publicSummary.RequiredRemaining, Is.EqualTo(-1));
        }

        [Test]
        public void MultipleAssigneesProgressIndependently()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestAssignmentSnapshot first = fixture.CreateDirectBountyAssignment("first", "person.prototype.first");
            QuestAssignmentSnapshot second = fixture.CreateDirectBountyAssignment("second", "person.prototype.second");
            fixture.Objectives.InstantiateForAssignment(first, transactionId: "tx.quest.objectives.first");
            fixture.Objectives.InstantiateForAssignment(second, transactionId: "tx.quest.objectives.second");

            QuestObjectiveOperationResult firstDefeat = fixture.Objectives.ApplySignal(Signal(first, QuestObjectiveCategory.DefeatTarget, "encounter.prototype.dynamic-bounty-target", "event.bounty.first", InformationSubjectType.Custom, actor: "person.prototype.first"));
            QuestObjectiveSnapshot firstObjective = fixture.Objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = first.AssignmentId, category = QuestObjectiveCategory.DefeatTarget, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();
            QuestObjectiveSnapshot secondObjective = fixture.Objectives.QueryObjectives(new QuestObjectiveQuery { assignmentId = second.AssignmentId, category = QuestObjectiveCategory.DefeatTarget, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();

            Assert.That(firstDefeat.Succeeded, Is.True, firstDefeat.Message);
            Assert.That(firstObjective.Satisfied, Is.True);
            Assert.That(secondObjective.Satisfied, Is.False);
        }

        [Test]
        public void PersistenceRoundTripsAndFailedPrepareLeavesRuntimeUnchanged()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestAssignmentSnapshot assignment = fixture.CreateAcceptedGuildAssignment("persist");
            fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.quest.objectives.persist.instantiate");
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", "event.persist.counter"));
            QuestObjectiveProgressPersistenceParticipant participant = new QuestObjectiveProgressPersistenceParticipant(fixture.Objectives, () => fixture.Quests, () => fixture.Participation, () => fixture.Registry);
            PersistenceParticipantSaveResult save = participant.CapturePayload();

            QuestObjectiveProgressRuntime restored = new QuestObjectiveProgressRuntime(fixture.Quests, fixture.Participation, fixture.Registry);
            QuestObjectiveProgressPersistenceParticipant restoredParticipant = new QuestObjectiveProgressPersistenceParticipant(restored, () => fixture.Quests, () => fixture.Participation, () => fixture.Registry);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestObjectiveProgressPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            int restoredCount = restored.ObjectiveCount;
            int restoredEvents = restored.Events.Count;

            QuestObjectiveProgressRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.objectives[0].objectiveDefinitionId = "quest-objective-definition.missing";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestObjectiveProgressPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(restoredCount, Is.EqualTo(4));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.ObjectiveCount, Is.EqualTo(restoredCount));
            Assert.That(restored.Events.Count, Is.EqualTo(restoredEvents));
        }

        private static QuestObjectiveSignal Signal(QuestAssignmentSnapshot assignment, QuestObjectiveCategory category, string targetId, string sourceEventId, InformationSubjectType targetType = InformationSubjectType.Custom, int amount = 1, double worldTime = 10d, string actor = null)
        {
            return new QuestObjectiveSignal
            {
                transactionId = $"tx.{sourceEventId}",
                sourceEventId = sourceEventId,
                sourceRuntimeId = "test.domain",
                questId = assignment.QuestId,
                assignmentId = assignment.AssignmentId,
                actorPersonId = actor ?? assignment.AssigneePersonId,
                participantPersonId = assignment.AssigneePersonId,
                category = category,
                target = new InformationSubjectReferenceData { subjectType = targetType, subjectId = targetId },
                amount = amount,
                worldTime = worldTime,
                committed = true
            };
        }

        private static QuestObjectiveStateFactData Fact(QuestObjectiveCategory category, string targetId, int value)
        {
            return new QuestObjectiveStateFactData
            {
                category = category,
                target = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Custom, subjectId = targetId },
                value = value,
                sourceRuntimeId = "test.state",
                sourceRevision = 1
            };
        }

        private static DefinitionRegistry Registry()
        {
            return PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
        }

        private sealed class RuntimeFixture
        {
            private int questIndex;

            private RuntimeFixture(DefinitionRegistry registry, QuestRuntime quests, QuestParticipationRuntime participation, QuestObjectiveProgressRuntime objectives)
            {
                Registry = registry;
                Quests = quests;
                Participation = participation;
                Objectives = objectives;
            }

            public DefinitionRegistry Registry { get; }
            public QuestRuntime Quests { get; }
            public QuestParticipationRuntime Participation { get; }
            public QuestObjectiveProgressRuntime Objectives { get; }

            public static RuntimeFixture Create()
            {
                DefinitionRegistry registry = Registry();
                QuestRuntime quests = new QuestRuntime(registry, PersistenceService.LocalWorldId);
                QuestParticipationRuntime participation = new QuestParticipationRuntime(quests, registry, PersistenceService.LocalWorldId);
                QuestObjectiveProgressRuntime objectives = new QuestObjectiveProgressRuntime(quests, participation, registry, PersistenceService.LocalWorldId);
                return new RuntimeFixture(registry, quests, participation, objectives);
            }

            public QuestAssignmentSnapshot CreateAcceptedGuildAssignment(string suffix)
            {
                QuestRuntimeOperationResult create = CreateQuest(PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, suffix, QuestSourceChannel.QuestBoard);
                QuestParticipationOperationResult offer = Participation.CreateOffer(new QuestOfferRequest
                {
                    transactionId = $"tx.quest.offer.{suffix}",
                    questId = create.Snapshot.QuestId,
                    recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.player" },
                    institutionalIssuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                    offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild", actingPersonId = "person.prototype.guild-clerk" },
                    channel = QuestOfferChannel.GuildCounter,
                    sourceInteractionPointId = "interaction-point.prototype.adventurer-guild-counter",
                    sourceLocationId = "location.prototype.adventurers-guild",
                    authorityBasisId = "authority.prototype.guild.quest-offer",
                    eligibilityContext = EligibleContext("person.prototype.player"),
                    worldTime = 1d
                });
                QuestParticipationOperationResult accept = Participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = $"tx.quest.accept.{suffix}", offerId = offer.Offer.OfferId, personId = "person.prototype.player", explicitConsent = true, eligibilityContext = EligibleContext("person.prototype.player"), worldTime = 2d });
                return accept.Assignment;
            }

            public QuestAssignmentSnapshot CreateAcceptedDeliveryAssignment(string suffix)
            {
                QuestRuntimeOperationResult create = Quests.CreateQuest(new QuestCreateRequest
                {
                    transactionId = $"tx.quest.delivery.create.{suffix}",
                    questId = $"quest.runtime.delivery.{suffix}.{++questIndex:000}",
                    questDefinitionId = PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId,
                    issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Business, issuerId = "business.prototype.merchant" },
                    intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.player" },
                    origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Contract, locationId = "location.prototype.market", interactionPointId = "interaction-point.prototype.merchant-guild-counter" },
                    createdWorldTime = 1d
                });
                QuestParticipationOperationResult offer = Participation.CreateOffer(new QuestOfferRequest
                {
                    transactionId = $"tx.quest.delivery.offer.{suffix}",
                    questId = create.Snapshot.QuestId,
                    recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.player" },
                    institutionalIssuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Business, issuerId = "business.prototype.merchant" },
                    offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Business, issuerId = "business.prototype.merchant", actingPersonId = "person.prototype.merchant" },
                    channel = QuestOfferChannel.InteractionPoint,
                    sourceInteractionPointId = "interaction-point.prototype.merchant-guild-counter",
                    sourceLocationId = "location.prototype.market",
                    authorityBasisId = "authority.prototype.merchant.quest-offer",
                    eligibilityContext = new QuestEligibilityContext { personId = "person.prototype.player", interactionPointId = "interaction-point.prototype.merchant-guild-counter", privilegedDiagnostics = true, facts = new QuestEligibilityFactSet(authorityGrants: new[] { "authority.prototype.merchant.quest-offer" }) },
                    worldTime = 1d
                });
                QuestParticipationOperationResult accept = Participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = $"tx.quest.delivery.accept.{suffix}", offerId = offer.Offer.OfferId, personId = "person.prototype.player", explicitConsent = true, eligibilityContext = new QuestEligibilityContext { personId = "person.prototype.player", interactionPointId = "interaction-point.prototype.merchant-guild-counter", privilegedDiagnostics = true, facts = new QuestEligibilityFactSet(authorityGrants: new[] { "authority.prototype.merchant.quest-offer" }) }, worldTime = 2d });
                return accept.Assignment;
            }

            public QuestAssignmentSnapshot CreateAcceptedHiddenAssignment(string suffix)
            {
                QuestRuntimeOperationResult create = Quests.CreateQuest(new QuestCreateRequest
                {
                    transactionId = $"tx.quest.hidden.create.{suffix}",
                    questId = $"quest.runtime.hidden.{suffix}.{++questIndex:000}",
                    questDefinitionId = PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId,
                    issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                    intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                    origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Discovery, locationId = "location.prototype.tavern" },
                    createdWorldTime = 1d
                });
                QuestParticipationOperationResult offer = Participation.CreateOffer(new QuestOfferRequest
                {
                    transactionId = $"tx.quest.hidden.offer.{suffix}",
                    questId = create.Snapshot.QuestId,
                    recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.scout" },
                    offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                    channel = QuestOfferChannel.NarrativeEventPlaceholder,
                    eligibilityContext = new QuestEligibilityContext { personId = "person.prototype.scout", privilegedDiagnostics = true },
                    worldTime = 1d
                });
                QuestParticipationOperationResult accept = Participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = $"tx.quest.hidden.accept.{suffix}", offerId = offer.Offer.OfferId, personId = "person.prototype.scout", explicitConsent = true, eligibilityContext = new QuestEligibilityContext { personId = "person.prototype.scout", privilegedDiagnostics = true, facts = new QuestEligibilityFactSet(knownSubjects: new[] { "subject.prototype.hidden-dungeon" }, historyFacts: new[] { "history.prototype.heard-dungeon-rumor" }) }, worldTime = 2d });
                return accept.Assignment;
            }

            public QuestAssignmentSnapshot CreateDirectBountyAssignment(string suffix, string personId)
            {
                QuestRuntimeOperationResult create = Quests.CreateQuest(new QuestCreateRequest
                {
                    transactionId = $"tx.quest.bounty.{suffix}",
                    questDefinitionId = PrototypeQuestDefinitionFactory.DynamicBountyDefinitionId,
                    repeatInstanceKey = suffix,
                    issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                    intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                    origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, interactionPointId = "interaction-point.prototype.bounty-board" },
                    createdWorldTime = 1d
                });
                QuestParticipationOperationResult assign = Participation.DirectAssign(new QuestDirectAssignmentRequest
                {
                    transactionId = $"tx.quest.bounty.assign.{suffix}",
                    questId = create.Snapshot.QuestId,
                    assigneePersonId = personId,
                    explicitConsent = true,
                    assignedBy = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                    authorityBasisId = "authority.prototype.bounty-board.post",
                    eligibilityContext = new QuestEligibilityContext { personId = personId, interactionPointId = "interaction-point.prototype.bounty-board", privilegedDiagnostics = true, facts = new QuestEligibilityFactSet(authorityGrants: new[] { "authority.prototype.bounty-board.post" }) },
                    worldTime = 1d
                });
                return assign.Assignment;
            }

            private QuestRuntimeOperationResult CreateQuest(string definitionId, string suffix, QuestSourceChannel sourceChannel, string interactionPointId = "interaction-point.prototype.adventurer-guild-counter")
            {
                questIndex++;
                return Quests.CreateQuest(new QuestCreateRequest
                {
                    transactionId = $"tx.quest.create.{suffix}",
                    questId = $"quest.runtime.{suffix}.{questIndex:000}",
                    questDefinitionId = definitionId,
                    issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                    intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                    origin = new QuestOriginReferenceData { sourceChannel = sourceChannel, locationId = "location.prototype.adventurers-guild", interactionPointId = interactionPointId },
                    createdWorldTime = 1d
                });
            }

            private static QuestEligibilityContext EligibleContext(string personId)
            {
                return new QuestEligibilityContext
                {
                    personId = personId,
                    locationId = "location.prototype.adventurers-guild",
                    interactionPointId = "interaction-point.prototype.adventurer-guild-counter",
                    privilegedDiagnostics = true,
                    facts = new QuestEligibilityFactSet(
                        organizationMemberships: new[] { "organization.prototype.adventurers-guild" },
                        authorityGrants: new[] { "authority.prototype.guild.quest-offer" })
                };
            }
        }
    }
}
