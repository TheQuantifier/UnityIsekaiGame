using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.History;

namespace UnityIsekaiGame.Knowledge.Integration
{
    public sealed class KnowledgeHistoryEventBridge
    {
        private readonly AuthoritativeHistoryRuntime history;
        private readonly Func<double> worldTimeProvider;
        private readonly Func<string> personIdProvider;
        private readonly Func<string> bodyIdProvider;
        private readonly Func<string> locationIdProvider;
        private string lastBodyId;

        public KnowledgeHistoryEventBridge(
            AuthoritativeHistoryRuntime historyRuntime,
            Func<double> authoritativeWorldTimeProvider,
            Func<string> currentPersonIdProvider,
            Func<string> currentBodyIdProvider,
            Func<string> currentLocationIdProvider)
        {
            history = historyRuntime ?? throw new ArgumentNullException(nameof(historyRuntime));
            worldTimeProvider = authoritativeWorldTimeProvider ?? (() => 0d);
            personIdProvider = currentPersonIdProvider ?? (() => string.Empty);
            bodyIdProvider = currentBodyIdProvider ?? (() => string.Empty);
            locationIdProvider = currentLocationIdProvider ?? (() => string.Empty);
            lastBodyId = bodyIdProvider();
        }

        public HistoryOperationResult EnsureCharacterCreation(string originId, string birthGiftId)
        {
            string personId = personIdProvider();
            string bodyId = bodyIdProvider();
            double time = WorldTime();
            return history.RecordBirthOrCreation(
                $"history.character-created.{personId}",
                $"history-event.character-created.{personId}",
                personId,
                bodyId,
                time,
                BuildMethodId(originId, birthGiftId));
        }

        public HistoryOperationResult RecordIdentityAssignment(string transactionSuffix, string originId, string birthGiftId)
        {
            string personId = personIdProvider();
            double time = WorldTime();
            return RecordLifeEvent(
                "history-event.life.identity",
                LifeEventCategory.Identity,
                LifeEventPayloadKind.Generic,
                $"identity.{transactionSuffix}",
                new[] { originId, birthGiftId },
                LifeEventSignificance.LifeDefining,
                LifeEventBiographyRelevance.IdentityDefining,
                "Character origin or birth gift assignment recorded.",
                personId,
                bodyIdProvider(),
                time);
        }

        public HistoryOperationResult RecordBodyTransition(string newBodyId, string reason)
        {
            string personId = personIdProvider();
            string normalizedNewBody = newBodyId ?? string.Empty;
            double time = WorldTime();
            string fromBody = lastBodyId ?? string.Empty;
            lastBodyId = normalizedNewBody;
            return history.RecordBodyTransition(
                $"history.body-transition.{personId}.{Stable(reason)}.{StableTime(time)}",
                $"history-event.body-transition.{personId}.{StableTime(time)}",
                personId,
                fromBody,
                normalizedNewBody,
                time,
                time,
                reason);
        }

        public HistoryOperationResult RecordDeath(string transactionId, string causeId)
        {
            string personId = personIdProvider();
            double time = WorldTime();
            return history.RecordDeathOrDisappearance(
                $"history.death.{transactionId}",
                $"history-event.death.{personId}.{Stable(transactionId)}",
                personId,
                bodyIdProvider(),
                time,
                presumed: false,
                causeId: causeId);
        }

        public HistoryOperationResult RecordRevival(string transactionId)
        {
            string personId = personIdProvider();
            double time = WorldTime();
            return RecordLifeEvent(
                "history-event.life.return-or-resurrection",
                LifeEventCategory.ReturnOrResurrection,
                LifeEventPayloadKind.Generic,
                $"revival.{transactionId}",
                Array.Empty<string>(),
                LifeEventSignificance.LifeDefining,
                LifeEventBiographyRelevance.MajorBiographyEvent,
                "Return or resurrection recorded.",
                personId,
                bodyIdProvider(),
                time);
        }

        public HistoryOperationResult RecordDiscovery(KnowledgeOperationResult knowledgeResult)
        {
            if (knowledgeResult?.Discovery == null || knowledgeResult.Evidence == null)
            {
                return null;
            }

            string personId = personIdProvider();
            double time = WorldTime();
            string subjectId = knowledgeResult.ResultingBelief?.Proposition.SubjectId ?? string.Empty;
            return history.RecordDiscoveryLifeEvent(
                $"history.discovery.{knowledgeResult.TransactionId}",
                $"history-event.discovery.{Stable(knowledgeResult.TransactionId)}",
                personId,
                subjectId,
                time,
                knowledgeResult.Evidence.EvidenceId,
                publicRecord: knowledgeResult.ResultingBelief?.Data.visibility == KnowledgeVisibility.Public);
        }

        public HistoryOperationResult RecordLocation(string placeId, bool entered)
        {
            string personId = personIdProvider();
            double time = WorldTime();
            string action = entered ? "entered" : "left";
            return RecordLifeEvent(
                "history-event.life.travel",
                LifeEventCategory.Travel,
                LifeEventPayloadKind.TravelOrMigration,
                $"location.{action}.{placeId}.{StableTime(time)}",
                new[] { placeId },
                LifeEventSignificance.Routine,
                LifeEventBiographyRelevance.Optional,
                $"Location {action}.",
                personId,
                bodyIdProvider(),
                time,
                placeId);
        }

        public IReadOnlyList<HistoryOperationResult> RecordQuestState(IEnumerable<string> questIds)
        {
            string personId = personIdProvider();
            double time = WorldTime();
            return (questIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => RecordLifeEvent(
                    "history-event.life.quest",
                    LifeEventCategory.QuestRelated,
                    LifeEventPayloadKind.Generic,
                    $"quest.{id}.{StableTime(time)}",
                    new[] { id },
                    LifeEventSignificance.Notable,
                    LifeEventBiographyRelevance.Optional,
                    "Quest state changed.",
                    personId,
                    bodyIdProvider(),
                    time,
                    relatedQuestId: id))
                .ToArray();
        }

        private HistoryOperationResult RecordLifeEvent(
            string definitionId,
            LifeEventCategory category,
            LifeEventPayloadKind payloadKind,
            string operationId,
            string[] relatedIds,
            LifeEventSignificance significance,
            LifeEventBiographyRelevance biography,
            string provenance,
            string personId,
            string bodyId,
            double time,
            string locationId = "",
            string relatedQuestId = "")
        {
            string stableOperation = Stable(operationId);
            return history.RecordLifeEvent(new RecordLifeEventRequest
            {
                TransactionId = $"history.{stableOperation}",
                EventId = $"history-event.{stableOperation}",
                EventDefinitionId = definitionId,
                Category = category,
                PayloadKind = payloadKind,
                OccurredAtWorldTime = time,
                RecordedAtWorldTime = time,
                PrimaryPersonId = personId,
                Participants = new[] { new LifeEventParticipantData { personId = personId, role = LifeEventParticipantRole.Subject, bodyId = bodyId } },
                BodyIds = string.IsNullOrWhiteSpace(bodyId) ? Array.Empty<string>() : new[] { bodyId },
                LocationId = string.IsNullOrWhiteSpace(locationId) ? locationIdProvider() : locationId,
                RelatedEntityIds = relatedIds ?? Array.Empty<string>(),
                Visibility = KnowledgeVisibility.Private,
                Significance = significance,
                BiographyRelevance = biography,
                PublicRecordRelevance = LifeEventPublicRecordRelevance.PersonalOnly,
                Outcome = LifeEventOutcome.Confirmed,
                RelatedQuestId = relatedQuestId,
                SourceSystem = "KnowledgeHistoryEventBridge",
                Provenance = provenance,
                HistoricalPayload = new HistoricalEventPayloadData { kind = HistoricalEventPayloadKind.Generic, note = provenance },
                LifeEventPayload = new LifeEventPayloadData { kind = payloadKind, subjectPersonId = personId },
                CorrelationId = operationId,
                Tags = new[] { "life-event", category.ToString() }
            });
        }

        private double WorldTime() => Math.Max(0d, worldTimeProvider());

        private static string BuildMethodId(string originId, string birthGiftId)
        {
            return string.Join("|", new[] { "character-generation", originId, birthGiftId }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string StableTime(double value) => Math.Floor(Math.Max(0d, value)).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

        private static string Stable(string value)
        {
            char[] characters = (value ?? string.Empty).ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray();
            return new string(characters).Trim('-');
        }
    }
}
