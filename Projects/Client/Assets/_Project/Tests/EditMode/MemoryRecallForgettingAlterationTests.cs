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
    public sealed class MemoryRecallForgettingAlterationTests
    {
        [Test]
        public void RecallAccessibleMemoryUpdatesMetadataButInspectionDoesNot()
        {
            using TestFixture fixture = CreateFixture();
            fixture.SeedEventAndMemory();

            MemoryRecallResult inspect = fixture.Memory.Recall(new MemoryRecallRequest
            {
                TransactionId = "tx.memory.inspect",
                RequestingPersonId = fixture.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 20,
                MutateMetadata = false,
                AccessContext = MemoryAccessContext.Debug
            });
            HistoryMemoryRecord afterInspect = fixture.GetMemory("memory.seed");

            MemoryRecallResult recall = fixture.Memory.Recall(new MemoryRecallRequest
            {
                TransactionId = "tx.memory.recall",
                RequestingPersonId = fixture.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 25,
                ReinforceOnSuccess = true
            });
            HistoryMemoryRecord afterRecall = fixture.GetMemory("memory.seed");

            Assert.That(inspect.Succeeded, Is.True);
            Assert.That(afterInspect.RecallCount, Is.EqualTo(0));
            Assert.That(recall.Succeeded, Is.True);
            Assert.That(afterRecall.RecallCount, Is.EqualTo(1));
            Assert.That(afterRecall.LastRecalledWorldTime, Is.EqualTo(25));
            Assert.That(afterRecall.ReinforcementCount, Is.EqualTo(1));
            Assert.That(fixture.History.HistoryRevision, Is.EqualTo(1));
        }

        [Test]
        public void SuppressionBlocksOrdinaryRecallUntilAllSourcesRemovedOrExpired()
        {
            using TestFixture fixture = CreateFixture();
            fixture.SeedEventAndMemory();

            fixture.Memory.AddSuppression(Suppression("tx.suppress.a", "suppression.a", 10, -1));
            fixture.Memory.AddSuppression(Suppression("tx.suppress.b", "suppression.b", 11, 30));

            MemoryRecallResult blocked = fixture.Recall("tx.recall.blocked", 12);
            fixture.Memory.RemoveSuppression("memory.seed", "suppression.a", "tx.remove.a", 20);
            MemoryRecallResult stillBlocked = fixture.Recall("tx.recall.still-blocked", 21);
            fixture.Memory.RemoveSuppression("memory.seed", "suppression.b", "tx.expire.b", 31, expireOnly: true);
            MemoryRecallResult recalled = fixture.Recall("tx.recall.after-suppression", 32);

            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Outcome, Is.EqualTo(MemoryRecallOutcome.BlockedBySuppression));
            Assert.That(stillBlocked.Succeeded, Is.False);
            Assert.That(recalled.Succeeded, Is.True);
            Assert.That(fixture.GetMemory("memory.seed").State, Is.EqualTo(MemoryState.Accessible));
        }

        [Test]
        public void SuppressionValidationAndRemovalRestoreUnderlyingState()
        {
            using TestFixture fixture = CreateFixture();
            fixture.SeedEventAndMemory();
            fixture.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.make.difficult",
                OwnerPersonId = fixture.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 9,
                AlterationType = MemoryAlterationType.Reconstruction,
                ResultingState = MemoryState.Difficult
            });

            HistoryOperationResult equalRange = fixture.Memory.AddSuppression(Suppression("tx.suppress.equal", "suppression.equal", 10, 10));
            HistoryOperationResult permanent = fixture.Memory.AddSuppression(Suppression("tx.suppress.permanent", "suppression.permanent", 10, -1));
            HistoryOperationResult bounded = fixture.Memory.AddSuppression(Suppression("tx.suppress.bounded", "suppression.bounded", 11, 20));
            HistoryOperationResult unknown = fixture.Memory.RemoveSuppression("memory.seed", "suppression.missing", "tx.remove.missing", 12);
            HistoryOperationResult permanentExpire = fixture.Memory.RemoveSuppression("memory.seed", "suppression.permanent", "tx.expire.permanent", 13, expireOnly: true);
            HistoryOperationResult boundedExpire = fixture.Memory.RemoveSuppression("memory.seed", "suppression.bounded", "tx.expire.bounded", 21, expireOnly: true);
            MemoryRecallResult stillSuppressed = fixture.Recall("tx.recall.after-one-expire", 22);
            fixture.Memory.RemoveSuppression("memory.seed", "suppression.permanent", "tx.remove.permanent", 23);
            HistoryMemoryRecord restored = fixture.GetMemory("memory.seed");

            Assert.That(equalRange.Succeeded, Is.False);
            Assert.That(unknown.Succeeded, Is.False);
            Assert.That(permanentExpire.Succeeded, Is.False);
            Assert.That(permanent.Succeeded, Is.True);
            Assert.That(bounded.Succeeded, Is.True);
            Assert.That(boundedExpire.Succeeded, Is.True);
            Assert.That(stillSuppressed.Succeeded, Is.False);
            Assert.That(stillSuppressed.Outcome, Is.EqualTo(MemoryRecallOutcome.BlockedBySuppression));
            Assert.That(restored.State, Is.EqualTo(MemoryState.Difficult));
        }

        [Test]
        public void PartialForgettingHidesDetailsButPreservesEventAndHistory()
        {
            using TestFixture fixture = CreateFixture();
            fixture.SeedEventAndMemory();
            long historyRevision = fixture.History.HistoryRevision;

            fixture.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.forget.participant",
                OwnerPersonId = fixture.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 30,
                AlterationType = MemoryAlterationType.DetailLoss,
                DetailIdsToForget = new[] { "detail.primary-person", "detail.time" },
                ResultingState = MemoryState.Altered
            });

            MemoryRecallResult recall = fixture.Recall("tx.recall.partial", 31);

            Assert.That(recall.Succeeded, Is.True);
            Assert.That(recall.Outcome, Is.EqualTo(MemoryRecallOutcome.PartiallyRecalled));
            Assert.That(recall.Entries.Single().UnavailableDetails.Select(detail => detail.detailId), Does.Contain("detail.primary-person"));
            Assert.That(fixture.History.HistoryRevision, Is.EqualTo(historyRevision));
            Assert.That(fixture.History.TryGetEvent("event.seed", out _), Is.True);
        }

        [Test]
        public void AlterationAndCorrectionPreserveRevisionHistory()
        {
            using TestFixture fixture = CreateFixture();
            fixture.SeedEventAndMemory();

            fixture.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.distort",
                OwnerPersonId = fixture.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 40,
                AlterationType = MemoryAlterationType.Distortion,
                ResultingState = MemoryState.Altered,
                DetailsToAddOrReplace = new[] { new MemoryDetailData { detailId = "detail.note", kind = MemoryDetailKind.Note, state = MemoryDetailState.Altered, value = "Wrong detail", confidence = 900 } }
            });
            fixture.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.correct",
                OwnerPersonId = fixture.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 41,
                AlterationType = MemoryAlterationType.Correction,
                ResultingState = MemoryState.Recovered,
                DetailsToAddOrReplace = new[] { new MemoryDetailData { detailId = "detail.note", kind = MemoryDetailKind.Note, state = MemoryDetailState.Recovered, value = "Corrected detail", confidence = 900 } }
            });

            HistoryMemoryRecord memory = fixture.GetMemory("memory.seed");

            Assert.That(memory.State, Is.EqualTo(MemoryState.Recovered));
            Assert.That(memory.Revisions.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(memory.RememberedDetails.Single(detail => detail.detailId == "detail.note").value, Is.EqualTo("Corrected detail"));
        }

        [Test]
        public void SnapshotsDetailsSuppressionsRevisionsAndRecallResultsAreImmutable()
        {
            using TestFixture fixture = CreateFixture();
            fixture.SeedEventAndMemory();
            fixture.Memory.AddSuppression(Suppression("tx.snapshot.suppress", "suppression.snapshot", 10, -1));
            PersonMemorySnapshot before = fixture.Memory.CreateSnapshot();
            HistoryMemoryRecord snapshotMemory = before.Memories.Single();
            string originalDetailValue = snapshotMemory.RememberedDetails.First().value;
            string originalSuppressionSource = snapshotMemory.Suppressions.Single().sourceId;
            int originalRevisionCount = snapshotMemory.Revisions.Count;

            snapshotMemory.Data.clarity = 0;
            snapshotMemory.RememberedDetails.First().value = "mutated";
            snapshotMemory.Suppressions.Single().sourceId = "mutated";
            snapshotMemory.Revisions.First().description = "mutated";

            HistoryMemoryRecord runtime = fixture.GetMemory("memory.seed");
            Assert.That(runtime.Clarity, Is.Not.EqualTo(0));
            Assert.That(runtime.RememberedDetails.First().value, Is.EqualTo(originalDetailValue));
            Assert.That(runtime.Suppressions.Single().sourceId, Is.EqualTo(originalSuppressionSource));
            Assert.That(runtime.Revisions.Count, Is.EqualTo(originalRevisionCount));

            PersonMemorySnapshot stableBefore = fixture.Memory.CreateSnapshot();
            int stableBeforeRevisionCount = stableBefore.Memories.Single().Revisions.Count;
            int stableBeforeUnavailableDetails = stableBefore.Memories.Single().RememberedDetails.Count(detail => detail.state == MemoryDetailState.Unavailable);
            MemoryRecallResult recall = fixture.Memory.Recall(new MemoryRecallRequest
            {
                TransactionId = "tx.snapshot.recall",
                RequestingPersonId = fixture.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 12,
                AccessContext = MemoryAccessContext.Debug,
                MutateMetadata = false
            });
            int recallDetailCount = recall.Entries.Single().UnavailableDetails.Count;
            fixture.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.snapshot.alter",
                OwnerPersonId = fixture.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 13,
                AlterationType = MemoryAlterationType.DetailLoss,
                DetailIdsToForget = new[] { "detail.time" },
                ResultingState = MemoryState.Altered,
                ClarityDelta = -100
            });

            Assert.That(stableBefore.Memories.Single().Revisions.Count, Is.EqualTo(stableBeforeRevisionCount));
            Assert.That(stableBefore.Memories.Single().RememberedDetails.Count(detail => detail.state == MemoryDetailState.Unavailable), Is.EqualTo(stableBeforeUnavailableDetails));
            Assert.That(recall.Entries.Single().UnavailableDetails.Count, Is.EqualTo(recallDetailCount));
        }

        [Test]
        public void DegradationIsDeterministicIdempotentAndPersistsBoundary()
        {
            using TestFixture first = CreateFixture();
            using TestFixture second = CreateFixture();
            first.SeedEventAndMemory();
            second.SeedEventAndMemory();

            MemoryDegradationRequest requestA = Degradation("tx.degrade.a", "memory.seed", 6, 6 + 86400d);
            MemoryDegradationRequest requestB = Degradation("tx.degrade.b", "memory.seed", 6, 6 + 86400d);
            first.Memory.ApplyDegradation(requestA);
            second.Memory.ApplyDegradation(requestB);
            HistoryMemoryRecord firstAfter = first.GetMemory("memory.seed");
            HistoryMemoryRecord secondAfter = second.GetMemory("memory.seed");

            first.Memory.ApplyDegradation(Degradation("tx.degrade.repeat", "memory.seed", 6, 6 + 86400d));
            HistoryMemoryRecord afterRepeat = first.GetMemory("memory.seed");
            first.Memory.ApplyDegradation(Degradation("tx.degrade.advance", "memory.seed", 6, 6 + 172800d));
            HistoryMemoryRecord afterAdvance = first.GetMemory("memory.seed");
            PersonMemorySaveData save = first.Memory.CreateSaveData();
            first.Memory.RestoreFromSaveData(save, first.Registry, first.History, first.KnownPersons, restoring: true);
            HistoryMemoryRecord afterRestore = first.GetMemory("memory.seed");
            first.Memory.ApplyDegradation(Degradation("tx.degrade.restore-repeat", "memory.seed", 6, 6 + 172800d));
            HistoryMemoryRecord afterRestoreRepeat = first.GetMemory("memory.seed");

            Assert.That(firstAfter.Clarity, Is.EqualTo(secondAfter.Clarity));
            Assert.That(firstAfter.Confidence, Is.EqualTo(secondAfter.Confidence));
            Assert.That(afterRepeat.Clarity, Is.EqualTo(firstAfter.Clarity));
            Assert.That(afterAdvance.Clarity, Is.LessThan(afterRepeat.Clarity));
            Assert.That(afterRestoreRepeat.Clarity, Is.EqualTo(afterRestore.Clarity));
            Assert.That(afterRestoreRepeat.LastDegradationEvaluatedWorldTime, Is.EqualTo(afterRestore.LastDegradationEvaluatedWorldTime));
        }

        [Test]
        public void DegradationBatchOrderAndEqualTimeRecordsResolveDeterministically()
        {
            using TestFixture forward = CreateFixture();
            using TestFixture reverse = CreateFixture();
            forward.SeedEventAndMemory();
            reverse.SeedEventAndMemory();
            forward.Memory.FormMemory(Memory("tx.memory.second.forward", "memory.second", forward.PersonId, "event.seed", 7));
            reverse.Memory.FormMemory(Memory("tx.memory.second.reverse", "memory.second", reverse.PersonId, "event.seed", 7));

            forward.Memory.ApplyDegradation(Degradation("tx.forward.seed", "memory.seed", 6, 6 + 86400d));
            forward.Memory.ApplyDegradation(Degradation("tx.forward.second", "memory.second", 7, 7 + 86400d));
            reverse.Memory.ApplyDegradation(Degradation("tx.reverse.second", "memory.second", 7, 7 + 86400d));
            reverse.Memory.ApplyDegradation(Degradation("tx.reverse.seed", "memory.seed", 6, 6 + 86400d));

            forward.Memory.AddSuppression(Suppression("tx.equal.suppress.b", "suppression.b", 20, 40));
            forward.Memory.AddSuppression(Suppression("tx.equal.suppress.a", "suppression.a", 20, 40));
            forward.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.equal.revision.a",
                OwnerPersonId = forward.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 30,
                AlterationType = MemoryAlterationType.DetailAddition,
                DetailsToAddOrReplace = new[] { new MemoryDetailData { detailId = "detail.equal.a", kind = MemoryDetailKind.Note, state = MemoryDetailState.Remembered, value = "A" } }
            });
            forward.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.equal.revision.b",
                OwnerPersonId = forward.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 30,
                AlterationType = MemoryAlterationType.DetailAddition,
                DetailsToAddOrReplace = new[] { new MemoryDetailData { detailId = "detail.equal.b", kind = MemoryDetailKind.Note, state = MemoryDetailState.Remembered, value = "B" } }
            });

            Assert.That(forward.GetMemory("memory.seed").Clarity, Is.EqualTo(reverse.GetMemory("memory.seed").Clarity));
            Assert.That(forward.GetMemory("memory.second").Clarity, Is.EqualTo(reverse.GetMemory("memory.second").Clarity));
            Assert.That(forward.GetMemory("memory.seed").Suppressions.Select(suppression => suppression.suppressionId), Is.EqualTo(new[] { "suppression.a", "suppression.b" }));
            Assert.That(forward.GetMemory("memory.seed").Revisions.Select(revision => revision.revisionId), Is.EqualTo(forward.GetMemory("memory.seed").Revisions.Select((revision, index) => $"memory.seed.revision.{index}").ToArray()));
        }

        [Test]
        public void PreviousBodyAssociationCanBeSuppressedAndRecovered()
        {
            using TestFixture fixture = CreateFixture(extraBodies: new[] { "body.previous" });
            fixture.History.RecordBodyTransition("tx.body", "event.body", fixture.PersonId, "body.previous", fixture.BodyId, 5, 5, "replacement");
            fixture.Memory.FormMemory(Memory("tx.previous", "memory.previous", fixture.PersonId, "event.body", 8, "body.previous"));

            fixture.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.body.suppress",
                OwnerPersonId = fixture.PersonId,
                MemoryId = "memory.previous",
                WorldTime = 10,
                AlterationType = MemoryAlterationType.Suppression,
                DetailIdsToForget = new[] { "detail.body" },
                ResultingState = MemoryState.Altered
            });
            MemoryRecallResult hidden = fixture.Memory.Recall(new MemoryRecallRequest { TransactionId = "tx.body.recall.hidden", RequestingPersonId = fixture.PersonId, MemoryId = "memory.previous", WorldTime = 11, MutateMetadata = false });

            fixture.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.body.recover",
                OwnerPersonId = fixture.PersonId,
                MemoryId = "memory.previous",
                WorldTime = 12,
                AlterationType = MemoryAlterationType.Recovery,
                ResultingState = MemoryState.Recovered,
                DetailsToAddOrReplace = new[] { new MemoryDetailData { detailId = "detail.body", kind = MemoryDetailKind.Body, state = MemoryDetailState.Recovered, value = "body.previous", confidence = 900 } }
            });
            MemoryRecallResult recovered = fixture.Memory.Recall(new MemoryRecallRequest { TransactionId = "tx.body.recall.recovered", RequestingPersonId = fixture.PersonId, MemoryId = "memory.previous", WorldTime = 13, MutateMetadata = false });

            Assert.That(hidden.Entries.Single().UnavailableDetails.Select(detail => detail.detailId), Does.Contain("detail.body"));
            Assert.That(recovered.Entries.Single().RecalledDetails.Single(detail => detail.detailId == "detail.body").value, Is.EqualTo("body.previous"));
        }

        [Test]
        public void SaveRestoreRoundTripsFeature84StateWithoutEvents()
        {
            using TestFixture fixture = CreateFixture();
            fixture.SeedEventAndMemory();
            fixture.Memory.AddSuppression(Suppression("tx.save.suppress", "suppression.save", 15, -1));
            fixture.Memory.AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = "tx.save.alter",
                OwnerPersonId = fixture.PersonId,
                MemoryId = "memory.seed",
                WorldTime = 16,
                AlterationType = MemoryAlterationType.DetailLoss,
                DetailIdsToForget = new[] { "detail.time" },
                ResultingState = MemoryState.Altered
            });

            PersonMemorySaveData saveData = fixture.Memory.CreateSaveData();
            int events = 0;
            fixture.Memory.MemoryChanged += (_, __) => events++;
            HistoryOperationResult restore = fixture.Memory.RestoreFromSaveData(saveData, fixture.Registry, fixture.History, fixture.KnownPersons, restoring: true);
            HistoryMemoryRecord restored = fixture.GetMemory("memory.seed");

            Assert.That(restore.Succeeded, Is.True);
            Assert.That(events, Is.EqualTo(0));
            Assert.That(restored.Suppressions.Count, Is.EqualTo(1));
            Assert.That(restored.Revisions.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(restored.RememberedDetails.Single(detail => detail.detailId == "detail.time").state, Is.EqualTo(MemoryDetailState.Unavailable));
        }

        [Test]
        public void CorruptPrepareAndVersionOneMigrationFailureLeaveLiveRuntimeUnchanged()
        {
            using TestFixture fixture = CreateFixture();
            fixture.SeedEventAndMemory();
            PersonMemoryPersistenceParticipant participant = new PersonMemoryPersistenceParticipant(fixture.Memory, fixture.History, () => fixture.Registry, () => fixture.KnownPersons);
            PersonMemorySaveData before = fixture.Memory.CreateSaveData();
            string beforeJson = JsonUtility.ToJson(before);

            PersonMemorySaveData corruptV2 = fixture.Memory.CreateSaveData();
            corruptV2.memories[0].suppressions = new[] { new MemorySuppressionData { suppressionId = "bad", memoryId = "other.memory", sourceId = "source", startedAtWorldTime = 1 } };
            var corruptPrepare = participant.PreparePayload(JsonUtility.ToJson(corruptV2), PersonMemorySaveData.CurrentSchemaVersion);

            PersonMemorySaveData corruptV1 = fixture.Memory.CreateSaveData();
            corruptV1.schemaVersion = 1;
            corruptV1.memories[0].historicalEventId = "event.missing";
            var v1Prepare = participant.PreparePayload(JsonUtility.ToJson(corruptV1), 1);
            string afterJson = JsonUtility.ToJson(fixture.Memory.CreateSaveData());

            Assert.That(corruptPrepare.Succeeded, Is.False);
            Assert.That(v1Prepare.Succeeded, Is.False);
            Assert.That(afterJson, Is.EqualTo(beforeJson));
            Assert.That(fixture.Memory.QueryByEvent("event.seed").Select(memory => memory.MemoryId), Is.EqualTo(new[] { "memory.seed" }));
            Assert.That(fixture.GetMemory("memory.seed").Suppressions.Count, Is.EqualTo(0));
            Assert.That(fixture.GetMemory("memory.seed").Revisions.Count, Is.EqualTo(before.memories[0].revisions.Length));
        }

        private static TestFixture CreateFixture(string[] extraBodies = null)
        {
            DefinitionRegistry registry = Registry();
            string[] persons = { "person.memory.owner", "person.other" };
            string[] bodies = new[] { "body.memory.current" }.Concat(extraBodies ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            history.Configure(registry, "world.memory.test", persons, bodies);
            PersonMemoryRuntime memory = new PersonMemoryRuntime();
            memory.Configure("person.memory.owner", registry, history, persons);
            return new TestFixture(registry, history, memory, persons, bodies);
        }

        private static DefinitionRegistry Registry()
        {
            return new DefinitionRegistry(new IGameDefinition[]
            {
                EventDefinition("history-event.person-participation", HistoricalEventCategory.CustomWorldEvent, KnowledgeVisibility.Public, HistoricalEventPayloadKind.Generic),
                EventDefinition("history-event.body-transition", HistoricalEventCategory.BodyTransition, KnowledgeVisibility.Private, HistoricalEventPayloadKind.BodyTransition)
            });
        }

        private static RecordHistoricalEventRequest Event()
        {
            return new RecordHistoricalEventRequest
            {
                TransactionId = "tx.event.seed",
                EventId = "event.seed",
                EventDefinitionId = "history-event.person-participation",
                OccurredAtWorldTime = 5,
                RecordedAtWorldTime = 5,
                PrimaryPersonId = "person.memory.owner",
                ParticipantPersonIds = new[] { "person.memory.owner" },
                BodyIds = new[] { "body.memory.current" },
                LocationId = "place.memory.test",
                Visibility = KnowledgeVisibility.Private,
                SourceSystem = "EditModeTest",
                Provenance = "Test fixture",
                Payload = new HistoricalEventPayloadData { kind = HistoricalEventPayloadKind.Generic, note = "seed" },
                Tags = new[] { "memory-test" }
            };
        }

        private static FormMemoryRequest Memory(string transactionId, string memoryId, string ownerPersonId, string eventId, double formedAt, string bodyAtTime = "body.memory.current")
        {
            return new FormMemoryRequest
            {
                TransactionId = transactionId,
                MemoryId = memoryId,
                OwnerPersonId = ownerPersonId,
                HistoricalEventId = eventId,
                Source = HistoryMemorySource.DirectObservation,
                FormedAtWorldTime = formedAt,
                RememberedOccurredAtWorldTime = 5,
                Confidence = 750,
                Clarity = 700,
                Salience = 600,
                FirstHand = true,
                BodyAtTimeId = bodyAtTime,
                Visibility = KnowledgeVisibility.Private,
                Tags = new[] { "memory-test" }
            };
        }

        private static MemoryDegradationRequest Degradation(string transactionId, string memoryId, double from, double to)
        {
            return new MemoryDegradationRequest
            {
                TransactionId = transactionId,
                OwnerPersonId = "person.memory.owner",
                MemoryId = memoryId,
                FromWorldTime = from,
                ToWorldTime = to,
                ConfidenceLossPerDay = 10,
                ClarityLossPerDay = 20,
                SalienceLossPerDay = 5
            };
        }

        private MemorySuppressionRequest Suppression(string transactionId, string suppressionId, double start, double end)
        {
            return new MemorySuppressionRequest
            {
                TransactionId = transactionId,
                OwnerPersonId = "person.memory.owner",
                MemoryId = "memory.seed",
                SuppressionId = suppressionId,
                SourceId = $"source.{suppressionId}",
                StartedAtWorldTime = start,
                EndedAtWorldTime = end
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

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, AuthoritativeHistoryRuntime history, PersonMemoryRuntime memory, string[] knownPersons, string[] knownBodies)
            {
                Registry = registry;
                History = history;
                Memory = memory;
                KnownPersons = knownPersons;
                KnownBodies = knownBodies;
            }

            public string PersonId => "person.memory.owner";
            public string BodyId => "body.memory.current";
            public DefinitionRegistry Registry { get; }
            public AuthoritativeHistoryRuntime History { get; }
            public PersonMemoryRuntime Memory { get; }
            public string[] KnownPersons { get; }
            public string[] KnownBodies { get; }

            public void SeedEventAndMemory()
            {
                History.RecordEvent(Event());
                Memory.FormMemory(Memory("tx.memory.seed", "memory.seed", PersonId, "event.seed", 6));
            }

            public MemoryRecallResult Recall(string transactionId, double worldTime)
            {
                return Memory.Recall(new MemoryRecallRequest
                {
                    TransactionId = transactionId,
                    RequestingPersonId = PersonId,
                    MemoryId = "memory.seed",
                    WorldTime = worldTime,
                    AttemptDifficult = true
                });
            }

            public HistoryMemoryRecord GetMemory(string memoryId)
            {
                Assert.That(Memory.TryGetMemory(memoryId, out HistoryMemoryRecord memory), Is.True);
                return memory;
            }

            public void Dispose()
            {
            }
        }
    }
}
