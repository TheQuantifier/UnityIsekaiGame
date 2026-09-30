using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class CharacterLifeEventsTests
    {
        [Test]
        public void RecordsLifeEventAsCanonicalHistoricalEvent()
        {
            TestFixture fixture = CreateFixture();

            HistoryOperationResult result = fixture.History.RecordLifeEvent(LifeEvent("tx.life.birth", "event.life.birth", "history-event.life.birth", LifeEventCategory.BirthOrCreation, LifeEventPayloadKind.BirthOrCreation, 1d));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.History.TryGetEvent("event.life.birth", out HistoricalEventRecord canonical), Is.True);
            Assert.That(canonical.IsLifeEvent, Is.True);
            Assert.That(canonical.LifeEventCategory, Is.EqualTo(LifeEventCategory.BirthOrCreation));
            Assert.That(canonical.Significance, Is.EqualTo(LifeEventSignificance.LifeDefining));
            Assert.That(fixture.History.QueryLifeEventsForPerson("person.life.subject").Single().EventId, Is.EqualTo(canonical.EventId));
            Assert.That(fixture.History.CreateSnapshot().Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void RejectsMissingRequiredParticipantWithoutPartialMutation()
        {
            TestFixture fixture = CreateFixture();

            RecordLifeEventRequest request = LifeEvent("tx.life.invalid", "event.life.invalid", "history-event.life.birth", LifeEventCategory.BirthOrCreation, LifeEventPayloadKind.BirthOrCreation, 1d);
            request.PrimaryPersonId = string.Empty;
            request.Participants = new[] { Participant("person.life.subject", LifeEventParticipantRole.Witness) };
            HistoryOperationResult result = fixture.History.RecordLifeEvent(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HistoryResultCode.InvalidParticipantRole));
            Assert.That(fixture.History.CreateSnapshot().Events.Count, Is.EqualTo(0));
        }

        [Test]
        public void QueriesBiographyPrivacyAndRememberedViewsSeparately()
        {
            TestFixture fixture = CreateFixture();
            fixture.History.RecordLifeEvent(LifeEvent("tx.life.public", "event.life.public-title", "history-event.life.title-grant", LifeEventCategory.Title, LifeEventPayloadKind.RoleOrTitleTransition, 1d, KnowledgeVisibility.Public));
            fixture.History.RecordLifeEvent(LifeEvent("tx.life.private", "event.life.private-diagnosis", "history-event.life.diagnosis", LifeEventCategory.Diagnosis, LifeEventPayloadKind.InjuryDiagnosisRecovery, 2d, KnowledgeVisibility.Private));
            fixture.History.RecordLifeEvent(LifeEvent("tx.life.hidden", "event.life.hidden-crime", "history-event.life.crime", LifeEventCategory.Crime, LifeEventPayloadKind.Legal, 3d, KnowledgeVisibility.Hidden));
            fixture.Memory.FormMemory(new FormMemoryRequest
            {
                TransactionId = "tx.memory.hidden",
                MemoryId = "memory.life.hidden",
                OwnerPersonId = "person.life.subject",
                HistoricalEventId = "event.life.hidden-crime",
                Source = HistoryMemorySource.WitnessTestimony,
                FormedAtWorldTime = 4d,
                RememberedOccurredAtWorldTime = 3d,
                Confidence = 800,
                Clarity = 800,
                Salience = 700,
                Visibility = KnowledgeVisibility.Private
            });

            string[] publicBiography = fixture.History.QueryBiography("person.life.subject", fixture.Memory, publicOnly: true).Select(entry => entry.EventId).ToArray();
            string[] knownBiography = fixture.History.QueryBiography("person.life.subject", fixture.Memory, personKnown: true).Select(entry => entry.EventId).ToArray();
            string[] rememberedBiography = fixture.History.QueryBiography("person.life.subject", fixture.Memory, personRemembered: true).Select(entry => entry.EventId).ToArray();

            Assert.That(publicBiography, Is.EqualTo(new[] { "event.life.public-title" }));
            Assert.That(knownBiography, Does.Contain("event.life.private-diagnosis"));
            Assert.That(knownBiography, Does.Contain("event.life.hidden-crime"));
            Assert.That(rememberedBiography, Is.EqualTo(new[] { "event.life.hidden-crime" }));
        }

        [Test]
        public void RelationshipsSequencesAndCorrectionsRoundTrip()
        {
            TestFixture fixture = CreateFixture();
            fixture.History.RecordLifeEvent(LifeEvent("tx.life.battle", "event.life.battle", "history-event.life.battle", LifeEventCategory.Combat, LifeEventPayloadKind.CombatParticipation, 1d));
            RecordLifeEventRequest injury = LifeEvent("tx.life.injury", "event.life.injury", "history-event.life.injury", LifeEventCategory.Injury, LifeEventPayloadKind.InjuryDiagnosisRecovery, 2d);
            injury.SequenceId = "sequence.life.battle-recovery";
            injury.SequenceTypeId = "sequence-type.battle-recovery";
            injury.SequenceOrder = 1;
            injury.Relationships = new[] { Relationship("rel.life.injury-caused-by-battle", LifeEventRelationshipType.Cause, "event.life.battle") };
            fixture.History.RecordLifeEvent(injury);
            RecordLifeEventRequest recovery = LifeEvent("tx.life.recovery", "event.life.recovery", "history-event.life.recovery", LifeEventCategory.Recovery, LifeEventPayloadKind.InjuryDiagnosisRecovery, 3d);
            recovery.SequenceId = "sequence.life.battle-recovery";
            recovery.SequenceTypeId = "sequence-type.battle-recovery";
            recovery.SequenceOrder = 2;
            recovery.SequenceStatus = LifeEventSequenceStatus.Completed;
            recovery.Relationships = new[] { Relationship("rel.life.recovery-resolves-injury", LifeEventRelationshipType.Resolution, "event.life.injury") };
            fixture.History.RecordLifeEvent(recovery);
            fixture.History.RecordLifeEvent(LifeEvent("tx.life.presumed", "event.life.presumed", "history-event.life.presumed-death", LifeEventCategory.Disappearance, LifeEventPayloadKind.DeathOrDisappearance, 4d, KnowledgeVisibility.Private));
            RecordLifeEventRequest correction = LifeEvent("tx.life.return", "event.life.return", "history-event.life.return", LifeEventCategory.ReturnOrResurrection, LifeEventPayloadKind.DeathOrDisappearance, 5d, KnowledgeVisibility.Public);
            correction.SupersedesEventId = "event.life.presumed";
            fixture.History.RecordLifeEvent(correction);

            AuthoritativeHistorySaveData save = fixture.History.CreateSaveData();
            TestFixture restored = CreateFixture();
            HistoryOperationResult restore = restored.History.RestoreFromSaveData(save, restored.Registry, restored.KnownPersons, restored.KnownBodies, restoring: true);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.History.TryGetLifeEventSequence("sequence.life.battle-recovery", out LifeEventSequenceRecord sequence), Is.True);
            Assert.That(sequence.Events.Select(record => record.EventId), Is.EqualTo(new[] { "event.life.injury", "event.life.recovery" }));
            Assert.That(restored.History.QueryRelatedLifeEvents("event.life.injury", LifeEventRelationshipType.Cause).Single().EventId, Is.EqualTo("event.life.battle"));
            Assert.That(restored.History.TryGetAcceptedEvent("event.life.presumed", out HistoricalEventRecord accepted), Is.True);
            Assert.That(accepted.EventId, Is.EqualTo("event.life.return"));
        }

        [Test]
        public void LifeEventSnapshotsAreImmutableAndDoNotMutateCurrentState()
        {
            TestFixture fixture = CreateFixture();
            fixture.History.RecordLifeEvent(LifeEvent("tx.life.role", "event.life.role", "history-event.life.role-appointment", LifeEventCategory.Role, LifeEventPayloadKind.RoleOrTitleTransition, 1d));
            LifeEventRecord snapshot = fixture.History.QueryLifeEventsForPerson("person.life.subject").Single();

            RecordLifeEventRequest title = LifeEvent("tx.life.title", "event.life.title", "history-event.life.title-grant", LifeEventCategory.Title, LifeEventPayloadKind.RoleOrTitleTransition, 2d);
            fixture.History.RecordLifeEvent(title);

            Assert.That(snapshot.EventId, Is.EqualTo("event.life.role"));
            Assert.That(snapshot.Participants.Count, Is.EqualTo(1));
            Assert.That(fixture.History.QueryLifeEventsByCategory(LifeEventCategory.Title).Single().EventId, Is.EqualTo("event.life.title"));
            Assert.That(fixture.History.QueryMajorLifeMilestones("person.life.subject").Select(record => record.EventId), Does.Contain("event.life.role"));
        }

        [Test]
        public void PersistenceParticipantRejectsCorruptLifeEventWithoutMutatingLiveRuntime()
        {
            TestFixture fixture = CreateFixture();
            fixture.History.RecordLifeEvent(LifeEvent("tx.life.good", "event.life.good", "history-event.life.birth", LifeEventCategory.BirthOrCreation, LifeEventPayloadKind.BirthOrCreation, 1d));
            AuthoritativeHistorySaveData save = fixture.History.CreateSaveData();
            save.events[0].lifeEventRelationships = new[] { Relationship("rel.bad", LifeEventRelationshipType.Cause, "event.life.missing") };
            AuthoritativeHistoryPersistenceParticipant participant = new AuthoritativeHistoryPersistenceParticipant(fixture.History, () => fixture.Registry, () => fixture.KnownPersons, () => fixture.KnownBodies);

            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(JsonUtility.ToJson(save), AuthoritativeHistorySaveData.CurrentSchemaVersion);

            Assert.That(prepare.Succeeded, Is.False);
            Assert.That(fixture.History.CreateSnapshot().Events.Single().EventId, Is.EqualTo("event.life.good"));
        }

        private static TestFixture CreateFixture()
        {
            string[] persons = { "person.life.subject", "person.life.witness", "person.life.uninformed" };
            string[] bodies = { "body.life.current", "body.life.previous" };
            DefinitionRegistry registry = Registry();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            history.Configure(registry, "world.life.test", persons, bodies);
            PersonMemoryRuntime memory = new PersonMemoryRuntime();
            memory.Configure("person.life.subject", registry, history, persons);
            return new TestFixture(registry, history, memory, persons, bodies);
        }

        private static DefinitionRegistry Registry()
        {
            return new DefinitionRegistry(new IGameDefinition[]
            {
                EventDefinition("history-event.life.birth", LifeEventCategory.BirthOrCreation, LifeEventPayloadKind.BirthOrCreation, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.IdentityDefining, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                EventDefinition("history-event.life.discovery", LifeEventCategory.Discovery, LifeEventPayloadKind.Discovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.Optional, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Discoverer),
                EventDefinition("history-event.life.role-appointment", LifeEventCategory.Role, LifeEventPayloadKind.RoleOrTitleTransition, LifeEventSignificance.Major, LifeEventBiographyRelevance.MajorBiographyEvent, LifeEventPublicRecordRelevance.OrganizationRecord, LifeEventParticipantRole.Subject),
                EventDefinition("history-event.life.title-grant", LifeEventCategory.Title, LifeEventPayloadKind.RoleOrTitleTransition, LifeEventSignificance.Major, LifeEventBiographyRelevance.PublicBiographyEvent, LifeEventPublicRecordRelevance.PublicRecord, LifeEventParticipantRole.Subject),
                EventDefinition("history-event.life.affiliation", LifeEventCategory.Affiliation, LifeEventPayloadKind.AffiliationTransition, LifeEventSignificance.Notable, LifeEventBiographyRelevance.NormallyIncluded, LifeEventPublicRecordRelevance.OrganizationRecord, LifeEventParticipantRole.Subject),
                EventDefinition("history-event.life.battle", LifeEventCategory.Combat, LifeEventPayloadKind.CombatParticipation, LifeEventSignificance.Major, LifeEventBiographyRelevance.MajorBiographyEvent, LifeEventPublicRecordRelevance.HistoricalArchive, LifeEventParticipantRole.Participant),
                EventDefinition("history-event.life.injury", LifeEventCategory.Injury, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Major, LifeEventBiographyRelevance.PrivateBiographyEvent, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                EventDefinition("history-event.life.diagnosis", LifeEventCategory.Diagnosis, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.PrivateBiographyEvent, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                EventDefinition("history-event.life.recovery", LifeEventCategory.Recovery, LifeEventPayloadKind.InjuryDiagnosisRecovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.NormallyIncluded, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject),
                EventDefinition("history-event.life.crime", LifeEventCategory.Crime, LifeEventPayloadKind.Legal, LifeEventSignificance.Major, LifeEventBiographyRelevance.RestrictedBiographyEvent, LifeEventPublicRecordRelevance.OrganizationRecord, LifeEventParticipantRole.Accused),
                EventDefinition("history-event.life.death", LifeEventCategory.Death, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.MajorBiographyEvent, LifeEventPublicRecordRelevance.PublicRecord, LifeEventParticipantRole.Subject),
                EventDefinition("history-event.life.presumed-death", LifeEventCategory.Disappearance, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.Major, LifeEventBiographyRelevance.RestrictedBiographyEvent, LifeEventPublicRecordRelevance.OrganizationRecord, LifeEventParticipantRole.Subject),
                EventDefinition("history-event.life.return", LifeEventCategory.ReturnOrResurrection, LifeEventPayloadKind.DeathOrDisappearance, LifeEventSignificance.LifeDefining, LifeEventBiographyRelevance.MajorBiographyEvent, LifeEventPublicRecordRelevance.PublicRecord, LifeEventParticipantRole.Subject)
            });
        }

        private static HistoricalEventDefinition EventDefinition(string id, LifeEventCategory category, LifeEventPayloadKind payloadKind, LifeEventSignificance significance, LifeEventBiographyRelevance biography, LifeEventPublicRecordRelevance publicRecord, LifeEventParticipantRole requiredRole)
        {
            HistoricalEventDefinition definition = ScriptableObject.CreateInstance<HistoricalEventDefinition>();
            definition.name = id;
            Set(definition, "eventDefinitionId", id);
            Set(definition, "displayName", id);
            Set(definition, "category", HistoricalEventCategory.CustomWorldEvent);
            Set(definition, "defaultVisibility", KnowledgeVisibility.Private);
            Set(definition, "payloadKind", HistoricalEventPayloadKind.Generic);
            Set(definition, "lifeEventDefinition", true);
            Set(definition, "lifeEventCategory", category);
            Set(definition, "lifeEventPayloadKind", payloadKind);
            Set(definition, "defaultSignificance", significance);
            Set(definition, "defaultBiographyRelevance", biography);
            Set(definition, "defaultPublicRecordRelevance", publicRecord);
            Set(definition, "requiredParticipantRoles", new[] { requiredRole });
            Set(definition, "optionalParticipantRoles", new[] { LifeEventParticipantRole.Witness });
            Set(definition, "mayBePrivate", true);
            Set(definition, "mayBeSecret", true);
            Set(definition, "mayBeCorrected", true);
            return definition;
        }

        private static RecordLifeEventRequest LifeEvent(string transactionId, string eventId, string definitionId, LifeEventCategory category, LifeEventPayloadKind payloadKind, double time, KnowledgeVisibility visibility = KnowledgeVisibility.Private)
        {
            LifeEventParticipantRole role = category == LifeEventCategory.Discovery ? LifeEventParticipantRole.Discoverer : category == LifeEventCategory.Combat ? LifeEventParticipantRole.Participant : category == LifeEventCategory.Crime ? LifeEventParticipantRole.Accused : LifeEventParticipantRole.Subject;
            return new RecordLifeEventRequest
            {
                TransactionId = transactionId,
                EventId = eventId,
                EventDefinitionId = definitionId,
                Category = category,
                PayloadKind = payloadKind,
                OccurredAtWorldTime = time,
                RecordedAtWorldTime = time,
                PrimaryPersonId = "person.life.subject",
                Participants = new[] { Participant("person.life.subject", role) },
                BodyIds = new[] { "body.life.current" },
                Visibility = visibility,
                Outcome = LifeEventOutcome.Confirmed,
                SourceSystem = "EditModeTest",
                Provenance = "Life event test fixture",
                LifeEventPayload = new LifeEventPayloadData { kind = payloadKind, subjectPersonId = "person.life.subject", note = eventId },
                Tags = new[] { "life-event-test" }
            };
        }

        private static LifeEventParticipantData Participant(string personId, LifeEventParticipantRole role)
        {
            return new LifeEventParticipantData { personId = personId, role = role, bodyId = "body.life.current" };
        }

        private static LifeEventRelationshipData Relationship(string id, LifeEventRelationshipType type, string targetEventId)
        {
            return new LifeEventRelationshipData { relationshipId = id, relationshipType = type, targetEventId = targetEventId, requiresAcyclic = true };
        }

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private sealed class TestFixture
        {
            public TestFixture(DefinitionRegistry registry, AuthoritativeHistoryRuntime history, PersonMemoryRuntime memory, string[] knownPersons, string[] knownBodies)
            {
                Registry = registry;
                History = history;
                Memory = memory;
                KnownPersons = knownPersons;
                KnownBodies = knownBodies;
            }

            public DefinitionRegistry Registry { get; }
            public AuthoritativeHistoryRuntime History { get; }
            public PersonMemoryRuntime Memory { get; }
            public string[] KnownPersons { get; }
            public string[] KnownBodies { get; }
        }
    }
}
