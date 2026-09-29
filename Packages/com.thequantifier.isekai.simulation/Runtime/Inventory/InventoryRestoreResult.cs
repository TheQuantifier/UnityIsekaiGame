namespace UnityIsekaiGame.Inventory
{
    public readonly struct InventoryRestoreResult
    {
        private InventoryRestoreResult(InventoryRestoreStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public InventoryRestoreStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == InventoryRestoreStatus.Success;

        public static InventoryRestoreResult Success()
        {
            return new InventoryRestoreResult(InventoryRestoreStatus.Success, "Inventory restored.");
        }

        public static InventoryRestoreResult Failure(InventoryRestoreStatus status, string message)
        {
            return new InventoryRestoreResult(status, message);
        }
    }
}
