using UnityEngine;

namespace UnityIsekaiGame.GameData
{
    [CreateAssetMenu(fileName = "Tag", menuName = "Unity Isekai Game/Game Data/Tag")]
    public sealed class TagDefinition : ScriptableObject, IGameDefinition
    {
        [SerializeField] private string tagId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDomain domain = CategoryDomain.General;
        [SerializeField] private int sortOrder;

        public string TagId => tagId;
        public string Id => tagId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDomain Domain => domain;
        public int SortOrder => sortOrder;
    }
}
