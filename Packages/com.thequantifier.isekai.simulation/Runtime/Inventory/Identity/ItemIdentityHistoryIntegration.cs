using System;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;

namespace UnityIsekaiGame.Inventory.Identity
{
    public enum ItemIdentityHistoryEventKind
    {
        Created,
        OwnershipTransferred,
        Destroyed
    }

    public sealed class ItemIdentityHistoryResult
    {
        private ItemIdentityHistoryResult(bool succeeded, string status, string message, HistoryOperationResult historyResult)
        {
            Succeeded = succeeded;
            Status = status ?? string.Empty;
            Message = message ?? string.Empty;
            HistoryResult = historyResult;
        }

        public bool Succeeded { get; }
        public string Status { get; }
        public string Message { get; }
        public HistoryOperationResult HistoryResult { get; }

        public static ItemIdentityHistoryResult Success(HistoryOperationResult historyResult, string message)
        {
            return new ItemIdentityHistoryResult(true, "Succeeded", message, historyResult);
        }

        public static ItemIdentityHistoryResult Failure(string status, string message, HistoryOperationResult historyResult = null)
        {
            return new ItemIdentityHistoryResult(false, status, message, historyResult);
        }
    }

    public static class ItemIdentityHistoryIntegration
    {
        public static ItemIdentityHistoryResult RecordItemEvent(
            ItemInstanceIdentityRuntime runtime,
            AuthoritativeHistoryRuntime history,
            string transactionId,
            string eventDefinitionId,
            string eventId,
            string itemInstanceId,
            string primaryPersonId,
            ItemIdentityHistoryEventKind eventKind,
            double worldTime,
            bool preview = false)
        {
            if (runtime == null)
            {
                return ItemIdentityHistoryResult.Failure("MissingItemRuntime", "Item identity runtime is missing.");
            }

            if (history == null)
            {
                return ItemIdentityHistoryResult.Failure("MissingHistoryRuntime", "Authoritative history runtime is missing.");
            }

            if (!runtime.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot snapshot))
            {
                return ItemIdentityHistoryResult.Failure("MissingItem", $"Item instance '{itemInstanceId}' was not found.");
            }

            RecordHistoricalEventRequest request = new RecordHistoricalEventRequest
            {
                TransactionId = transactionId,
                EventId = eventId,
                EventDefinitionId = eventDefinitionId,
                OccurredAtWorldTime = worldTime,
                RecordedAtWorldTime = worldTime,
                PrimaryPersonId = primaryPersonId ?? string.Empty,
                ParticipantPersonIds = string.IsNullOrWhiteSpace(primaryPersonId) ? Array.Empty<string>() : new[] { primaryPersonId },
                Visibility = KnowledgeVisibility.Private,
                SourceSystem = "ItemIdentityRuntime",
                Provenance = $"item.identity.{eventKind}",
                Payload = new HistoricalEventPayloadData
                {
                    kind = HistoricalEventPayloadKind.Generic,
                    itemId = snapshot.ItemInstanceId,
                    claimValueId = snapshot.ItemDefinitionId,
                    qualitativeValue = eventKind.ToString(),
                    note = $"Item {eventKind}: {snapshot.ItemInstanceId}"
                }
            };

            HistoryOperationResult result = preview
                ? history.PreviewRecordEvent(request)
                : history.RecordEvent(request);
            return result.Succeeded
                ? ItemIdentityHistoryResult.Success(result, $"Item history event '{eventId}' recorded for '{itemInstanceId}'.")
                : ItemIdentityHistoryResult.Failure("HistoryRejected", result.Message, result);
        }

        public static ItemInstanceOperationResult TransferOwnershipWithRequiredHistory(
            ItemInstanceIdentityRuntime runtime,
            AuthoritativeHistoryRuntime history,
            string transactionId,
            string eventDefinitionId,
            string eventId,
            string itemInstanceId,
            string primaryPersonId,
            string newOwnerPersonId,
            double worldTime)
        {
            ItemIdentityHistoryResult preview = RecordItemEvent(runtime, history, $"{transactionId}.history.preview", eventDefinitionId, eventId, itemInstanceId, primaryPersonId, ItemIdentityHistoryEventKind.OwnershipTransferred, worldTime, preview: true);
            if (!preview.Succeeded)
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, preview.Message);
            }

            ItemInstanceOperationResult transfer = runtime.TransferOwnership(itemInstanceId, ItemOwnershipKind.PersonOwned, newOwnerPersonId);
            if (!transfer.Succeeded)
            {
                return transfer;
            }

            ItemIdentityHistoryResult commit = RecordItemEvent(runtime, history, transactionId, eventDefinitionId, eventId, itemInstanceId, primaryPersonId, ItemIdentityHistoryEventKind.OwnershipTransferred, worldTime);
            return commit.Succeeded
                ? transfer
                : ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, commit.Message);
        }
    }
}
