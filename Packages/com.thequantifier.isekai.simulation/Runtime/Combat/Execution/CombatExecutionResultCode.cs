namespace UnityIsekaiGame.Combat.Execution
{
    public static class CombatExecutionResultCode
    {
        public const string Success = "Success";
        public const string Preview = "Preview";
        public const string Duplicate = "Duplicate";
        public const string InvalidRequest = "InvalidRequest";
        public const string MissingDefinition = "MissingDefinition";
        public const string MissingActor = "MissingActor";
        public const string MissingBody = "MissingBody";
        public const string StaleActor = "StaleActor";
        public const string StaleBody = "StaleBody";
        public const string ActorCannotAct = "ActorCannotAct";
        public const string RequirementFailed = "RequirementFailed";
        public const string CommitmentConflict = "CommitmentConflict";
        public const string CooldownActive = "CooldownActive";
        public const string NoChargesAvailable = "NoChargesAvailable";
        public const string InsufficientResource = "InsufficientResource";
        public const string UnsupportedCostType = "UnsupportedCostType";
        public const string MissingResource = "MissingResource";
        public const string ExecutionTooEarly = "ExecutionTooEarly";
        public const string MissingExecution = "MissingExecution";
        public const string FailedCostCommit = "FailedCostCommit";
        public const string FailedCostRefund = "FailedCostRefund";
        public const string FailedUnderlyingAction = "FailedUnderlyingAction";
        public const string InvalidClock = "InvalidClock";
        public const string AuthorityRequired = "AuthorityRequired";
        public const string Interrupted = "Interrupted";
        public const string Cancelled = "Cancelled";
        public const string AlreadyComplete = "AlreadyComplete";
        public const string RestoreClearedTransientState = "RestoreClearedTransientState";
    }
}
