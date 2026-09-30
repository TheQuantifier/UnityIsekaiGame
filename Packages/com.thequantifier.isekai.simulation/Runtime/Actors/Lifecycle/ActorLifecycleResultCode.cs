namespace UnityIsekaiGame.ActorLifecycle
{
    public static class ActorLifecycleResultCode
    {
        public const string Success = "Success";
        public const string Preview = "Preview";
        public const string DuplicateTransaction = "DuplicateTransaction";
        public const string InvalidRequest = "InvalidRequest";
        public const string MissingTarget = "MissingTarget";
        public const string StaleActor = "StaleActor";
        public const string MissingHealth = "MissingHealth";
        public const string InvalidState = "InvalidState";
        public const string IllegalTransition = "IllegalTransition";
        public const string PolicyRejected = "PolicyRejected";
        public const string CapabilityRejected = "CapabilityRejected";
        public const string RequirementRejected = "RequirementRejected";
        public const string DeathImmune = "DeathImmune";
        public const string RevivalWaitActive = "RevivalWaitActive";
        public const string ResourceRejected = "ResourceRejected";
        public const string RollbackFailed = "RollbackFailed";
        public const string RestoreInvalid = "RestoreInvalid";
        public const string NoChange = "NoChange";
    }
}
