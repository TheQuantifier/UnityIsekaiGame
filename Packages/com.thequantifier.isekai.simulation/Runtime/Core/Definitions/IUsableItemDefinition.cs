namespace UnityIsekaiGame.GameData
{
    public interface IUsableItemDefinition
    {
        bool IsUsable { get; }
        int UseEffectCount { get; }
        bool HasMissingUseEffect { get; }
    }
}
