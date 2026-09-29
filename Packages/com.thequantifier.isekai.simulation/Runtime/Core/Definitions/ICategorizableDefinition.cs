namespace UnityIsekaiGame.GameData
{
    public interface ICategorizableDefinition
    {
        CategoryDefinition PrimaryCategory { get; }
        CategoryDomain ClassificationDomain { get; }
    }
}
