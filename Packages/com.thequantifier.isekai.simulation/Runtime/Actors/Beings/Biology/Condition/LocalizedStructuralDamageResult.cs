namespace UnityIsekaiGame.Beings.Biology.Condition
{
    public sealed class LocalizedStructuralDamageResult
    {
        private LocalizedStructuralDamageResult(
            bool succeeded,
            bool preview,
            bool duplicate,
            LocalizedDamageResultCode code,
            string message,
            string transactionId,
            string actorBodyId,
            string targetNodeId,
            string injuryDefinitionId,
            string injuryId,
            int damageApplied,
            int previousIntegrity,
            int newIntegrity,
            InjurySeverity severity,
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
            InjuryDefinitionId = injuryDefinitionId ?? string.Empty;
            InjuryId = injuryId ?? string.Empty;
            DamageApplied = damageApplied;
            PreviousIntegrity = previousIntegrity;
            NewIntegrity = newIntegrity;
            Severity = severity;
            FunctionalState = functionalState;
            StructuralState = structuralState;
            RuntimePresence = runtimePresence;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public LocalizedDamageResultCode Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public string ActorBodyId { get; }
        public string TargetNodeId { get; }
        public string InjuryDefinitionId { get; }
        public string InjuryId { get; }
        public int DamageApplied { get; }
        public int PreviousIntegrity { get; }
        public int NewIntegrity { get; }
        public InjurySeverity Severity { get; }
        public StructureFunctionalState FunctionalState { get; }
        public StructureDamageState StructuralState { get; }
        public RuntimeStructurePresenceState RuntimePresence { get; }
        public BodyConditionSnapshot Snapshot { get; }

        public static LocalizedStructuralDamageResult Success(
            LocalizedStructuralDamageRequest request,
            string injuryId,
            int damageApplied,
            int previousIntegrity,
            int newIntegrity,
            InjurySeverity severity,
            StructureFunctionalState functionalState,
            StructureDamageState structuralState,
            RuntimeStructurePresenceState runtimePresence,
            BodyConditionSnapshot snapshot,
            bool preview = false,
            bool duplicate = false)
        {
            return new LocalizedStructuralDamageResult(
                true,
                preview,
                duplicate,
                duplicate ? LocalizedDamageResultCode.Duplicate : preview ? LocalizedDamageResultCode.Preview : LocalizedDamageResultCode.Success,
                duplicate ? "Duplicate localized structural damage transaction." : preview ? "Localized structural damage preview resolved." : "Localized structural damage applied.",
                request?.TransactionId,
                request?.TargetActorBodyId,
                request?.TargetNodeId,
                request?.InjuryDefinitionId,
                injuryId,
                damageApplied,
                previousIntegrity,
                newIntegrity,
                severity,
                functionalState,
                structuralState,
                runtimePresence,
                snapshot);
        }

        public static LocalizedStructuralDamageResult Failure(LocalizedStructuralDamageRequest request, LocalizedDamageResultCode code, string message, BodyConditionSnapshot snapshot = null)
        {
            return new LocalizedStructuralDamageResult(
                false,
                false,
                false,
                code,
                message,
                request?.TransactionId,
                request?.TargetActorBodyId,
                request?.TargetNodeId,
                request?.InjuryDefinitionId,
                string.Empty,
                0,
                0,
                0,
                InjurySeverity.Trivial,
                StructureFunctionalState.Unknown,
                StructureDamageState.Unknown,
                RuntimeStructurePresenceState.Unknown,
                snapshot);
        }
    }
}
