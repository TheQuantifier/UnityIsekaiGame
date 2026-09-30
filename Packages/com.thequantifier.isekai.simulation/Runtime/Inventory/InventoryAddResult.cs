namespace UnityIsekaiGame.Inventory
{
    public readonly struct InventoryAddResult
    {
        public InventoryAddResult(InventoryAddStatus status, int requestedQuantity, int addedQuantity)
        {
            Status = status;
            RequestedQuantity = requestedQuantity;
            AddedQuantity = addedQuantity;
        }

        public InventoryAddStatus Status { get; }
        public int RequestedQuantity { get; }
        public int AddedQuantity { get; }
        public int RemainingQuantity => RequestedQuantity - AddedQuantity;
        public bool AddedAll => Status == InventoryAddStatus.All;
        public bool AddedAny => AddedQuantity > 0;
    }
}
