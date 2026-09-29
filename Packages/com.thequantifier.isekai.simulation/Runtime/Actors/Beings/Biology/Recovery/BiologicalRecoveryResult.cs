using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;

namespace UnityIsekaiGame.Beings.Biology.Recovery
{
    public sealed class BiologicalRecoveryResult
    {
        private BiologicalRecoveryResult(
            bool succeeded,
            bool preview,
            bool duplicate,
            BiologicalRecoveryResultCode code,
            string message,
            string transactionId,
            string actorBodyId,
            string recoveryMethodId,
            string processId,
            float previousProgress,
            float newProgress,
            float appliedProgress,
            RecoveryProcessState previousState,
            RecoveryProcessState newState,
            BiologicalInteractionEvaluationResult compatibility,
            StructuralRecoveryResult structuralRecovery,
            VitalResourceMutationResult vitalResourceMutation,
            BiologicalRecoverySnapshot snapshot)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            RecoveryMethodId = recoveryMethodId ?? string.Empty;
            ProcessId = processId ?? string.Empty;
            PreviousProgress = previousProgress;
            NewProgress = newProgress;
            AppliedProgress = appliedProgress;
            PreviousState = previousState;
            NewState = newState;
            Compatibility = compatibility;
            StructuralRecovery = structuralRecovery;
            VitalResourceMutation = vitalResourceMutation;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public BiologicalRecoveryResultCode Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public string ActorBodyId { get; }
        public string RecoveryMethodId { get; }
        public string ProcessId { get; }
        public float PreviousProgress { get; }
        public float NewProgress { get; }
        public float AppliedProgress { get; }
        public RecoveryProcessState PreviousState { get; }
        public RecoveryProcessState NewState { get; }
        public BiologicalInteractionEvaluationResult Compatibility { get; }
        public StructuralRecoveryResult StructuralRecovery { get; }
        public VitalResourceMutationResult VitalResourceMutation { get; }
        public BiologicalRecoverySnapshot Snapshot { get; }

        public static BiologicalRecoveryResult Success(string transactionId, string actorBodyId, string recoveryMethodId, string processId, float previousProgress, float newProgress, float appliedProgress, RecoveryProcessState previousState, RecoveryProcessState newState, BiologicalInteractionEvaluationResult compatibility, StructuralRecoveryResult structuralRecovery, VitalResourceMutationResult vitalResourceMutation, BiologicalRecoverySnapshot snapshot, bool preview = false, bool duplicate = false, string message = "")
        {
            return new BiologicalRecoveryResult(
                true,
                preview,
                duplicate,
                duplicate ? BiologicalRecoveryResultCode.Duplicate : preview ? BiologicalRecoveryResultCode.Preview : BiologicalRecoveryResultCode.Success,
                string.IsNullOrWhiteSpace(message) ? duplicate ? "Duplicate recovery transaction." : preview ? "Recovery preview resolved." : "Recovery operation committed." : message,
                transactionId,
                actorBodyId,
                recoveryMethodId,
                processId,
                previousProgress,
                newProgress,
                appliedProgress,
                previousState,
                newState,
                compatibility,
                structuralRecovery,
                vitalResourceMutation,
                snapshot);
        }

        public static BiologicalRecoveryResult Failure(BiologicalRecoveryResultCode code, string message, string transactionId = "", string actorBodyId = "", string recoveryMethodId = "", string processId = "", BiologicalInteractionEvaluationResult compatibility = null, BiologicalRecoverySnapshot snapshot = null)
        {
            return new BiologicalRecoveryResult(
                false,
                false,
                false,
                code,
                message,
                transactionId,
                actorBodyId,
                recoveryMethodId,
                processId,
                0f,
                0f,
                0f,
                RecoveryProcessState.Unknown,
                RecoveryProcessState.Unknown,
                compatibility,
                null,
                null,
                snapshot);
        }
    }
}
