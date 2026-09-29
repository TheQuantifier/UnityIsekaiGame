using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class CharacterHistoryMemoryTimelineTests
    {
        [Test]
        public void RecordsValidEventsAndRejectsMissingReferencesAtomically()
        {
            TestFixture fixture = CreateFixture("person.history.actor", "body.history.actor");

            HistoryOperationResult recorded = fixture.History.RecordEvent(Event("tx.history.valid", "event.history.valid", "history-event.person-participation", 10, "person.history.actor", participants: new[] { "person.history.actor" }, bodies: new[] { "body.history.actor" }));
            HistoryOperationResult missingDefinition = fixture.History.RecordEvent(Event("tx.history.missing-definition", "event.history.missing-definition", "history-event.missing", 11, "person.history.actor"));
            HistoryOperationResult unknownPerson = fixture.History.RecordEvent(Event("tx.history.unknown-person", "event.history.unknown-person", "history-event.person-participation", 12, "person.unknown", participants: new[] { "person.unknown" }));

            Assert.That(recorded.Succeeded, Is.True, recorded.Message);
            Assert.That(missingDefinition.Succeeded, Is.False);
            Assert.That(missingDefinition.Code, Is.EqualTo(HistoryResultCode.MissingDefinition));
            Assert.That(unknownPerson.Succeeded, Is.False);
            Assert.That(unknownPerson.Code, Is.EqualTo(HistoryResultCode.MissingPerson));
            Assert.That(fixture.History.CreateSnapshot().Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void TimelineQueriesUseCanonicalEventsAndDeterministicOrdering()
        {
            TestFixture fixture = CreateFixture("person.history.a", "body.history.a", extraPersons: new[] { "person.history.b" }, extraBodies: new[] { "body.history.b" });

            fixture.History.RecordEvent(Event("tx.history.order.2", "event.history.same-time.b", "history-event.person-participation", 5, "person.history.a", sequence: 2, participants: new[] { "person.history.a", "person.history.b" }, bodies: new[] { "body.history.a" }, location: "place.prototype"));
            fixture.History.RecordEvent(Event("tx.history.order.1", "event.history.same-time.a", "history-event.person-participation", 5, "person.history.a", sequence: 1, participants: new[] { "person.history.a" }, bodies: new[] { "body.history.a" }, location: "place.prototype"));
            fixture.History.RecordEvent(Event("tx.history.order.tie", "event.history.same-time.aa", "history-event.person-participation", 5, "person.history.b", sequence: 1, participants: new[] { "person.history.b" }, bodies: new[] { "body.history.b" }, location: "place.prototype"));

            string[] personTimeline = fixture.History.QueryByPerson("person.history.a").Select(record => record.EventId).ToArray();
            string[] locationTimeline = fixture.History.QueryByLocation("place.prototype").Select(record => record.EventId).ToArray();

            Assert.That(personTimeline, Is.EqualTo(new[] { "event.history.same-time.a", "event.history.same-time.b" }));
            Assert.That(locationTimeline, Is.EqualTo(new[] { "event.history.same-time.a", "event.history.same-time.aa", "event.history.same-time.b" }));
            Assert.That(fixture.History.QueryByBody("body.history.a").Single(record => record.EventId == "event.history.same-time.a"), Is.Not.SameAs(fixture.History.QueryByPerson("person.history.a").Single(record => record.EventId == "event.history.same-time.a")));
        }

        [Test]
        public void CorrectionsPreserveOriginalAndAcceptedRecordWithoutRevisingBeliefs()
        {
            TestFixture fixture = CreateFixture("person.history.actor", "body.history.actor");
            fixture.History.RecordEvent(Event("tx.history.original", "event.history.original", "history-event.person-participation", 1, "person.history.actor", visibility: KnowledgeVisibility.Public));
            fixture.Memory.FormMemory(Memory("tx.memory.original", "memory.original", "person.history.actor", "event.history.original", 2, createKnowledge: true), fixture.Knowledge);
            string beliefId = fixture.Knowledge.CreateSnapshot().Beliefs.Single().BeliefId;

            HistoryOperationResult correction = fixture.History.RecordEvent(Event("tx.history.correction", "event.history.correction", "history-event.correction", 3, "person.history.actor", visibility: KnowledgeVisibility.Private, supersedes: "event.history.original"));

            Assert.That(correction.Succeeded, Is.True, correction.Message);
            Assert.That(fixture.History.TryGetEvent("event.history.original", out HistoricalEventRecord original), Is.True);
            Assert.That(original.Status, Is.EqualTo(HistoricalEventStatus.Superseded));
            Assert.That(fixture.History.TryGetAcceptedEvent("event.history.original", out HistoricalEventRecord accepted), Is.True);
            Assert.That(accepted.EventId, Is.EqualTo("event.history.correction"));
            Assert.That(fixture.Knowledge.CreateSnapshot().Beliefs.Single().BeliefId, Is.EqualTo(beliefId));
        }

        [Test]
        public void HiddenHistoryRequiresMemoryOrPrivilegedAccess()
        {
            TestFixture fixture = CreateFixture("person.history.witness", "body.history.witness", extraPersons: new[] { "person.history.uninformed" });
            fixture.History.RecordEvent(Event("tx.history.hidden", "event.history.hidden", "history-event.hidden-witnessed-event", 4, "person.history.witness", participants: new[] { "person.history.witness" }, visibility: KnowledgeVisibility.Hidden));

            Assert.That(fixture.History.QueryPersonAccessible("person.history.uninformed").Count, Is.EqualTo(0));
            Assert.That(fixture.History.QueryPersonAccessible("person.history.uninformed", privileged: true).Count, Is.EqualTo(1));

            HistoryOperationResult memory = fixture.Memory.FormMemory(Memory("tx.memory.hidden", "memory.hidden", "person.history.witness", "event.history.hidden", 5, createKnowledge: true), fixture.Knowledge);

            Assert.That(memory.Succeeded, Is.True, memory.Message);
            Assert.That(fixture.History.QueryPersonAccessible("person.history.witness", fixture.Memory).Select(record => record.EventId), Does.Contain("event.history.hidden"));
            Assert.That(fixture.Knowledge.CreateSnapshot().Evidence.Single().Data.relatedEventId, Is.EqualTo("event.history.hidden"));
        }

        [Test]
        public void ReconfiguringMemoryRuntimeForDifferentPersonClearsPriorOwnerState()
        {
            TestFixture fixture = CreateFixture("person.history.first", "body.history.actor", extraPersons: new[] { "person.history.second" });
            fixture.History.RecordEvent(Event("tx.history.owner-reset", "event.history.owner-reset", "history-event.person-participation", 1, "person.history.first"));
            fixture.Memory.FormMemory(Memory("tx.memory.owner-reset", "memory.owner-reset", "person.history.first", "event.history.owner-reset", 2));

            Assert.That(fixture.Memory.CreateSnapshot().Memories.Count, Is.EqualTo(1));

            fixture.Memory.Configure("person.history.second", fixture.Registry, fixture.History, new[] { "person.history.first", "person.history.second" });

            Assert.That(fixture.Memory.PersonId, Is.EqualTo("person.history.second"));
            Assert.That(fixture.Memory.MemoryRevision, Is.EqualTo(0));
            Assert.That(fixture.Memory.CreateSnapshot().Memories.Count, Is.EqualTo(0));
        }

        [Test]
        public void MemoryRecallForgetAndCorrectionDoNotMutateAuthoritativeHistory()
        {
            TestFixture fixture = CreateFixture("person.history.actor", "body.history.actor");
            fixture.History.RecordEvent(Event("tx.history.memory", "event.history.memory", "history-event.person-participation", 1, "person.history.actor"));
            fixture.Memory.FormMemory(Memory("tx.memory.form", "memory.form", "person.history.actor", "event.history.memory", 2));
            long historyRevision = fixture.History.HistoryRevision;

            HistoryOperationResult recall = fixture.Memory.RecallMemory("memory.form", "tx.memory.recall", 3);
            HistoryOperationResult forget = fixture.Memory.ForgetMemory("memory.form", "tx.memory.forget");
            HistoryOperationResult corrected = fixture.Memory.CorrectMemory("memory.form", Memory("tx.memory.correct", "memory.correct", "person.history.actor", "event.history.memory", 4));

            Assert.That(recall.Succeeded, Is.True, recall.Message);
            Assert.That(forget.Succeeded, Is.True, forget.Message);
            Assert.That(corrected.Succeeded, Is.True, corrected.Message);
            Assert.That(fixture.History.HistoryRevision, Is.EqualTo(historyRevision));
            Assert.That(fixture.Memory.TryGetMemory("memory.form", out HistoryMemoryRecord original), Is.True);
            Assert.That(original.State, Is.EqualTo(MemoryState.Corrected));
            Assert.That(fixture.Memory.TryGetMemory("memory.correct", out HistoryMemoryRecord newMemory), Is.True);
            Assert.That(newMemory.Accessible, Is.True);
        }

        [Test]
        public void BodyTransitionTracksPersistentPersonAcrossBodiesWithoutPublicContinuity()
        {
            TestFixture fixture = CreateFixture("person.history.actor", "body.history.old", extraBodies: new[] { "body.history.new" }, extraPersons: new[] { "person.history.observer" });

            HistoryOperationResult transition = fixture.History.RecordBodyTransition("tx.history.body-transition", "event.history.body-transition", "person.history.actor", "body.history.old", "body.history.new", 10, 10, "Test body replacement");
            fixture.Memory.FormMemory(Memory("tx.memory.previous-body", "memory.previous-body", "person.history.actor", "event.history.body-transition", 11, bodyAtTime: "body.history.old"));

            Assert.That(transition.Succeeded, Is.True, transition.Message);
            Assert.That(fixture.History.QueryBodyOccupations("person.history.actor").Select(record => record.BodyId), Does.Contain("body.history.new"));
            Assert.That(fixture.Memory.TryGetMemory("memory.previous-body", out HistoryMemoryRecord memory), Is.True);
            Assert.That(memory.BodyAtTimeId, Is.EqualTo("body.history.old"));
            Assert.That(fixture.History.QueryPersonAccessible("person.history.observer").Any(record => record.EventId == "event.history.body-transition"), Is.False);
        }

        [Test]
        public void PersistenceRoundTripsHistoryMemoryOrderingAndHiddenStateWithoutEvents()
        {
            TestFixture fixture = CreateFixture("person.history.actor", "body.history.actor", extraBodies: new[] { "body.history.new" });
            int historyEvents = 0;
            int memoryEvents = 0;
            fixture.History.HistoryChanged += (_, __) => historyEvents++;
            fixture.Memory.MemoryChanged += (_, __) => memoryEvents++;
            fixture.History.RecordEvent(Event("tx.history.persist.a", "event.history.persist.a", "history-event.hidden-witnessed-event", 2, "person.history.actor", sequence: 2, visibility: KnowledgeVisibility.Hidden));
            fixture.History.RecordEvent(Event("tx.history.persist.b", "event.history.persist.b", "history-event.person-participation", 2, "person.history.actor", sequence: 1));
            fixture.Memory.FormMemory(Memory("tx.memory.persist", "memory.persist", "person.history.actor", "event.history.persist.a", 3, createKnowledge: true), fixture.Knowledge);

            AuthoritativeHistorySaveData historySave = fixture.History.CreateSaveData();
            PersonMemorySaveData memorySave = fixture.Memory.CreateSaveData();
            TestFixture restored = CreateFixture("person.history.actor", "body.history.actor", extraBodies: new[] { "body.history.new" });
            int restoredHistoryEvents = 0;
            int restoredMemoryEvents = 0;
            restored.History.HistoryChanged += (_, __) => restoredHistoryEvents++;
            restored.Memory.MemoryChanged += (_, __) => restoredMemoryEvents++;

            HistoryOperationResult historyRestore = restored.History.RestoreFromSaveData(historySave, restored.Registry, restored.KnownPersons, restored.KnownBodies, restoring: true);
            HistoryOperationResult memoryRestore = restored.Memory.RestoreFromSaveData(memorySave, restored.Registry, restored.History, restored.KnownPersons, restoring: true);

            Assert.That(historyRestore.Succeeded, Is.True, historyRestore.Message);
            Assert.That(memoryRestore.Succeeded, Is.True, memoryRestore.Message);
            Assert.That(restored.History.CreateSnapshot().Events.Select(record => record.EventId), Is.EqualTo(new[] { "event.history.persist.b", "event.history.persist.a" }));
            Assert.That(restored.History.QueryPersonAccessible("person.history.actor", restored.Memory).Select(record => record.EventId), Does.Contain("event.history.persist.a"));
            Assert.That(restoredHistoryEvents, Is.EqualTo(0));
            Assert.That(restoredMemoryEvents, Is.EqualTo(0));
            Assert.That(historyEvents, Is.EqualTo(2));
            Assert.That(memoryEvents, Is.EqualTo(1));
        }

        [Test]
        public void PersistenceParticipantsUsePrepareCommitAndRejectCorruption()
        {
            TestFixture fixture = CreateFixture("person.history.actor", "body.history.actor");
            fixture.History.RecordEvent(Event("tx.history.participant", "event.history.participant", "history-event.person-participation", 1, "person.history.actor"));
            fixture.Memory.FormMemory(Memory("tx.memory.participant", "memory.participant", "person.history.actor", "event.history.participant", 2));
            AuthoritativeHistoryPersistenceParticipant historyParticipant = new AuthoritativeHistoryPersistenceParticipant(fixture.History, () => fixture.Registry, () => fixture.KnownPersons, () => fixture.KnownBodies);
            PersonMemoryPersistenceParticipant memoryParticipant = new PersonMemoryPersistenceParticipant(fixture.Memory, fixture.History, () => fixture.Registry, () => fixture.KnownPersons);

            Assert.That(historyParticipant.CapturePayload().Succeeded, Is.True);
            Assert.That(memoryParticipant.CapturePayload().Succeeded, Is.True);
            Assert.That(historyParticipant.PreparePayload("{\"schemaVersion\":1,\"events\":[{\"eventId\":\"bad\",\"eventDefinitionId\":\"history-event.missing\"}]}", 1).Succeeded, Is.False);
        }

        private static TestFixture CreateFixture(string personId, string bodyId, string[] extraPersons = null, string[] extraBodies = null)
        {
            DefinitionRegistry registry = Registry();
            string[] persons = new[] { personId }.Concat(extraPersons ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            string[] bodies = new[] { bodyId }.Concat(extraBodies ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            history.Configure(registry, "world.history.test", persons, bodies);
            PersonMemoryRuntime memory = new PersonMemoryRuntime();
            memory.Configure(personId, registry, history, persons);
            GameObject gameObject = new GameObject($"History Knowledge - {personId}");
            PersonKnowledgeRuntime knowledge = gameObject.AddComponent<PersonKnowledgeRuntime>();
            knowledge.Configure(registry, personId, actorId: "actor.history.test", bodyId: bodyId);
            return new TestFixture(registry, history, memory, knowledge, gameObject, persons, bodies);
        }

        private static DefinitionRegistry Registry()
        {
            return new DefinitionRegistry(new IGameDefinition[]
            {
                Fact(BuiltInKnowledgeFacts.EventOccurred, "Event Occurred", KnowledgeDomain.Historical, KnowledgePropositionType.Event, KnowledgeSubjectType.Event, KnowledgeValueType.Boolean),
                EventDefinition("history-event.person-participation", HistoricalEventCategory.CustomWorldEvent, KnowledgeVisibility.Public, HistoricalEventPayloadKind.Generic),
                EventDefinition("history-event.hidden-witnessed-event", HistoricalEventCategory.Discovery, KnowledgeVisibility.Hidden, HistoricalEventPayloadKind.Generic),
                EventDefinition("history-event.body-transition", HistoricalEventCategory.BodyTransition, KnowledgeVisibility.Private, HistoricalEventPayloadKind.BodyTransition),
                EventDefinition("history-event.correction", HistoricalEventCategory.Discovery, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Correction),
                EventDefinition("history-event.diagnosis", HistoricalEventCategory.Diagnosis, KnowledgeVisibility.DiagnosticOnly, HistoricalEventPayloadKind.Condition)
            });
        }

        private static RecordHistoricalEventRequest Event(string transactionId, string eventId, string definitionId, double time, string primaryPerson, long? sequence = null, string[] participants = null, string[] bodies = null, string location = "", KnowledgeVisibility visibility = KnowledgeVisibility.Public, string supersedes = "")
        {
            return new RecordHistoricalEventRequest
            {
                TransactionId = transactionId,
                EventId = eventId,
                EventDefinitionId = definitionId,
                OccurredAtWorldTime = time,
                RecordedAtWorldTime = time,
                Sequence = sequence,
                PrimaryPersonId = primaryPerson,
                ParticipantPersonIds = participants,
                BodyIds = bodies,
                LocationId = location,
                Visibility = visibility,
                SupersedesEventId = supersedes,
                SourceSystem = "EditModeTest",
                Provenance = "Test fixture",
                Payload = new HistoricalEventPayloadData { kind = HistoricalEventPayloadKind.Generic, note = eventId },
                Tags = new[] { "test-history" }
            };
        }

        private static FormMemoryRequest Memory(string transactionId, string memoryId, string ownerPersonId, string eventId, double formedAt, bool createKnowledge = false, string bodyAtTime = "")
        {
            return new FormMemoryRequest
            {
                TransactionId = transactionId,
                MemoryId = memoryId,
                OwnerPersonId = ownerPersonId,
                HistoricalEventId = eventId,
                Source = HistoryMemorySource.DirectObservation,
                FormedAtWorldTime = formedAt,
                RememberedOccurredAtWorldTime = Math.Max(0, formedAt - 1),
                Confidence = 800,
                Clarity = 700,
                Salience = 500,
                FirstHand = true,
                BodyAtTimeId = bodyAtTime,
                Visibility = KnowledgeVisibility.Private,
                CreateKnowledgeEvidence = createKnowledge,
                Tags = new[] { "test-memory" }
            };
        }

        private static HistoricalEventDefinition EventDefinition(string id, HistoricalEventCategory category, KnowledgeVisibility visibility, HistoricalEventPayloadKind payloadKind)
        {
            HistoricalEventDefinition definition = ScriptableObject.CreateInstance<HistoricalEventDefinition>();
            definition.name = id;
            Set(definition, "eventDefinitionId", id);
            Set(definition, "displayName", id);
            Set(definition, "category", category);
            Set(definition, "defaultVisibility", visibility);
            Set(definition, "payloadKind", payloadKind);
            return definition;
        }

        private static KnowledgeFactDefinition Fact(string id, string displayName, KnowledgeDomain domain, KnowledgePropositionType propositionType, KnowledgeSubjectType subjectType, KnowledgeValueType valueType)
        {
            KnowledgeFactDefinition definition = ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
            definition.name = displayName;
            Set(definition, "factId", id);
            Set(definition, "displayName", displayName);
            Set(definition, "domain", domain);
            Set(definition, "propositionType", propositionType);
            Set(definition, "subjectType", subjectType);
            Set(definition, "valueType", valueType);
            Set(definition, "certaintyThreshold", 700);
            Set(definition, "requiredEvidenceCount", 1);
            return definition;
        }

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, AuthoritativeHistoryRuntime history, PersonMemoryRuntime memory, PersonKnowledgeRuntime knowledge, GameObject gameObject, string[] knownPersons, string[] knownBodies)
            {
                Registry = registry;
                History = history;
                Memory = memory;
                Knowledge = knowledge;
                GameObject = gameObject;
                KnownPersons = knownPersons;
                KnownBodies = knownBodies;
            }

            public DefinitionRegistry Registry { get; }
            public AuthoritativeHistoryRuntime History { get; }
            public PersonMemoryRuntime Memory { get; }
            public PersonKnowledgeRuntime Knowledge { get; }
            public GameObject GameObject { get; }
            public string[] KnownPersons { get; }
            public string[] KnownBodies { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
            }
        }
    }
}
