namespace UnityIsekaiGame.GameData
{
    public interface IInventoryItemDefinition : IObjectDefinition
    {
        bool Stackable { get; }
        int MaximumStackSize { get; }
    }
}
