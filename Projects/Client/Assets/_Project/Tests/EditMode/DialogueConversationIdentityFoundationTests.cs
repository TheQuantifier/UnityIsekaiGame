using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Tests
{
    public sealed class DialogueConversationIdentityFoundationTests
    {
        [Test]
        public void PrototypeConversationDefinitionsRegisterAndValidate()
        {
            DefinitionRegistry registry = Registry();
            Assert.That(PrototypeConversationDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out ConversationDefinition _)), Is.True);
            Assert.That(registry.TryGet(PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId, out ConversationDefinition counter), Is.True);
            Assert.That(counter.Category, Is.EqualTo(ConversationCategory.QuestOffer));

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (ConversationDefinition definition in PrototypeConversationDefinitionFactory.CreateMissingConversationDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
        }

        [Test]
        public void ConversationRecordsQuestLocationAndProviderContextWithoutOwningThem()
        {
            DefinitionRegistry registry = Registry();
            ConversationRuntime runtime = new ConversationRuntime(registry, PersistenceService.LocalWorldId);

            ConversationOperationResult result = StartGuildCounter(runtime);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Snapshot.ConversationDefinitionId, Is.EqualTo(PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId));
            Assert.That(result.Snapshot.QuestId, Is.EqualTo("quest.prototype.guild.test"));
            Assert.That(result.Snapshot.QuestSourceId, Is.EqualTo("quest-source.prototype.guild-counter.test"));
            Assert.That(result.Snapshot.HostInteractionPointId, Is.EqualTo("interaction-point.prototype.adventurer-guild-counter"));
            Assert.That(result.Snapshot.OperatingOrganizationId, Is.EqualTo("organization.prototype.adventurers-guild"));
            Assert.That(result.Snapshot.SubjectLinks.Any(link => link.role == ConversationSubjectRole.Quest), Is.True);
            Assert.That(result.Snapshot.CreateInformationSubject().tags, Does.Contain(ConversationInformationSubject.ConversationTag));
        }

        [Test]
        public void ImmutableSnapshotsDoNotMutateRuntimeState()
        {
            ConversationRuntime runtime = new ConversationRuntime(Registry(), PersistenceService.LocalWorldId);
            ConversationSnapshot snapshot = StartGuildCounter(runtime).Snapshot;
            ConversationParticipantRecordData participant = snapshot.Participants[0];
            participant.personId = "person.mutated";
            ConversationSubjectLinkData link = snapshot.SubjectLinks[0];
            link.questId = "quest.mutated";

            ConversationSnapshot after;
            Assert.That(runtime.TryGetSnapshot(snapshot.ConversationId, out after), Is.True);
            Assert.That(after.Participants.Any(value => value.personId == "person.mutated"), Is.False);
            Assert.That(after.SubjectLinks.Any(value => value.questId == "quest.mutated"), Is.False);
        }

        [Test]
        public void PrivateProjectionRedactsHiddenDetailsForNonPrivilegedParticipants()
        {
            ConversationRuntime runtime = new ConversationRuntime(Registry(), PersistenceService.LocalWorldId);
            ConversationOperationResult start = runtime.StartConversation(new ConversationStartRequest
            {
                transactionId = "tx.conversation.private.test",
                conversationId = "conversation.test.private",
                conversationDefinitionId = PrototypeConversationDefinitionFactory.PrivateAudienceDefinitionId,
                participants = new[]
                {
                    Participant("person.prototype.player", ConversationParticipantRole.Initiator, hidden: false, locationId: "location.prototype.guild-head-office", interactionPointId: "interaction-point.prototype.guild-head-desk"),
                    Participant("person.prototype.guild-head", ConversationParticipantRole.Addressee, hidden: true, locationId: "location.prototype.guild-head-office", interactionPointId: "interaction-point.prototype.guild-head-desk")
                },
                subjectLinks = new[] { new ConversationSubjectLinkData { role = ConversationSubjectRole.Information, subject = new InformationSubjectReferenceData { subjectType = InformationSubjectType.KnowledgeRecord, subjectId = "knowledge.private" }, hidden = true } },
                hostLocationId = "location.prototype.guild-head-office",
                hostInteractionPointId = "interaction-point.prototype.guild-head-desk"
            });

            ConversationProjection denied = runtime.Query(new ConversationQuery { conversationId = start.Snapshot.ConversationId, access = ConversationAccessLevel.Public, requesterPersonId = "person.prototype.visitor" }).SingleOrDefault();
            ConversationProjection participant = runtime.Query(new ConversationQuery { conversationId = start.Snapshot.ConversationId, access = ConversationAccessLevel.Participant, requesterPersonId = "person.prototype.player" }).Single();
            ConversationProjection privileged = runtime.Query(new ConversationQuery { conversationId = start.Snapshot.ConversationId, access = ConversationAccessLevel.PrivilegedDiagnostic }).Single();

            Assert.That(denied, Is.Null);
            Assert.That(participant.Redacted, Is.True);
            Assert.That(participant.Snapshot.Participants.Any(value => value.hidden), Is.False);
            Assert.That(participant.Snapshot.SubjectLinks.Any(value => value.hidden), Is.False);
            Assert.That(privileged.Redacted, Is.False);
            Assert.That(privileged.Snapshot.Participants.Any(value => value.hidden), Is.True);
        }

        [Test]
        public void ProviderLocationOverlapAndRevisionFailuresDoNotMutate()
        {
            ConversationRuntime runtime = new ConversationRuntime(Registry(), PersistenceService.LocalWorldId);
            ConversationOperationResult missing = runtime.StartConversation(new ConversationStartRequest
            {
                transactionId = "tx.conversation.provider.fail",
                conversationDefinitionId = PrototypeConversationDefinitionFactory.MayorDeskDefinitionId,
                participants = new[] { Participant("person.prototype.player", ConversationParticipantRole.Initiator) },
                hostLocationId = "location.prototype.mayor-office",
                hostInteractionPointId = "interaction-point.prototype.mayor-desk"
            });

            Assert.That(missing.Succeeded, Is.False);
            Assert.That(runtime.Count, Is.Zero);
            Assert.That(runtime.Revision, Is.Zero);

            ConversationOperationResult start = StartGuildCounter(runtime);
            ConversationOperationResult overlap = StartGuildCounter(runtime, "tx.conversation.overlap", "conversation.test.overlap");
            ConversationOperationResult stale = runtime.TransitionLifecycle(new ConversationLifecycleRequest { transactionId = "tx.conversation.stale", conversationId = start.Snapshot.ConversationId, targetState = ConversationLifecycleState.Completed, expectedRevision = 0L });

            Assert.That(overlap.Status, Is.EqualTo(ConversationOperationStatus.OverlapRejected));
            Assert.That(stale.Status, Is.EqualTo(ConversationOperationStatus.RevisionConflict));
            Assert.That(runtime.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConversationPersistenceRoundTripAndFailedPrepareLeaveRuntimeUnchanged()
        {
            DefinitionRegistry registry = Registry();
            ConversationRuntime runtime = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            StartGuildCounter(runtime);
            ConversationPersistenceParticipant participant = new ConversationPersistenceParticipant(runtime, () => registry);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            Assert.That(save.Succeeded, Is.True, save.Message);

            ConversationRuntime restored = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            ConversationPersistenceParticipant restoredParticipant = new ConversationPersistenceParticipant(restored, () => registry);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, ConversationPersistenceParticipant.CurrentParticipantSchemaVersion);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload).Succeeded, Is.True);
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(restored.Events.Count, Is.EqualTo(1));

            ConversationRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.conversations[0].conversationDefinitionId = "conversation-definition.missing";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), ConversationPersistenceParticipant.CurrentParticipantSchemaVersion);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(restored.Events.Count, Is.EqualTo(1));
        }

        private static DefinitionRegistry Registry()
        {
            return PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>())));
        }

        private static ConversationOperationResult StartGuildCounter(ConversationRuntime runtime, string tx = "tx.conversation.guild.test", string id = "conversation.test.guild-counter")
        {
            return runtime.StartConversation(new ConversationStartRequest
            {
                transactionId = tx,
                conversationId = id,
                conversationDefinitionId = PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId,
                participants = new[]
                {
                    Participant("person.prototype.player", ConversationParticipantRole.Initiator),
                    Participant("person.prototype.guild-clerk", ConversationParticipantRole.Provider, organizationId: "organization.prototype.adventurers-guild"),
                    Participant("person.prototype.player", ConversationParticipantRole.QuestRecipient)
                },
                hostLocationId = "location.prototype.adventurers-guild",
                hostInteractionPointId = "interaction-point.prototype.adventurer-guild-counter",
                questId = "quest.prototype.guild.test",
                questSourceId = "quest-source.prototype.guild-counter.test",
                questListingId = "quest-listing.prototype.guild.test",
                operatingOrganizationId = "organization.prototype.adventurers-guild",
                worldTime = 1d
            });
        }

        private static ConversationParticipantRecordData Participant(string personId, ConversationParticipantRole role, string organizationId = "", bool hidden = false, string locationId = "location.prototype.adventurers-guild", string interactionPointId = "interaction-point.prototype.adventurer-guild-counter")
        {
            return new ConversationParticipantRecordData
            {
                personId = personId,
                role = role,
                representedOrganizationId = organizationId,
                currentLocationId = locationId,
                currentInteractionPointId = interactionPointId,
                hidden = hidden
            };
        }
    }
}
