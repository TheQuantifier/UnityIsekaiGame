namespace UnityIsekaiGame.Equipment
{
    public readonly struct EquipmentRestoreResult
    {
        private EquipmentRestoreResult(EquipmentRestoreStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public EquipmentRestoreStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == EquipmentRestoreStatus.Success;

        public static EquipmentRestoreResult Success()
        {
            return new EquipmentRestoreResult(EquipmentRestoreStatus.Success, "Equipment restored.");
        }

        public static EquipmentRestoreResult Failure(EquipmentRestoreStatus status, string message)
        {
            return new EquipmentRestoreResult(status, message);
        }
    }
}
