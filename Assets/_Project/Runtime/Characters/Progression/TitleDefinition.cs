using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Progression
{
    [CreateAssetMenu(fileName = "TitleDefinition", menuName = "Unity Isekai Game/Progression/Title Definition")]
    public sealed class TitleDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition
    {
        [SerializeField] private string titleId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;

        public string TitleId => titleId;
        public string Id => titleId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Title;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
    }
}
