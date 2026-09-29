namespace UnityIsekaiGame.Equipment
{
    public readonly struct EquipmentOperationResult
    {
        public EquipmentOperationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public static EquipmentOperationResult Success(string message)
        {
            return new EquipmentOperationResult(true, message);
        }

        public static EquipmentOperationResult Failure(string message)
        {
            return new EquipmentOperationResult(false, message);
        }
    }
}
