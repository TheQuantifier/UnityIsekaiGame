namespace UnityIsekaiGame.Networking
{
    public enum CombatAuthorityCommandType : byte
    {
        None = 0,
        PrimaryAttack = 10,
        CastAbility = 20
    }

    public enum CombatAuthorityFailure : byte
    {
        None = 0,
        InvalidCommand = 10,
        ReplayedCommand = 20,
        InvalidAim = 30,
        ActionUnavailable = 40,
        CooldownActive = 50,
        InsufficientResource = 60,
        InvalidTarget = 70,
        DeferredTransaction = 80,
        ServerRejected = 90
    }

    public static class CombatAuthorityLimits
    {
        public const int MaximumActionIdBytes = 128;
        public const int MaximumEntityIdBytes = 128;
        public const int MaximumResultMessageBytes = 256;
        public const float MinimumAimMagnitude = 0.95f;
        public const float MaximumAimMagnitude = 1.05f;
        public const float MaximumAimYawDelta = 100f;
        public const int MaximumWorldCombatants = 1024;
    }
}
