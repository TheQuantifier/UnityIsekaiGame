#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;

namespace UnityIsekaiGame.Development.Automation.Fixtures.History
{
    public sealed class HiddenHistoryFixtureHandle
    {
        public HiddenHistoryFixtureHandle(string eventId, string memoryId, string ownerPersonId, string sourceId)
        {
            EventId = eventId ?? string.Empty;
            MemoryId = memoryId ?? string.Empty;
            OwnerPersonId = ownerPersonId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
        }

        public string EventId { get; }
        public string MemoryId { get; }
        public string OwnerPersonId { get; }
        public string SourceId { get; }
    }

    public static class TestLabHistoryFixtureProviders
    {
        public const string WitnessMemoryFixtureId = "step8.history.witness-memory";

        public static void RegisterDefaults(TestLabScenarioContext context)
        {
            if (context == null)
            {
                return;
            }

            context.Fixtures.TryRegister(new TestLabFixtureProvider(
                WitnessMemoryFixtureId,
                new[] { TestLabScenarioContext.RuntimeBaselineFixtureId, TestLabScenarioContext.MutableStateScopeFixtureId },
                PrepareWitnessMemory), out _);
        }

        private static TestLabFixtureHandle PrepareWitnessMemory(TestLabScenarioContext context)
        {
            if (context?.Runtimes?.History == null || context.Runtimes.Memory == null || context.Runtimes.Knowledge == null)
            {
                return Failure("Step 8 witness memory fixture requires History, Memory, and Knowledge runtimes.");
            }

            string eventId = context.ScopedId("event", "hidden-secret");
            string memoryId = context.ScopedId("memory", "hidden-witness");
            string transactionId = context.ScopedId("history", "hidden-witness-fixture");
            string ownerPersonId = context.Runtimes.PersonId;
            double worldTime = 0d;

            if (!context.Runtimes.History.TryGetEvent(eventId, out HistoricalEventRecord eventRecord))
            {
                HistoryOperationResult eventResult = context.Runtimes.History.RecordEvent(new RecordHistoricalEventRequest
                {
                    TransactionId = $"{transactionId}.event",
                    EventId = eventId,
                    EventDefinitionId = "history-event.hidden-witnessed-event",
                    OccurredAtWorldTime = worldTime,
                    RecordedAtWorldTime = worldTime,
                    PrimaryPersonId = ownerPersonId,
                    ParticipantPersonIds = new[] { ownerPersonId },
                    BodyIds = context.Runtimes.KnownBodyIds.Take(1).ToArray(),
                    Visibility = KnowledgeVisibility.Hidden,
                    SourceSystem = "TestLabScenarioContext",
                    Provenance = "Scenario-owned fixture",
                    Payload = new HistoricalEventPayloadData
                    {
                        kind = HistoricalEventPayloadKind.Generic,
                        note = "Scenario-scoped hidden history fixture."
                    },
                    Tags = new[] { "feature.8.3", "fixture", "scenario-owned" }
                });

                if (!eventResult.Succeeded)
                {
                    return Failure($"Hidden event creation failed: {eventResult.Code} {eventResult.Message}");
                }

                eventRecord = eventResult.Event;
            }

            if (!context.Runtimes.Memory.TryGetMemory(memoryId, out HistoryMemoryRecord memoryRecord))
            {
                HistoryOperationResult memoryResult = context.Runtimes.Memory.FormMemory(new FormMemoryRequest
                {
                    TransactionId = $"{transactionId}.memory",
                    MemoryId = memoryId,
                    OwnerPersonId = ownerPersonId,
                    HistoricalEventId = eventId,
                    Source = HistoryMemorySource.DirectObservation,
                    FormedAtWorldTime = worldTime + 0.1d,
                    RememberedOccurredAtWorldTime = worldTime,
                    Confidence = 780,
                    Clarity = 720,
                    Salience = 650,
                    FirstHand = true,
                    Visibility = KnowledgeVisibility.Private,
                    BodyAtTimeId = context.Runtimes.KnownBodyIds.FirstOrDefault() ?? string.Empty,
                    DebugDescription = "Scenario-owned hidden witness memory.",
                    CreateKnowledgeEvidence = true,
                    Tags = new[] { "feature.8.3", "fixture", "memory" }
                }, context.Runtimes.Knowledge);

                if (!memoryResult.Succeeded)
                {
                    return Failure($"Witness memory creation failed: {memoryResult.Code} {memoryResult.Message}");
                }

                memoryRecord = memoryResult.Memory;
            }

            string eventSignature = $"definition={eventRecord.EventDefinitionId};person={ownerPersonId};visibility={eventRecord.Visibility}";
            TestLabFixtureHandle eventHandle = context.Ledger.EnsureEquivalent($"{WitnessMemoryFixtureId}.event", "historical-event", eventId, eventSignature, exists: true, actualSignature: eventSignature);
            if (!eventHandle.Succeeded)
            {
                return eventHandle;
            }

            string memorySignature = $"event={memoryRecord.HistoricalEventId};owner={memoryRecord.OwnerPersonId};source={memoryRecord.Source};state={memoryRecord.State}";
            TestLabFixtureHandle memoryHandle = context.Ledger.EnsureEquivalent(WitnessMemoryFixtureId, "memory", memoryId, memorySignature, exists: true, actualSignature: memorySignature);
            if (memoryHandle.Succeeded)
            {
                context.SetFixturePayload(WitnessMemoryFixtureId, new HiddenHistoryFixtureHandle(eventId, memoryId, ownerPersonId, "test-lab.history.fixture"));
            }

            return memoryHandle;
        }

        private static TestLabFixtureHandle Failure(string message)
        {
            return new TestLabFixtureHandle(WitnessMemoryFixtureId, "memory", WitnessMemoryFixtureId, string.Empty, TestLabFixtureEnsureOutcome.ValidationFailure, message);
        }
    }
}
#endif
