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
    public sealed class QuestIdentityRuntimeRecordsTests
    {
        [Test]
        public void PrototypeQuestDefinitionsRegisterAndValidate()
        {
            DefinitionRegistry registry = Registry();
            DefinitionValidationReport report = new DefinitionValidationReport();

            foreach (QuestDefinition definition in PrototypeQuestDefinitionFactory.CreateMissingQuestDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
            Assert.That(registry.TryGet(PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, out QuestDefinition guildPosting), Is.True);
            Assert.That(guildPosting.Id, Is.EqualTo(PrototypeQuestDefinitionFactory.GuildPostingDefinitionId));
            Assert.That(guildPosting.Category, Is.EqualTo(QuestCategory.GuildQuest));
            Assert.That(guildPosting.RepeatabilityPolicy, Is.EqualTo(QuestDefinitionRepeatabilityPolicy.Unique));
            Assert.That(guildPosting.DefaultSourceChannel, Is.EqualTo(QuestSourceChannel.QuestBoard));
        }

        [Test]
        public void QuestDefinitionAndRuntimeInstanceIdentityRemainSeparate()
        {
            QuestRuntime runtime = Runtime();

            QuestRuntimeOperationResult unique = CreateGuildQuest(runtime, "tx.quest.unique", "quest.runtime.guild-a");
            QuestRuntimeOperationResult duplicateUnique = CreateGuildQuest(runtime, "tx.quest.unique-other", "quest.runtime.guild-b");
            QuestRuntimeOperationResult bountyA = CreateBounty(runtime, "tx.quest.bounty-a", "wolf");
            QuestRuntimeOperationResult bountyB = CreateBounty(runtime, "tx.quest.bounty-b", "slime");

            Assert.That(unique.Succeeded, Is.True, unique.Message);
            Assert.That(duplicateUnique.Status, Is.EqualTo(QuestRuntimeOperationStatus.UniqueQuestAlreadyExists));
            Assert.That(bountyA.Succeeded, Is.True, bountyA.Message);
            Assert.That(bountyB.Succeeded, Is.True, bountyB.Message);
            Assert.That(bountyA.Snapshot.QuestDefinitionId, Is.EqualTo(bountyB.Snapshot.QuestDefinitionId));
            Assert.That(bountyA.Snapshot.QuestId, Is.Not.EqualTo(bountyB.Snapshot.QuestId));
            Assert.That(runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count, Is.EqualTo(3));
        }

        [Test]
        public void PreviewDuplicateAndRevisionDoNotMutateAuthoritativeQuestState()
        {
            QuestRuntime runtime = Runtime();
            long before = runtime.Revision;

            QuestRuntimeOperationResult preview = runtime.CreateQuest(new QuestCreateRequest
            {
                preview = true,
                questDefinitionId = PrototypeQuestDefinitionFactory.DynamicBountyDefinitionId,
                repeatInstanceKey = "preview",
                issuer = OrganizationIssuer(),
                intendedRecipient = OpenRecipient(),
                origin = GuildBoardOrigin(),
                subjectLinks = new[] { Subject("encounter.prototype.preview", QuestSubjectRole.Encounter, InformationSubjectType.Custom) }
            });
            QuestRuntimeOperationResult create = CreateBounty(runtime, "tx.quest.idempotent", "bear");
            QuestRuntimeOperationResult duplicate = CreateBounty(runtime, "tx.quest.idempotent", "bear");
            QuestRuntimeOperationResult stale = runtime.TransitionLifecycle(new QuestLifecycleTransitionRequest
            {
                transactionId = "tx.quest.stale",
                questId = create.Snapshot.QuestId,
                targetState = QuestRuntimeLifecycleState.Suspended,
                expectedRevision = before
            });

            Assert.That(preview.Status, Is.EqualTo(QuestRuntimeOperationStatus.Preview));
            Assert.That(runtime.Revision, Is.Not.EqualTo(before));
            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(stale.Status, Is.EqualTo(QuestRuntimeOperationStatus.RevisionConflict));
            Assert.That(runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count, Is.EqualTo(1));
        }

        [Test]
        public void IssuerRecipientOriginAndSubjectLinksRemainReferenceOnly()
        {
            QuestRuntime runtime = Runtime();
            QuestRuntimeOperationResult result = runtime.CreateQuest(new QuestCreateRequest
            {
                transactionId = "tx.quest.references",
                questId = "quest.runtime.civic-investigation",
                questDefinitionId = PrototypeQuestDefinitionFactory.CivicInvestigationDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Government, issuerId = "government.prototype.civic", actingPersonId = "person.prototype.mayor" },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = "person.prototype.player" },
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Government, locationId = "location.prototype.civic-office", interactionPointId = "interaction-point.prototype.civic-desk" },
                subjectLinks = new[]
                {
                    Subject("person.prototype.witness", QuestSubjectRole.Person, InformationSubjectType.PersonIdentity),
                    Subject("item.prototype-sword", QuestSubjectRole.Item, InformationSubjectType.Custom),
                    Subject("organization.prototype.adventurers-guild", QuestSubjectRole.Organization, InformationSubjectType.Organization),
                    Subject("government.prototype.civic", QuestSubjectRole.Government, InformationSubjectType.Custom),
                    Subject("location.prototype.basement-prison", QuestSubjectRole.Location, InformationSubjectType.Location),
                    Subject("knowledge.prototype.quest-incident", QuestSubjectRole.Incident, InformationSubjectType.KnowledgeRecord),
                    Subject("journey.prototype.delivery", QuestSubjectRole.Journey, InformationSubjectType.Custom),
                    Subject("encounter.prototype.ambush", QuestSubjectRole.Encounter, InformationSubjectType.Custom)
                }
            });

            QuestSnapshot snapshot = result.Snapshot;
            InformationSubjectReferenceData subject = snapshot.CreateInformationSubject();

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(snapshot.SubjectLinks.Count, Is.EqualTo(8));
            Assert.That(snapshot.Issuer.issuerId, Is.EqualTo("government.prototype.civic"));
            Assert.That(snapshot.IntendedRecipient.recipientId, Is.EqualTo("person.prototype.player"));
            Assert.That(snapshot.Origin.interactionPointId, Is.EqualTo("interaction-point.prototype.civic-desk"));
            Assert.That(subject.subjectType, Is.EqualTo(InformationSubjectType.Custom));
            Assert.That(subject.tags, Does.Contain(QuestInformationSubject.QuestTag));
        }

        [Test]
        public void HiddenQuestQueriesDoNotLeakCountsToOrdinaryCallers()
        {
            QuestRuntime runtime = Runtime();
            CreateGuildQuest(runtime, "tx.quest.public", "quest.runtime.public");
            QuestRuntimeOperationResult hidden = runtime.CreateQuest(new QuestCreateRequest
            {
                transactionId = "tx.quest.hidden",
                questId = "quest.runtime.hidden",
                questDefinitionId = PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId,
                issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Anonymous },
                intendedRecipient = OpenRecipient(),
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.Discovery },
                subjectLinks = new[] { Subject("location.prototype.secret-dungeon-entry", QuestSubjectRole.Location, InformationSubjectType.Location) }
            });

            Assert.That(hidden.Succeeded, Is.True, hidden.Message);
            Assert.That(runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PublicOnly }).Count, Is.EqualTo(1));
            Assert.That(runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count, Is.EqualTo(2));
            Assert.That(runtime.Query(new QuestQuery { access = QuestVisibilityAccess.PublicOnly, subjectId = "location.prototype.secret-dungeon-entry" }).Count, Is.EqualTo(0));
        }

        [Test]
        public void SnapshotsAreImmutableAfterRuntimeMutation()
        {
            QuestRuntime runtime = Runtime();
            QuestRuntimeOperationResult create = CreateBounty(runtime, "tx.quest.snapshot", "wyvern");
            QuestSnapshot snapshot = create.Snapshot;
            string questId = snapshot.QuestId;
            QuestSubjectLinkData copiedLink = snapshot.SubjectLinks[0];
            copiedLink.subject.subjectId = "mutated";

            runtime.TransitionLifecycle(new QuestLifecycleTransitionRequest
            {
                transactionId = "tx.quest.snapshot.retire",
                questId = questId,
                targetState = QuestRuntimeLifecycleState.Retired,
                expectedRevision = runtime.Revision,
                worldTime = 50d
            });

            Assert.That(snapshot.LifecycleState, Is.EqualTo(QuestRuntimeLifecycleState.Available));
            Assert.That(snapshot.SubjectLinks[0].subject.subjectId, Is.EqualTo("encounter.prototype.wyvern"));
            Assert.That(runtime.TryGetSnapshot(questId, out QuestSnapshot after), Is.True);
            Assert.That(after.LifecycleState, Is.EqualTo(QuestRuntimeLifecycleState.Retired));
        }

        [Test]
        public void PersistenceRoundTripAndFailedPrepareLeaveRuntimeUnchanged()
        {
            DefinitionRegistry registry = Registry();
            QuestRuntime runtime = new QuestRuntime(registry, PersistenceService.LocalWorldId);
            CreateBounty(runtime, "tx.quest.persist", "golem");
            QuestRuntimePersistenceParticipant participant = new QuestRuntimePersistenceParticipant(runtime, () => registry);

            PersistenceParticipantSaveResult save = participant.CapturePayload();
            QuestRuntime restored = new QuestRuntime(registry, PersistenceService.LocalWorldId);
            QuestRuntimePersistenceParticipant restoredParticipant = new QuestRuntimePersistenceParticipant(restored, () => registry);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, QuestRuntimePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            int restoredCount = restored.Count;
            int restoredEvents = restored.Events.Count;

            QuestRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.quests[0].questDefinitionId = "quest-definition.missing";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), QuestRuntimePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(restoredCount, Is.EqualTo(1));
            Assert.That(restoredEvents, Is.EqualTo(1));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.Count, Is.EqualTo(restoredCount));
            Assert.That(restored.Events.Count, Is.EqualTo(restoredEvents));
        }

        [Test]
        public void WrongWorldRestoreIsRejectedBeforeCommit()
        {
            DefinitionRegistry registry = Registry();
            QuestRuntime runtime = new QuestRuntime(registry, PersistenceService.LocalWorldId);
            CreateBounty(runtime, "tx.quest.world", "bandit");
            QuestRuntimeSaveData save = runtime.CreateSaveData();
            save.worldId = "world.other";

            QuestRuntimeOperationResult result = runtime.RestoreFromSaveData(save, registry, PersistenceService.LocalWorldId);

            Assert.That(result.Status, Is.EqualTo(QuestRuntimeOperationStatus.PersistenceInvalid));
            Assert.That(runtime.WorldId, Is.EqualTo(PersistenceService.LocalWorldId));
            Assert.That(runtime.Count, Is.EqualTo(1));
        }

        private static DefinitionRegistry Registry()
        {
            return PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()));
        }

        private static QuestRuntime Runtime()
        {
            return new QuestRuntime(Registry(), PersistenceService.LocalWorldId);
        }

        private static QuestRuntimeOperationResult CreateGuildQuest(QuestRuntime runtime, string transactionId, string questId)
        {
            return runtime.CreateQuest(new QuestCreateRequest
            {
                transactionId = transactionId,
                questId = questId,
                questDefinitionId = PrototypeQuestDefinitionFactory.GuildPostingDefinitionId,
                issuer = OrganizationIssuer(),
                intendedRecipient = OpenRecipient(),
                origin = GuildBoardOrigin(),
                subjectLinks = new[] { Subject("location.prototype.dungeon-entry", QuestSubjectRole.Location, InformationSubjectType.Location) }
            });
        }

        private static QuestRuntimeOperationResult CreateBounty(QuestRuntime runtime, string transactionId, string key)
        {
            return runtime.CreateQuest(new QuestCreateRequest
            {
                transactionId = transactionId,
                questDefinitionId = PrototypeQuestDefinitionFactory.DynamicBountyDefinitionId,
                repeatInstanceKey = key,
                issuer = OrganizationIssuer(),
                intendedRecipient = OpenRecipient(),
                origin = GuildBoardOrigin(),
                subjectLinks = new[] { Subject($"encounter.prototype.{key}", QuestSubjectRole.Encounter, InformationSubjectType.Custom) },
                tagIds = new[] { "bounty", key }
            });
        }

        private static QuestIssuerReferenceData OrganizationIssuer()
        {
            return new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" };
        }

        private static QuestRecipientReferenceData OpenRecipient()
        {
            return new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open };
        }

        private static QuestOriginReferenceData GuildBoardOrigin()
        {
            return new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, locationId = "location.prototype.adventurers-guild", interactionPointId = "interaction-point.prototype.guild-board" };
        }

        private static QuestSubjectLinkData Subject(string id, QuestSubjectRole role, InformationSubjectType type)
        {
            return new QuestSubjectLinkData
            {
                role = role,
                subject = new InformationSubjectReferenceData { subjectType = type, subjectId = id, tags = new[] { role.ToString().ToLowerInvariant() } },
                provenanceId = "test.quest.identity"
            };
        }
    }
}
