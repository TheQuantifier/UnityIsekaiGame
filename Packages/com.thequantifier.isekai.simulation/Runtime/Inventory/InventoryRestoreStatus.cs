namespace UnityIsekaiGame.Inventory
{
    public enum InventoryRestoreStatus
    {
        Success,
        MissingSaveData,
        MissingDefinitionId,
        MissingItemDefinition,
        WrongDefinitionType,
        InvalidQuantity,
        InvalidItemInstance,
        DuplicateInstanceId,
        ExternalAuthority
    }
}
