namespace UnityIsekaiGame.Inventory
{
    public readonly struct InventoryInstanceOperationResult
    {
        public InventoryInstanceOperationResult(bool succeeded, string message, int slotIndex = -1)
        {
            Succeeded = succeeded;
            Message = message;
            SlotIndex = slotIndex;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public int SlotIndex { get; }

        public static InventoryInstanceOperationResult Success(string message, int slotIndex)
        {
            return new InventoryInstanceOperationResult(true, message, slotIndex);
        }

        public static InventoryInstanceOperationResult Failure(string message)
        {
            return new InventoryInstanceOperationResult(false, message);
        }
    }
}
