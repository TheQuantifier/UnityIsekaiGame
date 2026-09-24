namespace UnityIsekaiGame.Combat
{
    public static class AttackResolutionResultCode
    {
        public const string Preview = "Preview";
        public const string Processed = "Processed";
        public const string DuplicateAttack = "DuplicateAttack";
        public const string InvalidRequest = "InvalidRequest";
        public const string AuthorityRequired = "AuthorityRequired";
        public const string MissingAttacker = "MissingAttacker";
        public const string MissingTarget = "MissingTarget";
        public const string StaleAttacker = "StaleAttacker";
        public const string StaleTarget = "StaleTarget";
        public const string AttackerNotReady = "AttackerNotReady";
        public const string TargetNotReady = "TargetNotReady";
        public const string MissingAccuracy = "MissingAccuracy";
        public const string MissingEvasion = "MissingEvasion";
        public const string UnknownDamageType = "UnknownDamageType";
        public const string RequirementFailed = "RequirementFailed";
        public const string OutOfRange = "OutOfRange";
        public const string InvalidRoll = "InvalidRoll";
        public const string DamageFailed = "DamageFailed";
    }
}
