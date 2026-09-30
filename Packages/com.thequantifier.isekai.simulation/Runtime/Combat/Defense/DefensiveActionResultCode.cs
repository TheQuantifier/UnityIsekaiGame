namespace UnityIsekaiGame.Combat.Defense
{
    public static class DefensiveActionResultCode
    {
        public const string Success = "Success";
        public const string Preview = "Preview";
        public const string InvalidRequest = "InvalidRequest";
        public const string MissingActor = "MissingActor";
        public const string MissingDefinition = "MissingDefinition";
        public const string MissingResource = "MissingResource";
        public const string MissingEquipment = "MissingEquipment";
        public const string StaleEquipment = "StaleEquipment";
        public const string UnequippedEquipment = "UnequippedEquipment";
        public const string IncompatibleEquipment = "IncompatibleEquipment";
        public const string StaleBody = "StaleBody";
        public const string InvalidRoll = "InvalidRoll";
        public const string ActorCannotAct = "ActorCannotAct";
        public const string Ineligible = "Ineligible";
        public const string Expired = "Expired";
        public const string InsufficientStamina = "InsufficientStamina";
        public const string DuplicateTransaction = "DuplicateTransaction";
        public const string NoActiveDefense = "NoActiveDefense";
    }
}
