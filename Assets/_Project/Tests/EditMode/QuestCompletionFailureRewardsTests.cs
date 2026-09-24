using System;
using System.Collections.Generic;
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
    public sealed class QuestCompletionFailureRewardsTests
    {
        [Test]
        public void PrototypeOutcomePoliciesRegisterAndValidate()
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
            Assert.That(guild.CompletionPolicy.policy, Is.EqualTo(QuestCompletionPolicy.RequireTurnIn));
            Assert.That(guild.DeadlineDefinitions.Count, Is.EqualTo(1));
            Assert.That(guild.RewardPackages.SelectMany(package => package.rewards).Count(), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void TurnInCompletionCreatesTerminalOutcomeAndClaimableRewards()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestAssignmentSnapshot assignment = fixture.AcceptedGuildAssignment("complete");
            fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.15.4.complete.objectives");
            fixture.Outcomes.TrackAssignment(assignment, "tx.15.4.complete.track");
            CompleteGuildObjectives(fixture, assignment);

            QuestCompletionEvaluationResult wrongCounter = fixture.Outcomes.EvaluateCompletion(new QuestCompletionEvaluationRequest { assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.other", worldTime = 3d });
            QuestOutcomeOperationResult complete = fixture.Outcomes.Complete(new QuestCompletionRequest { transactionId = "tx.15.4.complete.turn-in", assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.adventurer-guild-counter", issuerId = "organization.prototype.guild", locationId = "location.prototype.adventurers-guild", worldTime = 4d });
            QuestOutcomeOperationResult duplicate = fixture.Outcomes.Complete(new QuestCompletionRequest { transactionId = "tx.15.4.complete.turn-in", assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.adventurer-guild-counter", worldTime = 5d });

            Assert.That(wrongCounter.Status, Is.EqualTo(QuestOutcomeOperationStatus.TurnInRequired));
            Assert.That(complete.Succeeded, Is.True, complete.Message);
            Assert.That(complete.Outcome.OutcomeKind, Is.EqualTo(QuestTerminalOutcomeKind.Completed));
            Assert.That(complete.Rewards.Count, Is.EqualTo(2));
            Assert.That(complete.Rewards.All(reward => reward.State == QuestRewardEntitlementState.Claimable), Is.True);
            Assert.That(duplicate.Status, Is.EqualTo(QuestOutcomeOperationStatus.Duplicate));
            Assert.That(fixture.Quests.TryGetSnapshot(assignment.QuestId, out QuestSnapshot quest), Is.True);
            Assert.That(quest.LifecycleState, Is.EqualTo(QuestRuntimeLifecycleState.Available));
        }

        [Test]
        public void DeadlineFailureIsDeterministicAndCompletionCannotFollow()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            QuestAssignmentSnapshot assignment = fixture.AcceptedGuildAssignment("deadline");
            fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.15.4.deadline.objectives");
            fixture.Outcomes.TrackAssignment(assignment, "tx.15.4.deadline.track");

            QuestOutcomeOperationResult first = fixture.Outcomes.EvaluateDeadlines(assignment.AssignedWorldTime + 3d, "tx.15.4.deadline");
            QuestOutcomeOperationResult second = fixture.Outcomes.EvaluateDeadlines(assignment.AssignedWorldTime + 3d, "tx.15.4.deadline");
            QuestCompletionEvaluationResult completion = fixture.Outcomes.EvaluateCompletion(new QuestCompletionEvaluationRequest { assignmentId = assignment.AssignmentId, interactionPointId = "interaction-point.prototype.adventurer-guild-counter", worldTime = assignment.AssignedWorldTime + 3.1d });

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.Outcome.OutcomeKind, Is.EqualTo(QuestTerminalOutcomeKind.Expired));
            Assert.That(second.Status, Is.EqualTo(QuestOutcomeOperationStatus.Duplicate));
            Assert.That(completion.Status, Is.EqualTo(QuestOutcomeOperationStatus.AlreadyTerminal));
            Assert.That(fixture.Outcomes.QueryOutcomes(new QuestOutcomeQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count, Is.EqualTo(1));
        }

        [Test]
        public void ClaimRewardDelegatesToOwnerRuntimeAndIsIdempotent()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(new RecordingRewardExecutor());
            QuestAssignmentSnapshot assignment = fixture.AcceptedGuildAssignment("reward");
            fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.15.4.reward.objectives");
            CompleteGuildObjectives(fixture, assignment);
            QuestOutcomeOperationResult complete = fixture.Outcomes.Complete(new QuestCompletionRequest { transactionId = "tx.15.4.reward.complete", assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.adventurer-guild-counter", worldTime = 4d });
            QuestRewardEntitlementSnapshot reward = complete.Rewards.First(value => value.Category == QuestRewardCategory.Currency);

            QuestOutcomeOperationResult claim = fixture.Outcomes.ClaimReward(new QuestRewardClaimRequest { transactionId = "tx.15.4.reward.claim", entitlementId = reward.EntitlementId, claimantPersonId = assignment.AssigneePersonId, worldTime = 5d });
            QuestOutcomeOperationResult duplicate = fixture.Outcomes.ClaimReward(new QuestRewardClaimRequest { transactionId = "tx.15.4.reward.claim", entitlementId = reward.EntitlementId, claimantPersonId = assignment.AssigneePersonId, worldTime = 6d });

            Assert.That(claim.Succeeded, Is.True, claim.Message);
            Assert.That(claim.Reward.State, Is.EqualTo(QuestRewardEntitlementState.Granted));
            Assert.That(duplicate.Status, Is.EqualTo(QuestOutcomeOperationStatus.Duplicate));
            Assert.That(fixture.Executor.Requests.Count, Is.EqualTo(1));
            Assert.That(fixture.Executor.Requests[0].category, Is.EqualTo(QuestRewardCategory.Currency));
        }

        [Test]
        public void HiddenRewardsRedactForOrdinaryQueries()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(new RecordingRewardExecutor());
            QuestAssignmentSnapshot assignment = fixture.AcceptedHiddenAssignment("hidden");
            fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.15.4.hidden.objectives");
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.DiscoverLocation, "location.prototype.secret-dungeon-entry", "event.15.4.hidden.discover", InformationSubjectType.Location));

            QuestOutcomeOperationResult complete = fixture.Outcomes.Complete(new QuestCompletionRequest { transactionId = "tx.15.4.hidden.complete", assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, worldTime = 4d });
            Assert.That(complete.Succeeded, Is.True, complete.Message);
            QuestRewardEntitlementSnapshot publicReward = fixture.Outcomes.QueryRewards(new QuestRewardQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PublicOnly, includeHidden = true, includeTerminal = true }).Single();
            QuestRewardEntitlementSnapshot privilegedReward = fixture.Outcomes.QueryRewards(new QuestRewardQuery { assignmentId = assignment.AssignmentId, access = QuestVisibilityAccess.PrivilegedDiagnostic, includeTerminal = true }).Single();

            Assert.That(publicReward.Redacted, Is.True);
            Assert.That(publicReward.TargetDefinitionId, Is.EqualTo(string.Empty));
            Assert.That(privilegedReward.Redacted, Is.False);
            Assert.That(privilegedReward.TargetDefinitionId, Is.EqualTo("knowledge.prototype.hidden-dungeon-confirmed"));
        }

        [Test]
        public void OutcomePersistenceRoundTripsAndRejectsCorruptPayloadBeforeMutation()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(new RecordingRewardExecutor());
            QuestAssignmentSnapshot assignment = fixture.AcceptedGuildAssignment("persist");
            fixture.Objectives.InstantiateForAssignment(assignment, transactionId: "tx.15.4.persist.objectives");
            fixture.Outcomes.TrackAssignment(assignment, "tx.15.4.persist.track");
            CompleteGuildObjectives(fixture, assignment);
            fixture.Outcomes.Complete(new QuestCompletionRequest { transactionId = "tx.15.4.persist.complete", assignmentId = assignment.AssignmentId, requesterPersonId = assignment.AssigneePersonId, interactionPointId = "interaction-point.prototype.adventurer-guild-counter", worldTime = 4d });

            QuestOutcomePersistenceParticipant participant = fixture.Participant(fixture.Outcomes);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            QuestOutcomeRuntime restored = new QuestOutcomeRuntime(fixture.Quests, fixture.Participation, fixture.Objectives, fixture.Registry, fixture.Executor, PersistenceService.LocalWorldId);
            QuestOutcomePersistenceParticipant restoredParticipant = fixture.Participant(restored);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestOutcomePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            int restoredOutcomes = restored.TerminalOutcomeCount;
            int restoredRewards = restored.RewardEntitlementCount;

            QuestOutcomeRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.terminalOutcomes[0].assignmentId = "quest-assignment.missing";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestOutcomePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(restoredOutcomes, Is.EqualTo(1));
            Assert.That(restoredRewards, Is.EqualTo(2));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.TerminalOutcomeCount, Is.EqualTo(restoredOutcomes));
            Assert.That(restored.RewardEntitlementCount, Is.EqualTo(restoredRewards));
        }

        private static void CompleteGuildObjectives(RuntimeFixture fixture, QuestAssignmentSnapshot assignment)
        {
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", "event.15.4.counter"));
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.VisitLocation, "location.prototype.dungeon-entry", "event.15.4.dungeon", InformationSubjectType.Location));
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "event.15.4.defeat.1"));
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "event.15.4.defeat.2"));
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", "event.15.4.defeat.3"));
            fixture.Objectives.ApplySignal(Signal(assignment, QuestObjectiveCategory.UseInteractionPoint, "interaction-point.prototype.adventurer-guild-counter", "event.15.4.report"));
        }

        private static QuestObjectiveSignal Signal(QuestAssignmentSnapshot assignment, QuestObjectiveCategory category, string targetId, string sourceEventId, InformationSubjectType targetType = InformationSubjectType.Custom)
        {
            return new QuestObjectiveSignal
            {
                transactionId = $"tx.{sourceEventId}",
                sourceEventId = sourceEventId,
                sourceRuntimeId = "test.domain",
                questId = assignment.QuestId,
                assignmentId = assignment.AssignmentId,
                actorPersonId = assignment.AssigneePersonId,
                participantPersonId = assignment.AssigneePersonId,
                category = category,
                target = new InformationSubjectReferenceData { subjectType = targetType, subjectId = targetId },
                amount = 1,
                worldTime = 3d,
                committed = true
            };
        }

        private static DefinitionRegistry Registry()
        {
            return PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
        }

        private sealed class RuntimeFixture
        {
            private int questIndex;

            private RuntimeFixture(DefinitionRegistry registry, QuestRuntime quests, QuestParticipationRuntime participation, QuestObjectiveProgressRuntime objectives, QuestOutcomeRuntime outcomes, RecordingRewardExecutor executor)
            {
                Registry = registry;
                Quests = quests;
                Participation = participation;
                Objectives = objectives;
                Outcomes = outcomes;
                Executor = executor;
            }

            public DefinitionRegistry Registry { get; }
            public QuestRuntime Quests { get; }
            public QuestParticipationRuntime Participation { get; }
            public QuestObjectiveProgressRuntime Objectives { get; }
            public QuestOutcomeRuntime Outcomes { get; }
            public RecordingRewardExecutor Executor { get; }

            public static RuntimeFixture Create(RecordingRewardExecutor executor = null)
            {
                DefinitionRegistry registry = Registry();
                QuestRuntime quests = new QuestRuntime(registry, PersistenceService.LocalWorldId);
                QuestParticipationRuntime participation = new QuestParticipationRuntime(quests, registry, PersistenceService.LocalWorldId);
                QuestObjectiveProgressRuntime objectives = new QuestObjectiveProgressRuntime(quests, participation, registry, PersistenceService.LocalWorldId);
                RecordingRewardExecutor rewardExecutor = executor ?? new RecordingRewardExecutor();
                QuestOutcomeRuntime outcomes = new QuestOutcomeRuntime(quests, participation, objectives, registry, rewardExecutor, PersistenceService.LocalWorldId);
                return new RuntimeFixture(registry, quests, participation, objectives, outcomes, rewardExecutor);
            }

            public QuestAssignmentSnapshot AcceptedGuildAssignment(string suffix)
            {
                QuestRuntimeOperationResult create = Quests.CreateQuest(new QuestCreateRequest
                {
                    transactionId = $"tx.15.4.guild.create.{suffix}",
                    questId = $"quest.runtime.15.4.guild.{suffix}.{++questIndex:000}",
                    questDefinitionId = PrototypeQuestDefinitionFactory.GuildPostingDefinitionId,
                    issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.guild" },
                    intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.player" },
                    origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, locationId = "location.prototype.adventurers-guild", interactionPointId = "interaction-point.prototype.adventurer-guild-counter" },
                    createdWorldTime = 1d
                });
                QuestParticipationOperationResult offer = Participation.CreateOffer(new QuestOfferRequest
                {
                    transactionId = $"tx.15.4.guild.offer.{suffix}",
                    questId = create.Snapshot.QuestId,
                    recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.player" },
                    institutionalIssuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.guild" },
                    offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.guild", actingPersonId = "person.prototype.guild-clerk" },
                    channel = QuestOfferChannel.GuildCounter,
                    sourceInteractionPointId = "interaction-point.prototype.adventurer-guild-counter",
                    sourceLocationId = "location.prototype.adventurers-guild",
                    authorityBasisId = "authority.prototype.guild.quest-offer",
                    eligibilityContext = EligibleContext(),
                    worldTime = 1d
                });
                QuestParticipationOperationResult accept = Participation.AcceptOffer(new QuestAcceptOfferRequest { transactionId = $"tx.15.4.guild.accept.{suffix}", offerId = offer.Offer.OfferId, personId = "person.prototype.player", explicitConsent = true, eligibilityContext = EligibleContext(), worldTime = 2d });
                Assert.That(accept.Succeeded, Is.True, accept.Message);
                return accept.Assignment;
            }

            public QuestAssignmentSnapshot AcceptedHiddenAssignment(string suffix)
            {
                QuestRuntimeOperationResult create = Quests.CreateQuest(new QuestCreateRequest
                {
                    transactionId = $"tx.15.4.hidden.create.{suffix}",
                    questId = $"quest.runtime.15.4.hidden.{suffix}.{++questIndex:000}",
                    questDefinitionId = PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId,
                    issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                    intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                    origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Discovery },
                    visibility = QuestVisibility.Hidden,
                    createdWorldTime = 1d
                });
                Assert.That(create.Succeeded, Is.True, create.Message);
                QuestParticipationOperationResult direct = Participation.DirectAssign(new QuestDirectAssignmentRequest
                {
                    transactionId = $"tx.15.4.hidden.assign.{suffix}",
                    questId = create.Snapshot.QuestId,
                    assigneePersonId = "person.prototype.scout",
                    assignedBy = new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" },
                    explicitConsent = true,
                    eligibilityContext = new QuestEligibilityContext { personId = "person.prototype.scout", worldTime = 1d, privilegedDiagnostics = true, facts = new QuestEligibilityFactSet(knownSubjects: new[] { "subject.prototype.hidden-dungeon" }) },
                    worldTime = 2d,
                    visibility = QuestVisibility.Hidden
                });
                Assert.That(direct.Succeeded, Is.True, direct.Message);
                return direct.Assignment;
            }

            public QuestOutcomePersistenceParticipant Participant(QuestOutcomeRuntime runtime)
            {
                return new QuestOutcomePersistenceParticipant(runtime, () => Quests, () => Participation, () => Objectives, () => Registry, () => Executor);
            }

            private static QuestEligibilityContext EligibleContext()
            {
                return new QuestEligibilityContext
                {
                    personId = "person.prototype.player",
                    interactionPointId = "interaction-point.prototype.adventurer-guild-counter",
                    locationId = "location.prototype.adventurers-guild",
                    worldTime = 1d,
                    privilegedDiagnostics = true,
                    facts = new QuestEligibilityFactSet(
                        organizationMemberships: new[] { "organization.prototype.adventurers-guild" },
                        authorityGrants: new[] { "authority.prototype.guild.quest-offer" })
                };
            }
        }

        private sealed class RecordingRewardExecutor : IQuestRewardEffectExecutor
        {
            public readonly List<QuestRewardEffectRequest> Requests = new List<QuestRewardEffectRequest>();

            public QuestRewardEffectResult Execute(QuestRewardEffectRequest request)
            {
                Requests.Add(request);
                return QuestRewardEffectResult.Success($"owner.{request.category.ToString().ToLowerInvariant()}", $"{request.category}.{request.targetDefinitionId}.{request.quantity}");
            }
        }
    }
}
