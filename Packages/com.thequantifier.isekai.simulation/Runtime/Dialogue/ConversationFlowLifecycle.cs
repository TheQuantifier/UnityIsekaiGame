using System;
using System.Linq;

namespace UnityIsekaiGame.Dialogue
{
    /// <summary>
    /// Keeps conversation records and their presentation flows in the same lifecycle state.
    /// </summary>
    public static class ConversationFlowLifecycle
    {
        public static int InterruptOpenConversationsForParticipant(
            ConversationRuntime conversations,
            DialogueFlowRuntime dialogue,
            string personId,
            double worldTime,
            string transactionPrefix)
        {
            if (conversations == null || dialogue == null || string.IsNullOrWhiteSpace(personId)) return 0;

            ConversationProjection[] openConversations = conversations.Query(new ConversationQuery
            {
                access = ConversationAccessLevel.PrivilegedDiagnostic,
                requesterPersonId = personId,
                personId = personId,
                includeInactive = false
            }).ToArray();

            int interrupted = 0;
            foreach (ConversationProjection projection in openConversations)
            {
                ConversationSnapshot conversation = projection?.Snapshot;
                if (conversation == null) continue;

                foreach (DialogueFlowSnapshot flow in dialogue.Query(conversation.ConversationId))
                {
                    if (IsFlowTerminal(flow.State)) continue;
                    dialogue.TransitionLifecycle(new DialogueFlowLifecycleRequest
                    {
                        transactionId = TransactionId(transactionPrefix, "flow", flow.FlowId),
                        flowId = flow.FlowId,
                        targetState = DialogueFlowState.Ended,
                        worldTime = worldTime
                    });
                }

                ConversationOperationResult result = CloseConversation(
                    conversations,
                    conversation.ConversationId,
                    ConversationLifecycleState.Interrupted,
                    worldTime,
                    TransactionId(transactionPrefix, "conversation", conversation.ConversationId));
                if (result.Succeeded) interrupted++;
            }

            return interrupted;
        }

        public static ConversationOperationResult CloseConversationForFlow(
            ConversationRuntime conversations,
            DialogueFlowSnapshot flow,
            ConversationLifecycleState targetState,
            double worldTime,
            string transactionId)
        {
            if (flow == null)
            {
                long revision = conversations?.Revision ?? 0L;
                return ConversationOperationResult.Failure(ConversationOperationStatus.InvalidRequest, "Dialogue flow is missing.", revision);
            }

            return CloseConversation(conversations, flow.ConversationId, targetState, worldTime, transactionId);
        }

        public static ConversationOperationResult CloseConversation(
            ConversationRuntime conversations,
            string conversationId,
            ConversationLifecycleState targetState,
            double worldTime,
            string transactionId)
        {
            if (conversations == null)
                return ConversationOperationResult.Failure(ConversationOperationStatus.InvalidRequest, "Conversation runtime is missing.", 0L);
            if (!conversations.TryGetSnapshot(conversationId, out ConversationSnapshot conversation))
                return ConversationOperationResult.Failure(ConversationOperationStatus.InvalidRequest, $"Conversation '{conversationId}' is missing.", conversations.Revision);
            if (IsConversationTerminal(conversation.LifecycleState))
                return ConversationOperationResult.Success("Conversation was already closed.", conversations.Revision, conversations.Revision, conversation.ToSaveData());

            return conversations.TransitionLifecycle(new ConversationLifecycleRequest
            {
                transactionId = transactionId,
                conversationId = conversation.ConversationId,
                targetState = targetState,
                worldTime = worldTime,
                provenanceId = "prototype.scene.dialogue"
            });
        }

        public static bool IsFlowTerminal(DialogueFlowState state)
        {
            return state == DialogueFlowState.Ended || state == DialogueFlowState.Invalid;
        }

        public static bool IsConversationTerminal(ConversationLifecycleState state)
        {
            return state == ConversationLifecycleState.Completed
                || state == ConversationLifecycleState.Cancelled
                || state == ConversationLifecycleState.Interrupted
                || state == ConversationLifecycleState.Expired
                || state == ConversationLifecycleState.Historical
                || state == ConversationLifecycleState.Invalid;
        }

        private static string TransactionId(string prefix, string operation, string ownerId)
        {
            string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "tx.conversation.recovery" : prefix.Trim();
            string safeOwner = new string((ownerId ?? string.Empty)
                .Select(character => char.IsLetterOrDigit(character) || character == '.' || character == '-' ? character : '-')
                .ToArray())
                .Trim('-');
            return $"{safePrefix}.{operation}.{safeOwner}";
        }
    }
}
