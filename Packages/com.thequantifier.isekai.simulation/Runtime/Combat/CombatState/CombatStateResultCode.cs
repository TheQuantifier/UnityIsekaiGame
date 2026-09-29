namespace UnityIsekaiGame.Combat.CombatState
{
    public static class CombatStateResultCode
    {
        public const string Success = "Success";
        public const string Preview = "Preview";
        public const string InvalidRequest = "InvalidRequest";
        public const string MissingSource = "MissingSource";
        public const string MissingTarget = "MissingTarget";
        public const string StaleSource = "StaleSource";
        public const string StaleTarget = "StaleTarget";
        public const string SelfEngagementRejected = "SelfEngagementRejected";
        public const string NonHostile = "NonHostile";
        public const string DuplicateTransaction = "DuplicateTransaction";
        public const string TimeoutNotElapsed = "TimeoutNotElapsed";
        public const string ActiveEngagementPreventsExit = "ActiveEngagementPreventsExit";
        public const string InvalidEncounterId = "InvalidEncounterId";
        public const string ParticipantMissing = "ParticipantMissing";
        public const string ProcessingCapReached = "ProcessingCapReached";
        public const string EngagementMissing = "EngagementMissing";
        public const string SplitPreparationFailed = "SplitPreparationFailed";
        public const string IntegrityViolation = "IntegrityViolation";
    }
}
