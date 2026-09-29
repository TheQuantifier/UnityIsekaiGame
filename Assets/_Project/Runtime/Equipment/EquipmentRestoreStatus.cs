namespace UnityIsekaiGame.Equipment
{
    public enum EquipmentRestoreStatus
    {
        Success,
        MissingSaveData,
        DuplicateSlot,
        MissingDefinitionId,
        MissingItemDefinition,
        WrongDefinitionType,
        WrongSlotType,
        InvalidItemInstance,
        DuplicateInstanceId,
        ExternalAuthority
    }
}
