using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory.Composition
{
    public enum ItemCompositionHistoryEventKind
    {
        CompositionCreated,
        CompositionCorrected,
        ComponentAttached,
        ComponentDetached
    }

    public sealed class ItemCompositionHistoryResult
    {
        private ItemCompositionHistoryResult(bool succeeded, string status, string message, HistoryOperationResult historyResult, ItemCompositionOperationResult compositionResult)
        {
            Succeeded = succeeded;
            Status = status ?? string.Empty;
            Message = message ?? string.Empty;
            HistoryResult = historyResult;
            CompositionResult = compositionResult;
        }

        public bool Succeeded { get; }
        public string Status { get; }
        public string Message { get; }
        public HistoryOperationResult HistoryResult { get; }
        public ItemCompositionOperationResult CompositionResult { get; }

        public static ItemCompositionHistoryResult Success(HistoryOperationResult historyResult, ItemCompositionOperationResult compositionResult, string message)
        {
            return new ItemCompositionHistoryResult(true, "Succeeded", message, historyResult, compositionResult);
        }

        public static ItemCompositionHistoryResult Failure(string status, string message, HistoryOperationResult historyResult = null, ItemCompositionOperationResult compositionResult = null)
        {
            return new ItemCompositionHistoryResult(false, status, message, historyResult, compositionResult);
        }
    }

    public static class ItemCompositionHistoryIntegration
    {
        public static ItemCompositionHistoryResult SetCompositionWithRequiredHistory(
            ItemCompositionRuntime compositionRuntime,
            ItemInstanceIdentityRuntime itemRuntime,
            DefinitionRegistry registry,
            AuthoritativeHistoryRuntime history,
            ItemCompositionRecordData composition,
            string transactionId,
            string eventDefinitionId,
            string eventId,
            string primaryPersonId,
            ItemCompositionHistoryEventKind eventKind,
            double worldTime)
        {
            if (compositionRuntime == null || itemRuntime == null)
            {
                return ItemCompositionHistoryResult.Failure("MissingRuntime", "Item composition and identity runtimes are required.");
            }

            if (history == null)
            {
                return ItemCompositionHistoryResult.Failure("MissingHistoryRuntime", "Authoritative history runtime is missing.");
            }

            ItemCompositionOperationResult previewComposition = compositionRuntime.SetComposition(itemRuntime, registry, composition, ItemCompositionMutationPurpose.RepairModification, preview: true);
            if (!previewComposition.Succeeded)
            {
                return ItemCompositionHistoryResult.Failure(previewComposition.Status.ToString(), previewComposition.Message, compositionResult: previewComposition);
            }

            HistoryOperationResult previewHistory = RecordCompositionEvent(history, $"{transactionId}.history.preview", eventDefinitionId, eventId, composition.itemInstanceId, composition.compositionId, primaryPersonId, eventKind, worldTime, preview: true);
            if (!previewHistory.Succeeded)
            {
                return ItemCompositionHistoryResult.Failure("HistoryRejected", previewHistory.Message, previewHistory, previewComposition);
            }

            ItemCompositionRuntimeSaveData original = compositionRuntime.CreateSaveData();
            ItemCompositionOperationResult set = compositionRuntime.SetComposition(itemRuntime, registry, composition, ItemCompositionMutationPurpose.RepairModification);
            if (!set.Succeeded)
            {
                return ItemCompositionHistoryResult.Failure(set.Status.ToString(), set.Message, compositionResult: set);
            }

            HistoryOperationResult commitHistory = RecordCompositionEvent(history, transactionId, eventDefinitionId, eventId, composition.itemInstanceId, composition.compositionId, primaryPersonId, eventKind, worldTime, preview: false);
            if (!commitHistory.Succeeded)
            {
                compositionRuntime.RestoreFromSaveData(original, registry, itemRuntime);
                return ItemCompositionHistoryResult.Failure("HistoryRejected", commitHistory.Message, commitHistory, set);
            }

            return ItemCompositionHistoryResult.Success(commitHistory, set, "Composition change and history event committed.");
        }

        public static HistoryOperationResult RecordCompositionEvent(
            AuthoritativeHistoryRuntime history,
            string transactionId,
            string eventDefinitionId,
            string eventId,
            string itemInstanceId,
            string compositionId,
            string primaryPersonId,
            ItemCompositionHistoryEventKind eventKind,
            double worldTime,
            bool preview = false)
        {
            RecordHistoricalEventRequest request = new RecordHistoricalEventRequest
            {
                TransactionId = transactionId,
                EventId = eventId,
                EventDefinitionId = eventDefinitionId,
                OccurredAtWorldTime = worldTime,
                RecordedAtWorldTime = worldTime,
                PrimaryPersonId = primaryPersonId ?? string.Empty,
                ParticipantPersonIds = string.IsNullOrWhiteSpace(primaryPersonId) ? System.Array.Empty<string>() : new[] { primaryPersonId },
                Visibility = KnowledgeVisibility.Private,
                SourceSystem = "ItemCompositionRuntime",
                Provenance = $"item.composition.{eventKind}",
                Payload = new HistoricalEventPayloadData
                {
                    kind = HistoricalEventPayloadKind.Generic,
                    itemId = itemInstanceId ?? string.Empty,
                    claimValueId = compositionId ?? string.Empty,
                    qualitativeValue = eventKind.ToString(),
                    note = $"Item composition {eventKind}: {itemInstanceId}/{compositionId}"
                }
            };

            return preview ? history.PreviewRecordEvent(request) : history.RecordEvent(request);
        }
    }
}
