using System.Collections.Generic;

namespace UnityIsekaiGame.GameData
{
    public interface ITaggedDefinition
    {
        IReadOnlyList<TagDefinition> Tags { get; }
    }
}
