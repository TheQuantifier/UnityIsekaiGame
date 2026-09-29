using UnityEngine;

namespace UnityIsekaiGame.GameData
{
    public interface IObjectDefinition : IGameDefinition, ICategorizableDefinition, ITaggedDefinition
    {
        string Description { get; }
        Sprite Icon { get; }
    }
}
