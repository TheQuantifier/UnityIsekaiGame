namespace UnityIsekaiGame.Beings.Biology.Condition
{
    public sealed class StructuralRecoveryResult
    {
        private StructuralRecoveryResult(
            bool succeeded,
            bool preview,
            bool duplicate,
            StructuralRecoveryResultCode code,
            string message,
            string transactionId,
            string actorBodyId,
            string targetNodeId,
            string targetInjuryId,
            string recoveryMethodId,
            int integrityRestored,
            int previousIntegrity,
            int newIntegrity,
            StructureFunctionalState functionalState,
            StructureDamageState structuralState,
            RuntimeStructurePresenceState runtimePresence,
            BodyConditionSnapshot snapshot)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            TargetNodeId = targetNodeId ?? string.Empty;
            TargetInjuryId = targetInjuryId ?? string.Empty;
            RecoveryMethodId = recoveryMethodId ?? string.Empty;
            IntegrityRestored = integrityRestored;
            PreviousIntegrity = previousIntegrity;
            NewIntegrity = newIntegrity;
            FunctionalState = functionalState;
            StructuralState = structuralState;
            RuntimePresence = runtimePresence;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public StructuralRecoveryResultCode Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public string ActorBodyId { get; }
        public string TargetNodeId { get; }
        public string TargetInjuryId { get; }
        public string RecoveryMethodId { get; }
        public int IntegrityRestored { get; }
        public int PreviousIntegrity { get; }
        public int NewIntegrity { get; }
        public StructureFunctionalState FunctionalState { get; }
        public StructureDamageState StructuralState { get; }
        public RuntimeStructurePresenceState RuntimePresence { get; }
        public BodyConditionSnapshot Snapshot { get; }

        public static StructuralRecoveryResult Success(
            StructuralRecoveryRequest request,
            int integrityRestored,
            int previousIntegrity,
            int newIntegrity,
            StructureFunctionalState functionalState,
            StructureDamageState structuralState,
            RuntimeStructurePresenceState runtimePresence,
            BodyConditionSnapshot snapshot,
            bool preview = false,
            bool duplicate = false)
        {
            return new StructuralRecoveryResult(
                true,
                preview,
                duplicate,
                duplicate ? StructuralRecoveryResultCode.Duplicate : preview ? StructuralRecoveryResultCode.Preview : StructuralRecoveryResultCode.Success,
                duplicate ? "Duplicate structural recovery transaction." : preview ? "Structural recovery preview resolved." : "Structural recovery applied.",
                request?.TransactionId,
                request?.TargetActorBodyId,
                request?.TargetNodeId,
                request?.TargetInjuryId,
                request?.RecoveryMethodId,
                integrityRestored,
                previousIntegrity,
                newIntegrity,
                functionalState,
                structuralState,
                runtimePresence,
                snapshot);
        }

        public static StructuralRecoveryResult Failure(StructuralRecoveryRequest request, StructuralRecoveryResultCode code, string message, BodyConditionSnapshot snapshot = null)
        {
            return new StructuralRecoveryResult(
                false,
                false,
                false,
                code,
                message,
                request?.TransactionId,
                request?.TargetActorBodyId,
                request?.TargetNodeId,
                request?.TargetInjuryId,
                request?.RecoveryMethodId,
                0,
                0,
                0,
                StructureFunctionalState.Unknown,
                StructureDamageState.Unknown,
                RuntimeStructurePresenceState.Unknown,
                snapshot);
        }
    }
}
