namespace UnityIsekaiGame.Networking
{
    public enum InventoryAuthorityCommandType : byte
    {
        None = 0,
        UseSlot = 10,
        EquipSlot = 20,
        UnequipSlot = 30,
        DropQuantity = 40,
        MoveSlot = 50
    }

    public enum InventoryAuthorityFailure : byte
    {
        None = 0,
        InvalidCommand = 10,
        ReplayedCommand = 20,
        InvalidSlot = 30,
        InvalidQuantity = 40,
        InvalidEquipmentSlot = 50,
        NotOwned = 60,
        NotAllowed = 70,
        InventoryFull = 80,
        ItemUnavailable = 90,
        ServerRejected = 100
    }

    public static class InventoryAuthorityLimits
    {
        public const int MaximumSlots = 256;
        public const int MaximumQuantity = 1_000_000;
        public const int MaximumEquipmentSlots = 64;
        public const int MaximumDefinitionIdBytes = 128;
        public const int MaximumItemInstanceIdBytes = 128;
        public const int MaximumResultMessageBytes = 256;
    }
}
